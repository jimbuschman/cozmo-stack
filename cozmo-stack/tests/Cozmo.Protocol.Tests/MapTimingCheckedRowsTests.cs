using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Cozmo.Transport;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class MapTimingCheckedRowsTests
{
    [Theory]
    [InlineData(30000u,0u,0u)]
    [InlineData(30001u,1u,0u)]
    [InlineData(120000u,90000u,0u)]
    [InlineData(120001u,90001u,1u)]
    [InlineData(16777217u,16747216u,16657216u)] // VCVT.F32.U32 rounds before subtracting
    public void ThresholdWordsFollowNativeF32Order(uint now,uint cliff,uint edge)
    {var p=SourceMapAging.At(now);Assert.Equal(cliff,p.CliffThreshold);Assert.Equal(edge,p.EdgeThreshold);}
    [Theory]
    [InlineData(8,30000u)]
    [InlineData(9,120000u)]
    [InlineData(10,120000u)]
    public void CallbackExpiresOnEqualityAndCreatesFreshUnknownWithBothStamps(int type,uint now)
    {
        var plan=SourceMapAging.At(now);var old=new SourceMapData((MemoryMapContentType)type,0,0);
        var changed=plan.Apply(old);Assert.NotSame(old,changed);Assert.Equal(MemoryMapContentType.Unknown,changed.Type);Assert.Equal(now,changed.First);Assert.Equal(now,changed.Last);
        old.Last=1;Assert.Same(old,plan.Apply(old));Assert.Equal((MemoryMapContentType)type,old.Type);
    }
    [Fact]
    public void CallbackPreservesOtherTypesAndDoesNotMutateTheOldData()
    {
        var p=SourceMapAging.At(200000);for(int type=0;type<8;type++){var d=new SourceMapData((MemoryMapContentType)type,2,3);Assert.Same(d,p.Apply(d));Assert.Equal(3u,d.Last);}
        var cliff=new CliffMapData(new Vec2(1,0),1);Assert.NotSame(cliff,p.Apply(cliff));Assert.Equal(1u,cliff.Last);Assert.Equal(new Vec2(1,0),cliff.Normal);
    }
    [Fact]
    public void DefaultIntervalReturnsWithoutMapLookupAndRawRenderByteDoesNotGateScheduler()
    {
        var m=new SourceMapComponent();Assert.Equal(1,m.Enabled);Assert.Equal(0xBF800000u,BitConverter.SingleToUInt32Bits(m.BroadcastInterval));Assert.Equal(0f,m.BroadcastDeadline);
        int due=0;m.BroadcastDueForTest=()=>due++;m.CurrentOrigin=4;m.Maps[4]=null!;
        m.BroadcastMap(10);Assert.Equal(0,due);Assert.Equal(0f,m.BroadcastDeadline);
        m.SetRenderEnabled(0xA5);Assert.Equal(0xA5,m.Enabled);m.SetBroadcastInterval(float.NaN,7);m.BroadcastMap(10);Assert.Equal(0,due);Assert.Equal(7f,m.BroadcastDeadline);
    }
    [Fact]
    public void SchedulerCatchesUpOnceAndStoresDeadlineBeforeUnknownOriginLookup()
    {
        var m=new SourceMapComponent();m.SetRenderEnabled(0);m.SetBroadcastInterval(2,1);int due=0;m.BroadcastDueForTest=()=>{due++;Assert.Equal(9f,m.BroadcastDeadline);};
        m.Update(100000,7);Assert.Equal(1,due);Assert.Equal(9f,m.BroadcastDeadline);Assert.Null(m.LastAgingForTest);
        m.BroadcastMap(8);Assert.Equal(1,due);
    }
    [Fact]
    public void ActualRobotComponentsTickUsesStoredTimestampAndReportsMissingTransformWithoutTouchingLegacyRegions()
    {
        using var rig=new Rig();var m=rig.Robot.LocalizationWorld.Maps;
        Assert.NotNull(m.Current);var map=m.Current!;map.Insert(new[]{new Vec2(0,0),new Vec2(1,0)},MemoryMapContentType.Cliff,timestamp:1);
        Cozmo.Robot.Behavior.SteppedBehavior.ResetMissingForTests();var logs=new List<string>();var source=new List<string>();rig.Robot.Engine.StackLogLine+=logs.Add;rig.Robot.Engine.LogEntry+=e=>source.Add(e.Name+":"+e.Text);
        rig.State();rig.Robot.Engine.Tick();
        Assert.Equal(rig.Robot.Engine.Robot!.StoredState!.Timestamp,m.LastAgingForTest!.Value.Now);
        Assert.Single(map.Regions);Assert.Contains(logs,s=>s.Contains("M11-075/M11-046 aging"));Assert.DoesNotContain(source,s=>s.Contains("MISSING"));
    }
    [Fact]
    public void PoseConfirmerRoutesInvalidToValidTwiceBeforeStateRecipients()
    {
        var world=new BlockWorld();var obj=new ObservableObject(7,ObjectType.Block_LIGHTCUBE1,Array.Empty<KnownMarker>()){PoseState=PoseState.Known};
        var calls=new List<string>();world.SetObjectPoseChanged=(_,_)=>calls.Add("world");world.Maps.ObjectRouteForTest=calls.Add;world.PoseStateChanged+=(_,_,_)=>calls.Add("recipient");
        world.BroadcastObjectPoseChanged(obj,null,PoseState.Unknown);
        Assert.Equal(new[]{"world","add","add","recipient"},calls);Assert.Empty(world.Maps.Current!.Regions);
    }
    [Fact]
    public void BothInvalidUsesNativeErrorAndErrGBeforeReturningWithoutMapChanges()
    {
        bool old=EngineErrorState.ErrorFlagSet;try
        {
            EngineErrorState.ErrorFlagSet=false;var m=new SourceMapComponent();var logs=new List<(EngineLogLevel,string?,string,string)>();m.SourceLog=(l,c,k,t)=>logs.Add((l,c,k,t));
            var obj=new ObservableObject(uint.MaxValue,ObjectType.Block_LIGHTCUBE1,Array.Empty<KnownMarker>());m.ObjectRouteForTest=_=>throw new Exception("must not route");
            m.UpdateObjectPose(obj,null,PoseState.Unknown);
            Assert.Equal((EngineLogLevel.Error,(string?)null,"MapComponent.OnObjectPoseChanged.BothStatesAreInvalid","Object -1 changing from Invalid to Invalid"),Assert.Single(logs));Assert.True(EngineErrorState.ErrorFlagSet);
        }
        finally{EngineErrorState.ErrorFlagSet=old;}
    }
}
