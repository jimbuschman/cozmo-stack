using Cozmo.Robot.Vision;
using Xunit;
namespace Cozmo.Protocol.Tests;
public sealed class NativePeripheralMotionTests
{
    private static NativeMotionComponent Row(uint area,float x,float y)=>new(area,x,y,0,0,0,0);
    [Fact]
    public void OrderedComponentsUpdateAndDecayEachDirectionSeparately()
    {
        var state=new NativePeripheralMotion(128,128,.3f,.4f,100,1,3,.6f);
        var first=state.Reduce(new[]{Row(256,0,0)},2);
        Assert.Equal(2.125f,state.Top);Assert.Equal(2.125f,state.Left);Assert.Equal(0,state.Right);Assert.False(first.Any);
        var second=state.Reduce(new[]{Row(256,120,0)},2);
        Assert.Equal(3,state.Top);Assert.Equal(1.125f,state.Left);Assert.Equal(2.125f,state.Right);
        Assert.True(second.Any);Assert.Equal(3,second.Top);Assert.Equal((short)0,second.LeftX);
    }
    [Fact]
    public void AreaThresholdAndEmptyRowsUseOneDecay()
    {
        var state=new NativePeripheralMotion(128,128,.3f,.4f,100,1,3,.6f);
        state.Reduce(new[]{Row(249,0,0)},2);Assert.Equal(0,state.Top);
        state.Reduce(new[]{Row(250,0,0)},2);Assert.Equal(2.0517578125f,state.Top);
        state.Reduce(Array.Empty<NativeMotionComponent>(),2);Assert.Equal(1.0517578125f,state.Top);
    }
    [Fact]
    public void ZeroMaximumQualifiesEvenWhenEveryMagnitudeIsZero()
    {
        var state=new NativePeripheralMotion(128,128,.3f,.4f,100,1,0,.6f);
        var result=state.Reduce(Array.Empty<NativeMotionComponent>(),2);
        Assert.True(result.Any);Assert.Equal(0,result.Top);Assert.Equal(0,result.Left);Assert.Equal(0,result.Right);
    }
    [Fact]
    public void QuantizationUsesRoundfThenSaturatingArmSignedConversion()
    {
        Assert.Equal((short)3,NativePeripheralMotion.Quantize(1.25f,2));
        Assert.Equal((short)-3,NativePeripheralMotion.Quantize(-1.25f,2));
        Assert.Equal((short)-1,NativePeripheralMotion.Quantize(float.PositiveInfinity,1));
        Assert.Equal((short)0,NativePeripheralMotion.Quantize(float.NaN,1));
    }
}
