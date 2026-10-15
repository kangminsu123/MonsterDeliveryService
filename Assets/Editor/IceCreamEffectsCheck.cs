using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class IceCreamEffectsCheck
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    const string Folder = "Docs/ChangeRecords/20261015";
    [MenuItem("Tools/Gameplay/IceCream/Verify Effects")]
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
        var names = new[] { "playerItems", "selectedSlot", "nextAttackAt", "captureTarget", "captureStarted", "feedback", "lookPitch", "vertical", "rollUntil",
            "rivalBubblePlaytest", "rivalShoesPlaytest", "started", "finished", "iceCreamThrownAt" };
        var saved = names.Select(Get).ToArray();
        var inventory = (WeaponKind[])Get("inventory"); var oldInventory = (WeaponKind[])inventory.Clone();
        var ammo = (int[])Get("weaponAmmo"); var oldAmmo = (int[])ammo.Clone();
        var states = (DeliveryItemState[])Get("botItems"); var oldStates = (DeliveryItemState[])states.Clone();
        var captures = (float[])Get("botCaptureStarted"); var oldCaptures = (float[])captures.Clone();
        var player = (CharacterController)Get("player"); var rivals = (CharacterController[])Get("botControllers");
        var actors = new[] { player }.Concat(rivals.Where(r => r)).ToArray();
        var positions = actors.Select(a => a.transform.position).ToArray(); var rotations = actors.Select(a => a.transform.rotation).ToArray();
        var cam = (Camera)Get("cam"); var cameraPosition = cam.transform.position; var cameraRotation = cam.transform.rotation;
        bool enabled = game.enabled, third = game.thirdPersonPreview, background = Application.runInBackground;
        var objects = new List<GameObject>(); var report = new List<string>(); bool passed = false;
        void Check(bool ok, string text) { if (!ok) throw new Exception("ICE_CREAM: " + text); report.Add("PASS " + text); }
        void Place(CharacterController actor, Vector3 point)
        { actor.enabled = false; actor.transform.position = point; actor.enabled = true; Physics.SyncTransforms(); }
        void Reset()
        {
            foreach (var shot in UnityEngine.Object.FindObjectsByType<BrawlShot>()) if (shot.owner == game) { shot.gameObject.SetActive(false); UnityEngine.Object.Destroy(shot.gameObject); }
            Set("playerItems", new DeliveryItemState()); states[0] = new DeliveryItemState();
            Set("nextAttackAt", 0f); Set("selectedSlot", 1); inventory[1] = WeaponKind.IceCream; Set("rollUntil", 0f);
            Place(player, new Vector3(0, 150, 0)); Place(rivals[0], new Vector3(0, 150, 10));
            cam.transform.SetPositionAndRotation(new Vector3(0, 150.7f, 0), Quaternion.identity);
            Call("UpdateHeldWeapon"); Call("UpdateIceCreamFX"); Call("UpdateStunFX"); Call("UpdateBubbleWrapFX"); Physics.SyncTransforms();
        }
        BrawlShot Launch(Vector3 from, Vector3 velocity, int source = 0) => (BrawlShot)Call("LaunchIceCream", from, velocity, source);
        IEnumerator WaitShot(BrawlShot shot)
        {
            float end = Time.time + 3.5f;
            while (shot && shot.gameObject.activeSelf && Time.time < end) yield return null;
            Check(!shot || !shot.gameObject.activeSelf, "projectile terminates on actual collision or safety lifetime");
        }
        try
        {
            Directory.CreateDirectory(Folder + "/images"); Directory.CreateDirectory(Folder + "/evidence");
            Application.runInBackground = true; EditorApplication.update += EditorApplication.QueuePlayerLoopUpdate;
            yield return null; game.enabled = false; game.thirdPersonPreview = false;
            Set("rivalBubblePlaytest", false); Set("rivalShoesPlaytest", false); Set("started", true); Set("finished", false);
            Check((int)WeaponKind.IceCream == 5 && Enum.GetValues(typeof(WeaponKind)).Length == 10 && !Enum.GetNames(typeof(WeaponKind)).Contains("CordlessFan"), "ice cream replaces fan at ID5; catalog stays at nine items");
            Check((string)Call("ItemName", WeaponKind.IceCream) == "아이스크림", "HUD displays ice cream name");
            var prefab = Resources.Load<GameObject>("DeliveryItems/IceCream");
            Check(prefab && prefab.GetComponent<DeliveryHeldItem>() && prefab.GetComponentsInChildren<Renderer>().Length >= 10 && !prefab.GetComponentsInChildren<Collider>().Any(), "strawberry cone is a reusable collider-free held model");
            Check(Resources.Load<Sprite>("UI/InGame/item_IceCream"), "inventory sprite is rendered from ice cream model");
            Reset(); var own = (DeliveryItemState)Get("playerItems");
            Set("captureTarget", 0); Set("captureStarted", Time.time - 1); int budget = ammo[1];
            Call("UseWeapon");
            var shot = UnityEngine.Object.FindObjectsByType<BrawlShot>().Single(s => s.gameObject.activeSelf && s.kind == WeaponKind.IceCream);
            Check(shot.GetComponent<Rigidbody>().useGravity && Mathf.Abs(shot.GetComponent<Rigidbody>().linearVelocity.z - 18) < .01f, "native use launches gravity projectile at 18m/s even with rollout gate false");
            Check(shot.GetComponent<TrailRenderer>() && shot.GetComponent<SphereCollider>().radius == .18f, "thrown model has frost trail and swept physical collision");
            Check((int)Get("captureTarget") == -1 && inventory[1] == WeaponKind.IceCream && ammo[1] == budget, "throw cancels own capture and retains unlimited inventory");
            Call("UseWeapon"); Check(UnityEngine.Object.FindObjectsByType<BrawlShot>().Count(s => s.gameObject.activeSelf) == 1, "cooldown prevents duplicate throws on repeated input");
            Call("PoseIceCreamThrow"); Check(!((GameObject)Get("heldWeapon")).activeSelf, "held cone leaves the hand during throw");
            Set("selectedSlot", 2); Call("UpdateHeldWeapon"); Check(shot && shot.gameObject.activeSelf, "slot change keeps already thrown ice cream alive");
            captures[0] = Time.time - 1; yield return WaitShot(shot);
            Check(states[0].Frozen(Time.time) && states[0].stunUntil - Time.time > .85f && states[0].stunUntil - Time.time <= 1, "real ballistic hit freezes target for one second");
            Check(captures[0] == 0 && !states[0].CanCapture(Time.time) && states[0].force == Vector3.zero, "freeze cancels rival capture without damage or knockback");
            Call("UpdateIceCreamFX"); Call("UpdateStunFX"); yield return null;
            var fx = rivals[0].GetComponentInChildren<IceCreamFreezeEffect>();
            Check(fx && fx.GetComponentsInChildren<Renderer>().All(r => r.sharedMaterial.shader.isSupported) && !fx.GetComponentsInChildren<Collider>().Any(), "visible blue ice shell and crystals render without extra hitboxes");
            var meshes = fx.GetComponentsInChildren<MeshFilter>();
            Check(meshes.Length == 13 && meshes.All(m => m.sharedMesh.name == "PointedHexagonalIce") &&
                !fx.GetComponentsInChildren<Transform>().Any(t => t.name == "FrozenIceBlock"), "pointed six-sided core and twelve crystals replace all box geometry");
            var crystal = meshes[0].sharedMesh;
            Check(crystal.vertices.Select((v, i) => Vector3.Dot(v, crystal.normals[i])).All(dot => dot > 0),
                "crystal faces point outwards and render with back-face culling");
            var visual = ((DeliveryRobotVisual[])Get("botVisuals"))[0];
            Check(visual.model.GetComponent<Animator>().speed == 0, "freeze also pauses floating robot animation");
            Check(!rivals[0].GetComponentsInChildren<DeliveryStunEffect>().Any(d => d.gameObject.activeSelf), "ice freeze replaces yellow hammer dizziness feedback");
            float end = states[0].stunUntil; yield return new WaitForSeconds(1.05f); Call("UpdateIceCreamFX");
            Check(!states[0].Frozen(Time.time) && states[0].CanCapture(Time.time) && !fx.gameObject.activeSelf && visual.model.GetComponent<Animator>().speed == 1, "one-second timer expires and animation/actions resume");
            states[0].shieldUntil = Time.time + 8; game.IceCreamImpact(rivals[0], rivals[0].bounds.center, 0);
            Check(states[0].stunUntil == end && states[0].shieldUntil > Time.time, "shared two-second stun immunity ignores repeat freeze without spending aircap");
            Reset(); states[0].shieldUntil = Time.time + 8; captures[0] = 1;
            shot = Launch(new Vector3(0, 150.7f, 1), new Vector3(0, 2.4f, 18)); yield return WaitShot(shot);
            Check(!states[0].Frozen(Time.time) && states[0].shieldUntil == 0 && states[0].shieldBrokenAt > 0 && captures[0] == 1, "actual projectile spends aircap once with no freeze or capture loss");
            shot = Launch(new Vector3(0, 150.7f, 1), new Vector3(0, 2.4f, 18)); yield return WaitShot(shot);
            Check(states[0].Frozen(Time.time), "next actual projectile freezes after aircap is consumed");
            Reset(); var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); objects.Add(wall);
            wall.transform.position = new Vector3(0, 150.7f, 5); wall.transform.localScale = new Vector3(3, 3, .02f); Physics.SyncTransforms();
            shot = Launch(new Vector3(0, 150.7f, 1), new Vector3(0, 2.4f, 18)); yield return WaitShot(shot);
            Check(!states[0].Frozen(Time.time), "thin wall blocks real projectile before target");
            Reset(); wall.transform.position = cam.transform.position + Vector3.forward * .3f; Physics.SyncTransforms(); Call("UseWeapon");
            Check(!UnityEngine.Object.FindObjectsByType<BrawlShot>().Any(s => s.gameObject.activeSelf), "close wall prevents spawning through cover"); wall.SetActive(false);
            Reset(); Place(rivals[0], new Vector3(4, 150, 10));
            shot = Launch(new Vector3(0, 150.7f, 1), new Vector3(0, 2.4f, 18)); yield return WaitShot(shot);
            Check(!states[0].Frozen(Time.time) && ((IList)Get("itemAreas")).Count == 0, "miss has no homing, return, or freezing floor area");
            Reset(); own = (DeliveryItemState)Get("playerItems"); Set("captureTarget", 0); Set("captureStarted", Time.time - 1);
            shot = Launch(new Vector3(0, 150.7f, 9), new Vector3(0, 2.4f, -18), 1); yield return WaitShot(shot);
            Check(own.Frozen(Time.time) && !own.CanCapture(Time.time) && (int)Get("captureTarget") == -1, "opponent-origin actual throw freezes local player and resets capture");
            Set("nextAttackAt", 0f); Call("UseWeapon");
            Check(!UnityEngine.Object.FindObjectsByType<BrawlShot>().Any(s => s.gameObject.activeSelf), "frozen player cannot use an item");
            // Native map captures use explicit test state, without moving saved scene objects.
            Reset(); Place(player, positions[0]); player.transform.rotation = rotations[0];
            Set("lookPitch", 5f); Call("PositionCamera"); Call("UpdateHeldWeapon"); Set("iceCreamThrownAt", -10f); Call("PoseIceCreamThrow");
            Place(rivals[0], player.transform.position + player.transform.forward * 4); Call("UpdateIceCreamFX");
            game.enabled = true; yield return null; yield return new WaitForEndOfFrame(); Save("spiky_held"); game.enabled = false;
            game.IceCreamImpact(rivals[0], rivals[0].bounds.center, 0); Call("UpdateIceCreamFX"); Call("UpdateStunFX");
            game.enabled = true; yield return null; yield return new WaitForEndOfFrame(); Save("spiky_opponent"); game.enabled = false;
            Check(states[0].Frozen(Time.time), "native map opponent capture records a live freeze");
            Set("playerItems", new DeliveryItemState()); game.IceCreamImpact(player, player.bounds.center, 1); Call("UpdateStunFX");
            game.enabled = true; yield return null; yield return new WaitForEndOfFrame(); Save("spiky_local"); game.enabled = false;
            Check(((DeliveryItemState)Get("playerItems")).Frozen(Time.time), "native local capture records frost overlay and countdown");
            // Test and capture environment marks using actual authored map surfaces.
            Set("playerItems", new DeliveryItemState()); states[0] = new DeliveryItemState(); Call("UpdateIceCreamFX");
            var heldWeapon = (GameObject)Get("heldWeapon"); if (heldWeapon) heldWeapon.SetActive(false);
            SurfaceInkMark Mark(Collider surface) => surface.GetComponentsInChildren<SurfaceInkMark>().LastOrDefault(m => m.name == "IceCreamSurfaceMark");
            bool Conforms(SurfaceInkMark mark, Collider surface)
            {
                var mesh = mark.GetComponent<MeshFilter>().sharedMesh;
                return mesh.triangles.Distinct().All(i => Vector3.Distance(surface.ClosestPoint(mark.transform.TransformPoint(mesh.vertices[i])), mark.transform.TransformPoint(mesh.vertices[i])) < .02f);
            }
            var fence = GameObject.Find("Layer_05_Yards").GetComponentsInChildren<BoxCollider>()
                .First(c => c.name == "FenceCollision" && Vector3.Distance(c.bounds.center, positions[0]) > 10);
            RaycastHit face = default; bool found = false;
            foreach (var direction in new[] { Vector3.forward, Vector3.back, Vector3.right, Vector3.left })
                if (fence.Raycast(new Ray(fence.bounds.center - direction * 50, direction), out face, 100)) { found = true; break; }
            Check(found, "authored map fence provides the actual collision face");
            cam.transform.position = face.point + face.normal * 1.7f + Vector3.up * .15f; cam.transform.LookAt(face.point);
            shot = Launch(face.point + face.normal * 1.1f, -face.normal * 18); yield return WaitShot(shot);
            var wallMark = Mark(fence);
            Check(wallMark && wallMark.Paint && Conforms(wallMark, fence), "actual ice cream collision leaves a fitted mark on the authored wall");
            var block = new MaterialPropertyBlock(); wallMark.Paint.GetPropertyBlock(block);
            Check(block.GetColor("_Color").r > .9f && block.GetColor("_Color").b > .5f && !wallMark.GetComponentsInChildren<Collider>().Any(),
                "strawberry-coloured surface mark adds no gameplay collision");
            yield return new WaitForSeconds(.6f); Check(wallMark && wallMark.Opacity == 1, "ice cream surface mark remains fully visible during its hold");
            yield return new WaitForEndOfFrame(); Save("surface_wall");
            yield return new WaitForSeconds(2.85f);
            Check(wallMark && wallMark.Opacity > 0 && wallMark.Opacity < 1, "wall mark fades after three seconds rather than disappearing at once");
            yield return new WaitForEndOfFrame(); Save("surface_wall_fade");
            yield return new WaitForSeconds(.7f); Check(!wallMark, "surface mark and fitted mesh are removed after four seconds");
            var groundProbe = positions[0] + rotations[0] * Vector3.right * 2 + Vector3.up * 5;
            Check(Physics.Raycast(groundProbe, Vector3.down, out var ground, 12, ~0, QueryTriggerInteraction.Ignore), "authored map provides the floor for impact verification");
            cam.transform.position = ground.point + ground.normal * 2 + Vector3.back * 1.2f; cam.transform.LookAt(ground.point);
            shot = Launch(ground.point + ground.normal * 1.1f, -ground.normal * 18); yield return WaitShot(shot);
            var floorMark = Mark(ground.collider);
            Check(floorMark && floorMark.Paint && Conforms(floorMark, ground.collider), "actual downward throw leaves a fitted mark on the ground");
            Check(((IList)Get("itemAreas")).Count == 0 && !((DeliveryItemState)Get("playerItems")).Frozen(Time.time), "environment marks are visual only without freezing floor hazards");
            yield return new WaitForEndOfFrame(); Save("surface_floor");
            var tilted = GameObject.CreatePrimitive(PrimitiveType.Cube); objects.Add(tilted);
            tilted.transform.SetPositionAndRotation(new Vector3(0, 150, 30), Quaternion.Euler(25, 15, 0)); tilted.transform.localScale = new Vector3(3, .1f, 3); Physics.SyncTransforms();
            shot = Launch(tilted.transform.position + tilted.transform.up * 1.1f, -tilted.transform.up * 18); yield return WaitShot(shot);
            var tiltedMark = Mark(tilted.GetComponent<Collider>());
            Check(tiltedMark && Conforms(tiltedMark, tilted.GetComponent<Collider>()), "ice cream footprint also fits a sloped nonuniformly scaled surface");
            var previous = tiltedMark.transform.position; tilted.transform.position += Vector3.right;
            Check(Vector3.Distance(tiltedMark.transform.position, previous + Vector3.right) < .001f, "surface mark follows a moving struck object");
            int markCount = UnityEngine.Object.FindObjectsByType<SurfaceInkMark>().Count(m => m.name == "IceCreamSurfaceMark");
            states[0] = new DeliveryItemState { shieldUntil = Time.time + 8 };
            game.IceCreamImpact(rivals[0], rivals[0].bounds.center, 0, Vector3.up);
            Check(!states[0].Frozen(Time.time) && UnityEngine.Object.FindObjectsByType<SurfaceInkMark>().Count(m => m.name == "IceCreamSurfaceMark") == markCount,
                "aircap-blocked actor hit creates no environment splat");
            Call("ClearIceCreamMarks"); yield return null;
            Check(!UnityEngine.Object.FindObjectsByType<SurfaceInkMark>().Any(m => m.name == "IceCreamSurfaceMark"), "new-round cleanup removes ice cream marks without touching other item marks");
            passed = true;
        }
        finally
        {
            foreach (var shot in UnityEngine.Object.FindObjectsByType<BrawlShot>()) if (shot.owner == game) UnityEngine.Object.Destroy(shot.gameObject);
            foreach (var o in objects) if (o) UnityEngine.Object.Destroy(o);
            Call("ClearIceCreamMarks");
            for (int i = 0; i < names.Length; i++) Set(names[i], saved[i]);
            Array.Copy(oldInventory, inventory, inventory.Length); Array.Copy(oldAmmo, ammo, ammo.Length); Array.Copy(oldStates, states, states.Length); Array.Copy(oldCaptures, captures, captures.Length);
            for (int i = 0; i < actors.Length; i++) { Place(actors[i], positions[i]); actors[i].transform.rotation = rotations[i]; }
            cam.transform.SetPositionAndRotation(cameraPosition, cameraRotation); game.thirdPersonPreview = third; game.enabled = enabled;
            Call("UpdateHeldWeapon"); Call("PoseIceCreamThrow"); Call("UpdateIceCreamFX"); Call("UpdateStunFX"); Call("UpdateBubbleWrapFX");
            Application.runInBackground = background; EditorApplication.update -= EditorApplication.QueuePlayerLoopUpdate;
            string result = "ICE_CREAM: " + report.Count + " checks " + (passed ? "passed" : "INCOMPLETE") + "\n" + string.Join("\n", report);
            File.WriteAllText(Folder + "/evidence/ice-cream-check.txt", result); Debug.Log(result);
        }
    }
    static void Save(string name)
    {
        var image = ScreenCapture.CaptureScreenshotAsTexture(); File.WriteAllBytes(Folder + "/images/20261015_icecream_" + name + ".png", image.EncodeToPNG()); UnityEngine.Object.Destroy(image);
    }
}
