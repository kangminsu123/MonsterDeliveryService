using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Focused Editor check of the real use path, local screen cue and opponent world cue.
public static class RunningShoesEffectsCheck
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    [MenuItem("Tools/Gameplay/Running Shoes/Verify Effects")]
    public static void Verify()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Play Mode required.");
        EditorApplication.isPaused = false;
        UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>().StartCoroutine(Run());
    }

    static IEnumerator Run()
    {
        var game = UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>();
        var type = game.GetType();
        object Get(string n) => type.GetField(n, Flags).GetValue(game);
        void Set(string n, object v) => type.GetField(n, Flags).SetValue(game, v);
        object Call(string n, params object[] a) => type.GetMethod(n, Flags).Invoke(game, a);
        var names = new[] { "playerItems", "selectedSlot", "nextAttackAt", "captureTarget", "captureStarted",
            "runningScreenBlend", "rivalShoesPlaytest", "nextRivalShoesTest", "started", "finished", "lookPitch" };
        var saved = names.Select(Get).ToArray();
        var inventory = (WeaponKind[])Get("inventory");
        var ammo = (int[])Get("weaponAmmo");
        var inventorySaved = (WeaponKind[])inventory.Clone();
        var ammoSaved = (int[])ammo.Clone();
        var states = (DeliveryItemState[])Get("botItems");
        var stateSaved = states[0];
        var player = (CharacterController)Get("player");
        var rival = ((CharacterController[])Get("botControllers"))[0];
        var cam = (Camera)Get("cam");
        var transforms = new[] { player.transform, rival.transform, cam.transform };
        var positions = transforms.Select(t => t.position).ToArray();
        var rotations = transforms.Select(t => t.rotation).ToArray();
        bool enabled = game.enabled, background = Application.runInBackground, third = game.thirdPersonPreview;
        float fov = cam.fieldOfView;
        var auras = (RunningShoesAura[])Get("runningAuras");
        var existingAuras = (RunningShoesAura[])auras.Clone();
        var report = new List<string>();
        void Check(bool ok, string detail)
        {
            if (!ok) throw new Exception("RUNNING_SHOES: " + detail);
            report.Add("PASS " + detail);
        }
        void Place(CharacterController actor, Vector3 p)
        {
            actor.enabled = false; actor.transform.position = p; actor.enabled = true;
            Physics.SyncTransforms();
        }
        try
        {
            Application.runInBackground = true;
            EditorApplication.update += EditorApplication.QueuePlayerLoopUpdate;
            yield return null;
            game.enabled = false; game.thirdPersonPreview = false;
            Set("rivalShoesPlaytest", false); Set("started", true); Set("finished", false);
            Set("playerItems", new DeliveryItemState()); states[0] = new DeliveryItemState();
            Set("selectedSlot", 1); Set("nextAttackAt", 0f);
            inventory[1] = WeaponKind.RunningShoes;
            Call("UpdateHeldWeapon");
            Check(Enum.GetValues(typeof(WeaponKind)).Length == 10, "catalog has nine items plus empty hands");
            Check(!(GameObject)Get("heldWeapon") && !(DeliveryHeldItem)Get("heldItem"),
                "selecting running shoes displays no held shoe model");
            Check(!(bool)type.GetProperty("RunningShoesActive", Flags).GetValue(game),
                "selection alone does not activate the buff");
            Set("captureTarget", 0); Set("captureStarted", Time.time - 1);
            Call("UseWeapon");
            var state = (DeliveryItemState)Get("playerItems");
            Check(Mathf.Abs(state.speedUntil - Time.time - 5) < .01f && Mathf.Abs(state.MoveMultiplier(Time.time) - 1.5f) < .001f,
                "real left-click use grants exactly five seconds at +50 percent speed");
            Check((int)Get("captureTarget") == -1 && inventory[1] == WeaponKind.RunningShoes,
                "use cancels capture and keeps unlimited item in its slot");
            float until = state.speedUntil;
            Call("UseWeapon");
            Check(state.speedUntil == until, "cooldown prevents immediate reactivation");
            state.speedUntil = Time.time + 2; Set("nextAttackAt", 0f); Call("UseWeapon");
            Check(Mathf.Abs(state.speedUntil - Time.time - 5) < .01f && state.MoveMultiplier(Time.time) == 1.5f,
                "reuse refreshes five seconds without stacking speed");
            Call("UpdateRunningShoesFX");
            Check((float)Get("runningScreenBlend") > 0 && (!auras[0] || !auras[0].Visible),
                "first person activates screen wind and hides own world particles");
            Call("ActivateRunningShoes", 1); Call("UpdateRunningShoesFX");
            Check(auras[1] && auras[1].Visible && auras[1].Particles.isPlaying,
                "opponent use activates its world wind particles");
            var aura = auras[1]; var renderer = aura.Particles.GetComponent<ParticleSystemRenderer>();
            Check(aura.Particles.transform.localPosition.y > .2f &&
                aura.Particles.transform.position.y > rival.transform.position.y,
                "opponent wind begins at the floating lower torso rather than below the body");
            Check(RunningShoesAura.WindColor.b > RunningShoesAura.WindColor.g && RunningShoesAura.WindColor.g < .85f,
                "booster uses sky blue rather than green cyan");
            Call("EnsureRunningShoesVignette");
            var vignette = (Texture2D)Get("runningScreenVignette");
            Check(vignette.GetPixel(0, 64).a > .95f && vignette.GetPixel(127, 64).a > .95f &&
                vignette.GetPixel(64, 0).a > .95f && vignette.GetPixel(64, 127).a > .95f &&
                vignette.GetPixel(64, 64).a == 0,
                "soft screen glow surrounds all four edges while the centre remains clear");
            var pulseMethod = type.GetMethod("RunningShoesPulse", BindingFlags.Static | BindingFlags.NonPublic);
            float Pulse(float time) => (float)pulseMethod.Invoke(null, new object[] { time });
            Check(Pulse(.12f / 1.1f) > .95f && Pulse(.3f / 1.1f) > .6f && Pulse(.7f / 1.1f) < .01f,
                "screen glow has a strong beat, softer second beat and rest");
            var velocity = aura.Particles.velocityOverLifetime;
            Check(velocity.x.mode == velocity.y.mode && velocity.y.mode == velocity.z.mode,
                "all particle velocity axes use the same curve mode without Unity warnings");
            Check(renderer.sharedMaterial && renderer.sharedMaterial.shader.isSupported &&
                renderer.sharedMaterial.shader.name == "MonsterDelivery/RunningShoesAura",
                "world particles use the supported additive wind shader");
            aura.Particles.Simulate(.7f, true, true, false);
            Check(aura.Particles.particleCount > 0, "opponent wind actually emits visible particles");
            var particles = new ParticleSystem.Particle[aura.Particles.main.maxParticles];
            int count = aura.Particles.GetParticles(particles);
            float radius = 0;
            for (int i = 0; i < count; i++) radius = Mathf.Max(radius,
                new Vector2(particles[i].position.x, particles[i].position.y).magnitude);
            Check(count > 8 && radius > .9f && states[0].MoveMultiplier(Time.time) == 1.5f,
                "opponent has +50 percent speed and fine wind accents across a broad radius");
            Check(aura.Ribbons.Length == 6 && aura.Ribbons.All(r => r.positionCount == 32 &&
                r.sharedMaterial == renderer.sharedMaterial && !r.useWorldSpace),
                "three layered curved wind ribbons wrap the floating body with glow and thin cores");
            var ribbonPoint = aura.Ribbons[0].GetPosition(12);
            yield return null;
            yield return null; // Observe LateUpdate from a frame after the activation frame.
            Check(Vector3.Distance(ribbonPoint, aura.Ribbons[0].GetPosition(12)) > .001f,
                "wind ribbons rotate in actual play frames instead of staying as static rings");
            var beforeTrail = rival.transform.position;
            for (int i = 0; i < 3; i++) { Place(rival, rival.transform.position + Vector3.right * .2f); yield return null; }
            Check(aura.Trails.All(t => t.positionCount > 1 && t.time < .3f),
                "moving opponent leaves two short body-height slipstreams");
            Place(rival, beforeTrail);
            aura.Particles.Play(true);
            game.thirdPersonPreview = true; Call("UpdateRunningShoesFX");
            Check(auras[0] && auras[0].Visible && !(GameObject)Get("heldWeapon"),
                "third person shows own wind without adding shoes");
            game.thirdPersonPreview = false; Call("UpdateRunningShoesFX");
            Check(!auras[0].Visible && aura.Visible, "switching to first person hides only own world effect");
            Set("selectedSlot", 0); Call("UpdateHeldWeapon"); Call("UpdateRunningShoesFX");
            Check((bool)type.GetProperty("RunningShoesActive", Flags).GetValue(game) && aura.Visible,
                "switching slots preserves the active player and opponent buffs");
            Set("selectedSlot", 1); Call("DropSelectedWeapon"); Call("UpdateHeldWeapon");
            Check(inventory[1] == WeaponKind.None && state.MoveMultiplier(Time.time) == 1.5f,
                "discard removes the item while the already activated buff finishes normally");
            state.slowUntil = Time.time + 5;
            Check(Mathf.Abs(state.MoveMultiplier(Time.time) - .9f) < .001f,
                "slow and running shoes multiply instead of overwriting each other");
            state.slowUntil = 0;

            // Native Game View captures include the live HUD, unlike Camera.Render.
            inventory[1] = WeaponKind.RunningShoes; Set("selectedSlot", 1); Call("UpdateHeldWeapon");
            state.speedUntil = Time.time + 5; states[0].speedUntil = Time.time + 5;
            Set("runningScreenBlend", 1f); Set("lookPitch", 0f);
            game.enabled = true;
            yield return null;
            // Capture near the strong beat so the new perimeter glow is reviewable.
            while ((float)pulseMethod.Invoke(null, new object[] { Time.time }) < .85f) yield return null;
            yield return new WaitForEndOfFrame();
            Capture("running-shoes-first-person-refined-20261013.png");
            game.enabled = false;

            var direction = rival.transform.forward + rival.transform.right * .5f;
            direction.y = 0; direction.Normalize();
            Place(player, rival.transform.position + direction * 4);
            player.transform.rotation = Quaternion.LookRotation(-direction);
            Set("lookPitch", 0f); state.speedUntil = 0; Set("runningScreenBlend", 0f);
            states[0].speedUntil = Time.time + 5;
            Call("PositionCamera"); Call("UpdateRunningShoesFX");
            aura.Particles.Simulate(.7f, true, true, false); aura.Particles.Play(true);
            game.enabled = true;
            yield return null;
            yield return new WaitForEndOfFrame();
            Capture("running-shoes-opponent-ribbons-20261013.png");
            game.enabled = false;

            state.speedUntil = Time.time - 1; states[0].speedUntil = Time.time - 1;
            Call("UpdateRunningShoesFX");
            Check(state.MoveMultiplier(Time.time) == 1 && !aura.Visible && aura.Particles.particleCount == 0,
                "expiry restores speed and stops and clears opponent particles");
            Check(aura.Trails.All(t => t.positionCount == 0) && aura.Ribbons.All(r => !r.gameObject.activeInHierarchy),
                "expiry clears slipstreams and hides every wind ribbon");
            float fadeTimeout = Time.time + 1;
            while ((float)Get("runningScreenBlend") > .001f && Time.time < fadeTimeout)
            { Call("UpdateRunningShoesFX"); yield return null; }
            Check((float)Get("runningScreenBlend") <= .001f, "screen wind fades out after expiry");
            state.speedUntil = Time.time + 5; states[0].speedUntil = Time.time + 5;
            Set("finished", true); Call("UpdateRunningShoesFX");
            Check(!(bool)type.GetProperty("RunningShoesActive", Flags).GetValue(game) && !aura.Visible,
                "round finish suppresses player screen effect and opponent wind");
            File.WriteAllLines("art-review/running-shoes-effects-check-20261013.txt", report);
            Debug.Log("RUNNING_SHOES_EFFECTS_OK: " + report.Count + " checks passed.");
        }
        finally
        {
            game.enabled = false;
            Array.Copy(inventorySaved, inventory, inventory.Length); Array.Copy(ammoSaved, ammo, ammo.Length);
            for (int i = 0; i < names.Length; i++) Set(names[i], saved[i]);
            states[0] = stateSaved;
            Place(player, positions[0]); Place(rival, positions[1]);
            for (int i = 0; i < transforms.Length; i++) transforms[i].SetPositionAndRotation(positions[i], rotations[i]);
            for (int i = 0; i < auras.Length; i++) if (auras[i] && auras[i] != existingAuras[i])
            { UnityEngine.Object.Destroy(auras[i].gameObject); auras[i] = existingAuras[i]; }
            game.thirdPersonPreview = third; cam.fieldOfView = fov;
            Call("UpdateHeldWeapon"); Call("UpdateRunningShoesFX");
            game.enabled = enabled;
            Application.runInBackground = background;
            EditorApplication.update -= EditorApplication.QueuePlayerLoopUpdate;
        }
    }

    static void Capture(string name)
    {
        var image = ScreenCapture.CaptureScreenshotAsTexture();
        try { Directory.CreateDirectory("art-review"); File.WriteAllBytes("art-review/" + name, image.EncodeToPNG()); }
        finally { UnityEngine.Object.Destroy(image); }
    }
}
