using Cozmo.Robot.Animation.Wwise;
using Cozmo.Robot.Behavior;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class MidiRuntimeCheckedRowsTests
{
    private static WwiseParamBundle Props(params (byte Id, uint Word)[] values)
        => new(values.Select(x => x.Id).ToArray(), values.Select(x => x.Word).ToArray());
    private static (WwiseMidiRuntime Runtime, WwiseMidiClipContext Clip, WwiseRoutingNode Target) Rig(int category = 5)
    {
        var registry = new WwiseRuntimeRegistry();
        var target = new WwiseRoutingNode { Id = 110896138, Category44 = category };
        registry.Insert(target);
        var track = new WwiseMidiMusicNode { Track = true, Flags = 1, Properties3C = Props((0x38, target.Id)) };
        var runtime = new WwiseMidiRuntime(new WwiseRng(1));
        var clip = new WwiseMidiClipContext(runtime, track, new object()) { GameObject8 = new object(), EndF8 = uint.MaxValue };
        Assert.Equal(1, clip.ResolveTarget(registry));
        return (runtime, clip, target);
    }

    // E1/E2b/E3: signed bias before VCVT, zero and unordered use the negative bias.
    [Theory]
    [InlineData(0.5f, 1)] [InlineData(-0.5f, -1)]
    [InlineData(1.5f, 2)] [InlineData(-1.5f, -2)]
    [InlineData(0f, 0)] [InlineData(float.NaN, 0)]
    [InlineData(float.PositiveInfinity, int.MaxValue)] [InlineData(float.NegativeInfinity, int.MinValue)]
    public void PositionAndRescaleUseNativeBias(float value, int expected)
    {
        Assert.Equal(expected, WwiseMidiTiming.Position(value, 1000));
        Assert.Equal(expected, WwiseMidiTiming.Round(value));
    }

    [Fact]
    public void LiveSongEntryNamesTheMissingOwnerAndParserWithoutFeedingPairedNotesIntoNativeRuntime()
    {
        SteppedBehavior.ResetMissingForTests(); var missing = new List<string>();
        void On(string text) => missing.Add(text);
        SteppedBehavior.MissingReported += On;
        try
        {
            var renderer = new WwiseSongRenderer(WwiseSoundLibrary.Load(), _ => null, new WwiseRng(1));
            var clip = new WwiseMusicClipPlan(1, 2, WwiseMusicTrackNode.MidiPluginId, new(0, 2, 0, 0, 0, 1));
            renderer.BuildVoices(new(1) { Segments = new[] { new WwiseMusicSegmentPlan(3, 1, default, 80, new[] { clip }) } });
            Assert.Contains(missing, x => x.Contains("0x00A3D234") && x.Contains("M9-005"));
            Assert.Contains(missing, x => x.Contains("M9-024") && x.Contains("M9-030"));
            Assert.Empty(renderer.MidiRuntime.Pending); Assert.Empty(renderer.MidiRuntime.Clips);
        }
        finally { SteppedBehavior.MissingReported -= On; }
    }

    [Fact]
    public void StepAndRescalePreserveF32AndSignedOffsets()
    {
        Assert.Equal(11, WwiseMidiTiming.Step(7, 1.5f));
        Assert.Equal(-4, WwiseMidiTiming.Rescale(-7, 2f));
        Assert.Equal(4, WwiseMidiTiming.Rescale(7, 2f));
        Assert.Equal(0, WwiseMidiTiming.Step(744, float.NaN));
    }

    [Fact]
    public void UnmatchedNoteOffIsDroppedAndMatchingOffInheritsFirstVelocityAndSharedState()
    {
        var (runtime, clip, _) = Rig();
        Assert.Null(runtime.Admit(clip, new(0x80, 2, 60, 7), 3));
        Assert.Empty(runtime.Targets[0].Holders);
        var first = runtime.Admit(clip, new(0x90, 2, 60, 101), 1)!;
        var second = runtime.Admit(clip, new(0x90, 2, 60, 33), 2)!;
        var off = runtime.Admit(clip, new(0x90, 2, 60, 0), 3)!;
        Assert.Equal(new WwiseMidiEvent(0x80, 2, 60, 101), off.Event14);
        Assert.Same(first.NoteState8, off.NoteState8);
        Assert.Equal(2, first.NoteState8!.References);
        Assert.Equal(4, first.Flags1C & 4);
        Assert.Equal(0, second.Flags1C & 4);
        Assert.Null(runtime.Admit(clip, new(0x80, 3, 60, 99), 4));
        var secondOff = runtime.Admit(clip, new(0x80, 2, 60, 7), 4)!;
        Assert.Equal((byte)33, secondOff.Event14.Velocity);
    }

    [Fact]
    public void HoldersUseSignedStableOrderingWhileQueueUsesUnsignedOrderingAndFlagPriority()
    {
        var (runtime, clip, _) = Rig();
        var last = runtime.Admit(clip, new(0xB0, 1, 1, 1), int.MaxValue)!;
        var first = runtime.Admit(clip, new(0xB0, 1, 2, 1), -1)!;
        var equal = runtime.Admit(clip, new(0xB0, 1, 3, 1), -1)!;
        Assert.Equal(new[] { first, equal, last }, runtime.Targets[0].Holders);
        runtime.Enqueue(clip, new(0xB0, 1, 1, 1), uint.MaxValue);
        runtime.Enqueue(clip, new(0xB0, 1, 2, 1), 0, bit6: true);
        runtime.Enqueue(clip, new(0xB0, 1, 3, 1), 0);
        runtime.Enqueue(clip, new(0xB0, 1, 4, 1), 0, bit7: true);
        Assert.Equal(new byte[] { 4, 3, 2, 1 }, runtime.Pending.Select(x => x.Event.Key));
    }

    [Fact]
    public void DrainPopsBeforeOwnerAndProcessesEveryOffsetBeforeHeldRetirement()
    {
        var (runtime, clip, _) = Rig(); int calls = 0;
        runtime.OwnerVirtual4 = entry => { Assert.Empty(runtime.Pending); calls++; };
        runtime.Enqueue(clip, new(0xB0, 1, 1, 1), uint.MaxValue, bit7: true);
        runtime.Drain(1, 1);
        Assert.Equal(1, calls);
        Assert.Empty(runtime.Pending);
        Assert.Empty(runtime.Targets[0].Holders);
        Assert.Equal(1, clip.References3C);
    }

    [Fact]
    public void FrameDrivesClipsRescalesOffsetsAndSkipsParserAtZeroElapsed()
    {
        var (runtime, clip, _) = Rig();
        runtime.ContextFlags36 = 1; runtime.RendererVirtual44 = _ => { };
        runtime.Clips.Add(clip); int parses = 0;
        clip.ParserA3F6E0 = elapsed =>
        {
            Assert.Equal(2f, elapsed); parses++;
            return new[] { new WwiseParsedMidiEvent(new(0x90, 1, 60, 90), 3f, 0) };
        };
        runtime.Frame(1, 2f, 1000, 1);
        var holder = Assert.Single(runtime.Targets[0].Holders);
        // round(3 / 2)=2, then countdown max(0,2-step2)=0; note-on is submitted independently of due gate.
        Assert.Equal(0, holder.Offset18);
        Assert.Equal(1, holder.Flags1C & 1);
        Assert.Equal(2UL, runtime.Clock);
        clip.OffsetF4 = 2;
        runtime.Frame(1, 2f, 1000, 1);
        Assert.Equal(1, parses);
        runtime.ContextFlags36 = 0x11;
        runtime.Frame(1, 2f, 1000, 1);
        Assert.Equal(4UL, runtime.Clock);
    }

    [Fact]
    public void FiniteClipEndFiltersOnPositionBeforeSavedOffsetAndReleasesExtraReferenceOnlyOnce()
    {
        var (runtime, clip, _) = Rig();
        runtime.ContextFlags36 = 1; runtime.Clips.Add(clip);
        clip.EndF8 = 3; clip.OffsetF4 = 1; clip.References3C = 2;
        clip.ParserA3F6E0 = _ => new[]
        {
            new WwiseParsedMidiEvent(new(0x90, 1, 60, 91), 2f, 0),
            new WwiseParsedMidiEvent(new(0x90, 1, 61, 92), 3f, 0)
        };
        runtime.Frame(2, 1f, 1000, 1);
        Assert.Equal((byte)60, Assert.Single(runtime.Targets[0].Holders).Event14.Key);
        Assert.Equal(0u, clip.EndF8); Assert.Equal(4, clip.FlagsFC & 4); Assert.Equal(1, clip.References3C);
        runtime.Frame(0, 1f, 1000, 1);
        Assert.Equal(1, clip.References3C);
    }

    [Fact]
    public void FactoryPublishesBeforeInitializationAndRejectsNullOwnerOrAllocationFailure()
    {
        var (runtime, clip, _) = Rig();
        Assert.Equal(0x1F, runtime.CreateSource(16, null, clip.Track78, clip.Source7C, _ => 1, out var result));
        Assert.Null(result);
        Assert.Equal(1, runtime.CreateSource(16, runtime, clip.Track78, clip.Source7C, c =>
        { Assert.Same(runtime, c.Owner74); Assert.Same(clip.Track78, c.Track78); return 1; }, out result));
        Assert.NotNull(result);
        Assert.Equal(2, runtime.CreateSource(16, runtime, clip.Track78, clip.Source7C, _ => 2, out result));
        Assert.Null(result);
        runtime.Allocate = _ => false;
        Assert.Equal(2, runtime.CreateSource(16, runtime, clip.Track78, clip.Source7C, _ => throw new Exception(), out result));
    }

    [Fact]
    public void RegisteredFrameCallbackUsesMaskEightCookieZeroAndSnapshotsNextContext()
    {
        var (runtime, clip, _) = Rig(); runtime.ContextFlags36 = 1;
        var callback = new WwiseMidiFrameCallback();
        var context = new WwiseMidiCallbackContext { Renderer38 = runtime, Scale3C = 1 };
        callback.NormalContexts = context;
        runtime.RendererVirtual44 = _ => context.Next48 = null;
        var (second, _, _) = Rig(); second.ContextFlags36 = 1;
        context.Next48 = new() { Renderer38 = second, Scale3C = 2 };
        Action? registered = null;
        callback.Register((mask, cookie, action) => { Assert.Equal(8u, mask); Assert.Equal(0u, cookie); registered = action; }, () => (1, 1000));
        registered!();
        Assert.Equal(1UL, runtime.Clock); Assert.Equal(2UL, second.Clock);
    }

    [Fact]
    public void TargetLookupHonorsTrackOverrideZeroAndAlternateTable()
    {
        var (runtime, clip, target) = Rig(); var registry = new WwiseRuntimeRegistry(); registry.Insert(target);
        clip.Track78.Parent34 = new() { Flags = 2, Properties3C = Props((0x38, target.Id)) };
        clip.Track78.Properties3C = Props((0x38, 0));
        Assert.Equal(0, clip.ResolveTarget(registry)); Assert.Null(clip.Target34);
        clip.Track78.Flags = 0;
        Assert.Equal(1, clip.ResolveTarget(registry)); Assert.Same(target, clip.Target34);
        clip.Track78.Parent34.Flags = 10;
        Assert.Equal(0, clip.ResolveTarget(registry)); Assert.True(clip.Table38);
    }

    [Fact]
    public void LayerCopiesChildRequestsContinuesAfterRejectionAndRanSeqNeverRetries()
    {
        var (runtime, clip, layer) = Rig();
        var a = new WwiseRoutingNode { Id = 1, Category44 = 3, BaseBundle3C = Props((0x2F, 0x3F800000)) };
        var b = new WwiseRoutingNode { Id = 2, Category44 = 3, BaseBundle3C = Props((0x31, 70)) };
        var c = new WwiseRoutingNode { Id = 3, Category44 = 3 };
        layer.Children5C.AddRange(new[] { a, b, c });
        var heard = new List<byte>();
        runtime.Routing.Slots(a).Play128 = request => { heard.Add(request.Event84.Key); return 1; };
        runtime.Routing.Slots(c).Play128 = request => { heard.Add(request.Event84.Key); return 1; };
        var holder = runtime.Admit(clip, new(0x90, 1, 60, 80), 0)!;
        runtime.Replay(holder);
        Assert.Equal(new byte[] { 61, 60 }, heard);
        var ranSeq = new WwiseRoutingNode { Id = 4, Category44 = 2 }; int draws = 0;
        runtime.Routing.Slots(ranSeq).SelectA0A3B4 = _ => { draws++; return (b, 7); };
        var request = new WwiseMidiPlayRequest { Holder = holder, Event84 = holder.Event14 };
        Assert.Equal(0x52, runtime.Routing.Play(ranSeq, request));
        Assert.Equal(1, draws); Assert.Equal(1, request.Depth28); Assert.Equal((ushort)7, request.History2C[0]);
    }

    [Fact]
    public void FilterMutatesParentFirstClampsAndRejectsWithoutLocalWrites()
    {
        var (runtime, clip, target) = Rig();
        var holder = runtime.Admit(clip, new(0x90, 1, 126, 1), 0)!;
        target.Parent = new() { BaseBundle3C = Props((0x2F, 0x3F800000)) };
        target.BaseBundle3C = Props((0x2F, 0x3F800000), (0x30, 0xBF800000));
        var request = new WwiseMidiPlayRequest { Holder = holder, Event84 = holder.Event14 }; bool inherit = true;
        Assert.Equal(1, runtime.Routing.Filter(target, request, ref inherit));
        Assert.False(inherit); Assert.Equal(new WwiseMidiEvent(0x90, 1, 127, 1), request.Event84);
        target.BaseBundle3C = Props((0x35, 0xFFFF));
        request.Event84 = request.Event84 with { Channel = 32 };
        Assert.Equal(0x52, runtime.Routing.Filter(target, request, ref inherit)); // ARM ASR, no &15
    }

    [Fact]
    public void DelayGatesConsumeOnlyTheDefinedFlagAndUseF64SampleConversion()
    {
        var (runtime, clip, target) = Rig();
        var holder = runtime.Admit(clip, new(0x90, 1, 60, 80), 0)!;
        runtime.Routing.SampleRate = 1000;
        target.BaseBundle3C = Props((0x3B, 0x3F000000)); // 0.5 seconds
        var request = new WwiseMidiPlayRequest { Holder = holder, Event84 = holder.Event14, Context7C = 1 };
        Assert.Equal(1, runtime.Routing.Delay(target, request)); Assert.Equal(500u, request.Offset74);
        request.Flags124 = 3;
        Assert.Equal(1, runtime.Routing.Delay(target, request)); Assert.Equal((byte)2, request.Flags124); Assert.Equal(500u, request.Offset74);
        request.Context7C = 0;
        runtime.Routing.Slots(target).Defer9EDEB8 = (_, value) => { Assert.Equal(0.5f, value); return 1; };
        Assert.Equal(3, runtime.Routing.Delay(target, request));
    }

    [Fact]
    public void NoteOffDueGateIsStrictAndHeldRetirementRequiresBothCountersZero()
    {
        var (runtime, clip, _) = Rig();
        runtime.Admit(clip, new(0x90, 1, 60, 101), 0);
        var off = runtime.Admit(clip, new(0x80, 1, 60, 0), 10)!;
        int stops = 0, retirements = 0;
        runtime.NoteOffA3E920 = _ => stops++;
        runtime.A3EB4C = _ => retirements++;
        off.NoteState8!.Count40 = 1;
        Assert.False(runtime.StepHolder(off, 0, 10, true)); Assert.Equal(0, stops);
        Assert.True(runtime.StepHolder(off, 0, 11, true)); Assert.Equal(1, stops);
        runtime.AdvanceHolders(0, 11); Assert.Single(runtime.Held); Assert.Equal(0, retirements);
        off.NoteState8.Count40 = 0; off.NoteState8.Count5C = 1;
        runtime.AdvanceHolders(0, 11); Assert.Single(runtime.Held);
        off.NoteState8.Count5C = 0;
        runtime.AdvanceHolders(0, 11); Assert.Empty(runtime.Held); Assert.Equal(1, retirements);
    }
}
