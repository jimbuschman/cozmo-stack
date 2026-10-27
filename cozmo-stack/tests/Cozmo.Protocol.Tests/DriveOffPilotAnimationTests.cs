using System.Text;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Xunit;
namespace Cozmo.Protocol.Tests;
[Collection("M5 process statics")]
public sealed class DriveOffPilotAnimationTests
{
    [Fact]
    public void RetryPlaysRetainedConcreteClipButReportsNewSelectedNameAndDestructorClearsIt()
    {
        // Concrete action/real loader/streamer test with authored two-clip group
        // and explicit RNG words; no hand-written completion or playback callback.
        string root=Directory.CreateTempSubdirectory("pilot-retry-").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(root,"animations"));
            Directory.CreateDirectory(Path.Combine(root,"animationGroups"));
            Directory.CreateDirectory(Path.Combine(root,"animationGroupMaps"));
            File.WriteAllText(Path.Combine(root,"animations","a.json"),"{\"a\":[]}",Encoding.UTF8);
            File.WriteAllText(Path.Combine(root,"animations","b.json"),"{\"b\":[]}",Encoding.UTF8);
            File.WriteAllText(Path.Combine(root,"animationGroupMaps","AnimationTriggerMap.json"),"{\"Pairs\":[{\"CladEvent\":\"DriveEndDefault\",\"AnimName\":\"two\"}]}",Encoding.UTF8);
            File.WriteAllText(Path.Combine(root,"animationGroups","two.json"),"{\"Animations\":[{\"Name\":\"a\",\"Weight\":1,\"Mood\":\"Default\",\"CooldownTime_Sec\":0},{\"Name\":\"b\",\"Weight\":1,\"Mood\":\"Default\",\"CooldownTime_Sec\":0}]}",Encoding.UTF8);
            using var rig=new M4ControlTests.Rig();rig.ToSynced();
            rig.Robot.Animations.ManualTicking=true;var library=rig.Robot.Animations.LoadFrom(root);
            library.CooldownTimeSec=()=>10;library.CurrentMood=()=>SimpleMood.Default; // explicit group fixture inputs.
            Assert.Same(rig.Engine.ContextRandom,library.ContextRandom);
            Assert.Equal(2,library.GetGroupForSource("two")!.Entries.Count);
            int words=0;rig.Engine.ContextRandom.WordSourceForTest=()=>++words<=2?0u:0xC0000000u;
            var action=new TriggerAnimationAction(rig.Robot,new(AnimationTrigger.DriveEndDefault,1,true,0,60f,false),_=>{});
            try
            {
                Assert.Equal(0,action.Init());Assert.Equal("a",rig.Robot.Animations.Playing);
                Assert.Equal("a",action.GetAnimationCompletionName());Assert.Equal(2,words);
                Assert.Equal(0,action.Init());Assert.Equal("a",rig.Robot.Animations.Playing);
                Assert.Equal(4,words);Assert.Equal("b",action.GetAnimationCompletionName());
                var report=new RobotCompletedAction(action.Tag,0x1A,0,Array.Empty<uint>(),0,action.GetAnimationCompletionName(),action.GetCompletionVariant());
                Assert.Equal("010162",Convert.ToHexString(report.PackBody().AsSpan(13)));
                action.WatcherEnding();Assert.Null(rig.Robot.Animations.Playing);
                action.WatcherEnding(); // repeated disposal has no second stream mutation.
            }
            finally{action.WatcherEnding();rig.Engine.ContextRandom.WordSourceForTest=null;}
        }
        finally{Directory.Delete(root,true);}
    }
    [Theory]
    [InlineData(255,255)] [InlineData(256,0)] [InlineData(257,1)]
    public void AnimationStringEightNarrowsCopiedByteCountToo(int length,int count)
    {
        var report=new RobotCompletedAction(1,0x1A,0,Array.Empty<uint>(),0,new string('x',length),1);
        var union=report.PackBody().AsSpan(13).ToArray();
        Assert.Equal(2+count,union.Length);Assert.Equal((byte)1,union[0]);Assert.Equal((byte)count,union[1]);
        Assert.All(union.Skip(2),b=>Assert.Equal((byte)'x',b));
    }
}
