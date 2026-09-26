// fidelity: M6-007

namespace Cozmo.Robot.Animation.Wwise;

/// <summary>
/// The Wwise random/sequence step selection engine (M6-007, gapA §3). This is the runtime's own choice of
/// which playlist item a RanSeq container plays next. It is deliberately independent of the existing
/// <see cref="WwisePlayback"/>/<see cref="WwiseAudioSource"/> types so that M9 singing (which still runs on
/// the old path) cannot be disturbed by it.
///
/// The frozen rows are M6-wwise-bank.md Appendix B §3.1..§3.9. Addresses are libcozmoEngine.so VAs; they are
/// citations, not call targets. Only the step path is implemented here: the continuous paths (0xA0ABC4,
/// 0xA09F04) are not read and are out of scope for this record.
/// </summary>
/// <remarks>
/// Bank flag bits named in the rows, on the RanSeq's +0x91 byte (the mapping is §2.6):
/// <list type="bullet">
/// <item>bank bit2 → +0x91 b5 = ping-pong instead of wrap (§3.7);</item>
/// <item>bank bit3 → +0x91 b6 = continuous, so not the step path (§3.3);</item>
/// <item>bank bit4 → +0x91 b7 = one shared state for all game objects (§3.4);</item>
/// <item>weights in use → +0x91 b3, set when any playlist weight ≠ 50000 (§2.6, §3.6c).</item>
/// </list>
/// </remarks>
public static class WwiseSelectionRows
{
    /// <summary>The playlist weight the runtime writes when a container uses no weights (0x0098A6D4, §3.4).</summary>
    public const int DefaultWeight = 50000;
}

/// <summary>
/// The Wwise 64-bit LCG (AKRANDOM, M6-007 §3.1). One instance is the single global state the inlined draw
/// sites all share.
///
/// <code>s = s*0x5851F42D4C957F2D + 1; output = (u32)(s&gt;&gt;32) &gt;&gt; 1</code>
///
/// The seed is <b>not</b> source-reproducible: the original calls <c>SetSeed(0)</c> once at
/// <c>SoundEngine::Init</c> (0x0099EF80 → 0x009B0210 → 0x0099DB58), which resolves to <c>time(NULL)</c>
/// (§3.2). Cozmo's SetupConfig writes the SetRandomSeed field 0, so no other seed is ever installed. The
/// constructor therefore takes an injectable seed and defaults to the current Unix time in seconds; this is
/// a COMPATIBILITY_POLICY, not source fidelity — draws match in algorithm and distribution, never in
/// sequence (also true of the original across runs).
/// </summary>
public sealed class WwiseRng
{
    private const ulong Multiplier = 0x5851F42D4C957F2DUL;

    private ulong _state;

    /// <summary>An RNG seeded with the current Unix time in seconds, as the original's <c>time(NULL)</c>.</summary>
    public WwiseRng()
        : this(TimeSeed())
    {
    }

    /// <summary>An RNG with an explicit seed, for deterministic tests and for callers that model the seed.</summary>
    public WwiseRng(ulong seed) => _state = seed;

    /// <summary>The seed the parameterless constructor uses: the current Unix time in seconds, as ulong.</summary>
    public static ulong TimeSeed() => unchecked((ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds());

    /// <summary>The next draw: the high 32 bits of the advanced state, shifted right by one (a 31-bit value).</summary>
    public uint Next()
    {
        _state = unchecked(_state * Multiplier + 1UL);
        return (uint)(_state >> 32) >> 1;
    }
}

/// <summary>RanSeq mode (+0x91 b0..2; §2.6). Only mode 1 is a sequence in the frozen rows.</summary>
public enum WwiseSelectionMode
{
    /// <summary>Mode 0: pick from the playlist with the random engine (§3.6).</summary>
    Random = 0,

    /// <summary>Mode 1: walk the playlist in order, wrap or ping-pong (§3.7).</summary>
    Sequence = 1,
}

/// <summary>RanSeq randomMode (+0x90 b4..5; §2.6).</summary>
public enum WwiseRandomMode
{
    /// <summary>randomMode 0: the "standard" path, whose only exclusion is the blocked/avoid list (§3.6b).</summary>
    Standard = 0,

    /// <summary>randomMode 1: the shuffle path, which also excludes played items (§3.6b).</summary>
    Shuffle = 1,
}

/// <summary>
/// The fixed bank settings a RanSeq state is built from (M6-007 §3.4, §2.6). These do not change between
/// plays of a container; the mutable selection state lives in <see cref="WwiseSelectionState"/>.
/// </summary>
public sealed record WwiseContainerSelectionSettings(
    int Length,
    IReadOnlyList<int>? Weights,
    WwiseSelectionMode Mode,
    WwiseRandomMode RandomMode,
    int AvoidRepeat,
    bool UsesWeights,
    bool SequencePingPong)
{
    /// <summary>A random container with no weights, the common shipped shape.</summary>
    public static WwiseContainerSelectionSettings Random(int length, WwiseRandomMode randomMode, int avoidRepeat)
        => new(length, null, WwiseSelectionMode.Random, randomMode, avoidRepeat, false, false);

    /// <summary>A sequence container with wrap (no ping-pong).</summary>
    public static WwiseContainerSelectionSettings Sequence(int length, bool pingPong = false)
        => new(length, null, WwiseSelectionMode.Sequence, WwiseRandomMode.Standard, 0, false, pingPong);

    /// <summary>The weight of playlist item <paramref name="index"/>, or <see cref="WwiseSelectionRows.DefaultWeight"/>.</summary>
    public int WeightAt(int index) => Weights is null ? WwiseSelectionRows.DefaultWeight : Weights[index];
}

/// <summary>
/// The per-container selection state (M6-007 §3.4). One of these is created the first time a container is
/// played. It holds both the random state and the sequence state; only one is used per container.
///
/// Random state (§3.4): <c>remaining = counter = len</c>, <c>total = remaining weight = 50000·len</c>; when
/// weights are in use, <c>total = remaining = playlist total</c>. Init allocates the played and blocked
/// bitsets and the avoid list.
///
/// Sequence state (§3.4): <c>forward = 1, index = −1</c>.
/// </summary>
public sealed class WwiseSelectionState
{
    private readonly int _length;
    private readonly WwiseRandomMode _randomMode;
    private readonly int _avoidRepeat;
    private readonly bool _usesWeights;

    // Random state (M6-007 §3.6).
    private readonly long _totalWeight;
    private readonly bool[] _played;
    private readonly bool[] _blocked;
    private readonly LinkedList<(int Index, int Weight)> _avoid = new();

    private long _remainingWeight;
    private int _remaining;
    private int _counter;

    // Sequence state (M6-007 §3.4, §3.7).
    private bool _forward = true;
    private int _index = -1;

    public WwiseSelectionState(WwiseContainerSelectionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _length = settings.Length;
        _randomMode = settings.RandomMode;
        _avoidRepeat = settings.AvoidRepeat;
        _usesWeights = settings.UsesWeights;
        Mode = settings.Mode;
        SequencePingPong = settings.SequencePingPong;
        Weights = settings.Weights;

        _played = new bool[_length];
        _blocked = new bool[_length];

        // ctor 0xA06994: total = remaining weight = 50000·len, or the playlist total when weights are in use.
        long perItem = WwiseSelectionRows.DefaultWeight;
        _totalWeight = _usesWeights && settings.Weights is not null
            ? settings.Weights.Aggregate(0L, (a, w) => a + w)
            : perItem * _length;

        _remainingWeight = _totalWeight;
        _remaining = _length;
        _counter = _length; // so the first SelectRandomly plays without an immediate reset (§3.4, §3.6a)
    }

    /// <summary>The container's mode, copied from the settings.</summary>
    public WwiseSelectionMode Mode { get; }

    /// <summary>Whether a sequence reverses at the end instead of wrapping (§3.7).</summary>
    public bool SequencePingPong { get; }

    /// <summary>The playlist weights, or null when the container uses none.</summary>
    public IReadOnlyList<int>? Weights { get; }

    /// <summary>The number of items remaining eligible in the current random cycle (§3.6b/§3.6d).</summary>
    public int Remaining => _remaining;

    /// <summary>The sum of the remaining eligible weights (§3.6c).</summary>
    public long RemainingWeight => _remainingWeight;

    /// <summary>The distinct items still to be played before the random cycle resets (§3.6d).</summary>
    public int Counter => _counter;

    /// <summary>Whether playlist item <paramref name="index"/> is currently blocked/avoided.</summary>
    public bool IsBlocked(int index) => _blocked[index];

    /// <summary>Whether playlist item <paramref name="index"/> has been played in this random cycle.</summary>
    public bool IsPlayed(int index) => _played[index];

    /// <summary>
    /// The next playlist index to play for the step path (SelectPlayable 0xA0A3B4, §3.5).
    /// <list type="bullet">
    /// <item>Length 0 → −1 (none).</item>
    /// <item>Length 1 → item 0, with <b>no</b> RNG draw and no state change (the rows establish the absence of
    /// a draw; with one item the bookkeeping is unobservable).</item>
    /// <item>Sequence → §3.7, which never draws and never probes.</item>
    /// <item>Random → §3.6, then — for shuffle and for standard-with-avoid — a linear playability probe.</item>
    /// </list>
    /// </summary>
    /// <param name="rng">The shared global LCG.</param>
    /// <param name="isPlayable">
    /// The node's playability check (node vfunc +0x48). Null means every item is playable. It is consulted
    /// only for the random path, and only in the modes the rows give a retry.
    /// </param>
    public int SelectNext(WwiseRng rng, Func<int, bool>? isPlayable = null)
    {
        ArgumentNullException.ThrowIfNull(rng);

        if (_length == 0) return -1;
        if (_length == 1) return 0;
        if (Mode == WwiseSelectionMode.Sequence) return SequenceStep();

        int index = SelectRandomly(rng);
        if (index < 0) return -1;

        bool canProbe = _randomMode == WwiseRandomMode.Shuffle || _avoidRepeat > 0;
        if (canProbe && isPlayable is not null && !isPlayable(index))
            index = Probe(index, isPlayable);

        return index;
    }

    /// <summary>
    /// SelectRandomly 0xA08A44 (M6-007 §3.6). The counter, remaining items, remaining weight and avoid list
    /// are the fields +0xE, +0xC, +8 and +0x10/+0x14.
    /// </summary>
    private int SelectRandomly(WwiseRng rng)
    {
        // §3.6a: a fresh cycle when the counter reaches zero. The step path passes no loop info, so it always
        // takes this reset.
        if (_counter == 0) Reset();

        if (_remaining <= 0) return -1;

        int index;
        if (_usesWeights)
        {
            // §3.6c: weighted. r is an unsigned modulo of the 31-bit draw against the remaining weight.
            // The source divides by the remaining weight with uidivmod; a zero divisor is not addressed by
            // the rows. That cannot arise for a playlist with an eligible item, so it is returned as "none"
            // rather than modelled.
            if (_remainingWeight <= 0) return -1;
            uint r = rng.Next() % (uint)_remainingWeight;
            index = PickByRunningWeight(r);
        }
        else
        {
            // §3.6b: unweighted. k is the index among eligible items, 0-based.
            int k = (int)(rng.Next() % (uint)_remaining);
            index = KthEligible(k);
        }

        if (index < 0) return -1;
        ApplyAfterPick(index);
        return index;
    }

    /// <summary>§3.6a: counter = len; clear played; remaining = len; subtract the avoid list.</summary>
    private void Reset()
    {
        _counter = _length;
        Array.Clear(_played);

        if (_randomMode == WwiseRandomMode.Shuffle)
        {
            // Shuffle only: remaining weight = total − Σ weight(avoid list).
            long w = _totalWeight;
            foreach (var (item, _) in _avoid) w -= WeightAt(item);
            _remainingWeight = w;
        }

        _remaining = _length - _avoid.Count;
    }

    /// <summary>
    /// §3.6b eligibility. Standard excludes blocked items only when avoid &gt; 0 (otherwise "anything");
    /// shuffle excludes both played and blocked items.
    /// </summary>
    private bool IsEligible(int index)
        => _randomMode == WwiseRandomMode.Shuffle
            ? !_played[index] && !_blocked[index]
            : _avoidRepeat > 0
                ? !_blocked[index]
                : true;

    /// <summary>§3.6b: walk the indices and return the k-th eligible one (0-based), or −1.</summary>
    private int KthEligible(int k)
    {
        int seen = 0;
        for (int i = 0; i < _length; i++)
        {
            if (!IsEligible(i)) continue;
            if (seen == k) return i;
            seen++;
        }
        return -1;
    }

    /// <summary>§3.6c: the first eligible index whose running weight sum exceeds r, or −1.</summary>
    private int PickByRunningWeight(uint r)
    {
        long sum = 0;
        for (int i = 0; i < _length; i++)
        {
            if (!IsEligible(i)) continue;
            sum += WeightAt(i);
            if (sum > r) return i;
        }
        return -1;
    }

    /// <summary>§3.6d: the played/avoid bookkeeping after an item is selected.</summary>
    private void ApplyAfterPick(int index)
    {
        if (_randomMode == WwiseRandomMode.Standard)
        {
            if (!_played[index])
            {
                _played[index] = true;
                _counter--;
            }
            ApplyStandardAvoid(index);
        }
        else
        {
            _remaining--;
            _counter--;
            _remainingWeight -= WeightAt(index);
            _played[index] = true;
            AppendAndBlock(index, Math.Min(Math.Max(_avoidRepeat, 1), _length - 1));
        }
    }

    /// <summary>
    /// §3.6d standard: when avoid &gt; 0, remaining−−, append to the avoid list, set blocked and subtract the
    /// weight. When the list is longer than <c>min(avoid, len−1)</c>, the oldest entry is unblocked and its
    /// weight and item restored. (0xA08694)
    /// </summary>
    private void ApplyStandardAvoid(int index)
    {
        if (_avoidRepeat <= 0) return;

        _remaining--;
        _avoid.AddLast((index, WeightAt(index)));
        _blocked[index] = true;
        _remainingWeight -= WeightAt(index);

        int limit = Math.Min(_avoidRepeat, _length - 1);
        while (_avoid.Count > limit)
        {
            var oldest = _avoid.First!.Value;
            _avoid.RemoveFirst();
            _blocked[oldest.Index] = false;
            _remaining++;
            _remainingWeight += WeightAt(oldest.Index);
        }
    }

    /// <summary>
    /// §3.6d shuffle: remaining−−, counter−−, remaining weight −= w, set played, append and block. The limit
    /// is <c>min(max(avoid, 1), len−1)</c>; popping the oldest unblocks it and, if it is not played, restores
    /// its item and weight.
    /// </summary>
    private void AppendAndBlock(int index, int limit)
    {
        _avoid.AddLast((index, WeightAt(index)));
        _blocked[index] = true;

        while (_avoid.Count > limit)
        {
            int oldest = _avoid.First!.Value.Index;
            _avoid.RemoveFirst();
            _blocked[oldest] = false;
            if (!_played[oldest])
            {
                _remaining++;
                _remainingWeight += WeightAt(oldest);
            }
        }
    }

    /// <summary>
    /// §3.5 retry: a linear probe from idx+1 (mod len), skipping played/blocked, then the post-pick
    /// bookkeeping, up to len tries. Only shuffle and standard-with-avoid take this path.
    /// </summary>
    /// <remarks>
    /// The row names only the helper 0xA08694 for the probe. It is applied here as the same post-pick
    /// recording used on the primary path so that the probed item is not immediately re-chosen; the row does
    /// not split the helper's played and avoid parts for the probe. This is a low-risk unresolved detail: no
    /// shipped Cozmo.bnk container reaches it, because the probe exists only for children that fail the
    /// playability check.
    /// </remarks>
    private int Probe(int from, Func<int, bool> isPlayable)
    {
        int candidate = (from + 1) % _length;
        for (int tries = 0; tries < _length; tries++, candidate = (candidate + 1) % _length)
        {
            if (_played[candidate] || _blocked[candidate]) continue;
            if (!isPlayable(candidate)) continue;
            ApplyAfterPick(candidate);
            return candidate;
        }
        return -1;
    }

    /// <summary>
    /// §3.7 sequence step. Forward: idx+1; at len, reverse when ping-pong (idx−1, direction backward) or wrap
    /// to 0 otherwise. Backward: idx−1; at 0 it turns forward and plays 1. The first play gives 0.
    /// </summary>
    private int SequenceStep()
    {
        if (_forward)
        {
            int next = _index + 1;
            if (next >= _length)
            {
                if (SequencePingPong)
                {
                    _forward = false;
                    _index -= 1;
                }
                else
                {
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
                _forward = true;
                _index = 1;
            }
            else
            {
                _index = next;
            }
        }

        return _index;
    }

    private int WeightAt(int index) => Weights is null ? WwiseSelectionRows.DefaultWeight : Weights[index];
}

/// <summary>
/// The shared home of container state (M6-007 §3.4). Bank bit4 (+0x91 b7) decides whether a container's state
/// is one instance shared by every game object, or a sorted per-game-object map created on the first play.
/// All shipped Cozmo RanSeq containers have bit4 set, so their state is global and shared across the robot
/// game objects 7..10.
/// </summary>
public sealed class WwiseSelection
{
    private readonly bool _sharedAcrossGameObjects;
    private readonly Dictionary<uint, WwiseSelectionState> _shared = new();
    private readonly SortedDictionary<(uint Container, int GameObject), WwiseSelectionState> _perObject = new();

    /// <param name="rng">The single global LCG the whole engine shares (§3.1).</param>
    /// <param name="sharedAcrossGameObjects">Bank bit4: true for every shipped Cozmo container.</param>
    public WwiseSelection(WwiseRng rng, bool sharedAcrossGameObjects)
    {
        Rng = rng ?? throw new ArgumentNullException(nameof(rng));
        _sharedAcrossGameObjects = sharedAcrossGameObjects;
    }

    /// <summary>The global LCG this selection engine draws from.</summary>
    public WwiseRng Rng { get; }

    /// <summary>The state for a container and game object, created on first use (§3.4).</summary>
    public WwiseSelectionState StateFor(uint containerId, int gameObject, WwiseContainerSelectionSettings settings)
    {
        if (_sharedAcrossGameObjects)
        {
            if (!_shared.TryGetValue(containerId, out var state))
            {
                state = new WwiseSelectionState(settings);
                _shared[containerId] = state;
            }
            return state;
        }

        var key = (containerId, gameObject);
        if (!_perObject.TryGetValue(key, out var perObjectState))
        {
            perObjectState = new WwiseSelectionState(settings);
            _perObject[key] = perObjectState;
        }
        return perObjectState;
    }

    /// <summary>The next playlist index for a container play on a game object (§3.5).</summary>
    public int NextIndex(uint containerId, int gameObject, WwiseContainerSelectionSettings settings,
                         Func<int, bool>? isPlayable = null)
        => StateFor(containerId, gameObject, settings).SelectNext(Rng, isPlayable);
}