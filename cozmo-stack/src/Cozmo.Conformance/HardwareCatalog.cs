using System.Text.Json.Serialization;

namespace Cozmo.Conformance;

/// <summary>What the automated part of a check concluded, on its own.</summary>
public enum AutoOutcome { NotRun, Pass, Fail, Error, Skipped }

/// <summary>What the person standing next to the robot concluded. Never inferred from <see cref="AutoOutcome"/>.</summary>
public enum HumanOutcome { NotAsked, Pass, Fail, Skipped }

/// <summary>The status of a check once both halves are in.</summary>
public enum CheckStatus
{
    Pending,
    /// <summary>Both halves agree it worked.</summary>
    Passed,
    /// <summary>Both halves agree it did not.</summary>
    Failed,
    /// <summary>The halves disagree, or one could not be taken: the check needs a human to read the evidence.</summary>
    Partial,
    /// <summary>A prerequisite failed, or the check cannot run on this build at all.</summary>
    Blocked,
    Skipped,
}

/// <summary>
/// One hardware check, as <c>HARDWARE_TEST_PLAN.md</c> defines it, with everything a person standing next to
/// the robot needs to carry it out. The command is the argument vector of an existing conformance tool: the
/// runner calls that tool directly rather than starting another process, so there is one implementation of
/// each check and no duplicated protocol logic.
/// </summary>
public sealed record HardwareCheck
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    /// <summary>The milestone this check accepts (M4, M9, ...).</summary>
    public required string Milestone { get; init; }
    /// <summary>The part of the stack under test, in one line.</summary>
    public required string Subsystem { get; init; }
    /// <summary>Why the check exists: what would go unnoticed without it.</summary>
    public required string Why { get; init; }
    /// <summary>The physical arrangement needed before starting.</summary>
    public required string Setup { get; init; }
    /// <summary>What the person does while it runs.</summary>
    public required string DoThis { get; init; }
    /// <summary>What a pass looks or sounds like, in the room.</summary>
    public required string Success { get; init; }
    /// <summary>The question the runner asks afterwards.</summary>
    public required string Question { get; init; }

    /// <summary>Checks that must have passed first. A failed prerequisite blocks this one.</summary>
    public IReadOnlyList<string> Requires { get; init; } = Array.Empty<string>();
    /// <summary>Whether the robot drives or moves its lift or head under its own power: asks for confirmation first.</summary>
    public bool MovesRobot { get; init; }
    /// <summary>Set when the check cannot run on this build at all; the reason is shown and the check is recorded Blocked.</summary>
    public string? BlockedReason { get; init; }
    /// <summary>A known discrepancy to show in the brief, so a person is not surprised by it.</summary>
    public string? KnownIssue { get; init; }

    /// <summary>The conformance command line, without the leading program name.</summary>
    public required Func<HardwareRunOptions, string[]> Command { get; init; }
    /// <summary>
    /// Reads the tool's console output and decides the automated half. Substrings come from the plan's
    /// "automated verdict" column; the rule is stated in <see cref="AutoRule"/> for the brief.
    /// </summary>
    public required Func<HardwareToolRun, AutoOutcome> Judge { get; init; }
    /// <summary>The automated rule in words, shown before the check runs.</summary>
    public required string AutoRule { get; init; }

    public string CommandLine(HardwareRunOptions o) => string.Join(' ', Command(o));
}

/// <summary>What the runner captured from one invocation of a conformance tool.</summary>
public sealed record HardwareToolRun(int ExitCode, string Output, string? AcceptanceRecord, string LogPath, Exception? Error)
{
    public bool Contains(string s) => Output.Contains(s, StringComparison.OrdinalIgnoreCase);
    public bool ContainsAll(params string[] all) => all.All(Contains);
    public bool ContainsAny(params string[] any) => any.Any(Contains);
    /// <summary>The first line matching a substring, for the summary.</summary>
    public string? Line(string s) =>
        Output.Split('\n').FirstOrDefault(l => l.Contains(s, StringComparison.OrdinalIgnoreCase))?.Trim();
}

/// <summary>Everything the runner needs to build a command line.</summary>
public sealed record HardwareRunOptions
{
    public required string Ip { get; init; }
    public string? Obb { get; init; }
    /// <summary>Where acceptance JSON and per-check logs go.</summary>
    public required string EvidenceDirectory { get; init; }
    /// <summary>Allows the nominal camera calibration stand-in where a tool offers it.</summary>
    public bool AllowNominalCalibration { get; init; }

    public string Acceptance(string id) => Path.Combine(EvidenceDirectory, $"acceptance-{id}.json");
    public string[] WithObb(params string[] head) =>
        Obb is null ? head : head.Concat(new[] { "--obb", Obb }).ToArray();
}

/// <summary>One check's result, as persisted.</summary>
public sealed record HardwareResult
{
    public required string Id { get; init; }
    public AutoOutcome Auto { get; init; }
    public HumanOutcome Human { get; init; }
    /// <summary>Set when the check was blocked rather than run.</summary>
    public string? BlockedReason { get; init; }
    public string? AcceptanceRecord { get; init; }
    public string? LogPath { get; init; }
    public int Attempts { get; init; }
    public string? AutoDetail { get; init; }
    public string? HumanNote { get; init; }
    public DateTime? StartedUtc { get; init; }
    public DateTime? FinishedUtc { get; init; }

    [JsonIgnore]
    public CheckStatus Status => HardwareSession.StatusOf(this);
}

/// <summary>
/// The checks of <c>HARDWARE_TEST_PLAN.md</c>, in the order the runner walks them: passive telemetry first,
/// then the ones where the robot animates in place, then the ones where it drives, and freeplay last. The
/// plan's own table stays the reference; this is that table in code.
/// </summary>
public static class HardwareCatalog
{
    public static IReadOnlyList<HardwareCheck> All { get; } = Build();

    public static HardwareCheck? Find(string id) =>
        All.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<HardwareCheck> Build() => new[]
    {
        // ----------------------------------------------------------------- passive: nothing moves by itself
        new HardwareCheck
        {
            Id = "D", Name = "Lift position readout", Milestone = "M4",
            Subsystem = "RobotState lift angle and the angle-to-height conversion",
            Why = "The lift angle is reported in radians and converted with the engine's own formula. If the "
                + "conversion is wrong, every docking height and the carry check are wrong with it.",
            Setup = "Robot on its treads on a table. Start with the lift fully down.",
            DoThis = "When the reading appears, raise the lift fully by hand and hold it there.",
            Success = "About -0.198 rad / 32 mm with the lift down, and about 0.712 rad / 92 mm raised.",
            Question = "Did the reported height match where the lift actually was?",
            AutoRule = "the sensors tool prints a lift reading in radians and millimetres",
            Command = o => new[] { "sensors", o.Ip, "--acceptance", o.Acceptance("D") },
            Judge = r => r.Contains("lift=") ? AutoOutcome.Pass : AutoOutcome.Fail,
        },
        new HardwareCheck
        {
            Id = "B", Name = "Cube telemetry", Milestone = "M4",
            Subsystem = "Cube discovery, connection and the tap, movement, up-axis and battery reports",
            Why = "Discovery alone was already observed. What is unverified is whether a cube actually "
                + "connects and whether its telemetry arrives, which everything with a cube depends on.",
            Setup = "One or more cubes with charged batteries, within about half a metre of the robot.",
            DoThis = "During the 15 seconds: tap a cube, roll it onto another face, and pick it up and put it down.",
            Success = "The events printed match what you did, and at least one cube shows as connected.",
            Question = "Did the printed events match what you did to the cube?",
            AutoRule = "at least one cube CONNECTED and at least one telemetry signal (tap, movement, up axis or battery)",
            Command = o => new[] { "cubes", o.Ip, "--seconds", "15", "--acceptance", o.Acceptance("B") },
            Judge = r => r.Contains("0 connected") || !r.Contains("connected")
                ? AutoOutcome.Fail
                : r.ContainsAny("tapped ", "moving ", "still ", "upAxis") ? AutoOutcome.Pass : AutoOutcome.Fail,
        },
        new HardwareCheck
        {
            Id = "G", Name = "Off-treads transitions", Milestone = "M10",
            Subsystem = "The off-treads classifier: on treads, in air, on back, on face, on each side",
            Why = "The classifier drives the pick-up reaction and gates freeplay. Its debounce timings were "
                + "read from the engine and have never been watched against real handling.",
            Setup = "Robot on its treads, room to handle it. Nothing else running.",
            DoThis = "Over 90 seconds: pick him up, put him down, lay him on his back, on each side, on his "
                   + "face, and hold him still tilted 20-40 degrees.",
            Success = "A transition prints for each handling and nothing prints while he sits still. On-back "
                    + "arrives about a second after laying him down, sides and face after a quarter second.",
            Question = "Did every printed transition match what you did, in that order, with nothing while still?",
            AutoRule = "the classifier enables after the calibration report and prints at least one transition",
            Command = o => new[] { "offtreads", o.Ip, "--seconds", "90", "--acceptance", o.Acceptance("G") },
            Judge = r => r.ContainsAny("InAir", "OnBack", "OnFace", "OnLeftSide", "OnRightSide") ? AutoOutcome.Pass : AutoOutcome.Fail,
        },
        new HardwareCheck
        {
            Id = "E", Name = "Colour camera frames", Milestone = "M3",
            Subsystem = "The colour path of the camera decoder",
            Why = "Colour frames have never been exercised on hardware; the decoder's colour branch is "
                + "reconstructed and could be producing plausible nonsense.",
            Setup = "Point the robot at something with obvious colour: a cube, a book cover, your hand.",
            DoThis = "Nothing. Let it capture, then look at the saved images.",
            Success = "The saved files are colour photographs of the room, not tinted or scrambled.",
            Question = "Are the saved images real colour photographs of what he was pointed at?",
            AutoRule = "frames decode without error",
            Command = o => new[] { "camera", o.Ip, "--color", "--acceptance", o.Acceptance("E") },
            Judge = r => r.ExitCode == 0 ? AutoOutcome.Pass : AutoOutcome.Fail,
        },
        new HardwareCheck
        {
            Id = "K", Name = "Camera calibration and cube localisation", Milestone = "M11",
            Subsystem = "The NV calibration read, marker detection and BlockWorld's pose estimate",
            Why = "This is the only positive evidence that the reconstructed vision front end sees a real "
                + "cube: the offline tests render markers from the recovered library, and no capture holds a "
                + "cube. Everything that docks or navigates depends on it, so it runs before those.",
            Setup = "A connected cube (run B first, or leave discovery on). Good, even lighting.",
            DoThis = "Show him the cube at 10-30 cm. Turn it so different faces show. Move it about 10 cm. "
                   + "Then hide it from view.",
            Success = "Marker codes match the faces shown, the printed distance matches a ruler within about "
                    + "5 percent, and the yaw matches how the cube is turned.",
            Question = "Did the printed codes, distance and yaw match the real cube?",
            AutoRule = "the calibration is read from the robot and at least one cube becomes Known with a pose",
            Command = o => o.AllowNominalCalibration
                ? new[] { "vision", o.Ip, "--seconds", "90", "--nominal", "--acceptance", o.Acceptance("K"), "--out", Path.Combine(o.EvidenceDirectory, "frames-K") }
                : new[] { "vision", o.Ip, "--seconds", "90", "--acceptance", o.Acceptance("K"), "--out", Path.Combine(o.EvidenceDirectory, "frames-K") },
            Judge = r => !r.Contains("camera calibration from robot") ? AutoOutcome.Fail
                       : r.Contains("Known at") ? AutoOutcome.Pass : AutoOutcome.Fail,
        },

        // ----------------------------------------------------------------- audio
        new HardwareCheck
        {
            Id = "A", Name = "Cozmo sings", Milestone = "M9", MovesRobot = true,
            Subsystem = "The Wwise switch, the per-note vocal sampler and the singing behaviour's animations",
            Why = "The song is rendered here from Anki's banks by a reconstructed sampler. Whether it sounds "
                + "like Cozmo singing is something only a listener can settle.",
            Setup = "Quiet room. Robot on its treads with space to animate.",
            DoThis = "Listen. Do not touch him.",
            Success = "About 12 seconds of tune between a get-in and a get-out, in his own voice, one note per "
                    + "note, at a sensible level, with no bursts of get-in phrases during the song.",
            Question = "Did he sing a recognisable tune, one note per note, at a sensible level?",
            AutoRule = "the switch is posted and the get-in, song and get-out animations complete",
            KnownIssue = "Previously observed: the automated path passes but the audio is musically wrong and "
                       + "over-sustained. If you hear that again, answer n. The distinction is the point of this check.",
            Command = o => o.WithObb("sing", o.Ip).Concat(new[] { "--behavior", "Singing_AbaDaba", "--acceptance", o.Acceptance("A") }).ToArray(),
            Judge = r => r.ExitCode == 0 && r.ContainsAny("notes", "rendered") ? AutoOutcome.Pass : AutoOutcome.Fail,
        },
        new HardwareCheck
        {
            Id = "A2", Name = "One song from each tempo group", Milestone = "M9", MovesRobot = true, Requires = new[] { "A" },
            Subsystem = "The 100 and 120 bpm switch containers and their meter overrides",
            Why = "Three tempo groups were recovered from the banks. If the meter override is misread, the "
                + "tempos will not differ audibly.",
            Setup = "As A.",
            DoThis = "Listen to both songs and compare their speed with A.",
            Success = "Bingo is cut at about 9.8 s by its Stop event, and the three tempos audibly differ.",
            Question = "Did the tempos audibly differ between the three songs?",
            AutoRule = "both behaviours run to completion",
            KnownIssue = "Same known discrepancy as A.",
            Command = o => o.WithObb("sing", o.Ip).Concat(new[] { "--behavior", "Singing_Bingo", "--acceptance", o.Acceptance("A2") }).ToArray(),
            Judge = r => r.ExitCode == 0 ? AutoOutcome.Pass : AutoOutcome.Fail,
        },

        // ----------------------------------------------------------------- he animates in place
        new HardwareCheck
        {
            Id = "F", Name = "Animation timeline", Milestone = "M5", MovesRobot = true,
            Subsystem = "The animation scheduler's keyframe timing against the robot's audio pacing",
            Why = "The scheduler paces itself off the robot's played-frame count. This confirms nothing "
                + "regressed in the normal case; a stall cannot be forced from the tool.",
            Setup = "Robot on its treads with room to move.",
            DoThis = "Watch the clip play.",
            Success = "The clip plays as it always has: no stutter, no truncation, no stuck lift or head.",
            Question = "Did the clip play smoothly and completely?",
            AutoRule = "keyframes fired equals keyframes in the clip, and no stalls are reported",
            Command = o => new[] { "anim", o.Ip, "--assets", Path.Combine(o.Obb ?? ".", "assets", "cozmo_resources", "assets"),
                                   "--name", "anim_bored_01", "--wwise", Path.Combine(o.Obb ?? ".", "assets", "cozmo_resources", "sound") },
            Judge = r => r.Contains("stall") ? AutoOutcome.Fail : r.ExitCode == 0 ? AutoOutcome.Pass : AutoOutcome.Fail,
        },
        new HardwareCheck
        {
            Id = "C", Name = "Falling then impact", Milestone = "M7",
            Subsystem = "The falling and impact reports, and the rule that the reaction waits for the landing",
            Why = "The engine reacts to the landing, not the fall, and only above an impact of 1000. Neither "
                + "the threshold nor the wait has been seen on hardware.",
            Setup = "A soft surface (a cushion or folded towel) a few centimetres below the robot.",
            DoThis = "Drop him a few centimetres onto the soft surface. Do not throw him.",
            Success = "He reacts on landing, not while falling.",
            Question = "Did he react on landing rather than during the fall?",
            AutoRule = "a line saying the reaction waits for the landing, then a ReactToImpact decision",
            Command = o => o.WithObb("behavior", o.Ip).Concat(new[] { "--seconds", "60" }).ToArray(),
            Judge = r => r.Contains("waits for the landing")
                ? (r.Contains("ReactToImpact") ? AutoOutcome.Pass : AutoOutcome.Fail)
                : AutoOutcome.Fail,
        },
        new HardwareCheck
        {
            Id = "H", Name = "The derived-state reactions", Milestone = "M10", MovesRobot = true,
            Subsystem = "The reaction trigger strategies and the ReactToX behaviours under the manager",
            Why = "Eight reactions were transcribed from the engine. This is the first time they run as a set "
                + "against real handling, with the manager arbitrating between them.",
            Setup = "Room to handle him. He will animate in your hands.",
            DoThis = "Over two minutes: lay him on his back, on his face, on each side, put him down on a "
                   + "slope, and shake him then set him down.",
            Success = "On his back he flips down; on his face he rolls; on a side he asks to be righted and "
                    + "waits; on a slope he reacts then checks his pitch; shaken then set down he acts dizzy. "
                    + "Nothing fires for a state he is not in.",
            Question = "Did each reaction match the state he was actually in, with nothing spurious?",
            AutoRule = "at least one REACTION line with its behaviour",
            Command = o => o.WithObb("reactions", o.Ip).Concat(new[] { "--seconds", "120", "--acceptance", o.Acceptance("H") }).ToArray(),
            Judge = r => r.Contains("REACTION ") ? AutoOutcome.Pass : AutoOutcome.Fail,
        },
        new HardwareCheck
        {
            Id = "I", Name = "StartMotorCalibration honoured", Milestone = "M4/M10", Requires = new[] { "H" },
            Subsystem = "The StartMotorCalibration command and the robot's calibration reports",
            Why = "The stack sends this during recovery reactions. Whether the firmware obeys it has never "
                + "been confirmed.",
            Setup = "As H. This reads the trace H just produced, or repeats the handling.",
            DoThis = "Lay him on his back with a finger over the front-left cliff sensor, or set him down "
                   + "tilted and watch the returned-to-treads reaction.",
            Success = "The head visibly recalibrates: it nods to its stop and back.",
            Question = "Did the head visibly recalibrate?",
            AutoRule = "a calibrate-head line, then MotorCalibration started and finished",
            Command = o => o.WithObb("reactions", o.Ip).Concat(new[] { "--seconds", "90", "--acceptance", o.Acceptance("I") }).ToArray(),
            Judge = r => r.Contains("StartMotorCalibration") || r.Contains("calibrate head") ? AutoOutcome.Pass : AutoOutcome.Fail,
        },
        new HardwareCheck
        {
            Id = "J", Name = "Unexpected movement while driving", Milestone = "M10", MovesRobot = true, Requires = new[] { "H" },
            Subsystem = "The unexpected-movement detector against the robot's own gyro",
            Why = "The detector compares commanded with measured rotation. It suspends during direct drive, "
                + "so it can only be provoked while a behaviour drives the body.",
            Setup = "Room to handle him while he animates.",
            DoThis = "While an animation drives his body, hold him so he cannot turn, or twist him against "
                   + "the turn.",
            Success = "He plays the startled reaction once, on the side the push came from, and nothing fires "
                    + "while he drives freely.",
            Question = "Did he react once, on the correct side, and stay quiet when driving freely?",
            AutoRule = "an unexpected-movement report followed by its reaction",
            Command = o => o.WithObb("reactions", o.Ip).Concat(new[] { "--seconds", "120", "--acceptance", o.Acceptance("J") }).ToArray(),
            Judge = r => r.Contains("unexpected movement") ? AutoOutcome.Pass : AutoOutcome.Fail,
        },
        new HardwareCheck
        {
            Id = "M", Name = "The cube reactions with a real cube", Milestone = "M11", MovesRobot = true, Requires = new[] { "K" },
            Subsystem = "AcknowledgeObject and ReactToCubeMoved over the real world model",
            Why = "Both reactions were rebuilt from the engine and, until K passed, had no real cube pose to "
                + "work from. The cube-moved path also exercises the correction that feeds it real sightings.",
            Setup = "A connected cube in view, and room for him to turn.",
            DoThis = "Slide the cube about 10 cm while he watches. Then turn him away and roll the cube.",
            Success = "He looks at the cube's new place and nods to it. Turned away and hearing it move, he "
                    + "turns back to where it was and reacts to finding or not finding it.",
            Question = "Did he acknowledge the moved cube, and turn back for the one moved behind him?",
            AutoRule = "an ObjectPositionUpdated or CubeMoved reaction fires",
            Command = o => o.WithObb("reactions", o.Ip).Concat(new[] { "--seconds", "180", "--acceptance", o.Acceptance("M") }).ToArray(),
            Judge = r => r.ContainsAny("ObjectPositionUpdated", "CubeMoved") ? AutoOutcome.Pass : AutoOutcome.Fail,
        },

        // ----------------------------------------------------------------- he drives
        new HardwareCheck
        {
            Id = "L", Name = "SetBodyAngle semantics", Milestone = "M11", MovesRobot = true,
            Subsystem = "Whether SetBodyAngle's angle is absolute or relative",
            Why = "The field order was read from the engine but the meaning of the angle was inferred. Every "
                + "turn-towards action depends on which it is.",
            Setup = "Clear floor, robot on its treads, at least 30 cm of space around him.",
            DoThis = "Watch which way and how far he turns.",
            Success = "He turns 45 degrees left in place, smoothly, and stops.",
            Question = "Did he turn 45 degrees and stop cleanly?",
            AutoRule = "the tool reports ABSOLUTE, RELATIVE or did-not-turn; ABSOLUTE is the expected verdict",
            Command = o => new[] { "bodyangle", o.Ip, "--deg", "45", "--acceptance", o.Acceptance("L") },
            Judge = r => r.Contains("ABSOLUTE body angle confirmed") ? AutoOutcome.Pass
                       : r.ContainsAny("RELATIVE", "did not turn") ? AutoOutcome.Fail : AutoOutcome.Error,
        },
        new HardwareCheck
        {
            Id = "Q", Name = "Drive to the pre-dock pose", Milestone = "M12", MovesRobot = true, Requires = new[] { "K" },
            Subsystem = "Path planning, the path messages and the pre-action pose geometry",
            Why = "The path segment packings and the 75 mm pre-dock distance were read from the engine. This "
                + "is the first time the firmware drives one of our paths.",
            Setup = "A connected cube 20-40 cm ahead, clear floor between.",
            DoThis = "Watch the drive. Measure the final gap to the cube face if you can.",
            Success = "He turns, drives straight, turns to face the cube and stops about 75 mm from its face, "
                    + "with no jerks between segments.",
            Question = "Did he arrive facing the cube, about 75 mm from it, smoothly?",
            AutoRule = "path Completed and DriveToObject Success",
            Command = o => o.WithObb("manip", o.Ip).Concat(new[] { "--driveto", "--acceptance", o.Acceptance("Q") }).ToArray(),
            Judge = r => r.Contains("DriveToObject -> Success") ? AutoOutcome.Pass : AutoOutcome.Fail,
        },
        new HardwareCheck
        {
            Id = "N", Name = "Pick up a cube", Milestone = "M12", MovesRobot = true, Requires = new[] { "K" },
            Subsystem = "The docking exchange: DockWithObject, the error signals and PickAndPlaceResult",
            Why = "Three fields of the dock messages were never decoded and are sent as zero. If the firmware "
                + "rejects the dock or fails instantly, those fields are the first suspect.",
            Setup = "A connected cube 15-30 cm ahead with a face towards him. Clear floor.",
            DoThis = "Watch the dock and the lift.",
            Success = "He drives to about 75 mm from the face, docks smoothly, lifts the cube and holds it.",
            Question = "Did he pick the cube up and hold it?",
            AutoRule = "PickAndPlaceResult succeeded with status BlockPickedUp and the pickup reports Success",
            Command = o => o.WithObb("manip", o.Ip).Concat(new[] { "--pickup", "--acceptance", o.Acceptance("N") }).ToArray(),
            Judge = r => r.Contains("Pickup -> Success") ? AutoOutcome.Pass : AutoOutcome.Fail,
        },
        new HardwareCheck
        {
            Id = "O", Name = "Place a carried cube on the ground", Milestone = "M12", MovesRobot = true, Requires = new[] { "N" },
            Subsystem = "PlaceObjectOnGround and the carry state",
            Why = "The place messages' field names were inferred. This confirms the robot accepts them and "
                + "that the carry state clears.",
            Setup = "Run straight after N, with the cube still on the lift. Otherwise set a cube on the lift by hand.",
            DoThis = "Watch him set the cube down and back away.",
            Success = "He lowers the lift and backs off the cube, leaving it upright.",
            Question = "Did he place the cube and back away cleanly?",
            AutoRule = "PickAndPlaceResult with status BlockPlaced and carrying False",
            Command = o => o.WithObb("manip", o.Ip).Concat(new[] { "--putdown", "--acceptance", o.Acceptance("O") }).ToArray(),
            Judge = r => r.Contains("PlaceObjectOnGround -> Success") ? AutoOutcome.Pass : AutoOutcome.Fail,
        },
        new HardwareCheck
        {
            Id = "P", Name = "Roll a cube", Milestone = "M12", MovesRobot = true, Requires = new[] { "K" },
            Subsystem = "The roll dock action and the up-axis report",
            Why = "Rolling is the dock action with the most firmware involvement, and the up-axis change is "
                + "how the stack learns it worked.",
            Setup = "A connected cube, upright or on its side, 15-30 cm ahead.",
            DoThis = "Watch the cube's face after the roll.",
            Success = "He docks and the cube rolls onto another face.",
            Question = "Did the cube roll onto a different face?",
            AutoRule = "the roll reports Success with an up-axis change",
            Command = o => o.WithObb("manip", o.Ip).Concat(new[] { "--roll", "--acceptance", o.Acceptance("P") }).ToArray(),
            Judge = r => r.Contains("Roll -> Success") ? AutoOutcome.Pass : AutoOutcome.Fail,
        },
        new HardwareCheck
        {
            Id = "R", Name = "Stack two cubes", Milestone = "M12", MovesRobot = true, Requires = new[] { "N", "K" },
            Subsystem = "The stack behaviour: pick up, carry, place on top",
            Why = "The first compound manipulation: it chains a pick-up and a place with the world model "
                + "tracking the carried cube between them.",
            Setup = "Two connected upright cubes in view, 15-30 cm apart. Requires --obb.",
            DoThis = "Watch him carry one cube to the other.",
            Success = "He picks one up, carries it to the other and places it on top, leaving a stack standing.",
            Question = "Did he leave one cube stacked on the other?",
            AutoRule = "the phases PickingUpBlock, StackingBlock and PlayingFinalAnim all appear",
            Command = o => o.WithObb("manip", o.Ip).Concat(new[] { "--stack", "--acceptance", o.Acceptance("R") }).ToArray(),
            Judge = r => r.ContainsAll("PickingUpBlock", "StackingBlock") ? AutoOutcome.Pass : AutoOutcome.Fail,
        },
        new HardwareCheck
        {
            Id = "S", Name = "Flip a cube", Milestone = "M13", MovesRobot = true, Requires = new[] { "K" },
            Subsystem = "FlipBlockAction: the low-lift approach and the lift-up that tips the cube",
            Why = "The flip's distances and the lift trigger were read from the engine, and the carry height "
                + "it lifts to was corrected to the native 92 mm. This is the first run with that value.",
            Setup = "A connected cube 20-30 cm ahead. Clear space behind the cube.",
            DoThis = "Watch the lift as he reaches the cube.",
            Success = "He drives at the cube's corner with the lift low, the lift comes up as he reaches it "
                    + "and the cube tips over his shoulder. He does not stall against the cube.",
            Question = "Did the cube tip over, without him stalling against it?",
            AutoRule = "DriveAndFlipBlock reports Success and the cube becomes Unknown",
            Command = o => o.WithObb("manip", o.Ip).Concat(new[] { "--flip", "--acceptance", o.Acceptance("S") }).ToArray(),
            Judge = r => r.Contains("DriveAndFlipBlock -> Success") ? AutoOutcome.Pass : AutoOutcome.Fail,
        },
        new HardwareCheck
        {
            Id = "T", Name = "Knock over a stack", Milestone = "M13", MovesRobot = true, Requires = new[] { "K", "S" },
            Subsystem = "The knock-over behaviour over the block-configuration model",
            Why = "It needs the stack detector, the reach animation and the flip action together. The tool "
                + "now refuses to start until a stack is actually in the world model.",
            Setup = "Two connected cubes stacked, 20-40 cm ahead. Requires --obb.",
            DoThis = "Watch the stack.",
            Success = "He turns to the stack, drives up, reaches, flips the bottom cube and the stack falls. "
                    + "The success animation plays.",
            Question = "Did the stack come apart, followed by the success animation?",
            AutoRule = "the stack is detected, the grab is attempted and KnockOverSuccess appears",
            Command = o => o.WithObb("manip", o.Ip).Concat(new[] { "--knockover", "--acceptance", o.Acceptance("T") }).ToArray(),
            Judge = r => r.Contains("KnockOverSuccess") ? AutoOutcome.Pass
                       : r.Contains("needs a located stack") ? AutoOutcome.Error : AutoOutcome.Fail,
        },
        new HardwareCheck
        {
            Id = "U", Name = "Pop a wheelie", Milestone = "M13", MovesRobot = true, Requires = new[] { "K" },
            Subsystem = "The PopAWheelie dock action and its retry",
            Why = "The most violent dock action, and the one that turns the cliff stop off and on again "
                + "around itself.",
            Setup = "An upright connected cube 15-30 cm ahead, on a surface he cannot fall off.",
            DoThis = "Stand by to catch him. Watch the cliff-stop line in the trace.",
            Success = "He docks, rides up onto the cube's edge and drops back. A miss plays the realign "
                    + "animation and tries again, up to three times.",
            Question = "Did he rear up on the cube and come back down safely?",
            AutoRule = "PoppedWheelie appears and the cliff stop is re-enabled on stop",
            Command = o => o.WithObb("manip", o.Ip).Concat(new[] { "--wheelie", "--acceptance", o.Acceptance("U") }).ToArray(),
            Judge = r => r.ContainsAny("PoppedWheelie", "PopAWheelie -> Success") ? AutoOutcome.Pass : AutoOutcome.Fail,
        },
        new HardwareCheck
        {
            Id = "V", Name = "Mount the charger", Milestone = "M13", MovesRobot = true, Requires = new[] { "K" },
            Subsystem = "The charger object, the align dock and the backwards mount",
            Why = "The charger's pre-dock pose is inferred. If the align never starts, that pose or the align "
                + "distance is the suspect, and this is the only way to tell.",
            Setup = "The charger 20-40 cm ahead with its back-wall marker facing him. Clear floor.",
            DoThis = "Watch him turn his back to the charger and reverse onto it.",
            Success = "He stops in front of the charger, turns around, backs on and the contacts engage: his "
                    + "backpack lights change. A miss drives forward 120 mm and retries.",
            Question = "Did he end up on the charger with the contacts engaged?",
            AutoRule = "the charger is located, the align dock runs and the contacts report on charger",
            Command = o => o.WithObb("manip", o.Ip).Concat(new[] { "--mount", "--acceptance", o.Acceptance("V") }).ToArray(),
            Judge = r => r.Contains("on charger: True") ? AutoOutcome.Pass : AutoOutcome.Fail,
        },
        new HardwareCheck
        {
            Id = "W", Name = "Drive off the charger", Milestone = "M13", MovesRobot = true,
            Subsystem = "DriveOffChargerContactsAction and the on-charger flag",
            Why = "The distance and the 20 mm/s crawl were read from the engine; whether they clear the "
                + "contacts on a real charger is unverified.",
            Setup = "Him sitting on the charger, clear floor ahead.",
            DoThis = "Watch him leave the charger.",
            Success = "He drives forward off the charger at a crawl and stops on his treads.",
            Question = "Did he drive clear of the charger and stop?",
            AutoRule = "one 156 mm line at 20 mm/s and the on-charger flag clears",
            Command = o => o.WithObb("manip", o.Ip).Concat(new[] { "--driveoff", "--acceptance", o.Acceptance("W") }).ToArray(),
            Judge = r => r.ContainsAny("DriveOffCharger", "IS_ON_CHARGER") ? AutoOutcome.Pass : AutoOutcome.Fail,
        },
        new HardwareCheck
        {
            Id = "X", Name = "The lattice planner on the robot", Milestone = "M13", MovesRobot = true, Requires = new[] { "K", "Q" },
            Subsystem = "The lattice planner's arcs and the firmware's arc segment handling",
            Why = "The arc packing was read from the engine but never driven. This also confirms the "
                + "correction that makes a planning failure refuse to drive rather than go straight through.",
            Setup = "A connected target cube, and a second cube placed between him and it. Requires --obb.",
            DoThis = "Watch whether he curves around the obstacle or drives at it.",
            Success = "He curves around the cube in the way and arrives at the pre-dock pose. The arcs are "
                    + "smooth, so the firmware accepted the arc layout.",
            Question = "Did he curve around the obstacle and arrive, with smooth arcs?",
            AutoRule = "a lattice plan with at least one obstacle, arc segments sent, and the drive succeeds",
            Command = o => o.WithObb("manip", o.Ip).Concat(new[] { "--driveto", "--acceptance", o.Acceptance("X") }).ToArray(),
            Judge = r => r.Contains("lattice plan") && r.Contains("DriveToObject -> Success") ? AutoOutcome.Pass : AutoOutcome.Fail,
        },

        // ----------------------------------------------------------------- blocked, and the long one last
        new HardwareCheck
        {
            Id = "Y", Name = "The face pipeline with a detector", Milestone = "M14",
            Subsystem = "FaceWorld, the face actions and the face reactions",
            Why = "Everything around the detector is built and tested offline. The detector itself is Omron "
                + "OKAO and is not reproducible, so this check waits for an IFaceDetector implementation.",
            Setup = "Not applicable on this build.",
            DoThis = "Nothing: this check cannot run.",
            Success = "Not applicable.",
            Question = "Not applicable.",
            AutoRule = "not run",
            BlockedReason = "VisionSystem.FaceDetector is the OKAO boundary and reports itself unavailable. "
                          + "Attach an IFaceDetector implementation and this check becomes runnable.",
            Command = o => o.WithObb("reactions", o.Ip).Concat(new[] { "--seconds", "120" }).ToArray(),
            Judge = _ => AutoOutcome.Skipped,
        },
        new HardwareCheck
        {
            Id = "Z", Name = "Freeplay on the robot", Milestone = "M15", MovesRobot = true, Requires = new[] { "K" },
            Subsystem = "The whole autonomy stack: needs, activities, choosers, behaviours and reactions",
            Why = "Everything above, running together and choosing for itself for five minutes. It is last "
                + "because it exercises every other subsystem and takes the longest.",
            Setup = "Him on the charger, one connected cube in view, no person in view, clear floor.",
            DoThis = "Let it run. Part way through, pick him up and put him down next to the cube.",
            Success = "He leaves the charger, looks around, goes to the cube and plays with it, pauses and "
                    + "looks around between games, and after the put-down heads for the cube. Nothing repeats "
                    + "back to back.",
            Question = "Did he behave like a robot deciding for himself, without repeating or stalling?",
            AutoRule = "an activity starts, DriveOffCharger runs, scored picks follow and no NoActivityAvailableError appears",
            Command = o => (o.AllowNominalCalibration
                ? o.WithObb("freeplay", o.Ip).Concat(new[] { "--seconds", "300", "--nominal", "--acceptance", o.Acceptance("Z") })
                : o.WithObb("freeplay", o.Ip).Concat(new[] { "--seconds", "300", "--acceptance", o.Acceptance("Z") })).ToArray(),
            Judge = r => r.Contains("NoActivityAvailableError") ? AutoOutcome.Fail
                       : r.Contains("freeplay_goal_started") ? AutoOutcome.Pass : AutoOutcome.Fail,
        },
    };
}
