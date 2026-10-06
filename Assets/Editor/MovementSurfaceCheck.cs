using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Run in SampleScene Play Mode. Uses the real movement method and scene colliders.
public static class MovementSurfaceCheck
{
    [MenuItem("Tools/Level/Verify Jump Surfaces")]
    public static void Verify()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Run in Play Mode.");
        var game = UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var fields = typeof(MonsterDeliveryPrototype).GetFields(flags).Where(f => !f.IsInitOnly).ToArray();
        var values = fields.Select(f => f.GetValue(game)).ToArray();
        var player = (CharacterController)typeof(MonsterDeliveryPrototype).GetField("player", flags).GetValue(game);
        var vertical = typeof(MonsterDeliveryPrototype).GetField("vertical", flags);
        var update = typeof(MonsterDeliveryPrototype).GetMethod("UpdatePlayer", flags);
        var position = player.transform.position;
        var rotation = player.transform.rotation;
        float height = player.height; var center = player.center;
        var keyboard = InputSystem.AddDevice<Keyboard>();
        var report = new List<string>();
        try
        {
            var spots = new List<(string name, Vector3 point)>();
            spots.Add(("road", new Vector3(-10f, -.23f, 38.84f)));
            var vehicles = GameObject.Find("Layer_06_LowPolyTownProps").transform.Find("05_Parked_Vehicles");
            foreach (Transform vehicle in vehicles)
            {
                var c = vehicle.GetComponentInChildren<MeshCollider>();
                if (c.Raycast(new Ray(c.bounds.center + Vector3.up * 20, Vector3.down), out var hit, 40))
                    spots.Add((vehicle.name, hit.point));
            }
            foreach (Transform house in GameObject.Find("Layer_04_Houses").transform)
            {
                var c = house.GetComponentInChildren<MeshCollider>();
                var candidates = new List<Vector3>();
                // Cast below the roof to find actual low, upward-facing entrance steps.
                for (float x = c.bounds.min.x; x < c.bounds.max.x; x += .35f)
                for (float z = c.bounds.min.z; z < c.bounds.max.z; z += .35f)
                    if (c.Raycast(new Ray(new Vector3(x, 2.7f, z), Vector3.down), out var hit, 3)
                        && hit.point.y > .1f && hit.normal.y > .99f)
                        candidates.Add(hit.point);
                if (candidates.Count == 0) throw new Exception("No entrance step found: " + house.name);
                var front = candidates.OrderBy(p => p.y).ToArray();
                var low = front.Where(p => Mathf.Abs(p.y - front[0].y) < .03f).ToArray();
                spots.Add((house.name + " step", low[low.Length / 2]));
            }
            int failures = 0;
            foreach (var spot in spots)
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                player.enabled = false;
                player.transform.position = spot.point + Vector3.up * 1.04f;
                player.transform.rotation = Quaternion.identity;
                player.enabled = true; vertical.SetValue(game, 0f);
                Physics.SyncTransforms();
                for (int i = 0; i < 90; i++) update.Invoke(game, new object[] { keyboard });
                float baseY = player.transform.position.y, peak = baseY, previous = 0;
                string diagnostic = " grounded=" + player.isGrounded + " settled=" + player.transform.position.ToString("F2") + " velocity=" + vertical.GetValue(game);
                int jumps = 0;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                InputSystem.Update();
                if (!keyboard.spaceKey.isPressed) throw new Exception("Synthetic Space input was not applied.");
                diagnostic += " pressed=" + keyboard.spaceKey.isPressed;
                for (int i = 0; i < 240; i++)
                {
                    update.Invoke(game, new object[] { keyboard });
                    float velocity = (float)vertical.GetValue(game);
                    if (velocity > 0 && previous <= 0) jumps++;
                    previous = velocity; peak = Mathf.Max(peak, player.transform.position.y);
                }
                bool passed = peak - baseY > .65f && jumps >= 2;
                if (!passed) failures++;
                report.Add((passed ? "PASS " : "FAIL ") + spot.name + " at " + spot.point.ToString("F2") + " rise=" + (peak-baseY).ToString("F3") + " jumps=" + jumps + diagnostic);
            }
            Debug.Log("JUMP_SURFACES: " + (spots.Count - failures) + "/" + spots.Count + " passed; dt=" + Time.deltaTime + "\n" + string.Join("\n", report));
            if (failures > 0) throw new Exception(failures + " jump surface checks failed.");
        }
        finally
        {
            InputSystem.RemoveDevice(keyboard);
            for (int i = 0; i < fields.Length; i++) fields[i].SetValue(game, values[i]);
            player.enabled = false; player.transform.SetPositionAndRotation(position, rotation);
            player.height = height; player.center = center; player.enabled = true;
        }
    }
}
