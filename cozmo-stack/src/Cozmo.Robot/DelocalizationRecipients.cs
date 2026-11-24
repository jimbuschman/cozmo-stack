using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
namespace Cozmo.Robot;

// fidelity: M11-072 — the checked fields of each container-owned CantHandleTallStack.
// The host behavior binds to the manager's same data; this is not another behavior runner.
internal sealed class TallStackDelocalizationData
{
    internal bool HasStoredPose;
    // Legacy host facade snapshot; native target lifetime uses its own explicit control block.
    internal StackOfCubes? Stack;
    internal ITallStackWeakTarget? Target;
    internal bool TargetKnown = true; // constructor creates a known empty weak pair.
    internal Cozmo.Robot.Vision.Pose3d? StoredPose;
    internal void ClearStack() { Target = null; TargetKnown = true; Stack = null; }
    internal void Clear()
    {
        HasStoredPose = false; // +0x124, before the virtual ClearStack.
        ClearStack(); // +0x11C/+0x120; no action cancellation.
    }
}

public sealed partial class EngineRobot
{
    internal bool BlockConfigurationForceUpdate;
    internal TallStackDelocalizationData[] TallStackContainers { get; private set; } = Array.Empty<TallStackDelocalizationData>();
    private readonly SubscriptionOwners _configurationDelocalization = new(), _managerDelocalization = new(),
        _systemManagerDelocalization = new(), _cubeLightDelocalization = new();
    private void AddDelocalizationRecipient(SubscriptionOwners subscriptions, string name, Action handler)
    {
        subscriptions.Add(Engine.AnimationMessages.Subscribe(0x32, true, () =>
        {
            Engine.DelocalizationRecipientObservedForTest?.Invoke(name);
            handler();
        }));
    }
    private bool HasConfiguredBehavior(string id)
        => Enum.TryParse<BehaviorID>(id, false, out var behavior) && Engine.HostBehaviorMap?.ContainsKey(behavior) == true;

    // R2..R5 construction order. Host input replaces the already loaded native config map.
    // fidelity: M11-072 — 0x0050FC66/0x0050FCAC/0x0050FCBE/0x0050FDDE.
    internal void InitTallStackRecipientStorage()
    {
        if (Engine.HostBehaviorMap?.TryGetValue(BehaviorID.CantHandleTallStack, out var config) == true
            && config.TryGetProperty("behaviorClass", out var type) && type.ValueKind == System.Text.Json.JsonValueKind.String
            && type.GetString() == "CantHandleTallStack")
            TallStackContainers = new[] { new TallStackDelocalizationData(), new TallStackDelocalizationData() };
    }
    internal void InitDelocalizationRecipients()
    {
        Engine.BeforeConstructorDelocalizeForTest?.Invoke(this);
        if (!HasExternalInterface) return;
        AddDelocalizationRecipient(_configurationDelocalization, "BlockConfigurationManager", () =>
        {
            BlockConfigurationForceUpdate = true; // 0x006172A6..0x006172B6.
            Engine.DelocalizationConfigurationHook?.Invoke();
        });
        for (int i = 0; i < TallStackContainers.Length; ++i)
        {
            var state = TallStackContainers[i];
            AddDelocalizationRecipient(i == 0 ? _managerDelocalization : _systemManagerDelocalization,
                $"CantHandleTallStack[{i}]", state.Clear); // 0x005ED240..0x005ED258 -> 0x005ED1E4.
        }
        AddDelocalizationRecipient(_cubeLightDelocalization, "CubeLightComponent", () => Engine.DelocalizationCubeHook?.Invoke());
    }
    // Native InitReactionTriggerMap is after construction Delocalize (0x00510324 then 0x00510396).
    internal void InitDelocalizationStrategyRecipients()
    {
        if (!HasExternalInterface) return;
        foreach (var entry in Engine.HostReactionMap)
        {
            // Native 0x005A1872 deletes a strategy whose target behavior was not loaded.
            if (!HasConfiguredBehavior(entry.BehaviorId)) continue;
            string? name = entry.Trigger switch {
                ReactionTrigger.FacePositionUpdated => "FacePositionUpdated",
                ReactionTrigger.ObjectPositionUpdated => "ObjectPositionUpdated",
                ReactionTrigger.CubeMoved => "CubeMoved",
                _ => null,
            };
            if (name is null) continue;
            // R6/R7 reset only a temporary copy; R8's tag-32 path is a no-op, including a disabled trigger.
            // No persistent strategy/reaction data is cleared here.
            AddDelocalizationRecipient(_managerDelocalization, name, () => { });
        }
    }
    // The Robot destructor releases these owners in this order: +44, +48, BlockWorld (including
    // the configuration manager vector at 0x0061CD70), then cube-light handles at 0x00511478.
    internal void RetireManagerDelocalization() => _managerDelocalization.Retire();
    internal void RetireSystemManagerDelocalization() => _systemManagerDelocalization.Retire();
    internal void RetireConfigurationDelocalization() => _configurationDelocalization.Retire();
    internal void RetireCubeLightDelocalization() => _cubeLightDelocalization.Retire();
    internal void ReleaseDelocalizationStorage()
    {
        RetireManagerDelocalization(); RetireSystemManagerDelocalization();
        RetireConfigurationDelocalization(); RetireCubeLightDelocalization();
    }
}

public sealed partial class CozmoEngine
{
    internal Action<string>? DelocalizationRecipientObservedForTest;
    internal Action<EngineRobot>? BeforeConstructorDelocalizeForTest;
}
