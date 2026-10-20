using System.Collections.Generic;
using UnityEngine;

// Real game values stay in the existing prototype; this file only draws the HUD.
public sealed partial class MonsterDeliveryPrototype
{
    public string HudClock
    {
        get
        {
            if (NetPlaying) { int remaining = Mathf.CeilToInt(Mathf.Max(0,netRoundUntil-Time.time)); return (remaining/60).ToString("D2")+":"+(remaining%60).ToString("D2"); }
            if (mapExploreMode) return "자유 탐색";
            int seconds = Mathf.CeilToInt(Mathf.Max(0, roundSeconds - (Time.time - startedAt)));
            return (seconds / 60).ToString("D2") + ":" + (seconds % 60).ToString("D2");
        }
    }
    public float HudCaptureProgress => captureTarget < 0 ? 0 : Mathf.Clamp01((Time.time - captureStarted) / CaptureSeconds);
    public string HudInteractionHint
    {
        get
        {
            if (!focusedTarget) return "";
            if (IsParcelTarget(focusedTarget) && !AllowsSpecialReplacement(focusedTarget) && !HasEmptySlot()) return "G로 선택 아이템 버리기";

for (int i = 0; i < HouseCount; i++) if (mailboxes[i] == focusedTarget.gameObject)
            {
                float remaining = MailboxProtectionRemaining(i);
                if (remaining > 0) return Mathf.CeilToInt(remaining) + "초 후 점령 가능";
                if (MailboxOwnedByUs(i)) return "점령 완료";
            }
            return interactionReady ? focusedTarget.actionHint : "가까이 이동하거나 행동 가능 상태를 기다리세요";
        }
    }

    void DrawGameplayHUD()
    {
        var oldMatrix = GUI.matrix; var oldColor = GUI.color;
        float scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
        float w = Screen.width / scale, h = Screen.height / scale;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);
        GUI.color = Color.white;
        try
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DrawLatencyProof();
#endif
            DrawRunningShoesScreen(w, h);
            DrawBatteryScreen(w, h);
            DrawBubbleWrapScreen(w, h);
            DrawFountainInkScreen(w, h);
            DrawSlimeScreen(w, h);
            DrawIceCreamScreen(w, h);
            DrawStunScreen(w, h);
            DrawSpecialTestHUD(w,h,scale);
            DeliveryUIArt.Image(new Rect(38, 28, 248, 99), "score_panel");
            DeliveryUIArt.Image(new Rect(48, 36, 80, 82), "mailbox_icon");
            DeliveryUIArt.Text(new Rect(134, 38, 134, 24), HudScoreTitle, 21, Color.white);
            DeliveryUIArt.Text(new Rect(132, 61, 56, 56), (NetPlaying ? TeamScore(netPlayers[netOwn].team) : Score(0)).ToString(), Score(0) < 10 ? 51 : 38, NetPlaying ? LocalMailboxColor : colors[0]);
            DeliveryUIArt.Text(new Rect(187, 74, 86, 38), "/ " + HouseCount, 32, Color.white);
            DeliveryUIArt.Image(new Rect(w * .5f - 110, 28, 220, 72), "timer_panel");
            DeliveryUIArt.Text(new Rect(w * .5f - 106, 30, 212, mapExploreMode && !NetPlaying ? 46 : 62), HudClock, mapExploreMode && !NetPlaying ? 28 : 48, Color.white, TextAnchor.MiddleCenter);
            if (mapExploreMode && !NetPlaying) DeliveryUIArt.Text(new Rect(w * .5f - 110, 75, 220, 22), "시간 제한 없음", 14, new Color(.75f, .79f, .85f), TextAnchor.MiddleCenter);
            if (NetPlaying) {DrawNetTeamHud(w);DrawNetLabels(scale);}
            else
            {
            int participantCount = 0;
            for (int i = 0; i < bots.Length; i++) if (bots[i] && bots[i].activeInHierarchy) participantCount++;
            int badgeIndex = 0;
            for (int id = 1; id <= BotCount; id++)
            {
                if (!bots[id - 1] || !bots[id - 1].activeInHierarchy) continue;
                var rect = new Rect(w * .5f - (participantCount * 156 - 10) * .5f + badgeIndex++ * 156, 116, 146, 50);
                DeliveryUIArt.Image(rect, "badge_panel", colors[id]);
                DeliveryUIArt.Text(new Rect(rect.x + 8, rect.y, 48, 50), "P" + (id + 1), 24, Color.white, TextAnchor.MiddleCenter);
                DeliveryUIArt.Image(new Rect(rect.x + 62, rect.y + 13, 24, 24), "map_mailbox", colors[id]);
                DeliveryUIArt.Text(new Rect(rect.x + 92, rect.y, 46, 50), Score(id).ToString(), 26, Color.white, TextAnchor.MiddleCenter);
            }
            }
            DeliveryUIArt.Image(new Rect(38, h - 176, 124, 124), "portrait_panel");
            GUI.DrawTexture(new Rect(44, h - 170, 112, 112), DeliveryUIArt.Texture(HudPortraitName), ScaleMode.StretchToFill, true, 0, Color.white, 0, 24);
            DeliveryUIArt.Image(new Rect(174, h - 146, 8, 28), "badge_active", colors[0]);
            DeliveryUIArt.Text(new Rect(190, h - 152, 220, 38), "스태미나", 26, Color.white);
            DeliveryUIArt.Bar(new Rect(190, h - 108, 340, 24), stamina / 100, colors[0]);
            DeliveryUIArt.Text(new Rect(542, h - 114, 132, 36), (Time.time < playerItems.batteryUntil ? "무제한" : Mathf.RoundToInt(stamina) + " / 100"), 24, Color.white);
            DeliveryUIArt.Image(new Rect(190, h - 69, 244, 56), "control_panel");
            DeliveryUIArt.Image(new Rect(200, h - 62, 80, 42), "keycap_dark");
            DeliveryUIArt.Text(new Rect(200, h - 62, 80, 42), "Shift", 22, Color.white, TextAnchor.MiddleCenter);
            bool canDash = stamina >= 50 && Time.time >= rollUntil && !crouching && !playerItems.Stunned(Time.time);
            DeliveryUIArt.Text(new Rect(292, h - 64, 68, 46), "대쉬", 23, canDash ? Color.white : new Color(.6f, .65f, .7f));
            DeliveryUIArt.Text(new Rect(360, h - 64, 66, 46), Time.time < playerItems.batteryUntil ? "0" : "50", 26, canDash ? colors[0] : new Color(.6f, .65f, .7f), TextAnchor.MiddleCenter);
            DeliveryUIArt.Image(new Rect(w - 264, h - 78, 244, 56), "control_panel");
            DeliveryUIArt.Image(new Rect(w - 248, h - 71, 48, 42), "keycap_dark");
            DeliveryUIArt.Text(new Rect(w - 248, h - 71, 48, 42), "E", 24, Color.white, TextAnchor.MiddleCenter);
            DeliveryUIArt.Text(new Rect(w - 184, h - 73, 148, 46), "상호작용", 24, Color.white, TextAnchor.MiddleCenter);
            if (Selected != WeaponKind.None)
            {
                DeliveryUIArt.Image(new Rect(w - 264, h - 142, 244, 56), "control_panel");
                DeliveryUIArt.Image(new Rect(w - 248, h - 135, 48, 42), "keycap_dark");
                DeliveryUIArt.Text(new Rect(w - 248, h - 135, 48, 42), "G", 24, Color.white, TextAnchor.MiddleCenter);
                DeliveryUIArt.Text(new Rect(w - 190, h - 137, 160, 46), "아이템 버리기", 21, Color.white, TextAnchor.MiddleCenter);
            }
            if (IsSpecial(Selected) || Selected == WeaponKind.BoxingGloves || Selected == WeaponKind.Plunger || Selected == WeaponKind.RunningShoes || Selected == WeaponKind.BubbleWrap || Selected == WeaponKind.FountainPen || Selected == WeaponKind.Slime || Selected == WeaponKind.ToyHammer || Selected == WeaponKind.IceCream)
            {
                DeliveryUIArt.Image(new Rect(w - 264, h - 206, 244, 56), "control_panel");
                DeliveryUIArt.Text(new Rect(w - 258, h - 201, 232, 46), IsSpecial(Selected) ? "좌클릭 · 사용" : Selected == WeaponKind.IceCream ? "좌클릭 · 투척" : Selected == WeaponKind.ToyHammer ? "좌클릭 · 휘두르기" : Selected == WeaponKind.Slime ? "좌클릭 · 슬라임 투척" : Selected == WeaponKind.FountainPen ? "좌클릭 · 만년필 투척" : Selected == WeaponKind.BubbleWrap ? "좌클릭 · 보호막" : Selected == WeaponKind.RunningShoes ? "좌클릭 · 속도 강화" : Selected == WeaponKind.Plunger ? "좌클릭 · 던지기" : "좌클릭 · 밀치기", 22, Color.white, TextAnchor.MiddleCenter);
            }

DeliveryUIArt.Image(new Rect(w * .5f - 20, h * .5f - 20, 40, 40), "crosshair");
            if (focusedTarget)
            {
                bool parcel = IsParcelTarget(focusedTarget);
                var panel = new Rect(w * .5f - 180, h * .5f + 92, 360, parcel ? 150 : 176);
                DeliveryUIArt.Image(panel, "capture_panel");
                var testPickup=focusedTarget.GetComponent<SpecialDeliveryParcel>();
                DeliveryUIArt.Image(new Rect(panel.x + 14, panel.y + 12, 66, 92), testPickup&&testPickup.fixedItem!=WeaponKind.None?"item_"+testPickup.fixedItem:parcel ? "parcel_icon" : "mailbox_icon");
                DeliveryUIArt.Text(new Rect(panel.x + 94, panel.y + 14, 250, 38), focusedTarget.displayName, 28, Color.white);
                if (interactionReady)
                {
                    DeliveryUIArt.Image(new Rect(panel.x + 96, panel.y + 59, 48, 48), "keycap_light");
                    DeliveryUIArt.Text(new Rect(panel.x + 96, panel.y + 59, 48, 48), "E", 28, new Color(.05f, .06f, .08f), TextAnchor.MiddleCenter);
                    DeliveryUIArt.Text(new Rect(panel.x + 157, panel.y + 58, 190, 50), HudInteractionHint.Replace("E ", "").Replace("· ", ""), 25, Color.white);
                }
                else DeliveryUIArt.Text(new Rect(panel.x + 94, panel.y + 53, 246, 61), HudInteractionHint, 19, new Color(.77f, .81f, .87f));
                if (!parcel) DeliveryUIArt.Bar(new Rect(panel.x + 20, panel.y + 121, 320, 18), HudCaptureProgress, colors[0]);
                string progress = parcel && !AllowsSpecialReplacement(focusedTarget) && !HasEmptySlot() ? "슬롯 가득 참" : captureTarget >= 0 ? "점령 중 " + Mathf.FloorToInt(HudCaptureProgress * 100) + "%" : HudInteractionHint == "점령 완료" ? "내 우체통" : interactionReady ? (parcel ? "눌러서 줍기" : CaptureSeconds.ToString("0.#") + "초 동안 유지해 점령") : "상호작용 대기";
                DeliveryUIArt.Text(new Rect(panel.x + 20, panel.y + (parcel ? 115 : 141), 320, 30), progress, 21, Color.white, TextAnchor.MiddleCenter);
            }
            for (int slot = 0; slot < SlotCount; slot++)
            {
                var rect = new Rect(w * .5f - 254 + slot * 174, h - 156, 160, 128);
                bool selected = slot == selectedSlot;
                DeliveryUIArt.Image(rect, "slot_panel");
                if (selected) DeliveryUIArt.Image(rect, "slot_outline", colors[0]);
                var key = new Rect(rect.x + 12, rect.y + 12, 34, 32);
                DeliveryUIArt.Image(key, selected ? "keycap_light" : "keycap_dark", selected ? colors[0] : Color.white);
                DeliveryUIArt.Text(key, (slot + 1).ToString(), 22, selected ? new Color(.06f, .07f, .08f) : Color.white, TextAnchor.MiddleCenter);
                if (inventory[slot] == WeaponKind.None)
                {
                    DeliveryUIArt.Image(new Rect(rect.x + 60, rect.y + 38, 40, 44), "parcel_icon", new Color(1, 1, 1, .25f));
                    }
                else
                {
                    DeliveryUIArt.Image(new Rect(rect.x + 52, rect.y + 24, 56, 56), "item_" + inventory[slot]);
                    DeliveryUIArt.Text(new Rect(rect.x + 8, rect.y + 79, 144, 20), ItemName(inventory[slot]), 14, Color.white, TextAnchor.MiddleCenter);
                    string remaining = ItemRemainingText(slot);
                    DeliveryUIArt.Text(new Rect(rect.x + 10, rect.y + 100, 140, 16), remaining, 14, selected ? colors[0] : new Color(.75f, .79f, .85f), TextAnchor.MiddleCenter);
                }
            }
            if (playerItems.visionBlockedUntil > Time.time)
                DeliveryUIArt.Text(new Rect(w * .5f - 150, h * .5f - 80, 300, 36), "잉크 · 시야 차단", 22, Color.white, TextAnchor.MiddleCenter);
            else if (playerItems.Stunned(Time.time))
                DeliveryUIArt.Text(new Rect(w * .5f - 150, h * .5f - 80, 300, 36), playerItems.Frozen(Time.time) ? "빙결 · 잠시 행동 불가" : "기절 · 잠시 행동 불가", 22, Color.white, TextAnchor.MiddleCenter);
        }
        finally { GUI.matrix = oldMatrix; GUI.color = oldColor; }
    }
}

public static class DeliveryUIArt
{
    static readonly Dictionary<string, Texture2D> images = new();
    static readonly Dictionary<int, GUIStyle> styles = new();
    public static Texture2D Texture(string name)
    {
        if (!images.TryGetValue(name, out var image) || !image)
        {
            image = Resources.Load<Texture2D>("UI/InGame/" + name);
            if (!image) throw new System.InvalidOperationException("Missing UI PNG: " + name);
            images[name] = image;
        }
        return image;
    }
    public static void Image(Rect rect, string name) => Image(rect, name, Color.white);
    public static void Image(Rect rect, string name, Color tint)
    {
        var old = GUI.color; GUI.color = tint; GUI.DrawTexture(rect, Texture(name)); GUI.color = old;
    }
    public static void Text(Rect rect, string text, int size, Color color, TextAnchor alignment = TextAnchor.MiddleLeft)
    {
        if (!styles.TryGetValue(size, out var style))
            styles[size] = style = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = FontStyle.Bold, wordWrap = true, padding = new RectOffset(0, 0, 0, 0) };
        style.normal.textColor = color; style.alignment = alignment;
        GUI.Label(rect, text, style);
    }
    public static void Bar(Rect rect, float fraction, Color tint)
    {
        Image(rect, "bar_track");
        fraction = Mathf.Clamp01(fraction);
        if (fraction <= 0) return;
        var old = GUI.color; GUI.color = tint;
        GUI.DrawTextureWithTexCoords(new Rect(rect.x, rect.y, rect.width * fraction, rect.height), Texture("bar_fill"), new Rect(0, 0, fraction, 1));
        GUI.color = old;
    }
}
