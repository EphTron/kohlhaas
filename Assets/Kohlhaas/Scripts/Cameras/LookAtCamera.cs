using UnityEngine;

public class LookAtCamera : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] float rotateSpeed = 2f;
    [SerializeField] Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();
    }

    void Update()
    {
        if (!target) return;
        if (!cam) return;

        Vector3 direction = target.position - cam.transform.position;
        Quaternion targetRotation = Quaternion.LookRotation(direction);
        cam.transform.rotation = Quaternion.Slerp(
            cam.transform.rotation,
            targetRotation,
            rotateSpeed * Time.deltaTime
        );
    }
}
