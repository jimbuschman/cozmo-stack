using System.Collections.Concurrent;
using System.Text.Json;
using Cozmo.Bridge;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Xunit;
using Xunit.Abstractions;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class NeedsPersonalityMcpTests(ITestOutputHelper output)
{
    [Fact]
    public async Task NativeThresholdsSelectNeedsBehaviorAndRecoveryThroughMcp()
    {
        string directory = Directory.CreateTempSubdirectory("cozmo-needs-personality-").FullName;
        try
        {
            long wall = NativeRewardCanonicalTests.FixedClockMicros + 20_000_000;
            var options = NativeRewardCanonicalTests.SourceOptions(directory, () => wall, 1f, 0);
            var stored = NativeRewardCanonicalTests.StoredBlob(1, 1, 1, 2, 1, NativeRewardCanonicalTests.InitialSeconds());
            using var f = new Fixture(preloadAnimations: true, pumpAnimations: true, engineOptions: options,
                sourceNvEmpty: true, keepNeedsFull: true, sourceNvStored: tag => tag == NeedsManager.NeedsNvKey ? stored : null);
            using var bridge = new CozmoBridge(f.Rig.Robot, eventCapacity: 8192);
            await using var client = new McpServerTests.Client(bridge);
            await client.Initialize();
            Assert.True(f.Rig.Engine.GetNeedsStatus().Loaded);
            Assert.True(f.Rig.Engine.GetNeedsStatus().Paused);
            var logs = new ConcurrentQueue<string>();
            var forcedDas = new List<string>();
            f.Rig.Engine.StackLogLine += line => { if (line.StartsWith("MISSING", StringComparison.Ordinal)) logs.Enqueue(line); };
            f.Rig.Engine.LogEntry += e =>
            {
                if (e.Name == "needs.force_set_needs_levels") forcedDas.Add(e.Text);
            };

            var mode = Data(await client.Tool("set_needs_mode", new { mode = "real" }));
            Assert.Equal("confirmed", mode.GetProperty("status").GetString());
            Assert.False(mode.GetProperty("report").GetProperty("paused").GetBoolean());
            f.Rig.Tick(75000); // Shipped mild cooldown is at most 60 s.
            Assert.Equal("submitted", Data(await client.Tool("freeplay", new { enabled = true })).GetProperty("status").GetString());
            var manager = f.Robot.BehaviorManagerOwner!;
            var animations = new HashSet<AnimationTrigger>();
            var animationTags = new Dictionary<byte, AnimationTrigger>();
            var completedAnimations = new HashSet<AnimationTrigger>();
            int mark = f.Rig.Mark();
            byte activeAnimation = 0;
            var paths = new List<ExecutePath>();
            var segments = new List<AppendPathSegmentLine>();
            (float X, float Y, float Angle)? pathEnd = null;
            ExecutePath? executingPath = null;
            double pathDue = 0;
            f.Rig.DefaultTickMs = 100;

            try
            {
                foreach (var (need, boundary, warning, getIn) in new[]
                {
                    (NeedId.Energy, .21f, AnimationTrigger.NeedsMildLowEnergyRequest, AnimationTrigger.NeedsSevereLowEnergyGetIn),
                    (NeedId.Repair, .27f, AnimationTrigger.NeedsMildLowRepairRequest, AnimationTrigger.NeedsSevereLowRepairGetIn),
                    (NeedId.Play, .14f, AnimationTrigger.NeedsMildLowPlayRequest, AnimationTrigger.NeedsSevereLowPlayGetIn),
                })
                {
                    string lower = need.ToString().ToLowerInvariant();
                    long cursor = bridge.CurrentEventCursor();
                    var prior = f.Rig.Engine.GetNeedsStatus();
                    var result = await Set(need, boundary);
                    Assert.Equal(boundary, result.GetProperty(lower).GetSingle());
                    await AssertBracket(lower, "Warning", cursor);
                    if (need == NeedId.Energy)
                        Assert.Equal(FormattableString.Invariant($"{prior.Repair:F5}:{prior.Energy:F5}:{prior.Play:F5}:1.00000:0.21000:1.00000"),
                            forcedDas[^1]);
                    // Keep the live selection check inside the shipped Warning band while
                    // the behavior waits for a turn. Update (0x00695CBE..0x00695CE4) can
                    // apply its 60 s decay tick after the exact-boundary API check above.
                    await Set(need, boundary + .05f);
                    string warningId = $"Needs_MildLow{need}Request";
                    PumpUntil(() => manager.Current?.Id == warningId && animations.Contains(warning));
                    PumpUntil(() => manager.Current?.Id != warningId);
                    Assert.Contains(warning, completedAnimations);
                    await AssertStarted(warningId, cursor);

                    cursor = bridge.CurrentEventCursor();
                    await Set(need, MathF.BitDecrement(boundary));
                    await AssertBracket(lower, "Critical", cursor);
                    string getInId = $"Needs_SevereLow{need}GetIn";
                    PumpUntil(() => manager.Current?.Id == getInId && animations.Contains(getIn));
                    PumpUntil(() => f.Runtime.AI.SevereNeeds.Selected == (int)need);
                    Assert.Contains(getIn, completedAnimations);
                    await AssertStarted(getInId, cursor);

                    if (need == NeedId.Play)
                    {
                        PumpUntil(() => manager.Current?.Id == "Needs_SevereLowPlayBored");
                        PumpUntil(() => completedAnimations.Contains(AnimationTrigger.NothingToDoBoredIntro) &&
                            completedAnimations.Contains(AnimationTrigger.NothingToDoBoredEvent) &&
                            completedAnimations.Contains(AnimationTrigger.NothingToDoBoredOutro));
                        await AssertStarted("Needs_SevereLowPlayBored", cursor);
                    }
                    else
                    {
                        int pathsBefore = paths.Count;
                        string severe = $"Needs_SevereLow{need}State";
                        PumpUntil(() => manager.Current?.Id == severe);
                        Assert.Equal(need == NeedId.Energy ? 40f : 50f, f.Rig.Robot.Motion.Path.NativeMotionProfile.SpeedMmps);
                        PumpUntil(() => manager.Current?.Id == severe && paths.Count > pathsBefore);
                        await AssertStarted(severe, cursor);
                    }

                    cursor = bridge.CurrentEventCursor();
                    await Set(need, 1);
                    await AssertBracket(lower, "Full", cursor);
                    PumpUntil(() => f.Runtime.AI.SevereNeeds.Selected == 3 &&
                        manager.Current?.Id.StartsWith($"Needs_SevereLow{need}", StringComparison.Ordinal) != true);
                    var recovered = Data(await client.Tool("get_state", new { })).GetProperty("needs");
                    Assert.Equal(JsonValueKind.Null, recovered.GetProperty("selectedSevereNeed").ValueKind);
                }

                long rewardCursor = bridge.CurrentEventCursor();
                // Input from the native Behavior::NeedActionCompleted recipient (0x005BE40C -> 0x006960D0).
                // A refill cannot mark a reward pending. Use the real weighted producer with stored level 2.
                Assert.True(f.Rig.Engine.Needs.RegisterNeedsActionCompleted("RollACube"));
                PumpUntil(() => manager.Current?.Id == "EarnedSparks" && animations.Contains(AnimationTrigger.EarnedSparks));
                PumpUntil(() => manager.Current?.Id != "EarnedSparks");
                Assert.Contains(AnimationTrigger.EarnedSparks, completedAnimations);
                await AssertStarted("EarnedSparks", rewardCursor);
                var rewards = await Events(rewardCursor, "freeplay-sparks-awarded");
                var reward = Assert.Single(rewards.EnumerateArray()).GetProperty("payload");
                Assert.InRange(reward.GetProperty("amount").GetInt32(), 2, 3);
                Assert.Equal("needs.FreeplaySparksReward.RollACube", reward.GetProperty("displayKey").GetString());
            }
            finally
            {
                output.WriteLine($"At {f.Rig.Engine.Timer.Seconds:F1}s: {f.Runtime.Observe()}, runner={(manager.Current as SteppedBehavior)?.OwnedRunner?.GetType().Name}, animations={string.Join(',',animations)}, paths={paths.Count}, lines={segments.Count}");
                foreach (string line in logs) output.WriteLine(line);
            }

            async Task<JsonElement> Set(NeedId need, float value)
            {
                var response = Data(await client.Tool("set_needs", new
                {
                    repair = need == NeedId.Repair ? value : 1f,
                    energy = need == NeedId.Energy ? value : 1f,
                    play = need == NeedId.Play ? value : 1f,
                }));
                Assert.Equal("confirmed", response.GetProperty("status").GetString());
                return response.GetProperty("report");
            }

            async Task AssertBracket(string need, string bracket, long cursor)
            {
                f.Rig.Engine.DrainAppDelivery();
                var reports = await Events(cursor, "needs-changed");
                var report = Assert.Single(reports.EnumerateArray(), item =>
                    item.GetProperty("payload").GetProperty("action").GetString() == "NoAction").GetProperty("payload");
                Assert.Equal(bracket, report.GetProperty("brackets").GetProperty(need).GetString());
                var state = Data(await client.Tool("get_state", new { })).GetProperty("needs");
                Assert.Equal(bracket, state.GetProperty("brackets").GetProperty(need).GetString());
                Assert.Equal(report.GetProperty(need).GetSingle(), state.GetProperty(need).GetSingle());
            }

            async Task AssertStarted(string behavior, long cursor)
            {
                var reports = await Events(cursor, "behavior-changed");
                Assert.Contains(reports.EnumerateArray(), item =>
                    item.GetProperty("payload").GetProperty("newBehavior").GetString() == behavior);
            }

            async Task<JsonElement> Events(long cursor, string type) =>
                Data(await client.Tool("read_events", new { since = cursor, types = new[] { type } })).GetProperty("events");

            void ObserveAnimation(TriggerAnimationAction animation)
            {
                animations.Add(animation.Request.Trigger);
                if (animation.AnimationTagForTest != 0) animationTags[animation.AnimationTagForTest] = animation.Request.Trigger;
            }

            void PumpUntil(Func<bool> condition)
            {
                double deadline = f.Rig.Engine.Timer.Seconds + 55;
                while (!condition() && f.Rig.Engine.Timer.Seconds < deadline)
                {
                    f.AnswerInFlightNv();
                    f.Motors();
                    wall = NativeRewardCanonicalTests.FixedClockMicros + 20_000_000 + (long)(f.Rig.Engine.Timer.Seconds * 1_000_000);
                    if ((manager.Current as SteppedBehavior)?.OwnedRunner is ActionRunner action)
                    {
                        if (action is TriggerAnimationAction trigger) ObserveAnimation(trigger);
                        if (action is CompoundActionSequential sequence)
                            foreach (var child in sequence.Children.OfType<TriggerAnimationAction>()) ObserveAnimation(child);
                    }
                    foreach (var message in f.Rig.SentSince(mark))
                    {
                        switch (message)
                        {
                            case StartOfAnimation begin:
                                activeAnimation = begin.AnimId;
                                f.Rig.Data(new AnimationStarted { AnimId = activeAnimation });
                                break;
                            case EndOfAnimation:
                                if (animationTags.Remove(activeAnimation, out var endedTrigger))
                                    completedAnimations.Add(endedTrigger);
                                f.Rig.Data(new AnimationEnded { AnimId = activeAnimation });
                                break;
                            case ExecutePath path:
                                paths.Add(path);
                                executingPath = path;
                                pathDue = f.Rig.Engine.Timer.Seconds + 1;
                                f.Rig.Data(new PathFollowingEvent { EventId = path.EventId, EventType = 0 });
                                break;
                            case ClearPath:
                                executingPath = null;
                                pathEnd = null;
                                break;
                            case AppendPathSegmentLine segment:
                                segments.Add(segment);
                                float yaw = MathF.Atan2(segment.YEndMm - segment.YStartMm, segment.XEndMm - segment.XStartMm);
                                pathEnd = (segment.XEndMm, segment.YEndMm, segment.Speed.SpeedMmps < 0 ? yaw + MathF.PI : yaw);
                                break;
                            case AppendPathSegmentArc arc:
                                float angle = arc.StartRad + arc.SweepRad;
                                var (sin, cos) = MathF.SinCos(angle);
                                pathEnd = (arc.XCenterMm + cos * arc.RadiusMm, arc.YCenterMm + sin * arc.RadiusMm,
                                    angle + MathF.CopySign(MathF.PI / 2, arc.SweepRad));
                                break;
                            case AppendPathSegmentPointTurn turn:
                                pathEnd = (turn.XMm, turn.YMm, turn.TargetAngleRad);
                                break;
                        }
                    }
                    mark = f.Rig.Mark();
                    if (executingPath is { } active && f.Rig.Engine.Timer.Seconds >= pathDue)
                    {
                        if (pathEnd is { } end)
                        {
                            float offset = (float)(f.Robot.GetCheckedForwardPose().Translation -
                                f.Robot.GetCheckedMainPose().Translation).Length;
                            f.X = end.X - offset * MathF.Cos(end.Angle);
                            f.Y = end.Y - offset * MathF.Sin(end.Angle);
                            f.Yaw = end.Angle;
                        }
                        f.Rig.Data(new PathFollowingEvent { EventId = active.EventId, EventType = 2 });
                        executingPath = null;
                    }
                    f.Rig.Engine.DrainAppDelivery();
                    Thread.Sleep(1); // Let the cartesian planner owner finish on its worker.
                }
                Assert.True(condition(), $"Unexpected Freeplay at {f.Rig.Engine.Timer.Seconds:F1}: {f.Runtime.Observe()}");
                f.Rig.Engine.DrainAppDelivery();
            }
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static JsonElement Data(JsonElement response)
    {
        var result = response.GetProperty("result");
        Assert.False(result.GetProperty("isError").GetBoolean());
        return result.GetProperty("structuredContent");
    }
}
