using Cozmo.Protocol;

namespace Cozmo.Robot.Behavior;

// fidelity: M7-023
public sealed partial class MoodState
{
    private readonly object _lifetimeGate = new();
    private readonly Dictionary<(string ActionType, string ResultCategory), string> _actionResultEvents;
    private readonly List<RetainedSubscription> _subscriptions = new();
    private EngineRobot? _nativeRobot; // borrowed Robot+12C input
    private EngineRobot? _lifetimeOwner; // host identity of the enclosing Robot+440 owner
    private int _actionCallbackId;
    private bool _nativeRetired;
    internal Action<string>? RetirementObservedForTest;
    internal bool NativeRetired { get { lock (_lifetimeGate) return _nativeRetired; } }
    internal int ActionCallbackId { get { lock (_lifetimeGate) return _actionCallbackId; } }
    internal IReadOnlyList<uint> HistoryCounts { get { lock (_lifetimeGate) return _history.Select(h => h.Count).ToArray(); } }
    internal IReadOnlyList<int> HistoryStorageLengths { get { lock (_lifetimeGate) return _history.Select(h => h.StorageLength).ToArray(); } }
    internal RetainedSubscription? LastSubscriptionForTest { get { lock (_lifetimeGate) return _subscriptions.LastOrDefault(); } }
    internal int SubscriptionCount { get { lock (_lifetimeGate) return _subscriptions.Count; } }
    internal void AddSubscriptionForTest(RetainedSubscription handle) { lock (_lifetimeGate) _subscriptions.Add(handle); }
    internal void BindNativeLifetime(EngineRobot native, int actionCallbackId, bool initialized=true)
    {
        LogCallbackDeferral.RunGated(native.MoodLifetimeGate, () =>
        {
            LogCallbackDeferral.RunGated(_lifetimeGate, () =>
            {
                if (native.Lifetime.Destroying || _nativeRetired || (_lifetimeOwner is not null && !ReferenceEquals(native, _lifetimeOwner)) || native.MoodOwner is { } previous && !ReferenceEquals(previous, this))
                { SteppedBehavior.ReportMissing("M7-023 replacement/shared Mood owner construction is not established; retired or different MoodState is not rebound as the native owner"); return; }
                _lifetimeOwner=native; _nativeRobot = native; _actionCallbackId = actionCallbackId; native.MoodOwner = this;
                if (initialized && native.HasExternalInterface && _subscriptions.Count==0)
                    _subscriptions.Add(native.Engine.AnimationMessages.SubscribeMessage(0xC3, false, HandleMoodMessage));
                native.Lifetime.Bind(0x440, _ => DestroyForRobot());
            });
        });
    }
    internal void DestroyForRobot()
    {
        LogCallbackDeferral.RunGated(_lifetimeGate, () =>
        {
            if (_nativeRetired) { StackDiagnostics.Write("M1-034/M7-023: skipped repeated MoodManager destruction after free at 0x0067AE74 / 0x005111BA; retired state retained"); return; }
            // Short-circuit L4. Normal caller already nulled Robot+250; the unreachable unregister branch is not built.
            if (_actionCallbackId != 0 && _nativeRobot is not null && _nativeRobot.ActionListOwner is not null)
            { SteppedBehavior.ReportMissing("M7-023 normal destruction requires already-null ActionList at 0x00511150 before 0x0067AE24; live-list unregister branch is unreachable by the checked caller and is not built"); return; }
            while (_subscriptions.Count > 0)
            {
                var handle = _subscriptions[^1]; _subscriptions.RemoveAt(_subscriptions.Count - 1); handle.Dispose();
            }
            RetirementObservedForTest?.Invoke("subscriptions");
            _completionDisabled.Clear(); RetirementObservedForTest?.Invoke("completion set");
            _actionResultEvents.Clear(); RetirementObservedForTest?.Invoke("action event map");
            _lastEventSec.Clear(); RetirementObservedForTest?.Invoke("event times");
            for (int i = 8; i >= 0; i--) { _history[i].RetireStorage(); RetirementObservedForTest?.Invoke("history:" + i); }
            _nativeRetired = true; RetirementObservedForTest?.Invoke("allocation");
            // Skipped unregister leaves callback-id bytes unchanged; no final mood report or static model teardown.
        });
    }
    // fidelity: M7-012, M7-020
    // The app's MoodMessageUnion.cs writes an 8-bit variant, then the selected
    // message body. The native handler dispatches these four arms at 0x0067BC66.
    private void HandleMoodMessage(object? payload)
    {
        if (payload is not byte[] { Length: > 0 } bytes || _nativeRobot is not { } owner) return;
        using var execution = owner.Engine.EnterSynchronousOwner();
        if (owner.Lifetime.Destroying || !ReferenceEquals(owner.Engine.Robot, owner) || !ReferenceEquals(owner.MoodOwner, this)) return;
        var reader = new CladReader(bytes);
        byte tag = reader.U8();
        switch (tag)
        {
            case 0:
                SendEmotionsToGame(); // 0x0067BC76..0x0067BC7E
                break;
            case 1 when reader.Remaining >= 5:
            {
                byte emotion = reader.U8();
                float value = reader.F32();
                if (emotion >= _values.Length) return;
                // Emotion::SetValue at 0x006796C8 writes the raw float and clears the decay clock.
                LogCallbackDeferral.RunGated(_lifetimeGate, () =>
                {
                    if (_nativeRetired) return;
                    _values[emotion] = value;
                    _decaySec[emotion] = 0f;
                });
                break;
            }
            case 2 when reader.Remaining >= 6:
            {
                byte emotion = reader.U8();
                float delta = reader.F32();
                string key = reader.String8();
                if (reader.LastReadFailed || emotion >= _values.Length) return;
                int nul = key.IndexOf('\0'); // AddToEmotion takes a char* and calls strlen at 0x0067BDE6.
                if (nul >= 0) key = key[..nul];
                LogCallbackDeferral.RunGated(_lifetimeGate, () =>
                {
                    if (_nativeRetired) return;
                    // 0x0067BEDC..0x0067BF0A: use the event's graph if the key
                    // is registered, otherwise the shipped default graph.
                    float elapsed = RecordEventTime(key, owner.Engine.Timer.SecondsF);
                    var evt = _model.Event(key);
                    var graph = evt is null ? _model.DefaultRepetitionPenalty : evt.RepetitionPenalty;
                    float penalty = graph is null ? 1f : (float)graph.At(elapsed);
                    Add(emotion, penalty * delta); // 0x0067BE16..0x0067BE22
                });
                break;
            }
            case 3:
                string name = reader.String8();
                if (!reader.LastReadFailed) Trigger(name, owner.Engine.Timer.SecondsF); // 0x0067BCD2..0x0067BCEC
                break;
            case > 3:
                owner.Engine.EmitSourceLog(EngineLogLevel.Error, null, "MoodManager.HandleMoodEvent.UnhandledMessageUnionTag", $"Unexpected tag {tag}");
                Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak(); // 0x0067BCFE..0x0067BD38
                break;
        }
    }
    private bool RejectRetiredUse()
    {
        if (!_nativeRetired) return false;
        StackDiagnostics.Write("M1-034/M7-023: skipped retained-host use of MoodManager after native allocation retirement at 0x0067AE74 / Robot delete 0x005111BA; prior inline snapshot retained"); return true;
    }
}
