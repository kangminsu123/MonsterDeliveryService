using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class ParcelTouchCheck
{
    [MenuItem("Tools/Gameplay/Test Parcels/Verify Touch Pickup")]
    public static void Verify()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Play required; stop Play after this isolated fixture check.");
        var game = UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        var type = game.GetType();
        object Get(string n) => type.GetField(n, flags).GetValue(game);
        void Set(string n, object v) => type.GetField(n, flags).SetValue(game, v);
        object Call(string n, params object[] args) => type.GetMethod(n, flags).Invoke(game, args);
        var report = new List<string> { "2026-10-20 Unity Editor Play; controlled local and server-method fixtures, no separate connected peers." };
        void Check(bool ok, string n) { report.Add((ok ? "PASS " : "FAIL ") + n); }
        game.enabled = false; game.NetworkScreens = false; game.mapExploreMode = true;
        Call("Begin"); Set("finished", false); Set("rollUntil", 0f); Set("playerItems", new DeliveryItemState());
        var actor = (CharacterController)Get("player");
        var items = (WeaponKind[])Get("inventory");
        void Clear() { for (int i=0;i<3;i++) Call("ClearSlot",i); }
        var fixture = GameObject.CreatePrimitive(PrimitiveType.Cube); fixture.name="TouchPickupFixture";
        fixture.transform.position = new Vector3(0,101.1f,0); fixture.transform.localScale=Vector3.one*.7f;
        var target=fixture.AddComponent<InteractionTarget>();
        var parcel=fixture.AddComponent<FloatingParcel>();
        var visual=new GameObject("FloatingVisual");visual.transform.SetParent(fixture.transform,false);parcel.floating=visual.transform;
        void ResetBox() { fixture.SetActive(true); target.available=true; }
        void Move(CharacterController a, Vector3 p) { a.gameObject.SetActive(true);a.enabled=false;a.transform.position=p;a.enabled=true;Physics.SyncTransforms(); }
        try
        {
            Clear(); Move(actor,new Vector3(0,100,2)); Set("focusedTarget",target);Set("interactionReady",true);
            Call("TryPickup");Call("AutoPickupParcels");
            Check(target.available && items.All(x=>x==WeaponKind.None),"distant regular parcel is not collected by E or proximity");
            Check(game.HudInteractionHint=="","regular parcel has no automatic pickup instruction");
            Move(actor,new Vector3(0,100,0)); actor.transform.rotation=Quaternion.Euler(0,180,0); Set("focusedTarget",null);
            Call("AutoPickupParcels");
            Check(!target.available && items.Count(x=>x!=WeaponKind.None)==1,"contact grants one normal item without focus or E");
            Call("AutoPickupParcels");Check(items.Count(x=>x!=WeaponKind.None)==1,"collected parcel cannot grant twice");
            ResetBox(); for(int i=0;i<3;i++)items[i]=WeaponKind.BoxingGloves;
            Call("AutoPickupParcels");Check(target.available && items.All(x=>x==WeaponKind.BoxingGloves),"full inventory preserves parcel and items");
            Call("ClearSlot",1);Call("AutoPickupParcels");Check(!target.available && items[1]!=WeaponKind.None,"freeing a slot while touching collects automatically");
            Clear();ResetBox();Set("playerItems",new DeliveryItemState{stunUntil=Time.time+5});Call("AutoPickupParcels");Check(target.available,"stun blocks automatic pickup");
            Set("playerItems",new DeliveryItemState());Set("rollUntil",Time.time+5);Call("AutoPickupParcels");Check(target.available,"dash blocks automatic pickup");Set("rollUntil",0f);
            game.NetworkScreens=true;Call("AutoPickupParcels");Check(target.available,"client without server authority cannot consume parcel");game.NetworkScreens=false;
            fixture.SetActive(false);
            var special=UnityEngine.Object.FindObjectsByType<SpecialDeliveryParcel>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(x=>x.fixedItem==WeaponKind.None);
            special.gameObject.SetActive(true);special.Deliver(new Vector3(0,100,0));Physics.SyncTransforms();Call("AutoPickupParcels");
            Check(special.Available && items.All(x=>x==WeaponKind.None),"touching fragile parcel does not collect it");
            Set("focusedTarget",special.GetComponent<InteractionTarget>());Set("interactionReady",true);Call("TryPickup");
            Check(!special.Available && items.Count(x=>x!=WeaponKind.None)==1,"existing E dispatch still collects fragile parcel");
            for(int g=0;g<2;g++)
            {
                var players=(MonsterDeliveryPrototype.NetPlayer[])Get("netPlayers");players[g]=new MonsterDeliveryPrototype.NetPlayer{connected=true};
                var a=(CharacterController)Call("ActorController",g);Move(a,new Vector3(0,100,0));
                if(g==0)Set("playerItems",new DeliveryItemState());else ((DeliveryItemState[])Get("botItems"))[g-1]=new DeliveryItemState();
                ResetBox();Call("NetCaptureAndPickup",g,false);
                Check(!target.available && players[g].items.Count(x=>x>0)==1,"server contact pickup without E for actor "+g);
                ResetBox();players[g].items=new[]{1,2,3};Call("NetCaptureAndPickup",g,false);Check(target.available,"server full slots preserve parcel for actor "+g);
                players[g].items=new int[3];players[g].dashRemaining=.2f;Call("NetCaptureAndPickup",g,false);Check(target.available,"server dash blocks contact for actor "+g);
            }
            fixture.SetActive(false); Clear(); Set("playerItems",new DeliveryItemState());
            var pool=UnityEngine.Object.FindAnyObjectByType<ParcelSpawnPool>();var real=pool.parcels.First(x=>x.gameObject.activeSelf);
            var camera=(Camera)Get("cam");
            camera.transform.position=real.transform.position+new Vector3(0,2,-3);
            camera.transform.LookAt(real.transform.position+Vector3.up*FloatingParcel.HoverHeight);
            AirCannonCheck.Capture("Docs/ChangeRecords/20261020/images/20261020_touch_regular_fixture.png");
            real.transform.position=new Vector3(10,100,0);real.Animate(Time.time);Physics.SyncTransforms();
            var box=real.GetComponentInChildren<BoxCollider>();
            Move(actor,box.bounds.center-actor.center);Call("AutoPickupParcels");
            Check(!real.gameObject.activeSelf && items.Count(x=>x!=WeaponKind.None)==1,"actual pooled floating model contact awards item and removes parcel");
            Check(pool.CaptureSlots()[Array.IndexOf(pool.parcels,real)]==-1,"actual pool occupancy is cleared for existing respawn synchronization");
        }
        finally
        {
            UnityEngine.Object.Destroy(fixture);
            Directory.CreateDirectory("Docs/ChangeRecords/20261020/evidence");
            File.WriteAllLines("Docs/ChangeRecords/20261020/evidence/parcel-touch-play.txt",report);
            Debug.Log("PARCEL_TOUCH: "+report.Count(x=>x.StartsWith("PASS"))+" passed, "+report.Count(x=>x.StartsWith("FAIL"))+" failed. Stop Play to restore scene state.");
        }
    }
}
