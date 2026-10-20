using UnityEngine;
using System.Linq;

public sealed partial class MonsterDeliveryPrototype
{
    const float SpecialActionDuration = .6f;
    public const float BroadcasterRadius = 12, BroadcasterStunSeconds = 4;
    SpecialDeliveryParcel specialParcel;
    SpecialDeliveryParcel[] specialPickups = System.Array.Empty<SpecialDeliveryParcel>();
    int specialUseSlot = -1;
    float specialUseAt;
    readonly GameObject[] lifeJacketViews = new GameObject[4];
    readonly LifeJacketProtectionFX[] lifeJacketFX = new LifeJacketProtectionFX[4];
    readonly float[] specialFxAt = { -10, -10, -10, -10 };
    public bool SpecialDevelopmentPickups => !NetworkScreens && mapExploreMode;
    bool SpecialTestLoadout => SpecialDevelopmentPickups && specialParcel && specialParcel.testLoadout;
    static bool IsSpecial(WeaponKind kind) => kind >= WeaponKind.AirCannon && kind <= WeaponKind.ExpressBattery;
    static bool AllowsSpecialReplacement(InteractionTarget target) => target && target.TryGetComponent<SpecialDeliveryParcel>(out var pickup) && pickup.ReplaceSelected;
    static bool IsParcelTarget(InteractionTarget target) => target &&
        (target.GetComponent<FloatingParcel>() || target.GetComponent<PickupKind>() || target.GetComponent<SpecialDeliveryParcel>());

    void InitializeSpecialDelivery()
    {
        specialPickups = FindObjectsByType<SpecialDeliveryParcel>(FindObjectsSortMode.None).OrderBy(p => p.spawnId).ToArray();
        specialParcel = specialPickups.FirstOrDefault(p => p.fixedItem == WeaponKind.None);
        foreach (var pickup in specialPickups)
        {
            pickup.Initialize(this);
            if (pickup.fixedItem != WeaponKind.None) pickup.GetComponent<InteractionTarget>().displayName = ItemName(pickup.fixedItem);
        }
        ResetSpecialPickups();
        GrantSpecialTestLoadout();
    }
    void ResetSpecialPickups()
    {
        deliveryMinute = 0; deliveryNoticeUntil = 0;
        foreach (var pickup in specialPickups)
        {
            pickup.gameObject.SetActive(!pickup.ReplaceSelected || SpecialDevelopmentPickups);
            pickup.ResetRound();
        }
    }
    float[] CaptureSpecialPickups() => specialPickups.Select(p => p.Remaining).ToArray();
    void ApplySpecialPickups(float[] remaining)
    {
        if (remaining == null || remaining.Length != specialPickups.Length) return;
        for (int i = 0; i < remaining.Length; i++)
        {
            var pickup = specialPickups[i];
            pickup.gameObject.SetActive(!pickup.ReplaceSelected || SpecialDevelopmentPickups);
            if (!pickup.Scheduled) pickup.ApplySnapshot(remaining[i]);
        }
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
        if (kind == WeaponKind.PackingMachine && RemoteCaptureTarget(0, playerVisual.EyePosition, cam.transform.forward) < 0)
        { feedback = "18m 안의 상대 또는 중립 우체통을 조준하세요."; return; }
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
        // Keep the existing asset/enum ID so the original model and icon remain unchanged.
        if (kind == WeaponKind.PackingMachine)
        {
            int house = RemoteCaptureTarget(source, origin, aim);
            if (house >= 0) CaptureMailbox(house, source, true);
            return;
        }
        if (kind == WeaponKind.LifeJacket) { ActorState(source).immuneUntil = Time.time + 5; return; }
        if (kind == WeaponKind.ExpressBattery)
        {
            ActorState(source).batteryUntil = Time.time + 8;
            if (NetPlaying) netPlayers[source].stamina = 100; else if (source == 0) stamina = 100;
            return;
        }
        if (kind == WeaponKind.AirCannon) { FireAirCannon(source, origin, aim); return; }
        for (int target = 0; target < 4; target++)
        {
            var actor = ActorController(target);
            if (target == source || !actor || !actor.gameObject.activeInHierarchy || NetPlaying && !NetCanHit(source, target)) continue;
            Vector3 toward = actor.bounds.center - origin;
            float distance = toward.magnitude;
            bool inRange = distance <= BroadcasterRadius;
            if (!inRange) continue;
            if (!NetRay(source, origin, toward.normalized, distance + .1f, .01f, out var hit, out _, out _) || hit != actor) continue;
            ApplyItemHit(target, new DeliveryItemHit(stunSeconds: BroadcasterStunSeconds));
        }
    }

    int RemoteCaptureTarget(int source, Vector3 origin, Vector3 aim)
    {
        if (!started || finished || mapEditMode || aim.sqrMagnitude < .001f ||
            !NetRay(source, origin, aim.normalized, 18, .01f, out var hit, out _, out _)) return -1;
        var target = hit.GetComponentInParent<InteractionTarget>();
        int house = target ? System.Array.FindIndex(mailboxes, m => m == target.gameObject) : -1;
        if (house < 0 || !target.available || owner[house] == source) return -1;
        if (NetPlaying && owner[house] >= 0 &&
            netPlayers[NetGlobal(owner[house])]?.team == netPlayers[NetGlobal(source)]?.team) return -1;
        return house;
    }

    void UpdateSpecialPresentation()
    {
        if (!NetworkScreens && specialUseSlot >= 0 && Time.time - specialUseAt >= SpecialActionDuration)
        { if (weaponAmmo[specialUseSlot] == 0) ClearSlot(specialUseSlot); specialUseSlot = -1; }
        for (int local = 0; local < 4; local++)
        {
            var actor = ActorController(local);
            bool protectedNow = started && !finished && !mapEditMode && actor && actor.gameObject.activeInHierarchy && Time.time < ActorState(local).immuneUntil;
            bool visible = protectedNow && (local != 0 || thirdPersonPreview);
            if (visible && !lifeJacketFX[local])
            {
                var root = new GameObject("LifeJacketProtectionFX");
                root.transform.SetParent(actor.transform, false);
                lifeJacketFX[local] = root.AddComponent<LifeJacketProtectionFX>();
            }
            if (lifeJacketFX[local]) lifeJacketFX[local].Show(visible, ActorState(local).immuneUntil - Time.time);
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
                ShowSpecialUse(local, (WeaponKind)p.actionKind, visual.EyePosition, Quaternion.Euler(p.pitch, p.yaw, 0) * Vector3.forward, Mathf.Max(0, Time.time - p.actionAt));
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
    void ShowSpecialUse(int local, WeaponKind kind, Vector3 origin, Vector3 aim, float age = 0)
    {
        if (kind == WeaponKind.AirCannon || kind > WeaponKind.PackingMachine) return;
        if (Time.time - specialFxAt[local] < .5f) return;
        specialFxAt[local] = Time.time;
        var go = new GameObject("SpecialUse_" + kind);
        if (kind == WeaponKind.Broadcaster) go.AddComponent<BroadcasterFX>().Initialize(origin, age);
        else go.AddComponent<SpecialDeliveryPulse>().Initialize(kind, origin, aim);
    }
    void DrawSpecialTestHUD(float w, float h, float scale)
    {
        if (Time.time < playerItems.immuneUntil)
        {
            EnsureRunningShoesVignette();
            var tint = new Color(1, .72f, .12f, .22f + .05f * Mathf.Sin(Time.time * 5));
            GUI.DrawTexture(new Rect(0, 0, w, h), runningScreenVignette, ScaleMode.StretchToFill, true, 0, tint, 0, 0);
            DeliveryUIArt.Image(new Rect(510, h - 206, 270, 40), "control_panel");
            DeliveryUIArt.Text(new Rect(520, h - 202, 250, 32), "무적 · " + (playerItems.immuneUntil-Time.time).ToString("0.0") + "초",20,new Color(1,.75f,.2f));
        }
        foreach (var pickup in specialPickups)
        {
            if (!pickup.gameObject.activeInHierarchy || !pickup.ReplaceSelected || !pickup.Available || Vector3.Distance(cam.transform.position,pickup.transform.position)>22) continue;
            var p=cam.WorldToScreenPoint(pickup.transform.position+Vector3.up*(pickup.GetComponent<BoxCollider>().bounds.size.y+.3f));
            if(p.z<=0)continue;
            var rect=new Rect(p.x/scale-95,(Screen.height-p.y)/scale-14,190,28);
            DeliveryUIArt.Image(rect,"control_panel");DeliveryUIArt.Text(rect,ItemName(pickup.fixedItem),16,Color.white,TextAnchor.MiddleCenter);
        }
    }
}

// Existing beam reused for the remote mailbox item; replicated by the action snapshot.
public sealed class SpecialDeliveryPulse : MonoBehaviour
{
    LineRenderer line; Material material; Vector3 origin, forward; float began;
    public void Initialize(WeaponKind item, Vector3 at, Vector3 aim)
    {
        origin = at; forward = aim.normalized; began = Time.time;
        line = gameObject.AddComponent<LineRenderer>();
        material = new Material(Shader.Find("Sprites/Default")); line.sharedMaterial = material;
        line.startWidth = line.endWidth = .08f; line.useWorldSpace = true; line.positionCount = 2;
        line.SetPosition(0, origin); line.SetPosition(1, origin + forward * 18);
    }
    void Update()
    {
        float t = (Time.time - began) / .5f;
        if (t >= 1) { Destroy(gameObject); return; }
        var color = Color.yellow; color.a = 1 - t; line.startColor = line.endColor = color;
    }
    void OnDestroy() { if (material) Destroy(material); }
}
