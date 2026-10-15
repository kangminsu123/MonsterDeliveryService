using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class IceCreamSetup
{
    [MenuItem("Tools/Gameplay/IceCream/Build Model")]
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
        var gel = Paint("IceCreamStrawberry", new Color(1, .38f, .57f));
        var waffle = Paint("IceCreamWaffle", new Color(.72f, .38f, .12f));
        var shine = Paint("IceCreamHighlight", new Color(1, .94f, .76f));
        var scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("IceCream"); SceneManager.MoveGameObjectToScene(root, scene);
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
            Part("StrawberryScoop", new Vector3(0, .16f, 0), new Vector3(.38f, .34f, .38f), gel);
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * 2 / 8;
                Part("ScoopRuffle", new Vector3(Mathf.Cos(angle) * .13f, .04f, Mathf.Sin(angle) * .13f), Vector3.one * .13f, gel);
            }
            Part("VanillaSwirl", new Vector3(-.035f, .28f, 0), new Vector3(.24f, .15f, .24f), shine);
            Part("StrawberryTip", new Vector3(.01f, .36f, 0), Vector3.one * .13f, gel);
            var cone = new GameObject("WaffleCone"); cone.transform.SetParent(root.transform, false);
            const int sides = 24;
            var vertices = new Vector3[sides + 2]; var triangles = new int[sides * 6];
            vertices[0] = new Vector3(0, -.38f, 0); vertices[sides + 1] = Vector3.zero;
            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2 / sides;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * .155f, 0, Mathf.Sin(angle) * .155f);
                int next = (i + 1) % sides + 1;
                triangles[i * 6] = 0; triangles[i * 6 + 1] = i + 1; triangles[i * 6 + 2] = next;
                triangles[i * 6 + 3] = sides + 1; triangles[i * 6 + 4] = next; triangles[i * 6 + 5] = i + 1;
            }
            var mesh = new Mesh { name = "IceCreamConeMesh", vertices = vertices, triangles = triangles }; mesh.RecalculateNormals();
            string meshPath = "Assets/Resources/DeliveryItems/IceCreamCone.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existing) { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); mesh = existing; EditorUtility.SetDirty(existing); }
            else AssetDatabase.CreateAsset(mesh, meshPath);
            cone.AddComponent<MeshFilter>().sharedMesh = mesh; cone.AddComponent<MeshRenderer>().sharedMaterial = waffle;
            for (int i = 0; i < 5; i++)
                Part("WaffleRidge", new Vector3(0, -.04f - i * .055f, 0), new Vector3(.29f - i * .04f, .018f, .29f - i * .04f), waffle);
            var template = Resources.Load<GameObject>("DeliveryItems/PackingTape").GetComponent<DeliveryHeldItem>();
            var pose = root.AddComponent<DeliveryHeldItem>();
            pose.firstPersonPosition = new Vector3(.34f, -.28f, 1.05f);
            pose.firstPersonScale = .6f;
            pose.firstPersonRotation = Quaternion.Euler(-8, -12, -12);
            pose.thirdPersonBone = template.thirdPersonBone; pose.thirdPersonPosition = template.thirdPersonPosition;
            pose.thirdPersonRotation = Quaternion.identity;
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/Resources/DeliveryItems/IceCream.prefab");

            var cameraObject = new GameObject("IceCreamIconCamera"); SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false; camera.scene = scene;
            camera.orthographic = true; camera.orthographicSize = .46f;
            camera.transform.position = new Vector3(.8f, .7f, -1.1f); camera.transform.LookAt(Vector3.zero);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
            var lightObject = new GameObject("IceCreamIconLight"); SceneManager.MoveGameObjectToScene(lightObject, scene);
            var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.4f;
            light.transform.rotation = Quaternion.Euler(35, -35, 0);
            var target = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(256, 256, TextureFormat.RGBA32, false); var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); image.Apply();
                string icon = "Assets/Resources/UI/InGame/item_IceCream.png"; File.WriteAllBytes(icon, image.EncodeToPNG());
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
            AssetDatabase.SaveAssets(); Debug.Log("ICE_CREAM_MODEL_OK: strawberry waffle cone, collider-free held prefab and rendered inventory icon.");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
    }
}
