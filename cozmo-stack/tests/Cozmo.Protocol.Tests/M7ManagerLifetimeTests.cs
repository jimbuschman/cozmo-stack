using System.Reflection;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class M7ManagerLifetimeTests
{
    private static BehaviorManager? Manager(CozmoRobot robot)=>(BehaviorManager?)typeof(CozmoRobot).GetProperty("BehaviorManager",BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(robot);
    private static string Obb()
    {
        for(var d=new DirectoryInfo(AppContext.BaseDirectory);d is not null;d=d.Parent)
            if(Directory.Exists(Path.Combine(d.FullName,"re-analysis","obb","assets")))return Path.Combine(d.FullName,"re-analysis","obb");
        throw new DirectoryNotFoundException();
    }
    private static string Resources()=>Path.Combine(Obb(),"assets","cozmo_resources");
    private static BehaviorManager CheckedManager(CozmoRobot robot)=>Assert.IsType<BehaviorManager>(Manager(robot));
    private const string Lock="severe_need_component_lock";

    [Fact]
    public void RealReactionEntriesAndLocksSurviveTheFreeplayHandle()
    {
        // reach: production native construction/G2E wake/actual Freeplay composition and disposal.
        // Old: no manager before Freeplay, then a different owner whose Dispose retired its entries.
        using var rig=new M4ControlTests.Rig(Resources());rig.ToSynced();rig.State();
        using var m=new ManipulationSystem(rig.Robot,rig.Robot.Vision);
        var manager=CheckedManager(rig.Robot);
        Assert.Same(manager,m.ReactionLocks);
        rig.Engine.Needs.SetLevel(NeedId.Repair,0);rig.Engine.Needs.SetLevel(NeedId.Energy,1);
        rig.Robot.NotifyCozmoWakeup();rig.Tick();
        var strategy=manager.Reactions.First(r=>r.Strategy.Trigger==ReactionTrigger.RobotPickedUp).Strategy;
        Assert.True(manager.HasDisableLock(ReactionTrigger.RobotPickedUp,Lock));
        using(var stack=FreeplayStack.CreateNative(rig.Robot))
        {
            Assert.Same(manager,stack.Manager);
            Assert.Same(strategy,manager.Reactions.First(r=>r.Strategy.Trigger==ReactionTrigger.RobotPickedUp).Strategy);
            Assert.True(manager.HasDisableLock(ReactionTrigger.RobotPickedUp,Lock));
        }
        Assert.Same(manager,CheckedManager(rig.Robot));Assert.Same(manager,m.ReactionLocks);
        Assert.True(manager.HasDisableLock(ReactionTrigger.RobotPickedUp,Lock));
        Assert.Contains(manager.Reactions,r=>ReferenceEquals(r.Strategy,strategy));
        rig.Engine.Needs.SetSevereExpressed(NeedId.Repair,false);
        Assert.False(manager.HasDisableLock(ReactionTrigger.RobotPickedUp,Lock));
    }

    [Fact]
    public void NativeRemovalDropsEntriesAndReconnectBuildsAFreshUnlockedManager()
    {
        // reach: unit lifecycle interface: actual RobotManager Remove/Add (transport handshake not repeated).
        // Old: no native-owned manager to retire; host manager/locks outlived native removal.
        using var rig=new M4ControlTests.Rig(Resources());rig.ToSynced();
        using var m=new ManipulationSystem(rig.Robot,rig.Robot.Vision);
        var old=CheckedManager(rig.Robot);old.DisableAllReactionsWithLock("lifetime");
        Assert.True(old.HasDisableLock(ReactionTrigger.RobotPickedUp,"lifetime"));
        rig.Engine.InvokeEngineRequest(()=>{rig.Engine.Robots.RemoveRobot(CozmoEngine.RobotId,false);return 0;});
        Assert.Null(Manager(rig.Robot));Assert.Empty(old.Reactions);
        Assert.False(old.HasDisableLock(ReactionTrigger.RobotPickedUp,"lifetime"));
        rig.Engine.InvokeEngineRequest(()=>{rig.Engine.Robots.AddRobot(CozmoEngine.RobotId);return 0;});
        var fresh=CheckedManager(rig.Robot);Assert.NotSame(old,fresh);Assert.NotEmpty(fresh.Reactions);Assert.Same(fresh,m.ReactionLocks);
        foreach(var trigger in Enum.GetValues<ReactionTrigger>())Assert.False(fresh.HasDisableLock(trigger,"lifetime"));
    }

    [Fact]
    public void AnEmptyStartupMapDoesNotSaveLocksForLaterConfiguration()
    {
        // reach: unit absent-resource input plus real native owner/explicit later Freeplay resource binding.
        // Old: no native manager; a standalone manager precreated all21 entries and retained such locks.
        using var rig=new M4ControlTests.Rig();rig.ToSynced();rig.State();
        var manager=CheckedManager(rig.Robot);Assert.Empty(manager.Reactions);
        manager.DisableAllReactionsWithLock("before-map");
        Assert.False(manager.HasDisableLock(ReactionTrigger.RobotPickedUp,"before-map"));
        using var m=new ManipulationSystem(rig.Robot,rig.Robot.Vision);
        manager.InitializeRobotReactions(Resources(),rig.Robot.Vision); // explicit native resource input; no deferred-lock replay
        Assert.NotEmpty(manager.Reactions);
        Assert.False(manager.HasDisableLock(ReactionTrigger.RobotPickedUp,"before-map"));
    }
}
