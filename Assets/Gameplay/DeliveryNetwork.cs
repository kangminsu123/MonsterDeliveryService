using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.InputSystem;

// Two- or four-player development rooms. NGO/UTP handles connections and reliable messages.
// ponytail: direct IPv4 rooms require LAN or port forwarding; use Relay for public internet rooms.
public sealed partial class MonsterDeliveryPrototype
{
    public enum RoomPhase { Menu, Connecting, Lobby, Countdown, Playing, Results }
    public RoomPhase RoomState { get; private set; }
    public bool NetworkScreens = true;
    public bool NetPlaying => NetworkScreens && RoomState == RoomPhase.Playing;
    bool NetServer => netManager && netManager.IsServer;
    NetworkManager netManager;
    UnityTransport netTransport;
    readonly Dictionary<ulong, int> netSlots = new();
    NetPlayer[] netPlayers = new NetPlayer[4];
    readonly NetInput[] netInputs = new NetInput[4];
    int netOwn;
    public int NetTeamSize = 2;
    int NetCapacity => NetTeamSize * 2;
    string NetMode => NetTeamSize == 1 ? "1대1" : "2대2";
    public Color LocalMailboxColor => NetPlaying ? TeamColors[netPlayers[netOwn]?.team ?? 0] : colors[0];
    public string MapOwnershipLegend => NetPlaying ? "우리 팀  ·  흰색 미점령" : "내 점령  ·  흰색 미점령";
    float netNextSend, netPhaseUntil, netRoundUntil, netConnectedAt;
    bool netPause, netLeaving;
    string netNotice = "", netName = "배달원", netCode = "", netAddress = "127.0.0.1";
    ushort netPort = 7777;
    NetInput netLocalInput = new();
    FloatingParcel[] netParcels;
    readonly Dictionary<string, GameObject> netShotViews = new();
    static readonly Color[] TeamColors = { new(.25f, .7f, 1), new(1, .48f, .22f) };
    [Serializable] public sealed class NetInput
    {
        public float x, z, yaw, pitch;
        public bool jump, crouch, capture;
        public int slot, use, dash, drop;
        public string name;
    }
    [Serializable] public sealed class NetPlayer
    {
        public bool connected;
        public string name;
        public int team, color, selected, target = -1;
        public Vector3 position;
        public float actionAge, dashRemaining;
        public float yaw, pitch, stamina = 100, vertical, dashUntil, cooldown, captureAt, actionAt = -10;
        public int used, dashed, dropped;
        public int[] items = { 2, 5, 7 };
        public float stun, frozen, shield, shieldBreak = -10, speed, slow, ink, progress;
    }
    [Serializable] sealed class NetShot { public string id; public int kind; public Vector3 position; public Quaternion rotation; }
    [Serializable] sealed class NetFrame
    {
        public int own, phase, teamSize;
        public float remaining;
        public string notice;
        public NetPlayer[] players;
        public int[] owners;
        public int[] parcels;
        public NetShot[] shots;
    }
    [Serializable] sealed class NetImpact { public int kind; public Vector3 point, normal; }
    static readonly Color[] CoatColors = { new(1,.82f,.12f), new(1,.22f,.28f), new(.25f,.87f,.66f), new(.69f,.4f,1), new(.1f,.7f,1), new(1,.45f,.73f) };

    int NetLocal(int global) => global == netOwn ? 0 : global < netOwn ? global + 1 : global;
    int NetGlobal(int local) => local == 0 ? netOwn : local <= netOwn ? local - 1 : local;
    bool NetCanHit(int source, int target) => !NetworkScreens || !NetPlaying ||
        netPlayers[NetGlobal(source)] != null && netPlayers[NetGlobal(target)] != null &&
        netPlayers[NetGlobal(source)].team != netPlayers[NetGlobal(target)].team;
    bool MailboxOwnedByUs(int house) => owner[house] >= 0 && (NetPlaying
        ? netPlayers[NetGlobal(owner[house])]?.team == netPlayers[netOwn]?.team : owner[house] == 0);
    Color CaptureColor(int local) => NetPlaying || NetworkScreens && RoomState == RoomPhase.Results
        ? TeamColors[netPlayers[NetGlobal(local)]?.team ?? 0] : colors[local];
    int TeamScore(int team) => owner.Count(id => id >= 0 && netPlayers[NetGlobal(id)] != null && netPlayers[NetGlobal(id)].team == team);
    int NetCount => netPlayers.Count(p => p != null && p.connected);
    bool NetDevelopment => Application.isEditor || Debug.isDebugBuild;

    void InitializeNetworkScreens()
    {
        var args = Environment.GetCommandLineArgs();
        if (args.Contains("-delivery-explore")) NetworkScreens = false;
        if (!NetworkScreens || mapEditMode) return;
        Application.runInBackground = true;
        rivalBubblePlaytest = rivalShoesPlaytest = false;
        if (explorationRivalGloves) Destroy(explorationRivalGloves.gameObject);
        explorationRivalGloves = null;
        for (int i = 0; i < 3; i++) { if (!bots[i]) CreateBot(i); bots[i].SetActive(false); }
        netParcels = parcelPool ? parcelPool.parcels : Array.Empty<FloatingParcel>();
        RoomState = RoomPhase.Menu; NetMenuWorld();
        string Arg(string key) { int i = Array.IndexOf(args, key); return i >= 0 && i + 1 < args.Length ? args[i + 1] : ""; }
        if (ushort.TryParse(Arg("-delivery-port"), out var port)) netPort = port;
        if (Arg("-delivery-mode") == "1v1") NetTeamSize = 1;
        if (Arg("-delivery-name") != "") netName = Arg("-delivery-name");
        #if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (args.Contains("-delivery-proof")) BeginNetworkProof(Arg("-delivery-proof-dir"));
#endif
        if (args.Contains("-delivery-host")) HostRoom();
        else if (Arg("-delivery-join") != "") JoinRoom(Arg("-delivery-join"));
    }

    public static string EncodeRoom(string address, ushort port)
    {
        var ip = IPAddress.Parse(address).GetAddressBytes(); if (ip.Length != 4) throw new ArgumentException("IPv4 required");
        return "MD-" + BitConverter.ToString(ip).Replace("-", "") + "-" + port.ToString("X4");
    }
    public static bool DecodeRoom(string code, out string address, out ushort port)
    {
        address = ""; port = 7777; code = (code ?? "").Trim().ToUpperInvariant();
        if (IPAddress.TryParse(code, out var direct) && direct.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) { address = direct.ToString(); return true; }
        var parts = code.Split('-'); if (parts.Length != 3 || parts[0] != "MD" || parts[1].Length != 8 || parts[2].Length != 4) return false;
        try { address = new IPAddress(Enumerable.Range(0,4).Select(i => Convert.ToByte(parts[1].Substring(i*2,2),16)).ToArray()).ToString(); port = Convert.ToUInt16(parts[2],16); return port > 0; }
        catch (FormatException) { return false; } catch (OverflowException) { return false; }
    }
    static string LanAddress()
    {
        try { return Dns.GetHostAddresses(Dns.GetHostName()).FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !IPAddress.IsLoopback(a))?.ToString() ?? "127.0.0.1"; }
        catch (System.Net.Sockets.SocketException) { return "127.0.0.1"; }
    }

    void ConfigureRoom(bool host, string address)
    {
        netLeaving = false; netNotice = ""; netOwn = 0; netPlayers = new NetPlayer[4]; netSlots.Clear(); netLocalInput = new NetInput();
        var go = new GameObject("DeliveryRoom"); netTransport = go.AddComponent<UnityTransport>(); netManager = go.AddComponent<NetworkManager>();
        netManager.NetworkConfig = new NetworkConfig { ProtocolVersion = 2, NetworkTransport = netTransport, EnableSceneManagement = false, ConnectionApproval = true, ForceSamePrefabs = false, TickRate = 30 };
        netTransport.SetConnectionData(address, netPort, host ? "0.0.0.0" : null);
        netManager.ConnectionApprovalCallback = (request, response) =>
        {
            int slot = Enumerable.Range(0,NetCapacity).Where(i => !netSlots.Values.Contains(i)).DefaultIfEmpty(-1).First();
            response.Approved = RoomState == RoomPhase.Lobby && slot >= 0;
            response.CreatePlayerObject = false; response.Pending = false; response.Reason = "방이 가득 찼거나 경기가 시작되었습니다.";
            if (response.Approved) netSlots[request.ClientNetworkId] = slot;
        };
        netManager.OnClientConnectedCallback += client =>
        {
            Debug.Log("DELIVERY connected id="+client+" server="+NetServer);
            if (!NetServer) { SendNet("hello", new NetInput { name = netName }, NetworkManager.ServerClientId); return; }
            if (!netSlots.TryGetValue(client, out int slot)) { slot = 0; netSlots[client] = slot; }
            var available = Enumerable.Range(0,CoatColors.Length).Where(c => !netPlayers.Any(p => p != null && p.connected && p.color == c)).ToArray();
            netPlayers[slot] = new NetPlayer { connected = true, team = slot % 2, color = available[UnityEngine.Random.Range(0,available.Length)], name = client == NetworkManager.ServerClientId ? netName : "배달원 " + (slot+1) };
            netInputs[slot] = new NetInput(); NetBroadcast();
        };
        netManager.OnClientDisconnectCallback += client =>
        {
            if (netLeaving) return; Debug.Log("DELIVERY disconnected id="+client+" reason="+netManager.DisconnectReason);
            if (!NetServer) { netNotice = "방장과의 연결이 끊어졌습니다."; RoomState = RoomPhase.Results; started = false; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; return; }
            if (netSlots.Remove(client, out int slot) && netPlayers[slot] != null) netPlayers[slot].connected = false;
            if (RoomState == RoomPhase.Playing) { RoomState = RoomPhase.Results; netNotice = "참가자의 연결 종료로 경기를 마쳤습니다."; finished = true; }
            if (RoomState == RoomPhase.Countdown) { RoomState = RoomPhase.Lobby; netNotice = "참가자가 나가 시작을 취소했습니다."; }
            NetBroadcast();
        };
    }
    public void HostRoom()
    {
        if (netManager) return; RoomState = RoomPhase.Lobby; ConfigureRoom(true,"127.0.0.1");
        if (!netManager.StartHost()) { LeaveRoom(); netNotice = "방을 열지 못했습니다. 다른 방이 실행 중인지 확인하세요."; return; }
        Debug.Log("DELIVERY host listening "+netPort);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if(netProof)netProofStage=1;
#endif
        RegisterNetHandlers(); netCode = EncodeRoom(LanAddress(), netPort); netNextSend = 0;
    }
    public void JoinRoom(string code)
    {
        if (!DecodeRoom(code, out var address, out var port)) { netNotice = "방 코드를 다시 확인해 주세요."; return; }
        if (netManager) return; netPort = port; RoomState = RoomPhase.Connecting; ConfigureRoom(false,address); netConnectedAt = Time.unscaledTime;
        if (!netManager.StartClient()) { LeaveRoom(); netNotice = "접속을 시작하지 못했습니다."; return; }
        RegisterNetHandlers(); netCode = code; Debug.Log("DELIVERY joining "+address+":"+netPort);
    }
    void RegisterNetHandlers()
    {
        foreach (string type in new[] { "hello", "input", "start", "frame", "impact", "reset", "item" })
        {
            string kind = type;
            netManager.CustomMessagingManager.RegisterNamedMessageHandler("delivery/"+kind, (sender, reader) =>
            {
                if (reader.Length > 65536 || (!NetServer && sender != NetworkManager.ServerClientId)) return;
                try { reader.ReadValueSafe(out string json); ReceiveNet(kind,sender,json); }
                catch (Exception ex) when (ex is OverflowException || ex is ArgumentException) { Debug.LogWarning("Rejected invalid room message"); }
            });
        }
    }
    void SendNet(string kind, object payload, ulong recipient, NetworkDelivery delivery = NetworkDelivery.ReliableSequenced)
    {
        if (!netManager || !netManager.IsListening) return;
        string json = JsonUtility.ToJson(payload); using var writer = new FastBufferWriter(json.Length*2+16, Allocator.Temp);
        writer.WriteValueSafe(json); netManager.CustomMessagingManager.SendNamedMessage("delivery/"+kind,recipient,writer,delivery);
    }
    void ReceiveNet(string kind, ulong sender, string json)
    {
        if (kind == "frame" && !NetServer) { ApplyNetFrame(JsonUtility.FromJson<NetFrame>(json)); return; }
        if (kind == "impact" && !NetServer) { NetSurfaceImpact(JsonUtility.FromJson<NetImpact>(json)); return; }
        if (!NetServer || !netSlots.TryGetValue(sender,out int slot)) return;
        if (kind == "input")
        {
            var input = JsonUtility.FromJson<NetInput>(json);
            if (!float.IsFinite(input.x) || !float.IsFinite(input.z) || !float.IsFinite(input.yaw) || !float.IsFinite(input.pitch)) return;
            input.x = Mathf.Clamp(input.x,-1,1); input.z = Mathf.Clamp(input.z,-1,1); input.slot = Mathf.Clamp(input.slot,0,2);
            input.pitch = Mathf.Clamp(input.pitch,-80,80); input.yaw = Mathf.Repeat(input.yaw,360); netInputs[slot] = input;
        }
        if (kind == "hello") { var n = JsonUtility.FromJson<NetInput>(json).name ?? "배달원"; netPlayers[slot].name = n.Substring(0,Mathf.Min(n.Length,16)).Replace("\n", " ").Replace("\r", " "); NetBroadcast(); }
        if (kind == "start" && slot == 0) StartNetCountdown(true);
        if (kind == "reset" && slot == 0 && NetDevelopment) StartNetRound();
        if (kind == "item" && NetDevelopment && NetPlaying) { var i = JsonUtility.FromJson<NetInput>(json); if (i.slot >= 1 && i.slot <= 9) netPlayers[slot].items[netPlayers[slot].selected] = i.slot; }
    }

    void NetMenuWorld()
    {
        started = false; finished = false; netPause = false;
        if (focusedTarget) focusedTarget.SetFocus(false,false); focusedTarget = null;
        if (heldWeapon) heldWeapon.SetActive(false); if (heldCamera) heldCamera.enabled = false;
        for (int i=0;i<3;i++) if (bots[i]) bots[i].SetActive(false);
        if (playerVisual) playerVisual.SetFirstPerson(true);
        cam.transform.position = new Vector3(14,16,-19); cam.transform.LookAt(new Vector3(0,0,8));
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
    }
    public void LeaveRoom()
    {
        netLeaving = true; if (netManager) { netManager.Shutdown(); Destroy(netManager.gameObject); }
        netManager = null; netTransport = null; netSlots.Clear(); netPlayers = new NetPlayer[4]; netCode = "";
        foreach (var shot in FindObjectsByType<BrawlShot>()) if (shot.owner == this) Destroy(shot.gameObject);
        foreach (var view in netShotViews.Values) if (view) Destroy(view); netShotViews.Clear();
        ClearNetPlungers(); ClearIceCreamMarks(); ClearSlimePuddles(); RoomState = RoomPhase.Menu; NetMenuWorld();
    }
    void ReturnNetLobby()
    {
        if(!NetServer||RoomState!=RoomPhase.Results)return;
        RoomState=RoomPhase.Lobby;started=false;finished=false;NetMenuWorld();netNotice="";NetBroadcast();
    }
    void StartNetCountdown(bool development)
    {
        if (!NetServer || RoomState != RoomPhase.Lobby || NetCount < (development && NetDevelopment ? 2 : NetCapacity)) return;
        netNotice = NetCount < NetCapacity ? "개발용 인원 미달 테스트" : ""; RoomState = RoomPhase.Countdown; netPhaseUntil = Time.time+3; NetBroadcast();
    }
    public void StartNetRound()
    {
        if (!NetServer || NetCount < 2) return;
        ClearSlimePuddles(); ClearIceCreamMarks(); ClearNetPlungers();
        foreach (var shot in FindObjectsByType<BrawlShot>()) if (shot.owner == this) Destroy(shot.gameObject);
        Array.Fill(owner,-1); for(int i=0;i<10;i++) SetMailboxEffectColor(i,Color.white);
        if (parcelPool) parcelPool.ResetRound();
        for(int g=0;g<4;g++)
        {
            var p=netPlayers[g]; if(p==null||!p.connected) continue;
            var actor=ActorController(g); actor.gameObject.SetActive(true); actor.enabled=false;
            actor.transform.position = new Vector3(p.team==0 ? -5 : 5,1,(g/2)*3-1.5f); actor.enabled=true;
            actor.transform.rotation=Quaternion.Euler(0,p.team==0?90:270,0);
            var state=new DeliveryItemState { safePosition=actor.transform.position };
            if(g==0)playerItems=state;else botItems[g-1]=state;
            p.stamina=100;p.vertical=0;p.dashUntil=0;p.cooldown=0;p.target=-1;p.items=new[]{2,5,7};p.selected=1;p.actionAt=-10;
            netInputs[g]=new NetInput {yaw=p.team==0?90:270,slot=1};
            p.used=p.dashed=p.dropped=0;
        }
        netLocalInput=new NetInput {yaw=netPlayers[0].team==0?90:270,slot=1};
        RoomState=RoomPhase.Playing;started=true;finished=false;netRoundUntil=Time.time+roundSeconds;netPhaseUntil=0; netPause=false;
        SyncHostInventory(); NetBroadcast();
        Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;
    }

    bool TickNetwork()
    {
        if(!NetworkScreens||mapEditMode)return false;
        if(RoomState==RoomPhase.Connecting && Time.unscaledTime-netConnectedAt>10){LeaveRoom();netNotice="접속 시간이 초과되었습니다. 방 코드와 네트워크를 확인하세요.";}
        var kb=Keyboard.current;
        if(NetDevelopment&&kb!=null&&kb.f2Key.wasPressedThisFrame)netTools=!netTools;
        if(NetPlaying)
        {
            if(kb!=null&&kb.escapeKey.wasPressedThisFrame){netPause=!netPause;Cursor.lockState=netPause?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=netPause;}
            ReadNetInput();
            if(NetServer){netInputs[0]=netLocalInput;StepNetPlayers();UpdateNetPlungers();UpdateNetMelee();UpdateItemAreas();
                if(Time.time>=netRoundUntil){RoomState=RoomPhase.Results;finished=true;netNotice="";NetBroadcast();Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}}
            SyncHostInventory(); UpdateCameraForNet(); UpdateInteraction();
        }
        if(NetServer&&RoomState==RoomPhase.Lobby&&NetCount==NetCapacity)StartNetCountdown(false);
        if(NetServer&&RoomState==RoomPhase.Countdown&&Time.time>=netPhaseUntil)StartNetRound();
        if(netManager&&netManager.IsListening&&Time.unscaledTime>=netNextSend)
        {netNextSend=Time.unscaledTime+.05f;if(NetServer)NetBroadcast();else if(NetPlaying)SendNet("input",netLocalInput,NetworkManager.ServerClientId,NetworkDelivery.UnreliableSequenced);}
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        TickNetworkProof();
#endif
        return true;
    }
    void ReadNetInput()
    {
        var k=Keyboard.current;var mouse=Mouse.current; var i=netLocalInput;
        i.x=i.z=0;i.jump=i.crouch=i.capture=false;
        if(k==null||netPause)return;
        i.x=(k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0);i.z=(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0);
        i.jump=k.spaceKey.isPressed;i.crouch=k.cKey.isPressed;i.capture=k.eKey.isPressed;
        if(k.digit1Key.wasPressedThisFrame)i.slot=0;if(k.digit2Key.wasPressedThisFrame)i.slot=1;if(k.digit3Key.wasPressedThisFrame)i.slot=2;
        if(k.gKey.wasPressedThisFrame)i.drop++;if(k.leftShiftKey.wasPressedThisFrame)i.dash++;
        if(mouse!=null&&Cursor.lockState==CursorLockMode.Locked){var d=mouse.delta.ReadValue();i.yaw+=d.x*.12f;i.pitch=Mathf.Clamp(i.pitch-d.y*.12f,-80,80);if(mouse.leftButton.wasPressedThisFrame)i.use++;}
    }
    void UpdateCameraForNet()
    {crouching=netLocalInput.crouch;rollUntil=netPlayers[netOwn]?.dashUntil??0;lookPitch=netLocalInput.pitch;player.transform.rotation=Quaternion.Euler(0,netLocalInput.yaw,0);cam.transform.rotation=Quaternion.Euler(lookPitch,netLocalInput.yaw,0);PositionCamera();cam.fieldOfView=Mathf.Lerp(cam.fieldOfView,RunningShoesFieldOfView,Time.deltaTime*12);}

    void NetBroadcast()
    {
        if(!NetServer||netManager.CustomMessagingManager==null)return;
        for(int g=0;g<4;g++)if(netPlayers[g]!=null&&netPlayers[g].connected)
        {
            var p=netPlayers[g];var a=ActorController(g);var s=ActorState(g);p.dashRemaining=Mathf.Max(0,p.dashUntil-Time.time);p.position=a.transform.position;p.yaw=a.transform.eulerAngles.y;p.actionAge=Time.time-p.actionAt;
            p.stun=Mathf.Max(0,s.stunUntil-Time.time);p.frozen=Mathf.Max(0,s.frozenUntil-Time.time);p.shield=Mathf.Max(0,s.shieldUntil-Time.time);
            p.shieldBreak=Time.time-s.shieldBrokenAt;p.speed=Mathf.Max(0,s.speedUntil-Time.time);p.slow=Mathf.Max(0,s.slowUntil-Time.time);p.ink=Mathf.Max(0,s.visionBlockedUntil-Time.time);
            p.progress=p.target<0?0:Mathf.Clamp01((Time.time-p.captureAt)/(p.items[p.selected]==8?1.5f:3));
        }
        var shots=FindObjectsByType<BrawlShot>().Where(s=>s.owner==this&&s.gameObject.activeSelf).Select(s=>new NetShot{id=s.GetEntityId().ToString(),kind=(int)s.kind,position=s.transform.position,rotation=s.transform.rotation}).ToList();
        foreach(var p in netPlungers)if(p.model)shots.Add(new NetShot{id=p.model.GetEntityId().ToString(),kind=2,position=p.model.transform.position,rotation=p.model.transform.rotation});
        foreach(var pair in netSlots)if(pair.Key!=NetworkManager.ServerClientId&&netManager.ConnectedClientsIds.Contains(pair.Key))
            SendNet("frame",new NetFrame{own=pair.Value,teamSize=NetTeamSize,phase=(int)RoomState,remaining=RoomState==RoomPhase.Countdown?Mathf.Max(0,netPhaseUntil-Time.time):Mathf.Max(0,netRoundUntil-Time.time),players=netPlayers,owners=owner,parcels=parcelPool ? parcelPool.CaptureSlots() : Array.Empty<int>(),shots=shots.ToArray(),notice=netNotice},pair.Key,NetworkDelivery.ReliableFragmentedSequenced);
    }
    void ApplyNetFrame(NetFrame f)
    {
        if(f.players==null||f.players.Length!=4||f.owners==null||f.owners.Length!=10||f.own<0||f.own>3)return;
        bool starting=(RoomPhase)f.phase==RoomPhase.Playing&&RoomState!=RoomPhase.Playing;
        NetTeamSize=f.teamSize==1?1:2;netOwn=f.own;netPlayers=f.players;RoomState=(RoomPhase)f.phase;netNotice=f.notice;netRoundUntil=Time.time+f.remaining;netPhaseUntil=Time.time+f.remaining;
        started=NetPlaying;finished=RoomState==RoomPhase.Results;
        if(starting){ClearSlimePuddles();ClearIceCreamMarks();netPause=false;netLocalInput=new NetInput{yaw=f.players[netOwn].yaw,slot=1};Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
        if(finished){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
        for(int g=0;g<4;g++)
        {
            int l=NetLocal(g);var a=ActorController(l);var p=f.players[g];if(l>0)a.gameObject.SetActive(p!=null&&p.connected&&NetPlaying);
            if(p==null||!p.connected)continue;
            if(NetPlaying){a.enabled=false;a.transform.SetPositionAndRotation(p.position,Quaternion.Euler(0,p.yaw,0));a.enabled=true;}
            p.actionAt=Time.time-p.actionAge;p.dashUntil=Time.time+p.dashRemaining;
            var state=ActorState(l);bool freshInk=p.ink>0&&state.visionBlockedUntil<=Time.time;
            state.stunUntil=Time.time+p.stun;state.frozenUntil=Time.time+p.frozen;state.shieldUntil=Time.time+p.shield;state.shieldBrokenAt=p.shieldBreak<1?Time.time-p.shieldBreak:float.NegativeInfinity;
            state.speedUntil=Time.time+p.speed;state.slowUntil=Time.time+p.slow;state.visionBlockedUntil=Time.time+p.ink;
            if(freshInk)ShowFountainInkHit(l,a.bounds.center);
            SetPlayerColor(l,CoatColors[p.color]);
        }
        for(int i=0;i<10;i++){int v=f.owners[i]<0?-1:NetLocal(f.owners[i]);owner[i]=v;SetMailboxEffectColor(i,v<0?Color.white:CaptureColor(v));}
        if(parcelPool) parcelPool.ApplySnapshot(f.parcels);
        SyncHostInventory();SyncNetShotViews(f.shots??Array.Empty<NetShot>());
    }
    void SyncHostInventory()
    {
        var p=netPlayers[netOwn];if(p==null)return;
        for(int i=0;i<3;i++)inventory[i]=(WeaponKind)p.items[i];selectedSlot=p.selected;stamina=p.stamina;
        captureTarget=p.target;captureStarted=Time.time-p.progress*(Selected==WeaponKind.WorkGloves?1.5f:3);
        for(int g=0;g<4;g++)if(netPlayers[g]!=null)SetPlayerColor(NetLocal(g),CoatColors[netPlayers[g].color]);
    }
    void SyncNetShotViews(NetShot[] shots)
    {
        var seen=new HashSet<string>();foreach(var shot in shots)
        {
            seen.Add(shot.id);if(!netShotViews.TryGetValue(shot.id,out var model))
            {var prefab=Resources.Load<GameObject>("DeliveryItems/"+(WeaponKind)shot.kind);if(!prefab)continue;model=Instantiate(prefab);model.name="RemoteProjectile";netShotViews[shot.id]=model;}
            model.transform.SetPositionAndRotation(shot.position,shot.rotation);
        }
        foreach(var id in netShotViews.Keys.Where(id=>!seen.Contains(id)).ToArray()){Destroy(netShotViews[id]);netShotViews.Remove(id);}
    }
    public void ReplicateNetImpact(WeaponKind kind,Vector3 point,Vector3 normal,Collider hit)
    {
        if(!NetPlaying||!NetServer||hit&&hit.GetComponentInParent<CharacterController>())return;
        foreach(var client in netManager.ConnectedClientsIds)if(client!=NetworkManager.ServerClientId)SendNet("impact",new NetImpact{kind=(int)kind,point=point,normal=normal},client);
    }
    void NetSurfaceImpact(NetImpact p)
    {
        if(!NetPlaying)return;Collider surface=null;
        foreach(var h in Physics.RaycastAll(p.point+p.normal*.2f,-p.normal,.5f,~0,QueryTriggerInteraction.Ignore))if(!h.collider.GetComponentInParent<CharacterController>()){surface=h.collider;break;}
        if(!surface)return;
        if(p.kind==5)IceCreamImpact(surface,p.point,0,p.normal);
        if(p.kind==9)FountainPenImpact(surface,p.point,0,p.normal);
        if(p.kind==4)SlimeImpact(surface,p.point,0,p.normal);
    }
}

