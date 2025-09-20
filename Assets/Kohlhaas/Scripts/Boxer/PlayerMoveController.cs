using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class BoxerMoveController : MonoBehaviour
{
    [Header("Input")]
    public InputActionProperty MoveAction;
    public InputActionProperty LookPosAction;
    public InputActionProperty DashAction;

    [Header("References")]
    public Camera MainCamera;
    [SerializeField] private Animator animator;

    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float DashDistance = 3f;
    public float DashDuration = 0.2f;
    
    private bool isDashing = false;

    private float lastFightTime;
    private Vector2 moveInput;
    private Vector3 moveVector;
    private float currentSpeed;

    private enum PunchType { LeftHook, RightHook, Uppercut }

    private void OnEnable()
    {
        MoveAction.action.Enable();
        LookPosAction.action.Enable();
       
        DashAction.action.Enable();
       

      
        DashAction.action.performed += _ => Dash();
      
    }

    private void OnDisable()
    {
       
    }

    private void Update()
    {
        if (isDashing)
        {    
            return;
        }

        moveInput = MoveAction.action.ReadValue<Vector2>();
        if (moveInput.sqrMagnitude < 0.01f)
        {
            moveInput = Vector2.zero;
        }
        else
        {
            moveInput = moveInput.normalized;
        }

        // Speed
        float speed = moveSpeed;
       
        // Movement
        moveVector = new Vector3(moveInput.x, 0, moveInput.y);
        Vector3 move = moveVector * speed * Time.deltaTime;
        transform.position += move;
        currentSpeed = move.magnitude / Time.deltaTime;

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

        // Anim Blend Tree
        SetBlendParams(moveVector);
    }

    private void SetBlendParams(Vector3 moveDirWorld)
    {
        Vector3 moveLocal = transform.InverseTransformDirection(moveDirWorld);
        animator.SetFloat("forwardX", moveLocal.x);
        animator.SetFloat("forwardZ", moveLocal.z);
    }

    

    
    private void Dash()
    {
        if (!isDashing)
            StartCoroutine(DashCoroutine());
    }

    private IEnumerator DashCoroutine()
    {
        isDashing = true;

        Vector3 start = transform.position;
        Vector3 end = start + transform.forward * DashDistance;
        float elapsed = 0f;

        while (elapsed < DashDuration)
        {
            float t = elapsed / DashDuration;
            transform.position = Vector3.Lerp(start, end, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = end;
        isDashing = false;
    }
}
