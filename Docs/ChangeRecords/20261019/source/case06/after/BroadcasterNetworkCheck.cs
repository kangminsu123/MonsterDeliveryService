using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class BroadcasterNetworkCheck
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    [MenuItem("Tools/Gameplay/Special Delivery/Verify Broadcaster Host")]
    public static void Host()=>Start(true);
    [MenuItem("Tools/Gameplay/Special Delivery/Verify Broadcaster Client")]
    public static void Client()=>Start(false);
    public static void BootstrapClient()=>UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
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
        var t=game.GetType();object Get(string n)=>t.GetField(n,F).GetValue(game);void Set(string n,object v)=>t.GetField(n,F).SetValue(game,v);
        object Call(string n,params object[] args)=>t.GetMethod(n,F).Invoke(game,args);
        string root=Path.GetFullPath(Application.dataPath+"/..");if(!host)root=Path.GetFullPath(root+"/../../..");
        string folder=root+"/Docs/ChangeRecords/20261019",role=host?"host":"client";Directory.CreateDirectory(folder+"/evidence/broadcaster-connected");
        var log=new List<string>{DateTimeOffset.Now.ToString("O"),"Actual two Editors over localhost with artificial60ms one-way delay and20% frame/input omissions. Controlled positions; not WAN."};
        var errors=new List<string>();Application.LogCallback onError=(m,s,k)=>{if(k==LogType.Error||k==LogType.Exception||k==LogType.Assert)errors.Add(m);};
        Application.logMessageReceived+=onError;bool background=Application.runInBackground;GameObject floor=null;
        float hostStun=0,clientStun=0;bool hostExpired=false,clientExpired=false,immuneBlocked=true,cleared0=false,cleared1=false,fxSeen=false,saved=false;int peakFX=0,stage=-1;var fired=new HashSet<int>();
        try
        {
            Application.runInBackground=true;EditorApplication.update+=EditorApplication.QueuePlayerLoopUpdate;
            Set("latencyProof",true);Set("latencySaved",true);Set("latencyLabel","broadcaster-loss");game.NetworkScreens=true;game.NetTeamSize=1;
            if(host)game.HostRoom();else game.JoinRoom("127.0.0.1");
            float deadline=Time.realtimeSinceStartup+60;
            while(!game.NetPlaying&&Time.realtimeSinceStartup<deadline)yield return null;
            if(!game.NetPlaying)throw new Exception("Two-peer connection timed out");
            floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(0,149.9f,4);floor.transform.localScale=new Vector3(100,.2f,100);
            float began=Time.time;
            while(Time.time-began<(host?19:18))
            {
                float age=Time.time-began;int phase=Mathf.Min(2,(int)(age/6));
                var players=(MonsterDeliveryPrototype.NetPlayer[])Get("netPlayers");var input=(MonsterDeliveryPrototype.NetInput)Get("netLocalInput");input.yaw=host?0:180;input.pitch=0;input.slot=0;
                if(host&&stage!=phase)
                {
                    stage=phase;
                    for(int g=0;g<2;g++)
                    {
                        var actor=(CharacterController)Call("ActorController",g);actor.enabled=false;actor.transform.SetPositionAndRotation(new Vector3(0,151,g*8),Quaternion.Euler(0,g*180,0));actor.enabled=true;
                        var state=new DeliveryItemState{safePosition=actor.transform.position};if(phase==2&&g==1)state.immuneUntil=Time.time+5;
                        if(g==0)Set("playerItems",state);else((DeliveryItemState[])Get("botItems"))[0]=state;
                        players[g].vertical=0;players[g].cooldown=0;players[g].actionKind=0;players[g].selected=0;Call("GiveNetItem",g,0,11);
                    }
                    Physics.SyncTransforms();Call("NetBroadcast");
                }
                if((phase==1?!host:host)&&age>=phase*6+1.2f&&fired.Add(phase))input.use++;
                if(phase==0){clientStun=Mathf.Max(clientStun,players[1].stun);if(age>5.5f)clientExpired|=players[1].stun==0;if(age>3)cleared0|=players[0].items[0]==0&&players[0].ammo[0]==0;}
                if(phase==1){hostStun=Mathf.Max(hostStun,players[0].stun);if(age>11.5f)hostExpired|=players[0].stun==0;if(age>9)cleared1|=players[1].items[0]==0&&players[1].ammo[0]==0;}
                if(phase==2&&age>13.6f)immuneBlocked&=players[1].stun==0;
                var effects=UnityEngine.Object.FindObjectsByType<BroadcasterFX>(FindObjectsSortMode.None);peakFX=Mathf.Max(peakFX,effects.Length);
                fxSeen|=effects.Any(fx=>fx.GetComponentsInChildren<LineRenderer>().Length==14&&fx.GetComponent<ParticleSystem>().particleCount>0&&fx.GetComponent<Light>());
                if(phase==0&&!saved&&effects.Any(fx=>fx.Age>.35f))
                {
                    var visual=(DeliveryRobotVisual)Get("playerVisual");
                    if(!host){visual.SetFirstPerson(false);Camera.main.transform.SetPositionAndRotation(new Vector3(13,158,-4),Quaternion.LookRotation(new Vector3(-13,-6.5f,8)));}
                    AirCannonCheck.Capture(folder+"/images/20261019_broadcaster_"+role+"_connected.png");saved=true;
                    if(!host)visual.SetFirstPerson(true);
                }
                yield return null;
            }
            void Check(bool ok,string message)=>log.Add((ok?"PASS ":"FAIL ")+message);
            Check(clientStun>3.5f&&clientStun<=4.05f,"host broadcast gives client four seconds; max="+clientStun);
            Check(hostStun>3.5f&&hostStun<=4.05f,"client broadcast gives host four seconds; max="+hostStun);
            Check(clientExpired&&hostExpired,"both four-second stuns expire before the next stage");
            Check(cleared0&&cleared1,"both peers spend one use and clear the final slot");
            Check(immuneBlocked,"life jacket still blocks broadcast stun");
            Check(fxSeen&&saved,"both peers see and capture layered waves, sparks and warning light");
            Check(peakFX==1,"repeated action snapshots never duplicate an effect; peak="+peakFX);
            Check(UnityEngine.Object.FindObjectsByType<BroadcasterFX>(FindObjectsSortMode.None).Length==0,"all network broadcast effects clean up");
            Check(errors.Count==0,"no runtime errors during connected scenario");
        }
        finally
        {
            log.AddRange(errors);File.WriteAllLines(folder+"/evidence/broadcaster-connected/"+role+".txt",log);
            Set("latencyProof",false);game.LeaveRoom();if(floor)UnityEngine.Object.Destroy(floor);
            Application.runInBackground=background;EditorApplication.update-=EditorApplication.QueuePlayerLoopUpdate;Application.logMessageReceived-=onError;
        }
        Debug.Log("BROADCASTER_CONNECTED_"+role+"\n"+string.Join("\n",log));
    }
}
