using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class OneShotLoop : MonoBehaviour
{
    public double intervalBeats = 1.0; // 1=every beat, 0.5=8ths, 2=every 2 beats
    public double offsetBeats = 0.0;
    public bool active = true;

    AudioSource src;
    AudioClip clip;
    double nextDSP;
    double lastInterval, lastOffset;

    void Awake() { src = GetComponent<AudioSource>(); clip = src.clip; }
    void OnEnable() { Resync(); }
    void OnValidate() { if (Application.isPlaying) Resync(); }

    void Update()
    {
        if (!active || clip == null ) return;

        if (lastInterval != intervalBeats || lastOffset != offsetBeats) Resync();

        var now = AudioSettings.dspTime;
        while (nextDSP - now <= GlobalBeatClock.instance.lookahead)
        {
            src.clip = clip;
            src.PlayScheduled(nextDSP);
            nextDSP += GlobalBeatClock.instance.BeatsToSeconds(intervalBeats);
        }
    }

    void Resync()
    {
        if (GlobalBeatClock.instance == null) return;
        nextDSP = GlobalBeatClock.instance.AlignToGrid(offsetBeats, intervalBeats);
        lastInterval = intervalBeats;
        lastOffset = offsetBeats;
    }
}
