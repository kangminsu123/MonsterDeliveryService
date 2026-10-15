using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public static class ExplorationRivalNavigation
{
    [MenuItem("Tools/Gameplay/Rebuild Exploration Navigation")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Bake in Edit Mode.");
        var bounds = GameObject.Find("Layer_00_GrassBase").GetComponent<Renderer>().bounds;
        bounds.center += Vector3.up * 8;
        bounds.size = new Vector3(bounds.size.x + 4, 20, bounds.size.z + 4);
        var markups = new List<NavMeshBuildMarkup>();
        var parcels = GameObject.Find("Layer_12_TestParcelDrops");
        if (parcels) markups.Add(new NavMeshBuildMarkup { root = parcels.transform, ignoreFromBuild = true });
        var sources = new List<NavMeshBuildSource>();
        var readableMeshes = new Dictionary<Mesh,Mesh>();
        Physics.SyncTransforms();
        NavMeshBuilder.CollectSources(bounds, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);
        try
        {
            // Bake with temporary readable geometry; keep the game's imported mesh settings intact.
            for (int i = 0; i < sources.Count; i++)
            {
                var source = sources[i];
                if (!(source.sourceObject is Mesh mesh) || mesh.isReadable) continue;
                if (!readableMeshes.TryGetValue(mesh,out var readable))
                {
                    readable = new Mesh { name = mesh.name + "_NavigationBake", indexFormat = mesh.indexFormat, vertices = mesh.vertices, triangles = mesh.triangles };
                    readable.RecalculateBounds(); readableMeshes.Add(mesh,readable);
                }
                source.sourceObject = readable; sources[i] = source;
            }
            var data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0), sources, bounds, Vector3.zero, Quaternion.identity);
            if (!data) throw new System.InvalidOperationException("Navigation bake failed.");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Navigation")) AssetDatabase.CreateFolder("Assets/Resources","Navigation");
            const string path = "Assets/Resources/Navigation/ExplorationIsland.asset";
            var saved = AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
            if (saved) { EditorUtility.CopySerialized(data,saved); Object.DestroyImmediate(data); EditorUtility.SetDirty(saved); }
            else AssetDatabase.CreateAsset(data,path);
            AssetDatabase.SaveAssets();
            Debug.Log("EXPLORATION_NAVIGATION_OK: baked static island colliders; floating parcels excluded.");
        }
        finally { foreach (var mesh in readableMeshes.Values) Object.DestroyImmediate(mesh); }
    }
}
