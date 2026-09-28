using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// Pixel coordinates share the existing ReferenceSidewalks / ReferenceRoads plan.
public static class ReferenceHouses
{
    const float S = 160f / 1312f;
    const string RootName = "Layer_04_Houses";
    static readonly Vector2[] Centers = {
        new(303,163), new(685,163), new(992,163),
        new(321,424), new(997,424),
        new(321,709), new(666,709), new(997,709),
        new(321,990), new(997,990)
    };

    [MenuItem("Tools/Level/Place Reference Houses")]
    public static void Place()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var grass = GameObject.Find("Layer_00_GrassBase");
        var sidewalks = GameObject.Find("Layer_01_Sidewalks");
        var roads = GameObject.Find("Layer_02_Roads");
        if (EditorApplication.isPlaying || scene.name != "SampleScene" || !grass || !sidewalks || !roads)
            throw new Exception("Expected reference map in SampleScene, edit mode.");
        if (GameObject.Find(RootName)) throw new Exception("House layer already exists; adjust the placed instances instead of duplicating them.");
        string[] paths = Directory.GetFiles("Assets/Art/BakedCottages", "*.prefab", SearchOption.AllDirectories).OrderBy(x => x).ToArray();
        if (paths.Length != 10) throw new Exception("Expected 10 baked cottage prefabs.");
        var groundCollider = grass.GetComponent<MeshCollider>();
        float ground = grass.GetComponent<Renderer>().bounds.max.y;
        var sidewalkBounds = sidewalks.GetComponentsInChildren<Renderer>().Select(r => r.bounds).ToArray();
        int sceneRoots = scene.rootCount;
        EditorSceneManager.SaveScene(scene, "C:/Users/xlfhf/Documents/Codex/2026-09-24/glrt/work/before_house_placement.unity", true);
        Undo.IncrementCurrentGroup();
        int undo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Place 10 reference houses");
        var report = new List<string>();
        var placed = new List<Bounds>();
        try
        {
            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "House placement");
            for (int i = 0; i < paths.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i].Replace('\\', '/'));
                var house = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                Undo.RegisterCreatedObjectUndo(house, "Place house");
                house.name = "Plot_" + (i + 1).ToString("D2") + "_" + prefab.name;
                if(i>=8) house.transform.rotation=Quaternion.Euler(0,180,0);
                var original = BoundsOf(house);
                float widthPx = i < 8 ? 140 : 134;
                float depthPx = i < 3 ? 106 : i < 5 ? 138 : i < 8 ? 126 : 116;
                float scale = Mathf.Min(widthPx * S / original.size.x, depthPx * S / original.size.z);
                house.transform.localScale = Vector3.one * scale;
                var bounds = BoundsOf(house);
                var center = Centers[i];
                // Center the complete footprint (including steps) and place its lowest point on grass.
                house.transform.position += new Vector3((center.x - 656) * S - bounds.center.x,
                    ground - bounds.min.y, (599.5f - center.y) * S - bounds.center.z);
                bounds = BoundsOf(house);
                foreach (var mf in house.GetComponentsInChildren<MeshFilter>())
                {
                    var collider = Undo.AddComponent<MeshCollider>(mf.gameObject);
                    collider.sharedMesh = mf.sharedMesh;
                    collider.convex = false;
                }
                foreach (var transform in house.GetComponentsInChildren<Transform>()) transform.gameObject.isStatic = true;
                PrefabUtility.RecordPrefabInstancePropertyModifications(house.transform);
                int triangles = house.GetComponentsInChildren<MeshFilter>().Sum(m => m.sharedMesh.triangles.Length / 3);
                if (triangles > 3000 || Mathf.Abs(bounds.min.y - ground) > .002f)
                    throw new Exception(house.name + " failed polygon/ground check.");
                foreach (var p in new[] {
                    new Vector3(bounds.min.x,10,bounds.min.z), new Vector3(bounds.min.x,10,bounds.max.z),
                    new Vector3(bounds.max.x,10,bounds.min.z), new Vector3(bounds.max.x,10,bounds.max.z)})
                    if (!groundCollider.Raycast(new Ray(p, Vector3.down), out _, 30))
                        throw new Exception(house.name + " extends outside the island.");
                foreach (var pavement in sidewalkBounds)
                    if (OverlapXZ(bounds, pavement, .2f)) throw new Exception(house.name + " touches sidewalk.");
                foreach (var previous in placed)
                    if (OverlapXZ(bounds, previous, .5f)) throw new Exception("House footprints overlap.");
                var asphalt = roads.GetComponentInChildren<MeshCollider>();
                if (asphalt.Raycast(new Ray(new Vector3(bounds.center.x,10,bounds.center.z),Vector3.down),out _,30))
                    throw new Exception(house.name + " is on the road.");
                placed.Add(bounds);
                report.Add(house.name + " | position=" + house.transform.position.ToString("F3") +
                    " | uniform scale=" + scale.ToString("F3") + " | bounds=" + bounds.size.ToString("F3") +
                    " | triangles=" + triangles + " | ground=" + bounds.min.y.ToString("F3"));
            }
            Physics.SyncTransforms();
            if (root.transform.childCount != 10 || scene.rootCount != sceneRoots + 1)
                throw new Exception("Unexpected scene hierarchy change.");
            Undo.CollapseUndoOperations(undo);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Directory.CreateDirectory("art-review");
            File.WriteAllLines("art-review/reference-houses-audit.txt", report);
            Selection.activeGameObject = null;
            TopView();
            Debug.Log("REFERENCE_HOUSES_OK: 10 prefab instances; 3/2/3/2 layout; park empty; ground, island, sidewalk, road, triangle checks passed; scene saved.\n" + string.Join("\n",report));
        }
        catch { Undo.RevertAllDownToGroup(undo); throw; }
    }

    static Bounds BoundsOf(GameObject obj)
    {
        var renderers = obj.GetComponentsInChildren<Renderer>();
        var bounds = renderers[0].bounds;
        foreach (var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);
        return bounds;
    }
    static bool OverlapXZ(Bounds a, Bounds b, float clearance) =>
        a.min.x < b.max.x + clearance && a.max.x > b.min.x - clearance &&
        a.min.z < b.max.z + clearance && a.max.z > b.min.z - clearance;

    [MenuItem("Tools/Level/Inspect Houses Top")]
    public static void TopView()
    {
        var view = SceneView.lastActiveSceneView;
        if (view != null) { view.orthographic = true; view.LookAtDirect(new Vector3(0,0,0),Quaternion.Euler(90,0,0),100); view.Repaint(); }
    }
    [MenuItem("Tools/Level/Inspect Houses Perspective")]
    public static void Perspective()
    {
        var view = SceneView.lastActiveSceneView;
        if (view != null) { view.orthographic = true; view.LookAtDirect(new Vector3(0,0,0),Quaternion.Euler(62,0,0),98); view.Repaint(); }
    }
}
