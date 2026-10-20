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

public static class HoldZoomCheck
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    const string D="Docs/ChangeRecords/20261020/";
    [MenuItem("Tools/Gameplay/Camera/Verify Hold Zoom")]
    public static void Verify()
    {
        if(!EditorApplication.isPlaying)throw new Exception("Play required");
        var game=UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>();game.StartCoroutine(Run(game));
    }
    static IEnumerator Run(MonsterDeliveryPrototype game)
    {
        var type=game.GetType();object Get(string n)=>type.GetField(n,F).GetValue(game);
        void Set(string n,object v)=>type.GetField(n,F).SetValue(game,v);
        object Call(string n,params object[] a)=>type.GetMethod(n,F).Invoke(game,a);
        bool Zoom()=>(bool)type.GetProperty("ZoomHeld",F).GetValue(game);
        var report=new List<string>{DateTimeOffset.Now.ToString("O"),"Actual Editor Play with synthetic Unity Input System right-mouse states. Shared offline/network camera path; not separate network peers."};
        void Check(bool ok,string text)=>report.Add((ok?"PASS ":"FAIL ")+text);
        var originalMouse=UnityEngine.InputSystem.Mouse.current;var testMouse=InputSystem.AddDevice<Mouse>("HoldZoomCheckMouse");testMouse.MakeCurrent();
        bool pressed=false;
        void Mouse(bool down){pressed=down;testMouse.MakeCurrent();InputSystem.QueueStateEvent(testMouse,new MouseState{buttons=(ushort)(down?2:0)});InputSystem.Update();Cursor.lockState=CursorLockMode.Locked;}
        IEnumerator Settle(bool network=false)
        {
            float end=Time.unscaledTime+.65f;
            while(Time.unscaledTime<end){Mouse(pressed);Call(network?"UpdateCameraForNet":"UpdateCameraFieldOfView");yield return null;}
        }
        var oldPlayers=(MonsterDeliveryPrototype.NetPlayer[])Get("netPlayers");
        Directory.CreateDirectory(D+"evidence");Directory.CreateDirectory(D+"images");
        try
        {
            game.NetworkScreens=false;game.thirdPersonPreview=false;Call("Begin");game.enabled=false;
            var cam=(Camera)Get("cam");var player=(CharacterController)Get("player");
            player.enabled=false;player.transform.SetPositionAndRotation(new Vector3(.45f,1,19.2f),Quaternion.identity);player.enabled=true;
            Set("lookPitch",8f);Call("UpdateCamera");
            for(int slot=0;slot<3;slot++)Call("ClearSlot",slot);Call("UpdateHeldWeapon");
            Mouse(false);yield return Settle();Check(!Zoom()&&Mathf.Abs(cam.fieldOfView-60)<.05f,"released right button keeps the normal sixty-degree view");
            AirCannonCheck.Capture(D+"images/20261020_zoom_normal.png");
            Mouse(true);Check(Zoom(),"holding right button activates zoom even with empty hands");
            yield return Settle();Check(Mathf.Abs(cam.fieldOfView-35)<.05f,"held zoom smoothly converges to thirty-five degrees");
            AirCannonCheck.Capture(D+"images/20261020_zoom_held.png");
            Mouse(false);yield return Settle();Check(!Zoom()&&Mathf.Abs(cam.fieldOfView-60)<.05f,"release restores normal view without another click");
            Call("AddWeapon",WeaponKind.PackingMachine);Call("UpdateHeldWeapon");Mouse(true);yield return Settle();Call("UpdateHeldWeapon");
            var heldCamera=(Camera)Get("heldCamera");Check(heldCamera&&Mathf.Abs(heldCamera.fieldOfView-cam.fieldOfView)<.001f,"held-item camera uses the same zoom as the world camera");
            int[] ammo=(int[])Get("weaponAmmo");int before=ammo.Sum();
            Mouse(true);Call("UpdateCamera");Check(ammo.Sum()==before,"zoom does not spend item ammo or trigger use");
            var state=(DeliveryItemState)Get("playerItems");state.speedUntil=state.batteryUntil=Time.time+8;
            Mouse(true);yield return Settle();Check(Mathf.Abs(cam.fieldOfView-35)<.05f,"zoom takes priority over shoes and battery field-of-view effects");
            Mouse(false);yield return Settle();float boost=(float)type.GetProperty("RunningShoesFieldOfView",F).GetValue(game);
            Check(Mathf.Abs(cam.fieldOfView-boost)<.05f&&state.MoveMultiplier(Time.time)==1.5f,"release restores current booster view and preserves strongest movement bonus");
            game.thirdPersonPreview=true;Mouse(true);yield return Settle();Check(Zoom()&&Mathf.Abs(cam.fieldOfView-35)<.05f,"third-person preview also supports held zoom");game.thirdPersonPreview=false;
            var ps=Enumerable.Range(0,4).Select(i=>new MonsterDeliveryPrototype.NetPlayer{connected=i==0,position=player.transform.position}).ToArray();
            Set("netPlayers",ps);Set("netOwn",0);game.NetworkScreens=true;type.GetProperty("RoomState").SetValue(game,MonsterDeliveryPrototype.RoomPhase.Playing);Set("netPause",false);
            Mouse(true);yield return Settle(true);Check(Zoom()&&Mathf.Abs(cam.fieldOfView-35)<.05f,"network camera path uses the same right-button zoom");
            Set("netPause",true);Mouse(true);Call("UpdateCameraForNet");Check(!Zoom(),"pause prevents zoom while the right button remains held");
            Set("netPause",false);type.GetProperty("RoomState").SetValue(game,MonsterDeliveryPrototype.RoomPhase.Menu);Check(!Zoom(),"network menu prevents gameplay zoom");
            game.NetworkScreens=false;Set("finished",true);Check(!Zoom(),"finished round suppresses zoom");Set("finished",false);
            game.mapEditMode=true;Check(!Zoom(),"map editor does not use gameplay zoom");game.mapEditMode=false;
            Mouse(true);Cursor.lockState=CursorLockMode.None;Check(!Zoom(),"unlocked cursor suppresses zoom for UI interaction");
        }
        finally
        {
            InputSystem.RemoveDevice(testMouse);if(originalMouse!=null)originalMouse.MakeCurrent();
            game.NetworkScreens=false;game.mapEditMode=false;game.thirdPersonPreview=false;Set("netPlayers",oldPlayers);Set("netPause",false);game.enabled=true;Call("Begin");
            File.WriteAllLines(D+"evidence/hold-zoom-play.txt",report);
        }
        Debug.Log("HOLD_ZOOM_CHECK\n"+string.Join("\n",report));
    }
}
