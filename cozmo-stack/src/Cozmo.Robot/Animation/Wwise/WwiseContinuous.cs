// fidelity: M6-008

namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// Continuous RanSeq containers (M6-008). The frozen rows are M6-wwise-bank.md Appendix G §2 (2.1..2.9) and
/// Appendix H §1 (mode-4 sample-accurate chaining), plus H's headline corrections. Addresses are
/// libcozmoEngine.so 3.4.0-1204 VAs; they are citations, not call targets.
///
/// This is the runtime's continuation decision path: the loop count of a continuation item, the next item
/// chosen at the current voice's start, the transition timing and gains, and the mode-4 and mode-5
/// scheduling. It is a standalone decision module: it produces plan records and does no audio production,
/// and it is deliberately not wired into <see cref="WwisePlayback"/>, <see cref="WwiseAudioSource"/>,
/// <see cref="WwiseSongRenderer"/> or the animation scheduler. The live voice/PBI pieces the rows mark
/// RECOVERABLE_GAP (the PBI continuation internals, the action manager's delay resolution, EndOfEvent
/// latency) are left to those records and are named in M6-008's <c>unresolved</c>.
///
/// <para>
/// Unreachable in the shipped banks: every RanSeq <c>+0x91</c> byte is 0x12 or 0x1A with the ping-pong bit
/// (bit5) clear, and all 25 continuous containers are loop 0 or loop 1. So the ping-pong sequence branch,
/// the drawn loop count and the loopMin/loopMax range are source-exact but not capture-verifiable.
/// </para>
/// </summary>
public static class WwiseContinuous
{
    /// <summary>
    /// Bank bit1 in the container's flags byte maps to RanSeq +0x91 bit4 (gapA 2.6, gapD D4.2). Set on all
    /// 25 shipped continuous containers: each play allocates a fresh selection state and never saves it
    /// back. Clear selects the shared/per-object state path (0xA099BC / 0xA0729C), which is outside the
    /// frozen rows read here.
    /// </summary>
    public const byte FreshStateFlag = 0x02;

    /// <summary>Bank bit2 → +0x91 bit5 (gapA 2.6): a sequence reverses at the playlist end instead of wrapping.</summary>
    public const byte PingPongFlag = 0x04;

    /// <summary>Bank bit3 → +0x91 bit6 (gapA 2.6): the container is continuous.</summary>
    public const byte ContinuousFlag = 0x08;

    /// <summary>GapF 2.5: below this estimated length the next item is not scheduled and chains at the end.</summary>
    public const double MinimumCrossFadeLengthMs = 50.0;

    /// <summary>GapF 2.8: the mode-5 re-entry floor, the static at 0x1052438 (22 ms).</summary>
    public const double MinimumReentrySeconds = 0.022;

    /// <summary>Whether the container's flags byte asks for a fresh selection state per play (bank bit1).</summary>
    public static bool IsFreshStatePerPlay(byte containerFlags) => (containerFlags & FreshStateFlag) != 0;

    // ------------------------------------------------------------------ gapF 2.4: choosing the next item

    /// <summary>
    /// PrepareNextToPlay 0xA6A07C (gapF 2.4). It runs when the current voice's source starts, and it selects
    /// the next item <b>then</b>, not at the transition: it stores the next id, the mode (container +0x90 &amp;
    /// 0xF) and, for modes 1..3, the xfade. The xfade is
    /// <c>transition + min + rand·(max − min) + rtpcParam0F·1000</c>, clamped at 0 ms. The LCG draw is
    /// <b>skipped</b> when <c>max − min == 0</c> (0xA078E8 / 0xA07978), so the global stream is left
    /// untouched; on the shipped mode-1 sequences (min = max = 0) no draw is consumed.
    /// </summary>
    /// <param name="selector">The per-play continuation state (fresh for every shipped container).</param>
    /// <param name="mode">The container's transition mode (+0x90 &amp; 0xF).</param>
    /// <param name="transitionTimeMs">The RanSeq's transition time at +0x7C (gapA 2.6).</param>
    /// <param name="transitionMinMs">The modulation range minimum at +0x80.</param>
    /// <param name="transitionMaxMs">The modulation range maximum at +0x84.</param>
    /// <param name="rtpcParam0F">The RTPC value for parameter 0xF, multiplied by 1000 per the row.</param>
    public static WwiseContinuationPlan PrepareNextToPlay(
        WwiseContinuousSelector selector,
        WwiseContinuousTransitionMode mode,
        double transitionTimeMs = 0, double transitionMinMs = 0, double transitionMaxMs = 0,
        double rtpcParam0F = 0)
    {
        ArgumentNullException.ThrowIfNull(selector);

        int index = selector.Next();                              // 0xA09C40: the selection happens here
        if (index < 0) return WwiseContinuationPlan.None;         // gapF 2.9: no next item

        double xfade = 0;
        // The row says modes 1..3 store an xfade. Mode 3 is not named by the frozen rows and has no enum
        // value, so only the two recovered cross-fades compute one here.
        if (mode is WwiseContinuousTransitionMode.LinearCrossFade
                 or WwiseContinuousTransitionMode.ConstantPowerCrossFade)
        {
            xfade = transitionTimeMs + transitionMinMs + rtpcParam0F * 1000.0;
            // No draw for an empty range: the native branches past the whole LCG block.
            if (transitionMaxMs != transitionMinMs)
                xfade += selector.Rng.Next() / 2147483647.0 * (transitionMaxMs - transitionMinMs);
            if (xfade < 0) xfade = 0;                             // clamped ≥ 0 ms
        }

        return new WwiseContinuationPlan(true, selector.ItemAt(index), mode, xfade);
    }

    // ------------------------------------------------------------------ gapF 2.5: modes 1 and 2 start time

    /// <summary>
    /// Modes 1 and 2 scheduling (0xA6A580, gapF 2.5). Only a next item and an estimated length of at least
    /// 50 ms schedule an action; then <c>xfade = min(stored, length/2)</c> and the next starts
    /// <c>length − xfade</c> ms after the current voice's start, i.e. xfade ms before its estimated end. The
    /// action delay is <c>round-half-away((length − xfade)·rate/1000)</c> samples. A short or unknown length
    /// schedules nothing; the next then chains at the current one's end.
    /// </summary>
    /// <param name="lengthMs">The current voice's estimated length in ms; <c>NaN</c> models "unknown".</param>
    /// <param name="xfadeMs">The stored xfade from <see cref="PrepareNextToPlay"/>.</param>
    /// <param name="hasNext">Whether a next item exists.</param>
    /// <param name="rateHz">The rate the row's delay conversion divides by.</param>
    public static WwiseContinuationTiming ScheduleCrossFade(
        double lengthMs, double xfadeMs, bool hasNext, int rateHz = WwiseRuntimeSettings.MixRateHz)
    {
        // A NaN length models the row's "unknown": it schedules nothing and the next starts at the end.
        if (!hasNext || !(lengthMs >= MinimumCrossFadeLengthMs))
            return new WwiseContinuationTiming(false, lengthMs, 0, 0);

        double xfade = Math.Min(xfadeMs, lengthMs / 2.0);
        long delay = (long)Math.Round((lengthMs - xfade) * rateHz / 1000.0, MidpointRounding.AwayFromZero);
        return new WwiseContinuationTiming(true, lengthMs - xfade, delay, xfade);
    }

    // ------------------------------------------------------------------ gapF 2.6: cross-fade gains

    /// <summary>GapF 2.6: <c>durationFrames = ceil(ms / [0x1052444] msPerFrame)</c>.</summary>
    public static double TransitionFrames(double durationMs) =>
        Math.Ceiling(durationMs / WwiseRuntimeSettings.MsPerFrame);

    /// <summary>
    /// GapF 2.6: <c>t = (now − start) / durationFrames</c>. Outside the transition window is clamped to
    /// [0,1]; the transition itself is only evaluated inside it.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// A non-positive frame count. The row gives the formula only for a positive duration, and a
    /// zero-frame transition's t is not settled; the model refuses rather than choosing a value.
    /// </exception>
    public static double TransitionProgress(long nowFrame, long startFrame, double durationFrames)
    {
        if (durationFrames <= 0)
            throw new NotSupportedException(
                "a zero-frame transition (t = (now − start)/durationFrames) is not settled by gapF 2.6");
        double t = (nowFrame - startFrame) / durationFrames;
        return Math.Clamp(t, 0.0, 1.0);
    }

    /// <summary>
    /// GapF 2.6 mirror rule (0xA35F14): when start ≥ target and the mirror flag is set (it is for both) the
    /// curve c becomes 8−c, except 3 and 5. Mode 1's linear curve 4 stays linear; mode 2's sine curve 1
    /// becomes 7 (cos).
    /// </summary>
    public static byte MirrorCurve(byte curve, bool descending) =>
        descending && curve != 3 && curve != 5 ? (byte)(8 - curve) : curve;

    /// <summary>
    /// GapF 2.6, mode 1 linear in-gain <c>t</c> and mode 2 constant-power in-gain <c>sin(πt/2)</c>.
    /// </summary>
    public static double CrossFadeIn(WwiseContinuousTransitionMode mode, double t) => mode switch
    {
        WwiseContinuousTransitionMode.LinearCrossFade => t,
        WwiseContinuousTransitionMode.ConstantPowerCrossFade => Math.Sin(Math.PI * t / 2.0),
        _ => throw new NotSupportedException($"transition mode {mode} has no cross-fade gain"),
    };

    /// <summary>
    /// GapF 2.6, mode 1 linear out-gain <c>1−t</c> and mode 2 constant-power out-gain <c>cos(πt/2)</c>.
    /// </summary>
    public static double CrossFadeOut(WwiseContinuousTransitionMode mode, double t) => mode switch
    {
        WwiseContinuousTransitionMode.LinearCrossFade => 1.0 - t,
        WwiseContinuousTransitionMode.ConstantPowerCrossFade => Math.Cos(Math.PI * t / 2.0),
        _ => throw new NotSupportedException($"transition mode {mode} has no cross-fade gain"),
    };

    // ------------------------------------------------------------------ gapF 2.7 / gapG 1.1..1.3: mode 4

    /// <summary>
    /// Mode 4 at the current source's start (0xA6A2DC, gapG 1.1): a PlayAndContinue is built and enqueued
    /// with no previous PBI, xfade 0 and delay 0, so it executes immediately (frames = 0). It carries
    /// <c>+0x8C = 1</c> when <c>pbi+0x1BC</c> bit7 is set and <c>+0x90 = pbi+0x1C8</c>, the current chain id
    /// (gapG 1.1/1.2).
    /// </summary>
    /// <param name="currentChainId">The current PBI's +0x1C8 chain id (0 when it has none).</param>
    /// <param name="engineCommandType1">Whether <c>pbi+0x1BC</c> bit7 set <c>action+0x8C</c>.</param>
    public static WwisePlayAndContinue Mode4PlayAndContinue(uint currentChainId, bool engineCommandType1) =>
        new(HasPreviousPbi: false, XfadeMs: 0, DelaySamples: 0, StartOffsetSamples: 0,
            ChainId: currentChainId, EngineCommandType1: engineCommandType1);

    /// <summary>
    /// The chained PBI the mode-4 PlayAndContinue becomes (gapG 1.2/1.3): <c>pbi+0x1D8</c> (start offset in
    /// samples) is the pending delay remainder, and <c>pbi+0x1C8</c> (chain id) is the action's chain id,
    /// or a fresh global counter when it is zero. The voice attach (0xA4304C, gapG 1.5) then matches the
    /// chain id and adds the next source to the <b>same</b> voice; the pitch-node switch (0xA52B90,
    /// gapG 1.7) starts it on the sample right after the current source's last, with no fade or overlap.
    /// </summary>
    /// <param name="nextItemId">The item the selection chose.</param>
    /// <param name="startOffsetSamples">The start offset the PBI ctor reads (H 1.3), 0 for mode 4.</param>
    /// <param name="chainId">The action's chain id (action+0x90 → params+0x7C).</param>
    /// <param name="freshChainId">The counter value to use when the action's chain id is zero.</param>
    public static WwisePendingSource AttachPending(
        uint nextItemId, long startOffsetSamples, uint chainId, uint freshChainId) =>
        new(nextItemId, startOffsetSamples, chainId != 0 ? chainId : freshChainId);

    // ------------------------------------------------------------------ gapF 2.8: mode 5

    /// <summary>
    /// The mode-5 (trigger rate, 0xA09F04) scheduler. The first call selects A; later calls take the
    /// pre-selected B from +0x120. It then looks ahead to the next B. If B is null it plays A and stops
    /// scheduling; otherwise it plays A as a plain play, remembers B and re-enters after
    /// <c>max(transition/1000 s, 0.022 s) + params+0x74/rate</c>. There is no cross-fade.
    /// </summary>
    public sealed class WwiseMode5Scheduler
    {
        private readonly WwiseContinuousSelector _selector;
        private readonly double _transitionSeconds;
        private readonly double _startOffsetSamples;
        private readonly double _rateHz;

        private bool _first = true;
        private uint? _pending;

        /// <param name="selector">The per-play continuation state.</param>
        /// <param name="transitionMs">The container's transition time in ms (converted to seconds).</param>
        /// <param name="startOffsetSamples">
        /// The PBI start offset in samples (params+0x74, the action manager's sub-frame remainder
        /// <c>pending+0xC</c>). The row divides it by 48000; the InitialDelay term of 0x9F12E0 is not added
        /// because <c>params+0x7C</c> is 0 on this path (0xA0A16C..0xA0A1C0).
        /// </param>
        /// <param name="rateHz">The rate the row divides the start offset by.</param>
        public WwiseMode5Scheduler(WwiseContinuousSelector selector, double transitionMs,
                                   double startOffsetSamples = 0, int rateHz = WwiseRuntimeSettings.MixRateHz)
        {
            _selector = selector ?? throw new ArgumentNullException(nameof(selector));
            _transitionSeconds = transitionMs / 1000.0;
            _startOffsetSamples = startOffsetSamples;
            _rateHz = rateHz;
        }

        /// <summary>One trigger: the item to play now and whether scheduling stops after it.</summary>
        public WwiseMode5Step Step()
        {
            uint? a = _first ? Select() : _pending;
            _first = false;
            uint? b = Select();

            if (a is null) return new WwiseMode5Step(0, true, 0);

            if (b is null)
            {
                // gapF 2.8: B null → play A and stop scheduling. Shipped 777177819 (n=1, loop 1) lands here.
                _pending = null;
                return new WwiseMode5Step(a.Value, true, 0);
            }

            _pending = b;
            double period = Math.Max(_transitionSeconds, MinimumReentrySeconds) + _startOffsetSamples / _rateHz;
            return new WwiseMode5Step(a.Value, false, period);
        }

        private uint? Select()
        {
            int index = _selector.Next();
            return index < 0 ? null : _selector.ItemAt(index);
        }
    }
}

/// <summary>RanSeq transition mode (+0x90 low nibble; gapF 2.4/2.8, gapD D4.1).</summary>
public enum WwiseContinuousTransitionMode
{
    /// <summary>Not a continuous transition.</summary>
    None = 0,

    /// <summary>Mode 1: a linear amplitude cross-fade (gapF 2.5/2.6).</summary>
    LinearCrossFade = 1,

    /// <summary>Mode 2: a sine/cosine constant-power cross-fade (gapF 2.5/2.6).</summary>
    ConstantPowerCrossFade = 2,

    /// <summary>Mode 4: sample-accurate chaining into the same voice (gapF 2.7, gapG 1.1..1.7).</summary>
    SampleAccurateChain = 4,

    /// <summary>Mode 5: a fixed-period trigger with no cross-fade (gapF 2.8).</summary>
    TriggerRate = 5,
}

/// <summary>
/// The loop info one continuation item is built with (0xA091CC, gapF 2.2). <see cref="Loop"/> is the
/// container's loop field: 0 is infinite, 1 is a single pass, n ≥ 2 gives
/// <c>n + loopMin + round(rand·(loopMax − loopMin))</c>, at least 1.
/// </summary>
public readonly record struct WwiseContinuationLoop(ushort Loop, ushort LoopMin, ushort LoopMax)
{
    /// <summary>Flag b0: the loop field is not the single-pass value 1.</summary>
    public bool Enabled => Loop != 1;

    /// <summary>Flag b1: the loop field is 0, so the container loops forever.</summary>
    public bool Infinite => Loop == 0;

    /// <summary>GapF 2.9: a loop-0 container never ends on its own.</summary>
    public bool NeverEnds => Infinite;

    /// <summary>
    /// The loop info count (0xA09230..0xA09264, 0xA09350..0xA093F0). The draw happens only when the loop
    /// field is at least 2 (flags &amp; 3 == 1) <b>and</b> the range is non-empty
    /// (<c>(u16)(loopMax − loopMin) != 0</c>, 0xA0935C..0xA09368); otherwise the LCG is untouched and the
    /// count is <c>(short)(loop + loopMin)</c>, clamped to at least 1. When the range is non-empty,
    /// <c>draw = (int)(0.5 + fraction·(short)(loopMax − loopMin))</c> with
    /// <c>fraction = (rng.Hi &gt;&gt; 1) / 2147483647.0</c>, and the count is the 16-bit
    /// <c>(short)(loop + loopMin + draw)</c>, clamped to at least 1. <c>0.5 + x</c> truncated toward zero is
    /// round-half-up on the nonnegative operand.
    ///
    /// Unreachable in the shipped banks: all 25 continuous containers are loop 0 or loop 1, so neither the
    /// draw nor the loopMin/loopMax range is exercised. It is source-exact but not capture-verifiable.
    /// </summary>
    public int Count(WwiseRng rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        if (Loop < 2) return 1;

        short range = (short)(LoopMax - LoopMin);
        if (range == 0) return Math.Max(1, (int)(short)(Loop + LoopMin));

        double fraction = rng.Next() / 2147483647.0;
        int draw = (int)(0.5 + fraction * range);
        short count = (short)(Loop + LoopMin + draw);
        return Math.Max(1, (int)count);
    }
}

/// <summary>
/// The per-play continuation selection state (0xA0ABC4/0xA09C40, gapF 2.3). One is allocated fresh for every
/// shipped continuous play (bank bit1 → +0x91 bit4), so a sequence starts at item 0.
///
/// Length 0 returns none; length 1 returns item 0 with the loop count; a random container draws from its
/// <see cref="WwiseSelectionState"/> and a sequence walks the playlist, each gated by the same loop rule.
/// </summary>
public sealed class WwiseContinuousSelector
{
    private readonly IReadOnlyList<uint> _playlist;
    private readonly WwiseSelectionMode _mode;
    private readonly bool _pingPong;
    private readonly WwiseContinuationLoop _loop;
    private readonly WwiseSelectionState? _randomState;

    private int _count;
    private int _index = -1;
    private bool _forward = true;

    public WwiseContinuousSelector(
        IReadOnlyList<uint> playlist, WwiseSelectionMode mode, bool pingPong,
        WwiseContinuationLoop loop, WwiseRng rng, WwiseSelectionState? randomState = null)
    {
        ArgumentNullException.ThrowIfNull(playlist);
        ArgumentNullException.ThrowIfNull(rng);
        if (mode == WwiseSelectionMode.Random && playlist.Count > 1 && randomState is null)
            throw new ArgumentException(
                "a random continuous container with more than one item needs its selection state", nameof(randomState));

        _playlist = playlist;
        _mode = mode;
        _pingPong = pingPong;
        _loop = loop;
        Rng = rng;
        _randomState = randomState;
        _count = loop.Count(rng);                                  // 0xA091CC, gapF 2.2
    }

    /// <summary>The shared global LCG, used for the loop count and random item selection.</summary>
    public WwiseRng Rng { get; }

    /// <summary>The loop info the continuation is built with.</summary>
    public WwiseContinuationLoop Loop => _loop;

    /// <summary>The playlist length.</summary>
    public int Length => _playlist.Count;

    /// <summary>The item at an index the selector returned.</summary>
    public uint ItemAt(int index) => _playlist[index];

    /// <summary>The next item index, or −1 when the continuation ends (gapF 2.3/2.9).</summary>
    public int Next()
    {
        if (_playlist.Count == 0) return -1;
        if (_playlist.Count == 1) return SingleItemNext();
        return _mode == WwiseSelectionMode.Sequence ? SequenceNext() : RandomNext();
    }

    /// <summary>GapF 2.3: length 1 → count ≤ 0 ends; otherwise count−− unless infinite, and return item 0.</summary>
    private int SingleItemNext()
    {
        if (_count <= 0) return -1;
        if (!_loop.Infinite) _count--;
        return 0;
    }

    /// <summary>
    /// GapF 2.3 random: at a cycle reset (the selection state's counter is 0) the loop rule decides whether
    /// the next cycle starts; otherwise the state picks the item.
    /// </summary>
    private int RandomNext()
    {
        if (_randomState!.Counter == 0 && !AdvanceAtBoundary()) return -1;
        return _randomState.SelectNext(Rng);
    }

    /// <summary>
    /// GapF 2.3 sequence. The loop counter is decremented only at the <b>start-side</b> reversal: a
    /// forward-side wrap reverses the direction with no count change and no end check (0xA0863C..0xA08664),
    /// while the backward walk reaching 0 resets to 1 and then applies the flags rule
    /// (0xA085E4..0xA08630). A non-ping-pong wrap resets the index to 0 and applies the same rule. So
    /// <c>count</c> is the number of complete round trips, not passes.
    ///
    /// Unreachable in the shipped banks: every RanSeq +0x91 byte is 0x12/0x1A with the ping-pong bit (bit5)
    /// clear, so no shipped continuous sequence takes the reversal branch.
    /// </summary>
    private int SequenceNext()
    {
        if (_forward)
        {
            int next = _index + 1;
            if (next >= _playlist.Count)
            {
                if (_pingPong)
                {
                    // Forward-side reversal (0xA0863C): reverse only.
                    _forward = false;
                    _index -= 1;
                }
                else
                {
                    if (!AdvanceAtBoundary()) return -1;
                    _index = 0;
                }
            }
            else
            {
                _index = next;
            }
        }
        else
        {
            int next = _index - 1;
            if (next < 0)
            {
                // Start-side reversal (0xA085E4): reset to 1, then the loop rule.
                _forward = true;
                _index = 1;
                if (!AdvanceAtBoundary()) return -1;
            }
            else
            {
                _index = next;
            }
        }

        return _index >= 0 && _index < _playlist.Count ? _index : -1;
    }

    /// <summary>
    /// GapF 2.3, the shared wrap rule: not enabled (loop 1) ends; infinite resets; otherwise the loop count
    /// decrements and reaching 0 ends.
    /// </summary>
    private bool AdvanceAtBoundary()
    {
        if (!_loop.Enabled) return false;                          // loop 1
        if (_loop.Infinite) return true;                            // loop 0
        _count--;
        return _count > 0;
    }
}

/// <summary>GapF 2.4: what was chosen at the current voice's start and how the transition runs.</summary>
public readonly record struct WwiseContinuationPlan(
    bool HasNext, uint NextItemId, WwiseContinuousTransitionMode Mode, double XfadeMs)
{
    /// <summary>GapF 2.9: the selection returned none, so no next item is scheduled.</summary>
    public static readonly WwiseContinuationPlan None =
        new(false, 0, WwiseContinuousTransitionMode.None, 0);
}

/// <summary>GapF 2.5: when the next item starts relative to the current voice's start.</summary>
public readonly record struct WwiseContinuationTiming(
    bool HasScheduledAction, double StartFromVoiceStartMs, long DelaySamples, double XfadeMs);

/// <summary>
/// GapF 2.7 / gapG 1.1/1.2: the PlayAndContinue a continuous transition builds. Mode 4 leaves
/// <see cref="HasPreviousPbi"/> false, so the previous PBI is not faded out.
/// </summary>
public readonly record struct WwisePlayAndContinue(
    bool HasPreviousPbi, double XfadeMs, long DelaySamples, long StartOffsetSamples,
    uint ChainId, bool EngineCommandType1);

/// <summary>
/// GapG 1.3/1.5/1.7: the next source chained into the same voice as a pending source. Its chain id matches
/// the voice, and it starts on the output sample right after the current source's last.
/// </summary>
public readonly record struct WwisePendingSource(uint ItemId, long StartOffsetSamples, uint ChainId);

/// <summary>GapF 2.8: one mode-5 trigger, and the re-entry period in seconds when scheduling continues.</summary>
public readonly record struct WwiseMode5Step(uint ItemId, bool StopScheduling, double ReentrySeconds);