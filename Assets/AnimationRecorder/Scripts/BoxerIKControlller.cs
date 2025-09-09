using UnityEngine;

public class BoxerIKController : MonoBehaviour
{
    public Transform boxerHandLeft, boxerHandRight;
    public Transform targetLeft, targetRight;

    void Update()
    {
        boxerHandLeft.position = targetLeft.position;
        boxerHandLeft.rotation = targetLeft.rotation;
        boxerHandRight.position = targetRight.position;
        boxerHandRight.rotation = targetRight.rotation;
    }
}