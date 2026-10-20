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

public static class GloveEffectsCheck
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    [MenuItem("Tools/Gameplay/Gloves/Capture Ready")]
    public static void CaptureReady() => UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>().StartCoroutine(CaptureReadyFrame());
    static IEnumerator CaptureReadyFrame()
    {
        bool background = Application.runInBackground;
        Application.runInBackground = true; EditorApplication.update += EditorApplication.QueuePlayerLoopUpdate;
        try
        {
            yield return new WaitForSeconds(.4f);
            yield return new WaitForEndOfFrame(); Capture("glove-effects-ready-20261009.png");
            Debug.Log("GLOVE_CAPTURE_READY: native Game View saved.");
        }
        finally { Application.runInBackground = background; EditorApplication.update -= EditorApplication.QueuePlayerLoopUpdate; }
    }
    [MenuItem("Tools/Gameplay/Gloves/Verify Effects")]
    public static void Verify()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Play Mode required.");
        EditorApplication.isPaused = false;
        UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>().StartCoroutine(Run());
    }
    static IEnumerator Run()
    {
        var game = UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>(); var type = game.GetType();
        object Get(string n) => type.GetField(n, Flags).GetValue(game);
        void Set(string n, object v) => type.GetField(n, Flags).SetValue(game, v);
        object Call(string n, params object[] a) => type.GetMethod(n, Flags).Invoke(game, a);
        var excluded = new[] { "heldWeapon", "heldItem", "heldKind", "heldInFirstPerson", "heldCamera" };
        var fields = type.GetFields(Flags).Where(f => !f.IsInitOnly && !excluded.Contains(f.Name)).ToArray();
        var values = fields.Select(f => f.GetValue(game)).ToArray();
        var arrays = type.GetFields(Flags).Where(f => f.FieldType.IsArray).Select(f => (array: (Array)f.GetValue(game), saved: (Array)((Array)f.GetValue(game)).Clone())).ToArray();
        var player = (CharacterController)Get("player"); var bots = (CharacterController[])Get("botControllers");
        var cam = (Camera)Get("cam"); var visual = (DeliveryRobotVisual)Get("playerVisual");
        var transforms = new[] { player.transform, bots[0].transform, cam.transform };
        var positions = transforms.Select(t => t.position).ToArray(); var rotations = transforms.Select(t => t.rotation).ToArray();
        var inventory = (WeaponKind[])Get("inventory"); var ammo = (int[])Get("weaponAmmo");
        var states = (DeliveryItemState[])Get("botItems"); var owners = (int[])Get("owner");
        var mailbox = (GameObject[])Get("mailboxes"); var captures = (float[])Get("botCaptureStarted");
        var keyboardBefore = Keyboard.current; var mouseBefore = Mouse.current;
        var keyboard = InputSystem.AddDevice<Keyboard>(); var mouse = InputSystem.AddDevice<Mouse>();
        bool enabled = game.enabled, background = Application.runInBackground, third = game.thirdPersonPreview;
        var temporary = new List<GameObject>(); var report = new List<string>();
        void Check(bool ok, string detail) { if (!ok) throw new Exception("GLOVE_EFFECTS: " + detail); report.Add("PASS " + detail); }
        void Place(CharacterController actor, Vector3 p) { actor.enabled = false; actor.transform.position = p; actor.enabled = true; Physics.SyncTransforms(); }
        void Input(bool click = false, params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            var state = new MouseState(); if (click) state = state.WithButton(MouseButton.Left);
            InputSystem.QueueStateEvent(mouse, state); InputSystem.Update();
        }
        void Equip(WeaponKind kind)
        {
            Call("FinishGlovePush"); for (int i = 0; i < 3; i++) Call("ClearSlot", i);
            Call("AddWeapon", kind); Set("nextAttackAt", 0f); Set("rollUntil", 0f);
            Set("nextBoxingRightHand", true);
            Set("playerItems", new DeliveryItemState()); states[0] = new DeliveryItemState();
            Call("UpdateHeldWeapon"); Input();
        }
        void Hit() { Set("boxingStarted", Time.time - .161f); Call("UpdateGlovePush"); }
        void End() { Set("boxingStarted", Time.time - .43f); Call("UpdateGlovePush"); }
        void Shot() { Input(); Input(true); Call("UpdatePlayer", keyboard); Input(); }
        GameObject Cube(string name, Vector3 p, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.position = p; go.transform.localScale = size;
            temporary.Add(go); Physics.SyncTransforms(); return go;
        }
        try
        {
            Application.runInBackground = true; EditorApplication.update += EditorApplication.QueuePlayerLoopUpdate;
            game.enabled = false; game.thirdPersonPreview = false; Set("lookPitch", 0f); Set("vertical", -2f);
            Check(inventory.SequenceEqual(new[] { WeaponKind.Plunger, WeaponKind.None, WeaponKind.None }) && (int)Get("selectedSlot") == 0, "startup provides plunger in 1, empty 2 and 3, selects 1");
            Check(!(bool)Get("itemPlaytestEnabled"), "other household effects remain staged");
            Cube("Glove Check Floor", new Vector3(0, 149.95f, 0), new Vector3(80, .1f, 80));
            Place(player, new Vector3(0, 151, 0)); player.transform.rotation = Quaternion.identity;
            Place(bots[0], new Vector3(0, 151, 2.3f)); cam.transform.rotation = Quaternion.identity; Call("PositionCamera");
            for (int i = 0; i < owners.Length; i++) owners[i] = 1;
            Set("nextExplorationPlan", Time.time + 100); ((int[])Get("botTarget"))[0] = -1;
            Equip(WeaponKind.BoxingGloves);
            var held = (DeliveryHeldItem)Get("heldItem");
            var hand = held.GetComponentsInChildren<Transform>().Single(t => (t.name == "Hand_R" || t.name == "Hand_L") && cam.transform.InverseTransformPoint(t.position).x > 0);
            var otherHand = held.GetComponentsInChildren<Transform>().Single(t => (t.name == "Hand_R" || t.name == "Hand_L") && cam.transform.InverseTransformPoint(t.position).x < 0);
            var guard = cam.transform.InverseTransformPoint(hand.position);
            var otherGuard = cam.transform.InverseTransformPoint(otherHand.position);
            var otherRotation = otherHand.localRotation;
            captures[0] = 1; Shot();
            Check((int)Get("boxingSlot") == 0 && ammo[0] == 0 && states[0].force == Vector3.zero, "real left click starts unlimited glove animation before impact without consuming ammo");
            bool nextHand = (bool)Get("nextBoxingRightHand");
            Call("UseWeapon"); Check(ammo[0] == 0 && (bool)Get("nextBoxingRightHand") == nextHand, "repeat click during cooldown cannot duplicate attacks or hand alternation");
            Set("boxingStarted", Time.time - .14f); Call("UpdateGlovePush");
            Check(states[0].force == Vector3.zero, "windup has no early hit");
            Hit(); Call("LateUpdate");
            var extended = cam.transform.InverseTransformPoint(hand.position);
            Check((bool)Get("boxingRightHand") && extended.z > guard.z + .5f && Mathf.Abs(extended.x) < Mathf.Abs(guard.x) && extended.y > guard.y, "first click extends only right glove forward");
            Check(Vector3.Distance(cam.transform.InverseTransformPoint(otherHand.position), otherGuard) < .01f && Quaternion.Angle(otherHand.localRotation, otherRotation) < .1f, "left glove stays in guard during right punch");
            Check(states[0].force.z > 31 && Mathf.Abs(states[0].pendingLift - 6 * Mathf.Sqrt(3)) < .01f && captures[0] == 0, "stronger impact pushes and lifts rival while cancelling its capture without self-blocking");
            yield return new WaitForEndOfFrame(); Capture("glove-alternate-right-20261009.png");
            Hit(); Check(states[0].force.z > 31, "one swing resolves impact only once");
            float before = bots[0].transform.position.z, until = Time.time + .3f;
            while (Time.time < until) { Call("UpdateBots"); yield return null; }
            Check(bots[0].transform.position.z > before + 1 && bots[0].gameObject.activeSelf, "native controller is displaced even when rival has no capture target");
            End(); Check((int)Get("boxingSlot") == -1 && Vector3.Distance(cam.transform.InverseTransformPoint(hand.position), guard) < .01f, "animation returns to original guard");
            Set("nextAttackAt", 0f); Shot(); Set("boxingStarted", Time.time - .16f); Call("LateUpdate");
            Check(!(bool)Get("boxingRightHand") && cam.transform.InverseTransformPoint(otherHand.position).z > otherGuard.z + .5f && Vector3.Distance(cam.transform.InverseTransformPoint(hand.position), guard) < .01f, "second click extends left glove while right glove stays in guard");
            yield return new WaitForEndOfFrame(); Capture("glove-alternate-left-20261009.png");
            End();
            Check(Vector3.Distance(cam.transform.InverseTransformPoint(otherHand.position), otherGuard) < .01f && (bool)Get("nextBoxingRightHand"), "left glove recovers and following click alternates back to right");
            Equip(WeaponKind.BoxingGloves);
            game.thirdPersonPreview = true;
            var animator = visual.model.GetComponent<Animator>(); animator.Play("IDLE_ARMED", 0, 0); animator.Update(0);
            Call("UpdateHeldWeapon");
            var arm = visual.model.GetComponentsInChildren<Transform>().Single(t => t.name == "Arm.L");
            var otherArm = visual.model.GetComponentsInChildren<Transform>().Single(t => t.name == "Arm.R");
            var armGuard = arm.rotation;
            var otherArmGuard = otherArm.rotation;
            Set("nextAttackAt", 0f);
            Shot(); Set("boxingStarted", Time.time - .16f); Call("LateUpdate");
            var worldItem = (DeliveryHeldItem)Get("heldItem");
            var worldRight = worldItem.GetComponentsInChildren<Transform>().Single(t => t.name == "Hand_R");
            Check(Quaternion.Angle(arm.rotation, armGuard) > 20 && Vector3.Distance(worldRight.position, arm.TransformPoint(worldItem.rightPosition)) < .01f, "third-person arm animation keeps glove attached to its physical arm");
            Check(Quaternion.Angle(otherArm.rotation, otherArmGuard) < .1f, "third-person right punch leaves left arm in guard");
            yield return new WaitForEndOfFrame(); Capture("glove-push-third-person-20261009.png");
            End(); animator.Update(0);
            Check(Quaternion.Angle(arm.rotation, armGuard) < 1, "third-person arm returns to armed animation after push");
            Set("nextAttackAt", 0f); Shot(); Set("boxingStarted", Time.time - .16f); Call("LateUpdate");
            Check(Quaternion.Angle(otherArm.rotation, otherArmGuard) > 20 && Quaternion.Angle(arm.rotation, armGuard) < .1f, "third-person following click uses only physical left arm");
            End(); animator.Update(0);
            game.thirdPersonPreview = false; Call("PositionCamera"); Call("UpdateHeldWeapon");
            Place(bots[0], new Vector3(0, 151, 2.3f));
            var wall = Cube("Glove Check Wall", new Vector3(0, 151.7f, 1.4f), new Vector3(3, 3, .12f));
            Equip(WeaponKind.BoxingGloves); Shot(); Hit();
            Check(states[0].force == Vector3.zero && ammo[0] == 0, "wall blocks impact without consuming unlimited gloves"); wall.SetActive(false);
            Place(bots[0], new Vector3(0, 151, 4.5f)); Equip(WeaponKind.BoxingGloves); Shot(); Hit();
            Check(states[0].force.z > 31, "extended range hits rival at 4.5 metres from player");
            Place(bots[0], new Vector3(0, 151, 6)); Equip(WeaponKind.BoxingGloves); Shot(); Hit();
            Check(states[0].force == Vector3.zero, "target beyond 5 metres from camera is not hit");
            Place(bots[0], new Vector3(2.5f, 151, 0)); Equip(WeaponKind.BoxingGloves); Shot(); Hit();
            Check(states[0].force == Vector3.zero, "side target outside crosshair ray is not hit");
            Place(bots[0], new Vector3(.8f, 151, 2.3f)); Equip(WeaponKind.BoxingGloves);
            captures[0] = Time.time - 1; float offAimCapture = captures[0]; Shot(); Hit();
            Check(states[0].force == Vector3.zero && captures[0] == offAimCapture, "nearby rival inside old cone is not hit or interrupted when crosshair misses its body");
            Place(bots[0], new Vector3(.3f, 151, 2.3f)); Equip(WeaponKind.BoxingGloves); Shot(); Hit();
            Check(states[0].force.z > 0, "crosshair touching the side of the rival collision body still hits");
            Place(bots[0], new Vector3(0, 151, 2.3f)); Equip(WeaponKind.BoxingGloves);
            var aboveRay = cam.transform.position; Shot(); cam.transform.position = new Vector3(0, 153, 0);
            Hit(); Check(states[0].force == Vector3.zero, "crosshair above rival body does not hit"); cam.transform.position = aboveRay;
            Equip(WeaponKind.BoxingGloves); Shot(); cam.transform.position = new Vector3(0, 150.4f, 0);
            Hit(); Check(states[0].force == Vector3.zero, "crosshair in empty hover gap does not hit"); cam.transform.position = aboveRay;
            Equip(WeaponKind.BoxingGloves); Shot(); cam.transform.rotation = Quaternion.Euler(0, 30, 0); Hit();
            Check(states[0].force == Vector3.zero, "aiming away during windup cancels impact on previously aimed rival");
            cam.transform.rotation = Quaternion.identity;
            Equip(WeaponKind.BoxingGloves); cam.transform.rotation = Quaternion.Euler(0, 30, 0); Shot(); cam.transform.rotation = Quaternion.identity; Hit();
            Check(states[0].force.z > 31, "hit uses crosshair at impact rather than the earlier click direction");
            Equip(WeaponKind.BoxingGloves); Shot();
            Check(inventory[0] == WeaponKind.BoxingGloves && ammo[0] == 0 && (GameObject)Get("heldWeapon"), "unlimited gloves stay equipped throughout the action");
            Hit(); End(); Call("UpdateHeldWeapon");
            Check(inventory[0] == WeaponKind.BoxingGloves && (GameObject)Get("heldWeapon") && (string)Call("ItemRemainingText", 0) == "무제한", "recovery retains gloves and HUD shows unlimited instead of an ammo count");
            for (int i = 0; i < 8; i++) { Set("nextAttackAt", 0f); Shot(); Hit(); End(); }
            Check(inventory[0] == WeaponKind.BoxingGloves && ammo[0] == 0 && (int)Get("boxingSlot") == -1, "more than three attacks never exhaust or remove gloves");
            Equip(WeaponKind.BoxingGloves); Shot(); Call("DropSelectedWeapon"); Hit();
            Check(states[0].force == Vector3.zero && (int)Get("boxingSlot") == -1, "discard cancels pending impact without spawning a pickup");
            Equip(WeaponKind.BoxingGloves); Shot(); ((DeliveryItemState)Get("playerItems")).stunUntil = Time.time + 1; Hit();
            Check(states[0].force == Vector3.zero && (int)Get("boxingSlot") == -1 && inventory[0] == WeaponKind.BoxingGloves && ammo[0] == 0, "stun cancels windup while retaining unlimited gloves");
            Equip(WeaponKind.IceCream); Call("UseWeapon");
            Check(inventory[0] == WeaponKind.IceCream && states[0].force == Vector3.zero, "replacement ice cream never applies the removed fan push");
            var approachingGoal = Cube("Approaching Rival Check Mailbox", new Vector3(0, 151.8f, -7), new Vector3(.7f, .9f, .4f));
            approachingGoal.AddComponent<InteractionTarget>(); mailbox[0] = approachingGoal; owners[0] = -1;
            ((int[])Get("botTarget"))[0] = 0; Set("explorationRoute", new[] { new Vector3(0, 150, -7) }); Set("explorationCorner", 0);
            Place(bots[0], new Vector3(0, 151, 2.3f)); ((float[])Get("botVertical"))[0] = -2;
            float approachingBefore = bots[0].transform.position.z; Call("UpdateBots");
            Check(bots[0].transform.position.z < approachingBefore, "test rival actively approaches player before knockback");
            Place(bots[0], new Vector3(0, 151, 2.3f)); states[0] = new DeliveryItemState();
            Call("ApplyControl", 1, Vector3.forward * 18 + Vector3.up * 6, 0f);
            float baselineStart = bots[0].transform.position.z;
            while (states[0].KnockedBack) { Call("UpdateBots"); yield return null; }
            float baselineDistance = bots[0].transform.position.z - baselineStart;
            Place(bots[0], new Vector3(0, 151, 2.3f)); ((float[])Get("botVertical"))[0] = -2;
            Place(player, new Vector3(0, 151, 0)); cam.transform.rotation = Quaternion.identity; Call("PositionCamera");
            Equip(WeaponKind.BoxingGloves); Shot(); Hit();
            float boostedStart = bots[0].transform.position.z;
            while (states[0].KnockedBack) { Call("UpdateBots"); yield return null; }
            float boostedDistance = bots[0].transform.position.z - boostedStart;
            Check(baselineDistance > 4 && boostedDistance / baselineDistance > 2.7f && boostedDistance / baselineDistance < 3.3f, "approaching rival travels about 3x farther: " + baselineDistance.ToString("0.00") + "m -> " + boostedDistance.ToString("0.00") + "m");
            var residual = new DeliveryItemState(); residual.Control(Time.time, Vector3.forward * 31);
            residual.forceUntil = Time.time - 1;
            Check(!residual.CanCapture(Time.time), "capture remains blocked for the entire knockback even beyond the old 0.35 second timer");
            approachingGoal.SetActive(false);
            var rivalPad = Cube("Rival Capture Check Mailbox", new Vector3(0, 151.8f, 3), new Vector3(.7f, .9f, .4f));
            rivalPad.AddComponent<InteractionTarget>(); mailbox[0] = rivalPad; owners[0] = -1;
            ((int[])Get("botTarget"))[0] = 0;
            Place(bots[0], new Vector3(0, 151, 2)); Place(player, new Vector3(0, 151, 0)); cam.transform.rotation = Quaternion.identity; Call("PositionCamera");
            Equip(WeaponKind.BoxingGloves); captures[0] = Time.time - 2.7f; Call("UpdateBots");
            Check(owners[0] == -1 && Time.time - captures[0] > 2.6f, "rival naturally continues an almost-complete mailbox capture before hit");
            Shot(); Hit(); Call("UpdateBots");
            Check(captures[0] == 0 && owners[0] == -1 && !states[0].CanCapture(Time.time), "real glove hit resets rival capture timer and blocks capture during knockback");
            Place(bots[0], new Vector3(0, 151, 2)); states[0].force = Vector3.zero; states[0].forceUntil = Time.time - 1; states[0].pendingLift = 0;
            ((float[])Get("botVertical"))[0] = -2; Call("UpdateBots");
            Check(captures[0] > 0 && Time.time - captures[0] < .1f && owners[0] == -1, "rival returning after knockback starts capture from zero rather than restoring progress");
            captures[0] = Time.time - 2.9f; Call("UpdateBots"); Check(owners[0] == -1, "restarted rival capture still needs the full 3 seconds");
            captures[0] = Time.time - 3.01f; Call("UpdateBots"); Check(owners[0] == 1, "rival can capture normally after completing a fresh 3 second attempt");
            rivalPad.SetActive(false);
            Place(bots[0], new Vector3(6, 151, 5)); Place(player, new Vector3(0, 151, 0)); Call("PositionCamera");
            var pad = Cube("Glove Check Mailbox", new Vector3(0, 151.8f, 2), new Vector3(.7f, .9f, .4f));
            var interaction = pad.AddComponent<InteractionTarget>(); interaction.displayName = "우체통"; interaction.actionHint = "E · 점령"; mailbox[0] = pad; owners[0] = -1;
            Equip(WeaponKind.WorkGloves); Call("UpdateInteraction"); Input();
            game.enabled = true;
            yield return new WaitForEndOfFrame(); Capture("work-glove-capture-hud-20261009.png");
            game.enabled = false; Input(false, Key.E);
            Check((bool)Get("interactionReady") && (float)type.GetProperty("CaptureSeconds", Flags).GetValue(game) == 1.5f, "selected work gloves automatically halve capture time to 1.5 seconds");
            Set("captureTarget", 0); Set("captureStarted", Time.time - .75f); Call("UpdateCapture", keyboard);
            Check(Mathf.Abs(game.HudCaptureProgress - .5f) < .02f && inventory[0] == WeaponKind.WorkGloves, "HUD progress uses halved duration and retains unspent gloves");
            Set("captureStarted", Time.time - 1.4f); Call("UpdateCapture", keyboard);
            Check(owners[0] == -1, "capture cannot complete before 1.5 seconds");
            Input(); Call("UpdateCapture", keyboard);
            Check((int)Get("captureTarget") == -1 && inventory[0] == WeaponKind.WorkGloves, "releasing E resets capture without spending work gloves");
            Input(false, Key.E); Call("UpdateInteraction"); Set("captureTarget", 0); Set("captureStarted", Time.time - 1.51f); Call("UpdateCapture", keyboard);
            Check(owners[0] == 0 && inventory[0] == WeaponKind.WorkGloves && ammo[0] == 1, "successful 1.5 second capture retains unlimited work gloves");
            for (int i = 0; i < 3; i++)
            {
                owners[0] = -1; Call("UpdateInteraction"); Set("captureTarget", 0); Set("captureStarted", Time.time - 1.51f);
                Call("UpdateCapture", keyboard);
            }
            Check(owners[0] == 0 && inventory[0] == WeaponKind.WorkGloves && ammo[0] == 1 &&
                (string)Call("ItemRemainingText", 0) == "무제한", "repeated captures keep work gloves and the 1.5 second bonus");
            owners[0] = -1; Equip(WeaponKind.WorkGloves); Call("AddWeapon", WeaponKind.BoxingGloves);
            Check((float)type.GetProperty("CaptureSeconds", Flags).GetValue(game) == 3, "switching away removes the work glove benefit");
            Input(false, Key.Digit1); Call("SelectSlot", keyboard); Input(false, Key.E); Call("UpdateInteraction");
            Set("captureTarget", 0); Set("captureStarted", Time.time - .7f); Call("ApplyControl", 0, Vector3.forward * 18, 0f);
            Check((int)Get("captureTarget") == -1 && inventory[0] == WeaponKind.WorkGloves, "received knockback cancels progress but retains gloves");
            Call("UpdateCapture", keyboard);
            Check(game.HudCaptureProgress == 0 && (int)Get("captureTarget") == -1 && !((DeliveryItemState)Get("playerItems")).CanCapture(Time.time), "holding E while knocked back cannot continue player capture or restore HUD progress");
            Place(player, new Vector3(0, 151, 0)); Set("vertical", -2f); Set("stamina", 100f);
            Set("playerItems", new DeliveryItemState()); Call("ApplyControl", 0, Vector3.forward * (18 * Mathf.Sqrt(3)), 0f);
            Input(); Input(false, Key.S, Key.LeftShift, Key.Space); Call("UpdatePlayer", keyboard);
            Check(player.transform.position.z > 0 && (float)Get("stamina") == 100 && Time.time >= (float)Get("rollUntil"), "backward input, jump and Shift cannot resist knockback or spend dash stamina during it");
            Input();
            Call("DropSelectedWeapon"); Check((float)type.GetProperty("CaptureSeconds", Flags).GetValue(game) == 3, "discarding work gloves restores normal 3 second capture");
            File.WriteAllLines("art-review/glove-effects-check-20261009.txt", report);
            Debug.Log("GLOVE_EFFECTS_OK: " + report.Count + " checks passed.\n" + string.Join("\n", report));
        }
        finally
        {
            Call("FinishGlovePush"); foreach (var go in temporary) UnityEngine.Object.Destroy(go);
            foreach (var a in arrays) Array.Copy(a.saved, a.array, a.saved.Length);
            for (int i = 0; i < fields.Length; i++) fields[i].SetValue(game, values[i]);
            Place(player, positions[0]); Place(bots[0], positions[1]);
            for (int i = 0; i < transforms.Length; i++) transforms[i].SetPositionAndRotation(positions[i], rotations[i]);
            InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse); keyboardBefore?.MakeCurrent(); mouseBefore?.MakeCurrent();
            game.thirdPersonPreview = third; game.enabled = enabled; Call("UpdateHeldWeapon");
            Application.runInBackground = background; EditorApplication.update -= EditorApplication.QueuePlayerLoopUpdate;
        }
    }
    static void Capture(string file)
    {
        var image = ScreenCapture.CaptureScreenshotAsTexture();
        try { Directory.CreateDirectory("art-review"); File.WriteAllBytes("art-review/" + file, image.EncodeToPNG()); }
        finally { UnityEngine.Object.Destroy(image); }
    }
}
