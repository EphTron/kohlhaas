using UnityEngine;
using UnityEngine.InputSystem;

public class CharacterSteeringInput : MonoBehaviour
{
    public enum MoveReference
    {
        userOrigin,
        characterOrigin,
        characterForward
    }

    MovementMapper movementMapper; // Reference to the MovementMapper for input handling
    public InputActionProperty moveAction; // Action for movement input
    public InputActionProperty rotateAction; // Action for rotation input

    public MoveReference moveReference = MoveReference.userOrigin; // Reference type for movement



    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        movementMapper = GetComponent<MovementMapper>();
    }

    // Update is called once per frame
    void Update()
    {
        Vector2 moveInput = moveAction.action.ReadValue<Vector2>();
        Vector2 rotateInput = rotateAction.action.ReadValue<Vector2>();

        moveInput = Vector2.ClampMagnitude(moveInput, 1.0f);
        Vector3 moveDirection = Vector3.zero;

        if (moveReference == MoveReference.userOrigin)
        {
            Vector3 forward = movementMapper.userRoot.forward;
            forward.y = 0;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            moveDirection = (forward * moveInput.y + right * moveInput.x).normalized;
        }
        else if (moveReference == MoveReference.characterOrigin)
        {
            Vector3 forward = movementMapper.currentCharacter.characterParent.forward;
            Vector3 right = movementMapper.currentCharacter.characterParent.right;
            moveDirection = (forward * moveInput.y + right * moveInput.x).normalized;
        }
        else if (moveReference == MoveReference.characterForward)
        {
            Vector3 forward = movementMapper.currentCharacter.characterRigRoot.forward;
            forward.y = 0;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            moveDirection = (forward * moveInput.y + right * moveInput.x).normalized;
        }

        movementMapper.currentCharacter.Move(moveDirection.x, moveDirection.z);

        movementMapper.currentCharacter.Move(moveInput.x, moveInput.y);
    }
}
