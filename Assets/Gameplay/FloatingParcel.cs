using UnityEngine;

[DefaultExecutionOrder(-100)]
public sealed class FloatingParcel : MonoBehaviour
{
    public Transform floating;
    [System.NonSerialized] public ParcelSpawnPool spawnPool;
    public const float HoverHeight = 1.1f, BobAmplitude = .12f;

    void Update() => Animate(Time.time);

    public static FloatingParcel CollectTouching(CharacterController actor)
    {
        if (!actor || !actor.enabled || !actor.gameObject.activeInHierarchy) return null;
        var center = actor.transform.TransformPoint(actor.center);
        float half = Mathf.Max(0, actor.height * .5f - actor.radius);
        // Include the controller's contact skin so a solid parcel can be collected on contact.
        foreach (var hit in Physics.OverlapCapsule(center - Vector3.up * half, center + Vector3.up * half,
                     actor.radius + actor.skinWidth, ~0, QueryTriggerInteraction.Ignore))
        {
            var parcel = hit.GetComponentInParent<FloatingParcel>();
            if (parcel && !parcel.GetComponent<SpecialDeliveryParcel>() && parcel.TryCollect()) return parcel;
        }
        return null;
    }

    public void Animate(float time)
    {
        float phase = transform.position.x * .31f + transform.position.z * .17f;
        floating.localPosition = Vector3.up * (HoverHeight + BobAmplitude * Mathf.Sin(time * 1.8f + phase));
        floating.localRotation = Quaternion.Euler(0, time * 12, Mathf.Sin(time * 1.3f + phase) * 3);
    }

    public bool TryCollect()
    {
        var target = GetComponent<InteractionTarget>();
        if (spawnPool && !spawnPool.CanCollect) return false;
        if (!isActiveAndEnabled || !target || !target.available) return false;
        target.available = false; target.SetFocus(false, false);
        gameObject.SetActive(false);
        if (spawnPool) spawnPool.Collected(this);
        return true;
    }
}
