using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(CharacterController))]
public sealed class BoxerMoveController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Animator animator;

    [Header("Movement")]
    [SerializeField, Min(0.1f)] private float moveSpeed = 5f;
    [SerializeField, Min(0f)] private float animationDamping = 0.08f;
    [SerializeField, Min(0f)] private float aimDeadZone = 0.15f;
    [SerializeField, Min(0.1f)] private float locomotionAnimationSpeed = 1.5f;

    private static readonly int ForwardX = Animator.StringToHash("forwardX");
    private static readonly int ForwardZ = Animator.StringToHash("forwardZ");
    private static readonly int LocomotionSpeed = Animator.StringToHash("locomotionSpeed");

    private CharacterController characterController;
    private InputAction moveAction;
    private InputAction aimAction;
    private float verticalSpeed;

    private void Reset()
    {
        mainCamera = Camera.main;
        animator = GetComponentInChildren<Animator>();
    }

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        if (mainCamera == null) mainCamera = Camera.main;
        if (animator == null) animator = GetComponentInChildren<Animator>();

        if (mainCamera == null || animator == null)
        {
            Debug.LogError("BoxerMoveController requires a camera and a child Animator.", this);
            enabled = false;
            return;
        }

        animator.applyRootMotion = false;

        // Own these actions so enabling a boxer cannot disable another actor's input.
        moveAction = new InputAction("Move", InputActionType.Value);
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
        aimAction = new InputAction("Aim", InputActionType.Value, "<Mouse>/position");
    }

    private void OnEnable()
    {
        moveAction?.Enable();
        aimAction?.Enable();
    }

    private void OnDisable()
    {
        moveAction?.Disable();
        aimAction?.Disable();
        verticalSpeed = 0f;
        if (animator != null)
        {
            animator.SetFloat(ForwardX, 0f);
            animator.SetFloat(ForwardZ, 0f);
            animator.SetFloat(LocomotionSpeed, 1f);
        }
    }

    private void OnDestroy()
    {
        moveAction?.Dispose();
        aimAction?.Dispose();
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f || !characterController.enabled) return;

        FaceMouse();

        Vector2 input = Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);
        Vector3 velocity = new Vector3(input.x, 0f, input.y) * moveSpeed;
        if (characterController.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
        verticalSpeed += Physics.gravity.y * deltaTime;
        velocity.y = verticalSpeed;

        Vector3 startPosition = transform.position;
        characterController.Move(velocity * deltaTime);

        // Drive strafing relative to facing, using distance actually travelled.
        Vector3 displacement = transform.position - startPosition;
        displacement.y = 0f;
        Vector3 localMovement = transform.InverseTransformDirection(displacement)
            / (Mathf.Max(0.1f, moveSpeed) * deltaTime);
        localMovement = Vector3.ClampMagnitude(localMovement, 1f);
        animator.SetFloat(ForwardX, localMovement.x, animationDamping, deltaTime);
        animator.SetFloat(ForwardZ, localMovement.z, animationDamping, deltaTime);
        animator.SetFloat(LocomotionSpeed,
            Mathf.Lerp(1f, locomotionAnimationSpeed, localMovement.magnitude),
            animationDamping, deltaTime);
    }

    private void FaceMouse()
    {
        if (Mouse.current == null || mainCamera == null) return;

        Ray ray = mainCamera.ScreenPointToRay(aimAction.ReadValue<Vector2>());
        Plane movementPlane = new Plane(Vector3.up, transform.position);
        if (!movementPlane.Raycast(ray, out float distance)) return;

        Vector3 direction = ray.GetPoint(distance) - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > aimDeadZone * aimDeadZone)
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
    }
}
