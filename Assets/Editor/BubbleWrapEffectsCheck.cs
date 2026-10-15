using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class BubbleWrapEffectsCheck
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    [MenuItem("Tools/Gameplay/Bubble Wrap/Verify Effects")]
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
        var names = new[] { "playerItems", "selectedSlot", "nextAttackAt", "captureTarget", "captureStarted",
            "bubbleScreenBlend", "rivalBubblePlaytest", "nextRivalBubbleTest", "rivalShoesPlaytest",
            "started", "finished", "lookPitch", "feedback", "vertical", "stamina", "rollUntil" };
        var saved = names.Select(Get).ToArray();
        var inventory = (WeaponKind[])Get("inventory"); var inventorySaved = (WeaponKind[])inventory.Clone();
        var ammo = (int[])Get("weaponAmmo"); var ammoSaved = (int[])ammo.Clone();
        var states = (DeliveryItemState[])Get("botItems"); var rivalStateSaved = states[0];
        var captures = (float[])Get("botCaptureStarted"); var captureSaved = captures[0];
        var player = (CharacterController)Get("player"); var rival = ((CharacterController[])Get("botControllers"))[0];
        var cam = (Camera)Get("cam"); var transforms = new[] { player.transform, rival.transform, cam.transform };
        var positions = transforms.Select(t => t.position).ToArray(); var rotations = transforms.Select(t => t.rotation).ToArray();
        var shields = (BubbleWrapShield[])Get("bubbleShields"); var existing = (BubbleWrapShield[])shields.Clone();
        bool enabled = game.enabled, background = Application.runInBackground, third = game.thirdPersonPreview;
        var report = new List<string>();
        void Check(bool ok, string detail)
        {
            if (!ok) throw new Exception("BUBBLE_WRAP: " + detail);
            report.Add("PASS " + detail);
        }
        void Place(CharacterController actor, Vector3 p)
        { actor.enabled = false; actor.transform.position = p; actor.enabled = true; Physics.SyncTransforms(); }
        try
        {
            Application.runInBackground = true; EditorApplication.update += EditorApplication.QueuePlayerLoopUpdate;
            yield return null; game.enabled = false; game.thirdPersonPreview = false;
            Set("rivalBubblePlaytest", false); Set("rivalShoesPlaytest", false);
            Set("started", true); Set("finished", false);
            Check(inventory[2] == WeaponKind.BubbleWrap,
                "startup retains slot three bubble wrap for immediate testing");
            Set("selectedSlot", 2);
            Check(!(bool)Get("itemPlaytestEnabled"), "other staged item effects remain gated");
            Set("playerItems", new DeliveryItemState()); states[0] = new DeliveryItemState();
            Set("nextAttackAt", 0f); Call("UpdateHeldWeapon");
            Check(!(GameObject)Get("heldWeapon") && !(DeliveryHeldItem)Get("heldItem"),
                "selecting bubble wrap creates no held item model");
            Check(!(bool)type.GetProperty("BubbleWrapActive", Flags).GetValue(game),
                "selection alone does not grant protection");
            Set("captureTarget", 0); Set("captureStarted", Time.time - 1); Call("UseWeapon");
            var state = (DeliveryItemState)Get("playerItems");
            Check(Mathf.Abs(state.shieldUntil - Time.time - 8) < .01f && inventory[2] == WeaponKind.BubbleWrap,
                "real use grants eight seconds of one-hit protection with unlimited item retained");
            Check((int)Get("captureTarget") == -1, "activation cancels capture");
            float until = state.shieldUntil; Call("UseWeapon");
            Check(state.shieldUntil == until, "cooldown rejects immediate duplicate activation");
            state.shieldUntil = Time.time + 2; Set("nextAttackAt", 0f); Call("UseWeapon");
            Check(Mathf.Abs(state.shieldUntil - Time.time - 8) < .01f && float.IsNegativeInfinity(state.shieldBrokenAt),
                "reuse refreshes eight seconds without adding extra shield charges");
            Call("UpdateBubbleWrapFX");
            Check((float)Get("bubbleScreenBlend") > 0 && shields[0] && !shields[0].Visible,
                "first person uses screen protection and hides own world shell");
            Call("ActivateBubbleWrap", 1); Call("UpdateBubbleWrapFX");
            var shield = shields[1];
            Check(shield && shield.Visible && shield.transform.parent == rival.transform,
                "opponent protection visibly follows the floating actor");
            yield return null; yield return null; Call("UpdateBubbleWrapFX");
            Check(shield.Shell.sharedMaterial.shader.isSupported &&
                shield.Shell.sharedMaterial.shader.name == "MonsterDelivery/BubbleWrapShield",
                "transparent bubble shader is supported in the Editor");
            Check(Mathf.Abs(shield.Shell.sharedMaterial.GetFloat("_BubbleTime") - Time.time) < .001f,
                "shield surface bubble animation receives the live game clock");
            Check(SurfaceAnimationChanges(shield.Shell),
                "rendered surface bubbles visibly change between inflation and pop phases");
            Check(shield.Shell.bounds.Contains(rival.transform.position + Vector3.up * .7f) &&
                shield.Shell.bounds.Contains(rival.transform.position + Vector3.up * 1.4f),
                "bubble covers the floating torso and head");
            Check(!shield.GetComponentsInChildren<Collider>().Any(c => c.enabled) &&
                Mathf.Abs(rival.radius - .55f) < .001f, "visual bubble adds no physics collision");
            game.thirdPersonPreview = true; Call("UpdateBubbleWrapFX"); Call("UpdateHeldWeapon");
            Check(shields[0].Visible && !(GameObject)Get("heldWeapon"),
                "third person displays protection around own body without a held model");
            game.thirdPersonPreview = false; Call("UpdateBubbleWrapFX");
            Set("selectedSlot", 0); Call("UpdateHeldWeapon"); Call("UpdateBubbleWrapFX");
            Check((bool)type.GetProperty("BubbleWrapActive", Flags).GetValue(game),
                "slot switching preserves active protection");
            Set("selectedSlot", 2); Call("DropSelectedWeapon"); Call("UpdateHeldWeapon");
            Check(inventory[2] == WeaponKind.None && Time.time < state.shieldUntil,
                "discard removes the item without deleting protection already activated");
            state.slowUntil = Time.time + 5;
            Check(state.MoveMultiplier(Time.time) == .6f && Time.time < state.shieldUntil,
                "slow bypasses the shield without consuming it"); state.slowUntil = 0;

            var immune = new DeliveryItemState { shieldUntil = Time.time + 8, stunImmuneUntil = Time.time + 2 };
            Check(!immune.Control(Time.time, Vector3.zero, .8f) && immune.shieldUntil > Time.time &&
                float.IsNegativeInfinity(immune.shieldBrokenAt), "stun immunity does not waste shield protection");
            Check(!immune.Control(Time.time, Vector3.zero) && immune.shieldUntil > Time.time,
                "non-control events do not consume the shield");

            // Live HUD and real scene captures.
            inventory[2] = WeaponKind.BubbleWrap; Set("selectedSlot", 2); Call("UpdateHeldWeapon");
            Call("ActivateBubbleWrap", 0); Call("ActivateBubbleWrap", 1);
            Set("bubbleScreenBlend", 1f); Set("lookPitch", 0f);
            game.enabled = true; yield return null; yield return new WaitForEndOfFrame();
            Capture("bubble-wrap-first-person-fizz-20261013.png"); game.enabled = false;
            var direction = rival.transform.forward + rival.transform.right * .5f;
            direction.y = 0; direction.Normalize();
            Place(player, rival.transform.position + direction * 5.5f);
            player.transform.rotation = Quaternion.LookRotation(-direction);
            state.shieldUntil = 0; Set("bubbleScreenBlend", 0f); Set("lookPitch", 0f);
            Call("PositionCamera"); Call("UpdateBubbleWrapFX");
            game.enabled = true; yield return null; yield return new WaitForEndOfFrame();
            Capture("bubble-wrap-opponent-fizz-20261013.png"); game.enabled = false;

            Call("ActivateBubbleWrap", 0); state.busyUntil = 0;
            Set("captureTarget", 0); Set("captureStarted", Time.time - 1);
            Check(!(bool)Call("ApplyControl", 0, Vector3.right * 18 + Vector3.up * 6, .8f) &&
                state.shieldUntil == 0 && state.shieldBrokenAt == Time.time &&
                !state.Stunned(Time.time) && state.force == Vector3.zero && state.pendingLift == 0,
                "one shared control hit is absorbed without stun, knockback or vertical launch");
            Check((int)Get("captureTarget") == 0, "an absorbed hit preserves existing capture progress");
            Call("UpdateBubbleWrapFX");
            Check(!shields[0].Visible && shields[0].Pop.particleCount == 0,
                "first-person block is shown on screen without world particles clipping the camera");
            game.enabled = true; yield return null; yield return new WaitForEndOfFrame();
            Capture("bubble-wrap-block-screen-20261013.png"); game.enabled = false;
            Check((bool)Call("ApplyControl", 0, Vector3.right * 18, .8f) && state.Stunned(Time.time) &&
                state.force.x > 0, "second hit after the shield pops applies control normally");
            // Finish the local control test before capturing the opponent's pop.
            state = new DeliveryItemState(); Set("playerItems", state); Set("bubbleScreenBlend", 0f);

            states[0] = new DeliveryItemState(); Call("ActivateBubbleWrap", 1); Call("UpdateBubbleWrapFX");
            captures[0] = 1; Call("BeginPlungerPull", 1); Call("UpdateBubbleWrapFX");
            Check(states[0].shieldUntil == 0 && !states[0].Pulled(Time.time) && states[0].force == Vector3.zero &&
                captures[0] == 1, "actual plunger pull is blocked once without cancelling opponent capture");
            Check(shield.Visible && shield.Pop.particleCount > 0 &&
                shield.Shell.sharedMaterial.GetFloat("_Hit") > 0,
                "blocked opponent hit expands the bubble and emits a pop burst");
            int count = shield.Pop.particleCount; Call("UpdateBubbleWrapFX");
            Check(shield.Pop.particleCount == count, "a single block does not emit duplicate pop bursts");
            game.enabled = true; yield return null; yield return new WaitForEndOfFrame();
            Capture("bubble-wrap-opponent-pop-20261013.png"); game.enabled = false;
            Call("BeginPlungerPull", 1);
            Check(states[0].Pulled(Time.time), "next plunger can pull after one protection charge is spent");

            states[0] = new DeliveryItemState { shieldUntil = Time.time + 8 };
            Call("UpdateBubbleWrapFX"); states[0].shieldUntil = Time.time - 1; Call("UpdateBubbleWrapFX");
            Check(!shield.Visible && shield.Pop.particleCount == 0 &&
                float.IsNegativeInfinity(states[0].shieldBrokenAt), "natural expiry hides protection without a hit burst");
            state.shieldUntil = 0; state.shieldBrokenAt = float.NegativeInfinity;
            float timeout = Time.time + 1;
            while ((float)Get("bubbleScreenBlend") > .001f && Time.time < timeout)
            { Call("UpdateBubbleWrapFX"); yield return null; }
            Check((float)Get("bubbleScreenBlend") <= .001f, "screen protection fades away after expiry");
            Call("ActivateBubbleWrap", 0); Call("ActivateBubbleWrap", 1); Set("finished", true); Call("UpdateBubbleWrapFX");
            Check(!(bool)type.GetProperty("BubbleWrapActive", Flags).GetValue(game) && !shield.Visible,
                "round finish removes local and opponent protection displays");
            File.WriteAllLines("art-review/bubble-wrap-effects-check-20261013.txt", report);
            Debug.Log("BUBBLE_WRAP_EFFECTS_OK: " + report.Count + " checks passed.");
        }
        finally
        {
            game.enabled = false;
            Array.Copy(inventorySaved, inventory, inventory.Length); Array.Copy(ammoSaved, ammo, ammo.Length);
            for (int i = 0; i < names.Length; i++) Set(names[i], saved[i]);
            states[0] = rivalStateSaved; captures[0] = captureSaved;
            Place(player, positions[0]); Place(rival, positions[1]);
            for (int i = 0; i < transforms.Length; i++) transforms[i].SetPositionAndRotation(positions[i], rotations[i]);
            for (int i = 0; i < shields.Length; i++) if (shields[i] && shields[i] != existing[i])
            { UnityEngine.Object.Destroy(shields[i].gameObject); shields[i] = existing[i]; }
            game.thirdPersonPreview = third; Call("UpdateHeldWeapon"); Call("UpdateBubbleWrapFX");
            game.enabled = enabled; Application.runInBackground = background;
            EditorApplication.update -= EditorApplication.QueuePlayerLoopUpdate;
        }
    }

    static void Capture(string name)
    {
        var image = ScreenCapture.CaptureScreenshotAsTexture();
        try { Directory.CreateDirectory("art-review"); File.WriteAllBytes("art-review/" + name, image.EncodeToPNG()); }
        finally { UnityEngine.Object.Destroy(image); }
    }

    // Render only a fixed shield at two controlled times, excluding actor/camera movement.
    static bool SurfaceAnimationChanges(MeshRenderer shell)
    {
        var sphere = new GameObject("BubbleSurfaceCheck");
        var cameraObject = new GameObject("BubbleSurfaceCheckCamera");
        var material = new Material(shell.sharedMaterial);
        var target = new RenderTexture(160, 160, 24);
        var image = new Texture2D(160, 160, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            sphere.transform.position = Vector3.up * 500; sphere.transform.localScale = Vector3.one * 3;
            sphere.AddComponent<MeshFilter>().sharedMesh = shell.GetComponent<MeshFilter>().sharedMesh;
            sphere.AddComponent<MeshRenderer>().sharedMaterial = material;
            var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
            camera.transform.position = sphere.transform.position + Vector3.back * 5;
            camera.transform.LookAt(sphere.transform); camera.nearClipPlane = .1f; camera.farClipPlane = 10;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            camera.targetTexture = target;
            material.SetFloat("_Alpha", 1); material.SetFloat("_Hit", 0);
            material.SetFloat("_BubbleTime", 0); camera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 160, 160), 0, 0); image.Apply(); var before = image.GetPixels32();
            material.SetFloat("_BubbleTime", .37f); camera.Render();
            image.ReadPixels(new Rect(0, 0, 160, 160), 0, 0); image.Apply(); var after = image.GetPixels32();
            int changed = 0;
            for (int i = 0; i < before.Length; i++)
                if (Mathf.Abs(before[i].g - after[i].g) > 3 || Mathf.Abs(before[i].b - after[i].b) > 3) changed++;
            return changed > 100;
        }
        finally
        {
            RenderTexture.active = previous; target.Release();
            UnityEngine.Object.DestroyImmediate(cameraObject); UnityEngine.Object.DestroyImmediate(sphere);
            UnityEngine.Object.DestroyImmediate(material); UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
