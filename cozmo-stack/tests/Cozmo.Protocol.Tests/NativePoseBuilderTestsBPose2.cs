using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
using Cozmo.Transport;
using Xunit;
namespace Cozmo.Protocol.Tests;

/// <summary>
/// B-POSE-2 (research/20261014-b-pose-1-check.md items 1, 2, 5, 6, 10, 11). Expected values come from the rows in
/// research/20261014-pose-rows-recheck.md (R2, R21, R22, R28, R35-R38, the LoopBoundOverflow table) and
/// research/20261013-rodrigues-and-pose-tree.md, worked by hand in the comments; none is read back from the code under test.
/// </summary>
[Collection("M5 process statics")]
public sealed class NativePoseBuilderTestsBPose2
{
    private const string MathDir="../../../../coretech/common/include/anki/common/basestation/math/";
    private static int Bits(float v)=>BitConverter.SingleToInt32Bits(v);
    private static List<(EngineLogLevel Level,string Key,string Text)> Capture(out IDisposable scope,Action? flush=null)
    {var entries=new List<(EngineLogLevel,string,string)>();scope=Pose3d.UseSourceLog((level,_,key,text)=>entries.Add((level,key,text)),flush);return entries;}

    /// <summary>M11-066 R36 (a), 0x00845AB8..0x00845AE6: the exit test runs after a step on the new f's parent, so from.parent == to
    /// composes Tt into Tf, exits nothing, and (d) gives Tt^-1*(Tt*Tf). Expected: the row's sequence on copies of the two transforms
    /// (PreComposeWith R26, GetInverse R27, *= R24); it is not bit-equal to Tf.</summary>
    [Fact]
    public void M11_066_WalkA_FromParentEqualsToGoesThroughCombineNotAnEarlyExit()
    {
        using var root=new Pose3d("root");
        using var to=new Pose3d(Rotation3d.FromAngleAxis(0.3f,new Vec3(0,0,1)),new Vec3(100.7,-3.3,2.1),"to",root);
        using var from=new Pose3d(Rotation3d.FromAngleAxis(-1.1f,new Vec3(0,1,0)),new Vec3(1.1,2.2,3.3),"from",to);
        using var output=new Pose3d();
        Assert.True(Pose3d.GetWithRespectTo(from,to,output));
        var tf=from.Transform;var tt=to.Transform;tf.PreComposeWith(ref tt);   // (a) step 1
        var expected=to.Transform.Inverse();expected.MultiplyBy(in tf);         // (d)
        Assert.Equal(expected.Rotation,output.Quaternion);Assert.Equal(expected.Translation,output.Translation);
        Assert.False(output.Quaternion==from.Quaternion&&output.Translation==from.Translation);
        Assert.Same(to.Node,output.Node!.Parent);                                // (e) SetParent(out, to)
    }

    /// <summary>M11-066 R36 (b), 0x00845B42..0x00845B78: to deeper by 2; one step of Tt, then t.parent == f exits with Tt inverted (R27).</summary>
    [Fact]
    public void M11_066_WalkB_ExitInvertsTheWalkedToTransform()
    {
        using var root=new Pose3d("root");
        using var p=new Pose3d(Rotation3d.FromAngleAxis(0.7f,new Vec3(0,0,1)),new Vec3(10.1,20.2,0.5),"p",root);
        using var f=new Pose3d(Rotation3d.FromAngleAxis(0.2f,new Vec3(1,0,0)),new Vec3(-4.4,1.5,9.75),"f",p);
        using var output=new Pose3d();
        Assert.True(Pose3d.GetWithRespectTo(root,f,output));
        var tt=f.Transform;var tp=p.Transform;tt.PreComposeWith(ref tp);var expected=tt.Inverse();
        Assert.Equal(expected.Rotation,output.Quaternion);Assert.Equal(expected.Translation,output.Translation);
        Assert.Same(f.Node,output.Node!.Parent);
    }

    /// <summary>M11-066 R36 (c), 0x00845BD4..0x00845C16: equal depths; both step once, then HasSameParentAs ends the walk and (d) gives Tt^-1*Tf.</summary>
    [Fact]
    public void M11_066_WalkC_BothStepUntilTheyShareAParent()
    {
        using var root=new Pose3d("root");
        using var a1=new Pose3d(Rotation3d.FromAngleAxis(0.4f,new Vec3(0,0,1)),new Vec3(30,5,0),"a1",root);
        using var a2=new Pose3d(Rotation3d.FromAngleAxis(-0.9f,new Vec3(0,1,0)),new Vec3(1,2,3),"a2",a1);
        using var b1=new Pose3d(Rotation3d.FromAngleAxis(-2.5f,new Vec3(0,0,1)),new Vec3(-7,40,1),"b1",root);
        using var b2=new Pose3d(Rotation3d.FromAngleAxis(1.3f,new Vec3(1,0,0)),new Vec3(0.25,-6,11),"b2",b1);
        using var output=new Pose3d();
        Assert.True(Pose3d.GetWithRespectTo(a2,b2,output));
        var tf=a2.Transform;var ta1=a1.Transform;tf.PreComposeWith(ref ta1);
        var tt=b2.Transform;var tb1=b1.Transform;tt.PreComposeWith(ref tb1);
        var expected=tt.Inverse();expected.MultiplyBy(in tf);
        Assert.Equal(expected.Rotation,output.Quaternion);Assert.Equal(expected.Translation,output.Translation);
        Assert.Same(b2.Node,output.Node!.Parent);
    }

    /// <summary>LoopBoundOverflow table (manager, 2026-10-14): PoseTreeNode::FindRoot poseTreeNode.h 0x145 (325),
    /// HasSameRootAs 0x107 (263) and 0x10E (270), GetTreeDepth 0x189 (393). Each is an Error "%s:%d" with the full path, and sets the error flag.</summary>
    [Fact]
    public void M11_066_TreeLoopBoundOverflowsLogTheirFileAndLine()
    {
        var entries=Capture(out var scope);using var _=scope;bool prior=EngineErrorState.ErrorFlagSet;
        try
        {
            using var c=new Pose3d("c");c.SetParent(c);
            EngineErrorState.ErrorFlagSet=false;using var r=c.FindRoot();
            Assert.Contains(entries,e=>e.Level==EngineLogLevel.Error&&e.Key=="LoopBoundOverflow"&&e.Text==MathDir+"poseTreeNode.h:325");Assert.True(EngineErrorState.ErrorFlagSet);
            using var x=new Pose3d(Rotation3d.Identity,new Vec3(1,0,0),"x",c);using var output=new Pose3d();
            entries.Clear();EngineErrorState.ErrorFlagSet=false;
            Assert.True(Pose3d.GetWithRespectTo(x,c,output));   // both walks end at c after 1000 steps; c and x share the parent c
            Assert.Contains(entries,e=>e.Key=="LoopBoundOverflow"&&e.Text==MathDir+"poseTreeNode.h:263");
            Assert.Contains(entries,e=>e.Key=="LoopBoundOverflow"&&e.Text==MathDir+"poseTreeNode.h:270");
            Assert.Contains(entries,e=>e.Key=="LoopBoundOverflow"&&e.Text==MathDir+"poseTreeNode.h:393");
            Assert.True(EngineErrorState.ErrorFlagSet);
        }
        finally{EngineErrorState.ErrorFlagSet=prior;}
    }

    /// <summary>M11-066 R35 step 1 (0x00845A34..0x00845A68): a null node → VERIFY PoseBase.GetWithRespectTo.NullInputPose
    /// "... FromNull:%s ToNull:%s WrtOtherNull:%s" (Y/N), the error flag, ForceFlush, and false.</summary>
    [Fact]
    public void M11_066_NullInputPoseVerifiesAndReturnsFalse()
    {
        int flushes=0;var entries=Capture(out var scope,()=>flushes++);using var _=scope;bool prior=EngineErrorState.ErrorFlagSet;
        try
        {
            EngineErrorState.ErrorFlagSet=false;
            var empty=new Pose3d();empty.Dispose();using var to=new Pose3d("to");using var output=new Pose3d();
            Assert.False(Pose3d.GetWithRespectTo(empty,to,output));
            var v=Assert.Single(entries,e=>e.Key=="PoseBase.GetWithRespectTo.NullInputPose");
            Assert.Equal(EngineLogLevel.Error,v.Level);Assert.StartsWith("VERIFY(",v.Text);Assert.EndsWith("FromNull:Y ToNull:N WrtOtherNull:N",v.Text);
            Assert.True(EngineErrorState.ErrorFlagSet);Assert.Equal(1,flushes);
        }
        finally{EngineErrorState.ErrorFlagSet=prior;}
    }

    /// <summary>M11-065/M11-066 R38 and item 10 (0x00846F7E cbz): IsSameAs on a pose whose node is null calls GetWithRespectTo(P, this),
    /// whose NullInputPose VERIFY returns false; then the PosesHaveDifferentOrigins warning; false, no throw.</summary>
    [Fact]
    public void M11_066_IsSameAsOnANullNodeTakesTheDefinedFalsePath()
    {
        var entries=Capture(out var scope,()=>{});using var _=scope;bool prior=EngineErrorState.ErrorFlagSet;
        try
        {
            var empty=new Pose3d();empty.Dispose();using var other=new Pose3d("other");
            Assert.False(empty.IsSameAs(other,5,0.1));
            var v=Assert.Single(entries,e=>e.Key=="PoseBase.GetWithRespectTo.NullInputPose");Assert.EndsWith("FromNull:N ToNull:Y WrtOtherNull:N",v.Text);
            var w=Assert.Single(entries,e=>e.Key=="Pose3d.IsSameAs.PosesHaveDifferentOrigins");
            Assert.Equal(EngineLogLevel.Warning,w.Level);Assert.Equal("Could not get other pose w.r.t. this pose. Returning not same.",w.Text);
            Assert.True(entries.FindIndex(e=>e.Key=="PoseBase.GetWithRespectTo.NullInputPose")<entries.FindIndex(e=>e.Key=="Pose3d.IsSameAs.PosesHaveDifferentOrigins"));
        }
        finally{EngineErrorState.ErrorFlagSet=prior;}
    }

    /// <summary>M11-066 R37 (0x004DF628..0x004DF6DC): a 2-cycle a↔b. FindRoot stops after 1000 steps at b; HasSameRootAs walks a to a and b to b,
    /// so GetWithRespectTo is false: Error Pose3d.GetWithRespectToRoot.Failed, the error flag, and the fresh Pose3d("") is returned.</summary>
    [Fact]
    public void M11_066_GetWithRespectToRootFailureLogsTheError()
    {
        var entries=Capture(out var scope);using var _=scope;bool prior=EngineErrorState.ErrorFlagSet;
        try
        {
            using var a=new Pose3d(Rotation3d.Identity,new Vec3(5,0,0),"a");using var b=new Pose3d(Rotation3d.Identity,new Vec3(0,6,0),"b");
            a.SetParent(b);b.SetParent(a);EngineErrorState.ErrorFlagSet=false;
            using var output=a.GetWithRespectToRoot();
            var e=Assert.Single(entries,x=>x.Key=="Pose3d.GetWithRespectToRoot.Failed");
            Assert.Equal(EngineLogLevel.Error,e.Level);Assert.Equal("Could not get pose w.r.t. its own root. This should never happen.",e.Text);
            Assert.True(EngineErrorState.ErrorFlagSet);
            Assert.Equal(Rotation3d.Identity,output.Quaternion);Assert.Equal(Vec3.Zero,output.Translation);Assert.Equal("",output.Name);
        }
        finally{EngineErrorState.ErrorFlagSet=prior;}
    }

    /// <summary>M11-067 cv::Rodrigues both directions at 180 degrees (rodrigues-and-pose-tree.md, with the recheck's step 6 corrections).
    /// V→M of (pi_f,0,0): c = cos(pi_f) ≈ -1+3.8e-15, s = sin(pi_f) = -8.742278e-8, so R0 = 1, R4 = R8 = -1, R5 = -s = 0x33BBBD2E,
    /// R7 = s = 0xB3BBBD2E, the rest 0. M→V: every Jacobi pair has p = 0 (the columns are exactly orthogonal), W1 = W2 = sqrt(1+s^2) > 1,
    /// so c = -1/W is above -1 and s < 1e-5: the 180-degree branch. theta = acos(-1/W) ≈ pi - 8.7e-8 narrows to 0x40490FDA (not pi_f);
    /// ry = sqrt((R4+1)/2)·sign(R1) and rz = sqrt((R8+1)/2)·sign(R2) with sign(±0) = +1, so both are equal and positive (≈1.4e-7).</summary>
    [Fact]
    public void M11_067_RodriguesRoundTripAtAHalfTurnTakesThe180Branch()
    {
        var m=NativeRodrigues.VectorToMatrix(new Vec3(Radians.Pi,0,0));
        Assert.Equal(1.0,m[0,0]);Assert.Equal(-1.0,m[1,1]);Assert.Equal(-1.0,m[2,2]);
        Assert.Equal(0x33BBBD2E,Bits((float)m[1,2]));Assert.Equal(unchecked((int)0xB3BBBD2E),Bits((float)m[2,1]));
        Assert.Equal(0.0,m[0,1]);Assert.Equal(0.0,m[0,2]);Assert.Equal(0.0,m[1,0]);Assert.Equal(0.0,m[2,0]);
        var v=NativeRodrigues.MatrixToVector(m);
        Assert.Equal(0x40490FDA,Bits((float)v.X));
        Assert.Equal((float)v.Y,(float)v.Z);Assert.InRange((float)v.Y,1.3e-7f,1.5e-7f);
    }

    /// <summary>R2 IsNear (0x0084CC0E..0x0084CC1E): a's copy is rescaled with the flag forced on. a = 1e6 (flag off), b = 0.3:
    /// rescale(1e6) = 1e6 - (float)159155*2pi_f = 1e6 - 1000000.375 = -0.375; d = -0.375 - 0.3f = -0.675 (in range).
    /// Without the forced rescale, 1e6 - 0.3f = 999999.6875 rescales to -0.6875. So tolerance 0.68 is near, 0.67 is not.</summary>
    [Fact]
    public void M11_065_IsNearRescalesAUnflaggedLeftOperand()
    {
        var a=new Radians(1e6f,false);var b=new Radians(0.3f);
        Assert.True(Radians.IsNear(a,b,new Radians(0.68f)));
        Assert.False(Radians.IsNear(a,b,new Radians(0.67f)));
    }

    /// <summary>R2 operator-(float, Radians) (0x0084CA66..0x0084CA96): Radians(a) is built and rescaled first. 1e6 - Radians(0.3):
    /// -0.375 - 0.3f (f32), not rescale(1e6 - 0.3f) = -0.6875.</summary>
    [Fact]
    public void M11_065_FloatMinusRadiansRescalesTheFloatFirst()
    {
        float expected=-0.375f-0.3f;
        var r=1e6f-new Radians(0.3f);
        Assert.Equal(Bits(expected),Bits(r.Value));Assert.True(r.RescaleEnabled);
    }

    /// <summary>M11-003 GetCameraPose (0x00510FFC..0x00511064) with |a| &lt; 1e-5: R21 keeps the unit Y axis (no warning), R22 takes the
    /// Z axis because the angle IsNear 0, so r = (1, -0, -0, -2.5e-6f) after R3/R4 (cos(-2.5e-6f) = 1, sin = -2.5e-6f; f32 w = 1).
    /// R28: t = r·(17.52, 0, 17.52) by R8: x' = 17.52f, y' = m10·px with m10 = 2wz = -5e-6f, z' = 17.52f. A Y-axis rotation would leave y' = 0.</summary>
    [Fact]
    public void M11_003_GetCameraPoseNearZeroUsesTheZAxisSubstitution()
    {
        var entries=Capture(out var scope);using var _=scope;
        float a=5e-6f;
        using var camera=HeadGeometry.GetCameraPose(a);
        float wz=1f*-2.5e-6f;float m10=wz+wz;float px=(float)17.52;float y=m10*px;
        Assert.Equal(Bits(px),Bits((float)camera.Translation.X));
        Assert.Equal(Bits(y),Bits((float)camera.Translation.Y));Assert.NotEqual(0f,y);
        Assert.Equal(Bits(px),Bits((float)camera.Translation.Z));
        Assert.Equal("Camera",camera.Name);
        Assert.DoesNotContain(entries,e=>e.Key=="RotationVector3d.Constructor.AxisNotUnitLength");
        // The live helper flattens the identity-rotation neck (-13, 0, 49) with it: f32 sums.
        using var inRobot=HeadGeometry.CameraPoseInRobot(a);
        Assert.Equal(Bits(-13f+px),Bits((float)inRobot.Translation.X));Assert.Equal(Bits(0f+y),Bits((float)inRobot.Translation.Y));Assert.Equal(Bits(49f+px),Bits((float)inRobot.Translation.Z));
    }

    /// <summary>M12-001/M12-021 (verifier read, B-POSE-2 follow-up): the guarded static sb table built at 0x004E5868..0x004E58E4 holds
    /// RotationVector3d(Radians(float), Y_AXIS_3D) with words 0x00000000, 0x3FC90FDB (0x004E5882/0x004E5888), 0x40490FDB
    /// (0x004E58A4/0x004E58AA), 0xBFC90FDB (0x004E58C6/0x004E58CC); sb3 is not (float)(3pi/2)-2pi_f = 0xBFC90FDC.
    /// RotateBy (0x004E5A1C.., 0x0084BB3A) goes R28 -> R22 -> R3: q = Normalize(cosf(h), sinf(h)*0, sinf(h)*1, sinf(h)*0) with
    /// h = word*0.5f, then on an identity pose R6 with (1,0,0,0) and Normalize again (R4). No Rodrigues round trip.</summary>
    [Fact]
    public void M12_001_SbTableUsesTheEngineRadiansWordsAndRotatesThroughRotationVector3d()
    {
        Assert.Equal(new[]{0x00000000,0x3FC90FDB,0x40490FDB,unchecked((int)0xBFC90FDB)},Cozmo.Robot.Manipulation.CubePreActionPoses.SbRadiansBits);
        Assert.Equal(unchecked((int)0xBFC90FDB),Bits(Cozmo.Robot.Manipulation.CubePreActionPoses.SbRotations[3].Angle.Value));
        Assert.Equal(new Vec3(0,1,0),Cozmo.Robot.Manipulation.CubePreActionPoses.SbRotations[3].Axis);
        static (double W,double X,double Y,double Z) Norm((double W,double X,double Y,double Z) q)
        {
            double n=q.W*q.W;n+=q.X*q.X;n+=q.Y*q.Y;n+=q.Z*q.Z;
            if(Math.Abs(1-n)<BitConverter.Int64BitsToDouble(0x3E56A09E19D672B6)){double k=2/(n+1);return(k*q.W,k*q.X,k*q.Y,k*q.Z);}
            double l=Math.Sqrt(n);return(q.W/l,q.X/l,q.Y/l,q.Z/l);
        }
        for(int sb=1;sb<4;sb++)
        {
            float h=BitConverter.Int32BitsToSingle(Cozmo.Robot.Manipulation.CubePreActionPoses.SbRadiansBits[sb])*0.5f;
            float c=MathF.Cos(h),sn=MathF.Sin(h);
            var q=Norm(Norm((c,sn*0f,sn*1f,sn*0f)));
            using var pose=new Pose3d("p");
            using var rotated=Cozmo.Robot.Manipulation.CubePreActionPoses.RotateBy(pose,sb);
            Assert.Equal(new Rotation3d(q.W,q.X,q.Y,q.Z),rotated.Quaternion);
        }
    }

    /// <summary>Item 1: PoseDiagnostics.Missing goes to the stack's MISSING channel (SteppedBehavior.ReportMissing, deduplicated), never
    /// into the engine source sink the engine binds in TickAt (CozmoEngine.cs). B-POSE-3 builds the conditional parent VERIFY separately.</summary>
    [Fact]
    public void PoseMissingNeverEntersTheEngineSourceLog()
    {
        var entries=Capture(out var scope);using var _=scope;
        var stack=new List<string>();void On(string l){lock(stack)stack.Add(l);}
        SteppedBehavior.ResetMissingForTests();SteppedBehavior.MissingReported+=On;
        try
        {
            using(var p=new Pose3d("p")){}                                             // Remaining destruction gaps report MISSING.
            using(var q=new Pose3d("q")){}
        }
        finally{SteppedBehavior.MissingReported-=On;}
        Assert.DoesNotContain(entries,e=>e.Key=="MISSING"||e.Text.StartsWith("MISSING"));
        Assert.DoesNotContain(stack,l=>l.StartsWith("MISSING: M11-067 Dev_AssertIsValidParentPointer"));
        Assert.Single(stack,l=>l.StartsWith("MISSING: M11-077"));
    }
}
