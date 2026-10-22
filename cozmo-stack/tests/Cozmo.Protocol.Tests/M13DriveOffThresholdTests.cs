using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public class M13DriveOffThresholdTests
{
    private static void State(M4ControlTests.Rig rig, float x, bool contact = false)
    {
        var robot = rig.Engine.Robot!;
        var state = rig.MakeState(RobotStatusFlag.IsBodyAccMode |
            (contact ? RobotStatusFlag.IsOnCharger : (RobotStatusFlag)0),
            origin: robot.CurrentOriginId, frame: robot.PoseFrameId, x: x);
        rig.Data(state);
        rig.Tick();
        // Prove admission and the real history-to-pose update, rather than substituting SetPose or an origin miss.
        Assert.Equal(0u, robot.LastFullStateResult);
        Assert.Equal(state.PoseFrameId, robot.PoseFrameId);
        Assert.Equal(x, (float)robot.GetCheckedMainPose().Translation.X);
    }

    private static ushort[] Thresholds(M4ControlTests.Rig rig, int mark) => rig.SentSince(mark)
        .OfType<SetCliffDetectThreshold>().Select(m => m.Field0).ToArray();

    private static ObservableObject Charger(M4ControlTests.Rig rig) => Assert.Single(
        rig.Robot.LocalizationWorld.LocatedObjects.Where(o => o.Type == ObjectType.Charger_Basic
            && o.OriginId == rig.Engine.Robot!.CurrentOriginId));

    private static void Exit(M4ControlTests.Rig rig, float x)
    {
        uint before = rig.Engine.NativeUiDeliveryCount;
        uint? atSend = null;
        // Read-only observation: null preserves the actual send. The existing counter records external broadcast
        // enqueue, before host callbacks drain. Native platform broadcast 0x00511D8A precedes send 0x00511DA0.
        rig.Engine.Robot!.SendFault = message =>
        {
            if (message is SetCliffDetectThreshold { Field0: 400 }) atSend = rig.Engine.NativeUiDeliveryCount;
            return null;
        };
        try { State(rig, x); }
        finally { rig.Engine.Robot.SendFault = null; }
        Assert.Equal((uint?)(before + 1), atSend);
    }

    // reach: production (received, admitted, frame-matched RobotStates -> real history/pose, contact observation,
    // platform footprint tick, threshold wire and public reports; no pose/world/result substitution).
    // Old pre-B-M13-9 behaviour: contact-off clears the platform and sends 400 at x=0, instead of retaining 50.
    // This test rejects that value; current baseline already passes (no new production fix is required).
    // M4-019 SC4/C7: 0x00512E8A literal 50, BLE 0x00512E96, 400 once 0x00512E9C..EA6.
    // M4-019 C8: 0x00513CD8..0x00513E2A and 0x00511D44..DCA. M13-040 Claude checks 20261021/20261022;
    // charger ObservableObject footprint 0x0087713A, rest-claude-check 20261022.
    [Fact]
    public void DistanceRestores400OnlyAbove50WithoutAPlatformExitThenFootprintExitReportsFalse()
    {
        using var rig = new M4ControlTests.Rig(); rig.ToSynced();
        var platforms = new List<bool>(); var contacts = new List<bool>();
        rig.Robot.Sensors.OnChargerPlatformChanged += platforms.Add;
        rig.Robot.Sensors.ChargerEvent += contacts.Add;
        State(rig, 0, contact: true);
        Assert.Equal(new[] { true }, platforms);
        int mark = rig.Mark();
        State(rig, 0);
        Assert.Equal((ushort)50, rig.Robot.Sensors.CliffDetectThreshold);
        Assert.False(rig.Robot.Sensors.OnChargerContacts);
        foreach (float x in new[] { 25.6f, 49f, 50f })
        {
            State(rig, x);
            Assert.True(rig.Robot.Sensors.OnChargerPlatform);
            Assert.Equal((ushort)50, rig.Robot.Sensors.CliffDetectThreshold);
            Assert.Empty(Thresholds(rig, mark));
        }
        var charger = Charger(rig);
        Assert.Equal(new[] { true, false }, contacts);
        State(rig, 51);
        Assert.True(rig.Robot.Sensors.OnChargerPlatform);
        Assert.Equal(new[] { true }, platforms); // distance changes only the threshold, not platform/API state
        Assert.Equal(new ushort[] { 400 }, Thresholds(rig, mark));
        State(rig, 85);
        Assert.True(rig.Robot.Sensors.OnChargerPlatform);
        Assert.Equal(new ushort[] { 400 }, Thresholds(rig, mark)); // one-shot distance send
        Exit(rig, 87);
        Assert.False(rig.Robot.Sensors.OnChargerPlatform);
        Assert.Equal(new[] { true, false }, platforms);
        Assert.Equal(new ushort[] { 400, 400 }, Thresholds(rig, mark)); // platform change always sends too
        Assert.Same(charger, Charger(rig));
        State(rig, 100);
        Assert.Equal(new ushort[] { 400, 400 }, Thresholds(rig, mark));
        Assert.Equal(new[] { true, false }, platforms);
    }

    // reach: production (all poses, including the initial distance-rule consumption, enter via admitted states).
    // Old pre-B-M13-9 behaviour: 400 and platform=false immediately at contact-off x=60; expected 50 until exit.
    // This test rejects that value; current baseline already passes. SC4e: +0x528/+0x52C initialized only in ctor;
    // docking does not restart the one-shot. C8 platform exit sends 400 even with the distance send already used.
    // Same checked sources as above; zero heading avoids the open shared rotation last-bit boundary.
    [Fact]
    public void UsedDistanceScheduleLeaves50UntilFootprintExitAfterContactsDrop()
    {
        using var rig = new M4ControlTests.Rig(); rig.ToSynced();
        State(rig, 0); State(rig, 60);
        Assert.Equal((ushort)400, rig.Robot.Sensors.CliffDetectThreshold);
        var platforms = new List<bool>(); var contacts = new List<bool>();
        rig.Robot.Sensors.OnChargerPlatformChanged += platforms.Add;
        rig.Robot.Sensors.ChargerEvent += contacts.Add;
        State(rig, 60, contact: true);
        Assert.Equal((ushort)50, rig.Robot.Sensors.CliffDetectThreshold);
        int mark = rig.Mark();
        State(rig, 60);
        Assert.Equal((ushort)50, rig.Robot.Sensors.CliffDetectThreshold);
        Assert.False(rig.Robot.Sensors.OnChargerContacts);
        foreach (float x in new[] { 85.6f, 110f, 145f })
        {
            State(rig, x);
            Assert.True(rig.Robot.Sensors.OnChargerPlatform);
            Assert.Equal((ushort)50, rig.Robot.Sensors.CliffDetectThreshold);
            Assert.Empty(Thresholds(rig, mark));
        }
        var charger = Charger(rig);
        Assert.Equal(new[] { true }, platforms);
        Assert.Equal(new[] { true, false }, contacts);
        Exit(rig, 147);
        Assert.False(rig.Robot.Sensors.OnChargerPlatform);
        Assert.Equal(new[] { true, false }, platforms);
        Assert.Equal(new ushort[] { 400 }, Thresholds(rig, mark));
        Assert.Same(charger, Charger(rig));
        State(rig, 160);
        Assert.Equal(new ushort[] { 400 }, Thresholds(rig, mark));
        Assert.Equal(new[] { true, false }, platforms);
    }
}
