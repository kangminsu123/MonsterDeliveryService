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
    [MenuItem("Tools/Gameplay/Test Parcels/Upgrade Floating Parcels")]
    public static void Upgrade() => ParcelSpawnSetup.Build();

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

    public static bool AccessibleRoof(Transform house)
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
        if (root == "Layer_01_Sidewalks" || root == "Layer_02_Roads")
            return hit.point.y >= -.3f && hit.point.y < .5f;
        if (root != "Layer_04_Houses") return false;
        while (t.parent && t.parent.name != "Layer_04_Houses") t = t.parent;
        if (!AccessibleRoof(t)) return false;
        var landing = GameObject.Find("Layer_08_RooftopAccess").transform
            .Find(t.name.Substring(0, 7) + "_Roof_Stairs/Roof_Landing");
        // Exclude higher decorative tower tops even on a house with roof access.
        return landing && Mathf.Abs(hit.point.y - landing.GetComponent<Collider>().bounds.max.y) < .4f;
    }

    public static bool Surface(Vector3 p, out RaycastHit hit)
    {
        var hits = Physics.RaycastAll(new Vector3(p.x, 40, p.z), Vector3.down, 60, ~0, QueryTriggerInteraction.Ignore)
            .Where(h => h.collider.transform.root.name != Layer && !(h.collider is CharacterController))
            .OrderBy(h => h.distance).ToArray();
        hit = hits.Length > 0 ? hits[0] : default;
        // Never skip a tree/prop to accept the ground underneath it.
        return hits.Length > 0 && Allowed(hit);
    }

    public static bool Supported(Vector3 p)
    {
        foreach (float x in new[] { -.65f, 0, .65f })
            foreach (float z in new[] { -.65f, 0, .65f })
                if (!Surface(p + new Vector3(x, 0, z), out var hit) || Mathf.Abs(hit.point.y - p.y) > .035f) return false;
        return true;
    }

    public static bool Clear(Vector3 p)
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
    public static void Verify() => ParcelSpawnSetup.VerifyLayout();

    [MenuItem("Tools/Gameplay/Test Parcels/Capture Review")]
    public static void CaptureReview()
    {
        var parcels = GameObject.Find(Layer).GetComponentsInChildren<FloatingParcel>().Select(p => p.transform).ToArray();
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
