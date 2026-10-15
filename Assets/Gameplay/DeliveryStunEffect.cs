using UnityEngine;

// Follows shared stun state, so future stun items get the same visible feedback.
public sealed class DeliveryStunEffect : MonoBehaviour
{
    readonly LineRenderer[] rings = new LineRenderer[2];
    readonly Transform[] dots = new Transform[4];
    Material glow;
    void Awake()
    {
        glow = new Material(Resources.Load<Shader>("FX/RunningShoesAura")) { name = "StunDizzyGlow" };
        for (int i = 0; i < rings.Length; i++)
        {
            var ring = new GameObject("DizzyRing").AddComponent<LineRenderer>(); ring.transform.SetParent(transform, false);
            ring.sharedMaterial = glow; ring.useWorldSpace = false; ring.loop = true; ring.positionCount = 48;
            ring.startWidth = ring.endWidth = i == 0 ? .065f : .03f;
            ring.startColor = ring.endColor = new Color(1, .8f, .12f, i == 0 ? .8f : .45f);
            rings[i] = ring;
        }
        for (int i = 0; i < dots.Length; i++)
        {
            var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere); dot.name = "OrbitingDizzyDot";
            dot.transform.SetParent(transform, false); dot.transform.localScale = Vector3.one * .11f;
            var collider = dot.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
            dot.GetComponent<Renderer>().sharedMaterial = Resources.Load<Material>("DeliveryItems/HammerYellow"); dots[i] = dot.transform;
        }
    }
    void Update()
    {
        for (int i = 0; i < rings.Length; i++)
        for (int n = 0; n < rings[i].positionCount; n++)
        {
            float angle = n * Mathf.PI * 2 / rings[i].positionCount;
            rings[i].SetPosition(n, new Vector3(Mathf.Cos(angle) * (.65f + i * .06f),
                i * .13f + .08f * Mathf.Sin(angle * 2 + Time.time * 4), Mathf.Sin(angle) * .42f));
        }
        for (int i = 0; i < dots.Length; i++)
        {
            float angle = Time.time * 5 + i * Mathf.PI * .5f;
            dots[i].localPosition = new Vector3(Mathf.Cos(angle) * .7f, .1f + .1f * Mathf.Sin(angle * 2), Mathf.Sin(angle) * .47f);
        }
    }
    void OnDestroy() { if (glow) Destroy(glow); }
}
