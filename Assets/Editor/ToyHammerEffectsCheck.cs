using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class ToyHammerEffectsCheck
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    [MenuItem("Tools/Gameplay/Toy Hammer/Verify Effects")]
    public static void Verify()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Play Mode required.");
        UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>().StartCoroutine(Run());
    }
    static IEnumerator Run()
    {
        var game = UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>(); var type = game.GetType();
        object Get(string n) => type.GetField(n, Flags).GetValue(game);
        void Set(string n, object v) => type.GetField(n, Flags).SetValue(game, v);
        object Call(string n, params object[] a) => type.GetMethod(n, Flags).Invoke(game, a);
        var names = new[] { "playerItems", "selectedSlot", "nextAttackAt", "captureTarget", "captureStarted", "feedback", "lookPitch", "vertical",
            "rivalBubblePlaytest", "rivalShoesPlaytest", "started", "finished", "hammerSlot", "hammerStarted", "hammerHit", "rollUntil" };
        var saved = names.Select(Get).ToArray();
        var inventory = (WeaponKind[])Get("inventory"); var oldInventory = (WeaponKind[])inventory.Clone();
        var ammo = (int[])Get("weaponAmmo"); var oldAmmo = (int[])ammo.Clone();
        var states = (DeliveryItemState[])Get("botItems"); var oldStates = (DeliveryItemState[])states.Clone();
        var captures = (float[])Get("botCaptureStarted"); var oldCaptures = (float[])captures.Clone();
        var falls = (float[])Get("botVertical"); var oldFalls = (float[])falls.Clone();
        var player = (CharacterController)Get("player"); var rivals = (CharacterController[])Get("botControllers");
        var bots = (GameObject[])Get("bots");
        var originalRivals = (CharacterController[])rivals.Clone(); var originalBots = (GameObject[])bots.Clone();
        var actors = new[] { player }.Concat(rivals.Where(r => r)).ToArray();
        var positions = actors.Select(a => a.transform.position).ToArray(); var rotations = actors.Select(a => a.transform.rotation).ToArray();
        var cam = (Camera)Get("cam"); var cameraPosition = cam.transform.position; var cameraRotation = cam.transform.rotation;
        bool enabled = game.enabled, third = game.thirdPersonPreview, background = Application.runInBackground;
        var objects = new List<GameObject>(); var report = new List<string>();
        void Check(bool ok, string text) { if (!ok) throw new Exception("TOY_HAMMER: " + text); report.Add("PASS " + text); }
        void Place(CharacterController actor, Vector3 point)
        { actor.enabled = false; actor.transform.position = point; actor.enabled = true; Physics.SyncTransforms(); }
        void Reset()
        {
            Call("FinishToyHammerSwing"); Set("playerItems", new DeliveryItemState()); states[0] = new DeliveryItemState();
            Set("rollUntil", 0f); Set("nextAttackAt", 0f); Set("selectedSlot", 1); inventory[1] = WeaponKind.ToyHammer;
            Place(player, new Vector3(0, 150, 0)); Place(rivals[0], new Vector3(0, 150, 2.2f));
            cam.transform.SetPositionAndRotation(new Vector3(0, 150.9f, 0), Quaternion.identity);
            Call("UpdateHeldWeapon"); Call("UpdateStunFX");
        }
        void Resolve()
        { Call("UseWeapon"); Set("hammerStarted", Time.time - .19f); Call("UpdateToyHammerSwing"); }
        try
        {
            Application.runInBackground = true; EditorApplication.update += EditorApplication.QueuePlayerLoopUpdate;
            yield return null; game.enabled = false; game.thirdPersonPreview = false;
            Set("rivalBubblePlaytest", false); Set("rivalShoesPlaytest", false); Set("started", true); Set("finished", false);
            Check((int)WeaponKind.ToyHammer == 3 && Enum.GetValues(typeof(WeaponKind)).Length == 10 &&
                !Enum.GetNames(typeof(WeaponKind)).Contains("Megaphone"), "toy hammer replaces megaphone ID three without changing the nine-item catalog");
            Check(inventory[1] == WeaponKind.IceCream && (int)Get("selectedSlot") == 1, "startup selects ice cream in slot two");
            Check((string)Call("ItemName", WeaponKind.ToyHammer) == "뿅망치", "HUD and parcel use the toy hammer name");
            var prefab = Resources.Load<GameObject>("DeliveryItems/ToyHammer");
            Check(prefab && prefab.GetComponentsInChildren<Renderer>().Length > 15 && prefab.GetComponentsInChildren<Collider>().Length == 0,
                "ribbed toy hammer model is visible and adds no held collision geometry");
            Check(Resources.Load<Sprite>("UI/InGame/item_ToyHammer"), "inventory uses a rendered hammer icon");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); objects.Add(floor);
            floor.transform.position = new Vector3(0, 148.9f, 0); floor.transform.localScale = new Vector3(60, .2f, 60);
            Reset(); captures[0] = Time.time - 1;
            Set("captureTarget", 0); Set("captureStarted", Time.time - 1);
            var held = (DeliveryHeldItem)Get("heldItem"); var rest = held.transform.localRotation;
            var faces = held.GetComponentsInChildren<Transform>().Where(t => t.name == "FaceButton").ToArray();
            Check(faces.Length == 2 && Mathf.Abs(Vector3.Dot((faces[1].position - faces[0].position).normalized, cam.transform.forward)) > .94f,
                "first-person striking faces point toward the owner and opponent rather than sideways");
            Call("UseWeapon");
            Check((int)Get("hammerSlot") == 1 && !states[0].Stunned(Time.time), "left click starts a windup without an instant stun");
            Check((int)Get("captureTarget") == -1 && ((DeliveryItemState)Get("playerItems")).busyUntil > Time.time,
                "swing interrupts own capture and occupies the hand for its animation");
            Check(inventory[1] == WeaponKind.ToyHammer && (float)Get("nextAttackAt") > Time.time + .6f,
                "hammer remains unlimited and has a reuse cooldown");
            float started = (float)Get("hammerStarted"); Call("UseWeapon");
            Check((float)Get("hammerStarted") == started, "cooldown/active swing prevent duplicate attacks");
            yield return new WaitForSeconds(.19f); Call("UpdateToyHammerSwing"); Set("hammerStarted", Time.time - .19f); Call("PoseToyHammer");
            Check(states[0].Stunned(Time.time) && states[0].stunUntil > Time.time + 1.1f, "nearby enemy receives 1.2 seconds of stun at the hit frame");
            Check(states[0].force == Vector3.zero && states[0].pendingLift == 0 && captures[0] == 0 && !states[0].CanCapture(Time.time),
                "stun cancels target capture without knockback, damage or death");
            Check(Quaternion.Angle(rest, held.transform.localRotation) > 30, "first-person model visibly swings through impact");
            var fx = rivals[0].GetComponentInChildren<DeliveryStunEffect>(); yield return null;
            Check(fx && fx.transform.localPosition.y > 1.3f && fx.GetComponentsInChildren<LineRenderer>().Length == 2 &&
                fx.GetComponentsInChildren<Collider>().Length == 0, "dizzy rings and orbiting dots follow the floating head without hitboxes");
            float stunEnd = states[0].stunUntil; Call("UpdateToyHammerSwing");
            Check(states[0].stunUntil == stunEnd, "one swing resolves exactly one hit");
            var frozenPosition = rivals[0].transform.position; Call("UpdateBots");
            Check(Vector2.Distance(new Vector2(frozenPosition.x, frozenPosition.z), new Vector2(rivals[0].transform.position.x, rivals[0].transform.position.z)) < .01f,
                "existing bot movement stops while stunned");
            Set("hammerStarted", Time.time - .5f); Call("UpdateToyHammerSwing"); Call("PoseToyHammer");
            Check((int)Get("hammerSlot") == -1 && Quaternion.Angle(rest, held.transform.localRotation) < .1f, "swing returns to its saved hand pose");
            states[0].stunUntil = Time.time - .01f; states[0].shieldUntil = Time.time + 8; Call("UpdateStunFX");
            Check(!fx.gameObject.activeSelf, "dizzy feedback hides when stun ends");
            Set("nextAttackAt", 0f); Resolve();
            Check(states[0].stunUntil < Time.time && states[0].shieldUntil > Time.time, "two-second stun immunity rejects the hit without spending aircap");
            Reset(); states[0].shieldUntil = Time.time + 8; captures[0] = 1; Resolve();
            Check(states[0].shieldUntil == 0 && !states[0].Stunned(Time.time) && captures[0] == 1,
                "common aircap pipeline blocks the first hammer hit without capture loss or dizzy VFX");
            Call("FinishToyHammerSwing"); Set("nextAttackAt", 0f); Resolve();
            Check(states[0].Stunned(Time.time), "next hammer hit works after aircap has been consumed");
            Reset(); Place(rivals[0], new Vector3(0, 150, 4)); Resolve(); Check(states[0].Stunned(Time.time), "increased 3.5-metre reach hits an enemy beyond the former 2.5-metre sweep");
            Reset(); Place(rivals[0], new Vector3(0, 150, 4.6f)); Resolve(); Check(!states[0].Stunned(Time.time), "distant enemy is outside the increased melee reach");
            Reset(); Place(rivals[0], new Vector3(0, 150, -2)); Resolve(); Check(!states[0].Stunned(Time.time), "enemy behind the attacker is not struck");
            Reset(); Place(rivals[0], new Vector3(.83f, 150, 1.8f)); Resolve(); Check(states[0].Stunned(Time.time), "hammer head width counts a graze against the full visible body capsule");
            Reset(); Place(rivals[0], new Vector3(1.1f, 150, 1.8f)); Resolve(); Check(!states[0].Stunned(Time.time), "enemy outside the hammer width is not struck");
            Reset();
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); objects.Add(wall); wall.transform.position = new Vector3(0, 150.7f, 1);
            wall.transform.localScale = new Vector3(4, 3, .1f); Physics.SyncTransforms(); Resolve();
            Check(!states[0].Stunned(Time.time), "thin solid wall stops a swing before an enemy");
            wall.SetActive(false); Reset();
            var extra = new GameObject("HammerNearestTarget"); objects.Add(extra);
            var second = extra.AddComponent<CharacterController>(); second.height = 1.4f; second.radius = .55f; second.center = Vector3.up * .7f;
            rivals[1] = second; bots[1] = extra; states[1] = new DeliveryItemState(); Place(second, new Vector3(0, 150, 2.7f));
            Place(rivals[0], new Vector3(0, 150, 1.5f)); Resolve();
            Check(states[0].Stunned(Time.time) && !states[1].Stunned(Time.time), "only the nearest enemy is hit once");
            rivals[1] = originalRivals[1]; bots[1] = originalBots[1]; states[1] = oldStates[1]; extra.SetActive(false);
            Reset(); game.thirdPersonPreview = true; Call("PositionCamera"); Call("UpdateHeldWeapon");
            var thirdFaces = ((DeliveryHeldItem)Get("heldItem")).GetComponentsInChildren<Transform>().Where(t => t.name == "FaceButton").ToArray();
            Check(Mathf.Abs(Vector3.Dot((thirdFaces[1].position - thirdFaces[0].position).normalized, player.transform.forward)) > .94f,
                "third-person striking faces also align with the owner-to-opponent direction");
            Resolve();
            Check(states[0].Stunned(Time.time), "third-person camera distance does not shorten the hammer's melee reach");
            game.thirdPersonPreview = false;
            Reset(); Call("UseWeapon"); ((DeliveryItemState)Get("playerItems")).stunUntil = Time.time + 1;
            Set("hammerStarted", Time.time - .19f); Call("UpdateToyHammerSwing");
            Check((int)Get("hammerSlot") == -1 && !states[0].Stunned(Time.time), "being stunned during windup cancels the pending strike");
            Reset(); Call("UseWeapon"); Call("DropSelectedWeapon"); Call("UpdateToyHammerSwing"); Call("UpdateHeldWeapon");
            Check(inventory[1] == WeaponKind.None && (int)Get("hammerSlot") == -1 && !states[0].Stunned(Time.time), "G deletes hammer and cancels its windup");
            Reset(); Call("UseWeapon"); Set("selectedSlot", 2); Call("UpdateToyHammerSwing");
            Check((int)Get("hammerSlot") == -1 && !states[0].Stunned(Time.time), "forced item change cancels a stale hammer strike");

            floor.SetActive(false); Call("FinishToyHammerSwing");
            for (int i = 0; i < actors.Length; i++) Place(actors[i], positions[i]);
            Set("playerItems", new DeliveryItemState()); states[0] = new DeliveryItemState();
            Set("selectedSlot", 1); inventory[1] = WeaponKind.ToyHammer; Set("lookPitch", 12f); Set("nextAttackAt", 0f);
            Call("PositionCamera"); Call("UpdateHeldWeapon"); game.enabled = true;
            yield return null; yield return new WaitForEndOfFrame(); Save("art-review/toy-hammer-held-20261014.png"); game.enabled = false;
            Place(rivals[0], player.transform.position + player.transform.forward * 2);
            cam.transform.LookAt(rivals[0].bounds.center); Call("UseWeapon"); Set("hammerStarted", Time.time - .18f);
            Call("UpdateToyHammerSwing"); Call("PoseToyHammer");
            Check(states[0].Stunned(Time.time), "native map swing actually stuns the nearby opponent");
            Place(player, player.transform.position - player.transform.forward * .8f);
            game.enabled = true; yield return null; yield return new WaitForEndOfFrame(); Save("art-review/toy-hammer-hit-20261014.png"); game.enabled = false;
            Call("FinishToyHammerSwing"); Set("nextAttackAt", 0f); states[0] = new DeliveryItemState();
            game.thirdPersonPreview = true; Call("PositionCamera"); Call("UpdateHeldWeapon"); Call("UseWeapon"); Set("hammerStarted", Time.time - .18f); Call("PoseToyHammer");
            Check(Vector3.Distance(((DeliveryHeldItem)Get("heldItem")).transform.position, player.transform.position) < 2, "third-person hammer swing follows the actual robot hand");
            game.enabled = true; yield return null; yield return new WaitForEndOfFrame(); Save("art-review/toy-hammer-third-person-20261014.png"); game.enabled = false;
            Call("FinishToyHammerSwing"); game.thirdPersonPreview = false; Call("PositionCamera"); Call("UpdateHeldWeapon");
            game.ApplyItemHit(0, new DeliveryItemHit(stunSeconds: 1.2f)); Call("UpdateStunFX");
            Check(!((DeliveryItemState)Get("playerItems")).CanCapture(Time.time), "the shared incoming stun also blocks local movement actions and capture");
            game.enabled = true; yield return null; yield return new WaitForEndOfFrame(); Save("art-review/toy-hammer-stunned-screen-20261014.png"); game.enabled = false;
            string result = "TOY_HAMMER: " + report.Count + " checks passed\n" + string.Join("\n", report);
            File.WriteAllText("art-review/toy-hammer-check-20261014.txt", result); Debug.Log(result);
        }
        finally
        {
            Call("FinishToyHammerSwing"); Array.Copy(originalRivals, rivals, rivals.Length); Array.Copy(originalBots, bots, bots.Length);
            foreach (var o in objects) if (o) UnityEngine.Object.Destroy(o);
            for (int i = 0; i < names.Length; i++) Set(names[i], saved[i]);
            Array.Copy(oldInventory, inventory, inventory.Length); Array.Copy(oldAmmo, ammo, ammo.Length);
            Array.Copy(oldStates, states, states.Length); Array.Copy(oldCaptures, captures, captures.Length); Array.Copy(oldFalls, falls, falls.Length);
            for (int i = 0; i < actors.Length; i++) { Place(actors[i], positions[i]); actors[i].transform.rotation = rotations[i]; }
            cam.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
            game.thirdPersonPreview = third; game.enabled = enabled; Call("UpdateHeldWeapon"); Call("PoseToyHammer"); Call("UpdateStunFX"); Call("UpdateBubbleWrapFX");
            Application.runInBackground = background; EditorApplication.update -= EditorApplication.QueuePlayerLoopUpdate;
        }
    }
    static void Save(string path)
    { var image = ScreenCapture.CaptureScreenshotAsTexture(); File.WriteAllBytes(path, image.EncodeToPNG()); UnityEngine.Object.Destroy(image); }
}
