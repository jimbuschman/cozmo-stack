using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class M14LifetimeCheckedRowsTests
{
    [Fact]
    public void ActualRobotRemovalNullsPetThenFaceThenBlockAndLaterSpeechBeforeTheirRecipients()
    {
        using var robot=CozmoRobot.CreateOffline();var native=robot.Engine.Robot!;var order=new List<int>();
        // Supplied recipient fixtures check only the source-backed outer call/null order, not recursive bodies.
        foreach(int offset in new[]{0x3C,0x38,0x34,0x268})
            native.Lifetime.Bind(offset,_=>{Assert.True(native.Lifetime.IsKnownNull(offset));order.Add(offset);});
        robot.Engine.Robots.RemoveRobot(1,false);Assert.Equal(new[]{0x3C,0x38,0x34,0x268},order);
    }
    [Fact]
    public void MissingLiveWorldAndSpeechOwnersAreNamedOnTheStackChannelAtRemoval()
    {
        using var robot=CozmoRobot.CreateOffline();var stack=new List<string>();var source=new List<EngineLogEntry>();robot.Engine.StackLogLine+=stack.Add;robot.Engine.LogEntry+=source.Add;
        robot.Engine.Robots.RemoveRobot(1,false);
        Assert.Contains(stack,s=>s.Contains("M14-013 PetWorld")&&s.Contains("0x3C"));
        Assert.Contains(stack,s=>s.Contains("M14-013 FaceWorld")&&s.Contains("0x38"));
        Assert.Contains(stack,s=>s.Contains("M14-013 speech")&&s.Contains("0x268"));
        Assert.DoesNotContain(source,e=>e.Text.Contains("MISSING")||e.Name.Contains("MISSING"));
    }
    [Fact]
    public void RetainedHostWorldResetIsQuietButDoesNotCertifyTheNativeDestructionBody()
    {
        var faces=new FaceWorld();var pets=new PetWorld();int callbacks=0;
        // Seed nonempty live storage before installing event spies; this checks quiet clear, not native recursive frees.
        Assert.NotNull(faces.AddOrUpdateFace(new TrackedFace(new DetectedFace(1,new FaceRect(0,0,10,10)),1),Pose3d.Identity,false));
        // Exercise the existing host reset candidate's observable no-notification constraint (OV33 L2/L3).
        faces.FaceDeleted+=_=>callbacks++;faces.FaceObserved+=_=>callbacks++;faces.FaceIdChanged+=(_,_)=>callbacks++;faces.Log+=_=>callbacks++;
        pets.Update(new[]{new DetectedPet(1,PetType.Unknown,new FaceRect())},1,false);pets.PetObserved+=(_,_)=>callbacks++;
        faces.ResetToConstructed();pets.ResetToConstructed();Assert.Equal(0,callbacks);Assert.Empty(faces.Faces);Assert.Empty(pets.Pets);
        // There is no native lifetime binding here; the separate removal test requires that uncertainty be visible.
    }
}
