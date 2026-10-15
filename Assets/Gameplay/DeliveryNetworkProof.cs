#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public sealed partial class MonsterDeliveryPrototype
{
    [Serializable] sealed class ProofResult
    {public int own,players,expectedPlayers;public string mode;public bool parcelRelocated;public int parcelCount;public bool uniqueColors,teamsBalanced,sawLobby,sawPlaying,sawFrozen,sawOwnership,sawResults,remoteInput;public string[] checks;}
    bool netProof,netProofShot,netProofSaved;
    int netProofStage;
    float netProofStarted,netProofRoundStart;
    string netProofDirectory;
    Vector3 netProofMoveStart;
    int[] netProofParcelStart;
    bool netProofMoved,netProofDashed;
    ProofResult netProofResult=new();
    readonly List<string> netProofChecks=new();
    public void BeginNetworkProof(string directory=null)
    {
        netProof=true;netProofStage=0;netProofShot=netProofSaved=false;netProofStarted=Time.realtimeSinceStartup;
        netProofRoundStart=0;netProofMoved=netProofDashed=false;netProofParcelStart=null;
        netProofDirectory=string.IsNullOrEmpty(directory)?Path.Combine(Directory.GetCurrentDirectory(),"Docs/ChangeRecords/20261016/evidence/network"):directory;
        Directory.CreateDirectory(netProofDirectory);netProofResult=new ProofResult();netProofChecks.Clear();
        if(Application.isEditor)StartCoroutine(ProofCapture("network-main-menu"));
    }
    void ProofCheck(bool passed,string message)
    {netProofChecks.Add((passed?"PASS ":"FAIL ")+message);if(!passed)Debug.LogError("NET_PROOF: "+message);}
    void TickNetworkProof()
    {
        if(!netProof)return;
        netProofResult.expectedPlayers=NetCapacity;netProofResult.mode=NetMode;netProofResult.own=netOwn;netProofResult.players=Mathf.Max(netProofResult.players,NetCount);
        netProofResult.sawLobby|=RoomState==RoomPhase.Lobby||RoomState==RoomPhase.Countdown;
        if(NetCount==NetCapacity){netProofResult.uniqueColors=netPlayers.Where(p=>p!=null&&p.connected).Select(p=>p.color).Distinct().Count()==NetCapacity;netProofResult.teamsBalanced=netPlayers.Count(p=>p!=null&&p.connected&&p.team==0)==NetTeamSize&&netPlayers.Count(p=>p!=null&&p.connected&&p.team==1)==NetTeamSize;}
        if(NetPlaying)
        {
            netProofResult.sawPlaying=true;
            if(netProofRoundStart==0){netProofRoundStart=Time.realtimeSinceStartup;netProofMoveStart=netPlayers[NetCapacity==2?1:2].position;netProofParcelStart=parcelPool.CaptureSlots();}
            netProofResult.parcelCount=parcelPool.ActiveCount;
            netProofResult.parcelRelocated|=netProofParcelStart!=null&&!netProofParcelStart.SequenceEqual(parcelPool.CaptureSlots());
            float age=Time.realtimeSinceStartup-netProofRoundStart;
            netProofResult.sawFrozen|=Enumerable.Range(0,4).Any(g=>ActorState(NetLocal(g)).Frozen(Time.time));
            netProofResult.sawOwnership|=owner.Any(o=>o>=0);
            if(!NetServer&&netOwn==1&&age>2&&!netProofShot){netLocalInput.use++;netProofShot=true;}
            if(!NetServer&&netOwn==(NetCapacity==2?1:2)){netLocalInput.x=age>3.3f&&age<4?1:0;if(age>3.45f&&!netProofDashed){netLocalInput.dash++;netProofDashed=true;}}
            if(NetServer)
            {
                netProofMoved|=Vector3.Distance(netProofMoveStart,netPlayers[NetCapacity==2?1:2].position)>2;netProofDashed|=netPlayers[NetCapacity==2?1:2].dashed>0&&netPlayers[NetCapacity==2?1:2].stamina<100;
                if(netProofStage==1&&age>.8f)
                {
                    ProofCheck(netProofResult.uniqueColors,"all clients receive unique random coat colours");
                    ProofCheck(netProofResult.teamsBalanced,"room capacity splits into equal teams");
                    ProofCheck(DecodeRoom(EncodeRoom("127.0.0.1",7777),out var address,out var port)&&address=="127.0.0.1"&&port==7777,"room code round trips endpoint");
                    ProofCheck(!DecodeRoom("bad room",out _,out _),"invalid room code is rejected");
                    StartCoroutine(ProofCapture("network-gameplay"));netProofStage=2;
                }
                if(netProofStage==2&&netProofResult.sawFrozen){StartCoroutine(ProofCapture("network-client-ice-hit"));netProofStage=3;}
                if(age>5&&netProofStage<4)
                {
                    netProofResult.remoteInput=netPlayers[1].used>0;
                    ProofCheck(netProofMoved,"second client movement reaches host");
                    ProofCheck(netProofDashed,"client dash consumes authoritative stamina");
                    ProofCheck(netProofResult.remoteInput,"real client input reaches authoritative host");
                    ProofCheck(netProofResult.sawFrozen,"client-origin ice cream physically freezes the host");
                    playerItems=new DeliveryItemState{shieldUntil=Time.time+8};
                    if(NetCapacity==4){IceCreamImpact(player,player.bounds.center,2,Vector3.up);
                    ProofCheck(playerItems.shieldUntil>Time.time&&!playerItems.Frozen(Time.time),"team mate direct attacks do not spend aircap or freeze");}
                    IceCreamImpact(player,player.bounds.center,1,Vector3.up);
                    ProofCheck(playerItems.shieldUntil==0&&!playerItems.Frozen(Time.time),"opponent attack consumes aircap without freeze");
                    playerItems=new DeliveryItemState();
                    VerifyNetParcelPickup();
                    bool aimed=false;
                    foreach(var side in new[]{Vector3.forward,Vector3.back,Vector3.right,Vector3.left})
                    {
                        var centre=mailboxes[0].GetComponentInChildren<Collider>().bounds.center;
                        var position=centre+side*2;position.y=1;player.enabled=false;player.transform.position=position;player.enabled=true;Physics.SyncTransforms();
                        var rotation=Quaternion.LookRotation(centre-NetEye(0));var e=rotation.eulerAngles;
                        netInputs[0]=new NetInput{yaw=e.y,pitch=e.x>180?e.x-360:e.x,slot=1};
                        if(NetRay(0,NetEye(0),NetAim(0),3,.01f,out var hit,out _,out _)&&hit.GetComponentInParent<InteractionTarget>()==mailboxes[0].GetComponent<InteractionTarget>()){aimed=true;break;}
                    }
                    netPlayers[0].target=-1;NetCaptureAndPickup(0,true);netPlayers[0].captureAt=Time.time-3.1f;NetCaptureAndPickup(0,true);
                    ProofCheck(aimed&&owner[0]==0&&TeamScore(0)==1,"authoritative gaze capture awards a team mailbox");
                    ProofCheck(MailboxOwnedByUs(0),"team mailbox is marked already owned in local interaction UI");
                    netProofStage=4;NetBroadcast();
                }
                if(age>8&&netProofStage==4){netRoundUntil=Time.time-.1f;netProofStage=5;}
            }
        }
        if(RoomState==RoomPhase.Menu&&NetServer==false&&netProofStage==0&&Application.isEditor&&Time.realtimeSinceStartup-netProofStarted>1)
        {HostRoom();netProofStage=1;StartCoroutine(ProofCapture("network-lobby"));}
        if(RoomState==RoomPhase.Countdown&&Application.isEditor&&netProofStage==1&&!netProofShot){netProofShot=true;StartCoroutine(ProofCapture("network-"+NetCapacity+"-player-lobby"));}
        if(RoomState==RoomPhase.Results)
        {
            netProofResult.sawResults=true;
            if(NetServer&&netProofStage==5){ProofCheck(netProofResult.sawOwnership||owner.Any(o=>o>=0),"team ownership persists to results");ProofCheck(TeamScore(0)==1&&TeamScore(1)==0,"result scores match server mailbox ownership");StartCoroutine(ProofCapture("network-results"));netProofStage=6;}
            if(!netProofSaved){
                ProofCheck(netProofResult.parcelRelocated&&parcelPool.ActiveCount==30,"network snapshot delivers thirty parcels after relocation");
                ProofCheck(parcelPool.CaptureSlots().Where(i=>i>=0).Distinct().Count()==30,"network parcel anchors remain unique");
                if(!NetServer){ProofCheck(netProofResult.uniqueColors&&netProofResult.teamsBalanced,"client receives unique colours and balanced teams");ProofCheck(netProofResult.sawFrozen,"client receives server freeze state");ProofCheck(netProofResult.sawOwnership&&TeamScore(0)==1&&TeamScore(1)==0,"client ownership and result scores match host");}
                SaveNetworkProof();}
            if(!Application.isEditor&&Time.realtimeSinceStartup-netProofRoundStart>12)Application.Quit();
        }
        if(Time.realtimeSinceStartup-netProofStarted>60&&!netProofSaved){ProofCheck(false,"session timed out before results");SaveNetworkProof();if(!Application.isEditor)Application.Quit();}
    }
    void VerifyNetParcelPickup()
    {
        var parcel=netParcels.First(p=>p.transform.position.y<1);int index=Array.IndexOf(netParcels,parcel);int old=parcelPool.CaptureSlots()[index];
        bool aimed=false;
        foreach(var side in new[]{Vector3.forward,Vector3.back,Vector3.right,Vector3.left})
        {
            player.enabled=false;player.transform.position=parcel.transform.position+side*2+Vector3.up;player.enabled=true;Physics.SyncTransforms();
            var e=Quaternion.LookRotation(parcel.floating.position-NetEye(0)).eulerAngles;
            netInputs[0]=new NetInput{yaw=e.y,pitch=e.x>180?e.x-360:e.x,slot=2};
            if(NetRay(0,NetEye(0),NetAim(0),3,.01f,out var hit,out _,out _)&&hit.GetComponentInParent<FloatingParcel>()==parcel){aimed=true;break;}
        }
        netPlayers[0].items[2]=0;netPlayers[0].selected=2;NetCaptureAndPickup(0,true);
        ProofCheck(aimed&&parcelPool.ActiveCount==29&&netPlayers[0].items[2]>=1&&netPlayers[0].items[2]<=9,"authoritative E pickup grants one item and removes one parcel");
        parcelPool.Refill();
        ProofCheck(parcelPool.ActiveCount==30&&parcelPool.CaptureSlots()[index]!=old,"authoritative pickup refills a different empty spawn anchor");
        netPlayers[0].selected=1;
    }
    void SaveNetworkProof()
    {
        netProofSaved=true;netProofResult.checks=netProofChecks.ToArray();
        File.WriteAllText(Path.Combine(netProofDirectory,(NetServer?"host":"client-"+netOwn)+".json"),JsonUtility.ToJson(netProofResult,true));
        if(NetServer)File.WriteAllLines(Path.Combine(netProofDirectory,"host-checks.txt"),netProofChecks);
        Debug.Log("NETWORK_PROOF: "+(NetServer?"host":"client "+netOwn)+" completed");
    }
    public IEnumerator VerifyRoomLifecycle()
    {
        netProof=false;netProofChecks.Clear();
        if(RoomState==RoomPhase.Menu)HostRoom();
        yield return null;
        RoomState=RoomPhase.Results;ReturnNetLobby();ProofCheck(RoomState==RoomPhase.Lobby&&netManager.IsListening,"rematch returns to the same live lobby");
        LeaveRoom();yield return null;yield return null;
        ProofCheck(RoomState==RoomPhase.Menu&&!netManager,"leave returns to menu and clears network session");
        HostRoom();yield return null;
        ProofCheck(NetServer&&NetCount==1,"host can create another room after leaving");
        LeaveRoom();yield return null;yield return null;
        ProofCheck(RoomState==RoomPhase.Menu,"second leave restores menu");
        File.WriteAllLines(Path.Combine(Directory.GetCurrentDirectory(),"Docs/ChangeRecords/20261016/evidence/network/lifecycle-checks.txt"),netProofChecks);
        StartCoroutine(ProofCapture("network-menu-final"));
    }
    IEnumerator ProofCapture(string name)
    {
        if(Application.isBatchMode)yield break;
        if(name.Contains("player-lobby"))yield return new WaitForSeconds(.5f);
        yield return null;yield return new WaitForEndOfFrame();
        var image=ScreenCapture.CaptureScreenshotAsTexture();var folder=Path.Combine(Directory.GetCurrentDirectory(),"Docs/ChangeRecords/20261016/images");Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder,"20261016_"+(NetTeamSize==1?"1v1_":"")+name+".png"),image.EncodeToPNG());Destroy(image);
    }
}
#endif

