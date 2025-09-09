
using UnityEngine;

public class EasyBasslineSequencer : MonoBehaviour
{
    public EasyMonoBassSynth synth;

    public event System.Action<int, double> OnNote; // (midi, dspTime)

    public int[] midiSteps = new int[] { 54, -1, 49, -1, 49, 52, -1, 57, 56, -1, 49, -1, 51, -1, 50, -1 }; // 1 bar (16ths)
    [Range(0, 1)] public float probability = 1f; // PG feel
    public float sustainBeats = 0.5f;           // sustain(0.5)
    public AnimationCurve lpfOverBars = AnimationCurve.EaseInOut(0, 350, 8, 2000); // slider(350..2000)

    double bpm = 128;
    double nextStepDSP;
    int step;
    double secPerBeat;

    void OnEnable()
    {
        if (GlobalBeatClock.instance == null)
        {
            secPerBeat = 60.0 / this.bpm;
        }
        else
        {
            secPerBeat = 60.0 / GlobalBeatClock.instance.bpm;
        }
        nextStepDSP = AudioSettings.dspTime + 0.1;
        step = 0;
    }

    void Update()
    {
        secPerBeat = 60.0 / GlobalBeatClock.instance.bpm;
        double stepDur = 0.25 * secPerBeat; // 16ths

        while (nextStepDSP - AudioSettings.dspTime <= 0.05)
        {
            float barPos = step / 16f; // 0..8 bars if you loop 8x
            synth.lpfCutoff = lpfOverBars.Evaluate(barPos);

            int midi = midiSteps[step % midiSteps.Length];
            bool gate = midi >= 0 && (Random.value <= probability);
            if (gate)
            {
                synth.NoteOn(midi, sustainBeats, (float)secPerBeat);
                OnNote?.Invoke(midi, nextStepDSP); // <- notify lights/FX
            }
            else
            {
                synth.NoteOff();
            }

            step = (step + 1) % (16 * 8); // pretend "slow(8)" by looping 8 bars
            nextStepDSP += stepDur;
        }
    }
}
