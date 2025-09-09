using UnityEngine;

public class BasslineSequencer : MonoBehaviour
{
    public BeatClock clock;           // from earlier
    public MonoBassSynth synth;
    [Range(60, 200)] public float bpm = 150f;

    [Tooltip("Root MIDI note (C3=48)")]
    public int rootMidi = 48; // C3
    [Tooltip("8-bar pattern of semitone offsets (empty=rest)")]
    public string pattern =
        "f#3 a#3 c#3 d#3 a3 c#3 d3 f#2 c#3 d3 d#3 d2 d3"; // compact human ref (not parsed)

    // Concrete 16th-note grid over 8 bars (128 steps). -999 = rest.
    // Example derived from screenshot line; tweak freely.
    public int[] midiSteps = new int[]
    {
        54, -999, 49, -999,  49, 52, -999, 57,   56, -999, 49, -999, 51, -999, 50, -999,
        56, -999, 57, -999,  42, -999, 49, -999, 49, -999, 51, -999, 38, -999, 50, -999,
        54, -999, 49, -999,  49, 52, -999, 57,   56, -999, 49, -999, 51, -999, 50, -999,
        38, -999, 49, -999,  54, -999, 49, -999, 49, -999, 51, -999, 38, -999, 50, -999,

        // duplicate to fill 8 bars (128 steps total)
        54, -999, 49, -999,  49, 52, -999, 57,   56, -999, 49, -999, 51, -999, 50, -999,
        56, -999, 57, -999,  42, -999, 49, -999, 49, -999, 51, -999, 38, -999, 50, -999,
        54, -999, 49, -999,  49, 52, -999, 57,   56, -999, 49, -999, 51, -999, 50, -999,
        38, -999, 49, -999,  54, -999, 49, -999, 49, -999, 51, -999, 38, -999, 50, -999
    };

    [Header("Structure & PG")]
    public bool[] struct16 = new bool[16] {  // "{x*16}"
        true,true,true,true,true,true,true,true,true,true,true,true,true,true,true,true
    };
    [Range(0, 1)] public float probability = 1f; // PG (e.g., 0.8)

    [Header("LPF Automation (slider 350..2000 Hz)")]
    public AnimationCurve lpfOver8Bars =
        AnimationCurve.EaseInOut(0, 350, 8f, 2000);

    double nextStepDSP;
    int step; // 0..127
    double secPerBeat;

    void OnEnable()
    {
        secPerBeat = 60.0 / bpm;
        nextStepDSP = AudioSettings.dspTime + 0.1;
        step = 0;
    }

    void Update()
    {
        secPerBeat = 60.0 / bpm; // live BPM edits
        double stepDur = 0.25 * secPerBeat; // 16ths

        while (nextStepDSP - AudioSettings.dspTime <= 0.05)
        {
            int gateIndex = step % 16;
            bool gate = struct16[gateIndex] && (Random.value <= probability);

            // LPF automation across 8 bars
            float bars = (step / 16f) / 1f; // 16 steps per bar
            synth.lpfCutoff = lpfOver8Bars.Evaluate(bars);
            // Note on
            int midi = midiSteps[step];
            if (gate && midi >= 0)
            {
                synth.NoteOn(midi, 1f, 0.5f, (float)secPerBeat); // sustain 0.5 beats
            }
            else
            {
                synth.NoteOff();
            }

            step = (step + 1) % 128; // slow(8): 8 bars x 16 steps
            nextStepDSP += stepDur;
        }
    }
}
