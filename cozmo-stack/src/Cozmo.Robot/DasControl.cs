namespace Cozmo.Robot;

// fidelity: M3-051, M3-055
// Sink (b) is deliberately absent. This is the library control endpoint, not an uploader.
internal sealed record DasControlCall(string Name, object?[] Arguments);
internal sealed record DasSourcePath(int? Scope, string RelativePath);
internal sealed class DasControlStandIn
{
    internal Action<DasControlCall>? CallObserved; // test observer, no source-log entry.
    internal uint DisabledReasons { get; private set; }
    internal bool Paused { get; private set; } = true; // libDAS constructor 0xB254..0xB260.
    private readonly Dictionary<string, string> _globals = new();
    private void Call(string name, params object?[] args) => CallObserved?.Invoke(new(name, args));
    internal void SetArchiveLogConfig(uint archive, uint unarchive, string suffix)
        => Call("DASSetArchiveLogConfig", archive, unarchive, suffix);
    // These are source resource descriptors. Absolute roots are app inputs, not fabricated paths.
    internal void Configure(DasSourcePath config, DasSourcePath logs, DasSourcePath gameLogs)
        => Call("DASConfigure", config, logs, gameLogs);
    internal void InitAndroid(object context) => Call("DASPlatform_Android.Init", context);
    internal void InitForUnityPlayer() => Call("DASPlatform_Android.InitForUnityPlayer");
    internal void NativeInit(string game) => Call("DASNativeInit", game);
    internal void EnableNetwork(uint reason) { DisabledReasons &= ~reason; Call("DASEnableNetwork", reason); }
    internal void DisableNetwork(uint reason) { DisabledReasons |= reason; Call("DASDisableNetwork", reason); }
    internal void Pause(bool pause)
    {
        bool wasPaused = Paused;
        Paused = pause;
        Call("DASPauseUploadingToServer", pause);
        if (wasPaused && !pause) Call("Appender.Flush"); // libDAS 0xBCF0..0xBD00.
    }
    internal void SetGlobal(string name, string? value)
    {
        if (value is null) _globals.Remove(name); else _globals[name] = value;
        Call("_DAS_SetGlobal", name, value);
    }
    internal IReadOnlyDictionary<string, string> GetGlobalsForThisRun()
    {
        Call("_DAS_GetGlobalsForThisRun");
        return new Dictionary<string, string>(_globals);
    }
    internal void ForceFlushNow() => Call("DASForceFlushNow");
    internal void ForceFlushWithCallback(Action<bool, string> callback)
    {
        Call("DASForceFlushWithCallback");
        callback(false, ""); // operator's M3-051 ruling; no sink (b), always failed transfer.
    }
}

public sealed partial class CozmoEngine
{
    // fidelity: M3-055, M3-056
    internal DasControlStandIn Das { get; } = new();
    private bool _dasStarted;
    internal Action<bool, string>? DasStartupFlushCompleted;
    private void StartDasControl()
    {
        if (_dasStarted) return;
        _dasStarted = true;
        Das.SetArchiveLogConfig(0x00666C04, 0x00666E44, ".tar.gz"); // 0x00665A4E.
        if (Options.DataCollectionEnabled) Das.EnableNetwork(2); else Das.DisableNetwork(2);
        Das.Configure(new(null, "/DASConfig.json"), new(2, "DASLogs"), new(3, ""));
        EmitStackLog("MISSING: M3-055 DASConfigure absolute resource roots and DASPlatform_Android.Init / InitForUnityPlayer app input selection (0x00665CD6, 0x00665EEC/0x00665F14); source descriptors retained, no Android object or host path invented");
        Das.NativeInit("cozmo"); // 0x00665F3A, game literal at0x00BFE7BE.
        Das.Pause(false);
        MissingDasPlayerId("startup", 0x00665FAC);
        Das.ForceFlushWithCallback((ok, response) =>
        {
            DasStartupFlushCompleted?.Invoke(ok, response);
            EmitStackLog("MISSING: M3-055 startup flush callback recipient0x00667748 (vptr0x010301C4 invoke slot+0x18); stand-in returns failure, native engine callback reaction is not in the checked rows");
        });
        EmitStackLog("MISSING: M3-055 DasTransferTask.OnTransferReady caller/input at0x006A25FE; startup flush completes as failed transfer, no task behavior invented");
    }
    private void MissingDasPlayerId(string caller, uint address)
        => EmitStackLog($"MISSING: M3-055 {caller} $player_id MD5 input at0x{address:X8}; no digest or global value invented");

    // fidelity: M3-056 — host supplies the app's RequestDataCollectionOption bool, default off at startup.
    public void RequestDataCollectionOption(bool enabled)
        => PostGameMessage(() => { if (enabled) Das.EnableNetwork(2); else Das.DisableNetwork(2); });
}
