using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Reproduces the repeated FirstPerson call made by PoseNetworkPlayers.
public static class GloveRotationCheck
{
    const string Folder = "Docs/ChangeRecords/20261017/";
    [MenuItem("Tools/Gameplay/Gloves/Record Rotation Before")]
    public static void Before() => Run(false);
    [MenuItem("Tools/Gameplay/Gloves/Verify Stable Rotation")]
    public static void Verify() => Run(true);

    [MenuItem("Tools/Gameplay/Gloves/Verify Rotation In Play")]
    public static void VerifyPlay()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Play Mode required.");
        UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>().StartCoroutine(Play());
    }

    static System.Collections.IEnumerator Play()
    {
        var game = UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>();
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var type = game.GetType();
        void Set(string n, object v) => type.GetField(n, flags).SetValue(game,v);
        object Get(string n) => type.GetField(n, flags).GetValue(game);
        void Call(string n, params object[] args) => type.GetMethod(n, flags).Invoke(game,args);
        var log = new List<string> {DateTimeOffset.Now.ToString("O"),"SampleScene Play Mode: simulated connected local player; real PoseNetworkPlayers per frame. No remote connection."};
        bool background = Application.runInBackground;
        try
        {
            Application.runInBackground=true; EditorApplication.update+=EditorApplication.QueuePlayerLoopUpdate;
            game.enabled=false; game.NetworkScreens=false; game.thirdPersonPreview=false; Set("started",true); Set("finished",false);
            var players = new MonsterDeliveryPrototype.NetPlayer[4];
            players[0]=new MonsterDeliveryPrototype.NetPlayer {connected=true,name="장갑 검수",color=0,selected=0};
            Set("netPlayers",players); Set("netOwn",0); Set("<RoomState>k__BackingField",MonsterDeliveryPrototype.RoomPhase.Playing);
            foreach (var kind in new[]{WeaponKind.BoxingGloves,WeaponKind.WorkGloves})
            {
                for(int slot=0;slot<3;slot++)Call("ClearSlot",slot);
                Call("AddWeapon",kind); players[0].items=new[]{(int)kind,0,0};
                Call("PositionCamera"); Call("UpdateHeldWeapon");
                var item=(DeliveryHeldItem)Get("heldItem"); var hands=item.GetComponentsInChildren<Transform>().Where(t=>t.name=="Hand_R"||t.name=="Hand_L").ToArray();
                var initial=hands.Select(t=>t.localRotation).ToArray(); float max=0;
                for(int frame=0;frame<180;frame++)
                {
                    game.NetworkScreens=true; Call("PoseNetworkPlayers"); game.NetworkScreens=false;
                    for(int h=0;h<hands.Length;h++)max=Mathf.Max(max,Quaternion.Angle(initial[h],hands[h].localRotation));
                    yield return null;
                }
                if(max>=.1f)throw new Exception(kind+" Play rotation drift="+max);
                log.Add("PASS "+kind+" actual PoseNetworkPlayers across 180 rendered frames, max drift="+max.ToString("F5")+"deg");
                yield return new WaitForEndOfFrame();
                var image=ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Folder+"images/20261018_"+kind+"_play_after.png",image.EncodeToPNG()); UnityEngine.Object.Destroy(image);
                game.thirdPersonPreview=true;Call("UpdateHeldWeapon"); game.thirdPersonPreview=false;Call("UpdateHeldWeapon");
                log.Add("PASS "+kind+" third-person / first-person switch recreated presentation without exception");
            }
            File.WriteAllLines(Folder+"evidence/rotation-play.txt",log); Debug.Log("GLOVE_ROTATION_PLAY_OK: "+string.Join("\n",log));
        }
        finally { Application.runInBackground=background; EditorApplication.update-=EditorApplication.QueuePlayerLoopUpdate; game.NetworkScreens=false; game.enabled=true; }
    }

    static void Run(bool verify)
    {
        Directory.CreateDirectory(Folder + "evidence");
        Directory.CreateDirectory(Folder + "images");
        var log = new List<string> { DateTimeOffset.Now.ToString("O"), "Unity " + Application.unityVersion, "Native Editor prefab reproduction; repeated calls, not an online match." };
        int passed = 0;
        void Check(bool ok, string message) { log.Add((ok ? "PASS " : "FAIL ") + message); if (ok) passed++; }
        var scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Glove Rotation Review");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
        var camera = root.AddComponent<Camera>(); camera.enabled = false; camera.scene = scene;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f,.17f,.22f);
        camera.nearClipPlane = .01f; camera.farClipPlane = 5; camera.fieldOfView = 60;
        var light = new GameObject("Review Light").AddComponent<Light>(); light.transform.SetParent(root.transform, false);
        light.type = LightType.Point; light.range = 5; light.intensity = 2;
        try
        {
            foreach (string kind in new[] { "BoxingGloves", "WorkGloves" })
            {
                var item = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("DeliveryItems/" + kind), root.transform, false).GetComponent<DeliveryHeldItem>();
                try
                {
                    item.FirstPerson();
                    var hands = item.GetComponentsInChildren<Transform>().Where(t => t.name == "Hand_R" || t.name == "Hand_L").ToArray();
                    var positions = hands.Select(t => root.transform.InverseTransformPoint(t.position)).ToArray();
                    var rotations = hands.Select(t => Quaternion.Inverse(root.transform.rotation) * t.rotation).ToArray();
                    string stage = verify ? "after" : "before";
                    Capture(camera, kind + "_" + stage + "_initial");
                    float drift = 0, displacement = 0;
                    for (int frame = 0; frame < 600; frame++)
                    {
                        item.FirstPerson();
                        for (int h = 0; h < hands.Length; h++)
                        {
                            drift = Mathf.Max(drift, Quaternion.Angle(rotations[h], Quaternion.Inverse(root.transform.rotation) * hands[h].rotation));
                            displacement = Mathf.Max(displacement, Vector3.Distance(positions[h],root.transform.InverseTransformPoint(hands[h].position)));
                        }
                        if (frame == 11) Capture(camera, kind + "_" + stage + "_call12");
                    }
                    log.Add(kind + " 600 calls max drift=" + drift.ToString("F5") + "deg max displacement=" + displacement.ToString("F6") + "m");
                    if (!verify) continue;
                    Check(hands.Length == 2 && drift < .1f && displacement < .0001f, kind + " 600 calls preserve both guard poses");
                    root.transform.rotation = Quaternion.Euler(27,113,-9); item.FirstPerson();
                    Check(hands.Select((t,i) => Quaternion.Angle(rotations[i], Quaternion.Inverse(root.transform.rotation)*t.rotation) < .1f && Vector3.Distance(positions[i],root.transform.InverseTransformPoint(t.position)) < .0001f).All(x=>x), kind + " guard follows camera yaw/pitch/roll without drift");
                    foreach (bool right in new[] {true,false})
                    {
                        item.PushFirstPerson(1,right);
                        Check(hands.Select((t,i)=>Vector3.Distance(positions[i],root.transform.InverseTransformPoint(t.position)) > .5f).Count(x=>x)==1, kind + " only selected " + (right?"right":"left") + " glove extends");
                        item.FirstPerson();
                        Check(hands.Select((t,i)=>Vector3.Distance(positions[i],root.transform.InverseTransformPoint(t.position)) < .0001f && Quaternion.Angle(rotations[i],Quaternion.Inverse(root.transform.rotation)*t.rotation) < .1f).All(x=>x),kind + " repeated pose restores guard after punch");
                    }
                    root.transform.rotation = Quaternion.identity;
                    Capture(camera, kind + "_after_final");
                }
                finally { UnityEngine.Object.DestroyImmediate(item.gameObject); root.transform.rotation = Quaternion.identity; }
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
        log.Add("Passed=" + passed);
        File.WriteAllLines(Folder + "evidence/rotation-" + (verify ? "after" : "before") + ".txt", log);
        if (log.Any(s=>s.StartsWith("FAIL"))) throw new Exception("Glove rotation regression failed: " + string.Join("\n",log));
        Debug.Log("GLOVE_ROTATION_" + (verify ? "OK" : "BEFORE") + ": " + string.Join("\n",log));
    }

    static void Capture(Camera camera, string name)
    {
        var target = RenderTexture.GetTemporary(1280,720,24);
        var previous = RenderTexture.active;
        var image = new Texture2D(1280,720,TextureFormat.RGB24,false);
        try { camera.targetTexture=target; camera.Render(); RenderTexture.active=target; image.ReadPixels(new Rect(0,0,1280,720),0,0); image.Apply(); File.WriteAllBytes(Folder+"images/20261017_"+name+".png",image.EncodeToPNG()); }
        finally { camera.targetTexture=null; RenderTexture.active=previous; RenderTexture.ReleaseTemporary(target); UnityEngine.Object.DestroyImmediate(image); }
    }
}
