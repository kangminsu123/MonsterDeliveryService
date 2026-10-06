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
        var ammo = (int[])Get("weaponAmmo"); var savedAmmo = (int[])ammo.Clone();
        var seconds = (float[])Get("weaponSeconds"); var savedSeconds = (float[])seconds.Clone();
        var player = (CharacterController)Get("player"); var cam = (Camera)Get("cam");
        var position = player.transform.position; var rotation = player.transform.rotation;
        var cameraPosition = cam.transform.position; var cameraRotation = cam.transform.rotation; float fov = cam.fieldOfView;
        float height = player.height; var center = player.center;
        var keyboard = InputSystem.AddDevice<Keyboard>(); var mouse = InputSystem.AddDevice<Mouse>();
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
            Check(inventory.All(item => item == WeaponKind.None) && ammo.All(value => value == 0), "all three slots start empty");
            Check(Get("heldWeapon") == null && GameObject.Find("Attached Delivery Drone") == null, "no default held equipment or drone");
            Check(Mathf.Approximately((float)Get("stamina"), 100), "startup stamina 100");
            Check(!UnityEngine.Object.FindObjectsByType<Transform>().Any(t => t.name.StartsWith("Rival ") || t.name.StartsWith("Capture Pad ") || t.name == "Mystery Parcel"), "exploration stays free of bots, capture pads, and pickups");
            int steps = Mathf.Max(2, Mathf.RoundToInt(.2f / Time.deltaTime));
            Place(); var start = player.transform.position; Input(Key.W); Tick(steps); float walk = player.transform.position.z - start.z;
            Check(walk > .1f && Mathf.Abs(walk / (steps * Time.deltaTime) - 6.5f) < .3f, "WASD walking speed 6.5");
            Place(); Set("stamina", 100f); start = player.transform.position; Input(Key.W, Key.LeftShift); Tick(steps);
            float run = player.transform.position.z - start.z, spent = 100 - (float)Get("stamina");
            Check(run > walk * 1.4f && Mathf.Abs(run / (steps * Time.deltaTime) - 10.5f) < .3f && Mathf.Abs(spent - 28 * steps * Time.deltaTime) < .1f, "Shift sprint 10.5; stamina drains 28/sec");
            Input(); Tick(steps);
            Check(Mathf.Abs((float)Get("stamina") - Mathf.Min(100, 100 - spent + 20 * steps * Time.deltaTime)) < .1f, "stamina regenerates 20/sec while resting");
            Set("stamina", .1f); Input(Key.W, Key.LeftShift); Tick(1); start = player.transform.position; Tick(steps);
            Check((bool)Get("sprintExhausted") && (float)Get("stamina") > 0 && Mathf.Abs((player.transform.position.z - start.z) / (steps * Time.deltaTime) - 6.5f) < .3f, "empty stamina walks and recovers without sprint jitter");
            Input(); Tick(1); Input(Key.W, Key.LeftShift); start = player.transform.position; Tick(1);
            Check(!(bool)Get("sprintExhausted") && player.transform.position.z - start.z > 9 * Time.deltaTime, "release/repress Shift resumes sprint");
            Place(); start = player.transform.position; Input(Key.Space); Tick(1);
            Check(player.transform.position.y > start.y + .02f && (float)Get("vertical") > 0, "Space jumps with gravity instead of flight");
            Input(); InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 3 }); InputSystem.Update();
            Set("nextAttackAt", 0f); Set("rollUntil", 0f); Call("Update");
            Check(inventory.All(item => item == WeaponKind.None) && UnityEngine.Object.FindObjectsByType<BrawlShot>().Length == shotsBefore.Length, "mouse buttons cannot grant or fire equipment while empty");
            Check(cam.fieldOfView >= fov - .1f, "empty hands do not enter rifle aiming FOV");
            Call("Update");
            Check(inventory.All(item => item == WeaponKind.None), "subsequent Update keeps every slot empty");
            Debug.Log("PLAYER_BASICS: " + report.Count + " checks passed; dt=" + Time.deltaTime + "\n" + string.Join("\n", report));
        }
        finally
        {
            InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(keyboard);
            foreach (var shot in UnityEngine.Object.FindObjectsByType<BrawlShot>().Where(s => !shotsBefore.Contains(s))) UnityEngine.Object.DestroyImmediate(shot.gameObject);
            var held = (GameObject)Get("heldWeapon"); if (held != null) UnityEngine.Object.DestroyImmediate(held);
            for (int i = 0; i < fields.Length; i++) fields[i].SetValue(game, values[i]);
            Array.Copy(savedInventory, inventory, inventory.Length); Array.Copy(savedAmmo, ammo, ammo.Length); Array.Copy(savedSeconds, seconds, seconds.Length);
            player.enabled = false; player.transform.SetPositionAndRotation(position, rotation); player.height = height; player.center = center; player.enabled = true;
            cam.transform.SetPositionAndRotation(cameraPosition, cameraRotation); cam.fieldOfView = fov;
            Set("heldWeapon", null); Call("UpdateHeldWeapon");
        }
    }
}
