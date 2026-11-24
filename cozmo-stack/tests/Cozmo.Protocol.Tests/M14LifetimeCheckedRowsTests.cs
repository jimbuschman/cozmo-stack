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
    public void LiveWorldOwnersRetireAndMissingSpeechRecipientStaysVisibleAtRemoval()
    {
        using var robot=CozmoRobot.CreateOffline();var stack=new List<string>();var source=new List<EngineLogEntry>();robot.Engine.StackLogLine+=stack.Add;robot.Engine.LogEntry+=source.Add;
        var faces=robot.Vision.Faces;var pets=robot.Vision.Pets;
        Assert.NotNull(faces.AddOrUpdateFace(new TrackedFace(new DetectedFace(1,new FaceRect(0,0,10,10)),1),Pose3d.Identity,false));
        pets.Update(new[]{new DetectedPet(1,PetType.Unknown,new FaceRect())},1,false);
        Assert.Single(faces.Faces);Assert.Single(pets.Pets);
        // ~Robot releases PetWorld and FaceWorld at 0x00511298 and 0x005112AC,
        // before the old Robot allocation is freed and the retained facade is reset.
        robot.Engine.RobotStorageFreed=_=>{Assert.Empty(faces.Faces);Assert.Empty(pets.Pets);};
        robot.Engine.Robots.RemoveRobot(1,false);
        Assert.DoesNotContain(stack,s=>s.Contains("MISSING: Robot lifetime owner +0x3C"));
        Assert.DoesNotContain(stack,s=>s.Contains("MISSING: Robot lifetime owner +0x38"));
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
        // This fixture alone does not prove the native lifetime binding or recursive shared-control ownership.
    }
}
