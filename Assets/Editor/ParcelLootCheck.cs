using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
public static class ParcelLootCheck
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    [MenuItem("Tools/Gameplay/Test Parcels/Verify Random Loot")]
    public static void Verify()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Play Mode required.");
        bool background = Application.runInBackground;
        Application.runInBackground = true; EditorApplication.isPaused = false;
        EditorApplication.update += EditorApplication.QueuePlayerLoopUpdate;
        UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>().StartCoroutine(Run(background));
        EditorApplication.QueuePlayerLoopUpdate();
    }
    static IEnumerator Run(bool background)
    {
        yield return null;
        try { yield return CheckLoot(); }
        finally { EditorApplication.update -= EditorApplication.QueuePlayerLoopUpdate; Application.runInBackground = background; }
    }
    static IEnumerator CheckLoot()
    {
        var game = UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>(); var type = game.GetType();
        object Get(string n) => type.GetField(n, Flags).GetValue(game);
        void Set(string n, object v) => type.GetField(n, Flags).SetValue(game, v);
        void Call(string n, params object[] args) => type.GetMethod(n, Flags).Invoke(game, args);
        var fields = type.GetFields(Flags).Where(f => !f.IsInitOnly).ToArray(); var values = fields.Select(f => f.GetValue(game)).ToArray();
        var items = (WeaponKind[])Get("inventory"); var savedItems = (WeaponKind[])items.Clone();
        var ammo = (int[])Get("weaponAmmo"); var savedAmmo = (int[])ammo.Clone();
        var seconds = (float[])Get("weaponSeconds"); var savedSeconds = (float[])seconds.Clone();
        var player = (CharacterController)Get("player"); var camera = (Camera)Get("cam");
        var pp = player.transform.position; var pr = player.transform.rotation; var cp = camera.transform.position; var cr = camera.transform.rotation;
        var parcels = UnityEngine.Object.FindObjectsByType<FloatingParcel>(FindObjectsInactive.Include, FindObjectsSortMode.None).OrderBy(p => p.transform.position.y).ToArray();
        var active = parcels.Select(p => p.gameObject.activeSelf).ToArray(); var available = parcels.Select(p => p.GetComponent<InteractionTarget>().available).ToArray();
        bool enabled = game.enabled, explore = game.mapExploreMode, third = game.thirdPersonPreview; var random = UnityEngine.Random.state;
        var oldKeyboard = Keyboard.current; var oldMouse = Mouse.current;
        var keyboard = InputSystem.AddDevice<Keyboard>(); var mouse = InputSystem.AddDevice<Mouse>();
        var report = new List<string>();
        void Check(bool ok, string name) { if (!ok) throw new Exception("PARCEL_LOOT: " + name); report.Add("PASS " + name); }
        void Input(params Key[] keys) { InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); InputSystem.Update(); keyboard.MakeCurrent(); }
        void Look(FloatingParcel box)
        {
            Physics.SyncTransforms(); var point = box.GetComponentInChildren<BoxCollider>().bounds.center;
            player.enabled = false; player.transform.SetPositionAndRotation(box.transform.position + Vector3.up + Vector3.back * 1.8f, Quaternion.identity); player.enabled = true;
            camera.transform.position = player.transform.position + Vector3.up * 1.6f; camera.transform.LookAt(point);
            Set("lookPitch", Mathf.DeltaAngle(0, camera.transform.eulerAngles.x)); Set("vertical", 0f);
            Physics.SyncTransforms(); Call("UpdateInteraction");
        }
        try
        {
            game.enabled = false; game.mapExploreMode = true; game.thirdPersonPreview = false;
            Set("started", true); Set("finished", false); Set("rollUntil", 0f); Set("playerItems", new DeliveryItemState()); Set("selectedSlot", 0);
            for (int i = 0; i < 3; i++) Call("ClearSlot", i);
            Check(parcels.Length == 10, "existing ten floating parcels preserved");
            for (int i = 1; i <= 10; i++) Check(DeliveryUIArt.Texture("item_" + (WeaponKind)i), "original icon resolves for " + (WeaponKind)i);
            for (int i = 0; i < 3; i++)
            {
                parcels[i].gameObject.SetActive(true); parcels[i].GetComponent<InteractionTarget>().available = true;
                Input(); yield return null; Look(parcels[i]); Input(Key.E); Call("Update");
                Check(items.Count(x => x != WeaponKind.None) == i + 1 && (int)items[i] >= 1 && (int)items[i] <= 10, "E awards one random item into first empty slot " + (i + 1) + "; pressed=" + keyboard.eKey.wasPressedThisFrame + ", current=" + (Keyboard.current == keyboard) + ", ready=" + Get("interactionReady") + ", target=" + Get("focusedTarget") + ", count=" + items.Count(x => x != WeaponKind.None) + ", parcel=" + parcels[i].name + ", active=" + parcels[i].gameObject.activeSelf);
                Check((int)Get("selectedSlot") == i && !parcels[i].gameObject.activeSelf, "new slot selected and opened parcel removed");
            }
            Input(); yield return null; Look(parcels[3]); var before = (WeaponKind[])items.Clone(); Input(Key.E); Call("Update");
            Check(items.SequenceEqual(before) && parcels[3].gameObject.activeSelf && parcels[3].GetComponent<InteractionTarget>().available, "full slots never overwrite items or consume parcel");
            Check(!(bool)Get("interactionReady") && !parcels[3].GetComponent<InteractionTarget>().IsHighlighted && game.HudInteractionHint.Contains("G"), "full slots remove white outline and show G discard guidance");
            Input(); yield return null; Input(Key.Digit2); Call("Update"); Check((int)Get("selectedSlot") == 1, "digit 2 selects second slot through actual input");
            int sceneObjects = UnityEngine.Object.FindObjectsByType<GameObject>().Length;
            Input(); yield return null; Set("armedGloveSlot", 1); Input(Key.G); Call("Update");
            Check(items[1] == WeaponKind.None && items[0] == before[0] && items[2] == before[2], "G deletes only selected item");
            Check(ammo[1] == 0 && seconds[1] == 0 && (int)Get("armedGloveSlot") == -1, "discard clears usage and armed bonus");
            Check(UnityEngine.Object.FindObjectsByType<GameObject>().Length == sceneObjects, "discard spawns no recoverable world item");
            Input(); yield return null; Look(parcels[3]); Check((bool)Get("interactionReady"), "free slot immediately restores parcel eligibility");
            Input(Key.E); Call("Update"); Check(items[1] != WeaponKind.None && items[0] == before[0] && items[2] == before[2] && !parcels[3].gameObject.activeSelf, "next E fills discarded slot without changing others");
            Check(!parcels[3].TryCollect(), "opened parcel cannot award twice");
            Call("ClearSlot", 1); Input(); yield return null; Input(Key.G); Call("Update");
            Check(items[1] == WeaponKind.None && items[0] == before[0] && items[2] == before[2], "G on empty selected slot is harmless");
            Check(!(bool)Get("itemPlaytestEnabled") && !(GameObject)Get("heldWeapon"), "loot does not activate staged attacks or held models; enabled=" + Get("itemPlaytestEnabled") + ", held=" + Get("heldWeapon"));
            UnityEngine.Random.InitState(20261008);

var counts = new int[11];
            for (int i = 0; i < 200; i++)
            {
                Call("ClearSlot", 1); parcels[4].gameObject.SetActive(true); parcels[4].GetComponent<InteractionTarget>().available = true;
                Look(parcels[4]); Call("TryPickup"); counts[(int)items[1]]++;
            }
            Check(counts[0] == 0 && counts.Skip(1).All(x => x > 0), "200 seeded native pickups reach all ten kinds and never None");
            for (int i = 1; i <= 10; i++)
            {
                Call("ClearSlot", 1); Call("AddWeapon", (WeaponKind)i);
                Check(i == 5 ? seconds[1] == 4 : ammo[1] == (i <= 2 ? 3 : i == 3 || i == 4 || i == 9 ? 2 : 1), "configured usage initialized for " + (WeaponKind)i);
            }
            File.WriteAllLines("art-review/parcel-random-loot-check-20261008.txt", report); Debug.Log("PARCEL_LOOT_OK: " + report.Count + " checks; restored.");
        }
        finally
        {
            if (Get("focusedTarget") is InteractionTarget target) target.SetFocus(false, false);
            Array.Copy(savedItems, items, 3); Array.Copy(savedAmmo, ammo, 3); Array.Copy(savedSeconds, seconds, 3);
            for (int i = 0; i < parcels.Length; i++) { parcels[i].gameObject.SetActive(active[i]); parcels[i].GetComponent<InteractionTarget>().available = available[i]; parcels[i].GetComponent<InteractionTarget>().SetFocus(false, false); }
            for (int i = 0; i < fields.Length; i++) fields[i].SetValue(game, values[i]);
            game.enabled = enabled; game.mapExploreMode = explore; game.thirdPersonPreview = third; UnityEngine.Random.state = random;
            player.enabled = false; player.transform.SetPositionAndRotation(pp, pr); player.enabled = true; camera.transform.SetPositionAndRotation(cp, cr);
            InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse); oldKeyboard?.MakeCurrent(); oldMouse?.MakeCurrent();
        }
    }
}
