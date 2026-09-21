using Cozmo.Protocol;
using Cozmo.Robot;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// The light colour word, as the engine packs it (fidelity manifest M2-005).
///
/// <c>CubeLightComponent::SendTransitionMessage</c> 0x00638056 holds the whole of it. With the engine's
/// 32-bit <c>ColorRGBA</c> in r5 — red in the top byte, alpha in the bottom — it is three masked shifts
/// and a conditional bit:
///
/// <code>
///   mov.w  ip, #0x7c00
///   mov.w  r3, #0x3e0
///   tst.w  r5, #0xff                 ; is there any alpha?
///   and.w  r4, ip, r5, lsr #17       ; red   bits 27..31 -> bits 10..14
///   and.w  r7, r3, r5, lsr #14       ; green bits 19..23 -> bits  5..9
///   ubfx   r4, r5, #0xb, #5          ; blue  bits 11..15 -> bits  0..4
///   orrne  r7, r7, #0x8000           ; ... and bit 15 when alpha is non-zero
/// </code>
///
/// This had been PyCozmo's packing with the bit order recorded as unconfirmed and bit 15 left clear. The
/// bit order turns out to be right; bit 15 was not being sent.
/// </summary>
public class LightPackingTests
{
    /// <summary>Each channel lands in its own five bits, at the position the shifts put it.</summary>
    [Theory]
    [InlineData(255, 0, 0, 0x8000 | (31 << 10))]
    [InlineData(0, 255, 0, 0x8000 | (31 << 5))]
    [InlineData(0, 0, 255, 0x8000 | 31)]
    [InlineData(255, 255, 255, 0x8000 | 0x7FFF)]
    [InlineData(0, 0, 0, 0x8000)]                 // black is still a colour with alpha
    [InlineData(8, 8, 8, 0x8000 | (1 << 10) | (1 << 5) | 1)]   // the low three bits are dropped, not rounded
    [InlineData(7, 7, 7, 0x8000)]
    public void EachChannelKeepsItsTopFiveBitsInTheEnginesPositions(byte r, byte g, byte b, int expected) =>
        Assert.Equal((ushort)expected, LightState.Rgb(r, g, b));

    /// <summary>
    /// Bit 15 follows the alpha byte and nothing else: the engine tests the whole byte against zero, so
    /// any non-zero alpha sets it and only a zero alpha clears it. It is not a brightness.
    /// </summary>
    [Theory]
    [InlineData(255, true)]
    [InlineData(1, true)]
    [InlineData(128, true)]
    [InlineData(0, false)]
    public void BitFifteenIsSetWheneverTheAlphaByteIsNotZero(byte a, bool set)
    {
        ushort packed = LightState.Rgb(255, 255, 255, a);
        Assert.Equal(set, (packed & 0x8000) != 0);
        Assert.Equal(0x7FFF, packed & 0x7FFF);     // the colour half is unaffected either way
    }

    /// <summary>
    /// And the same arithmetic run over the engine's own 32-bit word, so the test would catch a channel
    /// being moved as well as a bit being dropped: this is the expression at 0x00638056 transcribed, fed
    /// the same input as <see cref="LightState.Rgb"/>.
    /// </summary>
    [Theory]
    [InlineData(0xFF00_00FFu)]      // red, opaque
    [InlineData(0x00FF_00FFu)]      // green
    [InlineData(0x0000_FFFFu)]      // blue
    [InlineData(0x1234_56FFu)]
    [InlineData(0xABCD_EF01u)]
    [InlineData(0xABCD_EF00u)]      // alpha 0
    public void ThePackingAgreesWithTheEnginesExpressionOnItsOwnWord(uint c)
    {
        ushort engine = (ushort)(((c >> 17) & 0x7C00) | ((c >> 14) & 0x03E0) | ((c >> 11) & 0x001F)
                                 | ((c & 0xFF) != 0 ? 0x8000u : 0u));
        Assert.Equal(engine, LightState.Rgb((byte)(c >> 24), (byte)(c >> 16), (byte)(c >> 8), (byte)c));
    }

    /// <summary>The colours the stack names go out with bit 15 set, including the one that means "off".</summary>
    [Fact]
    public void TheNamedColoursAllCarryTheAlphaBit()
    {
        foreach (var c in new[] { LedColor.Off, LedColor.Red, LedColor.Green, LedColor.Blue,
                                  LedColor.White, LedColor.Yellow, LedColor.Cyan, LedColor.Magenta })
            Assert.True((c.Packed & 0x8000) != 0, $"{c} went out with bit 15 clear");

        Assert.Equal((ushort)0x8000, LedColor.Off.Packed);
        Assert.Equal((ushort)(0x8000 | (31 << 10)), LedColor.Red.Packed);
    }
}
