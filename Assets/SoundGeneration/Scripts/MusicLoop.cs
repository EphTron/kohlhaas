using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class MusicLoop : MonoBehaviour
{
    public BeatClock clock;
    public AudioClip clip;
    public double intervalBeats = 1.0;
    public double offsetBeats = 0.0;
    public bool active = true;

    AudioSource src;
    double nextDSP;
    double lastInterval, lastOffset;

    void Awake() { 
        src = GetComponent<AudioSource>(); 
        clip = src.clip;
    }

    void OnEnable()
    {
        if (clock == null || clip == null) { enabled = false; return; }
        Resync();
    }

    void Update()
    {
        if (!active || clip == null) return;

        // react live to inspector changes
        if (lastInterval != intervalBeats || lastOffset != offsetBeats)
            Resync();

        var now = AudioSettings.dspTime;
        while (nextDSP - now <= clock.lookahead)
        {
            src.clip = clip;
            src.PlayScheduled(nextDSP);
            nextDSP += clock.BeatsToSeconds(intervalBeats);   // follows BPM glide
        }
    }

    void Resync()
    {
        nextDSP = clock.AlignToGrid(offsetBeats, intervalBeats);
        lastInterval = intervalBeats;
        lastOffset = offsetBeats;
    }
}
