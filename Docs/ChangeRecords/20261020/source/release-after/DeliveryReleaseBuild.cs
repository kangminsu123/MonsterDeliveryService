using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class DeliveryReleaseBuild
{
    const string Output="C:/Users/xlfhf/Documents/Codex/2026-10-20/durl/outputs";
    [Serializable] sealed class Result { public string platform,result,path,startedAt;public ulong bytes;public int errors,warnings;public double seconds;public int protocol=15; }
    [MenuItem("Tools/Gameplay/Release/Build Windows Package")]
    public static void Windows()=>Build(BuildTarget.StandaloneWindows64,"Windows/MonsterDeliveryService.exe");
    [MenuItem("Tools/Gameplay/Release/Build Mac Universal Package")]
    public static void Mac()=>Build(BuildTarget.StandaloneOSX,"macOS/MonsterDeliveryService.app");
    [MenuItem("Tools/Gameplay/Release/Restore Windows Editor")]
    public static void RestoreWindows()
    {
        if(EditorUserBuildSettings.activeBuildTarget!=BuildTarget.StandaloneWindows64)
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone,BuildTarget.StandaloneWindows64);
        PlayerSettings.SetArchitecture(NamedBuildTarget.Standalone,0);
        AssetDatabase.SaveAssets();
    }
    static void Build(BuildTarget target,string relative)
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before building");
        if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone,target))throw new InvalidOperationException("Missing platform module: "+target);
        string path=Output+"/MonsterDeliveryService_"+relative;
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var named=NamedBuildTarget.Standalone;var backend=PlayerSettings.GetScriptingBackend(named);int arch=PlayerSettings.GetArchitecture(named);
        string started=DateTimeOffset.Now.ToString("O");
        try
        {
            PlayerSettings.SetScriptingBackend(named,ScriptingImplementation.Mono2x);
            PlayerSettings.SetArchitecture(named,target==BuildTarget.StandaloneOSX?2:0);
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),locationPathName=path,target=target,options=BuildOptions.CompressWithLz4HC});
            var summary=report.summary;
            var result=new Result{platform=target.ToString(),result=summary.result.ToString(),path=path,startedAt=started,bytes=summary.totalSize,errors=(int)summary.totalErrors,warnings=(int)summary.totalWarnings,seconds=summary.totalTime.TotalSeconds};
            string evidence="Docs/ChangeRecords/20261020/evidence/build-"+(target==BuildTarget.StandaloneOSX?"macos":"windows")+".json";
            File.WriteAllText(evidence,JsonUtility.ToJson(result,true));
            if(summary.result!=BuildResult.Succeeded)throw new Exception("Build failed: "+target);
            Debug.Log("DELIVERY_RELEASE_BUILD_OK "+JsonUtility.ToJson(result));
        }
        finally { PlayerSettings.SetScriptingBackend(named,backend);PlayerSettings.SetArchitecture(named,arch); }
    }
}
