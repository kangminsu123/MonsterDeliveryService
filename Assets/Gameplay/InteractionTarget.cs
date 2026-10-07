using UnityEngine;
using UnityEngine.Rendering;

// A collider under this component participates in the shared crosshair interaction query.
public sealed class InteractionTarget : MonoBehaviour
{
    public string displayName = "물건";
    public string actionHint = "E · 상호작용";
    public float interactionDistance = 3f;
    public bool available = true;
    public MeshFilter visual;
    public Mesh outlineMesh;
    public Material outlineMaterial;
    MeshRenderer outline;
    MaterialPropertyBlock properties;
    public bool IsHighlighted => outline && outline.enabled;

    void Awake()
    {
        if (!visual || !outlineMesh || !outlineMaterial) return;
        var shell = new GameObject("InteractionOutline");
        shell.transform.SetParent(visual.transform, false);
        shell.AddComponent<MeshFilter>().sharedMesh = outlineMesh;
        outline = shell.AddComponent<MeshRenderer>();
        outline.sharedMaterial = outlineMaterial;
        outline.shadowCastingMode = ShadowCastingMode.Off;
        outline.receiveShadows = false;
        outline.enabled = false;
    }
    public void SetFocus(bool focused, bool ready)
    {
        if (!outline) return;
        outline.enabled = focused && ready;
        if (!outline.enabled) return;
        // MaterialPropertyBlock is not restored by a Play Mode script reload.
        properties ??= new MaterialPropertyBlock();
        properties.SetColor("_Color", Color.white);
        outline.SetPropertyBlock(properties);
    }
    void OnDisable() { if (outline) outline.enabled = false; }
}
