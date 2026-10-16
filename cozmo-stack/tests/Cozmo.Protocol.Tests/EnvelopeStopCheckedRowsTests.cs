using Cozmo.Robot.Animation.Wwise;
using Xunit;
namespace Cozmo.Protocol.Tests;

public sealed class EnvelopeStopCheckedRowsTests
{
    private static WwisePlayingInstance Pbi() => new WwisePlaybackBridge().CreatePbi(new(), 1, new object(), false);
    [Fact]
    public void FatalPopulationBranchSkipsOnlyThatItemAndKeepsItsPreviousStateAndOutput()
    {
        var output = new object();
        var state = new WwiseEnvelopeState { Marker44 = 2, Cursor3C = 100, Output30 = output, Deadline38 = 200 };
        new WwiseEnvelopeRuntime().SkipFatalPopulationGrowth(state);
        Assert.Same(output, state.Output30); Assert.Equal((2u, 100u, 200u), (state.Marker44, state.Cursor3C, state.Deadline38));
    }
    [Theory]
    [InlineData(0, true, true)] [InlineData(0, false, false)]
    [InlineData(1, true, true)] [InlineData(1, false, false)] [InlineData(2, true, false)]
    public void CleanupStopUsesPolicyScopeAndExactAttachmentIdentity(int scope, bool policy, bool stops)
    {
        var pbi = Pbi(); int calls = 0;
        var state = new WwiseEnvelopeState { HasModulator8 = true, Scope48 = scope, PbiC = pbi,
            ModulatorProperties = new(new byte[] { 1 }, new uint[] { policy ? 1u : 0u }) };
        state.Recipients.Add(new() { Identity = new object(), ReleaseVirtualC = () => { } });
        pbi.EnvelopeAttachments34 = new() { state };
        state.Event20 = new() { Voices2C = new() { Pbi = pbi }, ReleaseA3E27C = () => { } };
        pbi.StopVirtual0 = (voice, a, b) => { Assert.Equal(3u, state.Marker44); Assert.Equal((0, 1), (a, b)); calls++; };
        new WwiseEnvelopeRuntime().Cleanup(state);
        Assert.Equal(stops ? 1 : 0, calls); Assert.False(state.HasModulator8); Assert.Null(state.Event20); Assert.Empty(state.Recipients);
    }

    [Fact]
    public void ScopeOneSkipsAnotherStatesVoiceAndStopsBeforePendingActionsThenReleasesInReverse()
    {
        var runtime = new WwiseEnvelopeRuntime(); var order = new List<string>(); var pbi = Pbi(); var other = Pbi();
        var state = new WwiseEnvelopeState { HasModulator8 = true, Scope48 = 1 };
        state.Recipients.Add(new() { Identity = 1, ReleaseVirtualC = () => order.Add("release1") });
        state.Recipients.Add(new() { Identity = 2, ReleaseVirtualC = () => order.Add("release2") });
        pbi.EnvelopeAttachments34 = new() { state }; other.EnvelopeAttachments34 = new() { new() };
        pbi.StopVirtual0 = (_, _, _) => order.Add("stop"); other.StopVirtual0 = (_, _, _) => throw new Exception("wrong state");
        state.Event20 = new() { Voices2C = new() { Pbi = pbi, Next = new() { Pbi = other } },
            Actions48 = new() { Action = new object() }, ReleaseA3E27C = () => order.Add("event") };
        runtime.ActionMembershipA63A6C = (_, s) => ReferenceEquals(s, state);
        runtime.Cancel9AB1F4 = (_, r, kind) => { Assert.Equal(3, kind); order.Add("cancel" + r.Identity); };
        runtime.Cleanup(state);
        Assert.Equal(new[] { "stop", "cancel1", "cancel2", "release2", "release1", "event" }, order);
    }

    [Fact]
    public void NullEventFallbackAlwaysRunsNodeActionAndRetirementCleanupReversesRemovalOrder()
    {
        var runtime = new WwiseEnvelopeRuntime(); var order = new List<int>();
        WwiseEnvelopeState State(int id) { var state = new WwiseEnvelopeState { HasModulator8 = true, Scope48 = 1, References40 = 1, Marker44 = 3 };
            state.Recipients.Add(new() { Identity = id, ReleaseVirtualC = () => { } }); return state; }
        var first = State(1); var second = State(2); first.Next = second;
        runtime.ActiveHead = first; runtime.ActiveTail = second; runtime.ActiveCount = 2;
        runtime.NodeAction9F4A64 = (r, _, _, a, b) => { Assert.Equal((0, 4), (a, b)); order.Add((int)r.Identity); };
        runtime.Retire();
        Assert.Equal(new[] { 2, 1 }, order); Assert.Null(runtime.ActiveHead); Assert.Null(runtime.ActiveTail); Assert.Equal(0u, runtime.ActiveCount);
    }

    [Theory]
    [InlineData(0u, true)] [InlineData(0x02000000u, false)] [InlineData(0x04000000u, false)] [InlineData(0x06000000u, true)]
    public void PbiTransitionQueryUsesExactRawSelectorPredicate(uint selector, bool deferredFlag)
    {
        var pbi = Pbi(); pbi.Transition148 = 1; pbi.ReadTransitionSelector = _ => selector;
        int second = -1; pbi.StopVirtual0 = (_, _, b) => second = b;
        pbi.RequestEnvelopeStop();
        Assert.Equal(deferredFlag ? 0x40 : 0, pbi.Flags1BC & 0x40);
        Assert.Equal(deferredFlag ? 1 : 0, second);
    }

    [Fact]
    public void PbiZeroDurationStopClearsFadeOnlyInTheNativeBranch()
    {
        var pbi = Pbi(); pbi.Flags1BA = 8; pbi.Fade168 = 0.5f; pbi.MuteFade40 = 0.25f;
        int second = -1; pbi.StopVirtual0 = (_, _, b) => second = b;
        pbi.RequestEnvelopeStop();
        Assert.Equal(0f, pbi.Fade168); Assert.Equal(0f, pbi.MuteFade40); Assert.Equal(0, second);
        Assert.Equal(2, pbi.Flags1BD & 0x1E);
    }

    [Fact]
    public void DeadlinePreservesFiniteValueAndCompletionMarkerIsIndependentOfAmplitude()
    {
        var state = new WwiseEnvelopeState { HasModulator8 = true, Scope48 = 1, Attachments50 = 1, Deadline38 = int.MaxValue, Cursor3C = 9 };
        state.SetDeadline(uint.MaxValue); Assert.Equal(8u, state.Deadline38); Assert.Equal(2u, state.Marker44);
        state.SetDeadline(20); Assert.Equal(8u, state.Deadline38); Assert.False(state.ShouldRetire);
        state.Marker44 = 3; state.SetDeadline(30); Assert.Equal(3u, state.Marker44); Assert.True(state.ShouldRetire);
    }

    [Fact]
    public void SelectorTwoRejectsPriorStateOnNoteOnBeforeAnyReinitialization()
    {
        var runtime = new WwiseEnvelopeRuntime(); var prior = new WwiseEnvelopeState { Marker44 = 2 };
        var props = new WwiseParamBundle(new byte[] { 15 }, new uint[] { 2 });
        var input = new WwiseEnvelopeEvent(null, new(0x90, 0, 60, 100), null, null, null, 0, 1, null);
        Assert.Null(runtime.Acquire(props, input, prior, null, out bool created)); Assert.False(created); Assert.Equal(2u, prior.Marker44);
        input = input with { Midi4 = new(0x80, 0, 60, 100), Control18 = 3 };
        Assert.Same(prior, runtime.Acquire(props, input, prior, null, out created));
        Assert.Equal(1u, prior.Marker44); Assert.False(created);
    }
}
