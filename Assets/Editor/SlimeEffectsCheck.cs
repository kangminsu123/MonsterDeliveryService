using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class SlimeEffectsCheck
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    [MenuItem("Tools/Gameplay/Slime/Verify Effects")]
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
        var names = new[] { "playerItems", "selectedSlot", "nextAttackAt", "captureTarget", "captureStarted", "feedback",
            "rivalBubblePlaytest", "rivalShoesPlaytest", "started", "finished", "slimeThrownAt", "lookPitch", "vertical" };
        var saved = names.Select(Get).ToArray();
        var inventory = (WeaponKind[])Get("inventory"); var oldInventory = (WeaponKind[])inventory.Clone();
        var ammo = (int[])Get("weaponAmmo"); var oldAmmo = (int[])ammo.Clone();
        var states = (DeliveryItemState[])Get("botItems"); var oldStates = (DeliveryItemState[])states.Clone();
        var player = (CharacterController)Get("player"); var rivals = (CharacterController[])Get("botControllers");
        var actors = new[] { player }.Concat(rivals.Where(r => r)).ToArray();
        var positions = actors.Select(a => a.transform.position).ToArray();
        var rotations = actors.Select(a => a.transform.rotation).ToArray();
        var cam = (Camera)Get("cam"); var cameraPosition = cam.transform.position; var cameraRotation = cam.transform.rotation;
        bool enabled = game.enabled, third = game.thirdPersonPreview, background = Application.runInBackground;
        var report = new List<string>(); var objects = new List<GameObject>();
        void Check(bool ok, string text) { if (!ok) throw new Exception("SLIME: " + text); report.Add("PASS " + text); }
        void Place(CharacterController actor, Vector3 p)
        { actor.enabled = false; actor.transform.position = p; actor.enabled = true; Physics.SyncTransforms(); }
        void Reset()
        {
            Call("ClearSlimePuddles"); Set("playerItems", new DeliveryItemState()); states[0] = new DeliveryItemState();
            Place(player, new Vector3(-15, 150, 0)); Place(rivals[0], new Vector3(0, 150, 8));
            Set("nextAttackAt", 0f); Set("selectedSlot", 1); inventory[1] = WeaponKind.Slime;
            cam.transform.SetPositionAndRotation(new Vector3(0, 150.7f, 0), Quaternion.identity);
            Call("UpdateHeldWeapon"); Call("UpdateSlimeFX");
        }
        BrawlShot Launch(Vector3 p, Vector3 v, int source = 0) => (BrawlShot)Call("LaunchSlime", p, v, source);
        IEnumerator WaitShot(BrawlShot shot)
        {
            float deadline = Time.time + 7;
            while (shot && shot.gameObject.activeSelf && Time.time < deadline) yield return null;
            Check(!shot || !shot.gameObject.activeSelf, "thrown slime ends on actual impact or below the map and never returns");
        }
        GameObject Box(string name, Vector3 p, Vector3 s)
        {
            var o = GameObject.CreatePrimitive(PrimitiveType.Cube); o.name = name; o.transform.position = p; o.transform.localScale = s;
            objects.Add(o); Physics.SyncTransforms(); return o;
        }
        try
        {
            Application.runInBackground = true; EditorApplication.update += EditorApplication.QueuePlayerLoopUpdate;
            yield return null; game.enabled = false; game.thirdPersonPreview = false;
            Set("rivalBubblePlaytest", false); Set("rivalShoesPlaytest", false); Set("started", true); Set("finished", false);
            Check((int)WeaponKind.Slime == 4 && Enum.GetValues(typeof(WeaponKind)).Length == 10, "slime replaces tape ID four in the nine-item catalog");
            Check(inventory[1] == WeaponKind.IceCream && (int)Get("selectedSlot") == 1, "startup retains current ice cream test gear before explicitly equipping slime");
            Check((string)Call("ItemName", WeaponKind.Slime) == "슬라임", "HUD/parcel name is slime");
            var prefab = Resources.Load<GameObject>("DeliveryItems/Slime");
            Check(prefab && prefab.GetComponentsInChildren<Renderer>().Length >= 5 && prefab.GetComponentsInChildren<Collider>().Length == 0, "wet green held model contains geometry and no colliders");
            Check(Resources.Load<Sprite>("UI/InGame/item_Slime"), "slime has a native rendered inventory icon");
            var floor = Box("SlimeCheckFloor", new Vector3(0, 148.9f, 0), new Vector3(120, .2f, 120));
            Reset(); yield return null;
            Check((DeliveryHeldItem)Get("heldItem"), "selected slime has a visible first-person model");
            Call("UseWeapon");
            var fired = UnityEngine.Object.FindObjectsByType<BrawlShot>().Single(s => s.kind == WeaponKind.Slime);
            Check(fired && fired.GetComponent<Rigidbody>().useGravity && fired.GetComponent<Rigidbody>().linearVelocity.z > 15, "left click launches a visible physical projectile with gravity");
            Check(fired.GetComponentsInChildren<Renderer>().Any(r => r.enabled), "flying slime remains visible");
            Check((float)Get("nextAttackAt") > Time.time && inventory[1] == WeaponKind.Slime, "throw cooldown applies and unlimited test inventory is retained");
            Call("UseWeapon"); Check(UnityEngine.Object.FindObjectsByType<BrawlShot>().Count(s => s.kind == WeaponKind.Slime) == 1, "cooldown prevents a duplicate projectile");
            UnityEngine.Object.Destroy(fired.gameObject); yield return null;
            Reset();
            var closeCover = Box("SlimeCloseCover", new Vector3(.24f, 150.7f, .45f), new Vector3(.12f, 1, .1f));
            Call("UseWeapon");
            Check(!UnityEngine.Object.FindObjectsByType<BrawlShot>().Any(s => s.kind == WeaponKind.Slime), "slime-width near cover prevents spawning through a wall beside the centre aim line");
            UnityEngine.Object.Destroy(closeCover); yield return null;
            Reset(); yield return null;
            var highShot = Launch(new Vector3(-8, 150.7f, -12), Vector3.up * 21);
            Check(float.IsPositiveInfinity(highShot.maxDistance), "slime has no arbitrary forty-metre flight limit");
            yield return new WaitForSeconds(3.2f);
            Check(highShot && highShot.gameObject.activeSelf && highShot.transform.position.y > 155,
                "near-vertical throw remains visible in the air after three seconds");
            yield return WaitShot(highShot);
            Check(UnityEngine.Object.FindObjectsByType<SlimePuddle>().Length == 1,
                "high throw descends and creates its pool only after real ground contact");
            Reset(); yield return null;
            var longShot = Launch(new Vector3(-25, 150.7f, -12), new Vector3(12.728f, 15.728f, 0));
            yield return WaitShot(longShot);
            Check(UnityEngine.Object.FindObjectsByType<SlimePuddle>().Length == 1 &&
                UnityEngine.Object.FindObjectsByType<SlimePuddle>()[0].transform.position.x > 15,
                "angled throw travels over forty metres and still lands with a puddle");
            Reset(); yield return null;
            var pole = Box("SlimeTallPole", new Vector3(0, 155, 4), new Vector3(.25f, 12, .25f));
            var poleShot = Launch(new Vector3(0, 158, 0), Vector3.forward * 18);
            yield return WaitShot(poleShot);
            var poleSplat = pole.transform.Find("SlimeSurfaceSplat");
            Check(poleSplat && poleSplat.GetComponentsInChildren<Renderer>().Length >= 5 && poleSplat.GetComponentsInChildren<Collider>().Length == 0,
                "a high pole impact leaves visible sticky slime on the struck object without another hitbox");
            var poolsForPoleCheck = UnityEngine.Object.FindObjectsByType<SlimePuddle>();
            Check(poolsForPoleCheck.Length == 1 && Mathf.Abs(poolsForPoleCheck[0].transform.position.y - 149) < .05f,
                "pole impact more than four metres above the ground still creates the floor slow area");
            yield return new WaitForSeconds(.9f);
            Check(poleSplat && poleSplat.gameObject.activeSelf, "pole slime remains visible after the short impact burst ends");
            UnityEngine.Object.Destroy(pole); Reset(); yield return null;
            var lostShot = Launch(new Vector3(1500, -19.5f, 1500), Vector3.down * 5);
            yield return WaitShot(lostShot);
            Check(!lostShot || !lostShot.gameObject.activeSelf, "a missed slime falling below the playable map is cleaned up");
            Reset();
            var direct = Launch(new Vector3(0, 150.7f, 0), new Vector3(0, 2, 18));
            yield return WaitShot(direct);
            Check(states[0].slowUntil > Time.time + 2.5f && Mathf.Approximately(states[0].MoveMultiplier(Time.time), .6f), "real projectile hits rival and applies three seconds of 40 percent slow");
            Check(states[0].CanCapture(Time.time), "slow does not stun or cancel capture");
            Call("UpdateSlimeFX"); yield return null;
            var effect = rivals[0].GetComponentInChildren<SlimeSlowEffect>();
            Check(effect && effect.transform.parent == rivals[0].transform && effect.GetComponentsInChildren<Renderer>().Length >= 16, "slow visibly coats floating torso with animated drips");
            Check(effect.GetComponentsInChildren<Collider>().Length == 0, "slow VFX do not add hitboxes");
            var pools = UnityEngine.Object.FindObjectsByType<SlimePuddle>();
            Check(pools.Length == 1 && Mathf.Abs(pools[0].transform.position.y - 149) < .05f, "direct impact leaves a ground-aligned puddle below the hovering body");
            Call("UpdateItemAreas"); Check(states[0].slowUntil > Time.time + 2.4f, "floor refresh never shortens the stronger direct-hit duration");
            Reset(); yield return null;
            states[0].shieldUntil = Time.time + 8;
            var blocked = Launch(new Vector3(0, 150.7f, 0), new Vector3(0, 2, 18)); yield return WaitShot(blocked);
            Check(states[0].shieldUntil == 0 && states[0].slowUntil == 0, "common shield pipeline blocks direct slime and consumes exactly one shield");
            Check(UnityEngine.Object.FindObjectsByType<SlimePuddle>().Length == 0, "blocked slime does not reapply slow through an immediate puddle");
            Reset(); yield return null;
            Place(player, new Vector3(0, 150, 8)); Place(rivals[0], new Vector3(-15, 150, 0));
            var incoming = Launch(new Vector3(0, 150.7f, 0), new Vector3(0, 2, 18), 1); yield return WaitShot(incoming);
            Check(((DeliveryItemState)Get("playerItems")).slowUntil > Time.time + 2.5f, "opponent-source physical slime projectile also slows the local player");
            Reset(); yield return null;
            var wall = Box("SlimeCheckWall", new Vector3(0, 150, 4), new Vector3(4, 4, .1f));
            var wallShot = Launch(new Vector3(0, 150.7f, 0), new Vector3(0, 2, 18)); yield return WaitShot(wallShot);
            Check(states[0].slowUntil == 0, "thin wall stops the projectile before the rival");
            Check(UnityEngine.Object.FindObjectsByType<SlimePuddle>().Length == 1, "wall impact splashes down onto nearby ground");
            UnityEngine.Object.Destroy(wall); Reset(); yield return null;
            Call("AddSlimePuddle", new Vector3(0, 149, 0));
            Place(rivals[0], new Vector3(2.8f, 150, 0)); states[0].shieldUntil = Time.time + 8;
            Call("UpdateItemAreas");
            Check(states[0].slowUntil > Time.time && states[0].shieldUntil > Time.time, "three-metre ground hazard slows an inside actor without spending a shield");
            Place(rivals[0], new Vector3(3.2f, 150, 0)); states[0] = new DeliveryItemState(); Call("UpdateItemAreas");
            Check(states[0].slowUntil == 0, "outside the three-metre pool is not slowed");
            Place(rivals[0], new Vector3(0, 153, 0)); Call("UpdateItemAreas"); Check(states[0].slowUntil == 0, "another floor above the puddle is not affected");
            Place(rivals[0], new Vector3(2, 150, 0));
            wall = Box("SlimeAreaCover", new Vector3(1, 149.5f, 0), new Vector3(.1f, 2, 4));
            Call("UpdateItemAreas"); Check(states[0].slowUntil == 0, "solid cover separates the ground slow area");
            UnityEngine.Object.Destroy(wall); yield return null;
            Place(player, new Vector3(0, 150, 0)); Call("UpdateItemAreas");
            Check(((DeliveryItemState)Get("playerItems")).MoveMultiplier(Time.time) == .6f, "thrower can also be slowed by standing in the goo");
            Call("UpdateSlimeFX"); Check(!player.GetComponentInChildren<SlimeSlowEffect>(true).gameObject.activeSelf, "own torso goo is hidden in first person to keep the view clear");
            Place(player, new Vector3(-15, 150, 0)); yield return new WaitForSeconds(.35f); Call("UpdateSlimeFX");
            Check(((DeliveryItemState)Get("playerItems")).MoveMultiplier(Time.time) == 1, "ground slow clears shortly after leaving the puddle");
            yield return new WaitForSeconds(4.8f); Call("UpdateItemAreas");
            Check(UnityEngine.Object.FindObjectsByType<SlimePuddle>().Length == 0 && ((IList)Get("itemAreas")).Count == 0, "five-second puddle expires and removes render mesh and hazard state");

            // Native Game-view captures in the authored street, with normal gameplay HUD.
            floor.SetActive(false); Call("ClearSlimePuddles");
            var lamp = GameObject.Find("Layer_09_StreetLighting").GetComponentsInChildren<Collider>()
                .Where(c => c.bounds.size.y > 2).OrderBy(c => c.bounds.size.y).Last();
            var lampAim = lamp.bounds.center + Vector3.up * (lamp.bounds.extents.y * .65f);
            var lampOrigin = lampAim + Vector3.right * (lamp.bounds.extents.x + 3);
            Check(lamp.Raycast(new Ray(lampOrigin, Vector3.left), out var lampHit, 10), "authored street lamp exposes a real projectile collision surface");
            var realLampShot = Launch(lampHit.point + Vector3.right * 2, Vector3.left * 18);
            yield return WaitShot(realLampShot);
            Check(lamp.transform.Find("SlimeSurfaceSplat") && UnityEngine.Object.FindObjectsByType<SlimePuddle>().Length > 0,
                "real map lamp collision retains both a green impact splat and a ground puddle");
            cam.transform.SetPositionAndRotation(lampHit.point + Vector3.right * 4 + Vector3.up, Quaternion.LookRotation(-Vector3.right - Vector3.up * .15f));
            yield return null; yield return new WaitForEndOfFrame(); Save("art-review/slime-lamp-fixed-20261014.png");
            Call("ClearSlimePuddles");
            for (int i = 0; i < actors.Length; i++) Place(actors[i], positions[i]);
            Set("playerItems", new DeliveryItemState()); states[0] = new DeliveryItemState();
            Set("slimeThrownAt", float.NegativeInfinity); Set("lookPitch", 12f);
            Call("PositionCamera"); Call("UpdateHeldWeapon"); game.enabled = true;
            yield return null; yield return new WaitForEndOfFrame(); Save("art-review/slime-held-20261014.png"); game.enabled = false;
            var forward = player.transform.forward;
            Place(rivals[0], player.transform.position + forward * 5.5f); rivals[0].transform.LookAt(player.transform.position);
            game.SlimeImpact(rivals[0], rivals[0].bounds.center, 0, -forward);
            game.enabled = true; yield return null; yield return new WaitForEndOfFrame();
            Save("art-review/slime-opponent-20261014.png"); game.enabled = false;
            game.SlimeImpact(player, player.bounds.center, 1, -forward);
            game.enabled = true; yield return null; yield return new WaitForEndOfFrame();
            Save("art-review/slime-screen-20261014.png"); game.enabled = false;
            Call("ClearSlimePuddles"); Set("playerItems", new DeliveryItemState()); states[0] = new DeliveryItemState(); Call("UpdateSlimeFX");
            Check(!effect || !effect.gameObject.activeSelf, "expired/cleared slow hides opponent goo");
            game.thirdPersonPreview = true; Call("UpdateHeldWeapon");
            var held = (DeliveryHeldItem)Get("heldItem");
            Check(held && Vector3.Distance(held.transform.position, player.transform.position) < 2, "third-person slime follows the robot hand");
            game.thirdPersonPreview = false; inventory[1] = WeaponKind.BubbleWrap; Call("UpdateHeldWeapon");
            Check(!(GameObject)Get("heldWeapon"), "switching to aircap removes the held slime model");
            inventory[1] = WeaponKind.Slime; Call("ClearSlot", 1); Call("UpdateHeldWeapon");
            Check(inventory[1] == WeaponKind.None && !(GameObject)Get("heldWeapon"), "discard permanently clears the slime slot and held model");
            string result = "SLIME: " + report.Count + " checks passed\n" + string.Join("\n", report);
            File.WriteAllText("art-review/slime-check-20261014.txt", result); Debug.Log(result);
        }
        finally
        {
            Call("ClearSlimePuddles");
            foreach (var shot in UnityEngine.Object.FindObjectsByType<BrawlShot>().Where(s => s.owner == game && s.kind == WeaponKind.Slime)) UnityEngine.Object.Destroy(shot.gameObject);
            foreach (var o in objects) if (o) UnityEngine.Object.Destroy(o);
            for (int i = 0; i < names.Length; i++) Set(names[i], saved[i]);
            Array.Copy(oldInventory, inventory, inventory.Length); Array.Copy(oldAmmo, ammo, ammo.Length); Array.Copy(oldStates, states, states.Length);
            for (int i = 0; i < actors.Length; i++) { Place(actors[i], positions[i]); actors[i].transform.rotation = rotations[i]; }
            cam.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
            game.thirdPersonPreview = third; game.enabled = enabled; Call("UpdateHeldWeapon"); Call("UpdateSlimeFX"); Call("PoseSlimeThrow");
            Application.runInBackground = background; EditorApplication.update -= EditorApplication.QueuePlayerLoopUpdate;
        }
    }
    static void Save(string path)
    {
        var image = ScreenCapture.CaptureScreenshotAsTexture(); File.WriteAllBytes(path, image.EncodeToPNG()); UnityEngine.Object.Destroy(image);
    }
}
