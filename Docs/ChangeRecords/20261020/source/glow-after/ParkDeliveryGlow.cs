using UnityEngine;

// Warm pulsing light, a ground ring and six orbiting sparkles; no gameplay collider.
public sealed class ParkDeliveryGlow : MonoBehaviour
{
    LineRenderer ring;
    LineRenderer[] sparkles = new LineRenderer[12];
    Light glow;
    Material material;
    void Awake()
    {
        var root = new GameObject("ParcelSoftGlow"); root.transform.SetParent(transform, false);
        material = new Material(Resources.Load<Shader>("FX/RunningShoesAura"));
        ring = root.AddComponent<LineRenderer>(); ring.sharedMaterial = material;
        ring.useWorldSpace = false; ring.loop = true; ring.positionCount = 48;
        ring.startWidth = ring.endWidth = .085f;
        for (int i = 0; i < 48; i++)
        {
            float angle = i * Mathf.PI * 2 / 48;
            ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * .72f, .06f, Mathf.Sin(angle) * .72f));
        }
        for (int i = 0; i < sparkles.Length; i++)
        {
            var spark = new GameObject("GoldSparkle_" + i).AddComponent<LineRenderer>();
            spark.transform.SetParent(root.transform, false); spark.sharedMaterial = material;
            spark.useWorldSpace = false; spark.positionCount = 2;
            spark.startWidth = spark.endWidth = .035f;
            sparkles[i] = spark;
        }
        var lamp = new GameObject("WarmParcelLight"); lamp.transform.SetParent(root.transform, false);
        lamp.transform.localPosition = Vector3.up * .8f;
        glow = lamp.AddComponent<Light>(); glow.type = LightType.Point;
        glow.color = new Color(1, .76f, .32f); glow.range = 3.2f; glow.intensity = 1; glow.shadows = LightShadows.None;
        Animate();
        SetVisible(GetComponent<SpecialDeliveryParcel>().Available);
    }
    public void SetVisible(bool visible) { if (ring) ring.gameObject.SetActive(visible); }
    void Update() => Animate();
    void Animate()
    {
        float pulse = .5f + .5f * Mathf.Sin(Time.time * 2);
        ring.startColor = ring.endColor = new Color(1, .77f, .3f, .55f + pulse * .25f);
        glow.intensity = .9f + pulse * .5f;
        var right = transform.InverseTransformDirection(Camera.main ? Camera.main.transform.right : Vector3.right);
        for (int i = 0; i < 6; i++)
        {
            float angle = Time.time * .55f + i * Mathf.PI / 3;
            float twinkle = .5f + .5f * Mathf.Sin(Time.time * 3.5f + i * 2);
            var at = new Vector3(Mathf.Cos(angle) * .65f, .5f + i % 3 * .32f + Mathf.Sin(Time.time * 1.4f + i) * .14f, Mathf.Sin(angle) * .65f);
            float size = .045f + twinkle * .065f;
            for (int axis = 0; axis < 2; axis++)
            {
                var spark = sparkles[i * 2 + axis];
                var offset = (axis == 0 ? Vector3.up : right) * size;
                spark.SetPosition(0, at - offset); spark.SetPosition(1, at + offset);
                spark.startColor = spark.endColor = new Color(1, .87f, .48f, .35f + twinkle * .6f);
            }
        }
    }
    void OnDestroy() { if (material) Destroy(material); }
}
