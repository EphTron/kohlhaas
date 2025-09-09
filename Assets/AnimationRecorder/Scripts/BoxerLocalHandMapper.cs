using UnityEngine;
using UnityEngine.InputSystem;

public class BoxerLocalHandMapper : MonoBehaviour
{
    [Header("User Transforms")]
    public Transform vrLeftHand;
    public Transform vrRightHand;
    public Transform vrHead;
    public Transform vrRoot;

    [Header("Character Transforms")]
    public Transform characterRoot;
    public Transform characterHead;

    [Header("Target Transforms")]
    public Transform leftTarget;
    public Transform rightTarget;

    [Header("Input Action")]
    public InputActionProperty calibrateAction;
    public InputActionProperty recenterAction;

    [Header("Offsets")]
    private Vector3 characterOffset;
    private Vector3 positionOffsetLeft;
    private Vector3 positionOffsetRight;
    public Vector3 initialRotationOffsetLeft;
    public Vector3 initialRotationOffsetRight;

    void Start()
    {
       
    }

    void Update()
    {
        if (calibrateAction.action.IsPressed())
        {
            //Calibrate();
            return;
        }

        if (recenterAction.action.IsPressed())
        {
            Recenter();
            return;
        }

        // Apply VR hand positions as local space deltas
        Vector3 currentLeftOffset = vrRoot.InverseTransformPoint(vrLeftHand.position);
        Vector3 currentRightOffset = vrRoot.InverseTransformPoint(vrRightHand.position);

        //Vector3 deltaLeft = currentLeftOffset - initialPositionOffsetLeft;
        //Vector3 deltaRight = currentRightOffset - initialPositionOffsetRight;

        leftTarget.localPosition = currentLeftOffset - characterOffset;
        rightTarget.localPosition = currentRightOffset - characterOffset;

        // Optional: match rotation (local rotation not required unless necessary)
        leftTarget.rotation = vrLeftHand.rotation * Quaternion.Euler(initialRotationOffsetLeft);
        rightTarget.rotation = vrRightHand.rotation * Quaternion.Euler(initialRotationOffsetRight);

        // Update offsets for next frame
        //initialPositionOffsetLeft = currentLeftOffset;
        //initialPositionOffsetRight = currentRightOffset;
    }

    public void Calibrate()
    {
        positionOffsetLeft = vrRoot.InverseTransformPoint(vrLeftHand.position);
        positionOffsetRight = vrRoot.InverseTransformPoint(vrRightHand.position);
    }

    public void Recenter()
    {
        Vector3 headPos = vrRoot.InverseTransformPoint(vrHead.position);
        Vector3 characterHeadPos = characterRoot.InverseTransformDirection(characterHead.position);
        characterOffset = headPos - characterHeadPos;
    }
}
