using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// One focused Play Mode check against the real input, movement, and equipment paths.
public static class PlayerBasicsCheck
{
[MenuItem("Tools/Gameplay/Verify Player Basics")]
    public static void Verify()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Run in Play Mode.");
        bool background = Application.runInBackground;
        Application.runInBackground = true; EditorApplication.isPaused = false;
        EditorApplication.update += EditorApplication.QueuePlayerLoopUpdate;
        UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>().StartCoroutine(VerifyOnNextFrame(background));
        EditorApplication.QueuePlayerLoopUpdate();
    }
static System.Collections.IEnumerator VerifyOnNextFrame(bool background)
    {
        yield return null;
        try { yield return VerifyNow(); }
        finally { EditorApplication.update -= EditorApplication.QueuePlayerLoopUpdate; EditorApplication.isPaused = false; Application.runInBackground = background; }
    }
    static System.Collections.IEnumerator VerifyNow()
    {
        if (!EditorApplication.isPlaying || Time.deltaTime <= 0) throw new Exception("Run in unpaused Play Mode.");
        var game = UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = typeof(MonsterDeliveryPrototype);
        object Get(string name) => type.GetField(name, flags).GetValue(game);
        void Set(string name, object value) => type.GetField(name, flags).SetValue(game, value);
        void Call(string name, params object[] args) => type.GetMethod(name, flags).Invoke(game, args);
        var fields = type.GetFields(flags).Where(f => !f.IsInitOnly).ToArray();
        var values = fields.Select(f => f.GetValue(game)).ToArray();
        var inventory = (WeaponKind[])Get("inventory"); var savedInventory = (WeaponKind[])inventory.Clone();
        var seconds = (float[])Get("weaponSeconds"); var savedSeconds = (float[])seconds.Clone();
        var player = (CharacterController)Get("player"); var cam = (Camera)Get("cam");
        var position = player.transform.position; var rotation = player.transform.rotation;
        var cameraPosition = cam.transform.position; var cameraRotation = cam.transform.rotation; float fov = cam.fieldOfView;
        float height = player.height; var center = player.center;
        var keyboard = InputSystem.AddDevice<Keyboard>(); var mouse = InputSystem.AddDevice<Mouse>();
        bool previousEnabled = game.enabled; game.enabled = false;

var shotsBefore = UnityEngine.Object.FindObjectsByType<BrawlShot>();
        var report = new List<string>();
        void Check(bool ok, string detail) { if (!ok) throw new Exception("PLAYER_BASICS: " + detail); report.Add("PASS " + detail); }
        void Input(params Key[] keys) { InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); InputSystem.Update(); }
        void Tick(int count) { for (int i = 0; i < count; i++) Call("UpdatePlayer", keyboard); }
        void Place()
        {
            player.enabled = false; player.transform.SetPositionAndRotation(new Vector3(-10, .81f, 38.84f), Quaternion.identity);
            player.enabled = true; Set("vertical", 0f); Physics.SyncTransforms(); Input(); Tick(20);
        }
        try
        {
            Check(game.mapExploreMode && (bool)Get("started"), "grounded exploration startup (no auto flight)");
            Check(inventory.All(item => item == WeaponKind.None), "all three slots start empty");
            Check(Get("heldWeapon") == null && GameObject.Find("Attached Delivery Drone") == null, "no default held equipment or drone");
            Check(Mathf.Approximately((float)Get("stamina"), 100), "startup stamina 100");
            Check(!UnityEngine.Object.FindObjectsByType<Transform>().Any(t => t.name.StartsWith("Rival ") || t.name.StartsWith("Capture Pad ") || t.name == "Mystery Parcel"), "exploration stays free of bots, capture pads, and pickups");
            int steps = Mathf.Max(2, Mathf.RoundToInt(.2f / Time.deltaTime));
            Place(); var start = player.transform.position; Input(Key.W); Tick(steps); float walk = player.transform.position.z - start.z;
            Check(walk > .1f && Mathf.Abs(walk / (steps * Time.deltaTime) - 6.5f) < .3f, "WASD walking speed 6.5");
            Place(); Set("stamina", 100f); Set("rollUntil", 0f); start = player.transform.position; Input(Key.W, Key.LeftShift); Tick(1);
            Check(Mathf.Abs(player.transform.position.z - start.z - 17 * Time.deltaTime) < .08f && Mathf.Approximately((float)Get("stamina"), 50), "Shift dash moves at 17 and spends exactly 50 once; delta=" + (player.transform.position.z - start.z) + ", dt=" + Time.deltaTime + ", stamina=" + Get("stamina") + ", end=" + Get("rollUntil") + ", now=" + Time.time + ", pressed=" + keyboard.leftShiftKey.wasPressedThisFrame);
            Input(Key.W, Key.LeftShift); yield return null; Input(Key.W, Key.LeftShift); Tick(2);
            Check(Mathf.Approximately((float)Get("stamina"), 50), "holding Shift during dash does not repeatedly spend stamina");
            Set("rollUntil", 0f); player.enabled = false; player.transform.position = new Vector3(-10, .81f, 38.84f); player.enabled = true; Physics.SyncTransforms(); start = player.transform.position; Input(Key.W, Key.LeftShift); Tick(steps);
            Check(Mathf.Abs((player.transform.position.z - start.z) / (steps * Time.deltaTime) - 6.5f) < .3f, "holding Shift after dash returns to walking without sprint or automatic repeat; speed=" + ((player.transform.position.z - start.z) / (steps * Time.deltaTime)) + ", stamina=" + Get("stamina") + ", pressed=" + keyboard.leftShiftKey.wasPressedThisFrame + ", dashEnd=" + Get("rollUntil") + ", now=" + Time.time);
            Check(Mathf.Abs((float)Get("stamina") - Mathf.Min(100, 50 + 20 * steps * Time.deltaTime)) < .1f, "stamina regenerates 20/sec outside a dash");
            Input(); yield return null; Set("stamina", 100f); Input(Key.LeftShift); Tick(1);
            Check(Mathf.Approximately((float)Get("stamina"), 50) && Vector3.Dot((Vector3)Get("rollDirection"), player.transform.forward) > .99f, "Shift without movement dashes forward; stamina=" + Get("stamina") + ", direction=" + Get("rollDirection") + ", pressed=" + keyboard.leftShiftKey.wasPressedThisFrame + ", paused=" + EditorApplication.isPaused);
            Set("rollUntil", 0f); Input(); yield return null; Set("stamina", 49f); Input(Key.W, Key.LeftShift); Tick(1);
            Check((float)Get("rollUntil") == 0 && (float)Get("stamina") >= 49, "less than 50 stamina blocks a dash");
            Input(); yield return null; Set("stamina", 100f); Input(Key.W, Key.LeftCtrl); Tick(1);
            Check((float)Get("rollUntil") == 0 && (float)Get("stamina") == 100, "Ctrl no longer triggers a roll");
            Input(); yield return null; Set("stamina", 100f); Input(Key.W, Key.C, Key.LeftShift); Tick(1);
            Check((float)Get("rollUntil") == 0 && player.height == 1.15f, "crouching keeps its controller height and blocks dash");
            Input(); yield return null; Input(Key.W, Key.LeftShift); Set("stamina", 100f); Tick(1); Call("UpdateInteraction");
            Check(!(bool)Get("interactionReady") && (int)Get("captureTarget") < 0, "dash cancels capture and disables interaction until it ends");
            Set("rollUntil", 0f);
            Check(!game.thirdPersonPreview, "game defaults to first person");
            Place(); start = player.transform.position; Input(Key.Space); Tick(1);
            Check(player.transform.position.y > start.y + .02f && (float)Get("vertical") > 0, "Space jumps with gravity instead of flight");
            Input(); InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 3 }); InputSystem.Update();
            Set("nextAttackAt", 0f); Set("rollUntil", 0f); Call("Update");
            Check(inventory.All(item => item == WeaponKind.None) && UnityEngine.Object.FindObjectsByType<BrawlShot>().Length == shotsBefore.Length, "mouse buttons cannot grant or fire equipment while empty");
            Check(cam.fieldOfView >= fov - .1f, "empty hands do not enter rifle aiming FOV");
            Call("Update");
            Check(inventory.All(item => item == WeaponKind.None), "subsequent Update keeps every slot empty");
            string result = "PLAYER_BASICS: " + report.Count + " checks passed; dt=" + Time.deltaTime + "\n" + string.Join("\n", report);
            System.IO.File.WriteAllText("art-review/dash-firstperson-check-20261008.txt", result); Debug.Log(result);
        }
        finally
        {
            game.enabled = previousEnabled;

InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(keyboard);
            foreach (var shot in UnityEngine.Object.FindObjectsByType<BrawlShot>().Where(s => !shotsBefore.Contains(s))) UnityEngine.Object.DestroyImmediate(shot.gameObject);
            var held = (GameObject)Get("heldWeapon"); if (held != null) UnityEngine.Object.DestroyImmediate(held);
            for (int i = 0; i < fields.Length; i++) fields[i].SetValue(game, values[i]);
            Array.Copy(savedInventory, inventory, inventory.Length); Array.Copy(savedSeconds, seconds, seconds.Length);
            player.enabled = false; player.transform.SetPositionAndRotation(position, rotation); player.height = height; player.center = center; player.enabled = true;
            cam.transform.SetPositionAndRotation(cameraPosition, cameraRotation); cam.fieldOfView = fov;
            Set("heldWeapon", null); Call("UpdateHeldWeapon");
        }
    }
}
