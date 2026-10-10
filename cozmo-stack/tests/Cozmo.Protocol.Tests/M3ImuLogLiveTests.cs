using System.Net;
using System.Text;
using System.Text.Json;
using Cozmo.Robot;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M3-041, rows L1..L36 (re-analysis/research/20261009-M3-041-build-rows.md) and I1..I11
/// (20261009-M1-046-imu-reachability.md), driven through the live entry: a received RobotToEngine packet reaches
/// CozmoEngine.Broadcast and the Messaging handlers subscribed in EngineRobot.InitSubscriptions. The float text expected
/// in the files is the shipped libc++ formatter's output as recorded in the oracle fixture
/// (Fixtures/m3_041_libcxx_printf_oracle.json), never the C# formatter's.
/// </summary>
[Collection("SteppedBehavior missing-report statics")]
public class M3ImuLogLiveTests
{
    private sealed class FakePort : IEngineTransport
    {
        public bool TimedOut { get; set; }
        public event Action<ReceiverEvent>? Received;
        public void Start() { }
        public void Connect(IPAddress ip, bool isSimulated) { }
        public void Disconnect(IPEndPoint address) { }
        public void SendData(byte[] clad) { }
        public void Raise(ReceiverMarker m, IPEndPoint? a, byte[]? d = null) => Received?.Invoke(new ReceiverEvent(m, a, d));
    }

    private sealed class Rig : IDisposable
    {
        public long NowNs = 1_000_000_000;
        public readonly FakePort Port = new();
        public readonly CozmoRobot Robot;
        public readonly List<string> Log = new();
        public readonly string Root;
        public string LogsDir => Root + "/imu_logs";
        public static readonly IPAddress RobotIp = IPAddress.Parse("172.31.1.1");
        public static readonly IPEndPoint RobotEp = new(RobotIp, 5551);
        public CozmoEngine Engine => Robot.Engine;
        public ImuDiagnosticLog Imu => Engine.Robot!.ImuLog;

        public Rig(string? persistentPath = null)
        {
            Root = persistentPath ?? Path.Combine(Path.GetTempPath(), "cozmo-imu-" + Guid.NewGuid().ToString("N"));
            Robot = CozmoRobot.CreateForTest(Port, () => NowNs,
                new CozmoEngineOptions { BlockPoolPath = "", DataPlatformPersistentPath = Root });
            Engine.LogLine += l => { lock (Log) Log.Add(l); };
            Engine.ConnectToRobot(RobotIp); Tick();
            Port.Raise(ReceiverMarker.OnConnected, RobotEp); Tick();
            Data(new RobotAvailable { SerialNumberHead = 0x1234, HwVersion = 5 });
            Data(new FirmwareVersion { RobotId = 1, Signature = Encoding.UTF8.GetBytes("{\"version\": 2381, \"time\": 1546972025, \"build\": \"DEVELOPMENT\"}") });
            Tick();
            Data(new ManufacturingID { SerialNumber = 0xABCD, BodyHwVersion = 7, BodyColor = 2 });
            Tick();
            Assert.NotNull(Engine.Robot);
        }
        public void Tick(double ms = 60) { NowNs += (long)(ms * 1_000_000); Engine.Tick(); }
        public void Data(RobotMessage m) => Port.Raise(ReceiverMarker.Data, RobotEp, m.ToBytes());
        public void Remove() { Port.Raise(ReceiverMarker.OnDisconnected, RobotEp); Tick(); }
        public bool Logged(string part) { lock (Log) return Log.Any(l => l.Contains(part)); }
        public int LogIndex(string part) { lock (Log) return Log.FindIndex(l => l.Contains(part)); }
        public string[] Files() => Directory.Exists(LogsDir)
            ? Directory.GetFileSystemEntries(LogsDir).Where(File.Exists).Select(p => Path.GetFileName(p)!).OrderBy(x => x, StringComparer.Ordinal).ToArray()
            : Array.Empty<string>();
        public string Read(string name) => File.ReadAllText(Path.Combine(LogsDir, name), Encoding.ASCII);
        public void Dispose()
        {
            Robot.Dispose();
            try { if (Directory.Exists(Root)) Directory.Delete(Root, true); } catch (IOException) { }
        }
    }

    // The oracle's f32 rows at precision 6, in fixture order: 96 values (the specials first).
    private static readonly List<(float Value, string Text)> Oracle = LoadOracle();
    private static List<(float, string)> LoadOracle()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "m3_041_libcxx_printf_oracle.json")));
        var list = new List<(float, string)>();
        foreach (var row in doc.RootElement.GetProperty("rows").EnumerateArray())
        {
            if (row.GetProperty("fmt").GetString() != "%.*g" || row.GetProperty("precision").GetInt32() != 6) continue;
            if (row.GetProperty("f32_bits").ValueKind != JsonValueKind.String) continue;
            list.Add((BitConverter.Int32BitsToSingle(unchecked((int)Convert.ToUInt32(row.GetProperty("f32_bits").GetString(), 16))),
                      row.GetProperty("expected").GetString()!));
            if (list.Count == 96) break;
        }
        return list;
    }

    private static IMUDataChunk Chunk(int first, byte seq, byte chunkId, byte total)
    {
        var m = new IMUDataChunk { SeqId = seq, ChunkId = chunkId, TotalNumChunks = total };
        for (int i = 0; i < 8; ++i)
        {
            m.AX[i] = Oracle[first + i].Value; m.AY[i] = Oracle[first + 8 + i].Value; m.AZ[i] = Oracle[first + 16 + i].Value;
            m.GX[i] = Oracle[first + 24 + i].Value; m.GY[i] = Oracle[first + 32 + i].Value; m.GZ[i] = Oracle[first + 40 + i].Value;
        }
        return m;
    }

    // Row i of a chunk: AX[i] AY[i] AZ[i] GX[i] GY[i] GZ[i] (L12).
    private static string Rows(int first)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < 8; ++i)
            sb.Append(string.Join(" ", new[] { 0, 8, 16, 24, 32, 40 }.Select(k => Oracle[first + k + i].Text))).Append('\n');
        return sb.ToString();
    }

    private const string ProcessedHeader = "aX aY aZ gX gY gZ\n";            // L11, 0x005360CC, length 0x12
    private const string RawHeader = "timestamp aX aY aZ gX gY gZ\n";       // L19, 0x0053662C, length 0x1C

    private static IMURawDataChunk Raw(byte order, byte ts, short[] a, short[] g)
        => new() { Order = order, Timestamp = ts, A = a, G = g };

    private static readonly short[] Zero3 = new short[3];

    [Fact]
    public void L11_L19_HeaderLengthsAreTheCitedLengths()
    {
        Assert.Equal(0x12, ProcessedHeader.Length);
        Assert.Equal(0x1C, RawHeader.Length);
    }

    // L4, L9, L10, L11, L12, L13, L14, L15: a sequence change opens imuLog_<seq>.dat with the header, rows follow on
    // every packet, the final chunk (chunkId == numChunks-1) logs and closes.
    // fidelity: M3-041
    [Fact]
    public void L4_to_L15_ProcessedChunksWriteHeaderAndRowsAndCloseOnTheFinalChunk()
    {
        using var rig = new Rig();
        Assert.Equal(96, Oracle.Count);
        rig.Data(Chunk(0, 7, 0, 2)); rig.Tick();
        Assert.Equal(7u, rig.Imu.Counter);
        Assert.True(rig.Imu.IsOpen);
        Assert.Equal(new[] { "imuLog_7.dat" }, rig.Files());
        Assert.True(rig.Logged("info: [Unnamed] Robot.HandleImuData.OpeningLogFile: " + rig.LogsDir + "/imuLog_7.dat"));
        Assert.False(rig.Logged("ClosingLogFile"));
        rig.Data(Chunk(48, 7, 1, 2)); rig.Tick();                       // same sequence: no reopen, no second header
        Assert.False(rig.Imu.IsOpen);                                   // the final chunk closed it
        Assert.Equal(1, rig.Log.Count(l => l.Contains("OpeningLogFile")));
        Assert.True(rig.Logged("info: [Unnamed] Robot.HandleImuData.ClosingLogFile: "));
        Assert.Equal(ProcessedHeader + Rows(0) + Rows(48), rig.Read("imuLog_7.dat"));
        Assert.Equal(7u, rig.Imu.Counter);                              // L15: not reset
    }

    // L4: a first packet whose sequence equals the constructor's counter 0 skips directory, name and open; the rows
    // meet a closed filebuf and set badbit|failbit (L26, L31). A later sequence reopens and clears the state (L24).
    // fidelity: M3-041
    [Fact]
    public void L4_L26_SequenceZeroSkipsOpenAndTheClosedFilebufFailsTheInsertions()
    {
        using var rig = new Rig();
        rig.Data(Chunk(0, 0, 0, 3)); rig.Tick();
        Assert.False(Directory.Exists(rig.LogsDir));
        Assert.False(rig.Logged("OpeningLogFile"));
        Assert.Equal(5, rig.Imu.StreamState);                           // badbit | failbit
        rig.Data(Chunk(0, 1, 0, 3)); rig.Tick();
        Assert.Equal(0, rig.Imu.StreamState);                           // open success clears the state
        Assert.Equal(new[] { "imuLog_1.dat" }, rig.Files());
    }

    // L14: numChunks 0 gives -1, which no chunkId equals: the file stays open until robot removal (L35), which closes it
    // after the subscription vector without a ClosingLogFile diagnostic; the file is complete and released.
    // fidelity: M3-041
    [Fact]
    public void L14_L35_RobotRemovalClosesTheStreamWithoutAClosingDiagnostic()
    {
        using var rig = new Rig();
        rig.Data(Chunk(0, 9, 0, 0)); rig.Tick();
        rig.Data(Chunk(48, 9, 1, 0)); rig.Tick();
        Assert.True(rig.Imu.IsOpen);
        var imu = rig.Imu;
        rig.Remove();                                                   // RemoveRobot -> RobotLifetime.Destroy (+0x518)
        Assert.False(imu.IsOpen);
        Assert.False(rig.Logged("ClosingLogFile"));
        string expected = ProcessedHeader + Rows(0) + Rows(48);
        using (var exclusive = new FileStream(Path.Combine(rig.LogsDir, "imuLog_9.dat"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Assert.Equal(expected.Length, exclusive.Length);
        Assert.Equal(expected, rig.Read("imuLog_9.dat"));
    }

    // L16..L22: raw order 0 opens imuRawLog_<++counter>.dat with the 28-byte header and its first row; every packet is
    // one row "timestamp aX aY aZ gX gY gZ" (the accelerometer before the gyro), order 2 closes after its row.
    // fidelity: M3-041
    [Fact]
    public void L16_to_L22_RawPacketsLogOrderZeroToTwoAndCloseOnOrderTwo()
    {
        using var rig = new Rig();
        rig.Data(Raw(0, 255, new short[] { -32768, 32767, 0 }, new short[] { -1, 1, 12345 })); rig.Tick();
        Assert.Equal(1u, rig.Imu.Counter);
        Assert.True(rig.Imu.IsOpen);
        Assert.True(rig.Logged("info: [Unnamed] Robot.HandleImuRawData.OpeningLogFile: " + rig.LogsDir + "/imuRawLog_1.dat"));
        rig.Data(Raw(1, 0, new short[] { 5, 6, 7 }, new short[] { 8, 9, 10 })); rig.Tick();
        Assert.True(rig.Imu.IsOpen);
        rig.Data(Raw(2, 1, new short[] { -5, -6, -7 }, new short[] { -8, -9, -10 })); rig.Tick();
        Assert.False(rig.Imu.IsOpen);
        Assert.True(rig.Logged("info: [Unnamed] Robot.HandleImuRawData.ClosingLogFile: "));
        Assert.Equal(RawHeader + "255 -32768 32767 0 -1 1 12345\n0 5 6 7 8 9 10\n1 -5 -6 -7 -8 -9 -10\n", rig.Read("imuRawLog_1.dat"));
        Assert.Equal(new[] { "imuRawLog_1.dat" }, rig.Files());
        // L18: the counter is retained; the next burst takes the next number.
        rig.Data(Raw(0, 3, Zero3, Zero3)); rig.Data(Raw(2, 4, Zero3, Zero3)); rig.Tick();
        Assert.Equal(new[] { "imuRawLog_1.dat", "imuRawLog_2.dat" }, rig.Files());
        Assert.Equal(2u, rig.Imu.Counter);
    }

    // L18: the candidate name is rebuilt while a regular file of that name exists (no bound); a directory of that name is
    // not "existing" (stat mode bit 0x8000), so it is chosen and the open fails (L23/L24), leaving the rows suppressed
    // while order 2 still logs and closes (L25).
    // fidelity: M3-041
    [Fact]
    public void L18_RawNameSearchSkipsRegularFilesButNotDirectories()
    {
        using var rig = new Rig();
        Directory.CreateDirectory(rig.LogsDir);
        File.WriteAllText(Path.Combine(rig.LogsDir, "imuRawLog_1.dat"), "keep1");
        File.WriteAllText(Path.Combine(rig.LogsDir, "imuRawLog_2.dat"), "keep2");
        Directory.CreateDirectory(Path.Combine(rig.LogsDir, "imuRawLog_3.dat"));
        rig.Data(Raw(0, 1, Zero3, Zero3)); rig.Tick();
        Assert.Equal(3u, rig.Imu.Counter);                              // 1 and 2 exist; the directory 3 does not count
        Assert.True(rig.Logged("OpeningLogFile: " + rig.LogsDir + "/imuRawLog_3.dat"));
        Assert.False(rig.Imu.IsOpen);
        Assert.Equal(4, rig.Imu.StreamState);                           // failbit only: the open failed (L24)
        Assert.Equal("keep1", rig.Read("imuRawLog_1.dat"));
        Assert.Equal("keep2", rig.Read("imuRawLog_2.dat"));
        rig.Data(Raw(2, 2, Zero3, Zero3)); rig.Tick();
        Assert.True(rig.Logged("Robot.HandleImuRawData.ClosingLogFile"));
        Assert.Equal(4, rig.Imu.StreamState);                           // close of a null FILE is null: failbit ORed (L25)
    }

    // L9, L23: the processed name is not checked for an existing file; the open truncates it ("w").
    // fidelity: M3-041
    [Fact]
    public void L9_L23_ProcessedOpenTruncatesAnExistingFile()
    {
        using var rig = new Rig();
        Directory.CreateDirectory(rig.LogsDir);
        File.WriteAllText(Path.Combine(rig.LogsDir, "imuLog_4.dat"), new string('x', 5000));
        rig.Data(Chunk(0, 4, 0, 1)); rig.Tick();
        Assert.Equal(ProcessedHeader + Rows(0), rig.Read("imuLog_4.dat"));
    }

    // L8, L11: a CreateDirectory failure logs sErrorF "Robot.HandleImuData.CreateDirFailed" with the directory, sets the
    // global error flag, and continues: the OpeningLogFile info is still logged and the open is attempted (and fails).
    // fidelity: M3-041
    [Fact]
    public void L8_ProcessedCreateDirectoryFailureLogsAnErrorAndContinuesToOpen()
    {
        string blocker = Path.Combine(Path.GetTempPath(), "cozmo-imu-blocker-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(blocker, "a file where the persistent directory should be");
        try
        {
            using var rig = new Rig(blocker);
            EngineErrorState.ErrorFlagSet = false;
            rig.Data(Chunk(0, 5, 0, 2)); rig.Tick();
            Assert.True(rig.Logged("error: Robot.HandleImuData.CreateDirFailed: " + blocker + "/imu_logs"));
            Assert.True(EngineErrorState.ErrorFlagSet);
            int error = rig.LogIndex("CreateDirFailed"), opening = rig.LogIndex("OpeningLogFile");
            Assert.True(error >= 0 && opening > error);
            Assert.False(rig.Imu.IsOpen);
            Assert.Equal(4, rig.Imu.StreamState);
        }
        finally { File.Delete(blocker); EngineErrorState.ErrorFlagSet = false; }
    }

    // L17: the raw handler logs "Robot.HandleImuRawData.CreateDirFailed" and continues with the filename search.
    // fidelity: M3-041
    [Fact]
    public void L17_RawCreateDirectoryFailureLogsAnErrorAndContinues()
    {
        string blocker = Path.Combine(Path.GetTempPath(), "cozmo-imu-blocker-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(blocker, "x");
        try
        {
            using var rig = new Rig(blocker);
            EngineErrorState.ErrorFlagSet = false;
            rig.Data(Raw(0, 1, Zero3, Zero3)); rig.Tick();
            Assert.True(rig.Logged("error: Robot.HandleImuRawData.CreateDirFailed: " + blocker + "/imu_logs"));
            Assert.True(EngineErrorState.ErrorFlagSet);
            Assert.True(rig.Logged("OpeningLogFile: " + blocker + "/imu_logs/imuRawLog_1.dat"));
            Assert.Equal(1u, rig.Imu.Counter);
        }
        finally { File.Delete(blocker); EngineErrorState.ErrorFlagSet = false; }
    }

    // L1, L26: one shared counter word. A raw burst sets it to 1, so a processed packet with sequence 1 skips the open;
    // a processed sequence overwrites it, so the next raw burst numbers from there.
    // fidelity: M3-041
    [Fact]
    public void L26_ProcessedAndRawHandlersShareOneCounterWord()
    {
        using var rig = new Rig();
        rig.Data(Raw(0, 1, Zero3, Zero3)); rig.Data(Raw(2, 2, Zero3, Zero3)); rig.Tick();
        Assert.Equal(1u, rig.Imu.Counter);
        rig.Data(Chunk(0, 1, 0, 2)); rig.Tick();                        // sequence 1 == counter 1: no open, no imuLog_1.dat
        Assert.Equal(new[] { "imuRawLog_1.dat" }, rig.Files());
        // F6.2/F2.1: the filebuf is closed but its put area has room, so the rows are buffered and the state stays good.
        Assert.Equal(0, rig.Imu.StreamState);
        rig.Data(Chunk(0, 40, 0, 1)); rig.Tick();                       // sequence 40 stored; the final chunk closes
        Assert.Equal(40u, rig.Imu.Counter);
        rig.Data(Chunk(0, 40, 0, 1)); rig.Tick();                       // same sequence after a successful close: no reopen
        Assert.Equal(new[] { "imuLog_40.dat", "imuRawLog_1.dat" }, rig.Files());
        // F6.2/F5.2: the rows buffered while the FILE was null (the seq-1 packet) head the file, before its header.
        Assert.Equal(Rows(0) + ProcessedHeader + Rows(0), rig.Read("imuLog_40.dat"));
        Assert.Equal(4, rig.Imu.StreamState);                           // that packet's own final-chunk close of a null FILE: failbit (F5.4)
        rig.Data(Raw(0, 1, Zero3, Zero3)); rig.Data(Raw(2, 2, Zero3, Zero3)); rig.Tick();
        Assert.Equal(41u, rig.Imu.Counter);
        Assert.Contains("imuRawLog_41.dat", rig.Files());
    }

    // L23/L24/L26: opening while a file is open fails without touching the old FILE (failbit), the insertions are
    // suppressed, and the final packet still logs and closes the old file; close does not clear the failbit (L25).
    // fidelity: M3-041
    [Fact]
    public void L23_L24_L26_OpenWhileOpenFailsKeepsTheOldFileAndTheFinalPacketClosesIt()
    {
        using var rig = new Rig();
        rig.Data(Chunk(0, 1, 0, 2)); rig.Tick();                        // imuLog_1.dat open, not final
        Assert.True(rig.Imu.IsOpen);
        rig.Data(Raw(0, 1, new short[] { 1, 2, 3 }, new short[] { 4, 5, 6 })); rig.Tick();
        Assert.Equal(2u, rig.Imu.Counter);                              // the raw search incremented the shared word
        Assert.True(rig.Logged("OpeningLogFile: " + rig.LogsDir + "/imuRawLog_2.dat"));
        Assert.Equal(new[] { "imuLog_1.dat" }, rig.Files());            // no raw file was created
        Assert.Equal(4, rig.Imu.StreamState);
        Assert.True(rig.Imu.IsOpen);
        rig.Data(Raw(2, 2, Zero3, Zero3)); rig.Tick();                  // the final raw packet closes the OLD file
        Assert.False(rig.Imu.IsOpen);
        Assert.Equal(4, rig.Imu.StreamState);
        Assert.Equal(ProcessedHeader + Rows(0), rig.Read("imuLog_1.dat"));
    }

    private static readonly short[] ZeroA = new short[3];

    // F6.2 / F2.1 / F3.1 / F1.6: after a close the put area is still in write mode with room [pptr, epptr) = 4095 bytes
    // (F1.4: epptr = buf + 4096 - 1). Zero rows "0 0 0 0 0 0 0\n" are 14 bytes (raw handler: timestamp, six shorts, LF),
    // so 292 packets use 4088 bytes and the 293rd copies its first 7 bytes (4095) and fails on the 8th, the separator
    // space: overflow returns -1 with the FILE null, sputn is short, state |= 5 (F2.4). The next open clears the state
    // (F7.1) and the 4095 stale bytes head that file, before its header (F6.2, F4.1: the first header byte is stored in
    // the spare byte and one fwrite of 4096 bytes goes out).
    // fidelity: M3-041
    [Fact]
    public void F6_2_BufferFillsWhileTheFileIsNullStateBecomes5AtTheByteRoomRunsOutAndStaleBytesHeadTheNextFile()
    {
        using var rig = new Rig();
        rig.Data(Raw(0, 0, ZeroA, ZeroA)); rig.Data(Raw(2, 0, ZeroA, ZeroA)); rig.Tick();   // raw_1 complete, closed
        Assert.False(rig.Imu.IsOpen);
        Assert.Equal(0, rig.Imu.StreamState);                           // F5.4: a successful close leaves the state
        for (int i = 0; i < 292; ++i) rig.Data(Raw(1, 0, ZeroA, ZeroA));
        rig.Tick();
        Assert.Equal(0, rig.Imu.StreamState);                           // 292 * 14 = 4088 <= 4095: all buffered
        rig.Data(Raw(1, 0, ZeroA, ZeroA)); rig.Tick();
        Assert.Equal(5, rig.Imu.StreamState);                           // 4088 + 7 = 4095 bytes, then the 8th byte fails
        Assert.Equal(new[] { "imuRawLog_1.dat" }, rig.Files());         // nothing reached any file
        rig.Data(Raw(0, 0, ZeroA, ZeroA)); rig.Data(Raw(2, 0, ZeroA, ZeroA)); rig.Tick();
        Assert.Equal(0, rig.Imu.StreamState);                           // open success cleared it
        string stale = string.Concat(Enumerable.Repeat("0 0 0 0 0 0 0\n", 293)).Substring(0, 4095);
        string expected = stale + RawHeader + "0 0 0 0 0 0 0\n" + "0 0 0 0 0 0 0\n";
        Assert.Equal(expected, rig.Read("imuRawLog_2.dat"));
        Assert.Equal(RawHeader + "0 0 0 0 0 0 0\n" + "0 0 0 0 0 0 0\n", rig.Read("imuRawLog_1.dat"));
    }

    // F1.5 / F3.1: a stream never opened has pptr = epptr = 0; the first insertion calls overflow, which returns -1 for a
    // null FILE: state 5 at once and nothing buffered (the next file has no stale head).
    // fidelity: M3-041
    [Fact]
    public void F1_5_NeverOpenedStreamFailsAtTheFirstByteAndBuffersNothing()
    {
        using var rig = new Rig();
        rig.Data(Raw(1, 7, ZeroA, ZeroA)); rig.Tick();
        Assert.Equal(5, rig.Imu.StreamState);
        rig.Data(Raw(0, 0, ZeroA, ZeroA)); rig.Data(Raw(2, 0, ZeroA, ZeroA)); rig.Tick();
        Assert.Equal(RawHeader + "0 0 0 0 0 0 0\n" + "0 0 0 0 0 0 0\n", rig.Read("imuRawLog_1.dat"));
    }

    // G1: the four OpeningLogFile / ClosingLogFile logs use sChanneledInfoF with channel "Unnamed"; opening's format is "%s"
    // with the file name, closing's format is empty.
    // fidelity: M3-041
    [Fact]
    public void G1_LogLinesCarryTheUnnamedChannelAndTheFormatText()
    {
        using var rig = new Rig();
        rig.Data(Chunk(0, 3, 0, 1)); rig.Tick();
        rig.Data(Raw(0, 0, ZeroA, ZeroA)); rig.Data(Raw(2, 0, ZeroA, ZeroA)); rig.Tick();
        List<string> log; lock (rig.Log) log = rig.Log.ToList();
        Assert.Contains("info: [Unnamed] Robot.HandleImuData.OpeningLogFile: " + rig.LogsDir + "/imuLog_3.dat", log);
        Assert.Contains("info: [Unnamed] Robot.HandleImuData.ClosingLogFile: ", log);
        Assert.Contains("info: [Unnamed] Robot.HandleImuRawData.OpeningLogFile: " + rig.LogsDir + "/imuRawLog_4.dat", log);
        Assert.Contains("info: [Unnamed] Robot.HandleImuRawData.ClosingLogFile: ", log);
    }

    private static int PrefixCount(string path) => path.Where((c, i) => c == '/' && i > 0).Count() + 1;

    // F8.4: an empty path returns true without touching the disk.
    // fidelity: M3-041
    [Fact]
    public void F8_4_CreateDirectoryOfAnEmptyPathIsTrue() => Assert.True(EngineFileUtils.CreateDirectory(""));

    // F8.4/F8.5: each '/'-prefix (the slash at index 0 skipped) plus the whole path is one iteration; reaching iteration 200
    // returns false even though that directory was made; at most 199 components succeed.
    // fidelity: M3-041
    [Fact]
    public void F8_5_CreateDirectoryCapIs199Components()
    {
        string root = Path.Combine(Path.GetTempPath(), "cozmo-imu-cap-" + Guid.NewGuid().ToString("N"));
        try
        {
            string p199 = root;
            while (PrefixCount(p199) < 199) p199 += "/d";
            Assert.Equal(199, PrefixCount(p199));
            Assert.True(EngineFileUtils.CreateDirectory(p199));
            Assert.True(Directory.Exists(p199));
            string p200 = p199 + "/d";
            Assert.Equal(200, PrefixCount(p200));
            Assert.False(EngineFileUtils.CreateDirectory(p200));
            Assert.True(Directory.Exists(p200));                        // the 200th mkdir ran; the cap returns false after it
        }
        finally { try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch (IOException) { } }
    }

    // F8.4: an existing directory prefix is untouched; a prefix that exists as a file makes mkdir fail -> false.
    // fidelity: M3-041
    [Fact]
    public void F8_4_CreateDirectoryFailsWhenAPrefixIsAFile()
    {
        string root = Path.Combine(Path.GetTempPath(), "cozmo-imu-pfx-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(root + "/f", "x");
            Assert.False(EngineFileUtils.CreateDirectory(root + "/f/sub"));
            Assert.True(EngineFileUtils.CreateDirectory(root + "/ok/sub"));
            Assert.True(Directory.Exists(root + "/ok/sub"));
            Assert.True(EngineFileUtils.CreateDirectory(root + "/ok/sub"));   // all exist: true
        }
        finally { Directory.Delete(root, true); }
    }

    // F8.4: mkdir(prefix, 0x1C0) = 0700 (Unix only; the Windows host has no POSIX modes).
    // fidelity: M3-041
    [Fact]
    public void F8_4_CreateDirectoryMakesDirectoriesWithMode0700OnUnix()
    {
        if (OperatingSystem.IsWindows()) return;
        string root = Path.Combine(Path.GetTempPath(), "cozmo-imu-mode-" + Guid.NewGuid().ToString("N"));
        try
        {
            Assert.True(EngineFileUtils.CreateDirectory(root + "/a"));
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(root + "/a"));
        }
        finally { try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch (IOException) { } }
    }

    // F8.1/F8.2: DirectoryExists = stat ok and bit 0x4000; FileExists = stat ok and bit 0x8000, so a directory is not a file.
    // fidelity: M3-041
    [Fact]
    public void F8_2_FileExistsIsFalseForADirectoryAndDirectoryExistsIsFalseForAFile()
    {
        string root = Path.Combine(Path.GetTempPath(), "cozmo-imu-ex-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(root + "/f", "x");
            Assert.False(EngineFileUtils.FileExists(root));
            Assert.True(EngineFileUtils.DirectoryExists(root));
            Assert.True(EngineFileUtils.FileExists(root + "/f"));
            Assert.False(EngineFileUtils.DirectoryExists(root + "/f"));
            Assert.False(EngineFileUtils.FileExists(root + "/missing"));
            Assert.False(EngineFileUtils.DirectoryExists(root + "/missing"));
        }
        finally { Directory.Delete(root, true); }
    }

    // I2: both tags are subscribed with no debug/SDK gate; after removal the retired subscriptions run nothing.
    // fidelity: M3-041
    [Fact]
    public void I2_BothHandlersAreLiveWithoutAnyGateAndAfterRemovalNothingRuns()
    {
        using var rig = new Rig();
        var robot = rig.Engine.Robot!;
        rig.Data(Raw(0, 1, Zero3, Zero3)); rig.Tick();
        Assert.Equal(1u, rig.Imu.Counter);
        rig.Remove();
        robot.DeliverMessage(Raw(0, 1, Zero3, Zero3));                  // retired subscription: no callback
        Assert.Equal(1u, robot.ImuLog.Counter);
    }
}
