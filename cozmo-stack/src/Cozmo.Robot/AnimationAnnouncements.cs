using Cozmo.Robot.Behavior;

namespace Cozmo.Robot;

/// <summary>The engine's AnimationAvailable payload, preserving std::string bytes.</summary>
public sealed record AnimationAvailableMessage(ReadOnlyMemory<byte> Name);
public sealed record AnimationEndOfMessage(byte Value);

// Container order is the native unordered_map linked traversal order. No C# dictionary
// enumeration, filename sorting, or insertion-order policy supplies this missing producer.
internal sealed record NativeAnimationNameNode(byte[] Name, NativeAnimationNameNode? Next = null);
internal sealed record NativeAnimationNames(NativeAnimationNameNode? Head);
internal interface IAnimationAnnouncementRecipient
{
    void Available(byte[] name);
    void End(byte value);
}

internal sealed partial class RobotManager
{
    // fidelity: M1-055
    internal NativeAnimationNames? NativeCannedAnimationNames;
    internal Func<IAnimationAnnouncementRecipient?>? AnnouncementRecipient;

    // M1-055: Init-time public announcement excluded (operator2026-10-17).
    // OV126 routine6 checks its local consumers; retain this internal join without inventing a recipient.
    internal Action? InitTimeAnimationAnnouncementsInternal;
    internal void NotifyInitTimeAnimationAnnouncements()
    {
        StackDiagnostics.Write("not supported (M1-055): Init-time public animation announcements; internal consumer join retained for OV126 routine6");
        InitTimeAnimationAnnouncementsInternal?.Invoke();
    }

    internal bool BroadcastAvailableAnimations()
    {
        if (AnnouncementRecipient is not { } recipient)
        {
            SteppedBehavior.ReportMissing("M1-055 BroadcastAvailableAnimations 0x0052E8A2: context external-interface owner join is unbound");
            return false;
        }
        if (recipient() is null) return true; // before GetAnimationNames, End, or Debug.
        if (NativeCannedAnimationNames is not { } container)
        {
            SteppedBehavior.ReportMissing("M1-055 CannedAnimationContainer.GetAnimationNames 0x00588190: native hash traversal/population producer is unbuilt; no fabricated empty list");
            return false;
        }
        var names = new List<byte[]>(); // full vector copy precedes the first send.
        foreach (var name in Walk(container.Head))
            if (!name.AsSpan().SequenceEqual("_PROCEDURAL_"u8)) names.Add(name.ToArray());
        foreach (var name in names)
        {
            if (recipient() is not { } output) return LostRecipient();
            output.Available(name); // synchronous vslot+1C; receiver reloaded each time.
        }
        if (recipient() is not { } end) return LostRecipient();
        end.End(0);
        _engine.EmitSourceLog(EngineLogLevel.Debug, "Unnamed", "RobotManager.BroadcastAvailableAnimations", "Supposedly sent EndOfMessage");
        return true;
    }
    private static IEnumerable<byte[]> Walk(NativeAnimationNameNode? node)
    { for (; node is not null; node = node.Next) yield return node.Name; }
    private static bool LostRecipient()
    {
        StackDiagnostics.Write("M1-034/M1-055: skip broadcast tail after null external-interface reload at 0x0052E8BC/0x0052E8D4; original dereferences null in 0x0052F888/0x0052F92A");
        return false;
    }
}

public sealed partial class CozmoEngine
{
    // fidelity: M1-055
    public event Action<AnimationAvailableMessage>? AnimationAvailable;
    public event Action<AnimationEndOfMessage>? AnimationAnnouncementsEnded;
    private sealed class AnnouncementOutput(CozmoEngine engine) : IAnimationAnnouncementRecipient
    {
        public void Available(byte[] name)
        {
            var payload = new AnimationAvailableMessage(name);
            engine.NativeUiDeliveryCount=unchecked(engine.NativeUiDeliveryCount+1);
            if (engine.AnimationAvailable is { } handlers)
                foreach (Action<AnimationAvailableMessage> handler in handlers.GetInvocationList()) engine.RunIsolated(()=>handler(payload));
            engine.AnimationMessages.Deliver(0x5D, true, payload);
            // M1-055: raw CLAD serializer0x0071624E is excluded phone/game-message plumbing; typed C# API delivers the payload.
        }
        public void End(byte value)
        {
            var payload = new AnimationEndOfMessage(value);
            if (engine.AnimationAnnouncementsEnded is { } handlers)
                foreach (Action<AnimationEndOfMessage> handler in handlers.GetInvocationList()) engine.RunIsolated(()=>handler(payload));
            engine.DeliverNurture(0x9A, new[] { value }, payload);
        }
    }
    private void InitAnnouncementOutput()
    {
        var output = new AnnouncementOutput(this);
        Robots.AnnouncementRecipient = () => output;
    }

    // The native loading-state entry and loader result have no live producers yet.
    internal Func<bool>? NativeNonConfigLoadingActive;
    internal Func<(int Result, float Progress)>? NativeDoNonConfigDataLoading;
    internal Action<byte>? NativeSetEngineState;
    // True means this is the loading branch; the same tick must not run state 3's body.
    internal bool UpdateCheckedNonConfigLoading()
    {
        if (NativeNonConfigLoadingActive is not { } active)
        {
            SteppedBehavior.ReportMissing("M1-055 CozmoEngine.Update 0x004ED5F0: native non-config loading state entry producer is unbound");
            return false;
        }
        if (!active()) return false;
        if (NativeDoNonConfigDataLoading is not { } load)
        {
            SteppedBehavior.ReportMissing("M1-055 DoNonConfigDataLoading 0x00523570, called at 0x004ED5FA: native loader status/progress recipient unbound");
            return true;
        }
        var loading=load();_lastPolledProgress=loading.Progress;
        if (loading.Result != 1) return true;
        if (!Robots.BroadcastAvailableAnimations()) return true;
        if (NativeSetEngineState is { } set) set(3);
        else SteppedBehavior.ReportMissing("M1-055 SetEngineState(3) at 0x004ED60E ->0x004ECCBC: native state-transition owner/report/log body unbuilt");
        return true;
    }
}
