using System;
using System.Diagnostics;
using Unity.Netcode;
using UnityEngine;

public sealed partial class MonsterDeliveryPrototype
{
    float netStatsAt,netPingMs,netAckMs,netReceiveHz,netReceiveGapMs,netLastReceive;
    float netSendKB,netReceiveKB,netWorkMs,netWorkAlloc;
    int netSentBytes,netReceivedBytes,netReceivedFrames;
    bool netAllocAvailable;
    void ResetNetDiagnostics()
    {
        netStatsAt=Time.unscaledTime;netLastReceive=0;
        netPingMs=netAckMs=netReceiveHz=netReceiveGapMs=netSendKB=netReceiveKB=netWorkMs=netWorkAlloc=0;
        netSentBytes=netReceivedBytes=netReceivedFrames=0;
        long allocated=GC.GetAllocatedBytesForCurrentThread();GC.KeepAlive(new byte[128]);netAllocAvailable=GC.GetAllocatedBytesForCurrentThread()>allocated;
    }
    void TickNetDiagnostics()
    {
        float age=Time.unscaledTime-netStatsAt;if(age<1)return;
        netSendKB=netSentBytes/(1024f*age);netReceiveKB=netReceivedBytes/(1024f*age);netReceiveHz=netReceivedFrames/age;
        netSentBytes=netReceivedBytes=netReceivedFrames=0;netStatsAt=Time.unscaledTime;
        if(!netTransport||!netManager||!netManager.IsListening)return;
        if(!NetServer){netPingMs=netTransport.GetCurrentRtt(NetworkManager.ServerClientId);return;}
        ulong rtt=0;
        foreach(var id in netManager.ConnectedClientsIds)
            if(id!=NetworkManager.ServerClientId)rtt=Math.Max(rtt,netTransport.GetCurrentRtt(id));
        netPingMs=rtt;
    }
    void RecordNetFrameReceived()
    {
        if(netLastReceive>0)netReceiveGapMs=(Time.unscaledTime-netLastReceive)*1000;
        netLastReceive=Time.unscaledTime;netReceivedFrames++;
    }
    void RecordNetWork(long ticks,long allocated)
    {
        float ms=(float)((Stopwatch.GetTimestamp()-ticks)*1000.0/Stopwatch.Frequency);
        float bytes=GC.GetAllocatedBytesForCurrentThread()-allocated;
        netWorkMs=Mathf.Lerp(netWorkMs,ms,.1f);netWorkAlloc=Mathf.Lerp(netWorkAlloc,bytes,.1f);
    }
}
