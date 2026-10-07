using UnityEngine;

[DefaultExecutionOrder(-100)]
public sealed class FloatingParcel : MonoBehaviour
{
    public Transform floating;
    public const float HoverHeight = 1.1f, BobAmplitude = .12f;

    void Update() => Animate(Time.time);

    public void Animate(float time)
    {
        float phase = transform.position.x * .31f + transform.position.z * .17f;
        floating.localPosition = Vector3.up * (HoverHeight + BobAmplitude * Mathf.Sin(time * 1.8f + phase));
        floating.localRotation = Quaternion.Euler(0, time * 12, Mathf.Sin(time * 1.3f + phase) * 3);
    }

    public bool TryCollect()
    {
        var target = GetComponent<InteractionTarget>();
        if (!isActiveAndEnabled || !target || !target.available) return false;
        target.available = false; target.SetFocus(false, false);
        gameObject.SetActive(false);
        return true;
    }
}
