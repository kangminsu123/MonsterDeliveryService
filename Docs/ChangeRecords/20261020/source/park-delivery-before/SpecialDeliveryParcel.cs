using UnityEngine;

// One authored park parcel; the host owns collection and the ten-second test respawn.
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
    public void Initialize(MonsterDeliveryPrototype owner) { game = owner; ResetRound(); }
    public void ResetRound() => ApplySnapshot(0);
    void Update() { if (game && game.ParcelAuthority && !Available && Remaining <= 0) ApplySnapshot(0); }
    public bool TryCollect()
    {
        if (!game || !game.ParcelAuthority || !Available) return false;
        ApplySnapshot(respawnSeconds); return true;
    }
    public void ApplySnapshot(float remaining)
    {
        readyAt = Time.time + Mathf.Max(0, remaining);
        bool available = remaining <= 0;
        GetComponent<InteractionTarget>().available = available;
        GetComponent<BoxCollider>().enabled = available;
        if (model) model.SetActive(available);
    }
}
