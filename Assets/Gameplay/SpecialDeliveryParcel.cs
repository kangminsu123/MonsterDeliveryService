using UnityEngine;

// The host owns scheduled park delivery. Fixed test pickups remain available in exploration.
[RequireComponent(typeof(InteractionTarget), typeof(BoxCollider))]
public sealed class SpecialDeliveryParcel : MonoBehaviour
{
    public bool testLoadout = true;
    public int spawnId;
    public WeaponKind fixedItem;
    public bool ReplaceSelected => fixedItem != WeaponKind.None;
    public WeaponKind RewardKind => ReplaceSelected ? fixedItem : (WeaponKind)Random.Range(10, 15);
    [Min(1)] public float respawnSeconds = 10;
    public GameObject model;
    MonsterDeliveryPrototype game;
    float readyAt;
    public float Remaining => Mathf.Max(0, readyAt - Time.time);
    public bool Available => GetComponent<InteractionTarget>().available;
    public void Initialize(MonsterDeliveryPrototype owner)
    {
        game = owner;
        if (fixedItem == WeaponKind.None && !TryGetComponent<ParkDeliveryGlow>(out _)) gameObject.AddComponent<ParkDeliveryGlow>();
        ResetRound();
    }
    public bool Scheduled => fixedItem == WeaponKind.None && game && !game.SpecialDevelopmentPickups;
    public void ResetRound() { if (Scheduled) SetAvailable(false); else ApplySnapshot(0); }
    void Update() { if (game && game.ParcelAuthority && !Scheduled && !Available && Remaining <= 0) ApplySnapshot(0); }
    public bool TryCollect()
    {
        if (!game || !game.ParcelAuthority || !Available) return false;
        if (Scheduled) SetAvailable(false); else ApplySnapshot(respawnSeconds);
        return true;
    }
    public void ApplySnapshot(float remaining)
    {
        readyAt = Time.time + Mathf.Max(0, remaining);
        SetAvailable(remaining <= 0);
    }
    public void Deliver(Vector3 position)
    {
        transform.position = position; readyAt = 0; SetAvailable(true);
    }
    public void SetAvailable(bool available)
    {
        GetComponent<InteractionTarget>().available = available;
        GetComponent<BoxCollider>().enabled = available;
        if (model) model.SetActive(available);
        if (TryGetComponent<ParkDeliveryGlow>(out var glow)) glow.SetVisible(available);
    }
}
