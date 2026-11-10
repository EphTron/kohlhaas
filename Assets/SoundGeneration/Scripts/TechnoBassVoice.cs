using System;
using System.Threading;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public sealed class TechnoBassVoice : MonoBehaviour
{
    [Range(0, 0.5f)] public float gain = 0.18f;
    public float cutoff = 700;
    [Header("Supersaw")]
    [Range(0, 1)] public float supersawMix = 0.85f;
    [Range(0, 40)] public float detuneCents = 18;
    [Range(0, 1)] public float stereoWidth = 0.7f;
    [Range(0, 1)] public float foundationMix = 0.35f;
    [Range(0, 1)] public float kickDucking = 0.65f;
    [Range(0, 4)] public float filterEnvelope = 1.8f;
    [Range(0, 0.25f)] public float darkRumbleGain = 0.08f;
    struct Note { public double time, end, frequency, foundation; public float velocity; public bool dark; }
    // Single producer (main thread), single consumer (audio thread).
    readonly Note[] notes = new Note[256];
    int read, write, sampleRate;
    static readonly double[] Spread = { -1, -0.63, -0.28, 0, 0.28, 0.63, 1 };
    readonly double[] sawPhases = { 0.03, 0.19, 0.37, 0.51, 0.63, 0.79, 0.93 };
    readonly double[] ratios = new double[7];
    readonly double[] darkRatios = new double[7];
    double phase, foundationPhase, frequency, foundationFrequency, gateEnd, noteStart, envelope, velocity = 1;
    double lowLeft, lowRight, lowLeft2, lowRight2, previousLeft, previousRight, highLeft, highRight;
    double beatStart, beatDuration = 60.0 / 128, smoothedLevel;
    double darkness, rumblePhase;
    bool darkVoice;
    float publishedCutoff, publishedGain;
    float publishedMix, publishedDetune, publishedWidth, publishedFoundation, publishedDucking, publishedFilterEnvelope;
    float publishedRumble;
    AudioClip carrier;

    void Awake()
    {
        sampleRate = AudioSettings.outputSampleRate;
        carrier = AudioClip.Create("Techno bass carrier", 64, 2, sampleRate, false);
        var source = GetComponent<AudioSource>();
        source.clip = carrier;
        source.loop = true;
        source.playOnAwake = false;
    }
    void OnEnable() { PublishParameters(); GetComponent<AudioSource>().Play(); }
    void Update()
    {
        PublishParameters();
    }

    void PublishParameters()
    {
        Volatile.Write(ref publishedCutoff, Mathf.Clamp(cutoff, 40, 12000));
        Volatile.Write(ref publishedGain, Mathf.Clamp(gain, 0, 0.5f));
        Volatile.Write(ref publishedMix, Mathf.Clamp01(supersawMix));
        Volatile.Write(ref publishedDetune, Mathf.Clamp(detuneCents, 0, 40));
        Volatile.Write(ref publishedWidth, Mathf.Clamp01(stereoWidth));
        Volatile.Write(ref publishedFoundation, Mathf.Clamp01(foundationMix));
        Volatile.Write(ref publishedDucking, Mathf.Clamp01(kickDucking));
        Volatile.Write(ref publishedFilterEnvelope, Mathf.Clamp(filterEnvelope, 0, 4));
        Volatile.Write(ref publishedRumble, Mathf.Clamp(darkRumbleGain, 0, 0.25f));
    }

    public void SetBeatGrid(double startDSP, double secondsPerBeat)
    {
        Volatile.Write(ref beatStart, startDSP);
        Volatile.Write(ref beatDuration, Math.Max(0.01, secondsPerBeat));
    }

    public void Schedule(int midi, double time, double duration, float strength = 1, int foundationMidi = -1, bool dark = false)
    {
        int next = (write + 1) % notes.Length;
        if (next == Volatile.Read(ref read)) return;
        notes[write] = new Note {
            time = time, end = time + duration,
            frequency = 440 * Math.Pow(2, (midi - 69) / 12.0),
            foundation = 440 * Math.Pow(2, ((foundationMidi < 0 ? midi : foundationMidi) - 69) / 12.0),
            velocity = Mathf.Clamp01(strength), dark = dark
        };
        Volatile.Write(ref write, next);
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        double time = AudioSettings.dspTime;
        double dt = 1.0 / sampleRate;
        double baseCutoff = Math.Max(40, Volatile.Read(ref publishedCutoff));
        double lpCoefficient = 0;
        double hpCoefficient = Math.Exp(-2 * Math.PI * 30 * dt);
        double level = Volatile.Read(ref publishedGain);
        double mix = Volatile.Read(ref publishedMix);
        double width = Volatile.Read(ref publishedWidth);
        double foundation = Volatile.Read(ref publishedFoundation);
        double duckAmount = Volatile.Read(ref publishedDucking);
        double filterAmount = Volatile.Read(ref publishedFilterEnvelope);
        double detune = Volatile.Read(ref publishedDetune);
        double rumbleGain = Volatile.Read(ref publishedRumble);
        double epoch = Volatile.Read(ref beatStart), secondsPerBeat = Volatile.Read(ref beatDuration);
        for (int voice = 0; voice < ratios.Length; voice++)
        {
            ratios[voice] = Math.Pow(2, Spread[voice] * detune / 1200);
            darkRatios[voice] = Math.Pow(2, Spread[voice] * detune * 0.35 / 1200);
        }
        double attack = 1 - Math.Exp(-dt / 0.003);
        double release = 1 - Math.Exp(-dt / 0.055);
        double levelSmoothing = 1 - Math.Exp(-dt / 0.01);
        double styleSmoothing = 1 - Math.Exp(-dt / 0.025);
        int frame = 0;
        for (int i = 0; i < data.Length; i += channels, time += dt)
        {
            bool newNote = false;
            while (read != Volatile.Read(ref write) && notes[read].time <= time)
            {
                Note note = notes[read];
                frequency = note.frequency;
                foundationFrequency = note.foundation;
                gateEnd = note.end;
                noteStart = note.time;
                velocity = note.velocity;
                darkVoice = note.dark;
                newNote = true;
                Volatile.Write(ref read, (read + 1) % notes.Length);
            }
            bool gate = time < gateEnd;
            darkness += ((darkVoice ? 1 : 0) - darkness) * styleSmoothing;
            envelope += ((gate ? 1 : 0) - envelope) * (gate ? attack : release);
            phase += frequency * dt;
            phase -= Math.Floor(phase);
            double dry = 0.65 * BandlimitedSaw(phase, frequency * dt) + 0.35 * Math.Sin(phase * 2 * Math.PI);
            double sawLeft = 0, sawRight = 0;
            for (int voice = 0; voice < sawPhases.Length; voice++)
            {
                double ratio = ratios[voice] + (darkRatios[voice] - ratios[voice]) * darkness;
                double increment = frequency * ratio * dt;
                sawPhases[voice] += increment;
                sawPhases[voice] -= Math.Floor(sawPhases[voice]);
                double saw = BandlimitedSaw(sawPhases[voice], increment);
                double pan = Spread[voice] * width * (1 - 0.75 * darkness);
                sawLeft += saw * (1 - pan);
                sawRight += saw * (1 + pan);
            }
            foundationPhase += foundationFrequency * dt;
            foundationPhase -= Math.Floor(foundationPhase);
            double bottom = Math.Sin(foundationPhase * Math.PI * 2);
            double toneMix = mix * (1 - 0.35 * darkness);
            double toneFoundation = foundation + (Math.Max(foundation, 0.55) - foundation) * darkness;
            double left = dry * (1 - toneMix) + (sawLeft / 7 * (1 - toneFoundation) + bottom * toneFoundation) * toneMix;
            double right = dry * (1 - toneMix) + (sawRight / 7 * (1 - toneFoundation) + bottom * toneFoundation) * toneMix;
            // Recalculate the note's filter sweep every 16 samples, and on onset.
            if ((frame++ & 15) == 0 || newNote)
            {
                double brightness = baseCutoff * (1 - 0.65 * darkness)
                    * (1 + filterAmount * (1 - 0.65 * darkness) * Math.Exp(-Math.Max(0, time - noteStart) / 0.12));
                lpCoefficient = 1 - Math.Exp(-2 * Math.PI * Math.Min(sampleRate * 0.4, brightness) * dt);
            }
            lowLeft += lpCoefficient * (left - lowLeft);
            lowRight += lpCoefficient * (right - lowRight);
            lowLeft2 += lpCoefficient * (lowLeft - lowLeft2);
            lowRight2 += lpCoefficient * (lowRight - lowRight2);
            highLeft = hpCoefficient * (highLeft + lowLeft2 - previousLeft);
            highRight = hpCoefficient * (highRight + lowRight2 - previousRight);
            previousLeft = lowLeft2;
            previousRight = lowRight2;
            double beat = Math.Max(0, (time - epoch) / secondsPerBeat);
            double recovery = Math.Min(1, (beat - Math.Floor(beat)) * secondsPerBeat / 0.13);
            double toneDucking = duckAmount + (Math.Max(duckAmount, 0.75) - duckAmount) * darkness;
            double duck = 1 - toneDucking * (1 - recovery);
            smoothedLevel += (level - smoothedLevel) * levelSmoothing;
            double amplitude = envelope * velocity * smoothedLevel * duck;
            double drivenLeft = highLeft, drivenRight = highRight;
            double rumble = 0;
            if (darkness > 0.0001)
            {
                drivenLeft += (Math.Tanh(highLeft * 2.8) / 1.6 - highLeft) * darkness;
                drivenRight += (Math.Tanh(highRight * 2.8) / 1.6 - highRight) * darkness;
                double age = (beat - Math.Floor(beat)) * secondsPerBeat;
                // A low, driven tail breathes between kicks on the same DSP grid.
                rumblePhase += (38 + 14 * Math.Exp(-age / 0.08)) * dt;
                rumblePhase -= Math.Floor(rumblePhase);
                double body = Math.Sin(rumblePhase * Math.PI * 2) + 0.2 * Math.Sin(rumblePhase * Math.PI * 4);
                rumble = Math.Tanh(body * 2) * Math.Exp(-age / 0.32) * duck
                    * smoothedLevel * rumbleGain * 4 * darkness;
            }
            float outputLeft = (float)(drivenLeft * amplitude + rumble);
            float outputRight = (float)(drivenRight * amplitude + rumble);
            if (channels == 1) data[i] = (outputLeft + outputRight) * 0.5f;
            else
                for (int c = 0; c < channels; c++) data[i + c] = c % 2 == 0 ? outputLeft : outputRight;
        }
    }

    // PolyBLEP smooths each saw's discontinuity to reduce aliasing on octave jumps.
    static double BandlimitedSaw(double position, double increment)
    {
        double correction = 0;
        if (position < increment)
        {
            double t = position / increment;
            correction = 2 * t - t * t - 1;
        }
        else if (position > 1 - increment)
        {
            double t = (position - 1) / increment;
            correction = t * t + 2 * t + 1;
        }
        return 2 * position - 1 - correction;
    }

    void OnDisable()
    {
        // Stop the callback before clearing its queue and oscillator state.
        GetComponent<AudioSource>().Stop();
        read = write = 0;
        envelope = gateEnd = smoothedLevel = 0;
        darkness = rumblePhase = 0;
        darkVoice = false;
        lowLeft = lowRight = lowLeft2 = lowRight2 = previousLeft = previousRight = highLeft = highRight = 0;
    }

    void OnDestroy() { if (carrier) Destroy(carrier); }
}
