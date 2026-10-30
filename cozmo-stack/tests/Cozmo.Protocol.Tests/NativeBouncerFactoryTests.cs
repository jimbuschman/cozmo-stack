using System.Text.Json;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

public class NativeBouncerFactoryTests
{
    [Fact]
    public void BouncerOnlyNativeOwnerDoesNotEnableSiblingConstructors()
    {
        // Standalone Freeplay context: actual shared robot is supplied only to Bouncer.
        using var rig=new Rig(); var robot=rig.Robot.Engine.Robot!;
        var context=new BehaviorFactoryContext { Vision=rig.Vision,Manipulation=rig.M,BouncerNativeRobot=robot,
            Features=new FeatureGates(new[] { ("Bouncer",false) }) };
        var factory=new BehaviorFactory();
        var container=new BehaviorContainer(factory,context,new Dictionary<BehaviorID,JsonElement>());
        using var bouncerConfig=JsonDocument.Parse("{\"behaviorClass\":\"Bouncer\",\"behaviorID\":\"Bouncer\"}");
        using var rollConfig=JsonDocument.Parse("{\"behaviorClass\":\"RollBlock\",\"behaviorID\":\"RollBlock\"}");
        var bouncer=factory.CreateBehavior(container,BehaviorClass.Bouncer,context,bouncerConfig.RootElement,out var bouncerStatus);
        Assert.Equal(BehaviorCreateStatus.Created,bouncerStatus);
        Assert.IsType<NativeBouncerBehavior>(bouncer);
        var roll=factory.CreateBehavior(container,BehaviorClass.RollBlock,context,rollConfig.RootElement,out var rollStatus);
        Assert.Null(roll);Assert.Equal(BehaviorCreateStatus.NotImplemented,rollStatus);
        ((NativeBouncerBehavior)bouncer!).Dispose();
    }
    [Fact]
    public async Task StaleTriggerConstructorRetiresTagWithoutTouchingReplacementOrPublishingCompletion()
    {
        // Supplied face and admission; real TriggerAnimationAction constructor/MISSING callback and owned action queue.
        using var rig=new Rig(); var robot=rig.Robot.Engine.Robot!;
        using var doc=JsonDocument.Parse("{\"behaviorClass\":\"Bouncer\",\"behaviorID\":\"Bouncer\"}");
        var source=new BehaviorFactoryContext { Vision=rig.Vision,Manipulation=rig.M,NativeRobot=robot,Features=new FeatureGates(new[] { ("Bouncer",true) }) };
        using var behavior=new NativeBouncerBehavior(source,doc.RootElement,()=>new[]{7});
        var origin=robot.GetNativeOrigin(robot.CurrentOriginId);
        uint timestamp=rig.Robot.CameraSettings.LastProcessedImageTimestamp;
        var detected=new DetectedFace(7,new FaceRect(0,0,10,10)){NativeFaceDirectionThirdDegrees=0};
        rig.Vision.Faces.AddOrUpdateNativeFace(new TrackedFace(detected,timestamp),Pose3d.Identity,false,origin.Node!);
        var context=new BehaviorContext { Robot=rig.Robot,Triggers=new AnimationTriggerMap() };
        Assert.True(behavior.RunnableCore(context));
        using var first=new BehaviorScope(motion:rig.Robot.Motion);
        using var replacement=new BehaviorScope(motion:rig.Robot.Motion);
        int reports=0; using var subscription=rig.Robot.Engine.AnimationMessages.SubscribeMessage(0x5A,true,_=>reports++);
        bool reentered=false;
        behavior.TriggerMissingForTest=_=>
        {
            if(reentered)return; reentered=true;
            behavior.Stop(BehaviorStopReason.Interrupted); first.Dispose();
            behavior.StartAsync(context,replacement,CancellationToken.None).GetAwaiter().GetResult();
        };
        await behavior.StartAsync(context,first,CancellationToken.None);
        behavior.Update(context,0);
        Assert.True(reentered); Assert.True(behavior.EngineRunning);
        Assert.Equal(1,rig.Robot.Animations.Scheduler.IdleStack.Count(item=>item.Lock==behavior.Id));
        uint tag=Assert.IsType<uint>(behavior.LastStaleRetiredTag);
        Assert.False(ActionRunnerTagCounter.Global.IsInUse(tag)); Assert.Equal(0,reports);
        behavior.Stop(BehaviorStopReason.Interrupted);
    }
    [Fact]
    public async Task RestartDuringIdlePushKeepsNewRunAndOldRunConsumesNoRng()
    {
        // Supplied behavior admission; real shared scope and scheduler with synchronous log reentry.
        using var rig=new Rig(); var robot=rig.Robot.Engine.Robot!;
        using var doc=JsonDocument.Parse("{\"behaviorClass\":\"Bouncer\",\"behaviorID\":\"Bouncer\"}");
        var source=new BehaviorFactoryContext { Vision=rig.Vision,Manipulation=rig.M,NativeRobot=robot,Features=new FeatureGates(new[] { ("Bouncer",true) }) };
        using var behavior=new NativeBouncerBehavior(source,doc.RootElement,()=>Array.Empty<int>());
        var context=new BehaviorContext { Robot=rig.Robot,Triggers=new AnimationTriggerMap() };
        using var first=new BehaviorScope(motion:rig.Robot.Motion);
        using var replacement=new BehaviorScope(motion:rig.Robot.Motion);
        int words=0, callbacks=0;
        rig.Robot.Engine.ContextRandom.WordSourceForTest=()=>{words++;return 0u;};
        void OnLog(EngineLogEntry entry)
        {
            if(entry.Name!="AnimationStreamer.PushIdleAnimation" || callbacks++!=0)return;
            behavior.Stop(BehaviorStopReason.Interrupted);first.Dispose();
            behavior.StartAsync(context,replacement,CancellationToken.None).GetAwaiter().GetResult();
        }
        rig.Robot.Engine.LogEntry+=OnLog;
        try
        {
            await behavior.StartAsync(context,first,CancellationToken.None);
            Assert.Equal(6,words);
            Assert.True(behavior.EngineRunning);
            Assert.Equal(1,rig.Robot.Animations.Scheduler.IdleStack.Count(item=>item.Lock==behavior.Id));
        }
        finally
        {
            rig.Robot.Engine.LogEntry-=OnLog;
            rig.Robot.Engine.ContextRandom.WordSourceForTest=null;
            behavior.Stop(BehaviorStopReason.Interrupted);
        }
    }
    [Fact]
    public async Task MissingDetectorDirectionEndsConfiguredOwnerWithoutFakeDisplay()
    {
        // Supplied activity admission; actual configured factory and action queue.
        // Missing animations fail in the shared action path; Bouncer's void callbacks advance.
        using var rig=new Rig(); var robot=rig.Robot.Engine.Robot!;
        using var doc=JsonDocument.Parse("{\"behaviorClass\":\"Bouncer\",\"behaviorID\":\"Bouncer\"}");
        var inputs=new BehaviorFactoryContext { Vision=rig.Vision,Manipulation=rig.M,NativeRobot=robot,
            Features=new FeatureGates(new[] { ("Bouncer",true) }) };
        var factory=new BehaviorFactory(); var container=new BehaviorContainer(factory,inputs,new Dictionary<BehaviorID,JsonElement>());
        var behavior=Assert.IsType<NativeBouncerBehavior>(factory.CreateBehavior(container,BehaviorClass.Bouncer,inputs,doc.RootElement,out _));
        var origin=robot.GetNativeOrigin(robot.CurrentOriginId);
        rig.Vision.Faces.AddOrUpdateNativeFace(new TrackedFace(new DetectedFace(7,new FaceRect(0,0,10,10)),0),Pose3d.Identity,false,origin.Node!);
        var context=new BehaviorContext { Robot=rig.Robot,Triggers=new AnimationTriggerMap() };
        Assert.True(behavior.RunnableCore(context));
        var missing=new List<string>(); void Capture(string message)=>missing.Add(message);
        SteppedBehavior.MissingReported+=Capture;
        try
        {
            using var scope=new BehaviorScope(motion:rig.Robot.Motion);
            await behavior.StartAsync(context,scope,CancellationToken.None);
            bool running=true;
            for(int i=0;i<30 && running;i++)
            { running=behavior.Update(context,i*10); robot.ActionList.Update(); }
            Assert.False(running);
            Assert.Contains(missing,s=>s.Contains("M11-016/M14-010") && s.Contains("direction"));
            Assert.Null(behavior.OwnedRunner);
        }
        finally { SteppedBehavior.MissingReported-=Capture; behavior.Dispose(); }
    }
    [Fact]
    public void ConfiguredFactoryUsesFeatureGateAndRealFaceOriginSelection()
    {
        using var rig=new Rig();
        var robot=rig.Robot.Engine.Robot!;
        using var doc=JsonDocument.Parse("{\"behaviorClass\":\"Bouncer\",\"behaviorID\":\"Bouncer\"}");
        var context=new BehaviorContext { Robot=rig.Robot,Triggers=new AnimationTriggerMap() };
        var origin=robot.GetNativeOrigin(robot.CurrentOriginId);
        var face=new TrackedFace(new DetectedFace(7,new FaceRect(0,0,10,10)),1);
        rig.Vision.Faces.AddOrUpdateNativeFace(face,Pose3d.Identity,false,origin.Node!);
        NativeBouncerBehavior Create(bool enabled)
        {
            var factory=new BehaviorFactory();
            var inputs=new BehaviorFactoryContext { Vision=rig.Vision, Manipulation=rig.M, NativeRobot=robot,
                Features=new FeatureGates(new[] { ("Bouncer",enabled) }) };
            var container=new BehaviorContainer(factory,inputs,new Dictionary<BehaviorID,JsonElement>());
            var result=factory.CreateBehavior(container,BehaviorClass.Bouncer,inputs,doc.RootElement,out var status);
            Assert.Equal(BehaviorCreateStatus.Created,status);
            return Assert.IsType<NativeBouncerBehavior>(result);
        }
        using var off=Create(false); Assert.False(off.RunnableCore(context));
        using var on=Create(true); Assert.True(on.RunnableCore(context));
        var other=new TrackedFace(new DetectedFace(8,new FaceRect(0,0,10,10)),2)
            { HeadPose=new Pose3d(Rotation3d.Identity,new Vec3(500,200,50)) };
        rig.Vision.Faces.AddOrUpdateNativeFace(other,Pose3d.Identity,false,origin.Node!);
        var missing=new List<string>(); void Capture(string message)=>missing.Add(message);
        SteppedBehavior.MissingReported+=Capture;
        try
        {
            Assert.False(on.RunnableCore(context));
            Assert.Contains(missing,s=>s.Contains("M14-001/M11-066") && s.Contains("multi-face"));
        }
        finally { SteppedBehavior.MissingReported-=Capture; }
    }
}
