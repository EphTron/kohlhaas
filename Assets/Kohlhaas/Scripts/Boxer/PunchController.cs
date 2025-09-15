using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.InputSystem;

public class PunchIKController : MonoBehaviour
{
    public enum AttackType { Punch, Hook, Uppercut }

    [Header("Rig")]
    public TwoBoneIKConstraint rightArmIK;
    public Transform rightTarget;
    public Transform rightHint;
    public Transform rightShoulder;
    public Transform rightHand;

    [Header("Input (New Input System)")]
    public InputActionProperty attackAction;     // Button (e.g., mouse right / gamepad south)
    public InputActionProperty aimScreenPos;     // Vector2 screen position (e.g., <Pointer>/position)
    public InputActionProperty cycleAttack;      // Button to cycle attack types
    public AttackType selectedAttack = AttackType.Punch;

    [Header("Params")]
    public float maxRange = 2.2f;
    public float reachSpeed = 8f;
    public float retractSpeed = 10f;
    public float holdTime = 0.03f;
    public AnimationCurve reachCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public LayerMask aimMask = ~0;
    public float elbowSideOffset = 0.15f;
    public float elbowForwardOffset = 0.10f;
    public float shoulderTurn = 0.35f;

    bool busy;

    void OnEnable()
    {
        if (attackAction.reference != null) attackAction.action.Enable();
        if (aimScreenPos.reference != null) aimScreenPos.action.Enable();
        if (cycleAttack.reference != null)
        {
            cycleAttack.action.Enable();
            cycleAttack.action.performed += OnCycleAttack;
        }
    }

    void OnDisable()
    {
        if (cycleAttack.reference != null)
            cycleAttack.action.performed -= OnCycleAttack;
    }

    void OnCycleAttack(InputAction.CallbackContext _)
    {
        selectedAttack = (AttackType)(((int)selectedAttack + 1) % 3);
    }

    void Update()
    {
        if (busy) return;
        if (attackAction.reference == null || aimScreenPos.reference == null) return;

        if (attackAction.action.WasPressedThisFrame())
        {
            var cam = Camera.main;
            Vector2 screen = aimScreenPos.action.ReadValue<Vector2>();
            var ray = cam.ScreenPointToRay(screen);
            if (Physics.Raycast(ray, out var hit, 100f, aimMask))
            {
                Vector3 origin = rightHand.position;
                float dist = Vector3.Distance(origin, hit.point);
                if (dist <= maxRange)
                    StartCoroutine(AttackTo(hit.point, selectedAttack));
            }
        }
    }

    System.Collections.IEnumerator AttackTo(Vector3 worldTarget, AttackType type)
    {
        busy = true;

        // IK blend in
        float startW = rightArmIK.weight;
        float t = 0f;
        while (t < 0.1f)
        {
            t += Time.deltaTime * 12f;
            rightArmIK.weight = Mathf.Lerp(startW, 1f, t);
            yield return null;
        }

        // Reach path setup (you can replace these with your swing splines later)
        Vector3 start = rightHand.position;
        Quaternion startRot = rightHand.rotation;
        Quaternion endRot = Quaternion.LookRotation((worldTarget - start).normalized, Vector3.up);

        // Simple type-based pre-bias
        Vector3 preBias = Vector3.zero;
        switch (type)
        {
            case AttackType.Punch: preBias = Vector3.zero; break;
            case AttackType.Hook: preBias = Vector3.Cross(Vector3.up, (worldTarget - start)).normalized * 0.25f; break;
            case AttackType.Uppercut: preBias = Vector3.up * 0.25f; break;
        }

        float dur = Mathf.Max(0.05f, Vector3.Distance(start, worldTarget) / reachSpeed);
        float tt = 0f;
        while (tt < 1f)
        {
            tt += Time.deltaTime / dur;
            float a = reachCurve.Evaluate(Mathf.Clamp01(tt));

            // Lerp with a small curved bias depending on type
            Vector3 curve = Vector3.LerpUnclamped(Vector3.zero, preBias, Mathf.Sin(a * Mathf.PI));
            Vector3 p = Vector3.Lerp(start, worldTarget, a) + curve;

            rightTarget.position = p;
            rightTarget.rotation = Quaternion.Slerp(startRot, endRot, a);
            UpdateElbowHint(p, type);
            yield return null;
        }

        yield return new WaitForSeconds(holdTime);

        // Retract
        Vector3 retractPoint = start;
        float backDur = Mathf.Max(0.04f, Vector3.Distance(rightTarget.position, retractPoint) / retractSpeed);
        tt = 0f;
        while (tt < 1f)
        {
            tt += Time.deltaTime / backDur;
            float a = Mathf.SmoothStep(0f, 1f, tt);
            Vector3 p = Vector3.Lerp(worldTarget, retractPoint, a);
            rightTarget.position = p;
            rightTarget.rotation = Quaternion.Slerp(endRot, startRot, a);
            UpdateElbowHint(p, type);
            yield return null;
        }

        // IK hold/relax
        t = 0f;
        while (t < 0.1f)
        {
            t += Time.deltaTime * 10f;
            rightArmIK.weight = Mathf.Lerp(1f, 0.6f, t); // keep guard; set to 0f if fully anim-driven
            yield return null;
        }

        busy = false;
    }

    void UpdateElbowHint(Vector3 handGoal, AttackType type)
    {
        Vector3 shToHand = handGoal - rightShoulder.position;
        Vector3 fwd = shToHand.normalized;
        Vector3 side = Vector3.Cross(Vector3.up, fwd).normalized;

        float sideOff = elbowSideOffset;
        float fwdOff = elbowForwardOffset;

        // Small per-type elbow bias
        if (type == AttackType.Hook) sideOff *= 1.4f;
        if (type == AttackType.Uppercut) fwdOff *= 0.6f;

        Vector3 elbow = Vector3.Lerp(rightShoulder.position, handGoal, 0.45f)
                        + side * sideOff
                        + fwd * (-fwdOff);

        rightHint.position = elbow;
    }
}
