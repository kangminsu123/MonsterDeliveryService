using UnityEngine;

// A small unshadowed light and one soft ring; no gameplay collider or tall beacon.
public sealed class ParkDeliveryGlow : MonoBehaviour
{
    LineRenderer ring;
    Light glow;
    Material material;
    void Awake()
    {
        var root = new GameObject("ParcelSoftGlow"); root.transform.SetParent(transform, false);
        material = new Material(Resources.Load<Shader>("FX/RunningShoesAura"));
        ring = root.AddComponent<LineRenderer>(); ring.sharedMaterial = material;
        ring.useWorldSpace = false; ring.loop = true; ring.positionCount = 48;
        ring.startWidth = ring.endWidth = .035f;
        for (int i = 0; i < 48; i++)
        {
            float angle = i * Mathf.PI * 2 / 48;
            ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * .72f, .06f, Mathf.Sin(angle) * .72f));
        }
        var lamp = new GameObject("WarmParcelLight"); lamp.transform.SetParent(root.transform, false);
        lamp.transform.localPosition = Vector3.up * .8f;
        glow = lamp.AddComponent<Light>(); glow.type = LightType.Point;
        glow.color = new Color(1, .76f, .32f); glow.range = 2.2f; glow.intensity = .35f; glow.shadows = LightShadows.None;
        Animate();
        SetVisible(GetComponent<SpecialDeliveryParcel>().Available);
    }
    public void SetVisible(bool visible) { if (ring) ring.gameObject.SetActive(visible); }
    void Update() => Animate();
    void Animate()
    {
        float pulse = .5f + .5f * Mathf.Sin(Time.time * 2);
        ring.startColor = ring.endColor = new Color(1, .77f, .3f, .24f + pulse * .12f);
        glow.intensity = .35f + pulse * .15f;
    }
    void OnDestroy() { if (material) Destroy(material); }
}
