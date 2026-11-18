using System.Net;
using Cozmo.Conformance;
using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Xunit;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class CommandEventsRoundTests
{
    [Fact]
    public void RecognitionWindowMatchesOnlyTheFaceJustTaughtByThisRun()
    {
        var step=CommandEventsHw.Steps.Single(s=>s.Id=="face-recognised");
        var window=new CommandEventsHw.Window(step,1,0,"Temporary");
        byte[] Body(int id,string name)=>new CladWriter().I32(id).U32(10).Bytes(new byte[48]).String8(name).Bytes(new byte[37]).ToArray();
        window.AddRaw(step.Tag,Body(2,"Other"),1);
        window.AddApi(step.Tag,new(2,10,"Other"),1,()=>new());
        window.AddRaw(step.Tag,Body(1,""),2);
        window.AddApi(step.Tag,new(1,10,""),2,()=>new());
        window.AddRaw(step.Tag,Body(1,"Old"),2);
        window.AddApi(step.Tag,new(1,10,"Old"),2,()=>new());
        Assert.False(window.Matched());
        window.AddRaw(step.Tag,Body(1,"Temporary"),3);
        window.AddApi(step.Tag,new(1,10,"Temporary"),3,()=>new());
        Assert.True(window.Matched());
    }
    private static M15CubeRun Capture(Fixture fixture)
    {
        var run = new M15CubeRun("", "", IPAddress.Loopback, "", [], [], false, false) { Commands = true };
        run.SubscribeCommandReports(fixture.Rig.Engine);
        run.SubscribeCommandRobotReports(fixture.Rig.Robot);
        return run;
    }

    private static CommandEventsHw.Sample Check(CommandEventsHw.Window window, long expected)
    {
        var (raw, api, match) = window.Read();
        Assert.NotNull(raw);
        Assert.NotNull(api);
        Assert.True(match);
        Assert.Equal(expected, raw.Key.Id);
        Assert.Equal(raw.Key, api.Key);
        Assert.True(raw.AtMs >= 0 && api.AtMs >= 0);
        return api;
    }

    [Fact]
    public void FaceReportReplayRetainsNamedPayloadAndGuestExpiresById()
    {
        using var fixture = new Fixture();
        var run = Capture(fixture);
        var engine = fixture.Rig.Engine;
        var faces = fixture.M.Vision.Faces;
        const uint seenAt = 1000;
        void Observe(int id, string name, uint timestamp)
        {
            var rect = new FaceRect(100, 50, 50, 50);
            var face = new TrackedFace(new DetectedFace(id, rect, Name: name), timestamp)
            {
                HeadPose = new Pose3d(Rotation3d.Identity, new Vec3(100, 0, 60),
                    parent: fixture.Robot.GetNativeOrigin(fixture.Robot.CurrentOriginId))
            };
            Assert.NotNull(faces.AddOrUpdateFace(face, fixture.Robot.GetCheckedMainPose(), false));
            engine.PublishRobotObservedFace(new(id, timestamp, default, rect, name, [], [], [], []));
            engine.DrainAppDelivery();
        }

        var window = run.OpenCommandEvent("face-seen");
        Observe(-3, "", seenAt);
        Assert.Equal(seenAt, Check(window, -3).Key.RobotTimestamp);

        window = run.OpenCommandEvent("face-recognised");
        Observe(7, "Ada", seenAt + 1);
        Assert.Equal("Ada", Check(window, 7).Payload["Name"]!.GetValue<string>());

        window = run.OpenCommandEvent("face-lost", -3);
        faces.UpdateNative(seenAt + 15_000, fixture.Robot, () => true);
        engine.DrainAppDelivery();
        Assert.False(window.Matched());
        faces.UpdateNative(seenAt + 15_002, fixture.Robot, () => true);
        engine.DrainAppDelivery();
        Assert.Equal(-3, Check(window, -3).Payload["FaceId"]!.GetValue<int>());
        Assert.NotNull(faces.GetFace(7));
    }

    [Fact]
    public void CameraAndCubePacketReplayKeepTheWorldIdThroughPoseLoss()
    {
        using var fixture = new Fixture();
        const uint activeId = 4;
        M4ControlTests.ConnectCube(fixture.Rig, activeId);
        var run = Capture(fixture);
        var window = run.OpenCommandEvent("cube-seen");
        for (uint i = 1; i <= 4 && !window.Matched(); i++)
        {
            fixture.Motors();
            fixture.Image(i, cube: true);
        }
        var (observed, _, _) = window.Read();
        Assert.NotNull(observed);
        uint id = checked((uint)observed.Key.Id);
        Assert.NotEqual(activeId, id);
        Check(window, id);

        window = run.OpenCommandEvent("cube-moved", id);
        fixture.Rig.Data(new ObjectMoved { ObjectID = activeId, Timestamp = 9001 });
        fixture.Rig.Tick();
        Assert.Equal(9001u, Check(window, id).Key.RobotTimestamp);

        window = run.OpenCommandEvent("cube-tapped", id);
        fixture.Rig.Data(new ObjectTapped { ObjectID = activeId, Timestamp = 9002, NumTaps = 1, TapTime = 7, TapNeg = -25, TapPos = 45 });
        fixture.Rig.Tick();
        var tap = Check(window, id);
        Assert.Equal(9002u, tap.Key.RobotTimestamp);
        Assert.Equal(id, tap.Payload["WorldObjectId"]!.GetValue<uint>());
        Assert.Equal(activeId, tap.Payload["ObjectId"]!.GetValue<uint>());
        var (raw, _, _) = window.Read();
        Assert.Equal($"2A230000{Convert.ToHexString(new CladWriter().U32(id).ToArray())}0107E72D",
            raw!.Payload["bodyHex"]!.GetValue<string>());

        window = run.OpenCommandEvent("object-pose-lost", id);
        var cube = Assert.IsType<ObservableObject>(fixture.M.World.GetLocatedObjectById(id));
        Assert.Equal(0u, fixture.M.World.MarkObjectUnobserved(cube));
        fixture.Rig.Engine.DrainAppDelivery();
        Assert.False(window.Matched());
        Assert.Equal(0u, fixture.M.World.MarkObjectUnobserved(cube));
        fixture.Rig.Engine.DrainAppDelivery();
        Assert.Equal(id, Check(window, id).Payload["ObjectId"]!.GetValue<uint>());
    }

    [Fact]
    public void FilteredTapCarriesTheStrongestTapsOriginalTimestampAndWorldId()
    {
        using var fixture = new Fixture();
        const uint activeId = 4;
        M4ControlTests.ConnectCube(fixture.Rig, activeId);
        Assert.True(fixture.Robot.IsPhysicalRobot);
        var id = fixture.M.World.ConnectedObjectIdForActiveId(activeId)!.Value;
        var run = Capture(fixture);
        var window = run.OpenCommandEvent("cube-tapped", id);
        fixture.Rig.Data(new ObjectTapped { ObjectID = activeId, Timestamp = 101, TapPos = 70 });
        fixture.Rig.Data(new ObjectTapped { ObjectID = activeId, Timestamp = 102, TapPos = 95 });
        fixture.Rig.Tick(1);
        Assert.False(window.Matched());
        fixture.Rig.Tick(76);
        Assert.Equal(102u, Check(window, id).Key.RobotTimestamp);
    }

    [Fact]
    public void ReportWithWrongPublicIdOrOutOfWindowCallbackDoesNotPass()
    {
        var step = CommandEventsHw.Steps.Single(step => step.Id == "face-lost");
        var window = new CommandEventsHw.Window(step, -3, 1000);
        var body = new CladWriter().I32(-3).ToArray();
        window.AddRaw(step.Tag, body, 999);
        Assert.Null(window.Read().Raw);
        window.AddRaw(step.Tag, body, 1100);
        window.AddApi(step.Tag, new(-2), 1101, () => new());
        window.AddApi(step.Tag, new(-3), 36_001, () => new());
        Assert.False(window.Matched());
        Assert.Equal(-2, window.Read().Api!.Key.Id);
    }
}
