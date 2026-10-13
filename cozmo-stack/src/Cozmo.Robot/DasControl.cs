using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Cozmo.Robot;

// fidelity: M3-051, M3-055
// Sink (b) is deliberately absent. This is the library control endpoint, not an uploader.
internal sealed record DasControlCall(string Name, object?[] Arguments);
internal sealed record DasSourcePath(int? Scope, string RelativePath);
internal sealed class DasControlStandIn : IDisposable
{
    internal Action<DasControlCall>? CallObserved; // test observer, no source-log entry.
    internal uint DisabledReasons { get; private set; }
    internal bool Paused { get; private set; } = true; // libDAS constructor 0xB254..0xB260.
    private readonly Dictionary<string, string> _globals = new();
    private readonly object _globalGate = new();
    private readonly BlockingCollection<Action> _work = new();
    private Thread? _worker;
    private readonly object _workerGate = new();
    internal string? GameAppenderName { get; private set; }
    internal uint GlobalsRevision { get; private set; }

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
        lock (_globalGate)
        {
            // 0x801C..0x8092: $game replaces the game-log appender, or closes it on clear.
            // M3-051 excludes the appender's file/transfer work; retain its selected name only.
            if (name == "$game") GameAppenderName = string.IsNullOrEmpty(value) ? null : value;
            if (string.IsNullOrEmpty(value)) _globals.Remove(name); else _globals[name] = value;
            GlobalsRevision = unchecked(GlobalsRevision + 1); // 0x80DA..0x80F6.
        }
        Call("_DAS_SetGlobal", name, value);
    }
    internal IReadOnlyDictionary<string, string> GetGlobalsForThisRun()
    {
        Call("_DAS_GetGlobalsForThisRun");
        lock (_globalGate) return new Dictionary<string, string>(_globals);
    }
    internal void ForceFlushNow() => Call("DASForceFlushNow");
    internal void ForceFlushWithCallback(Action<bool, string> callback)
    {
        Call("DASForceFlushWithCallback");
        lock (_workerGate)
        {
            if (_worker is null)
            {
                _worker = new Thread(() => { foreach (var work in _work.GetConsumingEnumerable()) work(); })
                    { IsBackground = true, Name = "cozmo-das" };
                _worker.Start();
            }
            _work.Add(() => callback(false, "")); // M3-051: failed transfer, on the serial worker.
        }
    }
    // Offline test seams wait for startup callbacks before attaching later assertions.
    // Production startup never waits; every completion still runs on the worker.
    internal void WaitForPendingCallbacksForTest()
    {
        using var done = new ManualResetEventSlim();
        lock (_workerGate) { if (_worker is null) return; _work.Add(done.Set); }
        done.Wait();
    }
    public void Dispose()
    {
        Thread? worker;
        lock (_workerGate) { _work.CompleteAdding(); worker = _worker; }
        if (worker is not null && worker != Thread.CurrentThread) worker.Join();
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
        // fidelity: M3-058 — persisted host device-ID replaces DAS platform+0x1C input.
        string deviceId = LoadDasDeviceId();
        Das.SetGlobal("$player_id", Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(deviceId))));
        Das.ForceFlushWithCallback((ok, response) =>
        {
            EmitSourceLog(EngineLogLevel.Event, null, ok ? "das.upload" : "das.upload.fail", "live");
            if (DasStartupFlushCompleted is { } completed) DeliverIsolated(() => completed(ok, response));
        });
        // fidelity: M3-059 — background transfer app trigger is never supplied; task paths unreachable.
    }
    // fidelity: M3-058
    internal static string? DasDeviceIdPathForTest; // null in production; explicit test fixture input.
    private string LoadDasDeviceId()
    {
        string path = Options.DasDeviceIdPath ?? DasDeviceIdPathForTest ?? Path.Combine(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "cozmo-stack"), "device-id");
        try
        {
            if (File.Exists(path))
            {
                using var reader = new StreamReader(path, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: false);
                if (reader.ReadLine() is { } stored) return stored; // empty line is an ID; zero bytes is null.
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
        string id = Guid.NewGuid().ToString(); // Java UUID.randomUUID().toString(), lowercase v4.
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            using var file = new FileStream(path, FileMode.Create, FileAccess.Write);
            using var writer = new StreamWriter(file, new UTF8Encoding(false)) { NewLine = ((char)10).ToString() };
            writer.WriteLine(id);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
        return id; // DAS.java returns the new ID even when mkdir/create/write fails.

    }

    // fidelity: M3-056 — host supplies the app's RequestDataCollectionOption bool, default off at startup.
    public void RequestDataCollectionOption(bool enabled)
        => PostGameMessage(() => { if (enabled) Das.EnableNetwork(2); else Das.DisableNetwork(2); });
}
