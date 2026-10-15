using System.Collections.Generic;
using UnityEngine;

public sealed class SlimePuddle : MonoBehaviour
{
    Mesh mesh;
    Material material;
    readonly Transform[] bubbles = new Transform[12];
    float expires;
    public void Initialize(Vector3 point, float size, float seconds)
    {
        transform.position = point; float radius = size; expires = Time.time + seconds;
        const int rings = 10, sides = 64;
        var vertices = new Vector3[1 + rings * sides]; var uv = new Vector2[vertices.Length]; var triangles = new List<int>();
        vertices[0] = Vector3.up * .025f;
        uv[0] = Vector2.one * .5f;
        for (int ring = 1; ring <= rings; ring++)
        for (int side = 0; side < sides; side++)
        {
            float a = side * Mathf.PI * 2 / sides;
            float r = radius * ring / rings * (ring == rings ? .98f + .02f * Mathf.Sin(a * 7) : 1);
            var p = new Vector3(Mathf.Cos(a) * r, .025f, Mathf.Sin(a) * r);
            if (Physics.Raycast(point + p + Vector3.up * .6f, Vector3.down, out var hit, 1.2f, ~0, QueryTriggerInteraction.Ignore)
                && !hit.collider.GetComponentInParent<CharacterController>() && hit.normal.y > .65f)
                p.y = hit.point.y - point.y + .025f;
            int index = 1 + (ring - 1) * sides + side, next = 1 + (ring - 1) * sides + (side + 1) % sides;
            vertices[index] = p;
            uv[index] = new Vector2(p.x, p.z) / (radius * 2) + Vector2.one * .5f;
            if (ring == 1) triangles.AddRange(new[] { 0, next, index });
            else
            {
                int previous = index - sides, previousNext = next - sides;
                triangles.AddRange(new[] { previous, next, index, previous, previousNext, next });
            }
        }
        mesh = new Mesh { name = "ConformingSlimePool", vertices = vertices, uv = uv, triangles = triangles.ToArray() };
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = gameObject.AddComponent<MeshRenderer>();
        material = new Material(Resources.Load<Shader>("FX/SlimePool")) { name = "WetSlimePool" };
        renderer.sharedMaterial = material;
        for (int i = 0; i < bubbles.Length; i++)
        {
            float a = i * 2.39996f, r = radius * (.25f + i % 4 * .18f);
            bubbles[i] = SlimeGoo.Blob(transform, "SwellingSlimeBubble", new Vector3(Mathf.Cos(a) * r, .04f, Mathf.Sin(a) * r), Vector3.one * .2f, i % 3 == 0);
        }
        Destroy(gameObject, seconds);
    }
    void Update()
    {
        float remaining = expires - Time.time, fade = Mathf.Clamp01(remaining / .5f);
        material.SetFloat("_Opacity", fade);
        for (int i = 0; i < bubbles.Length; i++)
        {
            float phase = Mathf.Repeat(Time.time * .7f + i * .173f, 1);
            float scale = Mathf.Sin(phase * Mathf.PI) * .45f * fade;
            bubbles[i].localScale = new Vector3(scale, scale * .5f, scale);
        }
    }
    void OnDestroy() { if (mesh) Destroy(mesh); if (material) Destroy(material); }
}
