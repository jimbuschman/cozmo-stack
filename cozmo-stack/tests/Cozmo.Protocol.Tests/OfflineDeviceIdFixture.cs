using System.Runtime.CompilerServices;
using Cozmo.Robot;
namespace Cozmo.Protocol.Tests;
internal static class OfflineDeviceIdFixture
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        var root=Directory.CreateTempSubdirectory("cozmo-test-device-id-").FullName;
        CozmoEngine.DasDeviceIdPathForTest=Path.Combine(root,"device-id");
        // A pre-existing fixture input also prevents concurrent offline constructors racing their initial writes.
        File.WriteAllText(CozmoEngine.DasDeviceIdPathForTest,Guid.NewGuid().ToString("D")+"\n",new System.Text.UTF8Encoding(false));
        AppDomain.CurrentDomain.ProcessExit+=(_,_)=>{try{Directory.Delete(root,true);}catch(IOException){}catch(UnauthorizedAccessException){}};
    }
}
