using UnityEngine;

public class SimpleLocalPositionMapper : MonoBehaviour
{
    public Transform hmdRoot;
    public Transform hmdPoint;
    public Transform target;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 pointOffset = hmdRoot.InverseTransformPoint(hmdPoint.position);
        target.localPosition = pointOffset;
    }
}
