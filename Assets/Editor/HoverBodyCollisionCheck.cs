using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class HoverBodyCollisionCheck
{
    [MenuItem("Tools/Gameplay/Delivery Robot/Verify Body Collision")]
    public static void Verify()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Run in Play Mode.");
        bool background = Application.runInBackground;
        Application.runInBackground = true; EditorApplication.isPaused = false;
        EditorApplication.update += EditorApplication.QueuePlayerLoopUpdate;
        UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>().StartCoroutine(Run(background));
    }
    static IEnumerator Run(bool background)
    {
        yield return null;
        var game = UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>();
        var type = typeof(MonsterDeliveryPrototype);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        object Get(string n) => type.GetField(n,flags).GetValue(game);
        void Set(string n,object v) => type.GetField(n,flags).SetValue(game,v);
        var fields = type.GetFields(flags).Where(f=>!f.IsInitOnly).ToArray();
        var saved = fields.Select(f=>f.GetValue(game)).ToArray();
        var player = (CharacterController)Get("player");
        var visual = player.GetComponentInChildren<DeliveryRobotVisual>();
        var animator = visual.model.GetComponent<Animator>();
        var animation = animator.GetCurrentAnimatorStateInfo(0);
        var camera = Camera.main;
        var pos=player.transform.position; var rot=player.transform.rotation;
        var cp=camera.transform.position; var cr=camera.transform.rotation;
        float height=player.height; var center=player.center;
        bool enabled=game.enabled; game.enabled=false;
        var keyboard=InputSystem.AddDevice<Keyboard>();
        GameObject floor=null, obstacle=null;
        var report=new List<string>();
        void Check(bool ok,string detail) { if(!ok) throw new Exception("HOVER_BODY: "+detail); report.Add("PASS "+detail); }
        void Input(params Key[] keys) { InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys)); InputSystem.Update(); }
        void Tick(int count)
        {
            for(int i=0;i<count;i++) type.GetMethod("UpdatePlayer",flags).Invoke(game,new object[]{keyboard});
        }
        void Place(Vector3 point)
        {
            player.enabled=false; player.transform.SetPositionAndRotation(point,Quaternion.identity); player.enabled=true;
            Set("vertical",0f); Set("rollUntil",0f); Physics.SyncTransforms(); Input(); Tick(1);
        }
        GameObject Cube(string name,Vector3 point,Vector3 size)
        {
            var cube=GameObject.CreatePrimitive(PrimitiveType.Cube); cube.name=name; cube.transform.position=point; cube.transform.localScale=size;
            Physics.SyncTransforms(); return cube;
        }
        try
        {
            Check(Time.deltaTime>0,"real unpaused frame available");
            floor=Cube("Hover Collision Review Floor",new Vector3(0,99.9f,0),new Vector3(20,.2f,20));
            Place(new Vector3(0,101,0));
            int second=Mathf.CeilToInt(1f/Time.deltaTime);
            Tick(second);
            Check(Mathf.Abs(player.transform.position.y-101)<.015f,"ground sensor maintains 1m hover without a ground-touching collider");
            Check(Mathf.Abs(player.bounds.min.y-player.transform.position.y)<.002f &&
                Mathf.Abs(player.bounds.size.y-1.4f)<.002f,"body capsule starts at visual base and ends about 1.4m above it");
            bool CapsuleHit(float y) => Physics.RaycastAll(new Vector3(-2,y,0),Vector3.right,4,~0,QueryTriggerInteraction.Ignore).Any(h=>h.collider==player);
            Check(!CapsuleHit(100.5f),"horizontal ray through lower 50cm gap does not hit player");
            Check(CapsuleHit(101.7f),"horizontal ray through robot body hits player");
            var rival=GameObject.Find("Rival 1").GetComponent<CharacterController>();
            Check(Mathf.Abs(rival.center.y-rival.height*.5f)<.001f &&
                Mathf.Abs(rival.height-1.4f)<.001f,"red rival also has no lower collision extension");
            obstacle=Cube("Low Gap Obstacle",new Vector3(0,100.25f,1.5f),new Vector3(2,.5f,.2f));
            Input(Key.W); Tick(Mathf.CeilToInt(3.5f/(6.5f*Time.deltaTime)));
            Check(player.transform.position.z>3.2f,"real walking passes a 50cm obstacle beneath the robot without horizontal blocking");
            UnityEngine.Object.DestroyImmediate(obstacle); obstacle=null;
            obstacle=Cube("Body Height Obstacle",new Vector3(0,101.7f,1.5f),new Vector3(3,1.4f,.2f));
            Place(new Vector3(0,101,0)); Input(Key.W); Tick(Mathf.CeilToInt(3.5f/(6.5f*Time.deltaTime)));
            Check(player.transform.position.z<1.1f,"real walking is blocked by an obstacle at body height");
            Check(Mathf.Abs(player.transform.position.y-101)<.015f,"wall does not alter hover altitude");
            UnityEngine.Object.DestroyImmediate(obstacle); obstacle=null;
            Place(new Vector3(0,101,0)); Input(Key.W); Tick(1);
            type.GetMethod("LateUpdate",flags).Invoke(game,null); animator.Update(0); animator.Update(.2f);
            Check(animator.GetCurrentAnimatorStateInfo(0).IsName("WALK"),"one movement call retains horizontal velocity and WALK animation");
            Place(new Vector3(0,101,0)); Input(Key.Space); Tick(1);
            Check(player.transform.position.y>101 && (float)Get("vertical")>0,"Space jump leaves sensed ground");
            float launchSpeed = (float)Get("vertical");
            Input(Key.Space); Tick(1);
            Check((float)Get("vertical") < launchSpeed,"holding Space does not restart the jump while rising inside the ground-sensor range");
            Input(); Tick(second*2);
            Check(Mathf.Abs(player.transform.position.y-101)<.02f,"gravity lands back at 1m hover altitude");
            Place(new Vector3(0,101,0)); Input(Key.C); Tick(1);
            Check(Mathf.Abs(player.center.y-player.height*.5f)<.001f,"crouching keeps the lower collision gap empty");
            Place(new Vector3(11,101,0)); Tick(second);
            Check(player.transform.position.y<100.5f,"leaving a platform falls under gravity instead of hovering without ground");
            File.WriteAllLines("art-review/hover-body-collision-check-20261008.txt",report);
            Debug.Log("HOVER_BODY_OK: "+report.Count+" checks passed.\n"+string.Join("\n",report));
        }
        finally
        {
            if(obstacle) UnityEngine.Object.DestroyImmediate(obstacle);
            if(floor) UnityEngine.Object.DestroyImmediate(floor);
            InputSystem.RemoveDevice(keyboard);
            for(int i=0;i<fields.Length;i++) fields[i].SetValue(game,saved[i]);
            player.enabled=false; player.transform.SetPositionAndRotation(pos,rot); player.height=height; player.center=center; player.enabled=true;
            camera.transform.SetPositionAndRotation(cp,cr);
            animator.Play(animation.fullPathHash,0,animation.normalizedTime); animator.Update(0);
            game.enabled=enabled;
            Application.runInBackground=background;
            EditorApplication.update-=EditorApplication.QueuePlayerLoopUpdate;
        }
    }
}

