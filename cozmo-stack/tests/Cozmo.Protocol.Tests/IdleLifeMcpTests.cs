using System.Text.Json;
using Cozmo.Bridge;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
using Xunit;
using Xunit.Abstractions;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class IdleLifeMcpTests(ITestOutputHelper output)
{
    private static string? Bundle => Environment.GetEnvironmentVariable("COZMO_IDLE_LIFE_BUNDLE");

    [Fact]
    public async Task RecordedHeadRequestParksFreeplayAndReturnsToHiking()
    {
        var head = JsonSerializer.SerializeToElement(new { radians = .5f });
        if (Bundle is { } bundle)
        {
            head = ReadHeadRequest(bundle);
            var observed = File.ReadLines(Path.Combine(bundle, "events.jsonl"))
                .Select(ParseTrace).Where(e => e.At is >= 271747 and <= 273179 && e.Event == "robot.behavior_transition")
                .Select(e => e.Text).ToArray();
            Assert.Equal(new[] { "Hiking_FirstLookIntro", "Wait", "Hiking_ThinkAboutBeacons" }, observed);
        }

        using var f = new Fixture(preloadAnimations: true, pumpAnimations: true, sourceNvEmpty: true, keepNeedsFull: true);
        using var bridge = new CozmoBridge(f.Rig.Robot);
        await using var client = new McpServerTests.Client(bridge);
        await client.Initialize();
        var manager = f.Robot.BehaviorManagerOwner!;
        var pump = new DeviceReplies(f);
        // Supply the native debug activity selector so the empty fixture enters the recorded
        // Hiking chooser. The chooser still decides which behavior runs.
        f.Runtime.FreeplayObserver.ForcedActivity = "Hiking";
        var on = Data(await client.Tool("freeplay", new { enabled = true }));
        Assert.Equal("submitted", on.GetProperty("status").GetString());
        pump.Until(() => manager.Current?.Id == "Hiking_FirstLookIntro");
        var intro = Assert.IsType<PlayAnimBehavior>(manager.Current);
        var introRun = Assert.IsType<TriggerLiftSafeAnimationAction>(intro.OwnedRunner);
        Assert.Equal(AnimationTrigger.HikingIntro, introRun.Request.Trigger);
        pump.Until(() => pump.Started(introRun));

        // The original pre-command owner was PounceOnMotion_Socialize. This neutral
        // scene supplies an actual Hiking animation to interrupt with the same head request.
        long cursor = bridge.CurrentEventCursor();
        int cmd = await client.Send("tools/call", new { name = "head", arguments = head });
        var parked = Data(await client.Tool("get_state", new { })).GetProperty("freeplay");
        Assert.True(parked.GetProperty("pausedForCommand").GetBoolean());
        Assert.Same(f.Runtime.Bound["Wait"], manager.Current);
        Assert.True(introRun.DestructionObserved);
        var move = Assert.IsAssignableFrom<ActionRunner>(f.Robot.ActionList.GetCurrentAction());
        pump.Until(() => move.DestructionObserved);
        f.Rig.Tick(0);
        var result = Data(await client.Receive(cmd));
        Assert.Equal(0u, result.GetProperty("engineResult").GetUInt32());
        var resumed = Data(await client.Tool("get_state", new { })).GetProperty("freeplay");
        Assert.True(resumed.GetProperty("active").GetBoolean());
        Assert.False(resumed.GetProperty("pausedForCommand").GetBoolean());
        Assert.Same(f.Runtime.FreeplayObserver, manager.Activity);

        pump.Until(() => manager.Current?.Id == "Hiking_FirstLookIntro");
        var restartedIntro = Assert.IsType<TriggerLiftSafeAnimationAction>(intro.OwnedRunner);
        Assert.NotSame(introRun, restartedIntro);
        pump.Until(() => pump.Ended(restartedIntro));
        AssertNamedClip(f, restartedIntro, AnimationTrigger.HikingIntro);

        pump.Until(() => manager.Current?.Id == "Hiking_ThinkAboutBeacons");
        var beacon = Assert.IsType<ThinkAboutBeaconsBehavior>(manager.Current);
        Assert.NotNull(beacon.Selected);
        var beaconRun = Assert.IsType<TriggerAnimationAction>(beacon.OwnedRunner);
        pump.Until(() => pump.Ended(beaconRun));
        AssertNamedClip(f, beaconRun, AnimationTrigger.HikingReactToNewArea);
        var reports = await BehaviorEvents(client, cursor);
        var names = reports.Select(p => p.GetProperty("newBehavior").GetString()).ToArray();
        output.WriteLine(string.Join(" -> ", names));
        int introIndex = Array.IndexOf(names, "Hiking_FirstLookIntro");
        int beaconIndex = Array.IndexOf(names, "Hiking_ThinkAboutBeacons");
        Assert.True(introIndex > 0 && beaconIndex > introIndex);
        Assert.Contains("Wait", names[..introIndex]);
        Assert.Contains(reports, p => p.GetProperty("newBehavior").GetString() == "Hiking_ThinkAboutBeacons" &&
            p.GetProperty("activity").GetString() == "Hiking");
    }

    [Theory]
    [InlineData("VC_AlrightyResponse", AnimationTrigger.VC_Alrighty)]
    [InlineData("VC_HowAreYouDoing_AllGood", AnimationTrigger.VC_HowAreYouDoing_AllGood)]
    [InlineData("VC_HowAreYouDoing_Energy", AnimationTrigger.NeutralFace)]
    [InlineData("VC_HowAreYouDoing_Play", AnimationTrigger.NeutralFace)]
    [InlineData("VC_HowAreYouDoing_Repair", AnimationTrigger.NeutralFace)]
    public async Task ConfiguredVoiceResponseRunsNativeLastFaceTurnAndThenAuthoredAnimation(string id, AnimationTrigger expectedTrigger)
    {
        using var f = new Fixture(preloadAnimations: true, pumpAnimations: true, sourceNvEmpty: true);
        using var bridge = new CozmoBridge(f.Rig.Robot);
        await using var client = new McpServerTests.Client(bridge);
        await client.Initialize();
        var manager = f.Robot.BehaviorManagerOwner!;
        var response = Assert.IsType<PlayAnimBehavior>(f.Runtime.Bound[id]);
        var pump = new DeviceReplies(f);

        // These voice rows have no Freeplay config slot. Exercise the shipped Selection
        // ExecuteBehaviorByID request and the base nonempty-trigger gate with an empty FaceWorld.
        Assert.Empty(f.M.Vision.Faces.GetNativeFaceIds(f.Robot));
        Assert.True(response.IsRunnable(f.Runtime.Context));
        IActionRunner? initial = null;
        var reportTransition = manager.DasTransition;
        manager.DasTransition = next =>
        {
            if (ReferenceEquals(next, response)) initial = response.OwnedRunner;
            reportTransition?.Invoke(next);
        };
        Assert.Equal("submitted", Data(await client.Tool("freeplay", new { enabled = false })).GetProperty("status").GetString());
        f.Rig.Tick(); // Apply the already-submitted Wait request before the next host command.
        var admission = Data(await client.Tool("request-behavior", new { behavior = id }));
        Assert.Equal("submitted", admission.GetProperty("status").GetString());
        Assert.Equal(1, admission.GetProperty("report").GetProperty("numRuns").GetInt32());
        Assert.Equal(2, admission.GetProperty("report").GetProperty("nativeRunCount").GetInt32()); // Wait's retained +0x40 latch is spent first.
        pump.Until(() => ReferenceEquals(manager.Current, response));
        var turn = Assert.IsType<NativeLastFaceRunner>(initial);
        Assert.Equal(0, turn.RequestedId);
        Assert.Equal(Radians.Pi, turn.MaximumTurn);
        Assert.False(turn.SayName);
        pump.Until(() => response.OwnedRunner is TriggerLiftSafeAnimationAction);
        var clip = Assert.IsType<TriggerLiftSafeAnimationAction>(response.OwnedRunner);
        Assert.Equal(expectedTrigger, clip.Request.Trigger);
        pump.Until(() => pump.Ended(clip));
        pump.Until(() => manager.Current?.Id == "Wait");
        AssertNamedClip(f, clip, expectedTrigger);
        var reports = await BehaviorEvents(client, 0);
        Assert.Contains(reports, p => p.GetProperty("newBehavior").GetString() == id);
    }

    [Fact]
    public async Task BoredAnimationPlaysTheShippedSequenceBeforeSelectionReturnsToWait()
    {
        using var f = new Fixture(preloadAnimations: true, pumpAnimations: true, sourceNvEmpty: true);
        using var bridge = new CozmoBridge(f.Rig.Robot);
        await using var client = new McpServerTests.Client(bridge);
        await client.Initialize();
        var manager = f.Robot.BehaviorManagerOwner!;
        var bored = Assert.IsType<PlayAnimBehavior>(f.Runtime.Bound["NothingToDo_BoredAnim"]);
        var pump = new DeviceReplies(f);
        f.Rig.Robot.SetEnableFreeplayActivity(false);
        f.Rig.Robot.ExecuteBehaviorById((byte)BehaviorID.NothingToDo_BoredAnim, 1);
        pump.Until(() => ReferenceEquals(manager.Current, bored));
        var sequence = Assert.IsType<CompoundActionSequential>(bored.OwnedRunner);
        var children = sequence.Children.Select(Assert.IsType<TriggerLiftSafeAnimationAction>).ToArray();
        Assert.Equal(3, children.Length);
        Assert.Equal(bored.Triggers, children.Select(child => child.Request.Trigger));
        foreach (var child in children)
        {
            pump.Until(() => pump.Ended(child));
            AssertNamedClip(f, child, child.Request.Trigger);
        }
        pump.Until(() => manager.Current?.Id == "Wait");
        Assert.True(sequence.DestructionObserved);
        Assert.Contains(await BehaviorEvents(client, 0), p => p.GetProperty("newBehavior").GetString() == bored.Id);
    }

    [Fact]
    public async Task RecordedReturnToTreadsAdmitsThePutDownScan()
    {
        using var f = new Fixture(preloadAnimations: true, pumpAnimations: true, sourceNvEmpty: true, keepNeedsFull: true);
        using var bridge = new CozmoBridge(f.Rig.Robot);
        await using var client = new McpServerTests.Client(bridge);
        await client.Initialize();
        await client.Tool("freeplay", new { enabled = true });
        f.Rig.Tick();
        var manager = f.Robot.BehaviorManagerOwner!;
        var pump = new DeviceReplies(f);
        int mark = f.Rig.Mark();
        if (Bundle is { } bundle)
        {
            ReplayReturnToTreads(f, bundle);
        }
        else
        {
            for (int i = 0; i < 6; i++) f.Motors(RobotStatusFlag.IsPickedUp);
        }

        // The recorded sweep looks far enough to see a face and enters Socialize;
        // this fixture leaves the rest of the sweep empty after recorded treads input.
        var look = Assert.IsType<NativeLookForFaceAndCubeBehavior>(f.Runtime.Bound["PutDownDispatch_LookForFaceAndCube"]);
        pump.Until(() => ReferenceEquals(manager.Current, look));
        pump.Until(() => f.Rig.SentSince(mark).Any(m => m is SetHeadAngle or SetBodyAngle));
        var selected = await BehaviorEvents(client, 0);
        Assert.Contains(selected, p => p.GetProperty("newBehavior").GetString() == look.Id &&
            p.GetProperty("activity").GetString() == "PutDownDispatch");
        // At state 6 the stock neither-face-nor-cube branch publishes Hiking to
        // Freeplay. The subsequent activity choice also tests its current eligibility.
        pump.Until(() => f.Runtime.FreeplayObserver.DesiredActivityCacheForTest == "Hiking");
        pump.Until(() => !ReferenceEquals(manager.Current, look));
    }

    [Fact]
    public async Task StagedAnimationAndLookInPlaceReportTheirOwnersThroughMcp()
    {
        using var f = new Fixture(preloadAnimations: true, pumpAnimations: true, sourceNvEmpty: true);
        using var bridge = new CozmoBridge(f.Rig.Robot);
        await using var client = new McpServerTests.Client(bridge);
        await client.Initialize();
        var manager = f.Robot.BehaviorManagerOwner!;
        var pump = new DeviceReplies(f);

        // ActivitySparked supplies the trigger vector at runtime. Stage that input
        // and use the configured Selection request for this fixture.
        Assert.Equal("submitted", Data(await client.Tool("freeplay", new { enabled = false })).GetProperty("status").GetString());
        f.Rig.Tick(); // Apply the already-submitted Wait request before the next host command.
        var arbitrary = Assert.IsType<NativePlayArbitraryAnimBehavior>(f.Runtime.Bound["PlayArbitraryAnim"]);
        Assert.Equal("submitted", Data(await client.Tool("play-arbitrary-anim", new { trigger = "HikingIntro" })).GetProperty("status").GetString());
        pump.Until(() => ReferenceEquals(manager.Current, arbitrary));
        var clip = Assert.IsType<TriggerLiftSafeAnimationAction>(arbitrary.OwnedRunner);
        pump.Until(() => pump.Ended(clip));
        AssertNamedClip(f, clip, AnimationTrigger.HikingIntro);
        pump.Until(() => manager.Current?.Id == "Wait");

        var scan = Assert.IsType<NativeSparksLookInPlace>(f.Runtime.Bound["SparksLookInPlace"]);
        int mark = f.Rig.Mark();
        Assert.Equal("submitted", Data(await client.Tool("request-behavior", new { behavior = "SparksLookInPlace" })).GetProperty("status").GetString());
        pump.Until(() => ReferenceEquals(manager.Current, scan));
        pump.Until(() => f.Rig.SentSince(mark).Any(m => m is SetBodyAngle));
        var reports = await BehaviorEvents(client, 0);
        var names = reports.Select(p => p.GetProperty("newBehavior").GetString()).ToArray();
        int stagedAt = Array.IndexOf(names, arbitrary.Id);
        int scanAt = Array.IndexOf(names, scan.Id);
        Assert.True(stagedAt >= 0 && scanAt > stagedAt);
        Assert.Contains("Wait", names[(stagedAt + 1)..scanAt]);
    }

    [Fact]
    public async Task ArbitraryBehaviorRequestParksAndReturnsToFreeplay()
    {
        using var f = new Fixture(preloadAnimations: true, pumpAnimations: true, sourceNvEmpty: true);
        using var bridge = new CozmoBridge(f.Rig.Robot);
        await using var client = new McpServerTests.Client(bridge);
        await client.Initialize();
        f.Rig.Robot.SetEnableFreeplayActivity(true);
        f.Rig.Tick();
        var manager = f.Robot.BehaviorManagerOwner!;
        var arbitrary = Assert.IsType<NativePlayArbitraryAnimBehavior>(f.Runtime.Bound["PlayArbitraryAnim"]);
        var pump = new DeviceReplies(f);
        var result = Data(await client.Tool("play-arbitrary-anim", new { trigger = "HikingIntro" }));
        Assert.Equal("submitted", result.GetProperty("status").GetString());
        Assert.True(f.Runtime.ObserveHostFreeplay().PausedForCommand);
        pump.Until(() => ReferenceEquals(manager.Current, arbitrary));
        var clip = Assert.IsType<TriggerLiftSafeAnimationAction>(arbitrary.OwnedRunner);
        Assert.Equal(AnimationTrigger.HikingIntro, clip.Request.Trigger);
        pump.Until(() => pump.Ended(clip));
        pump.Until(() => f.Runtime.ObserveHostFreeplay().Active);
        Assert.False(f.Runtime.ObserveHostFreeplay().PausedForCommand);
        Assert.Same(f.Runtime.FreeplayObserver, manager.Activity);
        AssertNamedClip(f, clip, AnimationTrigger.HikingIntro);
    }

    [Fact]
    public async Task ConfiguredScanUsesSelectionAndRemembersOnlyTheCompletedScanLocation()
    {
        using var f = new Fixture(preloadAnimations: true, pumpAnimations: true, sourceNvEmpty: true);
        var manager = f.Robot.BehaviorManagerOwner!;
        var scan = Assert.IsType<NativeSparksLookInPlace>(f.Runtime.Bound["Hiking_LookInPlace360"]);
        var pump = new DeviceReplies(f);

        Assert.True(scan.ParametersAvailable);
        f.Robot.CarryingOwner!.SetCarrying(7);
        Assert.False(scan.IsRunnable(f.Runtime.Context)); // vtable +0x28 at 0x005C1D76.
        f.Robot.CarryingOwner.UnsetCarrying();
        Assert.True(scan.IsRunnable(f.Runtime.Context));

        // Keep the shipped random draws but make their timeline reproducible.
        f.Rig.Engine.ContextRandom.SetSeed(11);
        using var bridge = new CozmoBridge(f.Rig.Robot);
        Assert.Equal("submitted", (await BridgeCommandTests.Invoke(bridge, "request-behavior", """{"behavior":"Hiking_LookInPlace360"}""")).GetProperty("status").GetString());
        pump.Until(() => ReferenceEquals(manager.Current, scan));
        pump.Until(() => scan.NativeState == 2);
        var bodyPause = Assert.IsType<TriggerLiftSafeAnimationAction>(scan.OwnedRunner);
        Assert.Equal(AnimationTrigger.LookInPlaceForFacesBodyPause, bodyPause.Request.Trigger);
        pump.Until(() => scan.NativeState == 3);
        Assert.IsType<NativePanTiltRunner>(scan.OwnedRunner);
        pump.Until(() => scan.NativeState == 4);
        var headPause = Assert.IsType<TriggerLiftSafeAnimationAction>(scan.OwnedRunner);
        Assert.Equal(AnimationTrigger.LookInPlaceForFacesHeadMovePause, headPause.Request.Trigger);
        pump.Until(() => scan.OwnedRunner is NativePanTiltRunner);
        Assert.Equal(4, scan.NativeState);
        pump.Until(() => scan.OwnedRunner is TriggerLiftSafeAnimationAction && !ReferenceEquals(headPause, scan.OwnedRunner));
        Assert.Equal(4, scan.NativeState); // The shipped minimum is two pause/turn pairs.
        pump.Until(() => scan.NativeState == 5);
        Assert.IsType<NativePanTiltRunner>(scan.OwnedRunner);
        pump.Until(() => manager.Current?.Id == "Wait", timeoutSeconds: 180);

        Assert.True(scan.SearchIterations > 1);
        Assert.False(scan.IsRunnable(f.Runtime.Context));
        f.Yaw = 0; // Align the live frame so the 300 mm boundary is exact in f32.
        f.X = 299;
        f.Motors();
        Assert.False(scan.IsRunnable(f.Runtime.Context));
        f.X = 300;
        f.Motors();
        Assert.True(scan.IsRunnable(f.Runtime.Context));
    }

    [Theory]
    [InlineData(BehaviorID.FeedingPlayRequestAtFace, AnimationTrigger.NeedsMildLowEnergyRequest)]
    [InlineData(BehaviorID.FeedingPlayRequestAtFace_Severe, AnimationTrigger.NeedsSevereLowEnergyRequest)]
    public void ConfiguredFeedingRequestTurnsToFaceBeforeItsShippedAnimation(BehaviorID id, AnimationTrigger trigger)
    {
        using var f = new Fixture(preloadAnimations: true, pumpAnimations: true, sourceNvEmpty: true);
        var manager = f.Robot.BehaviorManagerOwner!;
        var request = Assert.IsType<PlayAnimBehavior>(f.Runtime.Bound[id.ToString()]);
        var pump = new DeviceReplies(f);
        var priorTransition = manager.DasTransition;
        IActionRunner? initial = null;
        manager.DasTransition = next =>
        {
            if (ReferenceEquals(next, request)) initial = request.OwnedRunner;
            priorTransition?.Invoke(next);
        };

        f.Rig.Robot.ExecuteBehaviorById((byte)id, 1);
        pump.Until(() => ReferenceEquals(manager.Current, request));
        var turn = Assert.IsType<NativeLastFaceRunner>(initial);
        Assert.Equal(0, turn.RequestedId);
        Assert.Equal(Radians.Pi, turn.MaximumTurn);
        Assert.False(turn.SayName);
        pump.Until(() => request.OwnedRunner is TriggerLiftSafeAnimationAction);
        var clip = Assert.IsType<TriggerLiftSafeAnimationAction>(request.OwnedRunner);
        Assert.Equal(trigger, clip.Request.Trigger);
        pump.Until(() => manager.Current?.Id == "Wait");
        AssertNamedClip(f, clip, trigger);
    }

    [Fact]
    public void ConfiguredSparkGateInputsAndCubeMovedReactionShareTheRobotOwners()
    {
        using var f = new Fixture(sourceNvEmpty: true);
        var manager = f.Robot.BehaviorManagerOwner!;
        string[] ids = ["SparksLookInPlace", "Hiking_VisitInterestingEdge", "RespondToRenameFace",
            "SparksPutDownBlock", "Hiking_DriveOffCharger", "EnrollFace", "Hiking_ThinkAboutBeacons", "SparksThinkAboutBeacons"];
        Assert.True(Assert.IsType<NativeFindFacesSocialize>(f.Runtime.Bound["SparksFindFaces"]).ParametersAvailable);
        Assert.Equal(SteppedBehavior.NoUnlockId, manager.ActiveSparkId);
        // +0x70 is the common base field, not a per-class opt-in; check every constructed
        // configured stepped behavior, including those not in the former allowlist.
        var configuredSteps=f.Runtime.Bound.Values.OfType<SteppedBehavior>()
            .Where(b=>b.NativeBaseInputs is not null).ToArray();
        Assert.NotEmpty(configuredSteps);
        foreach(var behavior in configuredSteps)
        {
            Assert.Equal(unchecked((int)behavior.NativeBaseInputs!.RequiredUnlock),behavior.UnlockIdentifier!());
            Assert.Equal(SteppedBehavior.NoUnlockId,behavior.ActiveSparkIdentifier!());
        }
        foreach (string id in ids)
        {
            var behavior = Assert.IsAssignableFrom<SteppedBehavior>(f.Runtime.Bound[id]);
            Assert.Equal(SteppedBehavior.NoUnlockId, behavior.UnlockIdentifier!());
            Assert.Equal(SteppedBehavior.NoUnlockId, behavior.ActiveSparkIdentifier!());
        }
        manager.SetNativeRequestedSpark(13, 0);
        manager.SwitchToNativeRequestedSpark();
        foreach (string id in ids)
            Assert.Equal(13, ((SteppedBehavior)f.Runtime.Bound[id]).ActiveSparkIdentifier!());
        foreach(var behavior in configuredSteps)
        {
            Assert.True(behavior.NativeBaseInputs!.TryGetActiveSparkId(out int active));
            Assert.Equal(13,active);
        }

        var moved = Assert.Single(manager.Reactions, entry => entry.Strategy.Trigger == ReactionTrigger.CubeMoved);
        Assert.Same(f.Runtime.Bound["ReactToCubeMoved"], moved.Behavior);
        Assert.Same(f.Runtime.Container.Behaviors[BehaviorID.ReactToCubeMoved], moved.Behavior);
    }

    private static void ReplayReturnToTreads(Fixture f, string bundle)
    {
        var states = new List<(double At, RobotState State)>();
        foreach (string line in File.ReadLines(Path.Combine(bundle, "messages.jsonl")))
        {
            using var doc = JsonDocument.Parse(line);
            var row = doc.RootElement;
            double at = row.GetProperty("atMs").GetDouble();
            if (at > 242400) break;
            if (at < 241560 || row.GetProperty("outbound").GetBoolean() || row.GetProperty("tag").GetString() != "0xF0") continue;
            var state = Assert.IsType<RobotState>(RobotMessage.Parse(Convert.FromHexString(row.GetProperty("payloadHex").GetString()!)));
            states.Add((at, state));
        }
        Assert.Contains(states, pair => pair.State.Has(RobotStatusFlag.IsPickedUp));
        Assert.Contains(states, pair => !pair.State.Has(RobotStatusFlag.IsPickedUp));
        uint offset = states[0].State.Timestamp - f.Robot.StoredState!.Timestamp - 60;
        double last = states[0].At - 60;
        foreach (var (at, state) in states)
        {
            state.Timestamp -= offset;
            state.PoseOriginId = f.Robot.CurrentOriginId;
            state.PoseFrameId = f.Robot.PoseFrameId;
            f.Rig.Data(state);
            f.Rig.Tick(at - last);
            last = at;
        }
        var final = states[^1].State;
        f.Rig.ContinueRobotTimestampAfterReplay(final.Timestamp);
        f.X = final.Pose.X;
        f.Y = final.Pose.Y;
        f.Yaw = final.Pose.Angle;
        f.Head = final.HeadAngle;
        f.Lift = final.LiftAngle;
    }

    private static (double At, string? Event, string? Text) ParseTrace(string line)
    {
        using var doc = JsonDocument.Parse(line);
        var row = doc.RootElement;
        return (row.GetProperty("atMs").GetDouble(), row.TryGetProperty("name", out var name) ? name.GetString() : null,
            row.TryGetProperty("text", out var text) ? text.GetString() : null);
    }

    private static JsonElement ReadHeadRequest(string bundle)
    {
        foreach (string line in File.ReadLines(Path.Combine(bundle, "events.jsonl")))
        {
            using var doc = JsonDocument.Parse(line);
            var row = doc.RootElement;
            if (row.GetProperty("atMs").GetDouble() is < 271116 or > 271117 || row.GetProperty("kind").GetString() != "mcp-input") continue;
            var request = row.GetProperty("params");
            Assert.Equal("head", request.GetProperty("name").GetString());
            return request.GetProperty("arguments").Clone();
        }
        throw new InvalidDataException("The archived 271116 ms head request is absent.");
    }

    private static void AssertNamedClip(Fixture f, TriggerAnimationAction action, AnimationTrigger trigger)
    {
        var library = f.Rig.Robot.Animations.Library!;
        var group = library.GetGroup(library.GetAnimationForTrigger(trigger));
        Assert.NotNull(group);
        Assert.Contains(group.Entries, e => e.Name == action.GetAnimationCompletionName());
    }

    private static JsonElement Data(JsonElement reply) => reply.GetProperty("result").GetProperty("structuredContent");

    private static async Task<JsonElement[]> BehaviorEvents(McpServerTests.Client client, long since) =>
        Data(await client.Tool("read_events", new { since, types = new[] { "behavior-changed" } }))
            .GetProperty("events").EnumerateArray().Select(item => item.GetProperty("payload")).ToArray();

    private sealed class DeviceReplies(Fixture f)
    {
        private int mark = f.Rig.Mark();
        private byte current;
        private readonly HashSet<byte> starts = new(), ends = new();

        internal bool Started(TriggerAnimationAction action) => action.HostAnimationTag != 0 && starts.Contains(action.HostAnimationTag);
        internal bool Ended(TriggerAnimationAction action) => action.HostAnimationTag != 0 && ends.Contains(action.HostAnimationTag);

        internal void Until(Func<bool> condition, double timeoutSeconds = 60)
        {
            double deadline = f.Rig.Engine.Timer.Seconds + timeoutSeconds;
            while (!condition() && f.Rig.Engine.Timer.Seconds < deadline)
            {
                f.AnswerInFlightNv();
                f.Motors();
                foreach (var message in f.Rig.SentSince(mark))
                {
                    switch (message)
                    {
                        case StartOfAnimation start:
                            current = start.AnimId;
                            starts.Add(current);
                            ends.Remove(current);
                            f.Rig.Data(new AnimationStarted { AnimId = current });
                            break;
                        case EndOfAnimation:
                            ends.Add(current);
                            f.Rig.Data(new AnimationEnded { AnimId = current });
                            break;
                    }
                }
                mark = f.Rig.Mark();
                f.Rig.Engine.DrainAppDelivery();
                Thread.Sleep(1);
            }
            Assert.True(condition(), $"{f.Runtime.Observe()} at {f.Rig.Engine.Timer.Seconds:F1}s");
            f.Rig.Engine.DrainAppDelivery();
        }
    }
}
