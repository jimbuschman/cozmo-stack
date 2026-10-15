using System.Net;
using Cozmo.Robot;
using Cozmo.Transport;
using Xunit;
namespace Cozmo.Protocol.Tests;
[Collection("M5 process statics")]
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
    [Fact] public void OfflineAndSharedRigUseDistinctOwnedCachesAndDeleteThemAfterDisposal()
    {
        string productionDefault=Path.Combine(Path.GetTempPath(),"cozmo-stack");
        Assert.Equal(productionDefault,new CozmoEngineOptions().AnimationCachePath);
        string first,second;
        using(var offline=CozmoRobot.CreateOffline())
        using(var rig=new M4ControlTests.Rig())
        {
            first=offline.Engine.Options.AnimationCachePath;second=rig.Engine.Options.AnimationCachePath;
            Assert.NotEqual(productionDefault,first);Assert.NotEqual(productionDefault,second);Assert.NotEqual(first,second);
            Assert.True(Directory.Exists(first));Assert.True(Directory.Exists(second));
            rig.Engine.InitOfflineLink(new(IPAddress.Loopback,5551));
            Assert.True(Directory.Exists(first+"/dockingTest"));Assert.True(Directory.Exists(second+"/liftLoadTest"));
        }
        Assert.False(Directory.Exists(first));Assert.False(Directory.Exists(second));
    }
    [Fact] public void IsolatingDefaultCacheKeepsAllOtherExplicitTestSettings()
    {
        var options=new CozmoEngineOptions{
            FilesPath=_temp,ResourcesPath=_temp+"/resources",BlockPoolPath="",
            DasDeviceIdPath=_temp+"/id",DataPlatformPersistentPath=_temp+"/persist",
            DataCollectionEnabled=false,VerboseLogDisplay=true,RobotVolume=.25f,
        };
        string cache;
        using(var robot=CozmoRobot.CreateForTest(new Port(),()=>0,options))
        {
            var actual=robot.Engine.Options;cache=actual.AnimationCachePath;
            Assert.NotEqual(options.AnimationCachePath,cache);Assert.Equal(_temp,actual.FilesPath);
            Assert.Equal(options.ResourcesPath,actual.ResourcesPath);Assert.Equal(options.BlockPoolPath,actual.BlockPoolPath);
            Assert.Equal(options.DasDeviceIdPath,actual.DasDeviceIdPath);Assert.Equal(options.DataPlatformPersistentPath,actual.DataPlatformPersistentPath);
            Assert.Equal(options.DataCollectionEnabled,actual.DataCollectionEnabled);Assert.Equal(options.VerboseLogDisplay,actual.VerboseLogDisplay);
            Assert.Equal(options.RobotVolume,actual.RobotVolume);
        }
        Assert.False(Directory.Exists(cache));Assert.Equal(Path.Combine(Path.GetTempPath(),"cozmo-stack"),options.AnimationCachePath);
    }
    [Fact] public void ConstructorFailureRemovesItsOwnedCache()
    {
        string? cache=null;
        Assert.Throws<InvalidOperationException>(()=>CozmoRobot.CreateForTest(new Port(),()=>0,engineCreated:engine=>
        {cache=engine.Options.AnimationCachePath;throw new InvalidOperationException("test constructor failure");}));
        Assert.NotNull(cache);Assert.False(Directory.Exists(cache));
    }
    [Fact] public void LiftCallbackBodyGapsAreReportedWhenTheConstructorRuns()
    {
        using var robot=Robot(_temp+"/cache");var gaps=new List<string>();robot.Engine.StackLogLine+=gaps.Add;
        robot.Engine.InitOfflineLink(new(IPAddress.Loopback,5551));
        Assert.Equal(2,gaps.Count(g=>g.Contains("M7-028 LiftLoadTest[")&&g.Contains("0x35/0x95 callback bodies")));
        Assert.Equal(2,robot.Engine.AnimationMessages.Count(0x35,true));Assert.Equal(2,robot.Engine.AnimationMessages.Count(0x95,false));
    }
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
