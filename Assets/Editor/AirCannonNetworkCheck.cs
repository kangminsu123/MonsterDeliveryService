using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class AirCannonNetworkCheck
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    [MenuItem("Tools/Gameplay/Special Delivery/Verify Air Cannon Host")]
    public static void Host()=>Start(true);
    [MenuItem("Tools/Gameplay/Special Delivery/Verify Air Cannon Client")]
    public static void Client()=>Start(false);
    public static void BootstrapClient()
    {UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");}
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
        object Call(string n,params object[] a)=>t.GetMethod(n,F).Invoke(game,a);
        string root=Path.GetFullPath(Application.dataPath+"/..");if(!host)root=Path.GetFullPath(root+"/../../..");
        string folder=root+"/Docs/ChangeRecords/20261019",role=host?"host":"client";
        Directory.CreateDirectory(folder+"/evidence/air-cannon-burst-connected");
        var log=new List<string>{DateTimeOffset.Now.ToString("O"),"Actual two Editor processes over localhost. Artificial60ms one-way delay and20% frame/input omissions. Controlled positions; not WAN."};
        var errors=new List<string>();Application.LogCallback onError=(m,s,k)=>{if(k==LogType.Error||k==LogType.Exception||k==LogType.Assert)errors.Add(m);};
        Application.logMessageReceived+=onError;bool background=Application.runInBackground;GameObject floor=null,wall=null;
        bool shotSeen=false,impactSeen=false,flightSaved=false,impactSaved=false,jumpSaved=false,wallSaved=false,wallBurst=false,earlySafe=true,cleared0=false,cleared1=false,immuneSafe=true;
        float highestHost=151,highestClient=151,selfHost=151,selfClient=151;int stage=-1;var fired=new HashSet<int>();
        try
        {
            Application.runInBackground=true;EditorApplication.update+=EditorApplication.QueuePlayerLoopUpdate;
            Set("latencyProof",true);Set("latencySaved",true);Set("latencyLabel","air-cannon-loss");game.NetworkScreens=true;game.NetTeamSize=1;
            if(host)game.HostRoom();else game.JoinRoom("127.0.0.1");
            float deadline=Time.realtimeSinceStartup+60;
            while(!game.NetPlaying&&Time.realtimeSinceStartup<deadline)yield return null;
            if(!game.NetPlaying)throw new Exception("Two-peer connection timed out");
            floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(0,149.9f,10);floor.transform.localScale=new Vector3(200,.2f,200);
            float began=Time.time;
            while(Time.time-began<(host?34:33))
            {
                float age=Time.time-began;int phase=Mathf.Min(5,(int)(age/5));
                var players=(MonsterDeliveryPrototype.NetPlayer[])Get("netPlayers");var input=(MonsterDeliveryPrototype.NetInput)Get("netLocalInput");input.yaw=0;input.pitch=phase==5?0:phase>=3?80:6;input.slot=0;
                if(host&&stage!=phase)
                {
                    stage=phase;
                    for(int g=0;g<2;g++)
                    {
                        var actor=(CharacterController)Call("ActorController",g);actor.enabled=false;
                        bool source=g==(phase==1||phase==4?1:0);actor.transform.SetPositionAndRotation(source?new Vector3(0,151,0):new Vector3(2,151,18),Quaternion.identity);actor.enabled=true;
                        var state=new DeliveryItemState{safePosition=actor.transform.position};if(phase==2&&g==1)state.immuneUntil=Time.time+5;
                        if(g==0)Set("playerItems",state);else((DeliveryItemState[])Get("botItems"))[0]=state;
                        players[g].vertical=0;players[g].cooldown=0;players[g].actionKind=0;players[g].selected=0;
                        Call("GiveNetItem",g,0,10);
                    }
                    Physics.SyncTransforms();Call("NetBroadcast");
                }
                if(phase==5&&!wall)
                {wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=new Vector3(0,152,10);wall.transform.localScale=new Vector3(10,5,.2f);Physics.SyncTransforms();}
                bool shooter=phase==1||phase==4?!host:host;float release=phase*5+1.2f;
                if(shooter&&age>=release&&fired.Add(phase))input.use++;
                if(age>.5f&&age<1)earlySafe&=Mathf.Abs(players[1].position.y-151)<.1f;
                if(phase==0)
                {highestClient=Mathf.Max(highestClient,players[1].position.y);if(age>3)cleared0|=players[0].items[0]==0&&players[0].ammo[0]==0;}
                if(phase==1)
                {highestHost=Mathf.Max(highestHost,players[0].position.y);if(age>8)cleared1|=players[1].items[0]==0&&players[1].ammo[0]==0;}
                if(phase==2&&age>12.5f)immuneSafe&=Mathf.Abs(players[1].position.y-151)<.15f&&Mathf.Abs(players[1].position.x-2)<.15f;
                if(phase==3)selfHost=Mathf.Max(selfHost,players[0].position.y);
                if(phase==4)selfClient=Mathf.Max(selfClient,players[1].position.y);
                if(!jumpSaved&&phase==(host?3:4)&&players[host?0:1].position.y>155)
                {
                    var position=players[host?0:1].position;
                    Camera.main.transform.SetPositionAndRotation(position+new Vector3(8,4,-8),Quaternion.LookRotation(new Vector3(-8,-3,8)));
                    var ownVisual=(DeliveryRobotVisual)Get("playerVisual");ownVisual.SetFirstPerson(false);
                    AirCannonCheck.Capture(folder+"/images/20261019_air_cannon_burst_"+role+"_self_jump.png");
                    ownVisual.SetFirstPerson(true);jumpSaved=true;
                }
                bool flying=host?UnityEngine.Object.FindObjectsByType<BrawlShot>(FindObjectsSortMode.None).Any(s=>s.kind==WeaponKind.AirCannon&&s.gameObject.activeSelf):((IDictionary)Get("netShotViews")).Count>0;
                shotSeen|=flying;var impact=GameObject.Find("AirCannonExplosion");impactSeen|=impact;
                if(phase==0&&(flying&&age>=1.3f&&!flightSaved||impact&&impact.GetComponent<AirCannonImpactFX>().Age>.08f&&!impactSaved))
                {
                    Camera.main.transform.SetPositionAndRotation(new Vector3(10,158,5),Quaternion.LookRotation(new Vector3(-10,-6,13)));
                    var ownVisual=(DeliveryRobotVisual)Get("playerVisual");ownVisual.SetFirstPerson(false);
                    if(flying&&age>=1.3f&&!flightSaved){AirCannonCheck.Capture(folder+"/images/20261019_air_cannon_burst_"+role+"_flight.png");flightSaved=true;}
                    if(impact&&impact.GetComponent<AirCannonImpactFX>().Age>.08f&&!impactSaved){AirCannonCheck.Capture(folder+"/images/20261019_air_cannon_burst_"+role+"_impact.png");impactSaved=true;}
                    ownVisual.SetFirstPerson(true);
                }
                if(phase==5&&impact&&impact.GetComponent<AirCannonImpactFX>().Age>.08f)
                {
                    wallBurst|=Vector3.Dot(impact.transform.forward,Vector3.back)>.98f&&impact.GetComponent<ParticleSystem>().particleCount>0&&impact.GetComponentsInChildren<LineRenderer>().Length==3;
                    if(!wallSaved)
                    {
                        Camera.main.transform.SetPositionAndRotation(new Vector3(11,157,0),Quaternion.LookRotation(new Vector3(-11,-5,10)));
                        var ownVisual=(DeliveryRobotVisual)Get("playerVisual");ownVisual.SetFirstPerson(false);
                        AirCannonCheck.Capture(folder+"/images/20261019_air_cannon_burst_"+role+"_wall.png");ownVisual.SetFirstPerson(true);wallSaved=true;
                    }
                }
                yield return null;
            }
            void Check(bool ok,string message)=>log.Add((ok?"PASS ":"FAIL ")+message);
            Check(earlySafe,"no pre-impact launch force");
            Check(shotSeen,"server ball and physics-free participant projectile are visible");
            Check(impactSeen&&flightSaved&&impactSaved,"both flight and reliable impact effect received and captured");
            Check(highestClient>155,"host shot lands then launches client; peak="+highestClient);
            Check(highestHost>155,"client shot lands then launches host; peak="+highestHost);
            Check(cleared0&&cleared1,"each peer spends one use and clears last-use slot");
            Check(immuneSafe,"authoritative life jacket blocks landed blast without client push");
            Check(selfHost>155,"host foot shot launches host and replicates to participant; peak="+selfHost);
            Check(selfClient>155,"client foot shot launches client and replicates to host; peak="+selfClient);
            Check(jumpSaved,"actual own foot-shot jump image captured");
            Check(wallBurst&&wallSaved,"wall impact particles, three shockwaves and surface normal are replicated and captured");
            Check(errors.Count==0,"no runtime errors during two-peer scenario");
        }
        finally
        {
            log.AddRange(errors);File.WriteAllLines(folder+"/evidence/air-cannon-burst-connected/"+role+".txt",log);
            Set("latencyProof",false);game.LeaveRoom();if(wall)UnityEngine.Object.Destroy(wall);if(floor)UnityEngine.Object.Destroy(floor);
            Application.runInBackground=background;EditorApplication.update-=EditorApplication.QueuePlayerLoopUpdate;Application.logMessageReceived-=onError;
        }
        Debug.Log("AIR_CANNON_CONNECTED_"+role+"\n"+string.Join("\n",log));
    }
}
