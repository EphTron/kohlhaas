using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using System.Collections.Generic;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)]
public sealed class BoxingController : MonoBehaviour
{
    [Header("Rig")]
    [Tooltip("Unanimated character root used as the local space for punch paths.")]
    [SerializeField] private Transform targetSpace;
    [SerializeField] private TwoBoneIKConstraint leftArmIK;
    [SerializeField] private TwoBoneIKConstraint rightArmIK;
    [SerializeField] private BoxingStanceController stanceController;
    [Tooltip("Additive chest rotation in a RigBuilder layer evaluated before the arm IK.")]
    [SerializeField] private OverrideTransform torsoTwist;
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

    [Header("Light Punch — Either Hand")]
    [SerializeField] private PunchProfile leadPunch = new PunchProfile
    {
        reachMultiplier = 0.78f, extensionTimeMultiplier = 0.8f,
        recoveryTimeMultiplier = 0.85f, impulseMultiplier = 0.65f
    };

    [Header("Lead-Lead-Rear Combo Finisher")]
    [SerializeField] private PunchProfile rearPunch = new PunchProfile
    {
        windupTime = 0.1f, pullbackDistance = 0.13f, outwardDraw = 0.035f,
        reachMultiplier = 1.08f, extensionTimeMultiplier = 1.05f,
        recoveryTimeMultiplier = 1.3f, impulseMultiplier = 1.65f,
        torsoWindupAngle = 12f, torsoStrikeAngle = 18f
    };

    [Header("Space-Click Uppercut")]
    [FormerlySerializedAs("hookPunch")]
    [SerializeField] private PunchProfile uppercutPunch = new PunchProfile
    {
        reachMultiplier = 1f, extensionTimeMultiplier = 0.8f,
        recoveryTimeMultiplier = 1f, impulseMultiplier = 1.1f,
        torsoWindupAngle = 6f, torsoStrikeAngle = 10f
    };
    [Tooltip("Hand dip relative to the normal guard, before the rising strike.")]
    [SerializeField] private Vector3 uppercutDipOffset = new Vector3(0f, -0.24f, -0.1f);
    [SerializeField] private Vector3 uppercutStrikeOffset = new Vector3(0f, 0.34f, 0.19f);
    [Tooltip("Control point of the rising arc, relative to guard. Keep X near zero for an upward strike.")]
    [SerializeField] private Vector3 uppercutControlOffset = new Vector3(0f, -0.22f, 0.23f);
    [SerializeField, Min(0.01f)] private float uppercutWindupTime = 0.09f;
    [SerializeField, Min(0.01f)] private float uppercutExtensionTime = 0.12f;
    [SerializeField, Min(0.01f)] private float uppercutRecoveryTime = 0.18f;
    [SerializeField, Min(0f)] private float uppercutImpulse = 6.5f;

    [Header("Input Patterns")]
    [Tooltip("Wait this long for the other held mouse button before firing a single punch.")]
    [SerializeField, Range(0.01f, 0.12f)] private float simultaneousWindow = 0.045f;
    [FormerlySerializedAs("hookInputWindow")]
    [SerializeField, Range(0.05f, 0.5f)] private float uppercutInputWindow = 0.22f;
    [SerializeField, Min(0.1f)] private float comboTapGap = 0.45f;
    [SerializeField, Min(0.2f)] private float comboDuration = 0.9f;
    [SerializeField, Min(0.1f)] private float punchBufferTime = 0.65f;

    [Header("Two-Hand Push — Straight Mode")]
    [SerializeField, Min(0.05f)] private float pushDistance = 0.38f;
    [SerializeField] private float pushHeight = 1.22f;
    [SerializeField, Min(0.01f)] private float pushExtensionTime = 0.12f;
    [SerializeField, Min(0.01f)] private float pushRecoveryTime = 0.2f;
    [SerializeField, Min(0f)] private float pushImpulse = 10f;

    [Header("Raised Guard — Swing Mode")]
    [SerializeField] private Vector3 raisedLeftGuard = new Vector3(-0.16f, 1.48f, 0.26f);
    [SerializeField] private Vector3 raisedRightGuard = new Vector3(0.16f, 1.48f, 0.26f);
    [SerializeField, Min(0.01f)] private float guardBlendTime = 0.12f;

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
    [FormerlySerializedAs("hookAction")]
    [SerializeField] private InputAction uppercutAction = new InputAction("Prime Uppercut", InputActionType.Button, "<Keyboard>/space");
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
    public bool IsGuarding => raisedGuardHeld;

    private sealed class Hand
    {
        public readonly Transform root, mid, tip, target, hint;
        public readonly float upperLength, lowerLength;
        public readonly Quaternion alignedWristLocalRotation;
        public readonly PunchMotion motion = new PunchMotion();
        public Vector3 previousContact;
        public bool hasHit;
        public PunchKind kind;
        public float impulse;

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
    private Hand torsoOwner;
    private readonly BoxingInputBuffer inputBuffer = new BoxingInputBuffer();
    private readonly Queue<BoxingCommand> pendingStrikes = new Queue<BoxingCommand>();
    private readonly HashSet<IPunchHitReceiver> pushReceivers = new HashSet<IPunchHitReceiver>();
    private bool acceptInput, raisedGuardHeld, defensiveBlendActive;
    private float defensiveBlendElapsed, currentTorsoYaw;
    private Vector3 leftDefensiveStart, rightDefensiveStart;
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
        if (stanceController == null) stanceController = GetComponent<BoxingStanceController>();
        if (torsoTwist != null)
        {
            var torsoData = torsoTwist.data;
            torsoData.space = OverrideTransformData.Space.Pivot;
            torsoData.sourceObject = null;
            torsoData.positionWeight = 0f;
            torsoData.rotationWeight = 1f;
            torsoTwist.data = torsoData;
        }
        else Debug.LogWarning("Assign a torso Override Transform to enable rear-punch shoulder rotation.", this);
        ConfigurePositionIK(leftArmIK);
        ConfigurePositionIK(rightArmIK);
        ConfigureInputWindows();
        leftPunchAction.performed += OnLeftPress;
        leftPunchAction.canceled += OnLeftRelease;
        rightPunchAction.performed += OnRightPress;
        rightPunchAction.canceled += OnRightRelease;
        uppercutAction.performed += OnUppercutPress;
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
        acceptInput = true;
        leftPunchAction?.Enable(); rightPunchAction?.Enable(); toggleModeAction?.Enable();
        uppercutAction?.Enable();
        if (leftHand == null) return;
        leftArmIK.weight = rightArmIK.weight = 1f;
        ResetTargets();
    }

    private void OnDisable()
    {
        acceptInput = false;
        leftPunchAction?.Disable(); rightPunchAction?.Disable(); toggleModeAction?.Disable();
        uppercutAction?.Disable();
        if (leftHand == null) return;
        ResetTargets();
        leftArmIK.weight = rightArmIK.weight = 0f;
    }

    private void OnDestroy()
    {
        leftPunchAction?.Dispose(); rightPunchAction?.Dispose(); toggleModeAction?.Dispose();
        uppercutAction?.Dispose();
    }

    private BoxingStance CurrentStance => stanceController != null ? stanceController.Stance : BoxingStance.Orthodox;
    private bool CanReadInput => acceptInput && readPlayerInput && Time.timeScale > 0f && leftHand != null;
    private void OnLeftPress(InputAction.CallbackContext context) { if (CanReadInput) inputBuffer.Press(-1, context.time, mode, CurrentStance); }
    private void OnRightPress(InputAction.CallbackContext context) { if (CanReadInput) inputBuffer.Press(1, context.time, mode, CurrentStance); }
    private void OnLeftRelease(InputAction.CallbackContext context) { if (CanReadInput) inputBuffer.Release(-1, context.time); }
    private void OnRightRelease(InputAction.CallbackContext context) { if (CanReadInput) inputBuffer.Release(1, context.time); }
    private void OnUppercutPress(InputAction.CallbackContext context) { if (CanReadInput) inputBuffer.ArmUppercut(context.time); }

    private void ConfigureInputWindows()
    {
        inputBuffer.SimultaneousWindow = simultaneousWindow;
        inputBuffer.UppercutWindow = uppercutInputWindow;
        inputBuffer.ComboGap = comboTapGap;
        inputBuffer.ComboDuration = comboDuration;
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused && leftHand != null)
        {
            inputBuffer.Clear();
            pendingStrikes.Clear();
            SetRaisedGuard(false);
        }
    }

    private void Update()
    {
        if (Time.deltaTime <= 0f)
        {
            inputBuffer.Clear();
            pendingStrikes.Clear();
            SetRaisedGuard(false);
            return;
        }
        ConfigureInputWindows();
        if (readPlayerInput)
        {
            if (toggleModeAction.WasPressedThisFrame()) ToggleMode();
            inputBuffer.Advance(InputState.currentTime);
            while (inputBuffer.TryDequeue(out BoxingCommand command))
            {
                if (command.Type == BoxingCommandType.Push)
                {
                    pendingStrikes.Clear();
                    BeginPush();
                }
                else if (command.Type == BoxingCommandType.GuardStart) SetRaisedGuard(true);
                else if (command.Type == BoxingCommandType.GuardEnd) SetRaisedGuard(false);
                else if (pendingStrikes.Count < 4 && !raisedGuardHeld) pendingStrikes.Enqueue(command);
                else inputBuffer.ResetCombo();
            }
        }
        if (defensiveBlendActive)
        {
            defensiveBlendElapsed += Time.deltaTime;
            if (defensiveBlendElapsed >= guardBlendTime) defensiveBlendActive = false;
        }
        UpdateHand(leftHand, leftGuard, leftElbowHint, leftPunchElbowHint, Time.deltaTime);
        UpdateHand(rightHand, rightGuard, rightElbowHint, rightPunchElbowHint, Time.deltaTime);
        if (torsoOwner != null && !torsoOwner.motion.IsActive) torsoOwner = null;
        SetTorsoYaw(torsoOwner != null ? torsoOwner.motion.TorsoYaw
            : Mathf.MoveTowards(currentTorsoYaw, 0f, 360f * Time.deltaTime));
        StartBufferedStrike(InputState.currentTime);
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

    public void ToggleMode()
    {
        mode = mode == BoxingMode.Straight ? BoxingMode.Swing : BoxingMode.Straight;
        inputBuffer.ResetCombo();
    }
    public bool TryPunchLeft() => TryStrike(leftHand, leftGuard, -1f, PunchKind.Jab, mode);
    public bool TryPunchRight() => TryStrike(rightHand, rightGuard, 1f, PunchKind.Jab, mode);

    private void StartBufferedStrike(double now)
    {
        if (raisedGuardHeld || defensiveBlendActive || IsPunchingLeft || IsPunchingRight) return;
        while (pendingStrikes.Count > 0)
        {
            BoxingCommand command = pendingStrikes.Dequeue();
            if (now - command.Time > punchBufferTime) continue;
            PunchKind kind = command.Kind;
            if (kind == PunchKind.Power && command.Stance != CurrentStance) kind = PunchKind.Jab;
            bool left = command.Side < 0;
            TryStrike(left ? leftHand : rightHand, left ? leftGuard : rightGuard, command.Side, kind, command.Mode);
            break;
        }
    }

    private bool TryStrike(Hand hand, Vector3 guard, float side, PunchKind kind, BoxingMode shape)
    {
        if (!isActiveAndEnabled || hand == null || raisedGuardHeld || IsPunchingLeft || IsPunchingRight) return false;
        defensiveBlendActive = false;
        bool isUppercut = kind == PunchKind.Uppercut;
        bool drivesTorso = kind == PunchKind.Power || isUppercut;
        PunchProfile profile = kind == PunchKind.Power ? rearPunch : isUppercut ? uppercutPunch : leadPunch;
        GetCircle(side, out Vector3 pivot, out float angle, out float sweep);
        float radius = FittedRadius(hand.root.position, hand.upperLength + hand.lowerLength,
            pivot, swingRadius * Mathf.Max(0.1f, profile.reachMultiplier));
        Vector3 drawnBack = guard + new Vector3(side * profile.outwardDraw, 0f, -profile.pullbackDistance);
        Vector3 strike = StraightStrike(guard, profile);
        Vector3? uppercutControl = null;
        if (isUppercut)
        {
            drawnBack = guard + MirrorOffset(uppercutDipOffset, side);
            strike = guard + MirrorOffset(uppercutStrikeOffset, side) * Mathf.Max(0.1f, profile.reachMultiplier);
            uppercutControl = guard + MirrorOffset(uppercutControlOffset, side);
        }
        float windup = Mathf.Max(0f, profile.windupTime)
            + (isUppercut ? uppercutWindupTime : shape == BoxingMode.Swing ? swingWindupTime : 0f);
        float extensionTime = isUppercut ? uppercutExtensionTime : shape == BoxingMode.Straight ? straightExtensionTime : swingExtensionTime;
        float recoveryTime = isUppercut ? uppercutRecoveryTime : shape == BoxingMode.Straight ? straightRetractionTime : swingRetractionTime;
        bool started = hand.motion.Start(shape, guard, strike, drawnBack,
            pivot, radius, angle, sweep, windup,
            extensionTime * Mathf.Max(0.1f, profile.extensionTimeMultiplier),
            extensionHoldTime,
            recoveryTime * Mathf.Max(0.1f, profile.recoveryTimeMultiplier),
            drivesTorso ? side * profile.torsoWindupAngle : 0f,
            drivesTorso ? -side * profile.torsoStrikeAngle : 0f,
            uppercutControl: uppercutControl);
        if (started)
        {
            hand.hasHit = false;
            hand.previousContact = ContactPosition(hand);
            hand.kind = kind;
            hand.impulse = (isUppercut ? uppercutImpulse : shape == BoxingMode.Straight ? straightImpulse : swingImpulse)
                * Mathf.Max(0f, profile.impulseMultiplier);
            if (drivesTorso) torsoOwner = hand;
        }
        return started;
    }

    private static Vector3 MirrorOffset(Vector3 offset, float side) => new Vector3(offset.x * side, offset.y, offset.z);

    private void BeginPush()
    {
        raisedGuardHeld = defensiveBlendActive = false;
        torsoOwner = null;
        pushReceivers.Clear();
        StartPushHand(leftHand, leftGuard, -1f);
        StartPushHand(rightHand, rightGuard, 1f);
    }

    private void StartPushHand(Hand hand, Vector3 guard, float side)
    {
        Vector3 start = targetSpace.InverseTransformPoint(hand.target.position);
        hand.motion.Reset(start);
        Vector3 end = new Vector3(guard.x + side * 0.04f, pushHeight, guard.z + pushDistance);
        hand.motion.Start(BoxingMode.Straight, start, end, start, Vector3.zero, 0f, 0f, 0f,
            0f, pushExtensionTime, 0.04f, pushRecoveryTime, 0f, 0f, guard);
        hand.kind = PunchKind.Push;
        hand.impulse = pushImpulse;
        hand.hasHit = false;
        hand.previousContact = ContactPosition(hand);
    }

    public void SetRaisedGuard(bool active)
    {
        if (leftHand == null || raisedGuardHeld == active) return;
        leftDefensiveStart = targetSpace.InverseTransformPoint(leftHand.target.position);
        rightDefensiveStart = targetSpace.InverseTransformPoint(rightHand.target.position);
        pendingStrikes.Clear();
        leftHand.motion.Reset(leftGuard);
        rightHand.motion.Reset(rightGuard);
        torsoOwner = null;
        raisedGuardHeld = active;
        defensiveBlendActive = true;
        defensiveBlendElapsed = 0f;
    }

    private bool IsLeadHand(float side)
    {
        bool southpaw = stanceController != null && stanceController.Stance == BoxingStance.Southpaw;
        return side < 0f ? !southpaw : southpaw;
    }

    private float FittedRadius(Vector3 shoulder, float armLength, Vector3 pivot, float requestedRadius)
    {
        // Fit the whole circle once, preserving its shape instead of clipping each point.
        float available = armLength * reachLimit - Vector3.Distance(shoulder, targetSpace.TransformPoint(pivot));
        return Mathf.Min(requestedRadius, Mathf.Max(0.01f, available / Mathf.Max(0.001f, Mathf.Abs(targetSpace.lossyScale.x))));
    }

    private void GetCircle(float side, out Vector3 pivot, out float angle, out float sweep)
    {
        pivot = swingPivot;
        pivot.x *= -side;
        angle = swingStartAngle * -side;
        sweep = swingSweepAngle * -side;
    }

    private Vector3 StraightStrike(Vector3 guard, PunchProfile profile) => new Vector3(guard.x,
        straightStrikeHeight, guard.z + straightDistance * Mathf.Max(0.1f, profile.reachMultiplier));

    private void SetTorsoYaw(float yaw)
    {
        currentTorsoYaw = yaw;
        if (torsoTwist == null) return;
        var data = torsoTwist.data;
        // Pivot mode adds to the Animator pose instead of replacing its stance.
        data.rotation = new Vector3(0f, yaw, 0f);
        torsoTwist.data = data;
    }

    private void UpdateHand(Hand hand, Vector3 guard, Vector3 elbowHint, Vector3 punchElbowHint, float deltaTime)
    {
        hand.motion.Advance(deltaTime);
        if (!hand.motion.IsActive && !hand.motion.AttackedThisStep) hand.motion.Reset(guard);
        Vector3 localGoal = hand.motion.Position;
        if (!hand.motion.IsActive && !hand.motion.AttackedThisStep)
        {
            localGoal = raisedGuardHeld ? (hand == leftHand ? raisedLeftGuard : raisedRightGuard) : guard;
            if (defensiveBlendActive)
                localGoal = Vector3.Lerp(hand == leftHand ? leftDefensiveStart : rightDefensiveStart,
                    localGoal, Mathf.SmoothStep(0f, 1f, defensiveBlendElapsed / Mathf.Max(0.01f, guardBlendTime)));
        }
        Vector3 goal = targetSpace.TransformPoint(localGoal);
        Vector3 delta = goal - hand.root.position;
        float distance = Mathf.Clamp(delta.magnitude,
            Mathf.Abs(hand.upperLength - hand.lowerLength) + 0.001f,
            (hand.upperLength + hand.lowerLength) * reachLimit);
        Vector3 direction = delta.sqrMagnitude > 0.000001f ? delta.normalized : targetSpace.forward;
        goal = hand.root.position + direction * distance;
        hand.target.position = goal;

        Vector3 activeElbowHint = hand.kind == PunchKind.Uppercut ? elbowHint + new Vector3(0f, 0.1f, 0.12f) : punchElbowHint;
        Vector3 hint = targetSpace.TransformPoint(Vector3.Lerp(elbowHint, activeElbowHint, hand.motion.PoseWeight)
            + (localGoal - guard) * (0.35f * hand.motion.PoseWeight)
            + (raisedGuardHeld ? Vector3.up * 0.15f : Vector3.zero));
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
                if (receiver != null && (hand.kind != PunchKind.Push || pushReceivers.Add(receiver)))
                {
                    Vector3 direction = hand.kind == PunchKind.Push ? targetSpace.forward
                        : travel.sqrMagnitude > 0.00001f ? travel.normalized : targetSpace.forward;
                    if (hand.kind == PunchKind.Push)
                    {
                        direction = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
                        point = closest.ClosestPoint((ContactPosition(leftHand) + ContactPosition(rightHand)) * 0.5f);
                    }
                    receiver.ReceivePunch(new PunchHit(point, direction, hand.impulse, hand.motion.Mode, hand.kind));
                }
            }
        }
        hand.previousContact = contact;
    }

    private void ResetTargets()
    {
        inputBuffer.Clear();
        pendingStrikes.Clear();
        pushReceivers.Clear();
        raisedGuardHeld = defensiveBlendActive = false;
        leftHand.motion.Reset(leftGuard); rightHand.motion.Reset(rightGuard);
        torsoOwner = null;
        SetTorsoYaw(0f);
        UpdateHand(leftHand, leftGuard, leftElbowHint, leftPunchElbowHint, 0f);
        UpdateHand(rightHand, rightGuard, rightElbowHint, rightPunchElbowHint, 0f);
        leftHand.previousContact = ContactPosition(leftHand);
        rightHand.previousContact = ContactPosition(rightHand);
    }

    private void OnGUI()
    {
        if (showModeLabel)
        {
            string left = leftPunchAction.GetBindingDisplayString();
            string right = rightPunchAction.GetBindingDisplayString();
            string combo = IsLeadHand(-1f) ? $"{left}-{left}-{right}" : $"{right}-{right}-{left}";
            GUI.Box(new Rect(12f, 12f, 460f, 72f),
                $"Boxing: {mode}{(raisedGuardHeld ? " | GUARD" : "")} | {toggleModeAction.GetBindingDisplayString()}: mode\nLight: {left} / {right} | Power: {combo}\nBoth: {(mode == BoxingMode.Straight ? "push" : "hold guard")} | {uppercutAction.GetBindingDisplayString()} then click: uppercut");
        }
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
        PunchProfile profile = leadPunch;
        float radius = swingRadius * Mathf.Max(0.1f, profile.reachMultiplier);
        var constraint = side < 0f ? leftArmIK : rightArmIK;
        if (constraint != null && IsArmValid(constraint))
        {
            var data = constraint.data;
            radius = FittedRadius(data.root.position, Vector3.Distance(data.root.position, data.mid.position)
                + Vector3.Distance(data.mid.position, data.tip.position), pivot, radius);
        }
        for (int i = 0; i <= 40; i++)
        {
            float t = i / 40f;
            Vector3 local = mode == BoxingMode.Straight ? Vector3.Lerp(guard, StraightStrike(guard, profile), t)
                : PunchMotion.CirclePoint(pivot, radius, angle + sweep * t);
            Vector3 point = targetSpace.TransformPoint(local);
            Gizmos.DrawLine(previous, point);
            previous = point;
        }
    }
}
