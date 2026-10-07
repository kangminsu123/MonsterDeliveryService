using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Saved floating test parcels; collection does not enable item rollout.
public static class TestParcelDrops
{
    const string Layer = "Layer_12_TestParcelDrops";
    const string PrefabPath = "Assets/Art/DeliveryProps/parcel.prefab";
    const int Count = 10;

    [MenuItem("Tools/Gameplay/Test Parcels/Upgrade Floating Parcels")]
    public static void Upgrade()
    {
        if (EditorApplication.isPlaying) throw new Exception("Upgrade parcels in Edit Mode.");
        var root = GameObject.Find(Layer);
        if (!root || root.transform.childCount != Count) throw new Exception("Expected 10 saved test parcels.");
        var parcels = root.transform.Cast<Transform>().ToArray();
        var anchors = parcels.Select(t => t.GetComponent<FloatingParcel>() ? t.position :
            new Vector3(BoundsOf(t.gameObject).center.x, BoundsOf(t.gameObject).min.y, BoundsOf(t.gameObject).center.z)).ToArray();
        var prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
        try { SetupFloatingParcel(prefab); PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath); }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        AssetDatabase.SaveAssets();
        for (int i = 0; i < parcels.Length; i++)
        {
            if (!Supported(anchors[i]) || !Clear(anchors[i]))
            {
                bool found = false;
                for (float radius = .5f; radius <= 4 && !found; radius += .5f)
                    for (int angle = 0; angle < 32 && !found; angle++)
                    {
                        float a = angle * Mathf.PI / 16;
                        var p = anchors[i] + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius;
                        if (!Surface(p, out var hit) || (hit.point.y > 1) != (anchors[i].y > 1)) continue;
                        p.y = hit.point.y;
                        if (!Supported(p) || !Clear(p) || anchors.Where((_, j) => j != i).Any(q => (q - p).sqrMagnitude < 16)) continue;
                        anchors[i] = p; found = true;
                    }
                if (!found) throw new Exception("No safe nearby anchor for enlarged parcel: " + parcels[i].name);
            }
            Undo.RecordObject(parcels[i], "Float parcel"); parcels[i].position = anchors[i];
            PrefabUtility.RecordPrefabInstancePropertyModifications(parcels[i]);
        }
        Verify();
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("FLOATING_PARCELS_UPGRADE_OK: 10 parcels enlarged 1.5x, bubbles, hover and pickup interaction saved; unsafe edges adjusted nearby.");
    }

    public static void SetupFloatingParcel(GameObject parcel)
    {
        var floating = parcel.transform.Find("Floating");
        var model = parcel.transform.Find("Model") ?? parcel.transform.Find("Floating/Model");
        if (!model) throw new Exception("Parcel model missing.");
        if (!floating) { floating = new GameObject("Floating").transform; floating.SetParent(parcel.transform, false); }
        floating.localPosition = Vector3.up * FloatingParcel.HoverHeight; floating.localRotation = Quaternion.identity;
        model.SetParent(floating, false); model.localPosition = Vector3.zero; model.localScale = Vector3.one * 1.5f;
        var b = BoundsOf(model.gameObject); model.position += floating.position - b.center;
        foreach (var c in model.GetComponentsInChildren<Collider>()) c.enabled = false;
        var box = floating.GetComponent<BoxCollider>(); if (!box) box = floating.gameObject.AddComponent<BoxCollider>();
        box.center = Vector3.zero; box.size = b.size;
        var motion = parcel.GetComponent<FloatingParcel>(); if (!motion) motion = parcel.AddComponent<FloatingParcel>(); motion.floating = floating;
        DeliveryPropsPlacement.AddInteraction(parcel, "parcel", "택배 상자", "E · 줍기");
        var bubble = floating.Find("Bubble");
        if (!bubble)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere); go.name = "Bubble";
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>()); bubble = go.transform;
            bubble.SetParent(floating, false);
        }
        bubble.localPosition = Vector3.zero; bubble.localScale = Vector3.one * 1.4f;
        const string matPath = "Assets/Art/DeliveryProps/ParcelBubble.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (!mat) { mat = new Material(Shader.Find("Delivery/Parcel Bubble")); AssetDatabase.CreateAsset(mat, matPath); }
        if (!mat.shader || !mat.shader.isSupported) throw new Exception("Parcel bubble shader unsupported.");
        var renderer = bubble.GetComponent<MeshRenderer>(); renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
        foreach (var r in model.GetComponentsInChildren<MeshRenderer>())
        {
            // Albedo emission keeps the cardboard readable at dawn without washing out its texture.
            var parcelMaterial = r.sharedMaterial;
            parcelMaterial.SetTexture("_EmissionMap", parcelMaterial.mainTexture);
            parcelMaterial.SetColor("_EmissionColor", new Color(.55f, .50f, .42f));
            parcelMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            MaterialEditor.FixupEmissiveFlag(parcelMaterial);
            parcelMaterial.EnableKeyword("_EMISSION"); EditorUtility.SetDirty(parcelMaterial);
        }
        foreach (Transform t in parcel.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = false;
    }

    [MenuItem("Tools/Gameplay/Test Parcels/Random Drop 10")]
    public static void Drop()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.name != "SampleScene")
            throw new Exception("Open SampleScene in Edit Mode to save test drops.");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var ground = GameObject.Find("Layer_00_GrassBase");
        var houses = GameObject.Find("Layer_04_Houses");
        if (!prefab || !ground || !houses) throw new Exception("Parcel prefab or reference map missing.");
        Physics.SyncTransforms();
        var roofs = houses.transform.Cast<Transform>().Where(h => AccessibleRoof(h)).ToArray();
        if (roofs.Length == 0) throw new Exception("No flat roofs with active stairs.");
        var rng = new System.Random();
        var points = new List<Vector3>();
        var angles = new List<float>();
        var map = ground.GetComponent<Renderer>().bounds;
        for (int i = 0; i < Count; i++)
        {
            // Guarantee ground/roof examples for this test; remaining surface choices are random.
            bool roof = i == 0 || (i > 1 && rng.Next(4) == 0);
            var area = roof ? roofs[rng.Next(roofs.Length)].GetComponent<Renderer>().bounds : map;
            bool found = false;
            for (int attempt = 0; attempt < 2000; attempt++)
            {
                var p = new Vector3(Mathf.Lerp(area.min.x, area.max.x, (float)rng.NextDouble()), 0,
                    Mathf.Lerp(area.min.z, area.max.z, (float)rng.NextDouble()));
                if (!Surface(p, out var hit) || (hit.point.y > 1) != roof) continue;
                p.y = hit.point.y;
                if (!Supported(p) || !Clear(p) || points.Any(q => (q - p).sqrMagnitude < 16)) continue;
                points.Add(p); angles.Add((float)rng.NextDouble() * 360); found = true; break;
            }
            if (!found) throw new Exception("Cannot find safe drop surface; previous drops preserved.");
        }
        Directory.CreateDirectory("art-review");
        const string backup = "art-review/before-test-parcel-drops.unity";
        if (!File.Exists(backup)) EditorSceneManager.SaveScene(scene, backup, true);
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Random test parcel drops");
        var old = GameObject.Find(Layer);
        if (old) Undo.DestroyObjectImmediate(old);
        var root = new GameObject(Layer); Undo.RegisterCreatedObjectUndo(root, "Test parcel layer");
        for (int i = 0; i < Count; i++)
        {
            var parcel = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            Undo.RegisterCreatedObjectUndo(parcel, "Drop parcel");
            parcel.name = "TestParcel_" + (i + 1).ToString("D2");
            parcel.transform.SetPositionAndRotation(points[i], Quaternion.Euler(0, angles[i], 0));
            PrefabUtility.RecordPrefabInstancePropertyModifications(parcel.transform);
        }
        Undo.CollapseUndoOperations(group);
        Verify();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = root;
        Debug.Log("TEST_PARCEL_DROP_OK: 10 saved random parcels. Use Random Drop 10 again to reroll.");
    }

    static bool AccessibleRoof(Transform house)
    {
        var access = GameObject.Find("Layer_08_RooftopAccess");
        if (!access || house.name.Length < 7) return false;
        var stairs = access.transform.Find(house.name.Substring(0, 7) + "_Roof_Stairs");
        return stairs && stairs.gameObject.activeInHierarchy &&
            stairs.GetComponentsInChildren<Collider>().Any(c => c.enabled && !c.isTrigger);
    }

    static bool Allowed(RaycastHit hit)
    {
        if (hit.normal.y < .999f) return false;
        var t = hit.collider.transform; string root = t.root.name;
        if (root == "Layer_00_GrassBase" || root == "Layer_01_Sidewalks" || root == "Layer_02_Roads")
            return hit.point.y >= -.3f && hit.point.y < .5f;
        if (root != "Layer_04_Houses") return false;
        while (t.parent && t.parent.name != "Layer_04_Houses") t = t.parent;
        if (!AccessibleRoof(t)) return false;
        var landing = GameObject.Find("Layer_08_RooftopAccess").transform
            .Find(t.name.Substring(0, 7) + "_Roof_Stairs/Roof_Landing");
        // Exclude higher decorative tower tops even on a house with roof access.
        return landing && Mathf.Abs(hit.point.y - landing.GetComponent<Collider>().bounds.max.y) < .4f;
    }

    static bool Surface(Vector3 p, out RaycastHit hit)
    {
        var hits = Physics.RaycastAll(new Vector3(p.x, 40, p.z), Vector3.down, 60, ~0, QueryTriggerInteraction.Ignore)
            .Where(h => h.collider.transform.root.name != Layer && !(h.collider is CharacterController))
            .OrderBy(h => h.distance).ToArray();
        hit = hits.Length > 0 ? hits[0] : default;
        // Never skip a tree/prop to accept the ground underneath it.
        return hits.Length > 0 && Allowed(hit);
    }

    static bool Supported(Vector3 p)
    {
        foreach (float x in new[] { -.65f, 0, .65f })
            foreach (float z in new[] { -.65f, 0, .65f })
                if (!Surface(p + new Vector3(x, 0, z), out var hit) || Mathf.Abs(hit.point.y - p.y) > .035f) return false;
        return true;
    }

    static bool Clear(Vector3 p)
    {
        // Room for the box and a standing player, beyond the supported footprint.
        var box = new Bounds(p + Vector3.up * 1.15f, new Vector3(1.7f, 2.2f, 1.7f));
        if (Physics.OverlapBox(box.center, box.extents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)
            .Any(c => c.transform.root.name != Layer && !(c is CharacterController))) return false;
        var plants = GameObject.Find("Layer_07_ReferenceLandscape");
        // Some visual plants have no collider; their bounds still exclude drop locations.
        return !plants || !plants.GetComponentsInChildren<Renderer>().Any(r => r.bounds.Intersects(box));
    }

    static Bounds BoundsOf(GameObject go)
    {
        var visual = go.transform.Find("Floating/Model") ?? go.transform.Find("Model") ?? go.transform;
        var renderers = visual.GetComponentsInChildren<MeshRenderer>().Where(r => r.name != "InteractionOutline").ToArray(); var b = renderers[0].bounds;
        foreach (var r in renderers.Skip(1)) b.Encapsulate(r.bounds);
        return b;
    }

    [MenuItem("Tools/Gameplay/Test Parcels/Verify Drops")]
    public static void Verify()
    {
        var root = GameObject.Find(Layer);
        if (!root || root.transform.childCount != Count) throw new Exception("Expected exactly 10 test parcels.");
        Physics.SyncTransforms(); int roofCount = 0; var report = new List<string>();
        foreach (Transform t in root.transform)
        {
            var b = BoundsOf(t.gameObject); var p = t.position;
            if (!Surface(p, out var hit) || !Supported(p) || !Clear(p) || Mathf.Abs(hit.point.y - p.y) > .004f)
                throw new Exception("Unsupported or obstructed parcel: " + t.name);
            if ((!EditorApplication.isPlaying && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject) != PrefabPath) ||
                t.Find("Floating/Model").GetComponentsInChildren<MeshRenderer>().Where(r => r.name != "InteractionOutline")
                    .Any(r => !r.sharedMaterial || !r.sharedMaterial.mainTexture ||
                        !r.sharedMaterial.IsKeywordEnabled("_EMISSION") ||
                        (r.sharedMaterial.globalIlluminationFlags & MaterialGlobalIlluminationFlags.EmissiveIsBlack) != 0))
                throw new Exception("Parcel prefab/material mismatch: " + t.name);
            var floating = t.Find("Floating"); var interaction = t.GetComponent<InteractionTarget>();
            if (!t.GetComponent<FloatingParcel>() || !floating || !floating.GetComponent<BoxCollider>() ||
                (t.Find("Floating/Model").localScale - Vector3.one * 1.5f).sqrMagnitude > .0001f ||
                !floating.Find("Bubble") || floating.Find("Bubble").GetComponentsInChildren<Collider>().Length > 0 ||
                !interaction || !interaction.visual || !interaction.outlineMesh || !interaction.outlineMaterial || interaction.actionHint != "E · 줍기")
                throw new Exception("Floating parcel configuration incomplete: " + t.name);
            if (hit.point.y > 1) roofCount++;
            foreach (Transform other in root.transform)
                if (t != other && (t.position - other.position).sqrMagnitude < 15.5f) throw new Exception("Parcel spacing failed.");
            report.Add(t.name + " | position=" + p.ToString("F3") + " | surface=" + hit.collider.name);
        }
        if (roofCount == 0) throw new Exception("Test needs at least one reachable flat-roof example.");
        int rejectedRoofs = 0;
        foreach (Transform house in GameObject.Find("Layer_04_Houses").transform)
        {
            if (AccessibleRoof(house)) continue;
            bool checkedRoof = false; var b = house.GetComponent<Renderer>().bounds;
            foreach (float x in new[] { -.25f, 0, .25f })
                foreach (float z in new[] { -.25f, 0, .25f })
                    foreach (var c in house.GetComponentsInChildren<Collider>())
                        if (c.Raycast(new Ray(new Vector3(b.center.x + x * b.size.x, 40, b.center.z + z * b.size.z), Vector3.down), out var h, 60) && h.point.y > 1)
                        {
                            if (Allowed(h)) throw new Exception("Roof without stairs was accepted: " + house.name);
                            checkedRoof = true;
                        }
            if (!checkedRoof) throw new Exception("Could not check forbidden roof: " + house.name);
            rejectedRoofs++;
        }
        Directory.CreateDirectory("art-review");
        report.Add("Forbidden house roofs rejected=" + rejectedRoofs + "/7");
        File.WriteAllLines("art-review/test-parcel-drops-20261006.txt", report);
        Debug.Log("TEST_PARCELS_VERIFY_OK: 10/10 supported, obstacle-free, spaced and textured. Ground=" +
            (Count - roofCount) + ", accessible flat roof=" + roofCount + ". Trees, stairs and inaccessible/pitched roofs excluded.");
    }

    [MenuItem("Tools/Gameplay/Test Parcels/Capture Review")]
    public static void CaptureReview()
    {
        var parcels = GameObject.Find(Layer).transform.Cast<Transform>().ToArray();
        foreach (bool roof in new[] { false, true })
        {
            var t = roof ? parcels.First(p => p.position.y > 1) :
                parcels.FirstOrDefault(p => Mathf.Abs(p.position.y + .23f) < .01f) ?? parcels.First(p => p.position.y < 1);
            var b = BoundsOf(t.gameObject);
            Capture("art-review/test-parcel-" + (roof ? "roof" : "ground") + "-20261006.png",
                b.center + new Vector3(2, 1.8f, -2.5f), b.center, 1.7f);
        }
    }

    static void Capture(string path, Vector3 eye, Vector3 target, float size)
    {
        var go = new GameObject("Parcel_Review_Camera") { hideFlags = HideFlags.HideAndDontSave };
        var rt = new RenderTexture(1200, 900, 24) { antiAliasing = 4 }; var old = RenderTexture.active;
        Texture2D image = null;
        try
        {
            var cam = go.AddComponent<Camera>(); cam.orthographic = true; cam.orthographicSize = size;
            cam.nearClipPlane = .1f; cam.farClipPlane = 500; cam.allowHDR = false;
            go.transform.position = eye; go.transform.LookAt(target); cam.targetTexture = rt;
            rt.Create(); cam.Render(); RenderTexture.active = rt;
            image = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally { RenderTexture.active = old; UnityEngine.Object.DestroyImmediate(go); rt.Release(); UnityEngine.Object.DestroyImmediate(rt); if (image) UnityEngine.Object.DestroyImmediate(image); }
    }
}
