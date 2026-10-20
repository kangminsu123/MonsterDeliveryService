using System;
using System.Linq;
using UnityEngine;

// Saved, verified anchors; only the authority chooses occupancy. Reuse the same 50 models.
[DefaultExecutionOrder(100)]
public sealed class ParcelSpawnPool : MonoBehaviour
{
    public const int SpawnCount = 100, LiveCount = 50;
    public Transform[] spawnPoints;
    public FloatingParcel[] parcels;
    [SerializeField] int[] slots = new int[LiveCount];
    readonly int[] lastSlots = new int[LiveCount];
    MonsterDeliveryPrototype game;

    public int ActiveCount => parcels.Count(p => p.gameObject.activeSelf);
    public bool CanCollect => game && game.ParcelAuthority;
    public int[] CaptureSlots() => (int[])slots.Clone();
    public void Initialize(MonsterDeliveryPrototype source)
    {
        if (spawnPoints.Length != SpawnCount || parcels.Length != LiveCount)
            throw new InvalidOperationException("Parcel pool needs 100 anchors and 50 models.");
        game = source;
        foreach (var parcel in parcels) parcel.spawnPool = this;
        ResetRound();
    }
    public void ResetRound()
    {
        // Partial Fisher-Yates: each occupied subset is sampled without replacement.
        var choices = Enumerable.Range(0, SpawnCount).ToArray();
        for (int i = 0; i < LiveCount; i++)
        {
            int selected = UnityEngine.Random.Range(i, SpawnCount);
            (choices[i], choices[selected]) = (choices[selected], choices[i]);
            Place(i, choices[i]);
        }
    }
    void Place(int parcel, int slot)
    {
        slots[parcel] = slot; lastSlots[parcel] = slot;
        var p = parcels[parcel]; var target = p.GetComponent<InteractionTarget>();
        target.SetFocus(false, false);
        if (slot >= 0)
        {
            p.transform.SetPositionAndRotation(spawnPoints[slot].position, spawnPoints[slot].rotation);
            p.Animate(Time.time);
        }
        target.available = slot >= 0;
        p.gameObject.SetActive(slot >= 0);
    }
    public void Collected(FloatingParcel parcel)
    {
        int index = Array.IndexOf(parcels, parcel);
        if (index >= 0) { lastSlots[index] = slots[index]; slots[index] = -1; }
    }
    void LateUpdate()
    {
        if (game && game.enabled && CanCollect) Refill();
    }
    public void Refill()
    {
        if (!CanCollect) return;
        for (int i = 0; i < parcels.Length; i++)
        {
            if (slots[i] >= 0) continue;
            // Keep the just-collected position empty for this refill, preventing pickup loops.
            var free = Enumerable.Range(0, SpawnCount).Where(s => s != lastSlots[i] && !slots.Contains(s)).ToArray();
            if (free.Length > 0) Place(i, free[UnityEngine.Random.Range(0, free.Length)]);
        }
    }
    public bool ApplySnapshot(int[] state)
    {
        if (state == null || state.Length != LiveCount) return false;
        ulong occupiedLow=0,occupiedHigh=0;
        for(int i=0;i<state.Length;i++)
        {
            int slot=state[i];if(slot < -1||slot>=SpawnCount)return false;
            if(slot<0)continue;
            ulong bit=1UL<<(slot&63);
            if(slot<64){if((occupiedLow&bit)!=0)return false;occupiedLow|=bit;}
            else{if((occupiedHigh&bit)!=0)return false;occupiedHigh|=bit;}
        }
        for (int i = 0; i < state.Length; i++)
            if(slots[i]!=state[i])Place(i, state[i]);
        return true;
    }
    void OnDrawGizmosSelected()
    {
        if (spawnPoints == null) return;
        for (int i = 0; i < spawnPoints.Length; i++) if (spawnPoints[i])
        {
            Gizmos.color = slots != null && slots.Contains(i) ? Color.cyan : Color.yellow;
            Gizmos.DrawWireSphere(spawnPoints[i].position + Vector3.up * FloatingParcel.HoverHeight, .7f);
        }
    }
}
