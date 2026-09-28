using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ReferenceParkHouseUpdate
{
    const float MapScale = 160f / 1312f;
    const string ParkName = "Layer_07_CentralParkPaving";
    const string MeshPath = "Assets/MapMeshes/Park/CentralParkPaving.asset";
    static readonly float[] StepFront = { -4.29f, -3.44f, -3.44f, -4.26f, -3.44f, -4.24f, -3.82f, -4.51f, -4.19f, -3.44f };

    sealed class RoofUnit
    {
        public Transform transform;
        public int plot;
        public Vector3 localPosition;
    }

    [MenuItem("Tools/Level/Inspect Central Park")]
    public static void InspectPark()
    {
        var view = SceneView.lastActiveSceneView;
        if (view != null)
        {
            var center = Pixel(670, 423);
            view.orthographic = true;
            view.LookAtDirect(new Vector3(center.x, 0, center.y), Quaternion.Euler(62, 0, 0), 28);
            view.Repaint();
        }
    }

    [MenuItem("Tools/Level/Shrink Houses and Pave Central Park")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var houses = GameObject.Find("Layer_04_Houses");
        var props = GameObject.Find("Layer_06_LowPolyTownProps");
        var grass = GameObject.Find("Layer_00_GrassBase");
        var pavement = AssetDatabase.LoadAssetAtPath<Material>("Assets/MapMaterials/Sidewalk_GrayConcrete.mat");
        if (EditorApplication.isPlaying || scene.name != "SampleScene" || !houses || houses.transform.childCount != 10 ||
            !props || !grass || !pavement || GameObject.Find(ParkName))
            throw new Exception("Expected the saved reference neighborhood, ten houses, props, and no existing park paving in edit mode.");

        var roofGroup = props.transform.Find("07_Flat_Roof_AC");
        if (!roofGroup || roofGroup.childCount != 6) throw new Exception("Expected six existing flat-roof condensers.");
        var roofUnits = new List<RoofUnit>();
        foreach (Transform unit in roofGroup)
        {
            int start = unit.name.IndexOf("Plot", StringComparison.Ordinal);
            if (start < 0 || !int.TryParse(unit.name.Substring(start + 4, 2), out int number))
                throw new Exception("Unexpected condenser name: " + unit.name);
            int plot = number - 1;
            if (!new[] { 0, 2, 3 }.Contains(plot)) throw new Exception("Condenser is on an unapproved roof.");
            roofUnits.Add(new RoofUnit { transform = unit, plot = plot,
                localPosition = houses.transform.GetChild(plot).InverseTransformPoint(unit.position) });
        }

        Undo.IncrementCurrentGroup();
        int undo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Shrink ten houses and pave central park");
        var report = new List<string>();
        try
        {
            float ground = grass.GetComponent<Renderer>().bounds.max.y;
            for (int i = 0; i < 10; i++)
            {
                var house = houses.transform.GetChild(i);
                if (!house.name.StartsWith("Plot_" + (i + 1).ToString("D2"))) throw new Exception("House order changed.");
                var localEntry = new Vector3(i == 6 ? 1.65f : 0, 0, StepFront[i]);
                var entry = house.TransformPoint(localEntry);
                float before = house.localScale.x;
                Undo.RecordObject(house, "Shrink house by ten percent");
                house.localScale *= .9f;
                house.position += entry - house.TransformPoint(localEntry);
                PrefabUtility.RecordPrefabInstancePropertyModifications(house);
                var bounds = BoundsOf(house.gameObject);
                if (Vector3.Distance(entry, house.TransformPoint(localEntry)) > .001f ||
                    Mathf.Abs(bounds.min.y - ground) > .01f || Mathf.Abs(house.localScale.x / before - .9f) > .0001f)
                    throw new Exception("House ground or entrance shifted: " + house.name);
                report.Add(house.name + " scale " + before.ToString("F3") + " -> " + house.localScale.x.ToString("F3") +
                    " bounds " + bounds.size.ToString("F2") + " entrance preserved");
            }

            Physics.SyncTransforms();
            var placed = new List<Bounds>();
            foreach (var unit in roofUnits)
            {
                var house = houses.transform.GetChild(unit.plot);
                Vector3 target = house.TransformPoint(unit.localPosition);
                Undo.RecordObject(unit.transform, "Seat condenser on resized flat roof");
                bool supported = false;
                foreach (var offset in new[] { Vector2.zero, new Vector2(.5f, 0), new Vector2(-.5f, 0),
                             new Vector2(0, .5f), new Vector2(0, -.5f), new Vector2(.9f, 0), new Vector2(-.9f, 0) })
                {
                    unit.transform.position = new Vector3(target.x + offset.x, target.y, target.z + offset.y);
                    Physics.SyncTransforms();
                    var bounds = BoundsOf(unit.transform.gameObject);
                    float half = Mathf.Max(bounds.extents.x, bounds.extents.z) + .25f;
                    if (!FlatRoofSupport(house, bounds.center, half, out float roofHeight)) continue;
                    unit.transform.position += Vector3.up * (roofHeight - bounds.min.y);
                    bounds = BoundsOf(unit.transform.gameObject);
                    if (placed.Any(other => OverlapXZ(bounds, other, .15f))) continue;
                    placed.Add(bounds);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(unit.transform);
                    report.Add(unit.transform.name + " flat roof height " + roofHeight.ToString("F3") + " gap " + (bounds.min.y - roofHeight).ToString("F3"));
                    supported = true;
                    break;
                }
                if (!supported) throw new Exception("No safe roof support after shrink: " + unit.transform.name);
            }

            var park = new GameObject(ParkName);
            Undo.RegisterCreatedObjectUndo(park, "Central park paving");
            var mesh = BuildParkMesh(ground);
            string folder = Path.GetDirectoryName(MeshPath).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/MapMeshes", "Park");
            var stored = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (stored) { EditorUtility.CopySerialized(mesh, stored); UnityEngine.Object.DestroyImmediate(mesh); mesh = stored; }
            else AssetDatabase.CreateAsset(mesh, MeshPath);
            park.AddComponent<MeshFilter>().sharedMesh = mesh;
            park.AddComponent<MeshRenderer>().sharedMaterial = pavement;
            park.AddComponent<MeshCollider>().sharedMesh = mesh;
            park.isStatic = true;
            Physics.SyncTransforms();
            var parkBounds = park.GetComponent<Renderer>().bounds;
            if (parkBounds.min.x < Pixel(543, 0).x || parkBounds.max.x > Pixel(790, 0).x ||
                parkBounds.min.z < Pixel(0, 540).y || parkBounds.max.z > Pixel(0, 331).y ||
                Mathf.Abs(parkBounds.max.y - (ground + .183f)) > .002f)
                throw new Exception("Park paving escapes the central block or has the wrong height: " + parkBounds);
            LowPolyTownPlacement.Verify();
            report.Add("Central park: 24-sided planting ring, three slab paths, concrete mesh collider; bounds " + parkBounds);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Directory.CreateDirectory("art-review");
            File.WriteAllLines("art-review/park-house-adjustment-audit.txt", report);
            Undo.CollapseUndoOperations(undo);
            Debug.Log("REFERENCE_PARK_HOUSE_UPDATE_OK\n" + string.Join("\n", report));
        }
        catch { Undo.RevertAllDownToGroup(undo); throw; }
    }

    static Bounds BoundsOf(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    static bool OverlapXZ(Bounds a, Bounds b, float gap) =>
        a.min.x < b.max.x + gap && a.max.x > b.min.x - gap &&
        a.min.z < b.max.z + gap && a.max.z > b.min.z - gap;

    static bool FlatRoofSupport(Transform house, Vector3 center, float half, out float height)
    {
        float low = float.MaxValue, high = float.MinValue;
        foreach (int x in new[] { -1, 0, 1 }) foreach (int z in new[] { -1, 0, 1 })
        {
            var ray = new Ray(new Vector3(center.x + x * half, 80, center.z + z * half), Vector3.down);
            bool found = false;
            RaycastHit nearest = default;
            foreach (var collider in house.GetComponentsInChildren<MeshCollider>())
                if (collider.Raycast(ray, out var hit, 160) && (!found || hit.distance < nearest.distance))
                { nearest = hit; found = true; }
            if (!found || nearest.normal.y < .999f) { height = 0; return false; }
            low = Mathf.Min(low, nearest.point.y);
            high = Mathf.Max(high, nearest.point.y);
        }
        height = (low + high) * .5f;
        return high - low <= .018f;
    }

    static Vector2 Pixel(float x, float y) => new((x - 656) * MapScale, (599.5f - y) * MapScale);

    static Mesh BuildParkMesh(float ground)
    {
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var triangles = new List<int>();
        var center = Pixel(670, 423);
        const int sectors = 24;
        for (int i = 0; i < sectors; i++)
        {
            float a = i * Mathf.PI * 2 / sectors + .004f;
            float b = (i + 1) * Mathf.PI * 2 / sectors - .004f;
            Vector2 innerA = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 3.2f;
            Vector2 outerA = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 7.6f;
            Vector2 outerB = center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * 7.6f;
            Vector2 innerB = center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * 3.2f;
            Prism(new[] { innerA, outerA, outerB, innerB }, ground + .183f, ground - .02f, vertices, normals, triangles);
        }
        PavedPath(center, Pixel(583, 344), 3.2f, ground + .177f, ground - .02f, vertices, normals, triangles);
        PavedPath(center, Pixel(752, 344), 3.2f, ground + .177f, ground - .02f, vertices, normals, triangles);
        PavedPath(center, Pixel(670, 529), 3.5f, ground + .177f, ground - .02f, vertices, normals, triangles);
        var mesh = new Mesh { name = "CentralParkPaving" };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    static void PavedPath(Vector2 start, Vector2 end, float width, float top, float bottom,
        List<Vector3> vertices, List<Vector3> normals, List<int> triangles)
    {
        Vector2 direction = (end - start).normalized;
        Vector2 side = new(-direction.y, direction.x);
        int count = Mathf.CeilToInt(Vector2.Distance(start, end) / 2.6f);
        for (int i = 0; i < count; i++)
        {
            Vector2 a = Vector2.Lerp(start, end, (i + .006f) / count);
            Vector2 b = Vector2.Lerp(start, end, (i + .994f) / count);
            Prism(new[] { a + side * width * .5f, b + side * width * .5f,
                b - side * width * .5f, a - side * width * .5f }, top, bottom, vertices, normals, triangles);
        }
    }

    static void Prism(Vector2[] points, float top, float bottom,
        List<Vector3> vertices, List<Vector3> normals, List<int> triangles)
    {
        float area = 0;
        for (int i = 0; i < points.Length; i++)
            area += points[i].x * points[(i + 1) % points.Length].y - points[(i + 1) % points.Length].x * points[i].y;
        if (area < 0) Array.Reverse(points);
        int first = vertices.Count;
        foreach (var p in points) { vertices.Add(new Vector3(p.x, top, p.y)); normals.Add(Vector3.up); }
        for (int i = 1; i < points.Length - 1; i++)
        { triangles.Add(first); triangles.Add(first + i + 1); triangles.Add(first + i); }
        for (int i = 0; i < points.Length; i++)
        {
            Vector2 a = points[i], b = points[(i + 1) % points.Length];
            Vector2 d = (b - a).normalized;
            Vector3 normal = new(d.y, 0, -d.x);
            int baseIndex = vertices.Count;
            vertices.Add(new Vector3(a.x, top, a.y)); vertices.Add(new Vector3(b.x, top, b.y));
            vertices.Add(new Vector3(b.x, bottom, b.y)); vertices.Add(new Vector3(a.x, bottom, a.y));
            for (int j = 0; j < 4; j++) normals.Add(normal);
            triangles.Add(baseIndex); triangles.Add(baseIndex + 1); triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex); triangles.Add(baseIndex + 2); triangles.Add(baseIndex + 3);
        }
    }
}
