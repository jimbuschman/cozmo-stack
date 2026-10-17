using System.Text;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Transport;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class LabStartupFeedingCorrectionTests
{
    // reach: production (actual engine constructor, no AB input substitution; existing log-observer deferral only).
    [Fact]
    public void RealStartupEnablesLabAndLogsBeforeTheMissingDeviceIdJoin()
    {
        CozmoEngine? engine=null;var entries=new List<EngineLogEntry>();
        // Defer observer delivery until the constructed engine can be subscribed; no
        // engine input or callee is replaced, and the actual startup emits the entry.
        LogCallbackDeferral.RunGated(new object(),()=>
        {
            engine=new CozmoEngine(new M4ControlTests.FakePort(),new CozmoEngineOptions{BlockPoolPath=""},()=>0);
            engine.LogEntry+=entries.Add;
        });
        using(engine)
        {
            var enable=Assert.Single(entries.Where(e=>e.Name=="AnkiLab.Enable"));
            Assert.Equal((EngineLogLevel.Info,"Unnamed","Enabling the lab"),(enable.Level,enable.Channel,enable.Text));
            Assert.True(engine!.Experiments.Enabled);Assert.True(engine.Experiments.Lab.Enabled);Assert.Null(engine.Experiments.DeviceIdInput);
        }
    }

    // reach: unit (native AnkiLab ctor unit; definition/activation owner joins MISSING at M15-0624CA6F4).
    [Fact]
    public void LabConstructorStartsEnabledBeforeInit()
    {
        var lab=new LabActivationRuntime{Definition=()=>new(),Log=(_,_,_,_,_)=>{}};
        Assert.True(lab.Enabled); // native ctor7EBB60 stores1 before any Enable call.
    }

    // reach: unit (real E2G signal and Activity selection entries; full Feeding lifecycle MISSING at5AB030/5ABB5C).
    [Fact]
    public void EmptyFeedingMapDeselectAndReselectHaveExactlyOneLiveSubscriptionWithoutDestroyRecipient()
    {
        using var robot=CozmoRobot.CreateForTest(new M4ControlTests.FakePort(),()=>1);
        var feeding=new FeedingDisconnectUnit{Engine=robot.Engine};
        var activity=new Activity{Id="Feeding",Strategy=new(),FeedingDisconnect=feeding};
        Assert.Null(feeding.DestroyControllerTree);
        activity.OnSelected(0);Assert.Equal(1,robot.Engine.AnimationMessages.Count(0x0D,true));
        activity.OnDeselected(1);Assert.Equal(0,robot.Engine.AnimationMessages.Count(0x0D,true));Assert.Empty(feeding.HandlesAC);
        activity.OnSelected(2);Assert.Equal(1,robot.Engine.AnimationMessages.Count(0x0D,true));Assert.Single(feeding.HandlesAC);
        activity.OnDeselected(3);Assert.Equal(0,robot.Engine.AnimationMessages.Count(0x0D,true));
    }

    // reach: unit (substitutes one controller state; nonempty native tree destruction MISSING at5ABC0A/5ACD64).
    [Fact]
    public void NonemptyMissingDestroyStillResetsMapAndReleasesHandles()
    {
        using var robot=CozmoRobot.CreateForTest(new M4ControlTests.FakePort(),()=>1);
        var feeding=new FeedingDisconnectUnit{Engine=robot.Engine};
        feeding.Controllers78.Add(7,new FeedingCubeControllerUnit{Id=7,State4=2,StateName=_=>"Deactivated",Log=(_,_,_,_)=>{}});
        feeding.OnSelected();bool released=false;
        robot.Engine.AnimationMessages.ReleaseObserved=step=>{if(step.StartsWith("unlink E2G")){Assert.Empty(feeding.Controllers78);released=true;}};
        Assert.False(feeding.OnDeselected());Assert.True(released);Assert.Empty(feeding.Controllers78);
        Assert.Empty(feeding.HandlesAC);Assert.Equal(0,robot.Engine.AnimationMessages.Count(0x0D,true));
    }

    // reach: unit (substitutes native Needs timestamp/time and locale input; join MISSING at M15-0626A5328).
    [Fact]
    public void LocaleLowercasesOnlyAsciiBytesAndKeepsNonAsciiUppercaseUtf8()
    {
        var audience=new LabAudienceTags{Log=(_,_,_,_,_)=>{}};
        audience.RegisterEngineTags(()=>0,()=>86400,()=>"\u00C4BC\u0130",()=>"\u00D6Z");
        var tags=audience.GetQualified();
        Assert.Equal(Encoding.UTF8.GetBytes("locale_language_\u00C4bc\u0130"),tags[0]);
        Assert.Equal(Encoding.UTF8.GetBytes("locale_country_\u00D6z"),tags[1]);
    }

    // reach: unit (direct malformed hash inputs; source log flush recipient MISSING at7EE468/7EE4F8 when unbound).
    [Fact]
    public void EmptyHashContinuesWhenNoFlushRecipientIsBound()
    {
        bool old=EngineErrorState.ErrorFlagSet;
        try
        {
            EngineErrorState.ErrorFlagSet=false;
            var errors=new List<string>();var lab=new LabActivationRuntime{Definition=()=>new(),Log=(level,_,key,_,_)=>{if(level==EngineLogLevel.Error)errors.Add(key);}};
            Assert.Equal((byte)99,lab.HashBucket(Array.Empty<byte>(),Array.Empty<byte>()));
            Assert.Equal(2,errors.Count);Assert.True(EngineErrorState.ErrorFlagSet);
        }
        finally{EngineErrorState.ErrorFlagSet=old;}
    }

    // reach: unit (actual Lab owner/flush binding; direct empty hash inputs, production lookup join MISSING at M15-0624CA6F4).
    [Fact]
    public void ActualOwnerFlushesBothAssertionsAndContinuesHash()
    {
        using var robot=CozmoRobot.CreateForTest(new M4ControlTests.FakePort(),()=>1);bool old=EngineErrorState.ErrorFlagSet;
        try
        {
            var calls=new List<string>();robot.Engine.Das.CallObserved=call=>calls.Add(call.Name);
            Assert.Null(robot.Engine.Experiments.Lab.AssertCallstackAndFlush);
            Assert.Equal((byte)99,robot.Engine.Experiments.Lab.HashBucket(Array.Empty<byte>(),Array.Empty<byte>()));
            Assert.Equal(new[]{"DASForceFlushNow","DASForceFlushNow"},calls);
        }
        finally{EngineErrorState.ErrorFlagSet=old;}
    }
}
