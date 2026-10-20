using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class AirCannonCheck
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic;
    const string D = "Docs/ChangeRecords/20261019/";
    [MenuItem("Tools/Gameplay/Special Delivery/Verify Air Cannon")]
    public static void Verify()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Fresh Play required");
        var game = UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>();
        game.StartCoroutine(Run(game));
    }
    static IEnumerator Run(MonsterDeliveryPrototype game)
    {
        var t = game.GetType(); object Get(string n) => t.GetField(n,F).GetValue(game);
        void Set(string n,object v) => t.GetField(n,F).SetValue(game,v);
        object Call(string n,params object[] a) => t.GetMethod(n,F|BindingFlags.Public).Invoke(game,a);
        var log = new List<string>{DateTimeOffset.Now.ToString("O"),"Actual Play; controlled positions, production input/use, ballistic physics and hit paths. Separate peers recorded separately."};
        void Check(bool ok,string message) => log.Add((ok?"PASS ":"FAIL ")+message);
        var actors = new[]{(CharacterController)Get("player")}.Concat((CharacterController[])Get("botControllers")).ToArray();
        var states = (DeliveryItemState[])Get("botItems");
        GameObject wall=null,floor=null;
        void Place(int id,Vector3 position)
        {actors[id].gameObject.SetActive(true);actors[id].enabled=false;actors[id].transform.SetPositionAndRotation(position,Quaternion.identity);actors[id].enabled=true;}
        void Reset()
        {
            Set("playerItems",new DeliveryItemState());for(int i=0;i<3;i++)states[i]=new DeliveryItemState();
            Place(0,new Vector3(0,151,-8));Place(1,new Vector3(2,151,0));Place(2,new Vector3(-2,151,0));Place(3,new Vector3(8,151,0));Physics.SyncTransforms();
            Call("UpdateBubbleWrapFX");
        }
        Vector3 center = new Vector3(0,150,0);
        try
        {
            game.enabled=false;game.NetworkScreens=false;Call("Begin");Set("nextRivalBubbleTest",float.PositiveInfinity);Reset();
            game.AirCannonImpact(center,0,Vector3.up);
            Check(states[0].force.x>59&&states[1].force.x< -59&&states[0].pendingLift==18&&states[1].pendingLift==18,"one impact hits every in-range rival radially with60m/s horizontal and18m/s lift");
            Check(states[2].force==Vector3.zero&&states[2].pendingLift==0,"outside five-metre sphere is excluded");
            Check(((DeliveryItemState)Get("playerItems")).force==Vector3.zero,"source is excluded");
            Reset();Place(2,new Vector3(3,151,0));game.AirCannonImpact(center,0,Vector3.up);
            Check(states[0].pendingLift==18&&states[1].pendingLift==18,"one rival does not occlude another rival in the same blast");
            Reset();states[0].shieldUntil=Time.time+10;states[1].immuneUntil=Time.time+5;game.AirCannonImpact(center,0,Vector3.up);
            Check(states[0].shieldUntil==0&&states[0].pendingLift==0,"aircap blocks and spends once");
            Check(states[1].pendingLift==0&&states[1].force==Vector3.zero,"life jacket blocks blast");
            Reset();wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=new Vector3(1,152,0);wall.transform.localScale=new Vector3(.3f,5,5);Physics.SyncTransforms();game.AirCannonImpact(center,0,Vector3.up);
            Check(states[0].pendingLift==0&&states[1].pendingLift==18,"wall blocks only the covered rival");UnityEngine.Object.DestroyImmediate(wall);wall=null;
            Reset();Place(1,new Vector3(0,157,0));game.AirCannonImpact(center,0,Vector3.up);
            Check(states[0].pendingLift==0,"vertical distance excludes another storey beyond blast radius");
            Reset();game.NetworkScreens=true;game.AirCannonImpact(center,0,Vector3.up);
            Check(states.All(s=>s.pendingLift==0),"participant impact replay cannot apply gameplay force");game.NetworkScreens=false;
            // Real ballistic flight over a floor well above the saved scene geometry.
            Reset();Place(0,new Vector3(0,151,0));Place(1,new Vector3(2,151,17));Place(2,new Vector3(-2,151,17));Place(3,new Vector3(9,151,17));
            floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(0,149.9f,10);floor.transform.localScale=new Vector3(100,.2f,100);Physics.SyncTransforms();
            foreach(var pulse in UnityEngine.Object.FindObjectsByType<SpecialDeliveryPulse>(FindObjectsSortMode.None))UnityEngine.Object.Destroy(pulse.gameObject);
            Set("lookPitch",0f);Call("UpdateCamera");Set("nextAttackAt",0f);Set("selectedSlot",0);
            Call("UseWeapon");
            var shot=UnityEngine.Object.FindObjectsByType<BrawlShot>(FindObjectsSortMode.None).Single(s=>s.kind==WeaponKind.AirCannon);
            var body=shot.GetComponent<Rigidbody>();
            log.Add("TRACE floor="+floor.GetComponent<Collider>().bounds+" enabled="+floor.GetComponent<Collider>().enabled+" simulation="+Physics.simulationMode+" shotCollider="+shot.GetComponent<Collider>().bounds+" enabled="+shot.GetComponent<Collider>().enabled+" trigger="+shot.GetComponent<Collider>().isTrigger+" floorIgnored="+Physics.GetIgnoreCollision(shot.GetComponent<Collider>(),floor.GetComponent<Collider>()));
            Check(states.All(s=>s.pendingLift==0),"normal click does not hit immediately at launch");
            Check(Mathf.Abs(body.linearVelocity.z-18)<.01f&&Mathf.Abs(body.linearVelocity.y-3)<.01f&&body.useGravity,"same18m/s forward plus3m/s upward gravity as slime; origin="+body.position);
            Check(((int[])Get("weaponAmmo"))[0]==0,"normal click consumes exactly one use");
            Camera.main.transform.SetPositionAndRotation(new Vector3(11,158,4),Quaternion.LookRotation(new Vector3(-11,-5,12)));
            float deadline=Time.time+3;bool flightCapture=false,impactCapture=false;
            while(Time.time<deadline&&(shot||!impactCapture))
            {
                if(shot&&Time.time>deadline-2.7f&&!flightCapture){Capture(D+"images/20261019_air_cannon_flight.png");flightCapture=true;}
                var explosion=GameObject.Find("AirCannonExplosion");
                if(!shot&&explosion&&!impactCapture){Capture(D+"images/20261019_air_cannon_impact.png");impactCapture=true;}
                yield return null;
            }
            Check(!shot&&states[0].pendingLift==18&&states[1].pendingLift==18,"real floor collision triggers one blast against both nearby rivals; remaining="+(shot?shot.transform.position.ToString():"none")+" lifts="+states[0].pendingLift+"/"+states[1].pendingLift);
            if(shot) log.Add("TRACE remainingCollider="+shot.GetComponent<Collider>().enabled+" BrawlShot="+shot.enabled+" body="+shot.GetComponent<Rigidbody>().detectCollisions+" floor="+floor.GetComponent<Collider>().bounds+" ray="+Physics.Raycast(new Vector3(0,155,17),Vector3.down,out var ground,10)+" ground="+ground.collider);
            Check(states[2].pendingLift==0,"real blast leaves distant rival untouched");
            Check(flightCapture&&impactCapture,"actual flight and impact images captured");
            Set("specialUseAt",Time.time-1);Call("UpdateSpecialPresentation");
            Check(((WeaponKind[])Get("inventory"))[0]==WeaponKind.None,"last-use slot clears after recoil while launched shot persists");
            // Same muzzle obstruction path must explode at nearby cover, rather than shooting through it.
            Reset();wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=new Vector3(0,152,-7.5f);wall.transform.localScale=new Vector3(4,5,.2f);Physics.SyncTransforms();
            Call("UseSpecialWeapon",0,WeaponKind.AirCannon,actors[0].bounds.center,Vector3.forward);
            Check(!UnityEngine.Object.FindObjectsByType<BrawlShot>(FindObjectsSortMode.None).Any(s=>s.kind==WeaponKind.AirCannon&&s.gameObject.activeSelf),"muzzle cover resolves immediately without a projectile behind wall");
        }
        finally
        {
            if(wall)UnityEngine.Object.Destroy(wall);if(floor)UnityEngine.Object.Destroy(floor);
            game.NetworkScreens=false;Call("Begin");game.enabled=true;
            Directory.CreateDirectory(D+"evidence");File.WriteAllLines(D+"evidence/air-cannon-offline.txt",log);
        }
        Debug.Log("AIR_CANNON_CHECK\n"+string.Join("\n",log));
    }
    public static void Capture(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var camera=Camera.main;var rt=RenderTexture.GetTemporary(1280,720,24);var previous=camera.targetTexture;var active=RenderTexture.active;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
        finally{camera.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.Destroy(image);}
    }
}
