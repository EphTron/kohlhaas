using UnityEngine;

public class FollowCamera : MonoBehaviour
{
    public Transform Target;         // Player to follow
    public Vector3 offset = new Vector3(0, 10, 0); // Offset above player
    public float angle = 90f; // Offset above player
    public float FollowSpeed = 5f;   // Elasticity

    private void LateUpdate()
    {
        if (Target == null) return;

        Vector3 desiredPosition = Target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, FollowSpeed * Time.deltaTime);

        transform.rotation = Quaternion.Euler(angle, 0f, 0f); // Top-down
    }
}