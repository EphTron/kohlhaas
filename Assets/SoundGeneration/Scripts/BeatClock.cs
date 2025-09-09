using UnityEngine;

public class BeatClock : MonoBehaviour
{
    [Range(60, 200)] public double bpm = 125;
    [Tooltip("Seconds to glide when BPM changes")] public double tempoGlide = 0.25;
    [Tooltip("Schedule ahead (s)")] public double lookahead = 0.1;

    double secPerBeat, targetSecPerBeat;
    double startDSP;

    public double Now => AudioSettings.dspTime;
    public double SecPerBeat => secPerBeat;

    void OnEnable()
    {
        startDSP = AudioSettings.dspTime + 0.1;
        targetSecPerBeat = secPerBeat = 60.0 / bpm;
    }

    void Update()
    {
        targetSecPerBeat = 60.0 / bpm;
        // one-pole glide
        double a = 1.0 - System.Math.Exp(-Time.unscaledDeltaTime / System.Math.Max(0.0001, tempoGlide));
        secPerBeat += (targetSecPerBeat - secPerBeat) * a;
    }

    public double BeatsToSeconds(double beats) => beats * secPerBeat;

    // Align the next event to the grid (offset + k*interval) strictly in the future
    public double AlignToGrid(double offsetBeats, double intervalBeats)
    {
        double elapsed = Now - startDSP;
        double beatsElapsed = elapsed / secPerBeat;
        double k = System.Math.Ceiling((beatsElapsed - offsetBeats) / System.Math.Max(0.0001, intervalBeats));
        double nextBeat = offsetBeats + System.Math.Max(0, k) * intervalBeats;
        return startDSP + nextBeat * secPerBeat;
    }
}
