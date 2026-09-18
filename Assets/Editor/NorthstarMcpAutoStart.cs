using System.Threading.Tasks;
using MCPForUnity.Editor.Services;
using MCPForUnity.Editor.Services.Transport;
using UnityEditor;

[InitializeOnLoad]
static class NorthstarMcpAutoStart
{
    static NorthstarMcpAutoStart()
    {
        EditorPrefs.SetBool("MCPForUnity.AutoStartOnLoad", true);
        EditorApplication.delayCall += Connect;
    }

    static async void Connect()
    {
        if (MCPServiceLocator.TransportManager.IsRunning(TransportMode.Http)) return;
        if (!MCPServiceLocator.Server.IsLocalHttpServerReachable()) MCPServiceLocator.Server.StartLocalHttpServer(quiet: true);
        for (var i = 0; i < 20 && !MCPServiceLocator.Server.IsLocalHttpServerReachable(); i++) await Task.Delay(500);
        if (MCPServiceLocator.Server.IsLocalHttpServerReachable()) await MCPServiceLocator.Bridge.StartAsync();
    }
}
