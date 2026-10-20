using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class ThrowMotionCheck
{
    const string Folder="Docs/ChangeRecords/20261018/";
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly WeaponKind[] Kinds={WeaponKind.Slime,WeaponKind.IceCream,WeaponKind.FountainPen};
    [MenuItem("Tools/Gameplay/Throws/Verify Motion And Release")]
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
        var log=new List<string>{DateTimeOffset.Now.ToString("O"),"SampleScene controlled local/network-state reproduction; actual release and projectile components."};
        void Check(bool ok,string detail){log.Add((ok?"PASS ":"FAIL ")+detail);}
        BrawlShot[] Shots()=>UnityEngine.Object.FindObjectsByType<BrawlShot>().Where(s=>s.owner==game).ToArray();
        void ClearShots(){foreach(var s in Shots())UnityEngine.Object.DestroyImmediate(s.gameObject);Call("ClearNetPlungers");}
        var player=(CharacterController)Get("player");var rival=((CharacterController[])Get("botControllers"))[0];
        var cam=(Camera)Get("cam");var visual=(DeliveryRobotVisual)Get("playerVisual");
        var remote=((DeliveryRobotVisual[])Get("botVisuals"))[0];
        var actors=new[]{player,rival};
        var players=new[]{new MonsterDeliveryPrototype.NetPlayer{connected=true,name="Thrower",team=0},new MonsterDeliveryPrototype.NetPlayer{connected=true,name="Observer",team=1},null,null};
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Throw Review Floor";floor.transform.position=new Vector3(0,149.9f,0);floor.transform.localScale=new Vector3(50,.2f,50);
        bool enabled=game.enabled,background=Application.runInBackground;
        void Equip(WeaponKind kind,bool network)
        {
            ClearShots();for(int i=0;i<3;i++)Call("ClearSlot",i);Call("AddWeapon",kind);Set("selectedSlot",0);Set("nextAttackAt",0f);Set("rollUntil",0f);
            Set("playerItems",new DeliveryItemState());game.NetworkScreens=network;game.thirdPersonPreview=false;
            players[0].items=new[]{(int)kind,0,0};players[0].selected=0;players[0].cooldown=0;players[0].actionKind=0;players[0].throwReleased=false;
            if(network)Call("SyncHostInventory");Call("PositionCamera");cam.transform.rotation=Quaternion.identity;Call("UpdateHeldWeapon");
        }
        void Age(float age,bool network)
        {
            if(!network){Set("handThrowStarted",Time.time-age);return;}
            players[0].actionAt=Time.time-age;
            var pending=(IList)Get("netHandThrows");
            if(pending.Count>0)pending[0].GetType().GetField("started").SetValue(pending[0],players[0].actionAt);
        }
        void Update(bool net)=>Call(net?"UpdateNetHandThrows":"UpdateHandThrow");
        void Pose(bool net)=>Call(net?"PoseNetworkPlayers":"PoseHandThrow");
        try
        {
            Directory.CreateDirectory(Folder+"images");Directory.CreateDirectory(Folder+"evidence");
            Application.runInBackground=true;EditorApplication.update+=EditorApplication.QueuePlayerLoopUpdate;
            game.enabled=false;Set("started",true);Set("finished",false);Set("lookPitch",0f);
            Set("netPlayers",players);Set("netOwn",0);Set("<RoomState>k__BackingField",MonsterDeliveryPrototype.RoomPhase.Playing);
            ((MonsterDeliveryPrototype.NetInput[])Get("netInputs"))[0]=new MonsterDeliveryPrototype.NetInput();
            ((MonsterDeliveryPrototype.NetInput[])Get("netInputs"))[1]=new MonsterDeliveryPrototype.NetInput();
            for(int i=0;i<2;i++){actors[i].enabled=false;actors[i].transform.position=new Vector3(i==0?0:1.4f,151,i==0?0:3);actors[i].transform.rotation=Quaternion.Euler(0,i==0?0:180,0);actors[i].enabled=true;actors[i].gameObject.SetActive(true);}
            foreach(bool network in new[]{false,true})foreach(var kind in Kinds)
            {
                string label=(network?"network":"local")+" "+kind;
                Equip(kind,network);var item=(DeliveryHeldItem)Get("heldItem");var root=(GameObject)Get("heldWeapon");
                var guard=item.transform.localPosition;
                Call(network?"UseNetWeapon":"UseWeapon",network?new object[]{0}:Array.Empty<object>());
                Check(Shots().Length==0,label+" raises hand before creating projectile");
                Age(.15f,network);Pose(network);
                Check(root.activeSelf&&item.transform.localPosition.y>guard.y+.3f,label+" held model is visible and raised in right screen space");
                yield return new WaitForEndOfFrame();Capture(Folder,"20261018_throw_"+(network?"netstate_":"local_")+kind+"_raised.png");
                Age(.215f,network);Update(network);Check(Shots().Length==0,label+" cannot release before 0.22 seconds");
                Age(.221f,network);Update(network);Pose(network);var shots=Shots();
                Check(shots.Length==1&&shots[0].kind==kind&&shots[0].sourceId==0,label+" releases exactly one authoritative projectile");
                Check(!root.activeSelf,label+" item leaves hand at release");
                var body=shots.Single().GetComponent<Rigidbody>();
                Check(kind==WeaponKind.FountainPen?!body.useGravity&&Mathf.Abs(body.linearVelocity.magnitude-48)<.1f:body.useGravity&&Mathf.Abs(body.linearVelocity.z-18)<.1f&&Mathf.Abs(body.linearVelocity.y-3)<.1f,label+" retains existing velocity and gravity");
                Update(network);Check(Shots().Length==1,label+" next update does not duplicate release");
                Age(.56f,network);Update(network);Pose(network);Check(root.activeSelf&&Vector3.Distance(item.transform.localPosition,guard)<.001f,label+" returns to guard after 0.55 seconds");
                Set("selectedSlot",1);if(network)players[0].selected=1;Update(network);
                Check(Shots().Length==1,label+" already released projectile survives slot change");
                foreach(string cancel in new[]{"slot","drop","stun","dash"})
                {
                    Equip(kind,network);Call(network?"UseNetWeapon":"UseWeapon",network?new object[]{0}:Array.Empty<object>());
                    if(cancel=="slot"){Set("selectedSlot",1);players[0].selected=1;}
                    if(cancel=="drop"){if(network)players[0].items[0]=0;else Call("DropSelectedWeapon");}
                    if(cancel=="stun")((DeliveryItemState)Get("playerItems")).stunUntil=Time.time+1;
                    if(cancel=="dash"){Set("rollUntil",Time.time+1);players[0].dashRemaining=.3f;}
                    Age(.23f,network);Update(network);Check(Shots().Length==0,label+" cancels unreleased projectile on "+cancel);players[0].dashRemaining=0;
                }
                Equip(kind,network);
            }
            foreach(var kind in Kinds)
            {
                Equip(kind,true);players[1].items=new[]{(int)kind,0,0};players[1].selected=0;players[1].actionKind=(int)kind;players[1].actionSlot=0;players[1].throwReleased=false;
                var arms=remote.model.GetComponentsInChildren<Transform>().Where(t=>t.name=="Arm.R"||t.name=="Arm.L").ToArray();var animator=remote.model.GetComponent<Animator>();
                animator.Play("IDLE_ARMED",0,0);animator.Update(0);var guard=arms.Select(t=>t.rotation).ToArray();
                players[1].actionAt=Time.time-.15f;Call("PoseNetworkPlayers");
                Check(arms.Select((t,i)=>remote.transform.InverseTransformPoint(t.position).x>0?Quaternion.Angle(guard[i],t.rotation)>95:Quaternion.Angle(guard[i],t.rotation)<.1f).All(x=>x),kind+" observer sees only right arm raised");
                var held=((DeliveryHeldItem[])Get("netHeld"))[1];var bone=remote.model.GetComponentsInChildren<Transform>().Single(t=>t.name==held.thirdPersonBone);
                Check(held.gameObject.activeSelf&&Vector3.Distance(held.transform.position,bone.TransformPoint(held.thirdPersonPosition))<.001f,kind+" raised item follows right-hand bone");
                yield return new WaitForEndOfFrame();Capture(Folder,"20261018_throw_opponent_"+kind+"_raised.png");
                animator.Update(0);players[1].actionAt=Time.time-.27f;players[1].throwReleased=true;Call("PoseNetworkPlayers");
                Check(!held.gameObject.activeSelf,kind+" released item is hidden on observer");
                animator.Update(0);players[1].actionAt=Time.time-.56f;Call("PoseNetworkPlayers");
                Check(held.gameObject.activeSelf,kind+" observer model returns after recovery");
                var state=new DeliveryItemState{shieldUntil=Time.time+8};Set("playerItems",state);
                void Impact(){if(kind==WeaponKind.IceCream)game.IceCreamImpact(player,player.bounds.center,1,Vector3.back);else if(kind==WeaponKind.Slime)game.SlimeImpact(player,player.bounds.center,1,Vector3.back);else game.FountainPenImpact(player,player.bounds.center,1,Vector3.back);}
                Impact();Check(state.shieldUntil==0&&!state.Stunned(Time.time)&&state.slowUntil<=Time.time&&state.visionBlockedUntil<=Time.time,kind+" aircap still blocks direct hit once");
                Impact();Check(kind==WeaponKind.IceCream?state.Frozen(Time.time):kind==WeaponKind.Slime?state.slowUntil>Time.time:state.visionBlockedUntil>Time.time,kind+" unshielded direct hit retains original status effect");
                if(kind==WeaponKind.IceCream){state.shieldUntil=Time.time+8;Impact();Check(state.shieldUntil>Time.time,"ice cream stun immunity still preserves aircap");}
            }
            var clone=JsonUtility.FromJson<MonsterDeliveryPrototype.NetPlayer>(JsonUtility.ToJson(players[1]));
            Check(clone.throwReleased&&clone.actionKind==(int)WeaponKind.FountainPen,"snapshot retains release phase and item kind");
        }
        finally
        {
            File.WriteAllLines(Folder+"evidence/throw-motion-check.txt",log);ClearShots();UnityEngine.Object.Destroy(floor);
            game.NetworkScreens=false;game.enabled=enabled;Application.runInBackground=background;EditorApplication.update-=EditorApplication.QueuePlayerLoopUpdate;
        }
        if(log.Any(s=>s.StartsWith("FAIL")))throw new Exception("THROW_MOTION_FAILED: "+string.Join("\n",log.Where(s=>s.StartsWith("FAIL"))));
        Debug.Log("THROW_MOTION_OK: "+log.Count(s=>s.StartsWith("PASS"))+" checks");
    }
    static void Capture(string folder,string name)
    {
        var image=ScreenCapture.CaptureScreenshotAsTexture();try{File.WriteAllBytes(folder+"images/"+name,image.EncodeToPNG());}finally{UnityEngine.Object.Destroy(image);}
    }

    [MenuItem("Tools/Gameplay/Throws/Host Connected Review")]
    public static void Host()=>StartConnected(true);
    [MenuItem("Tools/Gameplay/Throws/Join Connected Review")]
    public static void Join()=>StartConnected(false);
    static void StartConnected(bool host)
    {
        if(!EditorApplication.isPlaying)throw new Exception("Play Mode required");
        var game=UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>();game.StartCoroutine(Connected(game,host));
    }
    static IEnumerator Connected(MonsterDeliveryPrototype game,bool host)
    {
        var type=game.GetType();object Get(string n)=>type.GetField(n,Flags).GetValue(game);
        void Call(string n,params object[] a)=>type.GetMethod(n,Flags).Invoke(game,a);
        string directory=Path.GetFullPath(Application.dataPath+"/..");if(!host)directory=Path.GetFullPath(directory+"/../../..");
        string folder=Path.Combine(directory,Folder);Directory.CreateDirectory(folder+"images");Directory.CreateDirectory(folder+"evidence");
        string role=host?"host":"client";var log=new List<string>{DateTimeOffset.Now.ToString("O"),"Actual MPPM two Editor processes via localhost NGO, scripted item selection and use commands."};
        var raised=new HashSet<string>();var released=new HashSet<string>();var shotsSeen=new HashSet<string>();var captures=new HashSet<string>();
        bool background=Application.runInBackground;
        try
        {
            Application.runInBackground=true;EditorApplication.update+=EditorApplication.QueuePlayerLoopUpdate;game.NetTeamSize=1;game.NetworkScreens=true;
            if(host)game.HostRoom();else game.JoinRoom("127.0.0.1");
            float deadline=Time.realtimeSinceStartup+45;while(!game.NetPlaying&&Time.realtimeSinceStartup<deadline)yield return null;
            if(!game.NetPlaying)throw new Exception("Throw connection timeout");
            var input=(MonsterDeliveryPrototype.NetInput)Get("netLocalInput");input.slot=0;input.yaw=host?0:180;input.pitch=0;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Network Throw Review Floor";floor.transform.position=new Vector3(0,149.9f,0);floor.transform.localScale=new Vector3(80,.2f,80);
            if(host)
            {
                var players=(MonsterDeliveryPrototype.NetPlayer[])Get("netPlayers");
                for(int g=0;g<2;g++)
                {
                    players[g].items=new[]{4,5,9};players[g].selected=0;
                    var actor=(CharacterController)type.GetMethod("ActorController",Flags).Invoke(game,new object[]{g});
                    actor.enabled=false;actor.transform.position=new Vector3(g==0?0:1.4f,151,g==0?0:4);actor.enabled=true;players[g].vertical=0;
                }
                Call("NetBroadcast");
            }
            float wait=Time.realtimeSinceStartup;while(Time.realtimeSinceStartup-wait<.7f)yield return null;
            int own=(int)Get("netOwn"),other=own==0?1:0;int sent=0;float began=Time.realtimeSinceStartup;
            while(Time.realtimeSinceStartup-began<6)
            {
                float elapsed=Time.realtimeSinceStartup-began;input=(MonsterDeliveryPrototype.NetInput)Get("netLocalInput");input.slot=Mathf.Min(2,(int)(elapsed/1.4f));
                if(sent<3&&elapsed>(host?.4f:.8f)+sent*1.4f){input.use++;sent++;}
                var players=(MonsterDeliveryPrototype.NetPlayer[])Get("netPlayers");
                if(host)
                {
                    foreach(var shot in UnityEngine.Object.FindObjectsByType<BrawlShot>().Where(s=>s.owner==game))shotsSeen.Add(shot.sourceId+"_"+(int)shot.kind);
                }
                else foreach(var key in ((Dictionary<string,GameObject>)Get("netShotViews")).Keys)shotsSeen.Add(key);
                foreach(int g in new[]{own,other})
                {
                    var p=players[g];if(p.actionKind!=p.items[p.selected]||!Kinds.Contains((WeaponKind)p.actionKind))continue;
                    float age=Time.time-p.actionAt;string key=g+"_"+p.actionKind;
                    GameObject held=g==own?(GameObject)Get("heldWeapon"):((DeliveryHeldItem[])Get("netHeld"))[g]?.gameObject;
                    if(!held)continue;
                    string stage=null;
                    if(age>.13f&&age<.205f&&!p.throwReleased&&held.activeSelf){raised.Add(key);stage="raised";}
                    if(age>.25f&&age<.5f&&p.throwReleased&&!held.activeSelf){released.Add(key);stage="released";}
                    if(stage!=null&&captures.Add(key+stage))
                    {
                        log.Add("CAPTURE "+key+" "+stage+" actionAge="+age.ToString("F4")+" released="+p.throwReleased);
                        yield return new WaitForEndOfFrame();Capture(folder,"20261018_throw_connected_"+role+"_"+(g==own?"local":"opponent")+"_"+(WeaponKind)p.actionKind+"_"+stage+".png");
                    }
                }
                yield return null;
            }
            foreach(int g in new[]{own,other})foreach(var kind in Kinds)
            {
                string key=g+"_"+(int)kind;
                log.Add((raised.Contains(key)?"PASS ":"FAIL ")+role+" receives "+key+" windup with model in hand");
                log.Add((released.Contains(key)?"PASS ":"FAIL ")+role+" receives "+key+" release with model leaving hand");
            }
            log.Add((shotsSeen.Count==6?"PASS ":"FAIL ")+role+" sees six real projectile identities/source-kind pairs: "+shotsSeen.Count);
            File.WriteAllLines(folder+"evidence/throw-connected-"+role+".txt",log);Debug.Log("THROW_CONNECTED_"+role+": "+string.Join("\n",log));
        }
        finally{Application.runInBackground=background;EditorApplication.update-=EditorApplication.QueuePlayerLoopUpdate;}
    }
}
