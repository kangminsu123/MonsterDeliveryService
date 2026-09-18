using UnityEngine;
using UnityEngine.InputSystem;

public enum ItemKind { None, Axe, StunGun }

public sealed class MonsterDeliveryPrototype : MonoBehaviour
{
    const int DeliveryCount = 3;
    CharacterController player;
    Camera cam;
    readonly GameObject[] parcels = new GameObject[DeliveryCount], mailboxes = new GameObject[DeliveryCount], markers = new GameObject[DeliveryCount], monsters = new GameObject[3];
    readonly Renderer[] monsterRenderers = new Renderer[3];
    readonly int[] monsterHealth = new int[3];
    readonly float[] monsterStunnedUntil = new float[3];
    Rigidbody parcelBody;
    GameObject currentParcel, stunGun, axe, heldWeaponVisual;
    readonly ItemKind[] inventory = new ItemKind[4];
    int activeDelivery, impacts;
    int selectedSlot;
    bool ordered, holdingParcel, finished;
    float startedAt, finishedElapsed, lookPitch, vertical, nextActionAt, axeSwingUntil;
    Vector3 knockback;
    [Min(1f)] public float timeLimitSeconds = 300f;
    string feedback = "Enter를 눌러 택배차에서 오늘의 배송을 시작하세요.";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install() { if (FindAnyObjectByType<MonsterDeliveryPrototype>() == null) new GameObject("MonsterDeliveryPrototype").AddComponent<MonsterDeliveryPrototype>(); }

    void Start()
    {
        RenderSettings.ambientLight = new Color(.06f, .035f, .1f);
        CreateWorld(); Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
    }

    void CreateWorld()
    {
        Ground("Town Ground", new Vector3(0, -.5f, 35), new Vector3(150, 1, 180), new Color(.035f, .035f, .065f));
        Ground("Main Street", new Vector3(0, .02f, 36), new Vector3(12, .05f, 155), new Color(.09f, .1f, .13f));
        for (var z = -32; z < 112; z += 16) { Ground("Road Stripe", new Vector3(0, .06f, z), new Vector3(.35f, .03f, 6), new Color(.95f, .75f, .18f)); Ground("Sidewalk L", new Vector3(-11, .12f, z + 7), new Vector3(4, .18f, 15), new Color(.38f, .38f, .43f)); Ground("Sidewalk R", new Vector3(11, .12f, z + 7), new Vector3(4, .18f, 15), new Color(.38f, .38f, .43f)); }
        player = Create("Delivery Rider", PrimitiveType.Capsule, new Vector3(0, 1, -42), Vector3.one, new Color(1f, .32f, .06f)).AddComponent<CharacterController>(); player.height = 2; player.radius = .45f;
        cam = Camera.main ?? new GameObject("Main Camera").AddComponent<Camera>(); cam.tag = "MainCamera";
        CreateTruck();
        CreateHouse(0, new Vector3(-23, 2.5f, 12), "12 Maple Street", new Color(.48f, .18f, .12f));
        CreateHouse(1, new Vector3(23, 2.5f, 48), "38 Oak Avenue", new Color(.16f, .32f, .5f));
        CreateHouse(2, new Vector3(-23, 2.5f, 88), "77 Pine Road", new Color(.25f, .45f, .2f));
        CreateMonster(0, new Vector3(4, 1, 16)); CreateMonster(1, new Vector3(-5, 1, 53)); CreateMonster(2, new Vector3(5, 1, 88));
        stunGun = Create("One Shot Stun Gun", PrimitiveType.Cube, new Vector3(-3, .9f, -37), new Vector3(.35f, .25f, .9f), Color.cyan);
        axe = Create("Delivery Axe", PrimitiveType.Cube, new Vector3(3, .9f, -37), new Vector3(.18f, .9f, .18f), new Color(.7f, .7f, .74f));
        var light = FindAnyObjectByType<Light>(); if (light != null) { light.color = new Color(.5f, .45f, 1f); light.intensity = 1.3f; }
    }
    void CreateTruck()
    {
        Ground("Delivery Truck Body", new Vector3(0, 1.4f, -31), new Vector3(7, 2.8f, 12), new Color(.82f, .3f, .05f)); Ground("Truck Cargo Bay", new Vector3(0, 3.3f, -33), new Vector3(6.5f, 2, 7), new Color(.15f, .1f, .12f));
        for (var i = 0; i < DeliveryCount; i++) { parcels[i] = Create("Parcel " + (i + 1), PrimitiveType.Cube, ParcelPosition(i), Vector3.one, new Color(.95f, .7f, .23f)); parcels[i].AddComponent<Rigidbody>().isKinematic = true; parcels[i].AddComponent<ParcelImpact>().owner = this; }
    }
    void CreateHouse(int index, Vector3 position, string address, Color color)
    {
        Create(address, PrimitiveType.Cube, position, new Vector3(10, 5, 9), color); Create(address + " Roof", PrimitiveType.Cube, position + Vector3.up * 3.1f, new Vector3(11, 1.2f, 10), new Color(.12f, .04f, .05f));
        var mailboxPosition = position + new Vector3(position.x < 0 ? 7f : -7f, -1.25f, -2f);
        mailboxes[index] = Create(address + " Mailbox", PrimitiveType.Cube, mailboxPosition, new Vector3(1.5f, 1.5f, 1.5f), new Color(.03f, .9f, .85f));
        var trigger = mailboxes[index].AddComponent<BoxCollider>(); trigger.isTrigger = true; trigger.size = new Vector3(3, 2.5f, 3); var delivery = mailboxes[index].AddComponent<MailboxTrigger>(); delivery.owner = this; delivery.index = index;
        markers[index] = Create(address + " Marker", PrimitiveType.Cylinder, mailboxPosition + Vector3.up * 7, new Vector3(.55f, .08f, .55f), Color.yellow); markers[index].GetComponent<Collider>().enabled = false;
    }
    void CreateMonster(int index, Vector3 position) { monsters[index] = Create("Street Monster " + index, PrimitiveType.Capsule, position, new Vector3(1.25f, 1.7f, 1.25f), new Color(.1f, .01f, .1f)); monsterRenderers[index] = monsters[index].GetComponent<Renderer>(); monsterHealth[index] = 3; }
    GameObject Create(string name, PrimitiveType type, Vector3 position, Vector3 scale, Color color) { var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetPositionAndRotation(position, Quaternion.identity); go.transform.localScale = scale; go.GetComponent<Renderer>().material.color = color; return go; }
    void Ground(string name, Vector3 position, Vector3 scale, Color color) => Create(name, PrimitiveType.Cube, position, scale, color);
    Vector3 ParcelPosition(int index) => new Vector3(-2.2f + index * 2.2f, 1.2f, -38.6f);

    void Update()
    {
        UpdateCamera(); var kb = Keyboard.current;
        if (finished) { if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) StartOrder(); return; }
        if (!ordered) { if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) StartOrder(); return; }
        MovePlayer(); if (Time.time - startedAt > timeLimitSeconds) Finish(false, "근무 시간이 끝났습니다.");
        SelectInventorySlot(kb);
        UpdateHeldWeaponVisual();
        if (kb != null && kb.eKey.wasPressedThisFrame) Interact(); if (kb != null && kb.fKey.wasPressedThisFrame && holdingParcel) DropParcel(false); if (kb != null && kb.gKey.wasPressedThisFrame && holdingParcel) DropParcel(true);
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) UseCurrentItem();
        if (holdingParcel) { var direction = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized; currentParcel.transform.position = player.transform.position + Vector3.up * .8f + direction * 1.2f; currentParcel.transform.rotation = Quaternion.LookRotation(direction); }
        UpdateMonsters();
    }
    void MovePlayer()
    {
        var kb = Keyboard.current; if (kb == null) return; var move = Vector3.zero; if (kb.wKey.isPressed) move.z++; if (kb.sKey.isPressed) move.z--; if (kb.aKey.isPressed) move.x--; if (kb.dKey.isPressed) move.x++;
        player.Move(player.transform.TransformDirection(move.normalized) * 6f * Time.deltaTime); player.Move(knockback * Time.deltaTime); knockback = Vector3.MoveTowards(knockback, Vector3.zero, 12f * Time.deltaTime);
        var grounded = player.isGrounded;
        if (grounded && vertical < 0) vertical = -2f;
        if (grounded && kb.spaceKey.wasPressedThisFrame) vertical = 5.5f;
        vertical += -22f * Time.deltaTime;
        player.Move(Vector3.up * vertical * Time.deltaTime);
    }
    void UpdateCamera()
    {
        var mouse = Mouse.current; if (ordered && !finished && mouse != null) { var delta = mouse.delta.ReadValue(); player.transform.Rotate(0, delta.x * .12f, 0); lookPitch = Mathf.Clamp(lookPitch - delta.y * .12f, -80f, 80f); }
        cam.transform.position = player.transform.position + Vector3.up * 1.6f; cam.transform.rotation = Quaternion.Euler(lookPitch, player.transform.eulerAngles.y, 0);
        var aiming = SelectedItem == ItemKind.StunGun && mouse != null && mouse.rightButton.isPressed;
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, aiming ? 42f : 60f, Time.deltaTime * 12f);
    }
    void UpdateHeldWeaponVisual()
    {
        if (holdingParcel) { if (heldWeaponVisual != null) Destroy(heldWeaponVisual); return; }
        var item = SelectedItem;
        if (item == ItemKind.None) { if (heldWeaponVisual != null) Destroy(heldWeaponVisual); return; }
        var expectedName = item == ItemKind.Axe ? "Held Axe" : "Held Stun Gun";
        if (heldWeaponVisual == null || heldWeaponVisual.name != expectedName)
        {
            if (heldWeaponVisual != null) Destroy(heldWeaponVisual);
            heldWeaponVisual = Create(expectedName, item == ItemKind.Axe ? PrimitiveType.Cube : PrimitiveType.Capsule, Vector3.zero, Vector3.one, item == ItemKind.Axe ? new Color(.7f, .7f, .74f) : Color.cyan);
            heldWeaponVisual.GetComponent<Collider>().enabled = false;
            heldWeaponVisual.transform.SetParent(cam.transform, false);
        }
        var swing = item == ItemKind.Axe ? Mathf.Sin(Mathf.Clamp01((axeSwingUntil - Time.time) / .22f) * Mathf.PI) : 0f;
        heldWeaponVisual.transform.localPosition = item == ItemKind.Axe ? new Vector3(.52f - swing * .18f, -.55f + swing * .08f, .8f + swing * .12f) : new Vector3(.42f, -.4f, .75f);
        heldWeaponVisual.transform.localRotation = item == ItemKind.Axe ? Quaternion.Euler(25 + swing * 105f, 0, -28 + swing * 35f) : Quaternion.Euler(90, 0, 0);
        heldWeaponVisual.transform.localScale = item == ItemKind.Axe ? new Vector3(.14f, .85f, .14f) : new Vector3(.18f, .45f, .18f);
    }
    void UpdateMonsters()
    {
        for (var i = 0; i < monsters.Length; i++)
        {
            var monster = monsters[i]; if (!monster.activeSelf) continue;
            if (Time.time < monsterStunnedUntil[i]) { monsterRenderers[i].material.color = Color.cyan; continue; }
            monsterRenderers[i].material.color = new Color(.1f, .01f, .1f); var seesPlayer = Vector3.Distance(monster.transform.position, player.transform.position) < (holdingParcel ? 30f : 12f); var patrol = new Vector3(i % 2 == 0 ? 5 : -5, 1, 15 + i * 35 + Mathf.Sin(Time.time + i) * 12f); var target = seesPlayer ? player.transform.position : patrol;
            monster.transform.position = Vector3.MoveTowards(monster.transform.position, target, (seesPlayer ? 4.5f : 1.5f) * Time.deltaTime); var flat = target - monster.transform.position; flat.y = 0; if (flat.sqrMagnitude > .01f) monster.transform.rotation = Quaternion.LookRotation(flat);
            if (seesPlayer && Vector3.Distance(monster.transform.position, player.transform.position) < 1.6f && Time.time >= nextActionAt) { nextActionAt = Time.time + 1.1f; var push = player.transform.position - monster.transform.position; push.y = 0; knockback = push.normalized * 12f; vertical = 5f; if (holdingParcel) DropParcel(true); feedback = "괴물이 들이받았습니다!"; }
        }
    }
    void Interact()
    {
        if (holdingParcel) { feedback = "택배를 든 상태에서는 아이템을 집을 수 없습니다."; return; }
        if (!HasItem(ItemKind.Axe) && Near(axe)) { AddItem(ItemKind.Axe); axe.SetActive(false); feedback = "도끼를 슬롯에 넣었습니다. 숫자키로 장착하고 좌클릭하세요."; return; }
        if (!HasItem(ItemKind.StunGun) && stunGun.activeSelf && Near(stunGun)) { AddItem(ItemKind.StunGun); stunGun.SetActive(false); feedback = "단발 기절총을 슬롯에 넣었습니다. 숫자키로 장착하세요."; return; }
        if (activeDelivery < DeliveryCount && (currentParcel == null || Near(currentParcel)) && Near(currentParcel ?? parcels[activeDelivery])) { currentParcel ??= parcels[activeDelivery]; holdingParcel = true; parcelBody = currentParcel.GetComponent<Rigidbody>(); parcelBody.isKinematic = true; feedback = mailboxes[activeDelivery].name + "로 배송하세요. 노란 마커가 표시됩니다."; }
    }
    bool Near(GameObject go) => go != null && go.activeSelf && Vector3.Distance(player.transform.position, go.transform.position) < 2.7f;
    void DropParcel(bool throwIt) { if (!holdingParcel) return; holdingParcel = false; parcelBody.isKinematic = false; parcelBody.linearVelocity = player.transform.forward * (throwIt ? 10f : 0) + Vector3.up * (throwIt ? 2f : 0); feedback = throwIt ? "택배를 던졌습니다." : "택배를 내려놓았습니다."; }
    void UseCurrentItem()
    {
        if (holdingParcel) { feedback = "택배를 들고 있으면 무기를 쓸 수 없습니다."; return; }
        if (SelectedItem == ItemKind.StunGun) { FireStunGun(); return; }
        if (SelectedItem == ItemKind.Axe) SwingAxe(); else feedback = "아이템 슬롯에서 무기를 선택하세요.";
    }
    void FireStunGun()
    {
        inventory[selectedSlot] = ItemKind.None;
        var bolt = Create("Stun Bolt", PrimitiveType.Sphere, cam.transform.position + cam.transform.forward * 1.1f, Vector3.one * .1f, Color.cyan);
        var body = bolt.AddComponent<Rigidbody>(); body.useGravity = false; body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; body.linearVelocity = cam.transform.forward * 120f;
        bolt.AddComponent<StunBolt>().owner = this;
        feedback = "단발 기절총 발사!";
    }
    void SwingAxe()
    {
        if (Time.time < nextActionAt) return; nextActionAt = Time.time + .45f; axeSwingUntil = Time.time + .22f; var best = -1; var bestDistance = 3.2f;
        for (var i = 0; i < monsters.Length; i++) { if (!monsters[i].activeSelf) continue; var distance = Vector3.Distance(player.transform.position, monsters[i].transform.position); if (distance < bestDistance && Vector3.Dot(player.transform.forward, (monsters[i].transform.position - player.transform.position).normalized) > .1f) { best = i; bestDistance = distance; } }
        if (best < 0) { feedback = "도끼가 허공을 갈랐습니다."; return; } monsterHealth[best]--; feedback = "도끼 명중! 괴물 체력 " + monsterHealth[best] + "/3"; if (monsterHealth[best] <= 0) { monsters[best].SetActive(false); feedback = "괴물을 처치했습니다."; }
    }
    int MonsterIndex(GameObject go) { for (var i = 0; i < monsters.Length; i++) if (go == monsters[i]) return i; return -1; }
    public void StunMonster(GameObject hit)
    {
        var index = MonsterIndex(hit);
        feedback = index >= 0 ? "명중! 괴물이 5초간 기절했습니다." : "기절총이 빗나갔습니다.";
        if (index >= 0) monsterStunnedUntil[index] = Time.time + 5f;
    }
    ItemKind SelectedItem => inventory[selectedSlot];
    bool HasItem(ItemKind item) { for (var i = 0; i < inventory.Length; i++) if (inventory[i] == item) return true; return false; }
    void AddItem(ItemKind item)
    {
        for (var i = 0; i < inventory.Length; i++)
            if (inventory[i] == ItemKind.None) { inventory[i] = item; selectedSlot = i; return; }
        feedback = "아이템 슬롯이 가득 찼습니다.";
    }
    void SelectInventorySlot(Keyboard kb)
    {
        if (kb == null || holdingParcel) return;
        for (var i = 0; i < 4; i++)
            if ((i == 0 && kb.digit1Key.wasPressedThisFrame) || (i == 1 && kb.digit2Key.wasPressedThisFrame) || (i == 2 && kb.digit3Key.wasPressedThisFrame) || (i == 3 && kb.digit4Key.wasPressedThisFrame)) selectedSlot = i;
    }
    public void ParcelHit(GameObject parcel) { if (!ordered || finished || parcel != currentParcel || holdingParcel) return; impacts++; var renderer = parcel.GetComponent<Renderer>(); renderer.material.color = impacts < 2 ? new Color(.95f, .7f, .23f) : impacts < 10 ? new Color(1f, .25f, .05f) : Color.gray; if (impacts >= 10) Finish(false, "택배가 파손됐습니다."); }
    public void TryDeliver(GameObject other, int index) { if (finished || other != currentParcel || holdingParcel || index != activeDelivery) return; currentParcel.SetActive(false); currentParcel = null; activeDelivery++; impacts = 0; if (activeDelivery == DeliveryCount) Finish(true, "마을의 모든 택배를 배송했습니다!"); else feedback = "배송 완료! 택배차로 돌아가 다음 택배를 집으세요."; }
    void StartOrder()
    {
        ordered = true; finished = holdingParcel = false; activeDelivery = impacts = selectedSlot = 0; currentParcel = null; startedAt = Time.time; finishedElapsed = lookPitch = vertical = nextActionAt = 0; knockback = Vector3.zero;
        for (var i = 0; i < inventory.Length; i++) inventory[i] = ItemKind.None;
        for (var i = 0; i < DeliveryCount; i++) { parcels[i].SetActive(true); parcels[i].GetComponent<Rigidbody>().isKinematic = true; parcels[i].transform.SetPositionAndRotation(ParcelPosition(i), Quaternion.identity); markers[i].SetActive(false); }
        stunGun.SetActive(true); stunGun.transform.position = new Vector3(-3, .9f, -37); axe.SetActive(true); axe.transform.position = new Vector3(3, .9f, -37); for (var i = 0; i < monsters.Length; i++) { monsters[i].SetActive(true); monsterHealth[i] = 3; monsterStunnedUntil[i] = 0; }
        feedback = "택배차에서 도끼·단발 기절총·첫 택배를 준비하세요."; Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
    }
    void Finish(bool success, string reason) { finishedElapsed = Time.time - startedAt; finished = true; feedback = (success ? "배송 성공 — " : "배송 실패 — ") + reason; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    void WorldLabel(GameObject target, string text, GUIStyle style) { if (!target.activeSelf) return; var screen = cam.WorldToScreenPoint(target.transform.position + Vector3.up * 1.5f); if (screen.z > 0) GUI.Label(new Rect(screen.x - 150, Screen.height - screen.y, 300, 28), text, style); }
    void OnGUI()
    {
        var label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 19, fontStyle = FontStyle.Bold, wordWrap = true, normal = { textColor = new Color(.91f, .96f, 1f) } }; var heading = new GUIStyle(label) { fontSize = 26, normal = { textColor = new Color(1f, .9f, .62f) } };
        if (!ordered) { GUI.Box(new Rect(Screen.width / 2 - 310, Screen.height / 2 - 150, 620, 300), "MONSTER DELIVERY SERVICE\n\n미국 교외 주택가의 모든 택배를 배송하세요.\n택배를 들면 배송지 마커가 나타나며, 괴물이 멀리서도 당신을 발견합니다.\n\n기절총: 단발, 처치 불가 · 도끼: 근접 처치\n\nEnter 또는 Space로 근무 시작"); return; }
        if (finished) { GUI.Box(new Rect(Screen.width / 2 - 280, Screen.height / 2 - 130, 560, 260), feedback + "\n\n배송: " + activeDelivery + "/" + DeliveryCount + "\n경과 시간: " + finishedElapsed.ToString("0.0") + "초\n\nEnter 또는 Space로 다시 시작"); return; }
        for (var i = 0; i < DeliveryCount; i++) markers[i].SetActive(i == activeDelivery && holdingParcel);
        GUI.Box(new Rect(15, 15, 600, 140), "교외 마을 배송\n완료: " + activeDelivery + "/" + DeliveryCount + "  ·  택배: " + (holdingParcel ? "운반 중" : currentParcel == null ? "택배차에 있음" : "바닥에 있음") + "\n장착 무기: " + (SelectedItem == ItemKind.None ? "없음" : SelectedItem == ItemKind.Axe ? "도끼" : "단발 기절총") + "\n" + feedback);
        GUI.Box(new Rect(Screen.width / 2 - 330, 18, 660, 42), "현재 목표: " + (holdingParcel ? "노란 마커가 표시한 우편함에 F로 택배를 내려놓으세요." : "택배차의 도끼·단발 기절총 또는 다음 택배 가까이에서 E를 누르세요. Space: 점프 · 우클릭: 조준"));
        if (SelectedItem != ItemKind.None) GUI.Label(new Rect(Screen.width / 2 - 14, Screen.height / 2 - 21, 28, 42), "+", heading);
        for (var i = 0; i < inventory.Length; i++) GUI.Box(new Rect(Screen.width / 2 - 210 + i * 105, Screen.height - 78, 95, 56), (i == selectedSlot ? "[" : "") + (i + 1) + ": " + (inventory[i] == ItemKind.None ? "비어 있음" : inventory[i] == ItemKind.Axe ? "도끼" : "기절총") + (i == selectedSlot ? "]" : ""));
        for (var i = 0; i < monsters.Length; i++) if (monsters[i].activeSelf) WorldLabel(monsters[i], "HP " + monsterHealth[i] + "/3" + (Time.time < monsterStunnedUntil[i] ? " · 기절" : ""), heading);
        if (activeDelivery < DeliveryCount && currentParcel == null) WorldLabel(parcels[activeDelivery], "▼ 다음 택배 [E] ▼", heading); if (!HasItem(ItemKind.Axe)) WorldLabel(axe, "▼ 도끼 [E] ▼", heading); if (!HasItem(ItemKind.StunGun) && stunGun.activeSelf) WorldLabel(stunGun, "▼ 단발 기절총 [E] ▼", heading); if (holdingParcel && activeDelivery < DeliveryCount) { WorldLabel(mailboxes[activeDelivery], "▼ 배송지: F로 내려놓기 ▼", heading); WorldLabel(markers[activeDelivery], "▼ 배송지 ▼", heading); }
    }
}

public sealed class ParcelImpact : MonoBehaviour { public MonsterDeliveryPrototype owner; float lastHit; void OnCollisionEnter(Collision collision) { if (Time.time - lastHit > .45f) { lastHit = Time.time; owner.ParcelHit(gameObject); } } }
public sealed class MailboxTrigger : MonoBehaviour { public MonsterDeliveryPrototype owner; public int index; void OnTriggerEnter(Collider other) => owner.TryDeliver(other.gameObject, index); }
public sealed class StunBolt : MonoBehaviour
{
    public MonsterDeliveryPrototype owner;
    void Start() => Destroy(gameObject, 2f);
    void OnCollisionEnter(Collision collision) { owner.StunMonster(collision.gameObject); Destroy(gameObject); }
}
