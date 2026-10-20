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
using Stopwatch=System.Diagnostics.Stopwatch;

public static class NetSyncAuditCheck
{
    const string Folder="Docs/ChangeRecords/20261018/";
    const BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
    static string Label=>typeof(MonsterDeliveryPrototype).GetMethod("HammerSwingAt",BindingFlags.NonPublic|BindingFlags.Static)==null?"before":"after";
    [MenuItem("Tools/Gameplay/Network Audit/Verify and Benchmark")]
    public static void Verify()
    {if(!EditorApplication.isPlaying)throw new Exception("Fresh Play required");var game=UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>();game.StartCoroutine(Run(game));}
    static IEnumerator Run(MonsterDeliveryPrototype game)
    {
        var type=game.GetType();object Get(string n)=>type.GetField(n,Flags).GetValue(game);
        void Set(string n,object v)=>type.GetField(n,Flags).SetValue(game,v);
        object Call(string n,params object[] args)=>type.GetMethod(n,Flags).Invoke(game,args);
        string label=Label;var log=new List<string>{DateTimeOffset.Now.ToString("O"),"Controlled Play animation/weapon checks and same-frame warmed CPU/allocation benchmarks, not WAN."};
        void Check(bool ok,string text)=>log.Add((ok?"PASS ":"FAIL ")+text);
        bool oldEnabled=game.enabled,background=Application.runInBackground;
        var metrics=new Dictionary<string,object>();
        try
        {
            game.enabled=false;Application.runInBackground=true;EditorApplication.update+=EditorApplication.QueuePlayerLoopUpdate;
            Set("started",true);Set("finished",false);game.NetworkScreens=true;Set("<RoomState>k__BackingField",MonsterDeliveryPrototype.RoomPhase.Playing);Set("netOwn",0);
            var players=new[]{new MonsterDeliveryPrototype.NetPlayer{connected=true,team=0,color=0},new MonsterDeliveryPrototype.NetPlayer{connected=true,team=1,color=1},null,null};Set("netPlayers",players);
            var visual=(DeliveryRobotVisual)Get("playerVisual");var remote=((DeliveryRobotVisual[])Get("botVisuals"))[0];
            ((CharacterController)Get("player")).gameObject.SetActive(true);((CharacterController[])Get("botControllers"))[0].gameObject.SetActive(true);
            foreach(var rig in new[]{visual,remote})foreach(bool moving in new[]{false,true})foreach(bool holding in new[]{false,true})
            {
                rig.gameObject.SetActive(true);var animator=rig.model.GetComponent<Animator>();rig.SetMotion(moving,holding);for(int n=0;n<16;n++)animator.Update(.05f);
                string state=(moving?"WALK":"IDLE")+(holding?"_ARMED":"");
                Check(animator.GetCurrentAnimatorStateInfo(0).IsName(state),rig.name+" animator enters "+state);
            }
            var actors=new[]{(CharacterController)Get("player"),((CharacterController[])Get("botControllers"))[0]};
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Network Audit Review Floor";floor.transform.position=new Vector3(0,149.9f,0);floor.transform.localScale=new Vector3(100,.2f,100);
            for(int g=0;g<2;g++){actors[g].enabled=false;actors[g].gameObject.SetActive(true);actors[g].transform.SetPositionAndRotation(new Vector3(g*1.4f,151,g*3),Quaternion.Euler(0,g*180,0));actors[g].enabled=true;}
            var cam=(Camera)Get("cam");cam.transform.rotation=Quaternion.identity;Call("PositionCamera");
            for(int kind=1;kind<=9;kind++)
            {
                foreach(var p in players.Where(p=>p!=null)){p.items=new[]{kind,0,0};p.selected=0;p.actionAt=Time.time-2;p.actionKind=kind;p.actionSlot=0;p.plungerFlying=false;p.throwReleased=false;}
                Call("SyncHostInventory");Call("UpdateHeldWeapon");Call("PoseNetworkPlayers");
                bool held=kind!=6&&kind!=7;var own=(DeliveryHeldItem)Get("heldItem");var other=((DeliveryHeldItem[])Get("netHeld"))[1];
                Check((bool)own==held&&(bool)other==held,((WeaponKind)kind)+" has correct local and remote held models");
                if(kind==8)
                {float at=players[0].actionAt;Call("UseNetWeapon",0);Check(own.paired&&other.paired&&players[0].actionAt==at,"work gloves stay paired and passive without a click attack");}
                if(kind==6||kind==7)
                {
                    players[0].cooldown=0;Call("UseNetWeapon",0);var state=(DeliveryItemState)Get("playerItems");
                    Check(kind==6?state.speedUntil>Time.time:state.shieldUntil>Time.time,((WeaponKind)kind)+" applies authoritative activation state without a hand model");
                }
                if(kind==3)
                {
                    var animator=remote.model.GetComponent<Animator>();animator.Play("IDLE_ARMED",0,0);animator.Update(0);
                    var arm=remote.model.GetComponentsInChildren<Transform>().Single(t=>(t.name=="Arm.R"||t.name=="Arm.L")&&remote.transform.InverseTransformPoint(t.position).x>0);var rotation=arm.rotation;
                    foreach(var p in players.Where(p=>p!=null))p.actionAt=Time.time-.17f;
                    Call("PoseNetworkPlayers");Check(own.gameObject.activeSelf&&Quaternion.Angle(own.firstPersonRotation,own.transform.localRotation)>60,"online hammer uses full first-person swing instead of a small root bob");
                    Check(Quaternion.Angle(rotation,arm.rotation)>40,"online hammer moves the opponent right arm");
                    yield return new WaitForEndOfFrame();Capture(Folder+"images/20261018_audit_"+label+"_hammer.png");
                }
            }
            players[0].items=new[]{1,0,0};Call("SyncHostInventory");Call("UpdateHeldWeapon");
            players[0].controlLocked=true;actors[0].transform.position+=Vector3.forward;Call("PoseNetworkPlayers");
            Check(!visual.model.GetComponent<Animator>().GetBool("Moving"),"forced displacement does not play voluntary walking animation");
            players[0].controlLocked=false;
            game.LeaveRoom();game.HostRoom();yield return null;
            players=(MonsterDeliveryPrototype.NetPlayer[])Get("netPlayers");
            for(int g=1;g<4;g++)players[g]=new MonsterDeliveryPrototype.NetPlayer{connected=true,team=g%2,color=g,name="Review "+g};
            game.NetTeamSize=2;game.StartNetRound();
            double Bench(string name,Action action,int count=100)
            {
                for(int i=0;i<10;i++)action();long allocated=GC.GetAllocatedBytesForCurrentThread(),began=Stopwatch.GetTimestamp();
                for(int i=0;i<count;i++)action();double ms=(Stopwatch.GetTimestamp()-began)*1000.0/Stopwatch.Frequency/count;
                metrics[name+"_ms_per_call"]=ms;metrics[name+"_allocation_counter_available"]=GC.GetAllocatedBytesForCurrentThread()>allocated?1:0;return ms;
            }
            var broadcast=type.GetMethod("NetBroadcast",Flags);Bench("server_capture",()=>broadcast.Invoke(game,null),40);
            var pool=(ParcelSpawnPool)Get("parcelPool");var slots=pool.CaptureSlots();Bench("unchanged_parcels",()=>pool.ApplySnapshot(slots),200);
            var frameType=type.GetNestedType("NetFrame",BindingFlags.NonPublic);var shotType=type.GetNestedType("NetShot",BindingFlags.NonPublic);var frame=Activator.CreateInstance(frameType);
            void Field(string n,object value)=>frameType.GetField(n).SetValue(frame,value);
            Field("players",players);Field("owners",Get("owner"));Field("own",0);Field("phase",(int)MonsterDeliveryPrototype.RoomPhase.Playing);Field("teamSize",2);Field("parcels",slots);Field("shots",Array.CreateInstance(shotType,0));Field("remaining",100f);
            int sequence=(int)Get("netLastFrameSequence")+1;var apply=type.GetMethod("ApplyNetFrame",Flags);var args=new[]{frame};
            Bench("apply_frame",()=>{Field("sequence",sequence++);apply.Invoke(game,args);});
            var pack=type.GetMethod("PackNetJson",BindingFlags.Static|BindingFlags.NonPublic);string json=JsonUtility.ToJson(frame);byte[] compressed=null;
            Bench("serialize_compress",()=>{json=JsonUtility.ToJson(frame);compressed=(byte[])pack.Invoke(null,new object[]{json});});
            metrics["json_bytes"]=System.Text.Encoding.UTF8.GetByteCount(json);metrics["compressed_bytes"]=compressed.Length;
            Check(pool.ActiveCount==30&&slots.Distinct().Count()==30,"parcel optimization preserves 30 unique occupied anchors");
            var invalid=(int[])slots.Clone();invalid[1]=invalid[0];Check(!pool.ApplySnapshot(invalid),"duplicate parcel anchor snapshot is rejected");
            invalid=(int[])slots.Clone();invalid[0]=99;Check(!pool.ApplySnapshot(invalid),"out of range parcel snapshot is rejected");
            var shot=(BrawlShot)Call("LaunchFountainPen",new Vector3(0,170,0),Vector3.forward,48f,0);
            var registry=type.GetField("netActiveShots",Flags);
            if(registry!=null)
            {
                bool Tracked()=>((IEnumerable)registry.GetValue(game)).Cast<BrawlShot>().Contains(shot);
                Check(Tracked(),"live projectile registers with its owner without a scene search");shot.owner=null;Check(!Tracked(),"changing projectile owner removes the old registration");shot.owner=game;game.StartNetRound();
                Check(!Tracked()&&!shot.gameObject.activeSelf,"round reset clears registered projectiles before broadcasting");
            }
            else UnityEngine.Object.Destroy(shot.gameObject);
            var smooth=typeof(DeliveryRobotVisual).GetMethod("SmoothNetworkPosition");
            if(smooth!=null)
            {
                remote.SmoothNetworkPosition(false,0);var previous=remote.model.position;int movingFrames=0,stationaryFrames=0;bool colliderStable=true;float lag=0;
                for(int n=0;n<60;n++)
                {
                    if(n>0&&n%4==0)actors[1].transform.position+=Vector3.forward*(6.5f/30);
                    var rootPosition=actors[1].transform.position;remote.SmoothNetworkPosition(true,1f/120);
                    colliderStable&=actors[1].transform.position==rootPosition;
                    if(n>=4){if(Vector3.Distance(previous,remote.model.position)>.0001f)movingFrames++;else stationaryFrames++;}
                    lag=Mathf.Max(lag,Mathf.Abs(actors[1].transform.position.z-remote.model.position.z));previous=remote.model.position;
                }
                Check(movingFrames==56&&stationaryFrames==0,"host remote visual moves every render frame between30Hz input steps");
                Check(colliderStable,"visual smoothing never moves the authoritative collision capsule");Check(lag<.5f,"host remote visual follows within0.5m at6.5m/s");
                remote.SmoothNetworkPosition(false,0);
            }
            UnityEngine.Object.Destroy(floor);
        }
        finally
        {
            File.WriteAllLines(Folder+"evidence/sync-audit-"+label+".txt",log);File.WriteAllText(Folder+"evidence/sync-benchmark-"+label+".json",MiniJson(metrics));
            game.LeaveRoom();game.enabled=oldEnabled;Application.runInBackground=background;EditorApplication.update-=EditorApplication.QueuePlayerLoopUpdate;
        }
        Debug.Log("SYNC_AUDIT_"+label+": "+log.Count(x=>x.StartsWith("PASS"))+" PASS / "+log.Count(x=>x.StartsWith("FAIL"))+" FAIL");
        if(label=="after"&&log.Any(x=>x.StartsWith("FAIL")))throw new Exception("SYNC_AUDIT_FAILED");
    }
    static string MiniJson(Dictionary<string,object> values)=>"{\n"+string.Join(",\n",values.Select(p=>"  \""+p.Key+"\": "+Convert.ToString(p.Value,System.Globalization.CultureInfo.InvariantCulture)))+"\n}";
    static void Capture(string path){var image=ScreenCapture.CaptureScreenshotAsTexture();try{File.WriteAllBytes(path,image.EncodeToPNG());}finally{UnityEngine.Object.Destroy(image);}}
    [MenuItem("Tools/Gameplay/Network Audit/Host Movement Review")]
    public static void HostMove()=>BeginMove(true);
    [MenuItem("Tools/Gameplay/Network Audit/Join Movement Review")]
    public static void JoinMove()=>BeginMove(false);
    static void BeginMove(bool host)
    {if(!EditorApplication.isPlaying)throw new Exception("Play required");var game=UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>();game.StartCoroutine(Movement(game,host));}
    static IEnumerator Movement(MonsterDeliveryPrototype game,bool host)
    {
        var type=game.GetType();object Get(string n)=>type.GetField(n,Flags).GetValue(game);
        void Set(string n,object value)=>type.GetField(n,Flags).SetValue(game,value);
        object Call(string n,params object[] args)=>type.GetMethod(n,Flags).Invoke(game,args);
        string root=Path.GetFullPath(Application.dataPath+"/..");if(!host)root=Path.GetFullPath(root+"/../../..");
        string directory=Path.Combine(root,Folder),role=host?"host":"client";
        var log=new List<string>{DateTimeOffset.Now.ToString("O"),"Two real MPPM processes,60ms per-way frame/input delay and every fifth omitted. Keyboard state injection is internal to Unity Input System."};
        var pings=new List<float>();
        bool background=Application.runInBackground;var keyboard=Keyboard.current;var observed=new HashSet<string>();bool shift=false;
        var player=(CharacterController)Get("player");Vector3 origin=Vector3.zero;var rows=new List<string>{"time,local_x,local_y,local_z,server_x,server_y,server_z,height,stamina,dash"};
        try
        {
            Application.runInBackground=true;EditorApplication.update+=EditorApplication.QueuePlayerLoopUpdate;
            Set("latencyProof",true);Set("latencySaved",true);Set("latencyLabel","movement-loss");Set("netTools",true);game.NetworkScreens=true;game.NetTeamSize=1;
            if(host)game.HostRoom();else game.JoinRoom("127.0.0.1");
            float deadline=Time.realtimeSinceStartup+45;while(!game.NetPlaying&&Time.realtimeSinceStartup<deadline)yield return null;
            if(!game.NetPlaying)throw new Exception("Movement connection timeout");
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Movement Review Floor";floor.transform.position=new Vector3(0,149.9f,0);floor.transform.localScale=new Vector3(160,.2f,160);
            if(host)for(int g=0;g<2;g++)
            {
                var actor=(CharacterController)Call("ActorController",g);actor.enabled=false;actor.transform.position=new Vector3(g*8,151,0);actor.enabled=true;
                var p=((MonsterDeliveryPrototype.NetPlayer[])Get("netPlayers"))[g];p.vertical=0;p.items=new[]{0,0,0};p.selected=0;Call("NetBroadcast");
            }
            yield return new WaitForSecondsRealtime(.6f);origin=player.transform.position;float began=Time.time;
            while(Time.time-began<10)
            {
                float age=Time.time-began;Key[] keys=age>=1&&age<2?new[]{Key.D}:age>=2&&age<3?new[]{Key.S}:age>=3&&age<3.6f?new[]{Key.C,Key.W}:age>=4&&age<4.4f?new[]{Key.LeftShift,Key.W}:age>=5&&age<5.12f?new[]{Key.Space}:age>=6&&age<7?new[]{Key.W}:Array.Empty<Key>();
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));InputSystem.Update();
                ((MonsterDeliveryPrototype.NetInput)Get("netLocalInput")).yaw=age>=6?90:0;
                if(age>=4&&!shift){((MonsterDeliveryPrototype.NetInput)Get("netLocalInput")).dash++;shift=true;}
                yield return new WaitForEndOfFrame();
                var players=(MonsterDeliveryPrototype.NetPlayer[])Get("netPlayers");int own=(int)Get("netOwn");var p=players[own];var position=player.transform.position;
                if(age>1.5f&&age<2&&position.x>origin.x+1)observed.Add("strafe");
                if(age>2.5f&&age<3&&position.z<origin.z-1)observed.Add("backward");
                if(age>3.1f&&age<3.6f&&player.height<1.2f)observed.Add("crouch");
                if(age>4&&age<4.6f&&p.stamina<70)observed.Add("dash");
                if(age>5&&age<6&&position.y>origin.y+.5f)observed.Add("jump");
                if(age>6.4f&&age<7&&Mathf.Abs(Mathf.DeltaAngle(player.transform.eulerAngles.y,90))<1)observed.Add("turn");
                foreach(int g in new[]{0,1})if(players[g].position.y>151.5f)observed.Add("server-jump"+g);
                if(age>9&&Vector3.Distance(position,p.position)<.15f)observed.Add("settled");
                if(age>3)pings.Add(Convert.ToSingle(Get("netPingMs")));
                rows.Add(FormattableString.Invariant($"{age:F4},{position.x:F4},{position.y:F4},{position.z:F4},{p.position.x:F4},{p.position.y:F4},{p.position.z:F4},{player.height:F2},{p.stamina:F2},{p.dashRemaining:F3}"));
                yield return null;
            }
            foreach(string check in new[]{"strafe","backward","crouch","dash","jump","turn","server-jump0","server-jump1","settled"})log.Add((observed.Contains(check)?"PASS ":"FAIL ")+role+" movement "+check);
            log.Add("INFO final transport RTT mean_ms="+pings.Average().ToString("F3")+"; artificial application queue is excluded from transport RTT");Capture(directory+"images/20261018_sync_final_"+role+"_diagnostics.png");
            File.WriteAllLines(directory+"evidence/sync-movement-"+role+".txt",log);File.WriteAllLines(directory+"evidence/sync-movement-"+role+".csv",rows);
            Debug.Log("SYNC_MOVEMENT_"+role+": "+string.Join("\n",log));
        }
        finally{InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();Set("latencyProof",false);Set("latencySaved",false);Application.runInBackground=background;EditorApplication.update-=EditorApplication.QueuePlayerLoopUpdate;}
    }
    [MenuItem("Tools/Gameplay/Network Audit/Host Delayed Review")]
    public static void Host()=>Connect(true);
    [MenuItem("Tools/Gameplay/Network Audit/Join Delayed Review")]
    public static void Join()=>Connect(false);
    static void Connect(bool host)
    {if(!EditorApplication.isPlaying)throw new Exception("Play required");var game=UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>();game.StartCoroutine(Review(game,host));}
    static IEnumerator Review(MonsterDeliveryPrototype game,bool host)
    {
        var type=game.GetType();object Get(string n)=>type.GetField(n,Flags).GetValue(game);
        void Set(string n,object value)=>type.GetField(n,Flags).SetValue(game,value);
        object Call(string n,params object[] args)=>type.GetMethod(n,Flags).Invoke(game,args);
        string root=Path.GetFullPath(Application.dataPath+"/..");if(!host)root=Path.GetFullPath(root+"/../../..");
        string directory=Path.Combine(root,Folder),label=Label,role=host?"host":"client",metricsFolder=directory+"evidence/sync-perf/";
        Directory.CreateDirectory(metricsFolder);
        var log=new List<string>{DateTimeOffset.Now.ToString("O"),"Actual MPPM localhost. Existing diagnostic delays each outgoing frame/input by60ms and drops every fifth; no physical WAN. Scripted equipment and input."};
        var rows=new List<string>{"time,frame_ms,ping_ms,ack_ms,receive_hz,work_ms,work_alloc_counter,send_kb,receive_kb,own_x,own_y,own_z,remote_x,remote_y,remote_z,remote_visual_x,remote_visual_z,own_walk,remote_walk,own_frozen,remote_frozen,control_locked"};
        var accepted=new HashSet<int>();var frozen=new HashSet<int>();var controlled=new HashSet<int>();var captures=new HashSet<string>();
        bool ownWalk=false,remoteWalk=false,ownIdle=false,remoteIdle=false;int phase=-1,sent=-1;bool hit1=false,hit2=false,push=false;
        bool background=Application.runInBackground;int fps=Application.targetFrameRate,vsync=QualitySettings.vSyncCount;
        try
        {
            Application.runInBackground=true;EditorApplication.update+=EditorApplication.QueuePlayerLoopUpdate;
            Call("BeginLatencyProof",label+"-loss",metricsFolder);Set("netTools",true);game.NetTeamSize=1;game.NetworkScreens=true;
            if(host)game.HostRoom();else game.JoinRoom("127.0.0.1");
            float deadline=Time.realtimeSinceStartup+45;while(!game.NetPlaying&&Time.realtimeSinceStartup<deadline)yield return null;
            if(!game.NetPlaying)throw new Exception("Sync review connection timeout");
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Delayed Sync Review Floor";floor.transform.position=new Vector3(0,149.9f,0);floor.transform.localScale=new Vector3(160,.2f,160);
            var ownActor=(CharacterController)Get("player");var otherActor=((CharacterController[])Get("botControllers"))[0];
            void Place()
            {
                for(int g=0;g<2;g++)
                {
                    var actor=(CharacterController)Call("ActorController",g);actor.enabled=false;actor.transform.position=new Vector3(g*1.4f,151,g*4);actor.enabled=true;
                    ((MonsterDeliveryPrototype.NetPlayer[])Get("netPlayers"))[g].vertical=0;
                }
            }
            if(host){Place();Call("NetBroadcast");}
            float began=Time.time;
            while(Time.time-began<34)
            {
                float age=Time.time-began;int own=(int)Get("netOwn"),other=own==0?1:0;
                var players=(MonsterDeliveryPrototype.NetPlayer[])Get("netPlayers");var input=(MonsterDeliveryPrototype.NetInput)Get("netLocalInput");input.slot=0;
                if(age>7)input.yaw=host?0:180;
                if(age>=9&&age<27)
                {
                    int kind=1+Mathf.Min(8,(int)((age-9)/2));
                    if(host&&phase!=kind){phase=kind;Place();foreach(var p in players.Where(p=>p!=null&&p.connected)){p.items=new[]{kind,0,0};p.selected=0;}Call("NetBroadcast");}
                    if(sent<kind&&age>9+(kind-1)*2+(host?.35f:.9f)){input.use++;sent=kind;}
                    var mine=players[own];if(kind==8&&mine.items[mine.selected]==8||mine.actionKind==kind)accepted.Add(kind);
                    if(age>9+(kind-1)*2+1.2f&&captures.Add("item"+kind))
                    {yield return new WaitForEndOfFrame();Capture(directory+"images/20261018_sync_"+label+"_"+role+"_item"+kind+".png");}
                }
                if(age>=27&&host&&phase!=10)
                {
                    phase=10;Place();
                    var guest=(CharacterController)Call("ActorController",1);guest.enabled=false;guest.transform.position=new Vector3(0,151,2.8f);guest.enabled=true;
                    foreach(var p in players.Where(p=>p!=null&&p.connected)){p.items=new[]{5,0,0};p.selected=0;p.cooldown=0;}
                    Set("playerItems",new DeliveryItemState());((DeliveryItemState[])Get("botItems"))[0]=new DeliveryItemState();Call("NetBroadcast");
                }
                if(host&&age>27.4f&&!hit1){input.use++;hit1=true;}
                if(!host&&age>29.4f&&!hit2){input.use++;hit2=true;}
                if(host&&age>31.2f&&!push)
                {
                    push=true;Place();var guest=(CharacterController)Call("ActorController",1);guest.enabled=false;guest.transform.position=new Vector3(0,151,2.8f);guest.enabled=true;
                    foreach(var p in players.Where(p=>p!=null&&p.connected)){p.items=new[]{1,0,0};p.selected=0;p.cooldown=0;}
                    Set("playerItems",new DeliveryItemState());((DeliveryItemState[])Get("botItems"))[0]=new DeliveryItemState();Call("NetBroadcast");input.use++;
                }
                foreach(int g in new[]{0,1}){if(players[g].frozen>0)frozen.Add(g);if(players[g].controlLocked)controlled.Add(g);}
                var ownRig=(DeliveryRobotVisual)Get("playerVisual");var otherRig=((DeliveryRobotVisual[])Get("botVisuals"))[0];
                bool walk=ownRig.model.GetComponent<Animator>().GetBool("Moving"),otherMoving=otherRig.model.GetComponent<Animator>().GetBool("Moving");
                if(age>1.4f&&age<2.5f){ownWalk|=walk;remoteWalk|=otherMoving;}
                if(age>7.5f&&age<8.5f){ownIdle|=!walk;remoteIdle|=!otherMoving;}
                var pos=ownActor.transform.position;var remotePos=otherActor.transform.position;var model=otherRig.model.position;
                float Metric(string n)=>Convert.ToSingle(Get(n));
                rows.Add(FormattableString.Invariant($"{age:F5},{Time.unscaledDeltaTime*1000:F4},{Metric("netPingMs"):F3},{Metric("netAckMs"):F3},{Metric("netReceiveHz"):F3},{Metric("netWorkMs"):F5},{Metric("netWorkAlloc"):F1},{Metric("netSendKB"):F4},{Metric("netReceiveKB"):F4},{pos.x:F5},{pos.y:F5},{pos.z:F5},{remotePos.x:F5},{remotePos.y:F5},{remotePos.z:F5},{model.x:F5},{model.z:F5},{walk},{otherMoving},{players[own].frozen>0},{players[other].frozen>0},{players[own].controlLocked}"));
                yield return null;
            }
            void Check(bool ok,string detail)=>log.Add((ok?"PASS ":"FAIL ")+detail);
            Check(ownWalk&&remoteWalk,"both local and remote walk animation activates during movement");Check(ownIdle&&remoteIdle,"both local and remote return to idle after stopping");
            for(int k=1;k<=9;k++)Check(accepted.Contains(k),role+" receives own accepted action or passive equipment for "+(WeaponKind)k);
            foreach(int g in new[]{0,1})Check(frozen.Contains(g),role+" receives actual ice cream freeze for global "+g);
            Check(controlled.Contains(1),role+" receives authoritative boxing knockback on guest");
            File.WriteAllLines(directory+"evidence/sync-connected-"+label+"-"+role+".txt",log);File.WriteAllLines(directory+"evidence/sync-connected-"+label+"-"+role+".csv",rows);
            Debug.Log("SYNC_CONNECTED_"+label+"_"+role+": "+string.Join("\n",log));
        }
        finally{Set("latencyProof",false);Application.runInBackground=background;Application.targetFrameRate=fps;QualitySettings.vSyncCount=vsync;EditorApplication.update-=EditorApplication.QueuePlayerLoopUpdate;}
    }
}
