using UnityEngine;

public sealed partial class MonsterDeliveryPrototype
{
    const float GlovePushDuration = .42f, GloveHitDelay = .16f;
    int boxingSlot = -1;
    float boxingStarted;
    bool boxingHit;
    bool boxingRightHand, nextBoxingRightHand = true;

    float GlovePushAmount
    {
        get
        {
            float elapsed = Time.time - boxingStarted;
            if (elapsed < .07f) return -.12f * Mathf.SmoothStep(0, 1, elapsed / .07f);
            if (elapsed < GloveHitDelay) return Mathf.Lerp(-.12f, 1, Mathf.SmoothStep(0, 1, (elapsed - .07f) / (GloveHitDelay - .07f)));
            return 1 - Mathf.SmoothStep(0, 1, (elapsed - GloveHitDelay) / (GlovePushDuration - GloveHitDelay));
        }
    }

    void StartGlovePush()
    {
        boxingSlot = selectedSlot; boxingStarted = Time.time; boxingHit = false;
        boxingRightHand = nextBoxingRightHand;
        nextBoxingRightHand = !nextBoxingRightHand;
        captureTarget = -1; captureStarted = 0;
        playerItems.busyUntil = Time.time + GlovePushDuration;
        nextAttackAt = Time.time + .6f;
        feedback = "복싱 글러브 · 밀치기!";
    }

    void UpdateGlovePush()
    {
        if (boxingSlot < 0) return;
        if (!started || finished || playerItems.Stunned(Time.time) || Time.time < rollUntil)
        { FinishGlovePush(); return; }
        float elapsed = Time.time - boxingStarted;
        if (!boxingHit && elapsed >= GloveHitDelay)
        {
            boxingHit = true;
            // Constant horizontal drag and gravity make travel proportional to initial speed squared.
            float distanceScale = Mathf.Sqrt(3);
            HitGloveTarget(18 * distanceScale, 6 * distanceScale);
        }
        if (elapsed >= GlovePushDuration) FinishGlovePush();
    }

    void HitGloveTarget(float push, float lift)
    {
        Physics.SyncTransforms();
        var ray = cam.ViewportPointToRay(new Vector3(.5f, .5f, 0));
        if (!InteractionRayHit(ray, 5 - cam.nearClipPlane, out var hit)) return;
        for (int id = 1; id <= BotCount; id++)
        {
            var actor = ActorController(id);
            if (!actor || !actor.gameObject.activeSelf || hit.collider != actor) continue;
            var direction = actor.transform.position - player.transform.position; direction.y = 0;
            ApplyControl(id, direction.normalized * push + Vector3.up * lift);
            return;
        }
    }

    void FinishGlovePush()
    {
        boxingSlot = -1;
        if (heldItem && !thirdPersonPreview) heldItem.PushFirstPerson(0, boxingRightHand);
    }
}
