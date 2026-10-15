using Cozmo.Robot;
using Cozmo.Transport;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")] // Shares the process-static unowned-parent flag with pose/removal tests.
public sealed class M15BackupBuilderTests
{
    private sealed class Port : IEngineTransport
    {
        public bool TimedOut => false; public event Action<ReceiverEvent>? Received { add { } remove { } }
        public void Start() { } public void Connect(System.Net.IPAddress ip,bool simulated) { }
        public void Disconnect(System.Net.IPEndPoint endpoint) { } public void SendData(byte[] data) { }
    }
    private sealed class Rig : IDisposable
    {
        internal readonly BackupTestResources Files = new();
        internal readonly CozmoRobot Robot;
        internal CozmoEngine Engine => Robot.Engine;
        internal RobotDataBackupManager Backup => Engine.Backup!;
        internal readonly List<EngineLogEntry> Logs = new();
        internal readonly List<string> Stack = new();
        internal readonly List<object> Reports = new();
        private long _now = 1_000_000_000;
        internal Rig(string? config = null,bool addRobot = true)
        {
            Cozmo.Robot.Behavior.SteppedBehavior.ResetMissingForTests();
            var options = Files.Options();
            if (config != null) File.WriteAllText(Path.Combine(options.ResourcesPath!,"config/engine/backup_config.json"),config);
            Robot = CozmoRobot.CreateForTest(new Port(),() => _now,options);
            Engine.LogEntry += Logs.Add; Engine.StackLogLine += Stack.Add; Engine.BackupReport += Reports.Add;
            if (addRobot) Engine.Robots.AddRobot(CozmoEngine.RobotId);
        }
        internal void Tick() { _now += 60_000_000; Engine.Tick(); }
        internal void FileBytes(string name,byte[] data) => File.WriteAllBytes(Engine.BackupFiles.DirectoryPath + name,data);
        internal void Stats(string json) => File.WriteAllText(Engine.BackupFiles.DirectoryPath + "statsForBackup.json",json);
        public void Dispose() { Robot.Dispose(); Files.Dispose(); }
    }
    private static byte[] Block(uint tag,params byte[] data)
    { using var bytes = new MemoryStream(); using var w = new BinaryWriter(bytes); w.Write(tag); w.Write((uint)data.Length); w.Write(data); return bytes.ToArray(); }
    private static byte[] Words(params uint[] values) => values.SelectMany(BitConverter.GetBytes).ToArray();
    [Fact]
    public void PendingReadAllUsesTheNativeWarningKeyAndKeepsThePendingRequest()
    {
        using var rig=new Rig();rig.Backup.PendingCount=1;rig.Logs.Clear();
        rig.Backup.ReadAll();var entry=Assert.Single(rig.Logs);
        Assert.Equal("RobotDataBackupManager.ReadAllBackupDataFromRobot",entry.Name);
        Assert.Equal(EngineLogLevel.Warning,entry.Level);Assert.Null(entry.Channel);
        Assert.Equal("(tc0000) : Have pending requests so not reading",entry.Text); // M3-048 logger wrapper, tick zero.
        Assert.Equal(1u,rig.Backup.PendingCount);Assert.Empty(rig.Reports);
    }
    [Fact]
    public void MissingUnlockConfigErrorUsesTheNativeRelativePath()
    {
        BackupFiles.ClearDefaultsForTest();bool prior=EngineErrorState.ErrorFlagSet;
        try
        {
            using var rig=new Rig();EngineErrorState.ErrorFlagSet=false;
            rig.Robot.RequestUnlockDataFromBackup();rig.Tick();
            var entry=Assert.Single(rig.Logs.Where(l=>l.Name=="ProgressionUnlockComponent.InitDefaults.UnlockConfigJsonNotFound"));
            Assert.Equal(EngineLogLevel.Error,entry.Level);Assert.Null(entry.Channel);
            Assert.Equal("(tc0000) : Unlock Json config file config/engine/unlock_config_nurture.json not found.",entry.Text);
            Assert.True(EngineErrorState.ErrorFlagSet);
        }
        finally {BackupFiles.ClearDefaultsForTest();EngineErrorState.ErrorFlagSet=prior;}
    }
    [Fact]
    public void ConstructorUsesConfiguredSortedSetAndPendingHostRequestsRunOnTheEngineThread()
    {
        using var rig = new Rig("{\"tagsToBackup\":{\"z\":1581056,\"a\":1572864,\"duplicate\":1581056}}");
        Assert.Equal(new uint[]{0x180000,0x182000},rig.Backup.Tags);
        Assert.Equal(rig.Files.Root + "/output/robotBackups/",rig.Backup.Files.DirectoryPath);
        Assert.True(Directory.Exists(rig.Backup.Files.DirectoryPath));
        rig.Backup.PendingCount = 1; rig.Robot.RestoreRobotFromBackup(5); rig.Robot.WipeRobotGameData(); Assert.Empty(rig.Reports);
        rig.Tick(); Assert.Equal(new object[]{new RestoreRobotStatus(false,false),new RestoreRobotStatus(true,false)},rig.Reports);
        Assert.Contains(rig.Logs,l => l.Name == "RobotDataBackupManager.RestoreRobotFromBackup" && l.Text.EndsWith("Have pending requests so not restoring"));
        Assert.Contains(rig.Logs,l => l.Name == "RobotDataBackupManager.WipeRobotGameData" && l.Text.EndsWith("Have pending requests so not wiping"));
    }
    [Fact]
    public void ConstructorDirectoryFailureSkipsConfigAndSetsErrorFlagAfterSubscriptions()
    {
        using var files = new BackupTestResources(); string path = Path.Combine(files.Root,"blocked"); File.WriteAllText(path,"x");
        using var robot = CozmoRobot.CreateForTest(new Port(),() => 0,new CozmoEngineOptions{FilesPath=path,ResourcesPath="unused",BlockPoolPath=""});
        var logs = new List<EngineLogEntry>(); robot.Engine.LogEntry += logs.Add; EngineErrorState.ErrorFlagSet = false;
        robot.Engine.Robots.AddRobot(CozmoEngine.RobotId);
        Assert.Empty(robot.Engine.Backup!.Tags); Assert.True(EngineErrorState.ErrorFlagSet); Assert.Equal(1,robot.Engine.AnimationMessages.Count(0x6B,false));
        Assert.Contains(logs,l => l.Name == "RobotDataBackupManager.RestoreFromBackup.CreateDir" && l.Text.EndsWith("Unable to create directory for robot backups"));
    }
    [Fact]
    public void MissingConfigIsSilentAndMalformedConfigKeepsThePriorEmptyTagSet()
    {
        using(var rig = new Rig(addRobot:false))
        {
            File.Delete(Path.Combine(rig.Engine.Options.ResourcesPath!,"config/engine/backup_config.json"));
            rig.Engine.Robots.AddRobot(CozmoEngine.RobotId); Assert.Empty(rig.Backup.Tags);
            Assert.DoesNotContain(rig.Logs,l => l.Name.StartsWith("RobotDataBackupManager.Constructor."));
        }
        using(var rig = new Rig("{\"tagsToBackup\":{\"a\":1572864,\"z\":\"bad\"}}"))
        {
            Assert.Empty(rig.Backup.Tags); // First valid name must not survive the lost file.
            Assert.Contains(rig.Stack,l => l.Contains("M1-034 / M15-038") && l.Contains("0x00519CB0 / 0x00519D0C"));
        }
    }
    [Fact]
    public void ConnectionCountsUseIntOneForMissingFileAndWrappingUIntForExistingValues()
    {
        Assert.True(Json.TryParseFirst("{}"u8.ToArray(),out var document));
        BackupFiles.IncrementConnectionCount(document.RootElement,"42",false);
        var count = Json.Member(Json.Member(document.RootElement,"ConnectionCount"),"42"); Assert.Equal(FirmwareJsonKind.Int,count.Kind); Assert.Equal(1,count.Signed);
        Assert.True(Json.TryParseFirst("{\"ConnectionCount\":{\"42\":4294967295}}"u8.ToArray(),out document));
        BackupFiles.IncrementConnectionCount(document.RootElement,"42",true);
        count = Json.Member(Json.Member(document.RootElement,"ConnectionCount"),"42"); Assert.Equal(FirmwareJsonKind.UInt,count.Kind); Assert.Equal(0ul,count.Unsigned);
    }
    [Fact]
    public void ReadFailureKeepsCommittedAndCapturedFinalWritesEvenWhileOtherReadsRemain()
    {
        using var rig = new Rig(); rig.Backup.Files.RobotConnected(42); rig.Backup.CompletedOnboarding = true; rig.Backup.Committed.Put(0x182000,new byte[]{9}); rig.Backup.PendingCount = 3;
        rig.Backup.ReadSink(0x182000,1,1,new NvResult(-1,Array.Empty<byte>()));
        Assert.Equal(2u,rig.Backup.PendingCount); Assert.Equal(new byte[]{9},rig.Backup.Committed.Find(0x182000));
        Assert.Equal(Block(0x182000,9),File.ReadAllBytes(rig.Backup.Files.DirectoryPath + "42.backup"));
        Assert.Contains(rig.Logs,l => l.Name == "RobotDataBackupManager.ReadAllDataFromRobot.TagNotFound" && l.Text == "NVEntry_GameUnlocks");
        rig.Backup.ReadSink(0x181000,1,2,new NvResult(0,Array.Empty<byte>())); Assert.False(rig.Backup.CompletedOnboarding);
        Assert.Contains(rig.Stack,l => l.Contains("M15-039: writeAsJson") && l.Contains("StyledStreamWriter"));
        Assert.DoesNotContain(rig.Logs,l => l.Name == "MISSING" || l.Text.Contains("MISSING"));
    }
    [Fact]
    public void FailedWriteDropsAllPendingAndEraseDoesNotPromoteEmptyData()
    {
        using var rig = new Rig(); var b = rig.Backup; b.Committed.Put(0x182000,new byte[]{8});
        b.QueueDataToWrite(0x182000,new byte[]{1}); b.QueueDataToWrite(0x182000,new byte[]{2}); b.WriteDataForTag(0x182000,-2,true);
        Assert.False(b.HasPendingData(0x182000)); Assert.Equal(new byte[]{8},b.Committed.Find(0x182000));
        b.QueueDataToWrite(0x182000,Array.Empty<byte>()); b.WriteDataForTag(0x182000,0,false);
        Assert.False(b.HasPendingData(0x182000)); Assert.Equal(new byte[]{8},b.Committed.Find(0x182000));
        b.WriteDataForTag(0x182000,0,true); Assert.Contains(rig.Logs,l => l.Name == "RobotDataBackupManager.WriteDataForTag" && l.Text.Contains("doesn't exist in tagDataMap"));
    }
    [Fact]
    public void NativeMapGrowthIterationOverwriteAndPrimeWheelMatchTheCheckedRows()
    {
        var map = new BackupNativeMap(); foreach (uint tag in new uint[]{2,4,1,6,3}) map.Put(tag,new byte[]{(byte)tag});
        Assert.Equal(5u,map.Buckets); Assert.Equal(new uint[]{3,6,1,4,2},map.Nodes.Select(n => n.Tag));
        map.Put(4,new byte[]{99}); Assert.Equal(new uint[]{3,6,1,4,2},map.Nodes.Select(n => n.Tag));
        map.Put(8,new byte[]{8}); Assert.Equal(11u,map.Buckets); Assert.Equal(new uint[]{8,3,6,1,4,2},map.Nodes.Select(n => n.Tag));
        map.Clear(); Assert.Equal(11u,map.Buckets); Assert.Empty(map.Nodes);
        Assert.Equal(223u,BackupNativeMap.NextPrime(212)); Assert.Equal(223u,BackupNativeMap.NextPrime(221)); Assert.Equal(4294967291u,BackupNativeMap.NextPrime(4294967291));
        Assert.Throws<OverflowException>(() => BackupNativeMap.NextPrime(0xFFFFFFFC));
        var collisions = new BackupNativeMap(); foreach(uint key in new uint[]{11,1,22,2,33,99}) collisions.Put(key,Array.Empty<byte>());
        // Rehash to 11 sees bucket sequence 0,2,0,1,0; later runs splice before bucket 0's first node.
        Assert.Equal(new uint[]{99,11,22,33,2,1},collisions.Nodes.Select(n => n.Tag));
        var large = new BackupNativeMap(); for(uint key=1;key<=198;++key) large.Put(key,Array.Empty<byte>());
        Assert.Equal(397u,large.Buckets); Assert.Equal(Enumerable.Range(1,198).Reverse().Select(i => (uint)i),large.Nodes.Select(n => n.Tag));
    }
    [Fact]
    public void SelectorKeepsByteLexSmallestTieAndParserOverwritesWithoutFilteringTags()
    {
        using var rig = new Rig(); rig.Stats("{\"ConnectionCount\":{\"2\":9,\"10\":9,\"3\":0}}"); Assert.Equal("10.backup",rig.Backup.Files.SelectBackup());
        rig.FileBytes("10.backup",Block(2,1).Concat(Block(4,2)).Concat(Block(2,9)).ToArray());
        var map = new BackupNativeMap(); Assert.True(rig.Backup.Files.ParseBackup("10.backup",ref map,"M15-040"));
        Assert.Equal(new uint[]{4,2},map.Nodes.Select(n => n.Tag)); Assert.Equal(new byte[]{9},map.Find(2));
        rig.Stats("{\"ConnectionCount\":{\"9\":0}}"); Assert.Null(rig.Backup.Files.SelectBackup());
    }
    [Theory]
    [InlineData("{\"ConnectionCount\":[]}")]
    [InlineData("{\"ConnectionCount\":{\"42\":\"bad\"}}")]
    public void MalformedStatsKeepsTheOldGlobalBasenameAndFile(string stats)
    {
        using var rig = new Rig(); rig.Backup.Files.RobotConnected(41); rig.Stats(stats); rig.Backup.Files.RobotConnected(42);
        Assert.Contains(rig.Stack,l => l.Contains("M1-034 / M15-039") && l.Contains("0x008E89C2") && l.Contains("downstream absent-data path follows"));
        rig.Backup.CompletedOnboarding = true; rig.Backup.WriteBackupFile();
        Assert.True(File.Exists(rig.Backup.Files.DirectoryPath + "41.backup")); Assert.False(File.Exists(rig.Backup.Files.DirectoryPath + "42.backup"));
        Assert.Equal(stats,File.ReadAllText(rig.Backup.Files.DirectoryPath + "statsForBackup.json"));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void MalformedBackupKeepsTheWholePreviousCommittedMapAndOnboarding(bool oversized)
    {
        using var rig = new Rig(); rig.Backup.PendingCount = 0; rig.Backup.Committed.Put(0x182000,new byte[]{7}); rig.Backup.CompletedOnboarding = false;
        rig.FileBytes("5.backup",oversized ? Words(0x181000,uint.MaxValue) : new byte[]{1,2,3});
        rig.Robot.RestoreRobotFromBackup(5); rig.Tick(); Assert.Equal(new RestoreRobotStatus(false,false),Assert.Single(rig.Reports));
        Assert.Equal(new byte[]{7},rig.Backup.Committed.Find(0x182000)); Assert.False(rig.Backup.CompletedOnboarding);
        Assert.Contains(rig.Stack,l => l.Contains("M1-034 / M15-040") && l.Contains("0x0051BF0A..0x0051BF4C"));
    }
    [Fact]
    public void RestoreOptionsSkipsThrowingNamesAcceptsNumericPrefixesAndKeepsLowU32()
    {
        using var rig = new Rig(); rig.FileBytes("bad.backup",Array.Empty<byte>()); rig.FileBytes("4294967297.backup.extra",Array.Empty<byte>()); rig.FileBytes("  +12x.backup",Array.Empty<byte>());
        rig.Robot.RequestRobotRestoreData(); rig.Tick(); var result = Assert.IsType<RestoreRobotOptions>(Assert.Single(rig.Reports));
        Assert.True(result.ShouldPromptForRestore); Assert.Equal(new uint[]{1,12},result.RobotKeys.OrderBy(x => x));
        Assert.Contains(rig.Logs,l => l.Name == "RobotDataBackupManager.RobotConnected.AllBackups" && l.Text == "Have backup for 4294967297");
        Assert.Contains(rig.Stack,l => l.Contains("M15-042/M15-043") && l.Contains("0x0051C336"));
        Assert.Equal(ulong.MaxValue,BackupFiles.Stoull("-1")); Assert.Equal(0ul,BackupFiles.Stoull("0x10"));
        Assert.Throws<FormatException>(() => BackupFiles.Stoull("+")); Assert.Throws<OverflowException>(() => BackupFiles.Stoull("18446744073709551616"));
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void ShortUnlockBlockKeepsWrittenPrefixAndOnlyTheDeterministicStaleWords(int words)
    {
        using var rig = new Rig(); rig.Stats("{\"ConnectionCount\":{\"5\":1}}");
        rig.FileBytes("5.backup",Block(0x182000,Words(Enumerable.Range(0,words).Select(i => (uint)(7+i)).ToArray())));
        rig.Robot.RequestUnlockDataFromBackup(); rig.Tick(); var result = Assert.IsType<BackupUnlockStatus>(Assert.Single(rig.Reports));
        Assert.Equal(words switch { 0 => new uint[]{0x182000,13}, 1 => new uint[]{7,13}, 2 => new uint[]{7,8}, _ => new uint[]{7,8,9} },result.UnlockIds);
        Assert.True(result.FromBackup); Assert.Contains(rig.Stack,l => l.Contains("M1-034 / M15-043") && l.Contains("0x0051C6BC"));
    }
    [Fact]
    public void UnlockPreservesDuplicatesStopsAtZeroAndDeliversAppBeforeLocal()
    {
        using var rig = new Rig(); rig.Stats("{\"ConnectionCount\":{\"5\":1}}"); rig.FileBytes("5.backup",Block(0x182000,Words(10,10,0,99)));
        var order = new List<string>(); rig.Engine.BackupReport += _ => order.Add("app"); using var handle = rig.Engine.AnimationMessages.Subscribe(0x6D,true,() => order.Add("local"));
        rig.Robot.RequestUnlockDataFromBackup(); rig.Tick(); Assert.Equal(new[]{"app","local"},order);
        Assert.Equal(new uint[]{10,10},Assert.IsType<BackupUnlockStatus>(Assert.Single(rig.Reports)).UnlockIds);
        Assert.DoesNotContain(rig.Stack,l => l.Contains("Stack.RobotDataBackupManager.SkipMalformed"));
    }
    [Fact]
    public void FullUnlockBlockCarriesAll512UnknownValuesToTheApp()
    {
        using var rig = new Rig(); rig.Stats("{\"ConnectionCount\":{\"5\":1}}"); rig.FileBytes("5.backup",Block(0x182000,Words(Enumerable.Repeat(777u,512).ToArray())));
        rig.Robot.RequestUnlockDataFromBackup(); rig.Tick(); var result = Assert.IsType<BackupUnlockStatus>(Assert.Single(rig.Reports));
        Assert.Equal(512,result.UnlockIds.Count); Assert.All(result.UnlockIds,id => Assert.Equal(777u,id)); Assert.True(result.FromBackup);
        Assert.DoesNotContain(rig.Stack,l => l.Contains("Stack.RobotDataBackupManager.SkipMalformed"));
    }
    [Fact]
    public void DefaultsLogEveryEntryIncludingDuplicatesAndSkipNonstring()
    {
        BackupFiles.ClearDefaultsForTest();
        try
        {
            using var rig = new Rig(); File.WriteAllText(Path.Combine(rig.Engine.Options.ResourcesPath!,"config/engine/unlock_config_nurture.json"),"{\"defaultUnlocks\":[\"StackTwoCubes\",\"PickupCube\",\"StackTwoCubes\",123]}");
            rig.Robot.RequestUnlockDataFromBackup(); rig.Tick(); Assert.Equal(new uint[]{6,10},Assert.IsType<BackupUnlockStatus>(Assert.Single(rig.Reports)).UnlockIds);
            Assert.Equal(new[]{"StackTwoCubes defaults to true","PickupCube defaults to true","StackTwoCubes defaults to true"},rig.Logs.Where(l => l.Name == "ProgressionUnlockComponent.DefaultValue").Select(l => l.Text));
            Assert.Contains(rig.Logs,l => l.Name == "ProgressionUnlockComponent.InitDefaults.InvalidDefaultData");
            Assert.Contains(rig.Stack,l => l.Contains("M15-044 UnlockIdFromString"));
            File.Delete(Path.Combine(rig.Engine.Options.ResourcesPath!,"config/engine/unlock_config_nurture.json"));
            rig.Reports.Clear(); rig.Robot.RequestUnlockDataFromBackup(); rig.Tick();
            Assert.Equal(new uint[]{6,10},Assert.IsType<BackupUnlockStatus>(Assert.Single(rig.Reports)).UnlockIds);
            Assert.Equal(3,rig.Logs.Count(l => l.Name == "ProgressionUnlockComponent.DefaultValue")); // Nonempty set skips InitConfig.
        }
        finally { BackupFiles.ClearDefaultsForTest(); }
    }
    [Fact]
    public void EmptyDefaultSetIsRetriedOnTheNextQuery()
    {
        BackupFiles.ClearDefaultsForTest();
        try
        {
            using var rig = new Rig(); string path = Path.Combine(rig.Engine.Options.ResourcesPath!,"config/engine/unlock_config_nurture.json");
            File.WriteAllText(path,"{\"defaultUnlocks\":[]}"); rig.Robot.RequestUnlockDataFromBackup(); rig.Tick();
            Assert.Empty(Assert.IsType<BackupUnlockStatus>(Assert.Single(rig.Reports)).UnlockIds);
            rig.Reports.Clear(); File.WriteAllText(path,"{\"defaultUnlocks\":[\"PickupCube\"]}"); rig.Robot.RequestUnlockDataFromBackup(); rig.Tick();
            Assert.Equal(new uint[]{6},Assert.IsType<BackupUnlockStatus>(Assert.Single(rig.Reports)).UnlockIds);
        }
        finally { BackupFiles.ClearDefaultsForTest(); }
    }
    [Fact]
    public void NullResourceInputHasNoTagFallbackAndMissingDefaultsStayOnTheStackChannel()
    {
        BackupFiles.ClearDefaultsForTest(); Cozmo.Robot.Behavior.SteppedBehavior.ResetMissingForTests();
        using var files = new BackupTestResources(); using var robot = CozmoRobot.CreateForTest(new Port(),() => 0,new CozmoEngineOptions{FilesPath=files.Root,BlockPoolPath=""});
        robot.Engine.Robots.AddRobot(CozmoEngine.RobotId); Assert.Empty(robot.Engine.Backup!.Tags);
        var stack = new List<string>(); var engine = new List<EngineLogEntry>(); var reports = new List<object>();
        robot.Engine.StackLogLine += stack.Add; robot.Engine.LogEntry += engine.Add; robot.Engine.BackupReport += reports.Add;
        robot.RequestUnlockDataFromBackup(); robot.Engine.Tick();
        Assert.Empty(Assert.IsType<BackupUnlockStatus>(Assert.Single(reports)).UnlockIds);
        Assert.Contains(stack,l => l.Contains("M3-062/M15-043") && l.Contains("no host ResourcesPath"));
        Assert.DoesNotContain(engine,l => l.Name == "MISSING" || l.Text.Contains("MISSING"));
    }
    [Fact]
    public void EmptyConfiguredWipeSendsNoReportAndDoesNotDeleteFiles()
    {
        using var rig = new Rig("{\"tagsToBackup\":{}}"); rig.FileBytes("9.backup",new byte[]{1}); rig.Backup.CompletedOnboarding = true;
        rig.Robot.WipeRobotGameData(); rig.Tick(); Assert.Empty(rig.Reports); Assert.False(rig.Backup.CompletedOnboarding);
        Assert.Equal(new byte[]{1},File.ReadAllBytes(rig.Backup.Files.DirectoryPath + "9.backup"));
    }
    [Fact]
    public void StaticUnlockSubscriptionSurvivesRobotRemovalButManagerSubscriptionsDoNot()
    {
        using var rig = new Rig(); rig.Engine.Robots.RemoveRobots(); Assert.Null(rig.Engine.Backup);
        Assert.Equal(1,rig.Engine.AnimationMessages.Count(0x6E,false)); Assert.Equal(0,rig.Engine.AnimationMessages.Count(0x6B,false));
        Assert.Equal(0,rig.Engine.AnimationMessages.Count(0x6C,false)); Assert.Equal(0,rig.Engine.AnimationMessages.Count(0x6D,false));
        rig.Stats("{\"ConnectionCount\":{\"5\":1}}"); rig.FileBytes("5.backup",Block(0x182000,Words(6,0)));
        rig.Robot.RequestUnlockDataFromBackup(); rig.Tick(); Assert.Equal(new uint[]{6},Assert.IsType<BackupUnlockStatus>(Assert.Single(rig.Reports)).UnlockIds);
    }
    [Fact]
    public void LastRestoreWriteCallsBackupTwiceThenWipeUsesEraseAndKeepsTheFile()
    {
        using var files = new BackupTestResources(); var options = files.Options();
        File.WriteAllText(Path.Combine(options.ResourcesPath!,"config/engine/backup_config.json"),"{\"tagsToBackup\":{\"unlocks\":1581056}}");
        using var rig = new M4ControlTests.Rig(options.ResourcesPath,attachWorld:false,engineOptions:options); rig.ToSynced(); rig.State(); var nv = rig.Engine.NvStorage!;
        for (int i = 0; !nv.IsIdle && i < 30; ++i) { var cmd = rig.SentSince(0).OfType<NVCommand>().Last(); rig.Data(new NVOpResult{Tag=cmd.Tag,Op=0,Result=-1}); rig.Tick(); }
        Assert.True(nv.IsIdle); Assert.Equal(0u,rig.Engine.Backup!.PendingCount);
        string path = rig.Engine.Backup.Files.DirectoryPath + "1.backup"; File.WriteAllBytes(path,Block(0x182000,7,8));
        var order = new List<string>(); var reports = new List<object>(); rig.Engine.LogEntry += e => { if (e.Name == "RobotDataBackupManager.WriteBackupFile") order.Add(e.Text); };
        rig.Engine.BackupReport += r => { reports.Add(r); order.Add("report"); };
        rig.Robot.RestoreRobotFromBackup(1); rig.Tick(); Assert.Equal((byte)2,rig.SentSince(0).OfType<NVCommand>().Last().Op);
        rig.Data(new NVOpResult{Tag=0x182000,Op=2,Result=0}); rig.Tick(); rig.Tick(); Assert.Equal((byte)1,rig.SentSince(0).OfType<NVCommand>().Last().Op);
        rig.Data(new NVOpResult{Tag=0x182000,Op=1,Result=0}); rig.Tick();
        Assert.Equal(new[]{"report","Writing backup file","Writing backup file"},order); Assert.Equal(new RestoreRobotStatus(false,true),Assert.Single(reports));
        Assert.Equal(Block(0x182000,7,8,0,0),File.ReadAllBytes(path)); // NV pads to four bytes before the mirror promotion.
        reports.Clear(); order.Clear(); rig.Robot.WipeRobotGameData(); rig.Tick(); Assert.Equal((byte)2,rig.SentSince(0).OfType<NVCommand>().Last().Op);
        rig.Data(new NVOpResult{Tag=0x182000,Op=2,Result=0}); rig.Tick(); Assert.Equal(new RestoreRobotStatus(true,true),Assert.Single(reports));
        Assert.Equal(new[]{"report"},order); Assert.Equal(Block(0x182000,7,8,0,0),File.ReadAllBytes(path)); Assert.Empty(rig.Engine.Backup.Committed.Nodes);
    }
}
