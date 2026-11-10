using System;
using UnityEngine;

// Scene-local transport: audio is scheduled ahead; visuals read the same epoch.
[DefaultExecutionOrder(-100)]
public sealed class TechnoTransport : MonoBehaviour
{
    [Tooltip("Tempo is captured on Play/enable. Restart playback after editing.")]
    [Range(60, 200)] public float bpm = 128;
    [Range(0.05f, 0.3f)] public float lookahead = 0.15f;
    public event Action<long, double> ScheduleStep;

    public double StartDSP { get; private set; }
    public double SecondsPerBeat { get; private set; }
    public double BeatPosition => Math.Max(0, (AudioSettings.dspTime - StartDSP) / SecondsPerBeat);
    public long AudibleStep => AudioSettings.dspTime < StartDSP ? -1 : (long)Math.Floor(BeatPosition * 4);
    public int Bar => AudibleStep < 0 ? 0 : (int)(AudibleStep / 16);
    public int Phrase => Bar / 16;
    long nextStep;

    void OnEnable()
    {
        SecondsPerBeat = 60.0 / Math.Max(1, bpm);
        StartDSP = AudioSettings.dspTime + Math.Max(0.25, lookahead * 2);
        nextStep = 0;
    }

    void Update()
    {
        double now = AudioSettings.dspTime;
        double stepDuration = SecondsPerBeat / 4;
        // Skip missed notes after a stall instead of bursting through old events.
        if (StartDSP + nextStep * stepDuration < now + 0.01)
            nextStep = Math.Max(nextStep, (long)Math.Ceiling((now + 0.01 - StartDSP) / stepDuration));
        while (StartDSP + nextStep * stepDuration <= now + lookahead)
        {
            ScheduleStep?.Invoke(nextStep, StartDSP + nextStep * stepDuration);
            nextStep++;
        }
    }
}
