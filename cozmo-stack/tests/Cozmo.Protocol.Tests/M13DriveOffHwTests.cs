using System.Text.Json.Nodes;
using Cozmo.Conformance;
using Cozmo.Protocol;
using Xunit;

namespace Cozmo.Protocol.Tests;

// Grader tests for the drive-off-charger hardware check. Expected verdicts follow the engine rules the check cites,
// not the stack: M4-019 C7 (travel = summed |MoveRobotPoseForward(pose,-20) delta|, 400 once when > 50.0,
// 0x00512E2A..0x00512EA6), C8 (platform = arg || contacts; 50/400 on a change, 0x00511D44..0x00511DCA) and the
// first-state 50 (0x00512D80..0x00512DA2). With heading 0 the drive-centre travel equals the x travel.
public class M13DriveOffHwTests
{
    const double DriveStart = 100;
    static readonly double[] Xs = { 0, 6, 20, 35, 49, 52, 60, 75, 88, 100 };

    static M5Hw.WireMessage State(double at, float x, bool contact, uint frame = 0) => new(at, false, new RobotState
    {
        PoseFrameId = frame, PoseOriginId = 1, Pose = new RobotPose { X = x },
        Status = contact ? (uint)RobotStatusFlag.IsOnCharger : 0u,
    }.ToBytes());
    static M5Hw.WireMessage Threshold(double at, ushort value) => new(at, true, new SetCliffDetectThreshold { Field0 = value }.ToBytes());

    /// <summary>
    /// The engine's wire for a start on the contacts: 50 (platform), 50 (first state) while parked; states every 10 ms
    /// from <see cref="DriveStart"/> at <see cref="Xs"/>; contacts off from x = 6; 400 after the first state over 50 mm
    /// (x = 52); 400 after the platform exit following x = 88.
    /// </summary>
    static List<M5Hw.WireMessage> Engine(Func<double, ushort?>? extra = null, bool distance400 = true)
    {
        var wire = new List<M5Hw.WireMessage> { State(10, 0, true), Threshold(11, 50), Threshold(12, 50), State(40, 0, true), State(70, 0, true) };
        for (int i = 0; i < Xs.Length; i++)
        {
            double at = DriveStart + 10 * i;
            wire.Add(State(at, (float)Xs[i], Xs[i] == 0));
            if (extra?.Invoke(Xs[i]) is { } v) wire.Add(Threshold(at + 1, v));
            if (Xs[i] == 52 && distance400) wire.Add(Threshold(at + 1, 400));
            if (Xs[i] == 88) wire.Add(Threshold(at + 2, 400));
        }
        return wire;
    }
    static Dictionary<string, JsonObject> Grade(List<M5Hw.WireMessage> wire, double[]? platformFalse = null, bool platform = true) =>
        M13DriveOffHw.GradeThresholds(new(wire, DriveStart, platform, platformFalse ?? new[] { DriveStart + 80 + 1.5 }, false));
    static string Verdict(JsonObject row) => row["verdict"]!.GetValue<string>();

    // reach: unit (synthetic engine-ordered wire through the harness grader)
    [Fact]
    public void EngineOrderedWireMatchesEveryThresholdCheck()
    {
        var r = Grade(Engine());
        foreach (var id in new[] { "ON_CHARGER", "HOLD_50", "DISTANCE_400", "PLATFORM_EXIT" }) Assert.Equal("MATCH", Verdict(r[id]));
        Assert.Equal(52f, r["DISTANCE_400"]["crossingState"]!["travelMm"]!.GetValue<float>());
    }

    // reach: unit. Run 20261020-200624's bug: contacts off cleared the platform and sent 400 at x = 6.
    [Fact]
    public void FourHundredWhenTheContactsDropIsAProblem()
    {
        var r = Grade(Engine(x => x == 6 ? (ushort)400 : null, distance400: true));
        Assert.Equal("MISMATCH", Verdict(r["HOLD_50"]));
    }

    // reach: unit. 49 mm is not more than 50 (ble at 0x00512E96): a 400 there is early.
    [Fact]
    public void FourHundredAtFortyNineMillimetresIsEarly()
    {
        var r = Grade(Engine(x => x == 49 ? (ushort)400 : null, distance400: false));
        Assert.Equal("MISMATCH", Verdict(r["HOLD_50"]));
    }

    // reach: unit. Without the one-shot distance send only the exit 400 is left; the engine sends two.
    [Fact]
    public void MissingDistanceSendIsAProblem()
    {
        var r = Grade(Engine(distance400: false));
        Assert.Equal("MATCH", Verdict(r["HOLD_50"]));
        Assert.Equal("MISMATCH", Verdict(r["DISTANCE_400"]));
    }

    // reach: unit. If the platform never clears, one 400 (the distance one) is expected.
    [Fact]
    public void NoPlatformExitExpectsOneFourHundred()
    {
        var wire = Engine().Where(m => !(m.Outbound && m.AtMs == DriveStart + 80 + 2)).ToList();
        var r = Grade(wire, Array.Empty<double>());
        Assert.Equal("MATCH", Verdict(r["DISTANCE_400"]));
        Assert.Equal("INCONCLUSIVE", Verdict(r["PLATFORM_EXIT"]));
    }

    // reach: unit. Before the drive the engine sends exactly 50 and 50.
    [Fact]
    public void ParkedSendsOtherThanTwoFiftiesAreAProblem()
    {
        var one = Engine(); one.RemoveAt(2);
        Assert.Equal("MISMATCH", Verdict(Grade(one)["ON_CHARGER"]));
        var four = Engine(); four.Insert(3, Threshold(13, 400));
        Assert.Equal("MISMATCH", Verdict(Grade(four)["ON_CHARGER"]));
        Assert.Equal("MISMATCH", Verdict(Grade(Engine(), platform: false)["ON_CHARGER"]));
    }

    // reach: unit. A state exactly at 50.0 sits on the limit; the crossing is not graded.
    [Fact]
    public void CrossingOnTheLimitIsNotGraded()
    {
        var wire = Engine().Select(m => !m.Outbound && m.AtMs == DriveStart + 40 ? State(m.AtMs, 50, false) : m).ToList();
        var r = Grade(wire);
        Assert.Equal("INCONCLUSIVE", Verdict(r["HOLD_50"]));
        Assert.Equal("INCONCLUSIVE", Verdict(r["DISTANCE_400"]));
    }

    // reach: unit. A pose-frame change means a localization the recomputed travel cannot follow.
    [Fact]
    public void FrameChangeIsNotGraded()
    {
        var wire = Engine().Select(m => !m.Outbound && m.AtMs == DriveStart + 60 ? State(m.AtMs, 60, false, frame: 1) : m).ToList();
        Assert.Equal("INCONCLUSIVE", Verdict(Grade(wire)["HOLD_50"]));
    }

    // reach: unit. Not on the contacts at the start: nothing can be graded.
    [Fact]
    public void OffTheContactsIsNotGraded()
    {
        var wire = Engine().Select(m => !m.Outbound && m.AtMs < DriveStart ? State(m.AtMs, 0, false) : m).ToList();
        var r = Grade(wire);
        foreach (var id in new[] { "ON_CHARGER", "HOLD_50", "DISTANCE_400", "PLATFORM_EXIT" }) Assert.Equal("INCONCLUSIVE", Verdict(r[id]));
    }

    // reach: unit. SetOnChargerPlatform stores arg || contacts (0x00511D4C..0x00511D6A): no exit while the contacts are on.
    [Fact]
    public void PlatformClearingBeforeTheContactsDropIsAProblem()
    {
        var r = Grade(Engine(), new[] { DriveStart + 5 });
        Assert.Equal("MISMATCH", Verdict(r["PLATFORM_EXIT"]));
        Assert.Equal("MISMATCH", Verdict(Grade(Engine(), new[] { DriveStart + 81.5, DriveStart + 85 })["PLATFORM_EXIT"]));
    }

    // reach: unit. After the drive window the operator re-docks and lifts Cozmo: a second platform exit, its 400, a
    // frame change (SetNewPose ++frame 0x00515152). None of it is graded. The old grader (whole capture) gave
    // PLATFORM_EXIT MISMATCH (exits > 1) and HOLD_50 INCONCLUSIVE (frame change).
    [Fact]
    public void EvidenceAfterTheDriveWindowIsNotGraded()
    {
        var wire = Engine();
        wire.Add(State(400, 0, true)); wire.Add(Threshold(401, 50));
        wire.Add(State(500, 0, false, frame: 1)); wire.Add(Threshold(501, 400));
        var r = M13DriveOffHw.GradeThresholds(new(wire, DriveStart, true, new[] { DriveStart + 81.5, 500.5 }, false, DriveEndMs: 300));
        foreach (var id in new[] { "ON_CHARGER", "HOLD_50", "DISTANCE_400", "PLATFORM_EXIT" }) Assert.Equal("MATCH", Verdict(r[id]));
    }

    // reach: unit. IncrementSuspiciousCliffCount (0x0063451A..0x006345AA) sends max(cache-250,150): from 400 only 150.
    // A 150 between the distance 400 and the exit 400 leaves the 400 count and the first-is-400 rule intact.
    [Fact]
    public void IncrementBetweenTheTwoFourHundredsStillMatches()
    {
        var r = Grade(Engine(x => x == 60 ? (ushort)150 : null));
        Assert.Equal("MATCH", Verdict(r["DISTANCE_400"]));
    }

    // reach: unit. UpdateInternal 0x005C0DA8..0x005C0E32 returns 2 only off the platform with no current action.
    [Fact]
    public void BehaviourEndFollowsUpdateInternal()
    {
        Assert.Equal("MATCH", Verdict(M13DriveOffHw.GradeBehaviour(new(true, true, true, false, false, false, "Success"), true)));
        Assert.Equal("MISMATCH", Verdict(M13DriveOffHw.GradeBehaviour(new(true, true, true, false, true, false, "Success"), true)));
        Assert.Equal("MISMATCH", Verdict(M13DriveOffHw.GradeBehaviour(new(true, true, true, false, false, true, null), true)));
        Assert.Equal("MISMATCH", Verdict(M13DriveOffHw.GradeBehaviour(new(false, false, false, false, true, false, null), true)));
        Assert.Equal("INCONCLUSIVE", Verdict(M13DriveOffHw.GradeBehaviour(new(true, true, false, true, true, true, null), true)));
    }

    // reach: unit. The SevereNeeds writer is a recorded gap; its MISSING report makes the row a known missing piece.
    [Fact]
    public void SevereNeedsMissingReportIsAKnownGap()
    {
        var line = "MISSING: M13-017 " + M13DriveOffHw.SevereNeedsMissingText + " (R-M15-SEVERE-INPUT, row 39)";
        Assert.Equal("KNOWN_GAP", Verdict(M13DriveOffHw.GradeDrivingAnimations(new[] { line }, true)));
        Assert.Equal("INCONCLUSIVE", Verdict(M13DriveOffHw.GradeDrivingAnimations(Array.Empty<string>(), true)));
    }

    // reach: unit. Every check has a cited comparison and names only real records.
    [Fact]
    public void ChecksAreCitedAndRecordsExist()
    {
        Assert.Null(M13DriveOffHw.CheckDefinitionProblem());
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "re-analysis", "fidelity_manifest.json"))) dir = dir.Parent;
        Assert.NotNull(dir);
        var warnings = new List<string>();
        Assert.Equal(M13DriveOffHw.RecordIds.Length, M13DriveOffHw.LoadRecords(Path.Combine(dir!.FullName, "re-analysis", "fidelity_manifest.json"), warnings).Count);
    }
}
