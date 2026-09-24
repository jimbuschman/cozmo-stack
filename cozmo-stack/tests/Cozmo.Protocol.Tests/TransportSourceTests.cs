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
    /// AbsoluteLocalizationUpdate's last word is the heading, not an unknown integer.
    /// Robot::SendAbsLocalizationUpdate 0x00512734 packs the message in order: its two unsigned
    /// arguments (the timestamp and the pose frame id, strd at 0x0051279E), the pose's parent id from
    /// PoseBase::GetID, the transform's x and y at +0x20 and +0x24, and Rotation3d::GetAngleAroundZaxis
    /// (0x00512796). Its no-argument twin 0x00514710 supplies the timestamp from the latest vision-only
    /// state, which is what fixes the order of the first two.
    /// </summary>
    [Fact]
    public void AbsoluteLocalizationUpdateEndsWithTheHeadingInRadians()
    {
        var b = new AbsoluteLocalizationUpdate { Timestamp = 7, PoseFrameId = 3, PoseOriginId = 1, PoseX = 100.5f, PoseY = -20.25f, PoseAngleRad = 1.5f }.ToBytes();
        Assert.Equal(25, b.Length);                                 // tag + 24
        Assert.Equal(7u, BitConverter.ToUInt32(b, 1));
        Assert.Equal(3u, BitConverter.ToUInt32(b, 5));
        Assert.Equal(1u, BitConverter.ToUInt32(b, 9));
        Assert.Equal(100.5f, BitConverter.ToSingle(b, 13));
        Assert.Equal(-20.25f, BitConverter.ToSingle(b, 17));
        Assert.Equal(1.5f, BitConverter.ToSingle(b, 21));
        // and pycozmo's 0x80000000 in that last word is -0.0 radians
        Assert.Equal(0x80000000u, BitConverter.ToUInt32(BitConverter.GetBytes(-0.0f), 0));
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
    /// M1-011, R31 ReliableConnection::ReceivePing 0x00835C7C..0x00835D30: only a ping with isReply set
    /// measures a round trip. This case runs with SendSeparatePingMessages on, which is the library default
    /// and NOT the engine's configuration (RobotConnectionManager::Init stores 0 at 0x0062F06E, tunables
    /// table); the engine configuration is <see cref="TheEngineConfigurationNeverAnswersAPingRequest"/>.
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

        // a ping carrying our timestamp with isReply clear. Whether the robot ever sends one like this is
        // not established by the package (M1-033, HARDWARE_ONLY); here it is only an input.
        clock.NowMs = 150;
        c.ReceivePing(new PingPayload(100, 1, 0, false).ToBytes());
        Assert.Equal(0, c.PingRepliesSeen);
        Assert.True(double.IsNaN(c.LastPingRoundTripMs));            // R31: isReply clear measures nothing
        // R31: a request is answered only when SendSeparatePingMessages is set, as it is in this case
        Assert.True(pingsOut > sentBefore, "with SendSeparatePingMessages set, an unmarked ping is answered");

        // a real reply is what counts
        clock.NowMs = 200;
        c.ReceivePing(new PingPayload(150, 2, 1, true).ToBytes());
        Assert.Equal(1, c.PingRepliesSeen);
        Assert.Equal(50, c.LastPingRoundTripMs);
    }

    /// <summary>
    /// M1-011 in the engine's configuration, through the production receive path (a type-11 frame into
    /// ReliableTransport, HandleSubMessage 11 = ReceivePing, R12 table 0x0083744A).
    /// R31 ReceivePing 0x00835C7C..0x00835D30:
    /// under 17 bytes is ignored; otherwise numPingsReceived++; the peer's counters are kept only if the
    /// incoming numPingsSent is greater; a reply records now - timeSent, negative included; a request is
    /// answered only if sSendSeparatePingMessages, which RobotConnectionManager::Init sets to 0
    /// (0x0062F06E, tunables table), so in the engine a request is never answered.
    /// The ping payloads are written out by hand from R30's layout.
    /// </summary>
    [Fact]
    public void TheEngineConfigurationNeverAnswersAPingRequest()
    {
        var clk = new ManualClock { NowMs = 1000 };
        var t = ReliableTransport.CreateOffline(TransportOptions.EngineDefaults, clk);
        Assert.False(t.Options.SendSeparatePingMessages);
        t.OfflineConnect();
        t.ProcessIncoming(FrameCodec.Encode(Frame.Single(
            new SubMessage(ReliableMessageType.ConnectionResponse, Array.Empty<byte>(), 1), 1)));
        Assert.Equal(LinkState.Connected, t.State);
        t.OfflineOutbound.Clear();
        var c = t.Connection!;

        static byte[] Ping(double time, uint sent, uint received, byte isReply)
        {
            var b = new byte[17];
            BitConverter.GetBytes(time).CopyTo(b, 0);
            BitConverter.GetBytes(sent).CopyTo(b, 8);
            BitConverter.GetBytes(received).CopyTo(b, 12);
            b[16] = isReply;
            return b;
        }
        void Feed(byte[] payload) => t.ProcessIncoming(FrameCodec.Encode(Frame.Single(
            new SubMessage(ReliableMessageType.Ping, payload), 0)));

        clk.NowMs = 1010;

        // under 17 bytes: ignored, nothing counted
        Feed(Ping(900, 9, 9, 0)[..16]);
        Assert.Equal(0u, c.NumPingsReceived);

        // a request (isReply 0): counted, the peer's counters taken, NOT answered, nothing measured
        Feed(Ping(900, 5, 3, 0));
        Assert.Equal(1u, c.NumPingsReceived);
        Assert.Equal(5u, c.NumPingsSentTowardsUs);
        Assert.Equal(3u, c.NumPingsSentThatArrived);
        Assert.Empty(t.OfflineOutbound);
        Assert.Equal(0, c.PendingCount);
        Assert.Equal(0, c.PingRepliesSeen);
        Assert.True(double.IsNaN(c.LastPingRoundTripMs));

        // numPingsSent not greater (4 after 5): counted, the peer's counters kept as they were
        Feed(Ping(900, 4, 9, 0));
        Assert.Equal(2u, c.NumPingsReceived);
        Assert.Equal(5u, c.NumPingsSentTowardsUs);
        Assert.Equal(3u, c.NumPingsSentThatArrived);
        Assert.Empty(t.OfflineOutbound);

        // a reply stamped 7 ms in the future: the round trip is now - timeSent = -7, recorded as it is
        Feed(Ping(1017, 6, 4, 1));
        Assert.Equal(3u, c.NumPingsReceived);
        Assert.Equal(1, c.PingRepliesSeen);
        Assert.Equal(-7.0, c.LastPingRoundTripMs);
        Assert.Empty(t.OfflineOutbound);
    }
}
