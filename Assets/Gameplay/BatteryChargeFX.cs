using UnityEngine;

public sealed partial class MonsterDeliveryPrototype
{
    readonly BatteryChargeAura[] batteryAuras = new BatteryChargeAura[BotCount + 1];
    float batteryScreenBlend;
    bool BatteryActive => started && !finished && !mapEditMode && Time.time < playerItems.batteryUntil;

    void UpdateBatteryFX()
    {
        batteryScreenBlend = Mathf.MoveTowards(batteryScreenBlend, BatteryActive ? 1 : 0, Time.deltaTime * 6);
        for (int id = 0; id <= BotCount; id++)
        {
            var actor = ActorController(id);
            bool visible = started && !finished && !mapEditMode && actor && actor.gameObject.activeInHierarchy &&
                Time.time < ActorState(id).batteryUntil && (id != 0 || thirdPersonPreview);
            if (visible && !batteryAuras[id])
            {
                var root = new GameObject("BatteryChargeAura"); root.transform.SetParent(actor.transform, false);
                batteryAuras[id] = root.AddComponent<BatteryChargeAura>();
            }
            if (batteryAuras[id]) batteryAuras[id].Show(visible, ActorState(id).batteryUntil - Time.time);
        }
    }
    void DrawBatteryScreen(float w, float h)
    {
        if (batteryScreenBlend <= .001f) return;
        // Discrete charge cells and angular lightning distinguish this from the shoes' wind streaks.
        var matrix = GUI.matrix;
        var tint = BatteryChargeAura.ChargeColor;
        for (int side = 0; side < 2; side++)
        {
            float x = side == 0 ? 24 : w - 36;
            for (int cell = 0; cell < 5; cell++)
            {
                float phase = Mathf.Repeat(Time.time * 1.5f - cell * .15f, 1);
                tint.a = batteryScreenBlend * (.16f + .5f * (1 - phase));
                GUI.DrawTexture(new Rect(x, h * .38f + cell * 22, 12, 16), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, tint, 0, 2);
            }
            for (int bolt = 0; bolt < 3; bolt++)
            {
                var start = new Vector2(side == 0 ? 54 : w - 54, h * (.25f + bolt * .22f));
                tint = bolt % 2 == 0 ? BatteryChargeAura.ChargeColor : new Color(.3f, 1, 1);
                tint.a = batteryScreenBlend * (.3f + .3f * Mathf.Abs(Mathf.Sin(Time.time * 11 + bolt)));
                for (int segment = 0; segment < 4; segment++)
                {
                    var end = start + new Vector2((segment % 2 == 0 ? 1 : -1) * (side == 0 ? 14 : -14), 17);
                    float angle = Mathf.Atan2(end.y - start.y, end.x - start.x) * Mathf.Rad2Deg;
                    GUIUtility.RotateAroundPivot(angle, start);
                    GUI.DrawTexture(new Rect(start.x, start.y, Vector2.Distance(start, end), 2.5f), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, tint, 0, 0);
                    GUI.matrix = matrix; start = end;
                }
            }
        }
        if (!BatteryActive) return;
        float y = Time.time < playerItems.speedUntil ? h - 252 : h - 206;
        DeliveryUIArt.Image(new Rect(190, y, 312, 40), "control_panel");
        DeliveryUIArt.Text(new Rect(198, y + 4, 296, 32), "무한 스태미나 · +30% · " + (playerItems.batteryUntil - Time.time).ToString("0.0") + "초", 17, BatteryChargeAura.ChargeColor, TextAnchor.MiddleCenter);
    }
}

// Cosmetic electricity follows the authoritative battery timer, including remote snapshots.
public sealed class BatteryChargeAura : MonoBehaviour
{
    public static readonly Color ChargeColor = new(.78f, .38f, 1);
    readonly LineRenderer[] bolts = new LineRenderer[6];
    readonly LineRenderer[] circuits = new LineRenderer[2];
    readonly LineRenderer[] cells = new LineRenderer[3];
    LineRenderer icon;
    Material material;
    void Awake()
    {
        material = new Material(Resources.Load<Shader>("FX/RunningShoesAura")) { name = "BatteryElectricGlow" };
        for (int i = 0; i < bolts.Length; i++) bolts[i] = Line("ElectricArc", 9, false, .045f);
        for (int i = 0; i < circuits.Length; i++) circuits[i] = Line("ChargeCircuit", 17, false, .035f);
        icon = Line("BatteryIcon", 9, false, .045f);
        icon.SetPositions(new[] { new Vector3(-.34f,1.45f,0), new Vector3(.34f,1.45f,0), new Vector3(.34f,1.85f,0),
            new Vector3(.12f,1.85f,0), new Vector3(.12f,1.95f,0), new Vector3(-.12f,1.95f,0),
            new Vector3(-.12f,1.85f,0), new Vector3(-.34f,1.85f,0), new Vector3(-.34f,1.45f,0) });
        for (int i = 0; i < cells.Length; i++)
        {
            cells[i] = Line("ChargeCell", 2, false, .11f);
            cells[i].SetPosition(0,new Vector3(-.2f+i*.2f,1.52f,0));
            cells[i].SetPosition(1,new Vector3(-.2f+i*.2f,1.76f,0));
        }
    }
    LineRenderer Line(string label, int count, bool loop, float width)
    {
        var line = new GameObject(label).AddComponent<LineRenderer>(); line.transform.SetParent(transform, false);
        line.sharedMaterial = material; line.useWorldSpace = false; line.loop = loop; line.positionCount = count;
        line.startWidth = line.endWidth = width; return line;
    }
    public void Show(bool visible, float remaining)
    {
        gameObject.SetActive(visible); if (!visible) return;
        float time = Time.time, fade = Mathf.Clamp01(remaining / .35f);
        var violet = ChargeColor; violet.a = fade * .9f;
        var cyan = new Color(.3f, 1, 1, fade * .85f);
        icon.startColor = icon.endColor = violet;
        for (int i = 0; i < cells.Length; i++)
        {
            var color = cyan; color.a *= Mathf.Repeat(time * 1.6f, 1) > i / 3f ? 1 : .15f;
            cells[i].startColor = cells[i].endColor = color;
        }
        for (int i = 0; i < bolts.Length; i++)
        {
            bolts[i].startColor = bolts[i].endColor = i % 2 == 0 ? violet : cyan;
            float angle = i * Mathf.PI / 3 + time * .45f;
            for (int n = 0; n < 9; n++)
            {
                float jitter = n == 0 || n == 8 ? 0 : Mathf.Sin(Mathf.Floor(time * 15) * 7 + n * 11 + i) * .18f;
                float radius = .8f + jitter;
                bolts[i].SetPosition(n, new Vector3(Mathf.Cos(angle) * radius, -.75f + n * .24f, Mathf.Sin(angle) * radius));
            }
        }
        for (int i = 0; i < circuits.Length; i++)
        {
            circuits[i].startColor = circuits[i].endColor = i == 0 ? violet : cyan;
            for (int n = 0; n < 17; n++)
            {
                float a = n * Mathf.PI / 8 + time * (i == 0 ? 1 : -1);
                float radius = n % 2 == 0 ? .95f : .84f;
                circuits[i].SetPosition(n, new Vector3(Mathf.Cos(a)*radius, -.65f + i*.9f, Mathf.Sin(a)*radius));
            }
        }
    }
    void OnDestroy() { if (material) Destroy(material); }
}
