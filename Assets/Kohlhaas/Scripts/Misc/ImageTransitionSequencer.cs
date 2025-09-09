using UnityEngine;
using UnityEngine.UI;
using static System.Net.Mime.MediaTypeNames;

public class ImageTransitionSequencer : MonoBehaviour
{
    public Image targetImage;

    [Header("Timing (seconds)")]
    public float transitionIn = 1f;     // fade alpha -> 0
    public float countdown = 2f;        // wait while invisible
    public float transitionOut = 1f;    // fade alpha -> 1
    public float cooldown = 0.5f;       // wait after full cycle

    [Header("Colors")]
    public Color colorIn = Color.white; // applied during TransitionIn
    public Color colorOut = Color.red;  // applied during TransitionOut

    [Header("Options")]
    public bool loop = true;
    public bool playOnStart = true;

    enum Phase { Idle, In, Count, Out, Cooldown }
    Phase phase = Phase.Idle;
    float t; // phase timer

    void Reset()
    {
        targetImage = GetComponent<Image>();
    }

    void Start()
    {
        if (playOnStart) StartSequence();
    }

    public void StartSequence()
    {
        if (!targetImage) return;
        phase = Phase.In;
        t = 0f;
        // start from colorIn with current alpha=1
        var c = colorIn; c.a = 1f;
        targetImage.color = c;
        enabled = true;
    }

    void Update()
    {
        if (!targetImage || phase == Phase.Idle) return;
        t += Time.deltaTime;

        switch (phase)
        {
            case Phase.In:
                {
                    float a = transitionIn > 0f ? Mathf.Clamp01(1f - t / transitionIn) : 0f;
                    var c = colorIn; c.a = a;
                    targetImage.color = c;

                    if (t >= transitionIn)
                    {
                        // ensure alpha 0, move to countdown
                        c.a = 0f; targetImage.color = c;
                        phase = Phase.Count; t = 0f;
                    }
                }
                break;

            case Phase.Count:
                if (t >= countdown)
                {
                    // switch to out color, start fading in
                    phase = Phase.Out; t = 0f;
                    var c = colorOut; c.a = 0f; targetImage.color = c;
                }
                break;

            case Phase.Out:
                {
                    float a = transitionOut > 0f ? Mathf.Clamp01(t / transitionOut) : 1f;
                    var c = colorOut; c.a = a;
                    targetImage.color = c;

                    if (t >= transitionOut)
                    {
                        c.a = 1f; targetImage.color = c;
                        phase = Phase.Cooldown; t = 0f;
                    }
                }
                break;

            case Phase.Cooldown:
                if (t >= cooldown)
                {
                    if (loop)
                    {
                        // restart cycle
                        phase = Phase.In; t = 0f;
                        var c = colorIn; c.a = 1f; targetImage.color = c;
                    }
                    else
                    {
                        phase = Phase.Idle;
                        enabled = false;
                    }
                }
                break;
        }
    }
}
