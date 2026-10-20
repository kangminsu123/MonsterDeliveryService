using UnityEngine;

// Cosmetic action effect. Stun, immunity and teams remain in the shared hit path.
public sealed class BroadcasterFX : MonoBehaviour
{
    public const float Lifetime = 1.3f;
    readonly LineRenderer[] lines = new LineRenderer[14];
    Material material;
    Light beacon;
    float began;
    public float Age => Time.time - began;

    public void Initialize(Vector3 origin, float elapsed = 0)
    {
        transform.position = origin; began = Time.time - elapsed;
        material = new Material(Shader.Find("Sprites/Default"));
        for (int i = 0; i < lines.Length; i++)
        {
            var line = new GameObject(i < 5 ? "BroadcastWave" : i == 5 ? "WarningBeacon" : "SoundRay").AddComponent<LineRenderer>();
            line.transform.SetParent(transform, false); line.sharedMaterial = material;
            line.useWorldSpace = false; line.loop = i < 3 || i == 5;
            line.positionCount = i < 6 ? 64 : 9; lines[i] = line;
        }
        beacon = gameObject.AddComponent<Light>(); beacon.type = LightType.Point;
        beacon.color = new Color(1, .36f, .06f); beacon.range = 5; beacon.shadows = LightShadows.None;
        var particles = gameObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main; main.loop = false; main.playOnAwake = false; main.duration = Lifetime;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.45f, .85f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3, 8);
        main.startSize = new ParticleSystem.MinMaxCurve(.07f, .15f);
        main.startColor = new Color(1, .7f, .12f); main.maxParticles = 48;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = particles.emission; emission.enabled = false;
        var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = .5f; shape.rotation = new Vector3(90, 0, 0); shape.position = Vector3.down * .7f;
        var colors = particles.colorOverLifetime; colors.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[]{new GradientColorKey(Color.yellow,0),new GradientColorKey(new Color(1,.2f,.04f),1)},
            new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)}); colors.color = gradient;
        var renderer = particles.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Stretch; renderer.lengthScale = 2; renderer.velocityScale = .06f;
        particles.useAutoRandomSeed = false; particles.randomSeed = 711;
        particles.Play(); particles.Emit(40);
        if (elapsed > 0) { particles.Simulate(elapsed, true, false); particles.Play(); }
        Draw();
    }

    void Update()
    {
        if (Age >= Lifetime) { Destroy(gameObject); return; }
        Draw();
    }
    void Draw()
    {
        float age = Age, fade = Mathf.Clamp01(1 - age / Lifetime);
        beacon.intensity = (2 + 2 * Mathf.Sin(age * 12)) * fade;
        for (int index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            float t = Mathf.Clamp01((age - (index < 3 ? index * .14f : 0)) / .82f);
            line.enabled = index >= 3 || age >= index * .14f;
            var color = Color.Lerp(new Color(1,.22f,.03f), new Color(1,.9f,.2f), .5f+.5f*Mathf.Sin(age*10-index));
            color.a = index < 5 ? (1 - t) * fade : fade;
            line.startColor = line.endColor = color;
            line.startWidth = line.endWidth = index < 3 ? .24f * (1-t) + .035f : index < 6 ? .11f : .08f;
            for (int i = 0; i < line.positionCount; i++)
            {
                float u = i / (float)(line.loop ? line.positionCount : line.positionCount - 1);
                float angle = u * Mathf.PI * 2;
                float radius = MonsterDeliveryPrototype.BroadcasterRadius * t;
                Vector3 point;
                if (index < 3) point = new Vector3(Mathf.Cos(angle)*radius, -1.8f+index*.07f, Mathf.Sin(angle)*radius);
                else if (index < 5)
                {
                    angle = u * Mathf.PI;
                    point = index == 3 ? new Vector3(Mathf.Cos(angle)*radius, -1.75f+Mathf.Sin(angle)*radius,0) :
                        new Vector3(0,-1.75f+Mathf.Sin(angle)*radius,Mathf.Cos(angle)*radius);
                }
                else if (index == 5)
                {
                    angle += age * 8; radius = .55f + .15f*Mathf.Sin(age*12);
                    point = new Vector3(Mathf.Cos(angle)*radius,-.65f,Mathf.Sin(angle)*radius);
                }
                else
                {
                    angle = (index-6)*Mathf.PI/4;
                    radius = .7f + u * Mathf.Min(7,age*18);
                    point = new Vector3(Mathf.Cos(angle)*radius,-1.35f+Mathf.Sin(u*Mathf.PI*4-age*16)*.18f,Mathf.Sin(angle)*radius);
                }
                line.SetPosition(i,point);
            }
        }
    }
    void OnDestroy() { if (material) Destroy(material); }
}
