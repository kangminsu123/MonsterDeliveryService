using UnityEngine;
using UnityEngine.AI;

// One local test opponent, using the installed native NavMesh and existing capture rules.
public sealed partial class MonsterDeliveryPrototype
{
    NavMeshData explorationNavigation;
    NavMeshDataInstance explorationNavigationInstance;
    Vector3[] explorationRoute;
    int explorationCorner;
    float nextExplorationPlan;
    DeliveryHeldItem explorationRivalGloves;

    void InitializeExplorationRival()
    {
        // Player builds load baked data without requiring CPU-readable imported meshes.
        explorationNavigation = Resources.Load<NavMeshData>("Navigation/ExplorationIsland");
        if (!explorationNavigation) throw new System.InvalidOperationException("Bake test rival navigation in Tools/Gameplay/Rebuild Exploration Navigation.");
        explorationNavigationInstance = NavMesh.AddNavMeshData(explorationNavigation);
        if (!NavMesh.SamplePosition(bots[0].transform.position, out var spawn, 8, NavMesh.AllAreas))
            throw new System.InvalidOperationException("No walkable test rival spawn.");
        botControllers[0].enabled = false;
        bots[0].transform.position = spawn.position + Vector3.up;
        botControllers[0].enabled = true;
        botItems[0].safePosition = bots[0].transform.position;
        explorationRivalGloves = Instantiate(Resources.Load<GameObject>("DeliveryItems/BoxingGloves"), botVisuals[0].transform, false).GetComponent<DeliveryHeldItem>();
        explorationRivalGloves.name = "TestBoxingGloves";
        botVisuals[0].SetMotion(false, true);
        var animator = botVisuals[0].model.GetComponent<Animator>();
        animator.Play("IDLE_ARMED", 0, 0); animator.Update(0);
        explorationRivalGloves.Bind(botVisuals[0]);
        explorationRivalGloves.FollowBones();
    }

    bool PrepareExplorationRivalTarget()
    {
        if (botTarget[0] >= 0 && owner[botTarget[0]] != 1 && MailboxProtectionRemaining(botTarget[0]) <= 0) return true;
        botTarget[0] = -1;
        if (Time.time < nextExplorationPlan) return false;
        nextExplorationPlan = Time.time + 1;
        if (!NavMesh.SamplePosition(bots[0].transform.position - Vector3.up, out var start, 3, NavMesh.AllAreas)) return false;
        float shortest = float.PositiveInfinity;
        var path = new NavMeshPath();
        for (int i = 0; i < HouseCount; i++)
        {
            if (!mailboxes[i] || owner[i] == 1 || MailboxProtectionRemaining(i) > 0 ||
                !NavMesh.SamplePosition(mailboxes[i].transform.position, out var end, 2, NavMesh.AllAreas) ||
                !NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) ||
                path.status != NavMeshPathStatus.PathComplete) continue;
            var corners = path.corners;
            float length = 0;
            for (int c = 1; c < corners.Length; c++) length += Vector3.Distance(corners[c - 1], corners[c]);
            if (length >= shortest) continue;
            shortest = length; botTarget[0] = i; explorationRoute = corners; explorationCorner = 0;
        }
        return botTarget[0] >= 0;
    }

    Vector3 ExplorationRivalDirection()
    {
        if (explorationRoute == null) return Vector3.zero;
        while (explorationCorner < explorationRoute.Length)
        {
            var direction = explorationRoute[explorationCorner] - bots[0].transform.position;
            direction.y = 0;
            if (direction.sqrMagnitude > .16f) return direction.normalized;
            explorationCorner++;
        }
        return Vector3.zero;
    }

    void OnDestroy()
    {
        netLeaving = true;
        if (netManager) { netManager.Shutdown(); Destroy(netManager.gameObject); }
        ClearNetPlungers();
        DestroyRunningShoesFX();
        DestroyBubbleWrapFX();
        DestroyFountainPenFX();
        DestroySlimeFX();
        DestroyIceCreamFX();
        FinishToyHammerSwing();
        DestroyStunFX();
        FinishPlungerThrow();
        DestroyHeldPresentation();
        if (explorationNavigationInstance.valid) explorationNavigationInstance.Remove();
    }
}
