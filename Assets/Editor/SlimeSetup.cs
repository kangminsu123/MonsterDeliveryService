using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SlimeSetup
{
    [MenuItem("Tools/Gameplay/Slime/Build Model")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new Exception("Build in Edit Mode.");
        Directory.CreateDirectory("Assets/Resources/DeliveryItems");
        Material Paint(string name, Color color)
        {
            string path = "Assets/Resources/DeliveryItems/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(material, path); }
            material.color = color; material.SetFloat("_Glossiness", .92f); material.SetFloat("_Metallic", .08f);
            material.SetColor("_EmissionColor", color * 1.05f); material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            MaterialEditor.FixupEmissiveFlag(material);
            EditorUtility.SetDirty(material); return material;
        }
        var gel = Paint("SlimeGel", new Color(.24f, .78f, .018f));
        var shine = Paint("SlimeHighlight", new Color(.8f, 1, .36f));
        var scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Slime"); SceneManager.MoveGameObjectToScene(root, scene);
        try
        {
            void Part(string name, Vector3 p, Vector3 scale, Material material)
            {
                var part = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                SceneManager.MoveGameObjectToScene(part, scene); part.name = name;
                part.transform.SetParent(root.transform, false); part.transform.localPosition = p; part.transform.localScale = scale;
                UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
                part.GetComponent<Renderer>().sharedMaterial = material;
            }
            Part("JellyBody", Vector3.zero, new Vector3(.48f, .38f, .44f), gel);
            Part("JellyLobeLeft", new Vector3(-.15f, -.08f, .05f), new Vector3(.26f, .22f, .28f), gel);
            Part("JellyLobeRight", new Vector3(.15f, -.09f, -.05f), new Vector3(.25f, .2f, .24f), gel);
            Part("WetShine", new Vector3(-.09f, .13f, -.1f), new Vector3(.13f, .035f, .08f), shine);
            Part("WetShineSmall", new Vector3(.04f, .16f, -.05f), new Vector3(.05f, .025f, .04f), shine);
            var template = Resources.Load<GameObject>("DeliveryItems/PackingTape").GetComponent<DeliveryHeldItem>();
            var pose = root.AddComponent<DeliveryHeldItem>();
            pose.firstPersonPosition = new Vector3(.34f, -.27f, .8f);
            pose.firstPersonScale = .65f;
            pose.firstPersonRotation = Quaternion.Euler(-15, -15, 0);
            pose.thirdPersonBone = template.thirdPersonBone; pose.thirdPersonPosition = template.thirdPersonPosition;
            pose.thirdPersonRotation = Quaternion.identity;
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/Resources/DeliveryItems/Slime.prefab");

            var cameraObject = new GameObject("SlimeIconCamera"); SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false; camera.scene = scene;
            camera.orthographic = true; camera.orthographicSize = .34f;
            camera.transform.position = new Vector3(.8f, .7f, -1.1f); camera.transform.LookAt(Vector3.zero);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
            var lightObject = new GameObject("SlimeIconLight"); SceneManager.MoveGameObjectToScene(lightObject, scene);
            var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.4f;
            light.transform.rotation = Quaternion.Euler(35, -35, 0);
            var target = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(256, 256, TextureFormat.RGBA32, false); var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); image.Apply();
                string icon = "Assets/Resources/UI/InGame/item_Slime.png"; File.WriteAllBytes(icon, image.EncodeToPNG());
                AssetDatabase.ImportAsset(icon, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(icon);
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.SaveAndReimport();
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous; target.Release();
                UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image);
            }
            AssetDatabase.SaveAssets(); Debug.Log("SLIME_MODEL_OK: wet green jelly, collider-free held prefab and rendered inventory icon.");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
    }
}
