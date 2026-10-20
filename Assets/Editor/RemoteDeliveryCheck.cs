using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class RemoteDeliveryCheck
{
    const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Instance;
    const string D = "Docs/ChangeRecords/20261020/";
    [MenuItem("Tools/Gameplay/Special Delivery/Verify Remote Delivery")]
    public static void Verify()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Play required");
        var game = UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>(); game.StartCoroutine(Run(game));
    }
    static IEnumerator Run(MonsterDeliveryPrototype game)
    {
        var t = game.GetType(); object Get(string n) => t.GetField(n,F).GetValue(game);
        void Set(string n,object v) => t.GetField(n,F).SetValue(game,v);
        object Call(string n,params object[] a) => t.GetMethod(n,F).Invoke(game,a);
        var log = new List<string> { DateTimeOffset.Now.ToString("O"), "Actual Editor Play; controlled rays, inventory, and server records. Not separate peers or WAN." };
        void Check(bool ok,string msg) => log.Add((ok?"PASS ":"FAIL ")+msg);
        Directory.CreateDirectory(D+"images"); Directory.CreateDirectory(D+"evidence");
        var mailboxes=(GameObject[])Get("mailboxes");var original=mailboxes[0];
        var owners=(int[])Get("owner");var locks=(float[])Get("mailboxProtectedUntil");
        var player=(CharacterController)Get("player");var cam=(Camera)Get("cam");
        var oldPlayers=(MonsterDeliveryPrototype.NetPlayer[])Get("netPlayers");
        GameObject fixture=null,wall=null;
        void Place(CharacterController actor,Vector3 p) { actor.enabled=false;actor.transform.SetPositionAndRotation(p,Quaternion.identity);actor.enabled=true; }
        void Arm()
        {
            Set("selectedSlot",2);((WeaponKind[])Get("inventory"))[2]=WeaponKind.PackingMachine;
            ((int[])Get("weaponAmmo"))[2]=1;Set("nextAttackAt",0f);Set("specialUseSlot",-1);Set("playerItems",new DeliveryItemState());
        }
        try
        {
            game.NetworkScreens=false;Call("Begin");game.enabled=false;
            Place(player,new Vector3(0,151,0));Set("lookPitch",0f);Call("UpdateCamera");
            Vector3 eye=((DeliveryRobotVisual)Get("playerVisual")).EyePosition;
            fixture=GameObject.CreatePrimitive(PrimitiveType.Cube);fixture.name="Remote Capture Ray Fixture";
            fixture.transform.position=eye+Vector3.forward*10;fixture.transform.localScale=Vector3.one;
            fixture.AddComponent<InteractionTarget>();mailboxes[0]=fixture;Physics.SyncTransforms();
            Check((string)Call("ItemName",WeaponKind.PackingMachine)=="원격 배송기","display name is remote delivery while original asset ID is retained");
            Check(Resources.Load<GameObject>("DeliveryItems/PackingMachine")&&Resources.Load<Texture2D>("UI/InGame/item_PackingMachine"),"original model and icon still load");
            Arm();owners[0]=-1;locks[0]=0;Call("UseWeapon");
            Check(owners[0]==0&&((int[])Get("weaponAmmo"))[2]==0,"normal offline click instantly captures neutral mailbox and spends one use");
            Check(locks[0]>Time.time+19.9f,"remote capture starts a fresh twenty-second protection");
            Set("specialUseAt",Time.time-1);Call("UpdateSpecialPresentation");
            Check(((WeaponKind[])Get("inventory"))[2]==WeaponKind.None,"consumed model and slot clear after the existing short action");
            Arm();owners[0]=1;locks[0]=Time.time+10;
            Check(!(bool)Call("Capture",0,0),"ordinary capture still cannot bypass a protected enemy mailbox");
            Call("UseWeapon");
            Check(owners[0]==0&&locks[0]>Time.time+19.9f&&((int[])Get("weaponAmmo"))[2]==0,"remote use steals a protected enemy mailbox and renews protection");
            Arm();float saved=locks[0];Call("UseWeapon");
            Check(((int[])Get("weaponAmmo"))[2]==1&&locks[0]==saved&&(float)Get("nextAttackAt")==0,"own mailbox does not consume,rearm protection,or start cooldown");
            Arm();owners[0]=-1;fixture.transform.position=eye+Vector3.forward*37;Physics.SyncTransforms();Call("UseWeapon");
            Check(owners[0]==-1&&((int[])Get("weaponAmmo"))[2]==1,"beyond thirty-six metres is rejected without consumption");
            Arm();fixture.transform.position=eye+Vector3.forward*35;Physics.SyncTransforms();Call("UseWeapon");
            Check(owners[0]==0&&((int[])Get("weaponAmmo"))[2]==0,"extended range captures at thirty-five metres");
            owners[0]=-1;
            fixture.transform.position=eye+Vector3.forward*10;wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position=eye+Vector3.forward*5;wall.transform.localScale=new Vector3(3,3,.3f);Physics.SyncTransforms();Arm();Call("UseWeapon");
            Check(owners[0]==-1&&((int[])Get("weaponAmmo"))[2]==1,"wall blocks remote capture and keeps the item");
            UnityEngine.Object.DestroyImmediate(wall);wall=null;Arm();cam.transform.rotation=Quaternion.LookRotation(Vector3.right);Call("UseWeapon");
            Check(((int[])Get("weaponAmmo"))[2]==1&&owners[0]==-1,"empty aim does not consume the item");cam.transform.rotation=Quaternion.identity;
            var rival=((CharacterController[])Get("botControllers"))[0];rival.gameObject.SetActive(true);Place(rival,eye+Vector3.forward*5-Vector3.up*.7f);
            var states=(DeliveryItemState[])Get("botItems");states[0]=new DeliveryItemState();Physics.SyncTransforms();Arm();Call("UseWeapon");
            Check(owners[0]==-1&&((int[])Get("weaponAmmo"))[2]==1&&states[0].stunUntil==0&&states[0].packedUntil==0,"character blocks the ray but is never stunned or packed");
            Place(rival,new Vector3(10,151,0));Physics.SyncTransforms();Arm();owners[0]=-1;((DeliveryItemState)Get("playerItems")).stunUntil=Time.time+5;Call("UseWeapon");
            Check(owners[0]==-1&&((int[])Get("weaponAmmo"))[2]==1,"stunned player cannot use remote capture");
            // Exercise the same command used by host and remote inputs, with controlled server records.
            var ps=Enumerable.Range(0,4).Select(i=>new MonsterDeliveryPrototype.NetPlayer {connected=i<2,team=i==1?1:0}).ToArray();
            Set("netPlayers",ps);Set("netOwn",0);game.NetworkScreens=true;
            t.GetProperty("RoomState").SetValue(game,MonsterDeliveryPrototype.RoomPhase.Playing);
            var inputs=(MonsterDeliveryPrototype.NetInput[])Get("netInputs");inputs[0]=new MonsterDeliveryPrototype.NetInput();inputs[1]=new MonsterDeliveryPrototype.NetInput();
            foreach(int source in new[]{0,1})
            {
                Set("playerItems",new DeliveryItemState());states[0]=new DeliveryItemState();
                var p=ps[source];p.items[0]=(int)WeaponKind.PackingMachine;p.ammo[0]=1;p.selected=0;p.cooldown=0;
                var origin=(Vector3)Call("NetEye",source);fixture.transform.position=origin+Vector3.forward*30;owners[0]=1-source;locks[0]=Time.time+20;Physics.SyncTransforms();
                Call("UseNetWeapon",source);
                Check(owners[0]==source&&p.ammo[0]==0&&locks[0]>Time.time+19.9f,"server command source"+source+" steals locked enemy mailbox at thirty metres once");
                p.ammo[0]=1;p.cooldown=0;owners[0]=1-source;ps[1].team=0;Physics.SyncTransforms();Call("UseNetWeapon",source);
                Check(p.ammo[0]==1&&owners[0]==1-source,"server command source"+source+" cannot steal teammate mailbox");ps[1].team=1;
            }
            game.NetworkScreens=false;Set("netOwn",0);Set("netPlayers",oldPlayers);mailboxes[0]=original;
            UnityEngine.Object.DestroyImmediate(fixture);fixture=null;Call("Begin");
            // Actual authored mailbox and existing item visuals, using a controlled clear camera ray.
            var collider=original.GetComponentsInChildren<Collider>().First(c=>!c.isTrigger);
            Vector3 target=collider.bounds.center;Vector3[] directions={Vector3.forward,Vector3.back,Vector3.right,Vector3.left};bool found=false;
            log.Add("AUTHORED target="+target+" available="+original.GetComponent<InteractionTarget>().available+" collider="+collider.name+" owner="+owners[0]);
            foreach(var direction in directions)
            {
                player.transform.rotation=Quaternion.LookRotation(direction);
                Place(player,target-direction*8-(((DeliveryRobotVisual)Get("playerVisual")).EyePosition-player.transform.position));
                player.transform.rotation=Quaternion.LookRotation(direction);
                var from=((DeliveryRobotVisual)Get("playerVisual")).EyePosition;Physics.SyncTransforms();
                var hits=Physics.SphereCastAll(from,.01f,direction,MonsterDeliveryPrototype.RemoteDeliveryRange,~0,QueryTriggerInteraction.Ignore).Where(h=>h.collider!=player).OrderBy(h=>h.distance).Take(2);
                log.Add("RAY "+direction+" from="+from+" hits="+string.Join(",",hits.Select(h=>h.collider.name+":"+h.distance)));
                if((int)Call("RemoteCaptureTarget",0,from,direction)!=0)continue;
                player.transform.rotation=Quaternion.LookRotation(direction);Set("lookPitch",0f);Call("UpdateCamera");found=true;break;
            }
            Check(found,"authored mailbox has a clear eight-metre production aim for capture review");
            if(found)
            {
                Arm();owners[0]=1;locks[0]=Time.time+20;game.enabled=true;Call("UpdateHeldWeapon");
                yield return new WaitForSeconds(.15f);
                ScreenCapture.CaptureScreenshot(Path.GetFullPath(D+"images/20261020_remote_36m_before.png"));
                yield return new WaitForSeconds(.15f);Call("UseWeapon");
                yield return new WaitForSeconds(.1f);
                ScreenCapture.CaptureScreenshot(Path.GetFullPath(D+"images/20261020_remote_36m_capture.png"));
                yield return new WaitForSeconds(.2f);
                Check(owners[0]==0,"authored mailbox capture changes actual owner and HUD score");
            }
        }
        finally
        {
            if(wall)UnityEngine.Object.DestroyImmediate(wall);if(fixture)UnityEngine.Object.DestroyImmediate(fixture);
            mailboxes[0]=original;game.NetworkScreens=false;Set("netPlayers",oldPlayers);Set("netOwn",0);game.enabled=true;Call("Begin");
            File.WriteAllLines(D+"evidence/remote-delivery-36m-play.txt",log);
        }
        Debug.Log("REMOTE_DELIVERY_CHECK\n"+string.Join("\n",log));
    }
}
