using UnityEngine;

public readonly struct PunchHit
{
    public readonly Vector3 Point;
    public readonly Vector3 Direction;
    public readonly float Impulse;
    public readonly BoxingMode Mode;

    public PunchHit(Vector3 point, Vector3 direction, float impulse, BoxingMode mode)
    {
        Point = point;
        Direction = direction;
        Impulse = impulse;
        Mode = mode;
    }
}

// Enemies can later receive the same punch information as the training bag.
public interface IPunchHitReceiver
{
    void ReceivePunch(PunchHit hit);
}
