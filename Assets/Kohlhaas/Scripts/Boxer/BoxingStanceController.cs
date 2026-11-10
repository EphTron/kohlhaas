using UnityEngine;
using UnityEngine.InputSystem;

public enum BoxingStance { Orthodox, Southpaw }

[DisallowMultipleComponent]
[DefaultExecutionOrder(-90)]
public sealed class BoxingStanceController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;

    [Header("Stance")]
    [Tooltip("Orthodox: left foot forward. Southpaw: right foot forward.")]
    [SerializeField] private BoxingStance stance = BoxingStance.Orthodox;
    [SerializeField] private bool showStanceLabel = true;

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

    private static readonly int RightFootForward = Animator.StringToHash("rightFootForward");
    private static readonly int ForwardX = Animator.StringToHash("forwardX");
    private static readonly int MirroredForwardX = Animator.StringToHash("mirroredForwardX");
    private int tapCount;
    private int previousSide;
    private double sequenceStart, previousTap;
    private double lastSwitch = double.NegativeInfinity;
    private bool controllerReady;

    private void Reset() => animator = GetComponentInChildren<Animator>();

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator == null || !animator.isHuman || !HasRequiredParameters())
        {
            Debug.LogError("BoxingStanceController needs a humanoid Animator with the CombatLocomotion controller.", this);
            enabled = false;
            return;
        }
        controllerReady = true;
        leftTap.performed += OnLeftTap;
        rightTap.performed += OnRightTap;
        ApplyStance();
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
        ApplyStance();
    }

    private void OnDisable()
    {
        leftTap?.Disable();
        rightTap?.Disable();
        ResetSequence();
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
        ApplyStance();
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
        if (timestamp - lastSwitch < switchCooldown) return;
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
        ApplyStance();
    }

    private void ApplyStance()
    {
        if (controllerReady && animator != null)
            animator.SetBool(RightFootForward, stance == BoxingStance.Southpaw);
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
