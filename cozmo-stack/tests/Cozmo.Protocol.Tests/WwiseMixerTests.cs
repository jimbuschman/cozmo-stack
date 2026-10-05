using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M6-012 / gapE 2.1..2.7, gapF 3.1, gapG 5.1..5.3: the Wwise mixer. The per-connection gain ramp (linear
/// over one bus frame, first update not ramped), the mono → mono 1.0 and stereo → mono 0.70710677 per
/// channel matrices, the buffer consume/zero-pad order.
///
/// Every expected value below is the row's own arithmetic applied by hand, not a value read back from
/// <see cref="WwiseMixerConnection"/>. The addresses are the rows' citations.
/// </summary>
public class WwiseMixerTests
{
    /// <summary>The panner table entry 0xFFA970: <c>0x3F3504F3</c> is 0.70710677 (gapG 5.3).</summary>
    private const uint StereoToMonoBits = 0x3F3504F3u;

    private static void Close(float expected, float actual, float tolerance = 1e-6f)
    {
        Assert.True(MathF.Abs(expected - actual) <= tolerance,
            $"expected {expected:R}, got {actual:R} (tolerance {tolerance:R})");
    }

    /// <summary>
    /// M6-012 / gapG 5.3 (0x00A209BC, table 0xFFA970): stereo (0x3102) into mono (0x4101) writes
    /// <c>0x3F3504F3</c> to <c>matrix[ch][0]</c> for FL and FR. gapF 3.1: config 0x3102 is 2 channels,
    /// type 1; 0x4101 is 1 channel, type 1.
    /// </summary>
    [Fact]
    public void TheStereoToMonoMatrixIsThePannerTableEntryPerChannel()
    {
        Assert.Equal(StereoToMonoBits, BitConverter.SingleToUInt32Bits(WwiseMixerPan.StereoToMonoGain));
        Close(0.70710677f, WwiseMixerPan.StereoToMonoGain);

        uint stereo = 0x3102, mono = 0x4101;
        Assert.Equal(2, WwiseMixerPan.Channels(stereo));
        Assert.Equal(1, WwiseMixerPan.ConfigType(stereo));
        Assert.Equal(1, WwiseMixerPan.Channels(mono));
        Assert.Equal(1, WwiseMixerPan.ConfigType(mono));
        // gapG 5.3: the row is taken when out mask & 0x737 == 4; 0x4101's mask is 4.
        Assert.Equal(4u, WwiseMixerPan.ChannelMask(mono));
        Assert.Equal(4u, WwiseMixerPan.ChannelMask(mono) & 0x737u);

        var matrix = WwiseMixerPan.MatrixForChannelConfigs(stereo, mono);
        Assert.Equal(new[] { WwiseMixerPan.StereoToMonoGain, WwiseMixerPan.StereoToMonoGain }, matrix);
    }

    /// <summary>
    /// M6-012 / gapE 2.7, gapF 3.1: the same type-1 config with one channel each gives <c>matrix[0][0] = 1.0</c>,
    /// regardless of pan, for both aux and dry.
    /// </summary>
    [Fact]
    public void TheMonoToMonoMatrixIsOne()
    {
        var matrix = WwiseMixerPan.MatrixForChannelConfigs(0x4101, 0x4101);
        Assert.Equal(new[] { 1.0f }, matrix);
    }

    /// <summary>
    /// M6-012 / gapE 2.5 (0x00A4BF5C..0x00A4BFAC): on the voice's first update the start is forced to the
    /// end, so the first bus frame is not ramped; the whole frame carries the target gain.
    /// </summary>
    [Fact]
    public void TheFirstUpdateIsNotRamped()
    {
        var connection = new WwiseMixerConnection(1, 1);
        connection.Refresh(new[] { 1.0f }, 0.5f);

        Assert.True(connection.StartGain == 0.5f);
        Assert.True(connection.EndGain == 0.5f);

        var input = new[] { new[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f } };
        var destination = new[] { new float[8] };
        connection.ConsumeBuffer(input, destination, validFrames: 8, maxFrames: 8);

        // inc == 0 (V2-07, 0xA4669C): the constant-gain path, dst += src * start.
        Assert.Equal(new[] { 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f }, destination[0]);
    }

    /// <summary>
    /// M6-012 / gapE 2.2, 2.3, 2.4 (mixer 0x00A45E9C, kernel 0x00A46668, V2-07): <c>inc = (1/frames) * ((end * 1) - start)</c> and the lane gains
    /// <c>{s, s + i, (i + i) + s, s + 3i}</c>, then the second four plus <c>4i</c> (accumulated per block of 8; here every value is an exact binary fraction, so they equal
    /// <c>start + k * inc</c> by hand). From 0.5 to 1.0 over 8 frames inc = 0.0625 and the gains are 0.5, 0.5625, 0.625, 0.6875, 0.75, 0.8125, 0.875, 0.9375;
    /// the next frame starts at the end.
    /// </summary>
    [Fact]
    public void TheGainRampsLinearlyOverOneBusFrame()
    {
        var connection = new WwiseMixerConnection(1, 1);
        connection.Refresh(new[] { 1.0f }, 0.5f);   // first update: start = end = 0.5
        connection.Refresh(new[] { 1.0f }, 1.0f);   // ramp this frame

        Assert.True(connection.StartGain == 0.5f);
        Assert.True(connection.EndGain == 1.0f);

        var input = new[] { new[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f } };
        var destination = new[] { new float[8] };
        connection.ConsumeBuffer(input, destination, validFrames: 8, maxFrames: 8);

        // inc = (1.0 - 0.5)/8 = 0.0625.
        Assert.Equal(new[] { 0.5f, 0.5625f, 0.625f, 0.6875f, 0.75f, 0.8125f, 0.875f, 0.9375f }, destination[0]);

        // The end of the frame becomes the next frame's start (gapE 2.5).
        connection.Refresh(new[] { 1.0f }, 1.0f);
        Assert.True(connection.StartGain == 1.0f);
    }

    /// <summary>
    /// M6-012 / gapG 5.3: stereo → mono is 0.70710677 per channel, so either input channel alone reaches the
    /// mono output at 0.70710677 (first frame, so no ramp).
    /// </summary>
    [Fact]
    public void TheStereoToMonoMixAppliesPointSevenOhSevenPerChannel()
    {
        var matrix = WwiseMixerPan.MatrixForChannelConfigs(0x3102, 0x4101);

        var leftOnly = new WwiseMixerConnection(2, 1);
        leftOnly.Refresh(matrix, 1.0f);
        var leftInput = new[] { new[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f }, new[] { 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f } };
        var leftDestination = new[] { new float[8] };
        leftOnly.ConsumeBuffer(leftInput, leftDestination, 8, 8);
        Assert.Equal(Enumerable.Repeat(0.70710677f, 8), leftDestination[0]);

        var rightOnly = new WwiseMixerConnection(2, 1);
        rightOnly.Refresh(matrix, 1.0f);
        var rightInput = new[] { new[] { 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f }, new[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f } };
        var rightDestination = new[] { new float[8] };
        rightOnly.ConsumeBuffer(rightInput, rightDestination, 8, 8);
        Assert.Equal(Enumerable.Repeat(0.70710677f, 8), rightDestination[0]);
    }

    /// <summary>
    /// M6-012 / gapE 2.2, 2.3 with gapG 5.3: the ramp and the per-channel matrix combine. Stereo → mono,
    /// gain 0 → 1 over 8 frames, both inputs 1.0, so <c>out[k] = 2 * 0.70710677 * (k/8)</c>:
    /// 0, 0.17677669, 0.35355338, 0.53033008, 0.70710677, 0.88388348, 1.06066017, 1.23743687.
    /// </summary>
    [Fact]
    public void TheMixedRampUsesThePerChannelMatrixOverTheFrame()
    {
        var matrix = WwiseMixerPan.MatrixForChannelConfigs(0x3102, 0x4101);
        var connection = new WwiseMixerConnection(2, 1);
        connection.Refresh(matrix, 0.0f);   // first update: start = end = 0
        connection.Refresh(matrix, 1.0f);   // inc = (0.70710677 - 0)/8

        var input = new[] { new[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f }, new[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f } };
        var destination = new[] { new float[8] };
        connection.ConsumeBuffer(input, destination, 8, 8);

        float[] expected = { 0f, 0.17677669f, 0.35355338f, 0.53033008f, 0.70710677f, 0.88388348f, 1.06066017f, 1.23743687f };
        for (int k = 0; k < 8; k++) Close(expected[k], destination[0][k]);
    }

    /// <summary>
    /// M6-012 / gapE 2.1 (0x00A4FC28..0x00A4FC78): the voice buffer is zero-padded per channel from
    /// <c>uValidFrames</c> to <c>uMaxFrames</c> before mixing. Two valid frames of 1.0 mix to 1.0 and the
    /// padded tail to 0.
    /// </summary>
    [Fact]
    public void TheZeroPadExtendsTheVoiceBufferToTheBusFrame()
    {
        var connection = new WwiseMixerConnection(1, 1);
        connection.Refresh(new[] { 1.0f }, 1.0f);

        var input = new[] { new[] { 1f, 1f, 7f, 7f, 7f, 7f, 7f, 7f } };
        var destination = new[] { new float[8] };
        connection.ConsumeBuffer(input, destination, validFrames: 2, maxFrames: 8);

        Assert.Equal(new[] { 1f, 1f, 0f, 0f, 0f, 0f, 0f, 0f }, destination[0]);
        Assert.Equal(new[] { 1f, 1f, 0f, 0f, 0f, 0f, 0f, 0f }, input[0]);   // the pad is written into the voice buffer
    }

    /// <summary>
    /// M6-012 / gapE 2.5 (0x00A4C0D8..0x00A4C104): the <c>conn+0x6C bit2</c> first-update flag makes the
    /// start gain 0, so the first frame ramps in from zero to the target. From 0 to 0.5 over 8 frames inc = 0.0625 and the
    /// gains are 0, 0.0625, 0.125, 0.1875, 0.25, 0.3125, 0.375, 0.4375.
    /// </summary>
    [Fact]
    public void TheFadeInStartsTheFirstFrameAtZero()
    {
        var connection = new WwiseMixerConnection(1, 1);
        connection.Refresh(new[] { 1.0f }, 0.5f, fadeIn: true);

        Assert.True(connection.StartGain == 0f);
        Assert.True(connection.EndGain == 0.5f);

        var input = new[] { new[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f } };
        var destination = new[] { new float[8] };
        connection.ConsumeBuffer(input, destination, 8, 8);

        Assert.Equal(new[] { 0f, 0.0625f, 0.125f, 0.1875f, 0.25f, 0.3125f, 0.375f, 0.4375f }, destination[0]);
    }

    /// <summary>
    /// M6-012 / gapE 2.2: an LFE-bearing channel config is refused, because the row names an LFE→LFE path
    /// but gives no arithmetic. 0x8101 sets bit 15.
    /// </summary>
    [Fact]
    public void TheLfeChannelConfigIsRefused()
    {
        Assert.True(WwiseMixerPan.HasLfe(0x8101));
        Assert.False(WwiseMixerPan.HasLfe(0x4101));
        Assert.Throws<NotSupportedException>(() => WwiseMixerPan.MatrixForChannelConfigs(0x8101, 0x4101));
    }

    /// <summary>
    /// M6-012 / gapE 2.7, gapG 5.3: only the settled matrices are built; anything else (here stereo → stereo)
    /// throws rather than guessing the general panner arithmetic.
    /// </summary>
    [Fact]
    public void AnUnsettledChannelConfigIsRefused()
    {
        Assert.Throws<NotSupportedException>(() => WwiseMixerPan.MatrixForChannelConfigs(0x3102, 0x3102));
    }
}
