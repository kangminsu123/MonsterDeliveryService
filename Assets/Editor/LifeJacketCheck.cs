using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class LifeJacketCheck
{
    const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Instance;
    const string D = "Docs/ChangeRecords/20261020/";
    [MenuItem("Tools/Gameplay/Special Delivery/Capture Life Jacket")]
    public static void Capture()
    {
        SpecialItemRowCheck.ShowRow();
        var game = UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>();
        game.StartCoroutine(CaptureLocal(game));
    }
    static IEnumerator CaptureLocal(MonsterDeliveryPrototype game)
    {
        var t = game.GetType();
        yield return new WaitForSeconds(.5f);
        var items = (WeaponKind[])t.GetField("inventory", F).GetValue(game);
        var ammo = (int[])t.GetField("weaponAmmo", F).GetValue(game);
        items[0] = WeaponKind.LifeJacket; ammo[0] = 1;
        t.GetField("selectedSlot", F).SetValue(game, 0);
        t.GetField("nextAttackAt", F).SetValue(game, 0f);
        t.GetMethod("UseWeapon", F).Invoke(game, null);
        yield return new WaitForSeconds(.3f);
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(D + "images/20261020_lifejacket_firstperson.png"));
    }
    [MenuItem("Tools/Gameplay/Special Delivery/Verify Life Jacket")]
    public static void Verify()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Play required");
        var game = UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>();
        game.StartCoroutine(Run(game));
    }
    static IEnumerator Run(MonsterDeliveryPrototype game)
    {
        var t = game.GetType();
        object Get(string n) => t.GetField(n, F).GetValue(game);
        void Set(string n, object v) => t.GetField(n, F).SetValue(game, v);
        object Call(string n, params object[] a) => t.GetMethod(n, F).Invoke(game, a);
        var log = new List<string> { DateTimeOffset.Now.ToString("O"), "Actual Editor Play, controlled reproduction. Not separate network peers." };
        void Check(bool ok, string message) => log.Add((ok ? "PASS " : "FAIL ") + message);
        Directory.CreateDirectory(D + "evidence"); Directory.CreateDirectory(D + "images");
        try
        {
            game.NetworkScreens = false; Call("Begin"); game.enabled = false;
            var inventory = (WeaponKind[])Get("inventory"); var ammo = (int[])Get("weaponAmmo");
            Set("selectedSlot", 0); inventory[0] = WeaponKind.LifeJacket; ammo[0] = 1;
            Call("UpdateHeldWeapon");
            Check(!((GameObject)Get("heldWeapon")), "selected life jacket has no hand model in first person");
            game.thirdPersonPreview = true; Call("UpdateHeldWeapon");
            Check(!((GameObject)Get("heldWeapon")), "selected life jacket has no hand model in third person");
            game.thirdPersonPreview = false;
            Set("playerItems", new DeliveryItemState()); Set("nextAttackAt", 0f); Call("UseWeapon");
            var state = (DeliveryItemState)Get("playerItems");
            Check(Mathf.Abs(state.immuneUntil - Time.time - 5) < .03f && ammo[0] == 0, "normal use consumes once and grants five-second immunity");
            state.shieldUntil = Time.time + 8;
            Check(state.ReceiveHit(Time.time, new DeliveryItemHit(Vector3.one * 20, 3, 4, 5)) == DeliveryHitResult.Ignored && state.shieldUntil > Time.time && state.stunUntil == 0 && state.slowUntil == 0 && state.visionBlockedUntil == 0 && state.pendingLift == 0, "immunity blocks push, lift, stun, slow and ink without spending aircap");
            Check(state.ReceiveHit(Time.time, new DeliveryItemHit(slowSeconds: 3, shieldable: false)) == DeliveryHitResult.Ignored, "unshieldable area also respects immunity");
            Call("UpdateSpecialPresentation");
            Check(!UnityEngine.Object.FindObjectsByType<LifeJacketProtectionFX>(FindObjectsSortMode.None).Any(), "first-person world aura stays out of camera");
            // Render a protected rival in the actual park with a controlled external camera.
            var actors = (CharacterController[])Get("botControllers");
            var own = (CharacterController)Get("player"); var rival = actors[0];
            rival.gameObject.SetActive(true); rival.enabled = false;
            rival.transform.position = own.transform.position + own.transform.forward * 4; rival.enabled = true;
            var rivalStates = (DeliveryItemState[])Get("botItems"); rivalStates[0] = new DeliveryItemState();
            Call("UseSpecialWeapon", 1, WeaponKind.LifeJacket, rival.bounds.center, Vector3.forward);
            Call("UpdateSpecialPresentation");
            var fx = rival.GetComponentInChildren<LifeJacketProtectionFX>();
            Check(fx && fx.GetComponentsInChildren<LineRenderer>().Length == 11, "protected rival has three rings and eight moving glints");
            Check(fx && fx.GetComponentsInChildren<Collider>().Length == 0 && fx.GetComponentsInChildren<Rigidbody>().Length == 0, "aura is purely cosmetic with no collision");
            Call("UpdateSpecialPresentation");
            Check(rival.GetComponentsInChildren<LifeJacketProtectionFX>().Length == 1, "repeated snapshots reuse one aura");
            var camera = Camera.main;
            camera.transform.SetPositionAndRotation(rival.transform.position + new Vector3(3, 1.6f, -4), Quaternion.LookRotation(new Vector3(-3, -.8f, 4)));
            yield return null;
            AirCannonCheck.Capture(D + "images/20261020_lifejacket_rival.png");
            // The native Game screenshot includes IMGUI vignette and remaining-time HUD.
            Call("UpdateCamera");
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(D + "images/20261020_lifejacket_firstperson.png"));
            yield return new WaitForSeconds(.15f);
            Call("UpdateSpecialPresentation");
            Check(((WeaponKind[])Get("inventory"))[0] == WeaponKind.LifeJacket, "consumed slot remains only for the existing short action delay");
            yield return new WaitForSeconds(.5f); Call("UpdateSpecialPresentation");
            Check(inventory[0] == WeaponKind.None && state.immuneUntil > Time.time, "slot clears after action while immunity persists");
            while (Time.time < state.immuneUntil + .05f) yield return null;
            Call("UpdateSpecialPresentation");
            Check(!fx.gameObject.activeSelf, "world aura hides at immunity expiry");
            state.shieldUntil = 0;
            Check(state.ReceiveHit(Time.time, new DeliveryItemHit(stunSeconds: 1)) == DeliveryHitResult.Applied, "attacks apply again after the real five-second timer");
            game.thirdPersonPreview = true; state.immuneUntil = Time.time + 5; Call("UpdateSpecialPresentation");
            Check(own.GetComponentInChildren<LifeJacketProtectionFX>(), "third-person self view also shows protection");
            Set("finished", true); Call("UpdateSpecialPresentation");
            Check(!own.GetComponentInChildren<LifeJacketProtectionFX>(), "round finish hides protection despite a remaining deadline");
        }
        finally
        {
            game.thirdPersonPreview = false; Call("Begin"); game.enabled = true;
            File.WriteAllLines(D + "evidence/lifejacket-play.txt", log);
        }
        Debug.Log("LIFEJACKET_CHECK\n" + string.Join("\n", log));
    }
}
