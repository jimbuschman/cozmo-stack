using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using System.Reflection;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public class NativeBouncerScopeTests
{
    [Fact]
    public void CountCleanupAbortLogCannotOverwriteReplacementStream()
    {
        // Supplied active idle/neutral state isolates the checked Count cleanup branch;
        // the replacement uses the actual scheduler, not a timer or fabricated completion.
        using var rig=new Rig(); var scheduler=rig.Robot.Animations.Scheduler;
        using var scope=new BehaviorScope();
        scope.AcquireScopedIdle(scheduler,(AnimationTrigger)0x23F,"Bouncer",()=>0);
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        typeof(AnimationScheduler).GetField("_idleAnim",flags)!.SetValue(scheduler,StreamAnimation.Live());
        typeof(AnimationScheduler).GetField("_tag",flags)!.SetValue(scheduler,(byte)7);
        typeof(AnimationScheduler).GetField("_neutral",flags)!.SetValue(scheduler,new AnimationClip{Name="neutral"});
        var replacement=new AnimationClip { Name="replacement",Tracks=AnimationTrack.Head,
            Keyframes=new Keyframe[] { new HeadKeyframe(0,100,10,0) },DurationMs=100 };
        AnimationHandle? ticket=null; bool reentered=false;
        scheduler.SourceLog=(level,channel,key,text)=>
        {
            if(key!="AnimationStreamer.Abort" || reentered)return;
            reentered=true;ticket=scheduler.Play(replacement,0);
        };
        scope.Dispose();
        Assert.True(reentered);Assert.NotNull(ticket);
        Assert.Equal("replacement",scheduler.Playing);
        Assert.Equal(ticket!.Tag,scheduler.CurrentTag);
    }
    [Fact]
    public void ReentryDuringCountIdleRemovalKeepsReplacementEntry()
    {
        using var rig=new Rig(); var scheduler=rig.Robot.Animations.Scheduler;
        using var first=new BehaviorScope(); using var replacement=new BehaviorScope();
        first.AcquireScopedIdle(scheduler,(AnimationTrigger)0x23F,"Bouncer",()=>0);
        scheduler.PushIdleAnimation((AnimationTrigger)0x23F,"other");
        bool reentered=false;
        void Log(EngineLogEntry entry)
        {
            if(entry.Name!="AnimationStreamer.RemoveIdleAnimation.AnimationRemovedFromMiddleOfStack" || reentered)return;
            reentered=true;replacement.AcquireScopedIdle(scheduler,(AnimationTrigger)0x23F,"Bouncer",()=>0);
        }
        rig.Robot.Engine.LogEntry+=Log;
        try
        {
            first.Dispose(); Assert.True(reentered);
            Assert.Equal(1,scheduler.IdleStack.Count(item=>item.Lock=="Bouncer"));
        }
        finally { rig.Robot.Engine.LogEntry-=Log; }
    }
    private sealed class Strategy : IReactionTriggerStrategy
    {
        internal Action<bool>? Changed;
        public ReactionTrigger Trigger => (ReactionTrigger)1;
        public string Basis => "test";
        public bool ShouldResumeLast => false;
        public bool CanInterruptOtherTriggeredBehavior => true;
        public bool CanInterruptSelf => false;
        public BehaviorManager? Manager { get; set; }
        public bool ShouldTriggerBehavior(ReactionContext context,IBehavior behavior)=>false;
        public void EnabledStateChanged(BehaviorContext context,bool enabled)=>Changed?.Invoke(enabled);
    }
    [Theory]
    [InlineData("acquire-log")]
    [InlineData("strategy")]
    [InlineData("cleanup-log")]
    public void ReentrantUnscopedSameNameAdmissionSurvivesOldScopedWork(string point)
    {
        using var rig=new Rig();
        var context=new BehaviorContext { Robot=rig.Robot,Triggers=new AnimationTriggerMap() };
        using var manager=new BehaviorManager(context);
        var strategy=new Strategy(); manager.AddReaction(strategy,new WaitBehavior("scope-test"));
        using var first=new BehaviorScope(manager:manager); bool reentered=false;
        void Replace()
        {
            if(reentered)return;reentered=true;
            manager.DisableReactionsWithLock("Bouncer_behaviorLock",ReactionLockTables.Bouncer,false);
        }
        if(point=="strategy")strategy.Changed=enabled=>{if(!enabled)Replace();};
        manager.Log+=message=>
        {
            if(point=="acquire-log" && message.Contains("DisablingWithLock"))Replace();
            if(point=="cleanup-log" && message.Contains("RemoveDisableReactionsLock"))Replace();
        };
        first.AcquireScopedReactions("Bouncer",ReactionLockTables.Bouncer);
        first.Dispose();
        Assert.True(reentered);
        Assert.True(manager.HasDisableLock((ReactionTrigger)1,"Bouncer_behaviorLock"));
    }
    [Fact]
    public void InterruptedReactionAcquisitionCannotInsertAfterReplacement()
    {
        using var rig=new Rig();
        var context=new BehaviorContext { Robot=rig.Robot,Triggers=new AnimationTriggerMap() };
        using var manager=new BehaviorManager(context);
        var strategy=new Strategy(); manager.AddReaction(strategy,new WaitBehavior("scope-test"));
        using var first=new BehaviorScope(manager:manager); using var replacement=new BehaviorScope(manager:manager);
        bool reentered=false;
        strategy.Changed=enabled=>
        {
            if(enabled || reentered)return;
            reentered=true; first.Dispose(); replacement.AcquireScopedReactions("Bouncer",ReactionLockTables.Bouncer);
        };
        first.AcquireScopedReactions("Bouncer",ReactionLockTables.Bouncer);
        Assert.True(reentered);
        Assert.True(manager.HasDisableLock((ReactionTrigger)1,"Bouncer_behaviorLock"));
        replacement.Dispose();
        Assert.False(manager.HasDisableLock((ReactionTrigger)1,"Bouncer_behaviorLock"));
    }
    [Fact]
    public void CompletedScopeRetainsNativeNameWideAndLastMatchCleanup()
    {
        using var rig=new Rig();
        var context=new BehaviorContext { Robot=rig.Robot,Triggers=new AnimationTriggerMap() };
        using var manager=new BehaviorManager(context);
        manager.DisableReactionsWithLock("Bouncer_behaviorLock",ReactionLockTables.Bouncer,false);
        using var scope=new BehaviorScope(manager:manager);
        scope.AcquireScopedReactions("Bouncer",ReactionLockTables.Bouncer);
        var scheduler=rig.Robot.Animations.Scheduler;
        scheduler.PushIdleAnimation((AnimationTrigger)0x23F,"Bouncer");
        scope.AcquireScopedIdle(scheduler,(AnimationTrigger)0x23F,"Bouncer",()=>0);
        scheduler.PushIdleAnimation((AnimationTrigger)0x23F,"Bouncer");
        scope.Dispose();
        Assert.False(manager.HasDisableLock((ReactionTrigger)1,"Bouncer_behaviorLock"));
        Assert.Equal(2,scheduler.IdleStack.Count(item=>item.Lock=="Bouncer"));
    }
}
