using UnityEditor;
using UnityEngine;

public static class DeliveryNetworkCheck
{
    [MenuItem("Tools/Gameplay/Network/Verify Four Players")]
    public static void Verify()
    {
        if(!EditorApplication.isPlaying)throw new System.InvalidOperationException("Play Mode required");
        Object.FindAnyObjectByType<MonsterDeliveryPrototype>().BeginNetworkProof();
        EditorApplication.update+=EditorApplication.QueuePlayerLoopUpdate;
    }
    [MenuItem("Tools/Gameplay/Network/Verify Two Players")]
    public static void VerifyTwo()
    {
        if(!EditorApplication.isPlaying)throw new System.InvalidOperationException("Play Mode required");
        var game=Object.FindAnyObjectByType<MonsterDeliveryPrototype>();game.NetTeamSize=1;game.BeginNetworkProof();
        EditorApplication.update+=EditorApplication.QueuePlayerLoopUpdate;
    }
    [MenuItem("Tools/Gameplay/Network/Verify Room Lifecycle")]
    public static void VerifyLifecycle()
    {
        if(!EditorApplication.isPlaying)throw new System.InvalidOperationException("Play Mode required");
        var game=Object.FindAnyObjectByType<MonsterDeliveryPrototype>();game.StartCoroutine(game.VerifyRoomLifecycle());
        EditorApplication.update+=EditorApplication.QueuePlayerLoopUpdate;
    }
    [MenuItem("Tools/Gameplay/Network/Stop Proof Ticks")]
    public static void StopTicks()=>EditorApplication.update-=EditorApplication.QueuePlayerLoopUpdate;
}
