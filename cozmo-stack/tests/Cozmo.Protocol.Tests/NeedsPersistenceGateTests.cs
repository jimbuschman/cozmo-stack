using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using System.Text;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

// The MISSING assertions read the process-static SteppedBehavior.MissingReported; parallel engines contaminate them.
[Collection("SteppedBehavior missing-report statics")]
public sealed class NeedsPersistenceGateTests
{
    // reach: production — public startup with a host state directory, real ticker, and public persistence entries.
    // Literal file input substitutes host disk contents only; no persistence/clock adapter is enabled.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HostStateDirectoryNeverAppliesReadsRestorationDecayOrWrites(bool existing)
    {
        string root=Directory.CreateTempSubdirectory("needs-device-gate-").FullName;
        string file=Path.Combine(root,"needsState.json"),copy=Path.Combine(root,"other.json");
        const string saved="{\"_StateFileVersion\":5,\"_DateTime\":1700000000,\"_SerialNumber\":1234,\"CurNeedLevel\":{\"Repair\":25000,\"Energy\":50000,\"Play\":75000}}";
        var messages=new ConcurrentQueue<string>();
        void Diagnostic(string s){if(s.Contains("M15-030 needs device persistence skipped"))messages.Enqueue(s);}
        SteppedBehavior.MissingReported+=Diagnostic;
        try
        {
            Directory.CreateDirectory(Path.Combine(root,"animations"));
            File.WriteAllText(Path.Combine(root,"animations","a.json"),"{\"a\":[]}");
            if(existing)File.WriteAllText(file,saved);
            float[]? initial=null;
            using var robot=CozmoRobot.Create(engineOptions:new CozmoEngineOptions
            {
                ResourcesPath=root,BlockPoolPath="",NeedsDeviceDirectory=root,
                BeforeStart=e=>initial=new[]{NeedId.Repair,NeedId.Energy,NeedId.Play}.Select(n=>(float)e.Needs.State.GetNeedLevel(n)).ToArray()
            });
            Wait(()=>robot.Engine.IsReadyDelivered,"device startup");
            Assert.Equal(new[]{1f,1f,1f},initial); // Native default config; seeded disk levels must not be consumed.
            var needs=robot.Engine.Needs;
            Assert.Equal(0u,needs.SerialNumber);Assert.False(needs.DeviceDataPresent);
            Assert.False(needs.Load(file));Assert.False(needs.AttemptReadFromDevice(file));
            needs.ApplyDecayForTimeSinceLastDeviceWrite(false);
            Assert.Equal(new[]{1f,1f,1f},new[]{NeedId.Repair,NeedId.Energy,NeedId.Play}.Select(n=>(float)needs.State.GetNeedLevel(n)));
            needs.WriteDeviceFile(true);needs.WriteToDevice!(false);needs.Save(copy);
            long tick=robot.Engine.Timer.TickCount;Wait(()=>robot.Engine.Timer.TickCount>=tick+4,"device normal ticks");
            if(existing)Assert.Equal(saved,File.ReadAllText(file));else Assert.False(File.Exists(file));
            Assert.False(File.Exists(copy));Assert.Single(messages);
        }
        finally{SteppedBehavior.MissingReported-=Diagnostic;Directory.Delete(root,true);}
    }

    // reach: unit (controlled engine Update time substitutes waiting 61 seconds; default owner keeps production gate).
    [Fact]
    public void PeriodicNeedsWriteStillReachesTheGatedDeviceEntry()
    {
        string root=Directory.CreateTempSubdirectory("needs-periodic-gate-").FullName;
        try
        {
            using var robot=CozmoRobot.CreateOffline(engineOptions:new CozmoEngineOptions{NeedsDeviceDirectory=root});
            robot.Engine.Needs.Update(61); // Actual rate limiter and bound writer; no legacy persistence opt-in.
            Assert.False(File.Exists(Path.Combine(root,"needsState.json")));
        }
        finally{Directory.Delete(root,true);}
    }

    // reach: production — public startup/connect, real ticker, transport frames, actual NV FIFO and default owner.
    // The v5 input follows the checked M15-017 layout; it is NOT an official-app capture (fixture MISSING).
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RealConnectNeverRestoresOrWritesUnbuiltNeedsPersistence(bool hasV5)
    {
        string root=Directory.CreateTempSubdirectory("needs-persistence-gate-").FullName;
        var missing=new ConcurrentQueue<string>();
        void Diagnostic(string s){if(s.Contains("M15-030/M15-014"))missing.Enqueue(s);}
        SteppedBehavior.MissingReported+=Diagnostic;
        try
        {
            Directory.CreateDirectory(Path.Combine(root,"animations"));
            File.WriteAllText(Path.Combine(root,"animations","a.json"),"{\"a\":[]}");
            var results=new ConcurrentQueue<NvResult>();
            using var robot=CozmoRobot.Create(engineOptions:new CozmoEngineOptions
            {
                ResourcesPath=root,BlockPoolPath="",BeforeStart=e=>
                {e.ConnectionNeedsRead+=results.Enqueue;}
            });
            Wait(()=>robot.Engine.IsReadyDelivered,"startup");
            Assert.Throws<InvalidOperationException>(()=>robot.Engine.Needs.EnableUncertifiedPersistenceForTests());
            var nvCommands=new ConcurrentQueue<NVCommand>();var seen=new HashSet<ushort>();
            int ack=0;object traceGate=new();
            robot.Transport.FrameTrace+=f=>
            {
                if(!f.Outbound||f.Frame is not {} frame)return;
                lock(traceGate)
                {
                    ack=Math.Max(ack,frame.SeqMax);
                    foreach(var m in frame.Messages)
                        if(m.Type is ReliableMessageType.SingleReliableMessage or ReliableMessageType.SingleUnreliableMessage
                           && (!m.IsReliable||seen.Add(m.Seq)) && RobotMessage.Parse(m.Payload) is NVCommand cmd)
                            nvCommands.Enqueue(cmd);
                }
            };
            ushort sequence=1;
            void FrameIn(ReliableMessageType type,byte[] body)
            {
                ushort a;lock(traceGate)a=(ushort)ack;
                robot.Transport.ProcessIncoming(FrameCodec.Encode(Frame.Single(new SubMessage(type,body,sequence++),a)));
            }
            void Data(RobotMessage message)=>FrameIn(ReliableMessageType.SingleReliableMessage,message.ToBytes());
            robot.ConnectToRobot(IPAddress.Loopback,isSimulated:true);
            Wait(()=>robot.Engine.ConnectionState==1,"connect state 1");
            // The transport opens its connection asynchronously; a ConnectionResponse injected before that is dropped.
            Wait(()=>robot.Transport.Connection is not null,"transport connection");
            FrameIn(ReliableMessageType.ConnectionResponse,Array.Empty<byte>());
            Wait(()=>robot.Engine.ConnectionState==2,"connect state 2");
            Data(new FirmwareVersion{RobotId=1,Signature=Encoding.UTF8.GetBytes("{\"sim\":true}")});
            Wait(()=>robot.Engine.RobotValidated==true,"firmware");
            Data(new ManufacturingID{SerialNumber=0x1234,BodyHwVersion=5});
            Data(new SyncTimeAck());Data(new RobotState{Timestamp=1,PoseOriginId=1});
            var sent=new List<NVCommand>();byte[] body=V5SourceLayout();
            var date=typeof(NeedsManager).GetField("_stateDateTimeSec",BindingFlags.Instance|BindingFlags.NonPublic)!;
            double initialDate=(double)date.GetValue(robot.Engine.Needs)!;
            var deadline=DateTime.UtcNow.AddSeconds(15);
            while(DateTime.UtcNow<deadline)
            {
                while(nvCommands.TryDequeue(out var cmd))
                {
                    sent.Add(cmd);
                    bool needs=cmd.Tag==0x194000&&cmd.Op==0&&hasV5;
                    Data(new NVOpResult{Tag=cmd.Tag,Op=cmd.Op,Result=needs||cmd.Op!=0?(sbyte)0:(sbyte)-1,
                        Data=needs?Envelope(body):Array.Empty<byte>()});
                }
                if(!results.IsEmpty&&robot.Engine.NvStorage!.IsIdle&&robot.Engine.Robot!.ReadyToStream)break;
                Thread.Sleep(5);
            }
            Assert.True(robot.Engine.NvStorage!.IsIdle,"connection NV queue did not drain");
            var result=Assert.Single(results);
            Assert.Equal(hasV5?0:-1,result.Result);
            if(hasV5)Assert.Equal(body,result.Data); // Proves the NV envelope was accepted, not a rejected fixture.
            var needsOwner=robot.Engine.Needs;
            Assert.False(needsOwner.RobotReadSucceeded);Assert.False(needsOwner.HasRobotCopy);
            Assert.False(needsOwner.AwaitingRobotData);Assert.Equal(initialDate,(double)date.GetValue(needsOwner)!);
            // Direct public calls cannot bypass the gate either.
            needsOwner.StartWriteToRobot(1_700_000_100);needsOwner.PossiblyStartWriteToRobot(true);
            needsOwner.InitAfterReadFromRobotAttempt();Assert.Equal(0,needsOwner.StartReadFromRobot());
            Assert.False(needsOwner.FinishReadFromRobot(body,0));
            long tick=robot.Engine.Timer.TickCount;
            Wait(()=>robot.Engine.Timer.TickCount>=tick+4,"post-resolution ticks");
            while(nvCommands.TryDequeue(out var cmd))sent.Add(cmd);
            Assert.Single(sent.Where(c=>c.Tag==0x194000));
            Assert.DoesNotContain(sent,c=>c.Tag==0x194000&&c.Op!=0);
            Assert.Single(missing);
        }
        finally{SteppedBehavior.MissingReported-=Diagnostic;Directory.Delete(root,true);}
    }

    // reach: unit (direct lifecycle calls substitute reconnect; tests the per-connection diagnostic scope only).
    [Fact]
    public void PersistenceMissingIsReportedOnceAgainOnEachConnection()
    {
        var messages=new List<string>();
        void Diagnostic(string s){if(s.Contains("M15-030/M15-014"))messages.Add(s);}
        SteppedBehavior.MissingReported+=Diagnostic;
        try
        {
            var needs=new NeedsManager(()=>0);
            needs.InitAfterConnection();needs.StartWriteToRobot(1);needs.StartWriteToRobot(2);
            Assert.Single(messages);
            needs.InitAfterConnection();needs.StartWriteToRobot(3);needs.StartWriteToRobot(4);
            Assert.Equal(2,messages.Count);
        }
        finally{SteppedBehavior.MissingReported-=Diagnostic;}
    }

    private static void Wait(Func<bool> ready,string stage)=>
        Assert.True(SpinWait.SpinUntil(ready,TimeSpan.FromSeconds(10)),stage);

    // Literal offsets from M15-017; deliberately does not call the implementation's Pack.
    private static byte[] V5SourceLayout()
    {
        var b=new byte[0x74];
        BinaryPrimitives.WriteUInt32LittleEndian(b,5);
        BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan(4),1_700_000_000);
        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(0xC),25000);
        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(0x10),50000);
        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(0x14),75000);
        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(0x34),7);
        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(0x38),3);
        b[0x3C]=1;
        BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan(0x5C),1_699_999_900);
        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(0x64),2);
        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(0x68),1);
        BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan(0x6C),1_600_000_000);
        return b;
    }
    private static byte[] Envelope(byte[] body)
    {
        var b=new byte[16+body.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(b,0x435A4D4F);
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(8),(uint)body.Length);
        body.CopyTo(b,16);return b;
    }
}
