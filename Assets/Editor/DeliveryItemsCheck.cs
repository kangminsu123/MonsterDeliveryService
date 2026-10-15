using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// One focused check: real item use, physics, status rules, capture, and rollout isolation.
public static class DeliveryItemsCheck
{
    [MenuItem("Tools/Gameplay/Verify Delivery Items")]
    public static void Verify()
    {
        if (!EditorApplication.isPlaying || Time.deltaTime <= 0) throw new Exception("Run in unpaused SampleScene Play Mode.");
        var game = UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = typeof(MonsterDeliveryPrototype);
        object Get(string name) => type.GetField(name, flags).GetValue(game);
        void Set(string name, object value) => type.GetField(name, flags).SetValue(game, value);
        object Call(string name, params object[] args) => type.GetMethod(name, flags).Invoke(game, args);
        var fields = type.GetFields(flags).Where(f => !f.IsInitOnly).ToArray();
        var values = fields.Select(f => f.GetValue(game)).ToArray();
        var arrays = type.GetFields(flags).Where(f => f.FieldType.IsArray).Select(f => (array: (Array)f.GetValue(game), copy: (Array)((Array)f.GetValue(game)).Clone())).ToArray();
        bool enabled = game.enabled, explore = game.mapExploreMode;
        var player = (CharacterController)Get("player"); var camera = (Camera)Get("cam");
        var position = player.transform.position; var rotation = player.transform.rotation;
        var cameraPosition = camera.transform.position; var cameraRotation = camera.transform.rotation;
        var inventory = (WeaponKind[])Get("inventory"); var ammo = (int[])Get("weaponAmmo"); var seconds = (float[])Get("weaponSeconds");
        var states = (DeliveryItemState[])Get("botItems"); var controllers = (CharacterController[])Get("botControllers");
        var captures = (float[])Get("botCaptureStarted"); var parcels = (GameObject[])Get("pickups");
        var mailboxes = (GameObject[])Get("mailboxes"); var mailboxRenderers = (Renderer[])Get("mailboxRenderers");
        var owner = (int[])Get("owner"); var areas = (IList)Get("itemAreas");
        var existingShots = UnityEngine.Object.FindObjectsByType<BrawlShot>();
        var temporary = new List<GameObject>(); var report = new List<string>();
        var keyboard = InputSystem.AddDevice<Keyboard>();
        void Check(bool ok, string detail) { if (!ok) throw new Exception("DELIVERY_ITEMS: " + detail); report.Add("PASS " + detail); }
        void Empty() { for (int i = 0; i < inventory.Length; i++) Call("ClearSlot", i); Set("nextAttackAt", 0f); Set("playerItems", new DeliveryItemState { safePosition = position }); areas.Clear(); }
        void Equip(WeaponKind kind) { Empty(); Call("AddWeapon", kind); }
        void Place(CharacterController actor, Vector3 point) { actor.enabled = false; actor.transform.position = point; actor.enabled = true; Physics.SyncTransforms(); }
        try
        {
            game.enabled = false;
            Check(!(bool)Get("itemPlaytestEnabled") && inventory.All(i => i == WeaponKind.None) && Get("heldWeapon") == null, "normal startup has no default item, model, or enabled rollout");
            Call("AddWeapon", WeaponKind.BoxingGloves); Call("CreatePickups"); Call("UseWeapon");
            Check(inventory.All(i => i == WeaponKind.None) && parcels.All(p => p == null), "disabled rollout blocks grants, pickups, and item use");
            var state = new DeliveryItemState();
            Check(state.Control(100, Vector3.zero, .8f) && !state.CanCapture(100) && state.Stunned(100.7f) && !state.Stunned(100.9f), "0.8sec stun interrupts capture and expires");
            Check(!state.Control(101, Vector3.zero, .8f) && state.Control(102.9f, Vector3.zero, .8f), "stun immunity lasts 2sec after recovery");
            state = new DeliveryItemState { shieldUntil = 108, slowUntil = 105 };
            Check(!state.Control(100, Vector3.forward * 18) && state.shieldUntil == 0 && state.MoveMultiplier(100) == .6f, "bubble wrap blocks one control hit; slow bypasses shield");
            Check(state.Control(100.1f, Vector3.forward * 18) && state.Control(100.2f, Vector3.forward * 18) && Mathf.Abs(state.force.z - 6.3f) < .01f, "repeat knockback reduced to 35 percent");
            state = new DeliveryItemState { slowUntil = 105, speedUntil = 105 };
            Check(state.CanCapture(100) && Mathf.Abs(state.MoveMultiplier(100) - .9f) < .001f && state.MoveMultiplier(106) == 1, "slow preserves capture; slow and speed buffs expire");

            Set("itemPlaytestEnabled", true); Call("CreateBot", 0);
            var rival = ((GameObject[])Get("bots"))[0]; temporary.Add(rival);
            Place(player, new Vector3(0, 150, 0)); Place(controllers[0], new Vector3(0, 150, 2));
            camera.transform.SetPositionAndRotation(new Vector3(0, 150.5f, 0), Quaternion.identity);
            var pad = GameObject.CreatePrimitive(PrimitiveType.Cube); temporary.Add(pad); pad.name = "Item Check Mailbox";
            pad.transform.position = new Vector3(1, 150, 0); pad.AddComponent<InteractionTarget>(); pad.GetComponent<Collider>().enabled = false;
            mailboxes[0] = pad; mailboxRenderers[0] = pad.GetComponent<Renderer>(); owner[0] = -1;
            Check(Enum.GetValues(typeof(WeaponKind)).Length == 11, "catalog contains exactly ten household items and empty hands");
            foreach (WeaponKind kind in Enum.GetValues(typeof(WeaponKind))) if (kind != WeaponKind.None)
            { Equip(kind); Check(inventory[0] == kind && ((string)Call("ItemName", kind)) != "빈손" && (kind == WeaponKind.BoxingGloves ? ammo[0] == 0 : ammo[0] > 0), "catalog grant and budget: " + kind); }
            Empty(); Call("AddWeapon", WeaponKind.BoxingGloves); Call("AddWeapon", WeaponKind.Plunger); Call("AddWeapon", WeaponKind.BubbleWrap); Call("AddWeapon", WeaponKind.RunningShoes);
            Check(inventory.SequenceEqual(new[] { WeaponKind.BoxingGloves, WeaponKind.Plunger, WeaponKind.BubbleWrap }), "fourth pickup cannot overwrite three occupied slots");
            Call("DropSelectedWeapon"); Call("AddWeapon", WeaponKind.RunningShoes);
            Check(inventory[2] == WeaponKind.RunningShoes, "discard frees the selected slot for a new item");

            states[0] = new DeliveryItemState(); captures[0] = 1;
            Equip(WeaponKind.BoxingGloves); Call("UseWeapon");
            Check(states[0].force.z > 17 && states[0].pendingLift == 6 && captures[0] == 0 && ammo[0] == 2 && rival.activeSelf, "boxing gloves launch and interrupt without damage or death");
            float force = states[0].force.z; Call("UseWeapon"); Check(ammo[0] == 2 && states[0].force.z == force, "cooldown prevents duplicate item consumption");
            states[0] = new DeliveryItemState(); Equip(WeaponKind.Plunger); Call("UseWeapon");
            Check(states[0].force.z < -13 && ammo[0] == 2, "plunger pulls target toward the user");
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); temporary.Add(wall); wall.transform.position = new Vector3(0, 150.5f, 1); wall.transform.localScale = new Vector3(3, 3, .2f); Physics.SyncTransforms();
            states[0] = new DeliveryItemState(); Equip(WeaponKind.BoxingGloves); Call("UseWeapon");
            Check(states[0].force == Vector3.zero, "solid cover blocks cone attacks"); wall.SetActive(false);
            states[0] = new DeliveryItemState(); Equip(WeaponKind.ToyHammer); Call("UseWeapon");
            Check(!states[0].Stunned(Time.time) && (int)Get("hammerSlot") == 0, "toy hammer winds up before the hit");
            Set("hammerStarted", Time.time - .19f); Call("UpdateToyHammerSwing");
            Check(states[0].Stunned(Time.time), "nearby toy hammer impact resolves through shared stun state");
            states[0] = new DeliveryItemState(); Equip(WeaponKind.IceCream); Call("UseWeapon");
            Check(states[0].force == Vector3.zero && inventory[0] == WeaponKind.IceCream, "ice cream is a projectile rather than a continuous fan push");
            Equip(WeaponKind.RunningShoes); Call("UseWeapon"); state = (DeliveryItemState)Get("playerItems");
            Check(state.speedUntil > Time.time + 4.9f && inventory[0] == WeaponKind.None, "running shoes grant a 5sec speed and stamina buff");
            Equip(WeaponKind.BubbleWrap); Call("UseWeapon"); state = (DeliveryItemState)Get("playerItems");
            Check(state.shieldUntil > Time.time + 7.9f && inventory[0] == WeaponKind.None, "bubble wrap grants 8sec one-hit protection");

            Equip(WeaponKind.WorkGloves); Call("UseWeapon"); state = (DeliveryItemState)Get("playerItems"); state.busyUntil = 0;
            Check((int)Get("armedGloveSlot") == 0 && inventory[0] == WeaponKind.WorkGloves, "work gloves stay in their slot until successful capture");
            pad.GetComponent<Collider>().enabled = true;
            camera.transform.LookAt(pad.transform.position); Physics.SyncTransforms(); Call("UpdateInteraction");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E)); InputSystem.Update();
            Set("captureTarget", 0); Set("captureStarted", Time.time - 1.9f); Call("UpdateCapture", keyboard);
            Check(owner[0] == -1 && inventory[0] == WeaponKind.WorkGloves, "work gloves cannot capture before 2sec");
            Call("ApplyControl", 0, Vector3.forward * 18, 0f);
            Check((int)Get("captureTarget") == -1 && inventory[0] == WeaponKind.WorkGloves, "knockback cancels progress but retains unspent work gloves");
            state.forceUntil = 0; state.force = Vector3.zero; state.slowUntil = Time.time + 5;
            Call("UpdateInteraction");
            Set("captureTarget", 0); Set("captureStarted", Time.time - 2.1f); Call("UpdateCapture", keyboard);
            Check(owner[0] == 0 && inventory[0] == WeaponKind.None && (int)Get("armedGloveSlot") == -1, "successful 2sec capture consumes work gloves; slow does not interrupt");

            pad.GetComponent<Collider>().enabled = false; camera.transform.rotation = Quaternion.identity; Physics.SyncTransforms(); Call("UpdateInteraction");
            states[0] = new DeliveryItemState(); Equip(WeaponKind.Slime); Call("UseWeapon");
            Check(ammo[0] == 1 && UnityEngine.Object.FindObjectsByType<BrawlShot>().Any(s => !existingShots.Contains(s) && s.kind == WeaponKind.Slime), "packing tape creates a non-lethal throwable");
            game.ItemImpact(WeaponKind.Slime, rival.transform.position); Call("UpdateItemAreas");
            Check(states[0].MoveMultiplier(Time.time) == .6f && states[0].CanCapture(Time.time), "tape area slows rivals while allowing capture");
            Equip(WeaponKind.FountainPen); Call("UseWeapon"); game.FountainPenImpact(controllers[0], rival.transform.position, 0);
            Check(states[0].visionBlockedUntil > Time.time + 3.9f, "fountain pen directly splashes the target with four seconds of ink");
            foreach (var area in areas) area.GetType().GetField("until").SetValue(area, Time.time - 1); Call("UpdateItemAreas");
            Check(areas.Count == 0, "expired floor areas are removed; fountain pen adds no ink cloud");

            state = (DeliveryItemState)Get("playerItems");
            var safe = state.safePosition; Place(player, new Vector3(0, -9, 0)); Call("UpdatePlayer", (object)null);
            Check(Vector3.Distance(player.transform.position, safe) < .2f && player.enabled, "out-of-bounds player returns safely without death");

            Set("itemPlaytestEnabled", false); game.mapExploreMode = false; Call("Begin");
            Check(inventory.All(i => i == WeaponKind.None) && parcels.All(p => p == null) && !(bool)Get("itemPlaytestEnabled"), "normal match restart also defaults to empty hands with rollout off");
            Check(type.GetMethod("KillPlayer", flags) == null && type.GetMethod("DamageBot", flags) == null && type.GetField("playerHealth", flags) == null, "lethal damage and death paths are removed");
            var result = "DELIVERY_ITEMS: " + report.Count + " checks passed\n" + string.Join("\n", report);
            Debug.Log(result); System.IO.File.WriteAllText("art-review/delivery-items-20261006.txt", result);
        }
        finally
        {
            InputSystem.RemoveDevice(keyboard);
            foreach (var shot in UnityEngine.Object.FindObjectsByType<BrawlShot>().Where(s => !existingShots.Contains(s))) UnityEngine.Object.DestroyImmediate(shot.gameObject);
            foreach (var go in temporary) if (go != null) UnityEngine.Object.DestroyImmediate(go);
            areas.Clear(); foreach (var pair in arrays) Array.Copy(pair.copy, pair.array, pair.copy.Length);
            for (int i = 0; i < fields.Length; i++) fields[i].SetValue(game, values[i]);
            game.mapExploreMode = explore; game.enabled = enabled;
            Place(player, position); player.transform.rotation = rotation; camera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
        }
    }
}
