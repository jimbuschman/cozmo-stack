using System.Reflection;
using System.Text.Json;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Cozmo.Transport;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class M11SmallReadsBuilderTests
{
    private static string Resources()=>Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../../re-analysis/obb/assets/cozmo_resources"));
    private sealed class Fixture:IDisposable
    {
        internal readonly string Root=Directory.CreateTempSubdirectory("b112-").FullName;
        internal readonly List<string> Order=new();internal readonly M4ControlTests.Rig Rig;
        internal EngineRobot Native=null!;
        internal Fixture(bool hasInterface=true,bool missingFace=false)
        {
            var map=ReactionTriggerMap.Load(Resources()).Where(e=>e.Trigger is ReactionTrigger.FacePositionUpdated or ReactionTrigger.ObjectPositionUpdated or ReactionTrigger.CubeMoved).ToArray();
            Assert.Equal(3,map.Length);
            var configs=BehaviorConfigLoader.LoadResources(Resources(),null,null);
            string dir=Path.Combine(Root,"config/engine/behaviorSystem/behaviors");Directory.CreateDirectory(dir);
            foreach(string name in map.Select(e=>e.BehaviorId).Append("CantHandleTallStack"))
            {
                if(missingFace&&name==map[0].BehaviorId)continue;
                var id=Enum.Parse<BehaviorID>(name);
                File.WriteAllText(Path.Combine(dir,name+".json"),configs[id].GetRawText());
            }
            string reaction=Path.Combine(Root,"config/engine/behaviorSystem/reactionTrigger_behavior_map.json");
            File.WriteAllText(reaction,JsonSerializer.Serialize(new{reactionTriggerBehaviorMap=map.Select(e=>new{reactionTrigger=e.Trigger.ToString(),behaviorID=e.BehaviorId})}));
            var port=new M4ControlTests.FakePort();
            // Attach observations before the native Robot constructor is reached by the connect request.
            Rig=new(engineOptions:new CozmoEngineOptions{ResourcesPath=Root});
            Rig.Engine.EngineToGameMessage+=(tag,_)=>{if(tag==0x32)Order.Add("game");};
            Rig.Engine.DelocalizationRecipientObservedForTest=Order.Add;
            Rig.Engine.BeforeConstructorDelocalizeForTest=r=>
            {
                Native=r;r.HasExternalInterface=hasInterface;r.BlockConfigurationForceUpdate=false;
                Assert.Equal(2,r.TallStackContainers.Length);
                foreach(var state in r.TallStackContainers){state.HasStoredPose=true;state.Stack=new StackOfCubes(new uint[]{1,2,3});}
            };
            Rig.ToSynced();
        }
        public void Dispose(){Rig.Dispose();Directory.Delete(Root,true);}
    }
    private static readonly string[] ConstructorOrder={"BlockConfigurationManager","CantHandleTallStack[0]","CantHandleTallStack[1]","CubeLightComponent","game"};
    // reach: unit (direct later Delocalize and explicit app drain; constructor part enters the real connect tick).
    [Fact]
    public void ConstructionHasOnlyR1ThroughR5AndLaterDelocalizationAddsStrategiesInShippedOrder()
    {
        using var f=new Fixture();Assert.Equal(ConstructorOrder,f.Order);Assert.True(f.Native.BlockConfigurationForceUpdate);
        Assert.All(f.Native.TallStackContainers,s=>{Assert.False(s.HasStoredPose);Assert.Null(s.Stack);});
        f.Order.Clear();f.Native.BlockConfigurationForceUpdate=false;
        foreach(var state in f.Native.TallStackContainers){state.HasStoredPose=true;state.Stack=new StackOfCubes(new uint[]{1,2,3});}
        f.Native.Delocalize(false);f.Rig.Engine.DrainAppDelivery();
        Assert.Equal(ConstructorOrder.SkipLast(1).Concat(new[]{"FacePositionUpdated","ObjectPositionUpdated","CubeMoved","game"}),f.Order);
        Assert.True(f.Native.BlockConfigurationForceUpdate);Assert.All(f.Native.TallStackContainers,s=>{Assert.False(s.HasStoredPose);Assert.Null(s.Stack);});
    }
    // reach: unit (direct later Delocalize and explicit app drain; constructor part enters the real connect tick).
    [Fact]
    public void MissingTargetDeletesTheCorrespondingStrategyAndNoInterfaceRegistersNothing()
    {
        using(var missing=new Fixture(missingFace:true))
        {
            missing.Order.Clear();missing.Native.Delocalize(false);missing.Rig.Engine.DrainAppDelivery();
            Assert.Equal(ConstructorOrder.SkipLast(1).Concat(new[]{"ObjectPositionUpdated","CubeMoved","game"}),missing.Order);
        }
        using var noInterface=new Fixture(hasInterface:false);
        Assert.Empty(noInterface.Order);Assert.Equal(0,noInterface.Rig.Engine.AnimationMessages.Count(0x32,true));
        noInterface.Native.Delocalize(false);Assert.Empty(noInterface.Order);Assert.False(noInterface.Native.BlockConfigurationForceUpdate);
        Assert.All(noInterface.Native.TallStackContainers,s=>{Assert.True(s.HasStoredPose);Assert.NotNull(s.Stack);});
    }
    // reach: unit (robot-owned vision instance with direct frame/helper or fixture inputs; full native producer is not claimed).
    [Fact]
    public void ConfigurationFacadeReadsTheSameNativeDirtyFlagAndRobotRemovalRetiresItsRecipients()
    {
        using var f=new Fixture();var vision=f.Rig.Robot.Vision; vision.Calibration=CameraCalibration.Nominal(); vision.Enabled=false;using var m=new ManipulationSystem(f.Rig.Robot,vision);
        Assert.True(m.Configurations.ForceUpdate);m.Configurations.ForceUpdate=false;Assert.False(f.Native.BlockConfigurationForceUpdate);
        f.Order.Clear();f.Native.Delocalize(false);Assert.True(m.Configurations.ForceUpdate);
        f.Rig.Engine.Robots.RemoveRobot(1,false);Assert.Equal(0,f.Rig.Engine.AnimationMessages.Count(0x32,true));
    }
    // reach: unit (fixture creates native container; Freeplay reuses the engine Needs owner).
    [Fact]
    public void FreeplayManagerTallStackFacadeUsesTheClearedNativeContainerFields()
    {
        using var f=new Fixture();var vision=f.Rig.Robot.Vision; vision.Calibration=CameraCalibration.Nominal(); vision.Enabled=false;using var m=new ManipulationSystem(f.Rig.Robot,vision);
        string obb=Path.GetFullPath(Path.Combine(Resources(),"../.."));
        var context=new BehaviorContext{Robot=f.Rig.Robot,Triggers=AnimationTriggerMap.Load(Resources())};
        using var stack=FreeplayStack.Create(obb,f.Rig.Robot,context,()=>0,vision,m,withReactions:false);
        var behavior=Assert.IsType<CantHandleTallStackBehavior>(stack.Bound["CantHandleTallStack"]);
        var target=new StackOfCubes(new uint[]{1,2,3});f.Native.TallStackContainers[0].Stack=target;f.Native.TallStackContainers[0].HasStoredPose=true;
        Assert.Same(target,behavior.TargetStack);
        f.Native.Delocalize(false);Assert.Null(behavior.TargetStack);Assert.False(f.Native.TallStackContainers[0].HasStoredPose);
    }
    [Fact]
    public void RootGetParentSkipsAndPreservesThePreviousOutputAndPublicFindRootIsSafe()
    {
        bool old=EngineErrorState.ErrorFlagSet;
        using var root=new Pose3d("root");using var previous=new Pose3d("previous");Pose3d output=previous;var missing=new List<string>();
        void Observe(string s)=>missing.Add(s);SteppedBehavior.ResetMissingForTests();SteppedBehavior.MissingReported+=Observe;
        try
        {
            Assert.False(root.TryGetParent(ref output));Assert.Same(previous,output);
            Assert.Contains(missing,s=>s.Contains("M1-034/M11-076")&&s.Contains("0x008452BA"));
            using var found=root.FindRoot();Assert.Same(root.Node,found.Node);
            using var flat=root.GetWithRespectToRoot();Assert.Equal(root.Transform,flat.Transform);Assert.True(flat.IsRoot);
        }
        finally{SteppedBehavior.MissingReported-=Observe;EngineErrorState.ErrorFlagSet=old;}
    }
    [Fact]
    public void FindRootStopsAfterOneThousandStepsAndFlattenFailureReturnsAFreshRoot()
    {
        bool old=EngineErrorState.ErrorFlagSet;var logs=new List<(string Key,string Text)>();
        using var scope=Pose3d.UseSourceLog((_,_,k,t)=>logs.Add((k,t)));
        var poses=new List<Pose3d>{new("root")};
        try
        {
            for(int i=1;i<=1002;i++)poses.Add(new Pose3d("n",poses[^1]));
            using(var found=poses[^1].FindRoot())Assert.Same(poses[1].Node,found.Node);
            Assert.Contains(logs,l=>l.Key=="LoopBoundOverflow"&&l.Text.EndsWith("poseTreeNode.h:325"));
            logs.Clear();using var flat=poses[^1].GetWithRespectToRoot();
            Assert.True(flat.IsRoot);Assert.Equal(Vec3.Zero,flat.Translation);Assert.NotSame(poses[0].Node,flat.Node);
            Assert.Contains(logs,l=>l.Key=="Pose3d.GetWithRespectToRoot.Failed"&&l.Text=="Could not get pose w.r.t. its own root. This should never happen.");
            Assert.True(EngineErrorState.ErrorFlagSet);
        }
        finally{for(int i=poses.Count-1;i>=0;i--)poses[i].Dispose();EngineErrorState.ErrorFlagSet=old;}
    }
}

