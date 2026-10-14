using System.Net;
using Cozmo.Robot;
using Cozmo.Transport;
using Xunit;
namespace Cozmo.Protocol.Tests;
public sealed class M3M4X22BuilderTests:IDisposable
{
    private readonly string _temp=Directory.CreateTempSubdirectory("b3422-").FullName;
    public void Dispose()
    {
        string target=Path.GetFullPath(_temp);Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()),target,StringComparison.OrdinalIgnoreCase);
        Directory.Delete(target,true);
    }
    private sealed class Port:IEngineTransport
    {
        public bool TimedOut=>false;public event Action<ReceiverEvent>? Received{add{}remove{}}
        public void Start(){}public void Connect(IPAddress ip,bool simulated){}public void Disconnect(IPEndPoint address){}public void SendData(byte[] bytes){}
    }
    private CozmoRobot Robot(string cache)=>CozmoRobot.CreateForTest(new Port(),()=>0,new CozmoEngineOptions{AnimationCachePath=cache,DasDeviceIdPath=Path.Combine(_temp,Guid.NewGuid()+".id")});
    [Fact] public void TwoInstancesCreateOnlyTheirLeafDirectoriesAndFactoryJsonStateIsInMemory()
    {
        string cache=_temp+"/cache/";using var robot=Robot(cache);robot.Engine.InitOfflineLink(new(IPAddress.Loopback,5551));var sets=robot.Engine.Robot!.DevBehaviors;
        Assert.Equal(2,sets.Length);Assert.NotSame(sets[0].Factory.Logger,sets[1].Factory.Logger);
        foreach(var set in sets)
        {
            Assert.Equal(cache+"/dockingTest",set.Docking.LoggerDirectory);Assert.Equal(cache+"/liftLoadTest",set.Lift.LoggerDirectory);
            Assert.Equal(".log",set.Docking.LoggerExtension);Assert.Equal(0x1400000u,set.Docking.LoggerSize);Assert.Equal(0x1400000u,set.Lift.LoggerSize);
            Assert.Equal(".json",set.Factory.Logger.Extension);Assert.True(set.Factory.Logger.JsonMode);Assert.Equal(FirmwareJsonKind.Null,set.Factory.Logger.Root.Kind);Assert.False(set.Factory.Logger.FileOpen);
            Assert.Equal(0u,set.Lift.Field11C);Assert.Equal(0u,set.Lift.Field120);Assert.Equal(0u,set.Lift.Field124);Assert.Equal(0,set.Lift.Field218);Assert.Equal(1,set.Lift.Field219);
            Assert.Equal(0u,set.Lift.All);Assert.Equal(0u,set.Lift.Loads);Assert.Equal(0,set.Lift.Field224);
        }
        Assert.True(Directory.Exists(cache+"/dockingTest"));Assert.True(Directory.Exists(cache+"/liftLoadTest"));Assert.Empty(Directory.GetFiles(cache,"*",SearchOption.AllDirectories));
    }
    [Fact] public void FailedDirectoryCreationIsIgnoredAndLeavesTheExistingFile()
    {
        string blocked=Path.Combine(_temp,"blocked");File.WriteAllText(blocked,"keep");using var robot=Robot(blocked);var logs=new List<EngineLogEntry>();robot.Engine.LogEntry+=logs.Add;
        robot.Engine.InitOfflineLink(new(IPAddress.Loopback,5551));Assert.Equal(2,robot.Engine.Robot!.DevBehaviors.Length);Assert.Equal("keep",File.ReadAllText(blocked));
        Assert.DoesNotContain(logs,e=>e.Name.Contains("RollingFileLogger") || e.Name.Contains("CreateDirectory"));
        Assert.False(Directory.Exists(blocked+"/dockingTest"));Assert.False(Directory.Exists(blocked+"/liftLoadTest"));
    }
    [Fact] public void LiftExternalSubscriptionsArePerInstanceAndManagedStorageDoesNotLeakAcrossRemoval()
    {
        using var robot=Robot(_temp+"/cache");var hub=robot.Engine.AnimationMessages;int before35=hub.Count(0x35,true),before95=hub.Count(0x95,false);
        robot.Engine.InitOfflineLink(new(IPAddress.Loopback,5551));Assert.Equal(before35+2,hub.Count(0x35,true));Assert.Equal(before95+2,hub.Count(0x95,false));
        robot.Engine.Robots.RemoveRobot(CozmoEngine.RobotId,false);Assert.Equal(before35,hub.Count(0x35,true));Assert.Equal(before95,hub.Count(0x95,false));
    }
}
