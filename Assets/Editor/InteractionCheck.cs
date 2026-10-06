using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class InteractionCheck
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    [MenuItem("Tools/Gameplay/Verify Crosshair Interaction")]
    public static void Verify()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Run in Play Mode.");
        var game = UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>();
        var type = game.GetType();
        object Get(string n) => type.GetField(n, Flags).GetValue(game);
        void Set(string n, object v) => type.GetField(n, Flags).SetValue(game, v);
        void Call(string n, params object[] args) => type.GetMethod(n, Flags).Invoke(game, args);
        var camera = (Camera)Get("cam"); var position = camera.transform.position; var rotation = camera.transform.rotation;
        var fields = type.GetFields(Flags).Where(f => !f.IsInitOnly).ToArray(); var values = fields.Select(f => f.GetValue(game)).ToArray();
        var palette = (Color[])Get("colors"); var savedPalette = (Color[])palette.Clone();
        var owners = (int[])Get("owner"); var savedOwners = (int[])owners.Clone();
        var boxes = (GameObject[])Get("mailboxes"); var renderers = (Renderer[])Get("mailboxRenderers");
        var colors = renderers.Select(x => x.material.color).ToArray();
        var keyboard = InputSystem.AddDevice<Keyboard>(); var temporary = new List<GameObject>(); var report = new List<string>();
        var original = (InteractionTarget)Get("focusedTarget");
        bool EffectIs(int i, Color color)
        {
            var block = new MaterialPropertyBlock();
            return boxes[i].transform.Find("CaptureBeacon").GetComponentsInChildren<Renderer>().All(renderer =>
            { renderer.GetPropertyBlock(block); return block.GetColor("_Color") == color; });
        }
        void Check(bool okay, string message) { if (!okay) throw new Exception("INTERACTION: " + message); report.Add("PASS " + message); }
        void Aim(int i, float distance)
        {
            var box = boxes[i]; var point = box.transform.position + Vector3.up * 2.45f;
            camera.transform.position = point + box.transform.forward * distance; camera.transform.LookAt(point);
            Physics.SyncTransforms(); Call("UpdateInteraction");
        }
        try
        {
            Set("started", true); Set("finished", false); Set("playerItems", new DeliveryItemState());
            game.SetPlayerColor(0, Color.yellow);
            for (int i = 0; i < 10; i++)
            {
                owners[i] = -1; Call("SetMailboxEffectColor", i, Color.white); Aim(i, 2.5f);
                Check(Get("focusedTarget") as InteractionTarget == boxes[i].GetComponent<InteractionTarget>() && (bool)Get("interactionReady"), "actual mailbox " + (i + 1) + " selected by center ray and ready within 3m");
                Check(boxes[i].GetComponent<InteractionTarget>().IsHighlighted, "mailbox " + (i + 1) + " outline enabled");
                Check(boxes.Where((x,j) => j != i).All(x => !x.GetComponent<InteractionTarget>().IsHighlighted), "only the aimed mailbox is highlighted");
            }
            Check(Enumerable.Range(0, 10).All(i => EffectIs(i, Color.white)), "all neutral mailbox rings and sparks are white");
            Aim(8, 7);
            Check(Get("focusedTarget") as InteractionTarget == boxes[8].GetComponent<InteractionTarget>() && !(bool)Get("interactionReady"), "distant mailbox is identified but not interactable");
            var block = new MaterialPropertyBlock();
            boxes[8].GetComponentsInChildren<MeshRenderer>(true).First(x => x.name == "InteractionOutline").GetPropertyBlock(block);
            Check(!boxes[8].GetComponent<InteractionTarget>().IsHighlighted, "distant target has no outline");
            Aim(8, 2.5f);
            boxes[8].GetComponentsInChildren<MeshRenderer>(true).First(x => x.name == "InteractionOutline").GetPropertyBlock(block);
            Check(block.GetColor("_Color") == Color.white, "ready outline is white");
            var target = boxes[8].GetComponent<InteractionTarget>(); target.available = false; Call("UpdateInteraction");
            Check(!(bool)Get("interactionReady") && !target.IsHighlighted, "unavailable targets cannot interact or highlight"); target.available = true;
            var state = (DeliveryItemState)Get("playerItems"); state.stunUntil = Time.time + 10; Call("UpdateInteraction");
            Check(!(bool)Get("interactionReady") && !target.IsHighlighted, "stun disables interaction and outline"); state.stunUntil = 0;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); temporary.Add(wall);
            wall.transform.position = camera.transform.position + camera.transform.forward;
            wall.transform.localScale = new Vector3(2, 2, .1f); wall.transform.rotation = camera.transform.rotation;
            Physics.SyncTransforms(); Call("UpdateInteraction");
            Check(Get("focusedTarget") == null && !target.IsHighlighted, "nearest solid occluder prevents through-wall highlighting");
            wall.GetComponent<Collider>().isTrigger = true; Physics.SyncTransforms(); Call("UpdateInteraction");
            Check(Get("focusedTarget") as InteractionTarget == target, "triggers do not obstruct interaction rays"); wall.SetActive(false);
            camera.transform.rotation = Quaternion.LookRotation(Vector3.up); Call("UpdateInteraction");
            Check(Get("focusedTarget") == null && !target.IsHighlighted, "looking away immediately removes outline");
            Aim(8, 15); Check(Get("focusedTarget") == null, "targets outside 12m detection range are cleared");
            Aim(8, 2.5f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E)); InputSystem.Update();
            Set("captureTarget", 8); Set("captureStarted", Time.time - 3.1f); Call("UpdateCapture", keyboard);
            Check(owners[8] == 0 && owners[0] == -1, "E captures the aimed actual mailbox, not a nearby/nearest one");
            Check(EffectIs(8, Color.yellow) && EffectIs(0, Color.white), "E capture makes only that mailbox's ring and sparks yellow");
            Check(renderers[8].material.color == colors[8], "capture leaves mailbox body color unchanged");
            Call("Capture", 8, 1);
            Check(EffectIs(8, palette[1]) && renderers[8].material.color == colors[8], "rival recapture changes only the beacon to the new owner's color");
            var chosen = new Color(.15f, .8f, .9f); game.SetPlayerColor(1, chosen);
            Check(EffectIs(8, chosen) && EffectIs(0, Color.white), "player color selection updates owned beacons without changing neutral ones");
            Call("Capture", 8, 0);
            Call("UpdateInteraction"); Check(!(bool)Get("interactionReady") && !target.IsHighlighted, "owned mailbox cannot be recaptured or highlighted");
            owners[8] = -1; Aim(8, 2.5f); Set("captureTarget", 8); Set("captureStarted", Time.time);
            camera.transform.rotation = Quaternion.LookRotation(Vector3.up); Call("UpdateInteraction"); Call("UpdateCapture", keyboard);
            Check((int)Get("captureTarget") == -1, "looking away cancels capture progress");
            var prop = GameObject.CreatePrimitive(PrimitiveType.Cube); temporary.Add(prop); prop.transform.position = new Vector3(0, 150, 10);
            var generic = prop.AddComponent<InteractionTarget>(); generic.displayName = "Test Parcel";
            camera.transform.position = prop.transform.position - Vector3.forward * 2; camera.transform.LookAt(prop.transform);
            Physics.SyncTransforms(); Call("UpdateInteraction");
            Check(Get("focusedTarget") as InteractionTarget == generic && (bool)Get("interactionReady"), "other objects use the same center-ray marker without mailbox-specific selection");
            var result = "INTERACTION_OK: " + report.Count + " checks\n" + string.Join("\n", report);
            System.IO.File.WriteAllText("art-review/crosshair-interaction-20261006.txt", result); Debug.Log(result);
        }
        finally
        {
            if (Get("focusedTarget") is InteractionTarget current) current.SetFocus(false, false);
            foreach (var go in temporary) UnityEngine.Object.DestroyImmediate(go);
            InputSystem.RemoveDevice(keyboard); Array.Copy(savedOwners, owners, owners.Length); Array.Copy(savedPalette, palette, palette.Length);
            for (int i = 0; i < boxes.Length; i++) Call("SetMailboxEffectColor", i, owners[i] < 0 ? Color.white : palette[owners[i]]);
            for (int i = 0; i < renderers.Length; i++) renderers[i].material.color = colors[i];
            for (int i = 0; i < fields.Length; i++) fields[i].SetValue(game, values[i]);
            camera.transform.SetPositionAndRotation(position, rotation);
            if (original) original.SetFocus(true, (bool)Get("interactionReady"));
        }
    }
    [MenuItem("Tools/Gameplay/Preview Mailbox Interaction")]
    public static void Preview() => PreviewAt(2.5f);
    [MenuItem("Tools/Gameplay/Preview Distant Mailbox")]
    public static void PreviewDistant() => PreviewAt(7f);
    [MenuItem("Tools/Gameplay/Preview Captured Mailbox Effect")]
    public static void PreviewCaptured()
    {
        PreviewAt(2.5f);
        var game = UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>();
        game.GetType().GetMethod("Capture", Flags).Invoke(game, new object[] { 8, 0 });
        game.GetType().GetMethod("UpdateInteraction", Flags).Invoke(game, null);
    }
    static void PreviewAt(float distance)
    {
        if (!EditorApplication.isPlaying) throw new Exception("Run in Play Mode.");
        var game = UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>(); var type = game.GetType();
        var box = GameObject.Find("Layer_11_Mailboxes/Plot_09_Mailbox");
        var player = (CharacterController)type.GetField("player", Flags).GetValue(game);
        player.enabled = false; player.transform.position = box.transform.position + box.transform.forward * distance + Vector3.up;
        player.transform.rotation = Quaternion.LookRotation(-box.transform.forward); player.enabled = true;
        var camera = Camera.main; camera.transform.position = player.transform.position + Vector3.up * 1.6f;
        camera.transform.LookAt(box.transform.position + Vector3.up * 2.45f);
        type.GetField("lookPitch", Flags).SetValue(game, Mathf.DeltaAngle(0, camera.transform.eulerAngles.x));
        Physics.SyncTransforms(); type.GetMethod("UpdateInteraction", Flags).Invoke(game, null);
    }
}
