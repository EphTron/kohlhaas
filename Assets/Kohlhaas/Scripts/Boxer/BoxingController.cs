using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class BoxerController : MonoBehaviour
{
    [Header("Input")]
    public InputActionProperty MoveAction;
    public InputActionProperty LookPosAction;
    public InputActionProperty RunAction;
    public InputActionProperty DashAction;
    public InputActionProperty LeftHookAction;
    public InputActionProperty RightHookAction;
    public InputActionProperty UppercutAction;

    [Header("References")]
    public Camera MainCamera;
    [SerializeField] private Animator animator;

    [Header("Movement Settings")]
    public float WalkSpeed = 5f;
    public float RunSpeed = 8f;
    public float FightSpeed = 3f;
    public float DashDistance = 3f;
    public float DashDuration = 0.2f;
    public float FightCooldown = 3f;
    public float PunchDuration = 0.4f;

    private bool isRunning = false;
    private bool isFighting = false;
    private bool isPunching = false;
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
        RunAction.action.Enable();
        DashAction.action.Enable();
        LeftHookAction.action.Enable();
        RightHookAction.action.Enable();
        UppercutAction.action.Enable();

        RunAction.action.started += _ => StartRun();
        RunAction.action.canceled += _ => StopRun();
        DashAction.action.performed += _ => Dash();
        LeftHookAction.action.performed += _ => TriggerFight(PunchType.LeftHook);
        RightHookAction.action.performed += _ => TriggerFight(PunchType.RightHook);
        UppercutAction.action.performed += _ => TriggerFight(PunchType.Uppercut);
    }

    private void OnDisable()
    {
        MoveAction.action.Disable();
        LookPosAction.action.Disable();
        RunAction.action.Disable();
        DashAction.action.Disable();
        LeftHookAction.action.Disable();
        RightHookAction.action.Disable();
        UppercutAction.action.Disable();
    }

    private void Update()
    {
        if (isPunching || isDashing)
        {
            SetBlendParams(Vector3.zero);
            return;
        }

        moveInput = MoveAction.action.ReadValue<Vector2>();
        if (moveInput.sqrMagnitude < 0.01f) {
            moveInput = Vector2.zero; 
        }
        else
        {
            moveInput = moveInput.normalized;
        }

        // Speed
        float speed = WalkSpeed;
        if (isFighting)
        {
            speed = FightSpeed;
            if (Time.time - lastFightTime > FightCooldown)
                isFighting = false;
        }
        else if (isRunning)
        {
            speed = RunSpeed;
        }

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

    private void StartRun()
    {
        if (isFighting)
            isFighting = false;

        isRunning = true;
    }

    private void StopRun()
    {
        isRunning = false;
    }

    private void TriggerFight(PunchType type)
    {
        Debug.Log("Punch: " + type.ToString());
        isFighting = true;
        lastFightTime = Time.time;

        if (!isPunching)
            StartCoroutine(PunchCoroutine(type));
    }

    private IEnumerator PunchCoroutine(PunchType type)
    {
        isPunching = true;

        switch (type)
        {
            case PunchType.LeftHook:
                animator.SetTrigger("LeftHook");
                break;
            case PunchType.RightHook:
                animator.SetTrigger("RightHook");
                break;
            case PunchType.Uppercut:
                animator.SetTrigger("Uppercut");
                break;
        }

        yield return new WaitForSeconds(PunchDuration);
        isPunching = false;
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
