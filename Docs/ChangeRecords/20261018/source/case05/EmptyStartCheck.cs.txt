using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class EmptyStartCheck
{
    [MenuItem("Tools/Gameplay/Inventory/Verify Empty Start")]
    public static void Verify()
    {
        if(!EditorApplication.isPlaying)throw new InvalidOperationException("Fresh Play required");
        var game=UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>();
        game.StartCoroutine(Run(game));
    }
    static IEnumerator Run(MonsterDeliveryPrototype game)
    {
        const string folder="Docs/ChangeRecords/20261018/";
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var type=game.GetType();object Get(string name)=>type.GetField(name,flags).GetValue(game);
        object Call(string name,params object[] args)=>type.GetMethod(name,flags).Invoke(game,args);
        var log=new List<string>{DateTimeOffset.Now.ToString("O"),"Fresh Editor Play; real offline/host round initialization and serialized participant frame. Four server player records are controlled fixtures, not four network connections."};
        void Check(bool ok,string message)=>log.Add((ok?"PASS ":"FAIL ")+message);
        bool Empty()=>((WeaponKind[])Get("inventory")).All(x=>x==WeaponKind.None);
        bool oldEnabled=game.enabled;
        try
        {
            game.enabled=false;EditorApplication.update+=EditorApplication.QueuePlayerLoopUpdate;
            Check(Empty(),"fresh Start has three empty slots");
            Check((int)Get("selectedSlot")==0,"fresh Start selects first empty slot");
            game.NetworkScreens=false;Call("Begin");Call("UpdateHeldWeapon");
            Check(Empty(),"offline Begin has three empty slots");
            Check(!(GameObject)Get("heldWeapon"),"offline empty start has no held item model");
            game.enabled=true;Call("UpdateCamera");yield return null;
            yield return new WaitForEndOfFrame();Capture(folder+"images/20261018_empty_start_offline.png");game.enabled=false;
            Call("AddWeapon",WeaponKind.Plunger);Check(!Empty(),"existing item grant still fills an empty slot");
            Call("Begin");Check(Empty(),"offline restart removes previous inventory");
            Check(new MonsterDeliveryPrototype.NetPlayer().items.All(x=>x==0),"new network player defaults to empty inventory");
            game.NetworkScreens=true;game.HostRoom();yield return null;
            var players=(MonsterDeliveryPrototype.NetPlayer[])Get("netPlayers");
            for(int g=1;g<4;g++)players[g]=new MonsterDeliveryPrototype.NetPlayer{connected=true,team=g%2,color=g,name="Review "+g};
            game.StartNetRound();
            Check(players.All(p=>p.items.Length==3&&p.items.All(x=>x==0)&&p.selected==0),"host round initializes all four player records empty");
            Check(Empty()&&((MonsterDeliveryPrototype.NetInput)Get("netLocalInput")).slot==0,"host inventory and input select first empty slot");
            foreach(var p in players)p.items[0]=2;
            game.StartNetRound();Check(players.All(p=>p.items.All(x=>x==0)),"network rematch clears previously acquired items for everyone");
            var frameType=type.GetNestedType("NetFrame",BindingFlags.NonPublic);var frame=Activator.CreateInstance(frameType);
            void Field(string name,object value)=>frameType.GetField(name).SetValue(frame,value);
            Field("players",players);Field("own",1);Field("owners",Enumerable.Repeat(-1,10).ToArray());
            Field("phase",(int)MonsterDeliveryPrototype.RoomPhase.Playing);Field("teamSize",2);Field("remaining",180f);
            Field("parcels",((ParcelSpawnPool)Get("parcelPool")).CaptureSlots());
            frame=JsonUtility.FromJson(JsonUtility.ToJson(frame),frameType);
            game.LeaveRoom();Field("sequence",(int)Get("netLastFrameSequence")+1);Call("ApplyNetFrame",frame);Call("UpdateHeldWeapon");Call("PoseNetworkPlayers");
            Check(Empty()&&(int)Get("netOwn")==1,"participant applies serialized empty round inventory");
            Check((int)Get("selectedSlot")==0&&((MonsterDeliveryPrototype.NetInput)Get("netLocalInput")).slot==0,"participant selects first empty slot");
            Check(!(GameObject)Get("heldWeapon"),"participant empty start has no held item model");
            game.enabled=true;Call("UpdateCamera");yield return null;
            yield return new WaitForEndOfFrame();Capture(folder+"images/20261018_empty_start_participant_frame.png");game.enabled=false;
        }
        finally
        {
            File.WriteAllLines(folder+"evidence/empty-start-check.txt",log);
            game.LeaveRoom();game.enabled=oldEnabled;EditorApplication.update-=EditorApplication.QueuePlayerLoopUpdate;
        }
        Debug.Log("EMPTY_START: "+log.Count(x=>x.StartsWith("PASS "))+" PASS / "+log.Count(x=>x.StartsWith("FAIL "))+" FAIL");
        if(log.Any(x=>x.StartsWith("FAIL ")))throw new Exception("EMPTY_START_FAILED");
    }
    static void Capture(string path)
    {
        var image=ScreenCapture.CaptureScreenshotAsTexture();
        try{File.WriteAllBytes(path,image.EncodeToPNG());}finally{UnityEngine.Object.Destroy(image);}
    }
}
