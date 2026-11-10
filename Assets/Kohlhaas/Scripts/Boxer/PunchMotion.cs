using UnityEngine;

public enum BoxingMode { Straight, Swing }

// Geometry and timing are shared by player input, bots, and the scene gizmos.
internal sealed class PunchMotion
{
    public bool IsActive { get; private set; }
    public bool AttackedThisStep { get; private set; }
    public BoxingMode Mode { get; private set; }
    public Vector3 Position { get; private set; }
    public float AttackProgress { get; private set; }
    // Zero in the defensive guard, one in the punch pose, with a smooth recovery.
    public float PoseWeight { get; private set; }
    private Vector3 guard, strike, pivot;
    private float radius, startAngle, sweepAngle;
    private float elapsed, windup, extension, hold, recovery;

    public bool Start(BoxingMode mode, Vector3 guardPosition, Vector3 straightEnd,
        Vector3 circlePivot, float circleRadius, float circleStart, float circleSweep,
        float windupTime, float extensionTime, float holdTime, float recoveryTime)
    {
        if (IsActive) return false;
        Mode = mode;
        guard = guardPosition;
        pivot = circlePivot;
        radius = circleRadius;
        startAngle = circleStart;
        sweepAngle = circleSweep;
        strike = mode == BoxingMode.Straight ? straightEnd
            : CirclePoint(pivot, radius, startAngle + sweepAngle);
        windup = mode == BoxingMode.Swing ? Mathf.Max(0f, windupTime) : 0f;
        extension = Mathf.Max(0.001f, extensionTime);
        hold = Mathf.Max(0f, holdTime);
        recovery = Mathf.Max(0.001f, recoveryTime);
        elapsed = 0f;
        AttackProgress = 0f;
        PoseWeight = 0f;
        AttackedThisStep = false;
        Position = guard;
        IsActive = true;
        return true;
    }

    public void Advance(float deltaTime)
    {
        AttackedThisStep = false;
        if (!IsActive || deltaTime <= 0f) return;
        float previous = elapsed;
        elapsed += deltaTime;
        AttackedThisStep = elapsed > windup && previous < windup + extension;
        // Render the strike at least once before recovery. A long frame must not
        // skip the contact pose or report a backwards-moving recovery as a hit.
        if (previous < windup + extension && elapsed >= windup + extension)
        {
            AttackProgress = 1f;
            PoseWeight = 1f;
            Position = strike;
            return;
        }
        if (elapsed < windup)
        {
            PoseWeight = Mathf.SmoothStep(0f, 1f, elapsed / windup);
            Position = Vector3.Lerp(guard, CirclePoint(pivot, radius, startAngle),
                PoseWeight);
        }
        else if (elapsed < windup + extension)
        {
            AttackProgress = (elapsed - windup) / extension;
            PoseWeight = Mode == BoxingMode.Swing ? 1f : 1f - Mathf.Pow(1f - AttackProgress, 3f);
            Position = Mode == BoxingMode.Swing
                ? CirclePoint(pivot, radius, startAngle + sweepAngle * AttackProgress)
                : Vector3.Lerp(guard, strike, 1f - Mathf.Pow(1f - AttackProgress, 3f));
        }
        else if (elapsed < windup + extension + hold)
        {
            AttackProgress = 1f;
            PoseWeight = 1f;
            Position = strike;
        }
        else if (elapsed < windup + extension + hold + recovery)
        {
            AttackProgress = 1f;
            PoseWeight = 1f - Mathf.SmoothStep(0f, 1f, (elapsed - windup - extension - hold) / recovery);
            Position = Vector3.Lerp(guard, strike, PoseWeight);
        }
        else
        {
            IsActive = false;
            Position = guard;
            AttackProgress = 0f;
            PoseWeight = 0f;
        }
    }

    public void Reset(Vector3 guardPosition)
    {
        IsActive = false;
        AttackedThisStep = false;
        AttackProgress = 0f;
        PoseWeight = 0f;
        elapsed = 0f;
        Position = guardPosition;
    }

    public static Vector3 CirclePoint(Vector3 pivot, float radius, float angleDegrees)
    {
        float angle = angleDegrees * Mathf.Deg2Rad;
        return pivot + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius;
    }
}
