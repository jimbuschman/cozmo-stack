using System.Text.Json;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Xunit;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

// CLIFF-SPARK-BEHAVIOR-GAPS-1: the listed MISSING sites are either unbound-instance
// seams or wiring guards. These tests pin that production builds the configured owners.
[Collection("SteppedBehavior missing-report statics")]
public sealed class CliffSparkBehaviorGapsTests
{
    // Production reaction rows are the configured instances, so ReactToCliff's
    // TransitionToBackingUp (0x00605318) takes NativeCliffBackUp and ReactToPickup's
    // StartAnim (0x00607A00..0x00607A2C) takes the native SayTextAction branch; the
    // unbound "no back-up drive" and "requires the original Robot speech owner" lines
    // belong to the public standalone constructors only.
    [Fact]
    public void ProductionCliffAndPickupReactionsAreNativeBound()
    {
        using var f=new Fixture();
        var manager=f.Robot.BehaviorManagerOwner!;
        var cliff=Assert.IsType<ReactToCliffBehavior>(Assert.Single(manager.Reactions,r=>r.Strategy.Trigger==ReactionTrigger.CliffDetected).Behavior);
        Assert.True(cliff.NativeBound);
        Assert.Same(f.Runtime.Bound["ReactToCliff"],cliff);
        var pickup=Assert.IsType<ReactToPickupBehavior>(Assert.Single(manager.Reactions,r=>r.Strategy.Trigger==ReactionTrigger.RobotPickedUp).Behavior);
        Assert.True(pickup.NativeBound);
        Assert.Same(f.Runtime.Bound["ReactToPickup"],pickup);
    }

    // Wiring guards: NativeSparkedActivity:62, NativeStackBlocksBehavior:55/:61/:73 and
    // NativePutDownActions:21/:22 throw only when an owner the Robot/runtime constructor
    // builds is absent. Production builds every one of them before any behaviour runs.
    [Fact]
    public void ProductionBuildsEveryGuardedOwner()
    {
        using var f=new Fixture();
        Assert.NotNull(f.Robot.BehaviorManagerOwner);
        Assert.Same(f.Robot,f.Rig.Engine.Robot);
        Assert.NotNull(f.Robot.CarryingOwner);                 // 0x0050FE24..0x0050FE4A
        Assert.NotNull(f.Runtime.Context.AI);
        Assert.Same(f.Runtime.AI,f.Runtime.Context.AI);
        Assert.IsType<NativeStackBlocksBehavior>(f.Runtime.Bound["StackBlocks"]);
        Assert.IsType<NativeStackBlocksBehavior>(f.Runtime.Bound["SparksStackBlock"]);
        // Sparked Reload 0x005B1008: BehaviorManager::FindBehaviorByID(0x3D = PlayArbitraryAnim).
        Assert.Equal(0x3D,(int)BehaviorID.PlayArbitraryAnim);
        Assert.IsType<NativePlayArbitraryAnimBehavior>(f.Runtime.Bound["PlayArbitraryAnim"]);
        // The production factory builds the native Bring class, which asks the real
        // ProgressionUnlockOwner; the legacy BringCubeToBeaconBehavior seam is test-only.
        Assert.IsType<NativeBringCubeToBeaconBehavior>(f.Runtime.Bound["Hiking_BringCubeToBeacon"]);
        Assert.IsType<NativeBringCubeToBeaconBehavior>(f.Runtime.Bound["SparksBringCubeToBeacon"]);
        Assert.DoesNotContain(f.Runtime.Bound.Values,b=>b is BringCubeToBeaconBehavior);
    }

    // Sparked Reload 0x005B10AC: JsonTools::ParseUint8("numberOfRepetitions"). Every
    // shipped Sparked activity carries a value inside 0..255, so the ParseUint8 MISSING
    // at NativeSparkedActivity:89 is unreachable with the shipped files.
    [Fact]
    public void ShippedSparkRepetitionsFitParseUint8AndReachTheOwner()
    {
        using var f=new Fixture();
        string dir=Path.Combine(f.Rig.Engine.Options.ResourcesPath!,"config","engine","behaviorSystem","activities","sparks");
        var files=Directory.GetFiles(dir,"*.json");
        Assert.Equal(14,files.Length);
        var activities=NativeBehaviorRuntime.AllActivities(f.Runtime.Tree).ToList();
        foreach(string file in files)
        {
            using var doc=JsonDocument.Parse(File.ReadAllText(file),new JsonDocumentOptions{CommentHandling=JsonCommentHandling.Skip,AllowTrailingCommas=true});
            var root=doc.RootElement;
            int repetitions=root.GetProperty("numberOfRepetitions").GetInt32();
            Assert.InRange(repetitions,0,255);
            string id=root.GetProperty("activityID").GetString()!;
            var owner=Assert.IsType<NativeSparkedActivity>(activities.Single(a=>a.Id==id).SparkedOwner);
            Assert.Equal(repetitions,owner.Repetitions);
        }
    }
}
