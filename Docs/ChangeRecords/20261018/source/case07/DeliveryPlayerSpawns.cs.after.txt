using System;
using UnityEngine;

public sealed partial class MonsterDeliveryPrototype
{
    readonly Vector3[] roundSpawns = new Vector3[4];
    readonly int[] spawnOrder = { 0, 1, 2, 3 };
    Vector3 spawnCenter;

    void ChooseRoundSpawns()
    {
        var root = GameObject.Find("Layer_13_PlayerSpawns");
        if (!root || root.transform.childCount != 4)
            throw new InvalidOperationException("Expected four saved corner spawns. Run Tools/Gameplay/Map Layout/Build100 Spots50 Parcels and Corner Spawns.");
        spawnCenter = Vector3.zero;
        for (int i = 0; i < 4; i++) { spawnOrder[i] = i; spawnCenter += root.transform.GetChild(i).position * .25f; }
        for (int i = 0; i < 4; i++)
        {
            int other = UnityEngine.Random.Range(i, 4);
            (spawnOrder[i], spawnOrder[other]) = (spawnOrder[other], spawnOrder[i]);
            roundSpawns[i] = root.transform.GetChild(spawnOrder[i]).position;
        }
    }
    Quaternion SpawnRotation(int id)
    {
        var toward = spawnCenter - SpawnPosition(id); toward.y = 0;
        return Quaternion.LookRotation(toward);
    }
}
