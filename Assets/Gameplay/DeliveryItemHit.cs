using UnityEngine;

public enum DeliveryHitResult { Ignored, Blocked, Applied }

// All incoming item attacks use this payload; shields block them unless explicitly bypassed.
public readonly struct DeliveryItemHit
{
    public readonly Vector3 impulse;
    public readonly float stunSeconds, slowSeconds, inkSeconds;
    public readonly bool shieldable;

    public DeliveryItemHit(Vector3 impulse = default, float stunSeconds = 0, float slowSeconds = 0,
        float inkSeconds = 0, bool shieldable = true)
    {
        this.impulse = impulse; this.stunSeconds = stunSeconds; this.slowSeconds = slowSeconds;
        this.inkSeconds = inkSeconds; this.shieldable = shieldable;
    }
}