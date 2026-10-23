using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Transport;

namespace Cozmo.Conformance;

/// <summary>
/// Operator-run drive-off-the-charger check, in the M5-HW format. Cozmo starts on the charger contacts; the shipped
/// DriveOffCharger behaviour drives him off. Graded on the cliff-detect threshold wire (M4-019 C7/C8), the platform
/// flag (M13-040) and the behaviour's end (M13-017). OBSERVED is evidence, never layer acceptance.
/// </summary>
public static class M13DriveOffHw
{
    public const string TestId = "M13-DRIVEOFF-HW";
    public sealed record CheckDef(string Id, string Title, string[] Records);
    public static readonly CheckDef[] Checks = {
        new("ON_CHARGER", "On the contacts: platform set, threshold 50 sent twice, no 400", new[]{"M4-019","M13-040","M13-017"}),
        new("HOLD_50", "No 400 before more than 50 mm of travel, after the contacts drop", new[]{"M4-019","M13-040"}),
        new("DISTANCE_400", "400 after more than 50 mm, then 400 at the platform exit", new[]{"M4-019"}),
        new("PLATFORM_EXIT", "The platform clears once, only after the contacts dropped", new[]{"M4-019","M13-040","M13-024"}),
        new("BEHAVIOUR_END", "DriveOffCharger ends only off the platform with no action running", new[]{"M13-017"}),
        new("DRIVING_ANIMATIONS", "Driving animations behind the SevereNeeds gate", new[]{"M13-017","M5-063"}),
        new("DRIVE_OBSERVATIONS", "Drive requests, cliff reports, stops and what you saw", new[]{"M13-017","M13-013","M4-019"}),
    };
    public static readonly string[] RecordIds = Checks.SelectMany(x => x.Records).Concat(HarnessStartup.RecordIds).Distinct().ToArray();
    public sealed record SourceRule(string Check, string Record, string Citation, string Comparison);
    public static readonly SourceRule[] SourceRules = {
        new("ON_CHARGER", "M4-019", "0x00512D80..0x00512DA2; 0x00511D44..0x00511DCA (send 0x00511DA0)",
            "The first frame-matched state moves the travel accumulator from -1 to 0 and sends 50; the rising contact edge sets the platform, whose change sends 50. Before the drive the wire carries exactly 50, 50 and no 400 (no other sender runs: increments need a cache of at least 151, the 400 restore runs only from Delocalize)."),
        new("ON_CHARGER", "M13-040", "0x00512AAC..0x00512AB4; 0x00511990..0x00511C2D",
            "A synchronized RobotState with IS_ON_CHARGER (bit 12) calls SetOnCharger, which sets the platform flag on the rising edge."),
        new("ON_CHARGER", "M13-017", "0x005C0B10",
            "IsRunnableInternal returns the platform flag (robot+0x34A), so DriveOffCharger is runnable on the contacts."),
        new("HOLD_50", "M4-019", "0x00512E2A..0x00512E96; 0x00511D4C..0x00511D6A",
            "The travel is the summed XY displacement of MoveRobotPoseForward(pose, -20 mm) over frame-matched states; 400 is sent only once it is strictly greater than 50.0 (ble at 0x00512E96). SetOnCharger(false) leaves the platform set, so the contacts dropping sends nothing."),
        new("HOLD_50", "M13-040", "0x00513CD8..0x00513E2A",
            "The platform clears only when the robot's footprint leaves the located charger's; until then no platform change sends 400."),
        new("DISTANCE_400", "M4-019", "0x00512E84..0x00512EA6; 0x00511DA0",
            "One 400 on the first state whose travel exceeds 50 mm (flag +0x52C, set once); the platform exit sends 400 again. After the crossing state exactly two 400s, or one if the platform never cleared."),
        new("PLATFORM_EXIT", "M4-019", "0x00511D4C..0x00511D6A",
            "SetOnChargerPlatform(b) stores b or the contacts flag, so the platform can clear only after the contacts dropped, and changes once."),
        new("PLATFORM_EXIT", "M13-040", "0x00513CD8..0x00513E2A; 0x00514DB2..0x00514DD8",
            "The per-tick footprint test clears the platform when the robot quad stops intersecting the charger quad; straight back off the contacts that is at x > 85.9 mm (research/20261023-m13-017-groundwork-tests-claude-check.md). The exit position is recorded, not graded."),
        new("PLATFORM_EXIT", "M13-024", "0x00513CD8..0x00513E2A",
            "The footprint quads come from minAreaRect; the exit position depends on them and is an observation here."),
        new("BEHAVIOUR_END", "M13-017", "0x005C0DA8..0x005C0E32",
            "UpdateInternal keeps running while the platform is set; off the platform it keeps running while an action is current and otherwise stamps the whiteboard and returns 2."),
        new("DRIVING_ANIMATIONS", "M13-017", "0x005C0B34..0x005C0B50",
            "KNOWN GAP: the push of (0x97,0x93,0x8F) is gated on SevereNeeds state 3, whose writer has no live owner in the stack (R-M15-SEVERE-INPUT); the stack reports the missing piece."),
        new("DRIVING_ANIMATIONS", "M5-063", "0x004F0D80..0x004F0D8C",
            "Without the push, the driving animation handler keeps its default triplet; recorded only."),
        new("DRIVE_OBSERVATIONS", "M13-017", "0x005C09D4..0x005C09E6; 0x005C0C02..0x005C0C08; config/engine/behaviorSystem/behaviors/freeplay/driveOffCharger.json",
            "The drive is a DriveStraightAction of 96.0f + extraDistanceToDrive_mm (60.0) = 156 mm, re-driven while the platform stays set. Recorded for review, not graded (M13-013 path gaps)."),
        new("DRIVE_OBSERVATIONS", "M13-013", "0x005470F0..0x0054725E", "DriveStraightAction path details are recorded only."),
        new("DRIVE_OBSERVATIONS", "M4-019", "0x0053582C..0x00535998", "Cliff and stop reports from the robot are recorded only; a PotentialCliff on the platform is ignored by the engine."),
    };

    public sealed record CheckGuide(string CozmoDoes, string Checks, string YourJob, string Duration, bool Watched);
    public static readonly (string Id, CheckGuide Guide)[] Parts = {
        ("ON_CHARGER", new("sits on the charger. Nothing moves.",
            "the stack sees him on the charger and has sent the 'on the charger' cliff setting, the way the original engine does.",
            "Nothing. This part is automatic.", "about 3 s", false)),
        ("DRIVE_OFF", new("drives forward off the charger, about 16 cm, and stops on the floor.",
            "the cliff setting changes at the same distances as in the original engine, and the drive ends when the engine's would.",
            "Watch Cozmo drive off (up to 30 s). Don't touch him until TEST FINISHED.", "up to 30 s", true)),
    };
    public static string PartHeader(int number, string id)
    {
        var guide = Parts.Single(p => p.Id == id).Guide;
        return $"\n=== PART {number} of {Parts.Length}: {id} ({guide.Duration}) ===\n"
            + $"Cozmo {guide.CozmoDoes}\nThe program checks that {guide.Checks}\n"
            + (guide.Watched ? ">>> YOUR JOB: " : "Your job: ") + guide.YourJob;
    }
    public static readonly M5Hw.ObservationPlan DrivePlan = new("DRIVE", "Cozmo driving off the charger", "Cozmo and the charger",
        "until STOP WATCHING (up to 30 seconds)",
        "Cozmo drives forward off the charger and should stop on the floor about 16 cm away.", new[] {
            new M5Hw.ObservationQuestion("droveOff", "Did Cozmo drive all the way OFF the charger onto the floor?", M5Hw.ObservationChoices),
            new M5Hw.ObservationQuestion("stoppedOnCharger", "Did he STOP suddenly while still on the charger, then maybe start again?", M5Hw.ObservationChoices) });
    public static void Overview(TextWriter output)
    {
        output.WriteLine($"The test has {Parts.Length} parts. You only watch in the part marked WATCH.\n");
        for (int i = 0; i < Parts.Length; i++)
            output.WriteLine($"  {i + 1,2}. {Parts[i].Id,-12} {Parts[i].Guide.Duration,-11} {(Parts[i].Guide.Watched ? "WATCH: Cozmo and the charger" : "automatic")}");
        output.WriteLine("\nTotal: under a minute, plus about a minute to connect, plus your answers.");
    }
    public const string SetupText = "SETUP, before you press Enter:\n"
        + "  1. Put Cozmo ON the charger, backed on so the contacts touch (his backpack lights show he is charging).\n"
        + "  2. Put the charger on the FLOOR with at least 30 cm of clear, flat floor in front of it.\n"
        + "  3. Do not touch Cozmo from the start until TEST FINISHED is printed, including while you answer the questions.\n"
        + "     If he is moved or lifted, the run can't be checked.\n"
        +"  4. If he keeps driving towards an edge or an obstacle, press Ctrl+C (the run is still saved).";
    public static void Preview(TextWriter? output = null)
    {
        output ??= Console.Out;
        output.WriteLine("DRIVE-OFF-CHARGER TEST PREVIEW (no robot needed). This is exactly what the real run will show you.\n");
        output.WriteLine(SetupText + "\n");
        Overview(output);
        output.WriteLine(PartHeader(1, "ON_CHARGER"));
        output.WriteLine(PartHeader(2, "DRIVE_OFF"));
        output.WriteLine(M5Hw.DescribeWindow(DrivePlan) + "\n[you press Enter]\n>>> WATCH NOW <<<\n>>> STOP WATCHING <<<\n");
        foreach (var q in DrivePlan.Questions) output.WriteLine(M5Hw.DescribeQuestion(q) + "\n");
        output.WriteLine("Then the results, one line per check:");
        foreach (var c in Checks) output.WriteLine($"  {c.Id}: {c.Title}");
        output.WriteLine("\nEngine source for the checks: " + string.Join("; ", SourceRules.Select(r => $"{r.Check} {r.Record} {r.Citation}")));
    }

    public static string ResultKind(JsonObject result) => (result["verdict"]?.GetValue<string>() ?? "INCONCLUSIVE") switch
    {
        "MATCH" => "MATCH",
        "MISMATCH" => "MISMATCH",
        "KNOWN_GAP" => "KNOWN_GAP",
        "OBSERVATION" => "OBSERVATION",
        _ => "INCOMPLETE"
    };
    public static string DescribeResult(JsonObject result)
    {
        string kind = ResultKind(result);
        string meaning = kind switch
        {
            "MATCH" => "OK. The stack did what the original engine does.",
            "MISMATCH" => "PROBLEM. The stack did something the original engine would not. Needs review.",
            "KNOWN_GAP" => "KNOWN MISSING PIECE. Reported as expected; not a failure of this run.",
            "OBSERVATION" => "SAVED FOR REVIEW. Nothing here can pass or fail.",
            _ => "COULD NOT CHECK. Setup or evidence was missing (reason below). This is not a robot or stack failure by itself."
        };
        var reasons = new List<string>();
        foreach (string name in new[] { "missing", "problems" })
        {
            if (result[name] is JsonValue value && value.TryGetValue<string>(out var note)) reasons.Add(note);
            else if (result[name] is JsonArray notes) reasons.AddRange(notes.OfType<JsonValue>().Select(n => n.ToString()));
        }
        if (kind is "INCOMPLETE" or "MISMATCH" && reasons.Count == 0) reasons.Add("No reason was recorded for this result (harness defect; send the folder for review).");
        return $"{result["id"]}: {meaning} [{kind}]" + (reasons.Count == 0 ? "" : "\n  Details: " + string.Join(" ", reasons.Distinct()));
    }
    public static string OverallLine(IReadOnlyCollection<JsonObject> results, bool runErrors)
    {
        int problems = results.Count(r => ResultKind(r) == "MISMATCH"), incomplete = results.Count(r => ResultKind(r) == "INCOMPLETE");
        if (problems > 0) return $"OVERALL: {problems} PROBLEM(S) found. Send this folder for review.";
        if (incomplete > 0 || runErrors) return $"OVERALL: no problems found, but {incomplete} check(s) could not be done"
            + (runErrors ? " and the run reported errors" : "") + ". Send this folder for review.";
        return "OVERALL: no problems found in any check. Send this folder for review.";
    }
    public static string? CheckDefinitionProblem()
    {
        if (Checks.Any(c => !SourceRules.Any(r => r.Check == c.Id))) return "check has no source/known-gap basis";
        if (SourceRules.Any(r => !Checks.Any(c => c.Id == r.Check && c.Records.Contains(r.Record))
            || string.IsNullOrWhiteSpace(r.Citation) || string.IsNullOrWhiteSpace(r.Comparison))) return "invalid or uncited comparison";
        return null;
    }
    public static List<JsonObject> LoadRecords(string path, List<string> warnings)
    {
        if (CheckDefinitionProblem() is { } problem) throw new InvalidOperationException(problem);
        var all = JsonNode.Parse(File.ReadAllText(path))!["records"]!.AsArray().Select(x => x!.AsObject()).ToDictionary(x => x["id"]!.GetValue<string>());
        var missing = RecordIds.Where(id => !all.ContainsKey(id)).ToArray();
        if (missing.Length > 0) throw new InvalidOperationException("Missing fidelity records: " + string.Join(", ", missing));
        foreach (var id in RecordIds) if (all[id]["status"]!.GetValue<string>() is "HARDWARE_ONLY" or "IMPLEMENTATION_GAP" or "RECOVERABLE_GAP" or "BLOCKED_EXTERNAL") warnings.Add(id + ": " + all[id]["status"]);
        return RecordIds.Select(id => (JsonObject)all[id].DeepClone()).ToList();
    }

    // ---- Grading. Pure functions over the captured wire, so harness tests can feed them directly. ----

    /// <summary>One inbound RobotState in wire order. Position is its index in the ordered wire list.</summary>
    public sealed record StateRow(int Position, double AtMs, uint FrameId, uint OriginId, float X, float Y, float Angle, uint Status)
    {
        public bool Contact => (Status & (uint)RobotStatusFlag.IsOnCharger) != 0;   // M2-002: IS_ON_CHARGER bit 12
    }
    public sealed record ThresholdSend(int Position, double AtMs, ushort Value);
    /// <summary>
    /// One run's evidence. Grading stops at <paramref name="DriveEndMs"/> (behaviour end plus the 1.5 s trailing wait), so
    /// anything after the drive (the operator moving Cozmo while answering) is not graded.
    /// </summary>
    public sealed record Capture(IReadOnlyList<M5Hw.WireMessage> Wire, double DriveStartMs, bool PlatformAtDriveStart,
        IReadOnlyList<double> PlatformFalseAtMs, bool RestoreLogged, double DriveEndMs = double.MaxValue);

    public static List<StateRow> States(IReadOnlyList<M5Hw.WireMessage> wire)
    {
        var rows = new List<StateRow>();
        for (int i = 0; i < wire.Count; i++)
        {
            var m = wire[i];
            if (m.Outbound || m.Payload.Length == 0 || m.Payload[0] != (byte)RobotMessageId.State) continue;
            var s = RobotState.Read(new CladReader(m.Payload.AsMemory(1)));
            rows.Add(new(i, m.AtMs, s.PoseFrameId, s.PoseOriginId, s.Pose.X, s.Pose.Y, s.Pose.Angle, s.Status));
        }
        return rows;
    }
    public static List<ThresholdSend> Thresholds(IReadOnlyList<M5Hw.WireMessage> wire)
    {
        var rows = new List<ThresholdSend>();
        for (int i = 0; i < wire.Count; i++)
        {
            var m = wire[i];
            if (m.Outbound && m.Payload.Length == 3 && m.Payload[0] == (byte)RobotMessageId.SetCliffDetectThreshold)
                rows.Add(new(i, m.AtMs, BitConverter.ToUInt16(m.Payload, 1)));
        }
        return rows;
    }

    /// <summary>M4-019 C7 D3 (0x00517ED0..0x00517F3E): MoveRobotPoseForward(pose, -20 mm), not carrying (0x00512D14).</summary>
    static (float X, float Y) DriveCentre(float x, float y, float angle) => (x + -20f * MathF.Cos(angle), y + -20f * MathF.Sin(angle));
    /// <summary>
    /// The engine's accumulated travel after each state (0x00512E10..0x00512E80): per-component f32 difference, sum of
    /// squares, f32 sqrt, added to +0x528. The first state's term is taken from the identity pose (the constructor pose
    /// before the first state); <paramref name="fromIdentity"/> false drops that term, so callers can reject a run whose
    /// crossing depends on it.
    /// </summary>
    public static float[] Travel(IReadOnlyList<StateRow> states, bool fromIdentity)
    {
        var sums = new float[states.Count]; float total = 0f;
        var previous = fromIdentity ? DriveCentre(0f, 0f, 0f) : states.Count > 0 ? DriveCentre(states[0].X, states[0].Y, states[0].Angle) : default;
        for (int i = 0; i < states.Count; i++)
        {
            var c = DriveCentre(states[i].X, states[i].Y, states[i].Angle);
            float dx = c.X - previous.X, dy = c.Y - previous.Y;
            total += MathF.Sqrt(dx * dx + dy * dy);
            sums[i] = total; previous = c;
        }
        return sums;
    }
    const float Limit = 50.0f;           // 0x00512E8A literal
    const float AmbiguityMm = 0.01f;     // harness margin for libm cos/sin rounding; a closer state is not graded

    static JsonObject Row(string verdict, string? missing = null) => new() { ["verdict"] = verdict, ["missing"] = missing };
    static JsonArray Sends(IEnumerable<ThresholdSend> sends) => new(sends.Select(s => (JsonNode)new JsonObject { ["atMs"] = s.AtMs, ["value"] = s.Value, ["wirePosition"] = s.Position }).ToArray());

    /// <summary>Grades ON_CHARGER, HOLD_50, DISTANCE_400 and PLATFORM_EXIT from one capture.</summary>
    public static Dictionary<string, JsonObject> GradeThresholds(Capture c)
    {
        var results = new Dictionary<string, JsonObject>();
        var wire = c.Wire.Where(m => m.AtMs <= c.DriveEndMs).ToList();
        var platformFalse = c.PlatformFalseAtMs.Where(t => t <= c.DriveEndMs).ToList();
        var states = States(wire); var sends = Thresholds(wire);
        var before = states.Where(s => s.AtMs < c.DriveStartMs).ToList();
        var sendsBefore = sends.Where(s => s.AtMs < c.DriveStartMs).ToList();
        var sendsAfter = sends.Where(s => s.AtMs >= c.DriveStartMs).ToList();

        // ON_CHARGER
        JsonObject on;
        if (before.Count == 0) on = Row("INCONCLUSIVE", "No robot state arrived before the drive.");
        else if (!before[^1].Contact) on = Row("INCONCLUSIVE", "Cozmo was not on the charger contacts when the drive was due (put him on the charger before starting and run again).");
        else if (c.RestoreLogged) on = Row("INCONCLUSIVE", "The stack logged RestoringCliffDetectThreshold (a delocalization, for example Cozmo being lifted); the 50/400 schedule can't be checked.");
        else
        {
            var problems = new JsonArray();
            if (!c.PlatformAtDriveStart) problems.Add("Cozmo was on the contacts but the stack's platform flag was not set.");
            if (!sendsBefore.Select(s => s.Value).SequenceEqual(new ushort[] { 50, 50 }))
                problems.Add($"Before the drive the threshold messages were [{string.Join(", ", sendsBefore.Select(s => s.Value))}]; the engine sends exactly [50, 50].");
            on = Row(problems.Count == 0 ? "MATCH" : "MISMATCH");
            if (problems.Count > 0) on["problems"] = problems;
        }
        on["sendsBeforeDrive"] = Sends(sendsBefore); on["platformAtDriveStart"] = c.PlatformAtDriveStart;
        on["contactAtDriveStart"] = before.Count > 0 && before[^1].Contact;
        results["ON_CHARGER"] = on;

        // Travel, needed by the other three.
        string? travelProblem = null;
        if (before.Count == 0 || !before[^1].Contact) travelProblem = "Cozmo did not start on the charger contacts.";
        else if (c.RestoreLogged) travelProblem = "A delocalization restored 400 during the drive (Cozmo lifted or moved, or a stack fault; review the bundle).";
        else if (states.Any(s => s.FrameId != states[0].FrameId || s.OriginId != states[0].OriginId))
            travelProblem = "The robot's pose frame or origin changed during the drive (Cozmo lifted or moved, or a stack fault; review the bundle); the travel can't be recomputed.";
        var travel = Travel(states, true); var bare = Travel(states, false);
        int cross = Array.FindIndex(travel, t => t > Limit), crossBare = Array.FindIndex(bare, t => t > Limit);
        if (travelProblem is null && cross < 0) travelProblem = $"Cozmo travelled only {(travel.Length == 0 ? 0 : travel[^1]):F1} mm; more than 50 mm is needed.";
        if (travelProblem is null && (cross != crossBare || MathF.Abs(travel[cross] - Limit) < AmbiguityMm || cross > 0 && MathF.Abs(travel[cross - 1] - Limit) < AmbiguityMm))
            travelProblem = "The 50 mm crossing state is within rounding of the limit; it is not graded.";
        var crossing = travelProblem is null ? states[cross] : null;
        var contactDrop = states.FirstOrDefault(s => s.AtMs >= c.DriveStartMs && !s.Contact);
        int dropIndex = contactDrop is null ? -1 : states.IndexOf(contactDrop);
        JsonObject Travelling() => new()
        {
            ["crossingState"] = crossing is null ? null : new JsonObject { ["atMs"] = crossing.AtMs, ["wirePosition"] = crossing.Position, ["travelMm"] = travel[cross], ["previousTravelMm"] = cross > 0 ? travel[cross - 1] : null },
            ["contactsDropped"] = contactDrop is null ? null : new JsonObject { ["atMs"] = contactDrop.AtMs, ["travelMm"] = travel[dropIndex] },
            ["travelRule"] = "Recomputed from the received RobotStates: sum of |MoveRobotPoseForward(pose,-20 mm) difference| in f32 from the identity pose, frame-matched states only (M4-019 C7). Graded only if the crossing state is the same without the first-state term."
        };

        // HOLD_50
        JsonObject hold;
        if (travelProblem is not null) hold = Row("INCONCLUSIVE", travelProblem);
        else if (contactDrop is null || dropIndex >= cross) hold = Row("INCONCLUSIVE", "The contacts did not drop before 50 mm of travel, so the contacts-off stretch was not covered.");
        else
        {
            var early = sendsAfter.Where(s => s.Position < crossing!.Position).ToList();
            hold = Row(early.Count == 0 ? "MATCH" : "MISMATCH");
            if (early.Count > 0) hold["problems"] = new JsonArray((JsonNode)$"Threshold message(s) [{string.Join(", ", early.Select(s => s.Value))}] went out before Cozmo had travelled more than 50 mm; the engine sends nothing there.");
            hold["earlySends"] = Sends(early);
        }
        foreach (var p in Travelling()) hold[p.Key] = p.Value?.DeepClone();
        results["HOLD_50"] = hold;

        // DISTANCE_400
        JsonObject distance;
        int exits = platformFalse.Count;
        float? exitTravel = exits == 0 ? null : states.LastOrDefault(s => s.AtMs <= platformFalse[0]) is { } atExit ? travel[states.IndexOf(atExit)] : null;
        if (travelProblem is not null) distance = Row("INCONCLUSIVE", travelProblem);
        else if (exits > 0 && (exitTravel is null || exitTravel <= Limit)) distance = Row("INCONCLUSIVE", "The platform cleared before 50 mm of travel; that order is not covered by this check.");
        else
        {
            var late = sendsAfter.Where(s => s.Position > crossing!.Position).ToList();
            var fours = late.Where(s => s.Value == 400).ToList();
            int expected = exits == 0 ? 1 : 2;
            var problems = new JsonArray();
            if (fours.Count != expected) problems.Add($"After the 50 mm state there were {fours.Count} 400 message(s); the engine sends {expected} ({(expected == 2 ? "one for the distance, one for leaving the platform" : "one for the distance; the platform did not clear")}).");
            if (late.Count > 0 && late[0].Value != 400) problems.Add($"The first threshold message after the 50 mm state was {late[0].Value}, not 400.");
            distance = Row(problems.Count == 0 ? "MATCH" : "MISMATCH");
            if (problems.Count > 0) distance["problems"] = problems;
            distance["sendsAfterCrossing"] = Sends(late);
            if (fours.Count > 0) distance["distanceSendLagMs"] = fours[0].AtMs - crossing!.AtMs;
        }
        foreach (var p in Travelling()) distance[p.Key] = p.Value?.DeepClone();
        results["DISTANCE_400"] = distance;

        // PLATFORM_EXIT
        JsonObject exit;
        if (before.Count == 0 || !before[^1].Contact) exit = Row("INCONCLUSIVE", "Cozmo did not start on the charger contacts.");
        else if (exits == 0) exit = Row("INCONCLUSIVE", $"The platform flag did not clear during the drive (travel {(travel.Length == 0 ? 0 : travel[^1]):F1} mm). After a straight drive well past 86 mm this may be a stack fault; review the bundle and DRIVE_OBSERVATIONS.");
        else
        {
            var problems = new JsonArray();
            if (exits > 1) problems.Add($"The platform-cleared report came {exits} times; it can change only once while the contacts stay off.");
            if (contactDrop is null || contactDrop.AtMs > platformFalse[0]) problems.Add("The platform cleared before any robot state showed the contacts off; the engine keeps it while the contacts are on.");
            // The 400 the exit sends (0x00511DA0) is counted by DISTANCE_400.
            exit = Row(problems.Count == 0 ? "MATCH" : "MISMATCH");
            if (problems.Count > 0) exit["problems"] = problems;
        }
        var startState = before.Count > 0 ? before[^1] : null;
        var exitState = exits == 0 ? null : states.LastOrDefault(s => s.AtMs <= platformFalse[0]);
        exit["platformFalseAtMs"] = new JsonArray(platformFalse.Select(t => (JsonNode)t).ToArray());
        exit["exitPosition"] = startState is null || exitState is null ? null : new JsonObject
        {
            ["forwardMm"] = (exitState.X - startState.X) * MathF.Cos(startState.Angle) + (exitState.Y - startState.Y) * MathF.Sin(startState.Angle),
            ["travelMm"] = exitTravel, ["headingChangeRad"] = exitState.Angle - startState.Angle,
            ["expected"] = "about 85.9 mm forward for a straight drive with no heading change (observation, not graded)"
        };
        results["PLATFORM_EXIT"] = exit;
        return results;
    }

    public sealed record BehaviourRun(bool RunnableAtStart, bool Started, bool EndedOnItsOwn, bool TimedOut,
        bool PlatformAtEnd, bool ActionAtEnd, string? DriveResult);
    public static JsonObject GradeBehaviour(BehaviourRun b, bool platformAtStart)
    {
        JsonObject row;
        if (!platformAtStart) row = Row("INCONCLUSIVE", "Cozmo was not on the charger platform, so the behaviour could not run.");
        else if (!b.RunnableAtStart) row = new() { ["verdict"] = "MISMATCH", ["problems"] = new JsonArray((JsonNode)"DriveOffCharger was not runnable although the platform flag was set (IsRunnableInternal returns that flag).") };
        else if (b.TimedOut) row = Row("INCONCLUSIVE", "The behaviour was still running after 30 s and was stopped by the harness (the engine has no timeout of its own; see PLATFORM_EXIT).");
        else if (!b.EndedOnItsOwn) row = Row("INCONCLUSIVE", "The behaviour stopped without reaching its own end (UpdateInternal returning 2).");
        else
        {
            var problems = new JsonArray();
            if (b.PlatformAtEnd) problems.Add("The behaviour ended while the platform flag was still set.");
            if (b.ActionAtEnd) problems.Add("The behaviour ended while its drive action was still running.");
            row = new() { ["verdict"] = problems.Count == 0 ? "MATCH" : "MISMATCH" };
            if (problems.Count > 0) row["problems"] = problems;
        }
        row["run"] = JsonSerializer.SerializeToNode(b);
        return row;
    }
    public const string SevereNeedsMissingText = "BehaviorDriveOffCharger::InitInternal SevereNeeds state writer";
    public static JsonObject GradeDrivingAnimations(IEnumerable<string> stackLogLines, bool started)
    {
        var lines = stackLogLines.Where(l => l.Contains(SevereNeedsMissingText, StringComparison.Ordinal)).ToArray();
        var row = !started ? Row("INCONCLUSIVE", "The behaviour did not start.")
            : lines.Length > 0 ? new JsonObject { ["verdict"] = "KNOWN_GAP" }
            : Row("INCONCLUSIVE", "The expected missing-piece report for the SevereNeeds writer was not logged.");
        row["missingReports"] = new JsonArray(lines.Select(l => (JsonNode)l).ToArray());
        return row;
    }

    public static int Run(string[] args)
    {
        try
        {
            string? root = LinkCheck.FindRepoRoot(); if (root is null) throw new ArgumentException("Run inside the repository.");
            bool validate = false, preview = false, yes = false; string? resources = null, output = null; IPAddress ip = RobotAddress.DefaultFor(false);
            for (int i = 1; i < args.Length; i++) switch (args[i])
                {
                    case "--validate": validate = true; break;
                    case "--preview": preview = true; break;
                    case "--yes": yes = true; break;
                    case "--resources": resources = args[++i]; break;
                    case "--out": output = args[++i]; break;
                    default: if (!IPAddress.TryParse(args[i], out var parsed)) throw new ArgumentException("Unknown argument " + args[i]); ip = parsed; break;
                }
            var warnings = new List<string>(); var records = LoadRecords(Path.Combine(root, "re-analysis", "fidelity_manifest.json"), warnings);
            if (validate) { Console.WriteLine($"{TestId}: {Checks.Length} checks, {records.Count} valid records; no network opened.\n" + string.Join("\n", warnings)); return 0; }
            if (preview) { Preview(); return 0; }
            var options = EngineStartupResources.OptionsForRoot(resources);
            string bundle = Path.Combine(output ?? Path.Combine(root, "re-analysis", "acceptance", "hardware"), DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + TestId + "-" + Guid.NewGuid().ToString("N")[..6]);
            return new M13DriveOffRun(root, bundle, ip, options.ResourcesPath!, records, args, yes).Execute();
        }
        catch (Exception e) { Console.Error.WriteLine("m13-driveoff-hw: " + e.Message + "\nusage: m13-driveoff-hw [robot-ip] --resources <dir> [--yes] [--out <parent>] | --validate | --preview"); return 2; }
    }
}

internal sealed class M13DriveOffRun
{
    readonly string root, bundle, resources; readonly IPAddress ip; readonly List<JsonObject> records; readonly string[] argv; readonly bool yes;
    readonly Stopwatch clock = Stopwatch.StartNew(); readonly ConcurrentQueue<JsonObject> events = new();
    readonly ConcurrentQueue<(double T, bool Out, byte[] Raw, Frame? Frame, string? Error)> frames = new();
    readonly List<JsonObject> results = new(); readonly ConcurrentQueue<string> errors = new(); readonly CancellationTokenSource cancel = new();
    readonly ConcurrentQueue<string> stackLines = new();
    CozmoRobot? robot; RobotConnectionResponse? response; bool connected, disposed;
    HarnessStartup.Gate? startupGate; HarnessStartup.Report? startup;
    Task<string?>? pendingRead;
    double Now => clock.Elapsed.TotalMilliseconds;
    public M13DriveOffRun(string root, string bundle, IPAddress ip, string resources, List<JsonObject> records, string[] argv, bool yes)
    { this.root = root; this.bundle = bundle; this.ip = ip; this.resources = resources; this.records = records; this.argv = argv; this.yes = yes; }
    void Event(string kind, JsonObject data) { data["atMs"] = Now; data["kind"] = kind; events.Enqueue(data); }
    void Wait(int ms) { if (cancel.Token.WaitHandle.WaitOne(ms)) cancel.Token.ThrowIfCancellationRequested(); }
    bool Poll(Func<bool> predicate, int ms) { double end = Now + ms; while (Now < end) { if (predicate()) return true; Wait(10); } return predicate(); }
    JsonObject[] EngineLogs() => events.Where(e => e["kind"]?.GetValue<string>() == "engine-log").ToArray();
    string? ReadLineBounded()
    {
        if (pendingRead is { IsCompleted: true }) pendingRead = null;
        var read = pendingRead ??= Task.Run(Console.ReadLine);
        while (!read.IsCompleted) { if (cancel.IsCancellationRequested) return null; Thread.Sleep(50); }
        pendingRead = null; return read.GetAwaiter().GetResult();
    }
    List<M5Hw.WireMessage> Wire() => M5HwAnalysis.OrderedUnique(M5HwAnalysis.ReadWire(frames.ToArray().Select((f, index) =>
        new M5HwAnalysis.CapturedFrame(index + 1, f.T, f.Out, f.Frame)))).ToList();
    JsonObject Missing(string note) => new() { ["verdict"] = "INCONCLUSIVE", ["missing"] = note };

    public int Execute()
    {
        Directory.CreateDirectory(bundle);
        Console.WriteLine("DRIVE-OFF-CHARGER TEST\n\n"
            + "What this is for: Cozmo starts on his charger and drives off it. The program records every message sent\n"
            + "to him and checks them against the original Cozmo engine; that checking is automatic. In one part you\n"
            + "are asked what you saw; those answers are saved as notes and never decide pass or fail.\n\n"
            + M13DriveOffHw.SetupText + "\n");
        M13DriveOffHw.Overview(Console.Out);
        Console.WriteLine("\nCtrl+C stops the run at any time; everything recorded so far is still saved.\nResults folder: " + bundle);
        ConsoleCancelEventHandler interrupt = (_, e) => { e.Cancel = true; cancel.Cancel(); }; Console.CancelKeyPress += interrupt;
        using var timer = new Timer(_ => cancel.Cancel(), null, TimeSpan.FromMinutes(5), Timeout.InfiniteTimeSpan);
        var platformFalse = new ConcurrentQueue<double>();
        bool platformAtDriveStart = false; double driveStart = double.MaxValue, driveEnd = double.MaxValue; M13DriveOffHw.BehaviourRun? run = null;
        JsonObject? observation = null; var pathLines = new JsonArray();
        try
        {
            if (!yes) { Console.WriteLine("\nWhen Cozmo is on the charger, press Enter to connect. Connecting takes up to a minute; you don't need to watch yet."); ReadLineBounded(); cancel.Token.ThrowIfCancellationRequested(); }
            using var resolution = new HighResolutionTimer();
            robot = CozmoRobot.Create(engineOptions: new CozmoEngineOptions { ResourcesPath = resources, BeforeStart = e =>
            {
                EngineEvidence.Subscribe(e, line => { }, Event);
                e.StackLogLine += stackLines.Enqueue;
                e.ActionCompleted += a => Event("action-completed", JsonSerializer.SerializeToNode(a)!.AsObject());
            } });
            robot.Transport.FrameTrace += f => frames.Enqueue((Now, f.Outbound, (byte[])f.Raw.Clone(), f.Frame, f.Error));
            robot.Engine.ConnectionResponse += r => { response = r; Event("connection-response", new() { ["result"] = r.Result.ToString(), ["firmware"] = r.FwVersion }); };
            robot.HandlerFaulted += e => Event("handler-fault", new() { ["exception"] = e.ToString() });
            robot.Sensors.OnChargerPlatformChanged += on => { if (!on) platformFalse.Enqueue(Now); Event("platform-changed", new() { ["onPlatform"] = on }); };
            robot.Sensors.ChargerEvent += on => Event("charger-contacts", new() { ["onContacts"] = on });
            robot.Sensors.CliffDetected += c => Event("cliff-detected", JsonSerializer.SerializeToNode(c)!.AsObject());
            robot.Sensors.RobotStopped += () => Event("robot-stopped", new());
            robot.Sensors.OffTreadsStateChanged += (from, to) => Event("off-treads", new() { ["from"] = from.ToString(), ["to"] = to.ToString() });
            // fidelity: M2-021 — ConnectToRobot only after delivered readiness, as CozmoRobot.ConnectAsync does.
            startupGate = HarnessStartup.ConnectAfterReadiness(robot, () => robot.ConnectToRobot(ip), () => cancel.IsCancellationRequested);
            Event("startup-gate", startupGate.ToJson()); Console.WriteLine("Startup: " + startupGate.Describe());
            if (!startupGate.Ready) errors.Enqueue("startup: " + startupGate.Describe());
            connected = startupGate.Ready && Poll(() => response?.Result == RobotConnectionResult.Success && robot.Engine.Robot is { TimeSynced: true, FirstFullStateHandled: true }, 15000);
            if (connected) { bool assets = Poll(() => robot.Animations.ClipNames.Count > 0, 15000); bool calibrated = Poll(() => robot.State.CalibrationComplete, 20000); Event("check-readiness", new() { ["assets"] = assets, ["calibrated"] = calibrated }); connected = assets && calibrated; }
            startup = HarnessStartup.Analyse(EngineLogs(), startupGate, connected ? HarnessStartup.HasCachedNeutralFace(robot) : null,
                connected ? null : "connection or calibration did not complete");
            Event("startup-report", startup.ToJson()); Console.WriteLine("Startup: " + startup.Describe());
            if (!connected) throw new InvalidOperationException("Not connected; no part ran.");

            var r = robot;
            Console.WriteLine(M13DriveOffHw.PartHeader(1, "ON_CHARGER"));
            bool onContacts = Poll(() => r.State.Latest?.Has(RobotStatusFlag.IsOnCharger) == true && r.Sensors.OnChargerPlatform, 3000);
            Wait(500);
            platformAtDriveStart = r.Sensors.OnChargerPlatform;
            Event("on-charger-check", new() { ["contacts"] = r.State.Latest?.Has(RobotStatusFlag.IsOnCharger), ["platform"] = platformAtDriveStart });
            if (!onContacts)
            {
                bool contacts = r.State.Latest?.Has(RobotStatusFlag.IsOnCharger) == true;
                Console.WriteLine(contacts
                    ? "Cozmo is on the contacts but the stack did not set its charger-platform flag. Nothing will drive. This is reported as a PROBLEM; send the folder for review."
                    : "Cozmo is not on the charger contacts. Nothing will drive. Put him on the charger and run the test again.");
                driveStart = driveEnd = Now;
            }
            else
            {
                Console.WriteLine("Cozmo is on the charger.");
                Console.WriteLine(M13DriveOffHw.PartHeader(2, "DRIVE_OFF"));
                Console.WriteLine(M5Hw.DescribeWindow(M13DriveOffHw.DrivePlan));
                if (!yes) { Console.WriteLine("\nGet a clear view of Cozmo, then press Enter. He starts moving right away."); ReadLineBounded(); cancel.Token.ThrowIfCancellationRequested(); }
                var window = new JsonObject { ["check"] = "DRIVE_OFF", ["phase"] = "DRIVE", ["watch"] = M13DriveOffHw.DrivePlan.Watch, ["fromMs"] = Now };
                Console.WriteLine($">>> WATCH NOW: {M13DriveOffHw.DrivePlan.Watch} ({M13DriveOffHw.DrivePlan.HowLong}) <<<");
                Event("observation-window-start", (JsonObject)window.DeepClone());
                run = DriveOff(r, out driveStart); driveEnd = Now;
                window["toMs"] = Now; Console.WriteLine(">>> STOP WATCHING. Answer about what you just saw. <<<");
                Event("observation-window-end", (JsonObject)window.DeepClone());
                observation = window;
                foreach (var q in M13DriveOffHw.DrivePlan.Questions)
                {
                    Console.WriteLine("\n" + M5Hw.DescribeQuestion(q));
                    double asked = Now; string? raw = yes ? null : ReadLineBounded();
                    var answer = M5HwAnalysis.ObservationAnswer(window, q.Text, raw, q.Choices ?? Array.Empty<string>(), asked, Now);
                    window[q.Field] = answer; Event("operator-observation", (JsonObject)answer.DeepClone());
                }
                Console.WriteLine("\nAnything odd? Type it in your own words, or just press Enter to go on.");
                window["freeText"] = yes ? null : ReadLineBounded();
            }
        }
        catch (Exception e) { errors.Enqueue(e.ToString()); }
        finally
        {
            if (robot is not null && !disposed)
            {
                disposed = true;
                try { if (connected) robot.Motion.StopAllMotors(); } catch (Exception e) { errors.Enqueue("StopAllMotors: " + e.Message); }
                var d = Task.Run(robot.Dispose); if (!d.Wait(5000)) errors.Enqueue("Dispose did not finish in 5 seconds");
            }
            // Ctrl+C or an error inside DriveOff skips the assignment after it; the window still ends here, before disposal.
            if (driveEnd == double.MaxValue && driveStart != double.MaxValue) driveEnd = Now;
            Console.CancelKeyPress -= interrupt;
        }

        // Grade.
        var wire = Wire();
        bool restore = EngineLogs().Any(e => e["name"]?.GetValue<string>() == "CliffSensorComponent.ClearCliffRunningStats.RestoringCliffDetectThreshold"
            && e["atMs"]!.GetValue<double>() <= driveEnd);
        var graded = connected
            ? M13DriveOffHw.GradeThresholds(new(wire, driveStart, platformAtDriveStart, platformFalse.ToArray(), restore, driveEnd))
            : new Dictionary<string, JsonObject>();
        foreach (var def in M13DriveOffHw.Checks)
        {
            JsonObject result = def.Id switch
            {
                _ when !connected => Missing("connection/readiness unavailable: " + (startup?.Describe() ?? "startup not completed")),
                "BEHAVIOUR_END" => run is null ? Missing("The drive part did not run (Cozmo was not on the charger, or the run stopped).") : M13DriveOffHw.GradeBehaviour(run, platformAtDriveStart),
                "DRIVING_ANIMATIONS" => M13DriveOffHw.GradeDrivingAnimations(stackLines, run?.Started == true),
                "DRIVE_OBSERVATIONS" => Observations(wire, driveStart, observation),
                _ => graded.TryGetValue(def.Id, out var g) ? g : Missing("not graded")
            };
            result["id"] = def.Id; result["records"] = JsonSerializer.SerializeToNode(def.Records);
            result["sourceComparisons"] = JsonSerializer.SerializeToNode(M13DriveOffHw.SourceRules.Where(x => x.Check == def.Id));
            result["uncertainty"] = new JsonArray(records.Where(x => def.Records.Contains(x["id"]!.GetValue<string>()) && x["status"]!.GetValue<string>() is "HARDWARE_ONLY" or "BLOCKED_EXTERNAL" or "IMPLEMENTATION_GAP" or "RECOVERABLE_GAP").Select(x => (JsonNode)x["id"]!.GetValue<string>()).ToArray());
            result["resultKind"] = M13DriveOffHw.ResultKind(result); result["summary"] = M13DriveOffHw.DescribeResult(result);
            results.Add(result);
            Console.WriteLine("RESULT " + result["summary"]!.GetValue<string>());
        }
        WriteBundle(wire);
        Console.WriteLine("\n=== TEST FINISHED ===\n" + M13DriveOffHw.OverallLine(results, !errors.IsEmpty) + "\nThe same summary is in SUMMARY.txt in: " + bundle);
        return errors.IsEmpty && results.All(x => M13DriveOffHw.ResultKind(x) is "MATCH" or "OBSERVATION" or "KNOWN_GAP") ? 0 : 1;
    }

    /// <summary>The shipped DriveOffCharger (extra 60 mm), started and ticked by the harness every 33 ms, bounded to 30 s.</summary>
    M13DriveOffHw.BehaviourRun DriveOff(CozmoRobot r, out double driveStart)
    {
        using var m = new ManipulationSystem(r, r.Vision);
        var b = ShippedBehaviors.Navigation(m).OfType<DriveOffChargerBehavior>().Single(x => x.Id == "DriveOffCharger");
        var ctx = new BehaviorContext { Robot = r, Triggers = new AnimationTriggerMap() };
        b.Step += line => Event("behaviour-step", new() { ["line"] = line });
        driveStart = Now;
        bool runnable = b.IsRunnable(ctx); Event("behaviour-runnable", new() { ["runnable"] = runnable });
        if (!runnable) return new(false, false, false, false, r.Sensors.OnChargerPlatform, false, null);
        b.StartAsync(ctx, new BehaviorScope(), cancel.Token).GetAwaiter().GetResult();
        Event("behaviour-started", new());
        double t = 0, limit = Now + 30000; bool timedOut = false;
        while (b.Update(ctx, t))
        {
            if (Now > limit || cancel.IsCancellationRequested) { timedOut = true; break; }
            Thread.Sleep(33); t += 33;
        }
        bool platform = r.Sensors.OnChargerPlatform, action = b.HasCurrentAction;
        bool ended = !timedOut && b.DroveOffAtSec is not null;
        Event("behaviour-ended", new() { ["endedOnItsOwn"] = ended, ["timedOut"] = timedOut, ["platform"] = platform, ["action"] = action, ["driveResult"] = b.DriveResult?.ToString() });
        b.Stop(timedOut ? BehaviorStopReason.Cancelled : BehaviorStopReason.Completed);
        if (timedOut) r.Motion.StopAllMotors();
        Wait(1500); // trailing threshold sends and reports
        return new(true, true, ended, timedOut, platform, action, b.DriveResult?.ToString());
    }

    JsonObject Observations(List<M5Hw.WireMessage> wire, double driveStart, JsonObject? window)
    {
        var after = wire.Where(m => m.AtMs >= driveStart).ToList();
        var lines = new JsonArray();
        foreach (var m in after.Where(m => m.Outbound && m.Payload.Length > 0 && m.Payload[0] == (byte)RobotMessageId.AppendPathSegLine))
        {
            var l = AppendPathSegmentLine.Read(new CladReader(m.Payload.AsMemory(1)));
            lines.Add(new JsonObject { ["atMs"] = m.AtMs, ["lengthMm"] = MathF.Sqrt((l.XEndMm - l.XStartMm) * (l.XEndMm - l.XStartMm) + (l.YEndMm - l.YStartMm) * (l.YEndMm - l.YStartMm)), ["payloadHex"] = Convert.ToHexString(m.Payload) });
        }
        int Count(RobotMessageId id) => after.Count(m => !m.Outbound && m.Payload.Length > 0 && m.Payload[0] == (byte)id);
        return new JsonObject
        {
            ["verdict"] = "OBSERVATION",
            ["pathLines"] = lines, ["expectedLineMm"] = "156 (96.0 + extraDistanceToDrive_mm 60.0); one line per drive, re-driven while the platform stays set",
            ["robotCliffEvents"] = Count(RobotMessageId.CliffEvent), ["robotPotentialCliffs"] = Count(RobotMessageId.PotentialCliff), ["robotStopped"] = Count(RobotMessageId.RobotStopped),
            ["thresholdSendsAfterDrive"] = new JsonArray(M13DriveOffHw.Thresholds(after).Select(s => (JsonNode)new JsonObject { ["atMs"] = s.AtMs, ["value"] = s.Value }).ToArray()),
            ["apiEvents"] = new JsonArray(events.Where(e => e["atMs"]!.GetValue<double>() >= driveStart && e["kind"]?.GetValue<string>() is "platform-changed" or "charger-contacts" or "cliff-detected" or "robot-stopped" or "off-treads" or "action-completed" or "behaviour-ended").Select(e => e.DeepClone()).ToArray()),
            ["operator"] = window?.DeepClone()
        };
    }

    void WriteBundle(List<M5Hw.WireMessage> wire)
    {
        try
        {
            File.WriteAllText(Path.Combine(bundle, "SUMMARY.txt"), "Drive-off-charger test\n" + M13DriveOffHw.OverallLine(results, !errors.IsEmpty) + "\n\n"
                + "OK = the stack did what the original engine does. PROBLEM = it did something the engine would not.\n"
                + "COULD NOT CHECK = setup or evidence missing. KNOWN MISSING PIECE and SAVED FOR REVIEW never pass or fail.\n"
                + "Your answers are saved as notes; they never decide a result.\n\n"
                + string.Join("\n\n", results.Select(M13DriveOffHw.DescribeResult))
                + "\n\nStartup: " + (startup?.Describe() ?? "not completed")
                + "\n\nRun errors:\n" + (errors.IsEmpty ? "None." : string.Join("\n", errors)));
            File.WriteAllLines(Path.Combine(bundle, "frames.jsonl"), frames.Select(f => new JsonObject { ["atMs"] = f.T, ["outbound"] = f.Out, ["rawBase64"] = Convert.ToBase64String(f.Raw), ["error"] = f.Error }.ToJsonString()));
            File.WriteAllLines(Path.Combine(bundle, "messages.jsonl"), wire.Select(m => new JsonObject { ["atMs"] = m.AtMs, ["outbound"] = m.Outbound, ["tag"] = $"0x{m.Payload[0]:X2}", ["payloadHex"] = Convert.ToHexString(m.Payload), ["frameLine"] = m.FrameLine, ["submessageOrdinal"] = m.SubmessageOrdinal, ["reliableSequence"] = m.ReliableSequence, ["connectionEpoch"] = m.ConnectionEpoch }.ToJsonString()));
            File.WriteAllLines(Path.Combine(bundle, "events.jsonl"), events.Select(e => e.ToJsonString()));
            var files = new JsonArray();
            foreach (var relative in new[] { "cozmo-stack/src/Cozmo.Conformance/M13DriveOffHw.cs", "cozmo-stack/src/Cozmo.Conformance/HarnessStartup.cs", "re-analysis/acceptance/hardware/scripts/m13-driveoff-hw-1.md", "re-analysis/fidelity_manifest.json" })
            {
                string source = Path.Combine(root, relative); if (!File.Exists(source)) { errors.Enqueue("Missing provenance file " + relative); continue; }
                string dest = Path.Combine(bundle, "provenance", relative); Directory.CreateDirectory(Path.GetDirectoryName(dest)!); File.Copy(source, dest, true);
                files.Add(new JsonObject { ["path"] = relative, ["sha256"] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source))).ToLowerInvariant() });
            }
            string? commit = null; try { var p = Process.Start(new ProcessStartInfo("git", "rev-parse HEAD") { WorkingDirectory = root, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true }); commit = p!.StandardOutput.ReadToEnd().Trim(); p.WaitForExit(); } catch (Exception e) { errors.Enqueue("git: " + e.Message); }
            var report = new JsonObject { ["testId"] = M13DriveOffHw.TestId, ["createdUtc"] = DateTime.UtcNow.ToString("O"), ["commit"] = commit, ["argv"] = JsonSerializer.SerializeToNode(argv),
                ["resources"] = resources, ["connected"] = connected, ["startup"] = startup?.ToJson(), ["checks"] = JsonSerializer.SerializeToNode(results),
                ["records"] = JsonSerializer.SerializeToNode(records), ["provenance"] = files, ["errors"] = JsonSerializer.SerializeToNode(errors),
                ["acceptance"] = "NOT_JUDGED: manager review required; hardware success does not raise provenance" };
            File.WriteAllText(Path.Combine(bundle, "result.json"), report.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            File.WriteAllText(Path.Combine(bundle, "README.txt"), "M13-DRIVEOFF-HW evidence bundle. result.json holds every check, its engine citations and the source records; messages.jsonl deduplicates reliable resends; frames.jsonl keeps every datagram; events.jsonl keeps engine logs, API events and behaviour steps. Times are host capture times. No acceptance or manifest change was made.");
        }
        catch (Exception e) { Console.Error.WriteLine("Could not write the results folder: " + e.Message); }
    }
}
