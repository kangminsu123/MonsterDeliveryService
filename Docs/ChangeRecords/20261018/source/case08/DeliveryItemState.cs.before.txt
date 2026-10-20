using UnityEngine;

// Shared non-lethal state for the player and rivals. No renderer or automatic item grants.
public sealed class DeliveryItemState
{
    public float frozenUntil; // Visual marker; action lock and immunity use shared stun state.
    public bool Frozen(float now) => now < frozenUntil && Stunned(now);
    public float stunUntil, stunImmuneUntil, forceUntil, forceResistUntil, slowUntil;
    public float speedUntil, shieldUntil, busyUntil, visionBlockedUntil, pendingLift;
    public float shieldBrokenAt = float.NegativeInfinity;
    public Vector3 force, safePosition;
    public Transform pullSource;
    public float pullUntil, pullSpeed, pullStandOff;

    public bool Stunned(float now) => now < stunUntil;
    public bool Pulled(float now) => pullSource && now < pullUntil;
    public bool KnockedBack => force.sqrMagnitude > .01f || Time.time < pullUntil;
    public bool CanCapture(float now) => !Stunned(now) && !KnockedBack && now >= forceUntil && now >= busyUntil;
    public float MoveMultiplier(float now) => (now < slowUntil ? .6f : 1f) * (now < speedUntil ? 1.5f : 1f);

    public bool Control(float now, Vector3 impulse, float stunSeconds = 0) =>
        ReceiveHit(now, new DeliveryItemHit(impulse, stunSeconds: stunSeconds)) == DeliveryHitResult.Applied;

    public DeliveryHitResult ReceiveHit(float now, DeliveryItemHit hit)
    {
        bool stun = hit.stunSeconds > 0 && now >= stunImmuneUntil;
        bool push = hit.impulse.sqrMagnitude > .001f;
        if (!stun && !push && hit.slowSeconds <= 0 && hit.inkSeconds <= 0) return DeliveryHitResult.Ignored;
        if (hit.shieldable && now < shieldUntil)
        { shieldUntil = 0; shieldBrokenAt = now; return DeliveryHitResult.Blocked; }
        if (stun) { stunUntil = now + hit.stunSeconds; stunImmuneUntil = stunUntil + 2; }
        if (push)
        {
            pullSource = null; pullUntil = 0;
            var impulse = hit.impulse * (now < forceResistUntil ? .35f : 1f);
            force = new Vector3(impulse.x, 0, impulse.z);
            pendingLift = Mathf.Max(pendingLift, impulse.y);
            forceUntil = now + .35f;
            forceResistUntil = now + .8f;
        }
        if (hit.slowSeconds > 0) slowUntil = Mathf.Max(slowUntil, now + hit.slowSeconds);
        if (hit.inkSeconds > 0) visionBlockedUntil = now + hit.inkSeconds;
        return DeliveryHitResult.Applied;
    }

    public Vector3 TakeForce(float dt)
    {
        if (dt <= 0) return Vector3.zero;
        float movingTime = Mathf.Min(dt, force.magnitude / 30);
        var before = force;
        force = Vector3.MoveTowards(force, Vector3.zero, 30 * dt);
        // Average velocity includes the part of this frame after the push stops.
        return (before + force) * (.5f * movingTime / dt);
    }

    public float TakeLift() { float lift = pendingLift; pendingLift = 0; return lift; }
}
