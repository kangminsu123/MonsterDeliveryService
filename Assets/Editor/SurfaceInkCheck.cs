using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class SurfaceInkCheck
{
    const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
    [MenuItem("Tools/Gameplay/Fountain Pen/Verify Surface Ink")]
    public static void Verify()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Play Mode required.");
        EditorApplication.isPaused = false;
        UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>().StartCoroutine(Run());
    }

    static IEnumerator Run()
    {
        var game = UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>(); var type = game.GetType();
        object Get(string n) => type.GetField(n, Flags).GetValue(game);
        void Set(string n, object v) => type.GetField(n, Flags).SetValue(game, v);
        object Call(string n, params object[] a) => type.GetMethod(n, Flags).Invoke(game, a);
        var cam = (Camera)Get("cam"); var position = cam.transform.position; var rotation = cam.transform.rotation;
        var held = (GameObject)Get("heldWeapon"); bool heldVisible = held && held.activeSelf;
        var savedState = Get("playerItems"); var rivalDemo = Get("rivalBubblePlaytest");
        bool enabled = game.enabled, background = Application.runInBackground;
        var temporary = new List<GameObject>(); var marks = new List<SurfaceInkMark>(); var report = new List<string>();
        void Check(bool ok, string detail) { if (!ok) throw new Exception("SURFACE_INK: " + detail); report.Add("PASS " + detail); }
        GameObject Prop(PrimitiveType shape, Vector3 p, Vector3 scale)
        {
            var prop = GameObject.CreatePrimitive(shape); prop.transform.position = p; prop.transform.localScale = scale;
            temporary.Add(prop); Physics.SyncTransforms(); return prop;
        }
        IEnumerator Shoot(Vector3 from, Vector3 aim)
        {
            var shot = (BrawlShot)Call("LaunchFountainPen", from, aim.normalized, 48f, 0);
            float end = Time.time + 1.5f;
            while (shot && shot.gameObject.activeSelf && Time.time < end) yield return null;
            Check(!shot || !shot.gameObject.activeSelf, "real projectile ends on the struck surface");
        }
        SurfaceInkMark Mark(Collider surface)
        {
            var mark = surface.GetComponentsInChildren<SurfaceInkMark>().LastOrDefault();
            Check(mark && mark.Paint && mark.GetComponent<MeshFilter>().sharedMesh.triangles.Length > 0,
                "impact creates a visible surface footprint");
            marks.Add(mark); return mark;
        }
        bool Conforms(SurfaceInkMark mark, Collider collider)
        {
            var mesh = mark.GetComponent<MeshFilter>().sharedMesh; var vertices = mesh.vertices;
            return mesh.triangles.Distinct().All(i =>
                Vector3.Distance(collider.ClosestPoint(mark.transform.TransformPoint(vertices[i])),
                    mark.transform.TransformPoint(vertices[i])) < .02f);
        }
        try
        {
            Application.runInBackground = true; EditorApplication.update += EditorApplication.QueuePlayerLoopUpdate;
            yield return null; game.enabled = false; Set("rivalBubblePlaytest", false); if (held) held.SetActive(false);
            var wall = Prop(PrimitiveType.Cube, new Vector3(0, 150.7f, 5), new Vector3(3, 2, .1f)).GetComponent<Collider>();
            yield return Shoot(new Vector3(0, 150.7f, 0), Vector3.forward); var wallMark = Mark(wall);
            Check(Conforms(wallMark, wall) && !wallMark.GetComponentsInChildren<Collider>().Any(),
                "wall mark lies on the real face without adding a collider");
            Check(wallMark.Paint.sharedMaterial.GetColor("_EdgeColor") == Color.black &&
                wallMark.Paint.sharedMaterial.GetColor("_Color").r < .01f, "surface ink is black including its edge");
            yield return new WaitForSeconds(.6f);
            Check(wallMark && wallMark.Opacity == 1, "ink remains fully visible during the first second");
            yield return new WaitForSeconds(.85f);
            Check(wallMark && wallMark.Opacity > .1f && wallMark.Opacity < .8f, "ink fades gradually after the one-second hold");
            yield return new WaitForSeconds(.7f);
            Check(!wallMark, "finished stain and its generated mesh are removed after the fade");
            wall.gameObject.SetActive(false);

            var floor = Prop(PrimitiveType.Cube, new Vector3(0, 150, 5), new Vector3(3, .1f, 3)).GetComponent<Collider>();
            yield return Shoot(new Vector3(0, 153, 5), Vector3.down); var floorMark = Mark(floor);
            Check(Conforms(floorMark, floor), "downward projectile places ink on the floor surface");
            floor.gameObject.SetActive(false);

            var slope = Prop(PrimitiveType.Cube, new Vector3(0, 150, 5), new Vector3(3, .1f, 3));
            slope.transform.rotation = Quaternion.Euler(25, 15, 0); Physics.SyncTransforms();
            var normal = slope.transform.up;
            yield return Shoot(slope.transform.position + normal * 3, -normal);
            var slopeMark = Mark(slope.GetComponent<Collider>());
            Check(Conforms(slopeMark, slope.GetComponent<Collider>()), "ink fits a tilted surface with nonuniform scale");
            var markPosition = slopeMark.transform.position; slope.transform.position += Vector3.right;
            Check(Vector3.Distance(slopeMark.transform.position, markPosition + Vector3.right) < .001f,
                "stain remains attached when the struck prop moves"); slope.SetActive(false);

            var curved = Prop(PrimitiveType.Sphere, new Vector3(0, 150, 5), Vector3.one).GetComponent<Collider>();
            yield return Shoot(new Vector3(0, 150, 1), Vector3.forward); var curvedMark = Mark(curved);
            Check(Conforms(curvedMark, curved), "ink mesh follows a curved prop instead of floating in front of it");
            curved.gameObject.SetActive(false);

            int count = UnityEngine.Object.FindObjectsByType<SurfaceInkMark>().Length;
            var player = (CharacterController)Get("player");
            Set("playerItems", new DeliveryItemState { shieldUntil = Time.time + 8 });
            game.FountainPenImpact(player, player.transform.position, 1, Vector3.up);
            Check(UnityEngine.Object.FindObjectsByType<SurfaceInkMark>().Length == count &&
                ((DeliveryItemState)Get("playerItems")).visionBlockedUntil == 0,
                "shielded actor hit creates no environment stain and applies no screen ink");
            Set("playerItems", new DeliveryItemState());
            game.FountainPenImpact(player, player.transform.position, 1, Vector3.up);
            Check(UnityEngine.Object.FindObjectsByType<SurfaceInkMark>().Length == count,
                "unshielded actor keeps the existing actor ink effect without a surface decal");

            var fence = GameObject.Find("Layer_05_Yards").GetComponentsInChildren<BoxCollider>()
                .First(c => c.name == "FenceCollision" && Vector3.Distance(c.bounds.center, player.transform.position) > 10);
            var center = fence.bounds.center; RaycastHit face = default; bool found = false;
            foreach (var direction in new[] { Vector3.forward, Vector3.back, Vector3.right, Vector3.left })
                if (fence.Raycast(new Ray(center - direction * 50, direction), out face, 100)) { found = true; break; }
            Check(found, "authored map fence exposes a real impact face");
            cam.transform.SetPositionAndRotation(face.point + face.normal * 1.7f + Vector3.up * .15f, Quaternion.identity);
            cam.transform.LookAt(face.point);
            yield return Shoot(face.point + face.normal * 1.1f, -face.normal); var fenceMark = Mark(fence);
            yield return null; yield return new WaitForEndOfFrame(); Capture("art-review/fountain-pen-wall-ink-20261013.png");
            yield return new WaitForSeconds(1.4f);
            Check(fenceMark && fenceMark.Opacity < .8f, "authored wall capture reaches the fading phase");
            yield return null; yield return new WaitForEndOfFrame(); Capture("art-review/fountain-pen-wall-ink-fade-20261013.png");
            yield return new WaitForSeconds(.8f);

            var groundProbe = player.transform.position + player.transform.forward * 2 + Vector3.up * 5;
            Check(Physics.Raycast(groundProbe, Vector3.down, out var ground, 10, ~0, QueryTriggerInteraction.Ignore),
                "authored map provides a solid floor");
            cam.transform.position = ground.point + ground.normal * 2 + Vector3.back * 1.4f; cam.transform.LookAt(ground.point);
            yield return Shoot(ground.point + ground.normal * 1, -ground.normal);
            var groundMark = Mark(ground.collider);
            yield return null; yield return new WaitForEndOfFrame(); Capture("art-review/fountain-pen-ground-ink-20261013.png");
            var result = "SURFACE_INK: " + report.Count + " checks passed\n" + string.Join("\n", report);
            File.WriteAllText("art-review/surface-ink-check-20261013.txt", result); Debug.Log(result);
        }
        finally
        {
            foreach (var mark in marks) if (mark) UnityEngine.Object.Destroy(mark.gameObject);
            foreach (var prop in temporary) if (prop) UnityEngine.Object.Destroy(prop);
            foreach (var shot in UnityEngine.Object.FindObjectsByType<BrawlShot>().Where(s => s.owner == game))
                UnityEngine.Object.Destroy(shot.gameObject);
            Set("playerItems", savedState); Set("rivalBubblePlaytest", rivalDemo);
            cam.transform.SetPositionAndRotation(position, rotation); if (held) held.SetActive(heldVisible);
            Call("UpdateFountainInkFX"); Call("UpdateBubbleWrapFX"); game.enabled = enabled;
            Application.runInBackground = background; EditorApplication.update -= EditorApplication.QueuePlayerLoopUpdate;
        }
    }

    static void Capture(string path)
    {
        var texture = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.Destroy(texture);
    }
}
