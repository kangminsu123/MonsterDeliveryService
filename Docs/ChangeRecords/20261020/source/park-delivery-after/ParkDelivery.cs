using UnityEngine;

public sealed partial class MonsterDeliveryPrototype
{
    public const string ParkDeliveryNotice = "공원에 취급주의 택배가 도착했습니다!";
    int deliveryMinute;
    float deliveryNoticeUntil;
    MeshCollider deliveryPaving;

    void UpdateParkDelivery()
    {
        if (!ParcelAuthority || !started || finished || mapEditMode || SpecialDevelopmentPickups || !specialParcel) return;
        float elapsed = NetPlaying ? roundSeconds - (netRoundUntil - Time.time) : Time.time - startedAt;
        if (elapsed >= roundSeconds) return;
        int minute = Mathf.FloorToInt(elapsed / 60);
        if (minute <= deliveryMinute || minute == 0 || !TryParkDeliveryPosition(out var position)) return;
        deliveryMinute = minute;
        if (!specialParcel.TryGetComponent<ParkDeliveryGlow>(out _)) specialParcel.gameObject.AddComponent<ParkDeliveryGlow>();
        specialParcel.Deliver(position);
        deliveryNoticeUntil = Time.time + 5;
        Physics.SyncTransforms();
    }

    bool TryParkDeliveryPosition(out Vector3 position)
    {
        position = default;
        if (!deliveryPaving)
            deliveryPaving = GameObject.Find("Paved_Plaza_and_Four_Entrances")?.GetComponent<MeshCollider>();
        if (!deliveryPaving || !specialParcel) return false;
        var bounds = deliveryPaving.bounds;
        var box = specialParcel.GetComponent<BoxCollider>();
        var half = Vector3.Scale(box.size, specialParcel.transform.lossyScale) * .5f;
        // Sample only the authored park paving; reject edges and occupied box footprints.
        for (int attempt = 0; attempt < 160; attempt++)
        {
            var ray = new Ray(new Vector3(Random.Range(bounds.min.x + half.x, bounds.max.x - half.x),
                bounds.max.y + 10, Random.Range(bounds.min.z + half.z, bounds.max.z - half.z)), Vector3.down);
            if (!deliveryPaving.Raycast(ray, out var ground, 30) || ground.normal.y < .9f) continue;
            var at = ground.point + Vector3.up * .025f;
            bool clear = true;
            foreach (var corner in new[] { new Vector3(-half.x,0,-half.z), new Vector3(half.x,0,-half.z),
                new Vector3(-half.x,0,half.z), new Vector3(half.x,0,half.z) })
                if (!deliveryPaving.Raycast(new Ray(at + corner + Vector3.up * 3, Vector3.down), out _, 4)) { clear = false; break; }
            if (!clear) continue;
            var center = at + specialParcel.transform.TransformVector(box.center);
            foreach (var collider in Physics.OverlapBox(center, half + new Vector3(.2f,0,.2f), specialParcel.transform.rotation, ~0, QueryTriggerInteraction.Ignore))
                if (collider != deliveryPaving && collider != box) { clear = false; break; }
            if (clear) { position = at; return true; }
        }
        return false;
    }

    void ApplyParkDelivery(bool available, Vector3 position, int minute, float notice)
    {
        if (!specialParcel || SpecialDevelopmentPickups) return;
        deliveryMinute = minute;
        deliveryNoticeUntil = Time.time + Mathf.Clamp(notice, 0, 5);
        if (!specialParcel.TryGetComponent<ParkDeliveryGlow>(out _)) specialParcel.gameObject.AddComponent<ParkDeliveryGlow>();
        specialParcel.transform.position = position;
        specialParcel.SetAvailable(available);
    }

    void DrawParkDeliveryNotice(float width)
    {
        if (Time.time >= deliveryNoticeUntil) return;
        DeliveryUIArt.Image(new Rect(width * .5f - 370, 186, 740, 68), "control_panel");
        DeliveryUIArt.Text(new Rect(width * .5f - 360, 191, 720, 56), ParkDeliveryNotice, 28,
            new Color(1, .85f, .42f), TextAnchor.MiddleCenter);
    }
}
