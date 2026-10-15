using UnityEngine;

public sealed partial class MonsterDeliveryPrototype
{
    const int HeldViewLayer = 31;
    WeaponKind heldKind;
    bool heldInFirstPerson;
    DeliveryHeldItem heldItem;
    Camera heldCamera;

    static bool HasHeldModel(WeaponKind kind) => kind != WeaponKind.None && kind != WeaponKind.RunningShoes && kind != WeaponKind.BubbleWrap;

    void UpdateHeldWeapon()
    {
        if (!playerVisual || !cam) return;
        var kind = started && !finished && !mapEditMode && HasHeldModel(Selected) ? Selected : WeaponKind.None;
        bool firstPerson = !thirdPersonPreview;
        if (kind != heldKind || firstPerson != heldInFirstPerson)
        {
            if (heldWeapon) { heldWeapon.SetActive(false); Destroy(heldWeapon); }
            heldWeapon = null; heldItem = null;
            heldKind = kind; heldInFirstPerson = firstPerson;
            if (kind != WeaponKind.None)
            {
                var prefab = Resources.Load<GameObject>("DeliveryItems/" + kind);
                if (!prefab) throw new System.InvalidOperationException("Missing held item prefab: " + kind);
                heldWeapon = new GameObject("Held_" + kind);
                heldWeapon.transform.SetParent(firstPerson ? cam.transform : playerVisual.transform, false);
                heldItem = Instantiate(prefab, heldWeapon.transform, false).GetComponent<DeliveryHeldItem>();
                if (firstPerson)
                {
                    heldItem.FirstPerson();
                    foreach (var t in heldWeapon.GetComponentsInChildren<Transform>()) t.gameObject.layer = HeldViewLayer;
                    if (!heldCamera)
                    {
                        heldCamera = new GameObject("FirstPersonItemCamera").AddComponent<Camera>();
                        heldCamera.transform.SetParent(cam.transform, false);
                        heldCamera.clearFlags = CameraClearFlags.Depth; heldCamera.cullingMask = 1 << HeldViewLayer;
                        heldCamera.nearClipPlane = .01f; heldCamera.farClipPlane = 5;
                        var light = new GameObject("HeldItemSoftLight").AddComponent<Light>();
                        light.transform.SetParent(heldCamera.transform, false);
                        light.transform.localPosition = new Vector3(-.3f, .6f, .1f);
                        light.type = LightType.Point; light.range = 4; light.intensity = 1.8f;
                        light.cullingMask = 1 << HeldViewLayer; light.shadows = LightShadows.None;
                        cam.cullingMask &= ~(1 << HeldViewLayer);
                    }
                }
                else heldItem.Bind(playerVisual);
            }
        }
        if (heldCamera)
        {
            heldCamera.enabled = firstPerson && heldWeapon;
            heldCamera.fieldOfView = cam.fieldOfView; heldCamera.depth = cam.depth + 1;
            heldCamera.rect = cam.rect; heldCamera.aspect = cam.aspect;
        }
        if (heldItem && !firstPerson) heldItem.FollowBones();
    }
    void DestroyHeldPresentation()
    {
        if (heldWeapon) Destroy(heldWeapon);
        if (heldCamera) Destroy(heldCamera.gameObject);
    }
}
