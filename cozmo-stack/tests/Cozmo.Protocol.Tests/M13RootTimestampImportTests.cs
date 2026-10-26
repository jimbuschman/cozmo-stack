using System.Reflection;
using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class M13RootTimestampImportTests
{
    private static MotionPrimitiveSet Primitives()
    {
        for(var d=new DirectoryInfo(AppContext.BaseDirectory);d is not null;d=d.Parent)
            if(MotionPrimitiveSet.FromObb(Path.Combine(d.FullName,"re-analysis","obb")) is {} p)return p;
        throw new InvalidOperationException("Shipped primitives required");
    }
    private static uint Cache(LatticePlanner planner)=>(uint?)typeof(LatticePlanner)
        .GetField("_lastImportedMapTimestamp",BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(planner) ?? uint.MaxValue;
    private static void PriorObstacle(LatticeEnvironment env)
        =>env.ImportObstaclePolygons(new[]{new P2f[]{new(500,500),new(540,500),new(540,540),new(500,540)}},false);

    [Fact]
    public void NewOriginsSeedBothRootTimesFromTheCurrentProcessedImage()
    {
        // reach: production (decoded-capture API and public delocalization, real native origin/map construction).
        // Old: root First=Last=0 after both captures. Row74 0x00681384..96 and checked VC getter0x006527A2.
        using var rig=new M4ControlTests.Rig();rig.ToSynced();rig.State();
        var vision=rig.Robot.Vision;vision.Calibration=CameraCalibration.Nominal();vision.Enabled=true;vision.ModeEnableMask=0;
        var image=new GrayImage(vision.Calibration.Columns,vision.Calibration.Rows);
        uint previous=0;
        for(uint frame=1;frame<=2;frame++)
        {
            rig.State();uint stamp=rig.Engine.Robot!.PoseHistory.NewestTimeStamp;Assert.True(stamp>previous);
            Assert.NotNull(vision.ProcessCapture(image,frame,stamp));
            Assert.Equal(stamp,rig.Robot.CameraSettings.LastProcessedImageTimestamp);
            vision.World.OnRobotDelocalized();var root=vision.World.Maps.Current!.Tree.Root.Content.Data;
            Assert.Equal(MemoryMapContentType.Unknown,root.Type);Assert.Equal((stamp,stamp),(root.First,root.Last));
            previous=stamp;
        }
    }

    [Fact]
    public void MissingDestinationOriginMapSamplesTheConstructionClockAgain()
    {
        // reach: unit (substituted clock and direct UpdateMapOrigins; native origin lookup/merge inputs not supplied).
        // Old: new destination root Last=0. Uses the same checked MemoryMap constructor, no new merge clock rule.
        using var maps=new SourceMapComponent();uint stamp=123;
        typeof(SourceMapComponent).GetField("ConstructionImageTimestamp",BindingFlags.Instance|BindingFlags.NonPublic)
            ?.SetValue(maps,(Func<uint>)(()=>stamp));
        maps.CreateLocalizedMemoryMap(1,_=>false);Assert.Equal(123u,maps.Current!.LastObservedTime);
        stamp=456;maps.UpdateMapOrigins(1,2,()=>{});Assert.Equal(456u,maps.Current!.LastObservedTime);
    }

    [Theory] [InlineData(200u,200u)] [InlineData(200u,199u)]
    [InlineData(0x80000000u,0x7FFFFFFFu)] [InlineData(0xFFFFFFF0u,0x10u)]
    public void TightImportsKeepTheSnapshotForEqualOrUnsignedOlderLast(uint cached,uint sampled)
    {
        // reach: unit (direct tool StartPlanning, substituted root Last and prior obstacle snapshot; native map obstacle-source join remains M13-003 MISSING).
        // Old: always rebuilds from empty located objects => count0 and tightPadding=true, instead of retained count1/false.
        // Row74 0x004FD512..51C uses unsigned cached<sampled, not inequality or signed comparison.
        using var rig=new M4ControlTests.Rig();rig.ToSynced();
        var world=rig.Robot.Vision.World;var env=new LatticeEnvironment(Primitives());var planner=new LatticePlanner(env);
        world.Maps.Current!.Tree.Root.Content.Data.Last=cached;planner.StartPlanning(world,null,true);
        PriorObstacle(env);world.Maps.Current.Tree.Root.Content.Data.Last=sampled;
        planner.StartPlanning(world,null,false);
        Assert.Equal(1,env.ObstacleCount);Assert.False(env.TightPadding);Assert.Equal(cached,Cache(planner));
    }

    [Fact]
    public void NewerLastImportsAndItsCachedValueControlsTheNextTightCall()
    {
        // reach: unit (same substituted root/snapshot and tool entry; M13-003 native content query remains MISSING).
        // Old: no per-planner cache (reflection sentinel MaxValue), and second unchanged tight call rebuilds count0.
        using var rig=new M4ControlTests.Rig();rig.ToSynced();var world=rig.Robot.Vision.World;
        var env=new LatticeEnvironment(Primitives());var planner=new LatticePlanner(env);
        world.Maps.Current!.Tree.Root.Content.Data.Last=100;planner.StartPlanning(world,null,true);
        PriorObstacle(env);world.Maps.Current.Tree.Root.Content.Data.Last=201;
        planner.StartPlanning(world,null,false);Assert.Equal(0,env.ObstacleCount);Assert.True(env.TightPadding);Assert.Equal(201u,Cache(planner));
        PriorObstacle(env);planner.StartPlanning(world,null,false);Assert.Equal(1,env.ObstacleCount);
    }

    [Fact]
    public void NonTightImportsEvenWhenUnchangedAndANewPlannerStartsWithItsOwnZeroCache()
    {
        // reach: unit (substituted root/snapshot, two real planner owners; M13-003 source-query join MISSING).
        // Old: preliminary unchanged tight call clears count to0; there is no per-planner cache.
        // Non-tight unconditional rebuild is a retained control; ctor0x004FCCDE establishes zero for a new planner.
        using var rig=new M4ControlTests.Rig();rig.ToSynced();var world=rig.Robot.Vision.World;
        var env=new LatticeEnvironment(Primitives());var first=new LatticePlanner(env);
        world.Maps.Current!.Tree.Root.Content.Data.Last=200;first.StartPlanning(world,null,true);PriorObstacle(env);
        first.StartPlanning(world,null,false);Assert.Equal(1,env.ObstacleCount);
        first.StartPlanning(world,null,true);Assert.Equal(0,env.ObstacleCount);Assert.False(env.TightPadding);Assert.Equal(200u,Cache(first));
        PriorObstacle(env);world.Maps.Current.Tree.Root.Content.Data.Last=100;
        first.StartPlanning(world,null,false);Assert.Equal(1,env.ObstacleCount);
        var second=new LatticePlanner(env);Assert.Equal(0u,Cache(second));second.StartPlanning(world,null,false);
        Assert.Equal(0,env.ObstacleCount);Assert.Equal(100u,Cache(second));Assert.Equal(200u,Cache(first));
    }

    [Fact]
    public void ImportSkipsWhenAnotherThreadOwnsTheExistingPlannerMutex()
    {
        // reach: unit (holds actual runtime mutex on another thread; invokes tool import, not a robot path request).
        // Old: tool import ignores this mutex and clears count1 to0. Native failed try-lock skips0x004FD4DA..E2.
        using var rig=new M4ControlTests.Rig();rig.ToSynced();var world=rig.Robot.Vision.World;
        var env=new LatticeEnvironment(Primitives());var planner=new LatticePlanner(env);
        using var runtime=new NativeLatticePlannerRuntime(planner,world,()=>null,(_,_,_,_)=>{},_=>{});
        PriorObstacle(env);world.Maps.Current!.Tree.Root.Content.Data.Last=100;
        using var entered=new ManualResetEventSlim();using var release=new ManualResetEventSlim();
        var hold=Task.Run(()=>{lock(runtime.ContextMutex){entered.Set();release.Wait();}});
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));planner.StartPlanning(world,null,true);
            Assert.Equal(1,env.ObstacleCount);Assert.Equal(0u,Cache(planner));
        }
        finally {release.Set();Assert.True(hold.Wait(TimeSpan.FromSeconds(10)));}
    }
}
