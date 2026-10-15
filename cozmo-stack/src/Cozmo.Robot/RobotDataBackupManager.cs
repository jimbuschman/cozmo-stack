namespace Cozmo.Robot;

public sealed record RestoreRobotStatus(bool IsWipe, bool Success);
public sealed record RestoreRobotOptions(bool ShouldPromptForRestore, IReadOnlyList<uint> RobotKeys);
public sealed record BackupUnlockStatus(IReadOnlyList<uint> UnlockIds, bool FromBackup);

// fidelity: M15-033, M15-038, M15-039, M15-040, M15-041, M15-042
internal sealed class RobotDataBackupManager : IDisposable
{
    private readonly CozmoEngine _engine;
    private NvStorageComponent Nv => _engine.NvStorage!;
    internal readonly BackupFiles Files;
    internal readonly SortedSet<uint> Tags = new();
    internal BackupNativeMap Committed = new();
    private readonly Dictionary<uint,Queue<byte[]>> _pending = new();
    private readonly SubscriptionOwners _handles = new();
    internal uint PendingCount;
    internal byte Onboarding;
    internal bool CompletedOnboarding { get => Onboarding != 0; set => Onboarding = value ? (byte)1 : (byte)0; }
    private bool _failed, _disposed;
    internal bool HasPendingData(uint tag) => _pending.ContainsKey(tag);
    internal RobotDataBackupManager(CozmoEngine engine)
    {
        _engine = engine; Files = new(engine);
        // HasExternalInterface is true: the stack's API is the external interface.
        _handles.Add(engine.AnimationMessages.SubscribeMessage(0x6B,false,p => Restore((uint)p!)));
        _handles.Add(engine.AnimationMessages.Subscribe(0x6D,false,Wipe));
        _handles.Add(engine.AnimationMessages.Subscribe(0x6C,false,RequestOptions));
        if (!BackupFiles.CreateDirectory(Files.DirectoryPath))
        { Files.Error("RestoreFromBackup.CreateDir","Unable to create directory for robot backups"); return; }
        string? config = Files.Resource("config/engine/backup_config.json");
        if (config is null || !BackupFiles.RegularFile(config)) return;
        if (!Files.ReadJson(config,out var root))
        { if (!Files.JsonWasSkipped) Files.Error("Constructor.ReadJson","Failed to read backup_config.json"); return; }
        try
        {
            var tags = new SortedSet<uint>();
            foreach (var member in BackupFiles.Members(Json.Member(root,"tagsToBackup"))) tags.Add(Json.AsUInt(member.Value));
            Tags.UnionWith(tags);
        }
        // fidelity: M1-034, M15-038
        catch (JsonLogicError) { Files.Skip("M15-038","0x00519CB0 / 0x00519D0C (JsonCpp getMemberNames/asUInt)",config); }
    }
    internal void ReadAll()
    {
        if (PendingCount != 0) { Files.Log(EngineLogLevel.Warning,"ReadAllBackupDataFromRobot","Have pending requests so not reading"); return; }
        int ordinal = 0, total = Tags.Count;
        foreach (uint tag in Tags) { ++PendingCount; int index = ++ordinal; Nv.Read(tag,r => ReadSink(tag,index,total,r)); }
    }
    internal void ReadSink(uint tag, int ordinal, int total, NvResult result)
    {
        PendingCount = unchecked(PendingCount - 1);
        if (result.Result == -1) Files.Info("ReadAllDataFromRobot.TagNotFound",TagName(tag));
        else if (result.Result < -1) Files.Log(EngineLogLevel.Warning,"ReadAllDataFromRobot.ReadFailed",TagName(tag));
        else { Committed.Put(tag,result.Data); if (tag == 0x181000) Onboarding = result.Data.Length == 0 ? (byte)0 : result.Data[0]; }
        if (ordinal == total) WriteBackupFile();
    }
    internal void QueueDataToWrite(uint tag, byte[] data)
    {
        if (!Tags.Contains(tag)) { Files.Log(EngineLogLevel.Debug,"QueueDataToWrite",$"Ignoring invalid tag {TagName(tag)}[{unchecked((int)tag)}]","Unnamed"); return; }
        if (!_pending.TryGetValue(tag,out var queue)) _pending[tag] = queue = new();
        queue.Enqueue(data.ToArray());
    }
    internal void WriteDataForTag(uint tag, sbyte result, bool isWrite)
    {
        if (!_pending.TryGetValue(tag,out var queue)) { Files.Info("WriteDataForTag",$"Tag {TagName(tag)}[{unchecked((int)tag)}] doesn't exist in tagDataMap"); return; }
        if (result < 0 || !isWrite) { _pending.Remove(tag); return; }
        byte[] data = queue.Dequeue(); Committed.Put(tag,data); if (queue.Count == 0) _pending.Remove(tag);
        if (tag == 0x181000 && data.Length != 0) Onboarding = data[0];
        WriteBackupFile();
    }
    internal void WipeAll() { _pending.Clear(); Committed.Clear(); WriteBackupFile(); }
    internal void WriteBackupFile() => Files.WriteBackup(Committed,Onboarding);
    internal void Restore(uint key)
    {
        if (PendingCount != 0) { Files.Log(EngineLogLevel.Warning,"RestoreRobotFromBackup","Have pending requests so not restoring"); Status(false,false); return; }
        if (!BackupFiles.CreateDirectory(Files.DirectoryPath)) { Files.Info("RestoreFromBackup.NoDir","Failed to create backup dir"); Status(false,false); return; }
        string? name = key != 0 && BackupFiles.RegularFile(Files.DirectoryPath + key.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".backup")
            ? key.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".backup" : Files.SelectBackup();
        if (name is null) { Files.Error("RestoreFromBackup.NoBackup","No backup to restore from"); Status(false,false); return; }
        Files.Info("RestoreFromBackup",$"Using file {name} for backup");
        if (!Files.ParseBackup(name,ref Committed,"M15-040")) { Status(false,false); return; }
        _pending.Clear(); PendingCount = 0; _failed = false; Onboarding = 1;
        int ordinal = 0, total = Committed.Nodes.Count;
        foreach (var node in Committed.Nodes)
        {
            ++PendingCount; int index = ++ordinal;
            Nv.Write(node.Tag,node.Data,(uint)node.Data.Length,r =>
            {
                PendingCount = unchecked(PendingCount - 1);
                if (r.Result < 0) { Files.Log(EngineLogLevel.Warning,"RestoreFromBackup",$"Writing to tag {TagName(node.Tag)} failed with {NvStorageComponent.NvResultName(r.Result) ?? "(null)"}"); _failed = true; Status(false,false); }
                else if (index == total && !_failed) { Files.Info("RestoreFromBackup","Restore complete!"); Status(false,true); WriteBackupFile(); }
            });
        }
    }
    internal void Wipe()
    {
        if (PendingCount != 0) { Files.Log(EngineLogLevel.Warning,"WipeRobotGameData","Have pending requests so not wiping"); Status(true,false); return; }
        Files.Info("WipeRobotGameData","Wiping robot game data");
        _pending.Clear(); Committed.Clear(); PendingCount = 0; _failed = false; Onboarding = 0;
        int ordinal = 0, total = Tags.Count;
        foreach (uint tag in Tags)
        {
            ++PendingCount; int index = ++ordinal;
            Nv.Erase(tag,r =>
            {
                PendingCount = unchecked(PendingCount - 1);
                if (r.Result < 0) { Files.Log(EngineLogLevel.Warning,"WipeRobotGameData",$"Erase of tag {TagName(tag)} failed with {NvStorageComponent.NvResultName(r.Result) ?? "(null)"}"); _failed = true; Status(true,false); }
                else if (index == total && !_failed) { Files.Info("WipeRobotGameData","Wipe Complete!"); Status(true,true); }
            });
        }
    }
    internal void RequestOptions()
    {
        var keys = Files.EnumerateKeys(); Files.Info("HandleRequestRobotRestoreData",$"Will prompt for restore {(Onboarding == 0 ? "True" : "False")}");
        _engine.BroadcastBackup(0x3E,new RestoreRobotOptions((Onboarding ^ 1) != 0,keys));
    }
    private void Status(bool wipe, bool success) => _engine.BroadcastBackup(0x3D,new RestoreRobotStatus(wipe,success));
    private static string TagName(uint tag) => NvStorageComponent.NvEntryTagName(tag) ?? "(null)";
    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        WriteBackupFile(); Tags.Clear(); Committed.Clear(); _pending.Clear(); _handles.Retire();
    }
}

public sealed partial class CozmoEngine
{
    internal RobotDataBackupManager? Backup;
    internal BackupFiles BackupFiles = null!;
    private RetainedSubscription? _backupUnlockHandle;
    internal void InitBackupContext()
    {
        BackupFiles = new(this);
        _backupUnlockHandle = AnimationMessages.Subscribe(0x6E,false,BackupFiles.RequestUnlocks); // 0x005270E8, independent of Robot lifetime.
    }
    /// <summary>Native backup reports at the app boundary, delivered before local engine subscribers.</summary>
    public event Action<object>? BackupReport;
    internal void BroadcastBackup(ushort tag, object report)
    {
        // Native app delivery packs/copies the payload; a host observer cannot mutate the local signal's vector.
        report = report switch {
            BackupUnlockStatus unlocks => unlocks with { UnlockIds = Array.AsReadOnly(unlocks.UnlockIds.ToArray()) },
            RestoreRobotOptions options => options with { RobotKeys = Array.AsReadOnly(options.RobotKeys.ToArray()) },
            _ => report,
        };
        NativeUiDeliveryCount = unchecked(NativeUiDeliveryCount + 1);
        if (BackupReport is {} handlers) foreach (Action<object> handler in handlers.GetInvocationList()) RunIsolated(() => handler(report));
        AnimationMessages.Deliver(tag,true,report);
    }
}

public sealed partial class CozmoRobot
{
    // fidelity: M3-063 — replaces app request timing/key with explicit host inputs; bodies still run on the engine thread.
    public void RestoreRobotFromBackup(uint key) => Engine.PostGameMessage(() => Engine.AnimationMessages.Deliver(0x6B,false,key));
    public void RequestRobotRestoreData() => Engine.PostGameMessage(() => Engine.AnimationMessages.Deliver(0x6C,false));
    public void WipeRobotGameData() => Engine.PostGameMessage(() => Engine.AnimationMessages.Deliver(0x6D,false));
    public void RequestUnlockDataFromBackup() => Engine.PostGameMessage(() => Engine.AnimationMessages.Deliver(0x6E,false));
}
