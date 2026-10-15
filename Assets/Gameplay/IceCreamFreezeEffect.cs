using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

// An ice silhouette around the floating torso; presentation never adds a hitbox.
public sealed class IceCreamFreezeEffect : MonoBehaviour
{
    Material ice;
    Mesh crystal;
    void Awake()
    {
        ice = new Material(Resources.Load<Shader>("FX/IceCreamFrost")) { name = "IceCreamFrost" };
        crystal = CreateCrystal();
        AddCrystal("FrozenCrystalCore", Vector3.zero, new Vector3(1.65f, 2.1f, 1.35f), Quaternion.Euler(0, 15, -5));
        for (int i = 0; i < 12; i++)
        {
            float angle = i * Mathf.PI * 2 / 12;
            AddCrystal("IceCrystal", new Vector3(Mathf.Cos(angle) * .64f, -.38f + i % 3 * .42f, Mathf.Sin(angle) * .5f),
                new Vector3(.22f + i % 2 * .09f, .65f + i % 4 * .2f, .25f),
                Quaternion.Euler(Mathf.Sin(angle) * 28, i * 47, -Mathf.Cos(angle) * 28));
        }
    }

    void AddCrystal(string name, Vector3 position, Vector3 scale, Quaternion rotation)
    {
        var shard = new GameObject(name); shard.transform.SetParent(transform, false);
        shard.transform.localPosition = position; shard.transform.localScale = scale; shard.transform.localRotation = rotation;
        shard.AddComponent<MeshFilter>().sharedMesh = crystal;
        var renderer = shard.AddComponent<MeshRenderer>(); renderer.sharedMaterial = ice;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
    }

    // Six flat faces and pointed ends; one mesh is shared by the core and its splinters.
    static Mesh CreateCrystal()
    {
        var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var indices = new List<int>();
        Vector3 Ring(int i, float y, float radius)
        { float a = i * Mathf.PI / 3; return new Vector3(Mathf.Cos(a) * radius, y, Mathf.Sin(a) * radius); }
        void Face(Vector3 a, Vector3 b, Vector3 c)
        {
            int n = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c);
            uv.Add(Vector2.zero); uv.Add(Vector2.right); uv.Add(new Vector2(.5f, 1));
            indices.Add(n); indices.Add(n + 2); indices.Add(n + 1);
        }
        for (int i = 0; i < 6; i++)
        {
            var low = Ring(i, -.25f, .5f); var lowNext = Ring(i + 1, -.25f, .5f);
            var high = Ring(i, .2f, .43f); var highNext = Ring(i + 1, .2f, .43f);
            Face(Vector3.down * .5f, lowNext, low);
            Face(low, lowNext, high); Face(lowNext, highNext, high);
            Face(high, highNext, Vector3.up * .5f);
        }
        var mesh = new Mesh { name = "PointedHexagonalIce", vertices = vertices.ToArray(), uv = uv.ToArray(), triangles = indices.ToArray() };
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
    }
    void OnDestroy() { if (ice) Destroy(ice); if (crystal) Destroy(crystal); }
}
