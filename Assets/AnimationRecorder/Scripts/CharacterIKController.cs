using UnityEngine;
using UnityEngine.Windows;

public class CharacterIKController : MonoBehaviour
{
    public Animator animator;

    [Header("Character Transforms")]
    public Transform characterParent;
    public Transform characterRigRoot;
    public Transform characterHead;

    public Transform characterHandLeft;
    public Transform characterHandRight;

    [Header("Target Transforms")]
    public Transform targetLeft;
    public Transform targetRight;
    public Transform targetHead;

    [Header("Set Through Config Button")]

    [Header("Offsets")]
    public Vector3 initialRotationOffsetLeft;
    public Vector3 initialRotationOffsetRight;

    [Header("Movement Settings")]
    public float moveSpeed = 1.0f; // Speed of character movement

    void Update()
    {
        characterHandLeft.position = targetLeft.position;
        characterHandLeft.rotation = targetLeft.rotation;
        characterHandRight.position = targetRight.position;
        characterHandRight.rotation = targetRight.rotation; // * Quaternion.Euler(initialRotationOffsetRight);
    }

    public float GetCharacterHeight()
    {
        float height = characterHead.position.y - characterParent.position.y;
        return height;
    }

    public void Move(Vector2 moveVec2)
    {
        // In Update or wherever you compute movement:
        Vector3 moveInput = new Vector3(moveVec2.x, 0, moveVec2.y);
        Vector3 moveLocal = transform.InverseTransformDirection(moveInput.normalized);

        // apply move input to characters root transform
        characterParent.position += moveLocal * moveSpeed * Time.deltaTime; // Adjust speed as needed

        // Apply blend params
        animator.SetFloat("MoveX", moveLocal.x);
        animator.SetFloat("MoveZ", moveLocal.z);
    }

    public void Move(float moveX, float moveZ)
    {
        // In Update or wherever you compute movement:
        Vector3 moveInput = new Vector3(moveX, 0, moveZ);
        Vector3 moveLocal = transform.InverseTransformDirection(moveInput.normalized);

        // apply move input to characters root transform
        characterParent.position += moveLocal * Time.deltaTime; // Adjust speed as needed

        // Apply blend params
        animator.SetFloat("MoveX", moveLocal.x);
        animator.SetFloat("MoveZ", moveLocal.z);
    }
}