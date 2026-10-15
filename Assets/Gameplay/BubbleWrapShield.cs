using UnityEngine;
using UnityEngine.Rendering;

// Presentation only: the transparent bubble has no gameplay collider.
public sealed class BubbleWrapShield : MonoBehaviour
{
    public static readonly Color ShieldColor = new(.35f, 1, .8f);
    public MeshRenderer Shell { get; private set; }
    public ParticleSystem Pop { get; private set; }
    public bool Visible => Shell.enabled;
    Material shellMaterial, popMaterial;
    float shownAt, lastBreak = float.NegativeInfinity;
    bool wasActive;

    void Awake()
    {
        var shell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        shell.name = "ProtectiveBubble"; shell.transform.SetParent(transform, false);
        shell.transform.localPosition = Vector3.up * .7f;
        var collider = shell.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
        Shell = shell.GetComponent<MeshRenderer>();
        Shell.shadowCastingMode = ShadowCastingMode.Off; Shell.receiveShadows = false; Shell.enabled = false;
        shellMaterial = new Material(Resources.Load<Shader>("FX/BubbleWrapShield")) { name = "AirCapProtection" };
        shellMaterial.SetColor("_Color", ShieldColor); Shell.sharedMaterial = shellMaterial;

        var pop = new GameObject("ShieldPop"); pop.transform.SetParent(transform, false);
        pop.transform.localPosition = Vector3.up * .7f;
        Pop = pop.AddComponent<ParticleSystem>();
        Pop.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = Pop.main;
        main.loop = false; main.playOnAwake = false; main.duration = .45f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.3f, .45f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.8f, 2.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(.045f, .085f);
        main.startColor = ShieldColor; main.maxParticles = 48;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = Pop.emission; emission.rateOverTime = 0;
        var shape = Pop.shape; shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 1.15f; shape.radiusThickness = .08f;
        var color = Pop.colorOverLifetime; color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(ShieldColor, .3f),
            new GradientColorKey(ShieldColor, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
        color.color = gradient;
        var size = Pop.sizeOverLifetime; size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, 1, 1, 0));
        var renderer = Pop.GetComponent<ParticleSystemRenderer>();
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        popMaterial = new Material(Resources.Load<Shader>("FX/RunningShoesAura")) { name = "AirCapPop" };
        renderer.sharedMaterial = popMaterial;
    }

    public void Show(float shieldUntil, float brokenAt, bool showWorld)
    {
        bool active = Time.time < shieldUntil;
        float age = Time.time - brokenAt;
        bool breaking = age >= 0 && age < .45f;
        if (active && !wasActive) shownAt = Time.time;
        wasActive = active;
        Shell.enabled = showWorld && (active || breaking);
        float expand = active ? Mathf.SmoothStep(.85f, 1, (Time.time - shownAt) / .2f) : 1 + Mathf.Clamp01(age / .45f) * .3f;
        Shell.transform.localScale = new Vector3(2.5f, 2.65f, 2.5f) * expand;
        shellMaterial.SetFloat("_Alpha", active ? Mathf.Min(1, (shieldUntil - Time.time) * 2) : breaking ? 1 - age / .45f : 0);
        shellMaterial.SetFloat("_Hit", breaking ? 1 - age / .45f : 0);
        shellMaterial.SetFloat("_BubbleTime", Time.time);
        if (breaking && brokenAt != lastBreak)
        {
            lastBreak = brokenAt;
            if (showWorld) { Pop.Play(true); Pop.Emit(28); }
        }
        if (!showWorld || (!active && !breaking)) Pop.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    void OnDestroy()
    {
        if (shellMaterial) Destroy(shellMaterial);
        if (popMaterial) Destroy(popMaterial);
    }
}
