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

    [Header("Guard (relative to Target Space)")]
    [SerializeField] private Vector3 leftGuard = new Vector3(-0.18f, 1.4f, 0.28f);
    [SerializeField] private Vector3 rightGuard = new Vector3(0.18f, 1.4f, 0.28f);

    [Header("Straight Punch")]
    [SerializeField, Min(0f)] private float straightDistance = 0.38f;
    [SerializeField, Min(0.01f)] private float straightExtensionTime = 0.075f;
    [SerializeField, Min(0.01f)] private float straightRetractionTime = 0.13f;

    [Header("Swing Punch")]
    [SerializeField, Min(0f)] private float swingDistance = 0.35f;
    [SerializeField, Min(0f)] private float swingWidth = 0.3f;
    [SerializeField, Min(0f)] private float swingInwardDistance = 0.25f;
    [SerializeField, Min(0.01f)] private float swingExtensionTime = 0.12f;
    [SerializeField, Min(0.01f)] private float swingRetractionTime = 0.18f;

    [Header("Controls")]
    [SerializeField] private BoxingMode mode = BoxingMode.Straight;
    [SerializeField, Min(0f)] private float extensionHoldTime = 0.025f;
    [SerializeField] private bool readPlayerInput = true;
    [SerializeField] private bool showModeLabel = true;

    public BoxingMode Mode => mode;
    public bool IsPunchingLeft => leftHand != null && leftHand.motion.IsActive;
    public bool IsPunchingRight => rightHand != null && rightHand.motion.IsActive;

    private sealed class Hand
    {
        public readonly Transform root;
        public readonly Transform target;
        public readonly float length;
        public readonly PunchMotion motion = new PunchMotion();

        public Hand(TwoBoneIKConstraint constraint)
        {
            var data = constraint.data;
            root = data.root;
            target = data.target;
            length = Vector3.Distance(data.root.position, data.mid.position)
                + Vector3.Distance(data.mid.position, data.tip.position);
        }
    }

    private Hand leftHand;
    private Hand rightHand;
    private InputAction leftPunchAction;
    private InputAction rightPunchAction;
    private InputAction toggleModeAction;

    private void Awake()
    {
        if (targetSpace == null || !IsArmValid(leftArmIK) || !IsArmValid(rightArmIK))
        {
            Debug.LogError("BoxingController requires Target Space and two complete Two Bone IK constraints.", this);
            enabled = false;
            return;
        }

        leftHand = new Hand(leftArmIK);
        rightHand = new Hand(rightArmIK);
        ResetTargets();

        leftPunchAction = new InputAction("Left Punch", InputActionType.Button, "<Mouse>/leftButton");
        rightPunchAction = new InputAction("Right Punch", InputActionType.Button, "<Mouse>/rightButton");
        toggleModeAction = new InputAction("Boxing Mode", InputActionType.Button);
        toggleModeAction.AddBinding("<Keyboard>/leftCtrl");
        toggleModeAction.AddBinding("<Keyboard>/rightCtrl");
    }

    private static bool IsArmValid(TwoBoneIKConstraint constraint)
    {
        if (constraint == null) return false;
        var data = constraint.data;
        return data.root != null && data.mid != null && data.tip != null && data.target != null
            && data.mid.IsChildOf(data.root) && data.tip.IsChildOf(data.mid);
    }

    private void OnEnable()
    {
        leftPunchAction?.Enable();
        rightPunchAction?.Enable();
        toggleModeAction?.Enable();
        if (leftHand == null) return;
        leftArmIK.weight = 1f;
        rightArmIK.weight = 1f;
        ResetTargets();
    }

    private void OnDisable()
    {
        leftPunchAction?.Disable();
        rightPunchAction?.Disable();
        toggleModeAction?.Disable();
        if (leftHand == null) return;
        ResetTargets();
        leftArmIK.weight = 0f;
        rightArmIK.weight = 0f;
    }

    private void OnDestroy()
    {
        leftPunchAction?.Dispose();
        rightPunchAction?.Dispose();
        toggleModeAction?.Dispose();
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

        UpdateHand(leftHand, leftGuard, Time.deltaTime);
        UpdateHand(rightHand, rightGuard, Time.deltaTime);
    }

    public void ToggleMode()
    {
        mode = mode == BoxingMode.Straight ? BoxingMode.Swing : BoxingMode.Straight;
    }

    public bool TryPunchLeft() => TryPunch(leftHand, leftGuard, -1f);
    public bool TryPunchRight() => TryPunch(rightHand, rightGuard, 1f);

    private bool TryPunch(Hand hand, Vector3 guard, float side)
    {
        if (!isActiveAndEnabled || hand == null) return false;
        GetPath(mode, guard, side, out Vector3 strike, out Vector3 control);
        return hand.motion.Start(mode, guard, strike, control,
            mode == BoxingMode.Straight ? straightExtensionTime : swingExtensionTime,
            extensionHoldTime,
            mode == BoxingMode.Straight ? straightRetractionTime : swingRetractionTime);
    }

    private void GetPath(BoxingMode punchMode, Vector3 guard, float side,
        out Vector3 strike, out Vector3 control)
    {
        float distance = punchMode == BoxingMode.Straight ? straightDistance : swingDistance;
        strike = guard + Vector3.forward * distance;
        if (punchMode == BoxingMode.Swing) strike.x -= side * swingInwardDistance;
        control = (guard + strike) * 0.5f + Vector3.right * side * swingWidth;
    }

    private void UpdateHand(Hand hand, Vector3 guard, float deltaTime)
    {
        if (hand.motion.IsActive) hand.motion.Advance(deltaTime);
        else hand.motion.Reset(guard);

        Vector3 goal = targetSpace.TransformPoint(hand.motion.Position);
        // Avoid a locked, fully extended elbow when a target lies beyond arm reach.
        goal = hand.root.position + Vector3.ClampMagnitude(goal - hand.root.position, hand.length * reachLimit);
        hand.target.position = goal;
    }

    private void ResetTargets()
    {
        leftHand.motion.Reset(leftGuard);
        rightHand.motion.Reset(rightGuard);
        UpdateHand(leftHand, leftGuard, 0f);
        UpdateHand(rightHand, rightGuard, 0f);
    }

    private void OnGUI()
    {
        if (showModeLabel)
            GUI.Box(new Rect(12f, 12f, 360f, 52f), $"Boxing: {mode}\nLMB / RMB: punch     Ctrl: change mode");
    }

    private void OnDrawGizmosSelected()
    {
        if (targetSpace == null) return;
        DrawPath(leftGuard, -1f, Color.yellow);
        DrawPath(rightGuard, 1f, Color.cyan);
    }

    private void DrawPath(Vector3 guard, float side, Color color)
    {
        GetPath(mode, guard, side, out Vector3 strike, out Vector3 control);
        Gizmos.color = color;
        Vector3 previous = targetSpace.TransformPoint(guard);
        for (int i = 1; i <= 20; i++)
        {
            Vector3 point = targetSpace.TransformPoint(PunchMotion.PointOnPath(mode, guard, strike, control, i / 20f));
            Gizmos.DrawLine(previous, point);
            previous = point;
        }
    }
}
