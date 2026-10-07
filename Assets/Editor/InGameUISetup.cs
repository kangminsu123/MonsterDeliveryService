using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class InGameUISetup
{
    const string Folder = "Assets/Resources/UI/InGame";
    [MenuItem("Tools/Gameplay/InGame UI/Build Image Assets")]
    public static void BuildImages()
    {
        if (EditorApplication.isPlaying) throw new Exception("Build in Edit Mode.");
        Directory.CreateDirectory(Folder);
        Panel("score_panel", 310, 124, 25); Panel("timer_panel", 220, 72, 22);
        Panel("capture_panel", 360, 176, 26); Panel("minimap_panel", 350, 414, 26);
        Panel("portrait_panel", 124, 124, 32); Panel("control_panel", 244, 56, 16);
        Panel("badge_panel", 100, 50, 14, false, true);
        Panel("badge_active", 100, 50, 14, true);
        Panel("keycap_light", 48, 48, 10, true); Panel("keycap_dark", 80, 42, 9);
        Panel("bar_track", 340, 24, 12);
        Panel("bar_fill", 340, 24, 12, true);
        ImportItemIcons();

BuildSlotImages();

Crosshair(); MinimapIcons();
        Icon("mailbox_icon", "Assets/Art/DeliveryProps/mailbox.prefab", false);
        Icon("parcel_icon", "Assets/Art/DeliveryProps/parcel.prefab", false);
        Icon("robot_portrait", "Assets/Resources/Characters/DeliveryRobotPlayer.prefab", true);
        File.WriteAllText(Folder + "/README.md",
            "# 인게임 UI 이미지\n\n최신 1인칭 UI 시안을 참고해 게임에 사용할 투명 PNG를 부분별로 재제작했다. 글자와 점수는 이미지에 합성하지 않고 실제 상태로 표시한다.\n\n"
            + "- score_panel: 왼쪽 위 점령 현황\n- timer_panel: 상단 시간\n- badge_panel / badge_active: 플레이어 점령 수\n- minimap_panel: 전체 맵 프레임\n- capture_panel: 상호작용·점령 진행\n- portrait_panel / robot_portrait: 내 캐릭터\n- control_panel: 대쉬·상호작용 안내\n- keycap_light / keycap_dark: 키 표시\n- bar_track / bar_fill: 스태미나와 점령 진행바\n- slot_panel / slot_outline: 중앙 하단 아이템 슬롯 배경과 선택 테두리\n- crosshair: 중앙 조준점\n- mailbox_icon / parcel_icon: 실제 게임 모델로 렌더한 대상 아이콘\n\n"
            + "패널은 2배 해상도와 투명 모서리, Sprite 9-slice border로 저장했다. bar_fill과 배지는 플레이어 색으로 틴트한다. Tools > Gameplay > InGame UI > Build Image Assets에서 재생성할 수 있다.\n");
        AssetDatabase.Refresh();
        foreach (var path in Directory.GetFiles(Folder, "*.png"))
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None; importer.maxTextureSize = 1024;
            importer.filterMode = FilterMode.Bilinear;
            if (Path.GetFileName(path).Contains("panel") || Path.GetFileName(path).Contains("badge"))
                importer.spriteBorder = Vector4.one * 28;
            importer.SaveAndReimport();
        }
        UnityEngine.Debug.Log("INGAME_UI_IMAGES: " + Directory.GetFiles(Folder, "*.png").Length + " separate transparent PNGs saved.");
    }
    [MenuItem("Tools/Gameplay/InGame UI/Import Existing Item Icons")]
    public static void ImportItemIcons()
    {
        if (EditorApplication.isPlaying) throw new Exception("Import in Edit Mode.");
        Directory.CreateDirectory(Folder);
        for (int i = 1; i <= 10; i++)
        {
            var source = Directory.GetFiles("Assets/Art/DeliveryItemConcepts", (i + 1).ToString("D2") + "_*.png").Single();
            var path = Folder + "/item_" + (WeaponKind)i + ".png";
            File.Copy(source, path, true);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 256;
            importer.npotScale = TextureImporterNPOTScale.None; importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }
        Debug.Log("ITEM_ICONS_IMPORTED: reused all 10 original concept PNGs.");
    }


[MenuItem("Tools/Gameplay/InGame UI/Build Item Slot Images")]
    public static void BuildSlotImages()
    {
        if (EditorApplication.isPlaying) throw new Exception("Build in Edit Mode.");
        Directory.CreateDirectory(Folder);
        Panel("slot_panel", 160, 128, 18);
        Panel("slot_outline", 160, 128, 18, outline: true);
        AssetDatabase.Refresh();
        foreach (var name in new[] { "slot_panel", "slot_outline" })
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(Folder + "/" + name + ".png");
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None; importer.maxTextureSize = 1024;
            importer.maxTextureSize = 1024;
            importer.filterMode = FilterMode.Bilinear; importer.spriteBorder = Vector4.one * 28;
            importer.SaveAndReimport();
        }
    }


static void Panel(string name, int width, int height, float radius, bool light = false, bool tint = false, bool outline = false)
    {
        int w = width * 2, h = height * 2;
        var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var pixels = new Color[w * h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            var q = new Vector2(Mathf.Abs(x + .5f - w * .5f) - (w * .5f - radius * 2 - 2),
                Mathf.Abs(y + .5f - h * .5f) - (h * .5f - radius * 2 - 2));
            float d = new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0) - radius * 2;
            float coverage = Mathf.Clamp01(.5f - d);
            float border = Mathf.Clamp01(d + 3.5f);
            float t = (float)y / h;
            var fill = light ? new Color(.96f, .97f, .99f, .98f) : tint ? new Color(.11f, .12f, .14f, .94f)
                : Color.Lerp(new Color(.065f, .078f, .10f, .94f), new Color(.13f, .15f, .19f, .93f), t);
            if (outline) fill = Color.clear;
            var edge = outline ? Color.white : new Color(.9f, .92f, .95f, .82f);
            var c = Color.Lerp(fill, edge, border); c.a *= coverage;
            pixels[y * w + x] = c;
        }
        texture.SetPixels(pixels); texture.Apply(); Save(name, texture);
    }
    static void MinimapIcons()
    {
        foreach (bool arrow in new[] { false, true })
        {
            var texture = new Texture2D(48, 48, TextureFormat.RGBA32, false);
            var pixels = new Color[48 * 48];
            for (int y = 0; y < 48; y++) for (int x = 0; x < 48; x++)
            {
                float px = x * .5f, py = y * .5f;
                bool visible = arrow ? py >= 3 && py <= 22 && Mathf.Abs(px - 11.5f) <= (22 - py) * .48f
                    : (px >= 10 && px <= 13 && py >= 1 && py <= 9)
                    || (px >= 3 && px <= 19 && py >= 8 && py <= 17)
                    || (px >= 5 && px <= 17 && py >= 18 && py <= 20)
                    || (px >= 17 && px <= 21 && py >= 17 && py <= 22);
                pixels[y * 48 + x] = visible ? Color.white : Color.clear;
            }
            texture.SetPixels(pixels); texture.Apply(); Save(arrow ? "player_arrow" : "map_mailbox", texture);
        }
    }

static void Crosshair()
    {
        var texture = new Texture2D(80, 80, TextureFormat.RGBA32, false);
        var pixels = new Color[80 * 80];
        for (int y = 0; y < 80; y++) for (int x = 0; x < 80; x++)
        {
            float dx = Mathf.Abs(x - 39.5f), dy = Mathf.Abs(y - 39.5f);
            bool ink = dx <= 2 && dy >= 12 && dy <= 30 || dy <= 2 && dx >= 12 && dx <= 30 || dx <= 2 && dy <= 2;
            bool outline = dx <= 4 && dy >= 10 && dy <= 32 || dy <= 4 && dx >= 10 && dx <= 32 || dx <= 4 && dy <= 4;
            pixels[y * 80 + x] = ink ? Color.white : outline ? new Color(0, 0, 0, .7f) : Color.clear;
        }
        texture.SetPixels(pixels); texture.Apply(); Save("crosshair", texture);
    }
    static void Icon(string name, string path, bool portrait)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (!prefab) throw new Exception("Icon model missing: " + path);
        var clone = UnityEngine.Object.Instantiate(prefab);
        clone.transform.position = new Vector3(10000, 10000, 10000);
        var target = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32);
        var oldTarget = RenderTexture.active;
        var cameraObject = new GameObject("UI Icon Capture");
        var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
        var key = new GameObject("UI Icon Light").AddComponent<Light>(); key.type = LightType.Directional;
        key.intensity = 1.8f; key.transform.rotation = Quaternion.Euler(40, -30, 0); key.cullingMask = 1 << 31;
        var ambient = RenderSettings.ambientLight; bool fog = RenderSettings.fog;
        try
        {
            foreach (var component in clone.GetComponentsInChildren<MonoBehaviour>()) component.enabled = false;
            foreach (var animator in clone.GetComponentsInChildren<Animator>()) animator.enabled = false;
            foreach (var node in clone.GetComponentsInChildren<Transform>()) node.gameObject.layer = 31;
            foreach (var renderer in clone.GetComponentsInChildren<Renderer>())
                if (renderer.transform.name.Contains("Bubble") || renderer.GetComponentsInParent<Transform>().Any(t => t.name == "CaptureBeacon")) renderer.enabled = false;
            var renderers = clone.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
            var bounds = renderers[0].bounds; foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            var center = bounds.center;
            if (portrait) center.y = bounds.max.y - bounds.size.y * .29f;
            camera.cullingMask = 1 << 31; camera.orthographic = true;

            camera.cullingMask = 1 << 31; camera.orthographic = true;
            camera.orthographicSize = portrait ? bounds.size.y * .38f : Mathf.Max(bounds.size.y, bounds.size.x) * .57f;
            if (name == "mailbox_icon") { center.y = bounds.max.y - bounds.size.y * .26f; camera.orthographicSize = bounds.size.y * .31f; }
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
            camera.nearClipPlane = .01f; camera.farClipPlane = 30;
            camera.transform.position = center + (portrait ? new Vector3(0, .12f, 5) : new Vector3(-3, 1.4f, 5));
            camera.transform.LookAt(center);
            RenderSettings.ambientLight = new Color(.7f, .7f, .7f); RenderSettings.fog = false;
            camera.targetTexture = target;
            if (portrait) camera.Render(); else camera.RenderWithShader(Resources.Load<Shader>("MinimapFlat"), ""); RenderTexture.active = target;
            var image = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); image.Apply(); Save(name, image);
        }
        finally
        {
            RenderSettings.ambientLight = ambient; RenderSettings.fog = fog;
            RenderTexture.active = oldTarget; camera.targetTexture = null; target.Release();
            UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(key.gameObject); UnityEngine.Object.DestroyImmediate(clone);
        }
    }
    static void Save(string name, Texture2D texture)
    {
        File.WriteAllBytes(Folder + "/" + name + ".png", texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
    }
}
