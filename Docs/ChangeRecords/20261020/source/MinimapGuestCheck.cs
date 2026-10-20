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

[InitializeOnLoad]
public static class MinimapGuestCheck
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    static bool Client=>Environment.GetCommandLineArgs().Contains("-delivery-minimap-client");
    static MinimapGuestCheck()
    {
        if(Client)
        {
            EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode)Start(false);};
            EditorApplication.update+=()=>{if(EditorApplication.isPlaying&&EditorApplication.isPaused)EditorApplication.isPaused=false;};
        }
    }
    public static void BootstrapClient()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        EditorApplication.delayCall+=()=>EditorApplication.isPlaying=true;
    }
    [MenuItem("Tools/Gameplay/Minimap/Verify Connected Host And Guest")]
    public static void Host()=>Start(true);
    [MenuItem("Tools/Gameplay/Minimap/Verify Connected Guest")]
    public static void Guest()=>Start(false);
    static void Start(bool host)
    {
        if(!EditorApplication.isPlaying)throw new Exception("Fresh Play required");
        var assembly=typeof(Unity.Netcode.NetworkManager).Assembly;
        var registry=(IList)assembly.GetType("Unity.Netcode.ILPPMessageProvider").GetField("__network_message_types",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        if(registry.Count==0)assembly.GetType("__GEN.INetworkMessageHelper").GetMethod("InitializeMessages",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Invoke(null,null);
        var game=UnityEngine.Object.FindAnyObjectByType<MonsterDeliveryPrototype>();game.StartCoroutine(Run(game,host));
    }
    static IEnumerator Run(MonsterDeliveryPrototype game,bool host)
    {
        var type=game.GetType();object Get(string n)=>type.GetField(n,F).GetValue(game);
        void Set(string n,object v)=>type.GetField(n,F).SetValue(game,v);
        object Call(string n,params object[] args)=>type.GetMethod(n,F).Invoke(game,args);
        string root=Path.GetFullPath(Application.dataPath+"/..");if(!host)root=Path.GetFullPath(root+"/../../..");
        string folder=root+"/Docs/ChangeRecords/20261020/evidence/minimap-connected";Directory.CreateDirectory(folder);
        var log=new List<string>{DateTimeOffset.Now.ToString("O"),"Two actual NGO/UTP localhost Editor processes, 60ms artificial one-way latency and every fifth input/frame omitted. Production TickNetwork, minimap player reference and production rotation matrix. Not WAN or screen pixel tracking."};
        var errors=new List<string>();var editorErrors=new List<string>();
        Application.LogCallback error=(m,s,k)=>{if(k==LogType.Exception||k==LogType.Error||k==LogType.Assert){if(s.Contains("UnityEditor.Search"))editorErrors.Add(m);else errors.Add(m);}};Application.logMessageReceived+=error;
        var keyboard=InputSystem.AddDevice<Keyboard>("MinimapPeerKeyboard");var mouse=InputSystem.AddDevice<Mouse>("MinimapPeerMouse");
        var oldKeyboard=Keyboard.current;var oldMouse=Mouse.current;
        var oldBackground=InputSystem.settings.backgroundBehavior;var oldEditor=InputSystem.settings.editorInputBehaviorInPlayMode;
        var rows=new List<string>{"elapsed_s,phase,own,x,z,yaw,map_x,map_y,max_rotation_error_px"};
        void Check(bool ok,string text)=>log.Add((ok?"PASS ":"FAIL ")+text);
        void Input(bool moving)
        {
            keyboard.MakeCurrent();mouse.MakeCurrent();InputSystem.QueueStateEvent(keyboard,moving?new KeyboardState(Key.W):new KeyboardState());
            InputSystem.QueueStateEvent(mouse,new MouseState());InputSystem.Update();Cursor.lockState=CursorLockMode.Locked;
        }
        try
        {
            Application.runInBackground=true;EditorApplication.update+=EditorApplication.QueuePlayerLoopUpdate;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            game.enabled=false;game.NetworkScreens=true;game.NetTeamSize=1;
            if(host)game.HostRoom();else game.JoinRoom("127.0.0.1");
            Set("latencyProof",true);Set("latencySaved",true);Set("latencyLabel","minimap-loss");
            float deadline=Time.realtimeSinceStartup+70;
            while(!game.NetPlaying&&Time.realtimeSinceStartup<deadline){Input(false);Call("TickNetwork");yield return null;}
            Check(game.NetPlaying,"host and guest connect and enter a real match");
            if(!game.NetPlaying)yield break;
            var player=(CharacterController)Get("player");var map=game.GetComponent<DeliveryMinimap>();
            var mapPlayer=(Transform)typeof(DeliveryMinimap).GetField("player",F).GetValue(map);
            int own=(int)Get("netOwn");Check(host?own==0:own>0,"correct assigned global own slot="+own);
            Check(mapPlayer==player.transform,"minimap follows local actor, including guest global-to-local remapping");
            var matrix=typeof(DeliveryMinimap).GetMethod("PlayerMarkerMatrix",BindingFlags.Static|BindingFlags.NonPublic);
            var anchor=typeof(DeliveryMinimap).GetMethod("MarkerCenter",BindingFlags.Static|BindingFlags.NonPublic);
            float began=Time.realtimeSinceStartup,maxError=0,maxDrift=0;Vector3 first=player.transform.position,moveStart=first,moveEnd=first;
            float firstYaw=player.transform.eulerAngles.y,maxYaw=0;int samples=0;
            while(Time.realtimeSinceStartup-began<16)
            {
                float age=Time.realtimeSinceStartup-began;bool moving=age>=7&&age<9;
                var input=(MonsterDeliveryPrototype.NetInput)Get("netLocalInput");input.yaw=moving?0:age*65;input.pitch=0;
                Input(moving);Call("TickNetwork");
                var position=player.transform.position;var point=map.MapPosition(position);float frameError=0;
                foreach(float scale in new[]{.5f,2f/3,1f,4f/3,2f})
                {
                    var image=new Rect(1920-318+13,68,254,254);
                    var center=(Vector2)anchor.Invoke(null,new object[]{image,point,22f});var basis=Matrix4x4.Scale(new Vector3(scale,scale,1));
                    var draw=(Matrix4x4)matrix.Invoke(null,new object[]{basis,center,player.transform.eulerAngles.y});
                    frameError=Mathf.Max(frameError,Vector3.Distance(draw.MultiplyPoint3x4(center),basis.MultiplyPoint3x4(center)));
                }
                maxError=Mathf.Max(maxError,frameError);maxYaw=Mathf.Max(maxYaw,Mathf.Abs(Mathf.DeltaAngle(firstYaw,player.transform.eulerAngles.y)));
                if(age<1)first=position;
                if(age>=1&&age<6.5f)maxDrift=Mathf.Max(maxDrift,Vector2.Distance(new Vector2(position.x,position.z),new Vector2(first.x,first.z)));
                if(age<7)moveStart=position;if(age>=9&&age<9.5f)moveEnd=position;
                rows.Add(age+","+(moving?"move":"rotate")+","+own+","+position.x+","+position.z+","+player.transform.eulerAngles.y+","+point.x+","+point.y+","+frameError);samples++;
                yield return null;
            }
            Check(samples>60,"live network frames sampled="+samples);
            Check(maxYaw>120,"local yaw rotates through multiple directions");
            Check(maxDrift<.1f,"stationary turning preserves actual xz position; max drift="+maxDrift+"m");
            Check(maxError<.002f,"minimap anchor stays fixed across five scales; max error="+maxError+"px");
            Check(Vector3.Distance(moveStart,moveEnd)>1,"walking moves actual local actor and its minimap reference");
            Check((int)Get("latencyDroppedMessages")>0,"loss simulation actually omitted messages");
        }
        finally
        {
            InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(mouse);
            if(oldKeyboard!=null&&oldKeyboard.added)oldKeyboard.MakeCurrent();if(oldMouse!=null&&oldMouse.added)oldMouse.MakeCurrent();
            InputSystem.settings.backgroundBehavior=oldBackground;InputSystem.settings.editorInputBehaviorInPlayMode=oldEditor;
            Set("latencyProof",false);game.LeaveRoom();game.enabled=true;EditorApplication.update-=EditorApplication.QueuePlayerLoopUpdate;
            File.WriteAllLines(folder+"/"+(host?"host":"guest")+".csv",rows);
        }
        yield return new WaitForSeconds(.5f);Check(errors.Count==0,"no gameplay runtime or shutdown errors; count="+errors.Count);log.AddRange(errors);
        log.Add("EDITOR_ENVIRONMENT: unrelated UnityEditor.Search initialization exceptions="+editorErrors.Count);log.AddRange(editorErrors);
        Application.logMessageReceived-=error;File.WriteAllLines(folder+"/"+(host?"host":"guest")+".txt",log);
        Debug.Log("MINIMAP_PEER\n"+string.Join("\n",log));
        if(Client)EditorApplication.delayCall+=()=>EditorApplication.Exit(0);
    }
}
