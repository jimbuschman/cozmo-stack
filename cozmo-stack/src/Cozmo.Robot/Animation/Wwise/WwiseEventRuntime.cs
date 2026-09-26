// fidelity: M6-006

namespace Cozmo.Robot.Animation.Wwise;

/// <summary>What happened to one queued action when its turn came.</summary>
public enum WwiseActionOutcome
{
    /// <summary>The action's execute body ran (for Play, at least up to the unresolved PlayInternal).</summary>
    Executed,
    /// <summary>Object scope and no game object: skipped before EnqueueOrExecute (gapA 1.4).</summary>
    SkippedObjectScopeNoGameObject,
    /// <summary>Play probability was zero, or the draw exceeded it (gapA 1.10).</summary>
    SkippedProbability,
    /// <summary>The action's target did not resolve to a node/bus (Play 0xA62A1C error 0x0F).</summary>
    TargetMissing,
    /// <summary>The action object is absent, or its type-specific body is not recovered.</summary>
    NotParsed,
}

/// <summary>
/// One action the runtime handled, in the order it handled it. For a delayed action the launch tick is the
/// frame it was scheduled for; for a zero-delay action it is the tick at enqueue. The Play fields are the
/// parameters the execute body builds (gapA 1.11); the ones the rows mark unresolved are recorded but not
/// applied.
/// </summary>
public sealed record WwiseActionExecution(
    uint ActionId, ushort ActionType, WwiseActionOutcome Outcome,
    uint PlayingId, uint? GameObjectId, uint TargetId,
    long LaunchTick, long Frames, long DelaySamples, long SubFrameRemainderSamples,
    double FadeInMs, byte FadeCurve, double InitialDelaySamples);

/// <summary>
/// The Wwise 2016.2 control path (M6-006): queued PostEvent, ExecuteEvent, EnqueueOrExecute, the frame
/// drain, and the Play execute body. This is the runtime's own path, recovered from libcozmoEngine.so;
/// it is deliberately independent of <see cref="WwisePlayback"/>, <see cref="WwiseAudioSource"/> and the
/// other offline types so that M9 singing (which still runs on the old path) cannot be disturbed by it.
///
/// <para><b>Rows implemented.</b> gapA 1.1–1.7, 1.9–1.11; gapD D1.4–D1.6, D5.1, D5.2 (the dispatch and
/// pending-clear only), D5.3 (target resolution only), D5.5 (the lookup's not-registered → null rule);
/// gapG 2.1–2.2 for the delay resolution that a plain action uses. gapA 1.8's action enumeration is used
/// only to recognise Play/Stop/Seek.</para>
///
/// <para><b>Explicitly unresolved (not modelled, not guessed).</b>
/// <list type="bullet">
/// <item><b>PlayInternal (vfunc +0x128), the fade-in application and 0x9EE454/0xA616BC (gapA 1.11).</b>
/// Play execute resolves the target, builds the fade-in and initial-delay parameters and records them,
/// then stops. Nothing audible is applied.</item>
/// <item><b>The fade-in ranged draw (0xA61110, gapA 1.11).</b> The helper's body is not recovered (it is
/// not GetDelay's 0xA61260), so only property 0x10 and the curve are computed; the ranged term is not
/// applied. No shipped action carries a ranged fade (gapA 5.4).</item>
/// <item><b>Initial delay's RTPC (0xA11590) and ranged terms (gapA 1.11).</b> Only property 0x3B, in
/// seconds · rate rounded half away from zero (gapD D1.9), is computed.</item>
/// <item><b>Stop's 0xA79F08 / Seek's value formula and node handler (gapD D5.2/D5.3).</b> Stop applies
/// only the stated pending-action clear; Seek resolves the target and reports a missing one, nothing
/// else.</item>
/// <item><b>The EndOfEvent/callback-manager internals 0xA04F54 (gapA 1.3, gapD D3.x).</b> The play
/// count is modelled minimally: one for the in-flight event message, +1 per EnqueueOrExecute, −1 per
/// execute and −1 when ExecuteEvent returns.</item>
/// <item><b>The pending-list cap (gapD D1.4).</b> The rows state a cap exists and a full list drops the
/// action; its value is not recovered, so no cap is imposed and the drop path never fires.</item>
/// <item><b>The type-0x0503 PlayAndContinue look-ahead (gapD D1.4 correction, gapG 2.2–2.4).</b> It is
/// an internal action the continuous containers create (M6-008), not a bank action, so it is out of
/// this bounded task.</item>
/// <item><b>The delay table default [g+0x3C] (gapA 1.6).</b> Its value is not recovered; the runtime
/// uses 0, which is what makes the shipped no-prop-0x0F Play actions immediate.</item>
/// <item><b>Switch container resolution (gapA 4.1).</b> Out of this task's bounded scope.</item>
/// <item><b>Game-object registration (gapD D5.5).</b> The lookup's rule is modelled (registered → the
/// object, otherwise null); the registration API itself is not traced.</item>
/// </list></para>
/// </summary>
public sealed class WwiseEventRuntime
{
    /// <summary>AK_INVALID_PLAYING_ID: what PostEvent returns for an event it cannot find (gapA 1.2).</summary>
    public const uint InvalidPlayingId = 0;

    /// <summary>
    /// The global playing-ID counter. The native one is a single process-wide atomic ++ (gapA 1.2). Its
    /// initial value is not in the rows; it starts at 0 here so the first assigned id is 1 and 0 stays
    /// <see cref="InvalidPlayingId"/>.
    /// </summary>
    private static long s_nextPlayingId;

    private readonly Dictionary<uint, WwiseObject> _objects = new();
    private readonly Dictionary<uint, WwiseAction?> _actionCache = new();
    private readonly Dictionary<uint, WwiseNode?> _nodeCache = new();
    private readonly HashSet<uint> _registeredGameObjects = new();
    private readonly Queue<WwiseQueuedEvent> _messages = new();
    private readonly List<PendingAction> _pending = new();
    private readonly List<WwiseActionExecution> _log = new();
    private readonly Dictionary<uint, PlayingEvent> _playing = new();
    private readonly WwiseRng _rng;
    private long _tick;

    /// <param name="banks">The banks whose objects the control path reads; later banks win on a duplicate id.</param>
    /// <param name="rng">The single global LCG (M6-007). Defaults to a time-seeded instance.</param>
    public WwiseEventRuntime(IEnumerable<WwiseBank> banks, WwiseRng? rng = null)
    {
        ArgumentNullException.ThrowIfNull(banks);
        foreach (var bank in banks)
            foreach (var (id, o) in bank.Objects)
                _objects[id] = o;
        _rng = rng ?? new WwiseRng();
    }

    /// <summary>Type-1 queue messages posted but not yet pumped (gapA 1.2).</summary>
    public int QueuedEventCount => _messages.Count;

    /// <summary>Delayed actions waiting in the pending list (gapA 1.5).</summary>
    public int PendingActionCount => _pending.Count;

    /// <summary>The action-manager tick (mgr+0x4C); one per frame advanced.</summary>
    public long CurrentTick => _tick;

    /// <summary>Every action handled, in order, with its outcome.</summary>
    public IReadOnlyList<WwiseActionExecution> ActionLog => _log;

    /// <summary>Only the actions that executed, in execution order.</summary>
    public IReadOnlyList<WwiseActionExecution> ExecutedActions =>
        _log.Where(e => e.Outcome == WwiseActionOutcome.Executed).ToList();

    /// <summary>The global LCG this runtime draws from (delay ranges, probability, fade-in).</summary>
    public WwiseRng Rng => _rng;

    /// <summary>Registers a game object so the lookup at gapD D5.5 finds it.</summary>
    public void RegisterGameObject(uint id) => _registeredGameObjects.Add(id);

    /// <summary>Removes a game object; a lookup for it then returns null (gapD D5.5).</summary>
    public void UnregisterGameObject(uint id) => _registeredGameObjects.Remove(id);

    /// <summary>Whether a game object id is registered.</summary>
    public bool IsGameObjectRegistered(uint id) => _registeredGameObjects.Contains(id);

    /// <summary>
    /// Queued PostEvent (gapA 1.1/1.2). Looks the event up; not found → <see cref="InvalidPlayingId"/>
    /// and nothing enqueued. Otherwise assigns a playing id with an atomic increment of the global counter
    /// and reserves a type-1 message holding the event. Nothing executes on this thread.
    /// </summary>
    /// <param name="eventId">The event to post.</param>
    /// <param name="gameObjectId">The game object, or null; the pump resolves it (gapD D5.5).</param>
    /// <param name="targetPlayingId">ExecuteEvent's fourth argument (msg+0x10; gapD D5.1).</param>
    public uint PostEvent(uint eventId, uint? gameObjectId = null, uint targetPlayingId = 0)
    {
        if (!TryGetEvent(eventId, out _)) return InvalidPlayingId;             // gapA 1.2
        uint playingId = unchecked((uint)Interlocked.Increment(ref s_nextPlayingId));
        PlayingCountIncrement(playingId);                                     // the in-flight message, gapD D3.1
        _messages.Enqueue(new WwiseQueuedEvent(playingId, eventId, gameObjectId, targetPlayingId));
        return playingId;
    }

    /// <summary>
    /// The message pass (0x9ADFD8, gapA 1.3): each type-1 message resolves its game object and runs
    /// ExecuteEvent. Returns how many messages were pumped.
    /// </summary>
    public int PumpMessages()
    {
        int n = 0;
        while (_messages.Count > 0)
        {
            var m = _messages.Dequeue();
            ExecuteMessage(m);
            n++;
        }
        return n;
    }

    /// <summary>
    /// The drain (0x9A9F88, gapD D1.5): execute every pending action whose launch tick has arrived, in
    /// launch order (equal ticks FIFO). Returns how many executed.
    /// </summary>
    public int DrainDueActions()
    {
        int n = 0;
        while (_pending.Count > 0 && _pending[0].LaunchTick <= _tick)
        {
            var p = _pending[0];
            _pending.RemoveAt(0);
            ExecuteAction(p.Action, p.GameObjectId, p.Message, p.LaunchTick, p.Frames, p.DelaySamples, p.SubFrameRemainderSamples);
            PlayingCountDecrement(p.Message.PlayingId);                       // 0xA04F54
            n++;
        }
        return n;
    }

    /// <summary>
    /// One engine frame (Perform 0x9AF8A8, gapD D1.6): messages, then drain, then tick++. The render pass
    /// between drain and tick++ is the signal path (M6-017) and is not part of this control-path task.
    /// </summary>
    public void AdvanceFrame()
    {
        PumpMessages();
        DrainDueActions();
        _tick++;
    }

    /// <summary>Whether a playing id still has an outstanding event or action count (gapD D3.1).</summary>
    public bool IsPlaying(uint playingId) => _playing.ContainsKey(playingId);

    /// <summary>The minimal play count for a playing id (gapD D3.1); 0 when it is finished.</summary>
    public int OutstandingActionCount(uint playingId) =>
        _playing.TryGetValue(playingId, out var p) ? p.Outstanding : 0;

    // ---------------------------------------------------------------- the pump

    private void ExecuteMessage(WwiseQueuedEvent m)
    {
        uint? gameObj = ResolveGameObject(m.RawGameObjectId);                 // 0xA0C238, gapD D5.5
        ExecuteEvent(m, gameObj);
        PlayingCountDecrement(m.PlayingId);                                   // 0x9AF2AC
    }

    /// <summary>The game-object lookup (0xA0C238, gapD D5.5): registered → the object, else null.</summary>
    private uint? ResolveGameObject(uint? raw) =>
        raw is { } id && _registeredGameObjects.Contains(id) ? id : null;

    /// <summary>
    /// ExecuteEvent (0x9AA3DC, gapA 1.4): walk the event's actions in bank order. An object-scope action
    /// with no game object is skipped; a global-scope action runs with a null game object.
    /// </summary>
    private void ExecuteEvent(WwiseQueuedEvent m, uint? gameObj)
    {
        if (!TryGetEvent(m.EventId, out var ev)) return;
        foreach (uint actionId in WwiseBank.EventActions(ev))                 // gapA 2.2
        {
            if (!TryGetAction(actionId, out var action))
            {
                _log.Add(new WwiseActionExecution(actionId, 0, WwiseActionOutcome.NotParsed,
                    m.PlayingId, gameObj, 0, _tick, 0, 0, 0, 0, 0, 0));
                continue;
            }
            if (action.ObjectScope && gameObj is null)                        // gapA 1.4
            {
                _log.Add(new WwiseActionExecution(action.Id, action.Type,
                    WwiseActionOutcome.SkippedObjectScopeNoGameObject,
                    m.PlayingId, null, action.TargetId, _tick, 0, 0, 0, 0, 0, 0));
                continue;
            }
            EnqueueOrExecute(action, gameObj, m);
        }
    }

    /// <summary>
    /// EnqueueOrExecute (0x9AA0FC, gapA 1.5 / gapD D1.4): delay = GetDelay in samples; frames = delay /
    /// frame size with the sub-frame remainder kept; launch = tick + frames. frames == 0 executes now,
    /// otherwise the action is inserted into the pending list sorted by launch tick, equal ticks FIFO.
    /// </summary>
    private void EnqueueOrExecute(WwiseAction action, uint? gameObj, WwiseQueuedEvent m)
    {
        PlayingCountIncrement(m.PlayingId);                                   // 0xA04EDC
        long delay = GetDelay(action);
        int frameSize = WwiseRuntimeSettings.SamplesPerFrame;
        long frames = delay / frameSize;
        long remainder = delay - frames * frameSize;                          // queued+0xC
        long launch = _tick + frames;                                         // mgr+0x4C + frames
        if (frames == 0)
        {
            ExecuteAction(action, gameObj, m, launch, frames, delay, remainder);
            PlayingCountDecrement(m.PlayingId);                               // 0xA04F54
            return;
        }
        InsertPending(new PendingAction(action, gameObj, m, launch, frames, delay, remainder));
    }

    /// <summary>Sorted insert; equal launch ticks stay FIFO (gapD D1.4).</summary>
    private void InsertPending(PendingAction p)
    {
        int i = _pending.Count;
        while (i > 0 && _pending[i - 1].LaunchTick > p.LaunchTick) i--;
        _pending.Insert(i, p);
    }

    /// <summary>
    /// GetDelay (0xA61260, gapA 1.6): property 0x0F in samples (else the table default [g+0x3C], whose
    /// value is not recovered and is taken as 0), plus ranged 0x0F min, plus a rounded LCG draw over the
    /// range. Negative results cannot be a sample count, so they are clamped to 0.
    /// </summary>
    private long GetDelay(WwiseAction action)
    {
        long delay = action.DelaySamples ?? 0;
        if (action.RangedDelaySamples is { } range)
        {
            long span = range.Max - range.Min;
            long draw = (long)(0.5 + _rng.Next() / 2147483647.0 * span);
            delay += range.Min + draw;
        }
        return delay < 0 ? 0 : delay;
    }

    // ---------------------------------------------------------------- execute

    private void ExecuteAction(WwiseAction action, uint? gameObj, WwiseQueuedEvent m,
                               long launch, long frames, long delay, long remainder)
    {
        switch (action.Kind)
        {
            case 0x04: ExecutePlay(action, gameObj, m, launch, frames, delay, remainder); break;
            case 0x01: ExecuteStop(action, gameObj, m, launch, frames, delay, remainder); break;
            case 0x1E: ExecuteSeek(action, gameObj, m, launch, frames, delay, remainder); break;
            default:
                _log.Add(new WwiseActionExecution(action.Id, action.Type, WwiseActionOutcome.NotParsed,
                    m.PlayingId, gameObj, action.TargetId, launch, frames, delay, remainder, 0, 0, 0));
                break;
        }
    }

    /// <summary>
    /// Play execute (0xA62D38 → 0xA62A1C, gapA 1.10/1.11). Probability (absent from every shipped
    /// action) can skip it; then the target must resolve; then the fade-in and initial-delay parameters
    /// are built. PlayInternal and the fade-in application are RECOVERABLE_GAP and are not applied.
    /// </summary>
    private void ExecutePlay(WwiseAction action, uint? gameObj, WwiseQueuedEvent m,
                             long launch, long frames, long delay, long remainder)
    {
        if (action.FloatProp((byte)WwiseProp.Probability) is { } prob)        // prop 0x11, gapA 1.10
        {
            if (prob == 0f)
            {
                Record(action, WwiseActionOutcome.SkippedProbability, gameObj, m, launch, frames, delay, remainder);
                return;
            }
            double r = _rng.Next() / 2147483647.0 * 100.0;
            if (r > prob)
            {
                Record(action, WwiseActionOutcome.SkippedProbability, gameObj, m, launch, frames, delay, remainder);
                return;
            }
        }

        if (GetNode(action.TargetId) is null)                                 // 0xA6168C → 0x9A7EB0
        {
            Record(action, WwiseActionOutcome.TargetMissing, gameObj, m, launch, frames, delay, remainder);
            return;
        }

        // Fade-in (0xA61110): prop 0x10 in ms, not converted, plus ranged 0x10 min and a random draw, with
        // curve = +0x22 & 0x1F. The draw helper 0xA61110's body is not recovered (it is not the GetDelay
        // body at 0xA61260), so the ranged draw is not applied. No shipped action carries ranged 0x10
        // (gapA 5.4), so nothing observable is dropped; the property and curve are recorded.
        double fadeMs = action.FloatProp((byte)WwiseProp.TransitionTime) ?? 0;
        byte curve = action.Params is WwisePlayParams pp ? pp.FadeCurve : (byte)0;

        // Initial delay (0x9F12E0, gapA 1.11 / gapD D1.9): prop 0x3B is seconds · rate, rounded half away
        // from zero. The RTPC (0xA11590) and ranged terms are not applied.
        double initialDelay = 0;
        if (action.FloatProp((byte)WwiseProp.InitialDelay) is { } seconds)
            initialDelay = Math.Round(seconds * WwiseRuntimeSettings.MixRateHz, MidpointRounding.AwayFromZero);

        _log.Add(new WwiseActionExecution(action.Id, action.Type, WwiseActionOutcome.Executed,
            m.PlayingId, gameObj, action.TargetId, launch, frames, delay, remainder, fadeMs, curve, initialDelay));
    }

    /// <summary>
    /// Stop execute (0xA663C8, gapD D5.2). The voice stop 0xA79F08 is not recovered; what is recovered is
    /// the dispatch and the pending-action clear 0x9AB8AC: 0x0102/0x0103 clear the target's pending
    /// actions, 0x0104/0x0105 clear all, 0x0108/0x0109 clear the exceptions'.
    /// </summary>
    private void ExecuteStop(WwiseAction action, uint? gameObj, WwiseQueuedEvent m,
                             long launch, long frames, long delay, long remainder)
    {
        switch ((ushort)(action.Type - 0x0102))
        {
            case 0x0000 or 0x0001:                                            // 0x0102/0x0103
                if (GetNode(action.TargetId) is null)
                {
                    Record(action, WwiseActionOutcome.TargetMissing, gameObj, m, launch, frames, delay, remainder);
                    return;
                }
                ClearPendingForTarget(action.TargetId);
                break;
            case 0x0002 or 0x0003:                                            // 0x0104/0x0105
                _pending.Clear();
                break;
            case 0x0006 or 0x0007:                                            // 0x0108/0x0109
                ClearPendingForExceptions(action);
                break;
            default:
                Record(action, WwiseActionOutcome.NotParsed, gameObj, m, launch, frames, delay, remainder);
                return;
        }
        Record(action, WwiseActionOutcome.Executed, gameObj, m, launch, frames, delay, remainder);
    }

    /// <summary>
    /// Seek execute (0xA64B14 → 0xA645C8, gapD D5.3). Only the target resolution and its missing-target
    /// error 0x0F are recovered cleanly; the value formula's field mapping and the node seek handler are
    /// not applied.
    /// </summary>
    private void ExecuteSeek(WwiseAction action, uint? gameObj, WwiseQueuedEvent m,
                             long launch, long frames, long delay, long remainder)
    {
        switch ((ushort)(action.Type - 0x1E02))
        {
            case 0x0000 or 0x0001:                                            // 0x1E02/0x1E03
                if (GetNode(action.TargetId) is null)
                {
                    Record(action, WwiseActionOutcome.TargetMissing, gameObj, m, launch, frames, delay, remainder);
                    return;
                }
                break;
            default:
                Record(action, WwiseActionOutcome.NotParsed, gameObj, m, launch, frames, delay, remainder);
                return;
        }
        Record(action, WwiseActionOutcome.Executed, gameObj, m, launch, frames, delay, remainder);
    }

    private void ClearPendingForTarget(uint targetId) =>
        _pending.RemoveAll(p => p.Action.TargetId == targetId);

    private void ClearPendingForExceptions(WwiseAction action)
    {
        var exceptions = action.Params is WwiseStopParams sp ? sp.Exceptions : Array.Empty<(uint, bool)>();
        var ids = exceptions.Select(e => e.Id).ToHashSet();
        _pending.RemoveAll(p => ids.Contains(p.Action.TargetId));
    }

    // ---------------------------------------------------------------- bookkeeping

    private void Record(WwiseAction action, WwiseActionOutcome outcome, uint? gameObj, WwiseQueuedEvent m,
                        long launch, long frames, long delay, long remainder)
        => _log.Add(new WwiseActionExecution(action.Id, action.Type, outcome, m.PlayingId, gameObj,
            action.TargetId, launch, frames, delay, remainder, 0, 0, 0));

    private void PlayingCountIncrement(uint playingId)
    {
        if (!_playing.TryGetValue(playingId, out var p)) _playing[playingId] = p = new PlayingEvent();
        p.Outstanding++;
    }

    private void PlayingCountDecrement(uint playingId)
    {
        if (!_playing.TryGetValue(playingId, out var p)) return;
        if (--p.Outstanding <= 0) _playing.Remove(playingId);
    }

    private bool TryGetEvent(uint id, out WwiseObject ev)
    {
        if (_objects.TryGetValue(id, out var o) && o.Type == WwiseObjectType.Event)
        {
            ev = o;
            return true;
        }
        ev = null!;
        return false;
    }

    private bool TryGetAction(uint id, out WwiseAction action)
    {
        if (_actionCache.TryGetValue(id, out var cached))
        {
            action = cached!;
            return cached is not null;
        }
        var parsed = _objects.TryGetValue(id, out var o) ? WwiseAction.TryRead(o, out _) : null;
        _actionCache[id] = parsed;
        action = parsed!;
        return parsed is not null;
    }

    private WwiseNode? GetNode(uint id)
    {
        if (_nodeCache.TryGetValue(id, out var cached)) return cached;
        cached = _objects.TryGetValue(id, out var o) ? WwiseHierarchy.TryRead(o, out _) : null;
        _nodeCache[id] = cached;
        return cached;
    }

    private sealed record WwiseQueuedEvent(uint PlayingId, uint EventId, uint? RawGameObjectId, uint TargetPlayingId);

    private sealed record PendingAction(WwiseAction Action, uint? GameObjectId, WwiseQueuedEvent Message,
                                        long LaunchTick, long Frames, long DelaySamples, long SubFrameRemainderSamples);

    private sealed class PlayingEvent
    {
        public int Outstanding;
    }
}
