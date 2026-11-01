using Cozmo.Robot.Behavior;
using System.Reflection;
using System.Text.Json;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class ConfiguredBehaviorLookupTests
{
    [Fact]
    public void ConfiguredReactionLookupReturnsSameOwnerWithoutWideningHostScoring()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        var manager=f.Robot.BehaviorManagerOwner!;var bound=f.Runtime.Bound;
        var ack=Assert.IsType<AcknowledgeObjectBehavior>(bound["AcknowledgeObject"]);
        Assert.Same(ack,Assert.Single(manager.Reactions.Where(r=>r.Strategy.Trigger==ReactionTrigger.ObjectPositionUpdated)).Behavior);
        Assert.Same(ack,manager.Find("AcknowledgeObject"));
        Assert.Same(ack,Assert.Single(manager.FindByClass("AcknowledgeObject")));
        var hiccup=Assert.Single(manager.Reactions.Where(r=>r.Strategy.Trigger==ReactionTrigger.Hiccup)).Behavior;
        Assert.Same(bound[hiccup.Id],hiccup);Assert.Same(hiccup,manager.Find(hiccup.Id));
        Assert.Contains(hiccup,manager.FindByClass(hiccup.Class));
        Assert.Null(manager.Find("not-a-configured-behavior"));
        Assert.DoesNotContain(ack,manager.Behaviors);Assert.DoesNotContain(hiccup,manager.Behaviors);
        // This is a host scoring regression, not a claim that native ranks the global container.
        var host=M10Support.RunnableBehaviour("host-lookup-negative-control");manager.Add(host);
        Assert.Same(host,manager.Find(host.Id));
        var score=manager.ChooseAndSwitch(f.Rig.Engine.Timer.SecondsF);
        Assert.Contains(score.Scores,s=>s.Item1==host.Id);
        Assert.DoesNotContain(score.Scores,s=>s.Item1==ack.Id || s.Item1==hiccup.Id);
    }

    [Fact]
    public void ExplicitStartUsesConfiguredReactionInstance()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        var manager=f.Robot.BehaviorManagerOwner!;
        var ack=Assert.IsType<AcknowledgeObjectBehavior>(f.Runtime.Bound["AcknowledgeObject"]);
        f.Add(7);ack.ResetTargets(new uint[]{7}); // Supplied target boundary; real strategy delivery is covered by AcknowledgeObjectNativeTests.
        Assert.True(ack.IsRunnable(f.Runtime.Context));
        Assert.True(manager.StartAsync(ack.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        Assert.Same(ack,manager.Current);Assert.True(ack.EngineRunning);
    }

    [Fact]
    public void ShippedHikingScoringChooserKeepsItsOwnConfiguredCandidates()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        string path=Path.Combine(f.Rig.Engine.Options.ResourcesPath!,"config","engine","behaviorSystem","activities","freeplay","hiking.json");
        using var json=JsonDocument.Parse(File.ReadAllText(path),new JsonDocumentOptions{CommentHandling=JsonCommentHandling.Skip,AllowTrailingCommas=true});
        var expected=json.RootElement.GetProperty("behaviorChooser").GetProperty("behaviors").EnumerateArray()
            .Select(e=>e.GetProperty("behaviorID").GetString()!).ToArray();
        var activity=NativeBehaviorRuntime.AllActivities(f.Runtime.Tree).Single(a=>a.Id=="Hiking");
        var chooser=Assert.IsType<ScoringChooser>(activity.Chooser);
        var actual=Assert.IsAssignableFrom<IReadOnlyDictionary<string,IBehavior>>(typeof(ScoringChooser)
            .GetField("_bound",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(chooser));
        Assert.Equal(expected,chooser.BehaviorIds);
        Assert.Equal(expected.Where(f.Runtime.Bound.ContainsKey).OrderBy(x=>x),actual.Keys.OrderBy(x=>x));
        foreach(var entry in actual)Assert.Same(f.Runtime.Bound[entry.Key],entry.Value);
        Assert.DoesNotContain("AcknowledgeObject",actual.Keys);
        var evaluated=Assert.IsAssignableFrom<IReadOnlyList<ScoredBehaviorEntry>>(typeof(ScoringChooser)
            .GetField("_evaluationEntries",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(chooser));
        Assert.Equal(expected.Distinct().OrderBy(id=>(int)Enum.Parse<BehaviorID>(id)),evaluated.Select(e=>e.BehaviorId));
        Assert.Contains("Hiking_PounceOnMotion",evaluated.Select(e=>e.BehaviorId));
        Assert.Contains("Hiking_LookInPlaceForUnknown",evaluated.Select(e=>e.BehaviorId));
    }

    [Fact]
    public void CanonicalScorerDistinguishesAbsentConfigFromUnbuiltConfiguredOwner()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();const string id="SparksTrackLaser";
        Assert.False(f.Runtime.Bound.ContainsKey(id));Assert.True(f.Rig.Engine.HostBehaviorMap!.ContainsKey(BehaviorID.SparksTrackLaser));
        var entry=f.Robot.NativeConfiguredScoreFields![id].Value!;
        var chooser=new ScoringChooser(new[]{entry},f.Runtime.Bound,native:true);chooser.EnableOrdinaryUnbuiltBoundary(f.Robot);
        Assert.True(chooser.GetDesiredActiveBehavior(null,0,f.Runtime.Context,0).InputUnavailable);
        var property=f.Rig.Engine.GetType().GetProperty("HostBehaviorMap",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var original=f.Rig.Engine.HostBehaviorMap!;var changed=original.Where(p=>p.Key!=BehaviorID.SparksTrackLaser).ToDictionary(p=>p.Key,p=>p.Value);
        try
        {
            property.SetValue(f.Rig.Engine,changed);
            var absent=chooser.GetDesiredActiveBehavior(null,0,f.Runtime.Context,0);
            Assert.False(absent.InputUnavailable);Assert.Null(absent.Behavior);Assert.Equal("NativeAbsent",Assert.Single(absent.Scores).Note);
        }
        finally {property.SetValue(f.Rig.Engine,original);}
    }

    [Fact]
    public void StandaloneHostAddStillReplacesKnownId()
    {
        using var f=new PickupPyramidNativeExecutionTests.Fixture();
        using var standalone=new BehaviorManager(f.Runtime.Context,null,false);
        var first=M10Support.RunnableBehaviour("Wait");var replacement=M10Support.RunnableBehaviour("Wait");
        standalone.Add(first);standalone.Add(replacement);Assert.Same(replacement,standalone.Find("Wait"));
    }
}
