using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;

public static class SpecialDeliveryCheck
{
    const BindingFlags F=BindingFlags.NonPublic|BindingFlags.Instance;
    const string D="Docs/ChangeRecords/20261019/";
    [MenuItem("Tools/Gameplay/Special Delivery/Capture Equipped Views")]
    public static void CaptureViews()
    {
        if(!EditorApplication.isPlaying)throw new Exception("Play first");
        var game=UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>();game.StartCoroutine(EquippedViews(game));
    }
    static System.Collections.IEnumerator EquippedViews(MonsterDeliveryPrototype game)
    {
        ShowPark();var type=game.GetType();
        for(int slot=0;slot<3;slot++)
        {
            type.GetField("selectedSlot",F).SetValue(game,slot);
            yield return new WaitForSecondsRealtime(.3f);
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(D+"images/20261019_special_equipped_"+(10+slot)+".png"));
            yield return new WaitForSecondsRealtime(.3f);
        }
        type.GetField("selectedSlot",F).SetValue(game,0);
    }
    [MenuItem("Tools/Gameplay/Special Delivery/Verify Offline Test")]
    public static void Verify()
    {
        if(!EditorApplication.isPlaying)throw new Exception("Fresh Play required");
        var game=UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>();var t=game.GetType();
        object Get(string n)=>t.GetField(n,F).GetValue(game);void Set(string n,object v)=>t.GetField(n,F).SetValue(game,v);
        object Call(string n,params object[] a)=>t.GetMethod(n,F).Invoke(game,a);
        var log=new List<string>{DateTimeOffset.Now.ToString("O"),"Actual Play, controlled positions and direct production calls; not separate network peers."};
        void Check(bool ok,string msg)=>log.Add((ok?"PASS ":"FAIL ")+msg);
        game.enabled=false;game.NetworkScreens=false;Call("Begin");
        var parcel=UnityEngine.Object.FindObjectsByType<SpecialDeliveryParcel>(FindObjectsSortMode.None).Single(p=>p.fixedItem==WeaponKind.None);
        var inv=(WeaponKind[])Get("inventory");var ammo=(int[])Get("weaponAmmo");
        Check(inv.Select(x=>(int)x).SequenceEqual(new[]{10,11,12})&&ammo.All(a=>a==1),"offline test starts with cannon, broadcaster and packer, one use each");
        Check(UnityEngine.Object.FindObjectsByType<SpecialDeliveryParcel>(FindObjectsSortMode.None).Count(p=>p.fixedItem==WeaponKind.None)==1,"one park special parcel");
        Check(GameObject.Find("Layer_12_TestParcelDrops").GetComponentsInChildren<FloatingParcel>(true).Length==50,"ordinary fifty parcel pool retained");
        parcel.testLoadout=false;Call("Begin");Check(inv.All(x=>x==WeaponKind.None),"disabling test loadout restores normal empty start");
        parcel.testLoadout=true;Call("Begin");
        var actors=new[]{(CharacterController)Get("player"),((CharacterController[])Get("botControllers"))[0]};
        var states=(DeliveryItemState[])Get("botItems");
        void Reset()
        {
            for(int id=0;id<2;id++){actors[id].gameObject.SetActive(true);actors[id].enabled=false;actors[id].transform.SetPositionAndRotation(new Vector3(0,151,id*8),Quaternion.identity);actors[id].enabled=true;}
            foreach(var bot in ((CharacterController[])Get("botControllers")).Skip(1))if(bot)bot.gameObject.SetActive(false);
            Set("playerItems",new DeliveryItemState());states[0]=new DeliveryItemState();Physics.SyncTransforms();
        }
        void Fire(int kind) { if(kind==10)game.AirCannonImpact(actors[0].bounds.center+Vector3.forward*4,0,Vector3.up); else Call("UseSpecialWeapon",0,(WeaponKind)kind,actors[0].bounds.center,Vector3.forward); }
        GameObject wall=null;
        try
        {
            for(int kind=10;kind<=12;kind++)
            {
                Reset();Fire(kind);
                Check(kind==10?states[0].force.z>59&&states[0].pendingLift==18:Mathf.Abs(states[0].stunUntil-Time.time-(kind==11?3:4))<.03f,((WeaponKind)kind)+" production hit uses test effect");
                if(kind==12){Check(states[0].packedUntil==states[0].stunUntil,"packer visual shares authoritative stun deadline");Call("UpdateSpecialPresentation");Check(actors[1].GetComponentsInChildren<Transform>().Any(x=>x.name=="PackingEffect"),"packer creates imported cardboard effect");}
                Reset();states[0].shieldUntil=Time.time+8;Fire(kind);
                Check(states[0].shieldUntil==0&&!states[0].Stunned(Time.time)&&states[0].pendingLift==0,((WeaponKind)kind)+" shared aircap blocks effect");
                Reset();wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=new Vector3(0,151,kind==10?6:4);wall.transform.localScale=new Vector3(5,5,.4f);Physics.SyncTransforms();Fire(kind);
                Check(!states[0].Stunned(Time.time)&&states[0].pendingLift==0,((WeaponKind)kind)+" wall blocks hit");UnityEngine.Object.DestroyImmediate(wall);wall=null;
                if(kind>10){Reset();states[0].stunImmuneUntil=Time.time+5;Fire(kind);Check(!states[0].Stunned(Time.time)&&states[0].packedUntil==0,((WeaponKind)kind)+" shared stun immunity prevents re-lock");}
                Set("selectedSlot",kind-10);Set("nextAttackAt",0f);Set("playerItems",new DeliveryItemState());
                Call("UseWeapon");Check(ammo[kind-10]==0,((WeaponKind)kind)+" normal offline click consumes exactly once");
                Set("specialUseAt",Time.time-1);Call("UpdateSpecialPresentation");Check(inv[kind-10]==WeaponKind.None,((WeaponKind)kind)+" final-use model and slot clear after recoil");
                Check(Resources.Load<GameObject>("DeliveryItems/"+(WeaponKind)kind)&&Resources.Load<Texture2D>("UI/InGame/item_"+(WeaponKind)kind),((WeaponKind)kind)+" held model and HUD icon load");
            }
            Reset();Call("Begin");Set("selectedSlot",0);
            Set("focusedTarget",parcel.GetComponent<InteractionTarget>());Set("interactionReady",true);Call("TryPickup");
            Check(parcel.Available,"full three slots cannot consume park parcel");
            Call("ClearSlot",1);Call("TryPickup");
            Check(!parcel.Available&&parcel.Remaining>9.8f&&inv[1]>=WeaponKind.AirCannon,"E pickup grants one strong item and hides parcel for ten seconds");
            Check(!parcel.TryCollect(),"second collection cannot duplicate reward");
            parcel.ApplySnapshot(.02f);
            // Timer expiration is exercised by the connected test; the snapshot itself must stay hidden until authoritative update.
            Check(!parcel.Available,"positive authoritative snapshot hides model and collider");parcel.ResetRound();
            Check(parcel.Available&&parcel.model.activeSelf&&parcel.GetComponent<BoxCollider>().enabled,"round reset restores exactly one visible collectible");
        }
        finally
        {
            if(wall)UnityEngine.Object.DestroyImmediate(wall);
            Call("Begin");game.enabled=true;
            Directory.CreateDirectory(D+"evidence");File.WriteAllLines(D+"evidence/special-offline.txt",log);
        }
        Debug.Log("SPECIAL_OFFLINE_CHECK\n"+string.Join("\n",log));
    }

    [MenuItem("Tools/Gameplay/Special Delivery/Show Park Test")]
    public static void ShowPark()
    {
        var game=UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>();
        if(!EditorApplication.isPlaying||!game)throw new Exception("Play first");
        game.NetworkScreens=false;game.mapExploreMode=true;game.thirdPersonPreview=false;
        var t=game.GetType();t.GetMethod("Begin",F).Invoke(game,null);
        var parcel=UnityEngine.Object.FindObjectsByType<SpecialDeliveryParcel>(FindObjectsSortMode.None).Single(p=>p.fixedItem==WeaponKind.None);
        var player=(CharacterController)t.GetField("player",F).GetValue(game);
        player.enabled=false;player.transform.SetPositionAndRotation(parcel.transform.position+new Vector3(0,1,-3.7f),Quaternion.identity);player.enabled=true;
        t.GetField("lookPitch",F).SetValue(game,13f);t.GetMethod("UpdateCamera",F).Invoke(game,null);
        Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        Directory.CreateDirectory(D+"images");ScreenCapture.CaptureScreenshot(Path.GetFullPath(D+"images/20261019_special_park_test.png"));
    }
}
