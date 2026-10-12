namespace Cozmo.Robot;
public sealed partial class CozmoEngine
{
    // fidelity: M5-059, M5-060, M5-061
    internal static string AnimationCacheFile(string cache, string name)
        => cache + (name.StartsWith('/') ? "" : "/") + name;
    // fidelity: M5-061 — unreachable app/SDK inputs retain concrete quiet subscription owners.
    private RetainedSubscription? _transferFileSubscription, _dasSdkSubscription, _deviceDataSubscription;
    private readonly List<RetainedSubscription> _debugConsoleSubscriptions = new();
    private void InitAnimationTransfer()
    {
        _deviceDataSubscription = AnimationMessages.Subscribe(0xDB, false);
        _transferFileSubscription = AnimationMessages.Subscribe(0xF7, false);
        CleanUpAnimationTransfer();
        for (ushort tag = 0xDC; tag <= 0xDF; ++tag) _debugConsoleSubscriptions.Add(AnimationMessages.Subscribe(tag, false));
        _dasSdkSubscription = AnimationMessages.Subscribe(0xE0, false);
    }
    private void DestroyAnimationTransferMembers()
    {
        CleanUpAnimationTransfer();
        _transferFileSubscription?.Dispose(); _transferFileSubscription = null;
        _dasSdkSubscription?.Dispose(); _dasSdkSubscription = null;
        for (int i = _debugConsoleSubscriptions.Count - 1; i >= 0; --i) _debugConsoleSubscriptions[i].Dispose();
        _debugConsoleSubscriptions.Clear();
        _deviceDataSubscription?.Dispose(); _deviceDataSubscription = null;
    }
    private void CleanUpAnimationTransfer()
    {
        string path = AnimationCacheFile(Options.AnimationCachePath, "TestAnim.json");
        // File.Exists excludes directories; no log, just stat/S_ISREG then remove.
        // As in native remove(), a failed removal does not become a source log or an engine exception.
        if (!File.Exists(path)) return;
        try { File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        // M5-061: no TransferFile sender, so +0x14 directory name stays empty; no directory cleanup.
    }
}
