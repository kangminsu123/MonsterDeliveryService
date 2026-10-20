using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class SpecialItemRowCheck
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    const string D="Docs/ChangeRecords/20261019/";
    [MenuItem("Tools/Gameplay/Special Delivery/Verify Five Item Row")]
    public static void Verify()
    {
        if(!EditorApplication.isPlaying)throw new Exception("Fresh Play required");
        var game=UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>();var type=game.GetType();
        object Get(string n)=>type.GetField(n,F).GetValue(game);void Set(string n,object v)=>type.GetField(n,F).SetValue(game,v);
        object Call(string n,params object[] a)=>type.GetMethod(n,F|BindingFlags.Static).Invoke(game,a);
        var log=new List<string>{DateTimeOffset.Now.ToString("O"),"Actual Play calls, controlled inventory and unobstructed movement fixture. Network prediction uses production SimulateNetMovement. Separate peers verified separately."};
        void Check(bool ok,string s)=>log.Add((ok?"PASS ":"FAIL ")+s);
        var all=UnityEngine.Object.FindObjectsByType<SpecialDeliveryParcel>(FindObjectsSortMode.None).OrderBy(p=>p.spawnId).ToArray();
        var row=all.Where(p=>p.ReplaceSelected).ToArray();
        var inventory=(WeaponKind[])Get("inventory");var ammo=(int[])Get("weaponAmmo");
        bool enabled=game.enabled;GameObject floor=null,body=null;
        try
        {
            game.enabled=false;game.NetworkScreens=false;Call("Begin");
            Check(all.Length==6&&row.Length==5&&row.Select(p=>(int)p.fixedItem).SequenceEqual(new[]{10,11,12,13,14}),"one special parcel plus all five fixed item pickups");
            Check(row.All(p=>Mathf.Abs(p.transform.position.z-24.8f)<.01f)&&row.Select(p=>p.transform.position.x).Distinct().Count()==5,"five pickups saved in one straight horizontal park row");
            Check(row.All(p=>p.respawnSeconds==2),"fixed test pickups respawn after two seconds");
            Check(row.Select(p=>p.spawnId).SequenceEqual(new[]{1,2,3,4,5}),"stable pickup IDs for cross-peer snapshot ordering");
            foreach(var pickup in row)
            {
                Call("Begin");Set("selectedSlot",1);Set("focusedTarget",pickup.GetComponent<InteractionTarget>());Set("interactionReady",true);Call("TryPickup");
                Check(inventory[1]==pickup.fixedItem&&ammo[1]==1&&(int)Get("selectedSlot")==1,pickup.fixedItem+" replaces selected full slot and immediately equips one use");
                Check(!pickup.Available&&pickup.Remaining>1.8f&&!pickup.model.activeSelf,pickup.fixedItem+" model and collider hidden until authoritative two-second respawn");
                Check(Resources.Load<GameObject>("DeliveryItems/"+pickup.fixedItem)&&Resources.Load<Texture2D>("UI/InGame/item_"+pickup.fixedItem),pickup.fixedItem+" held prefab and icon load");
                Set("nextAttackAt",0f);Call("UseWeapon");
                if(pickup.fixedItem==WeaponKind.PackingMachine)
                { Check(ammo[1]==1&&inventory[1]==WeaponKind.PackingMachine,"remote delivery invalid aim keeps its single use; successful capture verified by RemoteDeliveryCheck"); continue; }
                Check(ammo[1]==0,pickup.fixedItem+" ordinary use click spends exactly one use");
                var state=(DeliveryItemState)Get("playerItems");
                if(pickup.fixedItem==WeaponKind.LifeJacket)Check(Mathf.Abs(state.immuneUntil-Time.time-5)<.01f,"life jacket activates five seconds");
                if(pickup.fixedItem==WeaponKind.ExpressBattery)Check(Mathf.Abs(state.batteryUntil-Time.time-8)<.01f&&(float)Get("stamina")==100,"battery activates eight seconds and full stamina");
                Set("specialUseAt",Time.time-1);Call("UpdateSpecialPresentation");
                Check(inventory[1]==WeaponKind.None,pickup.fixedItem+" last-use slot clears after presentation");
            }
            float now=Time.time;
            var immune=new DeliveryItemState{immuneUntil=now+5,shieldUntil=now+20};
            foreach(var hit in new[]{new DeliveryItemHit(Vector3.forward*60+Vector3.up*18),new DeliveryItemHit(stunSeconds:5),new DeliveryItemHit(slowSeconds:5,shieldable:false),new DeliveryItemHit(inkSeconds:5),new DeliveryItemHit(Vector3.back*14)})
                Check(immune.ReceiveHit(now,hit)==DeliveryHitResult.Ignored&&!immune.Stunned(now)&&immune.force==Vector3.zero&&immune.pendingLift==0&&immune.slowUntil==0&&immune.visionBlockedUntil==0,"life jacket ignores repeated incoming hit including unshieldable area; payload "+hit.stunSeconds+"/"+hit.slowSeconds+"/"+hit.inkSeconds);
            Check(Mathf.Abs(immune.shieldUntil-(now+20))<.001f,"life jacket does not consume underlying one-hit aircap; difference="+(immune.shieldUntil-(now+20)));
            immune.shieldUntil=0;Check(immune.ReceiveHit(now+5.01f,new DeliveryItemHit(stunSeconds:1))==DeliveryHitResult.Applied,"incoming effects resume after five-second immunity expires");
            var battery=new DeliveryItemState{batteryUntil=now+8};
            Check(Mathf.Abs(battery.MoveMultiplier(now)-1.3f)<.001f&&battery.MoveMultiplier(now+8.01f)==1,"battery gives exactly thirty percent speed and expires at eight seconds");
            battery.speedUntil=now+3;Check(battery.MoveMultiplier(now)==1.5f,"running shoes and battery use stronger speed rather than multiplying buffs");battery.speedUntil=0;
            floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(0,149.9f,0);floor.transform.localScale=new Vector3(150,.2f,150);
            body=new GameObject("Battery movement fixture");var controller=body.AddComponent<CharacterController>();Call("ConfigureHoverBody",controller);controller.transform.position=new Vector3(20,151,0);battery.safePosition=controller.transform.position;Physics.SyncTransforms();
            var p=new MonsterDeliveryPrototype.NetPlayer{stamina=1};var input=new MonsterDeliveryPrototype.NetInput{z=1};
            for(int frame=0;frame<60;frame++)Call("SimulateNetMovement",controller,battery,p,input,1f/60,true);
            Check(Mathf.Abs(controller.transform.position.z-8.45f)<.08f,"network authoritative walking travels8.45m in one second under thirty-percent boost");
            for(int dash=1;dash<=5;dash++){p.dashRemaining=0;input.dash=dash;Call("SimulateNetMovement",controller,battery,p,input,1f/60,true);}
            Check(p.stamina==100&&p.dashRemaining>0,"five authoritative network dashes keep full battery stamina");
            var local=(DeliveryItemState)Get("playerItems");local.batteryUntil=Time.time+8;Set("stamina",1f);Set("rollUntil",0f);
            if(Keyboard.current!=null){InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.LeftShift));InputSystem.Update();Call("UpdatePlayer",Keyboard.current);}
            Check((float)Get("stamina")==100&&(float)Get("rollUntil")>Time.time,"real offline Shift input dashes without stamina cost during battery");
            var botState=((DeliveryItemState[])Get("botItems"))[0];botState.shieldUntil=botState.speedUntil=0;
            game.NetworkScreens=true;Set("nextRivalBubbleTest",0f);Call("UpdateBubbleWrapFX");
            Check(botState.shieldUntil==0,"offline rival demo cannot grant aircap to a network opponent");
            bool shoesDemo=(bool)Get("rivalShoesPlaytest");Set("rivalShoesPlaytest",true);Set("nextRivalShoesTest",0f);Call("UpdateRunningShoesFX");
            Check(botState.speedUntil==0,"offline rival demo cannot grant running-shoes buff in network mode");
            Set("rivalShoesPlaytest",shoesDemo);game.NetworkScreens=false;
        }
        finally
        {
            if(Keyboard.current!=null){InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());InputSystem.Update();}
            if(floor)UnityEngine.Object.DestroyImmediate(floor);if(body)UnityEngine.Object.DestroyImmediate(body);
            Call("Begin");game.enabled=enabled;
            File.WriteAllLines(D+"evidence/special-row-offline.txt",log);
        }
        Debug.Log("SPECIAL_ROW_OFFLINE\n"+string.Join("\n",log));
    }
    [MenuItem("Tools/Gameplay/Special Delivery/Show Five Item Row")]
    public static void ShowRow()
    {
        if(!EditorApplication.isPlaying)throw new Exception("Play first");
        var game=UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>();var type=game.GetType();game.NetworkScreens=false;game.mapExploreMode=true;
        type.GetMethod("Begin",F).Invoke(game,null);
        var player=(CharacterController)type.GetField("player",F).GetValue(game);
        player.enabled=false;player.transform.SetPositionAndRotation(new Vector3(.45f,1,19.2f),Quaternion.identity);player.enabled=true;
        type.GetField("lookPitch",F).SetValue(game,8f);type.GetMethod("UpdateCamera",F).Invoke(game,null);
        Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        game.StartCoroutine(CaptureRow(game));
    }
    static System.Collections.IEnumerator CaptureRow(MonsterDeliveryPrototype game)
    {
        yield return new WaitForSecondsRealtime(.2f);
        var held=(GameObject)game.GetType().GetField("heldWeapon",F).GetValue(game);
        if(held)held.SetActive(false);yield return null;
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(D+"images/20261019_special_five_item_row.png"));
        yield return new WaitForSecondsRealtime(.3f);if(held)held.SetActive(true);
    }
}
