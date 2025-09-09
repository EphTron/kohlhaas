
using UnityEngine;
using UnityEditor;
using UnityEngine.InputSystem;
using System.Collections.Generic;

[RequireComponent(typeof(Animator))]
public class VRRigIKRecorder : MonoBehaviour
{
    [Header("Rig")]
    public Transform rigRoot;                        // Animator root
    public Transform[] chainsToRecord;               // e.g., RightShoulder / LeftShoulder (it will include children)
    public bool recordPosition = false;              // usually off for arms-only

    [Header("Input (XR / InputSystem)")]
    public InputActionProperty startAction;          // e.g., controller button
    public InputActionProperty stopAction;           // separate action (or map same binding to both if you like)

    [Header("Output")]
    [Tooltip("Asset-relative path (must be under Assets/)")]
    public string outputFolder = "Assets/Recordings";
    public string clipName = "Boxer_Arms";

    private UnityEditor.Animations.GameObjectRecorder recorder;
    private bool isRecording;

    void Reset()
    {
        var anim = GetComponent<Animator>();
        if (!rigRoot && anim) rigRoot = anim.transform;
    }

    void OnEnable()
    {
        if (startAction.action != null)
        {
            startAction.action.performed += OnStartPerformed;
            startAction.action.Enable();
        }
        if (stopAction.action != null)
        {
            stopAction.action.performed += OnStopPerformed;
            stopAction.action.Enable();
        }
    }

    void OnDisable()
    {
        if (startAction.action != null)
        {
            startAction.action.performed -= OnStartPerformed;
            startAction.action.Disable();
        }
        if (stopAction.action != null)
        {
            stopAction.action.performed -= OnStopPerformed;
            stopAction.action.Disable();
        }
        if (isRecording) End(); // safety
    }

    void LateUpdate()
    {
        if (isRecording && recorder != null)
            recorder.TakeSnapshot(Time.deltaTime);
    }

    void OnStartPerformed(InputAction.CallbackContext _)
    {
        if (!isRecording) Begin();
    }

    void OnStopPerformed(InputAction.CallbackContext _)
    {
        if (isRecording) End();
    }

    void Begin()
    {
        if (!rigRoot) rigRoot = transform;
        if (recorder != null) return;

        recorder = new UnityEditor.Animations.GameObjectRecorder(rigRoot.gameObject);

        if (rigRoot != null)
        {
            // rigRoot = the top of the hierarchy you want to capture
            recorder.BindComponentsOfType<Transform>(rigRoot.gameObject, true);
        }

        isRecording = true;
        Debug.Log("[VRRigIKRecorder] Recording started.");
    }

    void End()
    {
        if (recorder == null) return;

        var clip = new AnimationClip { name = clipName, legacy = false };
        recorder.SaveToClip(clip);
        recorder.ResetRecording();
        recorder = null;
        isRecording = false;

        EnsureFolder(outputFolder);
        var path = AssetDatabase.GenerateUniqueAssetPath($"{outputFolder}/{clipName}.anim");
        AssetDatabase.CreateAsset(clip, path);
        AssetDatabase.SaveAssets();

        Debug.Log($"[VRRigIKRecorder] Recording saved: {path}");
    }

    static void EnsureFolder(string assetPath)
    {
        if (AssetDatabase.IsValidFolder(assetPath)) return;

        var parts = assetPath.Replace('\\', '/').Split('/');
        string parent = "Assets";
        for (int i = 1; i < parts.Length; i++)
        {
            if (string.IsNullOrEmpty(parts[i])) continue;
            var current = $"{parent}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(current))
                AssetDatabase.CreateFolder(parent, parts[i]);
            parent = current;
        }
    }
}

