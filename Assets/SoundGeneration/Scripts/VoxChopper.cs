using UnityEngine;
using UnityEngine.Audio;

public class VoxChopper : MonoBehaviour
{
    public AudioSourcePool pool;          // assign in Inspector
    public AudioClip clip;

    [Header("Timing")]
    [Range(60, 200)] public double bpm = 128;
    public int barsStretch = 2;
    public int chopsPerBar = 32;

    [Header("Slicing")]
    public int slices = 8;
    public int[] sliceSequence = { 5, 6, 5, 6 };
    public int sliceRepeat = 4;

    [Header("Pitch & Level")]
    public int transposeSemis = 0;
    [Range(0f, 2f)] public float gain = 1f;

    [Header("Mixer Params (optional)")]
    public string lpfParam = "VoxLPF";     // exposed Lowpass cutoff on mixer (Hz)
    [Range(10f, 22000f)] public float lpfCutoff = 4000f;
    public string roomSendParam = "RoomSend"; // exposed reverb send (dB)
    public float roomSendDb = 0f;

    double nextDSP; int chopStep, seqIndex, repeatLeft;

    void OnEnable()
    {
        nextDSP = AudioSettings.dspTime + 0.1;
        chopStep = 0; seqIndex = 0; repeatLeft = sliceRepeat;
    }

    void Update()
    {
        if (!clip || pool == null) return;

        // drive mixer once per frame (group-wide)
        if (pool.outputGroup)
        {
            var mx = pool.outputGroup.audioMixer;
            if (!string.IsNullOrEmpty(lpfParam)) mx.SetFloat(lpfParam, lpfCutoff);
            if (!string.IsNullOrEmpty(roomSendParam)) mx.SetFloat(roomSendParam, roomSendDb);
        }

        double spb = 60.0 / bpm;
        double chopDur = (4.0 * spb / chopsPerBar) * barsStretch;

        while (nextDSP - AudioSettings.dspTime <= 0.05)
        {
            int idx = (sliceSequence.Length == 0) ? 0 : sliceSequence[Mathf.Clamp(seqIndex, 0, sliceSequence.Length - 1)];
            idx = Mathf.Clamp(idx, 0, Mathf.Max(0, slices - 1));

            ScheduleSliceGrain(idx, nextDSP, chopDur);

            nextDSP += chopDur;
            if (++chopStep >= chopsPerBar * barsStretch) chopStep = 0;

            if (--repeatLeft <= 0) { repeatLeft = sliceRepeat; seqIndex = (seqIndex + 1) % Mathf.Max(1, sliceSequence.Length); }
        }
    }

    void ScheduleSliceGrain(int sliceIndex, double startDSP, double grainDur)
    {
        var src = pool.Get();
        src.pitch = Mathf.Pow(2f, transposeSemis / 12f);
        src.volume = gain;

        int total = clip.samples;
        int sliceLen = total / Mathf.Max(1, slices);
        int sliceStart = sliceLen * sliceIndex;

        src.clip = clip;
        src.timeSamples = sliceStart;
        src.PlayScheduled(startDSP);
        src.SetScheduledEndTime(startDSP + grainDur + 0.0005);
    }
}
