using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class BroadcasterCheck
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    const string D="Docs/ChangeRecords/20261019/";
    [MenuItem("Tools/Gameplay/Special Delivery/Verify Broadcaster")]
    public static void Verify()
    {
        if(!EditorApplication.isPlaying)throw new Exception("Fresh Play required");
        var game=UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>();game.StartCoroutine(Run(game));
    }
    static IEnumerator Run(MonsterDeliveryPrototype game)
    {
        var t=game.GetType();object Get(string n)=>t.GetField(n,F).GetValue(game);void Set(string n,object v)=>t.GetField(n,F).SetValue(game,v);
        object Call(string n,params object[] args)=>t.GetMethod(n,F).Invoke(game,args);
        var log=new List<string>{DateTimeOffset.Now.ToString("O"),"Actual Play; controlled positions and production use/hit/FX. Separate peers recorded separately."};
        void Check(bool ok,string message)=>log.Add((ok?"PASS ":"FAIL ")+message);
        var actors=new[]{(CharacterController)Get("player")}.Concat((CharacterController[])Get("botControllers")).ToArray();
        var states=(DeliveryItemState[])Get("botItems");GameObject floor=null,wall=null;
        void Reset()
        {
            Set("playerItems",new DeliveryItemState());
            for(int i=0;i<4;i++)
            {
                if(i>0)states[i-1]=new DeliveryItemState();actors[i].gameObject.SetActive(true);actors[i].enabled=false;
                actors[i].transform.SetPositionAndRotation(new Vector3(i==1?2:i==2?-2:0,151,i==0?0:i==3?15:8),Quaternion.identity);actors[i].enabled=true;
            }
            Physics.SyncTransforms();
        }
        void Fire()=>Call("UseSpecialWeapon",0,WeaponKind.Broadcaster,actors[0].bounds.center,Vector3.forward);
        try
        {
            game.enabled=false;game.NetworkScreens=false;Call("Begin");Reset();Fire();
            Check(Mathf.Abs(states[0].stunUntil-Time.time-4)<.01f&&Mathf.Abs(states[1].stunUntil-Time.time-4)<.01f,"visible nearby rivals receive exactly four seconds of stun");
            Check(states[2].stunUntil==0,"outside12m target is excluded");
            Check(((DeliveryItemState)Get("playerItems")).stunUntil==0,"caster does not stun itself");
            Check(Mathf.Abs(states[0].stunImmuneUntil-states[0].stunUntil-2)<.01f,"two-second post-stun immunity remains");
            Reset();states[0].shieldUntil=Time.time+8;states[1].immuneUntil=Time.time+5;Fire();
            Check(states[0].stunUntil==0&&states[0].shieldUntil==0,"aircap blocks and consumes once");
            Check(states[1].stunUntil==0,"life jacket blocks broadcast");
            Reset();states[0].stunImmuneUntil=Time.time+5;Fire();Check(states[0].stunUntil==0,"stun immunity prevents a fresh lock");
            Reset();wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=new Vector3(0,152,4);wall.transform.localScale=new Vector3(12,5,.2f);Physics.SyncTransforms();Fire();
            Check(states[0].stunUntil==0&&states[1].stunUntil==0,"wall blocks broadcast hit");UnityEngine.Object.DestroyImmediate(wall);wall=null;
            Call("Begin");Reset();floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(0,149.9f,4);floor.transform.localScale=new Vector3(100,.2f,100);Physics.SyncTransforms();
            Set("selectedSlot",1);Set("nextAttackAt",0f);Set("lookPitch",0f);Call("UpdateCamera");Call("UpdateHeldWeapon");
            foreach(var fx in UnityEngine.Object.FindObjectsByType<BroadcasterFX>(FindObjectsSortMode.None))UnityEngine.Object.DestroyImmediate(fx.gameObject);
            ((float[])Get("specialFxAt"))[0]=-10;Call("UseWeapon");Call("UpdateStunFX");
            var effect=UnityEngine.Object.FindAnyObjectByType<BroadcasterFX>();
            Check(((int[])Get("weaponAmmo"))[1]==0&&effect,"normal click spends one use and starts the new effect");
            Call("ShowSpecialUse",0,WeaponKind.Broadcaster,actors[0].bounds.center,Vector3.forward,0f);
            Check(UnityEngine.Object.FindObjectsByType<BroadcasterFX>(FindObjectsSortMode.None).Length==1,"replayed action does not duplicate the effect");
            float expiry=states[0].stunUntil;
            yield return new WaitForSeconds(.38f);
            Check(effect&&effect.GetComponentsInChildren<LineRenderer>().Length==14&&effect.GetComponent<ParticleSystem>().particleCount>0&&effect.GetComponent<Light>(),"three waves, two arcs, beacon, eight sound rays, light and sparks are active");
            Check(effect.GetComponentsInChildren<Collider>().Length==0&&effect.GetComponentsInChildren<Rigidbody>().Length==0,"cosmetic effect cannot add collisions or gameplay hits");
            AirCannonCheck.Capture(D+"images/20261019_broadcaster_local.png");
            var visual=(DeliveryRobotVisual)Get("playerVisual");visual.SetFirstPerson(false);
            Camera.main.transform.SetPositionAndRotation(new Vector3(13,158,-4),Quaternion.LookRotation(new Vector3(-13,-6.5f,8)));
            AirCannonCheck.Capture(D+"images/20261019_broadcaster_overview.png");visual.SetFirstPerson(true);
            yield return new WaitForSeconds(1.1f);
            Check(!effect&&states[0].Stunned(Time.time),"effect cleans up after1.3seconds while four-second stun is still active");
            while(Time.time<expiry+.05f)yield return null;
            Check(!states[0].Stunned(Time.time),"stun actually ends after four seconds");
            Set("specialUseAt",Time.time-1);Call("UpdateSpecialPresentation");
            Check(((WeaponKind[])Get("inventory"))[1]==WeaponKind.None,"last-use slot clears after recoil");
            var late=new GameObject("LateBroadcasterCheck").AddComponent<BroadcasterFX>();late.Initialize(actors[0].bounds.center,.7f);
            Check(Mathf.Abs(late.Age-.7f)<.02f,"late action starts at the replicated elapsed time");
            yield return new WaitForSeconds(.7f);Check(!late,"late presentation also expires at the original action deadline");
        }
        finally
        {
            if(floor)UnityEngine.Object.Destroy(floor);if(wall)UnityEngine.Object.Destroy(wall);
            Call("Begin");game.enabled=true;File.WriteAllLines(D+"evidence/broadcaster-offline.txt",log);
        }
        Debug.Log("BROADCASTER_CHECK\n"+string.Join("\n",log));
    }
}
