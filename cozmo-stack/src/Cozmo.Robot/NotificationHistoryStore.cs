using System.Text.Json;

namespace Cozmo.Robot;

// fidelity: M15-111
// Host replacement for the app's SegmentationService data behind SetDeterminantData (tag 0xD6). Rules, from
// unity/scripts/csharp/Cozmo.Util.Segmentation/SegmentationService.cs: InstallDate is local midnight on first launch
// and persists (UpdateAppInstallTime, no platform timestamp); ids 36, 14 and 84 start at InstallDate
// (InitUnlockedTrickDateTimeMap); an unlock records its time only if the id has none (UpdateUnlockedTrickDateTimeMap);
// unset entries send 0 and false (SendDataToEngine). HasPlayed stays false: only app game and spark UI flows set it.
internal sealed class NotificationHistoryStore
{
    internal const string FileName="notificationHistory.json";
    private const int Count=85;
    private sealed record Stored(int InstallSeconds,int[] UnlockTimestamps,bool[] HasPlayed);
    private readonly object _gate=new();
    private readonly string _path;
    private readonly Action<string> _log;
    private readonly int _install;
    private readonly int[] _timestamps;
    private readonly bool[] _played;
    private NotificationHistoryStore(string path,Action<string> log,Stored data)
    {_path=path;_log=log;_install=data.InstallSeconds;_timestamps=data.UnlockTimestamps;_played=data.HasPlayed;}

    /// <summary>Loads the file, or creates it on first run. A corrupt or unreadable file is logged and recreated.</summary>
    internal static NotificationHistoryStore LoadOrCreate(string directory,long nowMicros,Action<string> log)
    {
        string path=Path.Combine(directory,FileName);
        if(File.Exists(path))
        {
            try
            {
                var stored=JsonSerializer.Deserialize<Stored>(File.ReadAllText(path));
                if(stored is {UnlockTimestamps.Length:Count,HasPlayed.Length:Count})return new(path,log,stored);
                log($"M15-111 notification history {path} has the wrong shape; recreating it, so install date and unlock times restart");
            }
            catch(Exception e) when(e is JsonException or IOException or UnauthorizedAccessException)
            {log($"M15-111 notification history {path} unreadable ({e.Message}); recreating it, so install date and unlock times restart");}
        }
        int install=LocalMidnightSeconds(nowMicros);int[] timestamps=new int[Count];timestamps[36]=timestamps[14]=timestamps[84]=install;
        var store=new NotificationHistoryStore(path,log,new(install,timestamps,new bool[Count]));
        store.Save();return store;
    }
    private static int LocalMidnightSeconds(long nowMicros)
    {
        DateTime today=DateTimeOffset.FromUnixTimeMilliseconds(nowMicros/1000).ToLocalTime().Date; // DateTime.Today on the engine's clock
        return unchecked((int)new DateTimeOffset(today,TimeZoneInfo.Local.GetUtcOffset(today)).ToUnixTimeSeconds());
    }
    internal NotificationDeterminants Snapshot()
    {lock(_gate)return new(_install,_timestamps.ToArray(),_played.ToArray());}
    /// <summary>Records the first unlock time of <paramref name="id"/>; false when it already has one or is out of range.</summary>
    internal bool RecordUnlock(uint id,long nowMicros)
    {
        lock(_gate)
        {
            if(id>=Count||_timestamps[id]!=0)return false;
            _timestamps[id]=unchecked((int)(nowMicros/1_000_000L));Save();return true;
        }
    }
    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            string temporary=_path+".tmp";
            File.WriteAllText(temporary,JsonSerializer.Serialize(new Stored(_install,_timestamps,_played)));
            File.Move(temporary,_path,true);
        }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException)
        {_log($"M15-111 notification history {_path} not written ({e.Message}); this run keeps it in memory");}
    }
}
