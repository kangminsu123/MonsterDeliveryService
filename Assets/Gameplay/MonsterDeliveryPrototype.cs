using UnityEngine;
using UnityEngine.InputSystem;

public enum ParcelState { Perfect, Damaged, Waste }

public sealed class MonsterDeliveryPrototype : MonoBehaviour
{
    CharacterController player;
    Camera cam;
    GameObject parcel, house, mailbox, monster, decoy, stunGun;
    Rigidbody parcelBody;
    Renderer parcelRenderer, monsterRenderer;
    ParcelState parcelState = ParcelState.Perfect;
    bool ordered, holding, holdingStunGun, finished, delivered, resultSuccess;
    int impacts, decoyCount;
    float startedAt, finishedElapsed, lookPitch, monsterStunnedUntil, monsterAttackAt, decoyUntil, reloadUntil;
    Vector3 parcelStart, houseStart, knockback;
    [Min(1f)] public float timeLimitSeconds = 180f;
    string feedback = "배송 시작을 누르면 저주 택배가 활성화됩니다.";
    AudioClip impactTone;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install() { if (FindAnyObjectByType<MonsterDeliveryPrototype>() == null) new GameObject("MonsterDeliveryPrototype").AddComponent<MonsterDeliveryPrototype>(); }

    void Start()
    {
        RenderSettings.ambientLight = new Color(.06f, .025f, .11f);
        CreateWorld(); impactTone = Tone(150, .09f);
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
    }

    void CreateWorld()
    {
        Ground("Night Village", new Vector3(0, -.5f, 36), new Vector3(102, 1, 195), new Color(.035f, .02f, .12f));
        Ground("Garage", new Vector3(0, 1.5f, -30), new Vector3(36, 4, 1), new Color(.18f, .08f, .28f));
        Ground("Alley Left", new Vector3(-30, 2, 30), new Vector3(1, 5, 180), new Color(.12f, .05f, .22f));
        Ground("Alley Right", new Vector3(30, 2, 30), new Vector3(1, 5, 180), new Color(.12f, .05f, .22f));
        player = Create("Delivery Rider", PrimitiveType.Capsule, new Vector3(0, 1, -39), Vector3.one, new Color(1f, .3f, .08f)).AddComponent<CharacterController>();
        player.height = 2; player.radius = .45f;
        Create("Reflective Vest", PrimitiveType.Cube, new Vector3(0, 1.2f, -39), new Vector3(.95f, .55f, .5f), new Color(1f, .75f, .08f)).transform.SetParent(player.transform);
        cam = Camera.main; if (cam == null) cam = new GameObject("Main Camera").AddComponent<Camera>(); cam.tag = "MainCamera";
        Create("Cursed Parcel Desk", PrimitiveType.Cube, new Vector3(-9, .5f, -27), new Vector3(2, 1, 1), new Color(1f, .45f, .05f));
        parcel = Create("Cursed Parcel", PrimitiveType.Cube, new Vector3(6, 1, -27), Vector3.one, new Color(.95f, .7f, .28f));
        parcelStart = parcel.transform.position;
        parcelBody = parcel.AddComponent<Rigidbody>(); parcelBody.mass = 2; parcelBody.interpolation = RigidbodyInterpolation.Interpolate;
        parcel.AddComponent<ParcelImpact>().owner = this; parcelRenderer = parcel.GetComponent<Renderer>();
        stunGun = Create("Stun Gun", PrimitiveType.Cube, new Vector3(12, .55f, -27), new Vector3(.35f, .25f, .9f), Color.cyan);
        house = Create("13 Running House", PrimitiveType.Cube, new Vector3(0, 2.5f, 48), new Vector3(5, 5, 5), new Color(.22f, .06f, .3f));
        houseStart = house.transform.position;
        var eye = Create("Big Monster Eye", PrimitiveType.Sphere, new Vector3(-3.6f, 3, 40.35f), new Vector3(1.2f, 1.2f, .3f), Color.white); eye.transform.SetParent(house.transform);
        mailbox = Create("Hungry Mailbox", PrimitiveType.Cube, new Vector3(0, 1.2f, 38.7f), new Vector3(1.8f, 1.5f, 1.6f), new Color(.03f, .9f, .85f)); mailbox.transform.SetParent(house.transform);
        var deliveryTrigger = new GameObject("Mailbox Delivery Trigger"); deliveryTrigger.transform.SetParent(mailbox.transform); deliveryTrigger.transform.localPosition = Vector3.zero;
        var trigger = deliveryTrigger.AddComponent<BoxCollider>(); trigger.isTrigger = true; trigger.size = new Vector3(4, 3, 4); deliveryTrigger.AddComponent<MailboxTrigger>().owner = this;
        var zone = Create("Delivery Zone", PrimitiveType.Cylinder, new Vector3(0, .08f, 38.7f), new Vector3(2.5f, .04f, 2.5f), new Color(.03f, .9f, .85f)); zone.GetComponent<Collider>().enabled = false; zone.transform.SetParent(house.transform);
        monster = Create("Parcel Hunter", PrimitiveType.Capsule, new Vector3(9, 1, 48), new Vector3(1.3f, 1.7f, 1.3f), new Color(.08f, .01f, .08f)); monsterRenderer = monster.GetComponent<Renderer>(); monster.SetActive(false);
        var light = FindAnyObjectByType<Light>(); if (light != null) { light.color = new Color(.35f, .25f, 1f); light.intensity = 1.5f; }
    }

    GameObject Create(string name, PrimitiveType type, Vector3 position, Vector3 scale, Color color)
    {
        var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetPositionAndRotation(position, Quaternion.identity); go.transform.localScale = scale; go.GetComponent<Renderer>().material.color = color; return go;
    }
    void Ground(string name, Vector3 position, Vector3 scale, Color color) => Create(name, PrimitiveType.Cube, position, scale, color);

    void Update()
    {
        UpdateCamera(); var kb = Keyboard.current; var mouse = Mouse.current;
        if (finished) { if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) StartOrder(); return; }
        if (!ordered) { if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) StartOrder(); return; }
        MovePlayer();
        var run = Time.time - startedAt; house.transform.position = houseStart + new Vector3(Mathf.Sin(run * .8f) * 15f, 0, Mathf.Sin(run * .45f) * 21f);
        if (Time.time - startedAt > timeLimitSeconds) Finish(false, "집을 놓쳤습니다");
        if (kb != null && kb.eKey.wasPressedThisFrame) Interact();
        if (kb != null && kb.fKey.wasPressedThisFrame) { if (holding) Drop(false); else if (holdingStunGun) DropStunGun(); }
        if (kb != null && holding && kb.gKey.wasPressedThisFrame) Drop(true);
        if (kb != null && kb.qKey.wasPressedThisFrame) ThrowDecoy();
        if (mouse != null && mouse.leftButton.wasPressedThisFrame) FireStunGun();
        if (holding) { var carryDirection = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized; parcel.transform.position = player.transform.position + Vector3.up * .8f + carryDirection * 1.2f; parcel.transform.rotation = Quaternion.LookRotation(carryDirection); }
        UpdateMonster();
    }

    void MovePlayer()
    {
        var kb = Keyboard.current; if (kb == null) return;
        var move = Vector3.zero; if (kb.wKey.isPressed) move.z++; if (kb.sKey.isPressed) move.z--; if (kb.aKey.isPressed) move.x--; if (kb.dKey.isPressed) move.x++;
        player.Move(player.transform.TransformDirection(move.normalized) * 6f * Time.deltaTime);
        player.Move(knockback * Time.deltaTime); knockback = Vector3.MoveTowards(knockback, Vector3.zero, 12f * Time.deltaTime);
        if (player.isGrounded && kb.spaceKey.wasPressedThisFrame) vertical = 6;
        vertical += Physics.gravity.y * Time.deltaTime; player.Move(Vector3.up * vertical * Time.deltaTime); if (player.isGrounded && vertical < 0) vertical = -1;
    }
    void UpdateCamera()
    {
        var mouse = Mouse.current;
        if (ordered && !finished && mouse != null) { var delta = mouse.delta.ReadValue(); player.transform.Rotate(0, delta.x * .12f, 0); lookPitch = Mathf.Clamp(lookPitch - delta.y * .12f, -80f, 80f); }
        cam.transform.position = player.transform.position + Vector3.up * 1.6f;
        cam.transform.rotation = Quaternion.Euler(lookPitch, player.transform.eulerAngles.y, 0);
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, holdingStunGun && mouse != null && mouse.rightButton.isPressed ? 45f : 60f, Time.deltaTime * 12f);
    }
    float vertical;

    void UpdateMonster()
    {
        monster.SetActive(ordered && !finished); if (!monster.activeSelf) return;
        if (decoy != null && Time.time >= decoyUntil) { Destroy(decoy); decoy = null; }
        var distracted = decoy != null;
        var target = distracted ? decoy.transform.position : holding ? player.transform.position : house.transform.position + new Vector3(9, -1.5f, -9);
        if (Time.time < monsterStunnedUntil) { monsterRenderer.material.color = Color.cyan; return; }
        monsterRenderer.material.color = new Color(.08f, .01f, .08f);
        monster.transform.position = Vector3.MoveTowards(monster.transform.position, target, (holding || distracted ? 6.6f : 2f) * Time.deltaTime);
        var flat = target - monster.transform.position; flat.y = 0; if (flat.sqrMagnitude > .01f) monster.transform.rotation = Quaternion.LookRotation(flat);
        if (holding && !distracted && Time.time >= monsterAttackAt && Vector3.Distance(monster.transform.position, player.transform.position) < 1.7f) { monsterAttackAt = Time.time + 1.2f; MonsterAttack(); }
    }
    void FireStunGun()
    {
        if (holding) { feedback = "택배를 들고 있으면 아이템을 쓸 수 없습니다."; return; }
        if (!holdingStunGun) { feedback = "기절총을 먼저 집으세요."; return; }
        if (Time.time < reloadUntil) { feedback = "기절총 장전 중…"; return; }
        var bolt = Create("Stun Bolt", PrimitiveType.Sphere, cam.transform.position + cam.transform.forward * 1.2f, Vector3.one * .06f, Color.cyan); var body = bolt.AddComponent<Rigidbody>(); body.useGravity = false; body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; body.linearVelocity = cam.transform.forward * 220f; bolt.AddComponent<StunBolt>().owner = this; reloadUntil = Time.time + 3f; feedback = "발사! 다음 탄환 장전 중…";
    }
    void ThrowDecoy()
    {
        if (holding) { feedback = "택배를 들고 있으면 아이템을 쓸 수 없습니다."; return; }
        if (decoyCount <= 0) { feedback = "가짜 택배가 없습니다."; return; }
        decoyCount--; decoy = Create("Fake Parcel", PrimitiveType.Cube, player.transform.position + player.transform.forward * 3f + Vector3.up, Vector3.one, new Color(.9f, .1f, .75f)); decoy.AddComponent<Rigidbody>().linearVelocity = player.transform.forward * 8f + Vector3.up * 2f; decoyUntil = Time.time + 5f; feedback = "가짜 택배 투척! 괴물이 5초간 속습니다.";
    }
    void Interact()
    {
        if (!ordered || finished) return;
        if (holding) { feedback = "택배를 들고 있으면 다른 아이템을 집을 수 없습니다."; return; }
        if (holdingStunGun) { feedback = "기절총을 들고 있습니다. F로 내려놓으세요."; return; }
        if (Near(stunGun)) { holdingStunGun = true; stunGun.GetComponent<Collider>().enabled = false; stunGun.transform.SetParent(cam.transform); stunGun.transform.localPosition = new Vector3(.38f, -.34f, .75f); stunGun.transform.localRotation = Quaternion.Euler(12, -8, 0); feedback = "기절총 획득! 우클릭 조준 · 좌클릭 발사."; return; }
        if (Near(parcel)) { holding = true; parcelBody.isKinematic = true; feedback = "저주 택배를 들었습니다. 괴물이 당신만 쫓습니다!"; }
    }
    bool Near(GameObject go) => Vector3.Distance(player.transform.position, go.transform.position) < 2.5f;
    void WorldLabel(GameObject target, string text, GUIStyle style)
    {
        var screen = cam.WorldToScreenPoint(target.transform.position + Vector3.up * 1.5f);
        if (screen.z > 0) GUI.Label(new Rect(screen.x - 160, Screen.height - screen.y, 320, 28), text, style);
    }
    void Drop(bool throwIt)
    {
        holding = false; parcelBody.isKinematic = false; parcelBody.linearVelocity = player.transform.forward * (throwIt ? 11 : 0) + Vector3.up * (throwIt ? 2 : 0); feedback = throwIt ? "택배 투척!" : "택배를 내려놓았습니다.";
    }
    void MonsterAttack()
    {
        var push = player.transform.position - monster.transform.position; push.y = 0;
        if (push.sqrMagnitude < .01f) push = -monster.transform.forward; push.Normalize();
        knockback = push * 16f; vertical = 7f;
        holding = false; parcelBody.isKinematic = false;
        var parcelPush = (push + player.transform.right * .45f).normalized;
        parcelBody.linearVelocity = parcelPush * 14f + Vector3.up * 5f;
        Hit(); feedback = finished ? feedback : "괴물에게 맞아 당신과 택배가 서로 다른 방향으로 날아갔습니다!";
    }
    void DropStunGun()
    {
        holdingStunGun = false; stunGun.transform.SetParent(null); stunGun.GetComponent<Collider>().enabled = true; stunGun.transform.SetPositionAndRotation(player.transform.position + player.transform.forward * 1.2f + Vector3.up * .5f, player.transform.rotation); feedback = "기절총을 내려놓았습니다.";
    }
    public void StunMonster()
    {
        monsterStunnedUntil = Time.time + 3f; feedback = "명중! 괴물이 3초간 기절했습니다.";
    }
    public void MissStun() { feedback = "기절총이 빗나갔습니다."; }
    public void Hit()
    {
        if (finished || !ordered) return;
        impacts++; parcelState = impacts >= 10 ? ParcelState.Waste : impacts >= 2 ? ParcelState.Damaged : ParcelState.Perfect;
        parcelRenderer.material.color = parcelState == ParcelState.Perfect ? new Color(.95f, .7f, .28f) : parcelState == ParcelState.Damaged ? new Color(1f, .25f, .05f) : Color.gray;
        feedback = impacts == 1 ? "택배에 금이 갔습니다!" : parcelState == ParcelState.Damaged ? "택배가 손상됐습니다. 아직 배송할 수 있습니다." : "택배 파손 — 배송 실패";
        AudioSource.PlayClipAtPoint(impactTone, player.transform.position, .35f); if (parcelState == ParcelState.Waste) Finish(false, "택배가 부서졌습니다");
    }
    void Finish(bool success, string reason)
    {
        finishedElapsed = Time.time - startedAt; finished = true; resultSuccess = success; feedback = (success ? "배송 성공" : "배송 실패") + " — " + reason;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
    }
    void StartOrder()
    {
        ordered = true; finished = delivered = holding = holdingStunGun = false; impacts = 0; parcelState = ParcelState.Perfect; decoyCount = 1; startedAt = Time.time; finishedElapsed = 0; lookPitch = 0; knockback = Vector3.zero; monsterStunnedUntil = monsterAttackAt = decoyUntil = reloadUntil = 0;
        if (decoy != null) Destroy(decoy); decoy = null;
        parcel.transform.SetPositionAndRotation(parcelStart, Quaternion.identity); parcelBody.isKinematic = false; parcelBody.linearVelocity = Vector3.zero; parcelBody.angularVelocity = Vector3.zero;
        stunGun.transform.SetParent(null); stunGun.SetActive(true); stunGun.GetComponent<Collider>().enabled = true; stunGun.transform.SetPositionAndRotation(new Vector3(12, .55f, -27), Quaternion.identity);
        house.transform.position = houseStart; monster.transform.position = houseStart + new Vector3(9, -1.5f, -9); feedback = "택배를 들면 괴물이 당신을 쫓습니다. 우클릭: 기절총 / Q: 가짜 택배";
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
    }
    public void TryDeliver(GameObject other)
    {
        if (other != parcel || holding || delivered || finished) return;
        delivered = true; Finish(parcelState != ParcelState.Waste, parcelState == ParcelState.Waste ? "파손 택배" : "도망가는 집 우편함 배송 완료");
    }
    void OnGUI()
    {
        var label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 20, fontStyle = FontStyle.Bold, wordWrap = true, normal = { textColor = new Color(.91f, .96f, 1f) } };
        var heading = new GUIStyle(label) { fontSize = 28, normal = { textColor = new Color(1f, .9f, .62f) } };
        if (!ordered) { GUI.Box(new Rect(Screen.width / 2 - 290, Screen.height / 2 - 155, 580, 310), "MONSTER DELIVERY SERVICE\n\n택배를 든 사람만 괴물의 표적이 됩니다.\n택배를 부수지 말고 도망가는 집 우편함에 넣으세요.\n\n기절총: 우클릭 조준 · 좌클릭 발사 · 한 발 뒤 3초 장전\n\nEnter 또는 Space로 배송 시작"); return; }
        if (finished) { GUI.Box(new Rect(Screen.width / 2 - 280, Screen.height / 2 - 145, 560, 290), (resultSuccess ? "배송 성공" : "배송 실패") + "\n\n" + feedback + "\n택배 충격: " + impacts + "/10\n경과 시간: " + finishedElapsed.ToString("0.0") + "초\n\nEnter 또는 Space로 다시 시작"); return; }
        if (holdingStunGun) GUI.Label(new Rect(Screen.width / 2 - 14, Screen.height / 2 - 21, 28, 42), "+", heading);
        var state = parcelState == ParcelState.Perfect ? "멀쩡" : parcelState == ParcelState.Damaged ? "위험" : "파손";
        GUI.Box(new Rect(15, 15, 590, 150), "도망가는 집 배송\n택배 상태: " + state + "  ·  충격: " + impacts + "/10\n기절총: " + (holdingStunGun ? Time.time < reloadUntil ? "장전 중 " + (reloadUntil - Time.time).ToString("0.0") + "초" : "발사 가능" : "바닥") + "  ·  가짜 택배: " + decoyCount + "개\n괴물: " + (Time.time < monsterStunnedUntil ? "기절" : decoy != null ? "가짜 택배 추격" : holding ? "당신 추격 중" : "택배를 기다림") + "\n" + feedback);
        var objective = holding ? "아이템은 못 씁니다. 괴물을 피해 청록 배송 구역에 택배를 F로 놓으세요." : holdingStunGun ? "우클릭 조준 · 좌클릭 발사. 한 발 뒤 3초 장전됩니다. F로 기절총 내려놓기." : "노란 저주 택배 또는 청록 기절총에 가까이 가서 E를 누르세요.";
        GUI.Box(new Rect(Screen.width / 2 - 330, 18, 660, 42), "현재 목표: " + objective);
        if (!holding) WorldLabel(parcel, "▼ 저주 택배 [E] ▼", heading);
        if (!holdingStunGun) WorldLabel(stunGun, "▼ 기절총 [E] ▼", heading);
        WorldLabel(mailbox, "▼ 도망가는 집 우편함: F로 배송 ▼", heading);
        if (holding || decoy != null) WorldLabel(monster, Time.time < monsterStunnedUntil ? "▼ 괴물: 기절함 ▼" : "▼ 괴물: 택배 냄새를 맡음 ▼", heading);
    }
    static AudioClip Tone(float frequency, float seconds)
    {
        var length = Mathf.CeilToInt(44100 * seconds); var samples = new float[length]; for (var i = 0; i < length; i++) samples[i] = Mathf.Sin(2 * Mathf.PI * frequency * i / 44100f) * .2f;
        var clip = AudioClip.Create("impact", length, 1, 44100, false); clip.SetData(samples, 0); return clip;
    }
}

public sealed class ParcelImpact : MonoBehaviour
{
    public MonsterDeliveryPrototype owner;
    float lastHit;
    void OnCollisionEnter(Collision collision) { if (Time.time - lastHit > .45f) { lastHit = Time.time; owner.Hit(); } }
}

public sealed class MailboxTrigger : MonoBehaviour
{
    public MonsterDeliveryPrototype owner;
    void OnTriggerEnter(Collider other) => owner.TryDeliver(other.gameObject);
}

public sealed class StunBolt : MonoBehaviour
{
    public MonsterDeliveryPrototype owner;
    void Start() => Destroy(gameObject, 2f);
    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.name == "Parcel Hunter") owner.StunMonster(); else owner.MissStun();
        Destroy(gameObject);
    }
}
