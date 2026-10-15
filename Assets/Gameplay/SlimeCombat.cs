using System.Collections.Generic;
using UnityEngine;

public sealed partial class MonsterDeliveryPrototype
{
    const float SlimeRadius = 3, SlimePoolSeconds = 5, SlimeHitSeconds = 3;
    float slimeThrownAt = float.NegativeInfinity;
    readonly SlimeSlowEffect[] slimeEffects = new SlimeSlowEffect[BotCount + 1];
    readonly List<GameObject> slimeSurfaceMarks = new();

    void MakeSlimeShot(Vector3 aim, float speed)
    {
        aim.Normalize();
        RaycastHit cover = default; float closest = float.PositiveInfinity;
        foreach (var hit in Physics.SphereCastAll(cam.transform.position, .23f, aim, .75f, ~0, QueryTriggerInteraction.Ignore))
            if (hit.collider != player && hit.distance < closest) { cover = hit; closest = hit.distance; }
        if (closest < float.PositiveInfinity)
        { SlimeImpact(cover.collider, cover.point, 0, cover.normal); return; }
        LaunchSlime(cam.transform.position + aim * .75f, aim * speed + Vector3.up * 3, 0);
    }

    BrawlShot LaunchSlime(Vector3 origin, Vector3 velocity, int sourceId)
    {
        var blob = Instantiate(Resources.Load<GameObject>("DeliveryItems/Slime"));
        blob.name = "ThrownSlime"; blob.transform.position = origin;
        blob.AddComponent<SlimeGoo>();
        var collider = blob.AddComponent<SphereCollider>(); collider.radius = .23f;
        var body = blob.AddComponent<Rigidbody>(); body.useGravity = true;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.linearVelocity = velocity;
        var source = ActorController(sourceId); if (source) Physics.IgnoreCollision(collider, source);
        var shot = blob.AddComponent<BrawlShot>(); shot.owner = this; shot.kind = WeaponKind.Slime;
        shot.sourceId = sourceId; // Gravity projectiles keep flying until a real impact.
        return shot;
    }

    public void SlimeImpact(Collider collider, Vector3 point, int sourceId, Vector3 normal)
    {
        if (!started || finished || mapEditMode) return;
        for (int id = 0; id <= BotCount; id++)
        {
            var actor = ActorController(id);
            if (id == sourceId || !actor || !collider || collider.GetComponentInParent<CharacterController>() != actor) continue;
            if (!NetCanHit(sourceId, id)) return;
            if (ApplyItemHit(id, new DeliveryItemHit(slowSeconds: SlimeHitSeconds)) != DeliveryHitResult.Applied) return;
            SlimeGoo.Splash(point);
            UpdateSlimeFX();
            feedback = id == 0 ? "슬라임 적중 · 이동 속도 -40%!" : "슬라임 적중 · 상대 이동 속도 -40%!";
            // A blocked direct hit must not immediately reapply itself as a floor hazard.
            AddSlimePuddle(actor.transform.position + Vector3.up * .1f);
            return;
        }
        if (!collider || collider.GetComponentInParent<CharacterController>()) return;
        var mark = Instantiate(Resources.Load<GameObject>("DeliveryItems/Slime"));
        mark.name = "SlimeSurfaceSplat";
        mark.transform.SetPositionAndRotation(point + normal * .04f, Quaternion.LookRotation(normal));
        mark.transform.localScale = new Vector3(1.4f, 1.6f, .25f);
        mark.transform.SetParent(collider.transform, true);
        mark.AddComponent<SlimeGoo>();
        slimeSurfaceMarks.RemoveAll(splat => !splat); slimeSurfaceMarks.Add(mark);
        Destroy(mark, SlimePoolSeconds);
        SlimeGoo.Splash(point + normal * .05f);
        AddSlimePuddle(point + normal * .06f);
    }

    void AddSlimePuddle(Vector3 point)
    {
        RaycastHit ground = default; float closest = float.PositiveInfinity;
        foreach (var hit in Physics.RaycastAll(point + Vector3.up * .35f, Vector3.down, Mathf.Infinity, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.GetComponentInParent<CharacterController>() || hit.normal.y < .65f || hit.distance >= closest) continue;
            ground = hit; closest = hit.distance;
        }
        if (float.IsPositiveInfinity(closest)) return;
        var puddle = new GameObject("SlimePuddle").AddComponent<SlimePuddle>();
        puddle.Initialize(ground.point, SlimeRadius, SlimePoolSeconds);
        itemAreas.Add(new ItemArea { kind = WeaponKind.Slime, point = ground.point,
            until = Time.time + SlimePoolSeconds, visual = puddle });
    }

    void UpdateSlimeFX()
    {
        for (int id = 0; id <= BotCount; id++)
        {
            var actor = ActorController(id);
            bool active = started && !finished && !mapEditMode && actor && actor.gameObject.activeInHierarchy && ActorState(id).slowUntil > Time.time;
            if (active && !slimeEffects[id])
            {
                var effect = new GameObject("SlimeSlowEffect"); effect.transform.SetParent(actor.transform, false);
                slimeEffects[id] = effect.AddComponent<SlimeSlowEffect>();
            }
            if (slimeEffects[id]) slimeEffects[id].gameObject.SetActive(active && (id != 0 || thirdPersonPreview));
        }
    }

    void PoseSlimeThrow()
    {
        if (heldKind != WeaponKind.Slime || !heldWeapon || !heldItem) return;
        float age = Time.time - slimeThrownAt;
        heldWeapon.SetActive(age >= .16f);
        if (!thirdPersonPreview)
        {
            heldItem.FirstPerson();
            heldItem.transform.localScale *= 1 + .035f * Mathf.Sin(Time.time * 4);
            if (age < .36f) heldItem.transform.localPosition += Vector3.down * (.18f * Mathf.Clamp01((.36f - age) / .2f));
        }
    }

    void DrawSlimeScreen(float w, float h)
    {
        float remaining = playerItems.slowUntil - Time.time;
        if (remaining <= 0 || !started || finished || mapEditMode) return;
        EnsureInkSplat();
        var tint = new Color(.35f, .95f, .045f, .55f * Mathf.Clamp01(remaining / .25f));
        for (int i = 0; i < 12; i++)
        {
            float x = w * (i / 11f), length = 110 + 75 * (.5f + .5f * Mathf.Sin(Time.time * 2 + i * 1.7f));
            GUI.DrawTexture(new Rect(x - 95, -90, 190, length * 2), inkSplat, ScaleMode.StretchToFill, true, 0, tint, 0, 0);
            GUI.DrawTexture(new Rect(x - 110, h - length * .65f, 220, length * 1.7f), inkSplat, ScaleMode.StretchToFill, true, 0, tint, 0, 0);
        }
        for (int i = 0; i < 5; i++)
        {
            float y = h * i / 4;
            GUI.DrawTexture(new Rect(-95, y - 120, 190, 240), inkSplat, ScaleMode.StretchToFill, true, 0, tint, 0, 0);
            GUI.DrawTexture(new Rect(w - 95, y - 120, 190, 240), inkSplat, ScaleMode.StretchToFill, true, 0, tint, 0, 0);
        }
        DeliveryUIArt.Image(new Rect(w * .5f - 175, h * .72f, 350, 48), "control_panel", new Color(.4f, .7f, .1f));
        DeliveryUIArt.Text(new Rect(w * .5f - 170, h * .72f, 340, 48), "슬라임 · 이동 속도 -40%", 25, new Color(.75f, 1, .35f), TextAnchor.MiddleCenter);
    }

    void ClearSlimePuddles()
    {
        foreach (var area in itemAreas) if (area.visual) Destroy(area.visual.gameObject);
        itemAreas.Clear();
        foreach (var mark in slimeSurfaceMarks) if (mark) Destroy(mark);
        slimeSurfaceMarks.Clear();
    }

    void DestroySlimeFX()
    {
        ClearSlimePuddles();
        foreach (var effect in slimeEffects) if (effect) Destroy(effect.gameObject);
    }
}
