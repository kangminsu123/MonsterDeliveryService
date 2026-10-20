using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class ProjectileDisplayCheck
{
    const string Folder="Docs/ChangeRecords/20261018/";
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    [MenuItem("Tools/Gameplay/Projectiles/Verify Display")]
    public static void Verify()
    {
        if(!EditorApplication.isPlaying)throw new Exception("Fresh Play Mode required");
        var game=UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>();game.StartCoroutine(Run(game));
    }
    static IEnumerator Run(MonsterDeliveryPrototype game)
    {
        var type=game.GetType();object Get(string n)=>type.GetField(n,Flags).GetValue(game);
        void Set(string n,object v)=>type.GetField(n,Flags).SetValue(game,v);
        object Call(string n,params object[] a)=>type.GetMethod(n,Flags).Invoke(game,a);
        var shotType=type.GetNestedType("NetShot",BindingFlags.NonPublic);
        object Shot(int kind,Vector3 position,Vector3 velocity,Vector3 acceleration)
        {
            var shot=Activator.CreateInstance(shotType);shotType.GetField("id").SetValue(shot,"review");shotType.GetField("kind").SetValue(shot,kind);
            shotType.GetField("position").SetValue(shot,position);shotType.GetField("rotation").SetValue(shot,Quaternion.identity);
            shotType.GetField("velocity")?.SetValue(shot,velocity);shotType.GetField("acceleration")?.SetValue(shot,acceleration);return shot;
        }
        void Sync(object shot){var array=Array.CreateInstance(shotType,shot==null?0:1);if(shot!=null)array.SetValue(shot,0);Call("SyncNetShotViews",array);}
        var motion=type.GetMethod("UpdateNetShotViews",Flags);
        void Tick()=>motion?.Invoke(game,new object[]{Time.time,Time.deltaTime});
        var log=new List<string>{DateTimeOffset.Now.ToString("O"),"Controlled Unity Play display check; scripted snapshots at 30Hz including one lost interval."};
        void Check(bool ok,string label)=>log.Add((ok?"PASS ":"FAIL ")+label);
        bool oldEnabled=game.enabled,oldBackground=Application.runInBackground;
        var traces=new List<string>{"kind,time,received,position_x,position_y"};
        string stage=motion==null?"before":"after";
        try
        {
            game.enabled=false;Application.runInBackground=true;EditorApplication.update+=EditorApplication.QueuePlayerLoopUpdate;
            Set("started",true);Set("finished",false);game.NetworkScreens=true;Set("netOwn",0);Set("<RoomState>k__BackingField",MonsterDeliveryPrototype.RoomPhase.Playing);
            var players=new[]{new MonsterDeliveryPrototype.NetPlayer{connected=true,items=new[]{2,0,0},selected=0},new MonsterDeliveryPrototype.NetPlayer{connected=true,items=new[]{2,0,0},selected=0},null,null};
            Set("netPlayers",players);Set("selectedSlot",0);Call("SyncHostInventory");Call("UpdateHeldWeapon");
            var actors=new[]{(CharacterController)Get("player"),((CharacterController[])Get("botControllers"))[0]};
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Projectile Display Review Floor";floor.transform.position=new Vector3(0,149.9f,0);floor.transform.localScale=new Vector3(60,.2f,60);
            for(int i=0;i<2;i++){actors[i].enabled=false;actors[i].gameObject.SetActive(true);actors[i].transform.SetPositionAndRotation(new Vector3(i*1.4f,151,i*4),Quaternion.Euler(0,i*180,0));actors[i].enabled=true;players[i].actionAt=Time.time-.4f;players[i].actionKind=2;}
            Call("PositionCamera");Call("PoseNetworkPlayers");
            var own=(GameObject)Get("heldWeapon");var other=((DeliveryHeldItem[])Get("netHeld"))[1].gameObject;
            var flying=typeof(MonsterDeliveryPrototype.NetPlayer).GetField("plungerFlying");
            foreach(var p in players.Where(p=>p!=null))flying?.SetValue(p,true);
            Call("PoseNetworkPlayers");Check(!own.activeSelf,"local plunger remains absent while airborne after 0.4 seconds");Check(!other.activeSelf,"opponent plunger remains absent while airborne");
            yield return new WaitForEndOfFrame();Capture(Folder+"images/20261018_plunger_"+stage+"_airborne.png");
            game.thirdPersonPreview=true;Call("UpdateHeldWeapon");Call("PoseNetworkPlayers");Check(!((GameObject)Get("heldWeapon")).activeSelf,"third-person local plunger also remains absent");
            foreach(var p in players.Where(p=>p!=null))flying?.SetValue(p,false);
            game.thirdPersonPreview=false;Call("UpdateHeldWeapon");Call("PoseNetworkPlayers");
            Check(((GameObject)Get("heldWeapon")).activeSelf&&other.activeSelf,"held plungers return when authoritative flight ends");
            var copy=JsonUtility.FromJson<MonsterDeliveryPrototype.NetPlayer>(JsonUtility.ToJson(players[0]));
            flying?.SetValue(players[0],true);copy=JsonUtility.FromJson<MonsterDeliveryPrototype.NetPlayer>(JsonUtility.ToJson(players[0]));
            Check(flying!=null&&(bool)flying.GetValue(copy),"snapshot retains plunger flight state");
            foreach(int kind in new[]{2,4,5,9})
            {
                Sync(null);Vector3 velocity=Vector3.right*(kind==9?48:kind==2?36:18),gravity=kind==4||kind==5?Physics.gravity:Vector3.zero;
                var origin=new Vector3(0,160,10);float began=Time.time,next=0;int packets=0,moving=0,waiting=0;float maxError=0;var previous=origin;
                while(Time.time-began<.65f)
                {
                    float age=Time.time-began;bool received=age>=next;
                    if(received){Sync(Shot(kind,origin+velocity*age+gravity*(.5f*age*age),velocity+gravity*age,gravity));packets++;next=age+(packets==5?2f/30:1f/30);}
                    Tick();var model=((Dictionary<string,GameObject>)Get("netShotViews"))["review"];
                    if(!received&&age>.04f){if(Vector3.Distance(model.transform.position,previous)>.0001f)moving++;else waiting++;}
                    maxError=Mathf.Max(maxError,Vector3.Distance(model.transform.position,origin+velocity*age+gravity*(.5f*age*age)));
                    traces.Add(FormattableString.Invariant($"{kind},{age:F5},{received},{model.transform.position.x:F5},{model.transform.position.y:F5}"));previous=model.transform.position;
                    yield return null;
                }
                Check(moving>0&&waiting==0,((WeaponKind)kind)+" moves on every render frame between snapshots: moving="+moving+" stationary="+waiting);
                Check(maxError<.15f,((WeaponKind)kind)+" trajectory error below 0.15m including skipped snapshot: "+maxError.ToString("F5"));
                var clone=JsonUtility.FromJson(JsonUtility.ToJson(Shot(kind,origin,velocity,gravity)),shotType);
                Check(shotType.GetField("velocity")!=null&&(Vector3)shotType.GetField("velocity").GetValue(clone)==velocity,((WeaponKind)kind)+" velocity survives snapshot JSON");
            }
            if(motion!=null)
            {
                Sync(Shot(2,new Vector3(0,160,10),Vector3.right*36,Vector3.zero));
                var states=(IDictionary)Get("netShotMotion");var state=states["review"];state.GetType().GetField("received").SetValue(state,Time.time-.5f);
                Tick();var model=((Dictionary<string,GameObject>)Get("netShotViews"))["review"];var pos=model.transform.position;
                Tick();Check(Vector3.Distance(pos,model.transform.position)<.001f,"prediction stops after 100ms without authoritative samples");
            }
            Sync(null);Check(((Dictionary<string,GameObject>)Get("netShotViews")).Count==0,"empty authoritative snapshot removes projectile immediately");
            foreach(var method in new[]{"LaunchSlime","LaunchIceCream","LaunchFountainPen"})
            {
                var shot=(BrawlShot)(method=="LaunchFountainPen"?Call(method,new Vector3(0,170,0),Vector3.forward,48f,0):Call(method,new Vector3(0,170,0),Vector3.forward*18,0));
                Check(shot.GetComponent<Rigidbody>().interpolation==RigidbodyInterpolation.Interpolate,method+" uses physics interpolation for host view");UnityEngine.Object.Destroy(shot.gameObject);
            }
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Projectile Sweep Review Wall";wall.transform.position=new Vector3(0,170,2);wall.transform.localScale=new Vector3(8,6,.2f);Physics.SyncTransforms();
            foreach(var method in new[]{"LaunchSlime","LaunchIceCream","LaunchFountainPen"})
            {
                var shot=(BrawlShot)(method=="LaunchFountainPen"?Call(method,new Vector3(0,170,0),Vector3.forward,48f,0):Call(method,new Vector3(0,170,0),Vector3.forward*18,0));
                yield return new WaitForSeconds(.25f);Check(!shot||!shot.gameObject.activeSelf,method+" still hits real solid cover with interpolation enabled");
            }
            UnityEngine.Object.Destroy(wall);Call("ClearNetShotViews");
            Check(((IDictionary)Get("netShotMotion")).Count==0&&((Dictionary<string,GameObject>)Get("netShotViews")).Count==0,"session cleanup clears models and prediction state");
            UnityEngine.Object.Destroy(floor);
        }
        finally
        {
            File.WriteAllLines(Folder+"evidence/projectile-display-"+stage+".txt",log);File.WriteAllLines(Folder+"evidence/projectile-display-"+stage+".csv",traces);
            Sync(null);game.enabled=oldEnabled;Application.runInBackground=oldBackground;EditorApplication.update-=EditorApplication.QueuePlayerLoopUpdate;
        }
        Debug.Log("PROJECTILE_DISPLAY_"+stage+": "+log.Count(s=>s.StartsWith("PASS"))+" PASS, "+log.Count(s=>s.StartsWith("FAIL"))+" FAIL");
        if(stage=="after"&&log.Any(s=>s.StartsWith("FAIL")))throw new Exception("PROJECTILE_DISPLAY_FAILED");
    }
    static void Capture(string path)
    {var image=ScreenCapture.CaptureScreenshotAsTexture();try{File.WriteAllBytes(path,image.EncodeToPNG());}finally{UnityEngine.Object.Destroy(image);}}

    [MenuItem("Tools/Gameplay/Projectiles/Host Connected Review")]
    public static void Host()=>BeginConnected(true);
    [MenuItem("Tools/Gameplay/Projectiles/Join Connected Review")]
    public static void Join()=>BeginConnected(false);
    static void BeginConnected(bool host)
    {
        if(!EditorApplication.isPlaying)throw new Exception("Play Mode required");
        var game=UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>();game.StartCoroutine(Connected(game,host));
    }
    static IEnumerator Connected(MonsterDeliveryPrototype game,bool host)
    {
        var type=game.GetType();object Get(string n)=>type.GetField(n,Flags).GetValue(game);
        void Call(string n,params object[] a)=>type.GetMethod(n,Flags).Invoke(game,a);
        string directory=Path.GetFullPath(Application.dataPath+"/..");if(!host)directory=Path.GetFullPath(directory+"/../../..");
        string folder=Path.Combine(directory,Folder),role=host?"host":"client";
        var log=new List<string>{DateTimeOffset.Now.ToString("O"),"Actual two MPPM processes, localhost NGO; scripted loadouts, positions and use inputs. No synthetic network delay."};
        var rows=new List<string>{"time,id,kind,frame_sequence,x,y,z"};
        var hidden=new HashSet<int>();var restored=new HashSet<int>();var returning=new HashSet<int>();var seenKinds=new HashSet<int>();var captured=new HashSet<string>();
        var previous=new Dictionary<string,Vector3>();var stationary=new Dictionary<int,int>();var moving=new Dictionary<int,int>();
        int lastSequence=-1,stage=-1,sent=-1;bool background=Application.runInBackground;int oldTarget=Application.targetFrameRate,oldVsync=QualitySettings.vSyncCount;
        try
        {
            Application.runInBackground=true;QualitySettings.vSyncCount=0;Application.targetFrameRate=120;EditorApplication.update+=EditorApplication.QueuePlayerLoopUpdate;
            game.NetworkScreens=true;game.NetTeamSize=1;if(host)game.HostRoom();else game.JoinRoom("127.0.0.1");
            float deadline=Time.realtimeSinceStartup+45;while(!game.NetPlaying&&Time.realtimeSinceStartup<deadline)yield return null;
            if(!game.NetPlaying)throw new Exception("Projectile connection timeout");
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Connected Projectile Review Floor";floor.transform.position=new Vector3(0,149.9f,0);floor.transform.localScale=new Vector3(100,.2f,100);
            var input=(MonsterDeliveryPrototype.NetInput)Get("netLocalInput");input.slot=0;input.yaw=host?0:180;input.pitch=-5;
            if(host)
            {
                var players=(MonsterDeliveryPrototype.NetPlayer[])Get("netPlayers");
                for(int g=0;g<2;g++)
                {
                    players[g].items=new[]{2,0,0};players[g].selected=0;
                    var actor=(CharacterController)type.GetMethod("ActorController",Flags).Invoke(game,new object[]{g});
                    actor.enabled=false;actor.transform.SetPositionAndRotation(new Vector3(g==0?0:1.4f,151,g==0?0:5),Quaternion.Euler(0,g==0?0:180,0));actor.enabled=true;players[g].vertical=0;
                }
                Call("NetBroadcast");
            }
            yield return new WaitForSecondsRealtime(.8f);float began=Time.time;
            int[] kinds={2,4,5,9};
            while(Time.time-began<8.7f)
            {
                float elapsed=Time.time-began;int current=Mathf.Min(3,(int)(elapsed/2));
                var players=(MonsterDeliveryPrototype.NetPlayer[])Get("netPlayers");
                if(host&&stage!=current)
                {
                    stage=current;foreach(var p in players.Where(p=>p!=null&&p.connected)){p.items=new[]{kinds[current],0,0};p.selected=0;}Call("NetBroadcast");
                }
                input=(MonsterDeliveryPrototype.NetInput)Get("netLocalInput");input.slot=0;
                if(sent<current&&elapsed>current*2+(host?.35f:.8f)){input.use++;sent=current;}
                int own=(int)Get("netOwn"),other=own==0?1:0;
                foreach(int g in new[]{own,other})
                {
                    var p=players[g];if(p.items[p.selected]!=2)continue;
                    var held=g==own?(GameObject)Get("heldWeapon"):((DeliveryHeldItem[])Get("netHeld"))[g]?.gameObject;
                    if(!held)continue;
                    bool flying=p.plungerFlying;
                    if(flying&&!held.activeSelf)hidden.Add(g);
                    if(hidden.Contains(g)&&!flying&&held.activeSelf)restored.Add(g);
                    string capture=null;
                    if(flying&&!held.activeSelf&&Time.time-p.actionAt>.2f&&Time.time-p.actionAt<.38f)capture="airborne_"+(g==own?"local":"opponent");
                    if(restored.Contains(g)&&!flying&&held.activeSelf)capture="returned_"+(g==own?"local":"opponent");
                    if(capture!=null&&captured.Add(capture))
                    {yield return new WaitForEndOfFrame();Capture(folder+"images/20261018_plunger_connected_"+role+"_"+capture+".png");}
                }
                if(host)
                {
                    foreach(var shot in UnityEngine.Object.FindObjectsByType<BrawlShot>().Where(s=>s.owner==game))seenKinds.Add((int)shot.kind);
                    var plungers=(IList)Get("netPlungers");foreach(var shot in plungers){seenKinds.Add(2);if((bool)shot.GetType().GetField("returning").GetValue(shot))returning.Add((int)shot.GetType().GetField("source").GetValue(shot));}
                }
                else
                {
                    int sequence=(int)Get("netLastFrameSequence");var views=(Dictionary<string,GameObject>)Get("netShotViews");var states=(IDictionary)Get("netShotMotion");
                    foreach(var pair in views.ToArray())
                    {
                        if(!pair.Value||!states.Contains(pair.Key))continue;
                        var sample=states[pair.Key].GetType().GetField("sample").GetValue(states[pair.Key]);int kind=(int)sample.GetType().GetField("kind").GetValue(sample);seenKinds.Add(kind);
                        var pos=pair.Value.transform.position;
                        if(sequence==lastSequence&&previous.TryGetValue(pair.Key,out var old)&&Time.deltaTime<.09f)
                        {var counts=Vector3.Distance(pos,old)>.0001f?moving:stationary;counts[kind]=counts.GetValueOrDefault(kind)+1;}
                        previous[pair.Key]=pos;rows.Add(FormattableString.Invariant($"{elapsed:F5},{pair.Key},{kind},{sequence},{pos.x:F5},{pos.y:F5},{pos.z:F5}"));
                        string capture="flight_"+kind;
                        if(captured.Add(capture)){yield return new WaitForEndOfFrame();Capture(folder+"images/20261018_projectile_connected_client_"+(WeaponKind)kind+".png");}
                    }
                    lastSequence=sequence;
                }
                yield return null;
            }
            void Check(bool ok,string detail)=>log.Add((ok?"PASS ":"FAIL ")+detail);
            foreach(int g in new[]{0,1}){Check(hidden.Contains(g),role+" hides airborne held plunger for global player "+g);Check(restored.Contains(g),role+" restores held plunger after return for global player "+g);}
            foreach(int kind in kinds)
            {
                Check(seenKinds.Contains(kind),role+" sees actual "+(WeaponKind)kind+" projectile");
                if(!host){int m=moving.GetValueOrDefault(kind),s=stationary.GetValueOrDefault(kind);Check(m>0&&m>=(m+s)*.9f,((WeaponKind)kind)+" movement between real packets: moving="+m+" stationary="+s);}
            }
            if(host)Check(returning.Count==2,"both authoritative plungers complete outward and returning phases");
            File.WriteAllLines(folder+"evidence/projectile-connected-"+role+".txt",log);File.WriteAllLines(folder+"evidence/projectile-connected-"+role+".csv",rows);
            Debug.Log("PROJECTILE_CONNECTED_"+role+": "+string.Join("\n",log));
            if(log.Any(s=>s.StartsWith("FAIL")))throw new Exception("CONNECTED_PROJECTILE_FAILED "+role);
        }
        finally{Application.runInBackground=background;Application.targetFrameRate=oldTarget;QualitySettings.vSyncCount=oldVsync;EditorApplication.update-=EditorApplication.QueuePlayerLoopUpdate;}
    }
}
