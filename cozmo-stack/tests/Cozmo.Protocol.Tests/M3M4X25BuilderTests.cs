using System.Collections;
using System.Reflection;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class M3M4X25BuilderTests
{
    [Fact]
    public void ReplacementAppendsBeforeRetiringOldAndMovesTheSlotBehindItsPeer()
    {
        // Checked assignments 0x00543FCA..0x00543FE2 / 0x00551706..0x00551784.
        var signal=new SubscriptionSignal<int>();var calls=new List<string>();RetainedSubscription? slot=null;
        slot=signal.Subscribe(1,_=>calls.Add("old"),()=>{Assert.NotNull(slot);signal.Deliver(1,0);});
        using var peer=signal.Subscribe(1,_=>calls.Add("peer"));
        RetainedSubscription.Replace(ref slot,signal.Subscribe(1,_=>calls.Add("new")));
        Assert.Equal(new[]{"peer","new"},calls);slot.Dispose();
    }

    [Fact]
    public void FutureOwnerRemovalSkipsItsCallbackButSelfRemovalKeepsDefinedSuccessor()
    {
        var signal=new SubscriptionSignal<int>();var calls=new List<string>();RetainedSubscription? first=null,future=null;
        first=signal.Subscribe(1,_=>{calls.Add("first");future!.Dispose();first!.Dispose();});
        // The self-removed node's next was patched before detaching it: this is the defined case.
        future=signal.Subscribe(1,_=>calls.Add("future"));
        using var last=signal.Subscribe(1,_=>calls.Add("last"));signal.Deliver(1,0);
        Assert.Equal(new[]{"first","last"},calls);
    }

    [Fact]
    public void SelfRemovalThenDeletingItsRetainedSuccessorSkipsTheUndefinedTailVisibly()
    {
        var signal=new SubscriptionSignal<int>();var calls=new List<string>();RetainedSubscription? first=null,next=null;
        signal.UndefinedTail=()=>calls.Add("M1-034/M2-020");
        first=signal.Subscribe(1,_=>{calls.Add("first");first!.Dispose();next!.Dispose();});
        next=signal.Subscribe(1,_=>calls.Add("freed"));using var last=signal.Subscribe(1,_=>calls.Add("last"));
        signal.Deliver(1,0);Assert.Equal(new[]{"first","M1-034/M2-020"},calls);
        calls.Clear();signal.Deliver(1,0);Assert.Equal(new[]{"last"},calls);
    }

    [Fact]
    public void RicReverseClearFinishesBeforeItsResponseIsDelivered()
    {
        using var rig=new M4ControlTests.Rig();var released=new List<byte>();bool response=false;
        rig.Engine.MessageSubscriptionReleasedForTest=(tag,name)=>{if(name=="RIC")released.Add((byte)tag);};
        rig.Engine.ConnectionResponse+=_=>{Assert.Equal(new byte[]{0xED,0xC9,0xEE,0xD2},released);response=true;};
        rig.ToSynced();Assert.True(response);
    }

    [Fact]
    public void NvAndBackupReleaseTheirOwnRawHandlesBeforeTheMessagingOwner()
    {
        using var rig=new M4ControlTests.Rig();rig.ToSynced();var released=new List<string>();
        rig.Engine.MessageSubscriptionReleasedForTest=(tag,name)=>{
            released.Add($"{(byte)tag:X2}:{name}");
            if(name=="Backup")foreach(ushort t in new ushort[]{0x6B,0x6D,0x6C})Assert.Equal(0,rig.Engine.AnimationMessages.Count(t,false));
        };
        rig.Engine.NvStorage!.ReleaseForRobot();
        Assert.Equal(new[]{"CD:NVStorage","C9:Backup"},released);
        Assert.Null(rig.Engine.Backup);
    }

    [Fact]
    public void ComponentDestructorsRetireAccelAndTapHandlesInNativeOrder()
    {
        using var rig=new M4ControlTests.Rig();rig.ToSynced();var released=new List<string>();
        rig.Engine.MessageSubscriptionReleasedForTest=(tag,name)=>released.Add($"{(byte)tag:X2}:{name}");
        rig.Robot.CubeAccel.ResetToConstructed();rig.Robot.Cubes.DestroyTapFilter();
        Assert.Equal(new[]{"F5:CubeAccel","B5:BlockTapFilter","B4:BlockTapFilter","B6:BlockTapFilter"},released);
    }

    [Fact]
    public void HeadAckIsUnlinkedBeforeTheBaseTailUnlocksItsTrack()
    {
        using var rig=new M4ControlTests.Rig();rig.ToSynced();rig.Calibrate();rig.State();
        _=rig.Robot.Motion.SetHeadAngleAsync(0.5f);rig.Tick();
        var list=(IList)typeof(CozmoMotion).GetField("_actions",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(rig.Robot.Motion)!;
        var runner=Assert.IsAssignableFrom<ActionRunner>(Assert.Single(list.Cast<object>()));
        Assert.True(rig.Robot.Motion.AreAnyTracksLocked(CozmoMotion.HeadTrack));bool checkedAtRelease=false;
        rig.Engine.MessageSubscriptionReleasedForTest=(_,name)=>{if(name=="HeadAction"){checkedAtRelease=true;Assert.True(rig.Robot.Motion.AreAnyTracksLocked(CozmoMotion.HeadTrack));}};
        runner.WatcherEnding();Assert.True(checkedAtRelease);Assert.False(rig.Robot.Motion.AreAnyTracksLocked(CozmoMotion.HeadTrack));
    }

    [Fact]
    public void EarlyHostConnectWithNoResourcesQueuesWithoutAConstructionOrLoadException()
    {
        bool queued=false;
        using var rig=new M4ControlTests.Rig(engineOptions:new CozmoEngineOptions());
        // Exercise the unready engine before its host constructor publishes it.
        using var robot=CozmoRobot.CreateForTest(rig.Port,()=>0,engineCreated:e=>{
            Assert.False(e.HostBehaviorMapLoaded);
            e.ConnectToRobot(M4ControlTests.Rig.RobotIp);
            Assert.Null(e.Robot);Assert.Null(e.RobotAddressInUse);queued=true;
        });
        Assert.True(queued);Assert.True(robot.Engine.HostBehaviorMapLoaded);
        robot.Engine.Tick();Assert.NotNull(robot.Engine.Robot);
    }

    [Fact]
    public void ResourceRootBehaviorMapIsLoadedBeforeTheFirstRobotConstruction()
    {
        var root=Directory.CreateTempSubdirectory("behavior-before-connect-").FullName;
        try
        {
            var dir=Path.Combine(root,BehaviorConfigLoader.RelativeDirectory);Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir,"one.json"),"{\"behaviorID\":\"AcknowledgeFace\",\"behaviorClass\":\"Wait\"}");
            using var rig=new M4ControlTests.Rig(engineOptions:new CozmoEngineOptions{ResourcesPath=root});
            Assert.True(rig.Engine.HostBehaviorMapLoaded);Assert.True(rig.Engine.HostBehaviorMap!.ContainsKey(BehaviorID.AcknowledgeFace));
            Assert.Null(rig.Engine.Robot);rig.Engine.ConnectToRobot(M4ControlTests.Rig.RobotIp);rig.Tick();Assert.NotNull(rig.Engine.Robot);
        }
        finally{Directory.Delete(root,true);}
    }
}
