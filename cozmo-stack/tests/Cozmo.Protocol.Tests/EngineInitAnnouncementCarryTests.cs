using System.Text;
using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Cozmo.Transport;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class EngineInitAnnouncementCarryTests
{
    private static byte[] B(string text) => Encoding.UTF8.GetBytes(text);
    private static FirmwareJsonValue Config(params string[] members)
    {
        var value=new FirmwareJsonValue{Kind=FirmwareJsonKind.Object};
        foreach(string key in members) value.Members.Add(Convert.ToHexString(B(key)),new());
        return value;
    }
    private static FirmwareJsonValue ValidConfig()=>Config("AdvertisingHostIP","RobotAdvertisingPort","UiAdvertisingPort");

    // reach: production — actual constructor, connection messages and outbound robot transport.
    [Fact]
    public void ValidHostConfigContinuesBootstrapAndRobotTrafficFlows()
    {
        using var r=new M4ControlTests.Rig(engineOptions:new CozmoEngineOptions{BlockPoolPath="",EngineConfigJson=B("{\"AdvertisingHostIP\":\"127.0.0.1\",\"RobotAdvertisingPort\":5551,\"UiAdvertisingPort\":5552}")});
        Assert.True(r.Engine.Handler.Initialised);Assert.Equal(3,r.Engine.NativeEngineConfig18!.Members.Count);
        Assert.False(r.Engine.NativeInitialized10); // no invented native tail completion.
        r.ToSynced();int mark=r.Mark();
        Assert.True(r.Engine.SendMessage(new CarryStateUpdate{State=CarryState.CARRY_1_BLOCK}));
        Assert.Contains(r.RawSince(mark),bytes=>bytes.SequenceEqual(new byte[]{0x49,1}));
    }

    // reach: production — invalid public EngineConfigJson fails actual engine construction clearly.
    [Theory]
    [InlineData("?","parsed",false)]
    [InlineData("[]","JSON object",false)]
    [InlineData("null","JSON object",false)]
    [InlineData("3","JSON object",false)]
    [InlineData("{}","AdvertisingHostIP",true)]
    [InlineData("{\"AdvertisingHostIP\":null}","RobotAdvertisingPort",true)]
    [InlineData("{\"AdvertisingHostIP\":null,\"RobotAdvertisingPort\":null}","UiAdvertisingPort",true)]
    public void InvalidHostConfigThrowsClearProblemAndKeepsCheckedMissingKeyError(string json,string problem,bool sourceError)
    {
        bool flag=EngineErrorState.ErrorFlagSet;
        try
        {
            EngineErrorState.ErrorFlagSet=false;
            var error=Assert.Throws<ArgumentException>(()=>new M4ControlTests.Rig(engineOptions:new CozmoEngineOptions{BlockPoolPath="",EngineConfigJson=B(json)}));
            Assert.Contains("EngineConfigJson",error.Message);Assert.Contains(problem,error.Message);
            Assert.Equal(sourceError,EngineErrorState.ErrorFlagSet);
        }
        finally{EngineErrorState.ErrorFlagSet=flag;}
    }

    // reach: unit (substitutes host config; native UI/InitInternal join MISSING at 0x004EC84C/0x004EC970).
    [Theory][InlineData(0,"AdvertisingHostIP")][InlineData(1,"RobotAdvertisingPort")][InlineData(2,"UiAdvertisingPort")]
    public void FirstMissingMemberErrorsAfterUnconditionalResetAndConfigCopy(int count,string missing)
    {
        using var r=new M4ControlTests.Rig();var e=r.Engine;var logs=new List<EngineLogEntry>();e.LogEntry+=logs.Add;
        string[] keys={"AdvertisingHostIP","RobotAdvertisingPort","UiAdvertisingPort"};
        var config=Config(keys.Take(count).ToArray());bool flag=EngineErrorState.ErrorFlagSet;
        try
        {
            EngineErrorState.ErrorFlagSet=false;e.NativeInitialized10=true;
            e.NativeInitRecipients.UiInit=_=>throw new Exception("must not reach UI");
            Assert.Equal(1,e.InitChecked(config));Assert.False(e.NativeInitialized10);Assert.True(EngineErrorState.ErrorFlagSet);
            Assert.NotSame(config,e.NativeEngineConfig18);config.Members.Clear();Assert.Equal(count,e.NativeEngineConfig18!.Members.Count);
            Assert.Equal(new[]{"CozmoEngine.Init.ReInit","CozmoEngine.Init"},logs.Select(x=>x.Name));
            Assert.Equal(EngineLogLevel.Info,logs[0].Level);Assert.Equal("Unnamed",logs[0].Channel);
            Assert.Equal("Reinitializing already-initialized CozmoEngineImpl with new config.",logs[0].Text);
            Assert.EndsWith($"No {missing} defined in Json config.",logs[1].Text);Assert.Equal(EngineLogLevel.Error,logs[1].Level);
        }
        finally{EngineErrorState.ErrorFlagSet=flag;}
    }

    // reach: unit (substitutes UI/InitInternal results; callees MISSING at 0x006601B0 and 0x004EDC6C).
    [Theory][InlineData(7,0,"CozmoEngine.Init","Error initializing UIMessageHandler")][InlineData(0,-4,"CozomEngine.Init","Failed calling internal init.")]
    public void NonzeroInitResultsKeepCodeAndTypo(int ui,int core,string key,string text)
    {
        using var r=new M4ControlTests.Rig();var e=r.Engine;var logs=new List<EngineLogEntry>();e.LogEntry+=logs.Add;
        bool flag=EngineErrorState.ErrorFlagSet;int calls=0;
        try
        {
            EngineErrorState.ErrorFlagSet=false;e.NativeInitRecipients.UiInit=_=>ui;e.NativeInitRecipients.InitInternal=()=>{calls++;return core;};
            e.NativeInitRecipients.LoadRobotConfigs=()=>throw new Exception("must not load");
            Assert.Equal(ui!=0?ui:core,e.InitChecked(ValidConfig()));Assert.Equal(ui!=0?0:1,calls);
            var log=Assert.Single(logs);Assert.Equal(key,log.Name);Assert.EndsWith(text,log.Text);Assert.Equal(EngineLogLevel.Error,log.Level);Assert.True(EngineErrorState.ErrorFlagSet);
        }
        finally{EngineErrorState.ErrorFlagSet=flag;}
    }

    // reach: unit (substitutes loader/UI/Needs leaf inputs; full Init joins MISSING at 0x004EC84C, 0x004EC970, 0x004EC9CE, 0x004ECA0A).
    [Fact]
    public void SuccessfulPrefixCallsConfigsLabNeedsAndRobotManagerInOrder()
    {
        using var r=new M4ControlTests.Rig();var e=r.Engine;var calls=new List<string>();
        e.NativeInitRecipients.UiInit=_=>{calls.Add("UI");return 0;};e.NativeInitRecipients.InitInternal=()=>{calls.Add("internal");return 0;};
        e.NativeInitRecipients.LoadRobotConfigs=()=>calls.Add("configs");e.Experiments.InitBeforeNeedsObserved=()=>calls.Add("lab");
        var values=Enumerable.Range(0,6).Select(i=>FirmwareJsonValue.FromReal(i)).ToArray();
        NativeNeedsInitInputs? input=null;
        input=new(values[0],values[1],values[2],values[3],values[4],values[5],(time,args)=>
        {calls.Add("needs");Assert.Same(input,args);Assert.Equal(e.Timer.SecondsF,time);Assert.Same(values[5],args.Config1D8);});
        e.NativeInitRecipients.Needs=input;e.NativeInitRecipients.DasConfig=new();
        e.LogEntry+=log=>{if(log.Name=="robot.init.time_spent_ms")calls.Add("manager");};
        Assert.Null(e.InitChecked(ValidConfig())); // unchecked remaining Init tail is not success 0.
        Assert.Equal(new[]{"UI","internal","configs","lab","needs","manager"},calls);
        Assert.False(e.NativeInitialized10);
    }

    // reach: unit (substitutes host config; native Init UI owner MISSING at 0x004EC84C).
    [Fact]
    public void MissingUiRecipientDoesNotInventSuccessOrRunInternal()
    {
        using var r=new M4ControlTests.Rig();r.Engine.NativeInitialized10=false;r.Engine.NativeInitRecipients.UiInit=null;
        r.Engine.NativeInitRecipients.InitInternal=()=>throw new Exception("missing UI must stop");
        Assert.Null(r.Engine.InitChecked(ValidConfig()));Assert.False(r.Engine.NativeInitialized10);
    }

    // reach: unit (substitutes malformed config at checked prefix; public constructor validation tested separately).
    [Fact]
    public void NonObjectConfigGateFailsClearlyWithoutAnInventedNativeError()
    {
        using var r=new M4ControlTests.Rig();var logs=new List<EngineLogEntry>();r.Engine.LogEntry+=logs.Add;
        r.Engine.NativeInitRecipients.UiInit=_=>throw new Exception("unchecked gate must stop");
        Assert.Contains("JSON object",Assert.Throws<ArgumentException>(()=>r.Engine.InitChecked(FirmwareJsonValue.FromReal(3))).Message);Assert.Empty(logs);
        Assert.Equal(FirmwareJsonKind.Real,r.Engine.NativeEngineConfig18!.Kind);Assert.False(r.Engine.NativeInitialized10);
    }

    private sealed class Output(List<string> calls,string label):IAnimationAnnouncementRecipient
    {
        public Action<byte[]>? OnAvailable;
        public void Available(byte[] name){calls.Add(label+":"+Convert.ToHexString(name));OnAvailable?.Invoke(name);}
        public void End(byte value)=>calls.Add(label+":end:"+value);
    }
    private static NativeAnimationNames Names(params byte[][] names)
    {
        NativeAnimationNameNode? head=null;
        for(int i=names.Length-1;i>=0;i--)head=new(names[i],head);
        return new(head);
    }

    // reach: unit (substitutes native hash traversal and external-interface slots; population MISSING at 0x00588190/0x0052E76E).
    [Fact]
    public void BroadcastCopiesNamesPreservesContainerOrderSkipsOnlyExactProceduralAndReloadsReceiver()
    {
        using var r=new M4ControlTests.Rig();var calls=new List<string>();var first=new Output(calls,"first");var later=new Output(calls,"later");
        var z=B("z");var a=new byte[]{0xFF,0,0x61};
        r.Engine.Robots.NativeCannedAnimationNames=Names(z,B("_PROCEDURAL_"),a,B("_PROCEDURAL_x"));
        IAnimationAnnouncementRecipient current=first;
        first.OnAvailable=_=>{a[0]=1;current=later;};r.Engine.Robots.AnnouncementRecipient=()=>current;
        r.Engine.LogEntry+=log=>{if(log.Name=="RobotManager.BroadcastAvailableAnimations")
        {Assert.Equal((EngineLogLevel.Debug,"Unnamed","Supposedly sent EndOfMessage"),(log.Level,log.Channel,log.Text));calls.Add("debug");}};
        Assert.True(r.Engine.Robots.BroadcastAvailableAnimations());
        Assert.Equal(new[]{"first:7A","later:FF0061","later:5F50524F4345445552414C5F78","later:end:0","debug"},calls);
    }

    // reach: unit (substitutes empty container/null interface; native population MISSING at 0x0052E76E).
    [Fact]
    public void EmptyContainerStillSendsEndButNullInterfaceSendsAndLogsNothing()
    {
        using var r=new M4ControlTests.Rig();var calls=new List<string>();var output=new Output(calls,"o");
        r.Engine.Robots.NativeCannedAnimationNames=new(null);r.Engine.Robots.AnnouncementRecipient=()=>null;
        r.Engine.LogEntry+=log=>{if(log.Name=="RobotManager.BroadcastAvailableAnimations")calls.Add("debug");};
        Assert.True(r.Engine.Robots.BroadcastAvailableAnimations());Assert.Empty(calls);
        r.Engine.Robots.AnnouncementRecipient=()=>output;Assert.True(r.Engine.Robots.BroadcastAvailableAnimations());
        Assert.Equal(new[]{"o:end:0","debug"},calls);
    }

    // reach: unit (enters tick; substitutes loader state/result; native names producer MISSING at 0x00588190).
    [Fact]
    public void MissingContainerDoesNotFabricateEndOrLoadingCompletion()
    {
        using var r=new M4ControlTests.Rig();var calls=new List<string>();
        r.Engine.NativeNonConfigLoadingActive=()=>true;r.Engine.NativeDoNonConfigDataLoading=()=>(1,1f);
        r.Engine.NativeSetEngineState=_=>calls.Add("state");r.Engine.AnimationAnnouncementsEnded+=_=>calls.Add("end");
        r.Engine.LogEntry+=log=>{if(log.Name=="RobotManager.BroadcastAvailableAnimations")calls.Add("debug");};
        r.Tick();Assert.Empty(calls);
    }

    // reach: unit (substitutes native state and container; state producer MISSING at 0x004ED5F0).
    [Fact]
    public void InactiveLoadingEntryDoesNotCallLoader()
    {
        using var r=new M4ControlTests.Rig();r.Engine.NativeNonConfigLoadingActive=()=>false;
        r.Engine.NativeDoNonConfigDataLoading=()=>throw new Exception("inactive loading must not enter loader");r.Tick();
    }

    // reach: unit (enters real engine tick; substitutes native loading state/result/container; joins MISSING at 0x004ED5F0/0x00523570/0x00588190).
    [Theory][InlineData(-1,false)][InlineData(0,false)][InlineData(1,true)][InlineData(2,false)]
    public void TickRequiresExactLoadingResultOneAndBroadcastsBeforeStateThree(int result,bool completes)
    {
        using var r=new M4ControlTests.Rig();var calls=new List<string>();var e=r.Engine;
        e.Robots.NativeCannedAnimationNames=Names(B("walk"));e.NativeNonConfigLoadingActive=()=>true;
        e.NativeDoNonConfigDataLoading=()=>(result,1f);e.NativeSetEngineState=value=>calls.Add("state:"+value);
        e.AnimationAvailable+=msg=>calls.Add("name:"+Encoding.UTF8.GetString(msg.Name.Span));e.AnimationAnnouncementsEnded+=msg=>calls.Add("end:"+msg.Value);
        e.LogEntry+=log=>{if(log.Name=="RobotManager.BroadcastAvailableAnimations")calls.Add("debug");};
        long ticks=e.Timer.TickCount;r.Tick();Assert.Equal(completes?new[]{"name:walk","end:0","debug","state:3"}:Array.Empty<string>(),calls);
        Assert.Equal(ticks,e.Timer.TickCount); // loading branch cannot also execute state 3 in this tick.
    }

    // reach: unit (substitutes prebound name producer and direct RobotManager Init; native startup order remains M1-054).
    [Fact]
    public void InitTimeAnnouncementStaysInternalEvenWithANameProducerAndPostLoadingStillDelivers()
    {
        using var r=new M4ControlTests.Rig();var e=r.Engine;var calls=new List<string>();var diagnostics=new List<string>();
        e.StackLogLine+=diagnostics.Add;
        e.Robots.NativeCannedAnimationNames=Names(B("walk"));
        e.AnimationAvailable+=_=>calls.Add("available");e.AnimationAnnouncementsEnded+=_=>calls.Add("end");
        using var local=e.AnimationMessages.Subscribe(0x5D,true,()=>calls.Add("local"));
        e.Robots.InitTimeAnimationAnnouncementsInternal=()=>calls.Add("internal");
        e.Robots.InitCheckedDasTail(new(),()=>0);
        Assert.Equal(new[]{"internal"},calls);
        Assert.Contains(diagnostics,d=>d.Contains("not supported (M1-055)"));
        Assert.DoesNotContain(diagnostics,d=>d.Contains("first Init broadcast") || d.Contains("raw CLAD byte serializer"));
        calls.Clear();e.NativeNonConfigLoadingActive=()=>true;e.NativeDoNonConfigDataLoading=()=>(1,1f);e.NativeSetEngineState=_=>calls.Add("state");
        r.Tick();Assert.Equal(new[]{"available","local","end","state"},calls);
        Assert.DoesNotContain(diagnostics,d=>d.Contains("raw CLAD byte serializer"));
    }

    private sealed class CarryFixture:IDisposable
    {
        internal readonly M4ControlTests.Rig Rig=new();
        internal readonly VisionSystem Vision;
        internal CarryingComponent Carry=>Rig.Engine.Robot!.CarryingOwner!;
        internal CarryFixture(){Rig.ToSynced();Vision=new(Rig.Robot,CameraCalibration.Nominal()){Enabled=false};Vision.World.CurrentOriginId=Rig.Engine.Robot!.CurrentOriginId;}
        internal ObservableObject Add(uint id)
        {
            var obj=new ObservableObject(id,ObjectType.Block_LIGHTCUBE1,CubeGeometry.MarkersFor(ObjectType.Block_LIGHTCUBE1))
            {Pose=new Pose3d(Rotation3d.Identity,new Vec3(100,0,0),parent:Rig.Engine.Robot!.GetNativeOrigin(Rig.Engine.Robot.CurrentOriginId)),PoseState=PoseState.Known,OriginId=Vision.World.CurrentOriginId};
            Vision.World.AddLocatedObject(obj);return obj;
        }
        public void Dispose(){Vision.Dispose();Rig.Dispose();}
    }

    // reach: unit (substitutes direct internal carry entry and located world; AttachObjectToLift join0x006331AE is not entered).
    [Fact]
    public void InternalSetterEntryReachesOwnerAndSendsCarryOne()
    {
        using var f=new CarryFixture();var obj=f.Add(7);int mark=f.Rig.Mark();
        f.Carry.SetCarryingObject(7,(ushort)obj.Markers[0].Code);
        Assert.Null(typeof(CarryingComponent).GetMethod("SetCarryingObject",new[]{typeof(uint),typeof(ushort)}));
        Assert.Same(f.Carry,f.Rig.Robot.Carrying);Assert.Equal(7u,f.Carry.CarriedObjectId);
        Assert.Equal((ushort)obj.Markers[0].Code,f.Carry.NativeMarkerCode);
        Assert.Contains(f.Rig.RawSince(mark),bytes=>bytes.SequenceEqual(new byte[]{0x49,1}));
    }

    // reach: unit (calls setter directly with located-world input; G2E entry MISSING at 0x005295F2).
    [Fact]
    public void FailedLookupKeepsMarkerAndIdSetsErrorAndSendsNothing()
    {
        using var f=new CarryFixture();f.Carry.SetCarrying(8);f.Carry.NativeMarkerCode=19;
        var logs=new List<EngineLogEntry>();f.Rig.Engine.LogEntry+=logs.Add;bool flag=EngineErrorState.ErrorFlagSet;int mark=f.Rig.Mark();
        try
        {
            EngineErrorState.ErrorFlagSet=false;Assert.False(f.Carry.SetCarryingObject(f.Vision.World,0xFFFFFFFE,33));
            Assert.Equal(8u,f.Carry.CarriedObjectId);Assert.Equal(19,f.Carry.NativeMarkerCode);Assert.True(EngineErrorState.ErrorFlagSet);Assert.Empty(f.Rig.RawSince(mark));
            var log=Assert.Single(logs);Assert.Equal("Robot.SetCarryingObject.NullCarryObject",log.Name);Assert.Equal(EngineLogLevel.Error,log.Level);
            Assert.EndsWith("Object -2 no longer exists in the world. Can't set it as robot's carried object.",log.Text);
        }
        finally{EngineErrorState.ErrorFlagSet=flag;}
    }

    // reach: unit (substitutes attach/top input; real Carrying owner/send path; G2E entry MISSING at 0x005295F2).
    [Theory][InlineData(false,1)][InlineData(true,2)]
    public void SetterStoresAndClearsMatchingLocalizationBeforeSendingOneOrTwo(bool stacked,byte expected)
    {
        using var f=new CarryFixture();var obj=f.Add(7);f.Vision.World.MarkerPosesRelativeToCamera=_=>true;
        Assert.True(f.Vision.World.SetLocalizedTo(obj));var sends=new List<RobotMessage>();
        f.Rig.Engine.Robot!.SendFault=message=>
        {
            if(message is CarryStateUpdate){Assert.Equal(7u,f.Carry.CarriedObjectId);Assert.Equal(23,f.Carry.NativeMarkerCode);Assert.Null(f.Vision.World.LocalizedToObjectId);sends.Add(message);return true;}return null;
        };
        // The checked attachment owner stores top first; the setter reads it only after clearing localization.
        f.Carry.AttachToLift(9,obj.Markers[0],new Pose3d(""),stacked?10u:null,null);
        Assert.True(f.Carry.SetCarryingObject(f.Vision.World,7,23));
        Assert.Equal(new byte[]{0x49,expected},Assert.Single(sends).ToBytes());
    }
}
