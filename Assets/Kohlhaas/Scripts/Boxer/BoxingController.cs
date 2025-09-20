using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class BoxerController : MonoBehaviour
{
    [Header("Input")]
    public InputActionProperty MoveAction;
    public InputActionProperty LookPosAction;
    public InputActionProperty DashAction;
    public InputActionProperty LeftHookAction;
    public InputActionProperty RightHookAction;
    public InputActionProperty UppercutAction;

    [Header("References")]
    public Camera MainCamera;
    public Transform head;
    public Transform leftShoulder;
    public Transform rightShoulder;
    public Transform leftGloveTip;
    public Transform rightGloveTip;
    public Transform leftHandTarget;
    public Transform rightHandTarget;
    [SerializeField] private Animator animator;

    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float headHeight = 1.6f;

    public float dashDistance = 3f;
    public float dashDuration = 0.2f;
    private bool isDashing = false;

    public float punchDurationLeft = 0.2f;
    private bool isPunchingLeft = false;

    public float punchDurationRight = 0.2f;
    private bool isPunchingRight = false;

    private Vector3 targetPoint;
    private Vector2 moveInput;
    private Vector3 moveVector;
    private Vector3 leftHandStartPos;
    private Vector3 rightHandStartPos;
    
    private float currentSpeed;
    private float maxReachFromCenterLeft = 0;
    private float maxReachFromCenterRight = 0;

    // calibration to get max reach
    private bool isCalibratedLeft = false;
    private bool isCalibratedRight = false;
    private float lastDistanceLeft = 0f;
    private float lastDistanceRight = 0f;
    private float threshhold = 0.005f;
    public float stepSizeLeft = 0.02f;
    public float stepSizeRight = 0.02f;
    private float originalStepSize = 0.02f;

    ReachCalibration reachCalib;

    private enum PunchType { LeftHook, RightHook, Uppercut }


    private void OnEnable()
    {
        MoveAction.action.Enable();
        LookPosAction.action.Enable();
        DashAction.action.Enable();
        LeftHookAction.action.Enable();
        RightHookAction.action.Enable();
        UppercutAction.action.Enable();

        DashAction.action.performed += _ => Dash();
        LeftHookAction.action.performed += _ => TriggerFight(PunchType.LeftHook);
        RightHookAction.action.performed += _ => TriggerFight(PunchType.RightHook);
        UppercutAction.action.performed += _ => TriggerFight(PunchType.Uppercut);
    }

    private void OnDisable()
    {
        MoveAction.action.Disable();
        LookPosAction.action.Disable();
        DashAction.action.Disable();
        LeftHookAction.action.Disable();
        RightHookAction.action.Disable();
        UppercutAction.action.Disable();
    }

    void Awake()
    {
        reachCalib = new ReachCalibration(
            transform, leftHandTarget, rightHandTarget,
            leftGloveTip, rightGloveTip,
            headHeight: headHeight, startDist: 0.30f, expandStep: 0.08f, maxProbeDist: 2.0f, reachTolerance: 0.015f
        );

        float distance = DistanceToParent(leftGloveTip, leftShoulder);
        Debug.Log("Distance Shoulder to tip "+ distance);
    }

    private void Start()
    {
        headHeight = head.position.y;
    }

    private void Update()
    {
        if (!isCalibratedLeft)
        {
            CalibrateLeft();
        //    reachCalib.Begin();
        }

        if (!isCalibratedRight)
        {
            CalibrateRight();
            //    reachCalib.Begin();
        }
        //if (reachCalib.IsRunning)
        //{
        //    reachCalib.Tick();
        //    return; // skip normal update while calibrating
        //} else 
        //{
        //    isCalibrated = true;
        //    maxReachFromCenterLeft = reachCalib.MaxLeft;
        //    maxReachFromCenterRight = reachCalib.MaxRight;
        //    Debug.Log("Calibration complete. Max Left Reach: " + maxReachFromCenterLeft + ", Max Right Reach: " + maxReachFromCenterRight);
        //}


        moveInput = MoveAction.action.ReadValue<Vector2>();
        if (moveInput.sqrMagnitude < 0.01f) {
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
            targetPoint = ray.GetPoint(dist);
            Vector3 dir = targetPoint - transform.position;
            dir.y = 0;
            if (dir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(dir.normalized);
        }

        targetPoint = new Vector3(targetPoint.x, head.position.y, targetPoint.z);

        // Anim Blend Tree
        SetBlendParams(moveVector);
    }

    private void SetBlendParams(Vector3 moveDirWorld)
    {
        Vector3 moveLocal = transform.InverseTransformDirection(moveDirWorld);
        animator.SetFloat("forwardX", moveLocal.x);
        animator.SetFloat("forwardZ", moveLocal.z);
    }

    private void TriggerFight(PunchType type)
    {
        Debug.Log("Punch: " + type.ToString());

        if (!isPunchingLeft && !isPunchingRight)
            StartCoroutine(PunchCoroutine(type));
    }

    private IEnumerator PunchCoroutine(PunchType type)
    {
        float punchDuration = (type == PunchType.LeftHook) ? punchDurationLeft : (type == PunchType.RightHook) ? punchDurationRight : 0.3f;
        if (type == PunchType.LeftHook)
            isPunchingLeft = true;
        else if (type == PunchType.RightHook)
            isPunchingRight = true;
        
        switch (type)
        {
            case PunchType.LeftHook:
                //animator.SetTrigger("LeftHook");
                leftHandTarget.position = targetPoint;
                break;
            case PunchType.RightHook:
                //animator.SetTrigger("RightHook");
                rightHandTarget.position = targetPoint;
                break;
            case PunchType.Uppercut:
                //animator.SetTrigger("Uppercut");
                break;
        }

        yield return new WaitForSeconds(punchDuration);
        if (type == PunchType.LeftHook)
            isPunchingLeft = false;
        else if (type == PunchType.RightHook)
            isPunchingRight = false;
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
        Vector3 end = start + transform.forward * dashDistance;
        float elapsed = 0f;

        while (elapsed < dashDuration)
        {
            float t = elapsed / dashDuration;
            transform.position = Vector3.Lerp(start, end, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = end;
        isDashing = false;
    }

    private void CalibrateLeft()
    {
        var direction = transform.forward; 
        var center = transform.position; 
        center.y = headHeight; 
        leftHandTarget.position = center + direction * (lastDistanceLeft); 
        rightHandTarget.position = center + direction * (lastDistanceRight);
        // calculate distances for left
        float tipToTargetDistance = Vector3.Distance(leftGloveTip.position, leftHandTarget.position); 
        float centerToTargetDistance = Vector3.Distance(center, leftHandTarget.position);
        float centerToTipDistance = Vector3.Distance(leftGloveTip.position, center); 

        Debug.Log($"Tip to Target: {tipToTargetDistance}, Target to Center: {centerToTargetDistance}, Tip to Center: {centerToTipDistance}, Max Reach from Center Left: {maxReachFromCenterLeft}"); 
        if (tipToTargetDistance > threshhold) // is not reachable
        { 
            stepSizeLeft = stepSizeLeft / 2; 
            lastDistanceLeft -= stepSizeLeft; 
            if (centerToTipDistance > maxReachFromCenterLeft) 
            { 
                maxReachFromCenterLeft = centerToTipDistance; 
            }

            if (Mathf.Abs(centerToTargetDistance - centerToTipDistance) <= threshhold || stepSizeLeft <= originalStepSize / 32)
            {
                leftHandTarget.position = center + direction * maxReachFromCenterLeft;
                isCalibratedLeft = true;
                Debug.Log("Left Hand Calibration complete. Max Reach from Center Left: " + maxReachFromCenterLeft);
            }
        } else // within threshhold - means reachable
        {
            lastDistanceLeft += stepSizeLeft*2; 
        }
    }

    private void CalibrateRight()
    {
        var direction = transform.forward;
        var center = transform.position;
        center.y = headHeight;
        rightHandTarget.position = center + direction * (lastDistanceRight);
        // calculate distances for left
        float tipToTargetDistance = Vector3.Distance(rightGloveTip.position, rightHandTarget.position);
        float centerToTargetDistance = Vector3.Distance(center, rightHandTarget.position);
        float centerToTipDistance = Vector3.Distance(rightGloveTip.position, center);

        Debug.Log($"Tip to Target: {tipToTargetDistance}, Target to Center: {centerToTargetDistance}, Tip to Center: {centerToTipDistance}, Max Reach from Center Left: {maxReachFromCenterLeft}");
        if (tipToTargetDistance > threshhold) // is not reachable
        {
            stepSizeRight = stepSizeRight / 2;
            lastDistanceRight -= stepSizeRight;
            if (centerToTipDistance > maxReachFromCenterRight)
            {
                maxReachFromCenterRight = centerToTipDistance;
            }

            if (Mathf.Abs(centerToTargetDistance - centerToTipDistance) <= threshhold || stepSizeRight <= originalStepSize / 32)
            {
                rightHandTarget.position = center + direction * maxReachFromCenterRight;
                isCalibratedRight = true;
                Debug.Log("Left Hand Calibration complete. Max Reach from Center Left: " + maxReachFromCenterRight);
            }
        }
        else // within threshhold - means reachable
        {
            lastDistanceRight += stepSizeRight;
        }
    }

    public float DistanceToParent(Transform start, Transform stopAtParent)
    {
        float total = 0f;
        Transform current = start;

        while (current != null && current != stopAtParent)
        {
            Transform parent = current.parent;
            if (parent == null) return -1f; // not a descendant

            total += Vector3.Distance(current.position, parent.position);
            current = parent;
        }

        if (current == stopAtParent) return total;
        return -1f;
    }
}
