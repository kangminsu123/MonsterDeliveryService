using UnityEngine;

// Native jelly geometry shared by the thrown blob, burst and attached sticky drips.
public sealed class SlimeGoo : MonoBehaviour
{
    Transform[] parts;
    Vector3[] sizes;
    void Awake()
    {
        parts = System.Array.ConvertAll(GetComponentsInChildren<MeshRenderer>(), r => r.transform);
        sizes = System.Array.ConvertAll(parts, t => t.localScale);
    }
    void Update()
    {
        for (int i = 0; i < parts.Length; i++)
        {
            float wobble = Mathf.Sin(Time.time * 8 + i * 1.3f) * .07f;
            parts[i].localScale = Vector3.Scale(sizes[i], new Vector3(1 + wobble, 1 - wobble, 1 + wobble));
        }
    }
    public static Transform Blob(Transform parent, string name, Vector3 point, Vector3 size, bool highlight = false)
    {
        var blob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        blob.name = name; blob.transform.SetParent(parent, false);
        blob.transform.localPosition = point; blob.transform.localScale = size;
        var collider = blob.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
        blob.GetComponent<Renderer>().sharedMaterial = Resources.Load<Material>(highlight ? "DeliveryItems/SlimeHighlight" : "DeliveryItems/SlimeGel");
        return blob.transform;
    }
    public static void Splash(Vector3 point)
    {
        var splash = new GameObject("SlimeSplash"); splash.transform.position = point;
        var particles = splash.AddComponent<ParticleSystem>(); particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main; main.loop = false; main.playOnAwake = false; main.duration = .5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.3f, .65f);
        main.startSize = new ParticleSystem.MinMaxCurve(.1f, .25f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2, 4); main.gravityModifier = 1;
        main.startColor = new Color(.48f, 1, .08f); main.maxParticles = 40;
        var emission = particles.emission; emission.rateOverTime = 0;
        var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .15f;
        var size = particles.sizeOverLifetime; size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, 1, 1, 0));
        var render = particles.GetComponent<ParticleSystemRenderer>();
        render.sharedMaterial = Resources.Load<Material>("DeliveryItems/SlimeGel");
        render.renderMode = ParticleSystemRenderMode.Mesh;
        var template = Resources.Load<GameObject>("DeliveryItems/Slime");
        render.mesh = template.GetComponentInChildren<MeshFilter>().sharedMesh;
        particles.Play(); particles.Emit(36); Destroy(splash, .8f);
    }
}
