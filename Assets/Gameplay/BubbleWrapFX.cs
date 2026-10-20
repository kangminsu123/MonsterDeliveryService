using UnityEngine;

public sealed partial class MonsterDeliveryPrototype
{
    readonly BubbleWrapShield[] bubbleShields = new BubbleWrapShield[BotCount + 1];
    float bubbleScreenBlend, nextRivalBubbleTest;
    bool rivalBubblePlaytest = true;
    Texture2D bubbleScreenRing;
    bool BubbleWrapActive => started && !finished && !mapEditMode && Time.time < playerItems.shieldUntil;

    void ActivateBubbleWrap(int id)
    {
        var state = ActorState(id); state.shieldUntil = Time.time + 8;
        state.shieldBrokenAt = float.NegativeInfinity;
        if (id == 0) feedback = "에어캡 · 8초 동안 1회 방어!";
    }

    void UpdateBubbleWrapFX()
    {
        bool playing = started && !finished && !mapEditMode;
        if (playing && !NetworkScreens && mapExploreMode && rivalBubblePlaytest && bots[0] && Time.time >= nextRivalBubbleTest)
        { ActivateBubbleWrap(1); nextRivalBubbleTest = Time.time + 12; }
        bubbleScreenBlend = Mathf.MoveTowards(bubbleScreenBlend, BubbleWrapActive ? 1 : 0, Time.deltaTime * 5);
        for (int id = 0; id <= BotCount; id++)
        {
            var actor = ActorController(id);
            if (!actor) continue;
            var state = ActorState(id);
            bool available = playing && actor.gameObject.activeInHierarchy;
            float until = available ? state.shieldUntil : 0;
            float brokenAt = available ? state.shieldBrokenAt : float.NegativeInfinity;
            if ((Time.time < until || Time.time - brokenAt < .45f) && !bubbleShields[id])
            {
                var root = new GameObject("BubbleWrapShield"); root.transform.SetParent(actor.transform, false);
                bubbleShields[id] = root.AddComponent<BubbleWrapShield>();
            }
            if (bubbleShields[id]) bubbleShields[id].Show(until, brokenAt, available && (id != 0 || thirdPersonPreview));
        }
    }

    void EnsureBubbleScreenRing()
    {
        if (bubbleScreenRing) return;
        const int size = 64;
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            var p = new Vector2(x, y) / (size - 1f) * 2 - Vector2.one;
            pixels[y * size + x] = new Color(1, 1, 1, Mathf.Exp(-Mathf.Pow((p.magnitude - .82f) / .07f, 2)));
        }
        bubbleScreenRing = new Texture2D(size, size, TextureFormat.RGBA32, false)
        { name = "AirCapScreenBubbles", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        bubbleScreenRing.SetPixels(pixels); bubbleScreenRing.Apply();
    }

    void DrawBubbleWrapScreen(float w, float h)
    {
        float age = Time.time - playerItems.shieldBrokenAt;
        float flash = started && !finished && !mapEditMode && age >= 0 && age < .45f ? 1 - age / .45f : 0;
        if (bubbleScreenBlend <= .001f && flash <= 0) return;
        EnsureRunningShoesVignette(); EnsureBubbleScreenRing();
        var tint = BubbleWrapShield.ShieldColor;
        tint.a = bubbleScreenBlend * (.13f + .025f * Mathf.Sin(Time.time * 2.3f)) + flash * .45f;
        GUI.DrawTexture(new Rect(0, 0, w, h), runningScreenVignette, ScaleMode.StretchToFill, true, 0, tint, 0, 0);
        for (int i = 0; i < 12; i++)
        {
            float theta = i * Mathf.PI * 2 / 12;
            float x = w * (.5f + Mathf.Cos(theta) * .49f);
            float y = h * (.5f + Mathf.Sin(theta) * .49f);
            float seed = Mathf.Repeat(i * .618f, 1);
            float phase = Mathf.Repeat(Time.time * (.65f + seed * .35f) + seed, 1);
            float pop = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.72f, .93f, phase));
            float grow = Mathf.SmoothStep(.6f, 1, Mathf.InverseLerp(.08f, .68f, phase));
            float size = (38 + i % 3 * 14) * (grow + pop * .7f);
            float alive = Mathf.SmoothStep(0, 1, phase / .12f) * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.82f, .98f, phase)));
            float glint = Mathf.Exp(-Mathf.Pow((phase - .8f) / .055f, 2));
            tint.a = bubbleScreenBlend * alive * (.3f + glint * .35f) + flash * .5f;
            GUI.DrawTexture(new Rect(x - size * .5f, y - size * .5f, size, size), bubbleScreenRing,
                ScaleMode.StretchToFill, true, 0, tint, 0, 0);
        }
        float panelY = h - (runningScreenBlend > .01f ? 252 : 206);
        if (BubbleWrapActive || flash > 0)
        {
            DeliveryUIArt.Image(new Rect(190, panelY, 354, 40), "control_panel");
            DeliveryUIArt.Text(new Rect(200, panelY + 4, 334, 32), flash > 0 ? "에어캡 · 공격을 막았습니다!" :
                "보호 중 · 1회 방어 · " + Mathf.Max(0, playerItems.shieldUntil - Time.time).ToString("0.0") + "초",
                20, BubbleWrapShield.ShieldColor, TextAnchor.MiddleCenter);
        }
    }

    void DestroyBubbleWrapFX()
    {
        foreach (var shield in bubbleShields) if (shield) Destroy(shield.gameObject);
        if (bubbleScreenRing) Destroy(bubbleScreenRing);
    }
}
