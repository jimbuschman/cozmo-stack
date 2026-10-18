using System.Net;
using Cozmo.Protocol;
using Cozmo.Robot;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class M3M4X29BuilderTests
{
    [Fact]
    public void ProductionCreateWithoutEngineOptionsSupportsNullResources()
    {
        // Actual production constructor, no fixture flag or resource override. No robot is contacted.
        using var robot=CozmoRobot.Create();
        Assert.Null(robot.Engine.Options.ResourcesPath);
        Assert.True(robot.Engine.HostBehaviorMapLoaded);
        Assert.Empty(robot.Engine.HostBehaviorMap!);
        Assert.Null(robot.Engine.Robot);
    }

    [Fact]
    public async Task ConnectAsyncWithoutEngineOptionsReachesCancellationInsteadOfConstructionFailure()
    {
        // Exercise the real Create -> ConnectAsync path using only localhost and a pre-cancelled wait.
        using var cancel=new CancellationTokenSource();cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>CozmoRobot.ConnectAsync(
            IPAddress.Loopback,isSimulated:true,cancellationToken:cancel.Token));
    }

    [Fact]
    // reach: unit (substitutes synced link and direct RemoveRobot; native disconnect join M1-044 is not exercised).
    public void MessagingRetiresTraceThenClosesImuThenRetiresImpl()
    {
        string root=Directory.CreateTempSubdirectory("b29-imu-").FullName;
        try
        {
            using var rig=new M4ControlTests.Rig(engineOptions:new CozmoEngineOptions{DataPlatformPersistentPath=root});
            rig.ToSynced();var robot=rig.Engine.Robot!;
            rig.Engine.Broadcast(new IMUDataChunk{SeqId=7,ChunkId=0,TotalNumChunks=2});
            Assert.True(robot.ImuLog.IsOpen);var order=new List<string>();
            rig.Engine.MessageSubscriptionReleasedForTest=(_,name)=>
            {
                if(name=="TracePrinter"){Assert.True(robot.ImuLog.IsOpen);order.Add("trace");}
                if(name=="Impl"){Assert.False(robot.ImuLog.IsOpen);order.Add("impl");}
            };
            rig.Engine.Robots.RemoveRobot(1,false);
            Assert.Equal("trace",order[0]);Assert.Contains("impl",order);Assert.Equal(3,order.Count(s=>s=="trace")); // CF, B0 and EC belong to TracePrinter.
            Assert.False(robot.ImuLog.IsOpen);Assert.DoesNotContain(rig.Log,l=>l.Contains("ClosingLogFile"));
            rig.Engine.MessageSubscriptionReleasedForTest=null;
        }
        finally{Directory.Delete(root,true);}
    }
}
