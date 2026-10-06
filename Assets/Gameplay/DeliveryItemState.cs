using UnityEngine;

// Shared non-lethal state for the player and rivals. No renderer or automatic item grants.
public sealed class DeliveryItemState
{
    public float stunUntil, stunImmuneUntil, forceUntil, forceResistUntil, slowUntil;
    public float speedUntil, shieldUntil, busyUntil, visionBlockedUntil, pendingLift;
    public Vector3 force, safePosition;

    public bool Stunned(float now) => now < stunUntil;
    public bool CanCapture(float now) => !Stunned(now) && now >= forceUntil && now >= busyUntil;
    public float MoveMultiplier(float now) => (now < slowUntil ? .6f : 1f) * (now < speedUntil ? 1.4f : 1f);

    public bool Control(float now, Vector3 impulse, float stunSeconds = 0)
    {
        bool stun = stunSeconds > 0 && now >= stunImmuneUntil;
        bool push = impulse.sqrMagnitude > .001f;
        if (!stun && !push) return false;
        if (now < shieldUntil) { shieldUntil = 0; return false; }
        if (stun) { stunUntil = now + stunSeconds; stunImmuneUntil = stunUntil + 2; }
        if (push)
        {
            impulse *= now < forceResistUntil ? .35f : 1f;
            force = new Vector3(impulse.x, 0, impulse.z);
            pendingLift = Mathf.Max(pendingLift, impulse.y);
            forceUntil = now + .35f;
            forceResistUntil = now + .8f;
        }
        return true;
    }

    public Vector3 TakeForce(float dt)
    {
        var result = force;
        force = Vector3.MoveTowards(force, Vector3.zero, 30 * dt);
        return result;
    }

    public float TakeLift() { float lift = pendingLift; pendingLift = 0; return lift; }
}
