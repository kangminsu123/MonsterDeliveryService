using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class ParkDeliveryCheck
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic;
    const string D = "Docs/ChangeRecords/20261020/";
    [MenuItem("Tools/Gameplay/Special Delivery/Verify Minute Park Delivery")]
    public static void Verify()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Play required");
        var game = UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>(); game.StartCoroutine(Run(game));
    }
    static IEnumerator Run(MonsterDeliveryPrototype game)
    {
        var type = game.GetType(); object Get(string n) => type.GetField(n,F).GetValue(game);
        void Set(string n, object v) => type.GetField(n,F).SetValue(game,v);
        object Call(string n, params object[] args) => type.GetMethod(n,F).Invoke(game,args);
        var log = new List<string> { DateTimeOffset.Now.ToString("O"), "Actual Unity Editor Play. Match clocks are advanced by the verification tool. Host and serialized client snapshot paths are exercised in one Editor; no separate remote peer." };
        void Check(bool ok, string text) => log.Add((ok ? "PASS " : "FAIL ") + text);
        var random = UnityEngine.Random.state;
        Directory.CreateDirectory(D+"evidence"); Directory.CreateDirectory(D+"images");
        try
        {
            game.NetworkScreens=false; game.mapExploreMode=false; Call("Begin"); game.enabled=false;
            var parcel=(SpecialDeliveryParcel)Get("specialParcel"); var all=(SpecialDeliveryParcel[])Get("specialPickups");
            void At(float elapsed) { Set("startedAt",Time.time-elapsed); Call("UpdateParkDelivery"); }
            Check(game.roundSeconds==300 && game.HudClock=="05:00", "new normal match starts at five minutes");
            Check(!parcel.Available && !parcel.model.activeSelf && !parcel.GetComponent<BoxCollider>().enabled, "no special parcel before first minute");
            Check(all.Where(p=>p.ReplaceSelected).All(p=>!p.gameObject.activeSelf), "five fixed development pickups are hidden in actual matches");
            Check(((WeaponKind[])Get("inventory")).All(k=>k==WeaponKind.None), "actual match starts empty without the old three-weapon test loadout");
            At(59.8f); Check(!parcel.Available && (int)Get("deliveryMinute")==0, "no early delivery before sixty seconds");
            At(60.05f); var first=parcel.transform.position;
            Check(parcel.Available && (int)Get("deliveryMinute")==1, "first minute delivers exactly one active fragile parcel");
            Check((float)Get("deliveryNoticeUntil")>Time.time && MonsterDeliveryPrototype.ParkDeliveryNotice=="공원에 취급주의 택배가 도착했습니다!", "delivery shows the requested five-second announcement");
            At(60.2f); Check(parcel.transform.position==first && (int)Get("deliveryMinute")==1, "repeated frames in same minute do not respawn or move the parcel");
            var glow=parcel.GetComponent<ParkDeliveryGlow>();
            Check(glow && glow.GetComponentsInChildren<LineRenderer>().Length==26 && glow.GetComponentInChildren<Light>().range==5 && glow.GetComponentInChildren<Light>().intensity<=5.01f, "gold ring, twelve crossed sparkles and a rising spiral and warm unshadowed light are visible");
            Check(glow.GetComponentsInChildren<Collider>().All(c=>c==parcel.GetComponent<BoxCollider>()), "glow adds no gameplay colliders");
            var spark=glow.GetComponentsInChildren<LineRenderer>().First(x=>x.name=="GoldSparkle_0");var before=spark.GetPosition(0);
            yield return new WaitForSeconds(.2f);
            Check(spark.GetPosition(0)!=before, "sparkles animate around the parcel over time");
            var cam=(Camera)Get("cam"); cam.transform.position=first+new Vector3(2.5f,2,-3.2f); cam.transform.LookAt(first+Vector3.up*.4f);
            AirCannonCheck.Capture(D+"images/20261020_fragile_strong_glow.png");
            // Keep the inspection camera fixed; draw the unchanged real HUD while simulation is frozen.
            var captureHUD=game.gameObject.AddComponent<ParkDeliveryCaptureHUD>();captureHUD.game=game;
            yield return null;
            ScreenCapture.CaptureScreenshot(D+"images/20261020_fragile_strong_notice.png");
            yield return new WaitForSeconds(.3f);UnityEngine.Object.Destroy(captureHUD);
            Set("focusedTarget",parcel.GetComponent<InteractionTarget>()); Set("interactionReady",true); Call("TryPickup");
            Check(!parcel.Available && ((WeaponKind[])Get("inventory")).Count(k=>k>=WeaponKind.AirCannon)==1, "actual pickup route consumes parcel and equips one random special item");
            Check(!parcel.TryCollect(), "two collectors cannot receive a second reward from the same delivery");
            Check(!glow.GetComponentsInChildren<LineRenderer>().Any(), "light and ring disappear immediately on collection");
            At(71); Check(!parcel.Available, "old ten-second respawn is disabled in actual matches");
            At(119.8f); Check(!parcel.Available, "collected parcel waits for the next full minute");
            At(120.05f); Check(parcel.Available && (int)Get("deliveryMinute")==2, "second-minute delivery returns after collection");
            var second=parcel.transform.position; At(180.05f);
            Check(parcel.Available && (int)Get("deliveryMinute")==3 && parcel.transform.position!=second && all.Count(p=>p.gameObject.activeInHierarchy&&p.Available)==1, "uncollected parcel is relocated at third minute without accumulation");
            At(240.05f); Check(parcel.Available && (int)Get("deliveryMinute")==4, "fourth-minute delivery is active");
            At(300.01f); Check((int)Get("deliveryMinute")==4, "no fifth delivery at match expiration");
            At(299.5f);Call("Update");Check(!(bool)Get("finished"), "match remains playable just before five minutes");
            At(300.1f);Call("Update");Check((bool)Get("finished"), "normal game loop ends the match at five minutes");
            Set("finished",true); Set("deliveryMinute",0); At(60.05f); Check((int)Get("deliveryMinute")==0, "finished match cannot spawn deliveries"); Set("finished",false);
            bool valid=true; var locations=new HashSet<Vector3>();
            var paving=GameObject.Find("Paved_Plaza_and_Four_Entrances").GetComponent<MeshCollider>();
            var positionMethod=type.GetMethod("TryParkDeliveryPosition",F);
            for(int i=0;i<120;i++)
            {
                object[] args={Vector3.zero}; bool found=(bool)positionMethod.Invoke(game,args);var at=(Vector3)args[0];
                valid &= found && paving.Raycast(new Ray(at+Vector3.up*2,Vector3.down),out _,3);
                locations.Add(at);
            }
            Check(valid && locations.Count>110, "120 random positions lie on authored park paving with validated unoccupied footprints");
            var counts=new int[5];UnityEngine.Random.InitState(20261020);
            for(int i=0;i<10000;i++)counts[(int)parcel.RewardKind-(int)WeaponKind.AirCannon]++;
            Check(counts.All(n=>n>1800&&n<2200), "five rewards sampled at equal twenty-percent probability: "+string.Join(",",counts));
            Call("Begin"); Check(!parcel.Available && (int)Get("deliveryMinute")==0 && (float)Get("deliveryNoticeUntil")==0, "restart clears pending parcel and announcement");
            game.NetworkScreens=true;type.GetProperty("RoomState").SetValue(game,MonsterDeliveryPrototype.RoomPhase.Playing);Set("netRoundUntil",Time.time+240);
            Call("UpdateParkDelivery");Check(!parcel.Available && !parcel.TryCollect(), "non-authoritative client cannot spawn or collect rewards itself");
            game.NetTeamSize=1; game.HostRoom(); yield return new WaitForSeconds(.2f);
            var ps=(MonsterDeliveryPrototype.NetPlayer[])Get("netPlayers"); ps[1]=new MonsterDeliveryPrototype.NetPlayer{connected=true,team=1,name="검수 상대"};
            game.StartNetRound();
            Check(game.NetPlaying && game.HudClock=="05:00" && ps.All(p=>p==null||p.items.All(k=>k==0)), "real NGO host starts five-minute match without test loadout");
            Set("netRoundUntil",Time.time+239.9f);Call("UpdateParkDelivery");
            Check(parcel.Available && (int)Get("deliveryMinute")==1, "host clock drives first-minute delivery authority");
            var hostPosition=parcel.transform.position;Call("NetBroadcast");
            var hostActor=(CharacterController)Get("player");hostActor.enabled=false;
            hostActor.transform.position=hostPosition+new Vector3(0,.1f,-1.8f);hostActor.enabled=true;Physics.SyncTransforms();
            var aim=Quaternion.LookRotation(parcel.GetComponent<BoxCollider>().bounds.center-(Vector3)Call("NetEye",0)).eulerAngles;
            ((MonsterDeliveryPrototype.NetInput[])Get("netInputs"))[0]=new MonsterDeliveryPrototype.NetInput{yaw=aim.y,pitch=aim.x>180?aim.x-360:aim.x,capture=true};
            Call("NetCaptureAndPickup",0,true);
            Check(!parcel.Available && ps[0].items.Count(k=>k>=10&&k<=14)==1, "real host pickup ray awards exactly one random special item");
            var frameType=type.GetNestedType("NetFrame",BindingFlags.NonPublic);var frame=Activator.CreateInstance(frameType);
            void Frame(string n,object v)=>frameType.GetField(n).SetValue(frame,v);
            Frame("sequence",999999);Frame("own",0);Frame("teamSize",1);Frame("phase",(int)MonsterDeliveryPrototype.RoomPhase.Playing);Frame("remaining",239f);
            Frame("players",ps);Frame("owners",Enumerable.Repeat(-1,10).ToArray());Frame("captureLocks",new float[10]);
            Frame("specialPickupRemaining",new float[all.Length]);Frame("deliveryAvailable",true);Frame("deliveryPosition",hostPosition);Frame("deliveryMinute",1);Frame("deliveryNotice",4f);
            string json=JsonUtility.ToJson(frame); var decoded=JsonUtility.FromJson(json,frameType);
            parcel.SetAvailable(false);parcel.transform.position=Vector3.zero;Call("ApplyNetFrame",decoded);
            Check(parcel.Available && parcel.transform.position==hostPosition && (float)Get("deliveryNoticeUntil")>Time.time, "JSON frame round-trip restores authoritative random location, availability and announcement");
            Frame("sequence",1000000);Frame("deliveryAvailable",false);Frame("deliveryNotice",0f);Call("ApplyNetFrame",JsonUtility.FromJson(JsonUtility.ToJson(frame),frameType));
            Check(!parcel.Available && !parcel.model.activeSelf, "collection snapshot hides client parcel and glow without auto-respawn");
            var manager=(Unity.Netcode.NetworkManager)Get("netManager");Check(manager.NetworkConfig.ProtocolVersion==15, "protocol fifteen prevents old snapshot clients from mixing with the new rules");
            File.WriteAllText(D+"evidence/park-delivery-strong-frame.json",json);
        }
        finally
        {
            UnityEngine.Random.state=random;
            File.WriteAllLines(D+"evidence/park-delivery-strong-play.txt",log);
            game.enabled=true;
        }
        game.LeaveRoom();yield return new WaitForSeconds(.5f);
        game.NetworkScreens=false;game.mapExploreMode=false;Call("Begin");
        Debug.Log("PARK_DELIVERY_CHECK\n"+string.Join("\n",log));
    }
}

public sealed class ParkDeliveryCaptureHUD : MonoBehaviour
{
    public MonsterDeliveryPrototype game;
    void OnGUI()
    {
        if(game && !game.enabled) typeof(MonsterDeliveryPrototype).GetMethod("DrawGameplayHUD",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,null);
    }
}
