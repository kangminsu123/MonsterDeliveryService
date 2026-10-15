using UnityEngine;
using UnityEngine.Rendering;

// World-only wind rises around the floating body; no collider or gameplay state.
public sealed class RunningShoesAura : MonoBehaviour
{
    public static readonly Color WindColor = new(.42f, .75f, 1);
    public ParticleSystem Particles { get; private set; }
    public LineRenderer[] Ribbons { get; private set; }
    public TrailRenderer[] Trails { get; private set; }
    public bool Visible { get; private set; }
    Material material;
    Vector3 previousPosition;
    float shownAt;

    void Awake()
    {
        var root = new GameObject("RisingWind");
        root.transform.SetParent(transform, false);
        // The controller origin already floats one metre above the ground.
        // Start inside the lower torso, rather than in the empty space below it.
        root.transform.localPosition = new Vector3(0, .25f, 0);
        root.transform.localRotation = Quaternion.Euler(-90, 0, 0);
        Particles = root.AddComponent<ParticleSystem>();
        Particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = Particles.main;
        main.loop = true; main.playOnAwake = false; main.duration = 1;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.65f, .85f);
        main.startSpeed = 0; main.startSize = new ParticleSystem.MinMaxCurve(.025f, .045f);
        main.maxParticles = 48; main.simulationSpace = ParticleSystemSimulationSpace.Local;
        var emission = Particles.emission; emission.rateOverTime = 24;
        var shape = Particles.shape; shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 1.05f; shape.radiusThickness = .25f;
        var velocity = Particles.velocityOverLifetime; velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        // Unity requires all velocity axes to use the same curve mode.
        velocity.x = new ParticleSystem.MinMaxCurve(0, 0);
        velocity.y = new ParticleSystem.MinMaxCurve(0, 0);
        velocity.z = new ParticleSystem.MinMaxCurve(1.1f, 1.4f);
        var noise = Particles.noise; noise.enabled = true; noise.strength = .1f; noise.frequency = 1.1f;
        var colors = Particles.colorOverLifetime; colors.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(WindColor, .35f), new GradientColorKey(WindColor, 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(.5f, .15f), new GradientAlphaKey(.25f, .65f), new GradientAlphaKey(0, 1) });
        colors.color = gradient;
        var size = Particles.sizeOverLifetime; size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, 1, 1, .35f));
        var renderer = Particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch; renderer.lengthScale = 2; renderer.velocityScale = .06f;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        material = new Material(Resources.Load<Shader>("FX/RunningShoesAura")) { name = "RunningShoesWind" };
        renderer.sharedMaterial = material;

        // Three swept arcs, each with a soft glow and a thin, bright core.
        Ribbons = new LineRenderer[6];
        for (int i = 0; i < Ribbons.Length; i++)
        {
            var ribbon = new GameObject("OrbitWind_" + i).AddComponent<LineRenderer>();
            ribbon.transform.SetParent(transform, false);
            ribbon.sharedMaterial = material; ribbon.useWorldSpace = false;
            ribbon.positionCount = 32; ribbon.textureMode = LineTextureMode.Stretch;
            ribbon.numCapVertices = 4; ribbon.numCornerVertices = 2;
            ribbon.widthCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(.18f, 1),
                new Keyframe(.7f, .7f), new Keyframe(1, 0));
            ribbon.widthMultiplier = i % 2 == 0 ? .2f : .055f;
            ribbon.colorGradient = WindGradient(i % 2 == 0 ? .18f : .75f);
            ribbon.shadowCastingMode = ShadowCastingMode.Off; ribbon.receiveShadows = false;
            Ribbons[i] = ribbon;
        }
        Trails = new TrailRenderer[2];
        for (int i = 0; i < Trails.Length; i++)
        {
            var trail = new GameObject("BoostSlipstream_" + i).AddComponent<TrailRenderer>();
            trail.transform.SetParent(transform, false);
            trail.transform.localPosition = new Vector3(i == 0 ? -.55f : .55f, .65f, -.25f);
            trail.sharedMaterial = material; trail.time = .24f; trail.minVertexDistance = .04f;
            trail.widthCurve = AnimationCurve.EaseInOut(0, 1, 1, 0); trail.widthMultiplier = .16f;
            trail.colorGradient = WindGradient(.65f); trail.emitting = false; trail.autodestruct = false;
            trail.shadowCastingMode = ShadowCastingMode.Off; trail.receiveShadows = false;
            Trails[i] = trail;
        }
    }

    static Gradient WindGradient(float alpha)
    {
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(new Color(.75f, .92f, 1), 0),
            new GradientColorKey(WindColor, .45f), new GradientColorKey(WindColor, 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(alpha, .2f),
                new GradientAlphaKey(alpha * .6f, .7f), new GradientAlphaKey(0, 1) });
        return gradient;
    }

    void PoseWind(float time)
    {
        float entrance = Mathf.SmoothStep(.75f, 1, (time - shownAt) / .22f);
        for (int band = 0; band < 3; band++)
        for (int j = 0; j < 32; j++)
        {
            float t = j / 31f;
            float angle = time * (band == 1 ? -4.5f : 4.5f) + band * 2.1f + t * Mathf.PI * 1.35f;
            float radius = entrance * (1.02f + .06f * Mathf.Sin(time * 3 + band));
            var point = new Vector3(Mathf.Cos(angle) * radius,
                .35f + band * .36f + (t - .5f) * .24f + Mathf.Sin(angle) * .12f,
                Mathf.Sin(angle) * radius);
            Ribbons[band * 2].SetPosition(j, point); Ribbons[band * 2 + 1].SetPosition(j, point);
        }
    }

    void LateUpdate()
    {
        if (!Visible) return;
        PoseWind(Time.time);
        bool moving = (transform.position - previousPosition).sqrMagnitude > .0001f;
        foreach (var trail in Trails) trail.emitting = moving;
        previousPosition = transform.position;
    }

    public void Show(bool visible)
    {
        if (Visible == visible) return;
        Visible = visible;
        foreach (var trail in Trails) { trail.emitting = false; trail.Clear(); }
        gameObject.SetActive(visible);
        if (visible)
        {
            shownAt = Time.time; previousPosition = transform.position;
            PoseWind(Time.time); Particles.Play(true);
        }
        else Particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    void OnDestroy() { if (material) Destroy(material); }
}
