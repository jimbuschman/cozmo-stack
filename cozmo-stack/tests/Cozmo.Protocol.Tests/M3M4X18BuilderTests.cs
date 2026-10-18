using System.Collections.Concurrent;
using System.Text;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Cozmo.Robot.Manipulation;
using Cozmo.Transport;
using Xunit;
namespace Cozmo.Protocol.Tests;
[Collection("M5 process statics")]
public sealed class M3M4X18BuilderTests
{
    private static void Offline(M4ControlTests.Rig rig) => rig.Engine.InitOfflineLink(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback,5551));
    // reach: unit (direct Engine.Broadcast substitutes production ProcessMessages; tests actual constructor subscriber order).
    [Theory]
    [InlineData(0xED,"Impl,RIC")] [InlineData(0xC4,"TurnAction,HeadAction,LiftAction")] [InlineData(0xC0,"Impl")]
    [InlineData(0xB4,"BlockTapFilter,Impl")] [InlineData(0xB5,"BlockTapFilter,Impl")]
    [InlineData(0xB6,"BlockTapFilter")]
    [InlineData(0xC9,"Backup,Impl,RIC")]
    [InlineData(0xD2,"FactoryTest[0],FactoryTest[1],Impl,RIC")]
    [InlineData(0xEE,"FactoryTest[0],FactoryTest[1],Impl,RIC")]
    [InlineData(0xB8,"FactoryTest[0],FactoryTest[1],Impl")]
    [InlineData(0xF3,"FactoryTest[0],FactoryTest[1],Impl")]
    [InlineData(0xD3,"DockingTestSimple[0],DockingTestSimple[1],Impl")]
    [InlineData(0xDA,"LiftLoadTest[0],LiftLoadTest[1]")]
    [InlineData(0xD6,"FactoryTest[0],FactoryTest[1]")]
    public void CheckedPerTagRegistrationOrder(int tag,string expected)
    {
        using var rig=new M4ControlTests.Rig();rig.Engine.ConnectToRobot(M4ControlTests.Rig.RobotIp);
        // Fresh constructor-only handshake retains the RIC for D2/EE/C9 order checks.
        rig.Tick();rig.Port.Raise(ReceiverMarker.OnConnected,M4ControlTests.Rig.RobotEp);rig.Tick();
        if(tag==0xED){rig.Engine.Broadcast(new RobotAvailable());rig.Engine.Broadcast(new FirmwareVersion{Signature=Encoding.UTF8.GetBytes("{\"sim\":true}")});}
        using var turn=tag==0xC4?new TurnInPlaceAction(new MotorSource(rig.Engine.Robot!),0,false):null;
        if(tag==0xC4)rig.Robot.Motion.QueueHeadAndLiftCompound(0,32);
        var calls=new List<string>();rig.Engine.MessageDispatchObservedForTest=(t,n)=>{if((int)t==tag)calls.Add(n);};
        RobotMessage m=tag switch
        {
            0xED=>new ManufacturingID(),0xC4=>new MotorActionAck(),0xC0=>new CliffEvent(),0xB4=>new ObjectMoved{ObjectID=99},0xB5=>new ObjectStoppedMoving{ObjectID=99},0xB6=>new ObjectTapped{ObjectID=99},0xC9=>new RobotAvailable(),
            0xD2=>new FWVersionInfo(),0xEE=>new FirmwareVersion{Signature=Encoding.UTF8.GetBytes("{}")},
            0xB8=>new PickAndPlaceResult(),0xF3=>new ObjectAvailable(),0xD3=>new DockingStatus(),0xDA=>new LiftLoad(),_=>new FactoryTestParameter()
        };
        rig.Engine.Broadcast(m);Assert.Equal(expected.Split(','),calls);
    }
    [Fact]
    public void NativeSignalSeesAppendedNodesAndSkipsUnlinkedNodesIncludingSelfRemoval()
    {
        var signal=new SubscriptionSignal<int>();var calls=new List<string>();RetainedSubscription? second=null;
        signal.Subscribe(1,_=>{calls.Add("first");second!.Dispose();signal.Subscribe(1,_=>calls.Add("appended"));});
        second=signal.Subscribe(1,_=>calls.Add("removed"));
        RetainedSubscription? self=null;self=signal.Subscribe(1,_=>{calls.Add("self");self!.Dispose();});
        signal.Subscribe(2,_=>calls.Add("other-tag"));signal.Deliver(1,0);
        Assert.Equal(new[]{"first","self","appended"},calls);
    }
    // reach: unit (direct dispatch with a prelinked Robot; explicit app drain replaces the transport/tick boundary).
    [Fact]
    public void ImplVersionWarningsUseSpacedHexAndStickyFlagsWithEmptyStringsOnCurrentMatches()
    {
        using var rig=new M4ControlTests.Rig();Offline(rig);
        var logs=new List<EngineLogEntry>();var reports=new List<EngineRobotCLADVersionMismatch>();
        rig.Engine.LogEntry+=logs.Add;rig.Engine.CLADVersionMismatch+=reports.Add;
        var m=new FWVersionInfo{Field0=0x12345678,Field3=new byte[16],Field4=Convert.FromHexString("AB12BABA15729540B41D23167F2459A2")};
        rig.Engine.Broadcast(m);rig.Engine.DrainAppDelivery();
        var warning=Assert.Single(logs.Where(e=>e.Name=="RobotFirmware.VersionMismatch"));
        Assert.EndsWith("Robot's EngineToRobot hash = "+string.Concat(Enumerable.Repeat(" 00",16))+". Engine's EngineToRobot hash =  D5 35 42 A1 7B B8 97 69 D8 09 4E CE 5A 96 4A 9E.",warning.Text);
        Assert.Equal(EngineLogLevel.Warning,warning.Level);Assert.Empty(warning.Data);
        var report=Assert.Single(reports);Assert.Equal(0x2A,report.Tag);Assert.True(report.EngineToRobotMismatch);Assert.False(report.RobotToEngineMismatch);Assert.Equal("",report.EngineRobotToEngine);
        m.Field3=Convert.FromHexString("D53542A17BB89769D8094ECE5A964A9E");rig.Engine.Broadcast(m);rig.Engine.DrainAppDelivery();
        Assert.Equal(2,reports.Count);Assert.True(reports[1].EngineToRobotMismatch);
        Assert.Equal(new[]{"","","",""},new[]{reports[1].EngineEngineToRobot,reports[1].EngineRobotToEngine,reports[1].RobotEngineToRobot,reports[1].RobotRobotToEngine});
        m.Field4=new byte[16];rig.Engine.Broadcast(m);rig.Engine.DrainAppDelivery();Assert.True(reports[2].RobotToEngineMismatch);Assert.True(reports[2].EngineToRobotMismatch);
        Assert.Equal(m.ToBytes()[1..],rig.Engine.Robot!.LastFWVersionPayload);
    }
    [Fact]
    public void FactoryHandlersRunTwiceAndMalformedHeaderLatchesCancelQueueStateAndWarnings()
    {
        using var rig=new M4ControlTests.Rig();Offline(rig);var logs=new List<EngineLogEntry>();var stack=new List<string>();rig.Engine.LogEntry+=logs.Add;rig.Engine.StackLogLine+=stack.Add;
        rig.Engine.Broadcast(new FactoryTestParameter{Field0=unchecked((uint)-7)});
        Assert.Equal(2,logs.Count(e=>e.Name=="BehaviorFactoryTest.HandleFactoryTestParameter.Recvd"));
        rig.Engine.Broadcast(new ObjectAvailable{ObjectType=ObjectType.Charger_Basic});
        rig.Engine.Broadcast(new PickAndPlaceResult{Field1=true,Field3=2});
        rig.Engine.Broadcast(new FirmwareVersion{Signature=Encoding.UTF8.GetBytes("{\"version\":\"F1.5.1\",\"build\":\"FACTORY\"}")});
        foreach(var set in rig.Engine.Robot!.DevBehaviors)
        {Assert.Equal(-7,set.Factory.StationId);Assert.True(set.Factory.SawCubeOrCharger);Assert.True(set.Factory.PickAndPlacePassed);Assert.False(set.Factory.HeaderMismatch);}
        var action=new CancelProbe();rig.Engine.Robot.ActionList.QueueAction(QueueActionPosition.Now,action,0);rig.Engine.Robot.ActionList.Update();
        rig.Engine.Broadcast(new FirmwareVersion{Signature=Encoding.UTF8.GetBytes("{")});
        Assert.True(action.Cancelled);
        foreach(var set in rig.Engine.Robot.DevBehaviors)
        {
            Assert.Equal(0x46,set.Factory.Result);Assert.False(set.Factory.IsRunnableInternal);Assert.Equal((byte)15,set.Factory.CurrentState!.Value);
            Assert.Equal(1u,set.Factory.QueueCount);var q=Assert.Single(set.Factory.QueuedWrites);Assert.Equal(0x80000010u,q.Tag);Assert.Equal(-7,q.StationId);Assert.Null(q.Data);
            Assert.Equal(rig.Engine.Timer.TimeStampMs,set.Factory.TimestampEntries[15]);
        }
        Assert.Equal(2,logs.Count(e=>e.Name=="BehaviorFactoryTest.EndTest.PreWriteResult" && e.Text.EndsWith("PARSE_HEADER_FAILED")));
        Assert.Equal(2,logs.Count(e=>e.Name=="BehaviorFactoryTest.EndTest.PassFailStatus" && e.Text.EndsWith("prevPassed: 0, currPassed: 0")));
        rig.Engine.Broadcast(new FirmwareVersion{Signature=Encoding.UTF8.GetBytes("{")});Assert.Equal(2,logs.Count(e=>e.Name=="BehaviorFactoryTest.EndTest.TestAlreadyComplete"));
        rig.Engine.Broadcast(new FirmwareVersion{Signature=Encoding.UTF8.GetBytes("[]")});Assert.Equal(2,stack.Count(e=>e.Contains("skipping FactoryTest firmware handler fatal JSON")));
    }
    [Fact]
    public void DockingCountersResetAndLiftVirtualDispatchActsOnlyWhenRunning()
    {
        using var rig=new M4ControlTests.Rig();Offline(rig);var logs=new List<EngineLogEntry>();rig.Engine.LogEntry+=logs.Add;
        foreach(var set in rig.Engine.Robot!.DevBehaviors)set.Docking.InitInternal();
        rig.Engine.Broadcast(new DockingStatus{Field1=0});rig.Engine.Broadcast(new DockingStatus{Field1=1});rig.Engine.Broadcast(new DockingStatus{Field1=255});
        Assert.Equal(new[]{"STATUS_BACKING_UP","STATUS_DOING_HANNS_MANEUVER","(null)"},logs.Where(e=>e.Name=="robot.docking.status").Select(e=>e.Text));
        foreach(var set in rig.Engine.Robot.DevBehaviors)
        {Assert.Equal(1u,set.Docking.BackingUpCount);Assert.True(set.Docking.DidHanns);set.Docking.InitInternal();Assert.Equal(0u,set.Docking.BackingUpCount);Assert.False(set.Docking.DidHanns);}
        rig.Engine.Broadcast(new LiftLoad{Field0=true});Assert.All(rig.Engine.Robot.DevBehaviors,s=>Assert.Equal(0u,s.Lift.All));
        foreach(var set in rig.Engine.Robot.DevBehaviors)set.Lift.Running=true;
        rig.Engine.Broadcast(new LiftLoad{Field0=true});rig.Engine.Broadcast(new LiftLoad{Field0=false});
        Assert.All(rig.Engine.Robot.DevBehaviors,s=>{Assert.Equal(2u,s.Lift.All);Assert.Equal(1u,s.Lift.Loads);Assert.False(s.Lift.HasLoad);Assert.True(s.Lift.Received);});
        Assert.Equal(4,logs.Count(e=>e.Name=="BehaviorLiftLoadTest.HandleLiftLoad.HasLoad"));
    }
    [Fact]
    public void TapFilterWarningsPrecedeImplAndBackupMissingPrecedesHeadId()
    {
        using var rig=new M4ControlTests.Rig();Offline(rig);var order=new List<string>();
        rig.Engine.LogEntry+=e=>order.Add(e.Name);
        rig.Engine.Broadcast(new ObjectMoved{ObjectID=unchecked((uint)-1)});
        Assert.Equal("BlockTapFilterComponent.HandleActiveObjectMoved.ObjectIDNull",order[0]);Assert.Equal("Robot.ActiveObjectMoved.UnknownActiveID",order[1]);
        order.Clear();rig.Engine.Broadcast(new ObjectStoppedMoving{ObjectID=99});Assert.Equal("BlockTapFilterComponent.HandleActiveObjectStopped.ObjectIDNull",order[0]);
        order.Clear();rig.Engine.Broadcast(new RobotAvailable());Assert.Equal(new[]{"DataPlatform.writeAsJson","robot.handle_robot_set_head_id"},order);
    }
    [Fact]
    public void DeviceIdUtf8BomIsKeptAndSharedOfflineFixtureUsesATemporaryPath()
    {
        string root=Directory.CreateTempSubdirectory("bom18-").FullName;string path=Path.Combine(root,"id");
        try
        {
            File.WriteAllBytes(path,new byte[]{0xef,0xbb,0xbf,(byte)'x',10});
            using var robot=CozmoRobot.CreateForTest(new Port(),()=>0,new CozmoEngineOptions{DasDeviceIdPath=path});
            string expected=Convert.ToHexStringLower(System.Security.Cryptography.MD5.HashData(Encoding.UTF8.GetBytes("\uFEFFx")));
            Assert.Equal(expected,robot.Engine.Das.GetGlobalsForThisRun()["$player_id"]);
            Assert.StartsWith(Path.GetTempPath(),CozmoEngine.DasDeviceIdPathForTest!);
            using var offline=CozmoRobot.CreateOffline();Assert.Null(offline.Engine.Options.DasDeviceIdPath);Assert.True(File.Exists(CozmoEngine.DasDeviceIdPathForTest));
        }
        finally {Directory.Delete(root,true);}
    }
    [Fact]
    public void EDImplPrecedesRICAndRICSelfRemovalSkipsTheSecondLambda()
    {
        using var rig=new M4ControlTests.Rig();rig.Engine.ConnectToRobot(M4ControlTests.Rig.RobotIp);rig.Tick();
        rig.Port.Raise(ReceiverMarker.OnConnected,M4ControlTests.Rig.RobotEp);rig.Tick();
        rig.Engine.Broadcast(new RobotAvailable());
        var firmware=new FirmwareVersion{Signature=Encoding.UTF8.GetBytes("{\"version\":0,\"time\":0,\"build\":\"DEVELOPMENT\"}")};
        rig.Engine.Broadcast(firmware);rig.Engine.Broadcast(firmware);
        var calls=new List<string>();rig.Engine.MessageDispatchObservedForTest=(t,n)=>{if(t==RobotMessageId.MfgId)calls.Add(n);};
        rig.Engine.Broadcast(new ManufacturingID());Assert.Equal(new[]{"Impl","RIC"},calls);
    }
    [Fact]
    public void ActionC4SubscribersFollowConstructionAndDAFollowsDockInit()
    {
        using var rig=new M4ControlTests.Rig();Offline(rig);
        using var first=new TurnInPlaceAction(new MotorSource(rig.Engine.Robot!),0,false);
        rig.Robot.Motion.QueueHeadAndLiftCompound(0,32);
        using var last=new TurnInPlaceAction(new MotorSource(rig.Engine.Robot!),0,false);
        var calls=new List<string>();rig.Engine.MessageDispatchObservedForTest=(t,n)=>{if(t==RobotMessageId.MotorActionAck)calls.Add(n);};
        rig.Engine.Broadcast(new MotorActionAck());Assert.Equal(new[]{"TurnAction","HeadAction","LiftAction","TurnAction"},calls);
        first.Dispose();calls.Clear();rig.Engine.Broadcast(new MotorActionAck());Assert.Equal(new[]{"HeadAction","LiftAction","TurnAction"},calls);
        using var vision=new VisionSystem(rig.Robot,CameraCalibration.Nominal()){Enabled=false};
        using var docking=new DockingSystem(rig.Robot,vision);
        using var a=docking.RegisterActionHandlers(default);using var b=docking.RegisterActionHandlers(default);
        calls.Clear();rig.Engine.MessageDispatchObservedForTest=(t,n)=>{if(t==RobotMessageId.LiftLoad)calls.Add(n);};
        rig.Engine.Broadcast(new LiftLoad{Field0=true});Assert.Equal(new[]{"LiftLoadTest[0]","LiftLoadTest[1]","IDockAction","IDockAction"},calls);
        a.Dispose();calls.Clear();rig.Engine.Broadcast(new LiftLoad());Assert.Equal(new[]{"LiftLoadTest[0]","LiftLoadTest[1]","IDockAction"},calls);
    }
    [Fact]
    public void EachConstructorBuildsItsLoggerAndEmptyHeaderStopsAtUnreadRow()
    {
        using var rig=new M4ControlTests.Rig();var logs=new List<string>();rig.Engine.StackLogLine+=logs.Add;Offline(rig);
        Assert.DoesNotContain(logs,e=>e.Contains("logger/directory side effects remain unread"));
        Assert.All(rig.Engine.Robot!.DevBehaviors,s=>{Assert.True(s.Factory.Logger.JsonMode);Assert.False(s.Factory.Logger.FileOpen);});
        rig.Engine.Broadcast(new FirmwareVersion());
        Assert.All(rig.Engine.Robot!.DevBehaviors,s=>Assert.Equal(0,s.Factory.Result));
        Assert.Equal(2,logs.Count(e=>e.Contains("FactoryTest Json::Reader::parse on an empty")));
    }
    [Fact]
    public void NativeInitialFactoryStateZeroSetsTheFirstDeadlineToFloatTwenty()
    {
        using var rig=new M4ControlTests.Rig();Offline(rig);var sets=rig.Engine.Robot!.DevBehaviors;
        Assert.All(sets,s=>Assert.Equal((byte)0,s.Factory.CurrentState!.Value));
        rig.Engine.Broadcast(new FirmwareVersion{Signature=Encoding.UTF8.GetBytes("{")});
        Assert.Equal((byte)15,sets[0].Factory.CurrentState!.Value);Assert.Equal(rig.Engine.Timer.SecondsF+20f,sets[0].Factory.StateDeadline);
        Assert.Equal(rig.Engine.Timer.SecondsF+20f,sets[1].Factory.StateDeadline);
    }
    private sealed class MotorSource : IPanTiltRobot,INativeMotorAckSource
    {
        private readonly EngineRobot _robot;internal MotorSource(EngineRobot robot)=>_robot=robot;
        public IDisposable SubscribeMotorAck(Action<byte> callback)=>_robot.SubscribeMessage(RobotMessageId.MotorActionAck,m=>callback(((MotorActionAck)m).ActionId),"TurnAction");
        public bool OnTreads=>true;public uint OriginId=>1;public float PoseAngleAroundZ=>0;public float HeadAngle=>0;public bool BodyMoving=>false;public bool HeadMoving=>false;
        public uint TurnInPlace(float a,float b,float c,float d,ushort e,bool f,out byte id){id=0;return 0;}
        public uint MoveHeadToAngle(float a,float b,float c,float d,out byte id){id=0;return 0;}
    }
    private sealed class CancelProbe : ActionRunner
    {
        internal bool Cancelled;
        internal CancelProbe():base(99,0){}
        public override int Init()=>0;public override uint CheckIfDone()=>EngineActionResult.Running;public override bool CanInterrupt()=>false;
        public override void Cancel(){Cancelled=true;base.Cancel();}
    }
    private sealed class Port:IEngineTransport
    {public bool TimedOut=>false;public event Action<ReceiverEvent>? Received{add{}remove{}}public void Start(){}public void Connect(System.Net.IPAddress ip,bool simulated){}public void Disconnect(System.Net.IPEndPoint e){}public void SendData(byte[] d){}}
}
