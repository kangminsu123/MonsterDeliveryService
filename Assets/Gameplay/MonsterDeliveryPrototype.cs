using UnityEngine;
using UnityEngine.InputSystem;

public enum WeaponKind { None, ShampooGun, Sprayer, StapleRifle, PerfumeSniper, CompactBomb, EnergyDrink, DeliveryDrone }

public sealed class MonsterDeliveryPrototype : MonoBehaviour
{
    const int HouseCount = 10, BotCount = 3, SlotCount = 3;
    readonly Color[] colors = { new(.2f, .75f, 1f), new(1f, .25f, .25f), new(.95f, .75f, .1f), new(.65f, .3f, 1f) };
    readonly GameObject[] mailboxes = new GameObject[HouseCount], houses = new GameObject[HouseCount], bots = new GameObject[BotCount], pickups = new GameObject[10];
    readonly Renderer[] mailboxRenderers = new Renderer[HouseCount], botRenderers = new Renderer[BotCount];
    readonly int[] owner = new int[HouseCount], botHealth = new int[BotCount], botTarget = new int[BotCount];
    readonly float[] botRespawnAt = new float[BotCount], botCaptureStarted = new float[BotCount], botAttackAt = new float[BotCount], pickupRespawnAt = new float[10];
    readonly WeaponKind[] inventory = new WeaponKind[SlotCount];
    readonly float[] weaponSeconds = new float[SlotCount];
    CharacterController player; Camera cam; GameObject heldWeapon;
    int selectedSlot, playerHealth = 100, captureTarget = -1;
    bool started, finished, playerAlive = true, crouching;
    float startedAt, lookPitch, vertical, captureStarted, playerRespawnAt, nextAttackAt, respawnShieldUntil, stamina = 100, rollUntil, mapCameraPitch = 42, mapCameraYaw;
    Vector3 rollDirection;
    string feedback = "Enter를 눌러 우편함 난투를 시작하세요.";
    [Min(30)] public float roundSeconds = 180;
    public bool mapEditMode;
    public bool mapExploreMode = true;
    bool droneFlying;

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
        if (mapExploreMode) { started = true; droneFlying = true; player.transform.position += Vector3.up * 2.5f; inventory[0] = WeaponKind.DeliveryDrone; UpdateHeldWeapon(); }
        Cursor.lockState = mapEditMode || mapExploreMode ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !mapEditMode && !mapExploreMode;
    }
    void CreateWorld()
    {
        cam = Camera.main ?? new GameObject("Main Camera").AddComponent<Camera>(); cam.tag = "MainCamera";
        if (mapEditMode) { cam.transform.position = new Vector3(0, 95, -110); cam.transform.rotation = Quaternion.Euler(mapCameraPitch, mapCameraYaw, 0); return; }
        player = Create("Player", PrimitiveType.Capsule, SpawnPosition(0), Vector3.one, colors[0]).AddComponent<CharacterController>(); player.height = 2; player.radius = .45f;
        if (mapExploreMode) { CreateExploreDrone(); return; }
        for (var i = 0; i < HouseCount; i++) CreateCapturePad(i);
        for (var i = 0; i < BotCount; i++) CreateBot(i);
        CreatePickups();
        var light = FindAnyObjectByType<Light>(); if (light != null) { light.intensity = .45f; light.color = new Color(.32f, .42f, 1); }
    }
    Vector3 TruckPosition(int id) => new[] { new Vector3(-116, 0, 0), new Vector3(0, 0, 96), new Vector3(116, 0, 0), new Vector3(0, 0, -96) }[id];
    Vector3 SpawnPosition(int id) => id == 0 ? new Vector3(0, 1, 0) : TruckPosition(id) - TruckPosition(id).normalized * 8f + Vector3.up;
    void CreateCapturePad(int i)
    {
        var p = new[] { new Vector3(0, .06f, 0), new Vector3(-108, .06f, 62), new Vector3(-48, .06f, 58), new Vector3(42, .06f, 58), new Vector3(106, .06f, 58), new Vector3(-106, .06f, -48), new Vector3(-46, .06f, -56), new Vector3(42, .06f, -56), new Vector3(108, .06f, -44), new Vector3(94, .06f, -100) }[i];
        mailboxes[i] = Ground("Capture Pad " + (i + 1), p, new Vector3(4, .08f, 4), Color.gray); mailboxRenderers[i] = mailboxes[i].GetComponent<Renderer>();
        var tag = new GameObject("Tag Zone " + i); tag.transform.position = p; var c = tag.AddComponent<SphereCollider>(); c.radius = 2.3f; c.isTrigger = true;
    }
    void CreateBot(int i) { bots[i] = Create("Rival " + (i + 1), PrimitiveType.Capsule, SpawnPosition(i + 1), Vector3.one, colors[i + 1]); botRenderers[i] = bots[i].GetComponent<Renderer>(); }
    void CreatePickups()
    {
        var kinds = new[] { WeaponKind.ShampooGun, WeaponKind.Sprayer, WeaponKind.StapleRifle, WeaponKind.PerfumeSniper, WeaponKind.CompactBomb, WeaponKind.EnergyDrink, WeaponKind.ShampooGun, WeaponKind.StapleRifle, WeaponKind.EnergyDrink, WeaponKind.DeliveryDrone };
        for (var i = 0; i < pickups.Length; i++)
        {
            var a = i * Mathf.PI * 2 / 9; var p = i == pickups.Length - 1 ? new Vector3(0, 1, 22) : new Vector3(Mathf.Sin(a) * 34, .7f, Mathf.Cos(a) * 28);
            pickups[i] = Create(i == pickups.Length - 1 ? "Drone Test Parcel" : "Mystery Parcel", PrimitiveType.Cube, p, new Vector3(1.5f, 1.1f, 1.2f), new Color(.46f, .28f, .12f)); pickups[i].AddComponent<PickupKind>().kind = kinds[i];
        }
    }
    GameObject Create(string name, PrimitiveType type, Vector3 position, Vector3 scale, Color color) { var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.position = position; go.transform.localScale = scale; go.GetComponent<Renderer>().material.color = color; return go; }
    GameObject Ground(string name, Vector3 position, Vector3 scale, Color color) => Create(name, PrimitiveType.Cube, position, scale, color);
    Color WeaponColor(WeaponKind kind) => kind switch { WeaponKind.ShampooGun => new Color(.75f, .95f, 1), WeaponKind.Sprayer => new Color(.2f, .9f, .8f), WeaponKind.StapleRifle => new Color(.95f, .72f, .2f), WeaponKind.PerfumeSniper => new Color(1, .3f, .75f), WeaponKind.CompactBomb => new Color(.95f, .45f, .7f), WeaponKind.EnergyDrink => new Color(.25f, 1, .25f), WeaponKind.DeliveryDrone => new Color(.2f, .65f, 1), _ => Color.white };

    void Update()
    {
        if (mapEditMode) { UpdateMapCamera(); return; }
        UpdateCamera(); var kb = Keyboard.current;
        if (mapExploreMode) { inventory[0] = WeaponKind.DeliveryDrone; selectedSlot = 0; UpdatePlayer(kb); if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) UseWeapon(); UpdateHeldWeapon(); return; }
        if (!started) { if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) Begin(); return; }
        if (finished) { if (kb != null && kb.enterKey.wasPressedThisFrame) Begin(); return; }
        if (Time.time - startedAt >= roundSeconds) { Finish(); return; }
        UpdatePlayer(kb); UpdateBots(); UpdatePickups(); UpdateCapture(kb); UpdateHeldWeapon();
    }
    void UpdatePlayer(Keyboard kb)
    {
        if (!playerAlive) { if (Time.time >= playerRespawnAt) RespawnPlayer(); return; }
        var move = Vector3.zero; if (kb != null) { if (kb.wKey.isPressed) move.z++; if (kb.sKey.isPressed) move.z--; if (kb.aKey.isPressed) move.x--; if (kb.dKey.isPressed) move.x++; }
        var flying = droneFlying;
        crouching = !flying && kb != null && kb.cKey.isPressed; player.height = crouching ? 1.15f : 2f; player.center = crouching ? new Vector3(0, -.425f, 0) : Vector3.zero;
        var sprinting = move.sqrMagnitude > 0 && kb != null && kb.leftShiftKey.isPressed && stamina > 0 && !crouching; stamina = Mathf.Clamp(stamina + (sprinting ? -28 : 20) * Time.deltaTime, 0, 100);
        if (!flying && kb != null && kb.leftCtrlKey.wasPressedThisFrame && move.sqrMagnitude > .01f && stamina >= 50 && Time.time >= rollUntil) { stamina -= 50; rollDirection = player.transform.TransformDirection(move.normalized); rollUntil = Time.time + .35f; feedback = "구르기! (-50 스태미나)"; }
        var rolling = Time.time < rollUntil;
        player.Move((rolling ? rollDirection * 17f : player.transform.TransformDirection(move.normalized) * (flying ? 12f : crouching ? 3.6f : sprinting ? 10.5f : 6.5f)) * Time.deltaTime);
        if (flying) { vertical = kb != null && kb.spaceKey.isPressed ? 6f : kb != null && kb.leftCtrlKey.isPressed ? -6f : 0f; }
        else { var grounded = player.isGrounded; if (grounded && vertical < 0) vertical = -2; if (grounded && kb != null && kb.spaceKey.isPressed) vertical = 7.5f; vertical += -20 * Time.deltaTime; }
        player.Move(Vector3.up * vertical * Time.deltaTime);
        KeepPlayerOutOfEnemySpawn();
        if (!mapExploreMode) { SelectSlot(kb); if (kb != null && kb.gKey.wasPressedThisFrame) DropSelectedWeapon(); if (kb != null && kb.eKey.wasPressedThisFrame) TryPickup(); if (Time.time >= rollUntil && Mouse.current != null && (Selected == WeaponKind.Sprayer || Selected == WeaponKind.StapleRifle ? Mouse.current.leftButton.isPressed : Mouse.current.leftButton.wasPressedThisFrame)) UseWeapon(); }
    }
    void UpdateCamera()
    {
        var mouse = Mouse.current; if (started && playerAlive && mouse != null) { var d = mouse.delta.ReadValue(); player.transform.Rotate(0, d.x * .12f, 0); lookPitch = Mathf.Clamp(lookPitch - d.y * .12f, -80, 80); }
        if (player == null) return; cam.transform.position = player.transform.position + Vector3.up * (crouching ? 1f : 1.6f); cam.transform.rotation = Quaternion.Euler(lookPitch, player.transform.eulerAngles.y, 0);
        var aim = (Selected == WeaponKind.StapleRifle || Selected == WeaponKind.PerfumeSniper) && mouse != null && mouse.rightButton.isPressed; cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, aim ? 42 : 60, Time.deltaTime * 12);
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
            if (!bots[i].activeSelf) { if (Time.time >= botRespawnAt[i]) RespawnBot(i); continue; }
            KeepBotOutOfEnemySpawn(i);
            if (botTarget[i] < 0 || owner[botTarget[i]] == i + 1) botTarget[i] = FindBotTarget(i);
            var target = mailboxes[botTarget[i]].transform.position; bots[i].transform.position = Vector3.MoveTowards(bots[i].transform.position, target, 3.5f * Time.deltaTime);
            var flat = target - bots[i].transform.position; flat.y = 0; if (flat.sqrMagnitude > .1f) bots[i].transform.rotation = Quaternion.LookRotation(flat);
            if (Vector3.Distance(bots[i].transform.position, target) < 2.2f) { if (botCaptureStarted[i] == 0) botCaptureStarted[i] = Time.time; if (Time.time - botCaptureStarted[i] >= 3) { Capture(botTarget[i], i + 1); botCaptureStarted[i] = 0; botTarget[i] = -1; } } else botCaptureStarted[i] = 0;
            if (playerAlive && Time.time >= respawnShieldUntil && Time.time >= rollUntil && Vector3.Distance(bots[i].transform.position, player.transform.position) < 2.4f && Time.time >= botAttackAt[i]) { botAttackAt[i] = Time.time + .8f; playerHealth -= 18; if (playerHealth <= 0) KillPlayer(); }
        }
    }
    void KeepPlayerOutOfEnemySpawn() { for (var id = 1; id <= BotCount; id++) { var away = player.transform.position - TruckPosition(id); away.y = 0; if (away.sqrMagnitude < 196) { player.Move((away.sqrMagnitude < .01f ? Vector3.forward : away.normalized) * (14 - Mathf.Sqrt(away.sqrMagnitude) + .2f)); feedback = "상대 택배차 안전구역에는 들어갈 수 없습니다."; return; } } }
    void KeepBotOutOfEnemySpawn(int bot) { for (var id = 0; id <= BotCount; id++) if (id != bot + 1) { var away = bots[bot].transform.position - TruckPosition(id); away.y = 0; if (away.sqrMagnitude < 196) { bots[bot].transform.position += (away.sqrMagnitude < .01f ? Vector3.forward : away.normalized) * (14 - Mathf.Sqrt(away.sqrMagnitude) + .2f); return; } } }
    int FindBotTarget(int bot) { for (var n = 0; n < HouseCount; n++) { var i = (bot + n + Random.Range(0, HouseCount)) % HouseCount; if (owner[i] != bot + 1) return i; } return 0; }
    void UpdatePickups() { for (var i = 0; i < pickups.Length; i++) { if (!pickups[i].activeSelf && Time.time >= pickupRespawnAt[i]) pickups[i].SetActive(true); if (pickups[i].activeSelf) pickups[i].transform.position += Vector3.up * Mathf.Sin(Time.time * 3 + i) * .003f; } }
    void TryPickup()
    {
        for (var i = 0; i < pickups.Length; i++) if (pickups[i].activeSelf && Vector3.Distance(player.transform.position, pickups[i].transform.position) < 2.5f) { if (!HasEmptySlot()) { feedback = "아이템 칸이 가득 찼습니다. G로 현재 무기를 버리세요."; return; } AddWeapon(pickups[i].GetComponent<PickupKind>().kind); pickups[i].SetActive(false); pickupRespawnAt[i] = Time.time + 12; return; }
    }
    void UpdateCapture(Keyboard kb)
    {
        if (!playerAlive || kb == null || !kb.eKey.isPressed) { captureTarget = -1; captureStarted = 0; return; }
        var target = NearestMailbox(player.transform.position); if (target < 0 || owner[target] == 0 || Vector3.Distance(player.transform.position, mailboxes[target].transform.position) > 2.4f) { captureTarget = -1; captureStarted = 0; return; }
        if (captureTarget != target) { captureTarget = target; captureStarted = Time.time; }
        if (Time.time - captureStarted >= 3) { Capture(target, 0); captureTarget = -1; captureStarted = 0; }
    }
    int NearestMailbox(Vector3 position) { var best = -1; var distance = 99f; for (var i = 0; i < HouseCount; i++) { var d = Vector3.Distance(position, mailboxes[i].transform.position); if (d < distance) { distance = d; best = i; } } return best; }
    void Capture(int house, int playerId) { owner[house] = playerId; mailboxRenderers[house].material.color = colors[playerId]; feedback = (playerId == 0 ? "우편함 점령!" : "Rival " + playerId + "이 우편함을 탈환했습니다."); }

    WeaponKind Selected => inventory[selectedSlot];
    void SelectSlot(Keyboard kb) { if (kb == null) return; if (kb.digit1Key.wasPressedThisFrame) selectedSlot = 0; if (kb.digit2Key.wasPressedThisFrame) selectedSlot = 1; if (kb.digit3Key.wasPressedThisFrame) selectedSlot = 2; }
    bool HasEmptySlot() { for (var i = 0; i < SlotCount; i++) if (inventory[i] == WeaponKind.None) return true; return false; }
    string ItemName(WeaponKind item) => item switch { WeaponKind.ShampooGun => "고압 샴푸건", WeaponKind.Sprayer => "산업용 분무기", WeaponKind.StapleRifle => "전동 타카건", WeaponKind.PerfumeSniper => "향수 저격 분사기", WeaponKind.CompactBomb => "화장품 컴팩트", WeaponKind.EnergyDrink => "에너지드링크", WeaponKind.DeliveryDrone => "대형 배송 드론", _ => "빈손" };
    string SlotText(int slot) => inventory[slot] == WeaponKind.DeliveryDrone ? ItemName(inventory[slot]) + " 무제한" : inventory[slot] == WeaponKind.Sprayer ? ItemName(inventory[slot]) + " " + weaponSeconds[slot].ToString("0.0") + "초" : inventory[slot] == WeaponKind.None ? "빈손" : ItemName(inventory[slot]) + " " + weaponAmmo[slot];
    void AddWeapon(WeaponKind weapon) { for (var i = 0; i < SlotCount; i++) if (inventory[i] == WeaponKind.None) { inventory[i] = weapon; weaponAmmo[i] = Ammo(weapon); weaponSeconds[i] = weapon == WeaponKind.Sprayer ? 5f : 0; selectedSlot = i; feedback = ItemName(weapon) + " 획득!"; return; } }
    void DropSelectedWeapon() { if (Selected == WeaponKind.DeliveryDrone) { feedback = "배송 드론은 무제한 장비라 버릴 수 없습니다."; return; } if (Selected == WeaponKind.None) { feedback = "버릴 장비가 없습니다."; return; } feedback = ItemName(Selected) + "을 버렸습니다."; inventory[selectedSlot] = WeaponKind.None; weaponAmmo[selectedSlot] = 0; weaponSeconds[selectedSlot] = 0; }
    int Ammo(WeaponKind weapon) => weapon switch { WeaponKind.ShampooGun => 6, WeaponKind.StapleRifle => 30, WeaponKind.PerfumeSniper => 5, WeaponKind.CompactBomb => 2, WeaponKind.EnergyDrink => 3, WeaponKind.DeliveryDrone => 1, _ => 0 };
    void UseWeapon()
    {
        if (!playerAlive || Selected == WeaponKind.None || Time.time < nextAttackAt) return;
        respawnShieldUntil = 0;
        if (Selected == WeaponKind.Sprayer) { Spray(); return; }
        if (Selected == WeaponKind.EnergyDrink) { playerHealth = Mathf.Min(100, playerHealth + 30); feedback = "에너지드링크를 마셨습니다. 체력 +30"; Consume(); nextAttackAt = Time.time + 1.5f; return; }
        if (Selected == WeaponKind.DeliveryDrone) { droneFlying = !droneFlying; feedback = droneFlying ? "배송 드론 비행 시작! Space 상승 · Left Ctrl 하강 · 클릭으로 착륙" : "배송 드론 착륙"; nextAttackAt = Time.time + .5f; return; }
        if (Selected == WeaponKind.CompactBomb) { ThrowCompact(); nextAttackAt = Time.time + .65f; return; }
        nextAttackAt = Time.time + (Selected == WeaponKind.StapleRifle ? .1f : Selected == WeaponKind.PerfumeSniper ? .9f : .75f); Fire();
    }
    void Spray()
    {
        nextAttackAt = Time.time + .1f; weaponSeconds[selectedSlot] -= .1f;
        if (Physics.Raycast(cam.transform.position, cam.transform.forward, out var hit, 18f)) HitBot(hit.collider.gameObject, 11);
        if (weaponSeconds[selectedSlot] <= 0) { inventory[selectedSlot] = WeaponKind.None; weaponSeconds[selectedSlot] = 0; feedback = "산업용 분무기가 비었습니다."; }
    }
    void Fire()
    {
        var kind = Selected; var pellets = kind == WeaponKind.ShampooGun ? 5 : 1;
        for (var i = 0; i < pellets; i++) { var spread = kind == WeaponKind.ShampooGun ? Quaternion.Euler(Random.Range(-7f, 7f), Random.Range(-7f, 7f), 0) : Quaternion.identity; MakeShot(spread * cam.transform.forward, kind == WeaponKind.PerfumeSniper ? 180 : 120, kind == WeaponKind.ShampooGun ? 18 : kind == WeaponKind.PerfumeSniper ? 80 : kind == WeaponKind.StapleRifle ? 26 : 16, false); }
        Consume();
    }
    void ThrowCompact() { var bolt = Create("Compact Bomb", PrimitiveType.Sphere, cam.transform.position + cam.transform.forward, Vector3.one * .35f, WeaponColor(Selected)); var body = bolt.AddComponent<Rigidbody>(); body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; body.linearVelocity = cam.transform.forward * 18 + Vector3.up * 3; var shot = bolt.AddComponent<BrawlShot>(); shot.owner = this; shot.damage = 35; shot.explosive = true; Consume(); }
    void MakeShot(Vector3 direction, float speed, int damage, bool explosive) { var bolt = Create("Delivery Shot", PrimitiveType.Sphere, cam.transform.position + direction, Vector3.one * .1f, WeaponColor(Selected)); var body = bolt.AddComponent<Rigidbody>(); body.useGravity = false; body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; body.linearVelocity = direction * speed; var shot = bolt.AddComponent<BrawlShot>(); shot.owner = this; shot.damage = damage; shot.explosive = explosive; }
    void Consume() { weaponAmmo[selectedSlot]--; if (weaponAmmo[selectedSlot] <= 0) inventory[selectedSlot] = WeaponKind.None; }
    readonly int[] weaponAmmo = new int[SlotCount];
    void DamageBot(int index, int damage) { botHealth[index] -= damage; if (botHealth[index] <= 0) KillBot(index); }
    public void Explode(Vector3 position, int damage) { for (var i = 0; i < BotCount; i++) if (bots[i].activeSelf && Vector3.Distance(position, bots[i].transform.position) < 5f) { DamageBot(i, damage); InterruptBot(i); } feedback = "화장품 컴팩트 폭발!"; }
    int NearestBot(float range) { var best = -1; for (var i = 0; i < BotCount; i++) if (bots[i].activeSelf && Vector3.Distance(player.transform.position, bots[i].transform.position) < range && Vector3.Dot(player.transform.forward, (bots[i].transform.position - player.transform.position).normalized) > 0) best = i; return best; }
    void KillBot(int i) { bots[i].SetActive(false); botRespawnAt[i] = Time.time + 3; botCaptureStarted[i] = 0; feedback = "상대를 처치했습니다."; }
    void InterruptBot(int i) { botCaptureStarted[i] = 0; }
    public void HitBot(GameObject hit, int damage) { for (var i = 0; i < BotCount; i++) if (hit == bots[i]) { DamageBot(i, damage); return; } }
    void RespawnBot(int i) { bots[i].SetActive(true); bots[i].transform.position = SpawnPosition(i + 1); botHealth[i] = 100; botTarget[i] = -1; }
    void KillPlayer() { playerAlive = false; player.enabled = false; droneFlying = false; respawnShieldUntil = 0; playerRespawnAt = Time.time + 3; captureTarget = -1; captureStarted = 0; feedback = "쓰러졌습니다. 택배차에서 리스폰합니다."; }
    void RespawnPlayer() { playerAlive = true; playerHealth = 100; player.enabled = true; player.transform.position = SpawnPosition(0); respawnShieldUntil = Time.time + 5; feedback = "택배차 앞 도로에서 리스폰했습니다. 5초 보호 상태입니다."; }
    void UpdateHeldWeapon()
    {
        if (Selected == WeaponKind.None || !playerAlive) { if (heldWeapon != null) Destroy(heldWeapon); return; }
        if (Selected == WeaponKind.DeliveryDrone)
        {
            if (heldWeapon == null || heldWeapon.name != "Attached Delivery Drone")
            {
                if (heldWeapon != null) Destroy(heldWeapon);
                heldWeapon = new GameObject("Attached Delivery Drone"); heldWeapon.transform.SetParent(cam.transform, false); heldWeapon.transform.localPosition = new Vector3(.48f, -.38f, .8f);
                DronePart(PrimitiveType.Cube, Vector3.zero, new Vector3(.28f, .08f, .2f), new Color(.2f, .65f, 1));
                DronePart(PrimitiveType.Cube, Vector3.zero, new Vector3(.8f, .035f, .055f), new Color(.18f, .2f, .24f));
                DronePart(PrimitiveType.Cube, Vector3.zero, new Vector3(.055f, .035f, .55f), new Color(.18f, .2f, .24f));
                foreach (var x in new[] { -.34f, .34f }) foreach (var z in new[] { -.23f, .23f }) DronePart(PrimitiveType.Cylinder, new Vector3(x, .02f, z), new Vector3(.18f, .025f, .18f), new Color(.12f, .15f, .19f));
                DronePart(PrimitiveType.Cube, new Vector3(0, -.08f, 0), new Vector3(.12f, .12f, .12f), new Color(.55f, .34f, .18f));
            }
            return;
        }
        var longGun = Selected == WeaponKind.Sprayer || Selected == WeaponKind.StapleRifle || Selected == WeaponKind.PerfumeSniper;
        if (heldWeapon == null || heldWeapon.name != "Held " + Selected) { if (heldWeapon != null) Destroy(heldWeapon); heldWeapon = Create("Held " + Selected, longGun ? PrimitiveType.Capsule : PrimitiveType.Cube, Vector3.zero, Vector3.one, WeaponColor(Selected)); heldWeapon.GetComponent<Collider>().enabled = false; heldWeapon.transform.SetParent(cam.transform, false); }
        heldWeapon.transform.localPosition = new Vector3(.48f, -.45f, .75f); heldWeapon.transform.localRotation = Quaternion.Euler(longGun ? 90 : 20, 0, -25); heldWeapon.transform.localScale = longGun ? new Vector3(.17f, .5f, .17f) : new Vector3(.18f, .75f, .18f);
    }
    void CreateExploreDrone()
    {
        heldWeapon = new GameObject("Attached Delivery Drone");
        heldWeapon.transform.SetParent(cam.transform, false);
        heldWeapon.transform.localPosition = new Vector3(.58f, -.48f, 1.35f);
        heldWeapon.transform.localScale = Vector3.one * .45f;
        DronePart(PrimitiveType.Cube, Vector3.zero, new Vector3(.32f, .1f, .24f), new Color(.12f, .55f, 1f));
        DronePart(PrimitiveType.Cube, Vector3.zero, new Vector3(.9f, .04f, .06f), new Color(.12f, .15f, .2f));
        DronePart(PrimitiveType.Cube, Vector3.zero, new Vector3(.06f, .04f, .6f), new Color(.12f, .15f, .2f));
        foreach (var x in new[] { -.38f, .38f }) foreach (var z in new[] { -.25f, .25f }) DronePart(PrimitiveType.Cylinder, new Vector3(x, .03f, z), new Vector3(.2f, .025f, .2f), new Color(.08f, .1f, .14f));
        DronePart(PrimitiveType.Cube, new Vector3(0, -.1f, 0), new Vector3(.14f, .14f, .14f), new Color(1f, .55f, .1f));
    }
    void DronePart(PrimitiveType type, Vector3 localPosition, Vector3 scale, Color color) { var part = Create("Drone Part", type, Vector3.zero, scale, color); part.transform.SetParent(heldWeapon.transform, false); part.transform.localPosition = localPosition; part.GetComponent<Collider>().enabled = false; }
    void Begin()
    {
        started = true; finished = false; playerAlive = true; playerHealth = 100; stamina = 100; droneFlying = false; rollUntil = 0; crouching = false; startedAt = Time.time; selectedSlot = 0; captureTarget = -1; captureStarted = 0; feedback = "배송 드론은 무제한으로 사용할 수 있습니다."; for (var i = 0; i < HouseCount; i++) { owner[i] = -1; mailboxRenderers[i].material.color = Color.gray; } for (var i = 0; i < SlotCount; i++) { inventory[i] = WeaponKind.None; weaponAmmo[i] = 0; weaponSeconds[i] = 0; } inventory[0] = WeaponKind.DeliveryDrone;
        RespawnPlayer(); for (var i = 0; i < BotCount; i++) RespawnBot(i); for (var i = 0; i < pickups.Length; i++) pickups[i].SetActive(true); Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
    }
    void Finish() { finished = true; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    int Score(int id) { var total = 0; for (var i = 0; i < HouseCount; i++) if (owner[i] == id) total++; return total; }
    int Rank(int id) { var rank = 1; for (var other = 0; other <= BotCount; other++) if (Score(other) > Score(id)) rank++; return rank; }
    string ResultBoard() { var board = ""; for (var rank = 1; rank <= BotCount + 1; rank++) for (var id = 0; id <= BotCount; id++) if (Rank(id) == rank) board += rank + "위  " + (id == 0 ? "나" : "Rival " + id) + "  ·  " + Score(id) + "개 점령\n"; return board; }
    void OnGUI()
    {
        var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 18, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
        if (mapExploreMode) { GUI.Box(new Rect(12, 12, 430, 42), "드론 비행 중  ·  WASD 이동  ·  마우스 시야  ·  Space 상승  ·  Left Ctrl 하강  ·  좌클릭 착륙/재이륙"); return; }
        if (mapEditMode) { GUI.Box(new Rect(15, 15, 600, 70), "맵 제작 모드  ·  WASD: 이동  ·  Q/E: 하강/상승  ·  Shift: 빠르게  ·  마우스: 시점"); return; }
        if (!started) { GUI.Box(new Rect(Screen.width / 2 - 300, Screen.height / 2 - 130, 600, 260), "MAILBOX BRAWL\n\n무지 택배를 열어야 내용물을 알 수 있습니다.\n우체통 앞에서 E를 3초간 유지해 점령하세요.\n1번 슬롯에 무제한 배송 드론이 기본 지급됩니다.\n\nEnter 또는 Space로 시작"); return; }
        if (finished) { GUI.Box(new Rect(Screen.width / 2 - 260, Screen.height / 2 - 180, 520, 360), "경기 종료\n\n내 순위: " + Rank(0) + "위  ·  점령: " + Score(0) + " / " + HouseCount + "\n\n[ 최종 순위 ]\n" + ResultBoard() + "\nEnter로 다시 시작"); return; }
        GUI.Box(new Rect(15, 15, 760, 110), "점령 " + Score(0) + "/" + HouseCount + "  ·  체력 " + playerHealth + "  ·  스태미나 " + stamina.ToString("0") + (droneFlying ? "  ·  드론 비행 중 (무제한)" : "") + (Time.time < respawnShieldUntil ? "  ·  보호 " + (respawnShieldUntil - Time.time).ToString("0.0") + "초" : "") + "  ·  남은 시간 " + Mathf.Max(0, roundSeconds - (Time.time - startedAt)).ToString("0") + "초\n" + feedback + "\n마우스 좌클릭: 드론 전환/장비 사용 · Shift: 달리기 · C: 숙이기 · Left Ctrl: 구르기/드론 하강 · Space: 점프/드론 상승 · G: 현재 장비 버리기");
        if (captureTarget >= 0) GUI.Box(new Rect(Screen.width / 2 - 130, Screen.height / 2 + 40, 260, 30), "점령 중 " + Mathf.Clamp01((Time.time - captureStarted) / 3).ToString("P0"));
        for (var i = 0; i < SlotCount; i++) GUI.Box(new Rect(Screen.width / 2 - 202 + i * 135, Screen.height - 75, 125, 52), (i == selectedSlot ? "[" : "") + (i + 1) + ": " + SlotText(i) + (i == selectedSlot ? "]" : ""));
        if (Selected != WeaponKind.None) GUI.Label(new Rect(Screen.width / 2 - 12, Screen.height / 2 - 20, 24, 40), "+", style);
        for (var i = 0; i < HouseCount; i++) Label(mailboxes[i], owner[i] < 0 ? "빈 우체통" : "점령: P" + (owner[i] + 1), style);
        if (pickups[pickups.Length - 1].activeSelf) Label(pickups[pickups.Length - 1], "▼ 배송 드론 테스트 상자 [E] ▼", style);
    }
    void Label(GameObject go, string text, GUIStyle style) { var p = cam.WorldToScreenPoint(go.transform.position + Vector3.up * 2); if (p.z > 0) GUI.Label(new Rect(p.x - 100, Screen.height - p.y, 200, 24), text, style); }
}

public sealed class PickupKind : MonoBehaviour { public WeaponKind kind; }
public sealed class BrawlShot : MonoBehaviour { public MonsterDeliveryPrototype owner; public int damage; public bool explosive; void Start() => Destroy(gameObject, 2); void OnCollisionEnter(Collision c) { if (explosive) owner.Explode(transform.position, damage); else owner.HitBot(c.gameObject, damage); Destroy(gameObject); } }
