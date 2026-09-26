using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// The M6-006 control path: the Action layout (gapA 1.7/1.9), the scope rule (gapA 1.4), the frame and
/// sub-frame delay split (gapA 1.5/1.6, gapD D1.4), FIFO pending order, and PostEvent's missing-event
/// return (gapA 1.2). The synthetic banks pin the recovered layout; expected numbers are derived from the
/// rows (48,000 Hz, 1,024 samples per frame), not from the implementation.
/// </summary>
public class WwiseEventRuntimeTests
{
    // ------------------------------------------------------------------ builders

    private static void U32(List<byte> b, uint v) => b.AddRange(BitConverter.GetBytes(v));
    private static void U16(List<byte> b, ushort v) => b.AddRange(BitConverter.GetBytes(v));
    private static void F32(List<byte> b, float v) => b.AddRange(BitConverter.GetBytes(v));

    private static byte[] Chunk(string tag, byte[] body)
    {
        var b = new List<byte>();
        b.AddRange(System.Text.Encoding.ASCII.GetBytes(tag));
        U32(b, (uint)body.Length);
        b.AddRange(body);
        return b.ToArray();
    }

    private static byte[] File(uint bankId, params byte[][] chunks)
    {
        var bkhd = new List<byte>();
        U32(bkhd, 120);            // version
        U32(bkhd, bankId);
        U32(bkhd, 0);
        U32(bkhd, 0);              // feedback flag clear
        U32(bkhd, 0);
        var file = new List<byte>();
        file.AddRange(Chunk("BKHD", bkhd.ToArray()));
        foreach (var c in chunks) file.AddRange(c);
        return file.ToArray();
    }

    private static byte[] Hirc(params (byte Type, byte[] Payload)[] objects)
    {
        var h = new List<byte>();
        U32(h, (uint)objects.Length);
        foreach (var (type, payload) in objects)
        {
            h.Add(type);
            U32(h, (uint)payload.Length);
            h.AddRange(payload);
        }
        return Chunk("HIRC", h.ToArray());
    }

    /// <summary>The 28-byte empty node block the shipped reader consumes.</summary>
    private static void NodeBlock(List<byte> b, uint parent)
    {
        b.Add(0); b.Add(0); b.Add(0);   // fx byte, no fx, overrideAttachment
        U32(b, 0); U32(b, parent);      // bus, parent
        b.Add(0);                       // bits
        b.Add(0);                       // property count
        b.Add(0);                       // ranged count
        b.Add(0);                       // positioning
        b.Add(0);                       // aux
        b.AddRange(new byte[6]);        // advanced settings
        U32(b, 0);                      // state-group count
        U16(b, 0);                      // RTPC count
    }

    private static byte[] SoundPayload(uint id, uint parent = 0)
    {
        var b = new List<byte>();
        U32(b, id);
        U32(b, 0x00040001);   // codec plug-in (low nibble 1), so no source parameter block
        b.Add(0);             // stream type
        U32(b, 12345);        // media id
        U32(b, 0);            // in-memory size
        b.Add(0);             // source bits
        NodeBlock(b, parent);
        return b.ToArray();
    }

    private static byte[] EventPayload(uint id, params uint[] actions)
    {
        var b = new List<byte>();
        U32(b, id);
        U32(b, (uint)actions.Length);
        foreach (var a in actions) U32(b, a);
        return b.ToArray();
    }

    /// <summary>The common Action layout (gapA 1.7) followed by the caller's type-specific bytes.</summary>
    private static byte[] ActionPayload(uint id, ushort type, uint target, bool isBus,
                                        (byte Id, uint Value)[] props,
                                        (byte Id, float Min, float Max)[] ranged,
                                        byte[] typeParams)
    {
        var b = new List<byte>();
        U32(b, id);
        U16(b, type);
        U32(b, target);
        b.Add(isBus ? (byte)1 : (byte)0);
        b.Add((byte)props.Length);
        foreach (var (pid, _) in props) b.Add(pid);
        foreach (var (_, value) in props) U32(b, value);
        b.Add((byte)ranged.Length);
        foreach (var (rid, _, _) in ranged) b.Add(rid);
        foreach (var (_, min, max) in ranged) { F32(b, min); F32(b, max); }
        b.AddRange(typeParams);
        return b.ToArray();
    }

    private static byte[] PlayParams(byte curve, uint bankId)
    {
        var b = new List<byte> { curve };
        U32(b, bankId);
        return b.ToArray();
    }

    private static byte[] StopParams(byte curve, params (uint Id, bool IsBus)[] exceptions)
    {
        var b = new List<byte> { curve };
        U32(b, (uint)exceptions.Length);
        foreach (var (id, isBus) in exceptions) { U32(b, id); b.Add(isBus ? (byte)1 : (byte)0); }
        return b.ToArray();
    }

    private static byte[] SeekParams(byte relative, float value, float min, float max, byte snap)
    {
        var b = new List<byte>();
        b.Add(relative); F32(b, value); F32(b, min); F32(b, max); b.Add(snap);
        U32(b, 0);                    // no exceptions
        return b.ToArray();
    }

    private static float Bits(uint v) => BitConverter.Int32BitsToSingle((int)v);

    // ------------------------------------------------------------------ action layout

    [Fact]
    public void TheActionLayoutParsesPlayStopAndSeekInBankOrder()
    {
        const uint target = 500;
        var bank = WwiseBank.Parse(File(1,
            Hirc(
                (2, SoundPayload(target)),
                (3, ActionPayload(100, 0x0403, target, false,
                    new (byte, uint)[] { (0x0F, 100), (0x10, BitConverter.SingleToUInt32Bits(250f)) },
                    new (byte, float, float)[] { (0x0F, 10f, 20f) },
                    PlayParams(4, 0xABCDEF01))),
                (3, ActionPayload(101, 0x0103, target, false,
                    Array.Empty<(byte, uint)>(), Array.Empty<(byte, float, float)>(),
                    StopParams(6, (0x11, false), (0x22, true)))),
                (3, ActionPayload(102, 0x1E03, target, true,
                    Array.Empty<(byte, uint)>(), Array.Empty<(byte, float, float)>(),
                    SeekParams(1, 0.5f, -0.67f, 0.76f, 0))))),
            "t.bnk");

        var play = WwiseAction.TryRead(bank.Objects[100], out var playProblem);
        Assert.Null(playProblem);
        Assert.NotNull(play);
        Assert.Equal(100u, play!.Id);
        Assert.Equal((ushort)0x0403, play.Type);
        Assert.Equal(target, play.TargetId);
        Assert.False(play.IsBus);
        Assert.Equal(0x04, play.Kind);
        Assert.True(play.ObjectScope);
        Assert.Equal(100u, play.Props[0x0F]);
        Assert.Equal(4800, play.DelaySamples);          // 100 ms · 48000/1000
        Assert.Equal(250f, play.FloatProp(0x10));
        Assert.Equal((10f, 20f), play.RangedProps[0x0F]);
        Assert.Equal((480L, 960L), play.RangedDelaySamples);
        var pp = Assert.IsType<WwisePlayParams>(play.Params);
        Assert.Equal(4, pp.FadeCurve);
        Assert.Equal(0xABCDEF01u, pp.BankId);

        var stop = WwiseAction.TryRead(bank.Objects[101], out var stopProblem);
        Assert.Null(stopProblem);
        Assert.NotNull(stop);
        Assert.True(stop!.ObjectScope);
        var sp = Assert.IsType<WwiseStopParams>(stop.Params);
        Assert.Equal(6, sp.FadeCurve);
        Assert.Equal(new[] { (0x11u, false), (0x22u, true) }, sp.Exceptions);

        var seek = WwiseAction.TryRead(bank.Objects[102], out var seekProblem);
        Assert.Null(seekProblem);
        Assert.NotNull(seek);
        Assert.True(seek!.IsBus);
        Assert.Equal(0x1E, seek.Kind);
        Assert.True(seek.ObjectScope);          // 0x1E03 has bit0 set (gapA 1.8)
        var sk = Assert.IsType<WwiseSeekParams>(seek.Params);
        Assert.Equal(1, sk.Relative);
        Assert.Equal(0.5f, sk.Value);
        Assert.Equal(-0.67f, sk.Min);
        Assert.Equal(0.76f, sk.Max);
        Assert.Equal(0, sk.Snap);
        Assert.Empty(sk.Exceptions);
    }

    // ------------------------------------------------------------------ scope

    [Fact]
    public void AnObjectScopeActionWithNoGameObjectIsSkippedAndAGlobalOneRunsWithNull()
    {
        const uint target = 500;
        // One event: an object-scope Stop 0x0103 and a global-scope Stop 0x0102 (gapA 1.4's own examples).
        var bank = WwiseBank.Parse(File(1,
            Hirc(
                (2, SoundPayload(target)),
                (3, ActionPayload(103, 0x0103, target, false,
                    Array.Empty<(byte, uint)>(), Array.Empty<(byte, float, float)>(), StopParams(4))),
                (3, ActionPayload(104, 0x0102, target, false,
                    Array.Empty<(byte, uint)>(), Array.Empty<(byte, float, float)>(), StopParams(4))),
                (4, EventPayload(900, 103, 104)))),
            "t.bnk");

        var runtime = new WwiseEventRuntime(new[] { bank }, new WwiseRng(1));
        uint playingId = runtime.PostEvent(900, gameObjectId: null);
        Assert.NotEqual(WwiseEventRuntime.InvalidPlayingId, playingId);

        runtime.AdvanceFrame();

        // The object-scope action is skipped; the global-scope action runs with a null game object.
        var skip = Assert.Single(runtime.ActionLog,
            e => e.Outcome == WwiseActionOutcome.SkippedObjectScopeNoGameObject);
        Assert.Equal(103u, skip.ActionId);

        var executed = Assert.Single(runtime.ExecutedActions);
        Assert.Equal(104u, executed.ActionId);
        Assert.Null(executed.GameObjectId);
        Assert.Equal(target, executed.TargetId);
    }

    // ------------------------------------------------------------------ delay and order

    [Fact]
    public void AZeroDelayActionExecutesOnTheFirstFrame()
    {
        const uint target = 500;
        var bank = WwiseBank.Parse(File(1,
            Hirc(
                (2, SoundPayload(target)),
                (3, ActionPayload(100, 0x0403, target, false,
                    Array.Empty<(byte, uint)>(), Array.Empty<(byte, float, float)>(), PlayParams(0, 0))),
                (4, EventPayload(900, 100)))),
            "t.bnk");

        var runtime = new WwiseEventRuntime(new[] { bank }, new WwiseRng(1));
        runtime.RegisterGameObject(7);
        uint playingId = runtime.PostEvent(900, 7);

        // Nothing runs on the caller's thread (gapA 1.2): the event is only queued.
        Assert.NotEqual(WwiseEventRuntime.InvalidPlayingId, playingId);
        Assert.Equal(1, runtime.QueuedEventCount);
        Assert.Empty(runtime.ExecutedActions);

        runtime.AdvanceFrame();

        var executed = Assert.Single(runtime.ExecutedActions);
        Assert.Equal(100u, executed.ActionId);
        Assert.Equal(playingId, executed.PlayingId);
        Assert.Equal(7u, executed.GameObjectId);
        Assert.Equal(target, executed.TargetId);
        Assert.Equal(0, executed.Frames);
        Assert.Equal(0, executed.DelaySamples);
        Assert.Equal(0, executed.LaunchTick);
    }

    [Fact]
    public void ADelayedActionWaitsItsFramesAndEqualTicksStayFifo()
    {
        const uint target = 500;
        // 32 ms · 48000/1000 = 1536 samples = 1 frame + 512 samples remainder.
        const uint delayMs = 32;
        var bank = WwiseBank.Parse(File(1,
            Hirc(
                (2, SoundPayload(target)),
                (3, ActionPayload(100, 0x0403, target, false,
                    new (byte, uint)[] { (0x0F, delayMs) }, Array.Empty<(byte, float, float)>(), PlayParams(0, 0))),
                (3, ActionPayload(101, 0x0403, target, false,
                    new (byte, uint)[] { (0x0F, delayMs) }, Array.Empty<(byte, float, float)>(), PlayParams(0, 0))),
                (4, EventPayload(900, 100, 101)))),
            "t.bnk");

        var runtime = new WwiseEventRuntime(new[] { bank }, new WwiseRng(1));
        runtime.RegisterGameObject(7);
        runtime.PostEvent(900, 7);

        runtime.AdvanceFrame();
        Assert.Equal(2, runtime.PendingActionCount);
        Assert.Empty(runtime.ExecutedActions);

        runtime.AdvanceFrame();
        var executed = runtime.ExecutedActions;
        Assert.Equal(2, executed.Count);
        // Equal launch ticks stay FIFO, so the event's action order is preserved (gapD D1.4).
        Assert.Equal(new uint[] { 100, 101 }, executed.Select(e => e.ActionId));
        Assert.All(executed, e =>
        {
            Assert.Equal(1536, e.DelaySamples);
            Assert.Equal(1, e.Frames);
            Assert.Equal(512, e.SubFrameRemainderSamples);
            Assert.Equal(1, e.LaunchTick);
        });
    }

    [Fact]
    public void TheFrameRemainderIsKeptWhenTheDelayIsNotAWholeNumberOfFrames()
    {
        const uint target = 500;
        // 100 ms · 48000/1000 = 4800 samples = 4 frames (4096) + 704 remainder.
        var bank = WwiseBank.Parse(File(1,
            Hirc(
                (2, SoundPayload(target)),
                (3, ActionPayload(100, 0x0403, target, false,
                    new (byte, uint)[] { (0x0F, 100) }, Array.Empty<(byte, float, float)>(), PlayParams(0, 0))),
                (4, EventPayload(900, 100)))),
            "t.bnk");

        var runtime = new WwiseEventRuntime(new[] { bank }, new WwiseRng(1));
        runtime.RegisterGameObject(7);
        runtime.PostEvent(900, 7);

        for (int frame = 0; frame < 4; frame++)
        {
            runtime.AdvanceFrame();
            Assert.Empty(runtime.ExecutedActions);
        }

        runtime.AdvanceFrame();
        var executed = Assert.Single(runtime.ExecutedActions);
        Assert.Equal(4800, executed.DelaySamples);
        Assert.Equal(4, executed.Frames);
        Assert.Equal(704, executed.SubFrameRemainderSamples);
        Assert.Equal(4, executed.LaunchTick);
    }

    // ------------------------------------------------------------------ shipped bytes

    /// <summary>
    /// Every shipped action of the three recovered kinds consumes its payload exactly under the layout the
    /// synthetic test pins. This is the source-shaped check that the field order is the bank's, not the
    /// test's: a wrong Seek ordering or a missed exception byte would leave bytes over.
    /// </summary>
    [Fact]
    public void TheShippedActionsOfTheRecoveredKindsConsumeTheirPayloadExactly()
    {
        if (WwiseAssets.Library is not { } lib) return;
        int parsed = 0, refused = 0;
        var problems = new List<string>();
        foreach (var bank in lib.Banks)
            foreach (var o in bank.Objects.Values)
            {
                if (o.Type != WwiseObjectType.EventAction) continue;
                var s = o.Payload.Span;
                if (s.Length < 6) continue;
                ushort type = (ushort)(s[4] | (s[5] << 8));
                if ((byte)(type >> 8) is not (0x01 or 0x04 or 0x1E)) continue;
                if (WwiseAction.TryRead(o, out var problem) is not null) parsed++;
                else { refused++; if (problems.Count < 5) problems.Add(problem!); }
            }

        Assert.True(parsed > 0, "no shipped actions of the recovered kinds parsed");
        Assert.True(refused == 0,
            refused > 0 ? $"{refused} shipped actions refused; first: {problems[0]}" : "");
    }

    // ------------------------------------------------------------------ missing event

    [Fact]
    public void AMissingEventReturnsZeroAndEnqueuesNothing()
    {
        var bank = WwiseBank.Parse(File(1, Hirc((2, SoundPayload(500)))), "t.bnk");
        var runtime = new WwiseEventRuntime(new[] { bank }, new WwiseRng(1));

        uint playingId = runtime.PostEvent(0xDEADBEEF);

        Assert.Equal(WwiseEventRuntime.InvalidPlayingId, playingId);
        Assert.Equal(0, runtime.QueuedEventCount);
        Assert.Equal(0, runtime.PendingActionCount);

        runtime.AdvanceFrame();
        Assert.Empty(runtime.ActionLog);
        Assert.Empty(runtime.ExecutedActions);
    }
}
