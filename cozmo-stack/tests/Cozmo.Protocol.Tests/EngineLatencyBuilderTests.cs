using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

public sealed class EngineLatencyBuilderTests
{
    // reach: production — public startup and pause API, real ticker; local publication/tick hooks only observe/order inputs.
    [Theory]
    [InlineData(false,11,22)]
    [InlineData(true,16,27)]
    public async Task LatencyUsesElevenEligibleTicksAndFreezesItsCountdownWhilePaused(bool pause,long first,long second)
    {
        string root=Directory.CreateTempSubdirectory("latency-startup-").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(root,"animations"));
            File.WriteAllText(Path.Combine(root,"animations","a.json"),"{\"a\":[]}");
            var ticks=new ConcurrentQueue<long>();var bodies=new ConcurrentQueue<byte[]>();
            var delivered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            int reports=0;
            using var robot=CozmoRobot.Create(engineOptions:new CozmoEngineOptions
            {
                ResourcesPath=root,BlockPoolPath="",BeforeStart=e=>
                {
                    e.AnimationMessages.SubscribeMessage(0x77,true,_=>ticks.Enqueue(e.Timer.TickCount));
                    e.EngineToGameMessage+=(tag,data)=>{if(tag==0x77)bodies.Enqueue(data.ToArray());};
                    e.Latency+=_=>{if(Interlocked.Increment(ref reports)==2)delivered.TrySetResult();};
                    e.TickCompleted+=()=>
                    {
                        if(!pause)return;
                        if(e.Timer.TickCount==5)e.SetGameBeingPaused(true);
                        if(e.Timer.TickCount==10)e.SetGameBeingPaused(false);
                    };
                }
            });
            await delivered.Task.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal(new[]{first,second},ticks.Take(2));
            var body=bodies.First();Assert.Equal(88,body.Length);
            // Native field order: 9 zero net globals; four empty avg/min/max triples; image current zero without robot.
            uint[] expected={0,0,0,0,0,0,0,0,0,0,0x7F800000,0xFF800000,0,0x7F800000,0xFF800000,
                0,0x7F800000,0xFF800000,0,0x7F800000,0xFF800000,0};
            Assert.Equal(expected,Enumerable.Range(0,22).Select(i=>BinaryPrimitives.ReadUInt32LittleEndian(body.AsSpan(i*4,4))));
        }
        finally{Directory.Delete(root,true);}
    }

    // reach: unit (direct samples substitute arrival delays; exact two-window accumulator is production code).
    [Fact]
    public void RecentStatsPreservesBothWindowsAndTheNativeResetBoundaries()
    {
        var stats=new RecentStatsAccumulator(100);
        Assert.Same(stats.First,stats.Selected);
        for(int i=1;i<=50;i++)stats.Add(i);
        Assert.Equal(0,stats.Second.Count);
        stats.Add(51);Assert.Equal(1,stats.Second.Count);
        for(int i=52;i<=100;i++)stats.Add(i);
        Assert.Same(stats.First,stats.Selected);Assert.Equal(50.5,stats.Selected.Mean);
        stats.Add(101);
        Assert.Equal(0,stats.First.Count);Assert.Equal(0,stats.First.Sum);
        Assert.Equal(double.MaxValue,stats.First.Min);Assert.Equal(-double.MaxValue,stats.First.Max);
        Assert.Same(stats.Second,stats.Selected);
        Assert.Equal(51,stats.Second.Count);Assert.Equal(3876,stats.Second.Sum);
        Assert.Equal(51,stats.Second.Min);Assert.Equal(101,stats.Second.Max);
        Assert.Equal(76,stats.Second.Mean);Assert.Equal(75.5,stats.Second.PreviousMean);Assert.Equal(11050,stats.Second.M2);
        for(int i=102;i<=151;i++)stats.Add(i);
        Assert.Equal(0,stats.Second.Count);Assert.Same(stats.First,stats.Selected);
        Assert.Equal(50,stats.First.Count);Assert.Equal(6325,stats.First.Sum);
        Assert.Equal(102,stats.First.Min);Assert.Equal(151,stats.First.Max);
        Assert.Equal(126.5,stats.First.Mean);Assert.Equal(126,stats.First.PreviousMean);Assert.Equal(10412.5,stats.First.M2);
    }

    // reach: unit (direct samples; expected IEEE words calculated from the checked f64 instruction order).
    [Fact]
    public void RecentStatsMeanAndM2UseTheIncrementalDoubleOperations()
    {
        var stats=new RecentStatsAccumulator(100);
        stats.Add(1e16);stats.Add(1);stats.Add(1);
        Assert.Equal(0x4327AF4C4A80AAACL,BitConverter.DoubleToInt64Bits(stats.First.Mean));
        Assert.Equal(0x468A4B9CF15C92C8L,BitConverter.DoubleToInt64Bits(stats.First.M2));
    }

    // reach: unit (controlled net clock and peer markers substitute real arrival timing; actual RCD push/pop and report).
    [Fact]
    public void ArrivalQueueSamplesAllKindsSkipsZeroAndPublishesItsSelectedStats()
    {
        using var rig=new M4ControlTests.Rig();
        double netMs=0;rig.Engine.NetTimeStampMs=()=>netMs;
        var payloads=new List<byte[]>();rig.Engine.EngineToGameMessage+=(tag,data)=>{if(tag==0x77)payloads.Add(data.ToArray());};
        rig.Port.Raise(ReceiverMarker.OnConnected,M4ControlTests.Rig.RobotEp);
        netMs=100;rig.Tick(0);Assert.Equal(0,rig.Engine.Rcm.ReceiveQueueStats.Selected.Count);
        netMs=100.25;rig.Port.Raise(ReceiverMarker.OnConnected,M4ControlTests.Rig.RobotEp);
        netMs=100.75;rig.Tick(0);
        netMs=101;rig.Data(new RobotPoked());netMs=102.5;rig.Tick(0);
        netMs=103;rig.Port.Raise(ReceiverMarker.OnDisconnected,M4ControlTests.Rig.RobotEp);
        netMs=103.25;rig.Tick(0);
        var stats=rig.Engine.Rcm.ReceiveQueueStats.Selected;
        Assert.Equal(3,stats.Count);Assert.Equal(.75,stats.Mean);Assert.Equal(.25,stats.Min);Assert.Equal(1.5,stats.Max);
        Assert.Equal(.875,stats.M2);
        netMs=200;rig.Port.Raise(ReceiverMarker.OnConnected,M4ControlTests.Rig.RobotEp);rig.Tick(0);
        Assert.Equal(4,stats.Count);Assert.Equal(.5625,stats.Mean);Assert.Equal(0,stats.Min);Assert.Equal(1.296875,stats.M2);
        for(int i=0;payloads.Count==0&&i<11;i++)rig.Tick(0);
        var body=Assert.Single(payloads);
        Assert.Equal(.5625f,BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(body.AsSpan(36,4))));
        Assert.Equal(0f,BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(body.AsSpan(40,4))));
        Assert.Equal(1.5f,BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(body.AsSpan(44,4))));
    }

    // reach: unit (controlled engine clock and a complete synthetic image substitute robot timing/pixels; real dispatch and update).
    [Fact]
    public void CurrentImageLatencyUsesImplMinusOneAndOnlyUpdatesOnTheFullRobotPath()
    {
        using var rig=new M4ControlTests.Rig();rig.ToSynced();
        var native=rig.Engine.Robot!;
        Assert.Equal(-1,native.LastCompletedImageEventTime);
        rig.NowNs=2_000_000_000;rig.Tick(0);
        Assert.Equal(0,native.CurrentImageLatencySeconds); // first-full-state early exit.
        rig.Data(rig.MakeState());rig.Tick(0);
        Assert.Equal(2,native.CurrentImageLatencySeconds); // now 1.0 - initial -1.0.
        rig.Data(new ImageChunk{ImageId=1,FrameTimestamp=1,ImageResolution=4,ImageEncoding=5,ImageChunkCount=1,Data=new byte[]{0}});
        rig.Tick(0);Assert.Equal(1,native.LastCompletedImageEventTime);Assert.Equal(0,native.CurrentImageLatencySeconds);
        rig.NowNs=2_250_000_000;rig.Tick(0);Assert.Equal(.25,native.CurrentImageLatencySeconds);
        rig.Engine.HasCameraCalibration=()=>true;rig.Engine.VisionUpdateAllResultsFailed=()=>true;
        rig.NowNs=3_000_000_000;rig.Tick(0);Assert.Equal(.25,native.CurrentImageLatencySeconds);
        rig.Engine.VisionUpdateAllResultsFailed=()=>false;rig.Tick(0);Assert.Equal(1,native.CurrentImageLatencySeconds);
        var reports=new List<LatencyMessage>();rig.Engine.Latency+=reports.Add;
        for(int i=0;reports.Count==0&&i<11;i++)rig.Tick(0);
        Assert.Equal(1000,Assert.Single(reports).Image.CurrentTimeMs);
    }

    // reach: unit (controlled 0.3s clock and synthetic frame; real image handler, Robot.Update and app report).
    [Fact]
    public void ImageLatencyPromotesTheTickFloatBeforeSubtractingTheDoubleEventTime()
    {
        using var rig=new M4ControlTests.Rig();rig.ToSynced();
        rig.Data(rig.MakeState());
        rig.Data(new ImageChunk{ImageId=1,FrameTimestamp=1,ImageResolution=4,ImageEncoding=5,ImageChunkCount=1,Data=new byte[]{0}});
        var reports=new List<LatencyMessage>();rig.Engine.Latency+=reports.Add;
        rig.Tick(0);
        for(int i=0;reports.Count==0&&i<11;i++)rig.Tick(0);
        // now_f32 = 0x3E99999A, event_f64 = 0x3FD3333333333333; difference * 1000 then f32.
        Assert.Equal(0x37480000,BitConverter.SingleToInt32Bits(Assert.Single(reports).Image.CurrentTimeMs));
    }

    // reach: production — public startup/connect, real framed transport input and dispatch; BeforeStart hosts receive C3 FIFO reports.
    [Fact]
    public async Task RecoveredHandlersRunThroughRealDispatchAndRobotPokedUsesTheAppFifo()
    {
        string root=Directory.CreateTempSubdirectory("settling-handlers-").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(root,"animations"));
            File.WriteAllText(Path.Combine(root,"animations","a.json"),"{\"a\":[]}");
            var logs=new ConcurrentQueue<EngineLogEntry>();var print=new ConcurrentQueue<string>();
            var order=new ConcurrentQueue<string>();var sizes=new ConcurrentQueue<int>();
            var done=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);int poked=0;
            using var robot=CozmoRobot.Create(engineOptions:new CozmoEngineOptions
            {
                ResourcesPath=root,BlockPoolPath="",BeforeStart=e=>
                {
                    e.LogEntry+=logs.Enqueue;e.StackLogLine+=print.Enqueue;
                    e.EngineToGameMessage+=(tag,data)=>{if(tag==0x33){sizes.Enqueue(data.Length);order.Enqueue("raw");}};
                    e.RobotPoked+=_=>{order.Enqueue("typed");if(Interlocked.Increment(ref poked)==2)done.TrySetResult();};
                }
            });
            Assert.True(SpinWait.SpinUntil(()=>robot.Engine.IsReadyDelivered,TimeSpan.FromSeconds(10)));
            robot.ConnectToRobot(IPAddress.Loopback,isSimulated:true);
            Assert.True(SpinWait.SpinUntil(()=>robot.Engine.ConnectionState==1,TimeSpan.FromSeconds(5)));
            void FrameIn(ReliableMessageType type,byte[] body,ushort seq)=>robot.Transport.ProcessIncoming(FrameCodec.Encode(
                Frame.Single(new SubMessage(type,body,seq),1)));
            FrameIn(ReliableMessageType.ConnectionResponse,Array.Empty<byte>(),1);
            Assert.True(SpinWait.SpinUntil(()=>robot.Engine.ConnectionState==2,TimeSpan.FromSeconds(5)));
            ushort sequence=2;
            void Data(RobotMessage m)=>FrameIn(ReliableMessageType.SingleReliableMessage,m.ToBytes(),sequence++);
            Data(new FirmwareVersion{RobotId=1,Signature=Encoding.UTF8.GetBytes("{\"sim\":true}")});
            Assert.True(SpinWait.SpinUntil(()=>robot.Engine.RobotValidated==true,TimeSpan.FromSeconds(5)));
            Data(new ManufacturingID{SerialNumber=1,BodyHwVersion=5});
            Data(new RobotPoked());
            Data(new RampTraverseStart());Data(new RampTraverseComplete());
            Data(new BridgeTraverseStart());Data(new BridgeTraverseComplete());
            Data(new MainCycleTimeError{Field0=7,Field1=13,Field2=11,Field3=17});
            Data(new DataDump{Field0=999,Field1=new byte[]{0,0x1F,0xA0,0xFF}});
            Data(new PrintText{Field0=123,Field1="fixture"});
            Data(new GoalPose()); // B3 Viz-only terminals produce no report or state mutation.
            Data(new RobotPoked());
            await done.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(new[]{"raw","typed","raw","typed"},order);
            Assert.Equal(new[]{0,0},sizes);
            Assert.Equal(2,logs.Count(l=>l.Name=="Robot.HandleRobotPoked"&&l.Level==EngineLogLevel.Info));
            foreach(string text in new[]{"started traversing a ramp.","completed traversing a ramp.","started traversing a bridge.","completed traversing a bridge."})
                Assert.Contains(logs,l=>l.Level==EngineLogLevel.Info&&l.Name=="RobotMessageHandler.ProcessMessage"&&l.Text.Contains(text));
            Assert.Contains(logs,l=>l.Name=="Robot.SetOnRamp.NoRampWithID"&&l.Level==EngineLogLevel.Warning&&l.Text.Contains("ID=-1 not found"));
            Assert.Contains(logs,l=>l.Name=="Robot.MainCycleTooLong"&&l.Text.Contains("Num errors: 7, Avg time: 17 us"));
            Assert.Contains(logs,l=>l.Name=="Robot.MainCycleTooLate"&&l.Text.Contains("Num errors: 11, Avg time: 13 us"));
            Assert.Contains(logs,l=>l.Name=="RobotMessageHandler.ProcessMessage.MessageDataDump"&&l.Text.Contains("ID: 1, size: 4, data: 001fa0ff"));
            Assert.Contains(print,s=>s.Contains("ROBOT-PRINT (1): fixture"));
        }
        finally{Directory.Delete(root,true);}
    }
}
