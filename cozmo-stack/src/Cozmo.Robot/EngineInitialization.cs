using System.Text;
using Cozmo.Robot.Behavior;
using Cozmo.Transport;

namespace Cozmo.Robot;

// These are native callee inputs, not implementations of the unread callees.
internal sealed class EngineInitRecipients
{
    internal Func<FirmwareJsonValue, int>? UiInit;
    internal Func<int>? InitInternal;
    internal Action? LoadRobotConfigs;
    internal NativeNeedsInitInputs? Needs;
    internal FirmwareJsonValue? DasConfig;
}

internal sealed record NativeNeedsInitInputs(FirmwareJsonValue Config160, FirmwareJsonValue Config178,
    FirmwareJsonValue Config190, FirmwareJsonValue Config1A8, FirmwareJsonValue Config1C0,
    FirmwareJsonValue Config1D8, Action<float, NativeNeedsInitInputs> Initialize);

public sealed partial class CozmoEngine
{
    // fidelity: M1-054
    internal bool NativeInitialized10;
    internal FirmwareJsonValue? NativeEngineConfig18;
    internal EngineInitRecipients NativeInitRecipients { get; } = new();

    // Null means a missing join, and never a successful native result. The checked range
    // ends at the RobotManager call; its later success/initialized stores are not claimed.
    internal int? InitChecked(FirmwareJsonValue config)
    {
        if (NativeInitialized10)
            EmitSourceLog(EngineLogLevel.Info, "Unnamed", "CozmoEngine.Init.ReInit",
                "Reinitializing already-initialized CozmoEngineImpl with new config.");
        NativeInitialized10 = false; // unconditional 0x004EC7F6, before config assignment/gates.
        NativeEngineConfig18 = Copy(config);
        if (NativeEngineConfig18.Kind is not (FirmwareJsonKind.Object or FirmwareJsonKind.Null))
            return Missing("Json.isMember non-object input at 0x004EC81C: malformed-root callee behavior is not checked; no error/result/crash invented");
        foreach (string member in new[] { "AdvertisingHostIP", "RobotAdvertisingPort", "UiAdvertisingPort" })
        {
            if (NativeEngineConfig18.Kind == FirmwareJsonKind.Object && NativeEngineConfig18.Members.ContainsKey(Convert.ToHexString(Encoding.UTF8.GetBytes(member)))) continue;
            Error("CozmoEngine.Init", $"No {member} defined in Json config.");
            return 1;
        }
        var calls = NativeInitRecipients;
        if (calls.UiInit is not { } ui) return Missing("UiMessageHandler.Init 0x006601B0, called at 0x004EC84C");
        int result = ui(NativeEngineConfig18);
        if (result != 0) { Error("CozmoEngine.Init", "Error initializing UIMessageHandler"); return result; }
        if (calls.InitInternal is not { } init) return Missing("InitInternal vslot+8 0x004EDC6C, called at 0x004EC970");
        result = init();
        if (result != 0) { Error("CozomEngine.Init", "Failed calling internal init."); return result; }
        if (calls.LoadRobotConfigs is not { } load) return Missing("full RobotDataLoader.LoadRobotConfigs 0x00522280, called at 0x004EC9CE; existing individual loaders do not establish the full call");
        load();
        Experiments.Init(); // built callee; no extra adapter between these checked units.
        float time = Timer.SecondsF; // getInstance/GetCurrentTimeInSeconds, 0x004EC9DA..DE.
        if (calls.Needs is not { } needs) return Missing("NeedsManager.Init 0x004ECA0A: six loader JSON values +160/+178/+190/+1A8/+1C0/+1D8 and native Init recipient");
        needs.Initialize(time, needs);
        if (calls.DasConfig is not { } das) return Missing("RobotManager.Init 0x004ECA1A: loader JSON +220 recipient");
        Robots.InitCheckedDasTail(das, _nowNs);
        return Missing("CozmoEngine.Init after RobotManager.Init 0x004ECA1A: remaining tail/result/initialized producer outside checked range");
    }

    private int? Missing(string detail)
    {
        SteppedBehavior.ReportMissing("M1-054 " + detail);
        return null;
    }
    private void Error(string key, string text)
    {
        EmitSourceLog(EngineLogLevel.Error, null, key, text);
        EngineErrorState.StoreAndMaybeBreak();
    }
    private void InitCheckedHostConfig()
    {
        if (Options.EngineConfigJson is not { } bytes)
        {
            SteppedBehavior.ReportMissing("M1-054 CozmoEngine.Init 0x004EC7B4: host engine-config JSON not supplied; no invented advertising defaults");
            return;
        }
        // Host supplies the already selected engine config; parsing is the existing checked reader.
        try
        {
            if (Json.TryParseFirst(bytes, out var document)) InitChecked(document.RootElement);
            else SteppedBehavior.ReportMissing("M1-054 host engine-config JSON could not be parsed; no native Init result invented");
        }
        catch (Exception e) when (e is JsonLogicError or JsonRuntimeError or JsonMissingSource)
        { SteppedBehavior.ReportMissing("M1-054 host engine-config reader: " + e.Message); }
    }
}
