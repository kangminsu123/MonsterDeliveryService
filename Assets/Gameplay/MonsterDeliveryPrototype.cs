using UnityEngine;
using UnityEngine.InputSystem;

public enum WeaponKind { None, Axe, Hammer, Sword, Rifle, Sniper }

public sealed class MonsterDeliveryPrototype : MonoBehaviour
{
    const int HouseCount = 10, BotCount = 3, SlotCount = 3;
    readonly Color[] colors = { new(.2f, .75f, 1f), new(1f, .25f, .25f), new(.95f, .75f, .1f), new(.65f, .3f, 1f) };
    readonly GameObject[] mailboxes = new GameObject[HouseCount], houses = new GameObject[HouseCount], bots = new GameObject[BotCount], pickups = new GameObject[9];
    readonly Renderer[] mailboxRenderers = new Renderer[HouseCount], botRenderers = new Renderer[BotCount];
    readonly int[] owner = new int[HouseCount], botHealth = new int[BotCount], botTarget = new int[BotCount];
    readonly float[] botRespawnAt = new float[BotCount], botCaptureStarted = new float[BotCount], botAttackAt = new float[BotCount], pickupRespawnAt = new float[9];
    readonly WeaponKind[] inventory = new WeaponKind[SlotCount];
    CharacterController player; Camera cam; GameObject heldWeapon;
    int selectedSlot, playerHealth = 100, captureTarget = -1;
    bool started, finished, playerAlive = true;
    float startedAt, lookPitch, vertical, captureStarted, playerRespawnAt, nextAttackAt, respawnShieldUntil, stamina = 100;
    string feedback = "Enter를 눌러 우편함 난투를 시작하세요.";
    [Min(30)] public float roundSeconds = 180;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install() { if (FindAnyObjectByType<MonsterDeliveryPrototype>() == null) new GameObject("MailboxBrawlPrototype").AddComponent<MonsterDeliveryPrototype>(); }

    void Start() { RenderSettings.ambientLight = new Color(.08f, .06f, .12f); CreateWorld(); Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    void CreateWorld()
    {
        Ground("Suburb Lawn", new Vector3(0, -.5f, 0), new Vector3(220, 1, 180), new Color(.09f, .2f, .1f));
        CreateRoads();
        player = Create("Player", PrimitiveType.Capsule, SpawnPosition(0), Vector3.one, colors[0]).AddComponent<CharacterController>(); player.height = 2; player.radius = .45f;
        cam = Camera.main ?? new GameObject("Main Camera").AddComponent<Camera>(); cam.tag = "MainCamera";
        for (var i = 0; i < 4; i++) CreateTruck(i);
        for (var i = 0; i < HouseCount; i++) CreateHouse(i);
        for (var i = 0; i < BotCount; i++) CreateBot(i);
        CreatePickups();
        var light = FindAnyObjectByType<Light>(); if (light != null) { light.intensity = 1.4f; light.color = new Color(.65f, .55f, 1); }
    }
    Vector3 TruckPosition(int id) => new[] { new Vector3(-96, 0, 0), new Vector3(0, 0, 76), new Vector3(96, 0, 0), new Vector3(0, 0, -76) }[id];
    Vector3 SpawnPosition(int id) => TruckPosition(id) - TruckPosition(id).normalized * 8f + Vector3.up;
    void CreateTruck(int id) { var p = TruckPosition(id); var truck = Create("Delivery Van " + id, PrimitiveType.Cube, p + Vector3.up * 1.5f, new Vector3(7, 3, 8), colors[id]); truck.transform.LookAt(Vector3.zero); }
    void CreateHouse(int i)
    {
        var p = new[] { new Vector3(-76, 3, 55), new Vector3(-42, 3, 55), new Vector3(42, 3, 55), new Vector3(76, 3, 55), new Vector3(-76, 3, -55), new Vector3(-42, 3, -55), new Vector3(42, 3, -55), new Vector3(76, 3, -55), new Vector3(-55, 3, 78), new Vector3(55, 3, -78) }[i]; var x = p.x; var side = p.z > 0 ? 1 : -1;
        var wall = new[] { new Color(.72f, .55f, .4f), new Color(.55f, .65f, .74f), new Color(.78f, .72f, .58f), new Color(.63f, .48f, .42f), new Color(.66f, .7f, .55f) }[i % 5];
        houses[i] = Create("House " + (i + 1), PrimitiveType.Cube, p, new Vector3(18, 6, 14), wall);
        var roof = Create("Roof " + (i + 1), PrimitiveType.Cube, p + Vector3.up * 3.6f, new Vector3(19.5f, 1.3f, 15.5f), new Color(.2f, .12f, .1f));
        var front = new Vector3(x, 1.7f, side * 40.85f); Create("Door " + (i + 1), PrimitiveType.Cube, front, new Vector3(2.2f, 3.4f, .25f), new Color(.18f, .1f, .06f));
        Create("Window L " + (i + 1), PrimitiveType.Cube, front + new Vector3(-5, .8f, 0), new Vector3(3.2f, 2.2f, .2f), new Color(.2f, .65f, .85f));
        Create("Window R " + (i + 1), PrimitiveType.Cube, front + new Vector3(5, .8f, 0), new Vector3(3.2f, 2.2f, .2f), new Color(.2f, .65f, .85f));
        var mb = new Vector3(x, 1, side * 15);
        mailboxes[i] = Create("Mailbox " + (i + 1), PrimitiveType.Cube, mb, new Vector3(1.4f, 1.6f, 1.4f), Color.gray); mailboxRenderers[i] = mailboxes[i].GetComponent<Renderer>();
        var tag = new GameObject("Tag Zone " + i); tag.transform.position = mb; var c = tag.AddComponent<SphereCollider>(); c.radius = 2.3f; c.isTrigger = true;
    }
    void CreateBot(int i) { bots[i] = Create("Rival " + (i + 1), PrimitiveType.Capsule, SpawnPosition(i + 1), Vector3.one, colors[i + 1]); botRenderers[i] = bots[i].GetComponent<Renderer>(); }
    void CreatePickups()
    {
        var kinds = new[] { WeaponKind.Hammer, WeaponKind.Sword, WeaponKind.Rifle, WeaponKind.Sniper, WeaponKind.Rifle, WeaponKind.Hammer, WeaponKind.Sword, WeaponKind.Rifle, WeaponKind.Sniper };
        for (var i = 0; i < pickups.Length; i++) { var a = i * Mathf.PI * 2 / pickups.Length; var p = new Vector3(Mathf.Sin(a) * 34, 1.2f, Mathf.Cos(a) * 28); pickups[i] = Create("Pickup " + kinds[i], PrimitiveType.Capsule, p, new Vector3(.6f, .6f, .6f), WeaponColor(kinds[i])); pickups[i].AddComponent<PickupKind>().kind = kinds[i]; }
    }
    void CreateRoads()
    {
        Ground("Main Street", Vector3.zero, new Vector3(220, .08f, 22), new Color(.12f, .12f, .13f));
        Ground("Cross Street", Vector3.zero, new Vector3(22, .08f, 180), new Color(.12f, .12f, .13f));
        Ground("North Sidewalk", new Vector3(0, .06f, 14), new Vector3(220, .12f, 5), new Color(.48f, .47f, .43f));
        Ground("South Sidewalk", new Vector3(0, .06f, -14), new Vector3(220, .12f, 5), new Color(.48f, .47f, .43f));
        Ground("West Cross Sidewalk", new Vector3(-14, .06f, 0), new Vector3(5, .12f, 180), new Color(.48f, .47f, .43f));
        Ground("East Cross Sidewalk", new Vector3(14, .06f, 0), new Vector3(5, .12f, 180), new Color(.48f, .47f, .43f));
        for (var x = -100; x <= 100; x += 14) Ground("Lane Mark", new Vector3(x, .11f, 0), new Vector3(6, .03f, .35f), new Color(.95f, .72f, .18f));
        for (var z = -80; z <= 80; z += 14) Ground("Cross Lane Mark", new Vector3(0, .11f, z), new Vector3(.35f, .03f, 6), new Color(.95f, .72f, .18f));
        for (var x = -90; x <= 90; x += 18) { CreateStreetLight(new Vector3(x, 0, 18)); CreateStreetLight(new Vector3(x, 0, -18)); }
        for (var x = -90; x <= 90; x += 18) { CreateTree(new Vector3(x + 7, 0, 29)); CreateTree(new Vector3(x - 7, 0, -29)); }
    }
    void CreateStreetLight(Vector3 p) { var pole = Create("Street Light", PrimitiveType.Cylinder, p + Vector3.up * 4, new Vector3(.2f, 4, .2f), new Color(.12f, .12f, .14f)); var lamp = Create("Street Lamp", PrimitiveType.Sphere, p + Vector3.up * 8, Vector3.one * .65f, new Color(1f, .72f, .28f)); var light = lamp.AddComponent<Light>(); light.range = 15; light.intensity = 2.2f; light.color = new Color(1f, .7f, .3f); }
    void CreateTree(Vector3 p) { Create("Tree Trunk", PrimitiveType.Cylinder, p + Vector3.up * 2, new Vector3(.55f, 2, .55f), new Color(.25f, .13f, .06f)); Create("Tree Crown", PrimitiveType.Sphere, p + Vector3.up * 5, Vector3.one * 3.4f, new Color(.08f, .34f, .12f)); }
    GameObject Create(string name, PrimitiveType type, Vector3 position, Vector3 scale, Color color) { var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.position = position; go.transform.localScale = scale; go.GetComponent<Renderer>().material.color = color; return go; }
    void Ground(string name, Vector3 position, Vector3 scale, Color color) => Create(name, PrimitiveType.Cube, position, scale, color);
    Color WeaponColor(WeaponKind kind) => kind switch { WeaponKind.Rifle => new Color(.2f, .9f, 1), WeaponKind.Sniper => new Color(1, .2f, .8f), WeaponKind.Hammer => new Color(1, .55f, .12f), WeaponKind.Sword => new Color(.8f, .8f, .9f), _ => Color.white };

    void Update()
    {
        UpdateCamera(); var kb = Keyboard.current;
        if (!started) { if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) Begin(); return; }
        if (finished) { if (kb != null && kb.enterKey.wasPressedThisFrame) Begin(); return; }
        if (Time.time - startedAt >= roundSeconds) { Finish(); return; }
        UpdatePlayer(kb); UpdateBots(); UpdatePickups(); UpdateCapture(kb); UpdateHeldWeapon();
    }
    void UpdatePlayer(Keyboard kb)
    {
        if (!playerAlive) { if (Time.time >= playerRespawnAt) RespawnPlayer(); return; }
        var move = Vector3.zero; if (kb != null) { if (kb.wKey.isPressed) move.z++; if (kb.sKey.isPressed) move.z--; if (kb.aKey.isPressed) move.x--; if (kb.dKey.isPressed) move.x++; }
        var sprinting = move.sqrMagnitude > 0 && kb != null && kb.leftShiftKey.isPressed && stamina > 0; stamina = Mathf.Clamp(stamina + (sprinting ? -28 : 20) * Time.deltaTime, 0, 100);
        player.Move(player.transform.TransformDirection(move.normalized) * (sprinting ? 10.5f : 6.5f) * Time.deltaTime);
        var grounded = player.isGrounded; if (grounded && vertical < 0) vertical = -2; if (grounded && kb != null && kb.spaceKey.isPressed) vertical = 7.5f; vertical += -20 * Time.deltaTime; player.Move(Vector3.up * vertical * Time.deltaTime);
        KeepPlayerOutOfEnemySpawn();
        SelectSlot(kb); if (kb != null && kb.gKey.wasPressedThisFrame) DropSelectedWeapon(); if (kb != null && kb.eKey.wasPressedThisFrame) TryPickup(); if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) UseWeapon();
    }
    void UpdateCamera()
    {
        var mouse = Mouse.current; if (started && playerAlive && mouse != null) { var d = mouse.delta.ReadValue(); player.transform.Rotate(0, d.x * .12f, 0); lookPitch = Mathf.Clamp(lookPitch - d.y * .12f, -80, 80); }
        if (player == null) return; cam.transform.position = player.transform.position + Vector3.up * 1.6f; cam.transform.rotation = Quaternion.Euler(lookPitch, player.transform.eulerAngles.y, 0);
        var aim = (Selected == WeaponKind.Rifle || Selected == WeaponKind.Sniper) && mouse != null && mouse.rightButton.isPressed; cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, aim ? 42 : 60, Time.deltaTime * 12);
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
            if (playerAlive && Time.time >= respawnShieldUntil && Vector3.Distance(bots[i].transform.position, player.transform.position) < 2.4f && Time.time >= botAttackAt[i]) { botAttackAt[i] = Time.time + .8f; playerHealth -= 18; if (playerHealth <= 0) KillPlayer(); }
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
    void AddWeapon(WeaponKind weapon) { for (var i = 0; i < SlotCount; i++) if (inventory[i] == WeaponKind.None) { inventory[i] = weapon; weaponAmmo[i] = Ammo(weapon); selectedSlot = i; feedback = weapon + " 획득!"; return; } }
    void DropSelectedWeapon() { if (Selected == WeaponKind.None) { feedback = "버릴 무기가 없습니다."; return; } feedback = Selected + "을 버렸습니다."; inventory[selectedSlot] = WeaponKind.None; weaponAmmo[selectedSlot] = 0; }
    int Ammo(WeaponKind weapon) => weapon == WeaponKind.Rifle ? 30 : weapon == WeaponKind.Sniper ? 5 : 0;
    void UseWeapon()
    {
        if (!playerAlive || Selected == WeaponKind.None || Time.time < nextAttackAt) return;
        respawnShieldUntil = 0;
        var gun = Selected == WeaponKind.Rifle || Selected == WeaponKind.Sniper; nextAttackAt = Time.time + (Selected == WeaponKind.Sword ? .28f : gun ? (Selected == WeaponKind.Rifle ? .12f : .7f) : .5f);
        if (gun) { Fire(); return; }
        var range = Selected == WeaponKind.Hammer ? 3.2f : Selected == WeaponKind.Sword ? 2.6f : 2.9f; var target = NearestBot(range); if (target < 0) return;
        DamageBot(target, Selected == WeaponKind.Hammer ? 40 : Selected == WeaponKind.Sword ? 20 : 28); if (Selected == WeaponKind.Hammer) InterruptBot(target);
    }
    void Fire()
    {
        var kind = Selected; var bolt = Create("Bullet", PrimitiveType.Sphere, cam.transform.position + cam.transform.forward, Vector3.one * .1f, WeaponColor(kind)); var body = bolt.AddComponent<Rigidbody>(); body.useGravity = false; body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; body.linearVelocity = cam.transform.forward * (kind == WeaponKind.Sniper ? 180 : 120); var shot = bolt.AddComponent<BrawlShot>(); shot.owner = this; shot.damage = kind == WeaponKind.Sniper ? 80 : 16;
        // ponytail: one magazine per pickup; add reload only when a reload loop proves useful.
        weaponAmmo[selectedSlot]--; if (weaponAmmo[selectedSlot] <= 0) inventory[selectedSlot] = WeaponKind.None;
    }
    readonly int[] weaponAmmo = new int[SlotCount];
    void DamageBot(int index, int damage) { botHealth[index] -= damage; if (botHealth[index] <= 0) KillBot(index); }
    int NearestBot(float range) { var best = -1; for (var i = 0; i < BotCount; i++) if (bots[i].activeSelf && Vector3.Distance(player.transform.position, bots[i].transform.position) < range && Vector3.Dot(player.transform.forward, (bots[i].transform.position - player.transform.position).normalized) > 0) best = i; return best; }
    void KillBot(int i) { bots[i].SetActive(false); botRespawnAt[i] = Time.time + 3; botCaptureStarted[i] = 0; feedback = "상대를 처치했습니다."; }
    void InterruptBot(int i) { botCaptureStarted[i] = 0; }
    public void HitBot(GameObject hit, int damage) { for (var i = 0; i < BotCount; i++) if (hit == bots[i]) { DamageBot(i, damage); return; } }
    void RespawnBot(int i) { bots[i].SetActive(true); bots[i].transform.position = SpawnPosition(i + 1); botHealth[i] = 100; botTarget[i] = -1; }
    void KillPlayer() { playerAlive = false; player.enabled = false; respawnShieldUntil = 0; playerRespawnAt = Time.time + 3; captureTarget = -1; captureStarted = 0; feedback = "쓰러졌습니다. 택배차에서 리스폰합니다."; }
    void RespawnPlayer() { playerAlive = true; playerHealth = 100; player.enabled = true; player.transform.position = SpawnPosition(0); respawnShieldUntil = Time.time + 5; feedback = "택배차 앞 도로에서 리스폰했습니다. 5초 보호 상태입니다."; }
    void UpdateHeldWeapon()
    {
        if (Selected == WeaponKind.None || !playerAlive) { if (heldWeapon != null) Destroy(heldWeapon); return; }
        if (heldWeapon == null || heldWeapon.name != "Held " + Selected) { if (heldWeapon != null) Destroy(heldWeapon); heldWeapon = Create("Held " + Selected, Selected == WeaponKind.Rifle || Selected == WeaponKind.Sniper ? PrimitiveType.Capsule : PrimitiveType.Cube, Vector3.zero, Vector3.one, WeaponColor(Selected)); heldWeapon.GetComponent<Collider>().enabled = false; heldWeapon.transform.SetParent(cam.transform, false); }
        heldWeapon.transform.localPosition = new Vector3(.48f, -.45f, .75f); heldWeapon.transform.localRotation = Quaternion.Euler(Selected == WeaponKind.Rifle || Selected == WeaponKind.Sniper ? 90 : 20, 0, -25); heldWeapon.transform.localScale = Selected == WeaponKind.Rifle || Selected == WeaponKind.Sniper ? new Vector3(.17f, .5f, .17f) : new Vector3(.18f, .75f, .18f);
    }
    void Begin()
    {
        started = true; finished = false; playerAlive = true; playerHealth = 100; stamina = 100; startedAt = Time.time; selectedSlot = 0; captureTarget = -1; captureStarted = 0; feedback = "우체통 앞에서 E를 3초간 누르세요."; for (var i = 0; i < HouseCount; i++) { owner[i] = -1; mailboxRenderers[i].material.color = Color.gray; } for (var i = 0; i < SlotCount; i++) { inventory[i] = WeaponKind.None; weaponAmmo[i] = 0; } inventory[0] = WeaponKind.Axe;
        RespawnPlayer(); for (var i = 0; i < BotCount; i++) RespawnBot(i); for (var i = 0; i < pickups.Length; i++) pickups[i].SetActive(true); Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
    }
    void Finish() { finished = true; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    int Score(int id) { var total = 0; for (var i = 0; i < HouseCount; i++) if (owner[i] == id) total++; return total; }
    int Rank(int id) { var rank = 1; for (var other = 0; other <= BotCount; other++) if (Score(other) > Score(id)) rank++; return rank; }
    string ResultBoard() { var board = ""; for (var rank = 1; rank <= BotCount + 1; rank++) for (var id = 0; id <= BotCount; id++) if (Rank(id) == rank) board += rank + "위  " + (id == 0 ? "나" : "Rival " + id) + "  ·  " + Score(id) + "개 점령\n"; return board; }
    void OnGUI()
    {
        var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 18, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
        if (!started) { GUI.Box(new Rect(Screen.width / 2 - 300, Screen.height / 2 - 130, 600, 260), "MAILBOX BRAWL\n\n우체통 앞에서 E를 3초간 유지해 점령하세요.\n상대의 우체통도 같은 방식으로 탈환할 수 있습니다.\n근접 무기로 시작하고, 공중 아이템에서 한정 탄창 총을 획득하세요.\n\nEnter 또는 Space로 시작"); return; }
        if (finished) { GUI.Box(new Rect(Screen.width / 2 - 260, Screen.height / 2 - 180, 520, 360), "경기 종료\n\n내 순위: " + Rank(0) + "위  ·  점령: " + Score(0) + " / " + HouseCount + "\n\n[ 최종 순위 ]\n" + ResultBoard() + "\nEnter로 다시 시작"); return; }
        GUI.Box(new Rect(15, 15, 620, 110), "점령 " + Score(0) + "/" + HouseCount + "  ·  체력 " + playerHealth + "  ·  스태미나 " + stamina.ToString("0") + (Time.time < respawnShieldUntil ? "  ·  보호 " + (respawnShieldUntil - Time.time).ToString("0.0") + "초" : "") + "  ·  남은 시간 " + Mathf.Max(0, roundSeconds - (Time.time - startedAt)).ToString("0") + "초\n" + feedback + "\nShift: 달리기 · Space 길게: 연속 점프 · G: 현재 무기 버리기");
        if (captureTarget >= 0) GUI.Box(new Rect(Screen.width / 2 - 130, Screen.height / 2 + 40, 260, 30), "점령 중 " + Mathf.Clamp01((Time.time - captureStarted) / 3).ToString("P0"));
        for (var i = 0; i < SlotCount; i++) GUI.Box(new Rect(Screen.width / 2 - 157 + i * 105, Screen.height - 75, 95, 52), (i == selectedSlot ? "[" : "") + (i + 1) + ": " + inventory[i] + (i == selectedSlot ? "]" : ""));
        if (Selected != WeaponKind.None) GUI.Label(new Rect(Screen.width / 2 - 12, Screen.height / 2 - 20, 24, 40), "+", style);
        for (var i = 0; i < HouseCount; i++) Label(mailboxes[i], owner[i] < 0 ? "빈 우체통" : "점령: P" + (owner[i] + 1), style);
    }
    void Label(GameObject go, string text, GUIStyle style) { var p = cam.WorldToScreenPoint(go.transform.position + Vector3.up * 2); if (p.z > 0) GUI.Label(new Rect(p.x - 100, Screen.height - p.y, 200, 24), text, style); }
}

public sealed class PickupKind : MonoBehaviour { public WeaponKind kind; }
public sealed class BrawlShot : MonoBehaviour { public MonsterDeliveryPrototype owner; public int damage; void Start() => Destroy(gameObject, 2); void OnCollisionEnter(Collision c) { owner.HitBot(c.gameObject, damage); Destroy(gameObject); } }
