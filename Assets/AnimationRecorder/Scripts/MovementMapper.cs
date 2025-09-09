using UnityEngine;
using UnityEngine.InputSystem;

public class MovementMapper : MonoBehaviour
{
    public enum  ScaleApproach
    {
        Ignore,
        ScaleToUserHeight,
        ScaleToCharacterScale
    }

    public CharacterIKController currentCharacter;

    [Header("User Transforms")]
    public Transform userRoot;
    public Transform userHead;
    public Transform userLeftHand;
    public Transform userRightHand;
    public Transform userHeadTarget;

    public Vector3 calibrationCenter;
    public GameObject calibrationMarker;

    [Header("Input Action")]
    public InputActionProperty calibrateAction;
    public InputActionProperty recenterAction;

    [Header("Scale Setting")]
    public ScaleApproach scaleApproach = ScaleApproach.ScaleToUserHeight;
    private float scaleFactor = 1.0f;

    void Start()
    {

    }

    void Update()
    {
        if (recenterAction.action.IsPressed())
        {
            Recenter();
            return;
        }

        // Get hand offsets relative to the user's head (local space)
        Vector3 leftHandHeadOffset = userHead.InverseTransformPoint(userLeftHand.position);
        Vector3 rightHandHeadOffset = userHead.InverseTransformPoint(userRightHand.position);

        if (scaleApproach == ScaleApproach.Ignore)
        {
            scaleFactor = 1.0f;
        }
        else if (scaleApproach == ScaleApproach.ScaleToCharacterScale)
        {
            scaleFactor = 1 / currentCharacter.characterParent.localScale.y;
        }
        else if (scaleApproach == ScaleApproach.ScaleToUserHeight)
        {
            float userHeight = userHead.position.y - userRoot.position.y;
            if (userHeight <= 0.01f)
            {
                Debug.LogWarning("User height is too small, using default scale factor of 1.0f.");
                userHeight = 1.0f; // Prevent division by zero or too small height
            }
            float characterHeight = currentCharacter.GetCharacterHeight();
            scaleFactor = characterHeight / userHeight;
        }

        // Apply scaled offset
        Vector3 characterHeadOffset = currentCharacter.characterParent.InverseTransformPoint(currentCharacter.characterHead.position);
        currentCharacter.targetLeft.localPosition = characterHeadOffset + (leftHandHeadOffset * scaleFactor);
        currentCharacter.targetRight.localPosition = characterHeadOffset + (rightHandHeadOffset * scaleFactor);

        currentCharacter.targetLeft.rotation = userLeftHand.rotation * Quaternion.Euler(currentCharacter.initialRotationOffsetLeft);
        currentCharacter.targetRight.rotation = userRightHand.rotation * Quaternion.Euler(currentCharacter.initialRotationOffsetRight);
    }

    public void Recenter()
    {
        calibrationCenter = userHead.position - new Vector3(0,userHead.position.y,0); 
        if (calibrationMarker)
        {
            calibrationMarker.transform.position = calibrationCenter;
        }
        // Local head offset in user space
        Vector3 localHeadOffset = userHead.position - userRoot.position;

        // Local character head offset in character space
        Vector3 characterHeadOffset = currentCharacter.characterHead.position - currentCharacter.characterParent.position;

        Debug.Log($"Character height: {currentCharacter.GetCharacterHeight():F2} m");
        Debug.Log($"User height: {localHeadOffset.y:F2} m");
    }
}
