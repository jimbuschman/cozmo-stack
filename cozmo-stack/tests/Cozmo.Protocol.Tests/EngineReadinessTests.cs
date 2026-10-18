using System.Net;
using System.Text;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Animation.Wwise;
using Cozmo.Transport;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class EngineReadinessTests
{
    private sealed class Port:IEngineTransport
    {
        public bool TimedOut=>false;
        public event Action<ReceiverEvent>? Received;
        public void Start(){} public void Connect(IPAddress ip,bool simulated){} public void Disconnect(IPEndPoint endpoint){}
        public void SendData(byte[] data){}
        internal void Arrive()=>Received?.Invoke(new(ReceiverMarker.OnConnected,new(IPAddress.Loopback,5551),null));
    }
    private sealed class Fixture:IDisposable
    {
        internal readonly string Root=Directory.CreateTempSubdirectory("engine-readiness-").FullName;
        internal readonly Port Transport=new();
        internal readonly CozmoRobot Robot;
        internal CozmoEngine Engine=>Robot.Engine;
        internal readonly List<string> Order=new();
        internal readonly List<float> Progress=new();
        private long _clock=1_000_000_000;
        private readonly bool _flag=EngineErrorState.ErrorFlagSet;
        internal Fixture(int clips=1,WwiseAudioInputDispatch? audio=null)
        {
            Directory.CreateDirectory(Path.Combine(Root,"animations"));Directory.CreateDirectory(Path.Combine(Root,"animationGroups"));
            for(int i=0;i<clips;i++)File.WriteAllText(Path.Combine(Root,"animations",$"clip{i}.json"),"{\"clip"+i+"\":[]}");
            Robot=CozmoRobot.CreateForTest(Transport,()=>_clock,new CozmoEngineOptions{BlockPoolPath="",ResourcesPath=Root,
                LabLocaleLanguage="en",LabLocaleCountry="US",AudioInputDispatch=audio},e=>
                {
                    e.EngineStateChanged+=s=>{Assert.Equal(s.NewState,e.EngineState);Order.Add($"{s.OldState}->{s.NewState}");};
                    e.EngineLoading+=p=>Progress.Add(p.Ratio);
                    e.AnimationAvailable+=a=>Order.Add(Encoding.UTF8.GetString(a.Name.Span));
                    e.AnimationAnnouncementsEnded+=a=>Order.Add("end:"+a.Value);
                },startReady:false);
        }
        internal void Tick(){_clock+=60_000_000;Engine.Tick();}
        internal void Finish()
        {
            Assert.True(SpinWait.SpinUntil(()=>{Tick();return Engine.IsReady||Engine.StartupFailed;},TimeSpan.FromSeconds(10)));
        }
        public void Dispose(){Robot.Dispose();Directory.Delete(Root,true);EngineErrorState.ErrorFlagSet=_flag;}
    }

    // reach: production — real constructor/start/poll/worker/file loader and actual name producer (one definition, no ordering ambiguity).
    [Fact]
    public void StartupPublishesStatesProgressNamesAndEndBeforeReadyThenRunsRobotWork()
    {
        using var f=new Fixture();Assert.Equal(new[]{"0->1"},f.Order);Assert.False(f.Engine.IsReady);
        f.Tick();Assert.Equal(new[]{"0->1","1->2"},f.Order);Assert.Empty(f.Progress);Assert.Equal(0L,f.Engine.Timer.TickCount);
        f.Finish();Assert.False(f.Engine.StartupFailed);Assert.Equal(new[]{"0->1","1->2","clip0","end:0","2->3"},f.Order);
        Assert.Equal(0f,f.Progress[0]);Assert.Equal(1f,f.Progress[^1]);Assert.Contains(f.Progress,p=>p>f.Progress[0]);
        Assert.Equal(0L,f.Engine.Timer.TickCount); // completion remains a state2 tick.
        f.Engine.ConnectToRobot(IPAddress.Loopback);f.Tick();Assert.NotNull(f.Engine.Robot);Assert.Equal(1L,f.Engine.Timer.TickCount);
    }

    // reach: production — real startup with two definitions; unknown native hash-order producer is never replaced by managed enumeration.
    [Fact]
    public void UncheckedMultiNameOrderFailsClearlyWithoutPublishingReadiness()
    {
        using var f=new Fixture(2);f.Finish();Assert.True(f.Engine.StartupFailed);Assert.False(f.Engine.IsReady);
        Assert.Contains("animation-name order",f.Engine.StartupFailureReason);Assert.DoesNotContain("2->3",f.Order);Assert.DoesNotContain("end:0",f.Order);
    }

    // reach: production — public Create, actual engine thread, missing resources policy M1-058/M1-034.
    [Fact]
    public void MissingResourcesReportsFailedWithoutHangingOrClaimingReady()
    {
        using var robot=CozmoRobot.Create(engineOptions:new CozmoEngineOptions{BlockPoolPath=""});
        Assert.True(SpinWait.SpinUntil(()=>robot.Engine.StartupFailed,TimeSpan.FromSeconds(5)));
        Assert.False(robot.Engine.IsReady);Assert.Contains("ResourcesPath",robot.Engine.StartupFailureReason);Assert.Equal(0L,robot.Engine.Timer.TickCount);
    }

    // reach: production — public ConnectAsync follows M2-021's host wait and fails when startup fails rather than waiting forever.
    [Fact]
    public async Task ConnectAsyncSurfacesStartupFailure()
    {
        await Assert.ThrowsAsync<IOException>(()=>CozmoRobot.ConnectAsync(IPAddress.Loopback,engineOptions:new CozmoEngineOptions{BlockPoolPath=""}));
    }

    // reach: production — early API request is processed before the state switch, while robot connection updates await state3.
    [Fact]
    public void EarlyConnectIsAcceptedWithoutInventingAnEngineHandlerGate()
    {
        using var f=new Fixture();f.Engine.ConnectToRobot(IPAddress.Loopback);f.Tick();
        Assert.NotNull(f.Engine.Robot);Assert.Equal(2,f.Engine.EngineState);Assert.Equal(0L,f.Engine.Timer.TickCount);
        f.Finish();f.Tick();Assert.True(f.Engine.IsReady);Assert.Equal(1L,f.Engine.Timer.TickCount);
    }

    // reach: production — public config -> actual UI/MessageHandler setup range gate.
    [Theory] [InlineData(-1)] [InlineData(65536)]
    public void AdvertisingPortRejectsValuesOutsideUnsignedSixteenBits(int port)
    {
        byte[] config=Encoding.UTF8.GetBytes("{\"AdvertisingHostIP\":\"127.0.0.1\",\"RobotAdvertisingPort\":"+port+",\"UiAdvertisingPort\":1}");
        Assert.Contains("RobotAdvertisingPort",Assert.Throws<ArgumentException>(()=>CozmoRobot.Create(engineOptions:new CozmoEngineOptions{EngineConfigJson=config})).Message);
    }

    // reach: unit (calls existing checked library loader directly; engine state/poll join is not exercised).
    [Fact]
    public void FailedJsonStillAddsItsPerFileShareAndThereIsOneCoarseAddition()
    {
        string root=Directory.CreateTempSubdirectory("progress-arithmetic-").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(root,"animations"));File.WriteAllText(Path.Combine(root,"animations","bad.json"),"?");
            var added=new List<float>();AnimationLibrary.OpenForLoad(root,null,null,null,()=>true,null,null,progressAdded:added.Add);
            Assert.Equal(new[]{0x3F333333,0x3E4CCCCD},added.Select(BitConverter.SingleToInt32Bits));
            Assert.Equal(0x3F666666,BitConverter.SingleToInt32Bits(added[0]+added[1]));
        }
        finally{Directory.Delete(root,true);}
    }

    // reach: production — real startup registration, six public C# entries -> real M6-023 dispatch; host provides a genuine empty-bank controller.
    [Fact]
    public void AllSixAudioRequestsReachTheRegisteredDispatchWithTheirBytesIntact()
    {
        var dispatch=new WwiseAudioInputDispatch(new WwiseEventRuntime(Array.Empty<WwiseBank>()));using var f=new Fixture(audio:dispatch);
        var seen=new List<(ushort Tag,byte[] Body,WwiseAudioInputResult Result)>();
        f.Engine.AppAudioDispatchObservedForTest=(tag,body,result)=>seen.Add((tag,body.ToArray(),result));
        var request=new WwisePostAudioEvent(0x11223344,7,0);byte[] body={1,2,3,4};
        f.Engine.PostAudioEvent(request);f.Engine.StopAllAudioEvents(body);f.Engine.PostAudioGameState(body);
        f.Engine.PostAudioSwitchState(body);f.Engine.PostAudioParameter(body);f.Engine.PostAudioMusicState(body);
        body[0]=99;f.Tick();
        Assert.Equal(new ushort[]{1,2,3,4,5,6},seen.Select(s=>s.Tag));Assert.Equal(request.ToBytes(),seen[0].Body);
        Assert.Equal(0u,seen[0].Result.PlayingId); // Empty bank: no event is played, no success fabricated.
        Assert.All(seen.Skip(1),s=>{Assert.Equal(new byte[]{1,2,3,4},s.Body);Assert.Equal(WwiseAudioInputOutcome.SubscribedNotHandled,s.Result.Outcome);});
    }
}
