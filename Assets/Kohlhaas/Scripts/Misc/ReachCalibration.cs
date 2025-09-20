// ReachCalibration.cs
using UnityEngine;


public sealed class ReachCalibration
{
    enum Side { Left, Right }

    // settings
    readonly float headHeight, startDist, expandStep, maxProbeDist, reachTolerance;

    // refs
    readonly Transform root;                  // boxer root
    readonly Transform leftTarget, rightTarget;
    readonly Transform leftTip, rightTip;

    // state
    bool running;
    Side side = Side.Left;
    Vector3 leftDir, rightDir;
    float goodDist, badDist;   // binary search bounds
    bool expanding;

    public float MaxLeft { get; private set; }
    public float MaxRight { get; private set; }
    public bool IsRunning => running;

    public ReachCalibration(
        Transform root, Transform leftTarget, Transform rightTarget,
        Transform leftTip, Transform rightTip,
        float headHeight = 1.6f, float startDist = 0.30f, float expandStep = 0.08f,
        float maxProbeDist = 2.0f, float reachTolerance = 0.015f)
    {
        this.root = root;
        this.leftTarget = leftTarget;
        this.rightTarget = rightTarget;
        this.leftTip = leftTip;
        this.rightTip = rightTip;

        this.headHeight = headHeight;
        this.startDist = startDist;
        this.expandStep = Mathf.Max(0.002f, expandStep);
        this.maxProbeDist = Mathf.Max(startDist, maxProbeDist);
        this.reachTolerance = Mathf.Max(0.001f, reachTolerance);
    }

    public void Begin()
    {
        if (running) return;
        running = true;
        side = Side.Left;
        SetupSideVectors();
        ResetPhase();
        SetTarget(side, startDist);
    }

    public void StopEarly()
    {
        running = false;
    }

    public void Tick()
    {
        if (!running) return;

        if (expanding)
        {
            float testDist = GetTargetDist(side);
            float err = GetErr(side);

            if (err <= reachTolerance)
            {
                goodDist = testDist;
                float next = testDist + expandStep;
                if (next > maxProbeDist)
                {
                    Commit(side, goodDist);
                    NextOrFinish();
                    return;
                }
                SetTarget(side, next);
            }
            else
            {
                badDist = testDist;
                expanding = false;
                SetTarget(side, 0.5f * (goodDist + badDist));
            }
            return;
        }

        // binary search
        {
            float testDist = GetTargetDist(side);
            float err = GetErr(side);
            if (err <= reachTolerance) goodDist = testDist; else badDist = testDist;

            if (badDist > 0f && Mathf.Abs(badDist - goodDist) <= 0.0025f)
            {
                Commit(side, goodDist);
                NextOrFinish();
                return;
            }

            float mid = (badDist > 0f) ? 0.5f * (goodDist + badDist) : (testDist + expandStep);
            SetTarget(side, mid);
        }
    }

    // --- internals ---
    Vector3 CenterXZ()
    {
        var p = root.position; p.y = headHeight; return p;
    }

    void SetupSideVectors()
    {
        Vector3 c = CenterXZ();

        leftDir = leftTarget.position - c; leftDir.y = 0f;
        if (leftDir.sqrMagnitude < 1e-4f) leftDir = root.forward;
        leftDir.Normalize();

        rightDir = rightTarget.position - c; rightDir.y = 0f;
        if (rightDir.sqrMagnitude < 1e-4f) rightDir = root.forward;
        rightDir.Normalize();
    }

    void ResetPhase()
    {
        goodDist = 0f;
        badDist = -1f;
        expanding = true;
    }

    void SetTarget(Side s, float dist)
    {
        Vector3 c = CenterXZ();
        if (s == Side.Left) leftTarget.position = c + leftDir * dist;
        else rightTarget.position = c + rightDir * dist;
    }

    float GetTargetDist(Side s)
    {
        Vector3 c = CenterXZ();
        Vector3 p = (s == Side.Left) ? leftTarget.position : rightTarget.position;
        p.y = c.y;
        return Vector3.Distance(c, p);
    }

    float GetErr(Side s)
    {
        if (s == Side.Left) return Vector3.Distance(leftTarget.position, leftTip.position);
        else return Vector3.Distance(rightTarget.position, rightTip.position);
    }

    void Commit(Side s, float bestDist)
    {
        if (s == Side.Left) MaxLeft = bestDist;
        else MaxRight = bestDist;
        SetTarget(s, bestDist); // park at max reach
    }

    void NextOrFinish()
    {
        if (side == Side.Left)
        {
            side = Side.Right;
            ResetPhase();
            SetTarget(side, startDist);
        }
        else
        {
            running = false;
            Debug.Log($"[ReachCalibration] Left={MaxLeft:F3}m Right={MaxRight:F3}m");
        }
    }
}
