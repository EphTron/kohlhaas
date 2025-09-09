using UnityEngine;

[RequireComponent(typeof(Light))]
public class ToggleLightController : MonoBehaviour
{
    public enum Mode { Toggle, Flash }
    public Mode mode = Mode.Flash;

    public int everyNBeats = 1;   // 1 = every beat, 2 = every 2nd, 4 = every bar (with 4/4)
    public int offsetBeats = 0;   // shift start
    public float holdSeconds = 0.08f; // flash length (used in Flash mode)

    GlobalBeatClock clock;
    Light lightSource;
    int beatCount;
    bool isBound;
    float offAt; // for Flash mode

    void Awake() { lightSource = GetComponent<Light>(); }
    private void Start()
    {
        TryBind();
    }

    void OnEnable()
    {
        TryBind();

    }
    void OnDisable()
    {
        if (isBound) clock.OnBeat -= HandleBeat;
    }

    void Update()
    {
        if (!isBound) { TryBind();}

        if (mode == Mode.Flash && lightSource.enabled && Time.time >= offAt)
            lightSource.enabled = false;
    }

    void TryBind()
    {
        if (!isBound && GlobalBeatClock.instance != null)
        {
            clock = GlobalBeatClock.instance;
            clock.OnBeat += HandleBeat;
            isBound = true;
        }
    }

    void HandleBeat(int bar, int beatInBar, double dspTime)
    {
        if (!isBound) { TryBind(); return; }

        beatCount++; // 1,2,3,...
        int n = Mathf.Max(1, everyNBeats);

        // only act on qualifying beats
        if (((beatCount - offsetBeats) % n) != 0) return;

        if (mode == Mode.Toggle)
        {
            lightSource.enabled = !lightSource.enabled;                 // flip once
        }
        else
        { // Flash
            lightSource.enabled = true;                       // on exactly at beat callback
            offAt = Time.time + Mathf.Max(0.01f, holdSeconds);
        }
    }
}
