using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Cozmo.Transport;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class PoseTreeVerifyBuilderTests
{
    private sealed class Capture : IDisposable
    {
        internal readonly List<(EngineLogLevel Level,string? Channel,string Key,string Text)> Logs=new();
        internal int Flushes;
        private readonly IDisposable _scope;
        internal Capture()=>_scope=Pose3d.UseSourceLog((l,c,k,t)=>Logs.Add((l,c,k,t)),()=>++Flushes);
        internal int Unowned=>Logs.Count(l=>l.Key=="PoseBase.Dev_AssertIsValidParentPointer.UnownedParent");
        public void Dispose()=>_scope.Dispose();
    }
    [Fact]
    public void ParentChecksAreSilentForOwnedAndNullAndReportTheExactChildParentTextOtherwise()
    {
        using var capture=new Capture();using var root=new Pose3d("root");var parent=new Pose3d("parent\0tail",root);
        using var child=new Pose3d("child\0tail",parent);
        Assert.False(child.IsRoot);Assert.True(child.HasParent);Assert.Empty(capture.Logs);
        parent.Dispose();Assert.False(child.IsRoot);Assert.True(child.HasParent);
        Assert.Equal(2,capture.Unowned);Assert.All(capture.Logs,l=>Assert.Equal((EngineLogLevel.Error,(string?)null,"PoseBase.Dev_AssertIsValidParentPointer.UnownedParent","VERIFY(parent->_ownerCount > 0): Pose 0(child) has parent 0(parent) which is not owned by any PoseBase wrapper"),l));
        Assert.Equal(2,capture.Flushes);Assert.True(EngineErrorState.ErrorFlagSet);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void UnequalDepthWalkChecksOldThenNewNodeAndContinuesAfterVerify(bool reverse)
    {
        using var root=new Pose3d("root");var parent=new Pose3d("parent",root);using var mid=new Pose3d("mid",parent);using var leaf=new Pose3d("leaf",mid);using var output=new Pose3d();
        parent.Dispose();using var capture=new Capture();
        Assert.True(reverse?Pose3d.GetWithRespectTo(root,leaf,output):Pose3d.GetWithRespectTo(leaf,root,output));
        // HasSameRootAs: 1; walk a/b: the new mid, then the old mid = 2 more.
        Assert.Equal(3,capture.Unowned);Assert.All(capture.Logs,l=>Assert.Contains("Pose 0(mid) has parent 0(parent)",l.Text));
        Assert.Equal(Vec3.Zero,output.Translation);
    }
    [Fact]
    public void EqualDepthWalkChecksSameParentAndThenToBeforeFrom()
    {
        using var root=new Pose3d("root");var a=new Pose3d("a",root);var b=new Pose3d("b",root);
        using var from=new Pose3d("from",a);using var to=new Pose3d("to",b);using var output=new Pose3d();a.Dispose();b.Dispose();
        using var capture=new Capture();Assert.True(Pose3d.GetWithRespectTo(from,to,output));
        Assert.Equal(new[]{"from","to","to","from","to","from"},capture.Logs.Where(l=>l.Key.EndsWith("UnownedParent")).Select(l=>l.Text.Contains("Pose 0(from)")?"from":"to"));
    }
    [Fact]
    public void FindRootChecksTwicePerNodeStepAndGetParentChecksBeforeWrapping()
    {
        using var root=new Pose3d("root");var parent=new Pose3d("parent",root);var mid=new Pose3d("mid",parent);using var leaf=new Pose3d("leaf",mid);
        parent.Dispose();mid.Dispose();using var capture=new Capture();
        using var found=leaf.FindRoot();Assert.Same(root.Node,found.Node);Assert.Equal(3,capture.Unowned);
        capture.Logs.Clear();var output=new Pose3d();
        try{Assert.True(leaf.TryGetParent(ref output));Assert.Same(leaf.Node!.Parent,output.Node);Assert.Equal(1,capture.Unowned);}
        finally{output.Dispose();}
    }
    [Fact]
    public void SameParentChecksIdentityThenLeftParentThenRightParent()
    {
        var root=new PoseTreeNode(new(Rotation3d.Identity,Vec3.Zero),"root",null);
        var left=new PoseTreeNode(new(Rotation3d.Identity,Vec3.Zero),"left",root);
        var right=new PoseTreeNode(new(Rotation3d.Identity,Vec3.Zero),"right",root);
        using var capture=new Capture();Assert.True(left.HasSameParent(left));Assert.Empty(capture.Logs);
        Assert.False(root.HasSameParent(right));Assert.Empty(capture.Logs);
        Assert.True(left.HasSameParent(right));Assert.Equal(2,capture.Unowned);
        Assert.Contains("Pose 0(left)",capture.Logs[0].Text);Assert.Contains("Pose 0(right)",capture.Logs[1].Text);
    }
    [Fact]
    public void OwnedParentsStaySilentAcrossBothDepthDirectionsAndEqualDepthWalk()
    {
        using var root=new Pose3d("root");using var a=new Pose3d("a",root);using var b=new Pose3d("b",root);
        using var left=new Pose3d("left",a);using var right=new Pose3d("right",b);using var output=new Pose3d();using var capture=new Capture();
        Assert.True(Pose3d.GetWithRespectTo(left,root,output));Assert.True(Pose3d.GetWithRespectTo(root,right,output));
        Assert.True(Pose3d.GetWithRespectTo(left,right,output));using var found=left.FindRoot();
        Assert.Same(root.Node,found.Node);Assert.Empty(capture.Logs);
    }
    [Fact]
    public void RemoveOwnerHasTwoSeparateVerifiesAndNeverUnderflows()
    {
        using var capture=new Capture();var pose=new Pose3d("origin"){Id=uint.MaxValue};var node=pose.Node!;
        pose.Dispose();Assert.Equal(0u,node.Owners);
        Assert.Equal("PoseBase.PoseTreeNode.RemoveOwner.ZeroOwnersWithID",capture.Logs[0].Key);
        Assert.Equal("VERIFY(GetID() == 0): Pose origin has non-zero ID=-1 but just had its last owner removed",capture.Logs[0].Text);
        node.RemoveOwner();Assert.Equal(0u,node.Owners);Assert.Equal("PoseBase.PoseTreeNode.RemoveOwner.ZeroOwners",capture.Logs[1].Key);
        Assert.Equal("VERIFY(_ownerCount > 0): ",capture.Logs[1].Text);
    }
    [Fact]
    public void CopiesAndWrappedNodesUseTheCheckedOwnerCountRules()
    {
        using var source=new Pose3d("source");source.Node!.Owners=7;
        using var copy=new Pose3d(source);Assert.Equal(1u,copy.Owners);Assert.Equal(7u,source.Owners);
        using(var wrapped=Pose3d.WrapExisting(source.Node)){Assert.Equal(8u,source.Owners);copy.Assign(source);Assert.Equal(1u,copy.Owners);}
        Assert.Equal(7u,source.Owners);
        var nullSource=new Pose3d();nullSource.Dispose();var node=copy.Node!;copy.Assign(nullSource);Assert.Equal(0u,node.Owners);Assert.Null(copy.Node);
    }
    [Fact]
    public void Exactly1001NodesPassTheGateLogBothRootCapsAndDepthAndReturnTheTransform()
    {
        var poses=new List<Pose3d>{new("root")};
        for(int i=1;i<=1000;++i)poses.Add(new Pose3d(Rotation3d.Identity,new Vec3(1,0,0),"n",poses[^1]));
        using var output=new Pose3d();using var capture=new Capture();
        try
        {
            Assert.True(PoseTreeNode.HasSameRoot(poses[^1].Node,poses[^1].Node));
            Assert.Equal(new[]{"../../../../coretech/common/include/anki/common/basestation/math/poseTreeNode.h:263","../../../../coretech/common/include/anki/common/basestation/math/poseTreeNode.h:270"},capture.Logs.Select(l=>l.Text));
            capture.Logs.Clear();Assert.True(Pose3d.GetWithRespectTo(poses[^1],poses[0],output));Assert.Equal(new Vec3(1000,0,0),output.Translation);
            Assert.Contains(capture.Logs,l=>l.Key=="LoopBoundOverflow"&&l.Text.EndsWith("poseTreeNode.h:263"));
            Assert.Contains(capture.Logs,l=>l.Key=="LoopBoundOverflow"&&l.Text.EndsWith("poseTreeNode.h:393"));
            Assert.DoesNotContain(capture.Logs,l=>l.Text.Contains("poseBase_impl.h"));Assert.Equal(0,capture.Unowned);
        }
        finally{for(int i=poses.Count-1;i>=0;--i)poses[i].Dispose();}
    }
    [Fact]
    public void LiveOriginListDestructionSuppressesIdVerifyAndRestoresTheFlag()
    {
        using var robot=CozmoRobot.CreateOffline();var engine=robot.Engine;engine.Robots.AddRobot(CozmoEngine.RobotId);
        var native=engine.Robot!;var root=native.GetNativeOrigin(native.CurrentOriginId);var node=root.Node!;
        var logs=new List<EngineLogEntry>();engine.LogEntry+=logs.Add;
        engine.Robots.RemoveRobots();
        Assert.Equal(0u,node.Owners);Assert.False(Pose3d.UnownedParentsAllowed);
        Assert.DoesNotContain(logs,l=>l.Name=="PoseBase.PoseTreeNode.RemoveOwner.ZeroOwnersWithID");
    }
}
