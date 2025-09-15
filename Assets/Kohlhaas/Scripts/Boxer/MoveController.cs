using UnityEngine;
using UnityEngine.InputSystem;

public class MoveController : MonoBehaviour
{

    [Header("Input")]
    public InputActionProperty MoveAction;
    public InputActionProperty LookPosAction;

    [Header("Movement Settings")]
    public float speed = 5f;

    [Header("References")]
    [SerializeField] private Camera MainCamera;
    [SerializeField] private Animator animator;

    private Vector2 moveInput;
    private Vector3 moveVector;

    private void OnEnable()
    {
        MoveAction.action.Enable();
        LookPosAction.action.Enable();
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        moveInput = MoveAction.action.ReadValue<Vector2>();
        if (moveInput.sqrMagnitude < 0.01f)
        {
            moveInput = Vector2.zero;
        }
        else
        {
            moveInput = moveInput.normalized;
        }

        // Movement
        moveVector = new Vector3(moveInput.x, 0, moveInput.y);
        Vector3 move = moveVector * speed * Time.deltaTime;
        transform.position += move;
        SetBlendParams(moveVector);

        // Rotation
        Ray ray = MainCamera.ScreenPointToRay(LookPosAction.action.ReadValue<Vector2>());
        Plane ground = new Plane(Vector3.up, Vector3.zero);
        if (ground.Raycast(ray, out float dist))
        {
            Vector3 lookPoint = ray.GetPoint(dist);
            Vector3 dir = lookPoint - transform.position;
            dir.y = 0;
            if (dir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(dir.normalized);
        }
    }

    private void SetBlendParams(Vector3 moveDirWorld)
    {
        Vector3 moveLocal = transform.InverseTransformDirection(moveDirWorld);
        animator.SetFloat("forwardX", moveLocal.x);
        animator.SetFloat("forwardZ", moveLocal.z);
    }
}
