using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class BatteryEffectsCheck
{
    const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Instance;
    const string D = "Docs/ChangeRecords/20261020/";
    [MenuItem("Tools/Gameplay/Special Delivery/Verify Battery Effects")]
    public static void Verify()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Play required");
        var game = UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>();
        game.StartCoroutine(Run(game));
    }
    static IEnumerator Run(MonsterDeliveryPrototype game)
    {
        var type = game.GetType();
        object Get(string n) => type.GetField(n, F).GetValue(game);
        void Set(string n, object v) => type.GetField(n, F).SetValue(game, v);
        object Call(string n, params object[] args) => type.GetMethod(n, F).Invoke(game, args);
        var log = new List<string> { DateTimeOffset.Now.ToString("O"), "Actual Editor Play with controlled inventory and positions; not separate network peers." };
        void Check(bool ok, string text) => log.Add((ok ? "PASS " : "FAIL ") + text);
        Directory.CreateDirectory(D + "images"); Directory.CreateDirectory(D + "evidence");
        try
        {
            game.NetworkScreens = false; game.mapExploreMode = true; game.thirdPersonPreview = false; Call("Begin");
            var player = (CharacterController)Get("player");
            player.enabled = false; player.transform.SetPositionAndRotation(new Vector3(.45f, 1, 19.2f), Quaternion.identity); player.enabled = true;
            Set("lookPitch", 8f);
            var items = (WeaponKind[])Get("inventory"); var ammo = (int[])Get("weaponAmmo");
            items[0] = WeaponKind.ExpressBattery; ammo[0] = 1; Set("selectedSlot", 0); Set("nextAttackAt", 0f);
            Call("UpdateHeldWeapon"); Call("UpdateCamera");
            var held = (DeliveryHeldItem)Get("heldItem");
            Check(held && Vector3.Dot(held.firstPersonRotation * Vector3.forward, Vector3.back) > .999f, "held battery front faces the player after180degree rotation");
            yield return new WaitForSeconds(.2f);
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(D + "images/20261020_battery_front.png"));
            yield return new WaitForSeconds(.15f);
            Call("UseWeapon"); var ownState = (DeliveryItemState)Get("playerItems");
            float expires = ownState.batteryUntil;
            Check(Mathf.Abs(expires - Time.time - 8) < .03f && ammo[0] == 0, "normal battery use grants eight seconds and spends exactly once");
            Check(Mathf.Abs(ownState.MoveMultiplier(Time.time) - 1.3f) < .001f, "battery retains30percent movement bonus");
            yield return new WaitForSeconds(.2f);
            Check((float)Get("stamina") == 100, "active battery restores stamina in production update");
            Check(!(bool)type.GetProperty("RunningShoesActive", F).GetValue(game), "battery alone never activates running-shoes screen wind");
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(D + "images/20261020_battery_firstperson.png"));
            yield return new WaitForSeconds(.15f);
            game.enabled = false;
            var bots = (CharacterController[])Get("botControllers"); var states = (DeliveryItemState[])Get("botItems");
            var rival = bots[0]; rival.gameObject.SetActive(true); rival.enabled = false;
            rival.transform.SetPositionAndRotation(new Vector3(.45f, 1, 22), Quaternion.Euler(0, 180, 0)); rival.enabled = true;
            states[0] = new DeliveryItemState { batteryUntil = expires };
            Call("UpdateRunningShoesFX"); Call("UpdateBatteryFX");
            var aura = rival.GetComponentInChildren<BatteryChargeAura>();
            Check(aura && aura.GetComponentsInChildren<LineRenderer>().Length == 12, "battery world effect has six arcs,two circuits,battery icon and three cells");
            Check(!rival.GetComponentInChildren<RunningShoesAura>(), "battery alone has no running-shoes world aura");
            Check(aura && aura.GetComponentsInChildren<Collider>().Length == 0 && aura.GetComponentsInChildren<Rigidbody>().Length == 0, "electricity has no collision or gameplay impulse");
            Call("UpdateBatteryFX");
            Check(rival.GetComponentsInChildren<BatteryChargeAura>().Length == 1, "repeated presentation reuses one aura");
            states[0].speedUntil = Time.time + 5; Call("UpdateRunningShoesFX");
            Check(rival.GetComponentInChildren<RunningShoesAura>() && aura.gameObject.activeSelf, "overlapping shoes and battery keep independent world effects");
            Check(Mathf.Abs(states[0].MoveMultiplier(Time.time) - 1.5f) < .001f, "overlap keeps strongest movement multiplier without stacking");
            states[0].speedUntil = 0; Call("UpdateRunningShoesFX");
            Check(!rival.GetComponentInChildren<RunningShoesAura>() && aura.gameObject.activeSelf, "ending shoes leaves only electricity while battery persists");
            Camera.main.transform.SetPositionAndRotation(rival.transform.position + new Vector3(3, 1.7f, -4), Quaternion.LookRotation(new Vector3(-3, -.6f, 4)));
            Call("UpdateBatteryFX"); AirCannonCheck.Capture(D + "images/20261020_battery_rival.png");
            yield return new WaitForSeconds(.4f); Call("UpdateSpecialPresentation");
            Check(items[0] == WeaponKind.None && ownState.batteryUntil > Time.time, "slot clears after use but battery status persists");
            game.thirdPersonPreview = true; Call("UpdateBatteryFX");
            Check(player.GetComponentInChildren<BatteryChargeAura>(), "third-person self also gets electricity");
            Set("finished", true); Call("UpdateBatteryFX");
            Check(!player.GetComponentInChildren<BatteryChargeAura>() && !aura.gameObject.activeSelf, "round finish hides all electric effects");
            Set("finished", false); game.thirdPersonPreview = false;
            while (Time.time < expires + .05f) yield return null;
            Call("UpdateBatteryFX");
            Check(!aura.gameObject.activeSelf && ownState.MoveMultiplier(Time.time) == 1, "real eight-second expiry hides electricity and restores normal speed");
        }
        finally
        {
            game.enabled = true; game.thirdPersonPreview = false; Call("Begin");
            File.WriteAllLines(D + "evidence/battery-effects-play.txt", log);
        }
        Debug.Log("BATTERY_EFFECTS_CHECK\n" + string.Join("\n", log));
    }
}
