using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class EnhancedCombatCheck
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    [MenuItem("Tools/Gameplay/Combat/Verify Enhanced Effects")]
    public static void Verify()
    {
        if(!EditorApplication.isPlaying)throw new Exception("Fresh Play required");
        var game=UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>();game.StartCoroutine(Run(game));
    }
    static IEnumerator Run(MonsterDeliveryPrototype game)
    {
        const string folder="Docs/ChangeRecords/20261018/";
        var t=game.GetType();object Get(string n)=>t.GetField(n,Flags).GetValue(game);
        void Set(string n,object v)=>t.GetField(n,Flags).SetValue(game,v);
        object Call(string n,params object[] a)=>t.GetMethod(n,Flags).Invoke(game,a);
        float Value(string n)=>Convert.ToSingle(t.GetField(n,BindingFlags.Static|BindingFlags.NonPublic).GetValue(null));
        var log=new List<string>{DateTimeOffset.Now.ToString("O"),"Actual item use and CharacterController movement on controlled flat floor; network uses host initialized records. Separate-peer test is recorded separately."};
        void Check(bool ok,string s)=>log.Add((ok?"PASS ":"FAIL ")+s);
        var player=(CharacterController)Get("player");var actors=((CharacterController[])Get("botControllers"));var target=actors[0];
        var states=(DeliveryItemState[])Get("botItems");var cam=(Camera)Get("cam");
        bool oldEnabled=game.enabled;GameObject floor=null;
        void Place(float distance=3)
        {
            player.enabled=false;player.transform.SetPositionAndRotation(new Vector3(0,151,-distance),Quaternion.identity);player.enabled=true;
            target.gameObject.SetActive(true);target.enabled=false;target.transform.SetPositionAndRotation(new Vector3(0,151,0),Quaternion.identity);target.enabled=true;
            Set("playerItems",new DeliveryItemState());states[0]=new DeliveryItemState();
            cam.transform.SetPositionAndRotation(((DeliveryRobotVisual)Get("playerVisual")).EyePosition,Quaternion.identity);Physics.SyncTransforms();
        }
        try
        {
            game.enabled=false;game.NetworkScreens=false;Set("started",true);Set("finished",false);EditorApplication.update+=EditorApplication.QueuePlayerLoopUpdate;
            foreach(var a in actors)a.gameObject.SetActive(false);
            floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Enhanced Combat Review Floor";floor.transform.position=new Vector3(0,149.9f,0);floor.transform.localScale=new Vector3(200,.2f,200);
            Check(Mathf.Abs(Value("GlovePushSpeed")-44.0908f)<.001f,"shared glove horizontal impulse44.0908");
            Check(Mathf.Abs(Value("GloveLiftSpeed")-14.6969f)<.001f,"shared glove lift impulse14.6969");
            Place();Call("HitGloveTarget",Value("GlovePushSpeed"),Value("GloveLiftSpeed"));
            Check(Mathf.Abs(states[0].force.magnitude-Value("GlovePushSpeed"))<.01f,"actual offline glove hit applies enhanced horizontal impulse");
            Check(Mathf.Abs(states[0].pendingLift-Value("GloveLiftSpeed"))<.01f,"actual offline glove hit applies enhanced lift");
            var move=t.GetMethod("MoveHoverActor",BindingFlags.NonPublic|BindingFlags.Static);var rows=new List<string>{"variant,time,x,y,z"};
            Vector2 Travel(float push,float lift,string label)
            {
                Place();target.transform.position=new Vector3(20,151,0);Physics.SyncTransforms();var state=new DeliveryItemState();state.Control(Time.time,Vector3.forward*push+Vector3.up*lift);
                float falling=state.TakeLift(),high=151;const float dt=.01f;
                for(int n=0;n<250;n++)
                {
                    falling-=20*dt;var args=new object[]{target,(state.TakeForce(dt)+Vector3.up*falling)*dt,falling};move.Invoke(null,args);falling=(float)args[2];high=Mathf.Max(high,target.transform.position.y);
                    rows.Add(FormattableString.Invariant($"{label},{n*dt:F3},{target.transform.position.x:F4},{target.transform.position.y:F4},{target.transform.position.z:F4}"));
                }
                return new Vector2(target.transform.position.z,high-151);
            }
            var before=Travel(18*Mathf.Sqrt(3),6*Mathf.Sqrt(3),"previous");var after=Travel(Value("GlovePushSpeed"),Value("GloveLiftSpeed"),"enhanced");
            log.Add("INFO previous travel/rise="+before+" enhanced="+after);
            Check(Mathf.Abs(after.x-32.4f)<.3f,"native controller horizontal travel is approximately32.4m");
            Check(Mathf.Abs(after.y-5.4f)<.2f,"native controller extra rise is approximately5.4m");
            Check(Mathf.Abs(after.x/before.x-2)<.03f&&Mathf.Abs(after.y/before.y-2)<.06f,"actual controlled travel and rise are approximately twice the previous values");
            File.WriteAllLines(folder+"evidence/enhanced-glove-path.csv",rows);
            Place();Call("HitToyHammerTarget");Check(Mathf.Abs(states[0].stunUntil-Time.time-5)<.05f,"offline hammer hit stuns5 seconds");
            Check(Mathf.Abs(states[0].stunImmuneUntil-Time.time-7)<.05f,"hammer immunity ends2 seconds after5-second stun");
            Place();game.IceCreamImpact(target,target.bounds.center,0,Vector3.up);
            Check(Mathf.Abs(states[0].frozenUntil-Time.time-3)<.05f&&states[0].Frozen(Time.time),"offline ice cream impact freezes3 seconds");
            Check(((string)Get("feedback")).Contains("3초"),"ice cream hit feedback says3 seconds");
            states[0].stunUntil=Time.time-1;states[0].frozenUntil=Time.time-1;states[0].stunImmuneUntil=Time.time+2;game.IceCreamImpact(target,target.bounds.center,0,Vector3.up);
            Check(!states[0].Frozen(Time.time),"shared recovery immunity blocks immediate ice refreeze");
            Place();states[0].shieldUntil=Time.time+8;Call("HitToyHammerTarget");Check(states[0].shieldUntil==0&&!states[0].Stunned(Time.time),"aircap blocks enhanced hammer without stun");
            Place();states[0].shieldUntil=Time.time+8;game.IceCreamImpact(target,target.bounds.center,0,Vector3.up);Check(states[0].shieldUntil==0&&!states[0].Frozen(Time.time),"aircap blocks enhanced ice without freeze");
            Place(40);target.gameObject.SetActive(false);Call("MakeIceCreamShot",Vector3.forward);
            var shot=UnityEngine.Object.FindObjectsByType<BrawlShot>(FindObjectsSortMode.None).First(s=>s.kind==WeaponKind.IceCream);
            Check((shot.GetComponent<Rigidbody>().linearVelocity-new Vector3(0,3,36)).magnitude<.01f,"offline ice launch uses forward36 and upward3m/s");
            Call("ClearActiveNetShots");
            for(int slot=0;slot<3;slot++)Call("ClearSlot",slot);Call("AddWeapon",WeaponKind.Plunger);Call("UpdateHeldWeapon");Call("UseWeapon");Set("plungerStarted",Time.time-.13f);Call("UpdatePlungerThrow");
            Check(Mathf.Abs(Vector3.Distance(cam.transform.position,(Vector3)Get("plungerEnd"))-30)<.1f,"offline plunger aims30m from camera");Call("FinishPlungerThrow");
            Check((int)Call("Ammo",WeaponKind.Slime)==3,"slime default uses3");Check((int)Call("Ammo",WeaponKind.BoxingGloves)==2,"boxing default remains2");
            game.NetworkScreens=true;game.HostRoom();yield return null;var players=(MonsterDeliveryPrototype.NetPlayer[])Get("netPlayers");
            players[1]=new MonsterDeliveryPrototype.NetPlayer{connected=true,team=1,color=1,name="Controlled guest"};game.NetTeamSize=1;game.StartNetRound();
            Check(((Unity.Netcode.NetworkManager)Get("netManager")).NetworkConfig.ProtocolVersion==7,"protocol7 requires updated effects at both peers");
            foreach(int kind in new[]{1,3})
            {
                Place();var input=((MonsterDeliveryPrototype.NetInput[])Get("netInputs"))[0];input.yaw=input.pitch=0;
                players[0].selected=0;players[0].cooldown=0;Call("GiveNetItem",0,0,kind);Call("UseNetWeapon",0);
                foreach(var melee in (IEnumerable)Get("netMelee"))melee.GetType().GetField("at").SetValue(melee,Time.time-1);Call("UpdateNetMelee");
                Check(kind==1?Mathf.Abs(states[0].force.magnitude-Value("GlovePushSpeed"))<.01f:Mathf.Abs(states[0].stunUntil-Time.time-5)<.05f,"server melee uses shared enhanced "+(WeaponKind)kind+" effect");
            }
            Place(40);target.gameObject.SetActive(false);var ni=((MonsterDeliveryPrototype.NetInput[])Get("netInputs"))[0];ni.yaw=ni.pitch=0;Call("LaunchNetProjectile",0,5);
            shot=UnityEngine.Object.FindObjectsByType<BrawlShot>(FindObjectsSortMode.None).First(s=>s.gameObject.activeSelf&&s.kind==WeaponKind.IceCream);
            Check((shot.GetComponent<Rigidbody>().linearVelocity-new Vector3(0,3,36)).magnitude<.01f,"server ice launch matches offline36m/s plus upward3");Call("ClearActiveNetShots");
            Call("LaunchNetProjectile",0,2);var plungers=(IList)Get("netPlungers");var plunger=plungers[0];var pt=plunger.GetType();var model=(GameObject)pt.GetField("model").GetValue(plunger);
            pt.GetField("distance").SetValue(plunger,29.9f);Call("UpdateNetPlungers");Check((float)pt.GetField("distance").GetValue(plunger)<=30.001f,"server plunger step never exceeds30m");
            pt.GetField("distance").SetValue(plunger,30f);Call("UpdateNetPlungers");Check((bool)pt.GetField("returning").GetValue(plunger),"server plunger returns at30m rather than15m");Call("ClearNetPlungers");
            Call("GiveNetItem",1,0,4);Check(players[1].ammo[0]==3,"server grants guest3 slime uses");
        }
        finally
        {
            File.WriteAllLines(folder+"evidence/enhanced-combat-check.txt",log);if(floor)UnityEngine.Object.Destroy(floor);game.LeaveRoom();game.enabled=oldEnabled;EditorApplication.update-=EditorApplication.QueuePlayerLoopUpdate;
        }
        Debug.Log("ENHANCED_COMBAT: "+log.Count(s=>s.StartsWith("PASS "))+" PASS / "+log.Count(s=>s.StartsWith("FAIL "))+" FAIL");
        if(log.Any(s=>s.StartsWith("FAIL ")))throw new Exception("ENHANCED_COMBAT_CHECK_FAILED");
    }
}
