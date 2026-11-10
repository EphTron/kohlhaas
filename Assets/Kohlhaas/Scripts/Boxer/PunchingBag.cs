using System;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider), typeof(ConfigurableJoint))]
public sealed class PunchingBag : MonoBehaviour, IPunchHitReceiver
{
    [Header("Suspension")]
    [Tooltip("Height of the fixed suspension point above the bag's centre, in local metres.")]
    [SerializeField, Min(0.1f)] private float suspensionHeight = 1.55f;
    [SerializeField, Range(5f, 75f)] private float swingLimit = 35f;
    [SerializeField, Min(0.1f)] private float mass = 7.5f;
    [SerializeField, Min(0f)] private float damping = 0.7f;
    [SerializeField] private Material ropeMaterial;

    [Header("Impact Feedback")]
    [SerializeField] private Renderer bagRenderer;
    [SerializeField] private Color hitColor = new Color(1f, 0.85f, 0.45f);
    [SerializeField, Min(0.01f)] private float flashDuration = 0.09f;
    [SerializeField, Min(0f)] private float impulseMultiplier = 1f;
    [SerializeField] private AudioClip impactSound;
    [SerializeField, Range(0f, 1f)] private float volume = 0.55f;
    [SerializeField] private bool showHitStats = true;
    [SerializeField] private InputAction resetBagAction = new InputAction("Reset Bag", InputActionType.Button, "<Keyboard>/r");

    public int HitCount { get; private set; }
    public event Action<PunchHit> HitReceived;

    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private Rigidbody body;
    private AudioSource speaker;
    private AudioClip generatedSound;
    private LineRenderer rope;
    private MaterialPropertyBlock properties;
    private Vector3 initialPosition, anchor;
    private Quaternion initialRotation;
    private Color restingColor;
    private float flash;
    private PunchHit lastHit;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.mass = mass;
        body.linearDamping = 0.25f;
        body.angularDamping = damping;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.solverIterations = 12;
        body.solverVelocityIterations = 4;
        initialPosition = body.position;
        initialRotation = body.rotation;
        anchor = transform.TransformPoint(Vector3.up * suspensionHeight);

        var joint = GetComponent<ConfigurableJoint>();
        joint.autoConfigureConnectedAnchor = false;
        joint.connectedBody = null;
        joint.anchor = Vector3.up * suspensionHeight;
        joint.connectedAnchor = anchor;
        joint.xMotion = joint.yMotion = joint.zMotion = ConfigurableJointMotion.Locked;
        joint.angularXMotion = joint.angularYMotion = joint.angularZMotion = ConfigurableJointMotion.Limited;
        joint.lowAngularXLimit = new SoftJointLimit { limit = -swingLimit };
        joint.highAngularXLimit = new SoftJointLimit { limit = swingLimit };
        joint.angularYLimit = new SoftJointLimit { limit = 30f };
        joint.angularZLimit = new SoftJointLimit { limit = swingLimit };
        joint.projectionMode = JointProjectionMode.PositionAndRotation;
        joint.projectionDistance = 0.02f;
        joint.projectionAngle = 5f;

        if (bagRenderer == null) bagRenderer = GetComponentInChildren<Renderer>();
        properties = new MaterialPropertyBlock();
        restingColor = bagRenderer != null && bagRenderer.sharedMaterial != null
            ? bagRenderer.sharedMaterial.GetColor(BaseColor) : Color.red;

        // The rope follows the physical bag; its ceiling endpoint stays fixed.
        var ropeObject = new GameObject("Suspension Rope");
        ropeObject.transform.SetParent(transform, false);
        rope = ropeObject.AddComponent<LineRenderer>();
        rope.useWorldSpace = true;
        rope.positionCount = 2;
        rope.startWidth = rope.endWidth = 0.025f;
        rope.sharedMaterial = ropeMaterial;
        rope.startColor = rope.endColor = Color.white;

        speaker = gameObject.AddComponent<AudioSource>();
        speaker.playOnAwake = false;
        speaker.spatialBlend = 0.8f;
        speaker.minDistance = 2f;
        speaker.maxDistance = 25f;
        speaker.rolloffMode = AudioRolloffMode.Linear;
        if (impactSound == null)
        {
            generatedSound = MakeThud();
            impactSound = generatedSound;
        }
    }

    private void OnEnable() => resetBagAction?.Enable();

    private void OnDisable()
    {
        resetBagAction?.Disable();
        if (bagRenderer != null) bagRenderer.SetPropertyBlock(null);
        flash = 0f;
    }

    private void OnDestroy()
    {
        resetBagAction?.Dispose();
        if (generatedSound != null) Destroy(generatedSound);
    }

    private void Update()
    {
        if (resetBagAction.WasPressedThisFrame()) ResetBag();
        flash = Mathf.Max(0f, flash - Time.deltaTime);
        if (bagRenderer != null)
        {
            properties.SetColor(BaseColor, Color.Lerp(restingColor, hitColor, flash / Mathf.Max(0.01f, flashDuration)));
            bagRenderer.SetPropertyBlock(properties);
        }
    }

    private void LateUpdate()
    {
        rope.SetPosition(0, transform.TransformPoint(Vector3.up * 0.7f));
        rope.SetPosition(1, anchor);
    }

    public void ReceivePunch(PunchHit hit)
    {
        if (!isActiveAndEnabled || body == null) return;
        body.AddForceAtPosition(hit.Direction.normalized * hit.Impulse * impulseMultiplier,
            hit.Point, ForceMode.Impulse);
        lastHit = hit;
        HitCount++;
        flash = flashDuration;
        speaker.pitch = hit.Mode == BoxingMode.Swing ? 0.85f : 1.05f;
        speaker.PlayOneShot(impactSound, volume);
        HitReceived?.Invoke(hit);
    }

    [ContextMenu("Reset Bag")]
    public void ResetBag()
    {
        if (body == null) return;
        body.position = initialPosition;
        body.rotation = initialRotation;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.WakeUp();
        flash = 0f;
        HitCount = 0;
    }

    private void OnGUI()
    {
        if (!showHitStats) return;
        string last = HitCount == 0 ? "Land a punch to test contact" : $"Last: {lastHit.Mode} | Impulse: {lastHit.Impulse * impulseMultiplier:0.0} N s";
        GUI.Box(new Rect(12f, 92f, 400f, 72f), $"Punching bag | Hits: {HitCount}\n{last}\n{resetBagAction.GetBindingDisplayString()}: reset bag");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 top = Application.isPlaying ? anchor : transform.TransformPoint(Vector3.up * suspensionHeight);
        Gizmos.DrawWireSphere(top, 0.08f);
        Gizmos.DrawLine(transform.position, top);
    }

    private static AudioClip MakeThud()
    {
        const int sampleRate = 22050;
        var samples = new float[4410];
        var random = new System.Random(17);
        float phase = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            float time = i / (float)sampleRate;
            phase += 2f * Mathf.PI * Mathf.Lerp(110f, 48f, time / 0.2f) / sampleRate;
            float noise = (float)random.NextDouble() * 2f - 1f;
            samples[i] = (Mathf.Sin(phase) * 0.7f + noise * 0.3f * Mathf.Exp(-time * 65f))
                * Mathf.Exp(-time * 24f) * Mathf.Min(1f, time * 1000f);
        }
        var clip = AudioClip.Create("Training Bag Thud", samples.Length, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
