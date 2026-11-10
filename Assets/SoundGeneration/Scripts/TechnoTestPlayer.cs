using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public sealed class TechnoTestPlayer : MonoBehaviour
{
    public float speed = 4;
    public float floorHalfExtent = 9;
    CharacterController controller;
    Vector3 spawn;
    Material material;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        spawn = transform.position;
        // A little emission keeps the test capsule visible between light pulses.
        material = GetComponent<Renderer>().material;
        material.SetColor("_BaseColor", new Color(0.65f, 0.9f, 1));
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", new Color(0.08f, 0.16f, 0.2f));
    }

    void Update()
    {
        var keys = Keyboard.current;
        if (keys == null) return;
        if (keys.rKey.wasPressedThisFrame)
        {
            controller.enabled = false;
            transform.position = spawn;
            controller.enabled = true;
        }
        Vector3 movement = new Vector3(
            (keys.dKey.isPressed ? 1 : 0) - (keys.aKey.isPressed ? 1 : 0), 0,
            (keys.wKey.isPressed ? 1 : 0) - (keys.sKey.isPressed ? 1 : 0));
        Vector3 target = transform.position + Vector3.ClampMagnitude(movement, 1) * (speed * Time.deltaTime);
        target.x = Mathf.Clamp(target.x, -floorHalfExtent, floorHalfExtent);
        target.z = Mathf.Clamp(target.z, -floorHalfExtent, floorHalfExtent);
        controller.Move(target - transform.position + Vector3.down * (9.81f * Time.deltaTime));
    }

    void OnDestroy() { if (material) Destroy(material); }
}
