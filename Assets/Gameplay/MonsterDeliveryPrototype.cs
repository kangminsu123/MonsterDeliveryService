using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum WeaponKind { None, BoxingGloves, Plunger, Megaphone, PackingTape, CordlessFan, RunningShoes, BubbleWrap, WorkGloves, PrinterInk, MiniTrampoline }

public sealed partial class MonsterDeliveryPrototype : MonoBehaviour
{
    const int HouseCount = 10, BotCount = 3, SlotCount = 3;
    readonly Color[] colors = { Color.yellow, new(1f, .25f, .25f), new(.95f, .75f, .1f), new(.65f, .3f, 1f) };
    readonly GameObject[] mailboxes = new GameObject[HouseCount], houses = new GameObject[HouseCount], bots = new GameObject[BotCount], pickups = new GameObject[10];
    readonly Renderer[] mailboxRenderers = new Renderer[HouseCount], botRenderers = new Renderer[BotCount];
    readonly int[] owner = new int[HouseCount], botTarget = new int[BotCount];
    readonly float[] botVertical = new float[BotCount], botCaptureStarted = new float[BotCount], pickupRespawnAt = new float[10];
    readonly WeaponKind[] inventory = new WeaponKind[SlotCount];
    readonly float[] weaponSeconds = new float[SlotCount];
    CharacterController player; Camera cam; GameObject heldWeapon;
    DeliveryRobotVisual playerVisual;
    InteractionTarget focusedTarget;
    bool interactionReady;
    int selectedSlot, captureTarget = -1, armedGloveSlot = -1;
    bool started, finished, crouching;
    float startedAt, lookPitch = 12, vertical, captureStarted, nextAttackAt, stamina = 100, rollUntil, mapCameraPitch = 42, mapCameraYaw;
    Vector3 rollDirection;
    string feedback = "Enter를 눌러 우편함 난투를 시작하세요.";
    [Min(30)] public float roundSeconds = 180;
    public bool mapEditMode;
    public bool mapExploreMode = true;
    public bool thirdPersonPreview = false;
    // Shift now triggers a dash once per key press.
    // Item attacks and held models remain staged; floating parcels award inventory items.
    bool itemPlaytestEnabled = false;
    DeliveryItemState playerItems = new();
    readonly DeliveryItemState[] botItems = new DeliveryItemState[BotCount];
    readonly CharacterController[] botControllers = new CharacterController[BotCount];
    readonly List<ItemArea> itemAreas = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install() { if (FindAnyObjectByType<MonsterDeliveryPrototype>() == null) new GameObject("MapExplorer").AddComponent<MonsterDeliveryPrototype>(); }

    void Start()
    {
        // Map preview uses the authored scene lighting so material colors match the Editor.
        if (!mapEditMode && !mapExploreMode)
        {
            RenderSettings.ambientLight = new Color(.025f, .045f, .12f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(.035f, .06f, .15f);
            RenderSettings.fogDensity = .008f;
        }
        CreateWorld();
        if (!mapEditMode) gameObject.AddComponent<DeliveryMinimap>().Initialize(this, player.transform, mailboxes);
        if (mapExploreMode) started = true;
        Cursor.lockState = mapEditMode || mapExploreMode ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !mapEditMode && !mapExploreMode;
    }
    public bool ShowMinimap => !mapEditMode && started && !finished;
    public Color LocalPlayerColor => colors[0];
    public Color GetMailboxMapColor(int index) => owner[index] < 0 ? Color.white : colors[owner[index]];

    void CreateWorld()
    {
        cam = Camera.main ?? new GameObject("Main Camera").AddComponent<Camera>(); cam.tag = "MainCamera";
        if (mapEditMode) { cam.transform.position = new Vector3(0, 95, -110); cam.transform.rotation = Quaternion.Euler(mapCameraPitch, mapCameraYaw, 0); return; }
        player = new GameObject("Player").AddComponent<CharacterController>();
        player.transform.position = SpawnPosition(0);
        player.height = 2; player.radius = .45f; playerItems.safePosition = SpawnPosition(0);
        var robotPrefab = Resources.Load<GameObject>("Characters/DeliveryRobotPlayer");
        if (!robotPrefab) throw new System.InvalidOperationException("DeliveryRobotPlayer prefab is missing.");
        playerVisual = Instantiate(robotPrefab, player.transform, false).GetComponent<DeliveryRobotVisual>();
        playerVisual.SetColor(colors[0]);
        var authored = GameObject.Find("Layer_11_Mailboxes");
        if (authored) for (int i = 0; i < HouseCount; i++)
        {
            var mailbox = authored.transform.Find("Plot_" + (i + 1).ToString("D2") + "_Mailbox");
            if (!mailbox) continue;
            mailboxes[i] = mailbox.gameObject;
            mailboxRenderers[i] = mailbox.Find("Model").GetComponentInChildren<MeshRenderer>();
            owner[i] = -1;
            SetMailboxEffectColor(i, Color.white);
        }
        if (mapExploreMode) return;
        for (var i = 0; i < HouseCount; i++) if (!mailboxes[i]) CreateCapturePad(i);
        for (var i = 0; i < BotCount; i++) CreateBot(i);
        if (itemPlaytestEnabled) CreatePickups();
        var light = FindAnyObjectByType<Light>(); if (light != null) { light.intensity = .45f; light.color = new Color(.32f, .42f, 1); }
    }
    Vector3 TruckPosition(int id) => new[] { new Vector3(-116, 0, 0), new Vector3(0, 0, 96), new Vector3(116, 0, 0), new Vector3(0, 0, -96) }[id];
    Vector3 SpawnPosition(int id) => id == 0 ? new Vector3(0, 1, 0) : TruckPosition(id) - TruckPosition(id).normalized * 8f + Vector3.up;
    void CreateCapturePad(int i)
    {
        var p = new[] { new Vector3(0, .06f, 0), new Vector3(-108, .06f, 62), new Vector3(-48, .06f, 58), new Vector3(42, .06f, 58), new Vector3(106, .06f, 58), new Vector3(-106, .06f, -48), new Vector3(-46, .06f, -56), new Vector3(42, .06f, -56), new Vector3(108, .06f, -44), new Vector3(94, .06f, -100) }[i];
        mailboxes[i] = Ground("Capture Pad " + (i + 1), p, new Vector3(4, .08f, 4), Color.gray); mailboxRenderers[i] = mailboxes[i].GetComponent<Renderer>();
        var interaction = mailboxes[i].AddComponent<InteractionTarget>(); interaction.displayName = "우체통"; interaction.actionHint = "E 유지 · 점령";
        var tag = new GameObject("Tag Zone " + i); tag.transform.position = p; var c = tag.AddComponent<SphereCollider>(); c.radius = 2.3f; c.isTrigger = true;
    }
    void CreateBot(int i)
    {
        bots[i] = Create("Rival " + (i + 1), PrimitiveType.Capsule, SpawnPosition(i + 1), Vector3.one, colors[i + 1]);
        botRenderers[i] = bots[i].GetComponent<Renderer>();
        bots[i].GetComponent<Collider>().enabled = false;
        botControllers[i] = bots[i].AddComponent<CharacterController>(); botControllers[i].height = 2; botControllers[i].radius = .45f;
        botItems[i] = new DeliveryItemState { safePosition = SpawnPosition(i + 1) };
    }
    void CreatePickups()
    {
        if (!itemPlaytestEnabled || mapExploreMode) return;
        for (var i = 0; i < pickups.Length; i++)
        {
            var a = i * Mathf.PI * 2 / pickups.Length;
            pickups[i] = Create("Mystery Parcel", PrimitiveType.Cube, new Vector3(Mathf.Sin(a) * 34, .7f, Mathf.Cos(a) * 28), new Vector3(1.5f, 1.1f, 1.2f), new Color(.46f, .28f, .12f));
            pickups[i].AddComponent<PickupKind>().kind = (WeaponKind)(i + 1);
            var interaction = pickups[i].AddComponent<InteractionTarget>(); interaction.displayName = "택배 상자"; interaction.actionHint = "E · 줍기";
        }
    }
    GameObject Create(string name, PrimitiveType type, Vector3 position, Vector3 scale, Color color) { var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.position = position; go.transform.localScale = scale; go.GetComponent<Renderer>().material.color = color; return go; }
    GameObject Ground(string name, Vector3 position, Vector3 scale, Color color) => Create(name, PrimitiveType.Cube, position, scale, color);
    void Update()
    {
        if (mapEditMode) { UpdateMapCamera(); return; }
        if (started && !finished) UpdateItemAreas();
        UpdateCamera(); var kb = Keyboard.current;
        if (mapExploreMode) { UpdatePlayer(kb); UpdateInteraction(); if (kb != null && kb.eKey.wasPressedThisFrame) TryPickup(); UpdateCapture(kb); UpdateHeldWeapon(); return; }
        if (!started) { if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) Begin(); return; }
        if (finished) { if (kb != null && kb.enterKey.wasPressedThisFrame) Begin(); return; }
        if (Time.time - startedAt >= roundSeconds) { Finish(); return; }
        UpdatePlayer(kb); UpdateInteraction();
        if (kb != null && kb.eKey.wasPressedThisFrame) TryPickup();
        UpdateBots(); UpdatePickups(); UpdateCapture(kb); UpdateHeldWeapon();
    }
    void LateUpdate()
    {
        if (!playerVisual) return;
        var velocity = player.velocity;
        playerVisual.SetMotion(started && !finished && !mapEditMode && new Vector2(velocity.x, velocity.z).sqrMagnitude > .04f,
            itemPlaytestEnabled && Selected != WeaponKind.None);
    }
    void UpdatePlayer(Keyboard kb)
    {
        if (playerItems.Stunned(Time.time)) kb = null;
        var move = Vector3.zero; if (kb != null) { if (kb.wKey.isPressed) move.z++; if (kb.sKey.isPressed) move.z--; if (kb.aKey.isPressed) move.x--; if (kb.dKey.isPressed) move.x++; }
        crouching = kb != null && kb.cKey.isPressed;
        // Resizing the controller resets contact state, even when the value is unchanged.
        var height = crouching ? 1.15f : 2f;
        if (player.height != height) { player.height = height; player.center = crouching ? new Vector3(0, -.425f, 0) : Vector3.zero; }
        var rolling = !playerItems.Stunned(Time.time) && Time.time < rollUntil;
        if (kb != null && kb.leftShiftKey.wasPressedThisFrame && !crouching && stamina >= 50 && Time.time >= rollUntil)
        {
            stamina -= 50;
            rollDirection = player.transform.TransformDirection(move.sqrMagnitude > .01f ? move.normalized : Vector3.forward);
            rollUntil = Time.time + .35f; rolling = true;
            captureTarget = -1; captureStarted = 0; feedback = "대쉬! (-50 스태미나)";
        }
        else if (!rolling) stamina = Mathf.Min(100, stamina + 20 * Time.deltaTime);
        var velocity = rolling ? rollDirection * 17f : player.transform.TransformDirection(move.normalized) * (crouching ? 3.6f : 6.5f);
        velocity *= playerItems.MoveMultiplier(Time.time);
        var grounded = player.isGrounded;
        if (grounded && vertical < 0) vertical = -2;
        if (grounded && kb != null && kb.spaceKey.isPressed) vertical = 7.5f;
        vertical = Mathf.Max(vertical, playerItems.pendingLift > 0 ? playerItems.TakeLift() : vertical);
        vertical += -20 * Time.deltaTime;
        // isGrounded describes the last Move: preserve the previous landing until the jump is decided.
        player.Move((velocity + playerItems.TakeForce(Time.deltaTime) + Vector3.up * vertical) * Time.deltaTime);
        RecoverActor(player, playerItems, ref vertical);
        PositionCamera();
        if (!mapExploreMode) KeepPlayerOutOfEnemySpawn();
        SelectSlot(kb);
        if (kb != null && kb.gKey.wasPressedThisFrame) DropSelectedWeapon();
        // Pickup dispatch happens after the shared crosshair query in Update.
        if (Time.time >= rollUntil && Mouse.current != null && (Selected == WeaponKind.CordlessFan ? Mouse.current.leftButton.isPressed : Mouse.current.leftButton.wasPressedThisFrame)) UseWeapon();
    }
    void UpdateCamera()
    {
        var mouse = Mouse.current;
        if (started && player != null && mouse != null) { var d = mouse.delta.ReadValue(); player.transform.Rotate(0, d.x * .12f, 0); lookPitch = Mathf.Clamp(lookPitch - d.y * .12f, -80, 80); }
        if (player == null) return;
        cam.transform.rotation = Quaternion.Euler(lookPitch, player.transform.eulerAngles.y, 0);
        PositionCamera();
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 60, Time.deltaTime * 12);
    }
    void PositionCamera()
    {
        if (!thirdPersonPreview) { cam.transform.position = player.transform.position + Vector3.up * (crouching ? 1f : 1.6f); return; }
        var pivot = player.transform.position + Vector3.up * (crouching ? .6f : .9f);
        var offset = cam.transform.right * .85f - cam.transform.forward * 4;
        float distance = offset.magnitude;
        Physics.SyncTransforms();
        foreach (var hit in Physics.SphereCastAll(pivot, .15f, offset.normalized, distance, ~0, QueryTriggerInteraction.Ignore))
            if (hit.collider != player) distance = Mathf.Min(distance, Mathf.Max(0, hit.distance - .05f));
        cam.transform.position = pivot + offset.normalized * distance;
    }
    void UpdateMapCamera()
    {
        var kb = Keyboard.current; var mouse = Mouse.current; if (cam == null) return;
        if (mouse != null) { var d = mouse.delta.ReadValue(); mapCameraYaw += d.x * .12f; mapCameraPitch = Mathf.Clamp(mapCameraPitch - d.y * .12f, 5, 85); cam.transform.rotation = Quaternion.Euler(mapCameraPitch, mapCameraYaw, 0); }
        var move = Vector3.zero; if (kb != null) { if (kb.wKey.isPressed) move.z++; if (kb.sKey.isPressed) move.z--; if (kb.aKey.isPressed) move.x--; if (kb.dKey.isPressed) move.x++; if (kb.eKey.isPressed) move.y++; if (kb.qKey.isPressed) move.y--; }
        var speed = kb != null && kb.leftShiftKey.isPressed ? 70f : 28f; cam.transform.position += (cam.transform.forward * move.z + cam.transform.right * move.x + Vector3.up * move.y) * speed * Time.deltaTime;
        if (kb != null && kb.escapeKey.wasPressedThisFrame) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }
    void UpdateBots()
    {
        for (var i = 0; i < BotCount; i++)
        {
            if (bots[i] == null || !bots[i].activeSelf) continue;
            var state = botItems[i]; var controller = botControllers[i];
            KeepBotOutOfEnemySpawn(i);
            if (botTarget[i] < 0 || owner[botTarget[i]] == i + 1) botTarget[i] = FindBotTarget(i);
            var target = mailboxes[botTarget[i]].transform.position;
            var flat = target - bots[i].transform.position; flat.y = 0;
            var moving = flat.magnitude > 1.5f && !state.Stunned(Time.time) && !VisionBlocked(bots[i].transform.position + Vector3.up, target + Vector3.up);
            var velocity = moving ? flat.normalized * 3.5f * state.MoveMultiplier(Time.time) : Vector3.zero;
            if (moving) bots[i].transform.rotation = Quaternion.LookRotation(flat);
            if (controller.isGrounded && botVertical[i] < 0) botVertical[i] = -2;
            if (state.pendingLift > 0) botVertical[i] = Mathf.Max(botVertical[i], state.TakeLift());
            botVertical[i] -= 20 * Time.deltaTime;
            controller.Move((velocity + state.TakeForce(Time.deltaTime) + Vector3.up * botVertical[i]) * Time.deltaTime);
            RecoverActor(controller, state, ref botVertical[i]);
            if (state.CanCapture(Time.time) && Vector3.Distance(bots[i].transform.position, target) < 2.2f)
            {
                if (botCaptureStarted[i] == 0) botCaptureStarted[i] = Time.time;
                if (Time.time - botCaptureStarted[i] >= 3) { Capture(botTarget[i], i + 1); botCaptureStarted[i] = 0; botTarget[i] = -1; }
            }
            else botCaptureStarted[i] = 0;
            // No invisible contact damage or free bot attacks while items are staged.
        }
    }
    void KeepPlayerOutOfEnemySpawn() { for (var id = 1; id <= BotCount; id++) { var away = player.transform.position - TruckPosition(id); away.y = 0; if (away.sqrMagnitude < 196) { player.Move((away.sqrMagnitude < .01f ? Vector3.forward : away.normalized) * (14 - Mathf.Sqrt(away.sqrMagnitude) + .2f)); feedback = "상대 택배차 안전구역에는 들어갈 수 없습니다."; return; } } }
    void KeepBotOutOfEnemySpawn(int bot) { for (var id = 0; id <= BotCount; id++) if (id != bot + 1) { var away = bots[bot].transform.position - TruckPosition(id); away.y = 0; if (away.sqrMagnitude < 196) { botControllers[bot].Move((away.sqrMagnitude < .01f ? Vector3.forward : away.normalized) * (14 - Mathf.Sqrt(away.sqrMagnitude) + .2f)); return; } } }
    int FindBotTarget(int bot) { for (var n = 0; n < HouseCount; n++) { var i = (bot + n + Random.Range(0, HouseCount)) % HouseCount; if (owner[i] != bot + 1) return i; } return 0; }
    void UpdatePickups() { for (var i = 0; i < pickups.Length; i++) { if (pickups[i] != null && !pickups[i].activeSelf && Time.time >= pickupRespawnAt[i]) pickups[i].SetActive(true); if (pickups[i] != null && pickups[i].activeSelf) pickups[i].transform.position += Vector3.up * Mathf.Sin(Time.time * 3 + i) * .003f; } }
    void UpdateInteraction()
    {
        Physics.SyncTransforms(); // Floating parcel colliders follow the current visual pose.
        InteractionTarget next = null; float distance = float.PositiveInfinity;
        if (cam && started && !finished && !mapEditMode &&
            InteractionRayHit(cam.ViewportPointToRay(new Vector3(.5f, .5f, 0)), 12, out var hit))
        {
            next = hit.collider.GetComponentInParent<InteractionTarget>(); distance = hit.distance + cam.nearClipPlane;
            if (thirdPersonPreview)
            {
                var origin = player.transform.position + Vector3.up * .35f;
                distance = Vector3.Distance(origin, hit.point);
                var toward = hit.point - origin;
                if (InteractionRayHit(new Ray(origin, toward.normalized), toward.magnitude + .01f, out var obstruction) &&
                    obstruction.collider.GetComponentInParent<InteractionTarget>() != next) next = null;
            }
            if (next && !next.isActiveAndEnabled) next = null;
        }
        if (focusedTarget && focusedTarget != next) focusedTarget.SetFocus(false, false);
        focusedTarget = next;
        interactionReady = next && next.available && distance <= next.interactionDistance && playerItems.CanCapture(Time.time) && Time.time >= rollUntil;
        if (next && (next.GetComponent<FloatingParcel>() || next.GetComponent<PickupKind>()) && !HasEmptySlot()) interactionReady = false;
        if (next && next.GetComponent<PickupKind>() && !itemPlaytestEnabled) interactionReady = false;
        for (int i = 0; i < HouseCount; i++) if (next && mailboxes[i] == next.gameObject && owner[i] == 0) interactionReady = false;
        if (next) next.SetFocus(true, interactionReady);
    }
    bool InteractionRayHit(Ray ray, float range, out RaycastHit nearest)
    {
        if (!thirdPersonPreview) return Physics.Raycast(ray, out nearest, range, ~0, QueryTriggerInteraction.Ignore);
        nearest = default; float closest = float.PositiveInfinity;
        // Temporary preview: native all-hit queries keep self-collisions out. Reuse a NonAlloc buffer if profiling shows GC pressure.
        foreach (var hit in Physics.RaycastAll(ray, range, ~0, QueryTriggerInteraction.Ignore))
            if (hit.collider != player && hit.distance < closest) { closest = hit.distance; nearest = hit; }
        return closest < float.PositiveInfinity;
    }
    void OnDisable() { if (focusedTarget) focusedTarget.SetFocus(false, false); }
    void TryPickup()
    {
        if (!interactionReady || !focusedTarget) return;
        var parcel = focusedTarget.GetComponent<FloatingParcel>();
        if (parcel)
        {
            if (!HasEmptySlot()) return;
            if (parcel.TryCollect())
            {
                AddWeapon((WeaponKind)Random.Range(1, 11)); focusedTarget = null; interactionReady = false;
                captureTarget = -1; captureStarted = 0;
            }
            return;
        }
        if (!itemPlaytestEnabled) return;
        for (var i = 0; i < pickups.Length; i++) if (pickups[i] == focusedTarget.gameObject)
        {
            if (!HasEmptySlot()) { feedback = "아이템 칸이 가득 찼습니다. G로 현재 아이템을 버리세요."; return; }
            AddWeapon(pickups[i].GetComponent<PickupKind>().kind); pickups[i].SetActive(false); pickupRespawnAt[i] = Time.time + 12; return;
        }
    }
    void UpdateCapture(Keyboard kb)
    {
        if (!interactionReady || !focusedTarget || !playerItems.CanCapture(Time.time) || kb == null || !kb.eKey.isPressed) { captureTarget = -1; captureStarted = 0; return; }
        var target = -1;
        for (int i = 0; i < HouseCount; i++) if (mailboxes[i] == focusedTarget.gameObject) { target = i; break; }
        if (target < 0 || owner[target] == 0) { captureTarget = -1; captureStarted = 0; return; }
        if (captureTarget != target) { captureTarget = target; captureStarted = Time.time; }
        if (Time.time - captureStarted >= CaptureSeconds) { Capture(target, 0); if (armedGloveSlot >= 0) { ClearSlot(armedGloveSlot); armedGloveSlot = -1; } captureTarget = -1; captureStarted = 0; }
    }
    void Capture(int house, int playerId) { owner[house] = playerId; SetMailboxEffectColor(house, colors[playerId]); feedback = (playerId == 0 ? "우편함 점령!" : "Rival " + playerId + "이 우편함을 탈환했습니다."); }

    public void SetPlayerColor(int playerId, Color color)
    {
        if ((uint)playerId >= colors.Length) throw new System.ArgumentOutOfRangeException(nameof(playerId));
        color.a = 1; colors[playerId] = color;
        if (playerId == 0 && playerVisual) playerVisual.SetColor(color);
        for (int i = 0; i < HouseCount; i++) if (mailboxes[i] && owner[i] == playerId) SetMailboxEffectColor(i, color);
    }
    void SetMailboxEffectColor(int house, Color color)
    {
        var beacon = mailboxes[house].transform.Find("CaptureBeacon");
        if (!beacon) return;
        var block = new MaterialPropertyBlock();
        foreach (var renderer in beacon.GetComponentsInChildren<Renderer>())
        {
            renderer.GetPropertyBlock(block); block.SetColor("_Color", color); renderer.SetPropertyBlock(block);
        }
    }

    WeaponKind Selected => inventory[selectedSlot];
    void SelectSlot(Keyboard kb) { if (kb == null) return; if (kb.digit1Key.wasPressedThisFrame) selectedSlot = 0; if (kb.digit2Key.wasPressedThisFrame) selectedSlot = 1; if (kb.digit3Key.wasPressedThisFrame) selectedSlot = 2; }
    bool HasEmptySlot() { for (var i = 0; i < SlotCount; i++) if (inventory[i] == WeaponKind.None) return true; return false; }
    string ItemName(WeaponKind item) => item switch
    {
        WeaponKind.BoxingGloves => "복싱 글러브", WeaponKind.Plunger => "압축 뚫어뻥",
        WeaponKind.Megaphone => "휴대용 확성기", WeaponKind.PackingTape => "포장용 테이프",
        WeaponKind.CordlessFan => "무선 선풍기", WeaponKind.RunningShoes => "러닝화",
        WeaponKind.BubbleWrap => "에어캡", WeaponKind.WorkGloves => "작업용 장갑",
        WeaponKind.PrinterInk => "프린터 잉크", WeaponKind.MiniTrampoline => "미니 트램펄린", _ => "빈손"
    };
    string SlotText(int slot) => inventory[slot] == WeaponKind.None ? "빈손" : ItemName(inventory[slot]) + " " + (inventory[slot] == WeaponKind.CordlessFan ? weaponSeconds[slot].ToString("0.0") + "초" : weaponAmmo[slot] + "회");
    int Ammo(WeaponKind item) => item switch { WeaponKind.BoxingGloves or WeaponKind.Plunger => 3, WeaponKind.Megaphone or WeaponKind.PackingTape or WeaponKind.PrinterInk => 2, WeaponKind.None or WeaponKind.CordlessFan => 0, _ => 1 };
    readonly int[] weaponAmmo = new int[SlotCount];
    float hornAt = -1;
    float CaptureSeconds => armedGloveSlot >= 0 ? 2 : 3;

    void AddWeapon(WeaponKind item)
    {
        if ((int)item < 1 || (int)item > 10) return;
        for (var i = 0; i < SlotCount; i++) if (inventory[i] == WeaponKind.None)
        {
            inventory[i] = item; weaponAmmo[i] = Ammo(item); weaponSeconds[i] = item == WeaponKind.CordlessFan ? 4 : 0;
            selectedSlot = i; feedback = ItemName(item) + " 획득!"; return;
        }
    }
    void ClearSlot(int slot) { inventory[slot] = WeaponKind.None; weaponAmmo[slot] = 0; weaponSeconds[slot] = 0; if (armedGloveSlot == slot) armedGloveSlot = -1; }
    void Consume() { if (--weaponAmmo[selectedSlot] <= 0) ClearSlot(selectedSlot); }
    void DropSelectedWeapon() { if (Selected == WeaponKind.None) return; feedback = ItemName(Selected) + "을 버렸습니다."; ClearSlot(selectedSlot); }
    void UseWeapon()
    {
        if (!itemPlaytestEnabled || !started || finished || playerItems.Stunned(Time.time) || Selected == WeaponKind.None || Time.time < nextAttackAt) return;
        var kind = Selected;
        if (kind == WeaponKind.WorkGloves && armedGloveSlot >= 0) return;
        if (kind == WeaponKind.MiniTrampoline && (!Physics.Raycast(cam.transform.position, cam.transform.forward, out var floor, 8, ~0, QueryTriggerInteraction.Ignore) || floor.normal.y < .5f)) return;
        captureTarget = -1; captureStarted = 0;
        playerItems.busyUntil = Time.time + (kind == WeaponKind.CordlessFan ? .15f : .5f);
        nextAttackAt = Time.time + (kind == WeaponKind.CordlessFan ? 0 : .6f);
        feedback = ItemName(kind) + " 사용!";
        switch (kind)
        {
            case WeaponKind.BoxingGloves: AttackCone(3, .6f, 18, 6, 0); break;
            case WeaponKind.Plunger:
                if (Physics.SphereCast(cam.transform.position, .15f, cam.transform.forward, out var hit, 9, ~0, QueryTriggerInteraction.Ignore) && !VisionBlocked(cam.transform.position, hit.point))
                    for (var id = 1; id <= BotCount; id++) if (ActorController(id) != null && hit.transform == ActorController(id).transform)
                    { var pull = player.transform.position - hit.transform.position; pull.y = 0; ApplyControl(id, pull.normalized * 14 + Vector3.up * 2); }
                break;
            case WeaponKind.Megaphone: hornAt = Time.time + .25f; playerItems.busyUntil = Time.time + .8f; feedback = "확성기 준비!"; break;
            case WeaponKind.PackingTape:
            case WeaponKind.PrinterInk: MakeShot(cam.transform.forward, 18, kind); break;
            case WeaponKind.CordlessFan:
                AttackCone(6, .75f, 8, 0, 0); weaponSeconds[selectedSlot] = Mathf.Max(0, weaponSeconds[selectedSlot] - Time.deltaTime);
                if (weaponSeconds[selectedSlot] <= 0) ClearSlot(selectedSlot);
                return;
            case WeaponKind.RunningShoes: playerItems.speedUntil = Time.time + 5; break;
            case WeaponKind.BubbleWrap: playerItems.shieldUntil = Time.time + 8; break;
            case WeaponKind.WorkGloves: armedGloveSlot = selectedSlot; return;
            case WeaponKind.MiniTrampoline:
                Physics.Raycast(cam.transform.position, cam.transform.forward, out var ground, 8, ~0, QueryTriggerInteraction.Ignore);
                AddArea(kind, ground.point, player.transform.forward); break;
        }
        Consume();
    }
    CharacterController ActorController(int id) => id == 0 ? player : botControllers[id - 1];
    DeliveryItemState ActorState(int id) => id == 0 ? playerItems : botItems[id - 1];
    void ApplyControl(int id, Vector3 impulse, float stun = 0)
    {
        if (!ActorState(id).Control(Time.time, impulse, stun)) return;
        if (id == 0) { captureTarget = -1; captureStarted = 0; rollUntil = 0; }
        else botCaptureStarted[id - 1] = 0;
    }
    void AttackCone(float range, float dot, float push, float lift, float stun)
    {
        for (var id = 1; id <= BotCount; id++)
        {
            var actor = ActorController(id); if (actor == null || !actor.gameObject.activeSelf) continue;
            var point = actor.transform.position + Vector3.up * .5f;
            var offset = point - cam.transform.position;
            if (offset.magnitude > range || Vector3.Dot(cam.transform.forward, offset.normalized) < dot || VisionBlocked(cam.transform.position, point)) continue;
            if (Physics.Linecast(cam.transform.position, point, out var hit, ~0, QueryTriggerInteraction.Ignore) && hit.transform != actor.transform) continue;
            var direction = actor.transform.position - player.transform.position; direction.y = 0;
            ApplyControl(id, direction.normalized * push + Vector3.up * lift, stun);
        }
    }
    void MakeShot(Vector3 direction, float speed, WeaponKind kind)
    {
        // Preserve near-cover and swept-path collision checks for thrown household items.
        if (Physics.Raycast(cam.transform.position, direction, out var hit, .6f, ~0, QueryTriggerInteraction.Ignore) || Physics.SphereCast(cam.transform.position, .12f, direction, out hit, .6f, ~0, QueryTriggerInteraction.Ignore)) { ItemImpact(kind, hit.point); return; }
        var bolt = Create("Thrown Delivery Item", PrimitiveType.Sphere, cam.transform.position + direction * .6f, Vector3.one * .24f, Color.white);
        bolt.GetComponent<Renderer>().enabled = false; // Item artwork and held models are intentionally deferred.
        var body = bolt.AddComponent<Rigidbody>(); body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; body.linearVelocity = direction * speed + Vector3.up * 3;
        var shot = bolt.AddComponent<BrawlShot>(); shot.owner = this; shot.kind = kind;
        if (player != null) foreach (var collider in player.GetComponents<Collider>()) Physics.IgnoreCollision(bolt.GetComponent<Collider>(), collider);
    }
    public void ItemImpact(WeaponKind kind, Vector3 point)
    {
        if (!itemPlaytestEnabled || finished || !started) return;
        if (kind == WeaponKind.PackingTape && Physics.Raycast(point + Vector3.up * .2f, Vector3.down, out var ground, 8, ~0, QueryTriggerInteraction.Ignore)) point = ground.point;
        AddArea(kind, point, Vector3.forward);
    }
    sealed class ItemArea
    {
        public WeaponKind kind; public Vector3 point, direction; public float until;
        public readonly float[] bounceAt = new float[BotCount + 1];
    }
    void AddArea(WeaponKind kind, Vector3 point, Vector3 direction)
    {
        itemAreas.Add(new ItemArea { kind = kind, point = point, direction = direction, until = Time.time + (kind == WeaponKind.PackingTape ? 5 : kind == WeaponKind.PrinterInk ? 4 : 8) });
    }
    void UpdateItemAreas()
    {
        if (!itemPlaytestEnabled || !started || finished) return;
        if (hornAt >= 0 && Time.time >= hornAt)
        { hornAt = -1; if (!playerItems.Stunned(Time.time)) AttackCone(4, .65f, 0, 0, .8f); }
        itemAreas.RemoveAll(area => Time.time >= area.until);
        foreach (var area in itemAreas)
        for (var id = 0; id <= BotCount; id++)
        {
            var actor = ActorController(id); if (actor == null || !actor.gameObject.activeSelf) continue;
            var state = ActorState(id); var offset = actor.transform.position - area.point;
            if (area.kind == WeaponKind.PrinterInk)
            { if (offset.sqrMagnitude < 9) state.visionBlockedUntil = Time.time + .05f; continue; }
            if (Mathf.Abs(offset.y) > 2.2f || new Vector2(offset.x, offset.z).sqrMagnitude > (area.kind == WeaponKind.PackingTape ? 9 : 1.5f)) continue;
            if (area.kind == WeaponKind.PackingTape) state.slowUntil = Time.time + .05f;
            else if (actor.isGrounded && Time.time >= area.bounceAt[id])
            {
                // Friendly launch pads benefit everyone and bypass defensive armor.
                area.bounceAt[id] = Time.time + .8f; state.force = area.direction * 12; state.pendingLift = 11; state.forceUntil = Time.time + .35f;
                if (id == 0) { captureTarget = -1; captureStarted = 0; } else botCaptureStarted[id - 1] = 0;
            }
        }
    }
    bool VisionBlocked(Vector3 from, Vector3 to)
    {
        var segment = to - from;
        foreach (var area in itemAreas) if (area.kind == WeaponKind.PrinterInk && Time.time < area.until)
        {
            var center = area.point + Vector3.up;
            var t = segment.sqrMagnitude < .001f ? 0 : Mathf.Clamp01(Vector3.Dot(center - from, segment) / segment.sqrMagnitude);
            if ((from + segment * t - center).sqrMagnitude < 9) return true;
        }
        return false;
    }
    void RecoverActor(CharacterController actor, DeliveryItemState state, ref float falling)
    {
        if (actor.isGrounded && actor.transform.position.y > -5) state.safePosition = actor.transform.position + Vector3.up * .1f;
        if (actor.transform.position.y >= -8) return;
        actor.enabled = false; actor.transform.position = state.safePosition; actor.enabled = true;
        falling = 0; state.force = Vector3.zero; state.pendingLift = 0; state.forceUntil = 0;
        if (actor == player) { captureTarget = -1; captureStarted = 0; } else for (var i = 0; i < BotCount; i++) if (actor == botControllers[i]) botCaptureStarted[i] = 0;
        feedback = "안전한 지면으로 복귀했습니다.";
    }
    void UpdateHeldWeapon() { if (heldWeapon != null) { Destroy(heldWeapon); heldWeapon = null; } }
    void Begin()
    {
        started = true; finished = false; stamina = 100; vertical = 0; rollUntil = 0; crouching = false; startedAt = Time.time;
        selectedSlot = 0; captureTarget = -1; captureStarted = 0; armedGloveSlot = -1; nextAttackAt = 0; hornAt = -1;
        itemAreas.Clear(); foreach (var shot in FindObjectsByType<BrawlShot>()) if (shot.owner == this) Destroy(shot.gameObject);
        playerItems = new DeliveryItemState { safePosition = SpawnPosition(0) };
        player.enabled = false; player.transform.position = SpawnPosition(0); player.enabled = true;
        for (var i = 0; i < HouseCount; i++) if (mailboxes[i] != null) { owner[i] = -1; SetMailboxEffectColor(i, Color.white); }
        for (var i = 0; i < SlotCount; i++) ClearSlot(i);
        for (var i = 0; i < BotCount; i++) if (bots[i] != null)
        { botControllers[i].enabled = false; bots[i].transform.position = SpawnPosition(i + 1); botControllers[i].enabled = true; botItems[i] = new DeliveryItemState { safePosition = SpawnPosition(i + 1) }; botVertical[i] = 0; botTarget[i] = -1; botCaptureStarted[i] = 0; }
        for (var i = 0; i < pickups.Length; i++) if (pickups[i] != null) { pickupRespawnAt[i] = 0; pickups[i].SetActive(itemPlaytestEnabled); }
        feedback = "빈손으로 시작합니다."; Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
    }
    void Finish() { if (focusedTarget) focusedTarget.SetFocus(false, false); focusedTarget = null; interactionReady = false; finished = true; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    int Score(int id) { var total = 0; for (var i = 0; i < HouseCount; i++) if (owner[i] == id) total++; return total; }
    int Rank(int id) { var rank = 1; for (var other = 0; other <= BotCount; other++) if (Score(other) > Score(id)) rank++; return rank; }
    string ResultBoard() { var board = ""; for (var rank = 1; rank <= BotCount + 1; rank++) for (var id = 0; id <= BotCount; id++) if (Rank(id) == rank) board += rank + "위  " + (id == 0 ? "나" : "Rival " + id) + "  ·  " + Score(id) + "개 점령\n"; return board; }
void OnGUI()
    {
        if (mapEditMode) { GUI.Box(new Rect(15, 15, 600, 70), "맵 제작 모드 · WASD 이동 · Q/E 하강/상승 · Shift 빠르게 · 마우스 시점"); return; }
        if (!started) { GUI.Box(new Rect(Screen.width / 2 - 300, Screen.height / 2 - 130, 600, 260), "MAILBOX BRAWL\n\n우체통 앞에서 E를 3초간 유지해 점령하세요.\n빈손으로 시작합니다.\n\nEnter 또는 Space로 시작"); return; }
        if (finished) { GUI.Box(new Rect(Screen.width / 2 - 260, Screen.height / 2 - 180, 520, 360), "경기 종료\n\n내 순위: " + Rank(0) + "위 · 점령: " + Score(0) + " / " + HouseCount + "\n\n[ 최종 순위 ]\n" + ResultBoard() + "\nEnter로 다시 시작"); return; }
        DrawGameplayHUD();
    }



}

public sealed class PickupKind : MonoBehaviour { public WeaponKind kind; }
// Legacy component name retained for existing collision-check tooling, now carries no damage.
public sealed class BrawlShot : MonoBehaviour
{
    public MonsterDeliveryPrototype owner; public WeaponKind kind;
    Rigidbody body; bool impacted;
    void Awake() => body = GetComponent<Rigidbody>();
    void Start() => Destroy(gameObject, 3);
    void FixedUpdate()
    {
        if (impacted || body == null) return;
        var travel = body.linearVelocity * Time.fixedDeltaTime;
        if (travel.sqrMagnitude > .0001f && body.SweepTest(travel.normalized, out var hit, travel.magnitude, QueryTriggerInteraction.Ignore)) Impact(hit.point);
    }
    void OnCollisionEnter(Collision c) => Impact(c.contacts.Length > 0 ? c.contacts[0].point : transform.position);
    void Impact(Vector3 point)
    {
        if (impacted) return;
        impacted = true; gameObject.SetActive(false); Destroy(gameObject);
        if (owner != null) owner.ItemImpact(kind, point);
    }
}
