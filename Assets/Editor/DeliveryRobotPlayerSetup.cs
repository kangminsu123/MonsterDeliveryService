using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class DeliveryRobotPlayerSetup
{
    const string PrefabPath = "Assets/Resources/Characters/DeliveryRobotPlayer.prefab";
    const string FbxPath = "Assets/Art/Characters/DeliveryRobot/DeliveryRobot.fbx";
    const string ControllerPath = "Assets/Art/Characters/DeliveryRobot/DeliveryRobot.controller";

    [MenuItem("Tools/Gameplay/Delivery Robot/Apply Blender Animations")]
    public static void ApplyAnimations()
    {
        if (EditorApplication.isPlaying) throw new Exception("Apply animations in Edit Mode.");
        var importer = (ModelImporter)AssetImporter.GetAtPath(FbxPath);
        var takes = importer.defaultClipAnimations;
        Debug.Log("ROBOT_FBX_TAKES: " + string.Join(", ", takes.Select(c => c.takeName + ":" + c.firstFrame + "-" + c.lastFrame)));
        var names = new[] { "IDLE", "WALK", "IDLE_ARMED", "WALK_ARMED" };
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        // Keep the animated Body bone in the pose instead of extracting its hover/lean as root motion.
        importer.motionNodeName = "RIG_RobotSkeleton";
        importer.animationCompression = ModelImporterAnimationCompression.Off;
        importer.importAnimation = true;
        importer.clipAnimations = names.Select(name =>
        {
            var clip = takes.Single(c => c.name == name || c.takeName == name || c.takeName.EndsWith("|" + name));
            clip.name = name;
            clip.loopTime = true;
            clip.loopPose = false;
            clip.lockRootRotation = true;
            clip.lockRootHeightY = true;
            clip.lockRootPositionXZ = true;
            return clip;
        }).ToArray();
        importer.SaveAndReimport();
        var clips = AssetDatabase.LoadAllAssetsAtPath(FbxPath).OfType<AnimationClip>().Where(c => names.Contains(c.name)).ToDictionary(c => c.name);
        if (clips.Count != 4) throw new Exception("All four Blender clips must import.");
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        var machine = controller.layers[0].stateMachine;
        foreach (var transition in machine.anyStateTransitions) machine.RemoveAnyStateTransition(transition);
        foreach (var state in machine.states) machine.RemoveState(state.state);
        controller.parameters = new AnimatorControllerParameter[0];
        controller.AddParameter("Moving", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Holding", AnimatorControllerParameterType.Bool);
        foreach (var name in names)
        {
            var state = machine.AddState(name);
            state.motion = clips[name];
            if (name == "IDLE") machine.defaultState = state;
            var transition = machine.AddAnyStateTransition(state);
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = .12f;
            transition.canTransitionToSelf = false;
            transition.AddCondition(name.StartsWith("WALK") ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, "Moving");
            transition.AddCondition(name.EndsWith("_ARMED") ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, "Holding");
        }
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        CreatePrefab();
        Debug.Log("ROBOT_ANIMATIONS_APPLIED: IDLE/WALK/IDLE_ARMED/WALK_ARMED, 2-second loops, visual-only motion, .12s transitions.");
    }

    [MenuItem("Tools/Gameplay/Delivery Robot/Apply Readable Materials")]
    public static void ApplyReadableMaterials()
    {
        if (EditorApplication.isPlaying) throw new Exception("Apply saved materials in Edit Mode.");
        const string folder = "Assets/Art/Characters/DeliveryRobot/";
        const string fbx = folder + "DeliveryRobot.fbx";
        var importer = (ModelImporter)AssetImporter.GetAtPath(fbx);
        var materials = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Material>().ToArray();
        foreach (var name in new[] { "MAT_RobotBody", "MAT_PlayerColor", "MAT_RobotDark" })
        {
            string path = folder + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(materials.Single(m => m.name == name)) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            if (name == "MAT_RobotBody") material.color = new Color(.78f, .78f, .76f, 1f);
            float strength = name == "MAT_PlayerColor" ? DeliveryRobotVisual.TeamEmission : name == "MAT_RobotBody" ? .85f : .12f;
            material.SetColor("_EmissionColor", material.color * strength);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            MaterialEditor.FixupEmissiveFlag(material);
            EditorUtility.SetDirty(material);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), material);
        }
        AssetDatabase.SaveAssets();
        importer.SaveAndReimport();
        Debug.Log("ROBOT_READABLE_MATERIALS_OK: body/vest/cap receive a soft emission floor, dark mechanical parts keep contrast.");
    }

    [MenuItem("Tools/Gameplay/Delivery Robot/Create Player Prefab")]
    public static void CreatePrefab()
    {
        if (EditorApplication.isPlaying) throw new Exception("Create the prefab in Edit Mode.");
        var scene = EditorSceneManager.NewPreviewScene();
        GameObject root = null;
        try
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Characters/DeliveryRobot/DeliveryRobot.fbx");
            if (!asset) throw new Exception("Import DeliveryRobot.fbx first.");
            root = new GameObject("DeliveryRobotPlayer");
            SceneManager.MoveGameObjectToScene(root, scene);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
            model.transform.SetParent(root.transform, false);
            var animator = model.GetComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (!animator.runtimeAnimatorController) throw new Exception("Apply Blender Animations before creating the player prefab.");
            animator.enabled = true;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var visual = root.AddComponent<DeliveryRobotVisual>();
            visual.model = model.transform;
            float bottom = Points(visual).Min(v => v.y);
            root.transform.localPosition = Vector3.up * (-1 + .25f - bottom);
            Directory.CreateDirectory("Assets/Resources/Characters");
            AssetDatabase.Refresh();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            if (!prefab) throw new Exception("Saving the robot player prefab failed.");
            AssetDatabase.SaveAssets();
            Selection.activeObject = prefab;
            Debug.Log("ROBOT_PREFAB_OK: 25cm clearance above a standing/crouching controller bottom; Resources/Characters/DeliveryRobotPlayer.");
        }
        finally
        {
            if (root) UnityEngine.Object.DestroyImmediate(root);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    [MenuItem("Tools/Gameplay/Delivery Robot/Verify Player")]
    public static void Verify()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Verify in Play Mode.");
        var game = UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>();
        var player = GameObject.Find("Player").GetComponent<CharacterController>();
        var visual = player.GetComponentInChildren<DeliveryRobotVisual>();
        var report = new List<string>();
        void Check(bool ok, string detail)
        {
            if (!ok) throw new Exception("ROBOT_PLAYER: " + detail);
            report.Add("PASS " + detail);
        }
        var palette = (Color[])typeof(MonsterDeliveryPrototype).GetField("colors", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
        var originalColor = palette[0];
        var rootPosition = player.transform.position;
        var modelPosition = visual.model.localPosition;
        var cameraPosition = Camera.main.transform.position;
        try
        {
            Check(visual && visual.model, "real Player owns the robot visual prefab");
            Check(player.GetComponentsInChildren<Collider>().Length == 1 && !player.GetComponent<Renderer>(),
                "only the original movement CharacterController remains; no capsule mesh or model colliders");
            Check(Mathf.Approximately(player.height, 2) && Mathf.Approximately(player.radius, .45f),
                "standing movement capsule keeps height 2m and radius .45m");
            var skins = visual.GetComponentsInChildren<SkinnedMeshRenderer>();
            Check(skins.Length == 22 && skins.All(r => r.sharedMesh && r.bones.All(b => b)),
                "22 imported skinned parts retain their rig");
            Check(skins.SelectMany(r => r.bones).Select(b => b.name).Distinct().OrderBy(n => n)
                .SequenceEqual(new[] { "Arm.L", "Arm.R", "Body" }), "body and two independent arm bones retained");
            var animator = visual.model.GetComponent<Animator>();
            Check(animator.enabled && animator.runtimeAnimatorController && !animator.applyRootMotion,
                "Blender clips animate the visual only, without an extra runtime bob");
            animator.Play("IDLE", 0, .25f); animator.Update(0);
            float topBobBottom = Points(visual).Min(v => v.y);
            animator.Play("IDLE", 0, .75f); animator.Update(0);
            float bottomBobBottom = Points(visual).Min(v => v.y);
            float foot = player.transform.position.y + player.center.y - player.height * .5f;
            Check(Mathf.Abs(bottomBobBottom - foot - .22f) < .005f && Mathf.Abs(topBobBottom - foot - .28f) < .005f,
                "visual hovers 22–28cm above the controller bottom throughout the bob");
            Check(player.transform.position == rootPosition && Camera.main.transform.position == cameraPosition,
                "hover does not move the collision controller or first-person camera");
            animator.Play("IDLE", 0, 0); animator.Update(0);
            var sharedColors = skins.SelectMany(r => r.sharedMaterials).Distinct().ToDictionary(m => m, m => m.color);
            var chosen = new Color(.15f, .8f, .9f);
            typeof(DeliveryRobotVisual).GetField("colorBlock", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(visual, null);
            game.SetPlayerColor(0, chosen);
            var block = new MaterialPropertyBlock();
            int coloredParts = 0;
            foreach (var r in skins)
            for (int i = 0; i < r.sharedMaterials.Length; i++)
            {
                r.GetPropertyBlock(block, i);
                if (r.sharedMaterials[i].name.StartsWith("MAT_PlayerColor"))
                {
                    coloredParts++;
                    Check(block.GetColor("_Color") == chosen, "player color reaches " + r.name);
                    Check(block.GetColor("_EmissionColor") == chosen * DeliveryRobotVisual.TeamEmission,
                        "player color also tints the soft emission on " + r.name);
                }
                else Check(block.isEmpty, "body/dark/glow material unaffected on " + r.name);
            }
            Check(coloredParts > 1 && sharedColors.All(pair => pair.Key.color == pair.Value),
                "vest/cap use per-player color without modifying shared FBX materials");
            Check(sharedColors.Keys.All(m => m.IsKeywordEnabled("_EMISSION") &&
                (m.globalIlluminationFlags & MaterialGlobalIlluminationFlags.EmissiveIsBlack) == 0 &&
                m.GetColor("_EmissionColor").maxColorComponent > 0), "saved character materials retain emission in shadow");
            game.SetPlayerColor(0, originalColor);
            var result = "ROBOT_PLAYER_OK: " + report.Count + " checks\n" + string.Join("\n", report);
            File.WriteAllText("art-review/delivery-robot-player-20261007.txt", result);
            Capture(player);
            Debug.Log(result);
        }
        finally
        {
            game.SetPlayerColor(0, originalColor);
            visual.model.localPosition = modelPosition;
        }
    }

    [MenuItem("Tools/Gameplay/Delivery Robot/Verify Animations")]
    public static void VerifyAnimations()
    {
        if (!EditorApplication.isPlaying || Time.deltaTime <= 0) throw new Exception("Verify in unpaused Play Mode.");
        var game = UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var fields = game.GetType().GetFields(flags).Where(f => !f.IsInitOnly).ToArray();
        var saved = fields.Select(f => f.GetValue(game)).ToArray();
        object Get(string n) => game.GetType().GetField(n, flags).GetValue(game);
        void Set(string n, object v) => game.GetType().GetField(n, flags).SetValue(game, v);
        void Call(string n, params object[] args) => game.GetType().GetMethod(n, flags).Invoke(game, args);
        var player = (CharacterController)Get("player");
        var visual = player.GetComponentInChildren<DeliveryRobotVisual>();
        var animator = visual.model.GetComponent<Animator>();
        var inventory = (WeaponKind[])Get("inventory"); var items = (WeaponKind[])inventory.Clone();
        var ammo = (int[])Get("weaponAmmo"); var savedAmmo = (int[])ammo.Clone();
        var seconds = (float[])Get("weaponSeconds"); var savedSeconds = (float[])seconds.Clone();
        var position = player.transform.position; var rotation = player.transform.rotation;
        var camera = Camera.main; var cp = camera.transform.position; var cr = camera.transform.rotation;
        var modelPosition = visual.model.localPosition;
        var state = animator.GetCurrentAnimatorStateInfo(0);
        var keyboard = InputSystem.AddDevice<Keyboard>();
        var report = new List<string>();
        void Check(bool ok, string detail) { if (!ok) throw new Exception("ROBOT_ANIMATION: " + detail); report.Add("PASS " + detail); }
        void Input(params Key[] keys) { InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); InputSystem.Update(); }
        void Tick() { Call("UpdatePlayer", keyboard); Call("LateUpdate"); animator.Update(0); animator.Update(.2f); }
        bool Is(string name) => animator.GetCurrentAnimatorStateInfo(0).IsName(name) && !animator.IsInTransition(0);
        try
        {
            Check(inventory.All(k => k == WeaponKind.None) && !(bool)Get("itemPlaytestEnabled"), "normal startup stays empty-handed with item rollout disabled");
            var clips = AssetDatabase.LoadAllAssetsAtPath(FbxPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
            Check(clips.Length == 4 && clips.All(c => c.isLooping && Mathf.Abs(c.length - 2) < .001f), "exactly four imported 2-second loop clips");
            Check(animator.enabled && animator.avatar && animator.avatar.isValid && !animator.applyRootMotion, "valid Generic avatar with root motion disabled");
            player.enabled = false; player.transform.SetPositionAndRotation(new Vector3(0, 100, 0), Quaternion.identity); player.enabled = true;
            Set("vertical", 0f); Set("rollUntil", 0f); Set("selectedSlot", 0);
            Input(); Tick(); Check(Is("IDLE"), "stationary/vertical-only movement selects IDLE");
            var body = visual.GetComponentsInChildren<Transform>().Single(t => t.name == "Body");
            var left = visual.GetComponentsInChildren<Transform>().Single(t => t.name == "Arm.L");
            var right = visual.GetComponentsInChildren<Transform>().Single(t => t.name == "Arm.R");
            animator.Play("IDLE", 0, 0); animator.Update(0);
            var idleUp = body.up; var leftRest = left.localRotation; var rightRest = right.localRotation;
            Vector3 ArmCenter(string side)
            {
                var skin = visual.GetComponentsInChildren<SkinnedMeshRenderer>().Single(r => r.name == "HERO_Arm_" + side);
                var mesh = new Mesh();
                try { skin.BakeMesh(mesh); return mesh.vertices.Select(v => skin.transform.TransformPoint(v)).Aggregate(Vector3.zero, (a,b) => a+b) / mesh.vertexCount; }
                finally { UnityEngine.Object.DestroyImmediate(mesh); }
            }
            var rightRestCenter = body.InverseTransformPoint(ArmCenter("R"));
            var leftRestCenter = body.InverseTransformPoint(ArmCenter("L"));
            void CheckGrip(string label)
            {
                var l = body.InverseTransformPoint(ArmCenter("L"));
                var r = body.InverseTransformPoint(ArmCenter("R"));
                Check(Vector3.Dot(body.TransformVector(l-leftRestCenter), player.transform.forward) > .12f &&
                    Vector3.Dot(body.TransformVector(r-rightRestCenter), player.transform.forward) > .12f,
                    label + " moves both actual arm skins forward");
                Check(Mathf.Abs(l.x) < Mathf.Abs(leftRestCenter.x)-.03f && Mathf.Abs(r.x) < Mathf.Abs(rightRestCenter.x)-.03f,
                    label + " brings both arm skins inward toward the center");
                Check(Mathf.Abs(Mathf.Abs(l.x)-Mathf.Abs(r.x)) < .01f,
                    label + " keeps a symmetric two-handed grip");
            }
            var roots = player.transform.position; var modelRoot = visual.model.position; var camRoot = camera.transform.position;
            animator.Play("IDLE", 0, .25f); animator.Update(0); float peak = Points(visual).Min(v => v.y);
            animator.Play("IDLE", 0, .75f); animator.Update(0); float low = Points(visual).Min(v => v.y);
            Check(Mathf.Abs(peak-low-.06f) < .002f, "IDLE moves evaluated skin by 6cm peak-to-peak");
            Check(player.transform.position == roots && visual.model.position == modelRoot && camera.transform.position == camRoot, "hover stays in bones without moving model root/controller/camera");
            Input(Key.W); Tick(); Check(Is("WALK"), "real W input and CharacterController movement select WALK");
            float lean = Mathf.Atan2(Vector3.Dot(body.up, player.transform.forward), Vector3.Dot(body.up, idleUp)) * Mathf.Rad2Deg;
            Check(lean > 4.5f && lean < 5.5f, "WALK body leans forward about 5deg (actual " + lean.ToString("F3") + ")");
            Check(Quaternion.Angle(leftRest, left.localRotation) > 8 && Quaternion.Angle(rightRest, right.localRotation) > 8, "both arms trail relative to the leaning body");
            Input(); Tick(); Check(Is("IDLE"), "releasing movement returns to IDLE");
            Set("itemPlaytestEnabled", true); Call("AddWeapon", WeaponKind.BoxingGloves); Tick();
            Check(Is("IDLE_ARMED"), "existing item acquisition/selection chooses armed idle");
            Check(Quaternion.Angle(rightRest, right.localRotation) > 70 && Quaternion.Angle(leftRest, left.localRotation) > 70, "armed idle raises both arms");
            CheckGrip("armed idle");
            Input(Key.W); Tick(); Check(Is("WALK_ARMED"), "equipped movement selects WALK_ARMED");
            lean = Mathf.Atan2(Vector3.Dot(body.up, player.transform.forward), Vector3.Dot(body.up, idleUp)) * Mathf.Rad2Deg;
            Check(lean > 4.5f && lean < 5.5f, "equipped walk keeps the forward body lean");
            Check(Quaternion.Angle(rightRest, right.localRotation) > 70 && Quaternion.Angle(leftRest, left.localRotation) > 70,
                "equipped walk holds both arms forward");
            CheckGrip("equipped walk");
            Input(Key.W, Key.Digit2); Tick(); Check(Is("WALK"), "empty slot while moving restores empty-handed WALK");
            Input(Key.W, Key.Digit1); Tick(); Check(Is("WALK_ARMED"), "equipped slot while moving restores WALK_ARMED");
            Input(); Tick(); Check(Is("IDLE_ARMED"), "stopping equipped movement returns to armed idle");
            Input(Key.W); Tick(); Input(Key.W, Key.G); Tick();
            Check(Is("WALK") && inventory[0] == WeaponKind.None, "discard while moving lowers both arms and returns to WALK");
            Input(); Tick(); Call("AddWeapon", WeaponKind.BoxingGloves); Tick();
            Input(Key.Digit2); Tick(); Check(Is("IDLE"), "selecting an empty slot lowers the arm");
            Input(Key.Digit1); Tick(); Check(Is("IDLE_ARMED"), "selecting the occupied slot restores armed idle");
            Input(Key.G); Tick(); Check(Is("IDLE") && inventory[0] == WeaponKind.None, "actual G discard returns to empty-handed IDLE");
            Call("AddWeapon", WeaponKind.BoxingGloves); Call("ClearSlot", 0); Input(); Tick();
            Check(Is("IDLE"), "item exhaustion/clear follows the same idle reset");
            foreach (var name in new[] { "IDLE", "WALK", "IDLE_ARMED", "WALK_ARMED" })
            {
                visual.SetMotion(name.StartsWith("WALK"), name.EndsWith("_ARMED"));
                animator.Play(name, 0, 0); animator.Update(0); var first = Points(visual).ToArray();
                animator.Play(name, 0, .9999f); animator.Update(0); var last = Points(visual).ToArray();
                Check(first.Zip(last, Vector3.Distance).Max() < .001f, name + " skin loop boundary matches within 1mm");
                float minClearance = float.MaxValue;
                for (int i=0; i<16; i++)
                {
                    animator.Play(name, 0, i/16f); animator.Update(0);
                    minClearance = Mathf.Min(minClearance, Points(visual).Min(v => v.y) - (player.transform.position.y - 1));
                }
                Check(minClearance > .20f, name + " keeps skin floating above controller feet (min " + minClearance.ToString("F3") + "m)");
            }
            var text = "ROBOT_ANIMATION_OK: " + report.Count + " checks\n" + string.Join("\n", report);
            File.WriteAllText("art-review/delivery-robot-animations-20261007.txt", text);
            Debug.Log(text);
        }
        finally
        {
            InputSystem.RemoveDevice(keyboard);
            for (int i=0;i<fields.Length;i++) fields[i].SetValue(game, saved[i]);
            Array.Copy(items, inventory, items.Length); Array.Copy(savedAmmo, ammo, ammo.Length); Array.Copy(savedSeconds, seconds, seconds.Length);
            player.enabled = false; player.transform.SetPositionAndRotation(position, rotation); player.enabled = true;
            camera.transform.SetPositionAndRotation(cp, cr); visual.model.localPosition = modelPosition;
            Call("LateUpdate"); animator.Play(state.fullPathHash, 0, state.normalizedTime); animator.Update(0);
        }
    }

    [MenuItem("Tools/Gameplay/Delivery Robot/Capture Animation Review")]
    public static void CaptureAnimationReview()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Capture in Play Mode.");
        var player = GameObject.Find("Player").GetComponent<CharacterController>();
        var visual = player.GetComponentInChildren<DeliveryRobotVisual>();
        var animator = visual.model.GetComponent<Animator>();
        var state = animator.GetCurrentAnimatorStateInfo(0);
        bool moving = animator.GetBool("Moving"), holding = animator.GetBool("Holding");
        try
        {
            foreach (var name in new[] { "IDLE", "WALK", "IDLE_ARMED", "WALK_ARMED" })
            {
                visual.SetMotion(name.StartsWith("WALK"), name.EndsWith("_ARMED"));
                animator.Play(name, 0, .25f); animator.Update(0); animator.Update(.15f);
                if (!animator.GetCurrentAnimatorStateInfo(0).IsName(name) || animator.IsInTransition(0))
                    throw new Exception("Capture must wait for the requested pose: " + name);
                Debug.Log("ROBOT_REVIEW_POSE: " + name + " right arm " + visual.GetComponentsInChildren<Transform>().Single(t => t.name == "Arm.R").localEulerAngles);
                Capture(player, "art-review/delivery-robot-animation-" + name + "-20261007.png");
            }
            Debug.Log("ROBOT_ANIMATION_REVIEW_OK: rendered all four poses on the real map player; live state restored.");
        }
        finally
        {
            visual.SetMotion(moving, holding);
            animator.Play(state.fullPathHash, 0, state.normalizedTime); animator.Update(0);
        }
    }

    [MenuItem("Tools/Gameplay/Delivery Robot/Verify Third Person")]
    public static void VerifyThirdPerson()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Verify in Play Mode.");
        var game = UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = game.GetType();
        object Get(string name) => type.GetField(name, flags).GetValue(game);
        void Set(string name, object value) => type.GetField(name, flags).SetValue(game, value);
        void Call(string name) => type.GetMethod(name, flags).Invoke(game, null);
        var player = GameObject.Find("Player").GetComponent<CharacterController>();
        var camera = Camera.main;
        var pp = player.transform.position; var pr = player.transform.rotation;
        var cp = camera.transform.position; var cr = camera.transform.rotation;
        var pitch = Get("lookPitch"); var focused = (InteractionTarget)Get("focusedTarget"); var ready = Get("interactionReady");
        bool thirdPerson = game.thirdPersonPreview;
        GameObject target = null, wall = null;
        var report = new List<string>();
        void Check(bool ok, string detail)
        {
            if (!ok) throw new Exception("THIRD_PERSON: " + detail);
            report.Add("PASS " + detail);
        }
        try
        {
            game.thirdPersonPreview = true;
            player.enabled = false; player.transform.SetPositionAndRotation(new Vector3(0, 150, 0), Quaternion.identity); player.enabled = true;
            Set("lookPitch", 12f); camera.transform.rotation = Quaternion.Euler(12, 0, 0); Call("PositionCamera");
            var pivot = player.transform.position + Vector3.up * .9f;
            var desired = pivot + camera.transform.right * .85f - camera.transform.forward * 4;
            Check(Vector3.Distance(camera.transform.position, desired) < .001f, "camera sits 4m behind and .85m right in clear space");
            var visual = player.GetComponentInChildren<DeliveryRobotVisual>();
            var screen = Points(visual).Select(camera.WorldToViewportPoint).ToArray();
            Check(screen.All(v => v.z > camera.nearClipPlane && v.x > 0 && v.x < 1 && v.y > 0 && v.y < 1),
                "entire hovering robot is visible inside the real perspective camera");
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = pivot + (desired - pivot).normalized * 2;
            wall.transform.localScale = new Vector3(4, 4, .2f);
            Physics.SyncTransforms(); Call("PositionCamera");
            Check(Vector3.Distance(camera.transform.position, pivot) < 2 &&
                !wall.GetComponent<Collider>().bounds.Contains(camera.transform.position), "solid wall pulls camera forward without entering the wall");
            wall.GetComponent<Collider>().isTrigger = true; Physics.SyncTransforms(); Call("PositionCamera");
            Check(Vector3.Distance(camera.transform.position, desired) < .001f, "triggers do not shorten camera distance");
            UnityEngine.Object.DestroyImmediate(wall); wall = null;
            Set("lookPitch", 0f); camera.transform.rotation = Quaternion.identity; Call("PositionCamera");
            target = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/DeliveryProps/parcel.prefab"));
            var interaction = target.GetComponent<InteractionTarget>();
            var collider = target.GetComponentInChildren<BoxCollider>();
            target.transform.position += pivot + Vector3.right * .85f + Vector3.forward * 1.5f - collider.bounds.center;
            Physics.SyncTransforms(); Call("UpdateInteraction");
            Check(Get("focusedTarget") as InteractionTarget == interaction && (bool)Get("interactionReady") && interaction.IsHighlighted,
                "crosshair selects a nearby target and shows a white outline although camera is over 5m away");
            target.transform.position += Vector3.forward * 3; Physics.SyncTransforms(); Call("UpdateInteraction");
            Check(!(bool)Get("interactionReady") && !interaction.IsHighlighted, "target beyond player 3m range has no ready outline");
            target.transform.position -= Vector3.forward * 3;
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = player.transform.position + new Vector3(.4f, .65f, .6f);
            wall.transform.localScale = new Vector3(.1f, 1, .2f);
            Physics.SyncTransforms(); Call("UpdateInteraction");
            Check(!(bool)Get("interactionReady"), "player-side wall prevents interaction around a corner visible to the shoulder camera");
            UnityEngine.Object.DestroyImmediate(wall); wall = null;
            var rayHit = type.GetMethod("InteractionRayHit", flags);
            object[] args = { new Ray(player.transform.position - Vector3.forward * 2, Vector3.forward), 4f, default(RaycastHit) };
            Check(!(bool)rayHit.Invoke(game, args), "player's own collider does not obstruct the crosshair ray");
            Call("UpdateInteraction"); Call("TryPickup");
            Check(!target.activeSelf && !interaction.available, "third-person ready target uses the existing parcel pickup dispatch");
            game.thirdPersonPreview = false; Call("PositionCamera");
            Check(Vector3.Distance(camera.transform.position, player.transform.position + Vector3.up * 1.6f) < .001f,
                "disabling preview restores the existing first-person camera position");
            File.WriteAllLines("art-review/third-person-preview-20261007.txt", report);
            Debug.Log("THIRD_PERSON_OK: " + report.Count + " checks passed.");
        }
        finally
        {
            if (Get("focusedTarget") is InteractionTarget current) current.SetFocus(false, false);
            if (target) UnityEngine.Object.DestroyImmediate(target);
            if (wall) UnityEngine.Object.DestroyImmediate(wall);
            game.thirdPersonPreview = thirdPerson;
            Set("lookPitch", pitch); Set("focusedTarget", focused); Set("interactionReady", ready);
            player.enabled = false; player.transform.SetPositionAndRotation(pp, pr); player.enabled = true;
            camera.transform.SetPositionAndRotation(cp, cr);
            if (focused) focused.SetFocus(true, (bool)ready);
        }
    }

    [MenuItem("Tools/Gameplay/Delivery Robot/Capture Third Person")]
    public static void CaptureThirdPerson()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Capture in Play Mode.");
        var camera = Camera.main;
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        float aspect = camera.aspect;
        var target = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
        Texture2D image = null;
        try
        {
            camera.aspect = 16f / 9;
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
            File.WriteAllBytes("art-review/third-person-camera-20261007.png", image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget; camera.aspect = aspect; RenderTexture.active = previousActive;
            if (image) UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    static IEnumerable<Vector3> Points(DeliveryRobotVisual visual)
    {
        var points = new List<Vector3>();
        foreach (var renderer in visual.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            var mesh = new Mesh();
            try
            {
                renderer.BakeMesh(mesh);
                points.AddRange(mesh.vertices.Select(v => renderer.transform.TransformPoint(v)));
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }
        return points;
    }

    static void Capture(CharacterController player, string path = "art-review/delivery-robot-player-20261007.png")
    {
        // Review multiple poses in one frame without reusing cached skin matrices.
        var skins = player.GetComponentsInChildren<SkinnedMeshRenderer>();
        var oldRecalculate = skins.Select(r => r.forceMatrixRecalculationPerRender).ToArray();
        foreach (var skin in skins) skin.forceMatrixRecalculationPerRender = true;
        var cameraObject = new GameObject("Robot Player Review") { hideFlags = HideFlags.HideAndDontSave };
        var target = new RenderTexture(1200, 1200, 24) { antiAliasing = 4 };
        var oldActive = RenderTexture.active;
        Texture2D image = null;
        try
        {
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = player.transform.position + new Vector3(3, 1.6f, 4);
            camera.transform.LookAt(player.transform.position + Vector3.up * .05f);
            camera.orthographic = true; camera.orthographicSize = 1.5f;
            camera.nearClipPlane = .1f; camera.farClipPlane = 500;
            camera.targetTexture = target; camera.Render();
            RenderTexture.active = target;
            image = new Texture2D(1200, 1200, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1200, 1200), 0, 0); image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally
        {
            for (int i=0; i<skins.Length; i++) skins[i].forceMatrixRecalculationPerRender = oldRecalculate[i];
            RenderTexture.active = oldActive;
            if (image) UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
