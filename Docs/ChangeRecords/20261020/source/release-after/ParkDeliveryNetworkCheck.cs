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
public static class ParkDeliveryNetworkCheck
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    static bool Client=>Environment.GetCommandLineArgs().Contains("-delivery-park-client");
    [Serializable] public sealed class Stage { public int phase; public Vector3 position; }
    static ParkDeliveryNetworkCheck()
    {
        if(Client) EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode)Start(false);};
    }
    public static void BootstrapClient()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        EditorApplication.delayCall+=()=>EditorApplication.isPlaying=true;
    }
    [MenuItem("Tools/Gameplay/Special Delivery/Verify Minute Delivery Connected Host")]
    public static void Host()=>Start(true);
    static void Start(bool host)
    {
        if(!EditorApplication.isPlaying)throw new Exception("Fresh Play required");
        var a=typeof(Unity.Netcode.NetworkManager).Assembly;
        var registry=(IList)a.GetType("Unity.Netcode.ILPPMessageProvider").GetField("__network_message_types",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        if(registry.Count==0)a.GetType("__GEN.INetworkMessageHelper").GetMethod("InitializeMessages",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Invoke(null,null);
        var game=UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>();game.StartCoroutine(Run(game,host));
    }
    static IEnumerator Run(MonsterDeliveryPrototype game,bool host)
    {
        var t=game.GetType();object Get(string n)=>t.GetField(n,F).GetValue(game);
        void Set(string n,object v)=>t.GetField(n,F).SetValue(game,v);
        object Call(string n,params object[] a)=>t.GetMethod(n,F).Invoke(game,a);
        string root=Path.GetFullPath(Application.dataPath+"/..");if(!host)root=Path.GetFullPath(root+"/../../..");
        string d=root+"/Docs/ChangeRecords/20261020",folder=d+"/evidence/park-connected",role=host?"host":"client";
        Directory.CreateDirectory(folder);Directory.CreateDirectory(d+"/images");string control=folder+"/stage.json";
        var log=new List<string>{DateTimeOffset.Now.ToString("O"),"Two separate Unity Editor processes connected through NGO/UTP localhost. Artificial60ms one-way delay; every fifth input/frame omitted. Clock jumps are test fixtures. Shared stage file only coordinates input timing and expected location; all asserted gameplay state is read from each peer's actual game snapshot."};
        var errors=new List<string>();Application.LogCallback error=(m,s,k)=>{if(k==LogType.Error||k==LogType.Exception||k==LogType.Assert)errors.Add(m);};Application.logMessageReceived+=error;
        bool[] seen=new bool[18];bool same=true,noEarly=true,one=true;int previous=-1;Vector3 first=default,second=default;
        var keyboard=InputSystem.AddDevice<Keyboard>("ParkDeliveryPeerKeyboard");var mouse=InputSystem.AddDevice<Mouse>("ParkDeliveryPeerMouse");
        var oldKeyboard=Keyboard.current;var oldMouse=Mouse.current;
        var background=InputSystem.settings.backgroundBehavior;var inputEditor=InputSystem.settings.editorInputBehaviorInPlayMode;
        var csv=new List<string>{"realtime_s,phase,delivery_minute,available,x,y,z,notice,host_items,client_items,fov,key_e,input_e,cursor,mouse_right,own_x,own_y,own_z"};
        try
        {
            Application.runInBackground=true;EditorApplication.update+=EditorApplication.QueuePlayerLoopUpdate;
            QualitySettings.vSyncCount=0;Application.targetFrameRate=60;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Set("latencyProof",true);Set("latencySaved",true);Set("latencyLabel","park-loss");game.NetworkScreens=true;game.NetTeamSize=1;
            if(host){File.WriteAllText(control,JsonUtility.ToJson(new Stage{phase=0}));game.HostRoom();}else game.JoinRoom("127.0.0.1");
            float deadline=Time.realtimeSinceStartup+90;
            while(!game.NetPlaying&&Time.realtimeSinceStartup<deadline)yield return null;
            if(!game.NetPlaying)throw new Exception("Two-peer park connection timed out");
            // A hidden Editor resets device focus between Updates. Run its unchanged TickNetwork
            // immediately after the synthetic input state, rather than depending on window focus.
            if(!host)game.enabled=false;
            float began=Time.realtimeSinceStartup;var stage=new Stage();var parcel=(SpecialDeliveryParcel)Get("specialParcel");var pickups=(SpecialDeliveryParcel[])Get("specialPickups");
            while(Time.realtimeSinceStartup-began<(host?63:61))
            {
                float age=Time.realtimeSinceStartup-began;
                int phase=age<3?0:age<7?1:age<20?2:age<26?3:age<32?4:age<38?5:age<44?6:age<52?7:age<56?8:9;
                if(host&&previous!=phase)
                {
                    if(phase==1||phase==3||phase==4||phase==5)
                    {
                        Set("netRoundUntil",Time.time+(phase==1?239.8f:phase==3?179.8f:phase==4?119.8f:59.8f));Call("UpdateParkDelivery");
                    }
                    if(phase==2)
                    {
                        var actor=(CharacterController)Call("ActorController",1);
                        bool approach=false;
                        for(int n=0;n<12;n++)
                        {
                            float angle=n*Mathf.PI/6;actor.enabled=false;actor.transform.position=parcel.transform.position+new Vector3(Mathf.Sin(angle)*1.8f,.15f,-Mathf.Cos(angle)*1.8f);actor.enabled=true;Physics.SyncTransforms();
                            var eye=(Vector3)Call("NetEye",1);var toward=parcel.GetComponent<BoxCollider>().bounds.center-eye;
                            object[] ray={1,eye,toward.normalized,3f,.01f,null,Vector3.zero,Vector3.zero};
                            if((bool)Call("NetRay",ray)&&((Collider)ray[5]).GetComponentInParent<SpecialDeliveryParcel>()==parcel){approach=true;break;}
                        }
                        if(!approach)throw new Exception("No unobstructed pickup approach found");
                        ((DeliveryItemState[])Get("botItems"))[0]=new DeliveryItemState{safePosition=actor.transform.position};
                        var ps=(MonsterDeliveryPrototype.NetPlayer[])Get("netPlayers");ps[1].vertical=0;Call("NetBroadcast");
                    }
                    if(phase==6||phase==7)
                    {
                        var ps=(MonsterDeliveryPrototype.NetPlayer[])Get("netPlayers");
                        Call("GiveNetItem",0,0,phase==6?13:14);ps[0].selected=0;ps[0].cooldown=0;Call("UseNetWeapon",0);
                    }
                    if(phase==8)Set("netRoundUntil",Time.time-.1f);
                    if(phase==9)game.StartNetRound();
                    stage=new Stage{phase=phase,position=parcel.transform.position};File.WriteAllText(control,JsonUtility.ToJson(stage));
                    previous=phase;
                }
                if(!host&&File.Exists(control))
                {try{stage=JsonUtility.FromJson<Stage>(File.ReadAllText(control));}catch(IOException){}catch(ArgumentException){}}
                phase=stage.phase;
                keyboard.MakeCurrent();mouse.MakeCurrent();
                bool capture=!host&&phase==2&&parcel.Available;
                InputSystem.QueueStateEvent(keyboard,capture?new KeyboardState(Key.E):new KeyboardState());
                bool zoom=phase==5;
                InputSystem.QueueStateEvent(mouse,new MouseState{buttons=(ushort)(zoom?2:0)});InputSystem.Update();
                // MPPM's hidden-window input bridge filters queued hardware events. Set only
                // the harness-owned synthetic devices directly, then use normal game input code.
                InputSystem.EnableDevice(keyboard);InputSystem.EnableDevice(mouse);
                InputState.Change(keyboard,capture?new KeyboardState(Key.E):new KeyboardState());
                InputState.Change(mouse,new MouseState{buttons=(ushort)(zoom?2:0)});
                if(game.NetPlaying)Cursor.lockState=CursorLockMode.Locked;
                if(!host&&phase==2&&parcel.Available)
                {
                    var eye=((DeliveryRobotVisual)Get("playerVisual")).EyePosition;var angles=Quaternion.LookRotation(parcel.GetComponent<BoxCollider>().bounds.center-eye).eulerAngles;
                    var input=(MonsterDeliveryPrototype.NetInput)Get("netLocalInput");input.yaw=angles.y;input.pitch=angles.x>180?angles.x-360:angles.x;
                }
                if(!host){Call("TickNetwork");Call("LateUpdate");}
                var players=(MonsterDeliveryPrototype.NetPlayer[])Get("netPlayers");int count=players.Where(p=>p!=null).Sum(p=>p.items.Count(k=>k>=10&&k<=14));
                int minute=(int)Get("deliveryMinute");var pos=parcel.transform.position;var cam=Camera.main;
                one &= pickups.Count(p=>p.gameObject.activeInHierarchy&&p.Available)<=1;
                if(phase==0){seen[0]|=game.NetPlaying&&game.roundSeconds==300&&game.HudClock.StartsWith("04:5");noEarly&=!parcel.Available;seen[1]|=players.Where(p=>p!=null).All(p=>p.items.All(k=>k==0))&&pickups.Where(p=>p.ReplaceSelected).All(p=>!p.gameObject.activeSelf);}
                if(phase==1&&minute==1&&parcel.Available&&Vector3.Distance(pos,stage.position)<.001f)
                {
                    seen[2]=true;seen[3]|=(float)Get("deliveryNoticeUntil")>Time.time;
                    var glow=parcel.GetComponent<ParkDeliveryGlow>();seen[4]|=glow&&glow.GetComponentsInChildren<LineRenderer>().Length==1&&glow.GetComponentInChildren<Light>();
                    if(first==default)first=pos;else same&=first==pos;
                    if(!File.Exists(d+"/images/20261020_park_connected_"+role+".png"))
                    {
                        var previousPosition=cam.transform.position;var rotation=cam.transform.rotation;
                        cam.transform.position=pos+new Vector3(2.5f,2,-3.2f);cam.transform.LookAt(pos+Vector3.up*.4f);
                        AirCannonCheck.Capture(d+"/images/20261020_park_connected_"+role+".png");cam.transform.SetPositionAndRotation(previousPosition,rotation);
                    }
                }
                if(phase==2){seen[5]|=!parcel.Available&&count==1;seen[6]|=age>18&&!parcel.Available&&count==1;}
                if(phase==3&&minute==2&&parcel.Available){seen[7]=Vector3.Distance(pos,stage.position)<.001f;second=pos;}
                if(phase==4&&minute==3&&parcel.Available)seen[8]|=Vector3.Distance(pos,stage.position)<.001f&&pos!=second;
                if(phase==5){seen[9]|=minute==4&&parcel.Available&&Vector3.Distance(pos,stage.position)<.001f;seen[10]|=Mathf.Abs(cam.fieldOfView-35)<.1f;}
                if(phase==6){seen[11]|=Mathf.Abs(cam.fieldOfView-60)<.2f;seen[12]|=players[0].immune>1;if(!host)seen[13]|=UnityEngine.Object.FindObjectsByType<LifeJacketProtectionFX>(FindObjectsSortMode.None).Any(fx=>fx.gameObject.activeInHierarchy&&fx.GetComponentsInChildren<LineRenderer>().Length==11);}
                if(phase==7){seen[14]|=players[0].battery>1;if(!host)seen[15]|=UnityEngine.Object.FindObjectsByType<BatteryChargeAura>(FindObjectsSortMode.None).Any(fx=>fx.gameObject.activeInHierarchy&&fx.GetComponentsInChildren<LineRenderer>().Length==12);}
                if(phase==8)seen[16]|=game.RoomState==MonsterDeliveryPrototype.RoomPhase.Results&&minute==4;
                if(phase==9)seen[17]|=game.NetPlaying&&!parcel.Available&&minute==0&&game.HudClock.StartsWith("04:5")&&(float)Get("deliveryNoticeUntil")<=Time.time;
                var localInput=(MonsterDeliveryPrototype.NetInput)Get("netLocalInput");var localActor=(CharacterController)Get("player");
                csv.Add(FormattableString.Invariant($"{age:F3},{phase},{minute},{parcel.Available},{pos.x:F4},{pos.y:F4},{pos.z:F4},{(float)Get("deliveryNoticeUntil")-Time.time:F3},{players[0]?.items.Count(k=>k>=10&&k<=14)},{players[1]?.items.Count(k=>k>=10&&k<=14)},{cam.fieldOfView:F2},{keyboard.eKey.isPressed},{localInput.capture},{Cursor.lockState},{mouse.rightButton.isPressed},{localActor.transform.position.x:F3},{localActor.transform.position.y:F3},{localActor.transform.position.z:F3}"));
                yield return null;
            }
            void Check(bool ok,string text)=>log.Add((ok?"PASS ":"FAIL ")+text);
            Check(seen[0]&&noEarly,"five-minute match starts with no early park delivery");Check(seen[1],"both inventories start empty and fixed development weapons are hidden");
            Check(seen[2],"first-minute parcel matches the host random position");Check(seen[3],"arrival notice is replicated");Check(seen[4],"small light and ring render on this peer");
            Check(same&&one,"one parcel remains stable within minute and never accumulates");Check(seen[5],"real remote E pickup awards exactly one random item across both inventories");Check(seen[6],"collected parcel remains absent past old ten-second respawn");
            Check(seen[7]&&seen[8]&&seen[9],"second, third and fourth minute positions synchronize and uncollected parcel relocates");
            Check(seen[10]&&seen[11],"right-button hold zooms to35 degrees and releases to60 on this network peer");Check(seen[12],"life-jacket immunity state synchronizes");
            if(!host)Check(seen[13],"remote life jacket gold rings render on host character");Check(seen[14],"battery duration synchronizes");if(!host)Check(seen[15],"remote battery electric arcs render separately from running shoes");
            Check(seen[16],"both peers reach results without a fifth delivery");Check(seen[17],"new network round resets clock, box and announcement");
            Check((int)Get("latencyDroppedMessages")>0,"test actually omitted every fifth frame/input");
        }
        finally
        {
            InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(mouse);
            if(oldKeyboard!=null&&oldKeyboard.added)oldKeyboard.MakeCurrent();if(oldMouse!=null&&oldMouse.added)oldMouse.MakeCurrent();
            InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=inputEditor;
            game.enabled=true;Set("latencyProof",false);game.LeaveRoom();EditorApplication.update-=EditorApplication.QueuePlayerLoopUpdate;
            File.WriteAllLines(folder+"/"+role+".csv",csv);
        }
        yield return new WaitForSeconds(.5f);log.Add((errors.Count==0?"PASS ":"FAIL ")+"no runtime or shutdown errors; count="+errors.Count);log.AddRange(errors);
        Application.logMessageReceived-=error;File.WriteAllLines(folder+"/"+role+".txt",log);Debug.Log("PARK_CONNECTED_"+role+"\n"+string.Join("\n",log));
        if(Client)EditorApplication.delayCall+=()=>EditorApplication.Exit(0);
    }
}
