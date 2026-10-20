using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed partial class MonsterDeliveryPrototype
{
    const float HandThrowRaise = .16f, HandThrowRelease = .22f, HandThrowDuration = .55f;
    int handThrowSlot = -1;
    WeaponKind handThrowKind;
    float handThrowStarted;
    bool handThrowReleased;
    sealed class NetHandThrow { public int source, slot; public WeaponKind kind; public float started; }
    readonly List<NetHandThrow> netHandThrows = new();

    static bool IsHandThrown(WeaponKind kind) => kind == WeaponKind.Slime || kind == WeaponKind.IceCream || kind == WeaponKind.FountainPen;

    static float ThrowArmAt(float age)
    {
        if (age < 0 || age >= HandThrowDuration) return 0;
        if (age < HandThrowRaise) return 105 * Mathf.SmoothStep(0, 1, age / HandThrowRaise);
        if (age < .30f) return Mathf.Lerp(105, -25, Mathf.SmoothStep(0, 1, (age - HandThrowRaise) / (.30f - HandThrowRaise)));
        return Mathf.Lerp(-25, 0, Mathf.SmoothStep(0, 1, (age - .30f) / (HandThrowDuration - .30f)));
    }

    static void PoseThrownItem(GameObject root, DeliveryHeldItem item, DeliveryRobotVisual visual, float age, bool released, bool firstPerson)
    {
        root.SetActive(!released || age >= HandThrowDuration);
        if (firstPerson)
        {
            item.FirstPerson();
            if (age < 0 || age >= HandThrowDuration) return;
            float raise = Mathf.SmoothStep(0, 1, age / HandThrowRaise);
            float swing = Mathf.SmoothStep(0, 1, (age - HandThrowRaise) / (HandThrowRelease - HandThrowRaise));
            float recover = 1 - Mathf.SmoothStep(0, 1, (age - .30f) / (HandThrowDuration - .30f));
            item.transform.localPosition += Vector3.Lerp(new Vector3(.04f, .38f, -.12f) * raise, new Vector3(-.06f, .05f, .28f), swing) * recover;
            item.transform.localRotation *= Quaternion.Euler(Mathf.Lerp(-70 * raise, 40, swing) * recover, -12 * raise * (1 - swing), -15 * raise * (1 - swing));
        }
        else
        {
            float angle = ThrowArmAt(age);
            if (angle != 0) visual.PushArm(angle / 24, true);
            item.FollowBones();
        }
    }

    void StartHandThrow(WeaponKind kind)
    {
        handThrowSlot = selectedSlot; handThrowKind = kind; handThrowStarted = Time.time; handThrowReleased = false;
        captureTarget = -1; captureStarted = 0;
        playerItems.busyUntil = Time.time + HandThrowDuration; nextAttackAt = Time.time + .6f;
        feedback = ItemName(kind) + " · 던지기!";
    }

    void UpdateHandThrow()
    {
        if (handThrowSlot < 0) return;
        if (!started || finished || mapEditMode || selectedSlot != handThrowSlot || Selected != handThrowKind || playerItems.Stunned(Time.time) || Time.time < rollUntil)
        { FinishHandThrow(); return; }
        float age = Time.time - handThrowStarted;
        if (!handThrowReleased && age >= HandThrowRelease)
        {
            handThrowReleased = true;
            if (handThrowKind == WeaponKind.IceCream) MakeIceCreamShot(cam.transform.forward);
            else MakeShot(cam.transform.forward, handThrowKind == WeaponKind.FountainPen ? 48 : 18, handThrowKind);
            ConsumeSelectedUse(true);
        }
        if (age >= HandThrowDuration) FinishHandThrow();
    }

    void FinishHandThrow()
    {
        int slot = handThrowSlot; handThrowSlot = -1;
        if (slot >= 0 && weaponAmmo[slot] <= 0) ClearSlot(slot);
    }

    void PoseHandThrow()
    {
        if (NetworkScreens || !IsHandThrown(heldKind) || !heldWeapon || !heldItem) return;
        float age = handThrowSlot == selectedSlot && heldKind == handThrowKind ? Time.time - handThrowStarted : float.PositiveInfinity;
        PoseThrownItem(heldWeapon, heldItem, playerVisual, age, handThrowReleased && handThrowSlot >= 0, !thirdPersonPreview);
        if (!thirdPersonPreview && heldKind == WeaponKind.Slime) heldItem.transform.localScale *= 1 + .035f * Mathf.Sin(Time.time * 4);
    }

    void UpdateNetHandThrows()
    {
        foreach (var shot in netHandThrows.ToArray())
        {
            var p = netPlayers[shot.source];
            bool current = p != null && p.actionAt == shot.started && p.actionKind == (int)shot.kind && p.actionSlot == shot.slot;
            if (!current || !p.connected || p.selected != shot.slot || p.items[shot.slot] != (int)shot.kind || ActorState(shot.source).Stunned(Time.time) || p.dashRemaining > 0)
            {
                if (current) p.actionKind = 0;
                netHandThrows.Remove(shot); continue;
            }
            if (Time.time - shot.started < HandThrowRelease) continue;
            netHandThrows.Remove(shot); p.throwReleased = true;
            LaunchNetProjectile(shot.source, (int)shot.kind);
            ConsumeNetItem(shot.source, shot.slot, true);
        }
    }
}
