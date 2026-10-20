using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class CornerSpawnCheck
{
    [MenuItem("Tools/Gameplay/Map Layout/Verify Random Corner Starts")]
    public static void Verify()
    {
        if(!EditorApplication.isPlaying)throw new Exception("Fresh Play required.");
        var game=UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>();game.StartCoroutine(Run(game));
    }
    static IEnumerator Run(MonsterDeliveryPrototype game)
    {
        const string folder="Docs/ChangeRecords/20261018/";
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var type=game.GetType();object Get(string n)=>type.GetField(n,flags).GetValue(game);
        void Set(string n,object v)=>type.GetField(n,flags).SetValue(game,v);
        object Call(string n,params object[] args)=>type.GetMethod(n,flags).Invoke(game,args);
        var log=new List<string>{DateTimeOffset.Now.ToString("O"),"Actual authored scene and Play initialization. Multiplayer rounds use controlled connected player records and a serialized participant frame, not separate processes."};
        void Check(bool ok,string message){log.Add((ok?"PASS ":"FAIL ")+message);}
        var corners=GameObject.Find("Layer_13_PlayerSpawns").transform.Cast<Transform>().Select(t=>t.position).ToArray();
        var player=(CharacterController)Get("player");var actors=new[]{player}.Concat((CharacterController[])Get("botControllers")).ToArray();
        int Corner(Vector3 p)=>Array.FindIndex(corners,c=>Vector3.Distance(c,p)<.1f);
        var random=UnityEngine.Random.state;bool oldEnabled=game.enabled;
        try
        {
            game.enabled=false;EditorApplication.update+=EditorApplication.QueuePlayerLoopUpdate;
            Check(Corner(player.transform.position)>=0,"fresh Play player starts at one of the saved map corners");
            Check(corners.Length==4&&corners.Select(p=>new Vector2(Mathf.Sign(p.x),Mathf.Sign(p.z))).Distinct().Count()==4,"four saved spawn regions cover all map quadrants");
            foreach(var point in corners)Check(RoundLayoutSetup.SafePlayerFloor(point-Vector3.up),"corner has supported floor and vegetation/capsule clearance "+point.ToString("F2"));
            UnityEngine.Random.InitState(20261018);int[,] hits=new int[4,4];bool distinct=true;
            for(int round=0;round<400;round++)
            {
                Call("ChooseRoundSpawns");var positions=(Vector3[])Get("roundSpawns");distinct&=positions.Distinct().Count()==4;
                for(int id=0;id<4;id++){int corner=Corner(positions[id]);if(corner<0)distinct=false;else hits[id,corner]++;}
            }
            Check(distinct,"400 allocations use saved corner positions without overlapping players");
            for(int id=0;id<4;id++)Check(Enumerable.Range(0,4).All(c=>hits[id,c]>0),"player"+id+" samples all four corners across400 seeded allocations");
            File.WriteAllLines(folder+"evidence/corner-random-distribution.csv",new[]{"player,south_west,north_west,north_east,south_east"}.Concat(Enumerable.Range(0,4).Select(id=>id+","+string.Join(",",Enumerable.Range(0,4).Select(c=>hits[id,c])))));
            game.NetworkScreens=false;bool starts=true;
            for(int round=0;round<40;round++)
            {
                Call("Begin");starts&=actors.All(a=>Corner(a.transform.position)>=0)&&actors.Select(a=>a.transform.position).Distinct().Count()==4;
                starts&=((WeaponKind[])Get("inventory")).All(k=>k==WeaponKind.None);
                starts&=Vector3.Distance(((DeliveryItemState)Get("playerItems")).safePosition,player.transform.position)<.01f;
            }
            Check(starts,"40 actual offline restarts place actors at distinct corners with empty inventory and matching safe position");
            foreach(var actor in actors.Skip(1))actor.gameObject.SetActive(false);
            for(int i=0;i<4;i++)
            {
                player.enabled=false;player.transform.position=corners[i];var inward=-corners[i];inward.y=0;player.transform.rotation=Quaternion.LookRotation(inward);player.enabled=true;
                Set("playerItems",new DeliveryItemState{safePosition=corners[i]});Set("vertical",0f);Set("lookPitch",12f);Call("UpdateCamera");
                var groundArgs=new object[]{player,corners[i],default(RaycastHit)};
                bool hasGround=(bool)type.GetMethod("TryHoverGround",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,groundArgs);
                log.Add("INFO corner"+(i+1)+" hover query="+hasGround+" deltaTime="+Time.deltaTime+" slopeLimit="+player.slopeLimit+" hits="+string.Join(";",Physics.RaycastAll(corners[i]+Vector3.up*.3f,Vector3.down,1.6f,~0,QueryTriggerInteraction.Ignore).Select(h=>h.collider.name+" point="+h.point.ToString("F3")+" normal="+h.normal.ToString("F2")+" interaction="+(bool)h.collider.GetComponentInParent<InteractionTarget>())));
                for(int n=0;n<60;n++)Call("UpdatePlayer",new object[]{null});
                Check(Vector3.Distance(player.transform.position,corners[i])<.08f,"corner"+(i+1)+" remains grounded through60 native movement updates; actual="+player.transform.position.ToString("F3")+" expected="+corners[i].ToString("F3"));
                player.enabled=false;player.transform.position=corners[i];player.enabled=true;
                var hitchArgs=new object[]{player,Vector3.down*2.5f,-20f};type.GetMethod("MoveHoverActor",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,hitchArgs);
                Check(Vector3.Distance(player.transform.position,corners[i])<.08f,"corner"+(i+1)+" keeps hover clearance during a2.5m descending hitch step");
                game.enabled=true;yield return null;yield return new WaitForEndOfFrame();Capture(folder+"images/20261018_player_corner_"+(i+1)+".png");game.enabled=false;
            }
            game.NetworkScreens=true;game.HostRoom();yield return null;
            var players=(MonsterDeliveryPrototype.NetPlayer[])Get("netPlayers");
            for(int g=1;g<4;g++)players[g]=new MonsterDeliveryPrototype.NetPlayer{connected=true,team=g%2,color=g,name="Review "+g};
            foreach(int count in new[]{2,4})
            {
                game.NetTeamSize=count/2;for(int g=0;g<4;g++)players[g].connected=g<count;
                bool rounds=true;var seen=new HashSet<int>();
                for(int round=0;round<80;round++)
                {
                    game.StartNetRound();var active=actors.Take(count).ToArray();
                    rounds&=active.All(a=>Corner(a.transform.position)>=0)&&active.Select(a=>a.transform.position).Distinct().Count()==count;
                    for(int g=0;g<count;g++)
                    {
                        seen.Add(Corner(actors[g].transform.position));
                        rounds&=Vector3.Distance(players[g].position,actors[g].transform.position)<.01f&&players[g].items.All(k=>k==0)&&players[g].ammo.All(n=>n==0);
                        rounds&=Mathf.Abs(Mathf.DeltaAngle(((MonsterDeliveryPrototype.NetInput[])Get("netInputs"))[g].yaw,actors[g].transform.eulerAngles.y))<.1f;
                    }
                }
                Check(rounds,count+"-player80 actual host rounds keep distinct corner positions, yaw and empty inventory");
                Check(seen.Count==4,count+"-player host rounds select all four map corners");
            }
            var pool=UnityEngine.Object.FindAnyObjectByType<ParcelSpawnPool>();
            var frameType=type.GetNestedType("NetFrame",BindingFlags.NonPublic);var frame=Activator.CreateInstance(frameType);
            void Field(string n,object v)=>frameType.GetField(n).SetValue(frame,v);
            Field("players",players);Field("own",1);Field("phase",(int)MonsterDeliveryPrototype.RoomPhase.Playing);Field("teamSize",2);Field("remaining",180f);Field("owners",Enumerable.Repeat(-1,10).ToArray());Field("captureLocks",new float[10]);Field("parcels",pool.CaptureSlots());
            var expected=players[1].position;float yaw=players[1].yaw;frame=JsonUtility.FromJson(JsonUtility.ToJson(frame),frameType);
            game.LeaveRoom();Field("sequence",(int)Get("netLastFrameSequence")+1);Call("ApplyNetFrame",frame);
            Check(Vector3.Distance(player.transform.position,expected)<.01f&&Corner(player.transform.position)>=0,"participant frame uses the server corner, not an independent local random choice");
            Check(Mathf.Abs(Mathf.DeltaAngle(((MonsterDeliveryPrototype.NetInput)Get("netLocalInput")).yaw,yaw))<.1f,"participant camera input starts with the server inward yaw");
            Check(Vector3.Distance(((DeliveryItemState)Get("playerItems")).safePosition,expected)<.01f,"participant fall recovery starts at the server corner");
            Check(pool.ActiveCount==50&&pool.CaptureSlots().Length==50,"participant frame applies the full50-parcel layout");
        }
        finally
        {
            File.WriteAllLines(folder+"evidence/corner-spawn-play-check.txt",log);game.LeaveRoom();game.enabled=oldEnabled;
            UnityEngine.Random.state=random;EditorApplication.update-=EditorApplication.QueuePlayerLoopUpdate;
        }
        Debug.Log("CORNER_STARTS: "+log.Count(x=>x.StartsWith("PASS "))+" PASS / "+log.Count(x=>x.StartsWith("FAIL "))+" FAIL");
        if(log.Any(x=>x.StartsWith("FAIL ")))throw new Exception("CORNER_SPAWN_CHECK_FAILED");
    }
    static void Capture(string path){var image=ScreenCapture.CaptureScreenshotAsTexture();try{File.WriteAllBytes(path,image.EncodeToPNG());}finally{UnityEngine.Object.Destroy(image);}}
}
