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

public static class PlungerEffectsCheck
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    [MenuItem("Tools/Gameplay/Plunger/Verify Throw")]
    public static void Verify()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Play Mode required.");
        EditorApplication.isPaused = false;
        UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>().StartCoroutine(Run());
    }
    [MenuItem("Tools/Gameplay/Plunger/Capture Ready")]
    public static void CaptureReady() => UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>().StartCoroutine(Ready());
    static IEnumerator Ready()
    {
        bool background = Application.runInBackground;
        Application.runInBackground = true; EditorApplication.update += EditorApplication.QueuePlayerLoopUpdate;
        try
        {
            yield return new WaitForSeconds(.3f); yield return new WaitForEndOfFrame();
            Capture("plunger-ready-20261012.png"); Debug.Log("PLUNGER_CAPTURE_READY");
        }
        finally { Application.runInBackground = background; EditorApplication.update -= EditorApplication.QueuePlayerLoopUpdate; }
    }
    static IEnumerator Run()
    {
        var game = UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>(); var type = game.GetType();
        object Get(string n) => type.GetField(n, Flags).GetValue(game);
        void Set(string n, object v) => type.GetField(n, Flags).SetValue(game, v);
        object Call(string n, params object[] a) => type.GetMethod(n, Flags).Invoke(game, a);
        var excluded = new[] { "heldWeapon", "heldItem", "heldKind", "heldInFirstPerson", "heldCamera", "flyingPlunger" };
        var fields = type.GetFields(Flags).Where(f => !f.IsInitOnly && !excluded.Contains(f.Name)).ToArray();
        var values = fields.Select(f => f.GetValue(game)).ToArray();
        var arrays = type.GetFields(Flags).Where(f => f.FieldType.IsArray).Select(f => (array: (Array)f.GetValue(game), saved: (Array)((Array)f.GetValue(game)).Clone())).ToArray();
        var player = (CharacterController)Get("player"); var rival = ((CharacterController[])Get("botControllers"))[0];
        var cam = (Camera)Get("cam"); var visual = (DeliveryRobotVisual)Get("playerVisual");
        var transforms = new[] { player.transform, rival.transform, cam.transform };
        var positions = transforms.Select(t => t.position).ToArray(); var rotations = transforms.Select(t => t.rotation).ToArray();
        var inventory = (WeaponKind[])Get("inventory"); var ammo = (int[])Get("weaponAmmo"); var states = (DeliveryItemState[])Get("botItems");
        var captures = (float[])Get("botCaptureStarted");
        var keyboardBefore = Keyboard.current; var mouseBefore = Mouse.current;
        var keyboard = InputSystem.AddDevice<Keyboard>(); var mouse = InputSystem.AddDevice<Mouse>();
        bool enabled = game.enabled, background = Application.runInBackground, third = game.thirdPersonPreview;
        var temporary = new List<GameObject>(); var report = new List<string>();
        bool returnSpeedChecked = false;
        void Check(bool ok, string detail) { if (!ok) throw new Exception("PLUNGER: " + detail); report.Add("PASS " + detail); }
        void Place(CharacterController actor, Vector3 p) { actor.enabled = false; actor.transform.position = p; actor.enabled = true; actor.Move(Vector3.zero); Physics.SyncTransforms(); }
        void Input(bool click = false, params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            var state = new MouseState(); if (click) state = state.WithButton(MouseButton.Left);
            InputSystem.QueueStateEvent(mouse, state); InputSystem.Update();
        }
        void Equip()
        {
            Call("FinishPlungerThrow"); for (int i = 0; i < 3; i++) Call("ClearSlot", i);
            Call("AddWeapon", WeaponKind.Plunger); Set("nextAttackAt", 0f); Set("rollUntil", 0f);
            Set("playerItems", new DeliveryItemState()); states[0] = new DeliveryItemState();
            Call("UpdateHeldWeapon"); Input();
        }
        void Shot() { Input(); Input(true); Call("UpdatePlayer", keyboard); Input(); }
        void Launch() { Set("plungerStarted", Time.time - .13f); Call("UpdatePlungerThrow"); }
        GameObject Flight() => (GameObject)Get("flyingPlunger");
        bool AtFront() => Vector3.Distance(rival.transform.position,
            player.transform.position + player.transform.forward * (player.radius + rival.radius + .5f)) < .09f;
        IEnumerator Return()
        {
            float timeout = Time.time + 4;
            while (((int)Get("plungerSlot") >= 0 || states[0].Pulled(Time.time)) && Time.time < timeout)
            {
                var flight = Flight(); var position = flight ? flight.transform.position : Vector3.zero;
                bool measure = !returnSpeedChecked && flight && (bool)Get("plungerReturning") &&
                    Vector3.Distance(position, ((DeliveryHeldItem)Get("heldItem")).transform.position) > 44 * Time.deltaTime + .1f;
                Call("UpdatePlungerThrow");
                if (measure && Flight())
                {
                    Check(Mathf.Abs(Vector3.Distance(position, Flight().transform.position) - 44 * Time.deltaTime) < .01f, "return flight moves at 44 metres per second");
                    returnSpeedChecked = true;
                }
                Call("UpdateBots");
                yield return null;
            }
            Check((int)Get("plungerSlot") == -1 && !Flight(), "flight finishes and leaves no thrown model");
            Call("UpdateHeldWeapon");
        }
        try
        {
            Application.runInBackground = true; EditorApplication.update += EditorApplication.QueuePlayerLoopUpdate;
            game.enabled = false; game.thirdPersonPreview = false; Set("lookPitch", 0f); Set("vertical", -2f);
            Set("rivalBubblePlaytest", false); Set("rivalShoesPlaytest", false);
            Check(inventory[0] == WeaponKind.Plunger &&
                (string)Call("ItemRemainingText", 0) == "무제한", "startup retains slot 1 plunger with unlimited uses");
            Check(Enum.GetValues(typeof(WeaponKind)).Length == 10 && !Enum.GetNames(typeof(WeaponKind)).Contains("MiniTrampoline"), "active catalog contains nine items and no trampoline");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.position = new Vector3(0, 149.95f, 0); floor.transform.localScale = new Vector3(80, .1f, 80); temporary.Add(floor);
            Place(player, new Vector3(0, 151, 0)); player.transform.rotation = Quaternion.identity;
            Place(rival, new Vector3(5, 151, 6)); cam.transform.rotation = Quaternion.identity; Call("PositionCamera");
            foreach (int i in Enumerable.Range(0, ((int[])Get("owner")).Length)) ((int[])Get("owner"))[i] = 1;
            ((int[])Get("botTarget"))[0] = -1; Set("nextExplorationPlan", Time.time + 100);
            Equip(); var held = (DeliveryHeldItem)Get("heldItem");
            Check(Vector3.Dot(held.transform.TransformDirection(Vector3.down), cam.transform.forward) > .99f, "first-person suction side faces the opponent");
            Check(held.GetComponentsInChildren<Collider>().Length == 0, "held plunger introduces no invisible collision or extra hands");
            var guard = held.transform.localPosition;
            Set("captureTarget", 0); Set("captureStarted", Time.time - 1); Shot();
            Check((int)Get("plungerSlot") == 0 && ammo[0] == 3 && !Flight() && captures[0] == 0, "real click winds up without an instant pull or premature charge consumption");
            Check((int)Get("captureTarget") == -1 && !((DeliveryItemState)Get("playerItems")).CanCapture(Time.time), "throw cancels player capture and blocks capture during action");
            Set("plungerStarted", Time.time - .06f); Call("PosePlungerThrow");
            Check(held.transform.localPosition.z < guard.z - .14f, "windup visibly draws the handle back");
            Call("UseWeapon"); Check(ammo[0] == 3 && !Flight(), "repeated clicks during windup cannot duplicate a throw");
            Launch(); Check(Flight() && !((GameObject)Get("heldWeapon")).activeSelf && ammo[0] == 3, "launch makes one world model and hides held copy without spending a charge");
            Check(Flight().GetComponentsInChildren<Transform>().All(t => t.gameObject.layer == 0) && Flight().GetComponentsInChildren<Collider>().Length == 0, "flight renders in the world camera without becoming a pickup or blocking itself");
            var launchPosition = Flight().transform.position; Call("UpdatePlungerThrow");
            Check(Mathf.Abs(Vector3.Distance(launchPosition, Flight().transform.position) - 36 * Time.deltaTime) < .01f, "outbound flight moves at 36 metres per second");
            yield return new WaitForEndOfFrame(); Capture("plunger-outbound-20261012.png");
            var launchForward = Flight().transform.TransformDirection(Vector3.down);
            Check(Vector3.Dot(launchForward, ((Vector3)Get("plungerEnd") - held.transform.position).normalized) > .98f, "suction cup leads the flight");
            Input(false, Key.Digit2); Call("SelectSlot", keyboard); Input(); Check((int)Get("selectedSlot") == 0, "slot switching waits until the thrown item returns");
            cam.transform.rotation = Quaternion.Euler(0, 60, 0);
            Place(player, new Vector3(2, 151, 0)); Call("PositionCamera"); Call("UpdateHeldWeapon");
            var fixedEnd = (Vector3)Get("plungerEnd"); yield return Return();
            Check((Vector3)Get("plungerEnd") == fixedEnd && inventory[0] == WeaponKind.Plunger && ammo[0] == 3 && ((GameObject)Get("heldWeapon")).activeSelf, "turning and moving do not redirect outbound throw and returned item follows current hand");
            Check(Vector3.Distance(((DeliveryHeldItem)Get("heldItem")).transform.localPosition, guard) < .01f, "catch restores exact equipped pose");
            cam.transform.rotation = Quaternion.identity; Place(player, new Vector3(0, 151, 0)); Call("PositionCamera");
            Place(rival, new Vector3(0, 151, 4)); Equip(); captures[0] = Time.time - 2.7f; Shot(); Launch();
            float timeout = Time.time + 2;
            while (!(bool)Get("plungerReturning") && Time.time < timeout) { Call("UpdatePlungerThrow"); yield return null; }
            Check(states[0].Pulled(Time.time) && captures[0] == 0 && !states[0].CanCapture(Time.time), "actual outbound hit starts controlled pull and resets nearly-complete capture");
            float forceTimer = states[0].forceResistUntil; float before = rival.transform.position.z;
            Call("UpdateBots"); Check(rival.transform.position.z < before, "native rival controller physically moves toward player after plunger impact");
            yield return new WaitForEndOfFrame(); Capture("plunger-hit-return-20261012.png");
            yield return Return();
            Check(AtFront() && !states[0].Pulled(Time.time) && !player.bounds.Intersects(rival.bounds), "hit rival arrives 1.6 metres in front without overlapping player body");
            Check(states[0].forceResistUntil == forceTimer && ammo[0] == 3, "return flight does not apply a second pull or consume any charge");
            Place(rival, new Vector3(0, 151, 8)); Equip(); Shot(); Launch();
            Check(!states[0].Pulled(Time.time), "retreating target has not been hit before moving");
            Place(rival, new Vector3(0, 151, 9.5f));
            yield return Return();
            Check(AtFront(), "rival moving away after launch reaches front: actor=" + rival.transform.position.ToString("F3") + " player=" + player.transform.position.ToString("F3") + " pullUntil=" + states[0].pullUntil + " now=" + Time.time + " radius=" + Get("plungerHitRadius") + " cup=" + Get("plungerCupOffset") + " shield=" + states[0].shieldUntil);
            var retreatGoal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            retreatGoal.transform.position = new Vector3(0, 151.8f, 22); retreatGoal.transform.localScale = new Vector3(.7f, .9f, .4f);
            retreatGoal.AddComponent<InteractionTarget>(); temporary.Add(retreatGoal);
            ((GameObject[])Get("mailboxes"))[0] = retreatGoal; ((int[])Get("owner"))[0] = -1;
            ((int[])Get("botTarget"))[0] = 0; Set("explorationRoute", new[] { new Vector3(0, 150, 22) }); Set("explorationCorner", 0);
            Place(rival, new Vector3(0, 151, 9)); ((float[])Get("botVertical"))[0] = -2; Equip();
            float walkingBefore = rival.transform.position.z; Call("UpdateBots");
            Check(rival.transform.position.z > walkingBefore, "native rival actively walks away before the throw");
            Shot(); Launch();
            bool movingHit = false; float hitPosition = 0, minimumPosition = float.PositiveInfinity; timeout = Time.time + 3;
            while (Time.time < timeout && (!movingHit || (int)Get("plungerSlot") >= 0 || states[0].Pulled(Time.time)))
            {
                Call("UpdatePlungerThrow");
                if (!movingHit && states[0].Pulled(Time.time)) { movingHit = true; hitPosition = rival.transform.position.z; }
                Call("UpdateBots");
                if (movingHit) minimumPosition = Mathf.Min(minimumPosition, rival.transform.position.z);
                yield return null;
            }
            Check(movingHit && AtFront(), "native walking rival is brought all the way to 1.6 metre front gap: travelled " + (hitPosition - minimumPosition).ToString("0.00") + " metres");
            retreatGoal.SetActive(false); ((int[])Get("botTarget"))[0] = -1; ((int[])Get("owner"))[0] = 1;
            foreach (float side in new[] { -1f, 1f })
            {
                Place(rival, new Vector3(5, 151, 6)); Equip(); Shot(); Launch();
                float queryRadius = (float)Get("plungerHitRadius");
                float radius = queryRadius - .02f;
                var mouth = Flight().transform.TransformPoint((Vector3)Get("plungerCupOffset"));
                var aim = ((Vector3)Get("plungerEnd") - Flight().transform.position).normalized;
                var right = Vector3.Cross(Vector3.up, aim).normalized;
                var contact = mouth + aim * 4;
                Place(rival, contact + right * side * (rival.radius + radius - .012f) - Vector3.up * rival.center.y);
                Check(radius > .13f && radius < .15f && queryRadius > .15f && queryRadius < .18f && ((Vector3)Get("plungerCupOffset")).y < -.33f,
                    "volume uses the measured 28cm cup width and its forward suction mouth");
                Check(!rival.Raycast(new Ray(mouth, aim), out var narrowHit, 15),
                    "cup-edge target lies outside the old thin center-line hit test");
                timeout = Time.time + 2;
                while (!(bool)Get("plungerReturning") && Time.time < timeout) { Call("UpdatePlungerThrow"); yield return null; }
                Check(states[0].Pulled(Time.time), "visible cup edge catches the target on side " + side);
                yield return Return(); Check(AtFront(), "cup-edge hit completes the full pull to player front on side " + side);
            }
            foreach (bool above in new[] { false, true })
            {
                Place(rival, new Vector3(5, 151, 6)); Equip(); Shot(); Launch();
                float queryRadius = (float)Get("plungerHitRadius");
                float radius = queryRadius - .02f;
                var mouth = Flight().transform.TransformPoint((Vector3)Get("plungerCupOffset"));
                var aim = ((Vector3)Get("plungerEnd") - Flight().transform.position).normalized;
                var contact = mouth + aim * 4;
                Place(rival, new Vector3(contact.x, above ? contact.y + radius - .012f :
                    contact.y - rival.height - radius + .012f, contact.z));
                timeout = Time.time + 2;
                while (!(bool)Get("plungerReturning") && Time.time < timeout) { Call("UpdatePlungerThrow"); yield return null; }
                Check(states[0].Pulled(Time.time), "cup volume also catches a vertical rim graze above=" + above);
                yield return Return(); Check(AtFront(), "vertical rim graze reaches player front above=" + above);
            }
            Place(rival, new Vector3(5, 151, 6)); Equip(); Shot(); Launch();
            var cupOrigin = Flight().transform.TransformPoint((Vector3)Get("plungerCupOffset"));
            var cupAim = ((Vector3)Get("plungerEnd") - Flight().transform.position).normalized;
            var cupRight = Vector3.Cross(Vector3.up, cupAim).normalized;
            Place(rival, cupOrigin + cupRight * (rival.radius + (float)Get("plungerHitRadius") - .02f - .01f) - Vector3.up * rival.center.y);
            Call("UpdatePlungerThrow"); Check(states[0].Pulled(Time.time), "actor already overlapping the cup is caught at the start of a movement step");
            yield return Return();

            Place(rival, new Vector3(5, 151, 6)); Equip(); Shot(); Launch();
            cupOrigin = Flight().transform.TransformPoint((Vector3)Get("plungerCupOffset"));
            cupAim = ((Vector3)Get("plungerEnd") - Flight().transform.position).normalized;
            cupRight = Vector3.Cross(Vector3.up, cupAim).normalized;
            var edgeWall = GameObject.CreatePrimitive(PrimitiveType.Cube); temporary.Add(edgeWall);
            edgeWall.transform.SetPositionAndRotation(cupOrigin + cupAim * 2 + cupRight * ((float)Get("plungerHitRadius") * .85f), Quaternion.LookRotation(cupAim));
            edgeWall.transform.localScale = new Vector3(.06f, 2, .06f); Physics.SyncTransforms();
            Place(rival, cupOrigin + cupAim * 4 - Vector3.up * rival.center.y); states[0].shieldUntil = Time.time + 8;
            Check(!edgeWall.GetComponent<Collider>().Raycast(new Ray(cupOrigin, cupAim), out var centerWall, 15),
                "thin obstacle touches the cup edge while missing its center line");
            yield return Return(); Check(states[0].pullUntil == 0 && states[0].shieldUntil > Time.time,
                "cup-edge wall collision stops flight before the protected target behind it"); edgeWall.SetActive(false);
            var nearWall = GameObject.CreatePrimitive(PrimitiveType.Cube); temporary.Add(nearWall);
            nearWall.transform.position = cam.transform.position + cam.transform.forward * .3f;
            nearWall.transform.localScale = new Vector3(3, 3, .05f); Physics.SyncTransforms();
            Place(rival, new Vector3(0, 151, 4)); Equip(); Shot(); Launch(); yield return Return();
            Check(states[0].pullUntil == 0, "wall between camera and suction mouth prevents launching through close cover"); nearWall.SetActive(false);
            Place(rival, new Vector3(1.3f, 151, 4)); Equip(); Shot(); Launch(); yield return Return();
            Check(states[0].pullUntil == 0, "rival outside the widened cup volume is not automatically targeted");
            Place(rival, new Vector3(0, 151, 14)); Equip(); Shot(); Launch(); yield return Return();
            Check(AtFront(), "fourteen metre hit ends at the same 1.6 metre front gap");
            Place(rival, new Vector3(0, 151, 17)); Equip(); Shot(); Launch(); yield return Return();
            Check(states[0].pullUntil == 0, "rival beyond fifteen metre throw range is not pulled");
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.position = new Vector3(0, 151.7f, 2.5f); wall.transform.localScale = new Vector3(3, 3, .1f); temporary.Add(wall); Physics.SyncTransforms();
            Place(rival, new Vector3(0, 151, 4)); Equip(); Shot(); Launch(); yield return Return();
            Check(states[0].pullUntil == 0 && ammo[0] == 3, "wall blocks rival hit but unlimited plunger still returns without spending a throw"); wall.SetActive(false);
            Place(rival, new Vector3(0, 151, 3)); Equip(); Shot(); Launch(); yield return Return();
            Check(AtFront(), "close three metre hit also stops at player front");
            Place(rival, new Vector3(0, 151, 10)); Equip(); Shot(); Launch(); timeout = Time.time + 2;
            while (!states[0].Pulled(Time.time) && (int)Get("plungerSlot") >= 0 && Time.time < timeout) { Call("UpdatePlungerThrow"); yield return null; }
            Check(states[0].Pulled(Time.time), "distant rival is attached before moving player");
            Place(player, new Vector3(2, 151, 1)); player.transform.rotation = Quaternion.Euler(0, 25, 0);
            cam.transform.rotation = player.transform.rotation; Call("PositionCamera"); Call("UpdateHeldWeapon"); yield return Return();
            Check(AtFront(), "moving and turning player still receives victim at current front position");
            Place(player, new Vector3(0, 151, 0)); player.transform.rotation = Quaternion.identity; cam.transform.rotation = Quaternion.identity; Call("PositionCamera");
            Place(rival, new Vector3(0, 151, 10)); Equip(); Shot(); Launch(); timeout = Time.time + 2;
            while (!states[0].Pulled(Time.time) && (int)Get("plungerSlot") >= 0 && Time.time < timeout) { Call("UpdatePlungerThrow"); yield return null; }
            Check(states[0].Pulled(Time.time), "wall test starts after actual attachment");
            wall.transform.position = new Vector3(0, 151.7f, 5); wall.SetActive(true); Physics.SyncTransforms(); yield return Return();
            Check(!states[0].Pulled(Time.time) && rival.bounds.min.z >= wall.GetComponent<Collider>().bounds.max.z - .05f &&
                rival.transform.position.z > 5.5f, "pull stops at inserted wall and never teleports victim through it"); wall.SetActive(false);
            Place(rival, new Vector3(0, 151, 4)); Equip(); states[0].shieldUntil = Time.time + 8; Shot(); Launch(); yield return Return();
            Check(states[0].pullUntil == 0 && states[0].shieldUntil == 0 && Mathf.Abs(rival.transform.position.z - 4) < .1f, "single-use shield blocks attachment without dragging victim");
            Place(rival, new Vector3(0, 151, 14)); Equip(); states[0].forceResistUntil = Time.time + .8f; Shot(); Launch(); yield return Return();
            Check(AtFront(), "repeat-hit resistance reduces pull speed but victim still reaches front");
            Place(rival, new Vector3(0, 153.5f, 0)); Equip(); Call("BeginPlungerPull", 1);
            Check(states[0].Pulled(Time.time), "hit directly above player still starts attachment despite zero horizontal offset");
            yield return Return(); Check(AtFront(), "elevated attached victim also arrives in front");
            Equip(); Shot(); ((DeliveryItemState)Get("playerItems")).stunUntil = Time.time + 1; Launch();
            Check((int)Get("plungerSlot") == -1 && !Flight() && ammo[0] == 3, "stun during windup cancels pending launch without consuming a charge");
            Equip(); Shot(); Call("DropSelectedWeapon"); Call("UpdatePlungerThrow");
            Check(inventory[0] == WeaponKind.None && !Flight() && (int)Get("plungerSlot") == -1, "G during windup deletes item and cancels throw");
            Place(rival, new Vector3(5, 151, 6)); Equip(); Shot(); Launch(); var discarded = Flight(); Call("DropSelectedWeapon"); Call("UpdatePlungerThrow");
            Check(inventory[0] == WeaponKind.None && !Flight() && !discarded.activeSelf && states[0].force == Vector3.zero, "G during flight removes world model and prevents any later hit");
            Equip(); ammo[0] = 0; Shot(); Launch();
            Check(inventory[0] == WeaponKind.Plunger && ammo[0] == 0 && Flight(), "unlimited plunger can launch even with a zero legacy charge counter");
            yield return Return(); Check(inventory[0] == WeaponKind.Plunger && (GameObject)Get("heldWeapon"), "return never removes unlimited plunger");
            for (int i = 0; i < 5; i++) { Set("nextAttackAt", 0f); Shot(); Launch(); yield return Return(); }
            Check(inventory[0] == WeaponKind.Plunger && ammo[0] == 0 && (string)Call("ItemRemainingText", 0) == "무제한", "more than three consecutive throws keep item equipped and unlimited");
            Equip(); game.thirdPersonPreview = true;
            var animator = visual.model.GetComponent<Animator>(); animator.Play("IDLE_ARMED", 0, 0); animator.Update(0);
            Call("UpdateHeldWeapon"); held = (DeliveryHeldItem)Get("heldItem");
            Check(Vector3.Dot(held.transform.TransformDirection(Vector3.down), visual.transform.forward) > .98f, "third-person suction side faces away from wearer");
            Shot(); Launch(); Check(Flight() && !((GameObject)Get("heldWeapon")).activeSelf, "third-person uses the same single world flight model");
            yield return Return(); Check(((GameObject)Get("heldWeapon")).activeSelf && held.transform.position.x > player.transform.position.x, "third-person catch restores plunger to physical right arm");
            states[0].pullSource = player.transform; states[0].pullUntil = Time.time + 2;
            Call("ApplyControl", 1, Vector3.forward * 18, 0f);
            Check(!states[0].Pulled(Time.time) && states[0].force.z > 0, "a new knockback cleanly replaces the prior pull");
            game.thirdPersonPreview = false; Call("UpdateHeldWeapon");
            Place(player, new Vector3(0, 151, 0)); Place(rival, new Vector3(0, 151, 7)); rival.transform.rotation = Quaternion.Euler(0, 180, 0);
            var playerPull = new DeliveryItemState { pullSource = rival.transform, pullUntil = Time.time + 2,
                pullSpeed = 44, pullStandOff = 1.6f };
            Set("playerItems", playerPull); Set("stamina", 100f); Set("rollUntil", 0f); Set("vertical", -2f);
            Input(); Input(false, Key.S, Key.LeftShift, Key.Space); Call("UpdatePlayer", keyboard);
            Check(player.transform.position.z > 0 && (float)Get("stamina") == 100 && Time.time >= (float)Get("rollUntil"),
                "pulled player cannot resist using backward movement, jump or dash");
            timeout = Time.time + 3;
            while (playerPull.Pulled(Time.time) && Time.time < timeout) { Call("UpdatePlayer", keyboard); yield return null; }
            Check(Vector3.Distance(player.transform.position, rival.transform.position + rival.transform.forward * 1.6f) < .09f,
                "shared player movement also reaches source front without overlapping");
            Input();
            File.WriteAllLines("art-review/plunger-cup-volume-check-20261014.txt", report);
            Debug.Log("PLUNGER_EFFECTS_OK: " + report.Count + " checks passed.");
        }
        finally
        {
            Call("FinishPlungerThrow"); foreach (var go in temporary) UnityEngine.Object.Destroy(go);
            foreach (var a in arrays) Array.Copy(a.saved, a.array, a.saved.Length);
            for (int i = 0; i < fields.Length; i++) fields[i].SetValue(game, values[i]);
            Place(player, positions[0]); Place(rival, positions[1]);
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
