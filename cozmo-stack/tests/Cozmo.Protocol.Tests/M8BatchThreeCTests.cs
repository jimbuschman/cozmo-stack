using System.Text.Json;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// R-BEH2 batch 3c: M8-013 (the two ExecuteBehavior game-to-engine messages and the selection chooser's handler), M8-014 (the whiteboard's three handlers
/// and UpdateBeaconRender) and M8-004 (the engine's zero score default). Every expected value comes from a row of inventory M8-framework Correction A4
/// (research 20260929-R-BEH2-pre-extraction.md sections 6 and 7 with the corrections of 20260930-R-BEH2-pre-extraction-check.md) or from the
/// IBehavior constructor / EvaluateScoreInternal rows of M8-004, and is cited per test.
/// </summary>
public class M8BatchThreeCTests
{
    private static float F(int bits) => BitConverter.Int32BitsToSingle(bits);

    // ------------------------------------------------------------------ M8-013: the wire

    /// <summary>
    /// M8-013 (A4 wire layout; pre-extraction section 7): union tag uint16 0x0093 / 0x0094, payload uint8 selector then signed int32 numRuns little-endian,
    /// 7 bytes in all, no padding (native pack 0x00738C38..0x00738C68 and 0x00738D7C..0x00738DAC).
    /// </summary>
    [Fact]
    public void TheExecuteBehaviorMessagesAreSevenBytesWithTheExactTagsAndNoPadding()
    {
        Assert.Equal(new byte[] { 0x94, 0x00, 0xB2, 0xFF, 0xFF, 0xFF, 0xFF }, new ExecuteBehaviorByIDMessage(0xB2, -1).ToUnionBytes());
        Assert.Equal(new byte[] { 0x93, 0x00, 0x05, 0x02, 0x00, 0x00, 0x00 }, new ExecuteBehaviorByExecutableTypeMessage(5, 2).ToUnionBytes());
        Assert.Equal(new byte[] { 0x07, 0x78, 0x56, 0x34, 0x12 }, new ExecuteBehaviorByIDMessage(7, 0x12345678).PackBody());   // little-endian, 5-byte payload
        Assert.Equal(5, ExecuteBehaviorMessage.PayloadSize);
    }

    [Fact]
    public void TheExecuteBehaviorMessagesUnpackFromTheExactBytes()
    {
        var id = ExecuteBehaviorByIDMessage.UnpackBody(new byte[] { 0xB2, 0xFE, 0xFF, 0xFF, 0xFF });          // numRuns is signed: -2
        Assert.Equal(0xB2, id.BehaviorID);
        Assert.Equal(-2, id.NumRuns);
        var ex = ExecuteBehaviorByExecutableTypeMessage.UnpackBody(new byte[] { 0x09, 0x00, 0x00, 0x00, 0x00 });  // zero is preserved verbatim
        Assert.Equal(9, ex.ExecutableBehaviorType);
        Assert.Equal(0, ex.NumRuns);
        Assert.Equal(0x0094, ExecuteBehaviorByIDMessage.UnionTag);
        Assert.Equal(0x0093, ExecuteBehaviorByExecutableTypeMessage.UnionTag);
        // the unpack is ReadBytes(1) + ReadBytes(4) with no length check (0x00738ba8..0x00738cec; Unpack 0x00751e8e forwards with no size compare):
        // trailing bytes are ignored, and a short buffer yields what the all-or-nothing reader yields (zero for the read that fails, no throw)
        var padded = ExecuteBehaviorByIDMessage.UnpackBody(new byte[] { 0x05, 0x02, 0x00, 0x00, 0x00, 0xAA, 0xBB, 0xCC });
        Assert.Equal((5, 2), (padded.BehaviorID, padded.NumRuns));
        var shortBuf = ExecuteBehaviorByIDMessage.UnpackBody(new byte[] { 0xB2, 0x01, 0x02, 0x03 });
        Assert.Equal((0xB2, 0), (shortBuf.BehaviorID, shortBuf.NumRuns));
        var empty = ExecuteBehaviorByIDMessage.UnpackBody(ReadOnlyMemory<byte>.Empty);
        Assert.Equal((0, 0), (empty.BehaviorID, empty.NumRuns));
    }

    // ------------------------------------------------------------------ M8-013: the handler

    private sealed class Fake : IBehavior
    {
        public Fake(string id) => Id = id;
        public string Id { get; }
        public string Class => "Fake";
        public bool IsRunnable(BehaviorContext c) => true;
        public double EvaluateScore(BehaviorContext c) => 0;
        public Task StartAsync(BehaviorContext c, BehaviorScope s, CancellationToken t) => Task.CompletedTask;
        public bool Update(BehaviorContext c, double nowMs) => true;
        public void Stop(BehaviorStopReason r) { }
    }

    private sealed class WithProcess : IBehavior
    {
        public WithProcess(string id) => Id = id;
        public string Id { get; }
        public string Class => "WithProcess";
        public bool IsRunnable(BehaviorContext c) => true;
        public double EvaluateScore(BehaviorContext c) => 0;
        public Task StartAsync(BehaviorContext c, BehaviorScope s, CancellationToken t) => Task.CompletedTask;
        public bool Update(BehaviorContext c, double nowMs) => true;
        public void Stop(BehaviorStopReason r) { }
    }

    private static SelectionChooser Chooser(out Fake wait, out Fake ack, out List<string> log)
    {
        wait = new Fake("Wait");
        ack = new Fake("AcknowledgeFace");
        var chooser = new SelectionChooser(new Dictionary<string, IBehavior> { ["Wait"] = wait, ["AcknowledgeFace"] = ack });
        var l = new List<string>();
        chooser.Log += l.Add;
        log = l;
        return chooser;
    }

    private static BehaviorContext Ctx()
    {
        var robot = CozmoRobot.CreateOffline();
        robot.Transport.OfflineAcceptConnection();
        return new BehaviorContext { Robot = robot, Triggers = new AnimationTriggerMap(), Random = new Random(1) };
    }

    /// <summary>
    /// M8-013 (A4): the ID message selects by <c>FindBehaviorByID</c> and stores numRuns at +0x3C (0x0060AA6E..0x0060AA88). BehaviorID 0xB2 is Wait
    /// (constructor 0x0060A988/0x0060A99A; BehaviorID 0xb2 per inventory C1 row 3). The chooser is the live one built by the activity loader
    /// from a <c>Selection</c> behaviorChooser config, and the message arrives through the subscription callback with the union tag.
    /// </summary>
    [Fact]
    public void AnExecuteBehaviorByIdMessageSelectsTheBehaviourAndStoresNumRuns()
    {
        var ctx = Ctx();
        var wait = new Fake("Wait"); var ack = new Fake("AcknowledgeFace");
        var bound = new Dictionary<string, IBehavior> { ["Wait"] = wait, ["AcknowledgeFace"] = ack };
        using var cfg = JsonDocument.Parse("{\"type\":\"Selection\"}");
        var chooser = Assert.IsType<SelectionChooser>(ActivityTreeLoader.BuildChooser(cfg.RootElement, bound, null, null));

        Assert.Equal(0xB2, (byte)BehaviorID.Wait);
        chooser.HandleMessage(0x0094, new byte[] { (byte)BehaviorID.AcknowledgeFace, 0x03, 0x00, 0x00, 0x00 });
        Assert.Same(ack, chooser.Requested);
        Assert.Equal(3, chooser.NumRuns);
        Assert.Same(ack, chooser.GetDesiredActiveBehavior(null, 0, ctx, 0).Behavior);

        chooser.HandleMessage(0x0094, new byte[] { 0xB2, 0xFF, 0xFF, 0xFF, 0xFF });          // Wait, -1 unlimited
        Assert.Same(wait, chooser.Requested);
        Assert.Equal(-1, chooser.NumRuns);
    }

    /// <summary>
    /// M8-013 (A4, pre-extraction section 7): numRuns is stored at +0x3C whether or not the lookup succeeds; a miss warns ("Unknown behavior",
    /// 0x0060AB84..0x0060ABB2) and selects null.
    /// </summary>
    [Fact]
    public void AMissStillStoresNumRunsWarnsAndSelectsNull()
    {
        var chooser = Chooser(out _, out var ack, out var log);
        chooser.RequestBehavior(ack, 5);
        chooser.HandleMessage(0x0094, new byte[] { 0xFF, 0x07, 0x00, 0x00, 0x00 });        // 0xFF is no BehaviorID
        Assert.Null(chooser.Requested);
        Assert.Equal(7, chooser.NumRuns);
        Assert.Contains(log, l => l.Contains("Unknown behavior 255"));       // "Unknown behavior %s" (0x0060AD00 area)
        chooser.HandleMessage(0x0094, new byte[] { (byte)BehaviorID.Wait, 1, 0, 0, 0 });
        Assert.Contains(log, l => l.Contains("selecting behavior name 'Wait'"));
    }

    /// <summary>
    /// M8-013 (A4): the executable-type message calls <c>FindBehaviorByExecutableType(payload[0])</c> (0x0060AA7C), stores numRuns on a hit or a
    /// miss (0x0060AAC8) and selects null on a miss. The finder's body is not in the inventory, so an unset finder throws rather than answers.
    /// </summary>
    [Fact]
    public void TheExecutableTypeMessageUsesItsFinderAndStoresNumRunsOnAMiss()
    {
        var chooser = Chooser(out _, out var ack, out var log);
        chooser.HandleMessage(0x0093, new byte[] { 4, 1, 0, 0, 0 });        // no finder: MISSING once, skipped
        chooser.HandleMessage(0x0093, new byte[] { 4, 1, 0, 0, 0 });
        Assert.Single(log, l => l.StartsWith("MISSING"));
        Assert.Equal(-1, chooser.NumRuns);
        log.Clear();

        var asked = new List<byte>();
        chooser.FindBehaviorByExecutableType = t => { asked.Add(t); return t == 4 ? ack : null; };
        chooser.HandleMessage(0x0093, new byte[] { 4, 1, 0, 0, 0 });
        Assert.Same(ack, chooser.Requested);
        Assert.Equal(1, chooser.NumRuns);

        chooser.HandleMessage(0x0093, new byte[] { 9, 0xFE, 0xFF, 0xFF, 0xFF });
        Assert.Null(chooser.Requested);
        Assert.Equal(-2, chooser.NumRuns);
        Assert.Equal(new byte[] { 4, 9 }, asked);
        // 0x0060ACCC / 0x0060ACF8 (strings in the .so): the hit and miss lines
        Assert.Contains(log, l => l.Contains("selecting behavior 'AcknowledgeFace' exec type '4'"));
        Assert.Contains(log, l => l.Contains("ExecuteBehaviorByExecutableType.NoBehavior") && l.Contains("No behavior for exec type 9"));
    }

    /// <summary>
    /// M8-013 (A4, pre-extraction section 7, 0x0060ABDC..0x0060AC24; helper 0x0060AE84..0x0060AEEA): when the selected pointer differs from +0x2C the
    /// chooser calls SetProcessEnabled(old, false) then SetProcessEnabled(new, true), filed under "SelectionBSRunnableChooser", and only for a
    /// non-null behaviour with a process field. Re-selecting the same behaviour changes nothing.
    /// </summary>
    [Fact]
    public void TheHandoffEnablesTheNewProcessAndDisablesTheOldOnlyWhenThePointerDiffers()
    {
        var a = new WithProcess("AcknowledgeFace"); var b = new WithProcess("Wait"); var plain = new Fake("Hiccup");
        var chooser = new SelectionChooser(new Dictionary<string, IBehavior> { ["AcknowledgeFace"] = a, ["Wait"] = b, ["Hiccup"] = plain });
        chooser.HasProcessField = x => x is WithProcess;
        var calls = new List<(string Id, string Key, bool On)>();
        chooser.SetAnalyzerEnabled = (x, key, on) => calls.Add((x.Id, key, on));

        chooser.HandleMessage(0x0094, new byte[] { (byte)BehaviorID.AcknowledgeFace, 1, 0, 0, 0 });
        Assert.Equal(new[] { ("AcknowledgeFace", "SelectionBSRunnableChooser", true) }, calls);   // old is null: no false call

        calls.Clear();
        chooser.HandleMessage(0x0094, new byte[] { (byte)BehaviorID.AcknowledgeFace, 2, 0, 0, 0 });
        Assert.Empty(calls);                                                                     // same pointer: no analyzer change
        Assert.Equal(2, chooser.NumRuns);                                                        // but numRuns is still stored

        chooser.HandleMessage(0x0094, new byte[] { (byte)BehaviorID.Wait, 1, 0, 0, 0 });
        Assert.Equal(new[] { ("AcknowledgeFace", "SelectionBSRunnableChooser", false), ("Wait", "SelectionBSRunnableChooser", true) }, calls);

        calls.Clear();
        chooser.HandleMessage(0x0094, new byte[] { (byte)BehaviorID.Hiccup, 1, 0, 0, 0 });       // no process field: only the old one is disabled
        Assert.Equal(new[] { ("Wait", "SelectionBSRunnableChooser", false) }, calls);
    }

    /// <summary>
    /// M8-013 (A4, pre-extraction section 7, 0x0060AB28..0x0060AB7A): a tag other than 0x93/0x94 logs
    /// SelectionBSRunnableChooser.HandleMessage.UnknownTag and selects null.
    /// </summary>
    [Fact]
    public void AnUnknownTagLogsAndSelectsNull()
    {
        var chooser = Chooser(out _, out var ack, out var log);
        chooser.RequestBehavior(ack, 4);
        chooser.HandleMessage(0x0095, ReadOnlyMemory<byte>.Empty);
        Assert.Contains(log, l => l.Contains("SelectionBSRunnableChooser.HandleMessage.UnknownTag") && l.Contains("got a tag we didn't subscribe to"));
        Assert.Null(chooser.Requested);
    }

    private sealed class FakeChooserInterface : IChooserExternalInterface
    {
        public readonly List<(ushort Tag, SelectionChooser Chooser)> Subscribed = new();
        public void SubscribeChooserHandler(ushort tag, SelectionChooser chooser) => Subscribed.Add((tag, chooser));
    }

    /// <summary>
    /// M8-013 (A4; 0x0060A87A..0x0060A938): the constructor subscribes tag 0x94 (ID, 0x0060A898..0x0060A8B4) and then tag 0x93 (executable type,
    /// 0x0060A91C..0x0060A938) only when the robot has an external interface; with none it subscribes nothing yet still resolves Wait
    /// (0x0060A984..0x0060A99E).
    /// </summary>
    [Fact]
    public void TheSubscriptionsAreMadeOnlyWhenThereIsAnExternalInterface()
    {
        var wait = new Fake("Wait");
        var bound = new Dictionary<string, IBehavior> { ["Wait"] = wait };
        var external = new FakeChooserInterface();
        var with = new SelectionChooser(bound, external);
        Assert.Equal(new ushort[] { 0x94, 0x93 }, external.Subscribed.Select(s => s.Tag).ToArray());
        Assert.All(external.Subscribed, s => Assert.Same(with, s.Chooser));

        var without = new SelectionChooser(bound);
        Assert.Same(wait, without.Wait);
        Assert.Equal(2, external.Subscribed.Count);
    }

    // ------------------------------------------------------------------ M8-014

    private static AIWhiteboard Whiteboard(Func<double>? clock = null) =>
        new(new BlockWorld(() => Array.Empty<(uint, ObjectType)>()), clock ?? (() => 0));

    private static Pose3d At(double x, double y, double z) => new(Mat3.Identity, new Vec3(x, y, z));

    private static AIWhiteboardHandlers Handlers(AIWhiteboard wb, Func<double>? now = null)
    {
        var h = new AIWhiteboardHandlers(wb, now ?? (() => 0))
        {
            TiltRadiansOf = _ => 0f,
            FindLocatedClosestMatchingType = (_, _, _, _, _) => false,
            RobotPose = () => Pose3d.Identity,
        };
        return h;
    }

    /// <summary>M8-014 (A4 float bit patterns): 2500 0x451C4000; 10 degrees 0x3E32B8C2; 50.0 0x42480000; pi 0x40490FDB; 35.0 0x420C0000; 1e-5 0x3727C5AC; 30.0; -0.5; -1.0.</summary>
    [Fact]
    public void TheWhiteboardConstantsAreTheEnginesBitPatterns()
    {
        Assert.Equal(0x451C4000, BitConverter.SingleToInt32Bits(AIWhiteboardHandlers.RemoveDistanceSq));
        Assert.Equal(0x3E32B8C2, BitConverter.SingleToInt32Bits(AIWhiteboardHandlers.TiltLimitRad));
        Assert.Equal(0x42480000, BitConverter.SingleToInt32Bits(AIWhiteboardHandlers.WorldMatchAxisMm));
        Assert.Equal(0x40490FDB, BitConverter.SingleToInt32Bits(AIWhiteboardHandlers.WorldMatchAngleRad));
        Assert.Equal(0x41F00000, BitConverter.SingleToInt32Bits(AIWhiteboardHandlers.MaxRelativeZ));
        Assert.Equal(30.0f, AIWhiteboardHandlers.MaxRelativeZ);
        Assert.Equal(0x420C0000, BitConverter.SingleToInt32Bits(AIWhiteboardBeaconRenderer.CenterZOffset));
        Assert.Equal(0x3727C5AC, BitConverter.SingleToInt32Bits(AIWhiteboardBeaconRenderer.FailureTimeEpsilon));
        Assert.Equal(-0.5f, AIWhiteboardBeaconRenderer.SecondRadiusStep);
        Assert.Equal(-1.0f, AIWhiteboardBeaconRenderer.ThirdRadiusStep);
        Assert.Equal(10, AIWhiteboardHandlers.MaxPossibleObjects);
    }

    /// <summary>
    /// M8-014 (A4; pre-extraction section 6, tag 69, 0x0056c4d2..0x0056c51a): the tilt is compared through <c>Anki::operator&lt;(Radians, Radians)</c>, not a
    /// raw float compare. That operator (O1, 0x84CC90) is false when the two are within 1e-5, so a tilt 5e-6 below the limit is NOT below it, and one
    /// 1e-4 below is.
    /// </summary>
    [Fact]
    public void TheTiltTestGoesThroughTheRadiansComparison()
    {
        var wb = Whiteboard();
        var h = Handlers(wb);
        float limit = F(0x3E32B8C2);

        h.TiltRadiansOf = _ => limit - 5e-6f;                       // a raw compare would accept this
        h.HandleRobotObservedPossibleObject(At(0, 0, 0), ObjectType.Block_LIGHTCUBE1);
        Assert.Empty(h.PossibleObjects);

        h.TiltRadiansOf = _ => limit;
        h.HandleRobotObservedPossibleObject(At(0, 0, 0), ObjectType.Block_LIGHTCUBE1);
        Assert.Empty(h.PossibleObjects);

        h.TiltRadiansOf = _ => limit - 1e-4f;
        h.HandleRobotObservedPossibleObject(At(0, 0, 0), ObjectType.Block_LIGHTCUBE1);
        Assert.Single(h.PossibleObjects);
    }

    /// <summary>
    /// M8-014 (round 2 verifier; GetRotatedParentAxis&lt;'Z'&gt; 0x5507a0..0x550854, tilt 0x0056c4d2..0x0056c4f2): the vector is matrix row 2, (R[2,0], R[2,1], R[2,2]);
    /// the signed component of largest magnitude is chosen (s = v0; |v1| &gt; |v0| picks v1; |v2| &gt; |s| picks v2, strict), then tilt = acosf(|s|).
    /// Expected values are worked from that sequence by hand, not from the code.
    /// </summary>
    [Fact]
    public void TheDefaultTiltFollowsTheEnginesLargestComponentSelection()
    {
        static Pose3d Row2(double a, double b, double c) =>
            new(new Mat3(1, 0, 0, 0, 1, 0, a, b, c), Vec3.Zero);   // only row 2 is read

        // identity: row 2 = (0,0,1): s = 0; |0| > |0| no; |1| > |0| yes -> s = 1; acos(1) = 0
        Assert.Equal(0f, AIWhiteboardHandlers.RotatedParentZTilt(Pose3d.Identity));
        // counter-example: pi/2 about Y gives row 2 = (-1, 0, 0): s = -1; nothing is larger; acos(|-1|) = 0 (the R22-only formula gave pi/2)
        Assert.Equal(0f, AIWhiteboardHandlers.RotatedParentZTilt(new Pose3d(Mat3.AboutY(Math.PI / 2), Vec3.Zero)), 3);
        Assert.Equal(0f, AIWhiteboardHandlers.RotatedParentZTilt(Row2(-1, 0, 0)));
        // R22 dominant: (0.6, 0, -0.8): s = 0.6; |0| > 0.6 no; |-0.8| > 0.6 yes -> s = -0.8; acos(0.8) = 0.6435011
        Assert.Equal(0.6435011f, AIWhiteboardHandlers.RotatedParentZTilt(Row2(0.6, 0, -0.8)), 5);
        // v1 dominant: (0.3, -0.9, 0.2): |-0.9| > 0.3 -> s = -0.9; |0.2| > 0.9 no; acos(0.9) = 0.4510268
        Assert.Equal(0.4510268f, AIWhiteboardHandlers.RotatedParentZTilt(Row2(0.3, -0.9, 0.2)), 5);
        // ties keep the earlier component (strict >): (0.5, -0.5, 0.5) -> s = 0.5 throughout; acos(0.5) = 1.0471976
        Assert.Equal(1.0471976f, AIWhiteboardHandlers.RotatedParentZTilt(Row2(0.5, -0.5, 0.5)), 5);
        // a tie in sign: (-0.5, 0.5, 0.5): s stays v0 = -0.5, |s| = 0.5; the tilt is the same, only |s| is used
        Assert.Equal(1.0471976f, AIWhiteboardHandlers.RotatedParentZTilt(Row2(-0.5, 0.5, 0.5)), 5);
        // the property's default is that function
        var h = new AIWhiteboardHandlers(Whiteboard(), () => 0);
        Assert.Equal(AIWhiteboardHandlers.RotatedParentZTilt(Row2(0.6, 0, -0.8)), h.TiltRadiansOf(Row2(0.6, 0, -0.8)));
    }

    [Fact]
    public void AnUnsetRobotPoseReportsMissingOnceAndSkips()
    {
        var h = Handlers(Whiteboard());
        var log = new List<string>();
        h.Log += log.Add;
        h.RobotPose = null;
        h.HandleRobotObservedPossibleObject(At(0, 0, 0), ObjectType.Block_LIGHTCUBE1);
        h.HandleRobotObservedPossibleObject(At(9000, 0, 0), ObjectType.Block_LIGHTCUBE1);
        Assert.Single(log, l => l.StartsWith("MISSING"));
        Assert.Empty(h.PossibleObjects);
    }

    /// <summary>M8-014 (A4, 0x0056c54a..0x0056c576): a pose that cannot be expressed relative to the robot stops (logged); its robot-relative Z must be at most 30.0.</summary>
    [Fact]
    public void ThePossibleObjectMustBeAtMostThirtyAboveTheRobot()
    {
        var h = Handlers(Whiteboard());
        var log = new List<string>();
        h.Log += log.Add;
        h.RobotPose = () => At(0, 0, 10);                           // the robot's pose: z = 10

        h.HandleRobotObservedPossibleObject(At(1000, 0, 40), ObjectType.Block_LIGHTCUBE1);      // relative z = 30.0 exactly: accepted
        Assert.Single(h.PossibleObjects);
        h.HandleRobotObservedPossibleObject(At(2000, 0, 40.5), ObjectType.Block_LIGHTCUBE1);    // relative z = 30.5: refused
        Assert.Single(h.PossibleObjects);

        h.RobotPose = () => null;                                   // the failed relative-pose conversion logs and stops
        h.HandleRobotObservedPossibleObject(At(3000, 0, 0), ObjectType.Block_LIGHTCUBE1);
        Assert.Single(h.PossibleObjects);
        Assert.Single(log);
    }

    /// <summary>
    /// M8-014 (A4, 0x0056c588..0x0056c61a): BlockWorld::FindLocatedClosestMatchingTypeHelper is asked with per-axis thresholds 50.0 (0x42480000) and pi
    /// (0x40490FDB); the candidate is retained only when none is found.
    /// </summary>
    [Fact]
    public void ThePossibleObjectIsRetainedOnlyWhenTheWorldHasNoMatch()
    {
        var h = Handlers(Whiteboard());
        (ObjectType Type, Vec3 Axis, float Angle)? asked = null;
        bool found = true;
        BlockWorldFilter? filter = null;
        h.FindLocatedClosestMatchingType = (t, _, axis, angle, f) => { asked = (t, axis, angle); filter = f; return found; };

        h.HandleRobotObservedPossibleObject(At(0, 0, 0), ObjectType.Block_LIGHTCUBE2);
        Assert.Empty(h.PossibleObjects);
        Assert.Equal(ObjectType.Block_LIGHTCUBE2, asked!.Value.Type);
        Assert.Equal(new Vec3(50, 50, 50), asked.Value.Axis);
        Assert.Equal(0x40490FDB, BitConverter.SingleToInt32Bits(asked.Value.Angle));
        Assert.NotNull(filter);                                     // the zero-initialised BlockWorldFilter (0x0056c5ac..0x0056c606)

        found = false;
        h.HandleRobotObservedPossibleObject(At(0, 0, 0), ObjectType.Block_LIGHTCUBE2);
        Assert.Single(h.PossibleObjects);
        var log = new List<string>();
        h.Log += log.Add;
        h.FindLocatedClosestMatchingType = null;                    // unset: MISSING once, skipped, no throw
        h.HandleRobotObservedPossibleObject(At(5000, 0, 0), ObjectType.Block_LIGHTCUBE2);
        h.HandleRobotObservedPossibleObject(At(6000, 0, 0), ObjectType.Block_LIGHTCUBE2);
        Assert.Single(log, l => l.StartsWith("MISSING"));
        Assert.Single(h.PossibleObjects);
    }

    /// <summary>M8-014 (A4, 0x0056c676..0x0056c692): the list is capped at ten; a full list pops its front before appending; the visualisation is redrawn.</summary>
    [Fact]
    public void ThePossibleObjectListIsCappedAtTenAndPopsTheOldest()
    {
        var h = Handlers(Whiteboard());
        int redraws = 0;
        h.PossibleObjectsRedrawn += () => redraws++;
        for (int i = 0; i < 12; i++) h.HandleRobotObservedPossibleObject(At(i * 1000.0, 0, 0), ObjectType.Block_LIGHTCUBE1);
        Assert.Equal(10, h.PossibleObjects.Count);
        Assert.Equal(2000.0, h.PossibleObjects[0].Pose.Translation.X);       // entries 0 and 1 were popped
        Assert.Equal(11000.0, h.PossibleObjects[9].Pose.Translation.X);
        Assert.Equal(12, redraws);
    }

    /// <summary>
    /// M8-014 (A4, tag 68, 0x0056c448..0x0056ae12): possible-object entries of the same type within squared distance 2500 (0x451C4000) of the observed pose
    /// are removed, then the visualisation is redrawn. Other types and farther entries stay. Pre-extraction: "at most 2500", so exactly 50 mm goes.
    /// </summary>
    [Fact]
    public void AnObservedObjectRemovesNearbyPossibleObjectsOfTheSameType()
    {
        var h = Handlers(Whiteboard());
        h.HandleRobotObservedPossibleObject(At(50, 0, 0), ObjectType.Block_LIGHTCUBE1);    // exactly 50 mm from the observation: removed
        h.HandleRobotObservedPossibleObject(At(-51, 0, 0), ObjectType.Block_LIGHTCUBE1);   // 51 mm: stays
        h.HandleRobotObservedPossibleObject(At(0, 30, 0), ObjectType.Block_LIGHTCUBE2);    // near but another type: stays
        h.HandleRobotObservedPossibleObject(At(0, 42, 30), ObjectType.Block_LIGHTCUBE1);   // 3-D: sqrt(1764 + 900) = 51.62 mm, stays
        Assert.Equal(4, h.PossibleObjects.Count);

        int redraws = 0;
        h.PossibleObjectsRedrawn += () => redraws++;
        h.HandleRobotObservedObject(At(0, 0, 0), ObjectType.Block_LIGHTCUBE1);
        Assert.Equal(1, redraws);
        Assert.DoesNotContain(h.PossibleObjects, p => p.Pose.Translation.X == 50);
        Assert.Equal(3, h.PossibleObjects.Count);
    }

    /// <summary>
    /// M8-014 (A4, tag 53, 0x0056cce4..0x0056ccf6): the first payload byte; non-zero returns, zero (OnTreads, ordinal 0) stores
    /// BaseStationTimer::GetCurrentTimeInSeconds() at whiteboard +0x48.
    /// </summary>
    [Fact]
    public void TheOffTreadsHandlerStoresTheTimeOnlyForOnTreads()
    {
        var wb = Whiteboard();
        double now = 12345.678;                                     // not representable as a float
        var h = Handlers(wb, () => now);
        h.HandleRobotOffTreadsStateChanged(2);
        Assert.Equal(0.0, wb.OffTreadsStateChangedAtSec);
        h.HandleRobotOffTreadsStateChanged(0);
        // GetCurrentTimeInSeconds returns a float and the store is `str r0,[r4,#0x48]` (0x0056ccee..0x0056ccf6): 12345.678f = 0x4640E6B6
        Assert.Equal(0x4640E6B6, BitConverter.SingleToInt32Bits((float)wb.OffTreadsStateChangedAtSec));
        Assert.Equal((double)BitConverter.Int32BitsToSingle(0x4640E6B6), wb.OffTreadsStateChangedAtSec);
        Assert.NotEqual(now, wb.OffTreadsStateChangedAtSec);
    }

    private sealed class FakeViz : IBeaconVizSink
    {
        public readonly List<string> Calls = new();
        public readonly List<(float X, float Y, float Z, float R, BeaconColor Color, bool Connect, int Segments, float Final)> Circles = new();
        public void EraseSegments(string name) => Calls.Add("erase " + name);
        public void DrawXYCircleAsSegments(float x, float y, float z, float r, BeaconColor color, bool connect, int segments, float fin)
        {
            Calls.Add("circle");
            Circles.Add((x, y, z, r, color, connect, segments, fin));
        }
    }

    /// <summary>
    /// M8-014 (A4, UpdateBeaconRender 0x0056aa3c..0x0056abff): erase the "AIWhiteboard.UpdateBeaconRender" segments, then per beacon three circles at
    /// the translation with z + 35.0, radii R, R - 0.5, R - 1.0, 8 segments, connectLastToFirst false, final argument 0.0, DARKGREEN when
    /// abs(+0x10) &lt; 1e-5, else ORANGE. AddBeacon calls UpdateBeaconRender (0x0056c3de), here through the whiteboard's render seam.
    /// </summary>
    [Fact]
    public void AddingABeaconEmitsTheEraseAndThreeCircles()
    {
        var wb = Whiteboard();
        var viz = new FakeViz();
        float failure = 0f;
        var renderer = new AIWhiteboardBeaconRenderer(viz)
        {
            ResolveBeaconPose = b => b.Pose,
            LastFailureTimeOf = _ => failure,
        };
        renderer.Attach(wb);

        wb.AddBeacon(At(10, 20, 5), 100);
        Assert.Equal(new[] { "erase AIWhiteboard.UpdateBeaconRender", "circle", "circle", "circle" }, viz.Calls);
        Assert.Equal(new[] { 100f, 99.5f, 99f }, viz.Circles.Select(c => c.R).ToArray());
        Assert.All(viz.Circles, c =>
        {
            Assert.Equal((10f, 20f, 40f), (c.X, c.Y, c.Z));
            Assert.Equal(BeaconColor.DarkGreen, c.Color);
            Assert.False(c.Connect);
            Assert.Equal(8, c.Segments);
            Assert.Equal(0f, c.Final);
        });

        viz.Calls.Clear(); viz.Circles.Clear();
        failure = F(0x3727C5AC);                                    // abs(+0x10) < 1e-5 is strict: 1e-5 itself is ORANGE
        wb.AddBeacon(At(0, 0, 0), 50);
        Assert.Equal(6, viz.Circles.Count);                         // erase once, then both beacons
        Assert.All(viz.Circles, c => Assert.Equal(BeaconColor.Orange, c.Color));

        viz.Calls.Clear(); viz.Circles.Clear();
        failure = -F(0x3727C5AC) * 0.5f;                            // the test is on abs()
        renderer.Render(wb.Beacons);
        Assert.All(viz.Circles, c => Assert.Equal(BeaconColor.DarkGreen, c.Color));
    }

    /// <summary>M8-014: the beacon pose helper (0x004DF628) is not supplied and AIBeacon has no +0x10 time yet: an unset seam reports MISSING once, and an empty render only erases.</summary>
    [Fact]
    public void TheBeaconRenderThrowsRatherThanGuessTheUnknownPoseBase()
    {
        var viz = new FakeViz();
        var renderer = new AIWhiteboardBeaconRenderer(viz);
        renderer.Render(Array.Empty<AIBeacon>());
        Assert.Equal(new[] { "erase AIWhiteboard.UpdateBeaconRender" }, viz.Calls);
        var log = new List<string>();
        renderer.Log += log.Add;
        viz.Calls.Clear();
        renderer.Render(new[] { new AIBeacon(Pose3d.Identity, 10) });          // unset: MISSING once, no circles, no throw
        renderer.Render(new[] { new AIBeacon(Pose3d.Identity, 10) });
        Assert.Single(log, l => l.StartsWith("MISSING"));
        Assert.DoesNotContain("circle", viz.Calls);
        renderer.ResolveBeaconPose = b => b.Pose;
        renderer.Render(new[] { new AIBeacon(Pose3d.Identity, 10) });
        Assert.Equal(2, log.Count);
        Assert.DoesNotContain("circle", viz.Calls);
    }

    // ------------------------------------------------------------------ M8-004

    private sealed class ScoreProbe : SteppedBehavior
    {
        public ScoreProbe() : base("probe", "Probe") { }
        protected override bool KeepsRunningWithoutAction => true;
        protected override bool IsRunnableInternal(BehaviorContext context) => true;
        public override bool IsRunnable(BehaviorContext context) => true;
        protected override void OnStart() { }
        protected override void OnUpdate() { }
        protected override void OnStop(BehaviorStopReason reason) { }
    }

    /// <summary>
    /// M8-004: <c>IBehavior::IBehavior</c> 0x005bbb74 writes zero to the flat score +0x100 (0x005bbd1c mov.w r8,#0; 0x005bbd28 strd r8,r8,[r4,#0x100]) and
    /// <c>EvaluateScoreInternal</c> 0x005beec2 returns +0x100 when the mood-scorer vector at +0xdc is empty (0x005beed4). A behaviour built in code carries that
    /// zero, so the stack's own ranking, which skips a score of zero or less, picks none of them.
    /// </summary>
    [Fact]
    public void ABehaviourBuiltInCodeScoresZeroLikeTheEngineDefault()
    {
        var ctx = Ctx();
        var probe = new ScoreProbe();
        Assert.Equal(0.0, probe.Score);
        Assert.Equal(0.0, probe.EvaluateScore(ctx));

        Assert.Equal(0.0, new PlayAnimBehavior("PlayAnim0", "PlayAnim", new[] { AnimationTrigger.Hiccup }).Score);
        Assert.Equal(0.0, new PlayArbitraryAnimBehavior().Score);
        Assert.Equal(0.0, new ReactBehavior("r", "React", ReactionTrigger.CliffDetected, _ => true).Score);
        Assert.Equal(0.0, new SingingBehavior("Singing_Bingo", "Cozmo_Sings_100Bpm", "Cozmo_Sings_Bingo").Score);

        using var m = new BehaviorManager(ctx, null);
        m.Add(probe);
        var decision = m.ChooseAndSwitch(0);
        Assert.Null(decision.Chosen);                                // nothing is picked by an invented score
    }

    /// <summary>M8-004 (config path, evidence text): a scored behaviour's flatScore comes only from the config key (ReadFromScoredJson 0x005bc4e0); absent it is zero.</summary>
    [Fact]
    public void AConfigWithoutAFlatScoreIsZero()
    {
        using var none = JsonDocument.Parse("{\"behaviorID\":\"x\"}");
        Assert.Equal(0.0, ScoredBehaviorEntry.FromJson(none.RootElement).FlatScore);
        using var some = JsonDocument.Parse("{\"behaviorID\":\"x\",\"scoring\":{\"flatScore\":4.5}}");
        Assert.Equal(4.5, ScoredBehaviorEntry.FromJson(some.RootElement).FlatScore);
    }
}
