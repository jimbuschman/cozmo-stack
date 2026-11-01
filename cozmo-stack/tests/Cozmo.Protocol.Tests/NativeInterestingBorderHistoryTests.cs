using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
using Xunit;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class NativeInterestingBorderHistoryTests
{
    [Theory]
    [InlineData(6)]
    [InlineData(9)]
    public void EnabledProcessRefreshesActualEmptySeedsAndDisabledProcessRetainsHistory(int seed)
    {
        using var f=new Fixture();f.Rig.Robot.Animations.LoadFrom(f.Rig.Engine.Options.ResourcesPath!);
        var analyzer=f.Runtime.AI.Analyzer;var edge=Assert.IsType<VisitInterestingEdgeBehavior>(f.Runtime.Bound["Hiking_VisitInterestingEdge"]);
        var map=f.M.World.Maps.Current!;var root=map.Tree.Root;
        root.ClearDescendants(map.Tree.Processor);
        root.ForceSet(new SourceNodeContent(new SourceMapData(MemoryMapContentType.Unknown,0,0)),map.Tree.Processor);
        Assert.False(map.HasContentType((MemoryMapContentType)9));Assert.False(map.HasContentType((MemoryMapContentType)6));
        Assert.True(analyzer.ActualBorderResultEmpty()); // source constructor, before this process request
        analyzer.AddEnableRequest(1,"test");analyzer.Update();Assert.True(analyzer.ActualBorderResultEmpty());
        int words=0;f.Rig.Engine.ContextRandom.WordSourceForTest=()=>{words++;return 0;};
        try {var ready=edge.EvaluateHostReadiness(f.Runtime.Context);Assert.True(ready.Available);Assert.False(ready.Runnable);Assert.Equal(0,words);}
        finally {f.Rig.Engine.ContextRandom.WordSourceForTest=null;}
        root.ForceSet(new SourceNodeContent(new SourceMapData((MemoryMapContentType)seed,0,0)),map.Tree.Processor);
        analyzer.AddDisableRequest(1,"test");analyzer.Update();Assert.True(analyzer.ActualBorderResultEmpty());
        analyzer.RemoveDisableRequest(1,"test");analyzer.Update();Assert.False(analyzer.ActualBorderResultEmpty());
        uint origin=f.M.World.Maps.CurrentOrigin;f.M.World.Maps.Maps[origin]=new MemoryMap();
        Assert.False(analyzer.ActualBorderResultEmpty()); // a changed map alone does not run the producer
        f.M.World.Maps.Maps[origin]=map;
        root.ForceSet(new SourceNodeContent(new SourceMapData(MemoryMapContentType.Unknown,0,0)),map.Tree.Processor);
        analyzer.AddDisableRequest(1,"test");analyzer.Update();Assert.False(analyzer.ActualBorderResultEmpty());
        analyzer.RemoveDisableRequest(1,"test");analyzer.Update();Assert.True(analyzer.ActualBorderResultEmpty());
        f.Rig.Engine.Robots.RemoveRobot(1,false);f.Rig.Engine.Robots.AddRobot(1,withRic:false);
        Assert.Null(analyzer.ActualBorderResultEmpty());
    }
}
