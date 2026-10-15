using UnityEngine;
using UnityEngine.Rendering;

// Presentation only. A hit burst and ink stains follow the struck actor, without colliders.
public sealed class FountainInkHit : MonoBehaviour
{
    readonly MeshRenderer[] stains = new MeshRenderer[7];
    readonly Vector3[] stainPositions = new Vector3[7];
    readonly LineRenderer[] rings = new LineRenderer[2];
    MaterialPropertyBlock properties;
    ParticleSystem burst;
    float hitAt = float.NegativeInfinity;
    public bool Visible => stains[0] && stains[0].enabled;
    public ParticleSystem Burst => burst;

    public void Initialize(Material ink, Material glow)
    {
        properties = new MaterialPropertyBlock();
        for (int i = 0; i < stains.Length; i++)
        {
            var spot = GameObject.CreatePrimitive(PrimitiveType.Quad);
            spot.name = "InkStain"; spot.transform.SetParent(transform, false);
            var collider = spot.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
            float angle = i * 2.39996f;
            stainPositions[i] = i == 0 ? new Vector3(0, .28f, -.65f) :
                new Vector3(Mathf.Cos(angle) * .38f, Mathf.Sin(angle) * .32f, -.64f - i * .003f);
            spot.transform.localPosition = stainPositions[i];
            spot.transform.localRotation = Quaternion.Euler(0, 0, i * 53);
            float size = i == 0 ? .95f : .55f + i % 3 * .12f;
            spot.transform.localScale = new Vector3(size, size * (i == 0 ? .7f : 1.15f), 1);
            stains[i] = spot.GetComponent<MeshRenderer>(); stains[i].sharedMaterial = ink;
            stains[i].shadowCastingMode = ShadowCastingMode.Off; stains[i].receiveShadows = false;
        }
        for (int i = 0; i < rings.Length; i++)
        {
            var ring = new GameObject("InkImpactRipple"); ring.transform.SetParent(transform, false);
            rings[i] = ring.AddComponent<LineRenderer>(); rings[i].sharedMaterial = glow;
            rings[i].useWorldSpace = false; rings[i].loop = true; rings[i].positionCount = 48;
            rings[i].widthMultiplier = .07f; rings[i].shadowCastingMode = ShadowCastingMode.Off;
        }
        var puff = new GameObject("InkHitBurst"); puff.transform.SetParent(transform, false);
        burst = puff.AddComponent<ParticleSystem>(); burst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = burst.main; main.loop = false; main.playOnAwake = false; main.duration = .7f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.45f, .7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.4f, 4.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(.18f, .34f);
        main.startColor = new Color(.13f, .025f, .24f, 1); main.maxParticles = 64;
        main.simulationSpace = ParticleSystemSimulationSpace.World; main.gravityModifier = .35f;
        var emission = burst.emission; emission.rateOverTime = 0;
        var shape = burst.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .38f;
        var color = burst.colorOverLifetime; color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, .45f), new GradientAlphaKey(0, 1) });
        color.color = gradient;
        var sizeOverTime = burst.sizeOverLifetime; sizeOverTime.enabled = true;
        sizeOverTime.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, 1, 1, .35f));
        burst.GetComponent<ParticleSystemRenderer>().sharedMaterial = ink;
    }

    public void Play(Vector3 impact)
    {
        hitAt = Time.time; burst.transform.position = impact;
        burst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); burst.Play(true); burst.Emit(48);
    }

    public void Show(float until, bool showWorld, Transform viewer)
    {
        transform.rotation = viewer.rotation;
        float age = Time.time - hitAt;
        bool active = showWorld && Time.time < until;
        float fade = Mathf.Clamp01((until - Time.time) / .65f);
        properties.SetColor("_Color", new Color(.055f, .009f, .10f, .94f * fade));
        for (int i = 0; i < stains.Length; i++)
        {
            stains[i].enabled = active; stains[i].SetPropertyBlock(properties);
            stains[i].transform.localPosition = stainPositions[i] + Vector3.down * Mathf.Clamp01(age / 4) * .12f;
        }
        for (int i = 0; i < rings.Length; i++)
        {
            float phase = (age - i * .07f) / .45f;
            rings[i].enabled = active && phase >= 0 && phase < 1;
            if (!rings[i].enabled) continue;
            float radius = Mathf.Lerp(.45f, 1.35f, phase);
            rings[i].startColor = rings[i].endColor = new Color(.65f, .23f, 1, (1 - phase) * .85f);
            for (int p = 0; p < 48; p++)
            {
                float angle = p * Mathf.PI * 2 / 48;
                rings[i].SetPosition(p, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * .8f, -.68f));
            }
        }
        burst.GetComponent<ParticleSystemRenderer>().enabled = showWorld && age < .8f;
    }
}
