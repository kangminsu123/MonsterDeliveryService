using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DeliveryHeldItemSetup
{
    const string Folder = "Assets/Art/DeliveryItems";
    [MenuItem("Tools/Gameplay/Held Items/Build Prefabs")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new Exception("Build in Edit Mode.");
        FountainPenSetup.Build();
        SlimeSetup.Build();
        ToyHammerSetup.Build();
        IceCreamSetup.Build();
        Directory.CreateDirectory(Folder + "/Materials"); Directory.CreateDirectory("Assets/Resources/DeliveryItems");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var scene = EditorSceneManager.NewPreviewScene(); GameObject reference = null;
        try
        {
            reference = (GameObject)PrefabUtility.InstantiatePrefab(Resources.Load<GameObject>("Characters/DeliveryRobotPlayer"), scene);
            var robot = reference.GetComponent<DeliveryRobotVisual>();
            var clip = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Characters/DeliveryRobot/DeliveryRobot.fbx").OfType<AnimationClip>().Single(c => c.name == "IDLE_ARMED");
            clip.SampleAnimation(robot.model.gameObject, 0);
            var nodes = robot.model.GetComponentsInChildren<Transform>();
            foreach (WeaponKind kind in Enum.GetValues(typeof(WeaponKind)))
            {
                if (kind == WeaponKind.None || kind == WeaponKind.RunningShoes || kind == WeaponKind.BubbleWrap || kind == WeaponKind.FountainPen || kind == WeaponKind.Slime || kind == WeaponKind.ToyHammer || kind == WeaponKind.IceCream) continue;
                string path = Folder + "/Models/" + kind + ".fbx";
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                importer.globalScale = 1; importer.useFileScale = true; importer.bakeAxisConversion = true;
                importer.importAnimation = false; importer.importCameras = false; importer.importLights = false;
                importer.importNormals = ModelImporterNormals.Import; importer.importTangents = ModelImporterTangents.CalculateMikk;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.SaveAndReimport();
                foreach (var source in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
                {
                    string materialPath = Folder + "/Materials/" + source.name + ".mat";
                    var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if (!material)
                    {
                        material = new Material(Shader.Find("Standard")) { name = source.name, color = source.color };
                        material.SetFloat("_Glossiness", .35f);
                        AssetDatabase.CreateAsset(material, materialPath);
                    }
                    material.SetColor("_EmissionColor", material.color * .8f); material.EnableKeyword("_EMISSION");
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                    MaterialEditor.FixupEmissiveFlag(material);
                    EditorUtility.SetDirty(material);
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), source.name), material);
                }
                importer.SaveAndReimport();
                GameObject root = new GameObject(kind.ToString());
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
                try
                {
                    var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), scene);
                    model.transform.SetParent(root.transform, false); model.name = "Model";
                    foreach (var c in model.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(c);
                    var pose = root.AddComponent<DeliveryHeldItem>();
                    pose.paired = kind == WeaponKind.BoxingGloves || kind == WeaponKind.WorkGloves;
                    // Imported product fronts face -Z; the plunger cup faces down.
                    var facing = kind == WeaponKind.Plunger ? Quaternion.Euler(-90, 0, 0) : Quaternion.Euler(0, 180, 0);
                    pose.firstPersonRotation = pose.paired ? Quaternion.Euler(-38, 0, 0) * facing : facing;
                    // FBX handedness mirrors Blender X: choose the physical right arm by position.
                    var physicalRight = nodes.Where(t => t.name == "Arm.R" || t.name == "Arm.L").OrderByDescending(t => robot.transform.InverseTransformPoint(t.position).x).First();
                    pose.thirdPersonBone = pose.paired ? "Body" : physicalRight.name;
                    var bone = nodes.Single(t => t.name == pose.thirdPersonBone);
                    var world = robot.transform.TransformPoint(new Vector3(pose.thirdPersonBone == "Body" ? 0 : .475f, .836f, .72f));
                    pose.thirdPersonPosition = bone.InverseTransformPoint(world);
                    pose.thirdPersonRotation = Quaternion.Inverse(bone.rotation) * robot.transform.rotation * facing;
                    foreach (string side in new[] { "R", "L" })
                    {
                        var arms = nodes.Where(t => t.name == "Arm.R" || t.name == "Arm.L").OrderBy(t => robot.transform.InverseTransformPoint(t.position).x).ToArray();
                        var arm = side == "R" ? arms[1] : arms[0];
                        float x = Mathf.Sign(robot.transform.InverseTransformPoint(arm.position).x) * .475f;
                        var point = robot.transform.TransformPoint(new Vector3(x, .836f, .72f));
                        var part = pose.paired ? model.GetComponentsInChildren<Transform>().Single(t => t.name == "Hand_" + side) : null;
                        var rotation = robot.transform.rotation * facing * (part ? part.rotation : Quaternion.identity);
                        if (side == "R") { pose.rightPosition = arm.InverseTransformPoint(point); pose.rightRotation = Quaternion.Inverse(arm.rotation) * rotation; }
                        else { pose.leftPosition = arm.InverseTransformPoint(point); pose.leftRotation = Quaternion.Inverse(arm.rotation) * rotation; }
                    }
                    pose.firstPersonPosition = kind switch
                    {
                        WeaponKind.BoxingGloves or WeaponKind.WorkGloves => new Vector3(0, -.43f, .92f),
                        WeaponKind.Plunger => new Vector3(.35f, -.10f, 1.20f),
                        WeaponKind.ToyHammer => new Vector3(.33f, -.30f, 1.20f),
                        WeaponKind.Slime => new Vector3(.36f, -.32f, 1.23f),
                        WeaponKind.IceCream => new Vector3(.36f, -.17f, 1.20f),
                        _ => new Vector3(.36f, -.31f, 1.20f)
                    };
                    pose.firstPersonScale = pose.paired ? 1.08f : 1;
                    PrefabUtility.SaveAsPrefabAsset(root, "Assets/Resources/DeliveryItems/" + kind + ".prefab");
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            foreach (var path in Directory.GetFiles(Folder + "/Materials", "*.mat"))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                mat.SetColor("_EmissionColor", mat.color * .8f); mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                MaterialEditor.FixupEmissiveFlag(mat);
                EditorUtility.SetDirty(mat);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("HELD_PREFABS_OK: 7 models, unit imports, readable materials, armed bone poses and forward first-person poses.");
        }
        finally { if (reference) UnityEngine.Object.DestroyImmediate(reference); EditorSceneManager.ClosePreviewScene(scene); }
    }
}
