using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ParcelSpawnSetup
{
    const string Folder = "Docs/ChangeRecords/20261018";
    const string Layer = "Layer_12_TestParcelDrops";
    const string Prefab = "Assets/Art/DeliveryProps/parcel.prefab";

    [MenuItem("Tools/Gameplay/Test Parcels/Build 100 Spawn Points")]
    public static void Build()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.name != "SampleScene") throw new Exception("Open SampleScene in Edit Mode.");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
        if (!prefab) throw new Exception("Parcel prefab missing.");
        Physics.SyncTransforms();
        var area = GameObject.Find("Layer_00_GrassBase").GetComponent<Renderer>().bounds;
        var roofs = GameObject.Find("Layer_04_Houses").transform.Cast<Transform>().Where(TestParcelDrops.AccessibleRoof).ToArray();
        var rng = new System.Random(20261016);
        var points = new List<Vector3>();
        for (int i = 0; i < ParcelSpawnPool.SpawnCount; i++)
        {
            int streetCount = ParcelSpawnPool.SpawnCount - 10;
            bool roof = i >= streetCount;
            var bounds = roof ? roofs[(i - streetCount) % roofs.Length].GetComponent<Renderer>().bounds : area;
            bool found = false;
            for (int attempt = 0; attempt < 20000; attempt++)
            {
                var point = new Vector3(Mathf.Lerp(bounds.min.x, bounds.max.x, (float)rng.NextDouble()), 0,
                    Mathf.Lerp(bounds.min.z, bounds.max.z, (float)rng.NextDouble()));
                if (!TestParcelDrops.Surface(point, out var hit) || (hit.point.y > 1) != roof) continue;
                point.y = hit.point.y;
                if (points.Any(p => (p - point).sqrMagnitude < 16) || !TestParcelDrops.Supported(point) || !TestParcelDrops.Clear(point)) continue;
                points.Add(point); found = true; break;
            }
            if (!found) throw new Exception("Cannot find safe anchor " + (i + 1) + "; existing scene preserved.");
        }
        Directory.CreateDirectory(Folder + "/source");
        if (!File.Exists(Folder + "/source/case07/SampleScene.unity.before.txt"))
            throw new Exception("Save the before-scene evidence before rebuilding the layout.");
        var old = GameObject.Find(Layer);
        if (old) Undo.DestroyObjectImmediate(old);
        var root = new GameObject(Layer); Undo.RegisterCreatedObjectUndo(root, "Build parcel spawn pool");
        var pool = Undo.AddComponent<ParcelSpawnPool>(root);
        var anchors = new GameObject("SpawnPoints").transform; anchors.SetParent(root.transform, false);
        pool.spawnPoints = points.Select((p, i) =>
        {
            var t = new GameObject("Spawn_" + (i + 1).ToString("D2") + (p.y > 1 ? "_Roof" : "_Street")).transform;
            t.SetParent(anchors, false); t.position = p; return t;
        }).ToArray();
        pool.parcels = Enumerable.Range(0, ParcelSpawnPool.LiveCount).Select(i =>
        {
            var parcel = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            parcel.name = "TestParcel_" + (i + 1).ToString("D2");
            return parcel.GetComponent<FloatingParcel>();
        }).ToArray();
        var random = UnityEngine.Random.state; UnityEngine.Random.InitState(20261016); pool.ResetRound(); UnityEngine.Random.state = random;
        foreach (var p in pool.parcels) PrefabUtility.RecordPrefabInstancePropertyModifications(p.transform);
        EditorUtility.SetDirty(pool); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        VerifyLayout(); Selection.activeGameObject = root;
        Debug.Log("PARCEL_POOL_SETUP_OK: 100 verified anchors (90 street/sidewalk, 10 accessible roofs), 50 reusable parcels saved.");
    }

    [MenuItem("Tools/Gameplay/Test Parcels/Verify Spawn Layout")]
    public static void VerifyLayout()
    {
        var pool = UnityEngine.Object.FindAnyObjectByType<ParcelSpawnPool>();
        if (!pool || pool.spawnPoints.Length != ParcelSpawnPool.SpawnCount || pool.parcels.Length != ParcelSpawnPool.LiveCount) throw new Exception("Expected 100 anchors and 50 parcel models.");
        Physics.SyncTransforms(); var rows = new List<string>();
        for (int i = 0; i < pool.spawnPoints.Length; i++)
        {
            var point = pool.spawnPoints[i].position;
            if (!TestParcelDrops.Surface(point, out var hit) || Mathf.Abs(hit.point.y - point.y) > .01f ||
                !TestParcelDrops.Supported(point) || !TestParcelDrops.Clear(point)) throw new Exception("Unsafe parcel spawn: " + pool.spawnPoints[i].name);
            if (hit.collider.transform.root.name == "Layer_00_GrassBase") throw new Exception("Grass spawn forbidden.");
            if (pool.spawnPoints.Where((p, j) => j != i).Any(p => (p.position - point).sqrMagnitude < 15.99f)) throw new Exception("Anchor separation below 4m.");
            rows.Add("PASS " + pool.spawnPoints[i].name + " | " + point.ToString("F3") + " | " + hit.collider.transform.root.name + "/" + hit.collider.name);
        }
        if (pool.ActiveCount != ParcelSpawnPool.LiveCount || pool.CaptureSlots().Distinct().Count() != ParcelSpawnPool.LiveCount) throw new Exception("Expected 50 unique occupied slots.");
        rows.Add("PASS 50 active models occupy 50 distinct anchors; 50 anchors empty");
        Directory.CreateDirectory(Folder + "/evidence"); File.WriteAllLines(Folder + "/evidence/parcel-layout-check.txt", rows);
        File.WriteAllText(Folder + "/source/parcel-spawn-points.json", JsonUtility.ToJson(new SpawnLayout { points = pool.spawnPoints.Select(p => p.position).ToArray() }, true));
        Debug.Log("PARCEL_LAYOUT_OK: " + rows.Count + " checks passed.");
    }
    [Serializable] sealed class SpawnLayout { public Vector3[] points; }
}
