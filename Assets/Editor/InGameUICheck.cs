using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine;

public static class InGameUICheck
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static Action restorePreview;
    static Keyboard captureKeyboard;
    [MenuItem("Tools/Gameplay/InGame UI/Verify")]
    public static void Verify()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Run in Play Mode.");
        var game = UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>();
        var type = game.GetType(); object Get(string name) => type.GetField(name, Flags).GetValue(game);
        void Set(string name, object value) => type.GetField(name, Flags).SetValue(game, value);
        var fields = type.GetFields(Flags).Where(f => !f.IsInitOnly).ToArray(); var values = fields.Select(f => f.GetValue(game)).ToArray();
        var owners = (int[])Get("owner"); var oldOwners = (int[])owners.Clone();
        var boxes = (GameObject[])Get("mailboxes");
        bool explore = game.mapExploreMode; float duration = game.roundSeconds;
        var report = new List<string>();
        void Check(bool ok, string message) { if (!ok) throw new Exception("INGAME_UI: " + message); report.Add("PASS " + message); }
        try
        {
            var paths = Directory.GetFiles("Assets/Resources/UI/InGame", "*.png");
            Check(paths.Length == 30, "all 30 UI parts and item icons are separate PNG files");
            Check(paths.All(path => { var importer = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
                return importer && importer.alphaIsTransparency && !importer.mipmapEnabled && importer.textureType == TextureImporterType.Sprite; }),
                "PNG alpha, Sprite import and no mipmaps survive asset import");
            Check(paths.All(path => DeliveryUIArt.Texture(Path.GetFileNameWithoutExtension(path))),
                "every HUD texture resolves through Resources for player builds");
            Check(!game.thirdPersonPreview, "first-person default matches latest concept");
            game.mapExploreMode = true; Check(game.HudClock == "자유 탐색", "exploration does not show a fake countdown");
            game.mapExploreMode = false; game.roundSeconds = 240; Set("startedAt", Time.time - 36);
            Check(game.HudClock == "03:24", "match countdown reads actual round time as minutes and seconds; clock=" + game.HudClock + ", round=" + game.roundSeconds + ", elapsed=" + (Time.time - (float)Get("startedAt")));
            Set("startedAt", Time.time - 999); Check(game.HudClock == "00:00", "expired timer clamps to zero");
            Set("captureTarget", -1); Check(game.HudCaptureProgress == 0, "no target has zero capture progress");
            Set("focusedTarget", boxes[8].GetComponent<InteractionTarget>()); Set("interactionReady", true); owners[8] = -1;
            Check(game.HudInteractionHint.Contains("E") && game.HudInteractionHint.Contains("점령"), "ready mailbox displays the real E capture action");
            Set("captureTarget", 8); Set("captureStarted", Time.time - 1.95f);
            Check(Mathf.Abs(game.HudCaptureProgress - .65f) < .001f, "capture bar reads actual elapsed capture time");
            Set("captureStarted", Time.time - 30); Check(game.HudCaptureProgress == 1, "capture bar clamps at full");
            owners[8] = 0; Check(game.HudInteractionHint == "점령 완료", "owned mailbox shows completion");
            owners[8] = -1; Set("interactionReady", false);
            Check(game.HudInteractionHint.Contains("기다리세요"), "unavailable target keeps readable guidance");
            var parcel = UnityEngine.Object.FindFirstObjectByType<FloatingParcel>();
            Set("focusedTarget", parcel.GetComponent<InteractionTarget>()); Set("interactionReady", true);
            Check(game.HudInteractionHint.Contains("줍기"), "parcel uses its pickup guidance");
            Check(!(bool)Get("itemPlaytestEnabled") && ((WeaponKind[])Get("inventory")).All(x => x == WeaponKind.None),
                "staged item use stays disabled and slots start empty");
            string result = "INGAME_UI_OK: " + report.Count + " checks\n" + string.Join("\n", report);
            File.WriteAllText("art-review/ingame-ui-check-20261008.txt", result); Debug.Log(result);
        }
        finally
        {
            Array.Copy(oldOwners, owners, owners.Length);
            for (int i = 0; i < fields.Length; i++) fields[i].SetValue(game, values[i]);
            game.mapExploreMode = explore; game.roundSeconds = duration;
        }
    }
    [MenuItem("Tools/Gameplay/InGame UI/Preview Capture")]
    public static void PreviewCapture()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Run in Play Mode.");
        RestorePreview();
        var game = UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>();
        var type = game.GetType();
        var fields = type.GetFields(Flags).Where(f => !f.IsInitOnly).ToArray(); var values = fields.Select(f => f.GetValue(game)).ToArray();
        void Set(string name, object value) => type.GetField(name, Flags).SetValue(game, value);
        var owners = (int[])type.GetField("owner", Flags).GetValue(game); var savedOwners = (int[])owners.Clone();
        var boxes = (GameObject[])type.GetField("mailboxes", Flags).GetValue(game);
        var colors = (Color[])type.GetField("colors", Flags).GetValue(game);
        var player = (CharacterController)type.GetField("player", Flags).GetValue(game);
        var camera = (Camera)type.GetField("cam", Flags).GetValue(game);
        var position = player.transform.position; var rotation = player.transform.rotation;
        var cameraPosition = camera.transform.position; var cameraRotation = camera.transform.rotation;
        bool explore = game.mapExploreMode, third = game.thirdPersonPreview, paused = EditorApplication.isPaused; float duration = game.roundSeconds;
        var previewKeyboard = InputSystem.AddDevice<Keyboard>(); captureKeyboard = previewKeyboard;
        InputSystem.QueueStateEvent(previewKeyboard, new KeyboardState(Key.E)); InputSystem.Update();

var effect = type.GetMethod("SetMailboxEffectColor", Flags);
        restorePreview = () => {
            InputSystem.RemoveDevice(previewKeyboard); captureKeyboard = null;

if (!game) return;
            if (type.GetField("focusedTarget", Flags).GetValue(game) is InteractionTarget target) target.SetFocus(false, false);
            Array.Copy(savedOwners, owners, owners.Length);
            for (int i = 0; i < fields.Length; i++) fields[i].SetValue(game, values[i]);
            for (int i = 0; i < boxes.Length; i++) effect.Invoke(game, new object[] { i, owners[i] < 0 ? Color.white : colors[owners[i]] });
            game.mapExploreMode = explore; game.thirdPersonPreview = third; game.roundSeconds = duration;
            player.enabled = false; player.transform.SetPositionAndRotation(position, rotation); player.enabled = true;
            camera.transform.SetPositionAndRotation(cameraPosition, cameraRotation); EditorApplication.isPaused = paused;
        };
        var capture = type.GetMethod("Capture", Flags);
        foreach (int index in new[] { 0, 2, 4 }) capture.Invoke(game, new object[] { index, 0 });
        capture.Invoke(game, new object[] { 1, 1 }); capture.Invoke(game, new object[] { 3, 3 });
        owners[8] = -1; effect.Invoke(game, new object[] { 8, Color.white });
        var point = boxes[8].transform.position + Vector3.up * 2.45f;
        camera.transform.position = point + boxes[8].transform.forward * 2.5f; camera.transform.LookAt(point);
        player.enabled = false; player.transform.SetPositionAndRotation(camera.transform.position - Vector3.up * 1.6f,
            Quaternion.Euler(0, camera.transform.eulerAngles.y, 0)); player.enabled = true;
        game.thirdPersonPreview = false; game.mapExploreMode = false; game.roundSeconds = 240;
        Set("startedAt", Time.time - 36.25f); Set("stamina", 50f); Set("rollUntil", 0f);
        Physics.SyncTransforms(); type.GetMethod("UpdateInteraction", Flags).Invoke(game, null);
        Set("captureTarget", 8); Set("captureStarted", Time.time - 1.95f);
        EditorApplication.isPaused = true;
        Debug.Log("INGAME_UI_PREVIEW: paused actual HUD with temporary 65% capture, 3 local owned mailboxes and stamina 50. Restore after capture.");
    }
    [MenuItem("Tools/Gameplay/InGame UI/Freeze Capture Progress")]
    public static void FreezeCaptureProgress()
    {
        if (!EditorApplication.isPlaying || !EditorApplication.isPaused || restorePreview == null) throw new Exception("Preview Capture and pause first.");
        var game = UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>();
        InputState.Change(captureKeyboard, new KeyboardState(Key.E), InputUpdateType.Dynamic);
        InputState.Change(captureKeyboard, new KeyboardState(Key.E), InputUpdateType.Editor);
        captureKeyboard.MakeCurrent();

typeof(MonsterDeliveryPrototype).GetField("captureTarget", Flags).SetValue(game, 8);
        typeof(MonsterDeliveryPrototype).GetField("captureStarted", Flags).SetValue(game, Time.time - 1.95f);
    }

[MenuItem("Tools/Gameplay/InGame UI/Restore Preview")]
    public static void RestorePreview() { var restore = restorePreview; restorePreview = null; restore?.Invoke(); }
}
