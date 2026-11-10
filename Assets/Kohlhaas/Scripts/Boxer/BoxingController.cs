using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)]
public sealed class BoxingController : MonoBehaviour
{
    [Header("Rig")]
    [Tooltip("Unanimated character root used as the local space for punch paths.")]
    [SerializeField] private Transform targetSpace;
    [SerializeField] private TwoBoneIKConstraint leftArmIK;
    [SerializeField] private TwoBoneIKConstraint rightArmIK;
    [SerializeField, Range(0.5f, 1f)] private float reachLimit = 0.98f;

    [Header("Guard and Elbows (relative to Target Space)")]
    [SerializeField] private Vector3 leftGuard = new Vector3(-0.18f, 1.28f, 0.28f);
    [SerializeField] private Vector3 rightGuard = new Vector3(0.18f, 1.28f, 0.28f);
    [Tooltip("Defensive elbow pole: low and close to the ribs.")]
    [SerializeField] private Vector3 leftElbowHint = new Vector3(-0.28f, 0.9f, 0.04f);
    [SerializeField] private Vector3 rightElbowHint = new Vector3(0.28f, 0.9f, 0.04f);
    [SerializeField] private Vector3 leftPunchElbowHint = new Vector3(-0.55f, 1.1f, -0.12f);
    [SerializeField] private Vector3 rightPunchElbowHint = new Vector3(0.55f, 1.1f, -0.12f);

    [Header("Wrist Alignment")]
    [Tooltip("Local hand-bone axis pointing from wrist toward knuckles. This model uses +Y.")]
    [SerializeField] private Vector3 gloveForwardAxis = Vector3.up;
    [Tooltip("Align the wrist with its solved forearm, preserving this hand's bind-pose roll.")]
    [SerializeField, Range(0f, 1f)] private float wristAlignmentWeight = 1f;
    [SerializeField] private Vector3 leftWristRotationOffset;
    [SerializeField] private Vector3 rightWristRotationOffset;

    [Header("Defensive Glove Orientation")]
    [Tooltip("Local hand-bone axis pointing toward the thumb side of each glove.")]
    [SerializeField] private Vector3 leftThumbAxis = Vector3.right;
    [SerializeField] private Vector3 rightThumbAxis = Vector3.left;
    [Tooltip("Roll gloves inward in guard; blend back to punch alignment during a strike.")]
    [SerializeField, Range(0f, 1f)] private float guardThumbInwardWeight = 1f;

    [Header("Straight Punch")]
    [SerializeField, Min(0f)] private float straightDistance = 0.38f;
    [Tooltip("Punch height stays independent of the lower defensive guard.")]
    [SerializeField] private float straightStrikeHeight = 1.4f;
    [SerializeField, Min(0.01f)] private float straightExtensionTime = 0.075f;
    [SerializeField, Min(0.01f)] private float straightRetractionTime = 0.13f;

    [Header("Circular Swing (mirrored from left hand)")]
    [SerializeField] private Vector3 swingPivot = new Vector3(-0.1f, 1.4f, 0.04f);
    [SerializeField, Min(0.01f)] private float swingRadius = 0.48f;
    [SerializeField] private float swingStartAngle = -105f;
    [SerializeField, Range(10f, 300f)] private float swingSweepAngle = 125f;
    [SerializeField, Min(0f)] private float swingWindupTime = 0.07f;
    [SerializeField, Min(0.01f)] private float swingExtensionTime = 0.16f;
    [SerializeField, Min(0.01f)] private float swingRetractionTime = 0.18f;

    [Header("Controls (expand each action to edit bindings)")]
    [SerializeField] private InputAction leftPunchAction = new InputAction("Left Punch", InputActionType.Button, "<Mouse>/leftButton");
    [SerializeField] private InputAction rightPunchAction = new InputAction("Right Punch", InputActionType.Button, "<Mouse>/rightButton");
    [SerializeField] private InputAction toggleModeAction = new InputAction("Change Mode", InputActionType.Button, "<Keyboard>/ctrl");
    [SerializeField] private BoxingMode mode = BoxingMode.Straight;
    [SerializeField, Min(0f)] private float extensionHoldTime = 0.025f;
    [SerializeField] private bool readPlayerInput = true;
    [SerializeField] private bool showModeLabel = true;

    [Header("Impacts")]
    [SerializeField] private LayerMask hitLayers = ~0;
    [SerializeField, Min(0.01f)] private float gloveHitRadius = 0.11f;
    [Tooltip("Distance from wrist toward knuckles, in world metres for this model.")]
    [SerializeField, Min(0f)] private float gloveContactOffset = 0.14f;
    [SerializeField, Min(0f)] private float straightImpulse = 4.5f;
    [SerializeField, Min(0f)] private float swingImpulse = 6f;

    public BoxingMode Mode => mode;
    public bool IsPunchingLeft => leftHand != null && leftHand.motion.IsActive;
    public bool IsPunchingRight => rightHand != null && rightHand.motion.IsActive;

    private sealed class Hand
    {
        public readonly Transform root, mid, tip, target, hint;
        public readonly float upperLength, lowerLength;
        public readonly Quaternion alignedWristLocalRotation;
        public readonly PunchMotion motion = new PunchMotion();
        public Vector3 previousContact;
        public bool hasHit;

        public Hand(TwoBoneIKConstraint constraint, Vector3 gloveForward, SkinnedMeshRenderer[] skins)
        {
            var data = constraint.data;
            root = data.root; mid = data.mid; tip = data.tip;
            target = data.target; hint = data.hint;
            upperLength = Vector3.Distance(root.position, mid.position);
            lowerLength = Vector3.Distance(mid.position, tip.position);

            // Use the authored skin bind pose, independently for left and right.
            // The currently playing animation may already contain a bent wrist.
            Quaternion reference = Quaternion.Inverse(mid.rotation) * tip.rotation;
            foreach (var skin in skins)
            {
                if (skin.sharedMesh == null) continue;
                Transform[] bones = skin.bones;
                int midIndex = System.Array.IndexOf(bones, mid);
                int tipIndex = System.Array.IndexOf(bones, tip);
                if (midIndex < 0 || tipIndex < 0) continue;
                Matrix4x4[] bindPoses = skin.sharedMesh.bindposes;
                if (midIndex >= bindPoses.Length || tipIndex >= bindPoses.Length) continue;
                reference = (bindPoses[midIndex] * bindPoses[tipIndex].inverse).rotation;
                break;
            }
            Vector3 forearmAxis = mid.InverseTransformDirection(tip.position - mid.position).normalized;
            // Remove wrist flexion while retaining the model's own twist around the arm.
            alignedWristLocalRotation = Quaternion.FromToRotation(reference * gloveForward.normalized,
                forearmAxis) * reference;
        }
    }

    private Hand leftHand, rightHand;
    private readonly RaycastHit[] sweepHits = new RaycastHit[32];
    private readonly Collider[] overlaps = new Collider[32];

    private void Awake()
    {
        if (targetSpace == null || !IsArmValid(leftArmIK) || !IsArmValid(rightArmIK)
            || gloveForwardAxis.sqrMagnitude < 0.001f)
        {
            Debug.LogError("BoxingController needs Target Space, two complete arm IK chains, and a nonzero glove forward axis.", this);
            enabled = false;
            return;
        }
        var skins = targetSpace.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        leftHand = new Hand(leftArmIK, gloveForwardAxis, skins);
        rightHand = new Hand(rightArmIK, gloveForwardAxis, skins);
        ConfigurePositionIK(leftArmIK);
        ConfigurePositionIK(rightArmIK);
        ResetTargets();
    }

    private static bool IsArmValid(TwoBoneIKConstraint constraint)
    {
        if (constraint == null) return false;
        var data = constraint.data;
        return data.root != null && data.mid != null && data.tip != null && data.target != null
            && data.mid.IsChildOf(data.root) && data.tip.IsChildOf(data.mid);
    }

    private static void ConfigurePositionIK(TwoBoneIKConstraint constraint)
    {
        var data = constraint.data;
        // IK solves the elbow and forearm first. LateUpdate then aligns the wrist
        // to those actual bones, without imposing an unrelated world-space roll.
        data.targetRotationWeight = 0f;
        data.maintainTargetRotationOffset = false;
        constraint.data = data;
    }

    private void OnEnable()
    {
        leftPunchAction?.Enable(); rightPunchAction?.Enable(); toggleModeAction?.Enable();
        if (leftHand == null) return;
        leftArmIK.weight = rightArmIK.weight = 1f;
        ResetTargets();
    }

    private void OnDisable()
    {
        leftPunchAction?.Disable(); rightPunchAction?.Disable(); toggleModeAction?.Disable();
        if (leftHand == null) return;
        ResetTargets();
        leftArmIK.weight = rightArmIK.weight = 0f;
    }

    private void OnDestroy()
    {
        leftPunchAction?.Dispose(); rightPunchAction?.Dispose(); toggleModeAction?.Dispose();
    }

    private void Update()
    {
        if (Time.deltaTime <= 0f) return;
        if (readPlayerInput)
        {
            if (toggleModeAction.WasPressedThisFrame()) ToggleMode();
            if (leftPunchAction.WasPressedThisFrame()) TryPunchLeft();
            if (rightPunchAction.WasPressedThisFrame()) TryPunchRight();
        }
        UpdateHand(leftHand, leftGuard, leftElbowHint, leftPunchElbowHint, Time.deltaTime);
        UpdateHand(rightHand, rightGuard, rightElbowHint, rightPunchElbowHint, Time.deltaTime);
    }

    // Animator and Animation Rigging have evaluated the actual glove bones by LateUpdate.
    private void LateUpdate()
    {
        if (leftHand == null || Time.deltaTime <= 0f) return;
        AlignWrist(leftHand, leftWristRotationOffset, leftThumbAxis, 1f);
        AlignWrist(rightHand, rightWristRotationOffset, rightThumbAxis, -1f);
        DetectHit(leftHand);
        DetectHit(rightHand);
    }

    public void ToggleMode() => mode = mode == BoxingMode.Straight ? BoxingMode.Swing : BoxingMode.Straight;
    public bool TryPunchLeft() => TryPunch(leftHand, leftGuard, -1f);
    public bool TryPunchRight() => TryPunch(rightHand, rightGuard, 1f);

    private bool TryPunch(Hand hand, Vector3 guard, float side)
    {
        if (!isActiveAndEnabled || hand == null || hand.motion.IsActive) return false;
        GetCircle(side, out Vector3 pivot, out float angle, out float sweep);
        float radius = FittedRadius(hand.root.position, hand.upperLength + hand.lowerLength, pivot);
        bool started = hand.motion.Start(mode, guard, StraightStrike(guard),
            pivot, radius, angle, sweep, swingWindupTime,
            mode == BoxingMode.Straight ? straightExtensionTime : swingExtensionTime,
            extensionHoldTime, mode == BoxingMode.Straight ? straightRetractionTime : swingRetractionTime);
        if (started)
        {
            hand.hasHit = false;
            hand.previousContact = ContactPosition(hand);
        }
        return started;
    }

    private float FittedRadius(Vector3 shoulder, float armLength, Vector3 pivot)
    {
        // Fit the whole circle once, preserving its shape instead of clipping each point.
        float available = armLength * reachLimit - Vector3.Distance(shoulder, targetSpace.TransformPoint(pivot));
        return Mathf.Min(swingRadius, Mathf.Max(0.01f, available / Mathf.Max(0.001f, Mathf.Abs(targetSpace.lossyScale.x))));
    }

    private void GetCircle(float side, out Vector3 pivot, out float angle, out float sweep)
    {
        pivot = swingPivot;
        pivot.x *= -side;
        angle = swingStartAngle * -side;
        sweep = swingSweepAngle * -side;
    }

    private Vector3 StraightStrike(Vector3 guard) => new Vector3(guard.x, straightStrikeHeight, guard.z + straightDistance);

    private void UpdateHand(Hand hand, Vector3 guard, Vector3 elbowHint, Vector3 punchElbowHint, float deltaTime)
    {
        hand.motion.Advance(deltaTime);
        if (!hand.motion.IsActive && !hand.motion.AttackedThisStep) hand.motion.Reset(guard);
        Vector3 goal = targetSpace.TransformPoint(hand.motion.Position);
        Vector3 delta = goal - hand.root.position;
        float distance = Mathf.Clamp(delta.magnitude,
            Mathf.Abs(hand.upperLength - hand.lowerLength) + 0.001f,
            (hand.upperLength + hand.lowerLength) * reachLimit);
        Vector3 direction = delta.sqrMagnitude > 0.000001f ? delta.normalized : targetSpace.forward;
        goal = hand.root.position + direction * distance;
        hand.target.position = goal;

        Vector3 hint = targetSpace.TransformPoint(Vector3.Lerp(elbowHint, punchElbowHint, hand.motion.PoseWeight)
            + (hand.motion.Position - guard) * (0.35f * hand.motion.PoseWeight));
        if (hand.hint != null) hand.hint.position = hint;
    }

    private void AlignWrist(Hand hand, Vector3 rotationOffset, Vector3 thumbAxis, float inwardSign)
    {
        Quaternion aligned = hand.mid.rotation * hand.alignedWristLocalRotation * Quaternion.Euler(rotationOffset);
        Vector3 forward = (hand.tip.position - hand.mid.position).normalized;
        Vector3 thumb = Vector3.ProjectOnPlane(aligned * thumbAxis, forward);
        Vector3 inward = Vector3.ProjectOnPlane(targetSpace.right * inwardSign, forward);
        if (thumb.sqrMagnitude > 0.0001f && inward.sqrMagnitude > 0.0001f)
        {
            // Roll the forearm together with its glove, so the inward-facing
            // thumb does not introduce a twist at the wrist or move the elbow.
            float angle = Vector3.SignedAngle(thumb, inward, forward);
            float guardWeight = (1f - hand.motion.PoseWeight) * guardThumbInwardWeight;
            Quaternion roll = Quaternion.AngleAxis(angle * guardWeight, forward);
            hand.mid.rotation = roll * hand.mid.rotation;
            aligned = roll * aligned;
        }
        hand.tip.rotation = Quaternion.Slerp(hand.tip.rotation, aligned, wristAlignmentWeight);
        hand.target.rotation = hand.tip.rotation;
    }

    private Vector3 ContactPosition(Hand hand) => hand.tip.position
        + hand.tip.TransformDirection(gloveForwardAxis.normalized) * gloveContactOffset;

    private void DetectHit(Hand hand)
    {
        Vector3 contact = ContactPosition(hand);
        Vector3 travel = contact - hand.previousContact;
        if (hand.motion.AttackedThisStep && !hand.hasHit)
        {
            Collider closest = null;
            Vector3 point = contact;
            float nearest = float.PositiveInfinity;
            float distance = travel.magnitude;
            if (distance > 0.00001f)
            {
                int count = Physics.SphereCastNonAlloc(hand.previousContact, gloveHitRadius,
                    travel / distance, sweepHits, distance, hitLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    var hit = sweepHits[i];
                    if (hit.collider.transform.IsChildOf(transform) || hit.distance >= nearest) continue;
                    closest = hit.collider; nearest = hit.distance; point = hit.point;
                }
            }
            // Sphere casts do not report colliders containing the initial sphere.
            int overlapping = Physics.OverlapSphereNonAlloc(hand.previousContact, gloveHitRadius,
                overlaps, hitLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < overlapping; i++)
            {
                var collider = overlaps[i];
                if (collider.transform.IsChildOf(transform)) continue;
                closest = collider; point = collider.ClosestPoint(hand.previousContact); break;
            }
            if (closest != null)
            {
                // A wall also consumes the strike, preventing a hit through it.
                hand.hasHit = true;
                var receiver = closest.GetComponentInParent<IPunchHitReceiver>();
                if (receiver != null)
                {
                    Vector3 direction = travel.sqrMagnitude > 0.00001f ? travel.normalized : targetSpace.forward;
                    receiver.ReceivePunch(new PunchHit(point, direction,
                        hand.motion.Mode == BoxingMode.Straight ? straightImpulse : swingImpulse, hand.motion.Mode));
                }
            }
        }
        hand.previousContact = contact;
    }

    private void ResetTargets()
    {
        leftHand.motion.Reset(leftGuard); rightHand.motion.Reset(rightGuard);
        UpdateHand(leftHand, leftGuard, leftElbowHint, leftPunchElbowHint, 0f);
        UpdateHand(rightHand, rightGuard, rightElbowHint, rightPunchElbowHint, 0f);
        leftHand.previousContact = ContactPosition(leftHand);
        rightHand.previousContact = ContactPosition(rightHand);
    }

    private void OnGUI()
    {
        if (showModeLabel)
            GUI.Box(new Rect(12f, 12f, 400f, 72f), $"Boxing: {mode}\n{leftPunchAction.GetBindingDisplayString()} / {rightPunchAction.GetBindingDisplayString()}: punch\n{toggleModeAction.GetBindingDisplayString()}: change mode");
    }

    private void OnDrawGizmosSelected()
    {
        if (targetSpace == null) return;
        DrawPath(leftGuard, -1f, Color.yellow);
        DrawPath(rightGuard, 1f, Color.cyan);
    }

    private void DrawPath(Vector3 guard, float side, Color color)
    {
        Gizmos.color = color;
        Vector3 previous = targetSpace.TransformPoint(guard);
        GetCircle(side, out Vector3 pivot, out float angle, out float sweep);
        float radius = swingRadius;
        var constraint = side < 0f ? leftArmIK : rightArmIK;
        if (constraint != null && IsArmValid(constraint))
        {
            var data = constraint.data;
            radius = FittedRadius(data.root.position, Vector3.Distance(data.root.position, data.mid.position)
                + Vector3.Distance(data.mid.position, data.tip.position), pivot);
        }
        for (int i = 0; i <= 40; i++)
        {
            float t = i / 40f;
            Vector3 local = mode == BoxingMode.Straight ? Vector3.Lerp(guard, StraightStrike(guard), t)
                : PunchMotion.CirclePoint(pivot, radius, angle + sweep * t);
            Vector3 point = targetSpace.TransformPoint(local);
            Gizmos.DrawLine(previous, point);
            previous = point;
        }
    }
}
