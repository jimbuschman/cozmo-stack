using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using Cozmo.Transport;

namespace Cozmo.Robot;

// fidelity: M15-033, M15-038, M15-039, M15-040, M15-042, M15-043
// Shared native file helpers. The static unlock handler uses a context, independently of any Robot/NV owner.
internal sealed class BackupFiles(CozmoEngine engine)
{
    internal readonly string DirectoryPath = Join(engine.Options.FilesPath + "/output", "robotBackups/");
    private static string Basename = ""; // native process global 0x01059D80, not one filename per Robot.
    private static readonly SortedSet<uint> Defaults = new(); // 0x0105B560; size is the initialization gate.
    private static string Join(string root, string name) => root + (name.Length != 0 && name[0] != '/' ? "/" : "") + name;
    // fidelity: M3-062 — resource-scope root is a host app input.
    internal string? Resource(string name) => engine.Options.ResourcesPath is {} root ? Join(root,name) : null;
    internal static bool RegularFile(string path) => File.Exists(path) && !Directory.Exists(path);
    internal static bool CreateDirectory(string path)
    {
        try { Directory.CreateDirectory(path); return true; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { return false; }
    }
    internal void Log(EngineLogLevel level, string key, string text, string? channel = null)
        => engine.EmitSourceLog(level, channel, "RobotDataBackupManager." + key, text);
    internal void Info(string key, string text) => Log(EngineLogLevel.Info, key, text, "Unnamed");
    internal void Error(string key, string text) { Log(EngineLogLevel.Error, key, text); EngineErrorState.StoreAndMaybeBreak(); }
    internal static void Missing(string text) => Behavior.SteppedBehavior.ReportMissing(text);
    // fidelity: M1-034 — policy skips are stack diagnostics, never invented engine entries (builder rule 11).
    internal void Skip(string record, string address, string unit,string downstream="absent-data") => engine.EmitStackLog(
        $"Stack.RobotDataBackupManager.SkipMalformed: M1-034 / {record}: skipped {unit}; original undefined/terminating access at {address}; prior state kept; downstream {downstream} path follows");
    internal bool JsonWasSkipped { get; private set; }
    internal bool ReadJson(string path, out FirmwareJsonValue root)
    {
        root = new();return ReadJson(path,root);
    }
    // fidelity: M15-044 — failed open supplies an empty stream to Reader; a false parse keeps its root mutations.
    internal bool ReadJson(string path,FirmwareJsonValue root,string owner="M15-038/M15-039/M15-040/M15-043")
    {
        JsonWasSkipped = false;bool ok;byte[] bytes=Array.Empty<byte>();
        try { bytes=File.ReadAllBytes(path); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        try { ok=Json.TryParseInto(bytes,root); }
        // fidelity: M1-034, M15-038, M15-039, M15-040, M15-043
        catch (JsonRuntimeError) { JsonWasSkipped = true; Skip(owner, "0x008E0D92", path); return false; }
        catch (JsonMissingSource e) { JsonWasSkipped = true; Missing(owner+" reader dependency: "+e.Message); return false; }
        if (ok) return true;
        engine.EmitSourceLog(EngineLogLevel.Error, null, "DataPlatform.readAsJson", $"Failed to read [{path}]");
        EngineErrorState.StoreAndMaybeBreak();
        // Reader-specific Debug text is best effort (M5-051); the read failure above remains visible.
        return false;
    }
    internal static IEnumerable<KeyValuePair<string,FirmwareJsonValue>> Members(FirmwareJsonValue value)
    {
        if (value.Kind == FirmwareJsonKind.Null) return Array.Empty<KeyValuePair<string,FirmwareJsonValue>>();
        if (value.Kind != FirmwareJsonKind.Object) throw new JsonLogicError("in Json::Value::getMemberNames(), value must be objectValue");
        return value.Members.OrderBy(p => p.Key, StringComparer.Ordinal); // hex identities preserve shorter-first byte memcmp.
    }
    internal static void IncrementConnectionCount(FirmwareJsonValue root, string key, bool existed)
    {
        var count = Json.Member(Json.Member(root, "ConnectionCount"), key);
        uint next = existed ? unchecked(Json.AsUInt(count) + 1) : 1;
        count.Assign(existed ? FirmwareJsonKind.UInt : FirmwareJsonKind.Int);
        if (existed) count.Unsigned = next; else count.Signed = 1;
    }
    internal void RobotConnected(uint serial)
    {
        string key = serial.ToString(CultureInfo.InvariantCulture), path = DirectoryPath + "statsForBackup.json";
        bool existed = RegularFile(path); FirmwareJsonValue root = new();
        if (existed && !ReadJson(path, out root))
        { if (!JsonWasSkipped) Error("RobotConnected.ReadStatsFailed", "Failed to read statsForBackup.json"); return; }
        try { IncrementConnectionCount(root, key, existed); }
        // fidelity: M1-034, M15-039
        catch (JsonLogicError) { Skip("M15-039", "0x0051A088..0x0051A0C8 / 0x008E89C2", path); return; }
        engine.EmitSourceLog(EngineLogLevel.Info, "Unnamed", "DataPlatform.writeAsJson", $"writing to {path}");
        if (!CreateDirectory(Path.GetDirectoryName(path)!))
        {
            engine.EmitSourceLog(EngineLogLevel.Error, null, "DataPlatform.writeAsJson", $"Failed to create folder {path}");
            EngineErrorState.StoreAndMaybeBreak(); Error("RobotConnected.WriteStatsFailed", "Failed to write backup stats file");
        }
        else Missing("M15-039: writeAsJson 0x0084C328..0x0084C4B6 calls StyledStreamWriter(tab). Its shipped formatting/comment/escaping body has no checked builder row; stats serialization/open/write/close and result-dependent WriteStatsFailed are not built");
        // Both defined writeAsJson results reach this assignment. No failure result is fabricated for the missing writer.
        Basename = key + ".backup";
    }
    internal string? SelectBackup()
    {
        string path = DirectoryPath + "statsForBackup.json";
        if (!RegularFile(path)) { Log(EngineLogLevel.Warning,"GetFileToUseForBackup.ReadJson.NoFile","No backup stats file found"); return null; }
        if (!ReadJson(path,out var root))
        { if (!JsonWasSkipped) Error("GetFileToUseForBackup.ReadJson.Failed","Failed to read statsForBackup.json"); return null; }
        uint maximum = 0; string name = "";
        try
        {
            foreach (var member in Members(Json.Member(root,"ConnectionCount")))
            {
                uint count = Json.AsUInt(member.Value);
                if (count > maximum) { maximum = count; name = Encoding.UTF8.GetString(Convert.FromHexString(member.Key)); }
            }
        }
        // fidelity: M1-034, M15-040
        catch (JsonLogicError) { Skip("M15-040","0x0051BBAC / 0x008E6C68 / 0x008E89C2",path); return null; }
        if (name.Length == 0) { Error("GetFileToUseForBackup.NoFile","Could not find a backup file to use from statsForBackup.json"); return null; }
        return name + ".backup";
    }
    internal bool ParseBackup(string name, ref BackupNativeMap target, string record)
    {
        byte[] data;
        try { data = File.ReadAllBytes(DirectoryPath + name); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { data = Array.Empty<byte>(); }
        if (data.Length == 0) { Log(EngineLogLevel.Warning,"ParseBackupFile.ReadFileFailed","Unable to read backup file"); return false; }
        // fidelity: M1-034 — validate the complete lost file before publishing its clear/inserts.
        var parsed = new BackupNativeMap(target.Buckets); int p = 0;
        while (p < data.Length)
        {
            if (data.Length - p < 8) { Skip(record,"0x0051BF0A..0x0051BF4C",name); return false; }
            uint tag = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(p)), length = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(p + 4));
            if (length > (uint)(data.Length - p - 8)) { Skip(record,"0x0051BF0A..0x0051BF4C",name); return false; }
            parsed.Put(tag,data.AsSpan(p + 8,(int)length).ToArray()); p += 8 + (int)length;
        }
        target = parsed; return true;
    }
    internal void WriteBackup(BackupNativeMap map, byte onboarding)
    {
        if (onboarding == 0) { Info("WriteBackupFile","Not completed onboarding not writing backup"); return; }
        Info("WriteBackupFile","Writing backup file");
        using var bytes = new MemoryStream(); Span<byte> header = stackalloc byte[8];
        foreach (var node in map.Nodes)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(header,node.Tag); BinaryPrimitives.WriteUInt32LittleEndian(header[4..],(uint)node.Data.Length);
            bytes.Write(header); bytes.Write(node.Data);
        }
        if (Basename.Length == 0) { Error("Destructor","Filename not set"); return; }
        try { using var file = new FileStream(DirectoryPath + Basename,FileMode.Create,FileAccess.Write); foreach (byte b in bytes.ToArray()) file.WriteByte(b); file.Flush(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { } // native bool ignored, no extra log.
    }
    internal IReadOnlyList<uint> EnumerateKeys()
    {
        var keys = new List<uint>();
        try
        {
            foreach (var entry in new DirectoryInfo(DirectoryPath).EnumerateFileSystemInfos())
            {
                if ((entry.Attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0) continue;
                string name = entry.Name; if (name.Length < 7) continue;
                int end = name.IndexOf(".backup",StringComparison.Ordinal); if (end < 0) continue;
                try { ulong key = Stoull(name[..end]); Info("RobotConnected.AllBackups","Have backup for " + key.ToString(CultureInfo.InvariantCulture)); keys.Add(unchecked((uint)Stoull(name[..end]))); }
                // fidelity: M1-034, M15-042, M15-043
                catch (Exception e) when (e is FormatException or OverflowException) { Skip("M15-042/M15-043","0x0051C336 / libc++ 0x00065428..0x00065490",name); }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } // native failed opendir/readdir ends its vector.
        return keys;
    }
    // Exact shipped stoull wrapper decisions; unshipped bionic strtoull arithmetic/isspace is the permitted runtime equivalence.
    internal static ulong Stoull(string text)
    {
        int p = 0; while (p < text.Length && " \t\n\r\v\f".Contains(text[p])) ++p;
        bool negative = p < text.Length && text[p] == '-'; if (p < text.Length && (text[p] == '+' || text[p] == '-')) ++p;
        int first = p; ulong value = 0; bool overflow = false;
        while (p < text.Length && text[p] >= '0' && text[p] <= '9')
        {
            uint d = (uint)(text[p++] - '0');
            if (value > ulong.MaxValue / 10 || value == ulong.MaxValue / 10 && d > ulong.MaxValue % 10) overflow = true;
            else value = value * 10 + d;
        }
        if (overflow) throw new OverflowException("stoull: out of range");
        if (first == p) throw new FormatException("stoull: no conversion");
        return negative ? unchecked(0ul - value) : value;
    }
    internal void RequestUnlocks()
    {
        const string key = "HandleRequestUnlockDataFromBackup.";
        string? name = SelectBackup(); var map = new BackupNativeMap();
        if (name is null) { Info(key+"NoBackup","No backup, sending default unlock data"); SendUnlocks(GetDefaultUnlocks()); return; }
        if (!ParseBackup(name,ref map,"M15-043")) { Info(key+"FailedToParseBackup","Failed to parse backup, sending default unlock data"); SendUnlocks(GetDefaultUnlocks()); return; }
        byte[]? data = map.Find(0x182000);
        if (data is null) { Info(key+"NoUnlockData","No unlock data in backup, sending default unlock data"); SendUnlocks(GetDefaultUnlocks()); return; }
        var ids = new List<uint>(); int words = Math.Min(512,data.Length / 4); bool zero = false;
        for (int i = 0; i < words; ++i) { uint id = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(i * 4)); if (id == 0) { zero = true; break; } ids.Add(id); }
        if (!zero && words < 512)
        {
            // fidelity: M1-034 — the checked stale words occupy destination indices 0 and 1, unless Unpack overwrote them.
            if (words == 0) ids.Add(0x182000); if (words < 2) ids.Add(13);
            Skip("M15-043","0x0051C6BC (stop before freed word 2 or the first later uninitialised word)",name+" unlock block","partial-data");
        }
        Info(key+"UnlockData",$"Found unlock data in backup file {name}"); SendUnlocks(ids);
    }
    private void SendUnlocks(IReadOnlyList<uint> ids) => engine.BroadcastBackup(0x6D,new BackupUnlockStatus(ids,true));
    internal static void ClearDefaultsForTest() => Defaults.Clear();
    internal static uint[] CurrentDefaults()=>Defaults.ToArray();
    internal bool ProgressionOverridesNeedRecovery;
    internal void InitProgressionDefaults(){ProgressionOverridesNeedRecovery=false;GetDefaultUnlocks(true);}
    private IReadOnlyList<uint> GetDefaultUnlocks(bool forceResourceRead=false)
    {
        if (Defaults.Count != 0 && !forceResourceRead) return Defaults.ToArray();
        // fidelity: M15-044 — Init 0x0064B89E and GetDefaultUnlocks 0x0064C0A2:
        // fresh null root, ignore the return; Init keeps this same root for overrides.
        var root=new FirmwareJsonValue();
        _=InitUnlockConfig(engine,engine.Options,root);
        if(forceResourceRead)ObserveProgressionOverrides(root);
        return Defaults.ToArray();
    }
    // fidelity: M15-044 — 0x0064BB68..0x0064BDB4; options are the host's DataPlatform path service.
    internal static bool InitUnlockConfig(CozmoEngine? context,CozmoEngineOptions? dataPlatform,FirmwareJsonValue root)
    {
        if(context is null || dataPlatform is null)return false;
        const string relative="config/engine/unlock_config_nurture.json";
        if(dataPlatform.ResourcesPath is not {} resources)
        { Missing("M3-062/M15-043/M15-044: InitConfig has no host ResourcesPath input; resource read is unavailable, defaults retained");return false; }
        var files=new BackupFiles(context);string path=Join(resources,relative);
        bool read=files.ReadJson(path,root,"M15-044");
        if(files.JsonWasSkipped)return false;
        if(!read)
        {
            context.EmitSourceLog(EngineLogLevel.Error,null,"ProgressionUnlockComponent.InitDefaults.UnlockConfigJsonNotFound","Unlock Json config file config/engine/unlock_config_nurture.json not found.");EngineErrorState.StoreAndMaybeBreak();
        }
        if(Defaults.Count!=0)return read;
        if(root.Kind==FirmwareJsonKind.Null)return MissingDefaults();
        FirmwareJsonValue list;
        try { list = Json.Member(root,"defaultUnlocks"); }
        // fidelity: M1-034, M15-044
        catch (JsonLogicError) { files.Skip("M15-044","0x008EABA6 (non-object non-null operator[])",path);return false; }
        if (list.Kind != FirmwareJsonKind.Array)
            return MissingDefaults();
        foreach (var item in list.Elements)
        {
            if (item.Kind != FirmwareJsonKind.String)
            { context.EmitSourceLog(EngineLogLevel.Error,null,"ProgressionUnlockComponent.InitDefaults.InvalidDefaultData","invalid element in unlock id's list (not a string)"); EngineErrorState.StoreAndMaybeBreak(); continue; }
            uint id=NativeUnlockNames.FromBytes(item.Bytes);
            Defaults.Add(id);
            context.EmitSourceLog(EngineLogLevel.Info,"ProgressionUnlockComponent.InitDefaults","ProgressionUnlockComponent.DefaultValue",$"{NativeUnlockNames.NameOf(id)} defaults to true");
        }
        return read;
        bool MissingDefaults()
        {
            context.EmitSourceLog(EngineLogLevel.Error,null,"ProgressionUnlockComponent.InitDefaults.MissingDefaults","missing key 'defaultUnlocks'");EngineErrorState.StoreAndMaybeBreak();return false;
        }
    }
    private void ObserveProgressionOverrides(FirmwareJsonValue root)
    {
        if(root.Kind==FirmwareJsonKind.Null)return;
        try
        {
            var value=Json.Member(root,"freeplayOverrides");
            ProgressionOverridesNeedRecovery=value.Kind!=FirmwareJsonKind.Array||value.Elements.Count>0;
        }
        catch(JsonLogicError){Skip("M15-034","0x0064B8D4 / JsonCpp operator[]","Progression Init overrides");ProgressionOverridesNeedRecovery=true;}
    }
}
