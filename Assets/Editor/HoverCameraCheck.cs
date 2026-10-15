using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class HoverCameraCheck
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    [MenuItem("Tools/Gameplay/Delivery Robot/Verify Hover Camera")]
    public static void Verify()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Run in Play Mode.");
        var game = UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>();
        var player = GameObject.Find("Player").GetComponent<CharacterController>();
        var visual = player.GetComponentInChildren<DeliveryRobotVisual>();
        var animator = visual.model.GetComponent<Animator>();
        var camera = Camera.main;
        var pos = player.transform.position; var rot = player.transform.rotation;
        var cp = camera.transform.position; var cr = camera.transform.rotation;
        bool preview = game.thirdPersonPreview;
        var crouchField = typeof(MonsterDeliveryPrototype).GetField("crouching", Flags);
        var crouch = crouchField.GetValue(game);
        var state = animator.GetCurrentAnimatorStateInfo(0);
        GameObject wall = null, target = null;
        var report = new List<string>();
        void Check(bool ok, string detail) { if (!ok) throw new Exception("HOVER_CAMERA: " + detail); report.Add("PASS " + detail); }
        void Position() => typeof(MonsterDeliveryPrototype).GetMethod("PositionCamera", Flags).Invoke(game, null);
        Vector3[] Points(SkinnedMeshRenderer skin)
        {
            var mesh = new Mesh();
            try { skin.BakeMesh(mesh); return mesh.vertices.Select(v => skin.transform.TransformPoint(v)).ToArray(); }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }
        try
        {
            Check(Mathf.Abs(player.height - 1.4f) < .001f && Mathf.Abs(player.radius - .55f) < .001f &&
                Mathf.Abs(player.center.y - .7f) < .001f, "collision capsule covers the hovering robot body only");
            var rival = GameObject.Find("Rival 1").GetComponentInChildren<DeliveryRobotVisual>();
            Check(Mathf.Abs(rival.transform.localPosition.y - visual.transform.localPosition.y) < .001f,
                "local player and red rival share the increased hover offset");
            player.enabled = false; player.transform.SetPositionAndRotation(new Vector3(0, 100, 0), Quaternion.identity); player.enabled = true;
            var skins = visual.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var clip in new[] { "IDLE", "WALK", "IDLE_ARMED", "WALK_ARMED" })
            {
                float min = float.MaxValue, max = float.MinValue;
                for (int i = 0; i < 16; i++)
                {
                    animator.Play(clip, 0, i / 16f); animator.Update(0);
                    float clearance = skins.SelectMany(Points).Min(v => v.y) - (player.transform.position.y - 1);
                    min = Mathf.Min(min, clearance); max = Mathf.Max(max, clearance);
                }
                Check(min > .95f && max < 1.04f, clip + " evaluated mesh clearance " + min.ToString("F3") + "–" + max.ToString("F3") + "m");
            }
            animator.Play("IDLE", 0, 0); animator.Update(0);
            game.thirdPersonPreview = false; crouchField.SetValue(game, false); camera.transform.rotation = Quaternion.identity; Position();
            var eyes = skins.Where(r => r.name.StartsWith("HERO_Eye_")).SelectMany(Points).ToArray();
            float eyeY = (eyes.Min(v=>v.y) + eyes.Max(v=>v.y)) * .5f;
            Check(Mathf.Abs(camera.transform.position.y - eyeY) < .002f &&
                Mathf.Abs(camera.transform.position.x - player.transform.position.x) < .002f &&
                Mathf.Abs(camera.transform.position.z - eyes.Max(v=>v.z) - .01f) < .002f,
                "camera matches actual eye center height and sits 1cm in front of eye surfaces");
            Check(camera.transform.position.y < Points(skins.Single(r=>r.name=="HERO_Cap")).Min(v=>v.y),
                "first-person viewpoint is below the hat");
            var stable = camera.transform.position;
            foreach (var t in new[] { .25f, .75f }) { animator.Play("IDLE",0,t); animator.Update(0); Position(); }
            Check(Vector3.Distance(stable,camera.transform.position) < .001f, "idle hover does not bob the first-person camera");
            Check(skins.All(r=>r.shadowCastingMode == ShadowCastingMode.ShadowsOnly) &&
                rival.GetComponentsInChildren<Renderer>().All(r=>r.shadowCastingMode == ShadowCastingMode.On),
                "local model cannot cover the first-person view; rival remains visible and local shadows remain");
            crouchField.SetValue(game,true); Position();
            Check(Mathf.Abs(camera.transform.position.y - stable.y + .6f) < .001f, "crouching lowers the eye camera by 60cm");
            crouchField.SetValue(game,false); Position();
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = player.transform.position + new Vector3(0, stable.y-player.transform.position.y,.42f);
            wall.transform.localScale = new Vector3(2,2,.04f);
            Physics.SyncTransforms(); Position();
            Check(camera.transform.position.z < .35f && !wall.GetComponent<Collider>().bounds.Contains(camera.transform.position),
                "wall pulls eye camera inside the capsule before clipping");
            UnityEngine.Object.DestroyImmediate(wall); wall=null; Position();
            target = new GameObject("Hover Camera Interaction Check");
            target.transform.position = camera.transform.position + Vector3.forward*2;
            target.AddComponent<BoxCollider>().size = Vector3.one*.4f;
            target.AddComponent<InteractionTarget>();
            Physics.SyncTransforms();
            var ray = typeof(MonsterDeliveryPrototype).GetMethod("InteractionRayHit", Flags);
            object[] args = { camera.ViewportPointToRay(new Vector3(.5f,.5f,0)), 3f, default(RaycastHit) };
            Check((bool)ray.Invoke(game,args) && ((RaycastHit)args[2]).collider.gameObject == target,
                "eye-front crosshair ray reaches nearby interaction target");
            UnityEngine.Object.DestroyImmediate(target); target=null;
            args = new object[] { new Ray(player.transform.position-Vector3.forward*2,Vector3.forward), 4f, default(RaycastHit) };
            Check(!(bool)ray.Invoke(game,args), "first-person interaction ray ignores own capsule");
            game.thirdPersonPreview=true; Position();
            Check(skins.All(r=>r.shadowCastingMode==ShadowCastingMode.On), "third-person review restores visible local robot");
            File.WriteAllLines("art-review/hover-eye-camera-100cm-check-20261008.txt",report);
            Debug.Log("HOVER_CAMERA_OK: "+report.Count+" checks passed.\n"+string.Join("\n",report));
        }
        finally
        {
            if(wall) UnityEngine.Object.DestroyImmediate(wall);
            if(target) UnityEngine.Object.DestroyImmediate(target);
            game.thirdPersonPreview=preview; crouchField.SetValue(game,crouch);
            player.enabled=false; player.transform.SetPositionAndRotation(pos,rot); player.enabled=true;
            visual.SetFirstPerson(!preview);
            animator.Play(state.fullPathHash,0,state.normalizedTime); animator.Update(0);
            camera.transform.SetPositionAndRotation(cp,cr);
        }
    }
    [MenuItem("Tools/Gameplay/Delivery Robot/Preview Raised Hover")]
    public static void Preview() => View(true);
    [MenuItem("Tools/Gameplay/Delivery Robot/Restore Eye First Person")]
    public static void Restore() => View(false);
    static void View(bool preview)
    {
        if (!EditorApplication.isPlaying) throw new Exception("Run in Play Mode.");
        var game=UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>();
        game.thirdPersonPreview=preview;
        typeof(MonsterDeliveryPrototype).GetMethod("PositionCamera",Flags).Invoke(game,null);
        EditorApplication.QueuePlayerLoopUpdate();
    }
}
