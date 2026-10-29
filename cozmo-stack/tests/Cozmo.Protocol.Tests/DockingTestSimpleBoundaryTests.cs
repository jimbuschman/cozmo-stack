using System.Text.Json;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public class DockingTestSimpleBoundaryTests
{
    // reach: unit/custom-adapter diagnostic coverage, NOT native DockingTestSimple conformance.
    // Checked diagnostic contract: 20261029-docking-test-simple-1-read.md, minimal contract 1-5.
    // Select the historical Navigation entry; direct Start substitutes manual admission with no charger
    // (so its runnable predicate would refuse). The real MountChargerAction receives ObjectId=100.
    // Missing-charger result is BadObject 0x03000004: M13-008, Init 0x0054E0CC..0x0054E170.
    [Fact]
    public void CustomAdapterReportsBoundaryBeforeLockAndActionAndRetainsBadObjectCompletion()
    {
        using var rig = new Rig();
        var behavior = ShippedBehaviors.Navigation(rig.M).OfType<MountChargerBehavior>().Single();
        Assert.Equal("DockingTestSimple", behavior.Id);
        Assert.Equal("DockingTestSimple", behavior.Class);
        Assert.Equal("DockingTestSimple", new MountChargerBehavior(rig.M).Id);
        Assert.Null(rig.M.World.GetLocatedObjectById(ChargerGeometry.ObjectId));
        using var scope = new BehaviorScope();
        var context = new BehaviorContext { Robot = rig.Robot, Triggers = new AnimationTriggerMap() };
        var events = new List<string>();
        void Missing(string line)
        {
            if (!line.Contains("M7-022")) return;
            Assert.False(scope.ReactionsDisabled);
            Assert.DoesNotContain("start MountChargerAction", behavior.Trace);
            events.Add(line);
        }
        void Step(string line)
        {
            if (line == "start MountChargerAction") Assert.True(scope.ReactionsDisabled);
            events.Add(line);
        }
        SteppedBehavior.ResetMissingForTests();
        SteppedBehavior.MissingReported += Missing;
        behavior.Step += Step;
        try
        {
            behavior.StartAsync(context, scope, default).GetAwaiter().GetResult();
            var diagnostic = Assert.Single(events, line => line.StartsWith("MISSING: M7-022"));
            Assert.Contains("native DockingTestSimple", diagnostic);
            Assert.Contains("excluded/unrecovered", diagnostic);
            Assert.Contains("custom MountCharger adapter", diagnostic);
            Assert.True(events.IndexOf(diagnostic) < events.IndexOf("start MountChargerAction"));
            behavior.AsyncWorkCompletion.WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
            Assert.Null(behavior.Result); // completion still belongs to the behavior update
            Assert.False(behavior.Update(context, 1000));
            Assert.Equal(ActionResult.BadObject, behavior.Result);
            Assert.Contains("MountChargerAction -> BadObject", behavior.Trace);
            Assert.Contains(behavior.Trace, line => line.Contains("MountChargerAction.Init.InvalidCharger"));
            Assert.Equal("done", behavior.Trace.Last());
        }
        finally
        {
            behavior.Step -= Step;
            SteppedBehavior.MissingReported -= Missing;
            behavior.Stop(BehaviorStopReason.Completed);
        }
    }

    // reach: unit/custom-adapter boundary coverage, not native behavior conformance.
    // Checked contract 4-5 preserves canonical unsupported status. Native class/ID9 and ctor
    // 0x0059CA88 -> 0x005CAFB0 establish metadata, not a C# constructor or supplied caller.
    [Fact]
    public void CanonicalFactoryDoesNotSubstituteTheCustomAdapter()
    {
        using var json = JsonDocument.Parse("{\"behaviorClass\":\"DockingTestSimple\",\"behaviorID\":\"DockingTestSimple\"}");
        var factory = new BehaviorFactory();
        var context = new BehaviorFactoryContext();
        var container = new BehaviorContainer(factory, context, Array.Empty<KeyValuePair<BehaviorID, JsonElement>>());
        var behavior = factory.CreateBehavior(container, BehaviorClass.DockingTestSimple, context, json.RootElement, out var status);
        Assert.Null(behavior);
        Assert.Equal(BehaviorCreateStatus.NotImplemented, status);
    }
}
