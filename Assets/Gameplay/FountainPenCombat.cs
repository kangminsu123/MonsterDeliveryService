using UnityEngine;

public sealed partial class MonsterDeliveryPrototype
{
    const float FountainPenRange = 35, InkBlindSeconds = 4;
    float fountainThrownAt = float.NegativeInfinity;
    Texture2D inkSplat;
    Material inkTrailMaterial, inkHitMaterial, surfaceInkMaterial;
    readonly FountainInkHit[] fountainInkHits = new FountainInkHit[BotCount + 1];

    Material InkTrailMaterial
    {
        get
        {
            if (!inkTrailMaterial) inkTrailMaterial = new Material(Resources.Load<Shader>("FX/RunningShoesAura"))
                { name = "FountainInkTrail" };
            return inkTrailMaterial;
        }
    }

    void MakeFountainPenShot(Vector3 aim, float speed)
    {
        aim.Normalize();
        var origin = cam.transform.position;
        var target = origin + aim * FountainPenRange;
        var muzzle = Selected == WeaponKind.FountainPen && heldItem
            ? heldItem.transform.TransformPoint(Vector3.forward * .51f) : origin + aim * .55f;
        var toMuzzle = muzzle - origin;
        if (InteractionRayHit(new Ray(origin, toMuzzle.normalized), toMuzzle.magnitude, out var hit))
        { FountainPenImpact(hit.collider, hit.point, 0, hit.normal); return; }
        LaunchFountainPen(muzzle, (target - muzzle).normalized, speed, 0);
    }

    BrawlShot LaunchFountainPen(Vector3 origin, Vector3 direction, float speed, int sourceId)
    {
        var pen = Instantiate(Resources.Load<GameObject>("DeliveryItems/FountainPen"));
        pen.name = "ThrownFountainPen"; pen.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(direction));
        var collider = pen.AddComponent<SphereCollider>(); collider.radius = .07f;
        var body = pen.AddComponent<Rigidbody>(); body.useGravity = false; body.linearDamping = 0;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.linearVelocity = direction.normalized * speed;
        var source = ActorController(sourceId); if (source) Physics.IgnoreCollision(collider, source);
        var trail = pen.AddComponent<TrailRenderer>(); trail.sharedMaterial = InkTrailMaterial;
        trail.time = .1f; trail.minVertexDistance = .05f; trail.startWidth = .045f; trail.endWidth = 0;
        trail.startColor = new Color(.55f, .24f, 1, .8f); trail.endColor = new Color(.25f, .06f, .5f, 0);
        var shot = pen.AddComponent<BrawlShot>(); shot.owner = this; shot.kind = WeaponKind.FountainPen;
        shot.sourceId = sourceId; shot.maxDistance = FountainPenRange;
        return shot;
    }

    public void FountainPenImpact(Collider collider, Vector3 point, int sourceId, Vector3 normal = default)
    {
        if (!started || finished || mapEditMode) return;
        for (int id = 0; id <= BotCount; id++)
        {
            var actor = ActorController(id);
            if (id == sourceId || !actor || !actor.gameObject.activeInHierarchy ||
                !collider || collider.GetComponentInParent<CharacterController>() != actor) continue;
            if (!NetCanHit(sourceId, id)) return;
            if (ApplyItemHit(id, new DeliveryItemHit(inkSeconds: InkBlindSeconds)) != DeliveryHitResult.Applied) return;
            ShowFountainInkHit(id, point);
            if (id == 0) feedback = "만년필 잉크에 맞았습니다!";
            else if (sourceId == 0) feedback = "만년필 적중 · 상대 시야 차단!";
            return;
        }
        if (!collider || collider.GetComponentInParent<CharacterController>()) return;
        if (normal.sqrMagnitude < .001f) normal = -cam.transform.forward;
        EnsureInkSplat();
        if (!surfaceInkMaterial)
        {
            surfaceInkMaterial = new Material(Resources.Load<Shader>("FX/FountainInk")) { name = "BlackSurfaceInk" };
            surfaceInkMaterial.mainTexture = inkSplat; surfaceInkMaterial.SetColor("_EdgeColor", Color.black);
            surfaceInkMaterial.SetColor("_Color", new Color(.004f, .004f, .004f, .98f));
        }
        var mark = new GameObject("SurfaceInkMark");
        mark.AddComponent<SurfaceInkMark>().Initialize(collider, point, normal.normalized, surfaceInkMaterial);
        var splash = new GameObject("FountainInkSplash"); splash.transform.position = point;
        var particles = splash.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main; main.loop = false; main.playOnAwake = false; main.duration = .45f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.2f, .4f); main.startSpeed = new ParticleSystem.MinMaxCurve(.8f, 1.7f);
        main.startSize = new ParticleSystem.MinMaxCurve(.035f, .085f); main.maxParticles = 24;
        main.startColor = new Color(.36f, .1f, .6f, .8f);
        var emission = particles.emission; emission.rateOverTime = 0;
        var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .08f;
        var size = particles.sizeOverLifetime; size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, 1, 1, 0));
        particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = surfaceInkMaterial;
        particles.Play(true); particles.Emit(18); Destroy(splash, .6f);
    }

    void ShowFountainInkHit(int id, Vector3 impact)
    {
        EnsureInkSplat();
        if (!inkHitMaterial)
        {
            inkHitMaterial = new Material(Resources.Load<Shader>("FX/FountainInk")) { name = "FountainInkStain" };
            inkHitMaterial.mainTexture = inkSplat;
        }
        var actor = ActorController(id);
        if (!fountainInkHits[id])
        {
            var hit = new GameObject("FountainInkHit"); hit.transform.SetParent(actor.transform, false);
            hit.transform.localPosition = actor.center;
            fountainInkHits[id] = hit.AddComponent<FountainInkHit>();
            fountainInkHits[id].Initialize(inkHitMaterial, InkTrailMaterial);
        }
        fountainInkHits[id].transform.rotation = cam.transform.rotation;
        fountainInkHits[id].Play(impact);
        UpdateFountainInkFX();
    }

    void UpdateFountainInkFX()
    {
        for (int id = 0; id <= BotCount; id++)
            if (fountainInkHits[id]) fountainInkHits[id].Show(ActorState(id).visionBlockedUntil,
                started && !finished && !mapEditMode && (id != 0 || thirdPersonPreview), cam.transform);
    }

    void PoseFountainPen()
    {
        if (heldKind != WeaponKind.FountainPen || !heldWeapon || !heldItem) return;
        float age = Time.time - fountainThrownAt;
        heldWeapon.SetActive(age >= .12f);
        if (!thirdPersonPreview && age < .24f && age >= .12f)
        {
            heldItem.FirstPerson();
            heldItem.transform.localPosition += Vector3.down * (.08f * (1 - (age - .12f) / .12f));
        }
        else if (!thirdPersonPreview) heldItem.FirstPerson();
    }

    void EnsureInkSplat()
    {
        if (inkSplat) return;
        const int size = 256;
        var centers = new Vector2[13]; var radii = new float[13];
        centers[0] = Vector2.zero; radii[0] = .57f;
        for (int i = 1; i < centers.Length; i++)
        {
            float angle = i * 2.39996f;
            centers[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (.35f + i % 3 * .08f);
            radii[i] = .14f + i % 4 * .04f;
        }
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            var p = new Vector2(x, y) / (size - 1f) * 2 - Vector2.one;
            float alpha = 0;
            for (int i = 0; i < centers.Length; i++)
                alpha = Mathf.Max(alpha, 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(radii[i] - .035f, radii[i], Vector2.Distance(p, centers[i]))));
            pixels[y * size + x] = new Color(1, 1, 1, alpha);
        }
        inkSplat = new Texture2D(size, size, TextureFormat.RGBA32, false)
        { name = "FountainInkOnLens", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        inkSplat.SetPixels(pixels); inkSplat.Apply();
    }

    void DrawFountainInkScreen(float w, float h)
    {
        float remaining = playerItems.visionBlockedUntil - Time.time;
        if (!started || finished || mapEditMode || remaining <= 0) return;
        EnsureInkSplat();
        float spread = Mathf.SmoothStep(.8f, 1, (InkBlindSeconds - remaining) / .16f);
        var color = new Color(.028f, .012f, .065f, .98f * Mathf.Clamp01(remaining / .65f));
        var center = new Vector2(w * .5f, h * .51f); var matrix = GUI.matrix;
        GUIUtility.RotateAroundPivot(Mathf.Repeat(playerItems.visionBlockedUntil * 29, 80) - 40, center);
        GUI.DrawTexture(new Rect(center.x - w * .74f * spread, center.y - h * .86f * spread,
            w * 1.48f * spread, h * 1.72f * spread), inkSplat, ScaleMode.StretchToFill, true, 0, color, 0, 0);
        GUI.matrix = matrix;
        for (int i = 0; i < 8; i++)
        {
            float x = w * (.12f + Mathf.Repeat(i * .381f, .78f));
            float y = h * (.12f + Mathf.Repeat(i * .247f, .76f));
            float radius = 34 + i % 3 * 20;
            GUI.DrawTexture(new Rect(x, y, radius * 2, radius * 1.6f), inkSplat, ScaleMode.StretchToFill, true, 0, color, 0, 0);
        }
    }

    void DestroyFountainPenFX()
    {
        foreach (var hit in fountainInkHits) if (hit) Destroy(hit.gameObject);
        foreach (var mark in FindObjectsByType<SurfaceInkMark>())
            if (mark.Paint && mark.Paint.sharedMaterial == surfaceInkMaterial) Destroy(mark.gameObject);
        if (surfaceInkMaterial) Destroy(surfaceInkMaterial);
        if (inkHitMaterial) Destroy(inkHitMaterial);
        if (inkSplat) Destroy(inkSplat);
        if (inkTrailMaterial) Destroy(inkTrailMaterial);
    }
}
