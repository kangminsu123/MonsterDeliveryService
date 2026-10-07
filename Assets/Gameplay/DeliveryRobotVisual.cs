using UnityEngine;

// The Blender clips move only the visual bones; movement and camera stay stable.
public sealed class DeliveryRobotVisual : MonoBehaviour
{
    public Transform model;
    public const float TeamEmission = .88f;
    Animator animator;
    Renderer[] renderers;
    MaterialPropertyBlock colorBlock;

    void Awake()
    {
        animator = model.GetComponent<Animator>();
        colorBlock = new MaterialPropertyBlock();
        renderers = model.GetComponentsInChildren<Renderer>();
    }

    public void SetMotion(bool moving, bool holding)
    {
        animator.SetBool("Moving", moving);
        animator.SetBool("Holding", holding);
    }

    public void SetColor(Color color)
    {
        color.a = 1;
        colorBlock ??= new MaterialPropertyBlock();
        foreach (var renderer in renderers)
        {
            var materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                if (!materials[i] || !materials[i].name.StartsWith("MAT_PlayerColor")) continue;
                renderer.GetPropertyBlock(colorBlock, i);
                colorBlock.SetColor("_Color", color);
                colorBlock.SetColor("_EmissionColor", color * TeamEmission);
                renderer.SetPropertyBlock(colorBlock, i);
            }
        }
    }
}
