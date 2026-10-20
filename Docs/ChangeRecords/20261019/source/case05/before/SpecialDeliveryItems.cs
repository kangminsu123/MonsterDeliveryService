using UnityEngine;
using System.Linq;

public sealed partial class MonsterDeliveryPrototype
{
    const float SpecialActionDuration = .6f;
    SpecialDeliveryParcel specialParcel;
    SpecialDeliveryParcel[] specialPickups = System.Array.Empty<SpecialDeliveryParcel>();
    int specialUseSlot = -1;
    float specialUseAt;
    readonly GameObject[] packingViews = new GameObject[4];
    readonly GameObject[] lifeJacketViews = new GameObject[4];
    readonly float[] specialFxAt = { -10, -10, -10, -10 };
    bool SpecialTestLoadout => specialParcel && specialParcel.testLoadout;
    static bool IsSpecial(WeaponKind kind) => kind >= WeaponKind.AirCannon && kind <= WeaponKind.ExpressBattery;
    static bool AllowsSpecialReplacement(InteractionTarget target) => target && target.TryGetComponent<SpecialDeliveryParcel>(out var pickup) && pickup.ReplaceSelected;
    static bool IsParcelTarget(InteractionTarget target) => target &&
        (target.GetComponent<FloatingParcel>() || target.GetComponent<PickupKind>() || target.GetComponent<SpecialDeliveryParcel>());

    void InitializeSpecialDelivery()
    {
        specialPickups = FindObjectsByType<SpecialDeliveryParcel>(FindObjectsSortMode.None).OrderBy(p => p.spawnId).ToArray();
        specialParcel = specialPickups.FirstOrDefault(p => p.fixedItem == WeaponKind.None);
        foreach (var pickup in specialPickups) pickup.Initialize(this);
        GrantSpecialTestLoadout();
    }
    void ResetSpecialPickups() { foreach (var pickup in specialPickups) pickup.ResetRound(); }
    float[] CaptureSpecialPickups() => specialPickups.Select(p => p.Remaining).ToArray();
    void ApplySpecialPickups(float[] remaining)
    {
        if (remaining == null || remaining.Length != specialPickups.Length) return;
        for (int i = 0; i < remaining.Length; i++) specialPickups[i].ApplySnapshot(remaining[i]);
    }
    void CollectSpecialLocal(SpecialDeliveryParcel pickup)
    {
        int slot = System.Array.IndexOf(inventory, WeaponKind.None);
        if (slot < 0 && pickup.ReplaceSelected) slot = selectedSlot;
        if (slot < 0 || !pickup.TryCollect()) return;
        var kind = pickup.RewardKind; ClearSlot(slot);
        inventory[slot] = kind; weaponAmmo[slot] = Ammo(kind); selectedSlot = slot;
        feedback = ItemName(kind) + " 장착 · 좌클릭으로 사용";
        focusedTarget = null; interactionReady = false;
    }
    void GrantSpecialTestLoadout()
    {
        specialUseSlot = -1;
        if (!SpecialTestLoadout) return;
        for (int slot = 0; slot < SlotCount; slot++)
        { inventory[slot] = WeaponKind.AirCannon + slot; weaponAmmo[slot] = 1; weaponSeconds[slot] = 0; }
        selectedSlot = 0; feedback = "강력한 아이템 3종 테스트 · 각 1회";
    }
    void StartSpecialUse(WeaponKind kind)
    {
        specialUseSlot = selectedSlot; specialUseAt = Time.time;
        captureTarget = -1; captureStarted = 0;
        playerItems.busyUntil = nextAttackAt = Time.time + SpecialActionDuration;
        feedback = ItemName(kind) + " 사용!";
        UseSpecialWeapon(0, kind, playerVisual.EyePosition, cam.transform.forward);
        ConsumeSelectedUse(true);
        ShowSpecialUse(0, kind, playerVisual.EyePosition, cam.transform.forward);
    }

    // Both offline and host combat use the same wall, team, shield and stun-immunity checks.
    void UseSpecialWeapon(int source, WeaponKind kind, Vector3 origin, Vector3 aim)
    {
        if (kind == WeaponKind.LifeJacket) { ActorState(source).immuneUntil = Time.time + 5; return; }
        if (kind == WeaponKind.ExpressBattery)
        {
            ActorState(source).batteryUntil = Time.time + 8;
            if (NetPlaying) netPlayers[source].stamina = 100; else if (source == 0) stamina = 100;
            return;
        }
        for (int target = 0; target < 4; target++)
        {
            var actor = ActorController(target);
            if (target == source || !actor || !actor.gameObject.activeInHierarchy || NetPlaying && !NetCanHit(source, target)) continue;
            Vector3 toward = actor.bounds.center - origin;
            float distance = toward.magnitude;
            bool inRange = kind == WeaponKind.Broadcaster ? distance <= 12 :
                kind == WeaponKind.AirCannon ? distance <= 12 && Vector3.Dot(aim, toward.normalized) >= .75f : distance <= 18;
            if (!inRange) continue;
            Vector3 ray = kind == WeaponKind.PackingMachine ? aim : toward.normalized;
            if (!NetRay(source, origin, ray, kind == WeaponKind.PackingMachine ? 18 : distance + .1f, .01f, out var hit, out _, out _) || hit != actor) continue;
            var impulse = toward; impulse.y = 0;
            var result = ApplyItemHit(target, kind == WeaponKind.AirCannon ?
                new DeliveryItemHit(impulse.normalized * 60 + Vector3.up * 18) :
                new DeliveryItemHit(stunSeconds: kind == WeaponKind.Broadcaster ? 3 : 4));
            if (kind == WeaponKind.PackingMachine && result == DeliveryHitResult.Applied)
                ActorState(target).packedUntil = ActorState(target).stunUntil;
        }
    }

    void UpdateSpecialPresentation()
    {
        if (!NetworkScreens && specialUseSlot >= 0 && Time.time - specialUseAt >= SpecialActionDuration)
        { if (weaponAmmo[specialUseSlot] == 0) ClearSlot(specialUseSlot); specialUseSlot = -1; }
        for (int local = 0; local < 4; local++)
        {
            var actor = ActorController(local);
            bool packed = actor && actor.gameObject.activeInHierarchy && Time.time < ActorState(local).packedUntil && ActorState(local).Stunned(Time.time);
            if (packed && !packingViews[local])
            {
                packingViews[local] = Instantiate(Resources.Load<GameObject>("SpecialDelivery/FragileParcel"), actor.transform);
                packingViews[local].name = "PackingEffect";
                packingViews[local].transform.localPosition = Vector3.up * .15f;
                packingViews[local].transform.localRotation = Quaternion.identity;
                packingViews[local].transform.localScale = Vector3.one * .8f;
                foreach (var c in packingViews[local].GetComponentsInChildren<Collider>()) Destroy(c);
                if (local == 0 && !thirdPersonPreview)
                    foreach (var r in packingViews[local].GetComponentsInChildren<Renderer>()) r.enabled = false;
            }
            if (!packed && packingViews[local]) { Destroy(packingViews[local]); packingViews[local] = null; }
            bool protectedNow = started && !finished && actor && actor.gameObject.activeInHierarchy && Time.time < ActorState(local).immuneUntil;
            bool visible = protectedNow && (local != 0 || thirdPersonPreview);
            if (visible && !lifeJacketViews[local])
            {
                lifeJacketViews[local] = Instantiate(Resources.Load<GameObject>("SpecialDelivery/LifeJacket"), actor.transform);
                lifeJacketViews[local].name = "LifeJacketProtection";
                lifeJacketViews[local].transform.localPosition = new Vector3(0, -.2f, .65f);
                lifeJacketViews[local].transform.localRotation = Quaternion.identity;
                lifeJacketViews[local].transform.localScale = Vector3.one * .65f;
            }
            if (lifeJacketViews[local]) lifeJacketViews[local].SetActive(visible);
        }
        if (NetPlaying)
        {
            for (int global = 0; global < 4; global++)
            {
                var p = netPlayers[global];
                if (p == null || !p.connected || !IsSpecial((WeaponKind)p.actionKind) || Time.time - p.actionAt > .3f) continue;
                int local = NetLocal(global);
                var visual = local == 0 ? playerVisual : botVisuals[local - 1];
                ShowSpecialUse(local, (WeaponKind)p.actionKind, visual.EyePosition, Quaternion.Euler(p.pitch, p.yaw, 0) * Vector3.forward);
            }
            var own = netPlayers[netOwn];
            if (own != null && IsSpecial((WeaponKind)own.actionKind) && own.actionKind == (int)Selected && own.actionSlot == selectedSlot)
                PoseSpecialUse(heldItem, playerVisual, Time.time - own.actionAt, !thirdPersonPreview);
            for (int global = 0; global < 4; global++) if (NetLocal(global) > 0 && netHeld[global] && IsSpecial(netHeldKinds[global]) &&
                netPlayers[global].actionKind == (int)netHeldKinds[global] && netPlayers[global].actionSlot == netPlayers[global].selected)
                PoseSpecialUse(netHeld[global], botVisuals[NetLocal(global) - 1], Time.time - netPlayers[global].actionAt, false);
        }
        else if (specialUseSlot == selectedSlot && IsSpecial(Selected))
            PoseSpecialUse(heldItem, playerVisual, Time.time - specialUseAt, !thirdPersonPreview);
    }
    static void PoseSpecialUse(DeliveryHeldItem item, DeliveryRobotVisual visual, float age, bool firstPerson)
    {
        if (!item || age < 0 || age >= SpecialActionDuration) return;
        float recoil = Mathf.Sin(age / SpecialActionDuration * Mathf.PI);
        item.gameObject.SetActive(true);
        if (firstPerson) { item.FirstPerson(); item.transform.localPosition += new Vector3(0, .06f, -.16f) * recoil; }
        else { visual.PushArm(.3f * recoil, true); item.FollowBones(); }
    }
    void ShowSpecialUse(int local, WeaponKind kind, Vector3 origin, Vector3 aim)
    {
        if (kind > WeaponKind.PackingMachine) return;
        if (Time.time - specialFxAt[local] < .5f) return;
        specialFxAt[local] = Time.time;
        var go = new GameObject("SpecialUse_" + kind);
        go.AddComponent<SpecialDeliveryPulse>().Initialize(kind, origin, aim);
    }
    void DrawSpecialTestHUD(float w, float h, float scale)
    {
        if (Time.time < playerItems.immuneUntil)
        {
            DeliveryUIArt.Image(new Rect(510, h - 206, 270, 40), "control_panel");
            DeliveryUIArt.Text(new Rect(520, h - 202, 250, 32), "모든 효과 면역 · " + (playerItems.immuneUntil-Time.time).ToString("0.0") + "초",20,new Color(1,.65f,.2f));
        }
        foreach (var pickup in specialPickups)
        {
            if (!pickup.ReplaceSelected || !pickup.Available || Vector3.Distance(cam.transform.position,pickup.transform.position)>22) continue;
            var p=cam.WorldToScreenPoint(pickup.transform.position+Vector3.up*(pickup.GetComponent<BoxCollider>().bounds.size.y+.3f));
            if(p.z<=0)continue;
            var rect=new Rect(p.x/scale-95,(Screen.height-p.y)/scale-14,190,28);
            DeliveryUIArt.Image(rect,"control_panel");DeliveryUIArt.Text(rect,ItemName(pickup.fixedItem),16,Color.white,TextAnchor.MiddleCenter);
        }
    }
}

// One short-lived line per use; item actions are replicated by the existing action snapshot.
public sealed class SpecialDeliveryPulse : MonoBehaviour
{
    LineRenderer line; Material material; WeaponKind kind; Vector3 origin, forward; float began;
    public void Initialize(WeaponKind item, Vector3 at, Vector3 aim)
    {
        kind = item; origin = at; forward = aim.normalized; began = Time.time;
        line = gameObject.AddComponent<LineRenderer>();
        material = new Material(Shader.Find("Sprites/Default")); line.sharedMaterial = material;
        line.startWidth = line.endWidth = .08f; line.useWorldSpace = true;
        line.loop = kind != WeaponKind.PackingMachine; line.positionCount = line.loop ? 32 : 2;
    }
    void Update()
    {
        float age = Time.time - began, t = age / .5f;
        if (t >= 1) { Destroy(gameObject); return; }
        var color = kind == WeaponKind.AirCannon ? Color.cyan : kind == WeaponKind.Broadcaster ? new Color(1, .3f, .12f) : Color.yellow;
        color.a = 1 - t; line.startColor = line.endColor = color;
        if (kind == WeaponKind.PackingMachine) { line.SetPosition(0, origin); line.SetPosition(1, origin + forward * 18); return; }
        var rotation = kind == WeaponKind.Broadcaster ? Quaternion.Euler(90, 0, 0) : Quaternion.LookRotation(forward);
        var center = kind == WeaponKind.Broadcaster ? origin : origin + forward * (1 + t * 10);
        float radius = kind == WeaponKind.Broadcaster ? t * 12 : .2f + t * 3;
        for (int i = 0; i < 32; i++)
        { float a = i * Mathf.PI * 2 / 32; line.SetPosition(i, center + rotation * new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * radius); }
    }
    void OnDestroy() { if (material) Destroy(material); }
}
