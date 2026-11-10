using System;
using UnityEngine;

public enum PunchKind { Jab = 0, Power = 1, Uppercut = 2, Push = 3 }

[Serializable]
public sealed class PunchProfile
{
    [Min(0f)] public float windupTime;
    [Tooltip("Straight punch: glove pullback before extension, in character-local metres.")]
    [Min(0f)] public float pullbackDistance;
    [Min(0f)] public float outwardDraw;
    [Min(0.1f)] public float reachMultiplier = 1f;
    [Min(0.1f)] public float extensionTimeMultiplier = 1f;
    [Min(0.1f)] public float recoveryTimeMultiplier = 1f;
    [Min(0f)] public float impulseMultiplier = 1f;
    [Tooltip("Degrees to draw the punching shoulder backward. Mirrored automatically per hand.")]
    [Range(0f, 35f)] public float torsoWindupAngle;
    [Tooltip("Degrees to drive the punching shoulder forward past the starting pose.")]
    [Range(0f, 35f)] public float torsoStrikeAngle;
}
