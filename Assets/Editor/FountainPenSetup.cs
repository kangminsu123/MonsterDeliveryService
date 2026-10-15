using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FountainPenSetup
{
    [MenuItem("Tools/Gameplay/Fountain Pen/Build Model")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new Exception("Build in Edit Mode.");
        var scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("FountainPen"); SceneManager.MoveGameObjectToScene(root, scene);
        try
        {
            var ink = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/DeliveryItems/Materials/ITEMMAT_Ink.mat");
            var gold = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/DeliveryItems/Materials/ITEMMAT_Gold.mat");
            if (!ink || !gold) throw new Exception("Existing pen ink/gold materials missing.");
            GameObject Part(string name, PrimitiveType type, Vector3 point, Vector3 scale, Material material)
            {
                var part = GameObject.CreatePrimitive(type); SceneManager.MoveGameObjectToScene(part, scene);
                part.name = name; part.transform.SetParent(root.transform, false);
                part.transform.localPosition = point; part.transform.localScale = scale;
                UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
                part.GetComponent<MeshRenderer>().sharedMaterial = material; return part;
            }
            var body = Part("InkBarrel", PrimitiveType.Cylinder, Vector3.zero, new Vector3(.095f, .28f, .095f), ink);
            body.transform.localRotation = Quaternion.Euler(90, 0, 0);
            Part("RoundedBack", PrimitiveType.Sphere, Vector3.back * .28f, Vector3.one * .094f, ink);
            foreach (float z in new[] { -.21f, .21f })
                Part("GoldBand", PrimitiveType.Cylinder, Vector3.forward * z, new Vector3(.101f, .012f, .101f), gold)
                    .transform.localRotation = Quaternion.Euler(90, 0, 0);
            Part("Grip", PrimitiveType.Cylinder, Vector3.forward * .285f, new Vector3(.068f, .045f, .068f), ink)
                .transform.localRotation = Quaternion.Euler(90, 0, 0);
            Part("GoldClip", PrimitiveType.Cube, new Vector3(0, .055f, -.12f), new Vector3(.022f, .014f, .24f), gold);
            var nibMesh = new Mesh { name = "FountainPenNib" };
            nibMesh.vertices = new[] { new Vector3(0,.008f,.30f), new Vector3(-.058f,.008f,.36f),
                new Vector3(0,.008f,.51f), new Vector3(.058f,.008f,.36f), new Vector3(0,-.008f,.30f),
                new Vector3(-.058f,-.008f,.36f), new Vector3(0,-.008f,.51f), new Vector3(.058f,-.008f,.36f) };
            nibMesh.triangles = new[] { 0,1,2,0,2,3,4,6,5,4,7,6,0,4,5,0,5,1,1,5,6,1,6,2,2,6,7,2,7,3,3,7,4,3,4,0 };
            nibMesh.RecalculateNormals(); nibMesh.RecalculateBounds();
            string meshPath = "Assets/Art/DeliveryItems/FountainPenNib.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existing) { EditorUtility.CopySerialized(nibMesh, existing); UnityEngine.Object.DestroyImmediate(nibMesh); nibMesh = existing; }
            else AssetDatabase.CreateAsset(nibMesh, meshPath);
            var nib = new GameObject("GoldNib"); nib.transform.SetParent(root.transform, false);
            nib.AddComponent<MeshFilter>().sharedMesh = nibMesh; nib.AddComponent<MeshRenderer>().sharedMaterial = gold;
            Part("NibSlit", PrimitiveType.Cube, new Vector3(0,.013f,.424f), new Vector3(.005f,.003f,.165f), ink);
            Part("BreatherHole", PrimitiveType.Sphere, new Vector3(0,.014f,.351f), new Vector3(.016f,.005f,.016f), ink);
            var template = Resources.Load<GameObject>("DeliveryItems/PrinterInk").GetComponent<DeliveryHeldItem>();
            var pose = root.AddComponent<DeliveryHeldItem>();
            pose.firstPersonPosition = new Vector3(.32f, -.28f, .78f);
            pose.firstPersonRotation = Quaternion.Euler(-16, -12, -8);
            pose.thirdPersonBone = template.thirdPersonBone; pose.thirdPersonPosition = template.thirdPersonPosition;
            pose.thirdPersonRotation = template.thirdPersonRotation * Quaternion.Euler(0,180,0);
            Directory.CreateDirectory("Assets/Resources/DeliveryItems");
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/Resources/DeliveryItems/FountainPen.prefab");

            var cameraObject = new GameObject("PenIconCamera"); SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false; camera.scene = scene;
            camera.orthographic = true; camera.orthographicSize = .47f;
            camera.transform.position = new Vector3(.8f, 1.25f, 1.7f); camera.transform.LookAt(Vector3.forward * .08f);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
            var lightObject = new GameObject("PenIconLight"); SceneManager.MoveGameObjectToScene(lightObject, scene);
            var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.4f;
            light.transform.rotation = Quaternion.Euler(35,-35,0);
            var target = new RenderTexture(256,256,24,RenderTextureFormat.ARGB32);
            var image = new Texture2D(256,256,TextureFormat.RGBA32,false); var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0,0,256,256),0,0); image.Apply();
                string icon = "Assets/Resources/UI/InGame/item_FountainPen.png"; File.WriteAllBytes(icon,image.EncodeToPNG());
                AssetDatabase.ImportAsset(icon,ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(icon);
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single; importer.alphaIsTransparency = true; importer.SaveAndReimport();
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous; target.Release();
                UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image);
            }
            AssetDatabase.SaveAssets(); Debug.Log("FOUNTAIN_PEN_MODEL_OK: native pen, gold nib, held poses and actual model icon.");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
    }
}
