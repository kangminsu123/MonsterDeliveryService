using UnityEngine;

// Warm pulsing light, a ground ring and six orbiting sparkles; no gameplay collider.
public sealed class ParkDeliveryGlow : MonoBehaviour
{
    LineRenderer ring;
    LineRenderer spiral;
    LineRenderer[] sparkles = new LineRenderer[24];
    Light glow;
    Material material;
    void Awake()
    {
        var root = new GameObject("ParcelSoftGlow"); root.transform.SetParent(transform, false);
        material = new Material(Resources.Load<Shader>("FX/RunningShoesAura"));
        ring = root.AddComponent<LineRenderer>(); ring.sharedMaterial = material;
        ring.useWorldSpace = false; ring.loop = true; ring.positionCount = 48;
        ring.startWidth = ring.endWidth = .18f;
        for (int i = 0; i < 48; i++)
        {
            float angle = i * Mathf.PI * 2 / 48;
            ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * .72f, .06f, Mathf.Sin(angle) * .72f));
        }
        spiral = new GameObject("RisingGoldSpiral").AddComponent<LineRenderer>();
        spiral.transform.SetParent(root.transform, false); spiral.sharedMaterial = material;
        spiral.useWorldSpace = false; spiral.positionCount = 64;
        spiral.startWidth = spiral.endWidth = .075f;
        for (int i = 0; i < sparkles.Length; i++)
        {
            var spark = new GameObject("GoldSparkle_" + i).AddComponent<LineRenderer>();
            spark.transform.SetParent(root.transform, false); spark.sharedMaterial = material;
            spark.useWorldSpace = false; spark.positionCount = 2;
            spark.startWidth = spark.endWidth = .065f;
            sparkles[i] = spark;
        }
        var lamp = new GameObject("WarmParcelLight"); lamp.transform.SetParent(root.transform, false);
        lamp.transform.localPosition = Vector3.up * .8f;
        glow = lamp.AddComponent<Light>(); glow.type = LightType.Point;
        glow.color = new Color(1, .76f, .32f); glow.range = 5; glow.intensity = 4; glow.shadows = LightShadows.None;
        Animate();
        SetVisible(GetComponent<SpecialDeliveryParcel>().Available);
    }
    public void SetVisible(bool visible) { if (ring) ring.gameObject.SetActive(visible); }
    void Update() => Animate();
    void Animate()
    {
        float pulse = .5f + .5f * Mathf.Sin(Time.time * 2);
        ring.startColor = ring.endColor = new Color(1, .8f, .32f, .85f + pulse * .15f);
        glow.intensity = 3 + pulse * 2;
        spiral.startColor = new Color(1, .75f, .2f, .9f);
        spiral.endColor = new Color(1, .95f, .6f, .25f);
        for (int i = 0; i < spiral.positionCount; i++)
        {
            float t = i / (float)(spiral.positionCount - 1);
            float angle = t * Mathf.PI * 4 - Time.time * 1.8f;
            spiral.SetPosition(i, new Vector3(Mathf.Cos(angle) * .88f, .12f + t * 1.9f, Mathf.Sin(angle) * .88f));
        }
        var right = transform.InverseTransformDirection(Camera.main ? Camera.main.transform.right : Vector3.right);
        for (int i = 0; i < 12; i++)
        {
            float angle = Time.time * .8f + i * Mathf.PI / 6;
            float twinkle = .5f + .5f * Mathf.Sin(Time.time * 3.5f + i * 2);
            var at = new Vector3(Mathf.Cos(angle) * .85f, .4f + i % 4 * .4f + Mathf.Sin(Time.time * 1.4f + i) * .2f, Mathf.Sin(angle) * .85f);
            float size = .09f + twinkle * .12f;
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
