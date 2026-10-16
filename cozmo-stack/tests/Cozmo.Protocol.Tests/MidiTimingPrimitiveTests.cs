using Cozmo.Robot.Animation.Wwise;
using Xunit;
namespace Cozmo.Protocol.Tests;

public sealed class MidiTimingPrimitiveTests
{
    // Native ARM execution of 0x009D7438..0x009D747C, R=48000 policy, mode0/1.
    // These verify an existing primitive, not a live LFO initializer/renderer join.
    [Theory]
    [InlineData(0x40B00000u,0x3A3CBAEAu,0x38F04C75u)]
    [InlineData(0xC0B00000u,0xBA3CBAEAu,0xB8F04C75u)]
    [InlineData(0x473B8000u,0x40C90FDBu,0x3F800000u)]
    [InlineData(0x473B7FFFu,0x40C90FDAu,0x3F7FFFFFu)]
    [InlineData(0x7FC00000u,0x40C90FDBu,0x3F800000u)]
    [InlineData(0x7F800000u,0x40C90FDBu,0x3F800000u)]
    [InlineData(0x80000000u,0x80000000u,0x80000000u)]
    public void PhaseStepMatchesNativeWords(uint frequency,uint radians,uint cycles)
    {
        float f=BitConverter.UInt32BitsToSingle(frequency);
        Assert.Equal(radians,BitConverter.SingleToUInt32Bits(WwiseLfoCoefficients.PhaseIncrement(f,true)));
        Assert.Equal(cycles,BitConverter.SingleToUInt32Bits(WwiseLfoCoefficients.PhaseIncrement(f,false)));
    }
    [Fact]
    public void ActualPbiFactoryInitializesPendingDeadlineIndependentlyOfStopOffset()
    {
        var bridge=new WwisePlaybackBridge();var pbi=bridge.CreatePbi(new WwisePlayInitParams(),7,new object(),false);
        Assert.Equal(0u,pbi.Field34);Assert.Equal(0xFFFFFFFFu,pbi.Field38);Assert.Equal(0xFFFFFFFFu,pbi.Field1F8);
        pbi.Field38=123;pbi.Field1F8=456;Assert.Equal(456u,pbi.TakeStopOffset9CBACC());Assert.Equal(123u,pbi.Field38);
        var next=bridge.CreatePbi(new WwisePlayInitParams(),7,new object(),false);Assert.Equal(0xFFFFFFFFu,next.Field38);Assert.Equal(0u,next.Field34);
    }
    [Fact]
    public void NaNFrequencyKeepsNativeCoefficientDivideWhileZeroSmoothingStillBypassesIt()
    {
        var c=WwiseLfoCoefficients.FromFrequencyAndDamping(float.NaN,1);
        Assert.True(float.IsNaN(c.C1));Assert.True(float.IsNaN(c.C2));
        var zero=WwiseLfoCoefficients.FromFrequencyAndDamping(float.NaN,-0f);
        Assert.Equal(0x3F800000u,BitConverter.SingleToUInt32Bits(zero.C1));Assert.Equal(0u,BitConverter.SingleToUInt32Bits(zero.C2));
    }
}
