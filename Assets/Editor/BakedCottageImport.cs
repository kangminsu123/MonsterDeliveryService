using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public static class BakedCottageImport
{
    const string Root = "Assets/Art/BakedCottages";
    const string Review = "art-review/baked-cottages-unity.png";

    [MenuItem("Tools/Art/Import and Verify Baked Cottages")]
    public static void Import()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var files = Directory.GetFiles(Root, "*.fbx", SearchOption.AllDirectories).OrderBy(x => x).ToArray();
        if (files.Length != 10) throw new Exception("Expected 10 cottage FBX files; found " + files.Length);
        var prefabs = new List<GameObject>();
        var report = new List<string>();
        foreach (var file in files)
        {
            string path = file.Replace('\\', '/');
            string name = Path.GetFileNameWithoutExtension(path);
            string folder = Path.GetDirectoryName(path).Replace('\\', '/');
            foreach (string texturePath in Directory.GetFiles(folder, "*.png"))
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath(texturePath.Replace('\\', '/'));
                bool normal = texturePath.EndsWith("_Normal.png");
                ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                ti.sRGBTexture = texturePath.EndsWith("_Albedo.png");
                ti.alphaSource = TextureImporterAlphaSource.FromInput;
                ti.alphaIsTransparency = false;
                ti.mipmapEnabled = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.maxTextureSize = 2048;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.SaveAndReimport();
            }
            string materialPath = folder + "/" + name + "_Baked.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!material)
            {
                material = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.color = Color.white;
            material.SetTexture("_MainTex", Texture(folder, name, "Albedo"));
            material.SetTexture("_BumpMap", Texture(folder, name, "Normal"));
            material.SetTexture("_MetallicGlossMap", Texture(folder, name, "MetallicSmoothness"));
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_GlossMapScale", 1f);
            material.SetFloat("_BumpScale", 1f);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_METALLICGLOSSMAP");
            EditorUtility.SetDirty(material);
            var mi = (ModelImporter)AssetImporter.GetAtPath(path);
            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.bakeAxisConversion = true;
            mi.importAnimation = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importNormals = ModelImporterNormals.Import;
            mi.importTangents = ModelImporterTangents.CalculateMikk;
            mi.generateSecondaryUV = true;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name + "_Baked"), material);
            mi.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                instance.name = name;
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                instance.transform.localScale = Vector3.one;
                foreach (var r in instance.GetComponentsInChildren<Renderer>()) r.sharedMaterial = material;
                int tris = instance.GetComponentsInChildren<MeshFilter>().Sum(m => m.sharedMesh.triangles.Length / 3);
                var renderers = instance.GetComponentsInChildren<Renderer>();
                var bounds = renderers[0].bounds;
                foreach (var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);
                if (tris > 3000 || tris < 100 || Mathf.Abs(bounds.min.y) > .01f || bounds.size.x < 5f || bounds.size.x > 12f)
                    throw new Exception(name + " invalid geometry: triangles=" + tris + " bounds=" + bounds);
                if (!material.shader.isSupported || !material.mainTexture || !material.GetTexture("_BumpMap") || !material.GetTexture("_MetallicGlossMap"))
                    throw new Exception(name + " missing/unsupported baked material");
                var prefab = PrefabUtility.SaveAsPrefabAsset(instance, folder + "/" + name + ".prefab");
                prefabs.Add(prefab);
                report.Add(name + ": " + tris + " triangles, bounds=" + bounds.size + ", ground=" + bounds.min.y + ", 1 baked material, 3 connected texture maps");
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }
        AssetDatabase.SaveAssets();
        Preview(prefabs);
        File.WriteAllLines("art-review/baked-cottages-import-audit.txt", report);
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(Root);
        EditorGUIUtility.PingObject(Selection.activeObject);
        Debug.Log("BAKED_COTTAGES_IMPORT_OK: 10 FBX + 10 material-assigned prefabs. " + Review + "\n" + string.Join("\n", report));
    }

    static Texture2D Texture(string folder, string name, string kind)
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/" + name + "_" + kind + ".png");
        if (!texture) throw new Exception("Missing baked texture: " + name + "_" + kind);
        return texture;
    }

    [MenuItem("Tools/Art/Preview Baked Cottages")]
    public static void PreviewImported()
    {
        var prefabs = Directory.GetFiles(Root, "*.prefab", SearchOption.AllDirectories).OrderBy(x => x)
            .Select(x => AssetDatabase.LoadAssetAtPath<GameObject>(x.Replace('\\', '/'))).ToList();
        Preview(prefabs);
    }

    static void Preview(List<GameObject> prefabs)
    {
        var preview = new PreviewRenderUtility();
        try
        {
            for (int i = 0; i < prefabs.Count; i++)
            {
                var go = UnityEngine.Object.Instantiate(prefabs[i]);
                go.transform.position = new Vector3((i % 5 - 2) * 10.3f, 0, i < 5 ? 9.1f : -9.1f);
                go.transform.rotation = Quaternion.Euler(0, -22, 0);
                preview.AddSingleGO(go);
            }
            preview.camera.orthographic = true;
            preview.camera.orthographicSize = 14f;
            preview.camera.nearClipPlane = .1f;
            preview.camera.farClipPlane = 200f;
            preview.camera.transform.position = new Vector3(0, 29, -57);
            preview.camera.transform.LookAt(new Vector3(0, 2, 0));
            preview.camera.clearFlags = CameraClearFlags.Color;
            preview.camera.backgroundColor = new Color(.43f, .44f, .45f);
            preview.ambientColor = new Color(.55f, .55f, .55f);
            preview.lights[0].intensity = 1.2f;
            preview.lights[0].transform.rotation = Quaternion.Euler(45, -35, 0);
            preview.lights[1].intensity = .5f;
            preview.lights[1].transform.rotation = Quaternion.Euler(60, 145, 0);
            preview.BeginPreview(new Rect(0, 0, 2400, 1200), GUIStyle.none);
            preview.Render(true);
            var rt = (RenderTexture)preview.EndPreview();
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var image = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            if (QualitySettings.activeColorSpace == ColorSpace.Linear && !rt.sRGB)
            {
                var pixels = image.GetPixels();
                for (int i = 0; i < pixels.Length; i++) pixels[i] = pixels[i].gamma;
                image.SetPixels(pixels);
            }
            image.Apply();
            Directory.CreateDirectory("art-review");
            File.WriteAllBytes(Review, image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
            RenderTexture.active = previous;
        }
        finally { preview.Cleanup(); }
    }
}
