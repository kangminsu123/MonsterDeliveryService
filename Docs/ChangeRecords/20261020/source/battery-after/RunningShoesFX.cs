using UnityEngine;

public sealed partial class MonsterDeliveryPrototype
{
    const float RunningShoesDuration = 5;
    readonly RunningShoesAura[] runningAuras = new RunningShoesAura[BotCount + 1];
    float runningScreenBlend, nextRivalShoesTest;
    Texture2D runningScreenVignette;
    bool rivalShoesPlaytest = false; // Current opponent preview demonstrates bubble-wrap protection.

    bool RunningShoesActive => started && !finished && !mapEditMode && Time.time < playerItems.speedUntil;
    float RunningShoesFieldOfView => (RunningShoesActive || BatteryActive) && player &&
        new Vector2(player.velocity.x, player.velocity.z).sqrMagnitude > 1 ? 66 : 60;

    void ActivateRunningShoes(int id)
    {
        ActorState(id).speedUntil = Time.time + RunningShoesDuration;
        if (id == 0) feedback = "러닝화 · 이동 속도 +50%!";
    }

    void UpdateRunningShoesFX()
    {
        if (started && !finished && !NetworkScreens && mapExploreMode && rivalShoesPlaytest && bots[0] && Time.time >= nextRivalShoesTest)
        {
            ActivateRunningShoes(1); nextRivalShoesTest = Time.time + 10;
        }
        runningScreenBlend = Mathf.MoveTowards(runningScreenBlend, RunningShoesActive ? 1 : 0, Time.deltaTime * 5);
        for (int id = 0; id <= BotCount; id++)
        {
            var actor = ActorController(id);
            bool visible = started && !finished && !mapEditMode && actor && actor.gameObject.activeInHierarchy &&
                Time.time < ActorState(id).speedUntil && (id != 0 || thirdPersonPreview);
            if (visible && !runningAuras[id])
            {
                var root = new GameObject("RunningShoesAura"); root.transform.SetParent(actor.transform, false);
                runningAuras[id] = root.AddComponent<RunningShoesAura>();
            }
            if (runningAuras[id]) runningAuras[id].Show(visible);
        }
    }

    static float RunningShoesPulse(float time)
    {
        float phase = Mathf.Repeat(time * 1.1f, 1);
        // A strong beat followed by a softer beat, then a short rest.
        return Mathf.Clamp01(Mathf.Exp(-Mathf.Pow((phase - .12f) / .065f, 2)) +
            .65f * Mathf.Exp(-Mathf.Pow((phase - .3f) / .09f, 2)));
    }

    void EnsureRunningShoesVignette()
    {
        if (runningScreenVignette) return;
        const int size = 128;
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float edge = Mathf.Min(x, y, size - 1 - x, size - 1 - y) / (size - 1f);
            pixels[y * size + x] = new Color(1, 1, 1, Mathf.Pow(1 - Mathf.SmoothStep(0, 1, edge / .14f), 1.5f));
        }
        runningScreenVignette = new Texture2D(size, size, TextureFormat.RGBA32, false)
        { name = "RunningShoesSkyVignette", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        runningScreenVignette.SetPixels(pixels); runningScreenVignette.Apply();
    }

    void DrawRunningShoesScreen(float w, float h)
    {
        if (runningScreenBlend <= .001f) return;
        bool moving = player && new Vector2(player.velocity.x, player.velocity.z).sqrMagnitude > 1;
        EnsureRunningShoesVignette();
        float pulse = RunningShoesPulse(Time.time);
        var tint = RunningShoesAura.WindColor;
        tint.a = runningScreenBlend * (.06f + .22f * pulse);
        GUI.DrawTexture(new Rect(0, 0, w, h), runningScreenVignette, ScaleMode.StretchToFill, true, 0, tint, 0, 0);
        var matrix = GUI.matrix;
        var center = new Vector2(w * .5f, h * .5f);
        for (int i = 0; i < 36; i++)
        {
            float phase = Mathf.Repeat(Time.time * (moving ? 2.5f : 1.1f) + i * .618f, 1);
            float theta = i * Mathf.PI * 2 / 36;
            var direction = new Vector2(Mathf.Cos(theta), Mathf.Sin(theta));
            float edge = Mathf.Min(w * .5f / Mathf.Max(.001f, Mathf.Abs(direction.x)),
                h * .5f / Mathf.Max(.001f, Mathf.Abs(direction.y)));
            var point = center + direction * edge * Mathf.Lerp(.73f, 1.12f, phase);
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            tint.a = runningScreenBlend * Mathf.Sin(phase * Mathf.PI) * (moving ? .5f : .3f) * (.65f + .35f * pulse);
            GUIUtility.RotateAroundPivot(angle, point);
            GUI.DrawTexture(new Rect(point.x, point.y, Mathf.Lerp(24, moving ? 140 : 85, phase), moving ? 3.5f : 2.5f),
                Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, tint, 0, 2);
            GUI.matrix = matrix;
        }
        if (RunningShoesActive)
        {
            DeliveryUIArt.Image(new Rect(190, h - 206, 312, 40), "control_panel");
            DeliveryUIArt.Text(new Rect(204, h - 202, 284, 32),
                "이동 속도 +50% · " + Mathf.Max(0, playerItems.speedUntil - Time.time).ToString("0.0") + "초",
                20, RunningShoesAura.WindColor, TextAnchor.MiddleCenter);
        }
    }

    void DestroyRunningShoesFX()
    {
        if (runningScreenVignette) Destroy(runningScreenVignette);
        foreach (var aura in runningAuras) if (aura) Destroy(aura.gameObject);
    }
}
