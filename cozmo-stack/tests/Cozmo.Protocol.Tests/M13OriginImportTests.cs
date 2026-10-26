using System.Collections.Concurrent;
using Cozmo.Robot;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class M13OriginImportTests
{
    private static string Obb()
    {
        for(var d=new DirectoryInfo(AppContext.BaseDirectory);d is not null;d=d.Parent)
        {
            var path=Path.Combine(d.FullName,"re-analysis/obb");
            if(MotionPrimitiveSet.FromObb(path) is not null)return path;
        }
        throw new InvalidOperationException("Shipped primitives required");
    }
    private static LatticeEnvironment Env()=>new(MotionPrimitiveSet.FromObb(Obb())!);
    private static readonly P2f[] Square={new(300,300),new(320,300),new(320,320),new(300,320)};
    private static float MinX(LatticeEnvironment env)=>env.Bucket(0)[0].Poly.Points.Min(p=>p.X);

    [Theory]
    [InlineData(null,20f)] [InlineData(uint.MaxValue,20f)] [InlineData(0u,0f)]
    [InlineData(1u,0f)] [InlineData(0x80000000u,0f)] [InlineData(0xFFFFFFFEu,0f)]
    public void RawCarryWordSelectsOriginBeforeCSpace(uint? raw,float shift)
    {
        // reach: unit; explicit raw-state input, not a claim about carrying setter/wire behavior.
        // O6 0x00517F7A/7E/+00517F90: only FFFFFFFF selects +20, other words select literal -0.
        Assert.Equal(shift==20?0x41A00000u:0x80000000u,
            BitConverter.SingleToUInt32Bits(LatticeEnvironment.OriginOffset(raw??uint.MaxValue)));
        var env=Env();Assert.True(env.ImportObstaclePolygons(new[]{Square},false,raw));
        // O10/O11 canonical front bits + padding; source radial expansion of this square is 6/sqrt(2).
        float expected=300f-6f/MathF.Sqrt(2f)-(BitConverter.UInt32BitsToSingle(0x41B0CCCC)+7f+shift);
        Assert.InRange(MathF.Abs(MinX(env)-expected),0f,0.0001f);
        Assert.Equal(1,env.ObstacleCount);Assert.False(env.TightPadding);
        Assert.All(Enumerable.Range(0,env.Primitives.NumAngles),t=>Assert.Equal(0.1f,Assert.Single(env.Bucket(t)).Penalty));
    }

    [Fact]
    public void EveryHeadingSamplesCarryAndBothPolygonRoutesUseOriginPose()
    {
        // reach: unit with explicitly substituted per-heading raw words. No global snapshot of carrying.
        var env=Env();int reads=0;
        Assert.True(env.ImportObstaclePolygons(new[]{Square},false,
            ()=>LatticeCarryingState.FromRaw(reads++==0?uint.MaxValue:0u)));
        Assert.Equal(env.Primitives.NumAngles,reads);
        Assert.True(env.AddPolygonObstacle(Square,123f,(uint?)uint.MaxValue));
        Assert.Equal(2,env.ObstacleCount);Assert.Equal(env.Bucket(0)[0].Poly.Points,env.Bucket(0)[1].Poly.Points);
        Assert.Equal(123f,env.Bucket(0)[1].Penalty);
        var rectangle=Env();using var pose=new Pose3d(Rotation3d.Identity,new Vec3(310,310,0));
        Assert.True(rectangle.AddRectangleObstacle(pose,20,20,uint.MaxValue));
        // These source inputs have different start corners and collinear merge subdivisions. Check the
        // independent axis-rectangle boundary, not an invented equality of their vertex representation.
        float radial=6f/MathF.Sqrt(2f),front=BitConverter.UInt32BitsToSingle(0x41B0CCCC),back=BitConverter.UInt32BitsToSingle(0xC25F999A);
        float minX=(300f-radial)-(20f+(front+7f)),maxX=(320f+radial)-(20f+(back-7f));
        float minY=(300f-radial)-(BitConverter.UInt32BitsToSingle(0x41D8CCCD)+7f);
        float maxY=(320f+radial)-(-BitConverter.UInt32BitsToSingle(0x41D8CCCD)-7f);
        foreach(var geometry in new[]{env,rectangle})
        {
            var points=geometry.Bucket(0)[0].Poly.Points;
            Assert.Equal(minX,points.Min(p=>p.X));Assert.Equal(maxX,points.Max(p=>p.X));
            Assert.Equal(minY,points.Min(p=>p.Y));Assert.Equal(maxY,points.Max(p=>p.Y));
            foreach(var corner in new[]{new P2f(minX,minY),new P2f(minX,maxY),new P2f(maxX,minY),new P2f(maxX,maxY)})Assert.Contains(corner,points);
            Assert.All(points,p=>Assert.True((p.X==minX||p.X==maxX)&&p.Y>=minY&&p.Y<=maxY ||
                (p.Y==minY||p.Y==maxY)&&p.X>=minX&&p.X<=maxX));
        }
        // At heading1, raw0 has no origin shift. Switching just that heading to sentinel moves C-space by -20*(cos,sin).
        var noCarry=Env();Assert.True(noCarry.ImportObstaclePolygons(new[]{Square},false,(uint?)null));
        float theta=env.Primitives.Angles[1];
        float dx=noCarry.Bucket(1)[0].Poly.Points.Average(p=>p.X)-env.Bucket(1)[0].Poly.Points.Average(p=>p.X);
        float dy=noCarry.Bucket(1)[0].Poly.Points.Average(p=>p.Y)-env.Bucket(1)[0].Poly.Points.Average(p=>p.Y);
        Assert.InRange(MathF.Abs(dx+20*MathF.Cos(theta)),0,0.001f);
        Assert.InRange(MathF.Abs(dy+20*MathF.Sin(theta)),0,0.001f);
    }

    [Theory]
    [InlineData(0x3EED6338u,2f,"420A4748 C173FFF1 4108AB21 42133904 C21B17FB C24FAF9D C2819A3D 3E096300")]
    [InlineData(0x3F333333u,7f,"4230E61F C0EAB348 3E940300 42334FB8 C1D12045 C285346F C28C271E C1670AF6")]
    [InlineData(0x40490FDAu,7f,"C1E8CCC9 42086667 C1E8CCCF C2086665 427B999B 42086664 427B9999 C2086668")]
    [InlineData(0x40490FDCu,7f,"C1E8CCCF 42086665 C1E8CCC9 C2086667 427B9999 42086668 427B999B C2086664")]
    [InlineData(0xC0490FDBu,7f,"C1E8CCCF 42086665 C1E8CCC9 C2086667 427B9999 42086668 427B999B C2086664")]
    public void ImportPoseChainMatchesCheckedArmCorpusUnderDeclaredHostLibmModel(uint headingBits,float padding,string expectedWords)
    {
        // reach: numeric unit; independently checked real ARM pose/origin/quad instructions, FPSCR0,
        // Python host cos/sin/atan2/ceil narrowed f32 hooks. These finite comparisons do not certify phone
        // libm or arbitrary .NET hosts. No expected value comes from the changed C# helper.
        // 20261026-cm13-04-read-codex-check.md optional numeric supplement; rawID0, canonical corner order.
        var quad=LatticeEnvironment.ImportRobotQuad(BitConverter.UInt32BitsToSingle(headingBits),padding,0);
        uint[] actual=quad.SelectMany(p=>new[]{BitConverter.SingleToUInt32Bits(p.X),BitConverter.SingleToUInt32Bits(p.Y)}).ToArray();
        uint[] expected=expectedWords.Split(' ').Select(x=>Convert.ToUInt32(x,16)).ToArray();
        Assert.Equal(expected,actual);
    }

    [Theory]
    [InlineData(0u,2f,"C1D8DB24 41D292C7 C1A67AF8 C1FC4B1D 4259A582 420CC653 4272D598 C1B5513F")]
    [InlineData(uint.MaxValue,7f,"C2520140 41E4B311 C2347D3E C21CD94B 421BD5B8 421A2A63 423959BA C1EA10E3")]
    public void LargeHeadingUsesExactlyOneInputRadiansConstruction(uint raw,float padding,string expectedWords)
    {
        // Numeric unit under the independently checked ARM/declared-host-hook model, not phone libm certification.
        // 0x0084C90A..92E returns3.25 directly without another range check; duplicate input construction
        // would yield -3.033185482/yawC0421FB6 rather than source yawC0421FB5. Safe quotient333980.
        float heading=BitConverter.UInt32BitsToSingle(0x4A001476);
        Assert.Equal(0x40500000u,BitConverter.SingleToUInt32Bits(new Radians(heading).Value));
        var quad=LatticeEnvironment.ImportRobotQuad(heading,padding,raw);
        Assert.Equal(expectedWords.Split(' ').Select(x=>Convert.ToUInt32(x,16)),
            quad.SelectMany(p=>new[]{BitConverter.SingleToUInt32Bits(p.X),BitConverter.SingleToUInt32Bits(p.Y)}));
    }

    [Fact]
    public void LegacyPolygonSignaturesExposeMissingInputWithoutMutatingSnapshot()
    {
        // Host API adapter boundary: no implicit no-carry policy when callers omit the source input.
        var env=Env();Assert.True(env.ImportObstaclePolygons(new[]{Square},true,0u));
        var prior=Enumerable.Range(0,env.Primitives.NumAngles).Select(env.Bucket).ToArray();
        using var pose=new Pose3d();var logs=new ConcurrentQueue<string>();void Log(string line)=>logs.Enqueue(line);
        StackDiagnostics.Reported+=Log;
        try
        {
            env.ImportObstaclePolygons(new[]{Square,Square},false);
            env.AddPolygonObstacle(Square,1000f);env.AddRectangleObstacle(pose,20,20,null,1000f); // legacy null name stays unambiguous
            Assert.Equal(3,logs.Count(l=>l.Contains("MISSING: M13-003")&&l.Contains("CarryingOwner")));
            Assert.True(env.TightPadding);Assert.Equal(1,env.ObstacleCount);
            for(int t=0;t<prior.Length;t++)Assert.Same(prior[t],env.Bucket(t));
        }
        finally{StackDiagnostics.Reported-=Log;}
    }

    [Theory] [InlineData(0)] [InlineData(3)]
    public void UnavailableContextNeverPublishesPartialBucketsOrPadding(int failAt)
    {
        // reach: unit; explicit unavailable-provider input, including disappearance during a rebuild.
        var env=Env();Assert.True(env.ImportObstaclePolygons(new[]{Square},false,0u));
        var prior=Enumerable.Range(0,env.Primitives.NumAngles).Select(env.Bucket).ToArray();
        var logs=new ConcurrentQueue<string>();void Log(string line)=>logs.Enqueue(line);
        StackDiagnostics.Reported+=Log;
        try
        {
            int reads=0;
            Assert.False(env.ImportObstaclePolygons(new[]{Square,Square},true,
                ()=>reads++==failAt?default:LatticeCarryingState.FromRaw(null)));
            Assert.Equal(failAt+1,reads);
            Assert.False(env.AddPolygonObstacle(Square,1000f,(Func<LatticeCarryingState>?)null));
            reads=0;Assert.False(env.AddPolygonObstacle(Square,1000f,
                ()=>reads++==failAt?default:LatticeCarryingState.FromRaw(0)));
            using var pose=new Pose3d();reads=0;
            Assert.False(env.AddRectangleObstacleWithCarrying(pose,20,20,
                ()=>reads++==failAt?default:LatticeCarryingState.FromRaw(0)));
            Assert.Equal(1,env.ObstacleCount);Assert.False(env.TightPadding);
            for(int t=0;t<prior.Length;t++)Assert.Same(prior[t],env.Bucket(t));
            Assert.Contains(logs,l=>l.Contains("MISSING: M13-003")&&l.Contains("CarryingOwner"));
        }
        finally{StackDiagnostics.Reported-=Log;}
    }

    [Fact]
    public void CarryChangeDoesNotInvalidateTimestampGateAndUnavailableAttemptKeepsCacheTiming()
    {
        // reach: unit; real map/cache with substituted raw-state/provider inputs.
        using var rig=new M4ControlTests.Rig();rig.ToSynced();var world=rig.Robot.Vision.World;
        var map=world.Maps.Current!;map.Tree.Root.Content=SourceNodeContent.FromShared(new ObservableMapData(7,Square,123));
        var env=Env();var planner=new LatticePlanner(env);
        Assert.True(planner.ImportBlockWorldObstacles(world,null,false));float shifted=MinX(env);
        Assert.False(planner.ImportBlockWorldObstacles(world,0,true));Assert.Equal(shifted,MinX(env));
        Assert.True(planner.ImportBlockWorldObstacles(world,0,false));Assert.InRange(MathF.Abs(MinX(env)-shifted-20),0,0.0001f);
        var prior=env.Bucket(0);map.Tree.Root.Content.Data.Last=124;
        Assert.False(planner.ImportBlockWorldObstaclesWithCarrying(world,()=>default,true));
        Assert.Same(prior,env.Bucket(0));Assert.False(env.TightPadding);
        // Cache stores124 before rebuilding, even though unavailable input skips publication.
        Assert.False(planner.ImportBlockWorldObstacles(world,null,true));Assert.Same(prior,env.Bucket(0));
    }

    [Fact]
    public void GenericFootprintRetainsSuppliedPoseAndCanonicalUnshiftedQuad()
    {
        // O10/O11 generic footprint: no importer-origin shift, no new angle-axis round trip.
        using var pose=new Pose3d(Rotation3d.Identity,new Vec3(100,200,0));
        var quad=ObjectFootprint.RobotBoundingQuadXY(pose,2);
        Assert.Equal(100f+(BitConverter.UInt32BitsToSingle(0x41B0CCCC)+2f),quad[0].X);
        Assert.Equal(200f+(-BitConverter.UInt32BitsToSingle(0x41D8CCCD)-2f),quad[0].Y);
        Assert.Equal(BitConverter.UInt32BitsToSingle(0x41B0CCCC),LatticeEnvironment.RobotQuad(0,0)[0].X);
        // A non-axis supplied quaternion from checked shipped_1 ARM corpus: footprint gets its yaw once,
        // then performs canonical rotation + supplied translation, without an importer-only origin operation.
        using var rotated=new Pose3d(new Rotation3d(BitConverter.Int64BitsToDouble(0x3FEF24DB1248DC1D),0,0,
            BitConverter.Int64BitsToDouble(0x3FCD688B130C8777)),new Vec3(11,23,0));
        var bounds=ObjectFootprint.RobotBoundingQuadXY(rotated,2);
        uint[] words="420A4748 C173FFF1 4108AB21 42133904 C21B17FB C24FAF9D C2819A3D 3E096300".Split(' ').Select(x=>Convert.ToUInt32(x,16)).ToArray();
        for(int i=0;i<4;i++)
        {Assert.Equal(11f+BitConverter.UInt32BitsToSingle(words[2*i]),bounds[i].X);Assert.Equal(23f+BitConverter.UInt32BitsToSingle(words[2*i+1]),bounds[i].Y);}
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void ProductionDispatchUsesCurrentEngineOwnerAndRetainsSnapshotWhenOwnerAbsent(bool absent)
    {
        // reach: production path dispatch/import with manually admitted cube pose (not camera/hardware).
        // Construct Manipulation before EngineRobot, deliberately giving Docking a distinct fallback owner.
        // Keep ALL input poses alive until m.Dispose joins the runtime, including assertion failures.
        using var rig=new M4ControlTests.Rig();
        using var goal=new Pose3d(Rotation3d.Identity,new Vec3(200,100,0));
        using var cubePose=new Pose3d(Rotation3d.Identity,new Vec3(300,0,22));
        var cube=new ObservableObject(77,ObjectType.Block_LIGHTCUBE1,Array.Empty<KnownMarker>());
        try
        {
            using var m=new ManipulationSystem(rig.Robot,rig.Robot.Vision);
            m.Docking.Carrying.SetCarrying(0); // explicit conflicting fallback: zero-shift
            rig.ToSynced();rig.State();Assert.True(m.LoadPlanner(Obb()));
            var owner=rig.Engine.Robot!.CarryingOwner;Assert.NotNull(owner);
            Assert.NotSame(owner,m.Docking.Carrying);Assert.Null(owner.CarriedObjectId);
            cubePose.SetParent(rig.Engine.Robot.GetNativeOrigin(rig.Engine.Robot.CurrentOriginId));
            cube.InitPose(cubePose,PoseState.Known);m.World.AddLocatedObject(cube);
            Assert.True(m.World.Maps.Current!.NativeContentCounts.TryGetValue(MemoryMapContentType.ObstacleObservable,out var cells)&&cells>0);
            var env=m.Planner!.Env;
            env.ImportBlockWorldObstacles(m.World,0,false);var prior=env.Bucket(0);float unshifted=MinX(env);
            var logs=new ConcurrentQueue<string>();void Log(string line)=>logs.Enqueue(line);
            StackDiagnostics.Reported+=Log;
            try
            {
                if(absent)rig.Engine.Robot.CarryingOwner=null; // explicit unavailable-owner fixture
                using var drive=new DriveToPoseAction(m){Goal=goal};Assert.Equal(0,drive.Init());
                Assert.Same(m.NativePlanners!.Lattice,m.NativePlanners.Current);
                Assert.Equal(1,env.ObstacleCount);
                if(absent)
                {Assert.Same(prior,env.Bucket(0));Assert.Contains(logs,l=>l.Contains("MISSING: M13-003")&&l.Contains("CarryingOwner"));}
                else Assert.InRange(MathF.Abs(MinX(env)-unshifted+20),0,0.0001f);
            }
            finally{rig.Engine.Robot.CarryingOwner=owner;StackDiagnostics.Reported-=Log;}
        }
        finally{CubePreActionPoses.ClearForSetPose(cube);cube.NativePose.Dispose();}
    }
}
