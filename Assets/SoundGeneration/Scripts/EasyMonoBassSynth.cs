using UnityEngine;

public class EasyMonoBassSynth : MonoBehaviour
{
    [Range(0, 1)] public float mixSquareSaw = 0.5f;
    [Range(0, 1)] public float gain = 0.75f;
    public float attack = 0.001f, decay = 0.075f, release = 0.08f;
    [Range(0, 1)] public float sustain = 0.5f;

    public float lpfCutoff = 350f, hpfCutoff = 150f;
    public int transposeSemitones = -12, coarse = 2;
    public float glide = 0.0f;

    float sr, phase, targetFreq, curFreq;
    enum Stage { Idle, Attack, Decay, Sustain, Release }
    Stage stage = Stage.Idle;
    float env;
    double noteOffDSP, dspCursor;
    float lp, hp, lpA, hpA;

    void Awake() { sr = AudioSettings.outputSampleRate; RecalcFilters(); }
    void OnValidate() { if (Application.isPlaying) RecalcFilters(); }
    void RecalcFilters()
    {
        lpA = Mathf.Exp(-2f * Mathf.PI * Mathf.Max(10f, lpfCutoff) / sr);
        hpA = Mathf.Exp(-2f * Mathf.PI * Mathf.Max(10f, hpfCutoff) / sr);
    }

    public void NoteOn(int midi, float sustainBeats, float secPerBeat)
    {
        float hz = MidiToHz(midi + transposeSemitones + coarse);
        targetFreq = hz;
        if (!(stage == Stage.Sustain && glide > 0)) curFreq = hz;
        stage = Stage.Attack; env = 0f;
        noteOffDSP = AudioSettings.dspTime + sustainBeats * secPerBeat;
    }
    public void NoteOff() { if (stage != Stage.Idle) stage = Stage.Release; }

    void OnAudioFilterRead(float[] data, int channels)
    {
        double srD = sr, invSR = 1.0 / srD;
        dspCursor = AudioSettings.dspTime;

        float atk = Mathf.Max(1e-5f, attack) * sr;
        float dec = Mathf.Max(1e-5f, decay) * sr;
        float rel = Mathf.Max(1e-5f, release) * sr;

        for (int i = 0; i < data.Length; i += channels)
        {
            dspCursor += invSR;
            if (stage != Stage.Release && dspCursor >= noteOffDSP) stage = Stage.Release;

            curFreq += (targetFreq - curFreq) * (glide <= 0 ? 1f : 1f - Mathf.Exp(-1f / (sr * glide)));

            phase += (float)(curFreq / srD);
            if (phase >= 1f) phase -= 1f;
            float sq = phase < 0.5f ? 1f : -1f;
            float saw = 2f * phase - 1f;
            float osc = Mathf.Lerp(sq, saw, mixSquareSaw);

            switch (stage)
            {
                case Stage.Attack: env += (1f - env) / atk; if (env >= 0.999f) { env = 1f; stage = Stage.Decay; } break;
                case Stage.Decay: env += (sustain - env) / dec; if (Mathf.Abs(env - sustain) < 1e-4f) stage = Stage.Sustain; break;
                case Stage.Sustain: env = sustain; break;
                case Stage.Release: env += (0f - env) / rel; if (env < 1e-4f) { env = 0; stage = Stage.Idle; } break;
                default: env = 0; break;
            }

            // smooth coefficient updates
            float lpfTargetA = Mathf.Exp(-2f * Mathf.PI * Mathf.Max(10f, lpfCutoff) / sr);
            lpA = Mathf.Lerp(lpA, lpfTargetA, 0.002f);
            float hpfTargetA = Mathf.Exp(-2f * Mathf.PI * Mathf.Max(10f, hpfCutoff) / sr);
            hpA = Mathf.Lerp(hpA, hpfTargetA, 0.002f);

            float x = osc * env * gain;
            hp = hpA * (hp + x) - x;              // 1-pole HPF
            lp = lp + (1f - lpA) * (hp - lp);     // 1-pole LPF
            float s = lp;

            for (int c = 0; c < channels; c++) data[i + c] = s;
        }
    }

    static float MidiToHz(int m) => 440f * Mathf.Pow(2f, (m - 69) / 12f);
}
