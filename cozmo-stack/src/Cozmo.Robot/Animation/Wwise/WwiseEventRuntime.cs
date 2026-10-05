// fidelity: M6-006, M6-023, M6-025
using System.Buffers.Binary;

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
    private readonly Dictionary<uint, WwiseGameObjectRef> _gameObjects = new();
    private readonly Queue<WwiseQueuedMessage> _messages = new();
    private readonly List<PendingAction> _pending = new();
    private readonly List<WwiseActionExecution> _log = new();
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

    /// <summary>Type-1 queue messages (event posts) posted but not yet pumped (gapA 1.2).</summary>
    public int QueuedEventCount => _messages.Count(m => m is WwiseQueuedEvent);

    /// <summary>Every queue message not yet pumped: the type-1 event posts and the type 0x12 / 0x13 game-object messages (C40.4).</summary>
    public int QueuedMessageCount => _messages.Count;

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

    /// <summary>
    /// Registers a game object so the lookup at gapD D5.5 finds it. The object is the constructor's <c>0xA0B340..0xA0B420</c> (<see cref="WwiseGameObjectRef.Create0A0B340"/>) with the listener mask the registration stores through <c>0xA0BA28</c>
    /// (<see cref="WwiseGameObjectRef.SetListenerMaskA0BA28"/>); Anki's registration passes mask 1 (<c>0x8D8CA0 movs r2,#1</c>, C40.4 correction 5), the default here. The registration message handler <c>0xA0C2B8</c> itself (its hash insert and what it does for an id that is already registered)
    /// is read only for the two <c>0xA0BA28</c> call sites, so registering an id twice keeps the first object and does not store the mask again.
    /// </summary>
    // fidelity: M6-010
    public WwiseGameObjectRef RegisterGameObject(uint id, byte listenerMask = 1)
    {
        if (_gameObjects.TryGetValue(id, out var existing)) return existing;
        var go = WwiseGameObjectRef.Create0A0B340(id);
        go.SetListenerMaskA0BA28(listenerMask);
        _gameObjects[id] = go;
        return go;
    }

    /// <summary>The registered game object, or null (the lookup <c>0xA0CAFC</c> / <c>0xA0CB78</c> make: a miss returns 2 and the message does nothing).</summary>
    public WwiseGameObjectRef? FindGameObject(uint id) => _gameObjects.GetValueOrDefault(id);

    /// <summary>
    /// The M6-025 Play -> PBI -> voice -> source bridge. When set, a Play whose target resolves is handed
    /// to <see cref="IWwisePlaybackBridge.OnPlay"/> after the fade-in/initial-delay params are built
    /// (M6-025 B1); when null the Play stops where M6-006 stopped (the target is recorded, nothing is
    /// created).
    /// </summary>
    public IWwisePlaybackBridge? PlaybackBridge { get; set; }

    /// <summary>Removes a game object; a lookup for it then returns null (gapD D5.5).</summary>
    public void UnregisterGameObject(uint id) => _gameObjects.Remove(id);

    /// <summary>Whether a game object id is registered.</summary>
    public bool IsGameObjectRegistered(uint id) => _gameObjects.ContainsKey(id);

    /// <summary>
    /// Queued PostEvent (<c>0x9A6704 -> 0x9A0EF8</c>, gapA 1.1/1.2, C40.1). Looks the event up; not found → <see cref="InvalidPlayingId"/> and nothing enqueued. Otherwise assigns a playing id with an atomic increment of the global counter, writes the playing-id entry
    /// with <c>0xA03108</c> (<see cref="WwisePlayingIdTable.CreateEntryA03108"/>: the event id, the game object id, the callback, the cookie and the flags, <c>[+0x1C] = 1</c> for the in-flight message) and reserves a type-1 message holding the event. Nothing executes on this thread. An entry whose
    /// creation fails (<c>0xA03108</c> returns 2) drops the message and returns 0 (<c>0x9A1040..0x9A1058</c>). Only the <c>numExternals == 0</c> path of <c>0x9A6704</c> is built (it tail-calls <c>0x9A0EF8</c>, <c>0x9A6704..0x9A6714</c>): with external sources it first builds a holder with
    /// <c>0x9A65C0</c>, which is unread, so the C# has no external-source parameter. The Anki callback context is <paramref name="callback"/> and <paramref name="cookie"/>; the playing-id counter's start value (<c>[g+0xF8]</c>) is not in any row (it starts at 0 here).
    /// </summary>
    /// <param name="eventId">The event to post.</param>
    /// <param name="gameObjectId">The game object, or null; the pump resolves it (gapD D5.5). Stored raw at <c>[item+0x24]</c> (a null leaves it unset).</param>
    /// <param name="targetPlayingId">ExecuteEvent's fourth argument (msg+0x10; gapD D5.1).</param>
    /// <param name="flags">The Wwise callback flags (<c>[item+0x48]</c>). Anki's wrapper <c>0x8D8CE4</c> passes only 0, 1, 5, 9 or 13 (C40.1 correction 11).</param>
    /// <param name="callback">The callback (<c>[item+0x40]</c>; Anki's is <c>0x8D8D41</c>); null clears the callback-less flag bits.</param>
    /// <param name="cookie">The cookie (<c>[item+0x44]</c>; Anki's is the callback context pointer).</param>
    // fidelity: M6-006, M6-016
    public uint PostEvent(uint eventId, uint? gameObjectId = null, uint targetPlayingId = 0,
                          uint flags = 0, Action<int, object>? callback = null, object? cookie = null)
    {
        if (!TryGetEvent(eventId, out _)) return InvalidPlayingId;             // gapA 1.2: 0x9A0F30..0x9A0F40
        uint playingId = unchecked((uint)Interlocked.Increment(ref s_nextPlayingId));   // 0x9A0FE4..0x9A1008
        if (PlayingIds.CreateEntryA03108(playingId, eventId, gameObjectId, callback, cookie, flags) != 1)   // 0x9A103C bl 0xA03108; 0x9A1040 cmp r0,#1
            return InvalidPlayingId;                                           // 0x9A1048..0x9A1058: the refs dropped, [msg+2] = 0x38, return 0
        _messages.Enqueue(new WwiseQueuedEvent(playingId, eventId, gameObjectId, targetPlayingId));
        return playingId;
    }

    /// <summary>
    /// <c>0x9A0354(goId, pairs, n)</c> (C40.4 T-A1): <c>n &gt; 4</c> returns 0x1F and queues nothing; otherwise it queues message type 0x12 (<c>[msg+4] = goId</c>, <c>[msg+0xC] = n</c>, the pairs copied) and returns 1. Anki's <c>0x8D913E</c> reports success only for 1 (and 0 when its engine flag
    /// <c>this[0]</c> is clear). The queued message is handled in the pump (<c>0x9AEDE0 -> 0xA0CAFC -> 0xA0BA3C</c>).
    /// </summary>
    // fidelity: M6-010
    public int SetGameObjectAuxSendValuesA9A0354(uint gameObjectId, IReadOnlyList<(uint BusId, float Gain)> pairs)
    {
        ArgumentNullException.ThrowIfNull(pairs);
        if (pairs.Count > 4) return 0x1F;                                      // 0x9A0354 cmp r2,#4; bls; mov r0,#0x1f
        _messages.Enqueue(new WwiseQueuedAuxSendValues(gameObjectId, pairs.ToArray()));   // 0x9A037C mov r1,#0x12; 0x9A039C..0x9A03A8
        return 1;
    }

    /// <summary>
    /// The Anki wrapper <c>0x8D913E</c> (tail-called by <c>0x8D290C</c>, which first tests its own byte at <c>[this+4]</c>; C40.4 T-A1): <paramref name="engineReady"/> is the byte <c>[this]</c>, which when zero returns 0 (<c>0x8D9142..0x8D914A</c>, <c>0x8D91B4</c>); otherwise it passes the pairs to
    /// <c>0x9A0354</c> (an empty list as a null pointer and a count of 0, <c>0x8D91A2..0x8D91A8</c>) and returns 1 only for the engine's result 1 (<c>0x8D91AE cmp r5,#1; movne r5,#0</c>), so 5 pairs (0x1F) give 0.
    /// </summary>
    // fidelity: M6-010, M6-016
    public bool SetGameObjectAuxSendValuesA8D913E(bool engineReady, uint gameObjectId, IReadOnlyList<(uint BusId, float Gain)> pairs)
        => engineReady && SetGameObjectAuxSendValuesA9A0354(gameObjectId, pairs) == 1;

    /// <summary>The Anki wrapper <c>0x8D91BA</c> (<c>SetGameObjectOutputBusVolume</c>): <paramref name="engineReady"/> (<c>[this]</c>) zero returns 0 (<c>0x8D91D0</c>); otherwise <c>0x9A044C(goId, v)</c> and 1 only for the engine's result 1 (<c>0x8D91C8..0x8D91CC</c>).</summary>
    // fidelity: M6-010, M6-016
    public bool SetGameObjectOutputBusVolumeA8D91BA(bool engineReady, uint gameObjectId, float volume)
        => engineReady && SetGameObjectOutputBusVolumeA9A044C(gameObjectId, volume) == 1;

    /// <summary><c>0x9A044C(goId, v)</c> (C40.4 T-A2b): queues message type 0x13 (<c>[msg+4] = goId</c>, <c>[msg+0x14] = v</c>, <c>[msg+0xC] = -1</c>) and returns 1. Its handler is <c>0x9AEDBC -> 0xA0CB78</c>.</summary>
    // fidelity: M6-010
    public int SetGameObjectOutputBusVolumeA9A044C(uint gameObjectId, float volume)
    {
        _messages.Enqueue(new WwiseQueuedOutputBusVolume(gameObjectId, volume));   // 0x9A0460 mov r1,#0x13; 0x9A0480..0x9A0488
        return 1;
    }

    /// <summary>
    /// The message pass (0x9ADFD8, gapA 1.3, dispatch table <c>0x9AE0B8</c>): in queue order, type 1 resolves its game object and runs ExecuteEvent, type 0x12 stores the game object's aux send values (<c>0x9AEDE0 -> 0xA0CAFC -> 0xA0BA3C</c>) and type 0x13 its output bus volume
    /// (<c>0x9AEDBC -> 0xA0CB78</c>) (C40.1, C40.4). Returns how many messages were pumped.
    /// </summary>
    // fidelity: M6-006, M6-010
    public int PumpMessages()
    {
        int n = 0;
        while (_messages.Count > 0)
        {
            var m = _messages.Dequeue();
            switch (m)
            {
                case WwiseQueuedEvent e: ExecuteMessage(e); break;
                case WwiseQueuedAuxSendValues a:                                  // 0x9AEDE0: 0xA0CAFC(mgr, [msg+4], msg+0x10, [msg+0xC]); a miss returns 2 and the handler ignores it
                    FindGameObject(a.GameObjectId)?.SetAuxValuesA0BA3C(a.Pairs, a.Pairs.Length);
                    break;
                case WwiseQueuedOutputBusVolume v:                                // 0x9AEDBC: 0xA0CB78(mgr, [msg+4], [msg+0x14], [msg+0xC])
                    if (FindGameObject(v.GameObjectId) is { } go)
                    {
                        go.Volume60 = v.Volume;                                   // 0xA0CBE8 str r7,[r5,#0x60]
                        go.Key78 = -1;                                            // 0xA0CBEC str r6,[r5,#0x78] (r6 = [msg+0xC] = -1)
                    }
                    break;
            }
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
    public bool IsPlaying(uint playingId) => PlayingIds.Find(playingId) is not null;

    /// <summary>The outstanding message and action count <c>[item+0x1C]</c> of a playing id (gapD D3.1); 0 when it is finished.</summary>
    public int OutstandingActionCount(uint playingId) => PlayingIds.Find(playingId)?.Count1C ?? 0;

    /// <summary>
    /// The playing-id entries of the callback manager (C31.4 R4.1, C31.1 R1.5): <c>[item+0x1C]</c> counts the control path's messages and actions, <c>[item+0x18]</c> the PBIs' references. The PBI's reference and release
    /// (<see cref="RegisterPbiPlayingId"/>, <see cref="ReleasePbiPlayingIdReference"/>) and the EndOfEvent body <c>0xA03618</c> live in <see cref="WwisePlayingIdTable"/>.
    /// </summary>
    // fidelity: M6-026
    public WwisePlayingIdTable PlayingIds { get; } = new();

    // ---------------------------------------------------------------- the pump

    private void ExecuteMessage(WwiseQueuedEvent m)
    {
        uint? gameObj = ResolveGameObject(m.RawGameObjectId);                 // 0xA0C238, gapD D5.5
        ExecuteEvent(m, gameObj);
        PlayingCountDecrement(m.PlayingId);                                   // 0x9AF2AC
    }

    /// <summary>The game-object lookup (0xA0C238, gapD D5.5): registered → the object, else null.</summary>
    private uint? ResolveGameObject(uint? raw) =>
        raw is { } id && _gameObjects.ContainsKey(id) ? id : null;

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

        var node = GetNode(action.TargetId);                                  // 0xA6168C → 0x9A7EB0
        if (node is null)
        {
            Record(action, WwiseActionOutcome.TargetMissing, gameObj, m, launch, frames, delay, remainder);
            return;
        }

        // Fade-in (0xA61110): prop 0x10 in ms, not converted, plus ranged 0x10 min and a random draw, with
        // curve = +0x22 & 0x1F. The draw helper 0xA61110's body is not recovered (it is not the GetDelay
        // body at 0xA61260), so the ranged draw is not applied. No shipped action carries ranged 0x10
        // (gapA 5.4), so nothing observable is dropped; the property and curve are recorded.
        double fadeMs = action.FloatProp((byte)WwiseProp.TransitionTime) ?? 0;
        byte curve = action.Params is WwisePlayParams wp ? wp.FadeCurve : (byte)0;

        // Initial delay (0x9F12E0, gapA 1.11 / gapD D1.9): prop 0x3B is seconds · rate, rounded half away
        // from zero. The RTPC (0xA11590) and ranged terms are not applied.
        double initialDelay = 0;
        if (action.FloatProp((byte)WwiseProp.InitialDelay) is { } seconds)
            initialDelay = Math.Round(seconds * WwiseRuntimeSettings.MixRateHz, MidpointRounding.AwayFromZero);

        // M6-025 B1: the Play helper resolves the target and calls node->vt+0x128(node, params). The
        // params struct it builds is handed to the bridge; the fields the M6-006 rows did not carry
        // (the queued action's custom params and the uninitialised 0x44-byte block) stay at their
        // documented gaps rather than being invented here.
        if (PlaybackBridge is { } bridge)
        {
            var init = new WwisePlayInitParams
            {
                TargetNodeId = action.TargetId,                               // params+4 (0xA62B90)
                // params+8 (0xA62BF0) = [actionctx+0x34]: the registered object for an object-scope action, else 0 (M6-026 5.3, C29.7).
                GameObjectId = action.ObjectScope ? gameObj : null,
                GameObjectRef = action.ObjectScope && gameObj is { } registered ? FindGameObject(registered) : null,   // the registered object 0xA0C238 found (C40.3: 0x9BC90C takes a reference on it)
                Transition = new WwiseFadeInTransition                         // params+0xC (0xA62BFC)
                {
                    FadeInTime = (float)fadeMs,                               // 0xA62AF0
                    FadeCurve = curve,                                        // 0xA62A8C
                },
                PlayingId = m.PlayingId,                                      // params+0x24 (0xA62B94)
                InitialDelaySamples = unchecked((uint)initialDelay),          // params+0x74 (0xA62BC8)
                Flags128 = (byte)(0x04 | (action.IsBus ? 0x08 : 0)),          // 0xA62BCC / 0xA62C04
            };
            bridge.OnPlay(node, m.PlayingId, gameObj, init);
        }

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

    /// <summary>
    /// <c>0xA04D48</c> (M6-026 6.2, C31.1 R1.5): a PBI takes a reference on its playing-id entry (<c>[entry+0x18]++</c>) and the entry's callback flags <c>[entry+0x48]</c> are stored to <c>pbi+4</c>; nothing changes when the
    /// entry is not there.
    /// </summary>
    // fidelity: M6-026
    public void RegisterPbiPlayingId(WwisePlayingInstance pbi) => PlayingIds.RegisterPbiA04D48(pbi);

    /// <summary>
    /// <c>0xA04DE8</c> (M6-026 1.10 step 5): Term releases that reference: <c>[entry+0x18]--</c>, then <c>0xA03618</c> (<see cref="WwisePlayingIdTable.EndOfEventA03618"/>), whose body does nothing while either counter is non-zero
    /// and otherwise needs the unread callees of <see cref="WwisePlayingIdTable.Seams"/>.
    /// </summary>
    // fidelity: M6-026
    public void ReleasePbiPlayingIdReference(uint playingId) => PlayingIds.ReleaseA04DE8(playingId);

    private void PlayingCountIncrement(uint playingId) => PlayingIds.GetOrCreate(playingId).Count1C++;

    private void PlayingCountDecrement(uint playingId)
    {
        var item = PlayingIds.Find(playingId);
        if (item is null) return;
        item.Count1C--;                                                       // 0xA04F54: [item+0x1C]--, then the tail call 0xA03618
        PlayingIds.EndOfEventA03618(item, playingId);                         // 0xA04F54 always tail-calls 0xA03618; its unread callees are required seams
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

    /// <summary>
    /// The parsed node (or bus) for an object id, cached; the node graph the playback-limit walker follows (M6-026: <c>[node+0x34]</c> parent,
    /// <c>[node+0x38]</c> output bus). Same lookup as the Play target resolution (<c>0xA6168C</c> -&gt; <c>0x9A7EB0</c>).
    /// </summary>
    // fidelity: M6-026
    public WwiseNode? FindNode(uint id) => GetNode(id);

    private WwiseNode? GetNode(uint id)
    {
        if (_nodeCache.TryGetValue(id, out var cached)) return cached;
        cached = _objects.TryGetValue(id, out var o) ? WwiseHierarchy.TryRead(o, out _) : null;
        _nodeCache[id] = cached;
        return cached;
    }

    private abstract record WwiseQueuedMessage;

    /// <summary>Message type 1 (<c>0x9AF244</c>).</summary>
    private sealed record WwiseQueuedEvent(uint PlayingId, uint EventId, uint? RawGameObjectId, uint TargetPlayingId) : WwiseQueuedMessage;

    /// <summary>Message type 0x12 (<c>0x9AEDE0</c>).</summary>
    private sealed record WwiseQueuedAuxSendValues(uint GameObjectId, (uint BusId, float Gain)[] Pairs) : WwiseQueuedMessage;

    /// <summary>Message type 0x13 (<c>0x9AEDBC</c>).</summary>
    private sealed record WwiseQueuedOutputBusVolume(uint GameObjectId, float Volume) : WwiseQueuedMessage;

    private sealed record PendingAction(WwiseAction Action, uint? GameObjectId, WwiseQueuedEvent Message,
                                        long LaunchTick, long Frames, long DelaySamples, long SubFrameRemainderSamples);

}

// =====================================================================================================
// M6-023: the app audio-input dispatch
//
// Unity PostAudioEvent -> AudioUnityInput -> AudioMuxInput -> AudioMultiplexer ->
// AudioEngineController::PostAudioEvent -> the M6-006 Wwise PostEvent. The whole path is source-backed
// (rows A1-A5 on the Unity side, B1-B6 on the native side); the Wwise core it reaches is
// WwiseEventRuntime above, so this stays the production control path.
//
// Explicitly not settled by the frozen rows, and therefore not invented here:
// <list type="bullet">
// <item><b>The callback context's first word.</b> B5 says AudioMultiplexer::ProcessMessage builds a
// context when callbackId != 0, and 0x008DED46/0x008DED4C store 0xff as its first word (0x008DED48 stores
// 0, the queued flag). The app path therefore supplies 0xff by default; a caller may still override the
// bits explicitly for a context built another way.</item>
// <item><b>Carrying the Wwise callback/cookie and the flags.</b> B6 passes callback 0x008D8D41, cookie ctx
// and the computed flags into the core PostEvent. WwiseEventRuntime.PostEvent has no callback and no flags
// seam (its third argument is <c>targetPlayingId</c>), so the callback id and the flags are recorded but
// not delivered; delivering them is a later M6-006 wiring step.</item>
// <item><b>The app-side callers.</b> A1's PlaySound.Play/Update and A2's GameAudioClient.PostAudioEvent
// are not modelled; WwiseAppAudioClient starts at A3's UnityAudioClient.PostEvent.</item>
// </list>
// =====================================================================================================

/// <summary>
/// The app's callback request (Unity <c>AudioCallbackFlag</c>, row A3). The app sends a callback id only
/// when the flag is not <see cref="EventNone"/>.
/// </summary>
[Flags]
public enum WwiseAudioCallbackFlag : byte
{
    EventNone = 0,
    EventDuration = 1,
    EventMarker = 2,
    EventComplete = 4,
    EventAll = 7,
    EventError = 255,
}

/// <summary>The <c>MessageGameToEngine</c> tags AudioUnityInput's ctor subscribes to (rows A5, B1): 1..6.</summary>
public enum WwiseGameToEngineTag : ushort
{
    PostAudioEvent = 1,
    StopAllAudioEvents = 2,
    PostAudioGameState = 3,
    PostAudioSwitchState = 4,
    PostAudioParameter = 5,
    PostAudioMusicState = 6,
}

/// <summary>The <c>MessageAudioClient</c> union tag (row A4): tag 0 is PostAudioEvent.</summary>
public enum WwiseAudioClientTag : byte
{
    PostAudioEvent = 0,
}

/// <summary>
/// The PostAudioEvent wire message (row A4): <c>u32 audioEvent, u32 gameObject, u16 callbackId</c>,
/// <c>Size = 10</c>. Little-endian, the platform's order.
/// </summary>
public readonly record struct WwisePostAudioEvent(uint AudioEvent, uint GameObject, ushort CallbackId)
{
    /// <summary>The message's wire size (A4: <c>Size = 10</c>).</summary>
    public const int Size = 10;

    /// <summary>Reads the ten-byte body. A shorter span is refused rather than padded.</summary>
    public static WwisePostAudioEvent Read(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < Size)
            throw new InvalidDataException($"PostAudioEvent is {Size} bytes, not {bytes.Length}");
        return new WwisePostAudioEvent(
            BinaryPrimitives.ReadUInt32LittleEndian(bytes),
            BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(4, 4)),
            BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(8, 2)));
    }

    /// <summary>Writes the ten-byte body in the row's field order (A4).</summary>
    public void Write(Span<byte> bytes)
    {
        if (bytes.Length < Size)
            throw new ArgumentException($"PostAudioEvent needs {Size} bytes, not {bytes.Length}", nameof(bytes));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, AudioEvent);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(4, 4), GameObject);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.Slice(8, 2), CallbackId);
    }

    /// <summary>The ten-byte body, for a caller that has nowhere to write it.</summary>
    public byte[] ToBytes()
    {
        var bytes = new byte[Size];
        Write(bytes);
        return bytes;
    }
}

/// <summary>
/// The Unity side of the dispatch (rows A1-A3): the app's own play-id counter, which is separate from
/// Wwise's, and the callback-id rule.
/// </summary>
public sealed class WwiseAppAudioClient
{
    private ushort _previousPlayId;

    /// <param name="previousPlayId">
    /// The counter's starting value. The field defaults to 0 (A3), which makes the first allocated id 1.
    /// </param>
    public WwiseAppAudioClient(ushort previousPlayId = 0) => _previousPlayId = previousPlayId;

    /// <summary>The last id <see cref="AllocatePlayId"/> returned, or the seed before the first call.</summary>
    public ushort PreviousPlayId => _previousPlayId;

    /// <summary>
    /// <c>_GetPlayId</c> (row A3): increment, and if the increment wraps to 0, increment again so 0 is
    /// never handed out.
    /// </summary>
    public ushort AllocatePlayId()
    {
        _previousPlayId++;
        if (_previousPlayId == 0) _previousPlayId++;
        return _previousPlayId;
    }

    /// <summary>
    /// <c>UnityAudioClient.PostEvent</c> (rows A2-A3): allocate the app play id, set the callback id to it
    /// only when the flag is not <see cref="WwiseAudioCallbackFlag.EventNone"/>, and build the wire
    /// message. The app play id is the app's counter; it is not Wwise's playing id.
    /// </summary>
    public WwisePostAudioEvent PostEvent(uint audioEvent, uint gameObject,
                                         WwiseAudioCallbackFlag callbackFlag = WwiseAudioCallbackFlag.EventNone)
    {
        ushort playId = AllocatePlayId();
        ushort callbackId = callbackFlag != WwiseAudioCallbackFlag.EventNone ? playId : (ushort)0;
        return new WwisePostAudioEvent(audioEvent, gameObject, callbackId);
    }
}

/// <summary>
/// The callback context AudioMultiplexer::ProcessMessage builds when callbackId != 0 (row B5). Its first
/// word is 0xff (0x008DED46/0x008DED4C), with 0 at +0x38 (queued, 0x008DED48); <see cref="CallbackId"/> is
/// the cookie the row records. A null context is the callbackId == 0 branch (0x008D8CEC), where the Wwise
/// PostEvent flags are 0 (0x008D8D30), not the formula.
/// </summary>
public readonly record struct WwiseAudioCallbackContext(ushort CallbackId, byte ContextBits)
{
    /// <summary>
    /// The Wwise PostEvent flags for a non-null context (row B6):
    /// <c>1 | (ctx&amp;2)&lt;&lt;1 | (ctx&amp;1)&lt;&lt;3</c>. Bit 0 (EndOfEvent) is always set; the
    /// context's bit 0 becomes flag bit 3 and its bit 1 becomes flag bit 2.
    /// </summary>
    public static byte PostEventFlagsFor(byte contextBits) =>
        (byte)(1 | ((contextBits & 2) << 1) | ((contextBits & 1) << 3));

    /// <summary>
    /// The flags the native wrapper passes for a context, or 0 when there is none. FUN_008D8CE4 tests the
    /// context at 0x008D8CEC and its null branch sets the flags to 0 at 0x008D8D30; only the non-null
    /// branch runs the formula. This keeps a null context from picking up
    /// <see cref="PostEventFlagsFor(byte)"/>'s always-set bit 0.
    /// </summary>
    public static byte PostEventFlagsFor(WwiseAudioCallbackContext? context) =>
        context is { } c ? c.PostEventFlags : (byte)0;

    /// <summary>This context's flags, from its own low bits.</summary>
    public byte PostEventFlags => PostEventFlagsFor(ContextBits);
}

/// <summary>
/// The callback context as an identity: the engine passes the context's pointer as the cookie (<c>0x8D8CF4</c>, <c>0x8D8D18..0x8D8D32</c>), so the playing-id entry's <c>[+0x44]</c> holds this object, not a number derived from it.
/// </summary>
public sealed class WwiseAudioCallbackContextObject
{
    /// <summary>The context's values (row B5).</summary>
    public WwiseAudioCallbackContext Context { get; }

    /// <summary>Creates the identity for a context.</summary>
    public WwiseAudioCallbackContextObject(WwiseAudioCallbackContext context) => Context = context;
}

/// <summary>What the engine's audio-input dispatch did with one envelope.</summary>
public enum WwiseAudioInputOutcome
{
    /// <summary>Envelope tag 1 (PostAudioEvent): parsed and routed to <see cref="WwiseEventRuntime.PostEvent"/>.</summary>
    Posted,
    /// <summary>Envelope tags 2..6 are subscribed (row B1) but are other message types, not handled by M6-023.</summary>
    SubscribedNotHandled,
    /// <summary>The envelope tag is outside the subscribed 1..6 set.</summary>
    NotSubscribed,
}

/// <summary>One dispatch result: the outcome, the envelope tag, the core's playing id and the context.</summary>
public readonly record struct WwiseAudioInputResult(
    WwiseAudioInputOutcome Outcome, ushort EnvelopeTag, uint PlayingId, ushort CallbackId,
    WwiseAudioCallbackContext? Context, byte PostEventFlags);

/// <summary>
/// The app audio-input dispatch (M6-023, rows B1-B6): AudioUnityInput's subscription and tag switch, then
/// AudioMuxInput::HandleMessage -> AudioMultiplexer::ProcessMessage -> AudioEngineController::PostAudioEvent
/// -> the M6-006 Wwise PostEvent. This is the production control path; it queues on
/// <see cref="WwiseEventRuntime"/> and returns the core's playing id, exactly as 0x009A6704 does.
/// </summary>
public sealed class WwiseAudioInputDispatch
{
    private readonly WwiseEventRuntime _runtime;

    public WwiseAudioInputDispatch(WwiseEventRuntime runtime)
        => _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));

    /// <summary>
    /// The Wwise callback the wrapper <c>0x8D8CE4</c> registers when the Post carries a context (<c>0x8D8D41</c>, the Anki trampoline: M6-016 6.2 queues <c>{ctx, info}</c> for the drain and calls <c>0x9A206C</c> for EndOfEvent). Its body is the Anki callback side, which this dispatch
    /// does not build: the callback is a host input and a Post with a context throws without it (a null callback would silently clear the EndOfEvent flag, <c>0xA031C8</c>).
    /// </summary>
    // fidelity: M6-023
    public Action<int, object>? Callback8D8D41 { get; set; }

    /// <summary>The envelope tags AudioUnityInput's ctor subscribes to (row B1): 1..6.</summary>
    public static IReadOnlyList<WwiseGameToEngineTag> SubscribedTags { get; } = new[]
    {
        WwiseGameToEngineTag.PostAudioEvent,
        WwiseGameToEngineTag.StopAllAudioEvents,
        WwiseGameToEngineTag.PostAudioGameState,
        WwiseGameToEngineTag.PostAudioSwitchState,
        WwiseGameToEngineTag.PostAudioParameter,
        WwiseGameToEngineTag.PostAudioMusicState,
    };

    /// <summary>
    /// <c>AudioUnityInput::HandleGameEvents</c> (row B2): switch on the envelope's u16 tag. Case 1 parses
    /// the ten-byte PostAudioEvent, builds the callback context when callbackId != 0 (row B5), computes the
    /// PostEvent flags (row B6) and routes to <see cref="WwiseEventRuntime.PostEvent"/>; cases 2..6 are the
    /// other subscribed types, recognised but not handled here; anything else is not subscribed.
    /// </summary>
    /// <param name="envelopeTag">The <c>MessageGameToEngine</c> tag (row A5).</param>
    /// <param name="body">The envelope body; for tag 1 it is the ten-byte PostAudioEvent.</param>
    /// <param name="callbackContextBits">
    /// The callback context's low bits. The app path stores 0xff as the context's first word
    /// (0x008DED46/0x008DED4C), so that is the default; a caller may override it for a context built
    /// another way.
    /// </param>
    public WwiseAudioInputResult HandleGameEvents(ushort envelopeTag, ReadOnlySpan<byte> body,
                                                  byte callbackContextBits = 0xff)
    {
        if (envelopeTag is < (ushort)WwiseGameToEngineTag.PostAudioEvent
                          or > (ushort)WwiseGameToEngineTag.PostAudioMusicState)
            return new WwiseAudioInputResult(WwiseAudioInputOutcome.NotSubscribed, envelopeTag, 0, 0, null, 0);

        if (envelopeTag != (ushort)WwiseGameToEngineTag.PostAudioEvent)
            return new WwiseAudioInputResult(WwiseAudioInputOutcome.SubscribedNotHandled, envelopeTag, 0, 0, null, 0);

        var ev = WwisePostAudioEvent.Read(body);                                       // row A4/B2
        WwiseAudioCallbackContext? ctx = ev.CallbackId != 0                            // row B5
            ? new WwiseAudioCallbackContext(ev.CallbackId, callbackContextBits)
            : null;
        // FUN_008D8CE4: the null-context branch sets the flags to 0 (0x008D8D30); only a non-null context
        // runs the formula (row B6). The flags reach the entry (C40.1): 0, 1, 5, 9 or 13.
        byte flags = WwiseAudioCallbackContext.PostEventFlagsFor(ctx);                  // row B6 / 0x008D8CEC

        // A context carries the Wwise callback 0x008D8D41 and the cookie ctx (0x008D8D0C..0x008D8D32); no context passes a null callback and cookie 0 (0x008D8CEC..0x008D8D30).
        Action<int, object>? callback = null;
        object? cookie = null;
        if (ctx is not null)
        {
            callback = Callback8D8D41 ?? throw new WwiseMissingBehaviourException(
                "M6-023 B6: a callback context makes the wrapper register the Anki trampoline 0x8D8D41 as the Wwise callback (the entry keeps the EndOfEvent flag only with a callback, 0xA031C8); supply WwiseAudioInputDispatch.Callback8D8D41");
            cookie = new WwiseAudioCallbackContextObject(ctx.Value);                   // 0x8D8CF4 strd r3,lr,[sp]; 0x8D8D18..0x8D8D32: the cookie is the context pointer itself
        }

        // AudioMuxInput::HandleMessage(PostAudioEvent) -> AudioMultiplexer::ProcessMessage ->
        // AudioEngineController::PostAudioEvent -> Wwise PostEvent. ExecuteEvent's fourth argument
        // (WwiseEventRuntime.PostEvent's targetPlayingId) is not set by this path, so it is 0.
        uint playingId = _runtime.PostEvent(ev.AudioEvent, ev.GameObject, 0, flags, callback, cookie);   // M6-006
        return new WwiseAudioInputResult(WwiseAudioInputOutcome.Posted, envelopeTag, playingId,
                                         ev.CallbackId, ctx, flags);
    }
}
