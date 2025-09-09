using UnityEngine;

// Attach to a GameObject with an AudioSource. Do NOT add another AudioListener.
[RequireComponent(typeof(AudioSource))]
public class VoxChop : MonoBehaviour
{
    [Header("Source")]
    private AudioClip clip;               // or set useMic = true
   

    [Header("Chop / Slice")]
    
    [Range(1, 64)] public int slices = 8;      // .slice(8)
    [Range(1, 64)] public int chop = 32;       // .chop(32)
    [Range(1, 16)] public int loopAt = 4;      // .loopAt(4)
    [Range(0f, 1f)] public float cutProb = 0.0f; // .cut(1) -> set to 1 for stutters

    [Header("Tone / Gain / Room")]
    public float semitone = 0f;                // .note("g#1") style pitch shift (approx)
    [Range(0f, 3f)] public float gain = 1f;    // .gain(...)
    [Range(0f, 1f)] public float room = 0.2f;  // .room(2) rough send

    [Header("LPF")]
    [Range(60, 20000)] public float lpfHz = 600f; // .lpf(slider(...))
    [Range(0.1f, 1.4f)] public float lpfQ = 0.707f;

    [Header("Tempo")]
    public float bpm = 120f;                   // .slow(2) -> halve bpm in UI or code
    public int stepsPerBar = 8;

    AudioSource src;
    float[] buf;
    int channels;
    int sliceSize; 
    int[] cutSampleOrder;
    [SerializeField] int[] customOrder;
    Biquad lpf; float sr; int hop; int step, framesLeftInSlice;
    float pitchRatio = 1f; float reverbSmear;
    // fields
    private System.Random rng;

    
    void Awake()
    {
        src = GetComponent<AudioSource>();
        clip = src.clip;
        src.playOnAwake = false; src.loop = true; src.spatialBlend = 0f;

        sr = AudioSettings.outputSampleRate;
        lpf = new Biquad(); lpf.SetLowPass(sr, lpfHz, lpfQ);
        reverbSmear = Mathf.Clamp01(room) * 0.25f; // tiny cheap "room" smear
                                                   // Awake()
        rng = new System.Random();
    }

    void OnEnable()
    {
        //if (useMic)
        //{
        //    if (Microphone.devices.Length == 0) return;
        //    micDevice = micDevice ?? Microphone.devices[0];
        //    clip = Microphone.Start(micDevice, true, 10, (int)sr);
        //    while (Microphone.GetPosition(micDevice) <= 0) { }
        //}

        if (clip == null) return;
        channels = clip.channels;
        buf = new float[clip.samples * channels];
        clip.GetData(new float[clip.samples], 0); // warm-up fix on some platforms
        clip.GetData(buf, 0);

        BuildOrder();
        src.Play(); // we don’t hear src (we render in-place), but keeps lifecycle tidy
        UpdateDerived();
    }

    void BuildOrder()
    {
        // deterministic chop order with small loops
        //order = new int[chop];
        cutSampleOrder = new int[chop];
        if (customOrder != null && customOrder.Length > 0)
        {
            cutSampleOrder = customOrder;
        }
        else
        {
            // pick random slice
            for (int i = 0; i < chop; i++)
            {
                cutSampleOrder[i] = (i % loopAt) * (slices / loopAt);
                //cutSampleOrder[i] = rng.Next(0, slices);
            }
        }
        
        sliceSize = Mathf.Max(32, (clip.samples / slices));
        hop = Mathf.Max(1, (int)(sr * 60f / (bpm * stepsPerBar)));
        step = 0;
        framesLeftInSlice = sliceSize;
    }

    void Update()
    {
        // live param tweaks
        pitchRatio = Mathf.Pow(2f, semitone / 12f);
        lpf.SetLowPass(sr, lpfHz, lpfQ);
        BuildOrder();
        UpdateDerived();
    }

    void OnAudioFilterRead(float[] data, int ch)
    {
        if (buf == null) { System.Array.Clear(data, 0, data.Length); return; }

        int readIdxBase = (cutSampleOrder[step % cutSampleOrder.Length] * sliceSize * channels);
        int readIdx = readIdxBase;

        for (int i = 0; i < data.Length; i += ch)
        {
            // resequence on step boundary
            if (--framesLeftInSlice <= 0)
            {
                step++;
                bool cut = rng.NextDouble() < cutProb;
                if (step % (stepsPerBar) == 0 || cut)  // “stutter” or bar reset
                    step = 0;

                readIdxBase = (cutSampleOrder[step % cutSampleOrder.Length] * sliceSize * channels);
                readIdx = readIdxBase;
                framesLeftInSlice = hop;
            }

            // read with simple pitch (nearest-neighbour)
            int srcIndex = (int)(readIdx * pitchRatio);
            srcIndex = Mathf.Clamp(srcIndex, 0, buf.Length - channels);

            for (int c = 0; c < ch; c++)
            {
                float s = buf[srcIndex + c];

                // cheap room smear (one-tap feedback)
                s = s + reverbSmear * (c < ch ? data[Mathf.Max(0, i + c - ch)] : 0f);

                // LPF
                s = lpf.Process(s);

                // gain
                data[i + c] = s * gain;
            }

            readIdx += channels;
        }
    }

    void UpdateDerived()
    {
        // keep things valid if user edits in Inspector at runtime
        slices = Mathf.Max(1, slices);
        chop = Mathf.Max(1, chop);
        loopAt = Mathf.Clamp(loopAt, 1, chop);
    }

    // Simple biquad for LPF
    class Biquad
    {
        double b0, b1, b2, a1, a2, z1, z2;
        public void SetLowPass(float fs, float fc, float Q)
        {
            fc = Mathf.Clamp(fc, 20f, fs * 0.45f);
            double w0 = 2.0 * Mathf.PI * fc / fs;
            double cos = System.Math.Cos(w0), sin = System.Math.Sin(w0);
            double alpha = sin / (2.0 * Q);

            double b0n = (1 - cos) / 2.0;
            double b1n = 1 - cos;
            double b2n = (1 - cos) / 2.0;
            double a0 = 1 + alpha;
            double a1n = -2 * cos;
            double a2n = 1 - alpha;

            b0 = b0n / a0; b1 = b1n / a0; b2 = b2n / a0;
            a1 = a1n / a0; a2 = a2n / a0;
        }
        public float Process(float x)
        {
            double y = b0 * x + z1;
            z1 = b1 * x + z2 - a1 * y;
            z2 = b2 * x - a2 * y;
            return (float)y;
        }
    }
}
