using System.Collections.Generic;
using UnityEngine;

// Static north-up map; only the player and ownership markers change each frame.
public sealed class DeliveryMinimap : MonoBehaviour
{
    MonsterDeliveryPrototype game;
    Transform player;
    GameObject[] mailboxes;
    Texture2D map, mailboxIcon, playerIcon;
    Bounds mapBounds;

    public Bounds MapBounds => mapBounds;
    public Texture2D MapTexture => map;
    public Vector2 MapPosition(Vector3 world)
    {
        return new Vector2(Mathf.Clamp01((world.x - mapBounds.min.x) / mapBounds.size.x),
            1 - Mathf.Clamp01((world.z - mapBounds.min.z) / mapBounds.size.z));
    }

    public void Initialize(MonsterDeliveryPrototype source, Transform actor, GameObject[] boxes)
    {
        game = source; player = actor; mailboxes = boxes;
        var bounds = new Bounds(actor.position, Vector3.zero);
        foreach (var box in boxes) if (box) bounds.Encapsulate(box.transform.position);
        foreach (var name in new[] { "Layer_00_GrassBase", "Layer_01_Sidewalks", "Layer_02_Roads", "Layer_04_Houses", "Layer_05_Yards",
            "Layer_06_LowPolyTownProps", "Layer_07_ReferenceLandscape", "Layer_08_RooftopAccess", "Layer_09_PitchedRoofRamps",
            "Layer_03_IslandTerrain/CoastalCliffs" })
        {
            var layer = GameObject.Find(name);
            if (layer) foreach (var renderer in layer.GetComponentsInChildren<Renderer>()) if (renderer.enabled) bounds.Encapsulate(renderer.bounds);
        }
        var shader = Resources.Load<Shader>("MinimapFlat");
        if (!shader || !shader.isSupported) throw new System.InvalidOperationException("MinimapFlat shader is missing or unsupported.");
        float side = Mathf.Max(bounds.size.x, bounds.size.z) + 28;
        mapBounds = new Bounds(bounds.center, new Vector3(side, bounds.size.y, side));
        var cameraObject = new GameObject("Minimap Bake Camera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false; camera.orthographic = true; camera.orthographicSize = side * .5f;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f, .16f, .18f);
        camera.transform.SetPositionAndRotation(new Vector3(bounds.center.x, bounds.max.y + 100, bounds.center.z), Quaternion.Euler(90, 0, 0));
        camera.nearClipPlane = .1f; camera.farClipPlane = bounds.size.y + 220;
        var target = new RenderTexture(512, 512, 24);
        var previousTarget = RenderTexture.active;
        var hidden = new List<Renderer>();
        var enabled = new List<bool>();
        hidden.AddRange(actor.GetComponentsInChildren<Renderer>());
        for (int i = 1; i <= 3; i++) {
            var rival = GameObject.Find("Rival " + i);
            if (rival) hidden.AddRange(rival.GetComponentsInChildren<Renderer>());
        }
        foreach (var box in boxes) if (box) {
            var beacon = box.transform.Find("CaptureBeacon");
            if (beacon) hidden.AddRange(beacon.GetComponentsInChildren<Renderer>());
        }
        foreach (var parcel in FindObjectsByType<FloatingParcel>(FindObjectsSortMode.None))
            hidden.AddRange(parcel.GetComponentsInChildren<Renderer>());
        foreach (var renderer in hidden) { enabled.Add(renderer.enabled); renderer.enabled = false; }
        try
        {
            camera.targetTexture = target; camera.RenderWithShader(shader, ""); RenderTexture.active = target;
            map = new Texture2D(512, 512, TextureFormat.RGB24, false);
            map.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); map.Apply();
        }
        finally
        {
            for (int i = 0; i < hidden.Count; i++) if (hidden[i]) hidden[i].enabled = enabled[i];
            RenderTexture.active = previousTarget; camera.targetTexture = null;
            target.Release(); Destroy(target); Destroy(cameraObject);
        }
        map.name = "Static Neighborhood Minimap";
        mailboxIcon = DeliveryUIArt.Texture("map_mailbox"); playerIcon = DeliveryUIArt.Texture("player_arrow");
    }



void OnGUI()
    {
        if (!game || !game.ShowMinimap || !map || !player) return;
        float scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
        var oldColor = GUI.color; var oldMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);
        float width = Screen.width / scale;
        var panel = new Rect(width - 388, 28, 350, 414);
        var image = new Rect(panel.x + 16, panel.y + 50, 318, 318);
        try
        {
            GUI.color = Color.white;
            DeliveryUIArt.Image(panel, "minimap_panel");
            DeliveryUIArt.Text(new Rect(panel.x + 22, panel.y + 9, 226, 36), "미니맵", 24, Color.white);
            DeliveryUIArt.Text(new Rect(panel.x + 240, panel.y + 9, 88, 36), "N ↑", 23, Color.white, TextAnchor.MiddleRight);
            GUI.DrawTexture(image, map, ScaleMode.StretchToFill, true, 0, Color.white, 0, 12);
            foreach (var box in mailboxes) if (box)
            {
                int index = System.Array.IndexOf(mailboxes, box);
                DrawMarker(image, MapPosition(box.transform.position), mailboxIcon, game.GetMailboxMapColor(index), 22);
            }
            var point = MapPosition(player.position);
            var center = new Vector2(image.x + point.x * image.width, image.y + point.y * image.height);
            var mapMatrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(player.eulerAngles.y, center);
            DrawMarker(image, point, playerIcon, game.LocalPlayerColor, 28);
            GUI.matrix = mapMatrix; GUI.color = Color.white;
            DeliveryUIArt.Image(new Rect(panel.x + 22, image.yMax + 10, 23, 23), "map_mailbox", game.LocalPlayerColor);
            DeliveryUIArt.Text(new Rect(panel.x + 54, image.yMax + 6, 280, 32), "내 점령  ·  흰색 미점령", 18, Color.white);
        }
        finally { GUI.color = oldColor; GUI.matrix = oldMatrix; }
    }

    static void DrawMarker(Rect image, Vector2 point, Texture2D icon, Color color, float size)
    {
        float x = Mathf.Clamp(image.x + point.x * image.width, image.x + size * .5f, image.xMax - size * .5f);
        float y = Mathf.Clamp(image.y + point.y * image.height, image.y + size * .5f, image.yMax - size * .5f);
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(x - size * .6f, y - size * .6f, size * 1.2f, size * 1.2f), icon);
        GUI.color = color;
        GUI.DrawTexture(new Rect(x - size * .5f, y - size * .5f, size, size), icon);
    }

void OnDestroy() { if (map) Destroy(map); }
}