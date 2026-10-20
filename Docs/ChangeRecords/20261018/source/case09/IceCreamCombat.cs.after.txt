using UnityEngine;

public sealed partial class MonsterDeliveryPrototype
{
    const float IceCreamSpeed = 36, FreezeSeconds = 3;
    float iceCreamThrownAt = float.NegativeInfinity;
    Material iceCreamSurfaceMaterial;
    Texture2D frostCrystal;
    readonly IceCreamFreezeEffect[] iceEffects = new IceCreamFreezeEffect[BotCount + 1];

    void MakeIceCreamShot(Vector3 aim)
    {
        aim.Normalize();
        RaycastHit cover = default; float closest = float.PositiveInfinity;
        foreach (var hit in Physics.SphereCastAll(cam.transform.position, .18f, aim, .75f, ~0, QueryTriggerInteraction.Ignore))
            if (hit.collider != player && hit.distance < closest) { cover = hit; closest = hit.distance; }
        // Also handle a wall already overlapping the spawn path.
        foreach (var collider in Physics.OverlapSphere(cam.transform.position, .18f, ~0, QueryTriggerInteraction.Ignore))
            if (collider != player) { IceCreamImpact(collider, collider.ClosestPoint(cam.transform.position), 0, -aim); return; }
        if (!float.IsPositiveInfinity(closest)) { IceCreamImpact(cover.collider, cover.point, 0, cover.normal); return; }
        LaunchIceCream(cam.transform.position + aim * .75f, aim * IceCreamSpeed + Vector3.up * 3, 0);
    }

    BrawlShot LaunchIceCream(Vector3 origin, Vector3 velocity, int sourceId)
    {
        var ice = Instantiate(Resources.Load<GameObject>("DeliveryItems/IceCream"));
        ice.name = "ThrownIceCream"; ice.transform.position = origin;
        var collider = ice.AddComponent<SphereCollider>(); collider.radius = .18f;
        var body = ice.AddComponent<Rigidbody>(); body.useGravity = true;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; body.linearVelocity = velocity;
        var source = ActorController(sourceId); if (source) Physics.IgnoreCollision(collider, source);
        var trail = ice.AddComponent<TrailRenderer>(); trail.sharedMaterial = InkTrailMaterial;
        trail.time = .14f; trail.startWidth = .1f; trail.endWidth = 0;
        trail.startColor = new Color(.55f, .9f, 1, .7f); trail.endColor = new Color(.7f, .95f, 1, 0);
        var shot = ice.AddComponent<BrawlShot>(); shot.owner = this; shot.kind = WeaponKind.IceCream; shot.sourceId = sourceId;
        return shot;
    }

    public void IceCreamImpact(Collider collider, Vector3 point, int sourceId, Vector3 normal = default)
    {
        if (!started || finished || mapEditMode || !collider) return;
        for (int id = 0; id <= BotCount; id++)
        {
            var actor = ActorController(id);
            if (id == sourceId || !actor || !actor.gameObject.activeInHierarchy ||
                collider.GetComponentInParent<CharacterController>() != actor) continue;
            if (!NetCanHit(sourceId, id)) return;
            if (ApplyItemHit(id, new DeliveryItemHit(stunSeconds: FreezeSeconds)) != DeliveryHitResult.Applied) return;
            ActorState(id).frozenUntil = ActorState(id).stunUntil;
            UpdateIceCreamFX(); UpdateStunFX();
            feedback = id == 0 ? $"아이스크림 · {FreezeSeconds:0.#}초 빙결!" : $"아이스크림 적중 · 상대 {FreezeSeconds:0.#}초 빙결!";
            return;
        }
        if (collider.GetComponentInParent<CharacterController>()) return;
        if (normal.sqrMagnitude < .001f) normal = -cam.transform.forward;
        EnsureInkSplat();
        if (!iceCreamSurfaceMaterial)
        {
            iceCreamSurfaceMaterial = new Material(Resources.Load<Shader>("FX/FountainInk")) { name = "IceCreamSurfaceSplat" };
            iceCreamSurfaceMaterial.mainTexture = inkSplat;
            iceCreamSurfaceMaterial.SetColor("_Color", new Color(1, .52f, .68f));
            iceCreamSurfaceMaterial.SetColor("_EdgeColor", new Color(.85f, .97f, 1));
        }
        var mark = new GameObject("IceCreamSurfaceMark");
        mark.AddComponent<SurfaceInkMark>().Initialize(collider, point, normal.normalized, iceCreamSurfaceMaterial,
            new Color(1, .52f, .68f), 3, 1);
    }

    void UpdateIceCreamFX()
    {
        for (int id = 0; id <= BotCount; id++)
        {
            var actor = ActorController(id);
            bool frozen = started && !finished && !mapEditMode && actor && actor.gameObject.activeInHierarchy && ActorState(id).Frozen(Time.time);
            if (frozen && !iceEffects[id])
            {
                var root = new GameObject("IceCreamFreeze"); root.transform.SetParent(actor.transform, false);
                root.transform.localPosition = actor.center;
                iceEffects[id] = root.AddComponent<IceCreamFreezeEffect>();
            }
            if (iceEffects[id]) iceEffects[id].gameObject.SetActive(frozen && (id != 0 || thirdPersonPreview));
            var visual = id == 0 ? playerVisual : botVisuals[id - 1];
            if (visual) visual.model.GetComponent<Animator>().speed = frozen ? 0 : 1;
        }
    }

    void PoseIceCreamThrow()
    {
        if (heldKind != WeaponKind.IceCream || !heldWeapon || !heldItem) return;
        float age = Time.time - iceCreamThrownAt;
        heldWeapon.SetActive(age >= .16f);
        if (!thirdPersonPreview)
        {
            heldItem.FirstPerson();
            if (age < .36f) heldItem.transform.localPosition += Vector3.down * (.18f * Mathf.Clamp01((.36f - age) / .2f));
        }
    }

    void DrawIceCreamScreen(float w, float h)
    {
        if (!started || finished || mapEditMode || !playerItems.Frozen(Time.time)) return;
        float alpha = Mathf.Clamp01((playerItems.frozenUntil - Time.time) / .15f);
        if (!frostCrystal)
        {
            frostCrystal = new Texture2D(64, 128, TextureFormat.RGBA32, false) { name = "PointedScreenFrost", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[64 * 128];
            for (int y = 0; y < 128; y++)
            for (int x = 0; x < 64; x++)
            {
                float v = y / 127f, u = Mathf.Abs(x / 63f - .5f);
                float edge = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(v * .5f - .025f, v * .5f, u));
                pixels[y * 64 + x] = new Color(.65f, .92f, 1, edge * (.18f + v * .3f));
            }
            frostCrystal.SetPixels(pixels); frostCrystal.Apply();
        }
        // Sharp frost points frame the view while leaving the centre clear.
        var matrix = GUI.matrix;
        for (int i = 0; i < 18; i++)
        {
            float x = w * i / 17f, length = 55 + i % 4 * 22;
            GUI.DrawTexture(new Rect(x - 48, -8, 96, length), frostCrystal, ScaleMode.StretchToFill, true, 0, new Color(1, 1, 1, alpha), 0, 0);
            var centre = new Vector2(x, h - length * .5f + 8);
            GUIUtility.RotateAroundPivot(180, centre);
            GUI.DrawTexture(new Rect(x - 48, h - length + 8, 96, length), frostCrystal, ScaleMode.StretchToFill, true, 0, new Color(1, 1, 1, alpha), 0, 0);
            GUI.matrix = matrix;
        }
        GUI.DrawTexture(new Rect(0, 0, 30, h), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(.6f, .9f, 1, .25f * alpha), 0, 0);
        GUI.DrawTexture(new Rect(w - 30, 0, 30, h), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(.6f, .9f, 1, .25f * alpha), 0, 0);
    }

    void DestroyIceCreamFX()
    {
        ClearIceCreamMarks();
        foreach (var effect in iceEffects) if (effect) Destroy(effect.gameObject);
        if (iceCreamSurfaceMaterial) Destroy(iceCreamSurfaceMaterial);
        if (frostCrystal) Destroy(frostCrystal);
    }

    void ClearIceCreamMarks()
    {
        foreach (var mark in FindObjectsByType<SurfaceInkMark>())
            if (mark.Paint && mark.Paint.sharedMaterial == iceCreamSurfaceMaterial) Destroy(mark.gameObject);
    }
}
