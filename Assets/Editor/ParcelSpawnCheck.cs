using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class ParcelSpawnCheck
{
    const string Folder = "Docs/ChangeRecords/20261018";
    [MenuItem("Tools/Gameplay/Test Parcels/Verify Respawn Pool")]
    public static void Verify()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Play Mode required.");
        EditorApplication.update -= EditorApplication.QueuePlayerLoopUpdate;
        EditorApplication.update += EditorApplication.QueuePlayerLoopUpdate;
        UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>().StartCoroutine(Run());
    }
    static IEnumerator Run()
    {
        yield return null;
        var game = UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>();
        var pool = UnityEngine.Object.FindAnyObjectByType<ParcelSpawnPool>();
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        void Set(string name, object value) => game.GetType().GetField(name, Flags).SetValue(game, value);
        var cam = Camera.main; var cp = cam.transform.position; var cr = cam.transform.rotation;
        var original = pool.CaptureSlots(); var random = UnityEngine.Random.state;
        var report = new List<string>(); ParcelSpawnReviewOverlay overlay = null;
        bool network = game.NetworkScreens, enabled = game.enabled; Camera heldCamera = null; bool heldCameraEnabled = false;
        void Check(bool value, string text) { if (!value) throw new Exception("PARCEL_RESPAWN: " + text); report.Add("PASS " + text); }
        bool Valid() => pool.ActiveCount == ParcelSpawnPool.LiveCount && pool.CaptureSlots().Distinct().Count() == ParcelSpawnPool.LiveCount && pool.CaptureSlots().All(s => s >= 0 && s < ParcelSpawnPool.SpawnCount);
        try
        {
            game.NetworkScreens = false; game.enabled = false;
            Check(pool.spawnPoints.Length == 100 && pool.parcels.Length == 50, "100 saved anchors and 50 pooled models");
            Check(Valid(), "round starts with 50 distinct occupied anchors and 50 empty anchors");
            var ids = pool.parcels.Select(p => p.GetEntityId()).ToArray();
            var used = new HashSet<int>(); var subsets = new HashSet<string>(); bool allRounds = true;
            UnityEngine.Random.InitState(20261016);
            for (int i = 0; i < 200; i++)
            {
                pool.ResetRound(); allRounds &= Valid(); var slots = pool.CaptureSlots();
                foreach (var slot in slots) used.Add(slot); subsets.Add(string.Join(",", slots.OrderBy(s => s)));
            }
            Check(allRounds, "200 new rounds keep 50 unique occupied anchors");
            Check(subsets.Count > 1 && used.Count == 100, "new rounds vary their subsets and exercise every saved anchor");
            bool cycles = true, duplicateGuard = true, relocated = true;
            for (int i = 0; i < 1000; i++)
            {
                int index = i % ParcelSpawnPool.LiveCount, old = pool.CaptureSlots()[index];
                cycles &= pool.parcels[index].TryCollect() && pool.ActiveCount == 49;
                duplicateGuard &= !pool.parcels[index].TryCollect(); pool.Refill();
                cycles &= Valid(); relocated &= pool.CaptureSlots()[index] != old;
            }
            Check(cycles, "1000 real TryCollect/refill cycles reduce to 49 then restore 50 without overlap");
            Check(duplicateGuard, "collected parcel cannot grant twice before refill");
            Check(relocated, "refill selects a different empty anchor instead of the consumed position");
            foreach (var p in pool.parcels.Take(5)) p.TryCollect(); Check(pool.ActiveCount == 45, "five pickups in one frame leave 45 parcels");
            pool.Refill(); Check(Valid(), "same-frame pickups refill to 50 unique anchors");
            foreach (var p in pool.parcels) p.TryCollect(); Check(pool.ActiveCount == 0, "all fifty simultaneous pickups are represented");
            pool.Refill(); Check(Valid(), "empty pool refills to fifty without duplicate anchors");
            Check(ids.SequenceEqual(pool.parcels.Select(p => p.GetEntityId())), "models are reused with no instantiate/destroy churn");
            var state = pool.CaptureSlots(); var invalid = (int[])state.Clone(); invalid[1] = invalid[0];
            Check(!pool.ApplySnapshot(invalid) && state.SequenceEqual(pool.CaptureSlots()), "duplicate network slots rejected atomically");
            invalid[1] = 100; Check(!pool.ApplySnapshot(invalid), "out-of-range network slot rejected");
            var boundary=Enumerable.Range(0,49).Concat(new[]{64}).ToArray();
            Check(pool.ApplySnapshot(boundary), "slots0 and64 coexist without a64-bit shift collision");
            invalid=(int[])boundary.Clone();invalid[0]=64;Check(!pool.ApplySnapshot(invalid), "duplicate upper-half slot rejected atomically");
            invalid=(int[])boundary.Clone();invalid[0]=-2;Check(!pool.ApplySnapshot(invalid), "invalid negative slot rejected");
            game.NetworkScreens = true;
            Check(!pool.CanCollect && !pool.parcels[0].TryCollect() && Valid(), "client/menu cannot locally collect or roll replacement positions");
            game.NetworkScreens = false;
            Check(pool.ApplySnapshot(original) && Valid(), "valid occupancy snapshot restores positions and availability");
            Check(pool.parcels.All(p => p.GetComponent<InteractionTarget>().available && p.GetComponentInChildren<BoxCollider>()), "all respawned boxes retain pickup colliders and availability");
            // Exercise the actual LateUpdate refill, rather than only calling the method.
            Set("started", true); Set("finished", false); game.enabled = true;
            int oldSlot = pool.CaptureSlots()[0]; pool.parcels[0].TryCollect();
            yield return null; yield return null;
            Check(Valid() && pool.CaptureSlots()[0] != oldSlot, "normal player-loop LateUpdate automatically replaces a pickup");
            game.enabled = false; Set("started", false);
            heldCamera = (Camera)game.GetType().GetField("heldCamera", Flags).GetValue(game); if(heldCamera){heldCameraEnabled=heldCamera.enabled;heldCamera.enabled=false;}
            overlay = new GameObject("ParcelSpawnReview").AddComponent<ParcelSpawnReviewOverlay>(); overlay.pool = pool; overlay.cam = cam;
            var area = GameObject.Find("Layer_00_GrassBase").GetComponent<Renderer>().bounds;
            cam.orthographic = true; cam.orthographicSize = Mathf.Max(area.size.z, area.size.x / cam.aspect) * .53f;
            cam.transform.SetPositionAndRotation(new Vector3(area.center.x, 150, area.center.z), Quaternion.Euler(90, 0, 0));
            overlay.title = "택배 50개 / 후보 100곳 · 풀밭 배치 없음";
            yield return Capture("20261018_parcels_100_points_50_live");
            overlay.oldSlot = pool.CaptureSlots()[0]; pool.parcels[0].TryCollect();
            Check(pool.ActiveCount == 49, "review pickup removes one box before replacement"); pool.Refill(); overlay.newSlot = pool.CaptureSlots()[0];
            overlay.title = "획득 후 빈 위치에 재생성 · 50개 유지";
            yield return Capture("20261018_parcels_after_refill");
            File.WriteAllLines(Folder + "/evidence/parcel-respawn-check.txt", report);
            Debug.Log("PARCEL_RESPAWN_OK: " + report.Count + " checks passed.");
        }
        finally
        {
            if (overlay) UnityEngine.Object.Destroy(overlay.gameObject);
            if(heldCamera)heldCamera.enabled=heldCameraEnabled;
            pool.ApplySnapshot(original); UnityEngine.Random.state = random;
            game.NetworkScreens = network; game.enabled = enabled;
            Set("started", false); Set("finished", false); cam.orthographic = false; cam.transform.SetPositionAndRotation(cp, cr);
            EditorApplication.update -= EditorApplication.QueuePlayerLoopUpdate;
        }
    }
    static IEnumerator Capture(string name)
    {
        yield return null; yield return new WaitForEndOfFrame();
        var image = ScreenCapture.CaptureScreenshotAsTexture(); Directory.CreateDirectory(Folder + "/images");
        File.WriteAllBytes(Folder + "/images/" + name + ".png", image.EncodeToPNG()); UnityEngine.Object.Destroy(image);
    }
}

public sealed class ParcelSpawnReviewOverlay : MonoBehaviour
{
    public ParcelSpawnPool pool; public Camera cam; public string title; public int oldSlot = -1, newSlot = -1;
    void OnGUI()
    {
        GUI.color = new Color(.025f, .04f, .07f, .94f); GUI.DrawTexture(new Rect(18, 18, 730, 104), Texture2D.whiteTexture); GUI.color = Color.white;
        DeliveryUIArt.Text(new Rect(34, 27, 700, 40), title, 25, Color.white);
        DeliveryUIArt.Text(new Rect(34, 72, 700, 32), "청록: 택배 있음   노랑: 빈 후보   빨강: 획득 위치   초록: 새 위치", 18, Color.white);
        var occupied = pool.CaptureSlots();
        for (int i = 0; i < pool.spawnPoints.Length; i++)
        {
            var p = cam.WorldToScreenPoint(pool.spawnPoints[i].position + Vector3.up * 1.5f); if (p.z < 0) continue;
            var color = i == newSlot ? Color.green : i == oldSlot ? new Color(1, .25f, .25f) : occupied.Contains(i) ? Color.cyan : Color.yellow;
            GUI.color = new Color(.02f, .025f, .035f, .9f); GUI.DrawTexture(new Rect(p.x - 16, Screen.height - p.y - 12, 32, 24), Texture2D.whiteTexture); GUI.color = Color.white;
            DeliveryUIArt.Text(new Rect(p.x - 16, Screen.height - p.y - 12, 32, 24), (i + 1).ToString("D2"), 16, color, TextAnchor.MiddleCenter);
        }
        var corners=GameObject.Find("Layer_13_PlayerSpawns");if(!corners)return;
        for(int i=0;i<corners.transform.childCount;i++)
        {
            var p=cam.WorldToScreenPoint(corners.transform.GetChild(i).position);
            DeliveryUIArt.Text(new Rect(p.x-65,Screen.height-p.y-20,130,40),"시작지역 "+(i+1),18,new Color(1,.35f,1),TextAnchor.MiddleCenter);
        }
    }
}

