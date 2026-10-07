using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// Imports the authored props and places one mailbox beside each existing yard entrance.
public static class DeliveryPropsPlacement
{
    const string AssetsRoot = "Assets/Art/DeliveryProps";
    const string Layer = "Layer_11_Mailboxes";
    static readonly string[] Names = { "parcel", "mailbox" };

    [MenuItem("Tools/Art/Delivery Props/Import and Place Mailboxes")]
    public static void ImportAndPlace()
    {
        RequireMap();
        Directory.CreateDirectory("art-review");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        const string backup = "art-review/before-mailbox-placement.unity";
        if (!File.Exists(backup)) EditorSceneManager.SaveScene(scene, backup, true);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach (string name in Names) ImportProp(name);
        AssetDatabase.SaveAssets();
        Place();
        Verify();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        CaptureReview();
        Selection.activeGameObject = GameObject.Find(Layer);
        SceneView.lastActiveSceneView?.FrameSelected();
        Debug.Log("DELIVERY_PROPS_OK: two textured FBX/prefabs imported; 10 grounded mailboxes beside yard gates; doors face roads; scene saved.");
    }

    static void ImportProp(string name)
    {
        foreach (string kind in new[] { "albedo", "orm" })
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(AssetsRoot + "/" + name + "_" + kind + ".png");
            if (!ti) throw new Exception("Missing texture: " + name + "_" + kind);
            ti.sRGBTexture = kind == "albedo";
            ti.isReadable = kind == "orm";
            ti.mipmapEnabled = true; ti.maxTextureSize = 2048;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
        }
        string packedPath = AssetsRoot + "/" + name + "_MetallicSmoothness.png";
        var orm = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetsRoot + "/" + name + "_orm.png");
        var pixels = orm.GetPixels();
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(pixels[i].b, 0, 0, 1 - pixels[i].g);
        var packed = new Texture2D(orm.width, orm.height, TextureFormat.RGBA32, false, true);
        try { packed.SetPixels(pixels); packed.Apply(); File.WriteAllBytes(packedPath, packed.EncodeToPNG()); }
        finally { UnityEngine.Object.DestroyImmediate(packed); }
        AssetDatabase.ImportAsset(packedPath, ImportAssetOptions.ForceSynchronousImport);
        var packedImporter = (TextureImporter)AssetImporter.GetAtPath(packedPath);
        packedImporter.sRGBTexture = false; packedImporter.maxTextureSize = 2048;
        packedImporter.textureCompression = TextureImporterCompression.Uncompressed;
        packedImporter.wrapMode = TextureWrapMode.Clamp; packedImporter.SaveAndReimport();
        var ormImporter = (TextureImporter)AssetImporter.GetAtPath(AssetsRoot + "/" + name + "_orm.png");
        ormImporter.isReadable = false; ormImporter.SaveAndReimport();

        string matPath = AssetsRoot + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (!mat) { mat = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(mat, matPath); }
        mat.color = Color.white;
        mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetsRoot + "/" + name + "_albedo.png");
        mat.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(packedPath));
        mat.SetFloat("_GlossMapScale", 1); mat.EnableKeyword("_METALLICGLOSSMAP");
        EditorUtility.SetDirty(mat);

        string modelPath = AssetsRoot + "/" + name + ".fbx";
        var mi = (ModelImporter)AssetImporter.GetAtPath(modelPath);
        mi.globalScale = 1; mi.useFileScale = true; mi.bakeAxisConversion = true;
        mi.importAnimation = false; mi.importCameras = false; mi.importLights = false;
        mi.importNormals = ModelImporterNormals.Import;
        mi.importTangents = ModelImporterTangents.CalculateMikk;
        mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "MAT_EXPORT_" + name), mat);
        mi.SaveAndReimport();
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        var wrapper = new GameObject(name == "mailbox" ? "Mailbox" : "DroppedParcel");
        var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, wrapper.transform);
        try
        {
            visual.name = "Model";
            // Preserve the model importer's axis conversion on the model root.
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = Vector3.one;
            foreach (var r in visual.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;
            if (name == "mailbox")
            {
                // The post foot identifies the rear; canonical prefab door direction is +Z.
                var points = visual.GetComponentsInChildren<MeshFilter>()
                    .SelectMany(m => m.sharedMesh.vertices.Select(v => m.transform.TransformPoint(v))).ToArray();
                float low = points.Min(v => v.y);
                var foot = points.Where(v => v.y < low + .001f).ToArray();
                var back = foot.Aggregate(Vector3.zero, (a, v) => a + v) / foot.Length;
                back.y = 0;
                if (back.sqrMagnitude < .01f) throw new Exception("Cannot resolve mailbox door direction.");
                visual.transform.localRotation = Quaternion.FromToRotation(-back.normalized, Vector3.forward) * visual.transform.localRotation;
            }
            var bounds = BoundsOf(wrapper);
            visual.transform.localPosition -= Vector3.up * bounds.min.y;
            if (name == "mailbox")
            {
                // Extend the lower timber only; the box, flag, brace and foot keep their dimensions.
                var mf = visual.GetComponentsInChildren<MeshFilter>().Single();
                var mesh = UnityEngine.Object.Instantiate(mf.sharedMesh);
                var vertices = mesh.vertices;
                for (int i = 0; i < vertices.Length; i++)
                {
                    var point = mf.transform.TransformPoint(vertices[i]);
                    point.y += 1.1f * Mathf.Clamp01((point.y - .135f) / (.65f - .135f));
                    vertices[i] = mf.transform.InverseTransformPoint(point);
                }
                mesh.vertices = vertices;
                mesh.RecalculateBounds(); mesh.RecalculateNormals(); mesh.RecalculateTangents();
                mesh.name = "Mailbox_RaisedPost";
                string meshPath = AssetsRoot + "/mailbox_RaisedPost.asset";
                var saved = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (saved) { EditorUtility.CopySerialized(mesh, saved); UnityEngine.Object.DestroyImmediate(mesh); EditorUtility.SetDirty(saved); }
                else { AssetDatabase.CreateAsset(mesh, meshPath); saved = mesh; }
                mf.sharedMesh = saved;
            }
            foreach (var mf in visual.GetComponentsInChildren<MeshFilter>())
            {
                var collider = mf.gameObject.GetComponent<MeshCollider>();
                if (!collider) collider = mf.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = mf.sharedMesh;
            }
            foreach (Transform tr in wrapper.GetComponentsInChildren<Transform>()) tr.gameObject.isStatic = true;
            bounds = BoundsOf(wrapper);
            if (Mathf.Abs(bounds.min.y) > .002f || Mathf.Abs(bounds.size.y - (name == "mailbox" ? 2.832f : .3841f)) > .01f)
                throw new Exception(name + " import scale mismatch: " + bounds);
            if (!mat.shader.isSupported || !mat.mainTexture || !mat.GetTexture("_MetallicGlossMap"))
                throw new Exception(name + " material incomplete.");
            if (name == "mailbox") { AddBeacon(wrapper); AddInteraction(wrapper); }
            else TestParcelDrops.SetupFloatingParcel(wrapper);
            PrefabUtility.SaveAsPrefabAsset(wrapper, AssetsRoot + "/" + name + ".prefab");
        }
        finally { UnityEngine.Object.DestroyImmediate(wrapper); }
    }

    public static void AddInteraction(GameObject mailbox, string assetName = "mailbox", string displayName = "우체통", string hint = "E 유지 · 점령")
    {
        var source = mailbox.GetComponentInChildren<MeshFilter>();
        var mesh = UnityEngine.Object.Instantiate(source.sharedMesh);
        var vertices = mesh.vertices; var normals = mesh.normals;
        var smooth = new Dictionary<Vector3, Vector3>();
        for (int i = 0; i < vertices.Length; i++) smooth[vertices[i]] = smooth.TryGetValue(vertices[i], out var n) ? n + normals[i] : normals[i];
        for (int i = 0; i < vertices.Length; i++) normals[i] = smooth[vertices[i]].normalized;
        mesh.normals = normals; mesh.name = assetName + "_Outline";
        var bounds = mesh.bounds; bounds.Expand(.15f); mesh.bounds = bounds;
        string meshPath = AssetsRoot + "/" + assetName + "_Outline.asset";
        var saved = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (saved) { EditorUtility.CopySerialized(mesh, saved); UnityEngine.Object.DestroyImmediate(mesh); EditorUtility.SetDirty(saved); }
        else { AssetDatabase.CreateAsset(mesh, meshPath); saved = mesh; }
        string matPath = AssetsRoot + "/InteractionOutline.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (!material) { material = new Material(Shader.Find("Delivery/Interaction Outline")); AssetDatabase.CreateAsset(material, matPath); }
        material.SetFloat("_Width", 4.5f); EditorUtility.SetDirty(material);
        if (!material.shader.isSupported) throw new Exception("Interaction outline shader unsupported.");
        var target = mailbox.GetComponent<InteractionTarget>(); if (!target) target = mailbox.AddComponent<InteractionTarget>(); target.displayName = displayName;
        target.actionHint = hint; target.visual = source; target.outlineMesh = saved; target.outlineMaterial = material;
    }

    static void AddBeacon(GameObject mailbox)
    {
        string materialPath = AssetsRoot + "/MailboxBeacon.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (!mat) { mat = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(mat, materialPath); }
        mat.shader = Shader.Find("Unlit/Color");
        if (!mat.shader || !mat.shader.isSupported) throw new Exception("Beacon unlit shader unavailable.");
        mat.color = Color.white;
        mat.mainTexture = Texture2D.whiteTexture;
        mat.DisableKeyword("_EMISSION"); EditorUtility.SetDirty(mat);

        var beacon = new GameObject("CaptureBeacon"); beacon.transform.SetParent(mailbox.transform, false);
        var ringObject = new GameObject("PulseRing"); ringObject.transform.SetParent(beacon.transform, false);
        ringObject.transform.localPosition = new Vector3(0, 2.16f, 0);
        var ring = ringObject.AddComponent<LineRenderer>();
        ring.useWorldSpace = false; ring.loop = true; ring.positionCount = 64; ring.widthMultiplier = .035f;
        ring.sharedMaterial = mat; ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ring.receiveShadows = false;
        for (int i = 0; i < ring.positionCount; i++)
        {
            float angle = i * Mathf.PI * 2 / ring.positionCount;
            ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * .48f, 0, Mathf.Sin(angle) * .48f));
        }
        var pulse = new AnimationClip { name = "MailboxBeaconPulse", legacy = true, wrapMode = WrapMode.Loop };
        foreach (string axis in new[] { "x", "z" })
            pulse.SetCurve("PulseRing", typeof(Transform), "localScale." + axis,
                new AnimationCurve(new Keyframe(0, .94f, 0, 0), new Keyframe(1.1f, 1.08f, 0, 0), new Keyframe(2.2f, .94f, 0, 0)));
        string clipPath = AssetsRoot + "/MailboxBeaconPulse.anim";
        var saved = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (saved) { EditorUtility.CopySerialized(pulse, saved); UnityEngine.Object.DestroyImmediate(pulse); EditorUtility.SetDirty(saved); }
        else { AssetDatabase.CreateAsset(pulse, clipPath); saved = pulse; }
        var animation = beacon.AddComponent<Animation>(); animation.AddClip(saved, saved.name);
        animation.clip = saved; animation.playAutomatically = true;

        var sparks = new GameObject("RisingSparks"); sparks.transform.SetParent(beacon.transform, false);
        sparks.transform.localPosition = new Vector3(0, 2.1f, 0);
        sparks.transform.localRotation = Quaternion.Euler(-90, 0, 0);
        var particles = sparks.AddComponent<ParticleSystem>(); particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main; main.duration = 3; main.loop = true; main.prewarm = true; main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 3.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(.18f, .3f);
        main.startSize = new ParticleSystem.MinMaxCurve(.045f, .075f);
        main.maxParticles = 16; main.simulationSpace = ParticleSystemSimulationSpace.Local;
        var emission = particles.emission; emission.rateOverTime = 4;
        var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(.9f, .8f, .2f);
        var size = particles.sizeOverLifetime; size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, 0), new Keyframe(.2f, 1), new Keyframe(.8f, .7f), new Keyframe(1, 0)));
        var renderer = particles.GetComponent<ParticleSystemRenderer>(); renderer.renderMode = ParticleSystemRenderMode.Mesh;
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        try { renderer.mesh = sphere.GetComponent<MeshFilter>().sharedMesh; }
        finally { UnityEngine.Object.DestroyImmediate(sphere); }
        renderer.sharedMaterial = mat; renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    [MenuItem("Tools/Art/Delivery Props/Verify Beacon Motion")]
    static void VerifyBeaconMotion()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Beacon motion check requires Play Mode.");
        var root = GameObject.Find(Layer);
        foreach (Transform mailbox in root.transform)
        {
            var animation = mailbox.GetComponentInChildren<Animation>();
            var particles = mailbox.GetComponentInChildren<ParticleSystem>();
            if (!animation || !animation.isPlaying || !particles || !particles.isPlaying || particles.particleCount == 0)
                throw new Exception("Beacon not animating: " + mailbox.name);
            var ring = mailbox.Find("CaptureBeacon/PulseRing");
            float time = animation[animation.clip.name].time;
            animation.clip.SampleAnimation(animation.gameObject, 0); float low = ring.localScale.x;
            animation.clip.SampleAnimation(animation.gameObject, 1.1f); float high = ring.localScale.x;
            animation.clip.SampleAnimation(animation.gameObject, Mathf.Repeat(time, animation.clip.length));
            if (Mathf.Abs(low - .94f) > .001f || Mathf.Abs(high - 1.08f) > .001f)
                throw new Exception("Pulse curve failed: " + mailbox.name);
            Debug.Log(mailbox.name + " beacon: pulse=" + low.ToString("F2") + ".." + high.ToString("F2") + " loop time=" + animation[animation.clip.name].time.ToString("F2") +
                " ring scale=" + mailbox.Find("CaptureBeacon/PulseRing").localScale.x.ToString("F3") + " sparks=" + particles.particleCount);
        }
        Debug.Log("BEACON_MOTION_OK: 10 looping pulse rings and 10 active rising-particle systems.");
    }

    static void RequireMap()
    {
        if (EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "SampleScene" ||
            !GameObject.Find("Layer_04_Houses") || GameObject.Find("Layer_04_Houses").transform.childCount != 10)
            throw new Exception("Expected SampleScene with 10 houses in Edit Mode.");
    }

    static void Place()
    {
        var houses = GameObject.Find("Layer_04_Houses").transform;
        var yards = GameObject.Find("Layer_05_Yards").transform;
        var ground = GameObject.Find("Layer_00_GrassBase").GetComponent<MeshCollider>();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetsRoot + "/mailbox.prefab");
        var root = GameObject.Find(Layer);
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Place mailboxes at ten house entrances");
        if (!root) { root = new GameObject(Layer); Undo.RegisterCreatedObjectUndo(root, "Mailbox layer"); }
        var report = new List<string>();
        try
        {
            for (int i = 0; i < 10; i++)
            {
                var house = houses.GetChild(i); var yard = yards.Find("Yard_" + (i + 1).ToString("D2"));
                Vector3 forward = -house.forward; forward.y = 0; forward.Normalize();
                var fence = yard.Find("WhitePicketFence").GetComponent<Renderer>().bounds;
                float gateZ = forward.z < 0 ? fence.min.z : fence.max.z;
                var paths = yard.GetComponentsInChildren<BoxCollider>().Where(c => c.name.StartsWith("Paver")).ToArray();
                var path = paths.OrderBy(c => Mathf.Abs(c.bounds.center.z - gateZ)).First();
                float entryX = path.bounds.center.x;
                float halfWidth = Mathf.Max(Mathf.Abs(path.bounds.min.x - entryX), Mathf.Abs(path.bounds.max.x - entryX));
                // Include both lanes of the tiled path.
                var gatePaths = paths.Where(c => Mathf.Abs(c.bounds.center.z - path.bounds.center.z) < .2f).ToArray();
                entryX = gatePaths.Average(c => c.bounds.center.x);
                halfWidth = gatePaths.Max(c => Mathf.Max(Mathf.Abs(c.bounds.min.x - entryX), Mathf.Abs(c.bounds.max.x - entryX)));
                string name = "Plot_" + (i + 1).ToString("D2") + "_Mailbox";
                var found = root.transform.Find(name);
                var mailbox = found ? found.gameObject : (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                if (!found) Undo.RegisterCreatedObjectUndo(mailbox, "Place mailbox"); else Undo.RecordObject(mailbox.transform, "Move mailbox");
                mailbox.name = name; mailbox.transform.rotation = Quaternion.LookRotation(forward);
                mailbox.transform.localScale = Vector3.one;
                bool placed = false;
                var rejected = new List<string>();
                foreach (float side in new[] { 1f, -1f })
                foreach (float lateral in new[] { .7f, 1.15f, 1.7f, 2.3f })
                foreach (float outward in new[] { .5f, .55f, .6f, .75f })
                {
                    if (placed) continue;
                    var p = new Vector3(entryX + side * (halfWidth + lateral), 0, gateZ) + forward * outward;
                    if (!ground.Raycast(new Ray(p + Vector3.up * 15, Vector3.down), out var support, 40)) continue;
                    mailbox.transform.position = new Vector3(p.x, support.point.y, p.z);
                    Physics.SyncTransforms();
                    var blocked = Obstructions(mailbox, root);
                    if (blocked.Length > 0) { rejected.Add(p.ToString("F2") + ": " + string.Join(",", blocked.Select(c => c.name))); continue; }
                    var b = BoundsOf(mailbox);
                    if (Mathf.Abs(b.min.y - support.point.y) > .004f) continue;
                    placed = true;
                    report.Add(name + " | house=" + house.name + " | position=" + mailbox.transform.position.ToString("F3") +
                        " | road-facing=" + forward.ToString("F1") + " | height=" + b.size.y.ToString("F3") +
                        " | gate-path clearance=" + (Mathf.Abs(b.center.x - entryX) - halfWidth - b.extents.x).ToString("F3") + " | overlaps=0");
                }
                if (!placed) throw new Exception("No clear grass beside gate for " + house.name + "\n" + string.Join("\n", rejected));
                PrefabUtility.RecordPrefabInstancePropertyModifications(mailbox.transform);
            }
            Undo.CollapseUndoOperations(group);
            File.WriteAllLines("art-review/mailbox-placement-20261006.txt", report);
        }
        catch { Undo.RevertAllDownToGroup(group); throw; }
    }

    static Collider[] Obstructions(GameObject mailbox, GameObject root)
    {
        var b = BoundsOf(mailbox); b.min += Vector3.up * .035f;
        return Physics.OverlapBox(b.center, b.extents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)
            .Where(c => !c.transform.IsChildOf(root.transform) && c.gameObject.name != "Layer_00_GrassBase").ToArray();
    }

    [MenuItem("Tools/Art/Delivery Props/Verify Mailboxes")]
    public static void Verify()
    {
        RequireMap(); var root = GameObject.Find(Layer);
        if (!root || root.transform.childCount != 10) throw new Exception("Expected exactly 10 placed mailboxes.");
        var houses = GameObject.Find("Layer_04_Houses").transform;
        var ground = GameObject.Find("Layer_00_GrassBase").GetComponent<MeshCollider>();
        Physics.SyncTransforms();
        for (int i = 0; i < 10; i++)
        {
            var mailbox = root.transform.Find("Plot_" + (i + 1).ToString("D2") + "_Mailbox");
            if (!mailbox || Vector3.Dot(mailbox.forward, -houses.GetChild(i).forward) < .99f) throw new Exception("Mailbox missing or faces house: " + i);
            var yard = GameObject.Find("Layer_05_Yards").transform.Find("Yard_" + (i + 1).ToString("D2"));
            var fence = yard.Find("WhitePicketFence").GetComponent<Renderer>().bounds;
            float front = mailbox.forward.z < 0 ? fence.min.z : fence.max.z;
            if ((mailbox.position.z - front) * mailbox.forward.z < .45f) throw new Exception("Mailbox hidden behind fence: " + mailbox.name);
            var b = BoundsOf(mailbox.gameObject);
            if (Mathf.Abs(b.size.y - 2.832f) > .01f) throw new Exception("Mailbox height mismatch: " + mailbox.name);
            if (!ground.Raycast(new Ray(mailbox.position + Vector3.up * 15, Vector3.down), out var support, 40) || Mathf.Abs(b.min.y - support.point.y) > .004f)
                throw new Exception("Floating mailbox: " + mailbox.name);
            var beacon = mailbox.Find("CaptureBeacon");
            if (!beacon || beacon.GetComponentsInChildren<Collider>().Length > 0 || !beacon.GetComponentInChildren<LineRenderer>() ||
                !beacon.GetComponent<Animation>() || !beacon.GetComponentInChildren<ParticleSystem>()) throw new Exception("Missing/solid beacon: " + mailbox.name);
            if (Obstructions(mailbox.gameObject, root).Length > 0) throw new Exception("Mailbox blocks existing geometry: " + mailbox.name);
            if (mailbox.GetComponentsInChildren<MeshRenderer>().Any(r => !r.sharedMaterial || !r.sharedMaterial.mainTexture || !r.sharedMaterial.shader.isSupported))
                throw new Exception("Broken material: " + mailbox.name);
        }
        Debug.Log("MAILBOX_VERIFY_OK: 10/10 grounded, road-facing, outside fences, obstacle-free and textured; parcel prefab available.");
    }

    static Bounds BoundsOf(GameObject go)
    {
        var rr = go.GetComponentsInChildren<MeshRenderer>(); var b = rr[0].bounds;
        foreach (var r in rr.Skip(1)) b.Encapsulate(r.bounds);
        return b;
    }

    [MenuItem("Tools/Art/Delivery Props/Capture Review")]
    public static void CaptureReview()
    {
        var layer = GameObject.Find(Layer).transform;
        foreach (var particles in layer.GetComponentsInChildren<ParticleSystem>())
        {
            particles.Simulate(2.8f, true, true, true);
            if (EditorApplication.isPlaying) particles.Play();
        }
        foreach (int index in new[] { 0, 6, 8 })
        {
            var m = layer.GetChild(index); var forward = m.forward;
            Capture("art-review/mailbox-plot-" + (index + 1).ToString("D2") + ".png",
                m.position + Vector3.up * 2.7f + forward * 4 - m.right * 3, m.position + Vector3.up * 1.4f, 2.7f);
        }
        Capture("art-review/mailboxes-overview-20261006.png", new Vector3(0, 105, -75), Vector3.zero, 91);
    }

    static void Capture(string path, Vector3 eye, Vector3 target, float size)
    {
        var go = new GameObject("Mailbox_Review_Camera") { hideFlags = HideFlags.HideAndDontSave };
        var rt = new RenderTexture(1400, 1000, 24) { antiAliasing = 4 };
        var old = RenderTexture.active; Texture2D image = null;
        try
        {
            var camera = go.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = size;
            camera.nearClipPlane = .1f; camera.farClipPlane = 500; camera.allowHDR = false;
            go.transform.position = eye; go.transform.LookAt(target);
            camera.targetTexture = rt; rt.Create(); camera.Render(); RenderTexture.active = rt;
            image = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally { RenderTexture.active = old; UnityEngine.Object.DestroyImmediate(go); rt.Release(); UnityEngine.Object.DestroyImmediate(rt); if (image) UnityEngine.Object.DestroyImmediate(image); }
    }
}
