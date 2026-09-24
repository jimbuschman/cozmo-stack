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

    /// <summary>
    /// Updated for M1-035 / M1-019: ReliableTransport::Disconnect queues an action (R38, B21, closure
    /// 0x00837FFA) that runs on the RelTransport executor after everything posted before it (R37), so in
    /// async mode the link is down once that action has run, not when Disconnect returns. The test waits
    /// for the executor before it looks.
    /// </summary>
    [Fact]
    public void ConnectAndDisconnectLeavesNoWorkersAndCanConnectAgain()
    {
        using var t = new ReliableTransport();
        for (int attempt = 0; attempt < 3; attempt++)
        {
            t.Connect(Nowhere, 59999);
            Assert.Equal(LinkState.Connecting, t.State);
            t.Disconnect($"attempt {attempt}");
            Assert.True(t.Flush(TimeSpan.FromSeconds(5)), "the posted disconnect never ran");
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

    /// <summary>
    /// REGRESSION ONLY (host structure, M1-014). Dispose from one of the transport's own event handlers runs on
    /// its dispatch thread, which the executor's shutdown joins; Dispose must not wait for the executor there,
    /// or the two wait on each other until the 2 s join bound runs out.
    /// </summary>
    [Fact]
    public void DisposeFromAnEventHandlerDoesNotWaitOutTheJoinBound()
    {
        using var t = new ReliableTransport();
        var done = new ManualResetEventSlim();
        long elapsedMs = -1; int once = 0;
        t.FrameTrace += _ =>
        {
            if (Interlocked.Exchange(ref once, 1) == 1) return;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            t.Dispose();
            elapsedMs = sw.ElapsedMilliseconds;
            done.Set();
        };
        t.Connect(Nowhere, 59994);                          // the ConnectionRequest is traced on the dispatch thread
        Assert.True(done.Wait(TimeSpan.FromSeconds(10)), "the handler never ran");
        Assert.True(elapsedMs < 1000, $"Dispose on the dispatch thread took {elapsedMs} ms");
    }

    /// <summary>
    /// REGRESSION ONLY (host structure, M1-014). A handler raised inline while a sync-mode Connect holds the
    /// transport lock (the ConnectionRequest's FrameTrace) waits for another thread that calls Disconnect and
    /// Connect. That thread must get through at once: the lifecycle lock is not held while the transport lock
    /// is taken or while a handler runs. (Before, the sync Connect held it across both, and the other thread
    /// blocked until the handler gave up.)
    /// </summary>
    [Fact]
    public void AHandlerInsideASyncConnectCanReachTheTransportFromAnotherThread()
    {
        using var t = new ReliableTransport(TransportOptions.EngineDefaults, null, manualPump: true);
        bool otherFinished = false; Exception? otherConnect = null; int once = 0;
        t.FrameTrace += _ =>
        {
            if (Interlocked.Exchange(ref once, 1) == 1) return;
            var other = new Thread(() =>
            {
                t.Disconnect("from another thread");
                try { t.Connect(Nowhere, 59990); } catch (Exception e) { otherConnect = e; }
            });
            other.Start();
            otherFinished = other.Join(TimeSpan.FromSeconds(3));
        };
        t.Connect(Nowhere, 59990);
        Assert.True(otherFinished, "another thread could not reach the transport while a handler ran inside Connect");
        Assert.IsType<InvalidOperationException>(otherConnect);   // "already running": the first link is open
        Assert.True(t.Flush(TimeSpan.FromSeconds(5)));
        Assert.Equal(LinkState.Disconnected, t.State);          // the posted Disconnect then ran (B21)
    }

    /// <summary>
    /// REGRESSION ONLY (host structure, M1-014). Dispose from a handler raised inline under the transport lock
    /// (sync mode) must not wait for the executor, whose dispose closure needs that lock; it returns at once and
    /// the link ends once the handler has returned.
    /// </summary>
    [Fact]
    public void DisposeFromAHandlerRaisedUnderTheTransportLockDoesNotWaitOutTheJoinBound()
    {
        var t = new ReliableTransport(TransportOptions.EngineDefaults, null, manualPump: true);
        long elapsedMs = -1; int once = 0;
        t.FrameTrace += _ =>
        {
            if (Interlocked.Exchange(ref once, 1) == 1) return;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            t.Dispose();
            elapsedMs = sw.ElapsedMilliseconds;
        };
        t.Connect(Nowhere, 59989);
        Assert.True(elapsedMs >= 0 && elapsedMs < 1000, $"Dispose inside the handler took {elapsedMs} ms");
        Assert.True(SpinWait.SpinUntil(() => t.State == LinkState.Disconnected, 5000), "the link never ended");
    }

    /// <summary>
    /// REGRESSION ONLY (host structure, M1-014). Once Dispose has begun, Connect refuses before it opens
    /// anything: it throws and the link state is untouched.
    /// </summary>
    [Fact]
    public void ConnectAfterDisposeHasBegunIsRefusedBeforeAnythingIsOpened()
    {
        var t = new ReliableTransport();
        t.Dispose();
        Assert.Throws<ObjectDisposedException>(() => t.Connect(Nowhere, 59993));
        Assert.Equal(LinkState.Idle, t.State);
        t.Disconnect();                                     // after Dispose: nothing to do
    }

    /// <summary>
    /// REGRESSION ONLY (host structure, M1-014). A post the executor refuses (it has been completed) fails
    /// visibly: Connect throws, closes the socket it opened and releases the link, so the next Connect is
    /// refused the same way rather than as "already running"; a send throws instead of being dropped.
    /// </summary>
    [Fact]
    public void APostTheExecutorRefusesFailsVisiblyAndReleasesTheLink()
    {
        using var t = new ReliableTransport();
        t.Executor.Complete();                              // test seam: the executor accepts nothing more
        Assert.Throws<ObjectDisposedException>(() => t.Connect(Nowhere, 59992));
        Assert.Equal(LinkState.Idle, t.State);
        Assert.Throws<ObjectDisposedException>(() => t.Connect(Nowhere, 59992));
        Assert.Throws<ObjectDisposedException>(() => t.Disconnect());

        using var s = new ReliableTransport();
        s.Connect(Nowhere, 59991);
        Assert.True(s.Flush(TimeSpan.FromSeconds(5)));
        s.Executor.Complete();
        Assert.Throws<ObjectDisposedException>(() => s.SendData(new GetManufacturingInfo().ToBytes()));
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

    /// <summary>
    /// Updated for M1-035: Disconnect is a queued action (R38, B21; R37), so the test waits for it to have run
    /// before it sends; otherwise the refusal would only be the Connecting-state refusal.
    /// </summary>
    [Fact]
    public void SendingAfterDisconnectIsRefusedRatherThanIgnored()
    {
        using var t = new ReliableTransport();
        t.Connect(Nowhere, 59996);
        t.Disconnect();
        Assert.True(t.Flush(TimeSpan.FromSeconds(5)), "the posted disconnect never ran");
        Assert.Equal(LinkState.Disconnected, t.State);
        Assert.Throws<InvalidOperationException>(() => t.Send(new GetManufacturingInfo()));
        Assert.Throws<InvalidOperationException>(() => t.SendData(new GetManufacturingInfo().ToBytes()));
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
        // Updated for M1-035: Disconnect is a queued action (R38, B21; R37). Waiting for it to have run makes
        // the test cover the shutdown itself again: it queues the notification rather than running it, and
        // the dispatcher join bound (2 s) is the only cost although the handler stays blocked for 5 s.
        Assert.True(t.Flush(TimeSpan.FromSeconds(5)), "the posted disconnect never ran");
        sw.Stop();
        Assert.Equal(LinkState.Disconnected, t.State);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(4), $"the disconnect took {sw.ElapsedMilliseconds} ms behind a blocked handler");
        released.Set();
    }
}
