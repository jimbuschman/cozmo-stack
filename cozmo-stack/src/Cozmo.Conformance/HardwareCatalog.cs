namespace Cozmo.Conformance;

/// <summary>
/// The hardware acceptance campaign: every check, in the order the runner walks them.
///
/// The order is by risk. Connection and telemetry first, because nothing below them means anything if the
/// link is wrong; then the things the robot does standing still; then the things that move it; then the
/// charger, early on purpose because the core review changed the marker geometry it docks against and a
/// failure there should not be found at the end of a long campaign; then reactions, navigation and
/// manipulation; then freeplay and the long run.
///
/// The A-Z identifiers of the original plan are kept exactly, so a result is still traceable to that table.
/// The checks added since carry their own ids: CON, FD, AUD, MOV, IDL, A3, Z2, and CR1..CR8 for the
/// core-review corrections that hardware can say something about.
///
/// What this document is not: it is not a way to change a fidelity record. A check that passes says the
/// behaviour was observed, not that its provenance improved; a check that fails is an investigation item.
/// </summary>
public static class HardwareCatalog
{
    // The phases, in campaign order. Used as headings and to group the summary.
    public const string PhaseConnection = "1. connection and telemetry";
    public const string PhaseAnimation = "2. animation controller";
    public const string PhaseFace = "3. face display";
    public const string PhaseAudio = "4. audio";
    public const string PhaseMotion = "5. basic motion";
    public const string PhaseIdle = "6. idle and live animation";
    public const string PhaseCamera = "7. camera";
    public const string PhaseWorld = "8. markers, cubes and the world model";
    public const string PhaseCharger = "9. the charger";
    public const string PhaseReactions = "10. robot state and reactions";
    public const string PhaseNavigation = "11. navigation";
    public const string PhaseManipulation = "12. manipulation";
    public const string PhaseFreeplay = "13. freeplay";
    public const string PhaseLongRun = "14. long run and stability";
    public const string PhaseBlocked = "15. blocked on this build";

    public static IReadOnlyList<HardwareCheck> All { get; } = Build();

    public static HardwareCheck? Find(string id) =>
        All.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>The phases in campaign order, as the catalog uses them.</summary>
    public static IReadOnlyList<string> Phases { get; } = All.Select(c => c.Phase).Distinct().ToList();

    private static IReadOnlyList<HardwareCheck> Build() => new[]
    {
        // ================================================================ 1. connection and telemetry
        new HardwareCheck
        {
            Id = "LINK", Name = "Connection, handshake and telemetry", Milestone = "M2/M3", Phase = PhaseConnection,
            Subsystem = "The UDP transport, the connection handshake, identity and the 30 Hz state stream",
            Why = "Everything below this reads the robot's own telemetry. A handshake that half works, a state "
                + "rate that is not 30 Hz, or a link that quietly resends produces results that look like "
                + "subsystem faults and are not. This is also the reading the safety check before every "
                + "movement test compares against.",
            Setup = "Robot awake, on its treads, on the same network. Nothing else connected to it.",
            Prerequisites = new[] { "the robot is on and its light is on", "no other application is connected to it" },
            DoThis = "Nothing. Watch the rate and the transport counters.",
            Success = "He sits still, his backpack light is on, and nothing about him changes for 20 seconds.",
            Question = "Did he stay awake and still, with no dropped connection?",
            ExpectedTelemetry = "connected, identity received, more than 20 RobotState at about 30 Hz, nothing pending",
            AutoRule = "the smoke test reports PASS: connected, identity received, telemetry flowing, no backlog",
            Evidence = new[]
            {
                new EvidenceItem("handshake", "connected in"),
                new EvidenceItem("identity", "firmware:"),
                new EvidenceItem("telemetry", "telemetry:"),
                new EvidenceItem("transport", "transport:"),
                new EvidenceItem("verdict", "SMOKE TEST:"),
            },
            FidelityRecords = new[] { "M1-001", "M1-002", "M1-003", "M1-004", "M1-005", "M1-006", "M1-007", "M1-008", "M1-009", "M1-010", "M1-011", "M1-012", "M2-001", "M2-002", "M2-004", "M2-006" },
            Timeout = TimeSpan.FromMinutes(2),
            Command = o => new[] { "connect", o.Ip, "--seconds", "20", "--log", o.FrameLog("LINK") },
            Judge = r => r.Contains("SMOKE TEST: PASS") ? AutoOutcome.Pass : AutoOutcome.Fail,
        },
        new HardwareCheck
        {
            Id = "D", Name = "Lift position readout", Milestone = "M4", Phase = PhaseConnection,
            Subsystem = "RobotState lift angle and the angle-to-height conversion, with battery, cliff and IMU",
            Why = "The lift angle is reported in radians and converted with the engine's own formula. If the "
                + "conversion is wrong, every docking height and the carry check are wrong with it.",
            Setup = "Robot on its treads on a table. Start with the lift fully down.",
            Prerequisites = new[] { "CON passed", "the lift is all the way down to begin with" },
            DoThis = "Follow the two prompts. First leave the lift alone, all the way down. Then, when it says "
                   + "RAISE THE LIFT, lift the arm by hand as far as it goes and hold it there until it stops reading.",
            Success = "The two readings it prints back match where the lift actually was: about -0.198 rad / 32 mm "
                    + "down, about 0.712 rad / 92 mm raised.",
            Question = "Did the two printed readings match where the lift actually was, down and raised?",
            ExpectedTelemetry = "a reading at each end of the travel, and both ends of the travel actually visited",
            AutoRule = "both ends of the lift's travel are seen: down below -0.10 rad and raised above 0.40 rad",
            Evidence = new[]
            {
                new EvidenceItem("readings", "lift="),
                new EvidenceItem("sequence", "lift sequence:"),
                new EvidenceItem("down", "lift down:"),
                new EvidenceItem("raised", "lift raised:"),
            },
            FidelityRecords = new[] { "M2-003", "M2-002", "M4-008" },
            NeedsHandling = true,
            Requires = new[] { "LINK" },
            Timeout = TimeSpan.FromMinutes(2),
            Command = o => new[] { "sensors", o.Ip, "--guide-lift", "--acceptance", o.Acceptance("D") },
            Judge = r => r.Contains("both ends seen=yes") ? AutoOutcome.Pass : AutoOutcome.Fail,
        },

        // ================================================================ 2. animation controller
        new HardwareCheck
        {
            Id = "F", Name = "Animation timeline", Milestone = "M5", Phase = PhaseAnimation, MovesRobot = true,
            Subsystem = "The animation scheduler's keyframe timing against the robot's audio pacing",
            Why = "The scheduler paces itself off the robot's played-frame count, and everything that animates "
                + "goes through it. This is the readiness check for the animation controller: one known clip, "
                + "end to end, before anything harder depends on it.",
            Setup = "Robot on its treads with room to move. Requires the OBB assets.",
            Prerequisites = new[] { "CON passed", "--obb given" },
            DoThis = "Stand in front of him and watch the whole clip: his head and lift move and his face "
                   + "changes. Watch for a stutter, a truncated ending, or a head or lift left somewhere odd.",
            Success = "The clip plays as it always has: no stutter, no truncation, no stuck lift or head.",
            Question = "Did the clip play smoothly and completely?",
            ExpectedTelemetry = "keyframes fired equals keyframes in the clip and no stalls are reported",
            AutoRule = "keyframes fired equals keyframes in the clip, and no stalls are reported",
            Evidence = new[]
            {
                new EvidenceItem("keyframes", "keyframes"),
                new EvidenceItem("audio frames", "audio"),
                new EvidenceItem("stalls", "stall"),
            },
            FidelityRecords = new[] { "M5-001", "M5-004", "M5-007", "M5-008", "M5-018", "M3-015" },
            NeedsObb = true, Requires = new[] { "LINK" },
            Command = o => new[] { "anim", o.Ip, "--assets", Path.Combine(o.Obb ?? ".", "assets", "cozmo_resources", "assets"),
                                   "--name", "anim_bored_01", "--wwise", Path.Combine(o.Obb ?? ".", "assets", "cozmo_resources", "sound") },
            Judge = r => r.Contains("stall") ? AutoOutcome.Fail : r.ExitCode == 0 ? AutoOutcome.Pass : AutoOutcome.Fail,
        },

        // ================================================================ 3. face display
        new HardwareCheck
        {
            Id = "FD", Name = "Face display", Milestone = "M3", Phase = PhaseFace,
            Subsystem = "The OLED display path: the 128x64 packing and the display message",
            Why = "The robot reports nothing about its own screen, so the packing is only ever confirmed by "
                + "looking at it. A face that is mirrored, offset or half drawn is invisible to every "
                + "automated check there is.",
            Setup = "Somewhere you can see his face. No particular lighting needed.",
            Prerequisites = new[] { "CON passed" },
            DoThis = "Look at his screen while the pattern is held, from straight on. Check it is square, "
                   + "the right way up, and steady rather than flickering.",
            Success = "A pair of eyes is drawn squarely on the screen, right way up, held steady for the whole "
                    + "time, and it clears afterwards.",
            Question = "Was the image drawn squarely on his screen, the right way up and steady?",
            ExpectedTelemetry = "the display messages are sent and acknowledged by the transport",
            AutoRule = "the tool sends the image and completes without error",
            Evidence = new[] { new EvidenceItem("display", "face"), new EvidenceItem("frames sent", "sent") },
            FidelityRecords = new[] { "M3-006", "M3-007", "M3-008", "M3-009" },
            Requires = new[] { "LINK" },
            Timeout = TimeSpan.FromMinutes(2),
            Command = o => new[] { "face", o.Ip, "--pattern", "eyes", "--seconds", "6", "--acceptance", o.Acceptance("FD") },
            Judge = r => r.ExitCode == 0 ? AutoOutcome.Pass : AutoOutcome.Fail,
        },

        // ================================================================ 4. audio
        new HardwareCheck
        {
            Id = "AUD", Name = "Speaker and the audio frame pacing", Milestone = "M3", Phase = PhaseAudio,
            Subsystem = "The mu-law companding transcribed from the engine and the robot's audio buffer pacing",
            Why = "Before any song is judged, the plain path has to be known good: a countable number of "
                + "separate beeps tests continuity and pacing without anyone having to judge tone quality. If "
                + "this is chopped, the singing checks above it are testing the wrong thing.",
            Setup = "Quiet room.",
            Prerequisites = new[] { "CON passed" },
            DoThis = "Count the beeps and listen for gaps or crackle.",
            Success = "Separate, clean beeps at a steady rate, all the same length, with no crackle between them.",
            Question = "Did you hear clean, evenly spaced beeps with no crackle or gaps?",
            ExpectedTelemetry = "every audio frame is sent and the robot's played-frame count keeps up",
            AutoRule = "the tool sends the tone and completes without error",
            Evidence = new[]
            {
                new EvidenceItem("frames", "frame"),
                new EvidenceItem("played counter", "played"),
                new EvidenceItem("codec", "codec"),
            },
            FidelityRecords = new[] { "M3-010", "M3-011", "M3-012", "M3-013", "M3-014", "M3-015" },
            Requires = new[] { "LINK" },
            Timeout = TimeSpan.FromMinutes(2),
            Command = o => new[] { "tone", o.Ip, "--sound", "beeps", "--seconds", "4", "--acceptance", o.Acceptance("AUD") },
            Judge = r => r.ExitCode == 0 ? AutoOutcome.Pass : AutoOutcome.Fail,
        },
        new HardwareCheck
        {
            Id = "A", Name = "Cozmo sings", Milestone = "M9", Phase = PhaseAudio, MovesRobot = true,
            Subsystem = "The Wwise switch, the per-note vocal sampler and the singing behaviour's animations",
            Why = "The song is rendered here from Anki's banks by a reconstructed sampler. Whether it sounds "
                + "like Cozmo singing is something only a listener can settle. The streaming architecture "
                + "changed in the core review, so this is a re-run, not a repeat.",
            Setup = "Quiet room. Robot on its treads with space to animate. Requires the OBB.",
            Prerequisites = new[] { "AUD passed", "--obb given" },
            DoThis = "Listen to the whole thing without touching him. It runs about 12 seconds: a get-in "
                   + "animation, the song, then a get-out.",
            Success = "About 12 seconds of tune between a get-in and a get-out, in his own voice, one note per "
                    + "note, at a sensible level, with no bursts of get-in phrases during the song.",
            Question = "Did he sing a recognisable tune, one note per note, at a sensible level?",
            ExpectedTelemetry = "the switch is posted, the get-in, song and get-out steps all complete, and the song renders with notes",
            AutoRule = "the switch is posted and the get-in, song and get-out animations complete",
            KnownIssue = "Previously observed: the automated path passes but the audio is musically wrong and "
                       + "over-sustained. If you hear that again, answer n. The distinction is the point of this check.",
            Evidence = new[]
            {
                new EvidenceItem("song", "song:"),
                new EvidenceItem("steps", "steps played"),
                new EvidenceItem("streaming counters", "stream"),
                new EvidenceItem("not produced", "not produced"),
            },
            FidelityRecords = new[] { "M9-001", "M9-002", "M9-004", "M9-005", "M9-006", "M9-007", "M9-008", "M9-009", "M9-010", "M9-011", "M9-012", "M9-015", "M9-016", "M9-018", "M9-019", "M9-020", "M9-021", "M9-027", "M9-013", "M9-014", "M9-022", "M9-024", "M9-025", "M9-026", "M9-023", "M6-001", "M6-002", "M6-003", "M6-004", "M6-005", "M6-006", "M6-007", "M3-013", "M3-015" }, CoreRegressions = new[] { "CORE-006" },
            NeedsObb = true, Requires = new[] { "AUD" },
            Command = o => o.WithObb("sing", o.Ip).Concat(new[] { "--behavior", "Singing_AbaDaba", "--acceptance", o.Acceptance("A") }).ToArray(),
            Judge = r => r.ExitCode == 0 && r.ContainsAny("notes", "rendered") ? AutoOutcome.Pass : AutoOutcome.Fail,
        },
        new HardwareCheck
        {
            Id = "A2", Name = "One song from each tempo group", Milestone = "M9", Phase = PhaseAudio, MovesRobot = true,
            Subsystem = "The 100 and 120 bpm switch containers and their meter overrides",
            Why = "Three tempo groups were recovered from the banks. If the meter override is misread, the "
                + "tempos will not differ audibly.",
            Setup = "As A.",
            Prerequisites = new[] { "A passed or heard" },
            DoThis = "Listen to the whole song and compare its speed with the one you just heard in A. Note "
                   + "whether it stops cleanly at the end rather than running on.",
            Success = "Bingo is cut at about 9.8 s by its Stop event, and the tempo audibly differs from A.",
            Question = "Did the tempo audibly differ from the first song, and did it stop cleanly at the end?",
            ExpectedTelemetry = "the behaviour runs to completion and the song is cut by its Stop event",
            AutoRule = "the behaviour runs to completion",
            KnownIssue = "Same known discrepancy as A.",
            Evidence = new[] { new EvidenceItem("song", "song:"), new EvidenceItem("steps", "steps played") },
            FidelityRecords = new[] { "M9-001", "M9-002", "M9-004", "M9-005", "M9-006", "M9-007", "M9-008", "M9-009", "M9-010", "M9-011", "M9-012", "M9-015", "M9-016", "M9-018", "M9-019", "M9-020", "M9-021", "M9-027", "M9-013", "M9-014", "M9-022", "M9-024", "M9-025", "M9-026", "M9-023", "M6-001", "M6-002", "M6-003", "M6-004", "M6-005", "M6-006", "M6-007", "M3-013", "M3-015" }, CoreRegressions = new[] { "CORE-006" },
            NeedsObb = true, Requires = new[] { "A" },
            Command = o => o.WithObb("sing", o.Ip).Concat(new[] { "--behavior", "Singing_Bingo", "--acceptance", o.Acceptance("A2") }).ToArray(),
            Judge = r => r.ExitCode == 0 ? AutoOutcome.Pass : AutoOutcome.Fail,
        },
        new HardwareCheck
        {
            Id = "A3", Name = "Sustained singing and a parameter posted mid-note", Milestone = "M9", Phase = PhaseAudio, MovesRobot = true,
            Subsystem = "The streaming render: how far ahead it runs, what the scheduler sends, and what the robot plays",
            Why = "The core review changed who waits for whom in the audio path twice: the scheduler now "
                + "refuses to send samples that have not been rendered, and the render follows consumption "
                + "rather than the wall clock. The failure modes that leaves are audible and only audible - "
                + "chopping, a hanging note, an underrun - and they need a song long enough to expose them "
                + "under the robot's real back-pressure. The vibrato parameter is posted while a note is "
                + "already sounding, which is the whole reason the song is rendered as it plays.",
            Setup = "Quiet room, robot on his treads. Requires the OBB.",
            Prerequisites = new[] { "A passed or heard", "--obb given" },
            DoThis = "Listen for the whole song. Half way through, the tool posts the vibrato parameter: "
                   + "listen for the voice changing while a note is already sounding.",
            Success = "Continuous sound with no alternating silence, no note that hangs or repeats, no "
                    + "reverb-like smear, and an audible change when the vibrato is posted.",
            Question = "Was the sound continuous throughout, and did the voice change when the parameter was posted?",
            ExpectedTelemetry = "frames rendered, ready, sent and robot-reported played all advance together, with no underruns",
            AutoRule = "the song runs to completion with no underruns reported",
            Evidence = new[]
            {
                new EvidenceItem("streaming counters", "stream:") { Limit = 80 },
                new EvidenceItem("underruns", "underrun"),
                new EvidenceItem("vibrato", "vibrato"),
                new EvidenceItem("steps", "steps played"),
            },
            FidelityRecords = new[] { "M9-001", "M9-002", "M9-004", "M9-005", "M9-006", "M9-007", "M9-008", "M9-009", "M9-010", "M9-011", "M9-012", "M9-015", "M9-016", "M9-018", "M9-019", "M9-020", "M9-021", "M9-027", "M9-013", "M9-014", "M9-022", "M9-024", "M9-025", "M9-026", "M9-023", "M6-001", "M6-002", "M6-003", "M6-004", "M6-005", "M6-006", "M6-007", "M3-013", "M3-015", "M9-003", "M9-017" }, CoreRegressions = new[] { "CORE-006" },
            NeedsObb = true, Requires = new[] { "A" },
            Timeout = TimeSpan.FromMinutes(4),
            Command = o => o.WithObb("sing", o.Ip).Concat(new[] { "--behavior", "Singing_Bingo", "--vibrato", "--seconds", "60", "--acceptance", o.Acceptance("A3") }).ToArray(),
            Judge = r => r.Contains("underruns: 0") || !r.Contains("underrun") ? AutoOutcome.Pass : AutoOutcome.Fail,
        },

        // ================================================================ 5. basic motion
        new HardwareCheck
        {
            Id = "MOV", Name = "Head, lift and a short drive", Milestone = "M4", Phase = PhaseMotion, MovesRobot = true,
            Subsystem = "SetHeadAngle, SetLiftHeight, DriveWheels and the robot's own action acknowledgements",
            Why = "The first check where he moves under his own power, and the floor under every movement test "
                + "after it. It is deliberately small: if the head and lift do not go where they are told, "
                + "nothing about docking or navigation can be interpreted.",
            Setup = "Clear floor, at least half a metre ahead of him, nothing he can drive off.",
            Prerequisites = new[] { "CON and D passed", "a clear, flat surface with no drop at the edge" },
            DoThis = "Watch the head, then the lift, then the short forward-and-back. Keep a hand ready.",
            Success = "The head tilts down and back, the lift rises and lowers, then he drives forward a few "
                    + "centimetres and back, and stops. Nothing keeps moving afterwards.",
            Question = "Did the head, lift and wheels each move as described and then stop?",
            ExpectedTelemetry = "a MotorActionAck for each commanded move and a checked stop at the end",
            AutoRule = "the robot acknowledges the head and lift actions and the stop is confirmed",
            Evidence = new[]
            {
                new EvidenceItem("acknowledgements", "MotorActionAck"),
                new EvidenceItem("head", "head"),
                new EvidenceItem("lift", "lift"),
                new EvidenceItem("stop", "stop"),
            },
            FidelityRecords = new[] { "M4-001", "M4-002", "M4-003", "M4-006", "M4-007" },
            Requires = new[] { "LINK", "D" },
            Timeout = TimeSpan.FromMinutes(3),
            Command = o => new[] { "drive", o.Ip, "--allow-drive", "--speed", "40", "--drive-seconds", "1", "--acceptance", o.Acceptance("MOV") },
            Judge = r => r.ContainsAny("MotorActionAck", "acknowledged") ? AutoOutcome.Pass : AutoOutcome.Fail,
        },

        // ================================================================ 6. idle and live animation
        new HardwareCheck
        {
            Id = "CR1", Name = "A live body shuffle stops at its duration", Milestone = "CORE-001", Phase = PhaseIdle, MovesRobot = true,
            Subsystem = "The animation scheduler's live keyframe path and the tick loop that serves its deadline",
            Why = "Idle streams a body keyframe with a duration and nothing else drives the scheduler. Before "
                + "the correction nothing called Advance in that state, so the stop never went out and the "
                + "wheels ran until something else countermanded them. Offline the stop is asserted against "
                + "the message log; on the robot the wheels either stop or they do not.",
            Setup = "Clear floor. He will creep forward a few centimetres.",
            Prerequisites = new[] { "MOV passed", "a clear, flat surface" },
            DoThis = "Watch the wheels. Be ready to lift him if he keeps going.",
            Success = "He shuffles forward for about a second and stops by himself. He does not keep creeping.",
            Question = "Did he stop by himself at the end of the shuffle, without continuing to creep?",
            ExpectedTelemetry = "the reported wheel speeds go non-zero and return to zero within a frame or two of the duration",
            AutoRule = "the wheels move and are back at zero half a second after the keyframe's duration",
            Evidence = new[]
            {
                new EvidenceItem("wheel samples", "t=") { Limit = 60 },
                new EvidenceItem("verdict", "automated:"),
                new EvidenceItem("duration", "wheels reported moving"),
            },
            FidelityRecords = new[] { "M5-006", "M5-007", "M7-010", "M7-017" }, CoreRegressions = new[] { "CORE-001" },
            Requires = new[] { "MOV" },
            Timeout = TimeSpan.FromMinutes(2),
            Command = o => new[] { "core", o.Ip, "--case", "CORE-001", "--acceptance", o.Acceptance("CR1") },
            Judge = r => r.Contains("automated: PASS") ? AutoOutcome.Pass : AutoOutcome.Fail,
        },
        new HardwareCheck
        {
            Id = "IDL", Name = "Idle keeps him alive without wandering", Milestone = "M8", Phase = PhaseIdle, MovesRobot = true,
            Subsystem = "The idle behaviour: keep-alive blinks, small body shuffles and head moves",
            Why = "Idle is what runs whenever nothing else does, so a fault here shows up as the robot looking "
                + "dead or as it drifting across the table over minutes. Neither is visible in one clip.",
            Setup = "Clear flat surface, robot on his treads, nothing in front of him. Requires the OBB.",
            Prerequisites = new[] { "CR1 passed", "--obb given" },
            DoThis = "Leave him alone for a minute and watch. Note where he starts and where he ends up.",
            Success = "He blinks and makes small movements, and he is still roughly where he started after a "
                    + "minute. Nothing repeats mechanically and nothing runs on.",
            Question = "Did he look alive without wandering away from where he started?",
            ExpectedTelemetry = "idle takes actions of its own during the minute, and none is left running at the end",
            AutoRule = "the idle layer took at least one action of its own (\"idle actions taken\" is not zero)",
            Evidence = new[]
            {
                new EvidenceItem("actions", "idle actions taken"),
                new EvidenceItem("decisions", "idle"),
                new EvidenceItem("arbiter", "[arbiter]"),
            },
            FidelityRecords = new[] { "M7-004", "M7-005", "M7-006", "M7-007", "M7-008", "M7-009", "M7-010", "M7-016", "M7-017" }, CoreRegressions = new[] { "CORE-001" },
            NeedsObb = true, Requires = new[] { "CR1" },
            Timeout = TimeSpan.FromMinutes(3),
            Command = o => o.WithObb("behavior", o.Ip).Concat(new[] { "--seconds", "60", "--no-react" }).ToArray(),
            Judge = r => r.ExitCode == 0 && !r.Contains("idle actions taken: 0") && r.Contains("idle actions taken")
                ? AutoOutcome.Pass : AutoOutcome.Fail,
        },
        new HardwareCheck
        {
            Id = "CR3", Name = "A cancelled animation stops sending", Milestone = "CORE-003", Phase = PhaseIdle, MovesRobot = true,
            Subsystem = "The scheduler's emission gate: a command of a cancelled playback must not get out",
            Why = "The generation check was made under the lock and the command sent after it was released. A "
                + "cancel landing in that window let one animation's motion go out in the middle of another. "
                + "On the robot that is exactly what it looks like: a twitch after everything should have "
                + "stopped.",
            Setup = "Clear floor, robot on his treads. Requires the OBB.",
            Prerequisites = new[] { "F and MOV passed", "--obb given" },
            DoThis = "Watch him closely for two seconds after the animation is cut short.",
            Success = "He stops when the animation is cut and stays stopped. No late twitch of the head, the "
                    + "lift or the wheels.",
            Question = "Did he stop cleanly with no late twitch after the animation was cut?",
            ExpectedTelemetry = "after the stop settles, wheel speed stays at zero and head and lift stop changing",
            AutoRule = "no wheel, head or lift movement is reported after the stop has settled",
            Evidence = new[]
            {
                new EvidenceItem("telemetry after the stop", "t=") { Limit = 60 },
                new EvidenceItem("verdict", "automated:"),
                new EvidenceItem("settled", "after the stop settled"),
            },
            FidelityRecords = new[] { "M5-023", "M5-008", "M5-006" }, CoreRegressions = new[] { "CORE-003" },
            NeedsObb = true, Requires = new[] { "F", "MOV" },
            Timeout = TimeSpan.FromMinutes(3),
            Command = o => o.WithObb("core", o.Ip).Concat(new[] { "--case", "CORE-003", "--acceptance", o.Acceptance("CR3") }).ToArray(),
            Judge = r => r.Contains("automated: PASS") ? AutoOutcome.Pass : AutoOutcome.Fail,
        },
        new HardwareCheck
        {
            Id = "CR2", Name = "A dropped link ends the animation, not the process", Milestone = "CORE-002", Phase = PhaseIdle, MovesRobot = true,
            Subsystem = "The animation tick loop's failure boundary when the robot goes away mid-clip",
            Why = "The tick loop runs on a background thread and every frame reaches the transport. Once the "
                + "link is gone the send throws, and before the correction that exception escaped a background "
                + "thread and took the process with it. This is the one check where the disconnect is the "
                + "subject, so the runner does not treat it as an interruption.",
            Setup = "Clear floor, robot on his treads, room for a short animation. Requires the OBB.",
            Prerequisites = new[] { "F passed", "--obb given" },
            DoThis = "Watch him for about five seconds. He starts an animation; part way through, the tool cuts "
                   + "the link on purpose and says so. Watch what he does in the second or two after that.",
            Success = "He is moving, and at the moment the tool says it is dropping the link he stops where he "
                    + "is and stays stopped - no twitching, no carrying on, no running away. The tool then "
                    + "prints its own verdict and the campaign moves to the next check instead of dying.",
            Question = "Did he stop where he was when the link was cut, and stay stopped?",
            ExpectedTelemetry = "the animation task completes, the ticker stops, and the failure is reported through Faulted",
            AutoRule = "the animation ends, the ticker stops and the exception is reported rather than thrown at nobody",
            Evidence = new[]
            {
                new EvidenceItem("before the drop", "ticker running"),
                new EvidenceItem("after the drop", "the animation task"),
                new EvidenceItem("fault", "Faulted"),
                new EvidenceItem("verdict", "automated:"),
            },
            FidelityRecords = new[] { "M5-022", "M1-014" }, CoreRegressions = new[] { "CORE-002" },
            NeedsObb = true, Requires = new[] { "F" },
            ExpectsDisconnect = true,
            Cleanup = "The link is deliberately dropped; the runner reconnects for the next check.",
            Timeout = TimeSpan.FromMinutes(3),
            Command = o => o.WithObb("core", o.Ip).Concat(new[] { "--case", "CORE-002", "--acceptance", o.Acceptance("CR2") }).ToArray(),
            Judge = r => r.Contains("automated: PASS") ? AutoOutcome.Pass : AutoOutcome.Fail,
        },

        // ================================================================ 7. camera
        new HardwareCheck
        {
            Id = "E", Name = "Colour camera frames", Milestone = "M3", Phase = PhaseCamera,
            Subsystem = "The colour path of the camera decoder",
            Why = "Colour frames have never been exercised on hardware; the decoder's colour branch is "
                + "reconstructed and could be producing plausible nonsense.",
            Setup = "Point the robot at something with obvious colour: a cube, a book cover, your hand.",
            Prerequisites = new[] { "CON passed" },
            DoThis = "Nothing. Let it capture, then look at the saved images.",
            Success = "The saved files are colour photographs of the room, not tinted or scrambled.",
            Question = "Are the saved images real colour photographs of what he was pointed at?",
            ExpectedTelemetry = "every frame decodes without error and is written out",
            AutoRule = "frames decode without error",
            Evidence = new[] { new EvidenceItem("frames", "frame"), new EvidenceItem("written", "wrote") },
            FidelityRecords = new[] { "M3-001", "M3-002", "M3-003", "M3-004", "M3-005", "M3-016" },
            Requires = new[] { "LINK" },
            Timeout = TimeSpan.FromMinutes(3),
            Command = o => new[] { "camera", o.Ip, "--color", "--out", o.Frames("E"), "--acceptance", o.Acceptance("E") },
            Judge = r => r.ExitCode == 0 ? AutoOutcome.Pass : AutoOutcome.Fail,
        },

        // ================================================================ 8. markers, cubes and the world model
        new HardwareCheck
        {
            Id = "B", Name = "Cube telemetry", Milestone = "M4", Phase = PhaseWorld,
            Subsystem = "Cube discovery, connection and the tap, movement, up-axis and battery reports",
            Why = "Discovery alone was already observed. What is unverified is whether a cube actually connects "
                + "and whether its telemetry arrives, which everything with a cube depends on.",
            Setup = "One or more cubes with charged batteries, within about half a metre of the robot.",
            Prerequisites = new[] { "CON passed", "at least one cube with a charged battery" },
            DoThis = "During the 15 seconds: tap a cube, roll it onto another face, and pick it up and put it down.",
            Success = "The events printed match what you did, and at least one cube shows as connected.",
            Question = "Did the printed events match what you did to the cube?",
            ExpectedTelemetry = "at least one cube CONNECTED and at least one telemetry signal",
            AutoRule = "at least one cube CONNECTED and at least one telemetry signal (tap, movement, up axis or battery)",
            Evidence = new[]
            {
                new EvidenceItem("connection", "connected"),
                new EvidenceItem("taps", "tapped"),
                new EvidenceItem("movement", "moving"),
                new EvidenceItem("up axis", "upAxis"),
            },
            FidelityRecords = new[] { "M4-009", "M4-010" },
            NeedsCube = true, NeedsHandling = true, Requires = new[] { "LINK" },
            Timeout = TimeSpan.FromMinutes(2),
            Command = o => new[] { "cubes", o.Ip, "--seconds", "15", "--acceptance", o.Acceptance("B") },
            Judge = r => r.Contains("0 connected") || !r.Contains("connected")
                ? AutoOutcome.Fail
                : r.ContainsAny("tapped ", "moving ", "still ", "upAxis") ? AutoOutcome.Pass : AutoOutcome.Fail,
        },
        new HardwareCheck
        {
            Id = "K", Name = "Camera calibration and cube localisation", Milestone = "M11", Phase = PhaseWorld,
            Subsystem = "The NV calibration read, marker detection and BlockWorld's pose estimate",
            Why = "This is the only positive evidence that the reconstructed vision front end sees a real cube: "
                + "the offline tests render markers from the recovered library, and no capture holds a cube. "
                + "Everything that docks or navigates depends on it, so it runs before those.",
            Setup = "A connected cube (run B first). Good, even lighting.",
            Prerequisites = new[] { "B passed", "a ruler or tape measure to check the printed distance" },
            DoThis = "Show him the cube at 10-30 cm. Turn it so different faces show. Move it about 10 cm. "
                   + "Then hide it from view.",
            Success = "Marker codes match the faces shown, the printed distance matches a ruler within about "
                    + "5 percent, and the yaw matches how the cube is turned.",
            Question = "Did the printed codes, distance and yaw match the real cube?",
            ExpectedTelemetry = "the calibration is read from the robot's NV storage and a cube becomes Known with a pose in millimetres",
            AutoRule = "the calibration is read from the robot and at least one cube becomes Known with a pose",
            KnownIssue = "M11-005 (RefineQuadrilateral sub-pixel refinement) is an open source gap. This check "
                       + "can say whether marker behaviour is operationally good enough; it does not close that "
                       + "gap and no result here changes its status.",
            Evidence = new[]
            {
                new EvidenceItem("calibration", "camera calibration"),
                new EvidenceItem("markers", "marker"),
                new EvidenceItem("objects", "Known at"),
                new EvidenceItem("forgetting", "miss "),
            },
            FidelityRecords = new[] { "M11-011", "M11-012", "M11-001", "M11-002", "M11-018", "M11-005", "M11-003", "M11-004", "M11-006", "M11-007", "M11-008", "M11-010", "M2-007" },
            NeedsCube = true, NeedsHandling = true, Requires = new[] { "B" },
            Timeout = TimeSpan.FromMinutes(4),
            Command = o => o.AllowNominalCalibration
                ? new[] { "vision", o.Ip, "--seconds", "90", "--nominal", "--acceptance", o.Acceptance("K"), "--out", o.Frames("K") }
                : new[] { "vision", o.Ip, "--seconds", "90", "--acceptance", o.Acceptance("K"), "--out", o.Frames("K") },
            Judge = r => !r.Contains("camera calibration from robot") ? AutoOutcome.Fail
                       : r.Contains("Known at") ? AutoOutcome.Pass : AutoOutcome.Fail,
        },

        // ================================================================ 9. the charger, early on purpose
        new HardwareCheck
        {
            Id = "V", Name = "Mount the charger", Milestone = "M13", Phase = PhaseCharger, MovesRobot = true,
            Subsystem = "The charger object, the align dock with the real 20 x 27 mm marker, and the backwards mount",
            Why = "The core review found the docking solve was building its object points from the marker's "
                + "width in both directions, so the charger's 20 x 27 marker was solved as a square and came "
                + "out 220 mm away when it was 286. That number is the DockingErrorSignal he steers by, so "
                + "this check is where the correction is either right or visibly wrong. It runs early, before "
                + "the long manipulation sequence, so a failure is found in the first half hour.",
            Setup = "Place the charger on a clear, flat floor with its back-wall marker facing him. Put him on "
                  + "the floor 20-40 cm directly in front of it, squarely facing the marker, with at least "
                  + "30 cm of clear floor on each side.",
            Prerequisites = new[] { "K passed (the marker pipeline works)", "the charger is plugged in and its marker is clean and unobstructed", "20-40 cm of clear floor between him and the charger" },
            DoThis = "Watch him turn his back to the charger and reverse onto it. Note where he stops relative "
                   + "to the charger, and whether the marker leaves his view before he turns.",
            Success = "He stops in front of the charger, turns around, backs on and the contacts engage: his "
                    + "backpack lights change. A miss drives forward 120 mm and retries.",
            Question = "Did he end up on the charger with the contacts engaged?",
            ExpectedTelemetry = "the charger is located with a pose, the align dock runs, docking error signals are sent, and the contacts report on charger",
            AutoRule = "the charger is located, the align dock runs and the contacts report on charger",
            KnownIssue = "The corrected geometry ends the align about 93 mm from the marker, where the marker "
                       + "may leave the camera's view. If the align starts and then he stops seeing it, say so "
                       + "in the note: that is the open question this check exists to settle, and it is not a "
                       + "reason to change the 20 x 27 mm geometry, which is the charger's own.",
            Evidence = new[]
            {
                new EvidenceItem("charger object", "Charger"),
                new EvidenceItem("observed marker", "marker") { Limit = 60 },
                new EvidenceItem("solved pose", "Known at"),
                new EvidenceItem("docking error signal", "DockingErrorSignal") { Limit = 80 },
                new EvidenceItem("robot pose", "pose"),
                new EvidenceItem("path events", "path"),
                new EvidenceItem("marker lost", "lost"),
                new EvidenceItem("contacts", "on charger"),
            },
            FidelityRecords = new[] { "M13-002", "M13-008", "M13-009", "M13-012", "M12-001", "M12-003", "M12-005", "M12-013", "M12-014" }, CoreRegressions = new[] { "CORE-005" },
            NeedsCharger = true, NeedsHandling = true, Requires = new[] { "K" },
            Timeout = TimeSpan.FromMinutes(5),
            Command = o => o.WithObb("manip", o.Ip).Concat(new[] { "--mount", "--acceptance", o.Acceptance("V") }).ToArray(),
            Judge = r => r.Contains("on charger: True") ? AutoOutcome.Pass : AutoOutcome.Fail,
        },
        new HardwareCheck
        {
            Id = "W", Name = "Drive off the charger", Milestone = "M13", Phase = PhaseCharger, MovesRobot = true,
            Subsystem = "DriveOffChargerContactsAction and the on-charger flag",
            Why = "The distance and the 20 mm/s crawl were read from the engine; whether they clear the "
                + "contacts on a real charger is unverified.",
            Setup = "Him sitting on the charger, clear floor ahead.",
            Prerequisites = new[] { "he is on the charger (V, or put him on by hand)" },
            DoThis = "Watch him leave the charger: one slow forward crawl, then a stop on his treads. Note "
                   + "whether he actually clears the contacts.",
            Success = "He drives forward off the charger at a crawl and stops on his treads.",
            Question = "Did he drive clear of the charger and stop?",
            ExpectedTelemetry = "one 156 mm line at 20 mm/s and the on-charger flag clears",
            AutoRule = "one 156 mm line at 20 mm/s and the on-charger flag clears",
            Evidence = new[]
            {
                new EvidenceItem("on charger", "on charger"),
                new EvidenceItem("drive off", "DriveOffCharger"),
                new EvidenceItem("status flag", "IS_ON_CHARGER"),
            },
            FidelityRecords = new[] { "M13-013", "M13-009" },
            NeedsCharger = true, Requires = new[] { "V" },
            Timeout = TimeSpan.FromMinutes(3),
            Command = o => o.WithObb("manip", o.Ip).Concat(new[] { "--driveoff", "--acceptance", o.Acceptance("W") }).ToArray(),
            Judge = r => r.ContainsAny("DriveOffCharger", "IS_ON_CHARGER") ? AutoOutcome.Pass : AutoOutcome.Fail,
        },

        // ================================================================ 10. robot state and reactions
        new HardwareCheck
        {
            Id = "G", Name = "Off-treads transitions", Milestone = "M10", Phase = PhaseReactions,
            Subsystem = "The off-treads classifier: on treads, in air, on back, on face, on each side",
            Why = "The classifier drives the pick-up reaction and gates freeplay. Its debounce timings were "
                + "read from the engine and have never been watched against real handling.",
            Setup = "Robot on its treads, room to handle it. Nothing else running.",
            Prerequisites = new[] { "CON passed", "somewhere soft to lay him down" },
            DoThis = "Over 90 seconds: pick him up, put him down, lay him on his back, on each side, on his "
                   + "face, and hold him still tilted 20-40 degrees.",
            Success = "A transition prints for each handling and nothing prints while he sits still. On-back "
                    + "arrives about a second after laying him down, sides and face after a quarter second.",
            Question = "Did every printed transition match what you did, in that order, with nothing while still?",
            ExpectedTelemetry = "the classifier enables after the calibration report and prints one transition per handling",
            AutoRule = "the classifier enables and reports being in the air and at least one resting state (on his back, face or side)",
            Evidence = new[]
            {
                new EvidenceItem("transitions", "->") { Limit = 60 },
                new EvidenceItem("classifier", "classifier"),
                new EvidenceItem("calibration", "calibration"),
            },
            FidelityRecords = new[] { "M10-001", "M10-005", "M7-015" },
            NeedsHandling = true, Requires = new[] { "LINK" },
            Timeout = TimeSpan.FromMinutes(4),
            Command = o => new[] { "offtreads", o.Ip, "--seconds", "90", "--acceptance", o.Acceptance("G") },
            // being lifted and one resting state: a single transition could be anything
            Judge = r => r.Contains("InAir") && r.ContainsAny("OnBack", "OnFace", "OnLeftSide", "OnRightSide")
                ? AutoOutcome.Pass : AutoOutcome.Fail,
        },
        new HardwareCheck
        {
            Id = "C", Name = "Falling then impact", Milestone = "M7", Phase = PhaseReactions,
            Subsystem = "The falling and impact reports, and the rule that the reaction waits for the landing",
            Why = "The engine reacts to the landing, not the fall, and only above an impact of 1000. Neither the "
                + "threshold nor the wait has been seen on hardware. The first run of this check watched for a "
                + "minute and saw only the pick-up reaction, which says nothing about falling: the drop now has "
                + "a window of its own and only the falling reaction counts inside it.",
            Setup = "A cushion or folded towel on the table, and room to hold him a few centimetres above it. "
                  + "Requires the OBB.",
            Prerequisites = new[] { "G passed", "something soft to drop him onto", "--obb given" },
            DoThis = "Wait for the prompt. Then hold him a few centimetres above the cushion and DROP him onto "
                   + "it - let go completely. Lifting him first will make him react to being picked up; that "
                   + "does not count and the tool will say so.",
            Success = "He reacts when he lands, not while he is in the air.",
            Question = "Did he react on landing rather than while falling?",
            ExpectedTelemetry = "the RobotFalling reaction fires inside the drop's own window and nothing else satisfies it",
            AutoRule = "the falling reaction fires during the window the tool opens for the drop",
            Evidence = new[]
            {
                new EvidenceItem("window", "window RobotFalling"),
                new EvidenceItem("expected", "expected reactions"),
                new EvidenceItem("falling", "falling"),
                new EvidenceItem("reaction", "REACTION"),
            },
            FidelityRecords = new[] { "M7-003", "M4-008" },
            NeedsHandling = true, NeedsObb = true, Requires = new[] { "G" },
            Timeout = TimeSpan.FromMinutes(3),
            Command = o => o.WithObb("reactions", o.Ip).Concat(new[] { "--expect", "RobotFalling", "--seconds", "45",
                                                                      "--acceptance", o.Acceptance("C") }).ToArray(),
            Judge = r => r.Contains("expected reactions: all seen") ? AutoOutcome.Pass : AutoOutcome.Fail,
        },
        new HardwareCheck
        {
            Id = "H", Name = "The derived-state reactions", Milestone = "M10", Phase = PhaseReactions, MovesRobot = true,
            Subsystem = "The reactions built on derived state: on back, on face, on side, slope, shaken, returned to treads",
            Why = "Each reaction's trigger and animation came from the engine. Whether the right one fires for "
                + "the state he is actually in can only be seen by handling him. Requires the OBB.",
            Setup = "Room to handle him and somewhere soft to lay him down.",
            Prerequisites = new[] { "G passed", "--obb given" },
            DoThis = "Do one thing at a time, when the prompt asks for it, and then leave him alone until the "
                   + "next prompt. The tool watches for one named reaction per prompt.",
            Success = "On his back he flips down; on his face he rolls; on a side he asks to be righted; on a "
                    + "slope he reacts then checks his pitch; shaken then set down he acts dizzy. Nothing "
                    + "fires for a state he is not in.",
            Question = "Did the right reaction fire for each state, and nothing for states he was not in?",
            ExpectedTelemetry = "each named reaction fires inside its own window: on back, on face, on a side, and shaken",
            AutoRule = "every reaction the check names fires during its own prompt, and no other reaction can stand in for it",
            Evidence = new[]
            {
                new EvidenceItem("windows", "window "),
                new EvidenceItem("expected", "expected reactions"),
                new EvidenceItem("reactions", "REACTION") { Limit = 60 },
                new EvidenceItem("off-treads", "off-treads"),
            },
            FidelityRecords = new[] { "M10-001", "M10-003", "M10-004", "M10-005", "M7-001", "M7-002", "M7-011", "M7-014" },
            NeedsHandling = true, NeedsObb = true, Requires = new[] { "G" },
            Timeout = TimeSpan.FromMinutes(5),
            Command = o => o.WithObb("reactions", o.Ip).Concat(new[] { "--expect", "RobotOnBack,RobotOnFace,RobotOnSide,RobotShaken",
                                                                      "--seconds", "160", "--acceptance", o.Acceptance("H") }).ToArray(),
            Judge = r => r.Contains("expected reactions: all seen") ? AutoOutcome.Pass : AutoOutcome.Fail,
        },
        new HardwareCheck
        {
            Id = "I", Name = "StartMotorCalibration honoured", Milestone = "M4/M10", Phase = PhaseReactions, MovesRobot = true,
            Subsystem = "StartMotorCalibration (0x58) and the MotorCalibration reports the robot answers with",
            Why = "The engine asks the robot to recalibrate its head in several states, and whether the robot "
                + "honours the request has never been observed. It could not be: the robot recalibrates on "
                + "every connection anyway, so a tool that watches for a calibration report sees one whatever "
                + "it does. This waits for the connection-time calibration to finish, asks, and then judges "
                + "only what arrives afterwards.",
            Setup = "Robot on its treads with room for the head to nod. Nothing to do beforehand.",
            Prerequisites = new[] { "LINK passed", "nothing in front of his head" },
            DoThis = "Wait for the line that says ASKING NOW, then watch his head. That is the whole check.",
            Success = "After ASKING NOW, his head nods down to its stop and comes back, within a few seconds. "
                    + "Nothing else moves.",
            Question = "Did his head nod to its stop and come back after the tool said ASKING NOW?",
            ExpectedTelemetry = "a MotorCalibration for the head with CalibStarted true, then one with false, both after the request",
            AutoRule = "the head reports calibration started and then finished, after the request and not before it",
            Evidence = new[]
            {
                new EvidenceItem("reports", "MotorCalibration") { Limit = 20 },
                new EvidenceItem("the request", "ASKING NOW"),
                new EvidenceItem("verdict", "calibration honoured:"),
            },
            FidelityRecords = new[] { "M4-012", "M4-013", "M8-008" },
            Requires = new[] { "LINK" },
            Timeout = TimeSpan.FromMinutes(3),
            Command = o => new[] { "calibrate", o.Ip, "--acceptance", o.Acceptance("I") },
            Judge = r => r.Contains("calibration honoured: yes") ? AutoOutcome.Pass : AutoOutcome.Fail,
        },
        new HardwareCheck
        {
            Id = "J", Name = "Unexpected movement while driving", Milestone = "M10", Phase = PhaseReactions, MovesRobot = true,
            Subsystem = "The unexpected-movement detector and its reaction",
            Why = "The detector suspends during direct drive, so it can only be provoked while a behaviour is "
                + "driving the body. Its thresholds came from the engine and have never been pushed against.",
            Setup = "Clear flat surface with room for him to turn on the spot. Requires the OBB.",
            Prerequisites = new[] { "H passed", "--obb given", "a hand free to hold him" },
            DoThis = "The tool turns him on the spot in bursts and says HOLD HIM NOW each time. Hold him by the "
                   + "body so he cannot turn - do not lift him off the surface, or he will react to being "
                   + "picked up instead. Let go between bursts.",
            Success = "He plays the startled reaction once he is held, and nothing fires while he turns freely.",
            Question = "Did he react to being held while his wheels were turning, and not while turning freely?",
            ExpectedTelemetry = "an unexpected movement report naming the side, then the reaction, inside the window the tool drives",
            AutoRule = "the unexpected-movement reaction fires during the window in which the tool drives the wheels",
            Evidence = new[]
            {
                new EvidenceItem("window", "window UnexpectedMovement"),
                new EvidenceItem("expected", "expected reactions"),
                new EvidenceItem("bursts", "HOLD HIM NOW"),
                new EvidenceItem("detections", "unexpected movement"),
            },
            FidelityRecords = new[] { "M10-002", "M10-006", "M10-007" },
            NeedsHandling = true, NeedsObb = true, Requires = new[] { "H" },
            Timeout = TimeSpan.FromMinutes(5),
            Command = o => o.WithObb("reactions", o.Ip).Concat(new[] { "--expect", "UnexpectedMovement", "--provoke-movement",
                                                                      "--seconds", "60", "--acceptance", o.Acceptance("J") }).ToArray(),
            Judge = r => r.Contains("expected reactions: all seen") ? AutoOutcome.Pass : AutoOutcome.Fail,
        },
        new HardwareCheck
        {
            Id = "M", Name = "The cube reactions with a real cube", Milestone = "M11", Phase = PhaseReactions, MovesRobot = true,
            Subsystem = "AcknowledgeObject and ReactToCubeMoved against a real, observed cube",
            Why = "These reactions depend on the world model noticing a cube moved, which depends on the whole "
                + "vision front end. Offline they run against synthetic observations.",
            Setup = "A connected cube in view, clear floor. Requires the OBB.",
            Prerequisites = new[] { "K passed", "--obb given" },
            DoThis = "Two prompts, one at a time. First slide the cube about 10 cm while he is looking at it. "
                   + "Then, when the second prompt comes, turn him away from the cube and roll it.",
            Success = "He looks at the cube's new place and nods to it. Turned away and hearing it move, he "
                    + "turns back to where it was and reacts to finding or not finding it.",
            Question = "Did he acknowledge the moved cube, and then react when it moved out of his sight?",
            ExpectedTelemetry = "ObjectPositionUpdated fires in the first window and CubeMoved in the second",
            AutoRule = "both named cube reactions fire, each in its own window",
            Evidence = new[]
            {
                new EvidenceItem("windows", "window "),
                new EvidenceItem("expected", "expected reactions"),
                new EvidenceItem("reactions", "REACTION") { Limit = 40 },
                new EvidenceItem("objects", "Known at"),
            },
            FidelityRecords = new[] { "M11-009", "M4-009", "M7-001", "M7-002", "M7-011", "M10-003", "M10-004" },
            NeedsCube = true, NeedsHandling = true, NeedsObb = true, Requires = new[] { "K" },
            Timeout = TimeSpan.FromMinutes(5),
            Command = o => o.WithObb("reactions", o.Ip).Concat(new[] { "--expect", "ObjectPositionUpdated,CubeMoved",
                                                                      "--seconds", "160", "--acceptance", o.Acceptance("M") }).ToArray(),
            Judge = r => r.Contains("expected reactions: all seen") ? AutoOutcome.Pass : AutoOutcome.Fail,
        },
        new HardwareCheck
        {
            Id = "CR8", Name = "Picking him up changes the frame everything is in", Milestone = "CORE-008", Phase = PhaseReactions,
            Subsystem = "The pose origin in the robot's state, the history's frame, and what the world forgets",
            Why = "Every RobotState carries a pose origin and nothing in the stack read it, so a pose from "
                + "before a delocalization was treated as an older pose rather than one in a coordinate frame "
                + "that no longer exists. Picking him up and putting him down is what the engine calls a "
                + "delocalization, and it needs hands: it cannot be provoked offline against a real state stream.",
            Setup = "A connected cube in view, and somewhere else on the table to put him down.",
            Prerequisites = new[] { "K passed", "a cube he can see and a second spot to set him down in" },
            DoThis = "Wait until the cube is reported located. Then pick him up, turn him around, and put him "
                   + "down somewhere else facing a different way.",
            Success = "Nothing physical to judge beyond the handling itself: the check is whether the software "
                    + "noticed. What you confirm is that you really did lift him and set him down elsewhere.",
            Question = "Did you lift him clear of the table and set him down somewhere else facing differently?",
            ExpectedTelemetry = "the reported pose origin changes, the map is cleared, and objects that are not carried stop being located",
            AutoRule = "at least one origin change is seen and handled",
            Evidence = new[]
            {
                new EvidenceItem("origins", "origin") { Limit = 40 },
                new EvidenceItem("delocalization", "DELOCALIZED"),
                new EvidenceItem("forgotten objects", "stopped being located"),
                new EvidenceItem("located objects", "located objects") { Limit = 40 },
            },
            FidelityRecords = new[] { "M11-019" }, CoreRegressions = new[] { "CORE-008" },
            NeedsCube = true, NeedsHandling = true, Requires = new[] { "K" },
            Timeout = TimeSpan.FromMinutes(4),
            Command = o => new[] { "core", o.Ip, "--case", "CORE-008", "--seconds", "90", "--acceptance", o.Acceptance("CR8") },
            Judge = r => r.Contains("automated: PASS") ? AutoOutcome.Pass : AutoOutcome.Fail,
        },
        new HardwareCheck
        {
            Id = "CR7", Name = "The ground he looks at reaches the map", Milestone = "CORE-007", Phase = PhaseReactions,
            Subsystem = "The overhead-edge detector, the ground ROI, and the memory map's edge insertion",
            Why = "The detector and the map's side of it were both built and nothing joined them, so the map "
                + "never held an edge however much ground he covered. The join is the engine's own now, and a "
                + "real floor under real light is the only place it can be seen working: synthetic frames "
                + "cannot tell a floor's texture from an obstacle's edge.",
            Setup = "Put him on a clear floor or table facing a visible edge 10-20 cm away: the edge of a "
                  + "table, a dark strip of tape, a book lying flat, or a step.",
            Prerequisites = new[] { "K passed (the calibration is read from the robot)", "a real edge in front of him, well lit" },
            DoThis = "Keep the edge in his view. You may turn him slowly by hand to sweep across it.",
            Success = "Nothing to judge by eye except that he was looking at the edge the whole time; the "
                    + "evidence is in the map's own region counts.",
            Question = "Was there a real edge in his view for the whole run?",
            ExpectedTelemetry = "frames carry edge points and the map accumulates regions, some of them obstacles",
            AutoRule = "frames are processed, edge points are found and the map holds at least one region",
            Evidence = new[]
            {
                new EvidenceItem("per-second counts", "frames") { Limit = 60 },
                new EvidenceItem("map regions", "map holds"),
                new EvidenceItem("content types", "region(s) of"),
            },
            FidelityRecords = new[] { "M11-017" }, CoreRegressions = new[] { "CORE-007" },
            NeedsHandling = true, Requires = new[] { "K" },
            Timeout = TimeSpan.FromMinutes(3),
            Command = o => new[] { "core", o.Ip, "--case", "CORE-007", "--seconds", "45", "--acceptance", o.Acceptance("CR7") },
            Judge = r => r.Contains("automated: PASS") ? AutoOutcome.Pass : AutoOutcome.Fail,
        },

        // ================================================================ 11. navigation
        new HardwareCheck
        {
            Id = "L", Name = "SetBodyAngle semantics", Milestone = "M11", Phase = PhaseNavigation, MovesRobot = true,
            Subsystem = "The body-angle message: whether the angle is absolute or relative, and its packing",
            Why = "TurnTowardsPose depends on which one it is. Getting it wrong turns him the wrong way by "
                + "however far he happens to be facing, which looks like a planner fault and is not.",
            Setup = "Clear floor, room to turn in place. Note which way he is facing before it starts.",
            Prerequisites = new[] { "MOV passed" },
            DoThis = "Watch how far he turns and compare it with where he started.",
            Success = "He turns 45 degrees left in place, smoothly, and stops.",
            Question = "Did he turn about 45 degrees in place and stop smoothly?",
            ExpectedTelemetry = "the tool prints ABSOLUTE body angle confirmed for a 45 degree request",
            AutoRule = "the turn matches the request and the tool names the semantics it confirmed",
            Evidence = new[] { new EvidenceItem("verdict", "body angle"), new EvidenceItem("turn", "turned") },
            FidelityRecords = new[] { "M11-014", "M11-015" },
            Requires = new[] { "MOV" },
            Timeout = TimeSpan.FromMinutes(3),
            Command = o => new[] { "bodyangle", o.Ip, "--deg", "45", "--acceptance", o.Acceptance("L") },
            Judge = r => r.Contains("ABSOLUTE body angle confirmed") ? AutoOutcome.Pass
                       : r.ContainsAny("RELATIVE", "did not turn") ? AutoOutcome.Fail : AutoOutcome.Fail,
        },
        new HardwareCheck
        {
            Id = "Q", Name = "Drive to the pre-dock pose", Milestone = "M12", Phase = PhaseNavigation, MovesRobot = true,
            Subsystem = "The path sender, the planner's three segments and DriveToPoseAction",
            Why = "The segment packings were read from the doler but never driven end to end against a cube "
                + "the robot located itself.",
            Setup = "A connected cube 15-30 cm ahead, clear floor.",
            Prerequisites = new[] { "K passed", "a connected cube in view, 15-30 cm ahead" },
            DoThis = "Watch the turn, the straight and the final turn. Look for jerks between segments.",
            Success = "He turns, drives straight, turns to face the cube and stops about 75 mm from its face. "
                    + "No jerks between segments.",
            Question = "Did he arrive squarely in front of the cube with no jerk between segments?",
            ExpectedTelemetry = "path started and completed, then DriveToPoseAction success",
            AutoRule = "the path completes and the drive action succeeds",
            Evidence = new[]
            {
                new EvidenceItem("path", "path") { Limit = 40 },
                new EvidenceItem("segments", "AppendPathSegment"),
                new EvidenceItem("result", "DriveTo"),
            },
            FidelityRecords = new[] { "M12-002", "M12-011", "M13-011" },
            NeedsCube = true, Requires = new[] { "K" },
            Timeout = TimeSpan.FromMinutes(4),
            Command = o => o.WithObb("manip", o.Ip).Concat(new[] { "--driveto", "--acceptance", o.Acceptance("Q") }).ToArray(),
            Judge = r => r.ContainsAny("DriveToPoseAction.CheckIfDone.Success", "DriveToObject -> Success") ? AutoOutcome.Pass : AutoOutcome.Fail,
        },
        new HardwareCheck
        {
            Id = "CR4", Name = "A late abort clears its own path and nobody else's", Milestone = "CORE-004", Phase = PhaseNavigation, MovesRobot = true,
            Subsystem = "Path ownership: PathRun, the path id, and the qualified abort",
            Why = "A run that was cancelled or timed out used to clear whatever path the sender had last sent, "
                + "which by then could be another action's. On the robot that is a drive that stops dead half "
                + "way for no visible reason. This installs a path, replaces it, and then lets the first one "
                + "clean up late.",
            Setup = "Clear floor with about 20 cm ahead of him.",
            Prerequisites = new[] { "MOV passed", "a clear, flat surface with no drop at the edge" },
            DoThis = "Watch whether he completes the second, longer drive or stops part way.",
            Success = "He drives the longer path all the way and stops at the end of it, not part way through.",
            Question = "Did he complete the longer drive instead of stopping part way?",
            ExpectedTelemetry = "the first run's abort sends nothing, and the second path reports Completed",
            AutoRule = "the late abort sends no clear and the second path completes",
            Evidence = new[]
            {
                new EvidenceItem("path ids", "path"),
                new EvidenceItem("late abort", "abort sent a clear"),
                new EvidenceItem("terminal event", "finished as"),
                new EvidenceItem("verdict", "automated:"),
            },
            FidelityRecords = new[] { "M12-002" }, CoreRegressions = new[] { "CORE-004" },
            Requires = new[] { "MOV" },
            Timeout = TimeSpan.FromMinutes(3),
            Command = o => new[] { "core", o.Ip, "--case", "CORE-004", "--acceptance", o.Acceptance("CR4") },
            Judge = r => r.Contains("automated: PASS") ? AutoOutcome.Pass : AutoOutcome.Fail,
        },
        new HardwareCheck
        {
            Id = "X", Name = "The lattice planner on the robot", Milestone = "M13", Phase = PhaseNavigation, MovesRobot = true,
            Subsystem = "The lattice planner's arcs and the firmware's arc segment handling",
            Why = "The arc packing was read from the engine but never driven. This also confirms the correction "
                + "that makes a planning failure refuse to drive rather than go straight through.",
            Setup = "A connected target cube, and a second cube placed between him and it. Requires the OBB.",
            Prerequisites = new[] { "K and Q passed", "two cubes: one target, one obstacle in the way", "--obb given" },
            DoThis = "Watch whether he curves around the obstacle or drives at it.",
            Success = "He curves around the cube in the way and arrives at the pre-dock pose. The arcs are "
                    + "smooth, so the firmware accepted the arc layout.",
            Question = "Did he curve around the obstacle and arrive, with smooth arcs?",
            ExpectedTelemetry = "a lattice plan with at least one obstacle, arc segments sent, and the drive succeeds",
            AutoRule = "a lattice plan with at least one obstacle, arc segments sent, and the drive succeeds",
            Evidence = new[]
            {
                new EvidenceItem("plan", "lattice plan"),
                new EvidenceItem("arcs", "AppendPathSegmentArc"),
                new EvidenceItem("result", "DriveToObject"),
            },
            FidelityRecords = new[] { "M13-001", "M13-003", "M13-004", "M13-005", "M13-011", "M12-002" },
            NeedsCube = true, NeedsObb = true, Requires = new[] { "K", "Q" },
            Timeout = TimeSpan.FromMinutes(5),
            Command = o => o.WithObb("manip", o.Ip).Concat(new[] { "--driveto", "--acceptance", o.Acceptance("X") }).ToArray(),
            // Q drives to the same pose with nothing in the way; what makes this X is an obstacle in the plan
            Judge = r => r.Contains("lattice plan") && !r.Contains("0 obstacle(s)") && r.Contains("obstacle")
                      && r.Contains("DriveToObject -> Success") ? AutoOutcome.Pass : AutoOutcome.Fail,
        },

        // ================================================================ 12. manipulation
        new HardwareCheck
        {
            Id = "N", Name = "Pick up a cube", Milestone = "M12", Phase = PhaseManipulation, MovesRobot = true,
            Subsystem = "DockWithObject, the docking error signal and the pick-and-place result",
            Why = "The docking exchange was transcribed from the engine and the unread fields of DockWithObject "
                + "are the first suspect if the firmware ignores it. Nothing offline can settle that.",
            Setup = "A connected cube 15-30 cm ahead, one face towards him, clear floor.",
            Prerequisites = new[] { "K and Q passed", "a connected cube 15-30 cm ahead" },
            DoThis = "Watch the approach and the lift. Be ready to catch the cube.",
            Success = "He drives to about 75 mm in front of the face, docks smoothly, lifts the cube and holds it.",
            Question = "Did he dock smoothly and end up holding the cube?",
            ExpectedTelemetry = "path completed, docking with the marker, error signals sent, BlockPickedUp, carrying set",
            AutoRule = "the pick-and-place result reports the block picked up",
            KnownIssue = "If the robot ignores DockWithObject or fails at once, the unread fields of that "
                       + "message are the first suspect (MANIPULATION.md section 4).",
            Evidence = new[]
            {
                new EvidenceItem("docking", "Docking with marker"),
                new EvidenceItem("error signal", "DockingErrorSignal") { Limit = 80 },
                new EvidenceItem("result", "PickAndPlaceResult"),
                new EvidenceItem("carrying", "carrying"),
            },
            FidelityRecords = new[] { "M12-001", "M12-003", "M12-004", "M12-005", "M12-007", "M12-008", "M12-009", "M12-013", "M12-014" },
            NeedsCube = true, Requires = new[] { "K", "Q" },
            Timeout = TimeSpan.FromMinutes(5),
            Command = o => o.WithObb("manip", o.Ip).Concat(new[] { "--pickup", "--acceptance", o.Acceptance("N") }).ToArray(),
            Judge = r => r.ContainsAny("BlockPickedUp", "Pickup -> Success") ? AutoOutcome.Pass : AutoOutcome.Fail,
        },
        new HardwareCheck
        {
            Id = "O", Name = "Place a carried cube on the ground", Milestone = "M12", Phase = PhaseManipulation, MovesRobot = true,
            Subsystem = "PlaceObjectOnGround and the carry state",
            Why = "The place height and the back-off distance came from the engine; whether they release the "
                + "cube cleanly is physical.",
            Setup = "Right after N, or with the cube set on the lift by hand.",
            Prerequisites = new[] { "N passed, or the cube placed on his lift by hand" },
            DoThis = "Watch him lower the lift, release the cube and back away from it. Note whether the "
                   + "cube is left upright and square on the floor.",
            Success = "He lowers the lift and backs off the cube, leaving it upright on the floor.",
            Question = "Did he put the cube down cleanly and back away from it?",
            ExpectedTelemetry = "BlockPlaced and the carrying flag clears",
            AutoRule = "the result reports the block placed and carrying clears",
            Evidence = new[] { new EvidenceItem("result", "PickAndPlaceResult"), new EvidenceItem("carrying", "carrying") },
            FidelityRecords = new[] { "M12-006", "M12-015" },
            NeedsCube = true, Requires = new[] { "N" },
            Timeout = TimeSpan.FromMinutes(4),
            Command = o => o.WithObb("manip", o.Ip).Concat(new[] { "--putdown", "--acceptance", o.Acceptance("O") }).ToArray(),
            Judge = r => r.ContainsAny("BlockPlaced", "PlaceObjectOnGround -> Success") ? AutoOutcome.Pass : AutoOutcome.Fail,
        },
        new HardwareCheck
        {
            Id = "P", Name = "Roll a cube", Milestone = "M12", Phase = PhaseManipulation, MovesRobot = true,
            Subsystem = "The roll dock action and the cube's own up-axis report",
            Why = "The roll is judged by the cube's accelerometer, so it is the one manipulation whose success "
                + "the hardware itself can confirm - but only with a real cube on a real surface.",
            Setup = "A connected cube ahead of him, upright or on its side.",
            Prerequisites = new[] { "K passed", "a connected cube 15-30 cm ahead" },
            DoThis = "Watch him dock against the cube and tip it onto another face. Note which face was up "
                   + "before and after.",
            Success = "He docks and the cube rolls onto another face; the reported up axis changes.",
            Question = "Did the cube roll onto a different face?",
            ExpectedTelemetry = "the roll succeeds and the up axis changes",
            AutoRule = "the roll reports success with a change of up axis",
            Evidence = new[] { new EvidenceItem("roll", "Roll"), new EvidenceItem("up axis", "up axis") },
            FidelityRecords = new[] { "M12-016", "M12-001", "M12-003", "M12-005", "M12-013" },
            NeedsCube = true, Requires = new[] { "K" },
            Timeout = TimeSpan.FromMinutes(4),
            Command = o => o.WithObb("manip", o.Ip).Concat(new[] { "--roll", "--acceptance", o.Acceptance("P") }).ToArray(),
            Judge = r => r.Contains("Roll -> Success") ? AutoOutcome.Pass : AutoOutcome.Fail,
        },
        new HardwareCheck
        {
            Id = "R", Name = "Stack two cubes", Milestone = "M12", Phase = PhaseManipulation, MovesRobot = true,
            Subsystem = "The stacking behaviour: pick up, carry, place on top, and the final animation",
            Why = "Stacking is the first check that chains three actions with the world model between them. "
                + "Its phases came from the engine; whether they survive a real carry is not knowable offline.",
            Setup = "Two connected upright cubes in view, clear floor. Requires the OBB.",
            Prerequisites = new[] { "N passed", "two connected cubes, both upright and in view", "--obb given" },
            DoThis = "Watch the three phases: pick up, carry, place on top.",
            Success = "He picks one up, carries it to the other and places it on top.",
            Question = "Did he end with one cube stacked on the other?",
            ExpectedTelemetry = "the phases PickingUpBlock, StackingBlock and PlayingFinalAnim in order",
            AutoRule = "the three stacking phases run in order",
            Evidence = new[] { new EvidenceItem("phases", "Block"), new EvidenceItem("result", "Success") },
            FidelityRecords = new[] { "M12-012", "M13-007", "M12-001", "M12-003", "M12-005", "M12-009", "M12-015", "M15-012" },
            NeedsCube = true, NeedsObb = true, Requires = new[] { "N", "K" },
            Timeout = TimeSpan.FromMinutes(6),
            Command = o => o.WithObb("manip", o.Ip).Concat(new[] { "--stack", "--acceptance", o.Acceptance("R") }).ToArray(),
            Judge = r => r.ContainsAll("PickingUpBlock", "StackingBlock") ? AutoOutcome.Pass : AutoOutcome.Fail,
        },
        new HardwareCheck
        {
            Id = "S", Name = "Flip a cube", Milestone = "M13", Phase = PhaseManipulation, MovesRobot = true,
            Subsystem = "The flip pre-action pose at the cube's corner and the lift-driven flip",
            Why = "The corner pose and the 135 degree approach were read from the engine. Whether they tip the "
                + "cube rather than stalling against it is physical.",
            Setup = "A connected cube 20-30 cm ahead, clear floor.",
            Prerequisites = new[] { "K passed", "a connected cube 20-30 cm ahead" },
            DoThis = "Watch the corner approach and the lift coming up as he reaches the cube.",
            Success = "He drives at the cube's corner with the lift low, the lift comes up as he reaches it, "
                    + "and the cube tips over his shoulder. He does not stall against it.",
            Question = "Did the cube tip over, without him stalling against it?",
            ExpectedTelemetry = "a Flipping pre-action pose, the flip drive, and DriveAndFlipBlock success",
            AutoRule = "the flip action reports success",
            Evidence = new[] { new EvidenceItem("pre-action", "Flipping"), new EvidenceItem("flip", "FlipBlock"), new EvidenceItem("result", "Success") },
            FidelityRecords = new[] { "M13-002", "M12-001" },
            NeedsCube = true, Requires = new[] { "K" },
            Timeout = TimeSpan.FromMinutes(5),
            Command = o => o.WithObb("manip", o.Ip).Concat(new[] { "--flip", "--acceptance", o.Acceptance("S") }).ToArray(),
            Judge = r => r.Contains("DriveAndFlipBlock -> Success") ? AutoOutcome.Pass : AutoOutcome.Fail,
        },
        new HardwareCheck
        {
            Id = "T", Name = "Knock over a stack", Milestone = "M13", Phase = PhaseManipulation, MovesRobot = true,
            Subsystem = "The knock-over behaviour: the grab attempt, the flip and the success animation",
            Why = "It needs the world model to hold a real stack, which is the part that cannot be simulated "
                + "convincingly.",
            Setup = "Two connected cubes stacked in front of him. Requires the OBB.",
            Prerequisites = new[] { "S passed", "two connected cubes, stacked", "--obb given" },
            DoThis = "Watch him reach for the bottom cube and knock the stack down.",
            Success = "He turns to the stack, drives to about 85 mm, reaches, flips the bottom cube and the "
                    + "stack falls. The success animation plays.",
            Question = "Did the stack come apart when he reached for it?",
            ExpectedTelemetry = "a stack in the world model, the grab attempt, the flip, and KnockOverSuccess",
            AutoRule = "the stack is recognised and the knock-over reports success",
            Evidence = new[] { new EvidenceItem("stack", "stack"), new EvidenceItem("result", "KnockOver") },
            FidelityRecords = new[] { "M13-014", "M13-002" },
            NeedsCube = true, NeedsObb = true, Requires = new[] { "K", "S" },
            Timeout = TimeSpan.FromMinutes(5),
            Command = o => o.WithObb("manip", o.Ip).Concat(new[] { "--knockover", "--acceptance", o.Acceptance("T") }).ToArray(),
            Judge = r => r.ContainsAny("KnockOverSuccess", "the stack came apart") ? AutoOutcome.Pass : AutoOutcome.Fail,
        },
        new HardwareCheck
        {
            Id = "U", Name = "Pop a wheelie", Milestone = "M13", Phase = PhaseManipulation, MovesRobot = true,
            Subsystem = "The wheelie dock action, the retry on a miss, and the cliff-stop re-enable",
            Why = "This is the action that most depends on the docking geometry being right, and the one where "
                + "a miss is most visible. The cliff stop being re-enabled afterwards matters for his safety.",
            Setup = "An upright connected cube ahead, clear floor. Requires the OBB.",
            Prerequisites = new[] { "K passed", "an upright connected cube ahead", "--obb given" },
            DoThis = "Stand by to catch him. Watch the cliff-stop line in the trace.",
            Success = "He docks, rides up onto the cube's edge and drops back. A miss plays the realign "
                    + "animation and tries again, up to three times.",
            Question = "Did he rear up on the cube and come back down safely?",
            ExpectedTelemetry = "PoppedWheelie and the cliff stop re-enabled on stop",
            AutoRule = "PoppedWheelie appears and the cliff stop is re-enabled on stop",
            Evidence = new[] { new EvidenceItem("wheelie", "Wheelie"), new EvidenceItem("cliff stop", "StopOnCliff") },
            FidelityRecords = new[] { "M13-015", "M12-003", "M12-005" },
            NeedsCube = true, NeedsObb = true, Requires = new[] { "K" },
            Timeout = TimeSpan.FromMinutes(5),
            Command = o => o.WithObb("manip", o.Ip).Concat(new[] { "--wheelie", "--acceptance", o.Acceptance("U") }).ToArray(),
            Judge = r => r.ContainsAny("PoppedWheelie", "PopAWheelie -> Success") ? AutoOutcome.Pass : AutoOutcome.Fail,
        },

        // ================================================================ 13. freeplay
        new HardwareCheck
        {
            Id = "Z", Name = "Freeplay on the robot", Milestone = "M15", Phase = PhaseFreeplay, MovesRobot = true,
            Subsystem = "The whole autonomy stack: needs, activities, choosers, behaviours and reactions",
            Why = "Everything above, running together and choosing for itself for five minutes.",
            Setup = "Him on the charger, one connected cube in view, no person in view, clear floor. Requires the OBB.",
            Prerequisites = new[] { "K passed", "a connected cube in view", "the charger placed with him on it", "--obb given" },
            DoThis = "Let it run. Part way through, pick him up and put him down next to the cube.",
            Success = "He leaves the charger, looks around, goes to the cube and plays with it, pauses and "
                    + "looks around between games, and after the put-down heads for the cube. Nothing repeats "
                    + "back to back.",
            Question = "Did he behave like a robot deciding for himself, without repeating or stalling?",
            ExpectedTelemetry = "an activity starts, DriveOffCharger runs, scored picks follow, no NoActivityAvailableError",
            AutoRule = "an activity starts, DriveOffCharger runs, scored picks follow and no NoActivityAvailableError appears",
            Evidence = new[]
            {
                new EvidenceItem("goals", "freeplay_goal_started"),
                new EvidenceItem("decisions", "activity") { Limit = 60 },
                new EvidenceItem("reactions", "REACTION") { Limit = 40 },
                new EvidenceItem("mood", "mood") { Limit = 40 },
                new EvidenceItem("errors", "NoActivityAvailableError"),
            },
            FidelityRecords = new[] { "M15-001", "M15-002", "M15-003", "M15-004", "M15-005", "M15-006", "M15-007", "M15-008", "M15-009", "M15-010", "M15-011", "M15-012", "M8-001", "M8-002", "M8-003", "M8-006", "M8-007", "M7-011", "M7-012", "M7-013", "M7-014", "M10-003", "M10-004" }, CoreRegressions = new[] { "CORE-009" },
            NeedsCube = true, NeedsCharger = true, NeedsHandling = true, NeedsObb = true, Requires = new[] { "K" },
            Timeout = TimeSpan.FromMinutes(8),
            Command = o => (o.AllowNominalCalibration
                ? o.WithObb("freeplay", o.Ip).Concat(new[] { "--seconds", "300", "--nominal", "--acceptance", o.Acceptance("Z") })
                : o.WithObb("freeplay", o.Ip).Concat(new[] { "--seconds", "300", "--acceptance", o.Acceptance("Z") })).ToArray(),
            Judge = r => r.Contains("NoActivityAvailableError") ? AutoOutcome.Fail
                       : r.Contains("freeplay_goal_started") ? AutoOutcome.Pass : AutoOutcome.Fail,
        },

        // ================================================================ 14. long run and stability
        new HardwareCheck
        {
            Id = "Z2", Name = "Fifteen minutes of freeplay, and the mood decaying through it", Milestone = "M15/CORE-009", Phase = PhaseLongRun, MovesRobot = true,
            Subsystem = "Stability over time, and the mood decay that the behaviour scoring reads",
            Why = "Two things only a long run shows. The first is stability: a leak, a thread that stops, an "
                + "audio path that degrades, a world model that fills up. The second is the mood, which "
                + "nothing in production advanced before the core review - an emotion stayed where an event "
                + "left it, and everything that scores behaviours read a value that should long since have "
                + "decayed. Fifteen minutes is long enough for the decay to be visible in the trace and for "
                + "his choices to shift with it.",
            Setup = "As Z, with enough space and battery for a quarter of an hour. Requires the OBB.",
            Prerequisites = new[] { "Z passed", "a charged battery", "a cube and clear floor", "--obb given" },
            DoThis = "Leave him to it. Look in occasionally: he should still be choosing, not stuck or "
                   + "repeating. Handle him once or twice to raise an emotion, then leave him be and watch "
                   + "the mood come back down in the trace.",
            Success = "He is still deciding and moving at the end, with no long stalls, no repeated behaviour "
                    + "back to back, and no degradation in his voice or his movement.",
            Question = "Was he still choosing sensibly at the end, with no stall, no repetition and no "
                     + "degradation in sound or movement?",
            ExpectedTelemetry = "mood values decay between events rather than holding, and activities keep being chosen for the whole run",
            AutoRule = "the run completes with activities chosen throughout and no NoActivityAvailableError",
            Evidence = new[]
            {
                new EvidenceItem("mood", "mood") { Limit = 80 },
                new EvidenceItem("goals", "freeplay_goal_started"),
                new EvidenceItem("decisions", "activity") { Limit = 80 },
                new EvidenceItem("errors", "NoActivityAvailableError"),
                new EvidenceItem("exceptions", "Exception"),
            },
            FidelityRecords = new[] { "M15-001", "M15-002", "M15-003", "M15-004", "M15-005", "M15-006", "M15-007", "M15-008", "M15-009", "M15-010", "M15-011", "M15-012", "M8-001", "M8-002", "M8-003", "M8-006", "M8-007", "M7-011", "M7-012", "M7-013", "M7-014", "M10-003", "M10-004" }, CoreRegressions = new[] { "CORE-009" },
            NeedsCube = true, NeedsHandling = true, NeedsObb = true, Requires = new[] { "Z" },
            Timeout = TimeSpan.FromMinutes(20),
            Command = o => (o.AllowNominalCalibration
                ? o.WithObb("freeplay", o.Ip).Concat(new[] { "--seconds", "900", "--nominal", "--acceptance", o.Acceptance("Z2") })
                : o.WithObb("freeplay", o.Ip).Concat(new[] { "--seconds", "900", "--acceptance", o.Acceptance("Z2") })).ToArray(),
            Judge = r => r.Contains("NoActivityAvailableError") ? AutoOutcome.Fail
                       : r.Contains("freeplay_goal_started") ? AutoOutcome.Pass : AutoOutcome.Fail,
        },

        // ================================================================ 15. blocked on this build
        new HardwareCheck
        {
            Id = "Y", Name = "The face pipeline with a detector", Milestone = "M14", Phase = PhaseBlocked,
            Subsystem = "FaceWorld, the face actions and the face reactions",
            Why = "Everything around the detector is built and tested offline. The detector itself is Omron "
                + "OKAO and is not reproducible, so this check waits for an IFaceDetector implementation.",
            Setup = "Not applicable on this build.",
            DoThis = "Nothing: this check cannot run.",
            Success = "Not applicable.",
            Question = "Not applicable.",
            AutoRule = "not run",
            ExpectedTelemetry = "not run",
            BlockedReason = "BLOCKED_EXTERNAL: VisionSystem.FaceDetector is the OKAO boundary and reports itself "
                          + "unavailable. Attach an IFaceDetector implementation and this check becomes runnable. "
                          + "You will not be asked to perform it.",
            FidelityRecords = new[] { "M14-001", "M14-002", "M14-003", "M14-004", "M14-005", "M14-007", "M11-016" },
            Command = o => o.WithObb("reactions", o.Ip).Concat(new[] { "--seconds", "120" }).ToArray(),
            Judge = _ => AutoOutcome.Skipped,
        },
    };
}
