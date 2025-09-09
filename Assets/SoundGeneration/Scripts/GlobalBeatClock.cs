using System;
using UnityEngine;

public class GlobalBeatClock : MonoBehaviour
{
    public static GlobalBeatClock instance;

    [Range(60, 200)] public double bpm = 128;
    public int beatsPerBar = 4;
    [Tooltip("Seconds to glide when BPM changes")] public double tempoGlide = 0.15;
    [Tooltip("Schedule ahead (s)")] public double lookahead = 0.08;

    public event Action<int, int, double> OnBeat; // (barIndex, beatInBar, dspTime)

    double secPerBeat, targetSecPerBeat, nextBeatDSP;
    int bar, beat;

    private void Awake()
    {
        if (instance != null)
        {
            Destroy(this.gameObject);
        }
        else
        {
            instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
        Debug.Log("GlobalBeatClock instance created");
    }

    void OnEnable()
    {
        targetSecPerBeat = secPerBeat = 60.0 / bpm;
    }

    void Update()
    {
        // glide tempo
        targetSecPerBeat = 60.0 / bpm;
        double a = 1.0 - Math.Exp(-Time.unscaledDeltaTime / Math.Max(0.0001, tempoGlide));
        secPerBeat += (targetSecPerBeat - secPerBeat) * a;

        // emit beats exactly when crossed
        double now = AudioSettings.dspTime;
        while (now >= nextBeatDSP)
        {
            OnBeat?.Invoke(bar, beat, nextBeatDSP);
            beat++;
            if (beat >= beatsPerBar) { beat = 0; bar++; }
            nextBeatDSP += secPerBeat;
        }
    }

    public double SecPerBeat => secPerBeat;

    public double Now => AudioSettings.dspTime;
    public double BeatsToSeconds(double beats) => beats * secPerBeat;

    public double AlignToGrid(double offsetBeats, double intervalBeats)
    {
        double start = Now; // align from "now"
        double beatsElapsed = 0.0;
        double k = System.Math.Ceiling((beatsElapsed - offsetBeats) / System.Math.Max(0.0001, intervalBeats));
        double nextBeat = offsetBeats + System.Math.Max(0, k) * intervalBeats;
        return start + nextBeat * secPerBeat;
    }
}
