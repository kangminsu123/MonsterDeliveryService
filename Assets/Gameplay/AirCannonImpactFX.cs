using UnityEngine;

// Visual only. Damage and launch are applied once by the authoritative impact handler.
public sealed class AirCannonImpactFX : MonoBehaviour
{
    const float Lifetime = .65f;
    readonly LineRenderer[] rings = new LineRenderer[3];
    Material material, puffMaterial;
    Texture2D puff;
    float began, radius;
    public float Age => Time.time - began;

    public void Initialize(Vector3 point, Vector3 normal, float blastRadius)
    {
        began = Time.time; radius = blastRadius;
        transform.SetPositionAndRotation(point + normal * .06f,
            Quaternion.LookRotation(normal.sqrMagnitude > .001f ? normal : Vector3.up));
        // A small radial texture makes soft air puffs rather than square particles.
        puff = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        var pixels = new Color[32 * 32];
        for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
        {
            float r = new Vector2((x - 15.5f) / 15.5f, (y - 15.5f) / 15.5f).magnitude;
            pixels[y * 32 + x] = new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1 - r), 2));
        }
        puff.SetPixels(pixels); puff.Apply(); puff.wrapMode = TextureWrapMode.Clamp;
        material = new Material(Shader.Find("Sprites/Default"));
        puffMaterial = new Material(material); puffMaterial.mainTexture = puff;
        for (int i = 0; i < rings.Length; i++)
        {
            var ring = new GameObject("AirShockwave").AddComponent<LineRenderer>();
            ring.transform.SetParent(transform, false); ring.sharedMaterial = material;
            ring.useWorldSpace = false; ring.loop = true; ring.positionCount = 48;
            rings[i] = ring;
        }
        var particles = gameObject.AddComponent<ParticleSystem>(); particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.loop = false; main.playOnAwake = false; main.duration = Lifetime;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.22f, .48f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(6, 12);
        main.startSize = new ParticleSystem.MinMaxCurve(.35f, .8f);
        main.startColor = new Color(.7f, 1, 1); main.maxParticles = 40;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = particles.emission; emission.enabled = false;
        var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Hemisphere; shape.radius = .1f;
        var colors = particles.colorOverLifetime; colors.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.cyan,1)},
            new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)});
        colors.color = gradient;
        particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = puffMaterial;
        particles.Play(); particles.Emit(28);
        particles.Emit(new ParticleSystem.EmitParams { position = point + normal * .08f,
            velocity = Vector3.zero, startSize = 2, startLifetime = .14f, startColor = Color.white }, 1);
        Draw(0);
    }

    void Update()
    {
        if (Age >= Lifetime) { Destroy(gameObject); return; }
        Draw(Age);
    }
    void Draw(float age)
    {
        float t = Mathf.Clamp01(age / Lifetime);
        float size = Mathf.Lerp(.15f, radius, 1 - Mathf.Pow(1 - Mathf.Clamp01(age / .3f), 3));
        for (int plane = 0; plane < rings.Length; plane++)
        {
            var line = rings[plane];
            var color = new Color(.5f, 1, 1, (1 - t) * (plane == 0 ? 1 : .55f));
            line.startColor = line.endColor = color;
            line.startWidth = line.endWidth = .26f * (1 - t) + .03f;
            for (int i = 0; i < line.positionCount; i++)
            {
                float a = i * Mathf.PI * 2 / line.positionCount;
                float x = Mathf.Cos(a) * size, y = Mathf.Sin(a) * size;
                line.SetPosition(i, plane == 0 ? new Vector3(x,y,0) : plane == 1 ? new Vector3(x,0,y) : new Vector3(0,x,y));
            }
        }
    }
    void OnDestroy() { if (material) Destroy(material); if (puffMaterial) Destroy(puffMaterial); if (puff) Destroy(puff); }
}
