using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Animations.Rigging;
using UnityEditor.Timeline.Actions;

public class BoxMechanicController : MonoBehaviour
{
    [Header("Input")]
    public InputActionProperty MoveAction;
    public InputActionProperty LookPosAction;
    public InputActionProperty LeftAttackAction;
    public InputActionProperty RightAttackAction;

    [Header("Right Rig")]
    public TwoBoneIKConstraint rightArmIK;
    public ChainIKConstraint rightShoulderIK;
    public ChainIKConstraint rightBackIK;
    //public Transform rightShoulderTarget;
    public Transform rightShoulder;
    public Transform rightHandTarget;
    public Transform rightElbow;
    public Transform rightHand;

    [Header("Left Rig")]
    public TwoBoneIKConstraint leftArmIK;
    public ChainIKConstraint leftShoulderIK;
    public ChainIKConstraint leftBackIK;
    //public Transform leftShoulderTarget;
    public Transform leftShoulder;
    public Transform leftElbow;
    public Transform leftHandTarget;
    public Transform leftHand;

    [Header("Rig")]
    public Transform head;
    public float headSize = 0.3f;

    [Header("Offsets")]
    public Vector3 initialRotationOffsetLeft;
    public Vector3 initialRotationOffsetRight;

    [Header("References")]
    public Transform leftGlove;
    public Transform rightGlove;
    [SerializeField] private Camera MainCamera;
    [SerializeField] private Animator animator;

    [Header("Movement Settings")]
    public float boxingArmWeight = 1f;
    public float boxingShoulderWeight = 0.5f;
    public float boxingBackWeight = 0.2f;

    public float maxDashRange = 2.2f;
    public float speed = 5f;

    [Header("Animation Settings")]
    public float leftAnimationTime = 6f;
    public float rightAnimationTime = 6f;

    private float leftAnimationProgress = 0f;
    private float rightAnimationProgress = 0f;

    private bool isBoxingLeft = false;
    private bool isBoxingRight = false;
    private bool isDashing = false;

    private Vector3 rightHandLocalStartPosition;
    private Vector3 leftHandLocalStartPosition;
    // capture the world start once at punch start
    private Vector3 leftHandWorldStartPosition;
    private Vector3 leftShoulderPosition;
    private Vector3 rightShoulderPosition;

    private Vector3 dashStartPosition;

    private Vector3 leftLocalTargetPosition;
    private Vector3 rightLocalTargetPosition;
    private Vector3 dashTargetPosition;
    
    private Vector3 currentTarget = Vector3.zero;
    private Vector2 moveInput;
    private Vector3 moveVector;


    private float leftArmLength;
    private float rightArmLength;

    void OnEnable()
    {
        if (LeftAttackAction.reference != null) LeftAttackAction.action.Enable();
        if (RightAttackAction.reference != null) RightAttackAction.action.Enable();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        leftHandLocalStartPosition = leftHand.localPosition;
        Debug.Log($"Left Hand Start Local Position: {leftHandLocalStartPosition}");
        leftShoulderPosition = leftShoulder.localPosition;
        rightHandLocalStartPosition = rightHand.localPosition;
        rightShoulderPosition = rightShoulder.localPosition;
        //leftHandTarget.rotation = leftHand.rotation * Quaternion.Euler(initialRotationOffsetLeft);
        //rightHandTarget.rotation = rightHand.rotation * Quaternion.Euler(initialRotationOffsetRight);

        leftArmLength = Vector3.Distance(leftShoulder.position, leftElbow.position) + Vector3.Distance(leftElbow.position, leftHand.position) + Vector3.Distance(leftHand.position, leftGlove.position);
        rightArmLength = Vector3.Distance(rightShoulder.position, rightElbow.position) + Vector3.Distance(rightElbow.position, rightHand.position) + Vector3.Distance(rightHand.position, rightGlove.position);
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

        // Move Character
        MoveCharacter();

        // Rotate Character
        RotateCharacter(out Vector3 targetPoint);

        // set the current mouse position target with the head height
        currentTarget = new Vector3(targetPoint.x, head.position.y + headSize / 2, targetPoint.z);
        Vector3 center = new Vector3(transform.position.x, head.position.y + headSize / 2, transform.position.z);

        if (RightAttackAction.action.WasPressedThisFrame() && !isBoxingRight)
        {
            isBoxingRight = true;
            rightHandLocalStartPosition = rightHand.localPosition;

            rightArmIK.weight = boxingArmWeight;
            rightShoulderIK.weight = boxingShoulderWeight;
            rightBackIK.weight = boxingBackWeight;
            //float distanceToTarget = Vector3.Distance(rightShoulder.position, currentTarget);
            float distanceToTarget = Vector3.Distance(center, currentTarget);
            if (distanceToTarget > rightArmLength && distanceToTarget <= maxDashRange)
            {
                //dash
                Vector3 dashTarget = transform.position + (currentTarget - center).normalized * (distanceToTarget - rightArmLength);
                transform.position = dashTarget;
                isDashing = true;
            }
            // capture final desired hand goal for this strike
            rightLocalTargetPosition = currentTarget;
        }

        if (LeftAttackAction.action.WasPressedThisFrame() && !isBoxingLeft)
        {
            StartBoxingLeft(center);
        }

        if (RightAttackAction.action.IsPressed() && isBoxingRight)
        {
            rightHandTarget.transform.position = new Vector3(targetPoint.x, head.position.y + headSize/2, targetPoint.z);
            //rightShoulderTarget.transform.position = new Vector3(targetPoint.x, head.position.y, targetPoint.z) + (rightShoulder.position - rightHand.position);
        }

        if (isBoxingLeft)
        {
            BoxingLeft();
        }

        if (isDashing)
        {
            if (isBoxingLeft)
            {
                Dash(leftAnimationTime, leftAnimationProgress);
            }
            else if (isBoxingRight)
            {
                Dash(rightAnimationTime, rightAnimationProgress);
            }
        }

        if (RightAttackAction.action.WasReleasedThisFrame() && isBoxingRight)
        {
            
            rightHand.localPosition = rightHandLocalStartPosition;
            rightHandTarget.transform.position = rightHand.position;
            rightShoulder.localPosition = rightShoulderPosition;
            //rightShoulderTarget.transform.position = rightShoulder.position;
            isBoxingRight = false;
            rightArmIK.weight = 0f;
            rightShoulderIK.weight = 0f;
        }

        //if (LeftAttackAction.action.WasReleasedThisFrame() && isBoxingLeft)
        //{

        //    leftHand.localPosition = leftHandLocalStartPosition;
        //    leftHandTarget.transform.position = leftHand.position;
        //    leftShoulder.localPosition = leftShoulderPosition;
        //    //leftShoulderTarget.transform.position = leftShoulder.position;
        //    isBoxingLeft = false;
        //    leftArmIK.weight = 0f;
        //    leftShoulderIK.weight = 0f;
        //}

        //leftHandTarget.rotation = leftHand.rotation;
        //rightHandTarget.rotation = rightHand.rotation;
    }

    private void MoveCharacter()
    {
        // Movement
        moveVector = new Vector3(moveInput.x, 0, moveInput.y);
        Vector3 move = moveVector * speed * Time.deltaTime;
        transform.position += move;
        SetBlendParams(moveVector);
    }

    private void RotateCharacter(out Vector3 targetPoint)
    {
        // Rotation
        Ray ray = MainCamera.ScreenPointToRay(LookPosAction.action.ReadValue<Vector2>());
        Plane ground = new Plane(Vector3.up, Vector3.zero);
        targetPoint = Vector3.zero;
        if (ground.Raycast(ray, out float dist))
        {
            targetPoint = ray.GetPoint(dist);
            Vector3 dir = targetPoint - transform.position;
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

    private void StartBoxingLeft(Vector3 center)
    {
        isBoxingLeft = true;
        leftArmIK.weight = boxingArmWeight;
        leftShoulderIK.weight = boxingShoulderWeight;
        leftBackIK.weight = boxingBackWeight;

        leftHandLocalStartPosition = leftHand.localPosition;
        leftHandWorldStartPosition = leftHand.position;
        leftLocalTargetPosition = transform.InverseTransformPoint(currentTarget);

        float distanceToTarget = Vector3.Distance(center, currentTarget);
        if (distanceToTarget > leftArmLength && distanceToTarget <= maxDashRange)
        {
            dashTargetPosition = transform.position + (currentTarget - center).normalized * (distanceToTarget - leftArmLength);
            Debug.Log($"Dashing from {transform.position}  to {dashTargetPosition}");
            dashStartPosition = transform.position;
            isDashing = true;
        }

        leftAnimationProgress = 0f; // start anim
    }

    private void BoxingLeft()
    {
        leftAnimationProgress += Time.deltaTime;

        float halfTime = leftAnimationTime * 0.5f;
        float t = leftAnimationProgress;

        if (t <= halfTime)
        {
            // forward
            float a = t / halfTime;
            leftHandTarget.localPosition = Vector3.Lerp(leftHandWorldStartPosition, leftLocalTargetPosition, a);
        }
        else if (t <= leftAnimationTime)
        {
            // backward
            float a = (t - halfTime) / halfTime;
            leftHandTarget.localPosition = Vector3.Lerp(leftLocalTargetPosition, leftHandWorldStartPosition, a);
        }
        else
        {
            // done
            leftHand.localPosition = leftHandLocalStartPosition;
            leftHandTarget.position = leftHand.position;
            leftShoulder.localPosition = leftShoulderPosition;
            isBoxingLeft = false;
            leftArmIK.weight = 0f;
            leftShoulderIK.weight = 0f;
            leftBackIK.weight = 0f;
            leftAnimationProgress = 0f;
        }
    }

    private void Dash(float animationTime, float progress)
    {
        float halfTime = animationTime * 0.5f;
        float t = progress;

        if (t <= halfTime)
        {
            // forward
            float a = t / halfTime;
            transform.position = Vector3.Lerp(dashStartPosition, dashTargetPosition, a);
        }
        else if (t <= animationTime)
        {
            // backward
            float a = (t - halfTime) / halfTime;
            //transform.position = Vector3.Lerp(dashTargetPosition, dashStartPosition, a);
        }
        else
        {
           isDashing = false;
        }
    }
}
