using UnityEngine;
using UnityEngine.InputSystem;

public enum BoxingStance { Orthodox, Southpaw }

[DisallowMultipleComponent]
[DefaultExecutionOrder(-90)]
public sealed class BoxingStanceController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [Tooltip("The child model containing the Animator and rig, separate from the Player movement root.")]
    [SerializeField] private Transform visualRoot;

    [Header("Stance")]
    [Tooltip("Orthodox: left foot forward. Southpaw: right foot forward.")]
    [SerializeField] private BoxingStance stance = BoxingStance.Orthodox;
    [SerializeField] private bool showStanceLabel = true;

    [Header("Stance Switch Hop")]
    [SerializeField, Min(0f)] private float hopHeight = 0.12f;
    [SerializeField, Min(0.12f)] private float hopDuration = 0.28f;

    [Header("Switch Gesture (left-right-left or right-left-right)")]
    [SerializeField] private InputAction leftTap = new InputAction("Left Stance Tap", InputActionType.Button, "<Keyboard>/a");
    [SerializeField] private InputAction rightTap = new InputAction("Right Stance Tap", InputActionType.Button, "<Keyboard>/d");
    [Tooltip("Maximum seconds between successive key presses. Holding a key does not repeat a tap.")]
    [SerializeField, Min(0.05f)] private float maximumTapGap = 0.22f;
    [Tooltip("Maximum seconds from the first press to the third press.")]
    [SerializeField, Min(0.1f)] private float maximumSequenceTime = 0.5f;
    [SerializeField, Min(0f)] private float switchCooldown = 0.3f;
    [SerializeField] private bool readPlayerInput = true;

    public BoxingStance Stance => stance;
    public bool IsSwitching => isHopping;

    private static readonly int RightFootForward = Animator.StringToHash("rightFootForward");
    private static readonly int ForwardX = Animator.StringToHash("forwardX");
    private static readonly int MirroredForwardX = Animator.StringToHash("mirroredForwardX");
    private static readonly int OrthodoxState = Animator.StringToHash("Base Layer.Movement");
    private static readonly int SouthpawState = Animator.StringToHash("Base Layer.MovementSouthpaw");
    private int tapCount;
    private int previousSide;
    private double sequenceStart, previousTap;
    private double lastSwitch = double.NegativeInfinity;
    private bool controllerReady;
    private bool needsInitialPose = true;
    private bool isHopping, swapStarted;
    private float hopElapsed, activeHopDuration, activeHopHeight;
    private Vector3 visualRestPosition;
    private BoxingStance appliedStance, hopDestination;

    private void Reset()
    {
        animator = GetComponentInChildren<Animator>();
        if (animator != null) visualRoot = animator.transform;
    }

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator == null || !animator.isHuman || !HasRequiredParameters())
        {
            Debug.LogError("BoxingStanceController needs a humanoid Animator with the CombatLocomotion controller.", this);
            enabled = false;
            return;
        }
        if (visualRoot == null) visualRoot = animator.transform;
        if (visualRoot == transform || !visualRoot.IsChildOf(transform)
            || !animator.transform.IsChildOf(visualRoot))
        {
            Debug.LogError("BoxingStanceController Visual Root must be the child model containing the Animator and rig.", this);
            enabled = false;
            return;
        }
        visualRestPosition = visualRoot.localPosition;
        controllerReady = true;
        leftTap.performed += OnLeftTap;
        rightTap.performed += OnRightTap;
    }

    private bool HasRequiredParameters()
    {
        bool stanceParameter = false, movementParameter = false, mirroredParameter = false;
        foreach (var parameter in animator.parameters)
        {
            stanceParameter |= parameter.nameHash == RightFootForward && parameter.type == AnimatorControllerParameterType.Bool;
            movementParameter |= parameter.nameHash == ForwardX && parameter.type == AnimatorControllerParameterType.Float;
            mirroredParameter |= parameter.nameHash == MirroredForwardX && parameter.type == AnimatorControllerParameterType.Float;
        }
        return stanceParameter && movementParameter && mirroredParameter;
    }

    private void OnEnable()
    {
        ResetSequence();
        leftTap?.Enable();
        rightTap?.Enable();
        needsInitialPose = true;
    }

    private void OnDisable()
    {
        leftTap?.Disable();
        rightTap?.Disable();
        ResetSequence();
        isHopping = false;
        if (controllerReady) SetVisualHeight(0f);
    }

    private void OnDestroy()
    {
        leftTap?.Dispose();
        rightTap?.Dispose();
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused) ResetSequence();
    }

    private void Update()
    {
        if (!controllerReady) return;
        if (Time.timeScale <= 0f)
        {
            ResetSequence();
            return;
        }
        // Mirroring swaps left/right animation motion as well as the feet.
        // A separate parameter keeps both blend trees correct during the crossfade.
        animator.SetFloat(MirroredForwardX, -animator.GetFloat(ForwardX));
        if (needsInitialPose)
        {
            // Initial load / re-enable should begin planted in the selected stance.
            appliedStance = stance;
            animator.Play(StateHash(stance), 0, 0f);
            animator.SetBool(RightFootForward, stance == BoxingStance.Southpaw);
            SetVisualHeight(0f);
            needsInitialPose = false;
            return;
        }
        if (!isHopping && stance != appliedStance) BeginHop();
        if (isHopping) UpdateHop(Time.deltaTime);
    }

    private void OnLeftTap(InputAction.CallbackContext context) => RecordTap(-1, context.time);
    private void OnRightTap(InputAction.CallbackContext context) => RecordTap(1, context.time);

    private void RecordTap(int side, double timestamp)
    {
        if (!readPlayerInput || !controllerReady || Time.timeScale <= 0f)
        {
            ResetSequence();
            return;
        }
        if (isHopping || timestamp - lastSwitch < switchCooldown) return;
        if (tapCount == 0 || timestamp - previousTap > maximumTapGap
            || timestamp - sequenceStart > maximumSequenceTime || side == previousSide)
        {
            tapCount = 1;
            sequenceStart = timestamp;
        }
        else
        {
            tapCount++;
        }
        previousSide = side;
        previousTap = timestamp;
        if (tapCount == 3)
        {
            lastSwitch = timestamp;
            ToggleStance();
        }
    }

    public void ToggleStance() => SetStance(stance == BoxingStance.Orthodox ? BoxingStance.Southpaw : BoxingStance.Orthodox);

    public void SetStance(BoxingStance newStance)
    {
        stance = newStance;
        ResetSequence();
    }

    private static int StateHash(BoxingStance value) => value == BoxingStance.Southpaw ? SouthpawState : OrthodoxState;

    private void BeginHop()
    {
        isHopping = true;
        swapStarted = false;
        hopElapsed = 0f;
        activeHopDuration = Mathf.Max(0.12f, hopDuration);
        activeHopHeight = Mathf.Max(0f, hopHeight);
        hopDestination = stance;
    }

    private void UpdateHop(float deltaTime)
    {
        hopElapsed += deltaTime;
        float t = Mathf.Clamp01(hopElapsed / activeHopDuration);
        // Ballistic-looking rise and fall: zero offset at takeoff and landing.
        SetVisualHeight(4f * activeHopHeight * t * (1f - t));

        if (!swapStarted && t >= 0.12f)
        {
            swapStarted = true;
            appliedStance = hopDestination;
            animator.SetBool(RightFootForward, appliedStance == BoxingStance.Southpaw);
            if (t >= 1f)
            {
                // A stalled frame must still finish in the correct planted stance.
                animator.Play(StateHash(appliedStance), 0, 0f);
            }
            else
            {
                // Swap after takeoff and finish before landing. Code owns the
                // crossfade so its duration follows the adjustable hop duration.
                float blendTime = Mathf.Min(activeHopDuration * 0.68f, activeHopDuration - hopElapsed);
                animator.CrossFadeInFixedTime(StateHash(appliedStance), blendTime, 0, 0f);
            }
        }
        if (t >= 1f)
        {
            isHopping = false;
            SetVisualHeight(0f);
        }
    }

    private void SetVisualHeight(float height)
    {
        if (visualRoot == null) return;
        Vector3 localOffset = visualRoot.parent != null
            ? visualRoot.parent.InverseTransformVector(Vector3.up * height) : Vector3.up * height;
        // Moving the model also moves the arm rig and targets before BoxingController.Update.
        visualRoot.localPosition = visualRestPosition + localOffset;
    }

    private void ResetSequence()
    {
        tapCount = 0;
        previousSide = 0;
    }

    private void OnGUI()
    {
        if (!showStanceLabel || !controllerReady) return;
        string left = leftTap.GetBindingDisplayString();
        string right = rightTap.GetBindingDisplayString();
        string foot = stance == BoxingStance.Southpaw ? "right" : "left";
        GUI.Box(new Rect(12f, 172f, 400f, 52f),
            $"Stance: {stance} ({foot} foot forward)\n{left}-{right}-{left} / {right}-{left}-{right}: switch stance");
    }
}
