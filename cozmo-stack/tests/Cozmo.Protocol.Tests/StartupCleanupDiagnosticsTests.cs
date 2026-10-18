using System.Net;
using System.Reflection;
using System.Text;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Transport;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class StartupCleanupDiagnosticsTests
{
    // reach: unit (substitutes the constructor-owned transport to observe teardown; actual constructor/error/cleanup runs).
    [Theory] [InlineData("?")] [InlineData("{}")]
    public void FailedEngineConstructionDisposesOwnedTransportAndEndsWorkers(string config)
    {
        using var transport=ReliableTransport.CreateOffline();
        Thread Worker(string field)
        {
            var owner=typeof(ReliableTransport).GetField(field,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(transport)!;
            return (Thread)owner.GetType().GetProperty("WorkerThread",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(owner)!;
        }
        var executor=Worker("_exec");var scheduler=Worker("_deferred");
        Assert.True(executor.IsAlive);Assert.True(scheduler.IsAlive);
        var constructor=typeof(CozmoRobot).GetConstructors(BindingFlags.Instance|BindingFlags.NonPublic).Single(c=>c.GetParameters().Length==7);
        bool flag=EngineErrorState.ErrorFlagSet;
        try
        {
            var error=Assert.Throws<TargetInvocationException>(()=>constructor.Invoke(new object?[]{null,transport,
                new CozmoEngineOptions{BlockPoolPath="",EngineConfigJson=Encoding.UTF8.GetBytes(config)},null,null,null,null}));
            Assert.Contains("EngineConfigJson",Assert.IsType<ArgumentException>(error.InnerException).Message);
            Assert.Throws<ObjectDisposedException>(()=>transport.Connect(IPAddress.Loopback));
            Assert.False(executor.IsAlive);Assert.False(scheduler.IsAlive);
        }
        finally{EngineErrorState.ErrorFlagSet=flag;}
    }

    // reach: production — received B0/EC packets -> MessageHandler -> retained TracePrinter diagnostic callbacks.
    [Fact]
    public void RobotTraceUsesResourceNamesAndRetainsRawWordArgumentsAndWifiIdentity()
    {
        string root=Directory.CreateTempSubdirectory("trace-readiness-").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(root,"config/engine"));
            File.WriteAllText(Path.Combine(root,"config/engine/AnkiLogStringTables.json"),"{\"nameTable\":{\"5\":\"sensor\"},\"formatTable\":{\"7\":\"v=%d\"}}");
            using var rig=new M4ControlTests.Rig(engineOptions:new CozmoEngineOptions{BlockPoolPath="",ResourcesPath=root});rig.ToSynced();
            var stack=new List<string>();var logs=new List<EngineLogEntry>();rig.Engine.StackLogLine+=stack.Add;rig.Engine.LogEntry+=logs.Add;
            int mark=rig.Mark();
            rig.Data(new PrintTrace{FormatId=0xAABB0007,NameId=5,Level=-1,Args=new[]{42,-7}});rig.Tick();
            rig.Data(new WiFiFlashID{Field0=0x10203});rig.Tick();
            Assert.Contains(stack,line=>line.Contains("Robot.Trace level=-1 formatWord=0xAABB0007 [sensor] v=%d [42, -7]"));
            var wifi=Assert.Single(logs.Where(l=>l.Name=="hardware.wifi.flash_id"));
            Assert.Equal("10203",wifi.Text);Assert.Equal("66051",wifi.Data["$data"]);
            Assert.DoesNotContain(rig.RawSince(mark),bytes=>bytes.Length>0 && bytes[0]==0xEC);
        }
        finally{Directory.Delete(root,true);}
    }

    // reach: production — real packet delivery with absent optional diagnostic tables; no readiness replacement input.
    [Fact]
    public void TraceWithoutStringTablesStillReportsIdentifiersAndArguments()
    {
        using var rig=new M4ControlTests.Rig();rig.ToSynced();var lines=new List<string>();rig.Engine.StackLogLine+=lines.Add;
        rig.Data(new PrintTrace{FormatId=9,NameId=8,Level=2,Args=new[]{-1}});rig.Tick();
        Assert.Contains(lines,line=>line.Contains("Robot.Trace level=2 formatWord=0x00000009 [name#8] fmt#9 [-1]"));
    }
}
