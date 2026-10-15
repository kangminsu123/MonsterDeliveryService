using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public static class ExplorationRivalCheck
{
    [MenuItem("Tools/Gameplay/Verify Exploration Rival")]
    public static void Verify()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isPaused) throw new Exception("Run in fresh unpaused Play Mode.");
        var game = UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>();
        bool background = Application.runInBackground;
        Application.runInBackground = true;
        EditorApplication.update += EditorApplication.QueuePlayerLoopUpdate;
        game.StartCoroutine(Observe(game, background));
    }

    static IEnumerator Observe(MonsterDeliveryPrototype game, bool background)
    {
        var report = new List<string>();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        object Get(string name) => typeof(MonsterDeliveryPrototype).GetField(name, flags).GetValue(game);
        void Check(bool ok, string message) { if (!ok) throw new Exception("EXPLORATION_RIVAL: " + message); report.Add("PASS " + message); }
        try
        {
            var bots = (GameObject[])Get("bots");
            var owners = (int[])Get("owner");
            var boxes = (GameObject[])Get("mailboxes");
            var colors = (Color[])Get("colors");
            Check(game.mapExploreMode && (bool)Get("started"), "exploration remains active without a match timer");
            Check(bots.Count(b => b && b.activeInHierarchy) == 1 && bots[0], "exactly one active local test opponent");
            Check(bots[0].GetComponentInChildren<DeliveryRobotVisual>(), "opponent uses the delivery robot prefab");
            Check(colors[1].r > .9f && colors[1].g < .3f && colors[1].b < .3f, "opponent color is red");
            var robot = bots[0].GetComponentInChildren<DeliveryRobotVisual>();
            var gloves = bots[0].GetComponentInChildren<DeliveryHeldItem>();
            Check(gloves && gloves.paired && gloves.name == "TestBoxingGloves", "red test opponent equips paired boxing gloves");
            Check(gloves.GetComponentsInChildren<Collider>().Length == 0, "test gloves do not change movement or collision");
            var rightGlove = gloves.GetComponentsInChildren<Transform>().Single(t => t.name == "Hand_R");
            var leftGlove = gloves.GetComponentsInChildren<Transform>().Single(t => t.name == "Hand_L");
            var block = new MaterialPropertyBlock();
            int colored = 0;
            foreach (var renderer in robot.GetComponentsInChildren<Renderer>())
                for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                    if (renderer.sharedMaterials[i].name.StartsWith("MAT_PlayerColor"))
                    {
                        renderer.GetPropertyBlock(block, i);
                        Check(Vector4.Distance(block.GetColor("_Color"), colors[1]) < .01f, "hat and vest material receives opponent red");
                        colored++;
                    }
            Check(colored > 0, "colored hat/vest material slots exist");
            Check(Get("explorationNavigation") is NavMeshData, "baked native navigation loaded from Resources");
            var controller = bots[0].GetComponent<CharacterController>();
            Check(NavMesh.SamplePosition(bots[0].transform.position - Vector3.up, out var start, 3, NavMesh.AllAreas), "spawn is on a walkable surface");
            for (int i = 0; i < boxes.Length; i++)
            {
                var path = new NavMeshPath();
                Check(NavMesh.SamplePosition(boxes[i].transform.position, out var end, 2, NavMesh.AllAreas) &&
                    NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete,
                    "complete route to mailbox " + (i + 1));
            }
            var originalPosition = bots[0].transform.position;
            int initialScore = owners.Count(id => id == 1);
            Check(initialScore <= 8, "fresh session leaves at least two capturable mailboxes");
            var originalBodyColors = boxes.Select(b => b.transform.Find("Model").GetComponentInChildren<Renderer>().sharedMaterial.color).ToArray();
            float deadline = Time.realtimeSinceStartup + 60;
            bool walking = false, idle = false, armedWalking = false, armedIdle = false;
            float maxGloveSeparation = 0;
            while (owners.Count(id => id == 1) < initialScore + 2 && Time.realtimeSinceStartup < deadline)
            {
                var animator = robot.model.GetComponent<Animator>();
                walking |= animator.GetBool("Moving") && new Vector2(controller.velocity.x, controller.velocity.z).sqrMagnitude > .04f;
                var captures = (float[])Get("botCaptureStarted");
                idle |= captures[0] > 0 && !animator.GetBool("Moving");
                armedWalking |= animator.GetCurrentAnimatorStateInfo(0).IsName("WALK_ARMED");
                armedIdle |= captures[0] > 0 && animator.GetCurrentAnimatorStateInfo(0).IsName("IDLE_ARMED");
                maxGloveSeparation = Mathf.Max(maxGloveSeparation, Vector3.Distance(rightGlove.position, leftGlove.position));
                yield return null;
            }
            Check(Vector3.Distance(originalPosition,bots[0].transform.position) > 4, "opponent naturally travels through the actual level");
            Check(walking && idle, "WALK while moving and IDLE while capturing");
            Check(armedWalking && armedIdle, "armed walk while travelling and armed idle while capturing");
            Check(maxGloveSeparation < 1.3f, "paired gloves remain beside moving opponent throughout travel and capture; separation=" + maxGloveSeparation.ToString("F3") + "m");
            Check(robot.transform.InverseTransformPoint(rightGlove.position).x > 0 && robot.transform.InverseTransformPoint(leftGlove.position).x < 0, "gloves follow their physical right and left arms");
            Check(owners.Count(id => id == 1) >= initialScore + 2, "opponent naturally captures two distinct mailboxes");
            int owned = Array.FindIndex(owners,id => id == 1);
            Check(game.GetMailboxMapColor(owned) == colors[1], "minimap reads red ownership");
            foreach (var renderer in boxes[owned].transform.Find("CaptureBeacon").GetComponentsInChildren<Renderer>())
            {
                renderer.GetPropertyBlock(block);
                Check(Vector4.Distance(block.GetColor("_Color"),colors[1]) < .01f, "captured beacon renders opponent red");
            }
            Check(boxes.Select((b,i)=>b.transform.Find("Model").GetComponentInChildren<Renderer>().sharedMaterial.color == originalBodyColors[i]).All(same=>same),
                "mailbox body colors remain unchanged");
            Check(!(bool)Get("itemPlaytestEnabled") && ((WeaponKind[])Get("inventory")).All(item=>item == WeaponKind.None), "test opponent does not enable item attacks or grant player equipment");
            File.WriteAllLines("art-review/exploration-rival-gloves-check-20261008.txt", report);
            Debug.Log("EXPLORATION_RIVAL_OK: " + report.Count + " checks; natural captures=" + owners.Count(id=>id == 1));
        }
        finally
        {
            Application.runInBackground = background;
            EditorApplication.update -= EditorApplication.QueuePlayerLoopUpdate;
        }
    }
}
