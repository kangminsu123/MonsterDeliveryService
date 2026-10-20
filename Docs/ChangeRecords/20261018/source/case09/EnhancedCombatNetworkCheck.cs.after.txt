using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class EnhancedCombatNetworkCheck
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly bool Client=Environment.GetCommandLineArgs().Contains("-delivery-enhanced-client");
    static EnhancedCombatNetworkCheck()
    {
        if(Client)EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredPlayMode)Start(false);};
    }
    public static void BootstrapClient()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");EditorApplication.delayCall+=()=>EditorApplication.isPlaying=true;
    }
    [MenuItem("Tools/Gameplay/Combat/Verify Connected Host")]
    public static void Host()=>Start(true);
    static void Start(bool host)
    {
        if(!EditorApplication.isPlaying)throw new Exception("Fresh Play required");
        var game=UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>();game.StartCoroutine(Run(game,host));
    }
    static IEnumerator Run(MonsterDeliveryPrototype game,bool host)
    {
        var t=game.GetType();object Get(string n)=>t.GetField(n,Flags).GetValue(game);
        void Set(string n,object v)=>t.GetField(n,Flags).SetValue(game,v);
        object Call(string n,params object[] a)=>t.GetMethod(n,Flags).Invoke(game,a);
        string root=Path.GetFullPath(Application.dataPath+"/..");if(!host)root=Path.GetFullPath(root+"/../../..");
        string folder=Path.Combine(root,"Docs/ChangeRecords/20261018"),role=host?"host":"client";
        Directory.CreateDirectory(folder+"/evidence/enhanced-connected");
        var log=new List<string>{DateTimeOffset.Now.ToString("O"),"Actual separate Unity Editor processes over localhost,60ms artificial one-way delay per outgoing frame/input and20 percent intentional drops. Controlled equipment, positions and inputs; not physical WAN."};
        var csv=new List<string>{"age,phase,host_y,guest_z,host_frozen,guest_stun,guest_slime_uses,plunger_distance,shot_speed"};
        bool background=Application.runInBackground;GameObject floor=null;
        bool sawPlayers=false,sawSlimeThree=false,sawSlimeZero=false,shieldBlocked=false,plungerPulled=false;
        float highest=151,hammer=0,ice=0,maxPlunger=0,maxIceSpeed=0;int phase=-1;var fired=new HashSet<int>();
        var errorMessages=new List<string>();Application.LogCallback errors=(text,stack,type)=>{if(type==LogType.Exception||type==LogType.Error||type==LogType.Assert)errorMessages.Add(text);};
        Application.logMessageReceived+=errors;
        try
        {
            Application.runInBackground=true;EditorApplication.update+=EditorApplication.QueuePlayerLoopUpdate;
            game.NetworkScreens=true;game.NetTeamSize=1;Set("latencyProof",true);Set("latencySaved",true);Set("latencyLabel","enhanced-loss");
            if(host)game.HostRoom();else game.JoinRoom("127.0.0.1");
            float deadline=Time.realtimeSinceStartup+60;
            while(!game.NetPlaying&&Time.realtimeSinceStartup<deadline)yield return null;
            if(!game.NetPlaying)throw new Exception("Connected enhanced combat timed out");
            floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Connected Enhanced Combat Floor";floor.transform.position=new Vector3(0,149.9f,0);floor.transform.localScale=new Vector3(160,.2f,160);
            float began=Time.time;
            while(Time.time-began<40)
            {
                float age=Time.time-began;int stage=age<4?0:age<12?1:age<19?2:age<26?3:age<33?4:5;
                var players=(MonsterDeliveryPrototype.NetPlayer[])Get("netPlayers");int own=(int)Get("netOwn");
                var input=(MonsterDeliveryPrototype.NetInput)Get("netLocalInput");input.slot=0;input.yaw=host?0:180;input.pitch=stage==2||stage==5?3:0;
                sawPlayers|=players.Count(p=>p!=null&&p.connected)==2;
                if(host&&phase!=stage)
                {
                    phase=stage;Call("ClearNetPlungers");Call("ClearActiveNetShots");Call("ClearSlimePuddles");Call("ClearIceCreamMarks");
                    int kind=stage==0?1:stage==1?3:stage==3?2:stage==4?4:5;
                    float distance=stage==3?25:stage==2?8:3;
                    for(int g=0;g<2;g++)
                    {
                        var actor=(CharacterController)Call("ActorController",g);actor.enabled=false;actor.transform.SetPositionAndRotation(new Vector3(0,151,g*distance),Quaternion.Euler(0,g==0?0:180,0));actor.enabled=true;
                        var state=new DeliveryItemState{safePosition=actor.transform.position};if(stage==5&&g==0)state.shieldUntil=Time.time+8;
                        if(g==0)Set("playerItems",state);else ((DeliveryItemState[])Get("botItems"))[0]=state;
                        players[g].vertical=0;players[g].cooldown=0;players[g].actionKind=0;players[g].target=-1;players[g].selected=0;
                        Call("GiveNetItem",g,0,kind);
                    }
                    Call("NetBroadcast");
                }
                void Fire(int id,float at,bool onHost)
                {if(host==onHost&&age>=at&&!fired.Contains(id)){input.use++;fired.Add(id);}}
                Fire(0,1,false);Fire(1,5,true);Fire(2,13,false);Fire(3,20,true);
                Fire(4,27,false);Fire(5,28,false);Fire(6,29,false);Fire(7,34,false);
                if(stage==0)highest=Mathf.Max(highest,players[0].position.y);
                if(stage==1)hammer=Mathf.Max(hammer,players[1].stun);
                if(stage==2)ice=Mathf.Max(ice,players[0].frozen);
                if(stage==3&&age>21)plungerPulled|=players[1].position.z<5;
                if(stage==4){sawSlimeThree|=players[1].items[0]==4&&players[1].ammo[0]==3;sawSlimeZero|=age>29.5f&&players[1].ammo[0]==0;}
                if(stage==5&&age>34)shieldBlocked|=players[0].shield==0&&players[0].shieldBreak<1&&players[0].frozen==0;
                if(host)
                {
                    foreach(var p in (IEnumerable)Get("netPlungers")){var model=(GameObject)p.GetType().GetField("model").GetValue(p);if(stage==3&&model)maxPlunger=Mathf.Max(maxPlunger,model.transform.position.z);}
                    foreach(var s in UnityEngine.Object.FindObjectsByType<BrawlShot>(FindObjectsSortMode.None))if(s.kind==WeaponKind.IceCream&&s.gameObject.activeSelf)maxIceSpeed=Mathf.Max(maxIceSpeed,s.GetComponent<Rigidbody>().linearVelocity.magnitude);
                }
                else
                {
                    foreach(var model in ((Dictionary<string,GameObject>)Get("netShotViews")).Values)if(stage==3&&model)maxPlunger=Mathf.Max(maxPlunger,model.transform.position.z);
                }
                csv.Add(FormattableString.Invariant($"{age:F3},{stage},{players[0].position.y:F3},{players[1].position.z:F3},{players[0].frozen:F3},{players[1].stun:F3},{players[1].ammo[0]},{maxPlunger:F3},{maxIceSpeed:F3}"));
                if(stage==1&&age>5.6f&&!fired.Contains(100)){fired.Add(100);Capture(folder+"/images/20261018_enhanced_connected_"+role+"_hammer.png");}
                if(stage==2&&age>13.8f&&!fired.Contains(101)){fired.Add(101);Capture(folder+"/images/20261018_enhanced_connected_"+role+"_ice.png");}
                yield return null;
            }
            void Check(bool ok,string s)=>log.Add((ok?"PASS ":"FAIL ")+s);
            Check(sawPlayers,"two real peers connected with matching protocol7");
            Check(highest>155.5f,"client-origin boxing visibly lifts authoritative host beyond4.5m; peak="+highest);
            Check(hammer>4.3f,"host-origin hammer reaches participant with about5s stun; max="+hammer);
            Check(ice>2.4f,"client-origin ice reaches host and replicates about3s freeze; max="+ice);
            Check(maxPlunger>18,"30m plunger passes old15m range in network flight; furthest="+maxPlunger);
            Check(plungerPulled,"plunger physically pulls participant from25m to host");
            Check(sawSlimeThree&&sawSlimeZero,"participant receives3 slime uses and consumes all3 via real input");
            Check(shieldBlocked,"opponent ice consumes aircap without freezing protected host");
            Check(errorMessages.Count==0,"no runtime errors during connected test");
            if(host)Check(maxIceSpeed>35,"authoritative ice projectile has enhanced launch speed; max="+maxIceSpeed);
            log.Add("INFO artificial60ms one-way and20percent dropped outgoing messages; no physical WAN; source-only test, no release package rebuild.");
        }
        finally
        {
            File.WriteAllLines(folder+"/evidence/enhanced-connected/"+role+".txt",log);File.WriteAllLines(folder+"/evidence/enhanced-connected/"+role+".csv",csv);
            if(errorMessages.Count>0)File.WriteAllLines(folder+"/evidence/enhanced-connected/"+role+"-errors.txt",errorMessages);
            Set("latencyProof",false);game.LeaveRoom();if(floor)UnityEngine.Object.Destroy(floor);Application.runInBackground=background;EditorApplication.update-=EditorApplication.QueuePlayerLoopUpdate;Application.logMessageReceived-=errors;
            if(Client)EditorApplication.delayCall+=()=>EditorApplication.Exit(0);
        }
        Debug.Log("ENHANCED_CONNECTED_"+role+": "+string.Join("\n",log));
    }
    // A hidden clone has no active Game view, so WaitForEndOfFrame never resumes there.
    static void Capture(string path)
    {
        var camera=Camera.main;if(!camera)camera=UnityEngine.Object.FindAnyObjectByType<Camera>();
        var target=RenderTexture.GetTemporary(1280,720,24);var previous=camera.targetTexture;var active=RenderTexture.active;
        var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
        try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
        finally{camera.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);UnityEngine.Object.Destroy(image);}
    }
}
