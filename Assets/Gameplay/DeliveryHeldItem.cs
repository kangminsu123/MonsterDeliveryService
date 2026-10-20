using System.Linq;
using UnityEngine;

// Saved poses are calibrated against Char's armed frame; geometry remains metre-scale.
public sealed class DeliveryHeldItem : MonoBehaviour
{
    public Vector3 firstPersonPosition;
    public Quaternion firstPersonRotation = Quaternion.identity;
    public float firstPersonScale = 1;
    public string thirdPersonBone;
    public Vector3 thirdPersonPosition;
    public Quaternion thirdPersonRotation = Quaternion.identity;
    public Vector3 rightPosition, leftPosition;
    public Quaternion rightRotation = Quaternion.identity, leftRotation = Quaternion.identity;
    public bool paired;
    Transform bone, rightBone, leftBone, rightPart, leftPart;
    Vector3 rightViewPosition, leftViewPosition;
    Quaternion rightViewRotation, leftViewRotation;
    bool viewPoseInitialized;

    public void FirstPerson()
    {
        transform.localPosition = firstPersonPosition;
        transform.localRotation = firstPersonRotation;
        transform.localScale = Vector3.one * firstPersonScale;
        if (!paired) return;
        // Network presentation calls this every frame: restore the calibrated guard, never add rotation again.
        if (viewPoseInitialized) { PushFirstPerson(0, true); return; }
        foreach (var part in GetComponentsInChildren<Transform>().Where(t => t.name == "Hand_R" || t.name == "Hand_L"))
        {
            float side = -Mathf.Sign(part.localPosition.x);
            // Camera-space guard: lower wrists, raised knuckles, a small inward angle.
            part.position = transform.parent.TransformPoint(firstPersonPosition + new Vector3(side * .37f, side < 0 ? -.02f : 0, side < 0 ? .035f : 0));
            var viewRotation = transform.parent.rotation;
            part.rotation = viewRotation * Quaternion.Euler(0, -side * 12, -side * 7) * Quaternion.Inverse(viewRotation) * part.rotation;
            if (part.name == "Hand_R") rightPart = part; else leftPart = part;
        }
        rightViewPosition = transform.parent.InverseTransformPoint(rightPart.position);
        leftViewPosition = transform.parent.InverseTransformPoint(leftPart.position);
        rightViewRotation = Quaternion.Inverse(transform.parent.rotation) * rightPart.rotation;
        leftViewRotation = Quaternion.Inverse(transform.parent.rotation) * leftPart.rotation;
        viewPoseInitialized = true;
    }
    public void PushFirstPerson(float amount, bool rightHand)
    {
        if (!paired || !rightPart || !leftPart) return;
        // FBX part names can be mirrored in camera space; choose the visible hand by its guard position.
        PushPart(rightPart, rightViewPosition, rightViewRotation, (rightViewPosition.x > 0) == rightHand ? amount : 0);
        PushPart(leftPart, leftViewPosition, leftViewRotation, (leftViewPosition.x > 0) == rightHand ? amount : 0);
    }
    void PushPart(Transform part, Vector3 position, Quaternion rotation, float amount)
    {
        position += new Vector3(-Mathf.Sign(position.x) * .13f, .15f, .55f) * amount;
        part.SetPositionAndRotation(transform.parent.TransformPoint(position), transform.parent.rotation * Quaternion.Euler(26 * amount, 0, 0) * rotation);
    }
    public void Bind(DeliveryRobotVisual visual)
    {
        var nodes = visual.model.GetComponentsInChildren<Transform>();
        bone = nodes.Single(t => t.name == thirdPersonBone);
        if (!paired) return;
        var arms = nodes.Where(t => t.name == "Arm.R" || t.name == "Arm.L").OrderBy(t => visual.transform.InverseTransformPoint(t.position).x).ToArray();
        leftBone = arms[0]; rightBone = arms[1];
        rightPart = GetComponentsInChildren<Transform>().Single(t => t.name == "Hand_R");
        leftPart = GetComponentsInChildren<Transform>().Single(t => t.name == "Hand_L");
    }
    public void FollowBones()
    {
        transform.SetPositionAndRotation(bone.TransformPoint(thirdPersonPosition), bone.rotation * thirdPersonRotation);
        if (!paired) return;
        rightPart.SetPositionAndRotation(rightBone.TransformPoint(rightPosition), rightBone.rotation * rightRotation);
        leftPart.SetPositionAndRotation(leftBone.TransformPoint(leftPosition), leftBone.rotation * leftRotation);
    }
}
