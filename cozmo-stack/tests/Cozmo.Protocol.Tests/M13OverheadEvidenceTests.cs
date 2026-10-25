using Cozmo.Conformance;
using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class M13OverheadEvidenceTests
{
    [Fact]
    public void FootprintOnlyCannotSatisfyCore007()
    {
        // reach: production footprint (connected robot state -> actual map); unit CORE-007 grading with
        // supplied frames/edge counters, no camera sequence. Old: true despite no overhead writer call.
        using var rig=new M4ControlTests.Rig();rig.ToSynced();rig.State();
        var map=Assert.IsType<MemoryMap>(rig.Robot.LocalizationWorld.Maps.Current);
        Assert.True(map.NativeContentCounts.GetValueOrDefault(MemoryMapContentType.ClearOfObstacle)>0);
        Assert.Equal(0,map.OverheadWriteCount);
        Assert.False(CoreChecks.Core007HasEvidence(1,1,map)); // CORE-007's existing "nothing reached the map" branch
        Assert.Equal("edge points were found but nothing reached the map",CoreChecks.Core007FailureReason(1,1));
    }

    [Fact]
    public void HasContentTypeUsesNativeTrackedContentInsteadOfLegacyRegions()
    {
        // reach: unit (explicit polygons/data, real native insert and processor index).
        // Old: false for native type9, true for legacy-only type10; wrapper 0x006817A8 forwards to tree.
        var map=new MemoryMap();
        map.InsertPreparedInterestingEdges(new[]{(new Point2f(-10,0),new Point2f(10,0))},1);
        Assert.True(map.HasContentType(MemoryMapContentType.InterestingEdge));
        map.FlagQuadAsNotInterestingEdges(new[]{new Vec2(30,0)});
        Assert.False(map.HasContentType(MemoryMapContentType.NotInterestingEdge));
    }

    [Fact]
    public void VisitStopNamesTheExplicitQuadWriterGap()
    {
        // reach: unit (protected Stop unit, bypasses unread lifecycle). Old: explicit 0x0067E6B0 absent.
        var messages=new List<string>();void Missing(string text)=>messages.Add(text);
        SteppedBehavior.ResetMissingForTests();SteppedBehavior.MissingReported+=Missing;
        try
        {
            var edge=new VisitInterestingEdgeBehavior("edge",new(false,105,110,15,0,10,20,1));
            typeof(VisitInterestingEdgeBehavior).GetMethod("OnStop",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!
                .Invoke(edge,new object[]{BehaviorStopReason.Completed});
            Assert.Contains(messages,s=>s.Contains("type10 FlagQuadAsNotInterestingEdges 0x0067E6B0/0x0067E71C"));
        }
        finally{SteppedBehavior.MissingReported-=Missing;}
    }
}
