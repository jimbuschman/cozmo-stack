using System.Text.Json;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot;

public sealed partial class CozmoRobot
{
    /// <summary>Owns per-robot map load/save for the lifetime of an MCP worker.</summary>
    public IDisposable? OpenRoomMapSession() => Engine.Options.C2.MapSave ? new C2MapSession(Engine) : null;
}

internal sealed class C2MapSession : IDisposable
{
    private readonly CozmoEngine _engine;
    private Guid _loadedRevision;
    private long _epoch;
    private uint _serial;
    private bool _disposed;
    internal C2MapSession(CozmoEngine engine)
    {
        _engine = engine;
        using var lease = engine.EnterSynchronousOwner();
        engine.SerialNumberAcquired += Connected;
        if (engine.AcquiredSerialNumber is { } serial) Connected(serial);
    }

    // The manufacturing-ID handshake publishes the body serial before readiness.
    // Always load into disconnected coordinate frames, including when a numeric
    // origin or native object ID has been reused in the new session.
    private void Connected(uint serial)
    {
        if (_disposed || serial == 0 || _engine.Robot is not { } owner ||
            (_serial == serial && _epoch == owner.C2MapOwnerEpoch)) return;
        Save();
        _serial = serial; _epoch = owner.C2MapOwnerEpoch; _loadedRevision = Guid.Empty;
        try
        {
            if (!File.Exists(MapPath)) return;
            var file = Read();
            _engine.RoomMap.Restore(_epoch, file);
            _loadedRevision = file.Revision;
            Log($"loaded {file.Layers.Length} unanchored map layers for robot {serial:X8}");
        }
        catch (Exception e) when (StorageFailure(e)) { Log($"could not load robot {serial:X8}: {e.Message}"); }
    }

    private string MapPath => Path.Combine(_engine.Options.FilesPath, "room-maps", $"{_serial:X8}.json");
    private SavedRoom Read()
    {
        using var input = new FileStream(MapPath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        var data = JsonSerializer.Deserialize<SavedRoom>(input);
        if (data is null || !data.ValidFor(_serial)) throw new InvalidDataException("Invalid room-map file or robot serial.");
        return data;
    }

    private static bool StorageFailure(Exception e) => e is IOException or UnauthorizedAccessException or
        InvalidDataException or JsonException or TimeoutException;
    private void Log(string text) => _engine.EmitStackLog(CozmoC2Settings.MapSaveName + ": " + text);

    private void Save()
    {
        if (_serial == 0) return;
        var capture = _engine.RoomMap.Capture(_epoch, _serial);
        if (capture.Layers.Length == 0) return;
        string path = MapPath;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var mutex = new Mutex(false, $"Local\\CozmoStackRoomMap-{_serial:X8}");
            try { if (!mutex.WaitOne(TimeSpan.FromSeconds(5))) throw new TimeoutException("Map file is busy."); }
            catch (AbandonedMutexException) { /* Ownership was granted after a writer exited. */ }
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                // Preserve independent sessions that finished since our load. A
                // reader sees either the old or the complete new JSON document.
                if (File.Exists(path))
                {
                    var prior = Read();
                    if (prior.Revision != _loadedRevision) capture = capture.Merge(prior);
                }
                using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    JsonSerializer.Serialize(output, capture);
                    output.Flush(flushToDisk:true);
                }
                File.Move(temporary, path, overwrite:true);
            }
            finally
            {
                try { File.Delete(temporary); } finally { mutex.ReleaseMutex(); }
            }
            Log($"saved {capture.Layers.Length} map layers for robot {_serial:X8}");
        }
        catch (Exception e) when (StorageFailure(e)) { Log($"could not save robot {_serial:X8}: {e.Message}"); }
    }

    public void Dispose()
    {
        using var lease = _engine.EnterSynchronousOwner();
        if (_disposed) return;
        _disposed = true;
        _engine.SerialNumberAcquired -= Connected;
        Save();
    }
}
