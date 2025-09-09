using UnityEngine;
using UnityEngine.Audio;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class AudioSourcePool : MonoBehaviour
{
    public AudioMixerGroup outputGroup; // assign e.g. "Vox" group (optional)
    [Min(1)] public int initial = 8;

    readonly List<AudioSource> pool = new();

    void Awake()
    {
        // optional: sanity check (warn if none or many listeners)
        if (FindObjectsOfType<AudioListener>().Length != 1)
            Debug.LogWarning("Expected exactly one AudioListener in the scene.");
    }

    void Start()
    {
        for (int i = 0; i < initial; i++) pool.Add(NewSource());
    }

    AudioSource NewSource()
    {
        var go = new GameObject("VoxSrc");
        go.transform.SetParent(transform, false);
        var s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = false;
        s.spatialBlend = 0f;
        if (outputGroup) s.outputAudioMixerGroup = outputGroup;
        return s;
    }

    public AudioSource Get()
    {
        foreach (var s in pool) if (!s.isPlaying) return s;
        var ns = NewSource(); pool.Add(ns); return ns;
    }
}
