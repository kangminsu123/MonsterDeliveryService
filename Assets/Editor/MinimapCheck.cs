using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class MinimapCheck
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static Action restoreReview;

    [MenuItem("Tools/Gameplay/Minimap/Verify")]
    public static void Verify()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Run in Play Mode.");
        var game = UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>();
        var map = game.GetComponent<DeliveryMinimap>();
        var type = game.GetType();
        object Get(string name) => type.GetField(name, Flags).GetValue(game);
        void Set(string name, object value) => type.GetField(name, Flags).SetValue(game, value);
        void Call(string name, params object[] args) => type.GetMethod(name, Flags).Invoke(game, args);
        var owners = (int[])Get("owner"); var savedOwners = (int[])owners.Clone();
        var palette = (Color[])Get("colors"); var savedPalette = (Color[])palette.Clone();
        var boxes = (GameObject[])Get("mailboxes");
        var bodyColors = boxes.Select(x => x.transform.Find("Model").GetComponentInChildren<Renderer>().sharedMaterial.color).ToArray();
        var fields = type.GetFields(Flags).Where(f => !f.IsInitOnly).ToArray(); var values = fields.Select(f => f.GetValue(game)).ToArray();
        var player = (CharacterController)Get("player"); var position = player.transform.position; var rotation = player.transform.rotation;
        var camera = (Camera)Get("cam"); var cameraPosition = camera.transform.position; var cameraRotation = camera.transform.rotation;
        var keyboard = InputSystem.AddDevice<Keyboard>();
        var inventory = (WeaponKind[])Get("inventory"); var savedInventory = (WeaponKind[])inventory.Clone();
        var seconds = (float[])Get("weaponSeconds"); var savedSeconds = (float[])seconds.Clone();
        var originalFocus = (InteractionTarget)Get("focusedTarget");
        bool thirdPerson = game.thirdPersonPreview; var report = new List<string>();
        void Check(bool condition, string message) { if (!condition) throw new Exception("MINIMAP: " + message); report.Add("PASS " + message); }
        try
        {
            Check(map && map.MapTexture && map.MapTexture.width == 512, "actual player has a baked 512px minimap");
            Check(map.MapTexture.GetPixels().Select(c => c.r + c.g + c.b).Max() > .3f, "map contains rendered neighborhood geometry");
            Check(boxes.Length == 10 && boxes.All(x => x), "all ten authored mailbox markers are present");

            Check(Resources.Load<Shader>("MinimapFlat")?.isSupported == true, "unlit map shader is available in Resources for builds");
            var extentRenderers = new[] { "Layer_00_GrassBase", "Layer_07_ReferenceLandscape", "Layer_03_IslandTerrain/CoastalCliffs" }
                .SelectMany(name => GameObject.Find(name).GetComponentsInChildren<Renderer>()).ToArray();
            Check(extentRenderers.All(r => r.bounds.min.x > map.MapBounds.min.x && r.bounds.max.x < map.MapBounds.max.x
                && r.bounds.min.z > map.MapBounds.min.z && r.bounds.max.z < map.MapBounds.max.z),
                "entire island, coastal cliffs and outer woodland fit inside the map");
            var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            var lightStates = lights.Select(l => l.enabled).ToArray();
            bool savedFog = RenderSettings.fog; float savedDensity = RenderSettings.fogDensity;
            var alternateObject = new GameObject("Minimap Lighting Check");
            try
            {
                foreach (var light in lights) light.enabled = false;
                RenderSettings.fog = true; RenderSettings.fogDensity = .5f;
                var alternateMap = alternateObject.AddComponent<DeliveryMinimap>(); alternateMap.enabled = false;
                alternateMap.Initialize(game, player.transform, boxes);
                Check(map.MapTexture.GetPixels32().SequenceEqual(alternateMap.MapTexture.GetPixels32()),
                    "map pixels stay identical with all lights off and dense fog enabled");
            }
            finally
            {
                for (int i = 0; i < lights.Length; i++) if (lights[i]) lights[i].enabled = lightStates[i];
                RenderSettings.fog = savedFog; RenderSettings.fogDensity = savedDensity;
                UnityEngine.Object.DestroyImmediate(alternateObject);
            }
Check(map.MapBounds.size.x == map.MapBounds.size.z && map.MapBounds.size.x > 20, "map is square with valid actual world bounds");
            Check(boxes.All(x => { var p = map.MapPosition(x.transform.position); return p.x > 0 && p.x < 1 && p.y > 0 && p.y < 1; }), "all mailbox positions fit inside map padding");
            Check(map.MapPosition(map.MapBounds.center + Vector3.forward).y < .5f && map.MapPosition(map.MapBounds.center + Vector3.right).x > .5f, "north is up and east is right");
            Check(map.MapPosition(map.MapBounds.center + Vector3.one * 10000) == new Vector2(1, 0), "off-map positions clamp to edge");
            Set("started", true); Set("finished", false);
            Check(game.ShowMinimap, "map is shown during actual exploration");
            Set("finished", true); Check(!game.ShowMinimap, "map hides behind results"); Set("finished", false);
            Set("started", false); Check(!game.ShowMinimap, "map hides before a match starts"); Set("started", true);
            game.mapEditMode = true; Check(!game.ShowMinimap, "map hides in map editing"); game.mapEditMode = false;
            for (int i = 0; i < owners.Length; i++) { owners[i] = -1; Call("SetMailboxEffectColor", i, Color.white); }
            Check(Enumerable.Range(0, 10).All(i => game.GetMailboxMapColor(i) == Color.white), "unclaimed markers are white");
            game.thirdPersonPreview = false; game.SetPlayerColor(0, Color.yellow);
            var point = boxes[8].transform.position + Vector3.up * 2.45f;
            camera.transform.position = point + boxes[8].transform.forward * 2.5f; camera.transform.LookAt(point);
            Set("playerItems", new DeliveryItemState()); Physics.SyncTransforms(); Call("UpdateInteraction");
            Check((bool)Get("interactionReady"), "actual mailbox is interactable before E capture");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E)); InputSystem.Update();
            Set("captureTarget", 8); Set("captureStarted", Time.time - 3.1f); Call("UpdateCapture", keyboard);
            Check(owners[8] == 0 && game.GetMailboxMapColor(8) == Color.yellow, "actual E capture makes its map marker local-player yellow");
            Check(Enumerable.Range(0, 10).Where(i => i != 8).All(i => game.GetMailboxMapColor(i) == Color.white), "capture leaves unrelated markers neutral");
            Call("Capture", 8, 1); Check(game.GetMailboxMapColor(8) == palette[1], "rival recapture immediately replaces the marker color");
            var cyan = new Color(.15f, .8f, .9f); game.SetPlayerColor(1, cyan);
            Check(game.GetMailboxMapColor(8) == cyan, "changing an owner's chosen color updates owned markers");
            Call("Capture", 8, 0); Call("Capture", 2, 0); game.SetPlayerColor(0, new Color(.75f, .3f, 1));
            Check(game.GetMailboxMapColor(8) == palette[0] && game.GetMailboxMapColor(2) == palette[0] && game.LocalPlayerColor == palette[0],
                "all local owned markers, player arrow and legend share the selected player color");
            Check(Enumerable.Range(0, 10).All(i => boxes[i].transform.Find("Model").GetComponentInChildren<Renderer>().sharedMaterial.color == bodyColors[i]),
                "marker changes leave mailbox body materials unchanged");
            Check(!(bool)Get("itemPlaytestEnabled") && inventory.All(x => x == WeaponKind.None), "minimap keeps staged items disabled and default hands empty");
            Call("Begin");
            Check(Enumerable.Range(0, 10).All(i => game.GetMailboxMapColor(i) == Color.white), "round restart resets all map markers to neutral");
            Check(UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Where(c => c.name == "Minimap Bake Camera").All(c => !c.enabled && !c.targetTexture), "background bake leaves no extra running camera or render target");
            string result = "MINIMAP_OK: " + report.Count + " checks\n" + string.Join("\n", report);
            Directory.CreateDirectory("art-review"); File.WriteAllText("art-review/minimap-flat-check-20261008.txt", result);
            Debug.Log(result);
        }
        finally
        {
            InputSystem.RemoveDevice(keyboard);
            if (Get("focusedTarget") is InteractionTarget current) current.SetFocus(false, false);
            Array.Copy(savedOwners, owners, owners.Length); Array.Copy(savedPalette, palette, palette.Length);
            Array.Copy(savedInventory, inventory, inventory.Length); Array.Copy(savedSeconds, seconds, seconds.Length);
            for (int i = 0; i < fields.Length; i++) fields[i].SetValue(game, values[i]);
            game.thirdPersonPreview = thirdPerson;
            for (int i = 0; i < boxes.Length; i++) Call("SetMailboxEffectColor", i, owners[i] < 0 ? Color.white : palette[owners[i]]);
            game.SetPlayerColor(0, palette[0]);
            player.enabled = false; player.transform.SetPositionAndRotation(position, rotation); player.enabled = true;
            camera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
            if (originalFocus) originalFocus.SetFocus(true, (bool)Get("interactionReady"));
        }
    }

    [MenuItem("Tools/Gameplay/Minimap/Preview Ownership")]
    public static void PreviewOwnership()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Run in Play Mode.");
        RestorePreview();
        var game = UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>();
        var type = game.GetType();
        var owners = (int[])type.GetField("owner", Flags).GetValue(game); var saved = (int[])owners.Clone();
        var colors = (Color[])type.GetField("colors", Flags).GetValue(game);
        var capture = type.GetMethod("Capture", Flags); var effect = type.GetMethod("SetMailboxEffectColor", Flags);
        var feedback = type.GetField("feedback", Flags); var oldFeedback = feedback.GetValue(game);
        restoreReview = () => {
            if (!game) return;
            Array.Copy(saved, owners, owners.Length); feedback.SetValue(game, oldFeedback);
            for (int i = 0; i < owners.Length; i++) effect.Invoke(game, new object[] { i, owners[i] < 0 ? Color.white : colors[owners[i]] });
        };
        capture.Invoke(game, new object[] { 0, 0 }); capture.Invoke(game, new object[] { 4, 0 }); capture.Invoke(game, new object[] { 8, 0 });
        capture.Invoke(game, new object[] { 1, 1 }); capture.Invoke(game, new object[] { 3, 3 });
        Debug.Log("MINIMAP_PREVIEW_READY: temporary local/rival ownership; restore after screenshot.");
    }

    [MenuItem("Tools/Gameplay/Minimap/Restore Preview")]
    public static void RestorePreview() { var restore = restoreReview; restoreReview = null; restore?.Invoke(); }


}
