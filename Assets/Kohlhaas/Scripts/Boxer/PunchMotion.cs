using UnityEngine;

public enum BoxingMode { Straight, Swing }

// The target trajectory is independent of the rig and input, so bots can use it too.
internal sealed class PunchMotion
{
    public bool IsActive { get; private set; }
    public BoxingMode Mode { get; private set; }
    public Vector3 Position { get; private set; }

    private Vector3 guard;
    private Vector3 strike;
    private Vector3 control;
    private float elapsed;
    private float extensionDuration;
    private float holdDuration;
    private float retractionDuration;

    public bool Start(BoxingMode mode, Vector3 start, Vector3 end, Vector3 arcControl,
        float extensionTime, float holdTime, float retractionTime)
    {
        if (IsActive) return false;

        Mode = mode;
        guard = start;
        strike = end;
        control = arcControl;
        extensionDuration = Mathf.Max(0.001f, extensionTime);
        holdDuration = Mathf.Max(0f, holdTime);
        retractionDuration = Mathf.Max(0.001f, retractionTime);
        elapsed = 0f;
        Position = guard;
        IsActive = true;
        return true;
    }

    public void Advance(float deltaTime)
    {
        if (!IsActive || deltaTime <= 0f) return;
        elapsed += deltaTime;

        float progress;
        if (elapsed < extensionDuration)
        {
            float t = elapsed / extensionDuration;
            // Fast extension, easing into the fully extended position.
            progress = 1f - (1f - t) * (1f - t) * (1f - t);
        }
        else if (elapsed < extensionDuration + holdDuration)
        {
            progress = 1f;
        }
        else if (elapsed < extensionDuration + holdDuration + retractionDuration)
        {
            float t = (elapsed - extensionDuration - holdDuration) / retractionDuration;
            progress = 1f - t * t * (3f - 2f * t);
        }
        else
        {
            Reset(guard);
            return;
        }

        Position = PointOnPath(Mode, guard, strike, control, progress);
    }

    public void Reset(Vector3 guardPosition)
    {
        IsActive = false;
        elapsed = 0f;
        Position = guardPosition;
    }

    public static Vector3 PointOnPath(BoxingMode mode, Vector3 start,
        Vector3 end, Vector3 arcControl, float progress)
    {
        float t = Mathf.Clamp01(progress);
        if (mode == BoxingMode.Straight) return Vector3.Lerp(start, end, t);

        float inverse = 1f - t;
        return inverse * inverse * start + 2f * inverse * t * arcControl + t * t * end;
    }
}
