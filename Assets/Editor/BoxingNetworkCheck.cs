using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class BoxingNetworkCheck
{
    const string Folder = "Docs/ChangeRecords/20261018/";
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    [MenuItem("Tools/Gameplay/Gloves/Record Network Punch Before")]
    public static void Before() => Start(false);
    [MenuItem("Tools/Gameplay/Gloves/Verify Network Punch")]
    public static void Verify() => Start(true);
    static void Start(bool verify)
    {
        if (!EditorApplication.isPlaying) throw new Exception("Fresh Play Mode required.");
        UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>().StartCoroutine(Run(verify));
    }
    static IEnumerator Run(bool verify)
    {
        var game = UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>(); var type = game.GetType();
        object Get(string n) => type.GetField(n, Flags).GetValue(game);
        void Set(string n, object v) => type.GetField(n, Flags).SetValue(game,v);
        object Call(string n, params object[] args) => type.GetMethod(n, Flags).Invoke(game,args);
        var log = new List<string> {DateTimeOffset.Now.ToString("O"),"SampleScene, controlled local network-state reproduction (not a remote connection)."};
        void Check(bool ok,string text) { log.Add((ok?"PASS ":"FAIL ")+text); }
        string stage=verify?"after":"before";
        var player=(CharacterController)Get("player"); var rival=((CharacterController[])Get("botControllers"))[0];
        var cam=(Camera)Get("cam");
        var players=new MonsterDeliveryPrototype.NetPlayer[4];
        players[0]=new MonsterDeliveryPrototype.NetPlayer {connected=true,items=new[]{1,8,0},selected=0,name="Punch review",color=0};
        players[1]=new MonsterDeliveryPrototype.NetPlayer {connected=true,items=new[]{1,8,0},selected=0,name="Opponent",color=1};
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Punch Check Floor";floor.transform.position=new Vector3(0,149.9f,0);floor.transform.localScale=new Vector3(50,.2f,50);
        void Place(CharacterController a,Vector3 p){a.enabled=false;a.transform.position=p;a.transform.rotation=Quaternion.identity;a.enabled=true;}
        bool enabled=game.enabled,background=Application.runInBackground;
        try
        {
            Directory.CreateDirectory(Folder+"images");Directory.CreateDirectory(Folder+"evidence");
            Application.runInBackground=true;EditorApplication.update+=EditorApplication.QueuePlayerLoopUpdate;
            game.enabled=false;game.NetworkScreens=true;game.thirdPersonPreview=false;Set("started",true);Set("finished",false);
            Set("netPlayers",players);Set("netOwn",0);Set("<RoomState>k__BackingField",MonsterDeliveryPrototype.RoomPhase.Playing);
            Place(player,new Vector3(0,151,0));Place(rival,new Vector3(0,151,3));rival.gameObject.SetActive(true);cam.transform.rotation=Quaternion.identity;
            ((MonsterDeliveryPrototype.NetInput[])Get("netInputs"))[0]=new MonsterDeliveryPrototype.NetInput();
            ((MonsterDeliveryPrototype.NetInput[])Get("netInputs"))[1]=new MonsterDeliveryPrototype.NetInput();
            Call("SyncHostInventory");Call("PositionCamera");Call("UpdateHeldWeapon");Call("PoseNetworkPlayers");
            var item=(DeliveryHeldItem)Get("heldItem");var hands=item.GetComponentsInChildren<Transform>().Where(t=>t.name=="Hand_R"||t.name=="Hand_L").ToArray();
            var guard=hands.Select(t=>cam.transform.InverseTransformPoint(t.position)).ToArray();
            var guardRot=hands.Select(t=>t.localRotation).ToArray();
            for(int swing=0;swing<(verify?4:1);swing++)
            {
                players[0].cooldown=0;Call("UseNetWeapon",0);
                Call("PoseNetworkPlayers");Check(((GameObject)Get("heldWeapon")).activeSelf,"swing "+swing+" gloves stay visible during windup");
                players[0].actionAt=Time.time-.16f;Call("PoseNetworkPlayers");
                float[] delta=hands.Select((t,h)=>cam.transform.InverseTransformPoint(t.position).z-guard[h].z).ToArray();
                log.Add("swing="+swing+" forward="+string.Join(",",delta.Select(x=>x.ToString("F4"))));
                bool right=swing%2==0;
                Check(hands.Select((t,h)=>(guard[h].x>0)==right?delta[h]>.53f:Mathf.Abs(delta[h])<.001f).All(x=>x),"swing "+swing+" extends only "+(right?"right":"left")+" hand");
                yield return new WaitForEndOfFrame();Capture(stage+"_"+(right?"right":"left")+"_local");
                players[0].actionAt=Time.time-.5f;Call("PoseNetworkPlayers");
                Check(hands.Select((t,h)=>Vector3.Distance(guard[h],cam.transform.InverseTransformPoint(t.position))<.001f&&Quaternion.Angle(guardRot[h],t.localRotation)<.1f).All(x=>x),"swing "+swing+" returns to guard");
            }
            if(verify)
            {
                Check(players[0].boxingPunches==4,"only four accepted glove swings advance hand alternation");
                int count=players[0].boxingPunches;float actionAt=players[0].actionAt;
                players[0].cooldown=Time.time+1;Call("UseNetWeapon",0);
                Check(players[0].boxingPunches==count&&players[0].actionAt==actionAt,"cooldown rejection does not change hand or restart animation");
                players[0].cooldown=0;((DeliveryItemState)Get("playerItems")).stunUntil=Time.time+1;Call("UseNetWeapon",0);
                Check(players[0].boxingPunches==count,"stunned input does not advance alternation");
                ((DeliveryItemState)Get("playerItems")).stunUntil=0;
                players[0].items[0]=3;Call("UseNetWeapon",0);players[0].items[0]=1;Call("PoseNetworkPlayers");
                Check(players[0].boxingPunches==count&&hands.Select((t,h)=>Vector3.Distance(guard[h],cam.transform.InverseTransformPoint(t.position))<.001f).All(x=>x),"switching from another item action does not invent a glove punch");
                players[0].cooldown=0;Call("UseNetWeapon",0);players[0].actionAt=Time.time-.16f;
                ((DeliveryItemState)Get("playerItems")).stunUntil=Time.time+1;Call("PoseNetworkPlayers");
                Check(hands.Select((t,h)=>Vector3.Distance(guard[h],cam.transform.InverseTransformPoint(t.position))<.001f).All(x=>x),"stun interrupts visible punch");
                ((DeliveryItemState)Get("playerItems")).stunUntil=0;players[0].dashRemaining=.2f;Call("PoseNetworkPlayers");
                Check(hands.Select((t,h)=>Vector3.Distance(guard[h],cam.transform.InverseTransformPoint(t.position))<.001f).All(x=>x),"dash interrupts visible punch");players[0].dashRemaining=0;
                players[0].actionAt=Time.time-1;
                for(int frame=0;frame<600;frame++)Call("PoseNetworkPlayers");
                Check(hands.Select((t,h)=>Quaternion.Angle(guardRot[h],t.localRotation)<.1f).All(x=>x),"600 idle network poses retain rotation fix");
                var visual=((DeliveryRobotVisual[])Get("botVisuals"))[0];var animator=visual.model.GetComponent<Animator>();
                var arms=visual.model.GetComponentsInChildren<Transform>().Where(t=>t.name=="Arm.R"||t.name=="Arm.L").ToArray();
                rival.transform.rotation=Quaternion.Euler(0,180,0);
                for(int swing=0;swing<2;swing++)
                {
                    animator.Play("IDLE_ARMED",0,0);animator.Update(0);
                    var armed=arms.Select(t=>t.rotation).ToArray();
                    players[1].boxingPunches=swing+1;players[1].actionKind=1;players[1].actionSlot=0;players[1].actionAt=Time.time-.16f;
                    Call("PoseNetworkPlayers");bool right=swing==0;
                    Check(arms.Select((t,i)=>(visual.transform.InverseTransformPoint(t.position).x>0)==right?Quaternion.Angle(armed[i],t.rotation)>20:Quaternion.Angle(armed[i],t.rotation)<.1f).All(x=>x),"remote "+(right?"right":"left")+" punch moves only matching arm");
                    var remote=((DeliveryHeldItem[])Get("netHeld"))[1];
                    var glove=remote.GetComponentsInChildren<Transform>().Single(t=>t.name=="Hand_R");
                    var rightArm=arms.OrderBy(t=>visual.transform.InverseTransformPoint(t.position).x).Last();
                    Check(Vector3.Distance(glove.position,rightArm.TransformPoint(remote.rightPosition))<.001f,"remote glove stays attached to animated arm");
                    yield return new WaitForEndOfFrame();Capture("after_"+(right?"right":"left")+"_opponent");
                }
                var copy=JsonUtility.FromJson<MonsterDeliveryPrototype.NetPlayer>(JsonUtility.ToJson(players[1]));
                Check(copy.boxingPunches==2&&copy.actionKind==1&&copy.actionSlot==0,"snapshot JSON preserves accepted hand and action metadata");
                // The actual client places its own global slot at local actor zero.
                players[1].actionAt=Time.time-.16f;Set("netOwn",1);Call("SyncHostInventory");Call("UpdateHeldWeapon");Call("PoseNetworkPlayers");
                item=(DeliveryHeldItem)Get("heldItem");
                hands=item.GetComponentsInChildren<Transform>().Where(t=>t.name=="Hand_R"||t.name=="Hand_L").ToArray();
                Check(hands.Where(t=>cam.transform.InverseTransformPoint(t.position).x<0).Any(t=>cam.transform.InverseTransformPoint(t.position).z>1.4f),"client global slot 1 displays left punch on its local first-person model");
            }
        }
        finally
        {
            File.WriteAllLines(Folder+"evidence/punch-"+stage+".txt",log);
            UnityEngine.Object.Destroy(floor);game.NetworkScreens=false;game.enabled=enabled;
            Application.runInBackground=background;EditorApplication.update-=EditorApplication.QueuePlayerLoopUpdate;
        }
        if(verify&&log.Any(s=>s.StartsWith("FAIL")))throw new Exception("BOXING_NETWORK_FAILED: "+string.Join("\n",log));
        Debug.Log("BOXING_NETWORK_"+(verify?"OK":"BEFORE")+": "+string.Join("\n",log));
    }
    static void Capture(string name)
    {
        var image=ScreenCapture.CaptureScreenshotAsTexture();
        try {File.WriteAllBytes(Folder+"images/20261018_punch_"+name+".png",image.EncodeToPNG());}
        finally {UnityEngine.Object.Destroy(image);}
    }

    [MenuItem("Tools/Gameplay/Gloves/Host Network Punch Review")]
    public static void HostReview() => StartReview(true);
    [MenuItem("Tools/Gameplay/Gloves/Join Network Punch Review")]
    public static void JoinReview() => StartReview(false);
    static void StartReview(bool host)
    {
        if(!EditorApplication.isPlaying)throw new Exception("Play Mode required");
        var game=UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>();
        game.StartCoroutine(ConnectedReview(game,host));
    }
    static IEnumerator ConnectedReview(MonsterDeliveryPrototype game,bool host)
    {
        var type=game.GetType();
        object Get(string n)=>type.GetField(n,Flags).GetValue(game);
        void Set(string n,object v)=>type.GetField(n,Flags).SetValue(game,v);
        void Call(string n,params object[] args)=>type.GetMethod(n,Flags).Invoke(game,args);
        // The clone's working directory is Library/VP/<id>; keep both peers' evidence at the real project root.
        string directory=Path.GetFullPath(Application.dataPath+"/..");
        if(!host)directory=Path.GetFullPath(directory+"/../../..");
        string record=Path.Combine(directory,Folder);Directory.CreateDirectory(record+"images");Directory.CreateDirectory(record+"evidence");
        string role=host?"host":"client";
        var rows=new List<string>{DateTimeOffset.Now.ToString("O"),"Two actual MPPM Editor processes, localhost NGO transport, scripted input commands."};
        bool background=Application.runInBackground;
        GameObject floor=null;
        try
        {
            Application.runInBackground=true;EditorApplication.update+=EditorApplication.QueuePlayerLoopUpdate;
            game.NetworkScreens=true;game.NetTeamSize=1;
            if(host)game.HostRoom();else game.JoinRoom("127.0.0.1");
            float deadline=Time.realtimeSinceStartup+45;
            while(!game.NetPlaying&&Time.realtimeSinceStartup<deadline)yield return null;
            if(!game.NetPlaying)throw new Exception("Punch review connection timeout");
            var input=(MonsterDeliveryPrototype.NetInput)Get("netLocalInput");input.slot=0;input.yaw=host?0:180;input.pitch=0;
            floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Connected Punch Review Floor";floor.transform.position=new Vector3(0,149.9f,0);floor.transform.localScale=new Vector3(50,.2f,50);
            if(host)
            {
                var players=(MonsterDeliveryPrototype.NetPlayer[])Get("netPlayers");
                for(int g=0;g<2;g++)
                {
                    players[g].items=new[]{1,8,0};players[g].selected=0;
                    var actor=(CharacterController)CallValue(game,"ActorController",g);
                    actor.enabled=false;actor.transform.position=new Vector3(g==0?0:.9f,151,g==0?0:3);actor.enabled=true;
                    players[g].vertical=0;
                }
                Call("NetBroadcast");
            }
            float ready=Time.realtimeSinceStartup;
            while(Time.realtimeSinceStartup-ready<.7f)yield return null;
            int own=(int)Get("netOwn"),other=own==0?1:0;
            var item=(DeliveryHeldItem)Get("heldItem");
            if(!item)throw new Exception("Gloves missing after authoritative equipment snapshot");
            var hands=item.GetComponentsInChildren<Transform>().Where(t=>t.name=="Hand_R"||t.name=="Hand_L").ToArray();
            var cam=(Camera)Get("cam");var guard=hands.Select(t=>cam.transform.InverseTransformPoint(t.position)).ToArray();
            var max=new float[2];var captured=new HashSet<string>();int sent=0;float began=Time.realtimeSinceStartup;
            while(Time.realtimeSinceStartup-began<5)
            {
                float elapsed=Time.realtimeSinceStartup-began;
                if(sent<2&&elapsed>(host?.4f:.8f)+sent*1.2f){((MonsterDeliveryPrototype.NetInput)Get("netLocalInput")).use++;sent++;}
                var players=(MonsterDeliveryPrototype.NetPlayer[])Get("netPlayers");
                if(players[own].boxingPunches>0)
                {
                    bool right=(players[own].boxingPunches&1)!=0;
                    float delta=hands.Where((t,h)=>(guard[h].x>0)==right).Select((t,h)=>cam.transform.InverseTransformPoint(t.position).z-(right?.92f:.955f)).Max();
                    max[right?0:1]=Mathf.Max(max[right?0:1],delta);
                }
                foreach(int g in new[]{own,other})
                {
                    var p=players[g];float age=Time.time-p.actionAt;
                    string label=role+"_"+(g==own?"local":"opponent")+"_"+p.boxingPunches;
                    if(p.boxingPunches>0&&age>.155f&&age<.28f&&captured.Add(label))
                    {
                        rows.Add("CAPTURE "+label+" accepted="+p.boxingPunches+" right="+((p.boxingPunches&1)!=0)+" age="+age.ToString("F4"));
                        yield return new WaitForEndOfFrame();
                        var image=ScreenCapture.CaptureScreenshotAsTexture();
                        File.WriteAllBytes(record+"images/20261018_punch_connected_"+label+".png",image.EncodeToPNG());UnityEngine.Object.Destroy(image);
                    }
                }
                yield return null;
            }
            var final=(MonsterDeliveryPrototype.NetPlayer[])Get("netPlayers");
            rows.Add((final[0].boxingPunches==2&&final[1].boxingPunches==2?"PASS ":"FAIL ")+role+" receives two accepted punches from both peers: "+final[0].boxingPunches+","+final[1].boxingPunches);
            rows.Add((max[0]>.3f&&max[1]>.3f?"PASS ":"FAIL ")+role+" visible local right/left forward maximum="+max[0].ToString("F4")+","+max[1].ToString("F4")+"m");
            rows.Add((captured.Contains(role+"_opponent_1")&&captured.Contains(role+"_opponent_2")?"PASS ":"FAIL ")+role+" captures both remote alternating punches");
            File.WriteAllLines(record+"evidence/punch-connected-"+role+".txt",rows);
            Debug.Log("BOXING_CONNECTED_"+role+": "+string.Join("\n",rows));
        }
        finally {Application.runInBackground=background;EditorApplication.update-=EditorApplication.QueuePlayerLoopUpdate;}
    }
    static object CallValue(MonsterDeliveryPrototype game,string method,params object[] args)=>game.GetType().GetMethod(method,Flags).Invoke(game,args);
}
