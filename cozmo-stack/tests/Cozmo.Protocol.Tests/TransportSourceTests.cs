using Cozmo.Protocol;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

// Three transport facts that had been standing on PyCozmo rather than on the engine
// (fidelity manifest M1-011, M1-012, M2-004).
public class TransportSourceTests
{
    /// <summary>
    /// SyncTime's second word is -20.0f. Robot::SendSyncTime 0x0051524C writes the base-station
    /// timestamp and then 0xC1A00000, which this stack had been sending as zero.
    /// </summary>
    [Fact]
    public void SyncTimeCarriesTheEnginesConstantInItsSecondWord()
    {
        var b = new SyncTime(12345).ToBytes();
        Assert.Equal(9, b.Length);                                  // tag + 8
        Assert.Equal(12345u, BitConverter.ToUInt32(b, 1));
        Assert.Equal(0xC1A00000u, BitConverter.ToUInt32(b, 5));
        Assert.Equal(-20.0f, BitConverter.ToSingle(b, 5));
    }

    /// <summary>
    /// The frame bound is the engine's 1406, from SetMaxNetMessageSize(1420) at 0x0062EFC6 less the
    /// 4-byte prefix and the 10-byte header.
    /// </summary>
    [Fact]
    public void TheFramePayloadBoundIsTheEngines()
    {
        Assert.Equal(1406, new TransportOptions().MaxFramePayloadBytes);
        Assert.Equal(1420, new TransportOptions().MaxFramePayloadBytes + 4 + 10);
    }

    /// <summary>
    /// A ping with isReply clear is answered, not counted. ReliableConnection::ReceivePing 0x00835C70
    /// branches on the byte at payload+0x10 and measures a round trip only when it is set.
    /// </summary>
    [Fact]
    public void OnlyAPingMarkedAsAReplyMeasuresTheRoundTrip()
    {
        var clock = new ManualClock();
        int pingsOut = 0;
        var c = new ReliableConnection(new TransportOptions { SendSeparatePingMessages = true }, clock,
                                       (type, _, _, _) => { if (type == ReliableMessageType.Ping) pingsOut++; });

        clock.NowMs = 100;
        c.SendPing();                                                // ours goes out with isReply clear
        int sentBefore = pingsOut;

        // the robot echoes it back verbatim: same timestamp, isReply still clear
        clock.NowMs = 150;
        c.ReceivePing(new PingPayload(100, 1, 0, false).ToBytes());
        Assert.Equal(0, c.PingRepliesSeen);
        Assert.True(double.IsNaN(c.LastPingRoundTripMs));            // the engine measures nothing here
        Assert.True(pingsOut > sentBefore, "the engine answers an unmarked ping");

        // a real reply is what counts
        clock.NowMs = 200;
        c.ReceivePing(new PingPayload(150, 2, 1, true).ToBytes());
        Assert.Equal(1, c.PingRepliesSeen);
        Assert.Equal(50, c.LastPingRoundTripMs);
    }
}
