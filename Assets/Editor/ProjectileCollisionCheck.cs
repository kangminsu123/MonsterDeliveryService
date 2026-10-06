using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

public static class ProjectileCollisionCheck
{
    static bool running;

    [MenuItem("Tools/Gameplay/Verify Projectile Collisions")]
    public static async void Verify()
    {
        if (running || !EditorApplication.isPlaying) throw new Exception("Run once in Play Mode.");
        running = true;
        var game = UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>();
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var cam = (Camera)typeof(MonsterDeliveryPrototype).GetField("cam", flags).GetValue(game);
        var fire = typeof(MonsterDeliveryPrototype).GetMethod("MakeShot", flags);
        var position = cam.transform.position; var rotation = cam.transform.rotation; bool enabled = game.enabled;
        bool background = Application.runInBackground; var simulation = Physics.simulationMode;
        var existing = UnityEngine.Object.FindObjectsByType<BrawlShot>();
        var report = new List<string>();
        int failures = 0;
        try
        {
            game.enabled = false;
            Application.runInBackground = true; Physics.simulationMode = SimulationMode.Script;
            var houses = GameObject.Find("Layer_04_Houses").transform;
            var yards = GameObject.Find("Layer_05_Yards").transform;
            var colliders = new List<Collider>();
            foreach (Transform house in houses) colliders.Add(house.GetComponentInChildren<MeshCollider>());
            foreach (Transform yard in yards) colliders.AddRange(yard.GetComponentsInChildren<BoxCollider>().Where(c => c.name == "FenceCollision"));
            Physics.SyncTransforms();
            foreach (var collider in colliders)
            {
                if (collider == null || !collider.enabled || collider.isTrigger) throw new Exception("Missing solid target collider.");
                RaycastHit face = default; Vector3 direction = default;
                foreach (var d in new[] { Vector3.forward, Vector3.back, Vector3.right, Vector3.left })
                {
                    var center = collider.bounds.center; center.y = collider.bounds.min.y + Mathf.Min(.7f, collider.bounds.size.y * .5f);
                    if (collider.Raycast(new Ray(center - d * 50, d), out face, 100)) { direction = d; break; }
                }
                if (direction == Vector3.zero) throw new Exception("No target face: " + collider.name);
                foreach (float gap in new[] { 5f, .3f })
                {
                    var origin = face.point - direction * gap;
                    // The scene may contain another fence in front; the first solid surface still must block the shot.
                    if (!Physics.Raycast(origin, direction, out var first, gap + .1f, ~0, QueryTriggerInteraction.Ignore))
                        throw new Exception("No solid surface in shot path: " + collider.name);
                    cam.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(direction));
                    var before = UnityEngine.Object.FindObjectsByType<BrawlShot>();
                    fire.Invoke(game, new object[] { direction, 180f, WeaponKind.PackingTape });
                    var shot = UnityEngine.Object.FindObjectsByType<BrawlShot>().FirstOrDefault(s => !before.Contains(s));
                    bool crossed = shot != null && Vector3.Dot(shot.transform.position - first.point, direction) > .15f;
                    // Explicit PhysX steps also run when the Editor Game View is unfocused.
                    for (int step = 0; step < 30 && shot != null && shot.gameObject.activeSelf; step++)
                    {
                        typeof(BrawlShot).GetMethod("FixedUpdate", flags)?.Invoke(shot, null);
                        Physics.Simulate(Time.fixedDeltaTime);
                        if (shot != null && shot.gameObject.activeSelf && Vector3.Dot(shot.transform.position - first.point, direction) > .15f) crossed = true;
                        await Task.Delay(10);
                    }
                    await Task.Delay(80);
                    bool passed = shot == null && !crossed;
                    if (!passed) failures++;
                    report.Add((passed ? "PASS " : "FAIL ") + collider.transform.parent.name + "/" + collider.name + " gap=" + gap + " first=" + first.collider.name + " crossed=" + crossed + (shot != null ? " remaining=" + shot.transform.position.ToString("F2") : " destroyed"));
                    if (shot != null) UnityEngine.Object.DestroyImmediate(shot.gameObject);
                }
            }
            // A clear path and a trigger must not delete an otherwise unobstructed shot.
            var trigger = new GameObject("Projectile Check Trigger");
            try
            {
                trigger.transform.position = new Vector3(0, 150, 4); var triggerCollider = trigger.AddComponent<BoxCollider>(); triggerCollider.isTrigger = true;
                Physics.SyncTransforms(); cam.transform.SetPositionAndRotation(new Vector3(0, 150, 0), Quaternion.identity);
                var before = UnityEngine.Object.FindObjectsByType<BrawlShot>(); fire.Invoke(game, new object[] { Vector3.forward, 120f, WeaponKind.PrinterInk });
                var shot = UnityEngine.Object.FindObjectsByType<BrawlShot>().First(s => !before.Contains(s));
                for (int step = 0; step < 5; step++) { typeof(BrawlShot).GetMethod("FixedUpdate", flags)?.Invoke(shot, null); Physics.Simulate(Time.fixedDeltaTime); await Task.Delay(10); }
                bool passed = shot != null && shot.gameObject.activeSelf && shot.transform.position.z > 6;
                if (!passed) failures++;
                report.Add((passed ? "PASS " : "FAIL ") + "unobstructed thrown item passes through trigger and keeps moving");
                if (shot != null) UnityEngine.Object.DestroyImmediate(shot.gameObject);
            }
            finally { UnityEngine.Object.DestroyImmediate(trigger); }
            var result = "PROJECTILE_COLLISIONS: " + (report.Count - failures) + "/" + report.Count + " passed\n" + string.Join("\n", report);
            Debug.Log(result);
            System.IO.File.WriteAllText("art-review/item-projectile-collisions-20261006.txt", result);
            if (failures > 0) Debug.LogError("PROJECTILE_COLLISIONS: " + failures + " shots passed through solid geometry.");
        }
        catch (Exception e) { Debug.LogException(e); }
        finally
        {
            foreach (var shot in UnityEngine.Object.FindObjectsByType<BrawlShot>().Where(s => !existing.Contains(s))) UnityEngine.Object.DestroyImmediate(shot.gameObject);
            if (cam != null) cam.transform.SetPositionAndRotation(position, rotation);
            if (game != null) game.enabled = enabled;
            Physics.simulationMode = simulation; Application.runInBackground = background;
            running = false;
        }
    }
}
