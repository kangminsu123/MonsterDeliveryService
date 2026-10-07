using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class FloatingParcelCheck
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    [MenuItem("Tools/Gameplay/Test Parcels/Verify Floating Interaction")]
    public static void Verify()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Run in Play Mode.");
        var game = UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>(); var type = game.GetType();
        object Get(string n) => type.GetField(n, Flags).GetValue(game);
        void Set(string n, object v) => type.GetField(n, Flags).SetValue(game, v);
        void Call(string n) => type.GetMethod(n, Flags).Invoke(game, null);
        var camera = Camera.main; var cp = camera.transform.position; var cr = camera.transform.rotation;
        var player = (CharacterController)Get("player"); var pp = player.transform.position; var pr = player.transform.rotation;
        var fields = type.GetFields(Flags).Where(f => !f.IsInitOnly).ToArray(); var values = fields.Select(f => f.GetValue(game)).ToArray();
        var inventory = (WeaponKind[])Get("inventory"); var savedInventory = (WeaponKind[])inventory.Clone();
        var ammo = (int[])Get("weaponAmmo"); var savedAmmo = (int[])ammo.Clone();
        var seconds = (float[])Get("weaponSeconds"); var savedSeconds = (float[])seconds.Clone();
        var parcels = GameObject.Find("Layer_12_TestParcelDrops").GetComponentsInChildren<FloatingParcel>();
        var localPositions = parcels.Select(p => p.floating.localPosition).ToArray();
        var localRotations = parcels.Select(p => p.floating.localRotation).ToArray();
        var available = parcels.Select(p => p.GetComponent<InteractionTarget>().available).ToArray();
        var report = new List<string>(); GameObject wall = null; Keyboard keyboard = null; var oldKeyboard = Keyboard.current;
        void Check(bool ok, string name) { if (!ok) throw new Exception("FLOATING_PARCEL_CHECK_FAILED: " + name); report.Add("PASS " + name); }
        void Look(FloatingParcel p, float distance)
        {
            var center = p.floating.position;
            camera.transform.position = center + Vector3.back * distance;
            camera.transform.LookAt(center); Physics.SyncTransforms(); Call("UpdateInteraction");
        }
        try
        {
            game.thirdPersonPreview = false; // Preserve the existing first-person input regression check.
            Set("playerItems", new DeliveryItemState()); Set("started", true); Set("finished", false);
            Set("mapEditMode", false); Set("mapExploreMode", true); Set("itemPlaytestEnabled", false);
            Array.Clear(inventory, 0, inventory.Length); Array.Clear(ammo, 0, ammo.Length); Array.Clear(seconds, 0, seconds.Length);
            Check(parcels.Length == 10, "10 active floating parcels");
            foreach (var p in parcels)
            {
                var target = p.GetComponent<InteractionTarget>();
                Look(p, 1.1f);
                Check((InteractionTarget)Get("focusedTarget") == target && (bool)Get("interactionReady") && target.IsHighlighted,
                    p.name + " ready white outline while item rollout disabled");
            }
            var box = parcels.First(p => p.name == "TestParcel_04"); var interaction = box.GetComponent<InteractionTarget>();
            Check(interaction.actionHint == "E · 줍기", "pickup hint");
            box.Animate(0); float a = box.floating.localPosition.y;
            box.Animate(.8f); float b = box.floating.localPosition.y;
            Check(Mathf.Abs(a - b) > .005f && a >= .98f && a <= 1.22f && b >= .98f && b <= 1.22f,
                "absolute bounded bob, no accumulated vertical drift");
            Check(box.floating.Find("Bubble").localScale.x == 1.4f && box.floating.Find("Model").localScale.x == 1.5f,
                "bubble and enlarged box");
            Look(box, 5); Check(!(bool)Get("interactionReady") && !interaction.IsHighlighted, "out of range has no outline");
            Look(box, 1.8f); interaction.available = false; Call("UpdateInteraction");
            Check(!(bool)Get("interactionReady") && !interaction.IsHighlighted, "unavailable has no outline"); interaction.available = true;
            var state = (DeliveryItemState)Get("playerItems"); state.stunUntil = Time.time + 1; Call("UpdateInteraction");
            Check(!(bool)Get("interactionReady") && !interaction.IsHighlighted, "stun blocks pickup"); state.stunUntil = 0;
            Look(box, 1.8f);
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name = "Parcel_Check_Occluder";
            wall.transform.position = box.floating.position + Vector3.back * .9f; wall.transform.localScale = new Vector3(.8f, .8f, .1f);
            Physics.SyncTransforms(); Call("UpdateInteraction");
            Check(!(bool)Get("interactionReady") && !interaction.IsHighlighted, "solid occlusion blocks pickup");
            wall.GetComponent<Collider>().isTrigger = true; Physics.SyncTransforms(); Call("UpdateInteraction");
            Check((bool)Get("interactionReady") && interaction.IsHighlighted, "bubble/trigger does not block box selection");
            UnityEngine.Object.DestroyImmediate(wall); wall = null;
            camera.transform.rotation = Quaternion.LookRotation(Vector3.up); Call("UpdateInteraction");
            Check(!interaction.IsHighlighted, "look away clears outline");
            PreviewBox(box, game);
            keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E)); InputSystem.Update();
            Call("Update");
            Check(!box.gameObject.activeSelf && !interaction.available && !interaction.IsHighlighted, "E collects box and bubble together");
            Check(!box.TryCollect(), "collected box cannot be collected twice");
            Check(inventory.Count(w => w != WeaponKind.None) == 1 && inventory.All(w => (int)w >= 0 && (int)w <= 10) && !(bool)Get("itemPlaytestEnabled"),
                "pickup grants one random household item without enabling staged attacks");
            File.WriteAllLines("art-review/floating-parcel-interaction-20261006.txt", report);
            Debug.Log("FLOATING_PARCEL_INTERACTION_OK: " + report.Count + " checks passed, including actual E input and white outlines; state restored.");
        }
        finally
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard); oldKeyboard?.MakeCurrent();
            if (wall) UnityEngine.Object.DestroyImmediate(wall);
            if ((InteractionTarget)Get("focusedTarget")) ((InteractionTarget)Get("focusedTarget")).SetFocus(false, false);
            for (int i = 0; i < parcels.Length; i++)
            {
                parcels[i].gameObject.SetActive(true); parcels[i].GetComponent<InteractionTarget>().available = available[i];
                parcels[i].GetComponent<InteractionTarget>().SetFocus(false, false);
                parcels[i].floating.localPosition = localPositions[i]; parcels[i].floating.localRotation = localRotations[i];
            }
            for (int i = 0; i < fields.Length; i++) fields[i].SetValue(game, values[i]);
            Array.Copy(savedInventory, inventory, inventory.Length); Array.Copy(savedAmmo, ammo, ammo.Length); Array.Copy(savedSeconds, seconds, seconds.Length);
            player.enabled = false; player.transform.SetPositionAndRotation(pp, pr); player.enabled = true;
            camera.transform.SetPositionAndRotation(cp, cr); Physics.SyncTransforms();
            var original = (InteractionTarget)Get("focusedTarget"); if (original) original.SetFocus(true, (bool)Get("interactionReady"));
        }
    }

    [MenuItem("Tools/Gameplay/Test Parcels/Preview Pickup")]
    public static void Preview()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Run in Play Mode.");
        var box = GameObject.Find("Layer_12_TestParcelDrops/TestParcel_04").GetComponent<FloatingParcel>();
        PreviewBox(box, UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>());
    }

    static void PreviewBox(FloatingParcel box, MonsterDeliveryPrototype game)
    {
        var type = game.GetType(); var player = (CharacterController)type.GetField("player", Flags).GetValue(game);
        player.enabled = false; player.transform.position = box.transform.position + Vector3.back * 1.8f + Vector3.up;
        player.transform.rotation = Quaternion.identity; player.enabled = true;
        var cam = Camera.main; cam.transform.position = player.transform.position + Vector3.up * 1.6f;
        cam.transform.LookAt(box.floating.position);
        type.GetField("lookPitch", Flags).SetValue(game, Mathf.DeltaAngle(0, cam.transform.eulerAngles.x));
        Physics.SyncTransforms(); type.GetMethod("UpdateInteraction", Flags).Invoke(game, null);
    }
}
