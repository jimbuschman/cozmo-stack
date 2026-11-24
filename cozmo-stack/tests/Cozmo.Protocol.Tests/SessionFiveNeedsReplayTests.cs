using System.Text.Json;
using Cozmo.Bridge;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Xunit;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class SessionFiveNeedsReplayTests
{
    private const string CaptureVariable = "COZMO_SESSION5_NEEDS_BUNDLE";
    private const double RecoveryStart = 730144.65, AfterLastDecay = 1153479;
    private sealed record Report(double At, NeedsActionId Action, float[] Levels, NeedBracketId[] Brackets, bool[] Parts);
    private sealed record Request(double At, string Name, JsonElement Arguments);

    [Fact]
    public async Task SessionFiveNeedsReplayKeepsTheRunningActivityUntilTheChildCanBeReplaced()
    {
        string? bundle = Environment.GetEnvironmentVariable(CaptureVariable);
        if (string.IsNullOrEmpty(bundle)) return;
        var requests = ReadRequests(bundle).ToArray();
        Assert.Equal(6, requests.Length);
        Assert.Equal("set_needs_mode", requests[0].Name);
        var reports = ReadReports(bundle, 640000, RecoveryStart + 1).Where(r => r.Action == NeedsActionId.NoAction).ToArray();
        Assert.Equal(5, reports.Length);
        string directory = Directory.CreateTempSubdirectory("cozmo-session-five-needs-").FullName;
        try
        {
            var options = NativeRewardCanonicalTests.SourceOptions(directory, () => NativeRewardCanonicalTests.FixedClockMicros + 20_000_000, 1f, 0);
            var stored = NativeRewardCanonicalTests.StoredBlob(1, 1, 1, 2, 1, NativeRewardCanonicalTests.InitialSeconds());
            using var f = new Fixture(preloadAnimations: true, pumpAnimations: true, engineOptions: options,
                sourceNvEmpty: true, keepNeedsFull: true, sourceNvStored: tag => tag == NeedsManager.NeedsNvKey ? stored : null);
            using var bridge = new CozmoBridge(f.Rig.Robot);
            var owner = f.Runtime.FreeplayObserver;
            var manager = f.Robot.BehaviorManagerOwner!;
            var needs = f.Rig.Engine.Needs;

            async Task Apply(int index)
            {
                var req = requests[index];
                var result = await BridgeCommandTests.Invoke(bridge, req.Name, req.Arguments.GetRawText());
                Assert.Equal("confirmed", result.GetProperty("status").GetString());
                Assert.True(result.GetProperty("report").GetProperty("applied").GetBoolean());
                if (index == 0) return;
                var report = reports[index - 1];
                string[] keys = ["repair", "energy", "play"];
                for (int n = 0; n < 3; n++)
                {
                    Assert.Equal(report.Levels[n], result.GetProperty("report").GetProperty(keys[n]).GetSingle());
                    Assert.Equal(report.Brackets[n], needs.State.GetNeedBracket((NeedId)n));
                }
            }

            void CheckActivity(string expectedActivity, string? expectedBehavior, string? expression)
            {
                var observation = bridge.GetState();
                Assert.Equal(expectedActivity, observation.GetProperty("activity").GetString());
                Assert.Equal(expectedBehavior, observation.GetProperty("behavior").GetString());
                var status = observation.GetProperty("needs");
                Assert.True(status.GetProperty("applied").GetBoolean());
                Assert.Equal(expression, status.GetProperty("expressedSevereNeed").GetString());
                Assert.Equal(expectedActivity.Contains("Energy", StringComparison.Ordinal) ? "Energy" :
                    expectedActivity.Contains("Repair", StringComparison.Ordinal) ? "Repair" : "Play",
                    status.GetProperty("selectedSevereNeed").GetString());
            }

            await Apply(0);
            await Apply(1);
            f.Rig.Robot.SetEnableFreeplayActivity(true);
            f.Rig.Tick();
            CheckActivity("NeedsSevereLowEnergy", "Needs_SevereLowEnergyGetIn", null);

            // The 15:23:39 get-in completion is an input boundary here. OnStop is
            // still the native-configured writer of the selected expression.
            manager.Stop(BehaviorStopReason.Completed, f.Rig.Engine.Timer.Seconds);
            f.Rig.Tick();
            CheckActivity("NeedsSevereLowEnergy", "Needs_SevereLowEnergyState", "Energy");
            var energy = manager.Current;

            await Apply(2); // 15:23:45.458: both Critical.
            Assert.Equal("NeedsSevereLowRepair", owner.PickNewActivity(f.Rig.Engine.Timer.Seconds, out _)?.Id);
            // 0x005AE68E..0x005AE6AC only consults WantsToEnd for a non-running
            // choice. The shipped Repair priority applies at the next activity pick.
            Assert.Same(energy, ((IManagedActivity)owner).GetDesiredActiveBehavior(energy, f.Rig.Engine.Timer.Seconds));
            CheckActivity("NeedsSevereLowEnergy", "Needs_SevereLowEnergyState", "Energy");

            await Apply(3); // 15:23:52.782: energy restored while the State is still running.
            f.Runtime.AI.SevereNeeds.Update();
            Assert.Same(energy, ((IManagedActivity)owner).GetDesiredActiveBehavior(energy, f.Rig.Engine.Timer.Seconds));
            CheckActivity("NeedsSevereLowEnergy", "Needs_SevereLowEnergyState", null);

            manager.Stop(BehaviorStopReason.Completed, f.Rig.Engine.Timer.Seconds);
            f.Rig.Tick();
            CheckActivity("NeedsSevereLowRepair", "Needs_SevereLowRepairGetIn", null);
            manager.Stop(BehaviorStopReason.Completed, f.Rig.Engine.Timer.Seconds);
            f.Rig.Tick();
            var repair = manager.Current;
            CheckActivity("NeedsSevereLowRepair", "Needs_SevereLowRepairState", "Repair");

            await Apply(4); // 15:24:21.157: repair restored, play becomes Critical.
            f.Runtime.AI.SevereNeeds.Update();
            Assert.Same(repair, ((IManagedActivity)owner).GetDesiredActiveBehavior(repair, f.Rig.Engine.Timer.Seconds));
            CheckActivity("NeedsSevereLowRepair", "Needs_SevereLowRepairState", null);
            manager.Stop(BehaviorStopReason.Completed, f.Rig.Engine.Timer.Seconds);
            f.Rig.Tick();
            CheckActivity("NeedsSevereLowPlayGetIn", "Needs_SevereLowPlayGetIn", null);

            await Apply(5);
            manager.Stop(BehaviorStopReason.Completed, f.Rig.Engine.Timer.Seconds);
            f.Rig.Tick();
            Assert.Null(f.Runtime.AI.SevereNeeds.SelectedNeed);
            Assert.NotEqual("NeedsSevereLowPlayGetIn", owner.Current?.Id);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void SingingChooserStartsTheUnlockedForcedSongWithItsNativeCompound()
    {
        string directory = Directory.CreateTempSubdirectory("cozmo-singing-").FullName;
        try
        {
            var options = NativeRewardCanonicalTests.SourceOptions(directory, () => NativeRewardCanonicalTests.FixedClockMicros, 1, 0);
            using var f = new Fixture(preloadAnimations: true, engineOptions: options, sourceNvEmpty: true);
            var singing = f.Runtime.FreeplayObserver.Freeplay.SubActivities.Single(a => a.Id == "Singing");
            var chooser = Assert.IsType<ScoringChooser>(singing.Chooser);
            var song = Assert.IsType<NativeSingingBehavior>(f.Runtime.Bound["Singing_Beethovens5th"]);
            var other = Assert.IsType<NativeSingingBehavior>(f.Runtime.Bound["Singing_AbaDaba"]);
            uint unlock = NativeUnlockNames.FromBytes("Singing_Beethovens5th"u8.ToArray());
            f.Robot.Progression!.SetUnlock(unlock, true);
            f.Robot.Progression.SetUnlock(NativeUnlockNames.FromBytes("Singing_AbaDaba"u8.ToArray()), true);
            f.Rig.Engine.Needs.State.ForceNextSong = (int)unlock;
            Assert.Equal(unlock, song.NativeBaseInputs!.RequiredUnlock);
            Assert.True(f.Robot.Progression.TryIsUnlocked(unlock, out bool unlocked) && unlocked);
            Assert.False(f.Rig.Robot.Sensors.OnChargerPlatform);
            Assert.False(song.NativeBaseInputs.IsCarrying);
            Assert.True(song.IsRunnable(f.Runtime.Context));
            Assert.False(other.IsRunnable(f.Runtime.Context));
            chooser.RandomDraw = () => .5;
            var choice = chooser.GetDesiredActiveBehavior(null, 0, f.Runtime.Context, f.Rig.Engine.Timer.Seconds);
            Assert.True(ReferenceEquals(song, choice.Behavior),
                $"{choice.Reason}: {string.Join(", ", choice.Scores)}");
            Assert.False(choice.InputUnavailable);

            var audio = new List<BehaviorAudioRequest>();
            f.Robot.BehaviorAudioRecipient = audio.Add;
            Assert.True(f.Robot.BehaviorManagerOwner!.StartAsync(song.Id, f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
            var compound = Assert.IsType<Cozmo.Robot.CompoundActionSequential>(song.OwnedRunner);
            Assert.Equal(new[] { AnimationTrigger.Singing_GetIn, AnimationTrigger.Singing_100bpm, AnimationTrigger.Singing_GetOut },
                compound.Children.Select(action => Assert.IsType<TriggerAnimationAction>(action).Request.Trigger));
            Assert.Contains(new BehaviorAudioRequest(song.Id, 0x14, 0xE017E775, 0x0DF58DEC, 1), audio);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void SessionFiveNeedsReplayCapsRepairOnFistBumpAndKeepsTheFullnessCooldown()
    {
        string? bundle = Environment.GetEnvironmentVariable(CaptureVariable);
        if (string.IsNullOrEmpty(bundle)) return;
        var reports = ReadReports(bundle, RecoveryStart, AfterLastDecay).ToArray();
        var refill = reports[0];
        Assert.Equal(NeedsActionId.NoAction, refill.Action);
        Assert.Equal(new[] { 1f, 1f, 1f }, refill.Levels);
        Assert.All(refill.Parts, Assert.True);

        var rng = new EngineRandom();
        rng.SetSeed(125, () => 125); // Repair and Energy ranges are zero for every replayed action.
        using var f = new Fixture(sourceNvEmpty: true);
        var needs = NeedsManager.FromObb(f.Rig.Engine.Options.ResourcesPath, () => 0, contextRandom: rng);
        needs.Connected = true;
        needs.State.ContextRandom = rng;
        for (int i = 0; i < refill.Parts.Length; i++) needs.State.SetPartDamageForTest(i, refill.Parts[i] ? (byte)1 : (byte)0);
        needs.ForceSetNeedsLevels(refill.Levels);
        Assert.Equal(3, needs.State.NumDamagedParts());
        int minute = 0;
        const double priorDecay = 693209.425;
        var delivered = new List<NeedsActionId>();
        needs.NeedsStateSent += delivered.Add;

        foreach (var report in reports.Skip(1))
        {
            delivered.Clear();
            if (report.Action == NeedsActionId.Decay)
            {
                // The capture timestamps are host receipt times. Replay the
                // shipped 60 s decay cadence with the captured ordering.
                float now = ++minute * (float)needs.Config.DecayPeriodSeconds;
                Assert.InRange((report.At - priorDecay) / 1000, now - .15, now + .15);
                needs.Update(now);
            }
            else
            {
                needs.Update((float)((report.At - priorDecay) / 1000));
                Assert.True(needs.RegisterNeedsActionCompleted(report.Action.ToString()));
            }
            Assert.Equal(report.Action, Assert.Single(delivered));
            Assert.Equal(report.Brackets[(int)NeedId.Repair], needs.State.GetNeedBracket(NeedId.Repair));
            Assert.InRange(needs.State.GetNeedLevel(NeedId.Repair), report.Levels[0] - 0.000002, report.Levels[0] + 0.000002);
            Assert.Equal(3, needs.State.NumDamagedParts());
        }
        var fistBump = Assert.Single(reports, r => r.Action == NeedsActionId.FistBump);
        // 0x0069C9CE..0x0069CA02 caps even a positive repair delta when all
        // parts are damaged. The pre-cap Full result starts the 1200 s window
        // at 0x006970AC..0x00697100; decay skips it at 0x00695D4C..0x00695D58.
        Assert.Equal(0x3E99984Au, BitConverter.SingleToUInt32Bits(fistBump.Levels[0]));
        Assert.All(reports.Where(r => r.At > fistBump.At), r => Assert.Equal(fistBump.Levels[0], r.Levels[0]));
        Assert.Equal(fistBump.Levels[0], (float)needs.State.GetNeedLevel(NeedId.Repair));
    }

    [Fact]
    public void SessionFiveNeedsReplaySeparatesTheLayerStartsFromThePlayGetInClip()
    {
        string? bundle = Environment.GetEnvironmentVariable(CaptureVariable);
        if (string.IsNullOrEmpty(bundle)) return;
        var outbound = RecordedRobotInput.ReadOutbound(bundle, at => at >= 722728.1921, 725724,
            [0x8E, 0x8F, 0x97, 0x98, 0x99, 0x9B]).ToArray();
        var starts = outbound.Where(row => row.Message is StartOfAnimation)
            .Select(row => (row.At, Tag: ((StartOfAnimation)row.Message).AnimId)).ToArray();
        Assert.Equal(new byte[] { 84, 85, 86, 87, 88 }, starts.Select(s => s.Tag));
        foreach (var tag in new byte[] { 84, 86 })
        {
            var start = starts.Single(s => s.Tag == tag);
            var next = starts.Single(s => s.Tag == tag + 1);
            // 0x0057C5C0..0x0057C624 applies layers with a null Animation,
            // then sends silence/face and assigns a normal non-idle tag.
            var frames = outbound.Where(r => r.At >= start.At && r.At < next.At).Select(r => r.Message).ToArray();
            Assert.Contains(frames, m => (byte)m.Id == 0x8F);
            Assert.Contains(frames, m => (byte)m.Id == 0x97);
            Assert.DoesNotContain(frames, m => (byte)m.Id is 0x8E or 0x98 or 0x99);
        }
        var named = ReadRows(bundle, 722700, 725725).Where(row =>
            row.GetProperty("kind").GetString() == "live-narration" &&
            row.TryGetProperty("event", out var ev) && ev.GetString() == "clip-initialized").Select(row => row.Clone()).ToArray();
        Assert.Contains(named, row => row.GetProperty("name").GetString() == "anim_play_getin_01" && row.GetProperty("tag").GetInt32() == 88);
        Assert.DoesNotContain(named, row => row.GetProperty("tag").GetInt32() is 84 or 86);
    }

    private static IEnumerable<Request> ReadRequests(string bundle)
    {
        foreach (var row in ReadRows(bundle, 638000, RecoveryStart))
        {
            if (row.GetProperty("kind").GetString() != "mcp-input" || !row.TryGetProperty("params", out var args)) continue;
            if (!args.TryGetProperty("name", out var name) || name.GetString() is not ("set_needs" or "set_needs_mode") ||
                !args.TryGetProperty("arguments", out var body)) continue;
            yield return new(row.GetProperty("atMs").GetDouble(), name.GetString()!, body.Clone());
        }
    }

    private static IEnumerable<Report> ReadReports(string bundle, double start, double end)
    {
        foreach (var row in ReadRows(bundle, start, end))
        {
            if (row.GetProperty("kind").GetString() != "engine-to-game" || row.GetProperty("tag").GetString() != "0x62") continue;
            var reader = new CladReader(Convert.FromHexString(row.GetProperty("bodyHex").GetString()!));
            var levels = reader.Array<float>(reader.U8(), reader.F32);
            var brackets = reader.Array<NeedBracketId>(reader.U8(), () => (NeedBracketId)reader.I32());
            var parts = reader.Array<bool>(reader.U8(), () => reader.U8() != 0);
            for (int i = 0; i < 3; i++) reader.I32(); // unlock level, stars and next stars
            yield return new(row.GetProperty("atMs").GetDouble(), (NeedsActionId)reader.I32(), levels, brackets, parts);
        }
    }

    private static IEnumerable<JsonElement> ReadRows(string bundle, double start, double end)
    {
        foreach (string line in File.ReadLines(Path.Combine(bundle, "events.jsonl")))
        {
            using var json = JsonDocument.Parse(line);
            var row = json.RootElement;
            double at = row.GetProperty("atMs").GetDouble();
            if (at > end) yield break;
            if (at >= start) yield return row;
        }
    }
}
