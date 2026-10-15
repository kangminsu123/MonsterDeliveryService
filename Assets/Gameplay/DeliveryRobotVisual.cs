using UnityEngine;

// The Blender clips move only the visual bones; movement and camera stay stable.
public sealed class DeliveryRobotVisual : MonoBehaviour
{
    public Transform model;
    public const float HoverClearance = 1f;
    public Vector3 eyeLocalPosition;
    public Vector3 EyePosition => transform.TransformPoint(eyeLocalPosition);
    public const float TeamEmission = .88f;
    Animator animator;
    Renderer[] renderers;
    MaterialPropertyBlock colorBlock;
    bool firstPerson;
    Transform[] arms;

    void Awake()
    {
        animator = model.GetComponent<Animator>();
        colorBlock = new MaterialPropertyBlock();
        renderers = model.GetComponentsInChildren<Renderer>();
        arms = System.Array.FindAll(model.GetComponentsInChildren<Transform>(), t => t.name == "Arm.R" || t.name == "Arm.L");
    }

    public void SetMotion(bool moving, bool holding)
    {
        animator.SetBool("Moving", moving);
        animator.SetBool("Holding", holding);
    }
    public void SetFirstPerson(bool value)
    {
        if (firstPerson == value) return;
        firstPerson = value;
        foreach (var renderer in renderers)
            renderer.shadowCastingMode = value ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On;
    }
    public void PushArm(float amount, bool rightHand)
    {
        foreach (var arm in arms)
            if ((transform.InverseTransformPoint(arm.position).x > 0) == rightHand)
                arm.rotation = Quaternion.AngleAxis(-24 * amount, transform.right) * arm.rotation;
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
