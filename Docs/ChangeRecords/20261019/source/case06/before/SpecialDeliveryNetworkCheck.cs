using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
public static class SpecialDeliveryNetworkCheck
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly bool DedicatedClient=Environment.GetCommandLineArgs().Contains("-delivery-row-client");
    static SpecialDeliveryNetworkCheck()
    {if(DedicatedClient)EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredPlayMode)Start(false);};}
    public static void BootstrapClient()
    {UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");EditorApplication.delayCall+=()=>EditorApplication.isPlaying=true;}
    [MenuItem("Tools/Gameplay/Special Delivery/Verify Connected Host")]
    public static void Host()=>Start(true);
    [MenuItem("Tools/Gameplay/Special Delivery/Verify Connected Client")]
    public static void Client()=>Start(false);
    static void Start(bool host)
    {
        if(!EditorApplication.isPlaying)throw new Exception("Fresh Play required");
        // A standalone readonly clone can miss NGO's generated RuntimeInitialize hook.
        // Only the verification harness restores the native generated registry when empty.
        var assembly=typeof(Unity.Netcode.NetworkManager).Assembly;
        var provider=assembly.GetType("Unity.Netcode.ILPPMessageProvider");
        var registry=(IList)provider.GetField("__network_message_types",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        if(registry.Count==0)
        {
            assembly.GetType("__GEN.INetworkMessageHelper").GetMethod("InitializeMessages",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Invoke(null,null);
            Debug.Log("SPECIAL_CHECK_SETUP: restored NGO native generated message registry for standalone clone, count="+registry.Count);
        }
        var game=UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>();game.StartCoroutine(Run(game,host));
    }
    static IEnumerator Run(MonsterDeliveryPrototype game,bool host)
    {
        var t=game.GetType();object Get(string n)=>t.GetField(n,F).GetValue(game);
        void Set(string n,object v)=>t.GetField(n,F).SetValue(game,v);
        object Call(string n,params object[] a)=>t.GetMethod(n,F).Invoke(game,a);
        string root=Path.GetFullPath(Application.dataPath+"/..");if(!host)root=Path.GetFullPath(root+"/../../..");
        string folder=root+"/Docs/ChangeRecords/20261019",role=host?"host":"client";
        Directory.CreateDirectory(folder+"/evidence/special-row-connected");Directory.CreateDirectory(folder+"/images");
        var log=new List<string>{DateTimeOffset.Now.ToString("O"),"Actual main Unity Editor and MPPM virtual Editor over localhost; artificial60ms one-way delay, every fifth frame/input deliberately dropped. Controlled positions and input. Not physical WAN."};
        var samples=new List<string>{"age,stage,host_y,guest_stun,host_packed,parcel_available,reward_count"};
        var errors=new List<string>();Application.LogCallback onError=(message,stack,type)=>{if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message);};
        Application.logMessageReceived+=onError;GameObject floor=null;bool background=Application.runInBackground;
        bool initial=false,cleared=false,remoteHeld=false,remotePack=false,hidden=false,respawned=false,oneReward=false,captured=false;
        bool rowPickup=false,rowHidden=false,rowRestored=false,lifeBlocked=false,lifeResumed=false,lifeVisible=false,batteryFull=false,batteryExpired=false;float immuneMax=0,batteryMax=0;
        float highest=151,broadcastStun=0,packed=0;int phase=-1;var fired=new HashSet<int>();
        var inputBackground=InputSystem.settings.backgroundBehavior;
        var editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
        Keyboard testKeyboard=null;
        var parcel=UnityEngine.Object.FindObjectsByType<SpecialDeliveryParcel>(FindObjectsSortMode.None).Single(p=>p.fixedItem==WeaponKind.None);
        try
        {
            Application.runInBackground=true;EditorApplication.update+=EditorApplication.QueuePlayerLoopUpdate;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            testKeyboard=InputSystem.AddDevice<Keyboard>("SpecialItemTestKeyboard");testKeyboard.MakeCurrent();
            Set("latencyProof",true);Set("latencySaved",true);Set("latencyLabel","special-loss");game.NetworkScreens=true;game.NetTeamSize=1;
            if(host)game.HostRoom();else game.JoinRoom("127.0.0.1");
            float deadline=Time.realtimeSinceStartup+60;
            while(!game.NetPlaying&&Time.realtimeSinceStartup<deadline)yield return null;
            if(!game.NetPlaying)throw new Exception("Special-item two-peer connection timed out");
            floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Special Delivery Connected Test Floor";floor.transform.position=new Vector3(0,149.9f,0);floor.transform.localScale=new Vector3(160,.2f,160);
            float began=Time.time;
            while(Time.time-began<(host?58:56))
            {
                float age=Time.time-began;int stage=age<5?0:age<12?1:age<20?2:age<35?3:age<43?4:5;
                var players=(MonsterDeliveryPrototype.NetPlayer[])Get("netPlayers");var input=(MonsterDeliveryPrototype.NetInput)Get("netLocalInput");
                int own=(int)Get("netOwn");input.slot=0;input.yaw=host?0:180;input.pitch=stage==0&&!host?14:0;
                if(age<.4f)initial|=players.Where(p=>p!=null&&p.connected).Count()==2&&players.Where(p=>p!=null&&p.connected).All(p=>p.items.SequenceEqual(new[]{10,11,12})&&p.ammo.All(n=>n==1));
                if(host&&phase!=stage)
                {
                    phase=stage;
                    for(int g=0;g<2;g++)
                    {
                        var actor=(CharacterController)Call("ActorController",g);actor.enabled=false;
                        actor.transform.SetPositionAndRotation(stage==3?parcel.transform.position+new Vector3(g==0?-.65f:.65f,1,-1.65f):stage>=4?new Vector3(stage==4?2.55f:4.65f,1,g==0?20.8f:23.2f):new Vector3(0,151,g*8),Quaternion.Euler(0,g==0?0:180,0));actor.enabled=true;
                        var state=new DeliveryItemState{safePosition=actor.transform.position};if(g==0)Set("playerItems",state);else ((DeliveryItemState[])Get("botItems"))[0]=state;
                        players[g].vertical=0;players[g].cooldown=0;players[g].actionKind=0;players[g].target=-1;players[g].selected=0;
                        if(stage>0||age>.4f)Call("GiveNetItem",g,0,stage==0?10:stage==1?11:12);
                        if(stage==3){players[g].items[0]=0;players[g].ammo[0]=0;}
                        if(stage>=4&&g==0)Call("GiveNetItem",g,0,11);
                    }
                    if(stage==3)parcel.ResetRound();
                    if(stage>=4)foreach(var pickup in UnityEngine.Object.FindObjectsByType<SpecialDeliveryParcel>(FindObjectsSortMode.None))pickup.ResetRound();Call("NetBroadcast");
                }
                void Fire(int id,float at,bool onHost)
                {if(host==onHost&&age>=at&&!fired.Contains(id)){input.use++;fired.Add(id);}}
                Fire(0,1.4f,false);Fire(1,6.4f,true);Fire(2,13.4f,false);
                Fire(10,37f,false);Fire(11,38.2f,true);
                if(host&&stage==4&&age>39&&!fired.Contains(20)){fired.Add(20);Call("GiveNetItem",0,0,10);((DeliveryItemState)Get("playerItems")).immuneUntil=Time.time+2; /* Keep attacker stationary while testing target immunity. */}
                Fire(12,39.2f,true);
                if(host&&stage==4&&age>42&&!fired.Contains(21)){fired.Add(21);Call("GiveNetItem",0,0,11);}
                Fire(13,42.3f,true);Fire(14,46f,false);
                if(!host)foreach(float at in new[]{47f,47.6f,48.2f,48.8f,49.4f,54.8f})if(age>=at&&fired.Add((int)(at*100))){input.dash++;}
                if(stage>=4)
                {
                    int kind=stage==4?13:14;
                    var pickup=UnityEngine.Object.FindObjectsByType<SpecialDeliveryParcel>(FindObjectsSortMode.None).Single(p=>(int)p.fixedItem==kind);
                    if(!host&&age<(stage==4?37:46))
                    {
                        var visual=(DeliveryRobotVisual)Get("playerVisual");Vector3 toward=pickup.GetComponent<BoxCollider>().bounds.center-visual.EyePosition;
                        input.yaw=Quaternion.LookRotation(toward).eulerAngles.y;input.pitch=-Mathf.Atan2(toward.y,new Vector2(toward.x,toward.z).magnitude)*Mathf.Rad2Deg;
                    }
                    if(Keyboard.current!=null)InputSystem.QueueStateEvent(Keyboard.current,!host&&((age>35.6f&&age<36.2f)||(age>44&&age<44.6f))?new KeyboardState(Key.E):new KeyboardState());
                    if(stage==4)
                    {
                        if(age>36.3f&&age<36.8f)rowPickup|=players[1].items[0]==13;rowHidden|=!pickup.Available;
                        if(age>38.5f)rowRestored|=pickup.Available;
                        immuneMax=Mathf.Max(immuneMax,players[1].immune);
                        if(age>39.5f&&age<41)lifeBlocked|=players[1].immune>0&&players[1].stun==0&&Mathf.Abs(players[1].position.z-23.2f)<.15f;
                        if(age>42.5f)lifeResumed|=players[1].immune<=0&&players[1].stun>2;
                        var views=(GameObject[])Get("lifeJacketViews");lifeVisible|=views.Any(v=>v&&v.activeSelf);
                        if(host&&age>39.6f&&!fired.Contains(22)){fired.Add(22);Capture(folder+"/images/20261019_special_lifejacket_remote.png");}
                    }
                    if(stage==5)
                    {
                        batteryMax=Mathf.Max(batteryMax,players[1].battery);
                        if(age>49.7f&&age<52)batteryFull|=players[1].battery>0&&players[1].stamina==100;
                        if(age>55)batteryExpired|=players[1].battery<=0&&players[1].stamina<70;
                        if(host&&age>46.7f&&!fired.Contains(23)){fired.Add(23);Capture(folder+"/images/20261019_special_battery_remote.png");}
                        if(!host&&age>46.7f&&!fired.Contains(24)){fired.Add(24);ScreenCapture.CaptureScreenshot(folder+"/images/20261019_special_battery_local.png");}
                    }
                }
                if(stage==0){highest=Mathf.Max(highest,players[0].position.y);if(age>2.5f)cleared|=players[1].items[0]==0&&players[1].ammo[0]==0;}
                if(stage==1)broadcastStun=Mathf.Max(broadcastStun,players[1].stun);
                if(stage==2)
                {
                    packed=Mathf.Max(packed,players[0].packed);
                    int remote=(int)Call("NetLocal",host?1:0);
                    var actor=(CharacterController)Call("ActorController",remote);
                    remotePack|=actor.GetComponentsInChildren<Transform>().Any(x=>x.name=="PackingEffect");
                    if(!host&&age>14&&!captured){captured=true;Capture(folder+"/images/20261019_special_row_connected_packing_client.png");}
                }
                var held=(DeliveryHeldItem[])Get("netHeld");remoteHeld|=held.Any(h=>h&&h.name.Contains("AirCannon"));
                if(stage==3)
                {
                    var visual=(DeliveryRobotVisual)Get("playerVisual");Vector3 toward=parcel.GetComponent<BoxCollider>().bounds.center-visual.EyePosition;
                    input.yaw=Quaternion.LookRotation(toward).eulerAngles.y;input.pitch=-Mathf.Atan2(toward.y,new Vector2(toward.x,toward.z).magnitude)*Mathf.Rad2Deg;
                    if(Keyboard.current!=null)InputSystem.QueueStateEvent(Keyboard.current,age>21&&age<22?new KeyboardState(Key.E):new KeyboardState());
                    int rewards=players.Take(2).Count(p=>p.items[0]>=10&&p.items[0]<=14);
                    if(age>22&&age<29){hidden|=!parcel.Available;oneReward|=rewards==1;}
                    if(age>32)respawned|=parcel.Available&&rewards==1;
                    samples.Add(FormattableString.Invariant($"{age:F3},{stage},{players[0].position.y:F3},{players[1].stun:F3},{players[0].packed:F3},{parcel.Available},{rewards}"));
                }
                yield return null;
            }
            void Check(bool ok,string message)=>log.Add((ok?"PASS ":"FAIL ")+message);
            Check(initial,"both peers receive the three one-use special items at round start");
            Check(highest>155.5f,"client cannon launches authoritative host upward; peak="+highest);
            Check(cleared,"client cannon final-use slot and held model clear");
            Check(broadcastStun>2.4f,"host broadcaster stun reaches client; max="+broadcastStun);
            Check(packed>3.4f,"client packer stun and packing deadline reach host and participant; max="+packed);
            Check(remoteHeld,"remote imported held model exists");
            if(!host)Check(remotePack,"participant sees imported parcel around packed opponent");
            Check(hidden&&oneReward,"simultaneous park E input yields one reward and one hidden parcel on both peers");
            Check(respawned,"one park parcel returns after ten seconds with no duplicate reward");
            Check(rowPickup&&rowHidden&&rowRestored,"actual client E equips life jacket and both peers see pickup disappear and return after two seconds");
            Check(immuneMax>4.4f&&lifeBlocked&&lifeResumed,"five-second life jacket ignores broadcast and cannon; effects resume after expiration; max="+immuneMax);
            if(host)Check(lifeVisible,"opponent wears imported life-jacket model while protection is active");
            Check(batteryMax>7.3f&&batteryFull&&batteryExpired,"actual client battery use gives eight seconds and five free dashes, then normal stamina cost resumes; max="+batteryMax);
            Check(errors.Count==0,"no runtime errors during connected scenario");
        }
        finally
        {
            File.WriteAllLines(folder+"/evidence/special-row-connected/"+role+".txt",log);File.WriteAllLines(folder+"/evidence/special-row-connected/"+role+".csv",samples);
            if(errors.Count>0)File.WriteAllLines(folder+"/evidence/special-row-connected/"+role+"-errors.txt",errors);
            if(Keyboard.current!=null)InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());
            if(testKeyboard!=null)InputSystem.RemoveDevice(testKeyboard);
            InputSystem.settings.backgroundBehavior=inputBackground;InputSystem.settings.editorInputBehaviorInPlayMode=editorInput;
            Set("latencyProof",false);WatchRoomClose(game,folder,role);if(floor)UnityEngine.Object.Destroy(floor);
            Application.runInBackground=background;EditorApplication.update-=EditorApplication.QueuePlayerLoopUpdate;Application.logMessageReceived-=onError;
            if(DedicatedClient)EditorApplication.delayCall+=()=>EditorApplication.Exit(0);
        }
        Debug.Log("SPECIAL_CONNECTED_"+role+"\n"+string.Join("\n",log));
    }
    static void WatchRoomClose(MonsterDeliveryPrototype game,string folder,string role)
    {
        var errors=new List<string>();
        Application.LogCallback onError=(message,stack,type)=>{if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message);};
        Application.logMessageReceived+=onError;
        double deadline=EditorApplication.timeSinceStartup+2;
        EditorApplication.CallbackFunction tick=null;
        tick=()=>
        {
            EditorApplication.QueuePlayerLoopUpdate();if(EditorApplication.timeSinceStartup<deadline)return;
            EditorApplication.update-=tick;Application.logMessageReceived-=onError;
            var manager=game.GetType().GetField("netManager",F).GetValue(game) as Unity.Netcode.NetworkManager;
            var log=new List<string>{DateTimeOffset.Now.ToString("O"),(manager==null?"PASS ":"FAIL ")+"room manager is released after graceful shutdown",(errors.Count==0?"PASS ":"FAIL ")+"no runtime error during room teardown"};log.AddRange(errors);
            File.WriteAllLines(folder+"/evidence/special-row-connected/"+role+"-exit.txt",log);
        };
        EditorApplication.update+=tick;game.LeaveRoom();
    }
    static void Capture(string path)
    {
        var camera=Camera.main;var target=RenderTexture.GetTemporary(1280,720,24);var previous=camera.targetTexture;var active=RenderTexture.active;
        var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
        try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
        finally{camera.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);UnityEngine.Object.Destroy(image);}
    }
}
