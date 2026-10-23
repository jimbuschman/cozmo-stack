using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class M13ConfigurationAudioNotifyTests
{
    private sealed class Fixture : IDisposable
    {
        internal readonly M4ControlTests.Rig Rig = new();
        internal readonly ManipulationSystem Manipulation;
        internal EngineRobot Robot => Rig.Engine.Robot!;
        internal BlockConfigurationManager Configurations => Manipulation.Configurations;
        internal readonly List<byte> Heights = new();
        internal readonly List<string> Visits = new();
        internal readonly List<BehaviorAudioRequest> Requests = new();
        private readonly RetainedSubscription _observation;
        private readonly List<uint> _cubeIds = new();
        internal Fixture()
        {
            Rig.ToSynced(); Rig.Robot.Vision.Enabled = false;
            Manipulation = new ManipulationSystem(Rig.Robot, Rig.Robot.Vision);
            // Observe the existing robot-owned clients; never construct or replace either subscriber.
            Assert.NotNull(Robot.BehaviorAudioA); Assert.NotNull(Robot.BehaviorAudioB);
            Assert.NotSame(Robot.BehaviorAudioA, Robot.BehaviorAudioB);
            Robot.BehaviorAudioA!.StepForTest = step => { if (step == "world") Visits.Add("A"); };
            Robot.BehaviorAudioB!.StepForTest = step => { if (step == "world") Visits.Add("B"); };
            Robot.BehaviorAudioRecipient = Requests.Add; // observe robot+0x58 requests; no M6/M9 playback substitute
            _observation = Robot.BehaviorPublicState.Subscribe(state => Heights.Add(state.StackHeight));
        }
        internal void AddCube(uint id, float z)
        {
            var type = (ObjectType)((int)ObjectType.Block_LIGHTCUBE1 + _cubeIds.Count);
            var cube = new ObservableObject(id, type, CubeGeometry.MarkersFor(type)) { OriginId = Robot.CurrentOriginId };
            var pose = new Pose3d(Rotation3d.Identity, new Vec3(200,0,z), "fixture cube", Robot.GetNativeOrigin(Robot.CurrentOriginId));
            cube.InitPose(pose, PoseState.Known);
            Rig.Robot.Vision.World.AddLocatedObject(cube); _cubeIds.Add(id);
        }
        internal void CreateTwoCubeStack()
        {
            Assert.Equal((byte)0, Robot.BehaviorPublicState.State.StackHeight);
            AddCube(10,22); AddCube(11,66); Update();
        }
        internal void Update()
        {
            int before = Configurations.NotifyCount;
            Configurations.ForceUpdate = true; // even unchanged height must reach Notify, not stop at the movement gate
            Configurations.Update();
            Assert.Equal(before + 1, Configurations.NotifyCount);
            Assert.False(Configurations.ForceUpdate);
        }
        internal void ClearObservations() { Heights.Clear(); Visits.Clear(); Requests.Clear(); }
        internal void RemoveAllCubes()
        {
            foreach (uint id in _cubeIds) Rig.Robot.Vision.World.MarkUnknown(id);
            _cubeIds.Clear();
        }
        internal void AssertStackRequests(uint id)
        {
            Assert.Equal(new[] { "A", "B" }, Requests.Select(request => request.Client));
            Assert.All(Requests, request =>
            {
                // H4: RobotAudioClient slot+8, object1, empty callback (00592348..0059236C).
                Assert.Equal(8, request.Slot); Assert.Equal(id, request.Id);
                Assert.Equal(1u, request.Value); Assert.Equal(0u, request.Arg3);
                Assert.Equal(0u, request.Extra0); Assert.Equal(0u, request.Extra1); Assert.True(request.EmptyCallback);
            });
        }
        public void Dispose()
        {
            Robot.BehaviorAudioA!.StepForTest = null; Robot.BehaviorAudioB!.StepForTest = null;
            Robot.BehaviorAudioRecipient = null; _observation.Dispose(); Manipulation.Dispose(); Rig.Dispose();
        }
    }

    // reach: unit (fixture cube poses/direct configuration Update; real robot broadcaster and both audio clients;
    // MISSING native container producer 0x0061682C, M11-037/M12-049; audio observed at robot+0x58, not M6/M9 output).
    // B-M13-15 baseline already gives [2], A then B and two EC3490EC requests. Historical missing notify join
    // instead gives height0, zero dispatches/requests; disabling that join must fail this test.
    // R8-R12/H4: 0064DCF4..0064DD68, 0064E656..0064E69A, 00592320..0059236C.
    [Fact]
    public void ZeroToTwoDispatchesOnceAndBothRealClientsPostCreatedInOrder()
    {
        using var f = new Fixture(); f.CreateTwoCubeStack();
        Assert.Equal((byte)2, f.Robot.BehaviorPublicState.State.StackHeight);
        Assert.Equal(new byte[] { 2 }, f.Heights);
        Assert.Equal(new[] { "A", "B" }, f.Visits); f.AssertStackRequests(0xEC3490EC);
        Assert.Equal((byte)1, f.Robot.BehaviorAudioA!.Stacked); Assert.Equal((byte)1, f.Robot.BehaviorAudioB!.Stacked);
    }

    // reach: unit (fixture cube poses/direct Update; real robot subscribers; producer MISSING 0x0061682C,
    // M11-037/M12-049; M6/M9 device output not exercised).
    // B-M13-15 baseline already gives dispatch [3], visits [A,B], zero audio requests. Historical missing join
    // leaves height0 and zero dispatches (this test rejects [ ] instead of [3]); it also lacks the initial height2 setup.
    // Notify compares the height byte; H4 compares only (height > 1), 0064DCF4 and 00592326..0059232E.
    [Fact]
    public void TwoToThreeDispatchesOnceToBothClientsWithoutAnotherStackEvent()
    {
        using var f = new Fixture(); f.CreateTwoCubeStack(); f.ClearObservations();
        f.AddCube(12,110); f.Update();
        Assert.Equal((byte)3, f.Robot.BehaviorPublicState.State.StackHeight);
        Assert.Equal(new byte[] { 3 }, f.Heights); Assert.Equal(new[] { "A", "B" }, f.Visits);
        Assert.Empty(f.Requests);
        Assert.Equal((byte)1, f.Robot.BehaviorAudioA!.Stacked); Assert.Equal((byte)1, f.Robot.BehaviorAudioB!.Stacked);
    }

    // reach: unit (fixture cubes/direct forced Update; real robot subscribers; producer MISSING 0x0061682C,
    // M11-037/M12-049; M6/M9 output not exercised).
    // No differing old B-M13-15 behavior: unchanged height already gives zero dispatches/visits/requests.
    // This is a negative control, not an old-code-failure claim. 0064DD0E -> 0064DD6C skips dispatch.
    [Fact]
    public void UnchangedHeightReachesNotifyButDispatchesToNeitherClient()
    {
        using var f = new Fixture(); f.CreateTwoCubeStack(); f.ClearObservations(); f.Update();
        Assert.Equal((byte)2, f.Robot.BehaviorPublicState.State.StackHeight);
        Assert.Empty(f.Heights); Assert.Empty(f.Visits); Assert.Empty(f.Requests);
    }

    // reach: unit (fixture cube removal/direct Update; real robot subscribers; producer MISSING 0x0061682C,
    // M11-037/M12-049; M6/M9 output not exercised).
    // B-M13-15 baseline already gives [0] and A/B 3BA32BC6 requests. Historical missing notify join leaves
    // zero dispatches/requests instead; disabling the join must fail this positive removal assertion.
    // No tallest -> zero: 0064DCE0..0064DD24; removal event 0059235E..0059236C.
    [Fact]
    public void LastStackGoingAwayDispatchesZeroAndBothRealClientsPostRemoved()
    {
        using var f = new Fixture(); f.CreateTwoCubeStack(); f.ClearObservations();
        f.RemoveAllCubes(); f.Update();
        Assert.Equal((byte)0, f.Robot.BehaviorPublicState.State.StackHeight);
        Assert.Equal(new byte[] { 0 }, f.Heights); Assert.Equal(new[] { "A", "B" }, f.Visits);
        f.AssertStackRequests(0x3BA32BC6);
        Assert.Equal((byte)0, f.Robot.BehaviorAudioA!.Stacked); Assert.Equal((byte)0, f.Robot.BehaviorAudioB!.Stacked);
    }
}
