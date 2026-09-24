using System.Net;
using Cozmo.Protocol;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// Runtime behaviour of the transport under conditions the happy path never reaches: handlers that throw or
/// block, a socket that fails, repeated connect and disconnect, and session state left over from a previous
/// connection. None of these need a robot.
/// </summary>
public class TransportHardeningTests
{
    private static (ReliableTransport t, ManualClock clk) Offline()
    {
        var clk = new ManualClock { NowMs = 1000 };
        return (ReliableTransport.CreateOffline(TransportOptions.EngineDefaults, clk), clk);
    }

    private static byte[] RobotFrame(ushort seqMin, ushort seqMax, ushort ack, params SubMessage[] msgs) =>
        FrameCodec.Encode(msgs.Length == 1 && !ReliableMessageTypes.IsMultiple(msgs[0].Type)
            ? Frame.Single(msgs[0], ack)
            : new Frame { Type = ReliableMessageType.MultipleMixedMessages, SeqMin = seqMin, SeqMax = seqMax, Ack = ack, Messages = msgs.ToList() });

    private static void Connect(ReliableTransport t)
    {
        t.OfflineConnect();
        t.ProcessIncoming(RobotFrame(1, 1, 1, new SubMessage(ReliableMessageType.ConnectionResponse, Array.Empty<byte>(), 1)));
    }

    // ------------------------------------------------------------- handler isolation

    [Fact]
    public void AHandlerThatThrowsIsCountedAndTheOthersStillRun()
    {
        var (t, _) = Offline();
        var seen = new List<byte[]>();
        t.DataReceived += _ => throw new InvalidOperationException("handler is broken");
        t.DataReceived += seen.Add;
        Connect(t);

        var payload = new GetManufacturingInfo().ToBytes();
        t.ProcessIncoming(RobotFrame(2, 2, 1, new SubMessage(ReliableMessageType.SingleReliableMessage, payload, 2)));

        Assert.True(t.HandlerFaults > 0, "the throwing handler should have been recorded");
        Assert.Equal(LinkState.Connected, t.State);     // and it must not have taken the transport down
    }

    [Fact]
    public void AThrowingHandlerDoesNotStopLaterMessagesBeingDelivered()
    {
        var (t, _) = Offline();
        int delivered = 0;
        t.DataReceived += _ => { delivered++; throw new Exception("every time"); };
        Connect(t);

        for (ushort seq = 2; seq <= 5; seq++)
            t.ProcessIncoming(RobotFrame(seq, seq, 1,
                new SubMessage(ReliableMessageType.SingleReliableMessage, new GetManufacturingInfo().ToBytes(), seq)));

        Assert.Equal(4, delivered);
        Assert.Equal(4, t.HandlerFaults);
    }

    [Fact]
    public void AThrowingWarningHandlerDoesNotRecurseOrEscape()
    {
        var (t, _) = Offline();
        t.Warning += _ => throw new Exception("even the warning handler is broken");
        t.ProcessIncoming(new byte[] { 0x00, 0x01, 0x02 });    // not a valid frame: produces a warning
        Assert.Equal(LinkState.Connecting, t.State);
    }

    // --------------------------------------------------------------- session state

    /// <summary>
    /// T-g1 — PRIMARY-SOURCE ORACLE. PendingMultiPartMessage::AddMessagePart takes a part only when its
    /// index is the one expected; any other part is ignored and the assembly is left as it is
    /// (0x008358A8-AA). Part 1 sets the count (0x008358AC-B2) and the part whose index equals the count
    /// completes the message (0x008358E4). So in 1/3, 3/3, 1/3, 2/3, 3/3 the early 3/3 and the repeated 1/3
    /// are ignored and the original assembly completes.
    /// </summary>
    [Fact]
    public void T_g1_AnOutOfOrderMultipartPartIsIgnoredAndTheAssemblyKept()
    {
        var (t, _) = Offline();
        var delivered = new List<byte[]>();
        t.DataReceived += delivered.Add;
        Connect(t);

        void Part(ushort seq, params byte[] p) =>
            t.ProcessIncoming(RobotFrame(seq, seq, 1, new SubMessage(ReliableMessageType.MultiPartMessage, p, seq)));
        Part(2, 1, 3, 0xA1, 0xA2);          // begins the assembly
        Part(3, 3, 3, 0xEE);                // early: ignored, nothing reset
        Part(4, 1, 3, 0xDD, 0xDD);          // part 1 again while part 2 is expected: ignored
        Assert.Empty(delivered);
        Part(5, 2, 3, 0xB1);
        Part(6, 3, 3, 0xC1, 0xC2);
        Assert.Equal(new byte[] { 0xA1, 0xA2, 0xB1, 0xC1, 0xC2 }, Assert.Single(delivered));
    }

    // ----------------------------------------------------------- lifecycle on a socket

    /// <summary>A port nothing is listening on: enough to exercise the real socket and thread lifecycle.</summary>
    private static readonly IPAddress Nowhere = IPAddress.Loopback;

    [Fact]
    public void ConnectAndDisconnectLeavesNoWorkersAndCanConnectAgain()
    {
        using var t = new ReliableTransport();
        for (int attempt = 0; attempt < 3; attempt++)
        {
            t.Connect(Nowhere, 59999);
            Assert.Equal(LinkState.Connecting, t.State);
            t.Disconnect($"attempt {attempt}");
            Assert.Equal(LinkState.Disconnected, t.State);
        }
        // three full cycles on one instance means shutdown really did release the workers each time
    }

    [Fact]
    public void ConnectingTwiceWithoutDisconnectingIsRefused()
    {
        using var t = new ReliableTransport();
        t.Connect(Nowhere, 59998);
        try
        {
            Assert.Throws<InvalidOperationException>(() => t.Connect(Nowhere, 59998));
        }
        finally { t.Disconnect(); }
    }

    [Fact]
    public void DisconnectReportsItsReasonExactlyOnce()
    {
        using var t = new ReliableTransport();
        var reasons = new List<string>();
        t.Disconnected += r => { lock (reasons) reasons.Add(r); };
        t.Connect(Nowhere, 59997);
        t.Disconnect("because the test said so");
        t.Disconnect("and again");
        t.Dispose();
        lock (reasons) Assert.Equal(new[] { "because the test said so" }, reasons);
    }

    [Fact]
    public void DisposeWithoutConnectingIsHarmless()
    {
        var t = new ReliableTransport();
        var reasons = new List<string>();
        t.Disconnected += reasons.Add;
        t.Dispose();
        t.Dispose();
        Assert.Empty(reasons);          // nothing was ever up, so there is nothing to report
    }

    [Fact]
    public void SendingAfterDisconnectIsRefusedRatherThanIgnored()
    {
        using var t = new ReliableTransport();
        t.Connect(Nowhere, 59996);
        t.Disconnect();
        Assert.Throws<InvalidOperationException>(() => t.Send(new GetManufacturingInfo()));
    }

    [Fact]
    public void ASlowHandlerDoesNotHoldUpTheReceiveThread()
    {
        using var t = new ReliableTransport();
        var released = new ManualResetEventSlim();
        t.Disconnected += _ => released.Wait(TimeSpan.FromSeconds(5));   // a handler that blocks

        t.Connect(Nowhere, 59995);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        t.Disconnect();
        sw.Stop();

        // Disconnect queues the notification rather than running it, so it returns promptly even though the
        // handler is still blocked. The dispatcher join bound is the only cost.
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(4), $"Disconnect took {sw.ElapsedMilliseconds} ms behind a blocked handler");
        released.Set();
    }
}
