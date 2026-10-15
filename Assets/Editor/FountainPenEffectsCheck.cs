using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class FountainPenEffectsCheck
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    [MenuItem("Tools/Gameplay/Fountain Pen/Verify Effects")]
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
            "rivalBubblePlaytest", "rivalShoesPlaytest", "started", "finished", "feedback", "fountainThrownAt", "lookPitch" };
        var saved = names.Select(Get).ToArray();
        var inventory = (WeaponKind[])Get("inventory"); var savedInventory = (WeaponKind[])inventory.Clone();
        var ammo = (int[])Get("weaponAmmo"); var savedAmmo = (int[])ammo.Clone();
        var states = (DeliveryItemState[])Get("botItems"); var savedStates = (DeliveryItemState[])states.Clone();
        var captures = (float[])Get("botCaptureStarted"); var savedCaptures = (float[])captures.Clone();
        var player = (CharacterController)Get("player"); var rivals = (CharacterController[])Get("botControllers");
        var cam = (Camera)Get("cam"); var actors = new[] { player }.Concat(rivals.Where(r => r)).ToArray();
        var positions = actors.Select(a => a.transform.position).ToArray();
        var cameraPosition = cam.transform.position; var cameraRotation = cam.transform.rotation;
        bool enabled = game.enabled, third = game.thirdPersonPreview, background = Application.runInBackground;
        var objects = new List<GameObject>(); var report = new List<string>();
        void Check(bool ok, string text) { if (!ok) throw new Exception("FOUNTAIN_PEN: " + text); report.Add("PASS " + text); }
        void Place(CharacterController a, Vector3 p)
        { a.enabled = false; a.transform.position = p; a.enabled = true; Physics.SyncTransforms(); }
        void Reset()
        {
            Set("playerItems", new DeliveryItemState()); states[0] = new DeliveryItemState();
            Set("nextAttackAt", 0f); Set("selectedSlot", 1); inventory[1] = WeaponKind.FountainPen;
            Place(player, new Vector3(0, 150, 0)); Place(rivals[0], new Vector3(0, 150, 12));
            cam.transform.SetPositionAndRotation(new Vector3(0, 150.7f, 0), Quaternion.identity);
            Call("UpdateHeldWeapon"); Call("UpdateFountainInkFX"); Physics.SyncTransforms();
        }
        BrawlShot Launch(Vector3 from, Vector3 direction, int source = 0) =>
            (BrawlShot)Call("LaunchFountainPen", from, direction, 48f, source);
        IEnumerator WaitShot(BrawlShot s)
        {
            float end = Time.time + 2;
            while (s && s.gameObject.activeSelf && Time.time < end) yield return null;
            Check(!s || !s.gameObject.activeSelf, "projectile terminates on collision or range");
        }
        try
        {
            Application.runInBackground = true; EditorApplication.update += EditorApplication.QueuePlayerLoopUpdate;
            yield return null; game.enabled = false; game.thirdPersonPreview = false;
            Set("rivalBubblePlaytest", false); Set("rivalShoesPlaytest", false);
            Set("started", true); Set("finished", false);
            Check((int)WeaponKind.FountainPen == 9 && Enum.GetValues(typeof(WeaponKind)).Length == 10,
                "nine-item catalog retains ID nine and names the new item FountainPen");
            Check(inventory[0] == WeaponKind.Plunger && inventory[1] == WeaponKind.IceCream && inventory[2] == WeaponKind.BubbleWrap && (int)Get("selectedSlot") == 1,
                "startup retains current ice cream test gear before explicitly equipping the fountain pen");
            Check(!(bool)Get("itemPlaytestEnabled"), "fountain pen is usable with other staged items gated");
            Check((string)Call("ItemName", WeaponKind.FountainPen) == "만년필", "HUD uses the fountain pen name");
            var combinedHit = new DeliveryItemHit(Vector3.forward * 18, stunSeconds: .8f, slowSeconds: 2, inkSeconds: 4);
            var packetState = new DeliveryItemState { shieldUntil = 108 };
            Check(packetState.ReceiveHit(100, combinedHit) == DeliveryHitResult.Blocked && packetState.shieldUntil == 0 &&
                packetState.force == Vector3.zero && packetState.stunUntil == 0 && packetState.slowUntil == 0 &&
                packetState.visionBlockedUntil == 0, "generic hit packet blocks every effect atomically without item-kind branches");
            Check(packetState.ReceiveHit(100.1f, combinedHit) == DeliveryHitResult.Applied && packetState.force.z == 18 &&
                packetState.stunUntil > 100.8f && packetState.slowUntil > 102 && packetState.visionBlockedUntil > 104,
                "same generic hit applies all effects after the one shield charge is spent");
            packetState = new DeliveryItemState { shieldUntil = 108, stunImmuneUntil = 102 };
            Check(packetState.ReceiveHit(100, new DeliveryItemHit(stunSeconds: .8f)) == DeliveryHitResult.Ignored &&
                packetState.shieldUntil == 108, "stun immunity and ineffective hits do not consume protection");
            Check(packetState.ReceiveHit(100, new DeliveryItemHit(slowSeconds: 2, shieldable: false)) == DeliveryHitResult.Applied &&
                packetState.shieldUntil == 108 && packetState.slowUntil == 102, "explicit floor-hazard bypass preserves tape slow rules");
            packetState = new DeliveryItemState { shieldUntil = 99 };
            Check(packetState.ReceiveHit(100, new DeliveryItemHit(inkSeconds: 4)) == DeliveryHitResult.Applied &&
                packetState.visionBlockedUntil == 104 && float.IsNegativeInfinity(packetState.shieldBrokenAt),
                "expired shield permits incoming effects without a defensive pop");
            var prefab = Resources.Load<GameObject>("DeliveryItems/FountainPen");
            Check(prefab && prefab.GetComponentsInChildren<Renderer>().Length >= 7 &&
                prefab.GetComponentsInChildren<Collider>().Length == 0, "native gold-nib pen model has no held colliders");
            Check(Resources.Load<Sprite>("UI/InGame/item_FountainPen"), "inventory has the actual pen model icon");
            Reset();
            var held = (DeliveryHeldItem)Get("heldItem");
            Check(held && held.transform.parent.parent == cam.transform, "first-person pen appears on the held camera");
            for (int i = 1; i < actors.Length; i++) Place(actors[i], new Vector3(i * 30, 150, 12));
            Place(rivals[0], new Vector3(0, 150, 12));
            Set("captureTarget", 0); Set("captureStarted", Time.time - 1); int budget = ammo[1];
            var before = UnityEngine.Object.FindObjectsByType<BrawlShot>();
            Call("UseWeapon");
            var shot = UnityEngine.Object.FindObjectsByType<BrawlShot>().Single(s => !before.Contains(s));
            var body = shot.GetComponent<Rigidbody>();
            Check(shot.kind == WeaponKind.FountainPen && !body.useGravity &&
                Mathf.Abs(body.linearVelocity.magnitude - 48) < .01f && shot.maxDistance == 35,
                "real click throws the pen at 48m/s with no gravity and a 35m range");
            Check(Vector3.Dot(shot.transform.forward, body.linearVelocity.normalized) > .999f,
                "gold nib leads the straight projectile");
            Check(shot.GetComponent<TrailRenderer>() && shot.GetComponentsInChildren<Renderer>().Length >= 8,
                "opponents see the physical pen and short ink trail");
            Check(inventory[1] == WeaponKind.FountainPen && ammo[1] == budget && (int)Get("captureTarget") == -1,
                "throw cancels own capture and retains unlimited test inventory");
            Call("UseWeapon"); Check(UnityEngine.Object.FindObjectsByType<BrawlShot>().Length == before.Length + 1,
                "cooldown prevents duplicate throws");
            Call("PoseFountainPen"); Check(!((GameObject)Get("heldWeapon")).activeSelf,
                "hand releases the model during the throw");
            Set("selectedSlot", 2); Call("UpdateHeldWeapon");
            Check(shot && shot.gameObject.activeSelf, "switching slots leaves the thrown pen in flight");
            yield return WaitShot(shot);
            Check(states[0].visionBlockedUntil > Time.time + 3.5f, "actual swept flight directly inks the rival for four seconds");
            var hitFX = rivals[0].GetComponentInChildren<FountainInkHit>();
            Check(hitFX && hitFX.Visible && hitFX.Burst.particleCount >= 40,
                "real hit produces a large 48-droplet burst and visible stains on the opponent");
            Check(hitFX.GetComponentsInChildren<Renderer>().All(r => r.sharedMaterial.shader.isSupported) &&
                !hitFX.GetComponentsInChildren<Collider>().Any(c => c.enabled),
                "ink shader renders with no new gameplay colliders");
            var hitPosition = hitFX.transform.position; Place(rivals[0], rivals[0].transform.position + Vector3.right * 2);
            Call("UpdateFountainInkFX");
            Check(Vector3.Distance(hitFX.transform.position, hitPosition + Vector3.right * 2) < .001f &&
                Mathf.Abs(hitFX.transform.localPosition.y - .7f) < .001f,
                "stains follow the struck floating torso instead of staying on the ground");
            game.FountainPenImpact(rivals[0], rivals[0].transform.position + Vector3.up * .7f, 0);
            Check(rivals[0].GetComponentsInChildren<FountainInkHit>().Length == 1,
                "repeat hit refreshes one reusable actor effect without accumulating stains");
            Check(states[0].force == Vector3.zero && !states[0].Stunned(Time.time) && states[0].CanCapture(Time.time),
                "ink only obstructs vision without knockback, stun, or capture interruption");
            Check(((IList)Get("itemAreas")).Count == 0, "impact leaves no old ground ink cloud");

            Reset();
            Call("ActivateBubbleWrap", 1); Call("UpdateBubbleWrapFX"); captures[0] = Time.time - 1;
            float protectedCapture = captures[0];
            shot = Launch(cam.transform.position + Vector3.forward, Vector3.forward); yield return WaitShot(shot);
            Check(states[0].shieldUntil == 0 && states[0].visionBlockedUntil == 0 && states[0].shieldBrokenAt > 0 &&
                captures[0] == protectedCapture, "real pen impact consumes rival protection once without ink or capture cancellation");
            var shields = (BubbleWrapShield[])Get("bubbleShields");
            Check(shields[1] && shields[1].Visible && shields[1].Pop.particleCount > 0 && !hitFX.Visible,
                "blocked rival hit shows the protective pop and no ink marks");
            shot = Launch(cam.transform.position + Vector3.forward, Vector3.forward); yield return WaitShot(shot);
            Check(states[0].visionBlockedUntil > Time.time + 3.5f && hitFX.Visible,
                "the next real pen hit inks the rival after protection has popped");
            Reset(); Call("ActivateBubbleWrap", 1); Call("UpdateBubbleWrapFX");
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); objects.Add(wall);
            wall.transform.position = new Vector3(0, 150.7f, 5); wall.transform.localScale = new Vector3(3, 3, .02f);
            Physics.SyncTransforms(); shot = Launch(cam.transform.position + Vector3.forward, Vector3.forward);
            yield return WaitShot(shot);
            Check(states[0].visionBlockedUntil == 0 && states[0].shieldUntil > Time.time,
                "thin solid cover blocks a fast pen without consuming the protected target shield");
            Reset(); wall.transform.position = cam.transform.position + Vector3.forward * .3f;
            Physics.SyncTransforms(); before = UnityEngine.Object.FindObjectsByType<BrawlShot>(); Call("UseWeapon");
            Check(UnityEngine.Object.FindObjectsByType<BrawlShot>().Length == before.Length && states[0].visionBlockedUntil == 0,
                "cover between camera and nib stops launch before the pen can spawn through a wall");
            wall.SetActive(false);
            Reset(); Place(rivals[0], new Vector3(4, 150, 12));
            shot = Launch(cam.transform.position + Vector3.forward, Vector3.forward);
            yield return WaitShot(shot);
            Check(states[0].visionBlockedUntil == 0 && ((IList)Get("itemAreas")).Count == 0,
                "misses expire at range without a return flight or nearby area debuff");
            Reset(); Place(rivals[0], new Vector3(4, 150, 16));
            shot = Launch(cam.transform.position + Vector3.forward, Vector3.forward);
            Place(rivals[0], new Vector3(0, 150, 16)); yield return WaitShot(shot);
            Check(states[0].visionBlockedUntil > Time.time + 3.5f, "a moving target entering the real flight path can be hit");
            Reset();
            var own = (DeliveryItemState)Get("playerItems"); own.shieldUntil = Time.time + 8;
            Set("captureTarget", 0); Set("captureStarted", Time.time - 1);
            shot = Launch(new Vector3(0, 150.7f, 11), Vector3.back, 1);
            yield return WaitShot(shot);
            Check(own.visionBlockedUntil == 0 && own.shieldUntil == 0 && own.shieldBrokenAt > 0 &&
                own.CanCapture(Time.time) && (int)Get("captureTarget") == 0,
                "opponent-origin pen is blocked by the player shield before screen ink or capture changes");
            shot = Launch(new Vector3(0, 150.7f, 11), Vector3.back, 1); yield return WaitShot(shot);
            Check(own.visionBlockedUntil > Time.time + 3.5f && states[0].visionBlockedUntil == 0,
                "opponent-origin projectile inks the player while ignoring its shooter");
            Check(own.shieldUntil == 0 && own.CanCapture(Time.time) && own.MoveMultiplier(Time.time) == 1 &&
                (int)Get("captureTarget") == 0, "unshielded ink preserves movement and current capture");
            Check(!player.GetComponentInChildren<FountainInkHit>().Visible, "own first-person hit hides world stains while keeping screen ink");
            float blindEnd = own.visionBlockedUntil;
            yield return new WaitForSeconds(.15f);
            game.FountainPenImpact(player, player.transform.position, 1);
            Check(own.visionBlockedUntil > blindEnd && own.visionBlockedUntil <= Time.time + 4.01f,
                "repeat hit refreshes four seconds without stacking durations");
            Call("EnsureInkSplat"); var ink = (Texture2D)Get("inkSplat");
            Check(ink.GetPixel(128, 128).a > .99f && ink.GetPixel(0, 0).a == 0,
                "organic ink texture blocks the center and leaves transparent outer edges");

            Place(player, positions[0]); Place(rivals[0], positions[0] + player.transform.forward * 5);
            states[0] = new DeliveryItemState(); Set("playerItems", new DeliveryItemState());
            Call("PositionCamera"); Set("lookPitch", 2f);
            cam.transform.LookAt(rivals[0].transform.TransformPoint(rivals[0].center)); Call("UpdateHeldWeapon");
            Call("ActivateBubbleWrap", 1); Call("UpdateBubbleWrapFX");
            shot = Launch(cam.transform.position + cam.transform.forward * .7f,
                (rivals[0].transform.TransformPoint(rivals[0].center) - cam.transform.position).normalized);
            yield return WaitShot(shot);
            Check(states[0].visionBlockedUntil == 0 && states[0].shieldUntil == 0,
                "native map shield-hit capture is a real blocked projectile");
            game.enabled = true; yield return null; yield return new WaitForEndOfFrame();
            SaveCapture("art-review/fountain-pen-aircap-block-20261013.png"); game.enabled = false;
            shot = Launch(cam.transform.position + cam.transform.forward * .7f,
                (rivals[0].transform.TransformPoint(rivals[0].center) - cam.transform.position).normalized);
            yield return WaitShot(shot); yield return new WaitForSeconds(.08f);
            game.enabled = true; yield return null; yield return new WaitForEndOfFrame();
            SaveCapture("art-review/fountain-pen-opponent-burst-20261013.png"); game.enabled = false;
            yield return new WaitForSeconds(.9f); Call("UpdateFountainInkFX");
            Check(hitFX.Visible && !hitFX.GetComponentsInChildren<LineRenderer>().Any(r => r.enabled),
                "impact ripples finish quickly while ink marks remain for the vision obstruction");
            game.enabled = true; yield return null; yield return new WaitForEndOfFrame();
            SaveCapture("art-review/fountain-pen-opponent-stains-20261013.png"); game.enabled = false;
            states[0].visionBlockedUntil = Time.time - .01f; Call("UpdateFountainInkFX");
            Check(!hitFX.Visible, "opponent marks disappear when four-second ink obstruction ends");
            for (int i = 0; i < actors.Length; i++) Place(actors[i], positions[i]);
            cam.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
            Set("playerItems", new DeliveryItemState()); Set("selectedSlot", 1);
            game.thirdPersonPreview = true; Call("UpdateHeldWeapon"); game.enabled = true;
            yield return null; yield return new WaitForEndOfFrame();
            held = (DeliveryHeldItem)Get("heldItem");
            Check(held && Vector3.Distance(held.transform.position, player.transform.position) < 2,
                "third-person pen follows the actual robot hand");
            SaveCapture("art-review/fountain-pen-third-person-20261013.png");
            game.enabled = false; game.thirdPersonPreview = false;
            Call("PositionCamera"); Call("UpdateHeldWeapon");
            Set("fountainThrownAt", float.NegativeInfinity); Call("PoseFountainPen"); game.enabled = true;
            yield return null; yield return new WaitForEndOfFrame();
            SaveCapture("art-review/fountain-pen-held-20261013.png");
            game.enabled = false;
            game.FountainPenImpact(player, player.transform.position + Vector3.up * .7f, 1);
            own = (DeliveryItemState)Get("playerItems"); game.enabled = true;
            yield return null; yield return new WaitForEndOfFrame();
            SaveCapture("art-review/fountain-pen-ink-hit-20261013.png"); game.enabled = false;
            yield return new WaitForSeconds(4.05f);
            Check(Time.time > own.visionBlockedUntil, "vision obstruction naturally expires after four seconds");
            game.enabled = true; yield return null; yield return new WaitForEndOfFrame();
            SaveCapture("art-review/fountain-pen-ink-clear-20261013.png");
            var result = "FOUNTAIN_PEN: " + report.Count + " checks passed\n" + string.Join("\n", report);
            File.WriteAllText("art-review/fountain-pen-check-20261013.txt", result); Debug.Log(result);
        }
        finally
        {
            foreach (var s in UnityEngine.Object.FindObjectsByType<BrawlShot>().Where(s => s.owner == game && s.kind == WeaponKind.FountainPen))
                UnityEngine.Object.Destroy(s.gameObject);
            foreach (var o in objects) if (o) UnityEngine.Object.Destroy(o);
            for (int i = 0; i < names.Length; i++) Set(names[i], saved[i]);
            Array.Copy(savedInventory, inventory, inventory.Length); Array.Copy(savedAmmo, ammo, ammo.Length);
            Array.Copy(savedStates, states, states.Length); Array.Copy(savedCaptures, captures, captures.Length);
            for (int i = 0; i < actors.Length; i++) Place(actors[i], positions[i]);
            cam.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
            game.thirdPersonPreview = third; game.enabled = enabled; Call("UpdateHeldWeapon"); Call("PoseFountainPen"); Call("UpdateFountainInkFX"); Call("UpdateBubbleWrapFX");
            Application.runInBackground = background; EditorApplication.update -= EditorApplication.QueuePlayerLoopUpdate;
        }
    }

    static void SaveCapture(string path)
    {
        var texture = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.Destroy(texture);
    }
}
