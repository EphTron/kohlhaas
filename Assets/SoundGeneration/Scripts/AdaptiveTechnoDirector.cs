using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(TechnoTransport))]
public sealed class AdaptiveTechnoDirector : MonoBehaviour
{
    public Transform player;
    public AudioSource kickTemplate, hatTemplate, vocalTemplate;
    public AudioClip vocalClip;
    public TechnoBassVoice bass;
    [Tooltip("Order: kick wash, hat accent, bass accent, phrase beam, secondary hat.")]
    public Light[] lights;
    public Vector3 floorCentre = Vector3.zero;
    [Min(1)] public float pitchRadius = 8;
    [Range(0, 2)] public float pitchRangeSemitones = 0.5f;
    [Min(0.05f)] public float pitchSmoothing = 0.5f;

    static readonly int[][] BassPatterns = {
        new[] { 40, -1, 40, -1, -1, -1, 43, -1, 40, -1, -1, 47, -1, -1, 43, -1 },
        new[] { 40, -1, -1, 40, -1, -1, 43, -1, 40, -1, 47, -1, -1, 43, -1, -1 },
        new[] { 40, -1, 40, -1, 43, -1, 47, -1, 40, -1, -1, 50, -1, 47, 43, -1 },
        new[] { 40, -1, -1, -1, -1, -1, 43, -1, 40, -1, -1, -1, -1, -1, 47, -1 }
    };
    static readonly int[] HatMasks = { 0x5555, 0x4444, 0xD555, 0x4544 };
    static readonly string[] GrooveNames = { "Drive", "Offbeat", "Lift", "Space" };

    sealed class Voice
    {
        public AudioSource source;
        public double reservedUntil;
    }
    struct Cue { public double time; public bool kick, hat, bass; }
    readonly List<Voice> kickVoices = new List<Voice>();
    readonly List<Voice> hatVoices = new List<Voice>();
    readonly List<Voice> vocalVoices = new List<Voice>();
    readonly Queue<Cue> cues = new Queue<Cue>();
    TechnoTransport clock;
    Quaternion[] lightRotations;
    Color[] lightColors;
    float[] lightIntensities;
    float pitchSemitones, proximity;
    double lastKick = -100, lastHat = -100, lastBass = -100;
    int loggedPhrase = -1;

    void Awake()
    {
        clock = GetComponent<TechnoTransport>();
        int count = lights.Length;
        lightRotations = new Quaternion[count];
        lightColors = new Color[count];
        lightIntensities = new float[count];
        for (int i = 0; i < count; i++)
        {
            if (!lights[i]) continue;
            lightRotations[i] = lights[i].transform.rotation;
            lightColors[i] = lights[i].color;
            lightIntensities[i] = lights[i].intensity;
        }
        for (int i = 0; i < 8; i++)
        {
            kickVoices.Add(NewVoice(kickTemplate, "Kick voice"));
            hatVoices.Add(NewVoice(hatTemplate, "Hat voice"));
            vocalVoices.Add(NewVoice(vocalTemplate, "Vocal voice"));
        }
        UpdatePosition(true);
    }

    void OnEnable()
    {
        clock.ScheduleStep += Schedule;
        if (bass) bass.enabled = true;
    }

    void OnDisable()
    {
        clock.ScheduleStep -= Schedule;
        StopVoices(kickVoices);
        StopVoices(hatVoices);
        StopVoices(vocalVoices);
        if (bass) bass.enabled = false;
        cues.Clear();
        lastKick = lastHat = lastBass = -100;
        loggedPhrase = -1;
        for (int i = 0; i < lights.Length; i++)
        {
            if (!lights[i]) continue;
            lights[i].intensity = lightIntensities[i];
            lights[i].color = lightColors[i];
            lights[i].transform.rotation = lightRotations[i];
        }
    }

    void Update()
    {
        UpdatePosition(false);
        if (bass) bass.cutoff = Mathf.Lerp(400, 2200, proximity);
        double now = AudioSettings.dspTime;
        while (cues.Count > 0 && cues.Peek().time <= now)
        {
            Cue cue = cues.Dequeue();
            if (cue.kick) lastKick = cue.time;
            if (cue.hat) lastHat = cue.time;
            if (cue.bass) lastBass = cue.time;
        }
        if (clock.AudibleStep >= 0 && loggedPhrase != clock.Phrase)
        {
            loggedPhrase = clock.Phrase;
            Debug.Log($"Techno: phrase {loggedPhrase + 1}, {GrooveNames[loggedPhrase % 4]} (bar {clock.Bar + 1})", this);
        }
        UpdateLights(now);
    }

    void UpdatePosition(bool immediate)
    {
        Vector3 offset = player ? player.position - floorCentre : Vector3.zero;
        offset.y = 0;
        float targetProximity = 1 - Mathf.Clamp01(offset.magnitude / Mathf.Max(1, pitchRadius));
        float smoothing = immediate ? 1 : 1 - Mathf.Exp(-Time.unscaledDeltaTime / Mathf.Max(0.05f, pitchSmoothing));
        proximity = Mathf.Lerp(proximity, targetProximity, smoothing);
        pitchSemitones = Mathf.Lerp(-pitchRangeSemitones, pitchRangeSemitones, proximity);
    }

    void Schedule(long step, double time)
    {
        int sixteenth = (int)(step % 16);
        int bar = (int)(step / 16);
        int variation = (bar / 16) % 4;
        double stepDuration = clock.SecondsPerBeat / 4;
        float pitch = Mathf.Pow(2, pitchSemitones / 12);
        bool kick = sixteenth % 4 == 0;
        bool hat = (HatMasks[variation] & (1 << sixteenth)) != 0;
        // A short pickup in the last bar announces the next phrase.
        if (bar % 16 == 15 && sixteenth >= 12) hat = true;
        int note = BassPatterns[variation][sixteenth];
        if (kick) Play(kickVoices, kickTemplate, kickTemplate.clip, time, 1, 0.9f);
        if (hat) Play(hatVoices, hatTemplate, hatTemplate.clip, time, pitch,
            sixteenth % 4 == 2 ? 0.65f : 0.4f);
        if (note >= 0 && bass) bass.Schedule(note, time, stepDuration * 0.8);

        // Slice choice changes with the phrase; all onsets stay on the transport.
        int vocalInterval = variation == 2 ? 2 : variation == 3 ? 8 : 4;
        if (vocalClip && step % vocalInterval == 0)
        {
            int slice = ((step / 4 + variation) % 2 == 0) ? 5 : 6;
            Play(vocalVoices, vocalTemplate, vocalClip, time, pitch, 0.45f,
                vocalClip.samples / 8 * slice, stepDuration * 0.9);
        }
        cues.Enqueue(new Cue { time = time, kick = kick, hat = hat, bass = note >= 0 });
    }

    Voice NewVoice(AudioSource template, string label)
    {
        var go = new GameObject(label);
        go.transform.SetParent(transform, false);
        var source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0;
        source.dopplerLevel = 0;
        source.outputAudioMixerGroup = template ? template.outputAudioMixerGroup : null;
        return new Voice { source = source };
    }

    void Play(List<Voice> pool, AudioSource template, AudioClip clip, double time,
        float pitch, float volume, int startSample = 0, double duration = 0)
    {
        if (!clip) return;
        double now = AudioSettings.dspTime;
        Voice voice = pool.Find(v => v.reservedUntil <= now);
        if (voice == null)
        {
            voice = NewVoice(template, "Extra techno voice");
            pool.Add(voice);
        }
        var source = voice.source;
        source.clip = clip;
        source.pitch = pitch;
        source.volume = volume;
        source.timeSamples = Mathf.Clamp(startSample, 0, clip.samples - 1);
        double available = (clip.samples - startSample) / (double)clip.frequency / pitch;
        double length = duration > 0 ? Math.Min(duration, available) : available;
        voice.reservedUntil = time + length + 0.02;
        source.PlayScheduled(time);
        if (duration > 0) source.SetScheduledEndTime(time + length);
    }

    static void StopVoices(List<Voice> pool)
    {
        foreach (var voice in pool) { voice.source.Stop(); voice.reservedUntil = 0; }
    }

    void UpdateLights(double now)
    {
        float kick = (float)Math.Exp(-Math.Max(0, now - lastKick) / 0.13);
        float hat = (float)Math.Exp(-Math.Max(0, now - lastHat) / 0.07);
        float bassPulse = (float)Math.Exp(-Math.Max(0, now - lastBass) / 0.17);
        int variation = clock.Phrase % 4;
        float phraseBeat = (float)(clock.BeatPosition % 64);
        float phraseAccent = clock.AudibleStep < 0 ? 0 : Mathf.Exp(-phraseBeat / 0.7f);
        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            if (!light) continue;
            light.enabled = true;
            float pulse = i == 0 ? kick : i == 2 ? bassPulse : i == 3 ? phraseAccent : hat;
            light.intensity = lightIntensities[i] * (0.08f + 0.92f * pulse) * Mathf.Lerp(0.8f, 1.1f, proximity);
            Color.RGBToHSV(lightColors[i], out float hue, out float saturation, out float value);
            light.color = Color.HSVToRGB(Mathf.Repeat(hue + variation * 0.08f, 1), saturation, value);
            // Complete each sweep in four bars, with different phases per fixture.
            float angle = Mathf.Sin((float)(clock.BeatPosition * Math.PI * 2 / 16) + i * 1.25f)
                * Mathf.Lerp(12, 30, proximity);
            light.transform.rotation = lightRotations[i] * Quaternion.Euler(0, angle, angle * 0.35f);
        }
    }

    void OnGUI()
    {
        if (!clock) return;
        GUI.Box(new Rect(12, 12, 390, 106), "Techno position test — WASD move / R reset");
        GUI.Label(new Rect(24, 38, 365, 24),
            $"Bar {clock.Bar % 16 + 1}/16   Phrase {clock.Phrase + 1}: {GrooveNames[clock.Phrase % 4]}");
        GUI.Label(new Rect(24, 62, 365, 24), $"Percussion / vocal pitch: {pitchSemitones:+0.00;-0.00;0.00} semitones");
        GUI.Label(new Rect(24, 86, 365, 24), "Centre (0, 0): higher / brighter. Outer floor: lower.");
    }
}
