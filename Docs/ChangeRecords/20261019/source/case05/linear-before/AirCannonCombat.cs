using UnityEngine;

public sealed partial class MonsterDeliveryPrototype
{
    public const float AirCannonRadius = 5;
    const float AirCannonSpeed = 18, AirCannonPush = 60, AirCannonLift = 18;

    void FireAirCannon(int source, Vector3 origin, Vector3 aim)
    {
        if (NetworkScreens && !NetServer) return;
        aim.Normalize();
        // Match slime's muzzle clearance and ballistic velocity, including nearby cover.
        if (NetRay(source, origin, aim, .75f, .23f, out var cover, out var point, out var normal))
        {
            ReplicateNetImpact(WeaponKind.AirCannon, point, normal, cover);
            AirCannonImpact(point, source, normal); return;
        }
        var orb = AirCannonFlight.Create(origin + aim * .75f);
        orb.name = "ThrownAirCannon";
        var collider = orb.AddComponent<SphereCollider>(); collider.center = Vector3.zero; collider.radius = .23f;
        var body = orb.AddComponent<Rigidbody>(); body.useGravity = true;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.linearVelocity = aim * AirCannonSpeed + Vector3.up * 3;
        var actor = ActorController(source); if (actor) Physics.IgnoreCollision(collider, actor);
        var shot = orb.AddComponent<BrawlShot>();
        shot.kind = WeaponKind.AirCannon; shot.sourceId = source; shot.owner = this;
    }

    public void AirCannonImpact(Vector3 point, int source, Vector3 normal)
    {
        if (!started || finished || mapEditMode || NetworkScreens && !NetServer) return;
        ShowAirCannonImpact(point);
        for (int target = 0; target <= BotCount; target++)
        {
            var actor = ActorController(target);
            if (target == source || !actor || !actor.gameObject.activeInHierarchy || !NetCanHit(source, target)) continue;
            Vector3 toward = actor.bounds.center - point;
            if (toward.sqrMagnitude > AirCannonRadius * AirCannonRadius) continue;
            // Start just above a floor impact, so its own floor cannot hide every target.
            var sight = point + normal * .05f + Vector3.up * .08f;
            var ray = actor.bounds.center - sight;
            bool blocked = false;
            foreach (var hit in Physics.RaycastAll(sight, ray.normalized, ray.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.collider.GetComponentInParent<CharacterController>()) { blocked = true; break; }
            if (blocked) continue;
            toward.y = 0;
            if (toward.sqrMagnitude < .001f)
            { toward = actor.transform.position - ActorController(source).transform.position; toward.y = 0; }
            if (toward.sqrMagnitude < .001f) toward = Vector3.forward;
            ApplyItemHit(target, new DeliveryItemHit(toward.normalized * AirCannonPush + Vector3.up * AirCannonLift));
        }
    }

    void ShowAirCannonImpact(Vector3 point)
    {
        var go = new GameObject("AirCannonExplosion");
        go.AddComponent<SpecialDeliveryPulse>().Initialize(WeaponKind.AirCannon, point, Vector3.up);
    }
}

// Identical physics-free artwork on host and participants; only BrawlShot owns collisions.
public sealed class AirCannonFlight : MonoBehaviour
{
    Material material;
    public static GameObject Create(Vector3 position)
    {
        var root = new GameObject("AirCannonOrb"); root.transform.position = position;
        var view = root.AddComponent<AirCannonFlight>();
        view.material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default"));
        view.material.color = new Color(.25f, .9f, 1);
        var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        var collider = ball.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
        ball.transform.SetParent(root.transform, false); ball.transform.localScale = Vector3.one * .46f;
        ball.GetComponent<Renderer>().sharedMaterial = view.material;
        var trail = root.AddComponent<TrailRenderer>(); trail.sharedMaterial = view.material;
        trail.time = .18f; trail.startWidth = .24f; trail.endWidth = .02f; trail.Clear();
        return root;
    }
    void OnDestroy() { if (material) Destroy(material); }
}
