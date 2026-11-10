using System;
using System.Threading;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public sealed class TechnoBassVoice : MonoBehaviour
{
    [Range(0, 0.5f)] public float gain = 0.18f;
    public float cutoff = 700;
    struct Note { public double time, end, frequency; }
    // Single producer (main thread), single consumer (audio thread).
    readonly Note[] notes = new Note[256];
    int read, write, sampleRate;
    double phase, frequency, gateEnd, envelope, lowpass, previousInput, highpass;
    float publishedCutoff, publishedGain;
    AudioClip carrier;

    void Awake()
    {
        sampleRate = AudioSettings.outputSampleRate;
        carrier = AudioClip.Create("Techno bass carrier", 64, 1, sampleRate, false);
        var source = GetComponent<AudioSource>();
        source.clip = carrier;
        source.loop = true;
        source.playOnAwake = false;
    }
    void OnEnable() { GetComponent<AudioSource>().Play(); }
    void Update()
    {
        Volatile.Write(ref publishedCutoff, Mathf.Clamp(cutoff, 40, 12000));
        Volatile.Write(ref publishedGain, gain);
    }

    public void Schedule(int midi, double time, double duration)
    {
        int next = (write + 1) % notes.Length;
        if (next == Volatile.Read(ref read)) return;
        notes[write] = new Note {
            time = time, end = time + duration,
            frequency = 440 * Math.Pow(2, (midi - 69) / 12.0)
        };
        Volatile.Write(ref write, next);
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        double time = AudioSettings.dspTime;
        double dt = 1.0 / sampleRate;
        double lpCoefficient = 1 - Math.Exp(-2 * Math.PI * Math.Max(40, Volatile.Read(ref publishedCutoff)) * dt);
        double hpCoefficient = Math.Exp(-2 * Math.PI * 30 * dt);
        double level = Volatile.Read(ref publishedGain);
        double attack = 1 - Math.Exp(-dt / 0.003);
        double release = 1 - Math.Exp(-dt / 0.055);
        for (int i = 0; i < data.Length; i += channels, time += dt)
        {
            while (read != Volatile.Read(ref write) && notes[read].time <= time)
            {
                Note note = notes[read];
                frequency = note.frequency;
                gateEnd = note.end;
                Volatile.Write(ref read, (read + 1) % notes.Length);
            }
            bool gate = time < gateEnd;
            envelope += ((gate ? 1 : 0) - envelope) * (gate ? attack : release);
            phase += frequency * dt;
            phase -= Math.Floor(phase);
            double oscillator = 0.65 * (2 * phase - 1) + 0.35 * Math.Sin(phase * 2 * Math.PI);
            lowpass += lpCoefficient * (oscillator - lowpass);
            highpass = hpCoefficient * (highpass + lowpass - previousInput);
            previousInput = lowpass;
            float output = (float)(highpass * envelope * level);
            for (int c = 0; c < channels; c++) data[i + c] = output;
        }
    }

    void OnDisable()
    {
        // Stop the callback before clearing its queue and oscillator state.
        GetComponent<AudioSource>().Stop();
        read = write = 0;
        envelope = lowpass = highpass = previousInput = gateEnd = 0;
    }

    void OnDestroy() { if (carrier) Destroy(carrier); }
}
