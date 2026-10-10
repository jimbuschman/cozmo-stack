using System.Net;
using System.Security.Cryptography;
using System.Text;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Vision;
using Cozmo.Transport;
using Xunit;
using FaceMsg = Cozmo.Protocol.FaceImage;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// The M3 device layer against re-analysis/inventory/M3-device.md: every expected value comes from a row of that
/// inventory (Appendix A rows A*, B*, C*; Appendix B rows 1a..1q, 2a..2f, 3a..3b), named in each test, never from what
/// the code returns.
/// </summary>
[Collection("SteppedBehavior missing-report statics")]
public class M3DeviceTests
{
    [Fact]
    public async Task M3_040_CompetingHostRegistrationDoesNotInvokeTheExecutingFrontTwice()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        DrainCalibrationRead(rig);
        var nv = rig.Robot.Engine.NvStorage!;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int firstCalls = 0, secondCalls = 0;
        var execution = Task.Run(() => nv.OnIdle(() =>
        {
            Interlocked.Increment(ref firstCalls);
            entered.SetResult();
            release.Task.GetAwaiter().GetResult();
        }));
        await entered.Task;
        try
        {
            nv.OnIdle(() => Interlocked.Increment(ref secondCalls));
            Assert.Equal(1, firstCalls);
            Assert.Equal(0, secondCalls);
        }
        finally { release.SetResult(); }
        await execution;
        Assert.Equal(1, firstCalls);
        Assert.Equal(1, secondCalls);
    }

    // Adopted I5: gate once, invoke-before-pop; callbacks can enqueue work without
    // stopping the current drain. Expectations come from 00645B08..00645BAE.
    [Fact]
    public void M3_040_IdleDrainKeepsItsEntryPredicateAcrossCallbacks()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        DrainCalibrationRead(rig);
        var nv = rig.Robot.Engine.NvStorage!;
        var order = new List<string>();
        int idleLogs = nv.Log.Count(l => l ==
            "debug: NVStorageComponent.ProcessOnIdleCallbacks.ProcessingCallback: ");
        nv.Read(0x80000001, _ =>
        {
            order.Add("terminal");
            Assert.False(nv.IsIdle); // SetState(0) follows the terminal callback.
        });
        rig.Tick();
        nv.OnIdle(() =>
        {
            order.Add("first");
            nv.Read(0x80000001, null);
            nv.OnIdle(() => order.Add("appended")); // queue gate prevents recursion
        });
        nv.OnIdle(() => order.Add("second"));
        rig.Data(new NVOpResult { Tag = 0x80000001, Op = 0, Result = -1 });
        rig.Tick();
        Assert.Equal(new[] { "terminal", "first", "second", "appended" }, order);
        Assert.Equal(3, nv.Log.Count(l => l ==
            "debug: NVStorageComponent.ProcessOnIdleCallbacks.ProcessingCallback: ") - idleLogs);
    }

    [Fact]
    public void M3_040_ThrowingIdleCallbackRemainsAtTheFront()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        DrainCalibrationRead(rig);
        var nv = rig.Robot.Engine.NvStorage!;
        int calls = 0;
        Assert.Throws<InvalidOperationException>(() => nv.OnIdle(() =>
        {
            calls++;
            throw new InvalidOperationException("callback");
        }));
        Assert.Throws<InvalidOperationException>(nv.ProcessOnIdle);
        Assert.Equal(2, calls); // 645B9A invoke precedes 645BA0 pop
    }

    [Fact]
    public void M3_038_DisposeDropsPendingFunctionsAndUnsubscribesTheLiveReplyEntry()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        DrainCalibrationRead(rig);
        var nv = rig.Robot.Engine.NvStorage!;
        int calls = 0;
        nv.Read(0x80000001, _ => calls++);
        rig.Tick();
        nv.Read(0x80000001, _ => calls++);
        nv.OnIdle(() => calls++);
        nv.Dispose();
        rig.Data(new NVOpResult { Tag = 0x80000001, Op = 0, Result = -1 });
        rig.Tick();
        nv.ProcessOnIdle();
        Assert.Equal(0, calls);
        Assert.True(nv.IsIdle);
        Assert.Empty(nv.QueuedTags);
    }

    // Checked Opus M3-026/M3-030 rows: 00644E3A..00644E4C and 006436DA.
    [Fact]
    public void M3_026_M3_030_CheckedReadLogsRunThroughTheLiveReplyEntry()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        DrainCalibrationRead(rig);
        var nv = rig.Robot.Engine.NvStorage!;
        int before = NvCommands(rig).Count;
        bool callbackSawDebug = false;
        nv.Read(0x80000001, _ => callbackSawDebug = nv.Log.Last() ==
            "debug: NVStorageComponent.HandleNVOpResult.ExecutingReadCallback: NVEntry_CameraCalib");
        Assert.Equal(before, NvCommands(rig).Count); // log on enqueue; send still belongs to Update
        Assert.Equal("info: NVStorageComponent.Read.QueueingReadRequest: NVEntry_CameraCalib", nv.Log.Last());
        rig.Tick();
        rig.Data(new NVOpResult { Tag = 0x80000001, Op = 0, Result = -1 });
        rig.Tick();
        Assert.True(callbackSawDebug);
        Assert.Contains("info: NVStorageComponent.HandleNVOpResult.ReadEntryNotFound: BaseTag: NVEntry_CameraCalib, Tag: 0x80000001, result: NV_NOT_FOUND", nv.Log);

        nv.Read(1, _ => { });
        Assert.Contains("warning: NVStorageComponent.Read.InvalidTag: Tag: 0x1", nv.Log);
        int callbacks = nv.Log.Count(l => l.Contains("ExecutingReadCallback"));
        nv.Read(0x80000001, null);
        rig.Tick();
        rig.Data(new NVOpResult { Tag = 0x80000001, Op = 0, Result = 3, Data = new byte[] { 1 } });
        rig.Tick();
        Assert.Equal(callbacks, nv.Log.Count(l => l.Contains("ExecutingReadCallback")));
        rig.Data(new NVOpResult { Tag = 0x80000001, Op = 0, Result = 0 });
        rig.Tick();
        Assert.Equal(callbacks, nv.Log.Count(l => l.Contains("ExecutingReadCallback")));
        nv.Read(0xC0000001, null);
        rig.Tick();
        rig.Data(new NVOpResult { Tag = 0xC0000001, Op = 0, Result = -8 });
        rig.Tick();
        Assert.Contains("info: NVStorageComponent.ResendLastCommand.Retry: Tag: 0xc0000001, Op: NVOP_READ, Attempt: 1", nv.Log);
        Assert.Contains("info: NVStorageComponent.HandleNVOpResult.ResentFailedRead: Tag 0xc0000001 resent due to NV_LOOP", nv.Log);
    }

    // Opus M3-031: shared resend helper 00645C6A; write caller 006431A2.
    [Theory]
    [InlineData((byte)2, "NVOP_ERASE", "0x182000", "0x182000")]      // saved tag = the erase tag (WB3); the ack's r8 is the message tag
    [InlineData((byte)3, "NVOP_WIPEALL", "0x0", "0x198000")]        // WB8: saved tag 0; WD2: a WIPEALL ack's tag is 0x198000 (the write chunk: M3_043_WD7_*)
    public void M3_031_CheckedWriteFamilyRetryLogsAndExhaustion(byte op, string opName, string savedTag, string ackTag)
    {
        using var rig = new Rig();
        rig.ToSuccess();
        DrainCalibrationRead(rig);
        var nv = rig.Robot.Engine.NvStorage!;
        NvResult? completion = null;
        if (op == NvStorageComponent.OpErase) nv.Erase(0x182000, r => completion = r);
        else nv.WipeAll(r => completion = r);
        rig.Tick();
        int initial = NvCommands(rig).Count;
        int logStart = nv.Log.Count;
        for (int attempt = 1; attempt <= 7; attempt++) // native ctor limit8; eight total transmissions
        {
            rig.Data(new NVOpResult { Tag = 0x182000, Op = op, Result = -8 });
            rig.Tick();
            Assert.Null(completion);
            Assert.Equal(initial + attempt, NvCommands(rig).Count);
            Assert.Contains($"info: NVStorageComponent.ResendLastCommand.Retry: Tag: {savedTag}, Op: {opName}, Attempt: {attempt}", nv.Log);
            Assert.Contains($"info: NVStorageComponent.HandleNVOpResult.ResentFailedWrite: Tag {ackTag} resent due to NV_LOOP, op: {opName}", nv.Log);
        }
        rig.Data(new NVOpResult { Tag = 0x182000, Op = op, Result = -8 });
        rig.Tick();
        Assert.Equal(-8, completion!.Value.Result);
        Assert.Equal(initial + 7, NvCommands(rig).Count);
        Assert.Contains($"error: NVStorageComponent.ResendLastCommand.NumRetriesExceeded: Tag: {savedTag}, Op: {opName}, Attempts: 8", nv.Log);
        var retryLogs = nv.Log.Skip(logStart).Where(l => l.Contains("ResendLastCommand") || l.Contains("ResentFailedWrite")).ToArray();
        Assert.Equal(15, retryLogs.Length); // Retry then ResentFailedWrite, seven pairs; final exceeded
        Assert.Contains(".Retry:", retryLogs[0]);
        Assert.Contains(".ResentFailedWrite:", retryLogs[1]);
    }

    // ================================================================== B-M3M4 build: the NV saved command (M3-027 N1..N13),
    // the completion order (M3-030 R, M3-031 W) and the retry error flag (M3-031 N9). Every expected value is a number or text
    // from the cited instructions of libcozmoEngine.so; the tests drive the live reply entry (Engine.Tick, NVOpResult).

    private static byte[] Pattern(int n)
    {
        var b = new byte[n];
        for (int i = 0; i < n; i++) b[i] = (byte)(1 + i * 7);
        return b;
    }

    /// <summary>Writes through the live entry: Update state 0 sends it, the ack completes it, the component is idle again.</summary>
    private static void WriteAndAck(Rig rig, uint tag, byte[] data)
    {
        var nv = rig.Robot.Engine.NvStorage!;
        NvResult? done = null;
        Assert.Equal(1, nv.Write(tag, data, r => done = r));
        rig.Tick();
        rig.Data(new NVOpResult { Tag = tag, Op = NvStorageComponent.OpWrite, Result = 0 });
        rig.Tick();
        Assert.NotNull(done);
    }

    private static NVCommand ReadAndGetCommand(Rig rig, uint tag, Action<NvResult>? callback = null)
    {
        rig.Robot.Engine.NvStorage!.Read(tag, callback);
        rig.Tick();
        return NvCommands(rig)[^1];
    }

    /// <summary>N1 (0x006428AE..0x006428B4: strd 0,0,[+0xE8]; str 0,[+0xF0]) and N13 (0x0064536A: Length = 0x400, a constant): a READ before any WRITE has no Data.</summary>
    [Fact]
    public void M3_027_N1_N13_AReadBeforeAnyWriteCarriesNoData()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        DrainCalibrationRead(rig);
        var cmd = ReadAndGetCommand(rig, 0x182000);
        Assert.Empty(cmd.Data);
        Assert.Equal(0x400, cmd.Length);
        Assert.Equal(0, cmd.Unknown);                       // +0xE5, written only by the constructor (0x006428AA)
    }

    /// <summary>
    /// N5..N8 (0x00645822..0x00645988), the chunk assembly (the live write path drives it one chunk per Update, M3_043_WB15_*), exercised directly. A one-chunk payload leaves the 16-byte header and the payload; the header is the magic 0x435A4D4F little-endian,
    /// a zero u32, the source length and a zero u16 at byte 12 (0x00645860..0x0064587E). Bytes 14 and 15 are never written (N6), so they are not
    /// asserted. The saved Length is the vector's size (0x00645918).
    /// </summary>
    [Theory]
    [InlineData(100, 116)]
    [InlineData(1008, 1024)]       // 0x3F0 payload bytes after the header: still one chunk (0x0064588A..0x00645898)
    public void M3_027_N5_N8_AOneChunkAssemblyIsTheHeaderAndThePayload(int payload, int expectedDataLength)
    {
        var data = Pattern(payload);
        var saved = new NvStorageComponent.SavedCommand();
        int taken = saved.AssembleWriteChunk(0x182000, data, 0, factoryDataFlag: false);
        Assert.Equal(payload, taken);
        Assert.Equal(expectedDataLength, saved.Data.Length);
        Assert.Equal(expectedDataLength, saved.Length);
        var expectedHeader = new byte[] { 0x4F, 0x4D, 0x5A, 0x43, 0, 0, 0, 0, (byte)(payload & 0xFF), (byte)(payload >> 8), 0, 0, 0, 0 };
        Assert.Equal(expectedHeader, saved.Data[..14]);
        Assert.Equal(data, saved.Data[16..]);
        Assert.Equal(NvStorageComponent.OpWrite, saved.Op);
        Assert.Equal(0x182000u, saved.Tag);
        Assert.Equal(new[] { 14, 15 }, NvStorageComponent.HeaderNeverWrittenBytes);
    }

    /// <summary>
    /// N5..N7: the first chunk is min(remaining, 0x3F0) after the header, later ones min(remaining, 0x400) with no header, and each replaces the
    /// vector's contents (0x00645766, 0x00645840..0x00645898).
    /// </summary>
    [Theory]
    [InlineData(1009, 1008, 1)]
    [InlineData(2500, 1008, 1024)]
    public void M3_027_N5_N7_ChunksAreSizedAndEachReplacesTheVector(int payload, int firstChunk, int secondChunk)
    {
        var data = Pattern(payload);
        var saved = new NvStorageComponent.SavedCommand();
        Assert.Equal(firstChunk, saved.AssembleWriteChunk(0x182000, data, 0, false));
        Assert.Equal(16 + firstChunk, saved.Data.Length);
        Assert.Equal(secondChunk, saved.AssembleWriteChunk(0x182400, data, firstChunk, false));
        Assert.Equal(data[firstChunk..(firstChunk + secondChunk)], saved.Data);      // no header, the first chunk is gone
        Assert.Equal(0x182400u, saved.Tag);
    }

    /// <summary>
    /// N9 (0x00645CD4..0x00645CEE): a resend copies the saved header and the saved Data, so the retransmitted READ is the first one.
    /// The Retry log names the saved tag and op (0x00645C88..0x00645C8E).
    /// </summary>
    [Fact]
    public void M3_027_N9_AResendCarriesTheSavedHeaderAndData()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        DrainCalibrationRead(rig);
        var first = ReadAndGetCommand(rig, 0x182000, _ => { });
        int count = NvCommands(rig).Count;
        rig.Data(new NVOpResult { Tag = 0x182000, Op = 0, Result = -8 });
        rig.Tick();
        var cmds = NvCommands(rig);
        Assert.Equal(count + 1, cmds.Count);
        var resent = cmds[^1];
        Assert.Equal((first.Tag, first.Length, first.Op, first.Unknown), (resent.Tag, resent.Length, resent.Op, resent.Unknown));
        Assert.Equal(first.Data, resent.Data);
        Assert.Empty(resent.Data);
        Assert.Contains("info: NVStorageComponent.ResendLastCommand.Retry: Tag: 0x182000, Op: NVOP_READ, Attempt: 1", rig.Robot.Engine.NvStorage!.Log);
    }

    /// <summary>
    /// N10 (0x00643888..0x006438BC): the re-request stores the response tag, op 0 and Length = total + 16 into the saved header and
    /// copies the saved Data; a reply never repopulates it. A later resend (N9) sends the re-request again.
    /// </summary>
    [Fact]
    public void M3_027_N10_TheRestRequestAndALaterResendCarryTheSavedData()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        DrainCalibrationRead(rig);
        ReadAndGetCommand(rig, 0x182000, _ => { });
        var blob = new byte[16 + 100];
        NvHeader(0x800, 0x435A4D4F).CopyTo(blob, 0);
        rig.Data(new NVOpResult { Tag = 0x182000, Op = 0, Result = NvStorageComponent.ResultMore, Length = 0, Data = blob });
        rig.Tick();
        var rest = NvCommands(rig)[^1];
        Assert.Equal(0x810, rest.Length);
        Assert.Equal(0x182000u, rest.Tag);
        Assert.Empty(rest.Data);                              // the saved vector, not the 100 bytes the reply carried
        int count = NvCommands(rig).Count;
        rig.Data(new NVOpResult { Tag = 0x182000, Op = 0, Result = -8 });
        rig.Tick();
        var resent = NvCommands(rig)[^1];
        Assert.Equal(count + 1, NvCommands(rig).Count);
        Assert.Equal(0x810, resent.Length);                   // the saved header was changed by the re-request
        Assert.Equal(rest.Data, resent.Data);
    }

    /// <summary>
    /// N2 (0x00642B52..0x00642B60) and N11 (0x0064575A..0x006457BC): SetState(0) clears only +0x48, +0x1C and +0x78, and the timeout
    /// invokes the callback and sets state 0, so neither a completion nor a timeout clears the saved Data.
    /// </summary>
    [Fact]
    public void M3_027_N2_N11_ACompletionAndATimeoutLeaveTheSavedData()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        DrainCalibrationRead(rig);
        rig.Data(new SyncTimeAck());
        rig.Data(new RobotState { Timestamp = 1000, PoseOriginId = 1 });
        rig.Tick();
        var nv = rig.Robot.Engine.NvStorage!;
        var first = ReadAndGetCommand(rig, 0x182000, _ => { });
        rig.Data(new NVOpResult { Tag = 0x182000, Op = 0, Result = -1 });             // a completion
        rig.Tick();
        NvResult? got = null;
        var second = ReadAndGetCommand(rig, 0x182000, r => got = r);
        Assert.Equal(first.Data, second.Data);
        rig.Data(new RobotState { Timestamp = 7000, PoseOriginId = 1 });             // past the 5 s deadline: the timeout
        rig.Tick();
        Assert.Equal(-4, got!.Value.Result);
        var third = ReadAndGetCommand(rig, 0x182000);
        Assert.Equal(first.Data, third.Data);
        Assert.Empty(third.Data);
    }

    /// <summary>N12 (0x00643F26..0x00643F44) and N1: a removed robot's component frees the saved Data; the next one starts with none.</summary>
    [Fact]
    public void M3_027_N12_N1_ADisconnectFreesTheSavedData()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        DrainCalibrationRead(rig);
        var nv = rig.Robot.Engine.NvStorage!;
        nv.OnDisconnected();
        Assert.Empty(ReadAndGetCommand(rig, 0x182000).Data);
    }

    /// <summary>N4 (0x006453B8..0x006453D2): after the send, ProcessRequest logs "StartTag: 0x%x, Length: %u" with the saved tag and length.</summary>
    [Fact]
    public void M3_027_N4_TheReadSendLogsItsSavedTagAndLength()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        DrainCalibrationRead(rig);
        ReadAndGetCommand(rig, 0x182000);
        Assert.Contains("debug: NVStorageComponent.ProcessRequest.SendingRead: StartTag: 0x182000, Length: 1024", rig.Robot.Engine.NvStorage!.Log);
    }

    /// <summary>
    /// W2 (0x00643384..0x00643420) in the engine's order: the broadcast (+0x40) - one BroadcastNVStorageOpResult whatever the result's
    /// sign (tag, result, op, index 0, no data) - then the debug log 0x006433C6 and the callback (+0x38), then the backup call
    /// (WriteDataForTag(tag, result, op == 1), an M15 recipient this stack does not have, so the MISSING line stands in for it).
    /// </summary>
    [Theory]
    [InlineData((sbyte)0)]
    [InlineData((sbyte)-6)]
    public void M3_031_W2_AWriteCompletionBroadcastsThenRunsTheCallbackThenTheBackup(sbyte result)
    {
        using var rig = new Rig();
        rig.ToSuccess();
        DrainCalibrationRead(rig);
        var nv = rig.Robot.Engine.NvStorage!;
        var order = new List<string>();
        var broadcasts = new List<NVStorageOpResult>();
        nv.NVStorageOpResultBroadcast += b => { order.Add("broadcast"); broadcasts.Add(b); };
        nv.Erase(0x182000, r => order.Add("callback:" + nv.Log[^1]), broadcast: true);
        rig.Tick();
        rig.Data(new NVOpResult { Tag = 0x182000, Op = NvStorageComponent.OpErase, Result = result });
        rig.Tick();
        Assert.Equal(new[] { "broadcast", "callback:debug: NVStorageComponent.HandleNVOpResult.ExecutingWriteCallback: NVEntry_GameUnlocks" }, order);
        var b0 = Assert.Single(broadcasts);
        Assert.Equal((0x182000u, NvStorageComponent.OpErase, result, 0), (b0.Tag, b0.Op, b0.Result, b0.Index));
        Assert.Empty(b0.Data);
        var log = nv.Log;
        int cb = log.ToList().FindIndex(l => l.Contains("ExecutingWriteCallback"));
        int backup = log.ToList().FindIndex(l => l.StartsWith("MISSING: RobotDataBackupManager::WriteDataForTag(0x182000, " + result + ", False)"));
        Assert.True(cb >= 0 && backup > cb, "the backup call follows the callback");
    }

    /// <summary>
    /// R1 (0x0064302C..0x00643034): a WIPEALL response (op 3) is normalised to 0x198000, so the callback is named NVEntry_NEXT_SLOT
    /// (EnumToString 0x007CEF34..0x007CF06C) and W2 passes that tag to the broadcast; the backup call is WipeAll (0x00643406).
    /// </summary>
    [Fact]
    public void M3_030_R1_M3_031_W2_AWipeAllResponseIsNamedByTheSentinelTag()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        DrainCalibrationRead(rig);
        var nv = rig.Robot.Engine.NvStorage!;
        var broadcasts = new List<NVStorageOpResult>();
        nv.NVStorageOpResultBroadcast += broadcasts.Add;
        nv.Request(0x182000, 0, NvStorageComponent.OpWipeAll, Array.Empty<byte>(), _ => { }, broadcast: true);
        rig.Tick();
        rig.Data(new NVOpResult { Tag = 0x182000, Op = NvStorageComponent.OpWipeAll, Result = 0 });
        rig.Tick();
        Assert.Contains("debug: NVStorageComponent.HandleNVOpResult.ExecutingWriteCallback: NVEntry_NEXT_SLOT", nv.Log);
        Assert.Equal(0x198000u, Assert.Single(broadcasts).Tag);
        Assert.Contains(nv.Log, l => l.StartsWith("MISSING: RobotDataBackupManager::WipeAll"));
    }

    /// <summary>
    /// R3 (0x006437BE..0x006437D2) and N9 (0x00645D62..0x00645D76): after sErrorF the engine stores _errG and then calls the debug-break
    /// gate, for the 1000-chunk bound of the read broadcast and for the exhausted retry counter.
    /// </summary>
    [Fact]
    public void M3_030_R3_M3_031_N9_TheErrorFlagIsStoredByTheLoopBoundAndByExhaustedRetries()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        DrainCalibrationRead(rig);
        var nv = rig.Robot.Engine.NvStorage!;
        EngineErrorState.ErrorFlagSet = false;
        nv.Read(0x80010000, null, null, broadcast: true);
        rig.Tick();
        rig.Data(new NVOpResult { Tag = 0x80010000, Op = 0, Result = 0, Length = 999, Data = new byte[1024] });
        rig.Tick();
        Assert.Contains(nv.Log, l => l.Contains("LoopBoundOverflow"));
        Assert.True(EngineErrorState.ErrorFlagSet);

        EngineErrorState.ErrorFlagSet = false;
        nv.Read(0x182000, _ => { });
        rig.Tick();
        for (int i = 0; i < 7; i++) { rig.Data(new NVOpResult { Tag = 0x182000, Op = 0, Result = -8 }); rig.Tick(); }
        Assert.False(EngineErrorState.ErrorFlagSet);          // seven resends: no error yet
        rig.Data(new NVOpResult { Tag = 0x182000, Op = 0, Result = -8 });
        rig.Tick();
        Assert.Contains("error: NVStorageComponent.ResendLastCommand.NumRetriesExceeded: Tag: 0x182000, Op: NVOP_READ, Attempts: 8", nv.Log);
        Assert.True(EngineErrorState.ErrorFlagSet);
        EngineErrorState.ErrorFlagSet = false;
    }

    /// <summary>
    /// G1 (0x00513C5C..0x00513C62, 0x00513DA6..0x00513DC2): until the first full state is handled Robot::Update logs the channeled debug
    /// line "Waiting for first full robot state to be handled" and returns. G2 (0x00513C7E..0x00513C9A): a non-zero UpdateAllResults
    /// warns Robot.Update.VisionComponentUpdateFail (empty format) and returns.
    /// </summary>
    [Fact]
    public void M3_032_G1_G2_RobotUpdateLogsItsTwoGates()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        var logs = new List<string>();
        rig.Engine.LogLine += l => { lock (logs) logs.Add(l); };
        rig.Tick();
        Assert.Contains("debug: Robot.Update: Waiting for first full robot state to be handled", logs);
        logs.Clear();
        SendFirstFullState(rig);
        logs.Clear();
        rig.Tick();
        Assert.DoesNotContain("debug: Robot.Update: Waiting for first full robot state to be handled", logs);
        rig.Engine.VisionUpdateAllResultsFailed = () => true;
        rig.Tick();
        Assert.Contains("warning: Robot.Update.VisionComponentUpdateFail: ", logs);
    }

    /// <summary>
    /// R4 (0x004D7F40..0x004D7FC6, the pair tables at 0x00C81014 and 0x00C81064, read from the binary) and R5 (0x00643C76..0x00643CE0): the finite key
    /// universe is 10 non-factory keys, 23 factory keys, and 0xDE000 and 0xDE030 from InitSizeTable. R6 (0x006441F8..0x00644340, EnumToString
    /// 0x007CEE38..0x007CF0A8): normalising any of them returns the key itself and every one has a name, so the callback's "%s" is never null.
    /// </summary>
    [Fact]
    public void M3_030_R4_R5_R6_EveryInitialisedKeyIsItsOwnBaseAndHasAName()
    {
        var keys = new uint[]
        {
            0x180000, 0x181000, 0x182000, 0x183000, 0x184000, 0x194000, 0x195000, 0x196000, 0x197000, 0x198000,            // R4: the normal pairs
            0x80000000, 0x80000001, 0x80000002, 0x80000003, 0x80000004, 0x80000005, 0x80000006, 0x80000007, 0x80000008,
            0x80000010, 0x80000011, 0x80000012, 0x80010000, 0x80020000, 0x80030000, 0x80040000, 0x80050000, 0x80060000,
            0x80100000, 0x80110000, 0xC0000000, 0xC0000001, 0xC0000004,                                                    // R4: the factory pairs
            0xDE000, 0xDE030,                                                                                              // R5
        };
        foreach (uint key in keys)
        {
            Assert.Equal(key, NvStorageComponent.GetBaseEntryTag(key));
            Assert.NotNull(NvStorageComponent.NvEntryTagName(key));
        }
    }

    // ================================================================== M3-043: the NV write side (rows WA1..WA15, WB1..WB21, WC1..WC5, WD1..WD9 of
    // research/20261010-nv-write-dispatch-rows.md). Expected values are the rows' addresses, texts and numbers, through the live engine entry.

    private static string[] LogSince(NvStorageComponent nv, int start) => nv.Log.Skip(start).ToArray();

    /// <summary>Synchronises the robot clock (robot+0x2C) to <paramref name="timestamp"/> through a RobotState, as a connected robot does.</summary>
    private static void SetRobotClock(Rig rig, uint timestamp)
    {
        rig.Data(new RobotState { Timestamp = timestamp, PoseOriginId = 1 });
        rig.Tick();
    }

    /// <summary>
    /// WA6, WA9, WA11 (0x00644520..0x006446B8, 0x006446F0..0x0064471E): every check is evaluated; an invalid tag warns InvalidTag with the name and the
    /// tag, the size check still runs (GetMaxSizeForEntryTag warns for a tag with no key), and the failure broadcasts (-6, op 1), then calls the
    /// callback with -6, and returns false. Nothing is queued or sent.
    /// </summary>
    [Fact]
    public void M3_043_WA6_WA11_AnInvalidTagFailsEveryCheckThenBroadcastsThenCallsBack()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        DrainCalibrationRead(rig);
        var nv = rig.Robot.Engine.NvStorage!;
        var order = new List<string>();
        nv.NVStorageOpResultBroadcast += b => order.Add($"broadcast:{b.Tag:x}:{b.Op}:{b.Result}:{b.Index}:{b.Data.Length}");
        int start = nv.Log.Count;
        int sent = NvCommands(rig).Count;
        Assert.Equal(0, nv.Write(1, new byte[] { 1, 2, 3, 4 }, r => order.Add("callback:" + r.Result), broadcast: true));
        Assert.Equal(new[] { "broadcast:1:1:-6:0:0", "callback:-6" }, order);
        var log = LogSince(nv, start);
        Assert.Contains(log, l => l.StartsWith("warning: NVStorageComponent.Write.InvalidTag: Tag: "));
        Assert.Contains(log, l => l.EndsWith("(0x1)"));
        Assert.Contains("warning: NVStorageComponent.GetMaxSizeForEntryTag.InvalidTag: 0x1", log);
        Assert.Empty(nv.QueuedTags);
        rig.Tick();
        Assert.Equal(sent, NvCommands(rig).Count);
    }

    /// <summary>WA7 (0x00644572..0x006445A0): a factory tag without +0x15C (0 in every normal run) warns FactoryTagNotAllowed; WA2: a null vector is only an info log and no callback.</summary>
    [Fact]
    public void M3_043_WA2_WA7_WA8_AFactoryTagAndNullDataAreRejected()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        DrainCalibrationRead(rig);
        var nv = rig.Robot.Engine.NvStorage!;
        int called = 0;
        int start = nv.Log.Count;
        Assert.Equal(0, nv.Write(0x80000001, new byte[] { 1, 2, 3, 4 }, _ => called++));
        Assert.Equal(1, called);
        Assert.Contains("warning: NVStorageComponent.Write.FactoryTagNotAllowed: Tag: NVEntry_CameraCalib (0x80000001)", LogSince(nv, start));

        start = nv.Log.Count;
        Assert.Equal(0, nv.Write(0x182000, (byte[]?)null, _ => called++));      // the vector overload: info, no broadcast, no callback
        Assert.Equal(1, called);
        Assert.Equal(new[] { "info: NVStorageComponent.Write.NullData: NVEntry_GameUnlocks" }, LogSince(nv, start));

        start = nv.Log.Count;
        Assert.Equal(0, nv.Write(0x182000, null, 4, _ => called++));             // the pointer overload: a warning, and the failure exit
        Assert.Equal(2, called);
        Assert.Contains("warning: NVStorageComponent.Write.NullData: NVEntry_GameUnlocks", LogSince(nv, start));
    }

    /// <summary>
    /// WA9 (0x00644616..0x006446CA): limit = the table size (0x1000 for 0x182000) - 0x10; the count is padded up to a multiple of 4 and rejected
    /// when count - 1 is not below the limit (unsigned), so 0 and anything over 0xFF0 fail with InvalidSize and the padded count and the limit; 5 bytes queue as 8.
    /// </summary>
    [Theory]
    [InlineData(0u, false, 0u)]
    [InlineData(5u, true, 8u)]
    [InlineData(0xFF0u, true, 0xFF0u)]
    [InlineData(0xFF1u, false, 0xFF4u)]
    public void M3_043_WA9_TheSizeIsPaddedAndLimited(uint numBytes, bool accepted, uint paddedBytes)
    {
        using var rig = new Rig();
        rig.ToSuccess();
        DrainCalibrationRead(rig);
        var nv = rig.Robot.Engine.NvStorage!;
        int start = nv.Log.Count;
        int result = nv.Write(0x182000, new byte[numBytes], numBytes, _ => { });
        var log = LogSince(nv, start);
        if (accepted)
        {
            Assert.Equal(1, result);
            Assert.Contains($"debug: NVStorageComponent.Write.DataQueued: NVEntry_GameUnlocks - numBytes: {paddedBytes}", log);
        }
        else
        {
            Assert.Equal(0, result);
            Assert.Contains($"warning: NVStorageComponent.Write.InvalidSize: Tag: NVEntry_GameUnlocks, {paddedBytes} bytes (limit 4080 bytes)", log);
        }
    }

    /// <summary>
    /// WA12, WA13, WB3..WB7, WB11..WB19: a successful Write queues an ERASE then the WRITE and sends nothing; the ERASE goes out on the next Update
    /// (Length = the tag's maximum size, op 2, the saved Data, which is empty before any write); its ack completes it; the WRITE dispatch sends nothing
    /// again; the next Update sends the one chunk (header + payload, Length = its size), and its ack completes the write.
    /// </summary>
    [Fact]
    public void M3_043_WA12_WB3_WB11_WB18_AWriteIsAnEraseThenADispatchThenAChunk()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        DrainCalibrationRead(rig);
        var nv = rig.Robot.Engine.NvStorage!;
        var data = Pattern(100);
        NvResult? done = null;
        int start = nv.Log.Count;
        int sent = NvCommands(rig).Count;
        Assert.Equal(1, nv.Write(0x182000, data, r => done = r));
        Assert.Equal(sent, NvCommands(rig).Count);                                 // Write sends nothing
        Assert.Equal(new uint[] { 0x182000, 0x182000 }, nv.QueuedTags);
        var queued = LogSince(nv, start);
        Assert.Equal("debug: NVStorageComponent.Write.PrecedingWriteWithErase: Tag: NVEntry_GameUnlocks", queued[0]);
        Assert.Equal("debug: NVStorageComponent.Write.DataQueued: NVEntry_GameUnlocks - numBytes: 100", queued[1]);

        rig.Tick();                                                                // ERASE dispatch
        var erase = Assert.Single(NvCommands(rig).Skip(sent));
        Assert.Equal((0x182000u, 0x1000, NvStorageComponent.OpErase, (byte)0), (erase.Tag, erase.Length, erase.Op, erase.Unknown));
        Assert.Empty(erase.Data);
        Assert.Contains("debug: NVStorageComponent.ProcessRequest.SendingErase: NVEntry_GameUnlocks (Tag: 0x182000) size: 4096", nv.Log);
        Assert.Contains("debug: NVStorageComponent.SetState: PrevState: 0, NewState: 1", nv.Log);
        rig.Data(new NVOpResult { Tag = 0x182000, Op = NvStorageComponent.OpErase, Result = 0 });
        rig.Tick();                                                                // the ack completes the ERASE; the same Update dispatches the WRITE (nothing sent)
        Assert.Contains("info: NVStorageComponent.HandleNVOpResult.WriteSuccess: BaseTag: NVEntry_GameUnlocks, lastTag: 0x182000, op: NVOP_ERASE, result: NV_OKAY", nv.Log);
        Assert.Contains("debug: NVStorageComponent.HandleNVOpResult.Recvd: Tag: 0x182000, Op: NVOP_ERASE, Result: NV_OKAY", nv.Log);
        Assert.Null(done);                                                         // the erase has no callback

        Assert.Single(NvCommands(rig).Skip(sent));
        Assert.Contains(nv.Log, l => l.StartsWith("debug: NVStorageComponent.ProcessRequest.SendingWrite: StartTag: 0x182000 (NVEntry_GameUnlocks), timeoutTime: "));
        rig.Tick();                                                                // the chunk
        var chunk = NvCommands(rig)[^1];
        Assert.Equal(2, NvCommands(rig).Skip(sent).Count());
        Assert.Equal((0x182000u, 116, NvStorageComponent.OpWrite), (chunk.Tag, chunk.Length, chunk.Op));
        Assert.Equal(new byte[] { 0x4F, 0x4D, 0x5A, 0x43, 0, 0, 0, 0, 100, 0, 0, 0, 0, 0 }, chunk.Data[..14]);
        Assert.Equal(data, chunk.Data[16..]);
        Assert.Contains("debug: NVStorageComponent.Update.SendingWriteMsg: BaseTag: NVEntry_GameUnlocks, tag: 0x182000, bytesSent: 100", nv.Log);
        rig.Data(new NVOpResult { Tag = 0x182000, Op = NvStorageComponent.OpWrite, Result = 0 });
        rig.Tick();
        Assert.Equal(0, done!.Value.Result);
        Assert.Contains("debug: NVStorageComponent.HandleNVOpResult.ExecutingWriteCallback: NVEntry_GameUnlocks", nv.Log);
        rig.Tick();
        Assert.True(nv.IsIdle);
    }

    /// <summary>
    /// WB15, WB19, WB20, WD4 (0x00645816..0x0064598A, 0x00643062..0x0064307E): a 2500-byte payload is three chunks, one per Update, tagged T, T + 0x400,
    /// T + 0x800 (non-factory base), of 1008 (after the header), 1024 and 468 bytes. An ack with more chunks left logs and calls nothing; the callback runs
    /// once, at the last ack. A READ, ERASE or WIPEALL after the write carries the last chunk (WB21): the saved Data and Length.
    /// </summary>
    [Fact]
    public void M3_043_WB15_WB20_WB21_AMultiChunkWriteSendsOneChunkPerUpdateAndLeavesTheLastChunkSaved()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        DrainCalibrationRead(rig);
        var nv = rig.Robot.Engine.NvStorage!;
        var data = Pattern(2500);
        int callbacks = 0;
        int sent = NvCommands(rig).Count;
        nv.Write(0x182000, data, _ => callbacks++);
        rig.Tick();                                                                // erase
        rig.Data(new NVOpResult { Tag = 0x182000, Op = NvStorageComponent.OpErase, Result = 0 });
        rig.Tick();                                                                // its ack, then the WRITE dispatch in the same Update
        var expected = new (uint Tag, int Length)[] { (0x182000, 1024), (0x182400, 1024), (0x182800, 468) };
        rig.Tick();                                                                // chunk 0: one chunk per Update call
        for (int i = 0; i < 3; i++)
        {
            var cmd = NvCommands(rig)[^1];
            Assert.Equal((expected[i].Tag, expected[i].Length, NvStorageComponent.OpWrite), (cmd.Tag, cmd.Length, cmd.Op));
            Assert.Equal(expected[i].Length, cmd.Data.Length);
            int count = NvCommands(rig).Count;
            rig.Tick();                                                            // nothing more is sent while the ack is awaited
            Assert.Equal(count, NvCommands(rig).Count);
            int before = nv.Log.Count;
            rig.Data(new NVOpResult { Tag = 0x182000, Op = NvStorageComponent.OpWrite, Result = (sbyte)(i == 0 ? 3 : 0) });   // any result >= 0 advances (WD4)
            rig.Tick();
            if (i < 2)
            {
                Assert.Equal(0, callbacks);
                Assert.Equal(count + 1, NvCommands(rig).Count);                    // the next chunk goes out in the Update after the ack
                Assert.DoesNotContain(nv.Log.Skip(before), l => l.Contains("WriteSuccess") || l.Contains("ExecutingWriteCallback"));
            }
        }
        Assert.Equal(1, callbacks);
        Assert.Equal(data[2032..], NvCommands(rig)[^1].Data);                      // the last chunk, no header
        Assert.Equal(new byte[] { 0x4F, 0x4D, 0x5A, 0x43 }, NvCommands(rig)[^3].Data[..4]);   // the first chunk carried the header

        nv.Read(0x182000, _ => { });
        rig.Tick();
        var read = NvCommands(rig)[^1];
        Assert.Equal((NvStorageComponent.OpRead, 0x400), (read.Op, read.Length));
        Assert.Equal(data[2032..], read.Data);                                     // WB21
        rig.Data(new NVOpResult { Tag = 0x182000, Op = 0, Result = -1 });
        rig.Tick();

        nv.WipeAll(_ => { });
        rig.Tick();
        var wipe = NvCommands(rig)[^1];
        Assert.Contains("debug: NVStoageComponent.ProcessRequest.SendingWipeAll: ", nv.Log);   // the engine's own spelling, empty format
        Assert.Equal((0u, 0x400, NvStorageComponent.OpWipeAll), (wipe.Tag, wipe.Length, wipe.Op));    // WB9: the stale Length (the READ's 0x400) and Data
        Assert.Equal(data[2032..], wipe.Data);
    }

    /// <summary>
    /// WB8, WB10, WD2, WD3 (0x006451CA, 0x0064302C): a WIPEALL ack is normalised to 0x198000 whatever its tag, so it is accepted while the
    /// awaited +0x20 is 0x198000, and the callback is named NVEntry_NEXT_SLOT. Its Length is the stale +0xE0 (WB9); a first-ever WIPEALL would send a Length the engine never wrote (WB10), which is 0 here under policy M3-037 (not reachable after the connection reads).
    /// </summary>
    [Fact]
    public void M3_043_WB8_WB10_WD2_AWipeAllIsAckedByTheSentinelWhateverTheAckTag()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        DrainCalibrationRead(rig);
        var nv = rig.Robot.Engine.NvStorage!;
        int calls = 0;
        nv.WipeAll(_ => calls++);
        Assert.Contains("debug: NVStorageComponent.WipeAll.Queued: ", nv.Log);
        rig.Tick();
        var wipe = NvCommands(rig)[^1];
        Assert.Equal((0u, 0x400, NvStorageComponent.OpWipeAll), (wipe.Tag, wipe.Length, wipe.Op));   // WB9: the Length is the last connection READ's, never rewritten
        Assert.Empty(wipe.Data);
        rig.Data(new NVOpResult { Tag = 0x1234, Op = NvStorageComponent.OpWipeAll, Result = 0 });
        rig.Tick();
        Assert.Equal(1, calls);
        Assert.Contains("debug: NVStorageComponent.HandleNVOpResult.ExecutingWriteCallback: NVEntry_NEXT_SLOT", nv.Log);
        Assert.Contains("info: NVStorageComponent.HandleNVOpResult.WriteSuccess: BaseTag: NVEntry_NEXT_SLOT, lastTag: 0x198000, op: NVOP_WIPEALL, result: NV_OKAY", nv.Log);
    }

    /// <summary>
    /// WD3 (0x00643080..0x006430A0): an ack while none is awaited, or for another tag, warns AckdTagBaseTagWasNeverSent "BaseTag: 0x%x, Tag: 0x%x" and changes nothing.
    /// </summary>
    [Fact]
    public void M3_043_WD3_AnAckNobodyAwaitsIsWarnedAndIgnored()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        DrainCalibrationRead(rig);
        var nv = rig.Robot.Engine.NvStorage!;
        rig.Data(new NVOpResult { Tag = 0x183000, Op = NvStorageComponent.OpWrite, Result = 0 });
        rig.Tick();
        Assert.Contains("warning: NVStorageComponent.HandleNVOpResult.AckdTagBaseTagWasNeverSent: BaseTag: 0x183000, Tag: 0x183000", nv.Log);
        nv.Erase(0x182000, _ => { });
        rig.Tick();
        rig.Data(new NVOpResult { Tag = 0x183000, Op = NvStorageComponent.OpErase, Result = 0 });
        rig.Tick();
        Assert.False(nv.IsIdle);                                                   // still awaiting the 0x182000 ack
        rig.Data(new NVOpResult { Tag = 0x5555, Op = 7, Result = 0 });
        rig.Tick();
        Assert.Contains(nv.Log, l => l.StartsWith("warning: NVStorageComponent.HandleNVOpResult.UnhandledOperation: "));
    }

    /// <summary>
    /// WC1..WC5 (0x006456F4..0x00645758, 0x00642B54): with an ack awaited, the write times out only when the clock is strictly past the deadline
    /// (set at the chunk send, clock + 5000): WriteTimeout with +0x20, the callback with -4, SetState(0); no retry, no ExecutingWriteCallback, no broadcast.
    /// A late ack then finds nothing awaited (WC5).
    /// </summary>
    [Fact]
    public void M3_043_WC1_WC5_AWriteTimesOutOnceTheClockPassesTheDeadline()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        DrainCalibrationRead(rig);
        rig.Data(new SyncTimeAck());
        SetRobotClock(rig, 1000);
        var nv = rig.Robot.Engine.NvStorage!;
        var results = new List<sbyte>();
        var broadcasts = new List<NVStorageOpResult>();
        nv.NVStorageOpResultBroadcast += broadcasts.Add;
        nv.Write(0x182000, Pattern(8), r => results.Add(r.Result), broadcast: true);
        rig.Tick();
        rig.Data(new NVOpResult { Tag = 0x182000, Op = NvStorageComponent.OpErase, Result = 0 });
        rig.Tick();
        rig.Tick();                                                                // the chunk: deadline = 1000 + 5000
        int sent = NvCommands(rig).Count;
        SetRobotClock(rig, 6000);                                                  // not strictly past
        Assert.Empty(results);
        SetRobotClock(rig, 6001);
        Assert.Equal(new sbyte[] { -4 }, results);
        Assert.Contains("warning: NVStorageComponent.Update.WriteTimeout: Tag: 0x182000", nv.Log);
        Assert.Equal(sent, NvCommands(rig).Count);                                 // no retry
        Assert.DoesNotContain(nv.Log, l => l.Contains("ExecutingWriteCallback"));
        Assert.Empty(broadcasts);
        Assert.True(nv.IsIdle);
        rig.Data(new NVOpResult { Tag = 0x182000, Op = NvStorageComponent.OpWrite, Result = 0 });
        rig.Tick();
        Assert.Equal(new sbyte[] { -4 }, results);
        Assert.Contains("warning: NVStorageComponent.HandleNVOpResult.AckdTagBaseTagWasNeverSent: BaseTag: 0x182000, Tag: 0x182000", nv.Log);
    }

    /// <summary>
    /// WD7..WD9 (0x00643194..0x006431E4, 0x0064328E..0x006432BA, 0x00645C54..0x00645D9A): a retryable negative ack resends the SAME chunk (saved tag, Length, Data),
    /// logs Retry and ResentFailedWrite, and does not re-arm the deadline; the eighth failure logs NumRetriesExceeded (error, _errG), WriteOpFailed and
    /// WriteFailed, completes with the result and drops the rest.
    /// </summary>
    [Fact]
    public void M3_043_WD7_WD9_ARetryableAckResendsTheSameChunkThenFails()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        DrainCalibrationRead(rig);
        var nv = rig.Robot.Engine.NvStorage!;
        NvResult? done = null;
        nv.Write(0x182000, Pattern(2500), r => done = r);
        rig.Tick();
        rig.Data(new NVOpResult { Tag = 0x182000, Op = NvStorageComponent.OpErase, Result = 0 });
        rig.Tick();
        rig.Tick();
        var first = NvCommands(rig)[^1];
        int count = NvCommands(rig).Count;
        Cozmo.Transport.EngineErrorState.ErrorFlagSet = false;
        for (int attempt = 1; attempt <= 7; attempt++)
        {
            rig.Data(new NVOpResult { Tag = 0x182000, Op = NvStorageComponent.OpWrite, Result = -5 });
            rig.Tick();
            Assert.Null(done);
            var resent = NvCommands(rig)[^1];
            Assert.Equal(count + attempt, NvCommands(rig).Count);
            Assert.Equal((first.Tag, first.Length, first.Op), (resent.Tag, resent.Length, resent.Op));
            Assert.Equal(first.Data, resent.Data);
            Assert.Contains($"info: NVStorageComponent.ResendLastCommand.Retry: Tag: 0x182000, Op: NVOP_WRITE, Attempt: {attempt}", nv.Log);
            Assert.Contains("info: NVStorageComponent.HandleNVOpResult.ResentFailedWrite: Tag 0x182000 resent due to NV_BUSY, op: NVOP_WRITE", nv.Log);
        }
        Assert.False(Cozmo.Transport.EngineErrorState.ErrorFlagSet);
        rig.Data(new NVOpResult { Tag = 0x182000, Op = NvStorageComponent.OpWrite, Result = -5 });
        rig.Tick();
        Assert.Equal(-5, done!.Value.Result);
        Assert.Contains("error: NVStorageComponent.ResendLastCommand.NumRetriesExceeded: Tag: 0x182000, Op: NVOP_WRITE, Attempts: 8", nv.Log);
        Assert.Contains("warning: NVStorageComponent.HandleNVOpResult.WriteOpFailed: Tag: 0x182000, op: NVOP_WRITE, result: NV_BUSY", nv.Log);
        Assert.Contains("warning: NVStorageComponent.HandleNVOpResult.WriteFailed: BaseTag: NVEntry_GameUnlocks, lastTag: 0x182000, op: NVOP_WRITE, result: NV_BUSY", nv.Log);
        Assert.True(Cozmo.Transport.EngineErrorState.ErrorFlagSet);
        Cozmo.Transport.EngineErrorState.ErrorFlagSet = false;
        int after = NvCommands(rig).Count;
        rig.Tick();
        rig.Tick();
        Assert.Equal(after, NvCommands(rig).Count);                                // the remaining chunks were dropped
    }

    /// <summary>
    /// WB10 (0x00642848..0x006428E8): the constructor ends with SetState(0), which logs "PrevState: %d, NewState: 0"; +8 is never written, so PrevState is 0
    /// (policy M3-037). The engine builds a component per Robot, so the line recurs when a removed robot's component is replaced, and the saved command is back to
    /// the constructed state (byte +0xE5 and Data; Tag, Length and op never written: 0).
    /// </summary>
    [Fact]
    public void M3_043_WB10_TheConstructorLogsSetStateZeroAndADisconnectRebuildsTheSavedCommand()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        var nv = rig.Robot.Engine.NvStorage!;
        const string line = "debug: NVStorageComponent.SetState: PrevState: 0, NewState: 0";
        Assert.Equal(line, nv.Log[0]);
        DrainCalibrationRead(rig);
        nv.Write(0x182000, Pattern(8), _ => { });
        rig.Tick();
        rig.Data(new NVOpResult { Tag = 0x182000, Op = NvStorageComponent.OpErase, Result = 0 });
        rig.Tick();
        rig.Tick();
        int before = nv.Log.Count(l => l == line);
        nv.OnDisconnected();
        Assert.Equal(before + 1, nv.Log.Count(l => l == line));
        nv.WipeAll(_ => { });
        rig.Tick();
        var wipe = NvCommands(rig)[^1];
        Assert.Equal((0u, 0, NvStorageComponent.OpWipeAll, (byte)0), (wipe.Tag, wipe.Length, wipe.Op, wipe.Unknown));
        Assert.Empty(wipe.Data);
    }

    // ================================================================== display: M3-006, M3-007, M3-009

    private const int Rows = 64, Cols = 128;

    private static byte[] Canvas(params (int Row, int Col)[] lit)
    {
        var c = new byte[Rows * Cols];
        foreach (var (r, col) in lit) c[r * Cols + col] = 1;
        return c;
    }

    /// <summary>B13 (from B8): a blank canvas is two skip opcodes, {0x3F, 0x3F}.</summary>
    [Fact]
    public void M3_009_B13_ABlankCanvasEncodesAs3F3F()
    {
        Assert.Equal(new byte[] { 0x3F, 0x3F }, FaceBitmapCodec.EncodeCanvas(new byte[Rows * Cols], Rows, Cols));
        Assert.Equal(new byte[] { 0x3F, 0x3F }, FaceBitmapCodec.Encode(new FaceBitmap()));
    }

    /// <summary>
    /// B10: column 0 with only row 0 lit is pair 0 = 1 (bit0 = row 0), one run: 0x80 | (0 &lt;&lt; 2) | 1 = 0x81; its
    /// trailing pair-0 run is dropped because column 1 is empty (B11). B8: the 127 empty columns that follow are a
    /// skip of 1 + 63 (0x3F, capped at n &lt;= 63) and then 1 + 62 (0x3E, capped at c + n &lt;= 127).
    /// </summary>
    [Fact]
    public void M3_007_B8_B10_B11_OneLitPixelThenSkips()
        => Assert.Equal(new byte[] { 0x81, 0x3F, 0x3E }, FaceBitmapCodec.EncodeCanvas(Canvas((0, 0)), Rows, Cols));

    /// <summary>
    /// B9: every column equal to the first: 0x81 for column 0 (its trailing pair-0 run dropped, the next column being
    /// equal, B11), then repeat 0x40 | 63 for columns 1..64 and 0x40 | 62 for 65..127.
    /// </summary>
    [Fact]
    public void M3_007_B9_EqualColumnsAreRepeats()
    {
        var lit = Enumerable.Range(0, Cols).Select(c => (0, c)).ToArray();
        Assert.Equal(new byte[] { 0x81, 0x7F, 0x7E }, FaceBitmapCodec.EncodeCanvas(Canvas(lit), Rows, Cols));
    }

    /// <summary>
    /// B11: at c == 127 the trailing run is always emitted. Columns 0..126 are empty (skip 0x3F for 0..63, skip 0x3E
    /// for 64..126), column 127 has row 0 lit: 0x81, then its pair-0 run of 31 pairs, 0x80 | (30 &lt;&lt; 2) = 0xF8.
    /// </summary>
    [Fact]
    public void M3_007_B11_TheTrailingRunIsAlwaysEmittedAtColumn127()
        => Assert.Equal(new byte[] { 0x3F, 0x3E, 0x81, 0xF8 }, FaceBitmapCodec.EncodeCanvas(Canvas((0, 127)), Rows, Cols));

    /// <summary>
    /// B11: a trailing pair-0 run is kept when the next column is non-empty and different. Column 0: row 0 (0x81, then
    /// the 31-pair zero run 0xF8, kept). Column 1: row 2 is pair 1 bit 0, so runs pair 0 = 0 (0x80), pair 1 = 1 (0x81),
    /// then a 30-pair zero run dropped because column 2 is empty. Then skip 1 + 63 (0x3F) and 1 + 61 (0x3D).
    /// </summary>
    [Fact]
    public void M3_007_B11_ATrailingZeroRunIsKeptWhenTheNextColumnDiffers()
        => Assert.Equal(new byte[] { 0x81, 0xF8, 0x80, 0x81, 0x3F, 0x3D },
                        FaceBitmapCodec.EncodeCanvas(Canvas((0, 0), (2, 1)), Rows, Cols));

    /// <summary>
    /// B10: pair p is row 2p in bit 0 and row 2p + 1 in bit 1. Column 0 with rows 0 and 1 (pair 0 = 3), row 2 (pair 1 =
    /// 1) and row 5 (pair 2 = bit 1 = 2), in the last column so the trailing zero run is emitted (B11): 0x83, 0x81,
    /// 0x82, then 29 zero pairs 0x80 | (28 &lt;&lt; 2) = 0xF0.
    /// </summary>
    [Fact]
    public void M3_007_B10_PairBitsAreRow2kAndRow2kPlus1()
        => Assert.Equal(new byte[] { 0x3F, 0x3E, 0x83, 0x81, 0x82, 0xF0 },
                        FaceBitmapCodec.EncodeCanvas(Canvas((0, 127), (1, 127), (2, 127), (5, 127)), Rows, Cols));

    /// <summary>
    /// A column of 2k runs, every column different from the one before (pairs alternate v, 0, v, ... with v = 1 on even
    /// columns and 2 on odd ones), so each column's trailing zero run is emitted (B11) and each column costs 2k bytes.
    /// </summary>
    private static byte[] RunColumns(Func<int, int[]> pairsOf)
    {
        var canvas = new byte[Rows * Cols];
        for (int c = 0; c < Cols; c++)
        {
            var pairs = pairsOf(c);
            for (int p = 0; p < 32; p++)
            {
                if ((pairs[p] & 1) != 0) canvas[(2 * p) * Cols + c] = 1;
                if ((pairs[p] & 2) != 0) canvas[(2 * p + 1) * Cols + c] = 1;
            }
        }
        return canvas;
    }

    private static int[] EightRuns(int c)
    {
        int v = c % 2 == 0 ? 1 : 2;
        var p = new int[32];
        p[0] = v; p[2] = v; p[4] = v; p[6] = v;       // v,0,v,0,v,0,v, then 25 zero pairs: 8 runs
        return p;
    }

    private static int[] SevenRunsEndingLit(int c)
    {
        int v = c % 2 == 0 ? 1 : 2;
        var p = new int[32];
        p[0] = v; p[2] = v; p[4] = v; p[31] = v;      // v,0,v,0,v,0(26),v: 7 runs, the last one lit
        return p;
    }

    /// <summary>
    /// B12: the raw fallback applies when the RLE is at least 1024 bytes. 128 columns of 8 bytes is exactly 1024, so
    /// the payload is the 128 little-endian u64 column masks (bit r = row r); with one column of 7 bytes the RLE is
    /// 1023 bytes and is sent as it is.
    /// </summary>
    [Fact]
    public void M3_007_B12_TheRawFallbackStartsAtExactly1024Bytes()
    {
        var at1024 = RunColumns(EightRuns);
        var raw = FaceBitmapCodec.EncodeCanvas(at1024, Rows, Cols)!;
        Assert.Equal(1024, raw.Length);
        for (int c = 0; c < Cols; c++)
        {
            ulong mask = 0;
            for (int r = 0; r < Rows; r++) if (at1024[r * Cols + c] != 0) mask |= 1UL << r;
            Assert.Equal(mask, BitConverter.ToUInt64(raw, c * 8));
        }
        Assert.Equal(at1024, FaceBitmapCodec.DecodeCanvas(raw));        // B14: size 1024 is raw

        var at1023 = RunColumns(c => c == 5 ? SevenRunsEndingLit(c) : EightRuns(c));
        var rle = FaceBitmapCodec.EncodeCanvas(at1023, Rows, Cols)!;
        Assert.Equal(1023, rle.Length);
        Assert.All(rle, b => Assert.True((b & 0x80) != 0, "every byte of this image is a run"));
        Assert.Equal(at1023, FaceBitmapCodec.DecodeCanvas(rle));        // B14 reference decoder
    }

    /// <summary>B6: CompressRLE needs a 64 x 128 image; anything else gives no face at all.</summary>
    [Fact]
    public void M3_007_B6_OnlyA64By128CanvasIsEncoded()
    {
        Assert.Null(FaceBitmapCodec.EncodeCanvas(new byte[64 * 127], 64, 127));
        Assert.Null(FaceBitmapCodec.EncodeCanvas(new byte[32 * 128], 32, 128));
    }

    /// <summary>B7: bit r of a column mask is set when pixel (r, c) is non-zero; any non-zero value counts.</summary>
    [Fact]
    public void M3_007_B7_AnyNonZeroPixelIsLit()
    {
        var c = new byte[Rows * Cols];
        c[0] = 0x80;                                              // row 0, column 0
        Assert.Equal(new byte[] { 0x81, 0x3F, 0x3E }, FaceBitmapCodec.EncodeCanvas(c, Rows, Cols));
    }

    /// <summary>
    /// B1, B15: the canvas is 64 rows x 128 columns and the wire image 128 columns of 64 rows as 32 two-row pairs. A
    /// pixel on the last canvas row (63) is pair 31, bit 1. This stack's 128 x 32 bitmap puts its row y on canvas row
    /// 2y (the class summary), so its row 31 is pair 31, bit 0.
    /// </summary>
    [Fact]
    public void M3_006_B1_B15_TheWireImageIs128ColumnsOf64RowsIn32Pairs()
    {
        var last = FaceBitmapCodec.EncodeCanvas(Canvas((63, 127)), Rows, Cols)!;
        Assert.Equal(new byte[] { 0x3F, 0x3E, 0x80 | (30 << 2) | 0, 0x80 | (0 << 2) | 2 }, last);

        var bmp = new FaceBitmap();
        bmp[127, 31] = 1;
        Assert.Equal(new byte[] { 0x3F, 0x3E, 0x80 | (30 << 2) | 0, 0x80 | (0 << 2) | 1 }, FaceBitmapCodec.Encode(bmp));
        Assert.Equal(1, FaceBitmapCodec.DecodeCanvas(last)[63 * Cols + 127]);
    }

    // ================================================================== audio encoding: M3-010, M3-011

/// <summary>
    /// C6 (0x00597AD8..0x00597B8E): the byte is <c>sign 0x80 | exp &lt;&lt; 4 | mant</c> with the exponent from
    /// the segment table 0x00C5C3F0 and the literal 0x46FFFE00 (32767.0f). Every expected byte below is
    /// hand-derived from that algorithm and table, not read from the encoder: 32767 -&gt; mag 32767, hi 127,
    /// exp 7, mant 15 = 0x7F; 16383 -&gt; hi 63, exp 6, mant 15 = 0x6F; -16383 -&gt; mag 16382 = 0xEF; -32767
    /// -&gt; mag 32766 = 0xFF; NaN -&gt; 0. The float path clamps to 1 / -1, truncating toward zero.
    /// </summary>
    [Fact]
    public void M3_010_C6_TheFloatIsClampedScaledAndTruncated()
    {
        Assert.Equal(0x00, AnkiMuLaw.Encode(float.NaN));
        Assert.Equal(0x7F, AnkiMuLaw.Encode(1f));
        Assert.Equal(0x7F, AnkiMuLaw.Encode(1.5f));            // clamped to 1
        Assert.Equal(0x7F, AnkiMuLaw.Encode((short)32767));
        Assert.Equal(0xFF, AnkiMuLaw.Encode(-1f));
        Assert.Equal(0xFF, AnkiMuLaw.Encode(-5f));             // clamped to -32767
        Assert.Equal(0xFF, AnkiMuLaw.Encode((short)-32767));
        Assert.Equal(0x6F, AnkiMuLaw.Encode(0.5f));            // trunc(16383.5)
        Assert.Equal(0x6F, AnkiMuLaw.Encode((short)16383));
        Assert.Equal(0xEF, AnkiMuLaw.Encode(-0.5f));           // trunc toward zero
        Assert.Equal(0xEF, AnkiMuLaw.Encode((short)-16383));
    }

    /// <summary>
    /// C6: mag = s ^ (s &gt;&gt; 15); byte = (s &lt; 0 ? 0x80 : 0) | exp &lt;&lt; 4 | mant, with mant = mag &gt;&gt; 4 when
    /// mag &gt;&gt; 8 is 0. So s = -1 has mag 0 and is s = 0 with the sign bit; s = 240 (mag &gt;&gt; 8 = 0) is s = 0 with
    /// mantissa 15; s = -241 has mag 240.
    /// </summary>
    [Fact]
    public void M3_010_C6_SignMagnitudeAndTheLowSegment()
    {
        byte zero = AnkiMuLaw.Encode((short)0);
        Assert.Equal(zero | 0x80, AnkiMuLaw.Encode((short)-1));
        Assert.Equal(zero | 0x0F, AnkiMuLaw.Encode((short)240));
        Assert.Equal(zero | 0x0F | 0x80, AnkiMuLaw.Encode((short)-241));
        Assert.Equal(zero | 0x01, AnkiMuLaw.Encode((short)16));
    }

    /// <summary>C5: a frame shorter than 744 samples is zero-padded, 0x00 being silence in this codec.</summary>
    [Fact]
    public void M3_010_C5_AShortFrameIsPaddedWith00()
    {
        var frames = CozmoAudio.ToFrames(Enumerable.Repeat((short)8000, 10).ToArray());
        var f = Assert.Single(frames);
        Assert.Equal(744, f.Length);
        Assert.All(f.Skip(10), b => Assert.Equal(0x00, b));
    }

    /// <summary>
    /// M3-010 row 1i: the segment table at 0x00C5C3F0 is exactly 128 bytes, [0]=0, [1]=1, [2..3]=2, [4..7]=3,
    /// [8..15]=4, [16..31]=5, [32..63]=6, [64..127]=7. The exponent is the byte's high nibble; each sample
    /// below has mag = hi &lt;&lt; 8, so hi is the table index. Expected exponents are read from the binary.
    /// </summary>
    [Fact]
    public void M3_010_1i_TheSegmentTableIsTheEngines128Values()
    {
        var expected = new (int Hi, int Exp)[]
        {
            (0, 0), (1, 1), (2, 2), (3, 2), (4, 3), (7, 3), (8, 4), (15, 4),
            (16, 5), (31, 5), (32, 6), (63, 6), (64, 7), (127, 7),
        };
        foreach (var (hi, exp) in expected)
        {
            short sample = (short)(hi << 8);
            int actual = AnkiMuLaw.Encode(sample) >> 4;
            Assert.True(actual == exp, $"mag>>8 = {hi}: exponent {actual}, the table says {exp}");
        }
        // The table has no entry past index 127: mag <= 32767, so mag>>8 <= 127.
        Assert.Equal(7, AnkiMuLaw.Encode(short.MaxValue) >> 4);
    }

    /// <summary>
    /// M3-010 rows 1b/1d/1c: NaN returns 0 and logs the engine's sWarningF; the scaling literal is 0x46FFFE00
    /// (32767.0f) and the product is taken in single precision.
    /// </summary>
    [Fact]
    public void M3_010_1b_1d_TheNaNPathLogsAndTheLiteralIs32767_0f()
    {
        var log = new List<string>();
        Assert.Equal(0, AnkiMuLaw.Encode(float.NaN, log.Add));
        Assert.Contains("warning: RobotAudioAnimationOnRobot.encodeMuLaw.sampleNaN: Audio sample from current stream is NaN", log);
        Assert.Equal(0x46FFFE00, BitConverter.SingleToInt32Bits(AnkiMuLaw.FullScale));
        Assert.Equal(32767f, AnkiMuLaw.FullScale);
    }

    /// <summary>A source that returns exactly the given PCM.</summary>
    private sealed class FixedPcmSource : IAnimationAudioSource
    {
        private readonly short[] _pcm;
        public FixedPcmSource(short[] pcm) => _pcm = pcm;
        public short[]? GetPcm(long eventId, float volume) => _pcm;
        public string? NameOf(long eventId) => "fixed";
    }

    /// <summary>
    /// C5/C6 through the live entry: the scheduler's <c>PopFrame</c> (AnimationScheduler.cs:1603-1614) encodes
    /// the source's 16-bit PCM and zero-pads the frame to 744 bytes. The expected byte is hand-derived from the
    /// segment table: 8000 -&gt; mag 8000, hi 31, exp 5, mant 15 = 0x5F. The engine's NaN warning belongs to
    /// <c>encodeMuLaw(float)</c>; this short PCM seam never produces NaN, so the log stays empty.
    /// </summary>
    [Fact]
    public void M3_010_C5_C6_PopFrameZeroPadsAndEncodesThroughTheScheduler()
    {
        var sink = new RobotSink();
        var log = new List<string>();
        var s = new AnimationScheduler(sink, new Random(1))
        {
            AudioSource = new FixedPcmSource(Enumerable.Repeat((short)8000, 10).ToArray()),
            Log = log.Add,
        };
        var clip = new AnimationClip
        {
            Name = "tone",
            Keyframes = new List<Keyframe> { new AudioKeyframe(0, new long[] { 1 }, 1.0f, new[] { 1.0f }, false) },
            Tracks = AnimationTrack.Audio,
            DurationMs = 1000,
        };
        s.Play(clip, 0);
        s.Advance(0);
        var frame = sink.AudioFrames.First();
        Assert.Equal(744, frame.Length);
        Assert.Equal(0x5F, frame[0]);
        Assert.All(frame.Skip(10), b => Assert.Equal(0x00, b));
        Assert.DoesNotContain(log, l => l.Contains("NaN"));
    }

    /// <summary>C3: 22320 Hz and 744 samples per frame; 30 Hz is 22320 / 744.</summary>
    [Fact]
    public void M3_011_C3_22320HzAnd744Samples()
    {
        Assert.Equal(22320, CozmoAudio.SampleRate);
        Assert.Equal(744, CozmoAudio.SamplesPerFrame);
        Assert.Equal(30, CozmoAudio.SampleRate / CozmoAudio.SamplesPerFrame);
    }

    // ================================================================== budgets and drain: M3-012..M3-015

    /// <summary>C9: bytes = min(8192 - (streamed - played), 30000), a negative value warning and becoming 0.</summary>
    [Fact]
    public void M3_012_C9_TheByteBudget()
    {
        var s = new StreamSendBuffer();
        var log = new List<string>();
        s.Log = log.Add;
        s.UpdateAmountToSend(0, 0);
        Assert.Equal(8192, s.NumBytesToSend);

        s.SendDirect(1000, false, () => true);
        s.UpdateAmountToSend(400, 0);
        Assert.Equal(8192 - (1000 - 400), s.NumBytesToSend);

        s.UpdateAmountToSend(1000 + 40000, 0);                 // the robot reports more played than streamed
        Assert.Equal(30000, s.NumBytesToSend);

        s.SendDirect(9000, false, () => true);                 // 10000 streamed, none played: 8192 - 10000 < 0
        s.UpdateAmountToSend(0, 0);
        Assert.Equal(0, s.NumBytesToSend);
        Assert.Single(log);
    }

    /// <summary>C9: audio = max(14 - (framesStreamed - framesPlayed), 0).</summary>
    [Fact]
    public void M3_012_C9_TheAudioBudget()
    {
        var s = new StreamSendBuffer();
        for (int i = 0; i < 10; i++) s.SendDirect(new AudioSilence(), _ => true);
        s.UpdateAmountToSend(0, 3);
        Assert.Equal(14 - (10 - 3), s.NumAudioFramesToSend);
        for (int i = 0; i < 10; i++) s.SendDirect(new AudioSilence(), _ => true);
        s.UpdateAmountToSend(0, 0);
        Assert.Equal(0, s.NumAudioFramesToSend);
    }

    /// <summary>
    /// C11, C12: every send adds EngineToRobot::Size() (1 for the tag plus the member) to bytes streamed; AudioSample
    /// (0x8E, 744 bytes, C4), AudioSilence (0x8F) and EndOfAnimation add one frame each; other messages none.
    /// </summary>
    [Fact]
    public void M3_012_C11_C12_WhatEachSendCounts()
    {
        var s = new StreamSendBuffer();
        s.SendDirect(new AudioSample(), _ => true);
        Assert.Equal((1 + 744, 1), (s.BytesStreamed, s.FramesStreamed));
        s.SendDirect(new AudioSilence(), _ => true);
        Assert.Equal((1 + 744 + 1, 2), (s.BytesStreamed, s.FramesStreamed));
        s.SendDirect(new EndOfAnimation(), _ => true);
        Assert.Equal((1 + 744 + 1 + 1, 3), (s.BytesStreamed, s.FramesStreamed));
        s.SendDirect(new FaceMsg { Image = new byte[] { 0x3F, 0x3F } }, _ => true);
        Assert.Equal(3, s.FramesStreamed);                      // a face is not an audio frame
        Assert.True(StreamSendBuffer.IsAudioMessage(RobotMessageId.AnimAudioSample));
        Assert.True(StreamSendBuffer.IsAudioMessage(RobotMessageId.AnimAudioSilence));
        Assert.False(StreamSendBuffer.IsAudioMessage(RobotMessageId.AnimFaceImage));
    }

    /// <summary>C14: FIFO, and the drain stops at the first message over the byte budget; nothing behind it goes.</summary>
    [Fact]
    public void M3_013_C14_TheDrainStopsAtTheFirstMessageOverTheByteBudget()
    {
        var s = new StreamSendBuffer();
        s.SendDirect(4192, false, () => true);                 // 4192 unplayed: the budget is 8192 - 4192 = 4000
        s.UpdateAmountToSend(0, 0);
        var sent = new List<int>();
        s.Buffer(null, 100, false, () => { sent.Add(100); return true; });
        s.Buffer(null, 5000, false, () => { sent.Add(5000); return true; });
        s.Buffer(null, 10, false, () => { sent.Add(10); return true; });
        Assert.False(s.SendBufferedMessages());
        Assert.Equal(new[] { 100 }, sent);
        Assert.Equal(2, s.Count);
        Assert.Equal(3900, s.NumBytesToSend);
    }

    /// <summary>C14: an audio message stops the drain when the audio budget is 0; the messages before it go.</summary>
    [Fact]
    public void M3_013_C14_AnAudioMessageStopsTheDrainWhenTheAudioBudgetIs0()
    {
        var s = new StreamSendBuffer();
        for (int i = 0; i < 14; i++) s.SendDirect(new AudioSilence(), _ => true);
        s.UpdateAmountToSend(0, 0);
        Assert.Equal(0, s.NumAudioFramesToSend);
        var sent = new List<string>();
        s.Buffer(null, 3, false, () => { sent.Add("face"); return true; });
        s.Buffer(null, 1, true, () => { sent.Add("audio"); return true; });
        s.Buffer(null, 3, false, () => { sent.Add("after"); return true; });
        Assert.False(s.SendBufferedMessages());
        Assert.Equal(new[] { "face" }, sent);
    }

    /// <summary>C12: EndOfAnimation is sent directly and is not budget-gated, even with no budget left.</summary>
    [Fact]
    public void M3_014_C12_EndOfAnimationIsNotBudgetGated()
    {
        var s = new StreamSendBuffer();
        s.UpdateAmountToSend(0, 0);
        for (int i = 0; i < 14; i++) s.SendDirect(new AudioSilence(), _ => true);
        s.UpdateAmountToSend(0, 0);
        bool sent = false;
        Assert.True(s.SendDirect(new EndOfAnimation(), _ => sent = true));
        Assert.True(sent);
        Assert.Equal(15, s.FramesStreamed);
    }

    /// <summary>A sink that stands for a robot: it reports played counters, and logs what the stream sends.</summary>
    private sealed class RobotSink : IAnimationSink
    {
        public int FramesPlayed, BytesPlayed;
        public readonly List<string> Log = new();
        /// <summary>Every non-null mu-law frame the scheduler handed out, in order.</summary>
        public readonly List<byte[]> AudioFrames = new();
        public int? AudioFramesPlayed => FramesPlayed;
        public int? AnimBytesPlayed => BytesPlayed;
        public void Face(FaceBitmap bitmap) => Log.Add("face");
        public void Audio(byte[]? mulawFrame)
        {
            if (mulawFrame is not null) AudioFrames.Add(mulawFrame);
            Log.Add(mulawFrame is null ? "silence" : "sample");
        }
        public void Head(sbyte angleDeg, uint durationMs) => Log.Add("head");
        public void Lift(byte heightMm, uint durationMs) => Log.Add("lift");
        public void AnimationStarted(byte tag) => Log.Add($"start:{tag}");
        public void AnimationEnded() => Log.Add("end");
        public void Body(BodyKeyframe keyframe) => Log.Add("body");
        public void BodyStop() => Log.Add("bodystop");
        public void Lights(LightsKeyframe keyframe) => Log.Add("lights");
        public void BackpackLights(ushort[] leds) => Log.Add("lights");
        public void Event(string eventId) => Log.Add($"event:{eventId}");
        public void Finished(string clipName, bool completed) => Log.Add("finished");
        public int Count(string what) => Log.Count(e => e == what);
    }

    private static AnimationClip SilentClip(uint durationMs, params Keyframe[] frames) => new()
    {
        Name = "silent",
        Keyframes = frames.ToList(),
        Tracks = AnimationTrack.Event,
        DurationMs = durationMs,
    };

    /// <summary>
    /// C15, A18: one engine Update refreshes the budgets and builds frames one at a time while the buffer is empty. With
    /// nothing played, one Update sends exactly the audio budget, 14 audio frames (C9); the 15th frame is built, its
    /// audio message stops the drain, and it waits in the buffer. The stream time steps 33 ms after every built frame
    /// whose drain returned without a send error, the budget-stopped one included (0x0057CA88..0x0057CA9C): 15 x 33.
    /// When the robot reports 14 played, the next Update flushes the leftover (1 audio frame) and builds 14 more, of
    /// which 13 go and the last waits: 28 audio frames, and the stream time 29 x 33.
    /// </summary>
    [Fact]
    public void M3_013_C15_OneUpdateStreamsFramesUntilTheBudgetStopsTheDrain()
    {
        var sink = new RobotSink();
        var s = new AnimationScheduler(sink, new Random(1));
        s.Play(SilentClip(10_000, new EventKeyframe(9_000, "late")), 0);
        s.Advance(0);
        Assert.Equal(14, sink.Count("silence"));
        Assert.True(s.Stream.Count > 0, "the 15th frame waits in the buffer");
        Assert.Equal(15 * 33, s.StreamTimeMs);

        s.Advance(1);                                              // nothing played: nothing more goes
        Assert.Equal(14, sink.Count("silence"));
        Assert.Equal(15 * 33, s.StreamTimeMs);                     // and no frame is built while the leftover waits

        sink.FramesPlayed = 14;
        s.Advance(2);
        Assert.Equal(28, sink.Count("silence"));
        Assert.Equal(29 * 33, s.StreamTimeMs);
    }

    /// <summary>A sound source with non-silent samples.</summary>
    private sealed class ToneSource : IAnimationAudioSource
    {
        public short[]? GetPcm(long eventId, float volume) => Enumerable.Repeat((short)8000, 744 * 40).ToArray();
        public string? NameOf(long eventId) => "tone";
    }

    /// <summary>
    /// C9 with C11: an AudioSample costs 745 bytes. With nothing played the byte budget is 8192, so one Update sends 10
    /// sample frames (7450 bytes) and the 11th (8195 &gt; 8192) waits: the byte budget binds before the audio budget.
    /// </summary>
    [Fact]
    public void M3_012_C9_C11_TheByteBudgetBindsSampleFramesAt10()
    {
        var sink = new RobotSink();
        var s = new AnimationScheduler(sink, new Random(1)) { AudioSource = new ToneSource() };
        var clip = new AnimationClip
        {
            Name = "tone",
            Keyframes = new List<Keyframe> { new AudioKeyframe(0, new long[] { 1 }, 1.0f, new[] { 1.0f }, false) },
            Tracks = AnimationTrack.Audio,
            DurationMs = 5_000,
        };
        s.Play(clip, 0);
        s.Advance(0);
        // M5 A15: the audio animation's Update (which starts the sound) runs before frame 0 is built, so frame 0 already
        // carries samples: 745 + StartOfAnimation 2, then 745 per frame
        Assert.Equal(0, sink.Count("silence"));
        int samples = sink.Count("sample");
        Assert.Equal(1 + (8192 - 745 - 2) / 745, samples);
        Assert.True(s.Stream.BytesStreamed <= 8192);

        sink.BytesPlayed = s.Stream.BytesStreamed;               // the robot plays what it has
        sink.FramesPlayed = s.Stream.FramesStreamed;
        s.Advance(1);
        // the leftover frame (745 B) and then 9 more: 8192 - 745 = 7447 leaves room for 9 of 745, not 10
        Assert.Equal(samples + 10, sink.Count("sample"));
    }

    /// <summary>
    /// C4, A16, A20 (M3-015): exactly one audio message per frame, first; StartOfAnimation once, after the first
    /// frame's audio; the keyframes after it. With no frames left and the buffer empty, EndOfAnimation goes directly
    /// and nothing follows it (0x0057CB6E..0x0057CBAE: no AudioSilence after the end).
    /// </summary>
    [Fact]
    public void M3_015_C4_A20_OneAudioMessagePerFrameFirstAndNothingAfterTheEnd()
    {
        var sink = new RobotSink { FramesPlayed = 0 };
        var s = new AnimationScheduler(sink, new Random(1));
        s.Play(SilentClip(66, new EventKeyframe(0, "a"), new EventKeyframe(33, "b"), new EventKeyframe(66, "c")), 0);
        s.Advance(0);
        Assert.Equal(new[] { "silence", "start:1", "event:a", "silence", "event:b", "silence", "event:c", "end" },
                     sink.Log);
        s.Advance(1);
        Assert.Equal("finished", sink.Log[^1]);                    // the next Update completes it (M5 A13), sending nothing
        s.Advance(2);
        Assert.Equal(9, sink.Log.Count);                           // still nothing after the end
    }

    /// <summary>
    /// A16 (0x0057C94E..0x0057CA7A): within a frame, audio, StartOfAnimation, head, lift, event, the face, backpack
    /// lights, body, whatever order the clip lists its keyframes in.
    /// </summary>
    [Fact]
    public void M3_015_A16_TheFrameIsBuiltInTheEnginesTrackOrder()
    {
        var sink = new RobotSink { FramesPlayed = 0 };
        var s = new AnimationScheduler(sink, new Random(1));
        var clip = new AnimationClip
        {
            Name = "order",
            Keyframes = new List<Keyframe>
            {
                new BodyKeyframe(0, 0, "STRAIGHT", 0),
                new LightsKeyframe(0, 0, new float[4], new float[4], new float[4], new float[4], new float[4]),
                new FaceKeyframe(0, ProceduralFacePose.ShippedNeutral()),
                new EventKeyframe(0, "e"),
                new LiftKeyframe(0, 100, 50, 0),
                new HeadKeyframe(0, 100, 5, 0),
            },
            Tracks = AnimationTrack.All,
            DurationMs = 1_000,
        };
        s.Play(clip, 0);
        s.Advance(0);
        Assert.Equal(new[] { "silence", "start:1", "head", "lift", "event:e", "face", "lights", "body" }, sink.Log.Take(8));
    }

    /// <summary>
    /// A16 (7), A17: the procedural face goes only when no face-animation frame was buffered and the face-animation track
    /// is at its end; a faceAnimations keyframe not yet due already holds it back. Here the face pose is at 0 and a
    /// two-frame face animation at 66: frames 0 and 33 carry no face (the pose, the only procedural keyframe, is consumed
    /// on frame 0 while the sprite is pending, M5 C7), 66 and 99 the animation's frames; then the clip has no frames left
    /// and ends (M5 A20), so no procedural face ever goes.
    /// </summary>
    [Fact]
    public void M3_015_A17_APendingFaceAnimationHoldsTheProceduralFaceBack()
    {
        var sink = new RobotSink { FramesPlayed = 0 };
        var s = new AnimationScheduler(sink, new Random(1))
        {
            FaceAnimations = _ => new[] { new FaceBitmap(), new FaceBitmap() },
        };
        s.Play(new AnimationClip
        {
            Name = "faces",
            Keyframes = new List<Keyframe>
            {
                new FaceKeyframe(0, ProceduralFacePose.ShippedNeutral()),
                new FaceAnimationKeyframe(66, "anim"),
            },
            Tracks = AnimationTrack.Face,
            DurationMs = 200,
        }, 0);
        s.Advance(0);
        var frames = new List<List<string>>();
        foreach (var e in sink.Log)
        {
            if (e is "silence" or "sample") frames.Add(new List<string>());
            frames[^1].Add(e);
        }
        Assert.DoesNotContain("face", frames[0]);
        Assert.DoesNotContain("face", frames[1]);
        Assert.Contains("face", frames[2]);
        Assert.Contains("face", frames[3]);
        Assert.Equal(4, frames.Count);
        Assert.Equal(2, sink.Count("face"));
    }

    /// <summary>
    /// A24, A25 (M3-013): a cancel does not clear the send buffer; the next Update flushes what the cancelled clip left,
    /// within the budget, and no EndOfAnimation follows because startSent is cleared.
    /// </summary>
    [Fact]
    public void M3_013_A24_A25_ACancelledClipsLeftoversAreFlushedWithNoEnd()
    {
        var sink = new RobotSink();
        var s = new AnimationScheduler(sink, new Random(1));
        s.Play(SilentClip(10_000, new EventKeyframe(9_000, "late")), 0);
        s.Advance(0);
        Assert.True(s.Stream.Count > 0, "a frame waits in the buffer");
        Assert.True(s.Stop());
        Assert.True(s.Stream.Count > 0, "the cancel keeps the buffer");
        sink.FramesPlayed = 14;
        s.Advance(1);
        Assert.Equal(15, sink.Count("silence"));                  // the leftover went
        Assert.Equal(0, s.Stream.Count);
        Assert.Equal(0, sink.Count("end"));
    }

    /// <summary>
    /// A12, A24, A28, gap4 L8 (M3-013, M5 fix B1): with the ProceduralLive idle on top of the stack, a cancelled clip's
    /// leftovers are dropped, not flushed: a top other than Count goes straight to the idle (0x0057D03A..0x0057D04C), which
    /// neither flushes nor sends an End, and the live idle's InitStream(live, 0xFF) (the previous idle was not the live one,
    /// 0x0057D3F0..0x0057D3FE) drops them (ClearSendBuffer, A12). No EndOfAnimation is sent.
    /// </summary>
    [Fact]
    public void M3_013_A13_A29_A12_WithTheLiveStreamActiveACancelledClipsLeftoversAreDropped()
    {
        var sink = new RobotSink();
        var s = new AnimationScheduler(sink, new Random(1));
        s.PushLiveQuietly();
        Assert.True(s.StreamLive(new HeadKeyframe(0, 100, 5, 0), 0));      // ProceduralLive on top
        sink.FramesPlayed = 1;
        s.Play(SilentClip(10_000, new EventKeyframe(9_000, "late")), 0);
        s.Advance(0);
        Assert.True(s.Stream.Count > 0, "a frame of the clip waits in the buffer");
        int silences = sink.Count("silence");
        Assert.True(s.Stop());
        Assert.True(s.Stream.Count > 0, "the cancel itself keeps the buffer (A24)");

        sink.FramesPlayed = 100;                                               // room enough to flush, were it flushed
        sink.Log.Clear();
        s.Advance(1);
        Assert.Empty(sink.Log);                                                // dropped, not sent
        Assert.Equal(0, s.Stream.Count);
        Assert.Equal(0, sink.Count("end"));
        Assert.True(silences > 0);
    }

    /// <summary>A12: a replacement's InitStream drops what the replaced clip left buffered (ClearSendBuffer, 0x0057B7CE).</summary>
    [Fact]
    public void M3_013_A12_AReplacementDropsTheLeftovers()
    {
        var sink = new RobotSink();
        var s = new AnimationScheduler(sink, new Random(1));
        s.Play(SilentClip(10_000, new EventKeyframe(9_000, "late")), 0);
        s.Advance(0);
        Assert.True(s.Stream.Count > 0);
        s.Play(SilentClip(10_000, new EventKeyframe(9_000, "late")), 1);
        Assert.Equal(0, s.Stream.Count);
    }

    /// <summary>A12, A13: an empty clip (no keyframes, no length) has endSent from InitStream and completes sending nothing.</summary>
    [Fact]
    public void M3_013_A12_AnEmptyClipSendsNothing()
    {
        var sink = new RobotSink();
        var s = new AnimationScheduler(sink, new Random(1));
        var done = s.Play(new AnimationClip { Name = "empty", Keyframes = new List<Keyframe>(), DurationMs = 0 }, 0)!;
        s.Advance(0);
        Assert.Equal(AnimationEndReason.Completed, done.Completion.Result);
        Assert.Equal(new[] { "finished" }, sink.Log);
    }

    /// <summary>C14, 0x0057BFAE: a send that fails stops the drain and is not counted; the message stays at the front.</summary>
    [Fact]
    public void M3_012_C14_AFailedSendIsNotCounted()
    {
        var s = new StreamSendBuffer();
        s.UpdateAmountToSend(0, 0);
        Assert.False(s.SendDirect(new AudioSilence(), _ => false));
        Assert.Equal((0, 0), (s.BytesStreamed, s.FramesStreamed));
        s.Buffer(null, 1, true, () => false);
        Assert.Equal(StreamSendBuffer.DrainResult.SendError, s.Drain());
        Assert.Equal(1, s.Count);
        Assert.Equal((0, 0), (s.BytesStreamed, s.FramesStreamed));
    }

    /// <summary>
    /// M3-017 (policy) through the engine's budget (C9, C14): a stand-alone Play with a robot that reports nothing played
    /// sends exactly what the byte budget allows, 10 frames of 745 bytes, and then gives up after the stall timeout
    /// instead of sending past the budget.
    /// </summary>
    [Fact]
    public void M3_017_C9_APlayNeverSendsPastTheBudget()
    {
        var sent = new List<RobotMessage>();
        var audio = new CozmoAudio(sent.Add) { PlayedFrames = () => 0, PlayedBytes = () => 0 };
        var elapsed = TimeSpan.Zero;
        audio.PlayElapsed = () => { elapsed += TimeSpan.FromMilliseconds(100); return elapsed; };
        audio.Play(CozmoAudio.Tone(440, TimeSpan.FromSeconds(1)));
        Assert.Equal(8192 / 745, sent.OfType<AudioSample>().Count());
        Assert.True(elapsed < TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// M3-017 through the budget with a robot that plays: every frame goes, and at no point are more than 14 frames or
    /// 8192 bytes unplayed (C9).
    /// </summary>
    [Fact]
    public void M3_017_C9_APlayFollowsTheRobotAndKeepsWithinBothBudgets()
    {
        var sent = new List<RobotMessage>();
        int played = 0, maxUnplayed = 0;
        using var frameSent = new AutoResetEvent(false);
        var audio = new CozmoAudio(m => { lock (sent) { sent.Add(m); maxUnplayed = Math.Max(maxUnplayed, sent.Count - Volatile.Read(ref played)); frameSent.Set(); } })
        {
            PlayedFrames = () => Volatile.Read(ref played),
            PlayedBytes = () => Volatile.Read(ref played) * 745,
        };
        var stop = false;
        var robot = new Thread(() =>
        {
            while (!Volatile.Read(ref stop))
            {
                frameSent.WaitOne();
                lock (sent) Volatile.Write(ref played, sent.Count);
            }
        }) { IsBackground = true };
        robot.Start();
        var pcm = CozmoAudio.Tone(440, TimeSpan.FromMilliseconds(600));
        audio.Play(pcm);
        Volatile.Write(ref stop, true);
        frameSent.Set();
        robot.Join();
        Assert.Equal(CozmoAudio.ToFrames(pcm).Count, sent.Count);
        Assert.True(maxUnplayed <= 8192 / 745, $"{maxUnplayed} frames were unplayed at once");
    }

    // ================================================================== camera: M3-001, M3-002, M3-003, M3-005, M3-018, M3-020

    private static ImageChunk Chunk(uint imageId, byte chunkId, byte[] data, byte total = 0, byte encoding = 8) => new()
    {
        ImageId = imageId, ChunkId = chunkId, ImageChunkCount = total, ImageEncoding = encoding,
        ImageResolution = 4, Data = data,
    };

    /// <summary>A1/H1: HandleImageChunk exits unless robot+0x29 (SyncTimeAck received): chunks before it never reach AddChunk.</summary>
    [Fact]
    public void M3_002_A1_ChunksBeforeSyncTimeAckAreIgnored()
    {
        bool synced = false;
        var cam = new CozmoCamera { TimeSynced = () => synced };
        var frames = new List<CameraFrame>();
        cam.FrameReceived += frames.Add;
        cam.Handle(Chunk(1, 0, new byte[] { 0, 1 }, total: 1));
        Assert.Empty(frames);
        Assert.Equal(0, cam.ChunksReceived);
        Assert.Equal(1, cam.ChunksIgnoredBeforeSync);
        synced = true;
        cam.Handle(Chunk(2, 0, new byte[] { 0, 1 }, total: 1));
        Assert.Single(frames);
    }

    /// <summary>R10: the constructor's id is 0xFFFFFFFF with valid 0, so a first chunk carrying that id takes the same-image path and is never delivered.</summary>
    [Fact]
    public void M3_002_R10_AFirstChunkWithTheConstructorsIdIsInvalid()
    {
        var cam = new CozmoCamera();
        var frames = new List<CameraFrame>();
        cam.FrameReceived += frames.Add;
        cam.Handle(Chunk(0xFFFFFFFF, 0, new byte[] { 0, 1 }, total: 1));
        Assert.Empty(frames);
        cam.Handle(Chunk(3, 0, new byte[] { 0, 1 }, total: 1));
        Assert.Single(frames);
    }

    /// <summary>R7/R10: the last chunk is chunkCount - 1 == chunkId in int arithmetic, so a count of 0 never completes, even at chunk 255.</summary>
    [Fact]
    public void M3_002_R10_AChunkCountOf0NeverCompletes()
    {
        var cam = new CozmoCamera();
        var frames = new List<CameraFrame>();
        cam.FrameReceived += frames.Add;
        for (int i = 0; i <= 255; i++) cam.Handle(Chunk(5, (byte)i, new byte[] { 1 }, total: 0));
        Assert.Empty(frames);
    }

    /// <summary>R7: a previous timestamp above the current one invalidates; equal timestamps are accepted (prev &lt;= cur passes).</summary>
    [Fact]
    public void M3_002_R7_AnEqualTimestampIsAccepted()
    {
        var cam = new CozmoCamera();
        var frames = new List<CameraFrame>();
        cam.FrameReceived += frames.Add;
        var a = Chunk(1, 0, new byte[] { 0, 1 }, total: 1); a.FrameTimestamp = 500;
        var b = Chunk(2, 0, new byte[] { 0, 1 }, total: 1); b.FrameTimestamp = 500;
        cam.Handle(a);
        cam.Handle(b);
        Assert.Equal(2, frames.Count);
        Assert.Equal(500u, frames[1].Timestamp);
    }

    /// <summary>
    /// A3: at most 3 completed images per event time go on to vision; the 4th and later are dropped with a warning. A
    /// new event time resets the count.
    /// </summary>
    [Fact]
    public void M3_005_A3_AtMostThreeImagesPerEventTimeReachVision()
    {
        double t = 1.0;
        var cam = new CozmoCamera { EventTime = () => t };
        int received = 0, toVision = 0;
        var log = new List<string>();
        cam.FrameReceived += _ => received++;
        cam.FrameForVision += _ => toVision++;
        cam.Log += log.Add;
        for (uint id = 1; id <= 5; id++) cam.Handle(Chunk(id, 0, new byte[] { 0, 1 }, total: 1));
        Assert.Equal(5, received);
        Assert.Equal(3, toVision);
        Assert.Equal(2, log.Count);
        Assert.Contains("4th image", log[0]);
        t = 1.06;
        cam.Handle(Chunk(9, 0, new byte[] { 0, 1 }, total: 1));
        Assert.Equal(4, toVision);
    }

    private static string Sha16(byte[] b) => Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant()[..16];

    private static byte[] Patched(byte[] header, int h, int w)
    {
        var c = (byte[])header.Clone();
        c[0x5E] = (byte)(h >> 8); c[0x5F] = (byte)h; c[0x60] = (byte)(w >> 8); c[0x61] = (byte)w;
        return c;
    }

    /// <summary>
    /// A13: gray header 324 bytes (table 0x00C48C40, SHA-256 prefix c44b69c9f614304a as the engine stores it, 296 x
    /// 400), SOF0 at 0x59 with 1 component sampled 0x11; colour 334 bytes (0x00C48D84, 106a14ba4dd23964), SOF0 at 0x59
    /// with 3 components, Y sampled 0x21 (2 x 1).
    /// </summary>
    [Fact]
    public void M3_001_A13_TheHeaderTablesAreTheEngines()
    {
        var gray = MiniJpeg.HeaderTable(color: false);
        var colour = MiniJpeg.HeaderTable(color: true);
        Assert.Equal(324, gray.Length);
        Assert.Equal(334, colour.Length);
        Assert.Equal("c44b69c9f614304a", Sha16(Patched(gray, 296, 400)));
        Assert.Equal("106a14ba4dd23964", Sha16(Patched(colour, 240, 320)));
        Assert.Equal(new byte[] { 0xFF, 0xC0 }, gray[0x59..0x5B]);
        Assert.Equal(new byte[] { 0xFF, 0xC0 }, colour[0x59..0x5B]);
        Assert.Equal(1, gray[0x62]);
        Assert.Equal(0x11, gray[0x64]);
        Assert.Equal(3, colour[0x62]);
        Assert.Equal(0x21, colour[0x64]);
    }

    /// <summary>A12: height big-endian at 0x5E/0x5F and width at 0x60/0x61; colour is built at half width (A9: w / 2).</summary>
    [Fact]
    public void M3_001_A12_TheSizeFieldsAreBigEndian()
    {
        var j = MiniJpeg.ToJpeg(new byte[] { 1, 2, 3 }, 160, 240, MiniJpeg.EncodingJpegMinimizedColor);
        Assert.Equal(new byte[] { 0x00, 0xF0, 0x00, 0xA0 }, j[0x5E..0x62]);
        var g = MiniJpeg.ToJpeg(new byte[] { 0, 2, 3 }, 320, 240, MiniJpeg.EncodingJpegMinimizedGray);
        Assert.Equal(new byte[] { 0x00, 0xF0, 0x01, 0x40 }, g[0x5E..0x62]);
    }

    /// <summary>
    /// A12: trailing 0xFF stripped, the first (flag) byte dropped, 0x00 stuffed after each 0xFF, FF D9 appended; with
    /// fewer than 2 bytes left the copy is skipped.
    /// </summary>
    [Fact]
    public void M3_003_A12_StripDropStuffAndEoi()
    {
        int hdr = MiniJpeg.HeaderLength(color: false);
        var j = MiniJpeg.ToJpeg(new byte[] { 0x01, 0xAA, 0xFF, 0xBB, 0xFF, 0xFF }, 320, 240, 8);
        Assert.Equal(new byte[] { 0xAA, 0xFF, 0x00, 0xBB, 0xFF, 0xD9 }, j[hdr..]);
        var one = MiniJpeg.ToJpeg(new byte[] { 0x05, 0xFF }, 320, 240, 8);
        Assert.Equal(new byte[] { 0xFF, 0xD9 }, one[hdr..]);
    }

    /// <summary>
    /// M3-020 (policy, MD2): an empty or all-0xFF payload, where the engine's strip reads data[-1], is a decode failure.
    /// </summary>
    [Fact]
    public void M3_020_AnEmptyOrAll0xFFPayloadIsADecodeFailure()
    {
        Assert.Empty(MiniJpeg.ToJpeg(Array.Empty<byte>(), 320, 240, 8));
        Assert.Empty(MiniJpeg.ToJpeg(new byte[] { 0xFF, 0xFF, 0xFF }, 320, 240, 8));
        var cam = new CozmoCamera();
        CameraFrame? f = null;
        cam.FrameReceived += x => f = x;
        cam.Handle(Chunk(1, 0, new byte[] { 0xFF, 0xFF }, total: 1));
        Assert.NotNull(f);
        Assert.False(f!.TryDecodeGray(out _, out var error));
        Assert.Contains("M3-020", error);
    }

    private static byte[] Jpeg(int w, int h, int channels, Func<int, int, int, byte> px)
    {
        var data = new byte[w * h * channels];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) for (int c = 0; c < channels; c++) data[(y * w + x) * channels + c] = px(x, y, c);
        using var o = new MemoryStream();
        new StbImageWriteSharp.ImageWriter().WriteJpg(data, w, h,
            channels == 1 ? StbImageWriteSharp.ColorComponents.Grey : StbImageWriteSharp.ColorComponents.RedGreenBlue, o, 95);
        return o.ToArray();
    }

    /// <summary>A11: a decoded frame must be 240 rows by 320 columns, otherwise BadDecode.</summary>
    [Fact]
    public void M3_001_A11_OnlyA240By320DecodeIsAccepted()
    {
        var small = new CameraFrame { Encoding = 5, Width = 320, Height = 240, Jpeg = Jpeg(100, 50, 1, (x, y, c) => 128) };
        Assert.False(small.TryDecodeGray(out _, out var error));
        Assert.Contains("BadDecode", error);
        var right = new CameraFrame { Encoding = 5, Width = 320, Height = 240, Jpeg = Jpeg(320, 240, 1, (x, y, c) => 128) };
        Assert.True(right.TryDecodeGray(out var img, out _));
        Assert.Equal((320, 240), (img!.Width, img.Height));
    }

    /// <summary>A7: IsColor is true for 2, 3, 4, 6, 7, 9 and anything above 8; false for 1, 5 and 8.</summary>
    [Fact]
    public void M3_018_A7_IsColor()
    {
        foreach (byte e in new byte[] { 2, 3, 4, 6, 7, 9, 10, 200, 255 }) Assert.True(EncodedImageDecoder.IsColor(e), $"encoding {e}");
        foreach (byte e in new byte[] { 1, 5, 8 }) Assert.False(EncodedImageDecoder.IsColor(e), $"encoding {e}");
    }

    /// <summary>
    /// I2 (0x004F2120..0x004F2134): encoding 0 takes the VERIFY-failure path, which calls
    /// <c>sVerifyFailedReturnFalse</c> with <c>"VERIFY(%s): %s"</c>, <c>"false"</c> and
    /// <c>EnumToString(0) = "NoneImageEncoding"</c> (0x004F2130) and leaves the return register 0. The exact
    /// text is <c>"VERIFY(false): NoneImageEncoding"</c>; it reaches the camera through
    /// <see cref="CozmoCamera.Log"/> / <see cref="VisionSystem.Log"/>.
    /// </summary>
    [Fact]
    public void M3_018_I2_IsColorOfZeroIsFalseAndLogsTheVerifyFailure()
    {
        var log = new List<string>();
        Assert.False(EncodedImageDecoder.IsColor(0, log.Add));
        Assert.Equal(new[] { "VERIFY(false): NoneImageEncoding" }, log);
    }

    /// <summary>
    /// I2 at the engine's point: the engine calls <c>EncodedImage::IsColor</c> from
    /// <c>VisionSystem::Update</c> (0x006B4B7C), so the log for encoding 0 comes from the vision path, not from
    /// the frame's assembly in <c>CozmoCamera</c>.
    /// </summary>
    [Fact]
    public void M3_018_I2_TheIsColorZeroLogIsEmittedAtTheVisionSystemCall()
    {
        using var rig = new Rig();
        using var vision = new VisionSystem(rig.Robot, CameraCalibration.Nominal());
        var log = new List<string>();
        vision.Log += log.Add;
        var frame = new CameraFrame { Encoding = 0, Width = 320, Height = 240, RawPayload = new byte[4], Jpeg = new byte[4] };
        Assert.Null(vision.ProcessFrame(frame));
        Assert.Contains("VERIFY(false): NoneImageEncoding", log);
    }

    /// <summary>
    /// Y0/Y1 (tbh 0x004F289C, base 0x004F2898): the gray dispatch. 0, 3, 4 and 10..255 all go to the default at
    /// 0x004F2924 with <c>EncodedImage.DecodeImageRGB.UnsupportedEncoding</c> and write no image; 1 and 2 are the raw
    /// cases; 5/6/8 decode a JPEG; 7 decodes and borders; 9 decodes the half-width colour JPEG and resizes. The
    /// dispatch is reached through the live entry <see cref="CameraFrame.TryDecodeGray"/>.
    /// </summary>
    [Fact]
    public void M3_001_Y0_TheGrayDispatchTable()
    {
        foreach (byte e in new byte[] { 0, 3, 4, 10, 200, 255 })
        {
            var f = new CameraFrame { Encoding = e, Width = 320, Height = 240, RawPayload = new byte[4], Jpeg = new byte[4] };
            Assert.False(f.TryDecodeGray(out var img, out var error), $"encoding {e}");
            Assert.Null(img);
            Assert.Contains("EncodedImage.DecodeImageRGB.UnsupportedEncoding", error);
        }
        // Y1: EnumToString(0) is "NoneImageEncoding" (pointer table 0x01034A60).
        Assert.False(new CameraFrame { Encoding = 0, Width = 320, Height = 240, RawPayload = new byte[4], Jpeg = new byte[4] }
            .TryDecodeGray(out _, out var e0));
        Assert.Contains("NoneImageEncoding", e0);

        var rawGray = new CameraFrame { Encoding = 1, Width = 320, Height = 240, RawPayload = new byte[320 * 240] };
        Assert.True(rawGray.TryDecodeGray(out var gray, out _));
        Assert.Equal((320, 240), (gray!.Width, gray.Height));

        var rawRgb = new CameraFrame { Encoding = 2, Width = 320, Height = 240, RawPayload = new byte[320 * 240 * 3] };
        Assert.True(rawRgb.TryDecodeGray(out var toGray, out _));
        Assert.Equal((320, 240), (toGray!.Width, toGray.Height));

        foreach (byte e in new byte[] { 5, 6 })
        {
            var f = new CameraFrame { Encoding = e, Width = 320, Height = 240, Jpeg = Jpeg(320, 240, 1, (x, y, c) => 128) };
            Assert.True(f.TryDecodeGray(out var g, out _), $"encoding {e}");
            Assert.Equal((320, 240), (g!.Width, g.Height));
        }

        var eight = new CameraFrame { Encoding = 8, Width = 320, Height = 240, Jpeg = Jpeg(320, 240, 1, (x, y, c) => 128) };
        Assert.True(eight.TryDecodeGray(out var g8, out _));
        Assert.Equal((320, 240), (g8!.Width, g8.Height));

        var nine = new CameraFrame { Encoding = 9, Width = 320, Height = 240, Jpeg = Jpeg(160, 240, 3, (x, y, c) => 100) };
        Assert.True(nine.TryDecodeGray(out var g9, out _));
        Assert.Equal((320, 240), (g9!.Width, g9.Height));
    }

    /// <summary>
    /// Z0/Z1 (tbh 0x004F21B2, base 0x004F21AE): the RGB dispatch. 0, 3, 4 and 10..255 take the default at
    /// 0x004F2256 with the same UnsupportedEncoding log and no image; 1 is gray replicated to RGB; 2 is a raw copy;
    /// 5/6/8 decode a colour JPEG; 7 decodes and borders; 9 decodes and resizes. The dispatch is reached through the
    /// live entry <see cref="CameraFrame.TryDecodeRgb"/>.
    /// </summary>
    [Fact]
    public void M3_018_Z0_TheRgbDispatchTable()
    {
        foreach (byte e in new byte[] { 0, 3, 4, 10, 200, 255 })
        {
            var f = new CameraFrame { Encoding = e, Width = 320, Height = 240, RawPayload = new byte[4], Jpeg = new byte[4] };
            Assert.False(f.TryDecodeRgb(out var rgb, out var error), $"encoding {e}");
            Assert.Null(rgb);
            Assert.Contains("EncodedImage.DecodeImageRGB.UnsupportedEncoding", error);
        }
        // Z1: EnumToString(0) is "NoneImageEncoding" (pointer table 0x01034A60).
        Assert.False(new CameraFrame { Encoding = 0, Width = 320, Height = 240, RawPayload = new byte[4], Jpeg = new byte[4] }
            .TryDecodeRgb(out _, out var e0));
        Assert.Contains("NoneImageEncoding", e0);

        var rawGray = new CameraFrame { Encoding = 1, Width = 320, Height = 240, RawPayload = new byte[320 * 240] };
        Assert.True(rawGray.TryDecodeRgb(out var r1, out _));
        Assert.Equal(320 * 240 * 3, r1!.Length);

        var rawRgb = new CameraFrame { Encoding = 2, Width = 320, Height = 240, RawPayload = new byte[320 * 240 * 3] };
        Assert.True(rawRgb.TryDecodeRgb(out var r2, out _));
        Assert.Equal(320 * 240 * 3, r2!.Length);

        foreach (byte e in new byte[] { 5, 6 })
        {
            var f = new CameraFrame { Encoding = e, Width = 320, Height = 240, Jpeg = Jpeg(320, 240, 3, (x, y, c) => 128) };
            Assert.True(f.TryDecodeRgb(out var r, out _), $"encoding {e}");
            Assert.Equal(320 * 240 * 3, r!.Length);
        }

        var eight = new CameraFrame { Encoding = 8, Width = 320, Height = 240, Jpeg = Jpeg(320, 240, 1, (x, y, c) => 128) };
        Assert.True(eight.TryDecodeRgb(out var r8, out _));
        Assert.Equal(320 * 240 * 3, r8!.Length);

        var nine = new CameraFrame { Encoding = 9, Width = 320, Height = 240, Jpeg = Jpeg(160, 240, 3, (x, y, c) => 100) };
        Assert.True(nine.TryDecodeRgb(out var r9, out _));
        Assert.Equal(320 * 240 * 3, r9!.Length);
    }

    /// <summary>
    /// Y4 (cvtColor code 7, coefficient triple at 0xE2AB0 = {4899, 9617, 1868}):
    /// <c>Y = (4899*R + 9617*G + 1868*B + 8192) &gt;&gt; 14</c>, arithmetic shift, R the payload byte 0. The expected
    /// values are computed by hand from the formula, not read from the code.
    /// </summary>
    [Fact]
    public void M3_001_Y4_ToGrayUsesTheCvtColorFixedPointArithmetic()
    {
        var payload = new byte[320 * 240 * 3];
        void Set(int pixel, byte r, byte g, byte b)
        {
            payload[3 * pixel] = r; payload[3 * pixel + 1] = g; payload[3 * pixel + 2] = b;
        }
        Set(0, 255, 0, 0);
        Set(1, 0, 255, 0);
        Set(2, 0, 0, 255);
        Set(3, 255, 255, 255);
        Set(4, 1, 0, 0);
        Set(5, 0, 1, 0);
        Set(6, 0, 0, 1);

        var f = new CameraFrame { Encoding = 2, Width = 320, Height = 240, RawPayload = payload };
        Assert.True(f.TryDecodeGray(out var image, out _));
        Assert.Equal(76, image!.Pixels[0]);     // (4899*255 + 8192) >> 14 = 76
        Assert.Equal(150, image.Pixels[1]);     // (9617*255 + 8192) >> 14 = 150
        Assert.Equal(29, image.Pixels[2]);      // (1868*255 + 8192) >> 14 = 29
        Assert.Equal(255, image.Pixels[3]);     // (16384*255 + 8192) >> 14 = 255
        Assert.Equal(0, image.Pixels[4]);       // (4899 + 8192) >> 14 = 0
        Assert.Equal(1, image.Pixels[5]);       // (9617 + 8192) >> 14 = 1
        Assert.Equal(0, image.Pixels[6]);       // (1868 + 8192) >> 14 = 0
    }

    /// <summary>
    /// Y2, M3-037 (SD2): case 1 reads <c>rows*cols</c> bytes from the vector start with no length check. A full
    /// payload copies through; a short payload's missing bytes read as 0; a long payload's extra bytes are ignored.
    /// </summary>
    [Fact]
    public void M3_001_Y2_RawGrayCopiesShortFillsAndLongTruncates()
    {
        var full = new byte[320 * 240];
        for (int i = 0; i < full.Length; i++) full[i] = (byte)(i * 7);
        var ff = new CameraFrame { Encoding = 1, Width = 320, Height = 240, RawPayload = full };
        Assert.True(ff.TryDecodeGray(out var fimg, out _));
        Assert.Equal(full, fimg!.Pixels);

        var shortPayload = new byte[100];
        Array.Fill(shortPayload, (byte)0xAB);
        var sf = new CameraFrame { Encoding = 1, Width = 320, Height = 240, RawPayload = shortPayload };
        Assert.True(sf.TryDecodeGray(out var simg, out _));
        Assert.Equal(320 * 240, simg!.Pixels.Length);
        Assert.Equal(0xAB, simg.Pixels[0]);
        Assert.Equal(0xAB, simg.Pixels[99]);
        Assert.Equal(0, simg.Pixels[100]);
        Assert.Equal(0, simg.Pixels[^1]);

        var longPayload = new byte[320 * 240 + 50];
        for (int i = 0; i < longPayload.Length; i++) longPayload[i] = (byte)(i & 0xFF);
        var lf = new CameraFrame { Encoding = 1, Width = 320, Height = 240, RawPayload = longPayload };
        Assert.True(lf.TryDecodeGray(out var limg, out _));
        Assert.Equal(320 * 240, limg!.Pixels.Length);
        for (int i = 0; i < 10; i++) Assert.Equal((byte)(i & 0xFF), limg.Pixels[i]);
    }

    /// <summary>Z6: case 2 is a straight copy with no channel swap (0x004F245A..0x004F246A).</summary>
    [Fact]
    public void M3_018_Z6_RawRgbIsACopyWithNoChannelSwap()
    {
        var payload = new byte[320 * 240 * 3];
        payload[0] = 1; payload[1] = 2; payload[2] = 3;
        payload[3] = 250; payload[4] = 100; payload[5] = 10;
        var f = new CameraFrame { Encoding = 2, Width = 320, Height = 240, RawPayload = payload };
        Assert.True(f.TryDecodeRgb(out var rgb, out _));
        Assert.Equal(320 * 240 * 3, rgb!.Length);
        Assert.Equal(new byte[] { 1, 2, 3, 250, 100, 10 }, rgb[0..6]);

        // M3-037: a short RGB payload zero-fills, a long one is truncated to rows*cols*3.
        var shortPayload = new byte[6];
        shortPayload[0] = 9; shortPayload[3] = 8;
        var sf = new CameraFrame { Encoding = 2, Width = 320, Height = 240, RawPayload = shortPayload };
        Assert.True(sf.TryDecodeRgb(out var srgb, out _));
        Assert.Equal(320 * 240 * 3, srgb!.Length);
        Assert.Equal(new byte[] { 9, 0, 0, 8, 0, 0, 0, 0, 0 }, srgb[0..9]);
        var longPayload = new byte[320 * 240 * 3 + 30];
        var lf = new CameraFrame { Encoding = 2, Width = 320, Height = 240, RawPayload = longPayload };
        Assert.True(lf.TryDecodeRgb(out var lrgb, out _));
        Assert.Equal(320 * 240 * 3, lrgb!.Length);
    }

    /// <summary>Z7: case 1 replicates the gray byte into all three channels (cvtColor code 8 GRAY2BGR).</summary>
    [Fact]
    public void M3_018_Z7_RawGrayReplicatesIntoThreeChannels()
    {
        var gray = new byte[320 * 240];
        gray[0] = 7; gray[1] = 200;
        var f = new CameraFrame { Encoding = 1, Width = 320, Height = 240, RawPayload = gray };
        Assert.True(f.TryDecodeRgb(out var rep, out _));
        Assert.Equal(new byte[] { 7, 7, 7, 200, 200, 200 }, rep![0..6]);
    }

    /// <summary>
    /// Y6 (0x004F2C2C..0x004F2CD6): case 7 decodes, then copyMakeBorder adds 160 zero columns left and right; the
    /// common tail sees 640 columns and rejects it. The "Got 640x240" proves the border ran.
    /// </summary>
    [Fact]
    public void M3_001_Y6_TheGrayBorderMakesTheResult640WideAndBadDecode()
    {
        var f = new CameraFrame { Encoding = 7, Width = 320, Height = 240, Jpeg = Jpeg(320, 240, 1, (x, y, c) => 128) };
        Assert.False(f.TryDecodeGray(out _, out var error));
        // The gray tail loads the RGB event name (0x004F2CF6 -> 0xBE497F), not a gray one.
        Assert.Contains("EncodedImage.DecodeImageRGB.BadDecode", error);
        Assert.Contains("Got 640x240", error);
    }

    /// <summary>Z3 (0x004F2596..0x004F2654): the colour counterpart of Y6; the result is 640 columns and BadDecode.</summary>
    [Fact]
    public void M3_018_Z3_TheRgbBorderMakesTheResult640WideAndBadDecode()
    {
        var f = new CameraFrame { Encoding = 7, Width = 320, Height = 240, Jpeg = Jpeg(320, 240, 3, (x, y, c) => 128) };
        Assert.False(f.TryDecodeRgb(out _, out var error));
        Assert.Contains("BadDecode", error);
        Assert.Contains("Got 640x240", error);
    }

    /// <summary>
    /// Y2/Z6 through the live entry: a raw frame reassembled by <see cref="CozmoCamera"/> (one chunk per 1000 bytes,
    /// under the 0x4B0 cap) is handed to <see cref="CameraFrame.TryDecodeGray"/>. The chunks carry encoding 1, which
    /// AddChunk stores unvalidated (G1).
    /// </summary>
    [Fact]
    public void M3_001_Y2_TheLiveCameraFrameDecodesRawGray()
    {
        var cam = new CozmoCamera();
        CameraFrame? frame = null;
        cam.FrameReceived += f => frame = f;
        var payload = new byte[320 * 240];
        for (int i = 0; i < payload.Length; i++) payload[i] = (byte)(i * 3);
        const int chunkBytes = 1000;
        int count = (payload.Length + chunkBytes - 1) / chunkBytes;
        for (int i = 0; i < count; i++)
            cam.Handle(Chunk(1, (byte)i, payload.Skip(i * chunkBytes).Take(chunkBytes).ToArray(), total: (byte)count, encoding: 1));

        Assert.NotNull(frame);
        Assert.Equal(1, frame!.Encoding);
        Assert.Equal(payload, frame.RawPayload);
        Assert.True(frame.TryDecodeGray(out var image, out _));
        Assert.Equal((320, 240), (image!.Width, image.Height));
        Assert.Equal(payload, image.Pixels);
    }

    /// <summary>
    /// A10: Resize is cv::resize with INTER_LINEAR. For the engine's 160 → 320 columns and 240 → 240 rows, OpenCV's
    /// pixel-centre mapping gives dst[0] = p[0], dst[2k] = (p[k-1] + 3 p[k] + 2) &gt;&gt; 2, dst[2k+1] = (3 p[k] + p[k+1] +
    /// 2) &gt;&gt; 2 and dst[319] = p[159], every row copied as it is.
    /// </summary>
    [Fact]
    public void M3_018_A10_InterLinear160To320()
    {
        var src = new byte[160 * 240];
        var rnd = new Random(7);
        rnd.NextBytes(src);
        var dst = Cozmo.Robot.Vision.Jpeg.OpenCvResize.ResizeLinear(new Cozmo.Robot.Vision.Jpeg.CvMat(240, 160, 1, src), 320, 240).Data;
        for (int y = 0; y < 240; y++)
        {
            byte P(int k) => src[y * 160 + k];
            for (int x = 0; x < 320; x++)
            {
                int k = x / 2;
                int expected = x == 0 ? P(0)
                             : x == 319 ? P(159)
                             : x % 2 == 0 ? (P(k - 1) + 3 * P(k) + 2) >> 2
                             : (3 * P(k) + P(k + 1) + 2) >> 2;
                Assert.Equal(expected, dst[y * 320 + x]);
            }
        }
    }

    /// <summary>
    /// A9, A10, A11, A25: a colour frame is a half-width JPEG decoded as colour, resized to 320 x 240; Save writes it as a
    /// JPEG of that size (quality 90). A gray decode of it is resized the same way (A8 case 9).
    /// </summary>
    [Fact]
    public void M3_018_A9_A25_AColourFrameDecodesTo320By240()
    {
        var f = new CameraFrame
        {
            Encoding = MiniJpeg.EncodingJpegMinimizedColor, Width = 320, Height = 240, JpegWidth = 160, IsColor = true,
            Jpeg = Jpeg(160, 240, 3, (x, y, c) => c == 0 ? (byte)200 : (byte)30),
        };
        Assert.True(f.TryDecodeRgb(out var rgb, out _));
        Assert.Equal(320 * 240 * 3, rgb!.Length);
        Assert.True(rgb[0] > rgb[2] + 100, "red stays red");
        Assert.True(f.TryDecodeGray(out var gray, out _));
        Assert.Equal((320, 240), (gray!.Width, gray.Height));
        var saved = StbImageSharp.ImageResult.FromMemory(f.PresentationJpeg(), StbImageSharp.ColorComponents.RedGreenBlue);
        Assert.Equal((320, 240), (saved.Width, saved.Height));
        Assert.Equal(90, EncodedImageDecoder.SaveQuality);

        var grayFrame = new CameraFrame { Encoding = 8, Jpeg = new byte[] { 1, 2 }, RawPayload = new byte[] { 3 } };
        Assert.Equal(grayFrame.Jpeg, grayFrame.PresentationJpeg());       // A25: encoding 8 writes the rebuilt JPEG
        var other = new CameraFrame { Encoding = 1, Jpeg = new byte[] { 1, 2 }, RawPayload = new byte[] { 3 } };
        Assert.Equal(other.RawPayload, other.PresentationJpeg());          // any other encoding: the raw bytes
    }

    // ================================================================== connection and camera settings: M3-019, M3-021..M3-024

    private sealed class FakePort : IEngineTransport
    {
        public readonly List<byte[]> Sent = new();
        public bool TimedOut { get; set; }
        public event Action<ReceiverEvent>? Received;
        public void Start() { }
        public void Connect(IPAddress ip, bool isSimulated) { }
        public void Disconnect(IPEndPoint address) { }
        public void SendData(byte[] clad) { lock (Sent) Sent.Add(clad); }
        public void Raise(ReceiverMarker m, IPEndPoint? a, byte[]? d = null) => Received?.Invoke(new ReceiverEvent(m, a, d));
        public List<RobotMessage> Messages() { lock (Sent) return Sent.Select(b => RobotMessage.Parse(b)).ToList(); }
    }

    private sealed class Rig : IDisposable
    {
        public long NowNs = 1_000_000_000;
        public readonly FakePort Port = new();
        public readonly CozmoRobot Robot;
        public CozmoEngine Engine => Robot.Engine;
        public static readonly IPAddress RobotIp = IPAddress.Parse("172.31.1.1");
        public static readonly IPEndPoint RobotEp = new(RobotIp, 5551);
        public const string ShippedFw = "{\"version\": 2381, \"time\": 1546972025, \"build\": \"DEVELOPMENT\"}";

        public Rig() => Robot = CozmoRobot.CreateForTest(Port, () => NowNs, new CozmoEngineOptions { BlockPoolPath = "" });
        public void Tick() { NowNs += 60_000_000; Engine.Tick(); }
        public void Data(RobotMessage m) => Port.Raise(ReceiverMarker.Data, RobotEp, m.ToBytes());
        /// <summary>The transport's OnDisconnected marker (CB32/CB33), handled at the next tick as RemoveRobot.</summary>
        public void Disconnected() => Port.Raise(ReceiverMarker.OnDisconnected, RobotEp);

        public void ToValidated(string fw = ShippedFw)
        {
            Engine.ConnectToRobot(RobotIp);
            Tick();
            Port.Raise(ReceiverMarker.OnConnected, RobotEp);
            Tick();
            Data(new RobotAvailable { SerialNumberHead = 0x1234, HwVersion = 5 });
            Data(new FirmwareVersion { RobotId = 1, Signature = Encoding.UTF8.GetBytes(fw) });
            Tick();
        }

        public void ToSuccess(string fw = ShippedFw, uint bodyHw = 7)
        {
            ToValidated(fw);
            Data(new ManufacturingID { SerialNumber = 0xABCD, BodyHwVersion = bodyHw, BodyColor = 2 });
            Tick();
        }

        public void Dispose() => Robot.Dispose();
    }

    /// <summary>
    /// 1h, 1i, 1o, A17, A18 (M3-019 policy MD1, M3-022, M3-026): at a Success response the VisionComponent's
    /// subscriber runs after Robot::SyncTime's sends and before TracePrinter's: it queues the NV CameraCalib read
    /// (tag 0x80000001) and sends SetCameraParams {f32 0.0, u16 0, bool 1}. M3-026: Read only queues; the read goes
    /// out from NVStorage::Update after Gate A, so it is not on the wire until the first synced full state. A24/3a
    /// (M3-023): no EnableColorImages at connection.
    /// </summary>
    [Fact]
    public void M3_019_M3_022_M3_023_1h_TheConnectionSendsSetCameraParamsAndQueuesTheCalibrationRead()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        var sent = rig.Port.Messages();
        int sync = sent.FindIndex(m => m is SyncTime);
        int camera = sent.FindIndex(m => m is SetCameraParams);
        int trace = sent.FindIndex(m => m is SetAppRunID);
        Assert.True(sync >= 0 && camera > sync && trace > camera, $"order: sync {sync}, camera {camera}, trace {trace}");
        Assert.Equal(new byte[] { 0x57, 0, 0, 0, 0, 0, 0, 1 }, sent[camera].ToBytes());
        // M3-026: the calibration read is queued, not sent, at connection.
        Assert.DoesNotContain(sent, m => m is NVCommand);
        Assert.DoesNotContain(sent, m => m is EnableColorImages);
        Assert.Single(sent.OfType<SetCameraParams>());

        // M3-033: after the first synced full state NVStorage::Update pops the queued reads one per tick, in the
        // engine's order; the CameraCalib read follows the 12 constructor reads.
        SendConnectionReadsUntilCalibration(rig);
        var read = NvCommands(rig)[^1];
        Assert.Equal(0x80000001u, read.Tag);
        Assert.Equal(1, read.Length);                              // NVEntry_CameraCalib's factory size-table value
        Assert.Equal(NvStorageComponent.OpRead, read.Op);
        Assert.Equal(0, read.Unknown);
    }

    private static byte[] Calibration56()
    {
        var w = new CladWriter();
        foreach (var f in new[] { 290f, 291f, 160f, 120f, 0f }) w.F32(f);
        w.U16(240); w.U16(320);
        for (int i = 0; i < 8; i++) w.F32(0.01f * (i + 1));
        return w.ToArray();
    }

    /// <summary>
    /// 1j, 2a, 2d (M3-022): vision starts disabled (+0x48 = 0) and the NV callback enables it on all three paths:
    /// NVResult ≠ 0, a size other than 56 (MakeWordAligned(CameraCalibration::Size())), and success, which also installs
    /// the calibration and hands it to vision. The reply's Length is the index, so the 56-byte blob sits at index 0.
    /// </summary>
    [Theory]
    [InlineData(-1, 56, false)]
    [InlineData(0, 40, false)]
    [InlineData(0, 56, true)]
    public void M3_022_1j_TheCalibrationCallbackEnablesVisionOnEveryPath(int result, int size, bool installs)
    {
        using var rig = new Rig();
        using var vision = new Cozmo.Robot.Vision.VisionSystem(rig.Robot) { Enabled = false };
        rig.ToSuccess();                                            // body hardware 7: distortion kept
        Assert.False(rig.Robot.CameraSettings.VisionEnabled);
        SendConnectionReadsUntilCalibration(rig);                   // M3-026/M3-033: the queued reads go out from Update
        var data = Calibration56()[..Math.Min(size, 56)];
        rig.Data(new NVOpResult { Tag = 0x80000001, Op = 0, Result = (sbyte)result, Length = 0, Data = data });
        rig.Tick();
        Assert.True(rig.Robot.CameraSettings.VisionEnabled);
        Assert.True(vision.Enabled);
        if (installs)
        {
            var cal = rig.Robot.CameraSettings.Calibration!;
            Assert.Equal((290.0, 291.0, 320, 240), (cal.FocalLengthX, cal.FocalLengthY, cal.Columns, cal.Rows));
            Assert.Equal(0.01, cal.DistortionCoefficients[0], 6);    // hardware 7: not zeroed
            Assert.Same(cal, vision.Calibration);
        }
        else
        {
            Assert.Null(rig.Robot.CameraSettings.Calibration);
            Assert.Null(vision.Calibration);
        }
    }

    /// <summary>
    /// 1j (M3-022): when the body hardware version (mfgId word 1, the engine Robot's +0x24) is at most 6, the
    /// callback zeroes all eight distortion coefficients before installing the calibration ("IgnoringDistCoeffs").
    /// The operator's robot reports 4.
    /// </summary>
    [Fact]
    public void M3_022_1j_BodyHardwareAtMostSixZeroesTheDistortionCoefficients()
    {
        using var rig = new Rig();
        rig.ToSuccess(bodyHw: 4);
        SendConnectionReadsUntilCalibration(rig);                   // M3-026/M3-033: the queued reads go out from Update
        rig.Data(new NVOpResult { Tag = 0x80000001, Op = 0, Result = 0, Length = 0, Data = Calibration56() });
        rig.Tick();
        var cal = rig.Robot.CameraSettings.Calibration!;
        Assert.All(cal.DistortionCoefficients, d => Assert.Equal(0.0, d, 6));
        Assert.True(rig.Robot.CameraSettings.VisionEnabled);
    }

    // ================================================================== NV storage wire core: M3-025..M3-031, M3-035

    private static List<NVCommand> NvCommands(Rig rig) => rig.Port.Messages().OfType<NVCommand>().ToList();

    /// <summary>
    /// The engine's connection-time NV read order (nv-pass3-connection-queue.md Q2 2c..2j): the 12 constructor
    /// reads (#1 ProgressionUnlock, #2 Inventory, #3/#4 FaceAlbum/Enrollment, #5..#12 the eight ascending RDBM
    /// backup tags), the CameraCalib read (0x80000001), then the mfgId lambda's Lab 0x196000 and Needs 0x194000.
    /// </summary>
    private static readonly uint[] ConnectionReadOrder =
    {
        0x182000, 0x195000, 0x184000, 0x183000,
        0x180000, 0x181000, 0x182000, 0x183000, 0x184000, 0x194000, 0x195000, 0x196000,
        0x80000001, 0x196000, 0x194000,
    };

    /// <summary>
    /// Establishes the first synced full state, so Robot::Update passes Gate A and NVStorage::Update runs (M3-026,
    /// M3-032), then lets the first queued connection-time read go out.
    /// </summary>
    private static void SendFirstFullState(Rig rig, uint timestamp = 1)
    {
        rig.Data(new SyncTimeAck());
        rig.Data(new RobotState { Timestamp = timestamp, PoseOriginId = 1 });
        rig.Tick();
    }

    /// <summary>Answers the in-flight read with <paramref name="result"/> and ticks, which sends the next one.</summary>
    private static void AnswerInFlight(Rig rig, sbyte result = -1)
    {
        var cmd = NvCommands(rig)[^1];
        rig.Data(new NVOpResult { Tag = cmd.Tag, Op = 0, Result = result, Length = 0, Data = Array.Empty<byte>() });
        rig.Tick();
    }

    /// <summary>Answers the in-flight read with <paramref name="result"/> and <paramref name="data"/>.</summary>
    private static void AnswerInFlight(Rig rig, sbyte result, byte[] data)
    {
        var cmd = NvCommands(rig)[^1];
        rig.Data(new NVOpResult { Tag = cmd.Tag, Op = 0, Result = result, Length = 0, Data = data });
        rig.Tick();
    }

    /// <summary>A version-5 NeedsStateOnRobot payload (the Needs read's terminal data; no rewrite follows).</summary>
    private static byte[] NeedsV5() =>
        NonFactoryBlob(Cozmo.Robot.Behavior.NeedsStateOnRobot.Pack(new Cozmo.Robot.Behavior.NeedsStateOnRobot { Version = 5 }));

    /// <summary>
    /// Establishes the first synced full state and drains the whole connection-time NV queue, one read per tick,
    /// leaving it idle. The queue is the engine's 12 constructor reads, CameraCalib, Lab 0x196000 and (when a
    /// NeedsManager is attached) Needs 0x194000; the count is not fixed here so the helper works either way.
    /// </summary>
    private static void DrainConnectionQueue(Rig rig)
    {
        SendFirstFullState(rig);
        var nv = rig.Robot.Engine.NvStorage!;
        int guard = 0;
        while (!nv.IsIdle && guard++ < 100) AnswerInFlight(rig);
        Assert.True(nv.IsIdle, "the connection NV queue never drained");
    }

    /// <summary>
    /// Establishes the first synced full state and answers the 12 constructor reads, leaving the CameraCalib read
    /// (0x80000001) in flight so a test can answer it with its own result.
    /// </summary>
    private static void SendConnectionReadsUntilCalibration(Rig rig)
    {
        SendFirstFullState(rig);
        for (int i = 0; i < 12; i++) AnswerInFlight(rig);
        Assert.Equal(0x80000001u, rig.Robot.Engine.NvStorage!.InFlightTag);
    }

    /// <summary>
    /// Drains the whole connection-time queue (the old helper's contract: an empty queue before the test's own
    /// read). M3-033: the queue is the engine's 12 constructor reads, CameraCalib, Lab and Needs.
    /// </summary>
    private static void DrainCalibrationRead(Rig rig) => DrainConnectionQueue(rig);

    private static byte[] NvHeader(uint total, uint magic)
    {
        var header = new byte[16];
        BitConverter.GetBytes(magic).CopyTo(header, 0);
        BitConverter.GetBytes(total).CopyTo(header, 8);
        return header;
    }

    /// <summary>M3-025: the tag validity rules and the two size tables (0x644148..0x6441A0; 0x643FC8..0x64404E).</summary>
    [Fact]
    public void M3_025_TheTagValidityAndSizeTables()
    {
        Assert.True(NvStorageComponent.IsValidEntryTag(0x180000));
        Assert.True(NvStorageComponent.IsValidEntryTag(0x183000));
        Assert.True(NvStorageComponent.IsValidEntryTag(0x184000));
        Assert.True(NvStorageComponent.IsValidEntryTag(0x197000));
        Assert.True(NvStorageComponent.IsValidEntryTag(0xDE000));
        Assert.True(NvStorageComponent.IsValidEntryTag(0xDE030));
        Assert.False(NvStorageComponent.IsValidEntryTag(0x198000));   // the sentinel key is rejected explicitly
        Assert.False(NvStorageComponent.IsValidEntryTag(0x199000));   // in range but not an exact key
        Assert.False(NvStorageComponent.IsValidEntryTag(0x181001));   // not a multiple of 0x1000
        Assert.False(NvStorageComponent.IsValidEntryTag(0x100000));   // fails (tag-0x180000)>>14 <= 0x1e

        // M3-025 (pass 4b Q1): the 23 factory keys and their values.
        Assert.True(NvStorageComponent.IsFactoryEntryTag(0x80000001));
        Assert.True(NvStorageComponent.IsFactoryEntryTag(0x80000012));
        Assert.True(NvStorageComponent.IsFactoryEntryTag(0xC0000004));
        Assert.True(NvStorageComponent.IsFactoryEntryTag(0x80010000));
        Assert.True(NvStorageComponent.IsFactoryEntryTag(0x80110000));
        Assert.False(NvStorageComponent.IsFactoryEntryTag(0x80000009));
        Assert.False(NvStorageComponent.IsFactoryEntryTag(0x80000013));
        Assert.False(NvStorageComponent.IsFactoryEntryTag(0x80120000));
        Assert.Equal(1, NvStorageComponent.MaxFactorySizeForEntryTag(0x80000001));
        Assert.Equal(1, NvStorageComponent.MaxFactorySizeForEntryTag(0xC0000000));
        Assert.Equal(0xFFFF, NvStorageComponent.MaxFactorySizeForEntryTag(0x80010000));
        Assert.Equal(0xFFFF, NvStorageComponent.MaxFactorySizeForEntryTag(0x80100000));
        Assert.Equal(0x1000, NvStorageComponent.MaxSizeForEntryTag(0x180000));
        Assert.Equal(0x10000, NvStorageComponent.MaxSizeForEntryTag(0x184000));
        Assert.Equal(0, NvStorageComponent.MaxSizeForEntryTag(0x198000));   // the getter refuses the sentinel (1c-4)
        Assert.Equal(0x30, NvStorageComponent.MaxSizeForEntryTag(0xDE000));
        Assert.Equal(0x1DFD0, NvStorageComponent.MaxSizeForEntryTag(0xDE030));
        Assert.Equal(0, NvStorageComponent.MaxSizeForEntryTag(0x123456));
    }

    /// <summary>
    /// M3-025/GetBaseEntryTag (0x6441F8..0x6443F4): an exact factory key is its own base; a factory-block tag
    /// takes tag &amp; 0xFFFF0000 only when that base is not 0xC0000000 (0x006442B2, the inverted compare); a
    /// positive tag below 0x198000 takes the largest _maxSizeTable key at or below it (0x00644228..0x00644338);
    /// anything else is the sentinel 0x198000.
    /// </summary>
    [Fact]
    public void M3_025_GetBaseEntryTagFactoriesAndTheSentinel()
    {
        Assert.Equal(0xC0000004u, NvStorageComponent.GetBaseEntryTag(0xC0000004));   // exact factory key: itself
        Assert.Equal(0x198000u, NvStorageComponent.GetBaseEntryTag(0xC0001234));     // (tag & 0xFFFF0000) == 0xC0000000: sentinel (0x006442B2)
        Assert.Equal(0x80000000u, NvStorageComponent.GetBaseEntryTag(0x80000000));   // exact factory key: itself
        Assert.Equal(0x182000u, NvStorageComponent.GetBaseEntryTag(0x182000));       // exact non-factory key: itself
        Assert.Equal(0x183000u, NvStorageComponent.GetBaseEntryTag(0x183500));       // floor: largest key <= 0x183500
        Assert.Equal(0x184000u, NvStorageComponent.GetBaseEntryTag(0x190000));       // floor: 0x184000 <= 0x190000 < 0x194000
        Assert.Equal(0x198000u, NvStorageComponent.GetBaseEntryTag(0x90000000));     // top bit, unrecognised -> sentinel
        Assert.Equal(0x198000u, NvStorageComponent.GetBaseEntryTag(0x199000));       // >= 0x198000 -> sentinel
        Assert.Equal(0x198000u, NvStorageComponent.GetBaseEntryTag(0x198000));       // the sentinel key maps to itself
    }

    /// <summary>
    /// M3-025/1e-1: the reply-accept check compares GetBaseEntryTag(reply.tag) with the pending request tag, so a
    /// reply carrying a tag in the request's factory block is accepted even though the raw tags differ; a reply
    /// whose base is a different factory block is dropped.
    /// </summary>
    [Fact]
    public void M3_025_TheReplyAcceptCheckUsesTheBaseTag()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        DrainCalibrationRead(rig);
        var nv = rig.Robot.Engine.NvStorage!;

        NvResult? got = null;
        nv.Read(0x80010000, r => got = r);                            // base 0x80010000
        rig.Tick();                                                   // M3-026: Update sends the queued read
        rig.Data(new NVOpResult { Tag = 0x80011234, Op = 0, Result = 0, Length = 0, Data = new byte[1] });
        rig.Tick();
        Assert.NotNull(got);
        Assert.Equal(0, got!.Value.Result);
        Assert.Single(got.Value.Data);

        NvResult? dropped = null;
        nv.Read(0x80010000, r => dropped = r);
        rig.Tick();
        rig.Data(new NVOpResult { Tag = 0x80021234, Op = 0, Result = 0, Length = 0, Data = new byte[1] });   // base 0x80020000
        rig.Tick();
        Assert.Null(dropped);                                         // the reply's base differs: dropped
    }

    /// <summary>M3-026: a READ of an invalid tag is not sent, and the callback gets (-6, empty).</summary>
    [Fact]
    public void M3_026_AnInvalidTagIsNotSentAndTheCallbackGetsMinusSix()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        DrainCalibrationRead(rig);
        int before = NvCommands(rig).Count;
        NvResult? got = null;
        rig.Robot.Engine.NvStorage!.Read(0x123456, r => got = r);
        Assert.NotNull(got);
        Assert.Equal(-6, got!.Value.Result);
        Assert.Empty(got.Value.Data);
        Assert.Equal(before, NvCommands(rig).Count);                             // nothing was sent
        Assert.Contains(rig.Robot.Engine.NvStorage.Log, l => l.Contains("InvalidTag"));
    }

    /// <summary>
    /// M3-027: a non-factory READ computes Length = 0x400 from the tag; the caller no longer passes it. M3-026: the
    /// read is queued by Read and sent by the next Update (Tick).
    /// </summary>
    [Fact]
    public void M3_027_AReadComputesItsLengthFromTheTag()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        DrainCalibrationRead(rig);
        rig.Robot.Engine.NvStorage!.Read(0x182000, _ => { });
        rig.Tick();                                                              // M3-026: Update sends it
        Assert.Equal(0x400, NvStorageComponent.NonFactoryReadLength);            // 0x64536A mov.w r0,#0x400
        var cmd = NvCommands(rig)[^1];
        Assert.Equal(0x182000u, cmd.Tag);
        Assert.Equal(0x400, cmd.Length);
        Assert.Equal(NvStorageComponent.OpRead, cmd.Op);
        Assert.Equal(0, cmd.Unknown);
        Assert.Empty(cmd.Data);   // N1 (0x006428AE..0x006428B4): no WRITE has run, so the saved vector (+0xE8) is empty; later writes: M3_027_N5_N8_*
    }

    /// <summary>
    /// M3-028/0x6430F2..0x64311C: a non-factory header shorter than 16 bytes completes with -3; a bad magic or a
    /// total over MaxSizeForEntryTag(base)-16 completes with -1.
    /// </summary>
    [Fact]
    public void M3_028_TheNonFactoryHeaderRejectsShortBadAndOversizeBlobs()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        DrainCalibrationRead(rig);
        var nv = rig.Robot.Engine.NvStorage!;
        NvResult? got = null;
        Assert.Equal(16, NvStorageComponent.NvHeaderSize);                       // 0x6430F2 cmp r3,#0x10
        Assert.Equal(0x435A4D4Fu, NvStorageComponent.NonFactoryHeaderMagic);     // 0x643108 movw/movt

        nv.Read(0x182000, r => got = r);
        rig.Tick();                                                              // M3-026: Update sends it
        rig.Data(new NVOpResult { Tag = 0x182000, Op = 0, Result = NvStorageComponent.ResultMore, Length = 0, Data = new byte[15] });
        rig.Tick();
        Assert.Equal(-3, got!.Value.Result);

        got = null;
        nv.Read(0x182000, r => got = r);
        rig.Tick();
        rig.Data(new NVOpResult { Tag = 0x182000, Op = 0, Result = NvStorageComponent.ResultMore, Length = 0, Data = NvHeader(0x800, 0xDEADBEEF) });
        rig.Tick();
        Assert.Equal(-1, got!.Value.Result);

        got = null;
        nv.Read(0x182000, r => got = r);
        rig.Tick();
        rig.Data(new NVOpResult { Tag = 0x182000, Op = 0, Result = NvStorageComponent.ResultMore, Length = 0, Data = NvHeader(0x1000, 0x435A4D4F) });
        rig.Tick();
        Assert.Equal(-1, got!.Value.Result);                                     // 0x1000 > MaxSizeForEntryTag(0x182000)-16 = 0xFF0
    }

    /// <summary>
    /// M3-028/0x643840..0x6438CA: when the header's total exceeds the blob, the rest is re-requested with
    /// Length = total + 16, reliable and not hot, with no re-arm (the request stays in flight).
    /// </summary>
    [Fact]
    public void M3_028_TheRestIsReRequestedWithoutReArming()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        DrainCalibrationRead(rig);
        NvResult? got = null;
        rig.Robot.Engine.NvStorage!.Read(0x182000, r => got = r);
        rig.Tick();                                                              // M3-026: Update sends it
        var blob = new byte[16 + 100];
        NvHeader(0x800, 0x435A4D4F).CopyTo(blob, 0);
        rig.Data(new NVOpResult { Tag = 0x182000, Op = 0, Result = NvStorageComponent.ResultMore, Length = 0, Data = blob });
        rig.Tick();
        var cmd = NvCommands(rig)[^1];
        Assert.Equal(0x810, cmd.Length);                                         // Length = total (0x800) + 16
        Assert.Equal(0x182000u, cmd.Tag);
        Assert.Equal(NvStorageComponent.OpRead, cmd.Op);
        Assert.Null(got);
    }

    /// <summary>
    /// M3-028/M3-029: for a non-factory read whose header total fits in the first blob, the delivered entry is
    /// exactly the header total (the resize at 0x643922 targets the reply vector, so reassembly copies total at
    /// offset 0), with the 16-byte header skipped and no 16-byte zero tail.
    /// </summary>
    [Fact]
    public void M3_028_M3_029_TheDeliveredNonFactorySizeIsTheHeaderTotal()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        DrainCalibrationRead(rig);
        NvResult? got = null;
        rig.Robot.Engine.NvStorage!.Read(0x182000, r => got = r);
        rig.Tick();                                                      // M3-026: Update sends it
        const int total = 100;
        var blob = new byte[200];                                        // larger than header + total
        for (int i = 16; i < blob.Length; i++) blob[i] = (byte)(i + 1);
        NvHeader(total, 0x435A4D4F).CopyTo(blob, 0);
        rig.Data(new NVOpResult { Tag = 0x182000, Op = 0, Result = NvStorageComponent.ResultOkay, Length = 0, Data = blob });
        rig.Tick();
        Assert.NotNull(got);
        Assert.Equal(0, got!.Value.Result);
        Assert.Equal(total, got.Value.Data.Length);                      // exactly TOT, no +16 and no zero tail
        var payload = blob[16..(16 + total)];
        Assert.Equal(payload, got.Value.Data);
    }

    /// <summary>
    /// M3-028/M3-029: the header-fits resize (0x00643922) happens once, on the header branch. It must not stay on
    /// the request and cap a later duplicate index-0 blob.
    /// </summary>
    [Fact]
    public void M3_028_M3_029_TheHeaderTotalCapDoesNotPersistToALaterDuplicateBlob()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        DrainCalibrationRead(rig);
        NvResult? got = null;
        rig.Robot.Engine.NvStorage!.Read(0x182000, r => got = r);
        rig.Tick();                                                      // M3-026: Update sends it
        const int total = 100;
        var first = new byte[200];                                       // fits: total <= 200 - 16
        for (int i = 16; i < first.Length; i++) first[i] = (byte)(i + 1);
        NvHeader(total, 0x435A4D4F).CopyTo(first, 0);
        rig.Data(new NVOpResult { Tag = 0x182000, Op = 0, Result = NvStorageComponent.ResultMore, Length = 0, Data = first });
        rig.Tick();
        Assert.Null(got);

        // a later duplicate index-0 blob: the resize is one-time, so this one is not capped at total
        var second = new byte[200];
        for (int i = 16; i < second.Length; i++) second[i] = (byte)(i + 101);
        rig.Data(new NVOpResult { Tag = 0x182000, Op = 0, Result = NvStorageComponent.ResultOkay, Length = 0, Data = second });
        rig.Tick();
        Assert.NotNull(got);
        Assert.Equal(0, got!.Value.Result);
        Assert.Equal(184, got.Value.Data.Length);                        // 200 - 16, not the first blob's total
        Assert.Equal(second[16..200], got.Value.Data);
    }

    /// <summary>
    /// M3-028/M3-029 (pass 4b Q3): the total-size trim is only on the fits branch. After a Length = size+16
    /// re-request the engine reassembles each blob with count = size - 16 and no TOT bound.
    /// </summary>
    [Fact]
    public void M3_028_M3_029_TheReRequestPathDoesNotTrimToTheHeaderTotal()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        DrainCalibrationRead(rig);
        NvResult? got = null;
        rig.Robot.Engine.NvStorage!.Read(0x182000, r => got = r);
        rig.Tick();                                                              // M3-026: Update sends it
        const int total = 100;
        // header only: the entry does not fit in the first blob -> re-request
        rig.Data(new NVOpResult { Tag = 0x182000, Op = 0, Result = NvStorageComponent.ResultMore, Length = 0, Data = NvHeader(total, 0x435A4D4F) });
        rig.Tick();
        Assert.Null(got);

        // the robot resends; the header is already accepted, so no trim to total applies
        var blob = new byte[200];
        for (int i = 16; i < blob.Length; i++) blob[i] = (byte)(i + 1);
        rig.Data(new NVOpResult { Tag = 0x182000, Op = 0, Result = NvStorageComponent.ResultOkay, Length = 0, Data = blob });
        rig.Tick();
        Assert.NotNull(got);
        Assert.Equal(0, got!.Value.Result);
        Assert.Equal(184, got.Value.Data.Length);                        // size - 16 = 200 - 16, not capped at 100
        Assert.Equal(blob[16..200], got.Value.Data);
    }

    /// <summary>
    /// M3-030: with an empty callback the assembled bytes go into the request's sink; with the broadcast flag the
    /// completed buffer is re-chunked into 0x400 blocks (result 3 for a non-final chunk, the request's actual result
    /// for the final one, index in word@4). M3-026: Read queues, Update sends.
    /// </summary>
    [Fact]
    public void M3_030_TheSinkIsFilledAndTheBufferIsBroadcastInChunks()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        DrainCalibrationRead(rig);
        var nv = rig.Robot.Engine.NvStorage!;
        Assert.Equal(1024, NvStorageComponent.BlobStride);                       // 0x643538 lsls r0,r4,#0xa
        var sink = new List<byte>();
        var chunks = new List<NVStorageOpResult>();
        nv.NVStorageOpResultBroadcast += chunks.Add;

        nv.Read(0x80000001, null, sink, broadcast: true);
        rig.Tick();                                                              // M3-026: Update sends it
        rig.Data(new NVOpResult { Tag = 0x80000001, Op = 0, Result = NvStorageComponent.ResultMore, Length = 0, Data = new byte[1024] });
        rig.Tick();
        rig.Data(new NVOpResult { Tag = 0x80000001, Op = 0, Result = NvStorageComponent.ResultMore, Length = 1, Data = new byte[1024] });
        rig.Tick();
        // the terminal result is not 0, so the final chunk must carry it (0x0064573C), not a forced 0
        rig.Data(new NVOpResult { Tag = 0x80000001, Op = 0, Result = NvStorageComponent.ResultScheduled, Length = 0, Data = Array.Empty<byte>() });
        rig.Tick();

        Assert.Equal(2048, sink.Count);                                          // index 0 and 1, stride 1024
        Assert.Equal(2, chunks.Count);
        Assert.Equal((sbyte)3, chunks[0].Result);                                // non-final chunk is MORE
        Assert.Equal(NvStorageComponent.ResultScheduled, chunks[1].Result);      // final chunk is the actual result
        Assert.Equal(0, chunks[0].Index);
        Assert.Equal(1, chunks[1].Index);
        Assert.Equal(0x80000001u, chunks[0].Tag);
        Assert.Equal(1024, chunks[0].Data.Length);
    }

    /// <summary>
    /// M3-030/3a (0x00643568..0x00643596): the reassembly writes each applied blob straight into the caller's
    /// +0x54 vector, so after a timeout (Update state 1, 0x006457A8..0x006457C0) the sink still holds the blobs
    /// already applied. The timeout's own callback gets (nullptr, 0, -4), not the sink.
    /// </summary>
    [Fact]
    public void M3_030_TheSinkKeepsAppliedBlobsAfterATimeout()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        DrainCalibrationRead(rig);
        rig.Data(new SyncTimeAck());
        rig.Data(new RobotState { Timestamp = 1000, PoseOriginId = 1 });
        rig.Tick();

        var nv = rig.Robot.Engine.NvStorage!;
        var sink = new List<byte>();
        NvResult? got = null;
        nv.Read(0x80010000, r => got = r, sink);                 // factory tag: no 16-byte header
        rig.Tick();                                              // sent: deadline = 1000 + 5000 = 6000
        var blob = Enumerable.Range(0, 1024).Select(i => (byte)i).ToArray();
        rig.Data(new NVOpResult { Tag = 0x80010000, Op = 0, Result = NvStorageComponent.ResultMore, Length = 0, Data = blob });
        rig.Tick();
        Assert.Equal(blob, sink);                                // applied as it arrived, not at completion

        rig.Data(new RobotState { Timestamp = 6001, PoseOriginId = 1 });
        rig.Tick();                                              // 6001 > 6000: the read times out
        Assert.NotNull(got);
        Assert.Equal(-4, got!.Value.Result);
        Assert.Empty(got.Value.Data);                            // the engine's timeout delivers (nullptr, 0, -4)
        Assert.Equal(blob, sink);                                // the applied blob survives in the sink
    }

    /// <summary>
    /// M3-030/3b (0x00643770..0x00643798): the broadcast loop runs at most 1000 chunks (cmp r4,#0x3e8); a buffer
    /// that still has data after the 1000th broadcast stops there and logs LoopBoundOverflow via sErrorF with
    /// "../../../../engine/components/nvStorageComponent.cpp", line 0x4a7.
    /// </summary>
    [Fact]
    public void M3_030_TheBroadcastLoopStopsAt1000Chunks()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        DrainCalibrationRead(rig);
        var nv = rig.Robot.Engine.NvStorage!;
        var chunks = new List<NVStorageOpResult>();
        nv.NVStorageOpResultBroadcast += chunks.Add;

        nv.Read(0x80010000, null, null, broadcast: true);
        rig.Tick();
        // one 1024-byte blob at index 999 grows the buffer to 1000*1024 (zero-filled), so the completion needs
        // exactly 1000 chunks and the bound fires.
        rig.Data(new NVOpResult { Tag = 0x80010000, Op = 0, Result = 0, Length = 999, Data = new byte[1024] });
        rig.Tick();

        Assert.Equal(1000, chunks.Count);
        Assert.Contains(nv.Log, l => l.Contains("LoopBoundOverflow"));
        Assert.Contains(nv.Log, l => l.Contains("nvStorageComponent.cpp:1191"));   // 0x4a7 in decimal
    }

    /// <summary>
    /// M3-030 (0x00643600..0x00643694): the read completion logs its outcome by the final result: ReadSuccess
    /// (0, 0x00643640), ReadEntryNotFound (-1, 0x0064360E) or ReadFailed (anything else, 0x0064366E). The base
    /// tag is named through NVStorage::EnumToString(NVEntryTag) (0x7CEE38). A negative result also logs
    /// ReadOpFailed first (0x006434E4).
    /// </summary>
    [Fact]
    public void M3_030_TheReadCompletionLogsTheOutcome()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        DrainCalibrationRead(rig);
        var nv = rig.Robot.Engine.NvStorage!;

        nv.Read(0x182000, _ => { });
        rig.Tick();
        rig.Data(new NVOpResult { Tag = 0x182000, Op = 0, Result = 0, Length = 0, Data = NonFactoryBlob(new byte[] { 1 }) });
        rig.Tick();
        Assert.Contains(nv.Log, l => l.Contains("ReadSuccess") && l.Contains("NVEntry_GameUnlocks") && l.Contains("NV_OKAY"));

        nv.Read(0x182000, _ => { });
        rig.Tick();
        rig.Data(new NVOpResult { Tag = 0x182000, Op = 0, Result = -1, Length = 0, Data = Array.Empty<byte>() });
        rig.Tick();
        Assert.Contains(nv.Log, l => l.Contains("ReadOpFailed") && l.Contains("NV_NOT_FOUND"));
        Assert.Contains(nv.Log, l => l.Contains("ReadEntryNotFound") && l.Contains("NV_NOT_FOUND"));

        nv.Read(0x182000, _ => { });
        rig.Tick();
        rig.Data(new NVOpResult { Tag = 0x182000, Op = 0, Result = -6, Length = 0, Data = Array.Empty<byte>() });
        rig.Tick();
        Assert.Contains(nv.Log, l => l.Contains("ReadOpFailed") && l.Contains("NV_BAD_ARGS"));
        Assert.Contains(nv.Log, l => l.Contains("ReadFailed") && l.Contains("NV_BAD_ARGS"));
    }

    /// <summary>
    /// M3-025 (0x00644274, 0x006442EC): GetBaseEntryTag warns FactoryTagNotFound when the factory path finds no
    /// key, and TagIsTooSmall when the non-factory coarse test tag >> 15 &lt; 0x33 fails or no table key is at or
    /// below the tag. Both formats are "0x%x" with the tag. Driven through the reply accept check (OnResult).
    /// </summary>
    [Fact]
    public void M3_025_GetBaseEntryTagLogsTheMissingFactoryTagAndTheTooSmallTag()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        DrainCalibrationRead(rig);
        var nv = rig.Robot.Engine.NvStorage!;

        NvResult? got = null;
        nv.Read(0x182000, r => got = r);
        rig.Tick();
        rig.Data(new NVOpResult { Tag = 0x90000000, Op = 0, Result = 0, Length = 0, Data = Array.Empty<byte>() });
        rig.Tick();
        Assert.Null(got);                                        // the sentinel base != 0x182000: dropped
        Assert.Contains(nv.Log, l => l.Contains("GetBaseEntryTag.FactoryTagNotFound") && l.Contains("0x90000000"));

        nv.Read(0x182000, r => got = r);
        rig.Tick();
        rig.Data(new NVOpResult { Tag = 0x199000, Op = 0, Result = 0, Length = 0, Data = Array.Empty<byte>() });
        rig.Tick();
        Assert.Null(got);
        Assert.Contains(nv.Log, l => l.Contains("GetBaseEntryTag.TagIsTooSmall") && l.Contains("0x199000"));
    }

    /// <summary>
    /// M3-031/0x645C6A..0x645D7A: a retryable negative result resends the identical command 7 times (8
    /// transmissions; ResendLastCommand increments +0xF4 then compares &lt; +0xF5 = 8), then completes with the
    /// original result (ReadOpFailed).
    /// </summary>
    [Fact]
    public void M3_031_ARetryableNegativeResultIsResentThenCompletes()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        DrainCalibrationRead(rig);
        var nv = rig.Robot.Engine.NvStorage!;
        Assert.Equal(7, NvStorageComponent.MaxReadResends);                      // +0xF5 = 8, 0-based counter -> 7 resends
        NvResult? got = null;
        nv.Read(0x80000001, r => got = r);
        rig.Tick();                                                              // M3-026: the initial transmission
        int afterRead = NvCommands(rig).Count;                                   // the initial transmission

        for (int i = 0; i < NvStorageComponent.MaxReadResends; i++)
        {
            rig.Data(new NVOpResult { Tag = 0x80000001, Op = 0, Result = -8, Length = 0, Data = Array.Empty<byte>() });
            rig.Tick();
            Assert.Null(got);
            Assert.Equal(afterRead + i + 1, NvCommands(rig).Count);              // one identical resend per retry
        }
        Assert.Equal(NvStorageComponent.MaxReadResends, NvCommands(rig).Count - afterRead);   // 7 resends after the initial
        rig.Data(new NVOpResult { Tag = 0x80000001, Op = 0, Result = -8, Length = 0, Data = Array.Empty<byte>() });
        rig.Tick();
        Assert.NotNull(got);
        Assert.Equal(-8, got!.Value.Result);
        Assert.Equal(afterRead + NvStorageComponent.MaxReadResends, NvCommands(rig).Count);
        // M3-031: ResendLastCommand logs Retry (info, 0x00645C6A..0x00645D34) for each of the 7 resends, then
        // NumRetriesExceeded (error, 0x00645D34) when +0xF4 reaches +0xF5 = 8; the caller then logs ReadOpFailed
        // (0x006434E4) for the negative result.
        Assert.Equal(NvStorageComponent.MaxReadResends, nv.Log.Count(l => l.Contains("ResendLastCommand.Retry")));
        Assert.Contains(nv.Log, l => l.Contains("ResendLastCommand.Retry") && l.Contains("NVOP_READ") && l.Contains("Attempt: 7"));
        Assert.Contains(nv.Log, l => l.Contains("ResendLastCommand.NumRetriesExceeded") && l.Contains("Attempts: 8"));
        Assert.Contains(nv.Log, l => l.Contains("ReadOpFailed") && l.Contains("NV_LOOP"));
    }

    /// <summary>
    /// M3-031/0x64575A..0x6457C0: a read whose deadline (the synchronised robot clock robot+0x2C + 5000) has passed
    /// delivers (-4, empty) and is not retried. M3-026: the deadline is armed when Update sends the queued read, so
    /// the read must go out before the later state arrives.
    /// </summary>
    [Fact]
    public void M3_031_TheRobotClockTimeoutDeliversMinusFour()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        DrainCalibrationRead(rig);
        rig.Data(new SyncTimeAck());
        rig.Data(new RobotState { Timestamp = 1000, PoseOriginId = 1 });
        rig.Tick();
        Assert.Equal(1000u, rig.Robot.State.Latest!.Timestamp);
        Assert.Equal(1000u, rig.Engine.Robot!.StoredState!.Timestamp);

        var nv = rig.Robot.Engine.NvStorage!;
        NvResult? got = null;
        nv.Read(0x182000, r => got = r);
        rig.Tick();                                                              // sent now: deadline = 1000 + 5000 = 6000
        rig.Data(new RobotState { Timestamp = 6001, PoseOriginId = 1 });
        rig.Tick();
        Assert.NotNull(got);
        Assert.Equal(-4, got!.Value.Result);
        Assert.Empty(got.Value.Data);
        Assert.Contains("warning: NVStorageComponent.Update.ReadTimeout: Tag: 0x182000", nv.Log);
    }

    /// <summary>
    /// M3-031/M3-027: the deadline is armed from the synchronised robot clock (robot+0x2C) when Update sends the
    /// queued read, and the timeout compare is strictly greater. This stack's robot+0x2C is
    /// <c>EngineRobot.StoredState</c>, the state that passed the time-sync gate (0x0051293C..0x00512954), not the
    /// unfiltered <c>State.Latest</c>.
    /// </summary>
    [Fact]
    public void M3_031_TheDeadlineUsesTheSyncedClockAtSend()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        DrainCalibrationRead(rig);                                               // StoredState = 1
        var nv = rig.Robot.Engine.NvStorage!;
        NvResult? got = null;
        nv.Read(0x182000, r => got = r);
        rig.Tick();                                                              // sent: deadline = 1 + 5000 = 5001

        rig.Data(new RobotState { Timestamp = 5001, PoseOriginId = 1 });
        rig.Tick();
        Assert.Null(got);                                                        // 5001 is not > 5001

        rig.Data(new RobotState { Timestamp = 5002, PoseOriginId = 1 });
        rig.Tick();
        Assert.NotNull(got);
        Assert.Equal(-4, got!.Value.Result);
        Assert.Empty(got.Value.Data);
    }

    /// <summary>
    /// M3-026: <c>Read</c> only queues; the request goes out from the next <c>Update</c>, and the queue is one at a
    /// time (the next request waits for the completion, then the next Update).
    /// </summary>
    [Fact]
    public void M3_026_AReadIsNotSentUntilTheNextUpdateAndTheQueueIsOneAtATime()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        DrainCalibrationRead(rig);
        var nv = rig.Robot.Engine.NvStorage!;
        int before = NvCommands(rig).Count;
        NvResult? first = null, second = null;

        nv.Read(0x182000, r => first = r);
        nv.Read(0x183000, r => second = r);
        Assert.Equal(before, NvCommands(rig).Count);                             // Read sends nothing

        rig.Tick();                                                              // one Update: the front goes out
        var sent = NvCommands(rig);
        Assert.Equal(before + 1, sent.Count);
        Assert.Equal(0x182000u, sent[^1].Tag);
        Assert.Null(first);
        Assert.Null(second);

        // complete the first; the second goes out only on the next Update
        rig.Data(new NVOpResult { Tag = 0x182000, Op = 0, Result = 0, Length = 0, Data = new byte[1] });
        rig.Tick();
        Assert.NotNull(first);
        sent = NvCommands(rig);
        Assert.Equal(before + 2, sent.Count);
        Assert.Equal(0x183000u, sent[^1].Tag);
        Assert.Null(second);
    }

    /// <summary>
    /// M3-035/0x643E80..0x643F8C: a disconnect discards the in-flight read with no callback, and the old deadline
    /// cannot fire afterwards even as the robot clock advances. This drives the live removal path, not
    /// <c>OnDisconnected</c> directly: the transport's OnDisconnected marker is handled at the next tick as
    /// <c>RobotManager::RemoveRobot</c> (CB32/CB33 0x0052F248..0x0052F364), which raises
    /// <c>CozmoEngine.RobotRemoved</c>; <c>CozmoRobot.ResetDevices</c> (CozmoRobot.cs:670) then calls
    /// <c>NvStorageComponent.OnDisconnected</c>.
    /// </summary>
    [Fact]
    public void M3_035_ADisconnectDiscardsTheReadWithNoCallbackOrTimeout()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        DrainCalibrationRead(rig);
        rig.Data(new SyncTimeAck());
        rig.Data(new RobotState { Timestamp = 1000, PoseOriginId = 1 });
        rig.Tick();

        var nv = rig.Robot.Engine.NvStorage!;
        NvResult? got = null, queued = null;
        nv.Read(0x182000, r => got = r);
        rig.Tick();                                          // the read goes out and is in flight
        Assert.Equal(0x182000u, nv.InFlightTag);
        nv.Read(0x183000, r => queued = r);                  // a second read is queued behind it
        Assert.Single(nv.QueuedTags);

        // the live path: OnDisconnected -> RemoveRobot -> RobotRemoved -> ResetDevices -> OnDisconnected
        rig.Disconnected();
        rig.Tick();
        Assert.Null(rig.Engine.Robot);
        Assert.True(nv.IsIdle);
        Assert.Empty(nv.QueuedTags);                         // the queue is discarded with the in-flight request

        // a late reply and a later clock cannot deliver a callback or fire a timeout
        rig.Data(new NVOpResult { Tag = 0x182000, Op = 0, Result = 0, Length = 0, Data = new byte[20] });
        rig.Tick();
        Assert.Null(got);
        Assert.Null(queued);

        rig.Data(new RobotState { Timestamp = 6001, PoseOriginId = 1 });
        rig.Tick();
        nv.Update();
        Assert.Null(got);
        Assert.Null(queued);
        Assert.True(nv.IsIdle);
    }

    // ================================================================== M3-033/M3-034: the connection-time NV queue

    /// <summary>A non-factory reply: the 16-byte "OMZC" header, whose u32@8 is the payload length, then the payload.</summary>
    private static byte[] NonFactoryBlob(byte[] payload)
    {
        var data = new byte[16 + payload.Length];
        NvHeader((uint)payload.Length, NvStorageComponent.NonFactoryHeaderMagic).CopyTo(data, 0);
        payload.CopyTo(data, 16);
        return data;
    }

    /// <summary>
    /// M3-033 (nv-pass3-connection-queue.md Q2 2c..2j): the engine Robot constructor queues 12 reads, then the
    /// mfgId lambda's CameraCalib, Lab and Needs reads follow, and they go out one per Robot::Update after Gate A
    /// in that order. The expected order is the extraction's list, not the implementation's. No VisionSystem is
    /// built: the engine still queues #3/#4 and the queue still drains to ready-to-stream.
    /// </summary>
    [Fact]
    public void M3_033_TheConnectionQueuesTheEngineNvReadsBeforeReadyToStream()
    {
        using var rig = new Rig();
        var needs = new Cozmo.Robot.Behavior.NeedsManager(() => 0) { NvStorage = rig.Robot.Engine.NvStorage };
        needs.AttachConnectionRead(rig.Robot.Engine);
        rig.Robot.Engine.SerialNumberAcquired += needs.InitAfterSerialNumberAcquired;
        try
        {
            rig.ToSuccess();
            SendFirstFullState(rig);

            var tags = new List<uint>();
            var nv = rig.Robot.Engine.NvStorage!;
            int i = 0;
            while (!nv.IsIdle && i < 100)
            {
                tags.Add(NvCommands(rig)[^1].Tag);
                // the last read is the Needs read (0x194000); a version-5 reply leaves no rewrite queued
                if (i == 14) AnswerInFlight(rig, 0, NeedsV5());
                else AnswerInFlight(rig);
                i++;
            }
            Assert.Equal(ConnectionReadOrder, tags);
            Assert.True(rig.Engine.Robot!.ReadyToStream);
        }
        finally { rig.Robot.Engine.SerialNumberAcquired -= needs.InitAfterSerialNumberAcquired; needs.DetachConnectionRead(); }
    }

    /// <summary>
    /// M3-034: the connection reads' callbacks reach their layers, in the live order. The engine Robot is built
    /// by ConnectToRobot (AddRobot) and queues #1..#12; <c>CozmoRobot.ConnectAsync</c> returns on the Success
    /// response and only then does the caller build the VisionSystem, so the FaceAlbum/Enrollment reads (#3/#4)
    /// complete after it exists and its handler adopts them. The Needs read (#15) reaches the NeedsManager.
    /// ProgressionUnlock, Inventory, the RDBM backup reads and Lab have no component here, so their reads complete
    /// with a no-op sink (the record's unresolved names each missing layer).
    /// </summary>
    [Fact]
    public void M3_034_TheConnectionReadCallbacksReachTheirLayers()
    {
        using var rig = new Rig();
        var needs = new Cozmo.Robot.Behavior.NeedsManager(() => 0) { NvStorage = rig.Robot.Engine.NvStorage };
        needs.AttachConnectionRead(rig.Robot.Engine);
        rig.Robot.Engine.SerialNumberAcquired += needs.InitAfterSerialNumberAcquired;
        try
        {
            // the live order: ConnectToRobot first (AddRobot queues the reads), then the VisionSystem
            rig.ToValidated();
            using var vision = new Cozmo.Robot.Vision.VisionSystem(rig.Robot) { Enabled = false };
            rig.Data(new ManufacturingID { SerialNumber = 0xABCD, BodyHwVersion = 7, BodyColor = 2 });
            rig.Tick();
            SendFirstFullState(rig);

            var album = new byte[] { 0x11, 0x22, 0x33 };
            var enrollment = new byte[] { 0x44, 0x55 };
            var nv = rig.Robot.Engine.NvStorage!;
            int i = 0;
            while (!nv.IsIdle && i < 100)
            {
                var cmd = NvCommands(rig)[^1];
                Assert.Equal(ConnectionReadOrder[i], cmd.Tag);
                byte[] data = i switch
                {
                    2 => NonFactoryBlob(album),       // #3 FaceAlbum 0x184000
                    3 => NonFactoryBlob(enrollment),  // #4 FaceEnrollment 0x183000
                    14 => NeedsV5(),                  // #15 Needs 0x194000 (version 5: no rewrite queued)
                    _ => Array.Empty<byte>(),
                };
                rig.Data(new NVOpResult { Tag = cmd.Tag, Op = 0, Result = data.Length > 0 ? (sbyte)0 : (sbyte)-1, Length = 0, Data = data });
                rig.Tick();
                i++;
            }

            Assert.Equal(15, i);
            var (gotAlbum, gotEnrollment) = vision.GetSerializedFaceData();
            Assert.Equal(album, gotAlbum);
            Assert.Equal(enrollment, gotEnrollment);
            Assert.True(needs.RobotReadSucceeded);
            Assert.True(needs.HasRobotCopy);
        }
        finally { rig.Robot.Engine.SerialNumberAcquired -= needs.InitAfterSerialNumberAcquired; needs.DetachConnectionRead(); }
    }

    /// <summary>
    /// M3-033/CD20: ready-to-stream is set by the NV on-idle callback only after the whole queue drains. While the
    /// last read is in flight it is still unset; completing it runs the callback and sets it. No VisionSystem is
    /// built, so this also proves the engine's reads drain when nothing consumes the FaceAlbum result.
    /// </summary>
    [Fact]
    public void M3_033_ReadyToStreamWaitsForTheWholeConnectionQueue()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        SendFirstFullState(rig);
        var nv = rig.Robot.Engine.NvStorage!;
        Assert.False(rig.Engine.Robot!.ReadyToStream);
        while (nv.QueuedTags.Count > 0)
        {
            AnswerInFlight(rig);
            Assert.False(rig.Engine.Robot!.ReadyToStream);   // still reads in the queue or in flight
        }
        Assert.NotNull(nv.InFlightTag);                       // the last read is in flight
        Assert.False(rig.Engine.Robot!.ReadyToStream);
        AnswerInFlight(rig);                                  // the last read completes: the on-idle callback runs
        Assert.True(nv.IsIdle);
        Assert.True(rig.Engine.Robot!.ReadyToStream);
    }

    /// <summary>
    /// M3-034: a VisionSystem built after the connection reads already completed adopts the buffered result. This
    /// is the live order's late edge: the reads complete after Gate A, which can be after the caller builds the
    /// VisionSystem; the engine keeps ConnectionFaceAlbumResult for it.
    /// </summary>
    [Fact]
    public void M3_034_AVisionSystemBuiltAfterTheReadsAdoptTheBufferedFaceAlbum()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        SendFirstFullState(rig);

        var album = new byte[] { 0x0A, 0x0B };
        var enrollment = new byte[] { 0x0C };
        var nv = rig.Robot.Engine.NvStorage!;
        int i = 0;
        while (!nv.IsIdle && i < 100)
        {
            var cmd = NvCommands(rig)[^1];
            byte[] data = i switch
            {
                2 => NonFactoryBlob(album),       // #3 FaceAlbum 0x184000
                3 => NonFactoryBlob(enrollment),  // #4 FaceEnrollment 0x183000
                _ => Array.Empty<byte>(),
            };
            rig.Data(new NVOpResult { Tag = cmd.Tag, Op = 0, Result = data.Length > 0 ? (sbyte)0 : (sbyte)-1, Length = 0, Data = data });
            rig.Tick();
            i++;
        }

        Assert.NotNull(rig.Engine.ConnectionFaceAlbumResult);   // buffered with no subscriber
        using var vision = new Cozmo.Robot.Vision.VisionSystem(rig.Robot) { Enabled = false };
        var (gotAlbum, gotEnrollment) = vision.GetSerializedFaceData();
        Assert.Equal(album, gotAlbum);
        Assert.Equal(enrollment, gotEnrollment);
    }

    /// <summary>
    /// M3-034 (0x0065A85E..0x0065A9DE): the FaceEnrollment read callback (the engine's VC+0x300 read, #4
    /// 0x183000) logs <c>ReadFaceEnrollDataNotFound</c> when the result is -1 (0x0065A8F0: sChanneledInfoF,
    /// channel "Unnamed", no fields) and <c>ReadFaceEnrollDataFail</c> for any other non-zero result (0x0065A916:
    /// sWarningF, "NVResult = %s" with NVStorage::EnumToString(NVResult)). Result 0 installs the album.
    /// </summary>
    [Theory]
    [InlineData(-1, "ReadFaceEnrollDataNotFound", "NV_NOT_FOUND")]
    [InlineData(-6, "ReadFaceEnrollDataFail", "NV_BAD_ARGS")]
    public void M3_034_TheEnrollReadCallbackLogsNotFoundAndFail(sbyte result, string logName, string resultName)
    {
        using var rig = new Rig();
        var logs = new List<string>();
        rig.Engine.LogLine += l => { lock (logs) logs.Add(l); };
        rig.ToSuccess();
        SendFirstFullState(rig);
        var nv = rig.Robot.Engine.NvStorage!;
        int i = 0;
        while (!nv.IsIdle && i < 100)
        {
            var cmd = NvCommands(rig)[^1];
            // #4 FaceEnrollment 0x183000 is index 3 in ConnectionReadOrder; every other read answers -1.
            sbyte answer = i == 3 ? result : (sbyte)-1;
            rig.Data(new NVOpResult { Tag = cmd.Tag, Op = 0, Result = answer, Length = 0, Data = Array.Empty<byte>() });
            rig.Tick();
            i++;
        }

        Assert.Null(rig.Engine.ConnectionFaceAlbumResult);       // the enroll read did not succeed
        Assert.Contains(logs, l => l.Contains($"VisionComponent.LoadFaceAlbumFromRobot.{logName}"));
        if (logName == "ReadFaceEnrollDataFail")
            Assert.Contains(logs, l => l.Contains("ReadFaceEnrollDataFail") && l.Contains($"NVResult = {resultName}"));
    }

    /// <summary>
    /// M3-034/3d (0x0051116C, ~VisionComponent 0x0065258E): the Robot's VisionComponent, and with it the
    /// FaceAlbum/Enrollment bytes at VC+0x2F4/VC+0x300, is destroyed on removal, so
    /// <c>CozmoEngine.ConnectionFaceAlbumResult</c> is cleared with the serial (ClearAcquiredSerialNumber,
    /// called from RobotManager::RemoveRobot). A VisionSystem built after the reconnect does not adopt the old
    /// album.
    /// </summary>
    [Fact]
    public void M3_034_TheFaceAlbumResultIsClearedOnRemoval()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        SendFirstFullState(rig);

        var nv = rig.Robot.Engine.NvStorage!;
        int i = 0;
        while (!nv.IsIdle && i < 100)
        {
            var cmd = NvCommands(rig)[^1];
            byte[] data = i switch
            {
                2 => NonFactoryBlob(new byte[] { 0x11 }),   // #3 FaceAlbum 0x184000
                3 => NonFactoryBlob(new byte[] { 0x22 }),   // #4 FaceEnrollment 0x183000
                _ => Array.Empty<byte>(),
            };
            rig.Data(new NVOpResult { Tag = cmd.Tag, Op = 0, Result = data.Length > 0 ? (sbyte)0 : (sbyte)-1, Length = 0, Data = data });
            rig.Tick();
            i++;
        }
        Assert.NotNull(rig.Engine.ConnectionFaceAlbumResult);

        rig.Disconnected();                                      // the live removal path
        rig.Tick();
        Assert.Null(rig.Engine.Robot);
        Assert.Null(rig.Engine.ConnectionFaceAlbumResult);

        using var vision = new Cozmo.Robot.Vision.VisionSystem(rig.Robot) { Enabled = false };
        var (album, enrollment) = vision.GetSerializedFaceData();
        Assert.Empty(album);
        Assert.Empty(enrollment);
    }

    /// <summary>
    /// M3-033/M15-014: the engine queues the Needs read 0x194000 from its mfgId handler, exactly once, and
    /// buffers the terminal result. The tag is also one of the eight RDBM backup reads (0x194000), so the
    /// connection queue legitimately carries it twice: the backup read and the Needs read. A NeedsManager
    /// attached after the read completed adopts the buffered Needs result through its OnRobotRead path
    /// (FinishReadFromRobot then the resolver), and attaching does not queue a further read.
    /// </summary>
    [Fact]
    public void M3_033_TheEngineQueuesOneNeedsReadAndALateNeedsManagerAdoptsTheBufferedResult()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        SendFirstFullState(rig);

        var nv = rig.Robot.Engine.NvStorage!;
        int needsReads = 0;
        int i = 0;
        while (!nv.IsIdle && i < 100)
        {
            var cmd = NvCommands(rig)[^1];
            bool isNeeds = cmd.Tag == Cozmo.Robot.Behavior.NeedsManager.NeedsNvKey;
            if (isNeeds) needsReads++;
            byte[] data = isNeeds ? NeedsV5() : Array.Empty<byte>();
            rig.Data(new NVOpResult { Tag = cmd.Tag, Op = 0, Result = data.Length > 0 ? (sbyte)0 : (sbyte)-1, Length = 0, Data = data });
            rig.Tick();
            i++;
        }
        // #9 the RDBM backup read and #15 the mfgId Needs read share the tag; no third read exists.
        Assert.Equal(2, needsReads);
        Assert.NotNull(rig.Engine.ConnectionNeedsResult);       // buffered with no NeedsManager attached

        var needs = new Cozmo.Robot.Behavior.NeedsManager(() => 0) { NvStorage = rig.Robot.Engine.NvStorage };
        needs.AttachConnectionRead(rig.Robot.Engine);
        rig.Robot.Engine.SerialNumberAcquired += needs.InitAfterSerialNumberAcquired;
        try
        {
            // the serial edge replays the already-known serial; the manager adopts the buffered read
            rig.Robot.Engine.RaiseSerialNumberAcquired(0xABCD);
            Assert.Equal(0xABCDu, needs.SerialNumber);
            Assert.True(needs.RobotReadSucceeded);
            Assert.True(needs.HasRobotCopy);

            // the adoption did not queue a further read (or write) of the tag
            Assert.Equal(2, NvCommands(rig).Count(c => c.Tag == Cozmo.Robot.Behavior.NeedsManager.NeedsNvKey));
        }
        finally { rig.Robot.Engine.SerialNumberAcquired -= needs.InitAfterSerialNumberAcquired; needs.DetachConnectionRead(); }
    }

    /// <summary>
    /// M3-033/M15-014: the engine Needs read's terminal result reaches the NeedsManager's OnRobotRead semantics even
    /// when it is a failure. A missing item (-1) makes FinishReadFromRobot return false (RobotReadSucceeded false)
    /// and the resolver still runs (InitAfterReadFromRobotAttempt always does; the read is no longer outstanding).
    /// </summary>
    [Fact]
    public void M3_033_ANegativeNeedsReadStillResolvesThroughTheEnginePath()
    {
        using var rig = new Rig();
        var needs = new Cozmo.Robot.Behavior.NeedsManager(() => 0) { NvStorage = rig.Robot.Engine.NvStorage };
        needs.AttachConnectionRead(rig.Robot.Engine);
        rig.Robot.Engine.SerialNumberAcquired += needs.InitAfterSerialNumberAcquired;
        try
        {
            rig.ToSuccess();
            SendFirstFullState(rig);
            var nv = rig.Robot.Engine.NvStorage!;
            int i = 0;
            while (!nv.IsIdle && i < 100)
            {
                var cmd = NvCommands(rig)[^1];
                // every read, the Needs read included, reports a missing item (-1)
                rig.Data(new NVOpResult { Tag = cmd.Tag, Op = 0, Result = -1, Length = 0, Data = Array.Empty<byte>() });
                rig.Tick();
                i++;
            }
            Assert.False(needs.RobotReadSucceeded);
            Assert.False(needs.AwaitingRobotData);          // the callback cleared +0x3d0 and the resolver ran
        }
        finally { rig.Robot.Engine.SerialNumberAcquired -= needs.InitAfterSerialNumberAcquired; needs.DetachConnectionRead(); }
    }

    /// <summary>
    /// M3-022/M3-033: the wait helper returns the calibration the engine's single connection read installed
    /// (0x006583FA queues it; the 0x0065AB68 callback installs it), driven through Camera.OnRobotConnected.
    /// </summary>
    [Fact]
    public async Task M3_022_TheConnectionCalibrationWaitReturnsTheEngineRead()
    {
        using var rig = new Rig();
        using var vision = new Cozmo.Robot.Vision.VisionSystem(rig.Robot) { Enabled = false };
        rig.ToSuccess();
        var wait = vision.WaitForConnectionCalibrationAsync(TimeSpan.FromSeconds(1));
        SendConnectionReadsUntilCalibration(rig);
        rig.Data(new NVOpResult { Tag = 0x80000001, Op = 0, Result = 0, Length = 0, Data = Calibration56() });
        rig.Tick();
        var cal = await wait;
        Assert.NotNull(cal);
        Assert.Same(rig.Robot.CameraSettings.Calibration, cal);
    }

    /// <summary>M3-022: a connection calibration read that never completes makes the wait return null (the removed read's contract).</summary>
    [Fact]
    public async Task M3_022_TheConnectionCalibrationWaitReturnsNullWhenTheReadNeverCompletes()
    {
        using var rig = new Rig();
        using var vision = new Cozmo.Robot.Vision.VisionSystem(rig.Robot) { Enabled = false };
        var cal = await vision.WaitForConnectionCalibrationAsync(TimeSpan.FromMilliseconds(50));
        Assert.Null(cal);
    }

    /// <summary>
    /// M3-022/1j: the callback logs "…Recvd" with the received distortion (0x0065ACE8) before the body
    /// hardware version's &lt;= 6 check logs "IgnoringDistCoeffs" and zeroes the coefficients (0x0065AD5A..0x0065AD9C).
    /// </summary>
    [Fact]
    public void M3_022_1j_TheRecvdLogCarriesTheReceivedDistortionBeforeIgnoring()
    {
        using var rig = new Rig();
        var logs = new List<string>();
        rig.Robot.CameraSettings.Log += logs.Add;
        rig.ToSuccess(bodyHw: 4);
        SendConnectionReadsUntilCalibration(rig);
        rig.Data(new NVOpResult { Tag = 0x80000001, Op = 0, Result = 0, Length = 0, Data = Calibration56() });
        rig.Tick();
        int recvd = logs.FindIndex(l => l.Contains("ReadCameraCalibration.Recvd"));
        int ignoring = logs.FindIndex(l => l.Contains("IgnoringDistCoeffs"));
        Assert.True(recvd >= 0, "no Recvd line");
        Assert.True(ignoring >= 0, "no IgnoringDistCoeffs line");
        Assert.True(recvd < ignoring, $"Recvd ({recvd}) must precede IgnoringDistCoeffs ({ignoring})");
        Assert.Contains("0.01", logs[recvd]);                   // the received distortion, before the <=6 zeroing
    }

    private static DefaultCameraParams Defaults(float maxGain, float gain, ushort min, ushort max) => new()
    {
        Field0 = maxGain, Field1 = gain, Field2 = min, Field3 = max,
        Field4 = Enumerable.Range(0, 17).Select(i => (byte)(i * 10)).ToArray(),
    };

    private static List<SetCameraParams> CameraParamsAfter(Rig rig, int from)
        => rig.Port.Messages().Skip(from).OfType<SetCameraParams>().ToList();

    /// <summary>
    /// 1k, A19, A22 (M3-021): DefaultCameraParams, with no time-sync gate, and 16 in [min, max]: SetCameraSettings(16,
    /// gain) first, sending {gain, 16, false}; then the limits max, min, 0.1, maxGain and the gamma table installed.
    /// </summary>
    [Fact]
    public void M3_021_1k_DefaultCameraParamsSetsTheInitialExposureThenInstallsTheLimits()
    {
        using var rig = new Rig();
        rig.ToSuccess();                                          // no SyncTimeAck: the handler is not gated
        int before = rig.Port.Messages().Count;
        rig.Data(Defaults(3.0f, 1.5f, 1, 50));
        rig.Tick();
        var sent = Assert.Single(CameraParamsAfter(rig, before));
        Assert.Equal((1.5f, (ushort)16, false), (sent.Gain, sent.ExposureMs, sent.AutoExposureEnabled));
        var s = rig.Robot.CameraSettings;
        Assert.Equal((50, 1, 0.1f, 3.0f), (s.MaxExposureMs, s.MinExposureMs, s.MinGain, s.MaxGain));
        Assert.Equal(17, s.GammaTable.Length);
        Assert.Equal((16, 1.5f), s.NextCameraParams!.Value);
    }

    /// <summary>A19/1k: 16 outside [msg+8, msg+0xA] is BadInitialExposureTime: nothing sent and nothing installed.</summary>
    [Fact]
    public void M3_021_1k_AnInitialExposureOutsideTheRangeDoesNothing()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        int before = rig.Port.Messages().Count;
        rig.Data(Defaults(3.0f, 1.5f, 20, 50));
        rig.Tick();
        Assert.Empty(CameraParamsAfter(rig, before));
        var s = rig.Robot.CameraSettings;
        Assert.Equal((66, 1, 0.1f, 4.0f), (s.MaxExposureMs, s.MinExposureMs, s.MinGain, s.MaxGain));
    }

    /// <summary>
    /// 1e, 1k, 1l (M3-021): the first SetCameraSettings is checked against the constructor's limits (gain 0.1..4.0), so a
    /// gain of 5.0 sends nothing, but the robot's limits (maxGain 8.0) are still installed. A min of 0 is stored as 1 (1c).
    /// </summary>
    [Fact]
    public void M3_021_1l_TheFirstSettingsAreCheckedAgainstTheConstructorLimits()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        int before = rig.Port.Messages().Count;
        rig.Data(Defaults(8.0f, 5.0f, 0, 60));
        rig.Tick();
        Assert.Empty(CameraParamsAfter(rig, before));
        var s = rig.Robot.CameraSettings;
        Assert.Equal((60, 1, 8.0f), (s.MaxExposureMs, s.MinExposureMs, s.MaxGain));
    }

    /// <summary>1a, 1l, A20 (M3-021): exposure valid when min &lt;= e &lt;= max (1..66), gain when 0.1 &lt;= g &lt;= 4.0, NaN invalid.</summary>
    [Fact]
    public void M3_021_1l_SetCameraSettingsRangeChecks()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        var s = rig.Robot.CameraSettings;
        Assert.False(s.SetCameraSettings(67, 2.0f));
        Assert.False(s.SetCameraSettings(0, 2.0f));
        Assert.False(s.SetCameraSettings(16, 4.01f));
        Assert.False(s.SetCameraSettings(16, 0.09f));
        Assert.False(s.SetCameraSettings(16, float.NaN));
        int before = rig.Port.Messages().Count;
        CurrentCameraParams? info = null;
        s.CurrentCameraParamsChanged += p => info = p;
        Assert.True(s.SetCameraSettings(66, 4.0f));
        Assert.True(s.SetCameraSettings(1, 0.1f));
        Assert.Equal(2, CameraParamsAfter(rig, before).Count);
        Assert.Equal(new CurrentCameraParams(0.1f, 1, true), info);      // auto-exposure +0x329 starts 1 (1n)
    }

    /// <summary>A24, 3a (M3-023): EnableColorImages stores the flag and sends EnableColorImages {b}.</summary>
    [Fact]
    public void M3_023_A24_EnableColorImagesStoresAndSends()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        Assert.False(rig.Robot.CameraSettings.ColorImagesEnabled);
        rig.Robot.CameraSettings.EnableColorImages(true);
        Assert.True(rig.Robot.CameraSettings.ColorImagesEnabled);
        Assert.True(Assert.IsType<EnableColorImages>(rig.Port.Messages()[^1]).Enable);
    }

    /// <summary>C1 (M3-024): "sim" null in the firmware JSON: physical robot, output source 2 (PlayOnRobot); otherwise 1.</summary>
    [Theory]
    [InlineData(Rig.ShippedFw, RobotAudioOutputSource.PlayOnRobot, true)]
    [InlineData("{\"version\": 2381, \"time\": 1546972025, \"sim\": null}", RobotAudioOutputSource.PlayOnRobot, true)]
    [InlineData("{\"version\": 2381, \"time\": 1546972025, \"sim\": true}", RobotAudioOutputSource.PlayOnDevice, false)]
    public void M3_024_C1_TheAudioOutputSourceComesFromSim(string fw, RobotAudioOutputSource source, bool physical)
    {
        using var rig = new Rig();
        rig.ToSuccess(fw);
        Assert.Equal(source, rig.Engine.Robot!.AudioOutputSource);
        Assert.Equal(physical, rig.Engine.Robot.IsPhysicalRobot);
    }

    /// <summary>C10 (M3-012): the played counters are written only by the AnimationState handler, and only once time synced.</summary>
    [Fact]
    public void M3_012_C10_ThePlayedCountersComeOnlyFromATimeSyncedAnimationState()
    {
        using var rig = new Rig();
        rig.ToSuccess();
        rig.Data(new AnimationState { NumAnimBytesPlayed = 500, NumAudioFramesPlayed = 5, Tag = 3 });
        rig.Tick();
        Assert.Equal((0, 0), (rig.Engine.Robot!.NumAnimBytesPlayed, rig.Engine.Robot.NumAudioFramesPlayed));
        rig.Data(new SyncTimeAck());
        rig.Data(new AnimationState { NumAnimBytesPlayed = 500, NumAudioFramesPlayed = 5, Tag = 3 });
        rig.Tick();
        Assert.Equal((500, 5), (rig.Engine.Robot.NumAnimBytesPlayed, rig.Engine.Robot.NumAudioFramesPlayed));
    }

    /// <summary>
    /// CD12, C15 (M3-013): Robot::Update runs the streamer only while synced and ready to stream, once per tick.
    /// M3-026: the connection-time calibration read only goes out after the first synced full state, and ready to
    /// stream waits for the queue to drain (CD20).
    /// </summary>
    [Fact]
    public void M3_013_CD12_TheEngineTickRunsTheStreamerOnlyWhileStreamingIsOpen()
    {
        using var rig = new Rig();
        int updates = 0;
        rig.Engine.AnimationStreamerUpdate = () => updates++;
        rig.ToSuccess();
        rig.Tick();
        Assert.Equal(0, updates);                                  // before the first full state, no streamer and no NV send

        // M3-033: Gate A sends the first connection-time read; ready to stream waits for the whole queue.
        SendFirstFullState(rig);
        Assert.Equal(0, updates);
        var nv = rig.Robot.Engine.NvStorage!;
        while (nv.QueuedTags.Count > 0) AnswerInFlight(rig);       // drain all but the last read
        Assert.False(rig.Engine.Robot!.ReadyToStream);             // the last read is still in flight
        Assert.Equal(0, updates);

        AnswerInFlight(rig);                                       // the last read completes; the state-0 path opens ready
        Assert.True(rig.Engine.Robot!.ReadyToStream);
        Assert.Equal(0, updates);                                  // the streamer gate was computed before NVStorage::Update
        rig.Tick();                                                // the next Update opens streaming and runs the streamer
        Assert.Equal(1, updates);
        rig.Tick();
        Assert.Equal(2, updates);
    }

    /// <summary>
    /// CD12, C15, A20 (M3-013, M3-014), the production wiring: the engine's own 60 ms thread (StartProduction) runs
    /// Robot::Update, which runs the streamer (AnimationStreamerUpdate → CozmoAnimations.EngineUpdate →
    /// AnimationScheduler.Advance) while streaming is open; no 30 Hz tick loop thread is started. A clip streams
    /// within the budget the robot's AnimationState reports, completes, and ends with EndOfAnimation, with nothing
    /// after it.
    /// </summary>
    [Fact]
    public void M3_013_CD12_OnAProductionEngineTheEngineTickStreamsAClipToItsEnd()
    {
        var port = new FakePort();
        using var robot = CozmoRobot.CreateForTest(port, EngineTickRunner.HostNowNs, new CozmoEngineOptions { BlockPoolPath = "" });
        using var ticks = new TickSignal(robot.Engine);
        robot.Engine.StartProduction();
        Assert.True(robot.Animations.EngineDriven);
        void Data(RobotMessage m) => port.Raise(ReceiverMarker.Data, Rig.RobotEp, m.ToBytes());
        bool Sent(Func<RobotMessage, bool> p) => port.Messages().Any(p);

        robot.Engine.ConnectToRobot(Rig.RobotIp);
        ticks.Until(() => robot.Engine.ConnectionState == 1);
        Assert.True(robot.Engine.ConnectionState == 1, "no connect");
        port.Raise(ReceiverMarker.OnConnected, Rig.RobotEp);
        ticks.Until(() => robot.Engine.ConnectionState == 2);
        Assert.True(robot.Engine.ConnectionState == 2, "not connected");
        Data(new RobotAvailable { SerialNumberHead = 0x1234, HwVersion = 5 });
        Data(new FirmwareVersion { RobotId = 1, Signature = Encoding.UTF8.GetBytes(Rig.ShippedFw) });
        ticks.Until(() => Sent(m => m is GetManufacturingInfo));
        Assert.True(Sent(m => m is GetManufacturingInfo), "no GetManufacturingInfo");
        Data(new ManufacturingID { SerialNumber = 0xABCD, BodyHwVersion = 7, BodyColor = 2 });
        ticks.Until(() => Sent(m => m is SyncTime));
        Assert.True(Sent(m => m is SyncTime), "no SyncTime");
        // M3-026/M3-032: the connection reads are queued at Success but only go out after Gate A, so establish the
        // first synced full state and answer each read, in the engine's order, until the queue drains and ready to
        // stream opens (CD20). M3-033: 12 constructor reads, then CameraCalib, Lab and (with no NeedsManager here) no
        // Needs read.
        Data(new SyncTimeAck());
        Data(new RobotState { Timestamp = 10, PoseOriginId = 1 });
        int answered = 0;
        while (!robot.AnimationStreamingOpen)
        {
            ticks.Until(() => robot.AnimationStreamingOpen || port.Messages().OfType<NVCommand>().Count() > answered);
            if (robot.AnimationStreamingOpen) break;
            var cmd = port.Messages().OfType<NVCommand>().ElementAt(answered);
            answered++;
            bool calibration = cmd.Tag == 0x80000001;
            Data(new NVOpResult
            {
                Tag = cmd.Tag, Op = 0, Result = calibration ? (sbyte)0 : (sbyte)-1, Length = 0,
                Data = calibration ? Calibration56() : Array.Empty<byte>(),
            });
        }
        Assert.True(robot.AnimationStreamingOpen, "streaming never opened");

        int before = port.Messages().Count;
        var clip = new AnimationClip
        {
            Name = "head",
            Keyframes = new List<Keyframe> { new HeadKeyframe(0, 100, 10, 0) },
            Tracks = AnimationTrack.Head,
            DurationMs = 300,
        };
        var done = robot.Animations.Play(clip)!;
        uint ts = 20;
        while (!done.IsCompleted)
        {
            // the robot plays everything it was given: its AnimationState reports what the stream sent
            var sent = port.Messages().Skip(before).ToList();
            int frames = sent.Count(m => m is AudioSample or AudioSilence or EndOfAnimation);
            int bytes = sent.Where(m => (byte)m.Id >= 0x8E && (byte)m.Id <= 0x9B).Sum(m => m.ToBytes().Length);
            Data(new AnimationState { Timestamp = ts++, NumAudioFramesPlayed = frames, NumAnimBytesPlayed = bytes, Tag = 1 });
            ticks.Next();
        }
        Assert.True(done.IsCompleted, "the clip never completed");
        Assert.Equal(AnimationEndReason.Completed, done.Result);

        var after = port.Messages().Skip(before).ToList();
        int start = after.FindIndex(m => m is StartOfAnimation s && s.AnimId == 1);
        int end = after.FindIndex(m => m is EndOfAnimation);
        Assert.True(start >= 0 && end > start, $"start {start}, end {end}");
        Assert.Contains(after.Take(end), m => m is Protocol.HeadAngle);
        Assert.Equal(1, after.Count(m => m is EndOfAnimation));
        Assert.DoesNotContain(after.Skip(end + 1), m => m is AudioSample or AudioSilence);    // A20: nothing after the end

        var ticker = typeof(CozmoAnimations).GetField("_ticker", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        Assert.Null(ticker.GetValue(robot.Animations));                                        // no 30 Hz tick loop
    }
}
