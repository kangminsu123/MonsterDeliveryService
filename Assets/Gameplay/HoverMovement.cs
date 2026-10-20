using UnityEngine;

public sealed partial class MonsterDeliveryPrototype
{
    static void ConfigureHoverBody(CharacterController actor)
    {
        actor.height = 1.4f;
        actor.radius = .55f;
        actor.center = Vector3.up * .7f;
        actor.skinWidth = .03f;
        actor.stepOffset = .15f;
    }

    // Ground sensing is a query, not a collider in the empty space under the robot.
    static bool TryHoverGround(CharacterController actor, Vector3 position, out RaycastHit ground)
    {
        ground = default;
        float closest = float.PositiveInfinity;
        foreach (var hit in Physics.RaycastAll(position + Vector3.up * .3f, Vector3.down,
                     DeliveryRobotVisual.HoverClearance + .6f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.GetComponentInParent<CharacterController>() ||
                hit.collider.GetComponentInParent<InteractionTarget>() ||
                hit.normal.y < Mathf.Cos(actor.slopeLimit * Mathf.Deg2Rad) || hit.distance >= closest) continue;
            closest = hit.distance; ground = hit;
        }
        return closest < float.PositiveInfinity;
    }

    static bool HoverGrounded(CharacterController actor) =>
        TryHoverGround(actor, actor.transform.position, out var ground) &&
        Mathf.Abs(actor.transform.position.y - ground.point.y - DeliveryRobotVisual.HoverClearance) < .15f;

    static void MoveHoverActor(CharacterController actor, Vector3 motion, ref float falling)
    {
        var next = actor.transform.position + motion;
        // A long frame can put the next probe below the floor. Check from the current height first.
        var probe = new Vector3(next.x, Mathf.Max(next.y, actor.transform.position.y), next.z);
        if (falling <= 0 && (TryHoverGround(actor, probe, out var ground) || TryHoverGround(actor, next, out ground)) &&
            next.y <= ground.point.y + DeliveryRobotVisual.HoverClearance)
        {
            motion.y = ground.point.y + DeliveryRobotVisual.HoverClearance - actor.transform.position.y;
            falling = -2;
        }
        // One Move retains horizontal velocity for the existing WALK animation.
        actor.Move(motion);
    }
}
