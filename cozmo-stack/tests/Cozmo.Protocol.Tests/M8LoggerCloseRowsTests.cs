using Cozmo.Robot;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("ObjectID process statics")]
public sealed class M8LoggerCloseRowsTests
{
    private sealed class FileBuffer(List<string> order,bool succeeds=true):IFactoryLogFileBuffer
    {
        public bool IsOpen {get;set;}=true;
        internal FirmwareJsonValue? Written;
        public void WriteJson(FirmwareJsonValue root){Written=root;order.Add("json");}
        public bool Close(){order.Add("close");IsOpen=false;return succeeds;}
    }
    [Theory]
    [InlineData(false,true)] [InlineData(true,true)] [InlineData(true,false)]
    public void ActualRobotLoggerCloseHasTheSourceLogJsonCloseAndStringResetOrder(bool json,bool closeSuccess)
    {
        using var robot=CozmoRobot.CreateOffline();robot.Engine.Robots.AddRobot(CozmoEngine.RobotId);
        var actual=robot.Engine.Robot!.DevBehaviors[0].Factory.Logger;
        // The real factory owner is JSON-mode; a non-JSON instance checks the alternate conditional body only.
        var logger=json?actual:new CheckedFactoryLogger(false);
        var order=new List<string>();var buffer=new FileBuffer(order,closeSuccess);
        logger.FileBuffer=buffer;logger.Field0="first";logger.Field0C="second";logger.StreamState=2;
        logger.SourceLog=(l,c,k,t)=>
        {
            Assert.Equal(EngineLogLevel.Info,l);Assert.Equal("Unnamed",c);Assert.Equal("FactoryTestLogger.CloseLog.Closing",k);Assert.Equal("",t);
            Assert.Equal("first",logger.Field0);Assert.Equal("second",logger.Field0C);order.Add("log");
        };
        logger.Dispose();
        Assert.Equal(json?new[]{"log","json","close"}:new[]{"log","close"},order);
        Assert.Equal("",logger.Field0);Assert.Equal("",logger.Field0C);Assert.False(logger.FileOpen);
        Assert.Equal(closeSuccess?2u:6u,logger.StreamState);
        if(json)Assert.Same(logger.Root,buffer.Written);else Assert.Null(buffer.Written);
        logger.Dispose();Assert.Equal(json?3:2,order.Count);
    }
    [Fact]
    public void ClosedProductionCleanupOnlyResetsStringsAndAddsNoClosingSourceEntry()
    {
        using var robot=CozmoRobot.CreateOffline();robot.Engine.Robots.AddRobot(CozmoEngine.RobotId);
        var native=robot.Engine.Robot!;var logs=new List<EngineLogEntry>();robot.Engine.LogEntry+=logs.Add;
        foreach(var set in native.DevBehaviors){Assert.False(set.Factory.Logger.FileOpen);set.Factory.Logger.Field0="held";set.Factory.Logger.Field0C="held";}
        robot.Engine.Robots.RemoveRobots(); // real DestroyRobot -> host dev-storage cleanup
        foreach(var set in native.DevBehaviors)
        {
            Assert.Equal("",set.Factory.Logger.Field0);Assert.Equal("",set.Factory.Logger.Field0C);
            Assert.Equal(4u,set.Docking.Logger.StreamState);Assert.Equal(4u,set.Lift.Logger.StreamState);
        }
        Assert.DoesNotContain(logs,e=>e.Name=="FactoryTestLogger.CloseLog.Closing");
    }
    [Theory]
    [InlineData(false,false)] [InlineData(false,true)] [InlineData(true,false)] [InlineData(true,true)]
    public void RollingCloseFollowsStopReleaseCloseAndSetsFailbit(bool queue,bool success)
    {
        using var robot=CozmoRobot.CreateOffline();robot.Engine.Robots.AddRobot(CozmoEngine.RobotId);
        var logger=robot.Engine.Robot!.DevBehaviors[0].Docking.Logger;
        var order=new List<string>();logger.FileBuffer=new FileBuffer(order,success);
        if(queue){logger.StopDispatch=()=>order.Add("stop");logger.ReleaseDispatch=()=>order.Add("release");}
        logger.Dispose();
        Assert.Equal(queue?new[]{"stop","release","close"}:new[]{"close"},order);
        Assert.Equal(success?0u:4u,logger.StreamState);
        logger.StopDispatch=null;logger.ReleaseDispatch=null;logger.FileBuffer=null; // native-input fixture has no live native queue/file ownership
    }

}
