using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// A short-lived stain fitted to the struck collider, including slopes and curved props.
public sealed class SurfaceInkMark : MonoBehaviour
{
    Mesh mesh;
    MeshRenderer paint;
    MaterialPropertyBlock properties;
    float paintedAt;
    float holdSeconds = 1, fadeSeconds = 1;
    Color tint = new(.004f, .004f, .004f, 1);
    public float Opacity => 1 - Mathf.SmoothStep(0, 1, (Time.time - paintedAt - holdSeconds) / fadeSeconds);
    public MeshRenderer Paint => paint;

    public void Initialize(Collider surface, Vector3 point, Vector3 normal, Material material,
        Color? color = null, float hold = 1, float fade = 1)
    {
        tint = color ?? new Color(.004f, .004f, .004f, 1);
        holdSeconds = Mathf.Max(0, hold); fadeSeconds = Mathf.Max(.01f, fade);
        paintedAt = Time.time;
        transform.SetPositionAndRotation(point, Quaternion.LookRotation(normal) *
            Quaternion.Euler(0, 0, Mathf.Repeat(point.x * 31 + point.y * 17 + point.z * 13, 360)));
        transform.SetParent(surface.transform, true);
        for (int attempt = 0; attempt < 3 && !mesh; attempt++)
            mesh = FitSurface(surface, point, normal, 1.4f / (1 << attempt));
        if (!mesh) { Destroy(gameObject); return; }
        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        paint = gameObject.AddComponent<MeshRenderer>(); paint.sharedMaterial = material;
        paint.shadowCastingMode = ShadowCastingMode.Off; paint.receiveShadows = false;
        properties = new MaterialPropertyBlock();
        Update(); Destroy(gameObject, holdSeconds + fadeSeconds);
    }

    Mesh FitSurface(Collider surface, Vector3 point, Vector3 normal, float size)
    {
        const int side = 17;
        var vertices = new Vector3[side * side]; var uv = new Vector2[vertices.Length];
        var colors = new Color[vertices.Length]; var valid = new bool[vertices.Length];
        var triangles = new List<int>();
        for (int y = 0; y < side; y++)
        for (int x = 0; x < side; x++)
        {
            int i = y * side + x;
            uv[i] = new Vector2(x, y) / (side - 1f); colors[i] = Color.white;
            var offset = transform.right * (uv[i].x - .5f) * size + transform.up * (uv[i].y - .5f) * size;
            if (!surface.Raycast(new Ray(point + offset + normal * .45f, -normal), out var hit, .9f)) continue;
            valid[i] = true; vertices[i] = transform.InverseTransformPoint(hit.point + hit.normal * .012f);
        }
        void Triangle(int a, int b, int c)
        { if (valid[a] && valid[b] && valid[c]) { triangles.Add(a); triangles.Add(b); triangles.Add(c); } }
        for (int y = 0; y < side - 1; y++)
        for (int x = 0; x < side - 1; x++)
        {
            int i = y * side + x;
            Triangle(i, i + side, i + 1); Triangle(i + 1, i + side, i + side + 1);
        }
        if (triangles.Count == 0) return null;
        var result = new Mesh { name = "SurfaceInkFootprint", vertices = vertices, uv = uv, colors = colors };
        result.SetTriangles(triangles, 0); result.RecalculateNormals(); result.RecalculateBounds();
        return result;
    }

    void Update()
    {
        if (!paint) return;
        var color = tint; color.a *= Opacity;
        properties.SetColor("_Color", color);
        paint.SetPropertyBlock(properties);
    }

    void OnDestroy() { if (mesh) Destroy(mesh); }
}
