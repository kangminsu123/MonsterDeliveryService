using UnityEngine;

// Presentation follows the replicated immunity deadline; it never changes hit detection.
public sealed class LifeJacketProtectionFX : MonoBehaviour
{
    readonly LineRenderer[] rings = new LineRenderer[3];
    readonly LineRenderer[] sparks = new LineRenderer[8];
    Material material;

    void Awake()
    {
        material = new Material(Resources.Load<Shader>("FX/RunningShoesAura")) { name = "LifeJacketGoldGlow" };
        for (int i = 0; i < rings.Length; i++) rings[i] = Line("ProtectionRing", 64, true);
        for (int i = 0; i < sparks.Length; i++) sparks[i] = Line("ProtectionSpark", 4, false);
    }
    LineRenderer Line(string label, int count, bool loop)
    {
        var line = new GameObject(label).AddComponent<LineRenderer>();
        line.transform.SetParent(transform, false); line.sharedMaterial = material;
        line.useWorldSpace = false; line.loop = loop; line.positionCount = count;
        line.startWidth = line.endWidth = loop ? .045f : .035f;
        return line;
    }
    public void Show(bool visible, float remaining)
    {
        gameObject.SetActive(visible);
        if (!visible) return;
        float time = Time.time;
        float fade = Mathf.Clamp01(remaining / .4f);
        var gold = new Color(1, .72f + .12f * Mathf.Sin(time * 5), .12f, fade * .8f);
        for (int r = 0; r < rings.Length; r++)
        {
            rings[r].startColor = rings[r].endColor = gold;
            for (int n = 0; n < 64; n++)
            {
                float a = n * Mathf.PI * 2 / 64 + time * (r == 1 ? -.8f : .8f);
                Vector3 p = r == 0 ? new Vector3(Mathf.Cos(a) * 1.05f, -.75f, Mathf.Sin(a) * 1.05f) :
                    new Vector3(Mathf.Cos(a) * .95f, Mathf.Sin(a) * 1.25f + .05f, 0);
                if (r == 2) p = Quaternion.Euler(0, 90, 0) * p;
                rings[r].SetPosition(n, p);
            }
        }
        for (int i = 0; i < sparks.Length; i++)
        {
            float phase = Mathf.Repeat(time * .7f + i / 8f, 1);
            float a = time * .8f + i * Mathf.PI / 4;
            var center = new Vector3(Mathf.Cos(a), -.8f + phase * 2.1f, Mathf.Sin(a));
            var color = gold; color.a *= Mathf.Sin(phase * Mathf.PI);
            sparks[i].startColor = sparks[i].endColor = color;
            sparks[i].SetPositions(new[] { center + Vector3.up * .12f, center + Vector3.right * .07f,
                center - Vector3.up * .12f, center - Vector3.right * .07f });
        }
    }
    void OnDestroy() { if (material) Destroy(material); }
}
