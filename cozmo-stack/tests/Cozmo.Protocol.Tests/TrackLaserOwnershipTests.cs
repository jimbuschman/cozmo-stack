using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Xunit;
namespace Cozmo.Protocol.Tests;

// These constrain the visible implementation gap, not the unbuilt native lifecycle.
[Collection("SteppedBehavior missing-report statics")]
public sealed class TrackLaserOwnershipTests
{
    private static string Obb()
    {
        for(var d=new DirectoryInfo(AppContext.BaseDirectory);d is not null;d=d.Parent)
        {var p=Path.Combine(d.FullName,"re-analysis","obb");if(Directory.Exists(p))return p;}
        throw new InvalidOperationException("OBB required");
    }
    [Fact]
    public void ShippedFactoryNamesMissingCameraLifecycleOwnerWithoutSourceLogs()
    {
        SteppedBehavior.ResetMissingForTests();var missing=new List<string>();var source=new List<string>();
        void OnMissing(string s)=>missing.Add(s);
        SteppedBehavior.MissingReported+=OnMissing;
        try
        {
            var container=BehaviorContainer.LoadShipped(Obb(),new BehaviorFactoryContext{SourceLog=(l,c,k,t)=>source.Add(k+":"+t)});
            Assert.False(container.Behaviors.ContainsKey(BehaviorID.SparksTrackLaser));
            Assert.Single(missing.Where(s=>s.Contains("M7-025/M7-029 TrackLaser factory")));
            Assert.Contains(missing,s=>s.Contains("0x005FAC4E")&&s.Contains("0x005FBD40")&&s.Contains("M11-087"));
            Assert.DoesNotContain(source,s=>s.Contains("MISSING")||s.Contains("M7-025"));
        }
        finally{SteppedBehavior.MissingReported-=OnMissing;}
    }
    [Fact]
    public void ActualFreeplayAssemblyKeepsTrackLaserUnboundAndReportsTheDependencyOnStackChannel()
    {
        SteppedBehavior.ResetMissingForTests();using var robot=CozmoRobot.CreateOffline();var stackLog=new List<string>();var source=new List<string>();
        robot.Engine.StackLogLine+=stackLog.Add;robot.Engine.LogEntry+=entry=>source.Add(entry.Name+":"+entry.Text);
        var ctx=new BehaviorContext{Robot=robot,Triggers=new AnimationTriggerMap()};
        using var stack=FreeplayStack.Create(Obb(),robot,ctx,()=>1,needs:new NeedsManager(()=>0),withReactions:false);
        Assert.False(stack.Bound.ContainsKey("SparksTrackLaser"));Assert.Contains("SparksTrackLaser",stack.UnboundIds);
        Assert.Contains(stackLog,s=>s.Contains("M7-025/M7-029 TrackLaser factory"));
        Assert.DoesNotContain(source,s=>s.Contains("MISSING")||s.Contains("M7-025"));
    }
}
