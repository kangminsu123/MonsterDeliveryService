using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ToyHammerSetup
{
    [MenuItem("Tools/Gameplay/Toy Hammer/Build Model")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new Exception("Build in Edit Mode.");
        Directory.CreateDirectory("Assets/Resources/DeliveryItems");
        Material Paint(string name, Color color)
        {
            string path = "Assets/Resources/DeliveryItems/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(material, path); }
            material.color = color; material.SetFloat("_Glossiness", .62f);
            material.SetColor("_EmissionColor", color * .6f); material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            MaterialEditor.FixupEmissiveFlag(material); EditorUtility.SetDirty(material); return material;
        }
        var red = Paint("HammerRed", new Color(1, .045f, .13f));
        var pink = Paint("HammerRib", new Color(1, .2f, .29f));
        var yellow = Paint("HammerYellow", new Color(1, .77f, .04f));
        var blue = Paint("HammerBlue", new Color(.06f, .36f, .95f));
        var scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("ToyHammer"); SceneManager.MoveGameObjectToScene(root, scene);
        try
        {
            GameObject Part(string name, PrimitiveType type, Vector3 p, Vector3 scale, Material material)
            {
                var part = GameObject.CreatePrimitive(type); SceneManager.MoveGameObjectToScene(part, scene);
                part.name = name; part.transform.SetParent(root.transform, false);
                part.transform.localPosition = p; part.transform.localScale = scale;
                UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
                part.GetComponent<Renderer>().sharedMaterial = material; return part;
            }
            Part("YellowHandle", PrimitiveType.Cylinder, new Vector3(0, .355f, 0), new Vector3(.085f, .405f, .085f), yellow);
            Part("GripEnd", PrimitiveType.Sphere, new Vector3(0, -.06f, 0), new Vector3(.12f, .1f, .12f), blue);
            for (int i = 0; i < 5; i++) Part("GripRing", PrimitiveType.Cylinder, new Vector3(0, i * .055f, 0), new Vector3(.098f, .012f, .098f), blue);
            Part("HeadNeck", PrimitiveType.Sphere, new Vector3(0, .76f, 0), new Vector3(.17f, .15f, .17f), yellow);
            Part("RubberHead", PrimitiveType.Cylinder, new Vector3(0, .84f, 0), new Vector3(.29f, .32f, .29f), red)
                .transform.localRotation = Quaternion.Euler(0, 0, 90);
            for (int i = 0; i < 9; i++)
                Part("AccordionRib", PrimitiveType.Cylinder, new Vector3((i - 4) * .075f, .84f, 0), new Vector3(.37f, .018f, .37f), i % 2 == 0 ? red : pink)
                    .transform.localRotation = Quaternion.Euler(0, 0, 90);
            foreach (int side in new[] { -1, 1 })
            {
                Part("SoftHammerFace", PrimitiveType.Sphere, new Vector3(side * .385f, .84f, 0), new Vector3(.13f, .4f, .4f), red);
                Part("FaceButton", PrimitiveType.Sphere, new Vector3(side * .45f, .84f, 0), new Vector3(.025f, .16f, .16f), yellow);
            }
            var head = new GameObject("HammerHead"); SceneManager.MoveGameObjectToScene(head, scene);
            head.transform.SetParent(root.transform, false); head.transform.localPosition = Vector3.up * .84f;
            // Turn only the head: its two striking faces now point toward the owner and target.
            foreach (var part in root.GetComponentsInChildren<MeshRenderer>())
                if (part.name == "RubberHead" || part.name == "AccordionRib" || part.name == "SoftHammerFace" || part.name == "FaceButton")
                    part.transform.SetParent(head.transform, true);
            head.transform.localRotation = Quaternion.Euler(0, 90, 0);
            var template = Resources.Load<GameObject>("DeliveryItems/Megaphone").GetComponent<DeliveryHeldItem>();
            var pose = root.AddComponent<DeliveryHeldItem>();
            pose.firstPersonPosition = new Vector3(.22f, -.25f, 1.05f);
            pose.firstPersonRotation = Quaternion.Euler(-12, 0, -18); pose.firstPersonScale = .55f;
            pose.thirdPersonBone = template.thirdPersonBone; pose.thirdPersonPosition = template.thirdPersonPosition;
            pose.thirdPersonRotation = template.thirdPersonRotation;
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/Resources/DeliveryItems/ToyHammer.prefab");

            var cameraObject = new GameObject("HammerIconCamera"); SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false; camera.scene = scene;
            camera.orthographic = true; camera.orthographicSize = .72f;
            camera.transform.position = new Vector3(1.2f, 1.1f, -2); camera.transform.LookAt(Vector3.up * .46f);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
            var lightObject = new GameObject("HammerIconLight"); SceneManager.MoveGameObjectToScene(lightObject, scene);
            var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.4f;
            light.transform.rotation = Quaternion.Euler(35, -35, 0);
            var target = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(256, 256, TextureFormat.RGBA32, false); var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); image.Apply();
                string icon = "Assets/Resources/UI/InGame/item_ToyHammer.png"; File.WriteAllBytes(icon, image.EncodeToPNG());
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
            AssetDatabase.SaveAssets(); Debug.Log("TOY_HAMMER_MODEL_OK: ribbed red toy head, yellow handle, blue grip and rendered inventory icon.");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
    }
}
