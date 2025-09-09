using UnityEngine;

public class RotationLightController : MonoBehaviour
{
    public enum AimPreset { Forward, Up, Down, Left, Right, Back, Custom }
    public enum Axis { X, Y, Z }
    public enum SpaceRef { Local, World }
    public enum Motion { PingPong, Continuous } // PingPong = scan, Continuous = loop

    [Header("Initial Aim")]
    public AimPreset initialAim = AimPreset.Down;
    public Vector3 customDirection = Vector3.down; // used if AimPreset.Custom
    public Vector3 upHint = Vector3.forward;       // roll reference when aiming

    [Header("Rotation")]
    public SpaceRef axisSpace = SpaceRef.Local;
    public Axis rotateAround = Axis.Y;
    public float minAngle = -45f;      // degrees, relative to initial aim
    public float maxAngle = 45f;      // degrees, relative to initial aim
    public float speedDegPerSec = 30f; // sweep speed
    public Motion motion = Motion.PingPong;

    [Header("Start Phase")]
    [Range(0, 1)] public float phase01 = 0f; // 0..1 start position between min..max

    Quaternion baseRot;
    float curAngle;   // relative angle from baseRot
    float dir = 1f;   // for Continuous wrap

    void Awake()
    {
        // store base rotation from initial aim
        Vector3 aim =
            initialAim == AimPreset.Forward ? Vector3.forward :
            initialAim == AimPreset.Up ? Vector3.up :
            initialAim == AimPreset.Down ? Vector3.down :
            initialAim == AimPreset.Left ? Vector3.left :
            initialAim == AimPreset.Right ? Vector3.right :
            initialAim == AimPreset.Back ? Vector3.back :
            customDirection.sqrMagnitude < 1e-6f ? Vector3.down : customDirection.normalized;

        // build a rotation that looks along 'aim' with a roll hint
        Vector3 up = upHint.sqrMagnitude < 1e-6f ? Vector3.forward : upHint.normalized;
        baseRot = Quaternion.LookRotation(aim, up);

        // set initial phase position between min..max
        curAngle = Mathf.Lerp(minAngle, maxAngle, Mathf.Clamp01(phase01));
        ApplyRotation(curAngle);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        float span = maxAngle - minAngle;
        if (span <= 0f) { ApplyRotation(Mathf.Clamp(curAngle, minAngle, maxAngle)); return; }

        float delta = speedDegPerSec * dt;

        if (motion == Motion.PingPong)
        {
            // move and bounce inside [min,max]
            curAngle += delta * dir;
            if (curAngle > maxAngle) { curAngle = maxAngle; dir = -1f; }
            else if (curAngle < minAngle) { curAngle = minAngle; dir = 1f; }
        }
        else // Continuous
        {
            // loop around, mapping into [min,max]
            curAngle += delta * dir;
            if (curAngle > maxAngle) curAngle = minAngle + (curAngle - maxAngle);
            if (curAngle < minAngle) curAngle = maxAngle - (minAngle - curAngle);
        }

        ApplyRotation(curAngle);
    }

    void ApplyRotation(float angle)
    {
        // choose axis vector in the requested space
        Vector3 axis =
            rotateAround == Axis.X ? Vector3.right :
            rotateAround == Axis.Y ? Vector3.up : Vector3.forward;

        if (axisSpace == SpaceRef.Local)
            axis = transform.TransformDirection(axis); // convert local axis to world

        // rotation = baseRot then rotate around chosen axis by 'angle'
        Quaternion q = Quaternion.AngleAxis(angle, axis);
        // Important: rotate around axis in WORLD space, so multiply on the *left*
        transform.rotation = q * baseRot;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        // visualize min/center/max rays
        Gizmos.color = Color.cyan;
        var saved = transform.rotation;

        // recompute baseRot in edit if needed
        Vector3 aim =
            initialAim == AimPreset.Forward ? Vector3.forward :
            initialAim == AimPreset.Up ? Vector3.up :
            initialAim == AimPreset.Down ? Vector3.down :
            initialAim == AimPreset.Left ? Vector3.left :
            initialAim == AimPreset.Right ? Vector3.right :
            initialAim == AimPreset.Back ? Vector3.back :
            (customDirection.sqrMagnitude < 1e-6f ? Vector3.down : customDirection.normalized);
        Vector3 up = upHint.sqrMagnitude < 1e-6f ? Vector3.forward : upHint.normalized;
        baseRot = Quaternion.LookRotation(aim, up);

        Vector3 axis =
            rotateAround == Axis.X ? Vector3.right :
            rotateAround == Axis.Y ? Vector3.up : Vector3.forward;
        if (axisSpace == SpaceRef.Local && Application.isPlaying)
            axis = transform.TransformDirection(axis);

        void DrawAt(float a, Color c)
        {
            Quaternion q = Quaternion.AngleAxis(a, axis);
            transform.rotation = q * baseRot;
            Gizmos.color = c;
            Gizmos.DrawRay(transform.position, transform.forward * 2f);
        }

        DrawAt(minAngle, Color.yellow);
        DrawAt(Mathf.Lerp(minAngle, maxAngle, 0.5f), Color.cyan);
        DrawAt(maxAngle, Color.magenta);

        transform.rotation = saved;
    }
#endif
}
