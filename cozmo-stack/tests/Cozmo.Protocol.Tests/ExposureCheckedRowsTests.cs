using System.Text;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
using Cozmo.Transport;
using Xunit;
namespace Cozmo.Protocol.Tests;
[Collection("M5 process statics")]
public sealed class ExposureCheckedRowsTests:IDisposable
{
    private readonly bool _error=EngineErrorState.ErrorFlagSet;
    public void Dispose()=>EngineErrorState.ErrorFlagSet=_error;
    private sealed record Log(EngineLogLevel Level,string? Channel,string Key,string Text);
    private static float F(uint bits)=>BitConverter.UInt32BitsToSingle(bits);
    private static ExposurePipeline Pipeline(List<Log>? logs=null)=>new((l,c,k,t)=>logs?.Add(new(l,c,k,t)));
    [Theory][InlineData(0xB727C5ACu)][InlineData(0x3F800054u)][InlineData(0x7FC00001u)]
    public void InclusiveTolerantBoundsAndNaNStoreAllFourWords(uint bits)
    {
        var p=Pipeline();Assert.Equal(0u,p.SetExposureParameters(233,F(bits),F(bits),2));Assert.Equal((byte)233,p.MidValue);Assert.Equal(bits,BitConverter.SingleToUInt32Bits(p.MidPercentile));Assert.Equal(bits,BitConverter.SingleToUInt32Bits(p.MaxChange));Assert.Equal(2,p.SubSample);
    }
    [Theory][InlineData(0xB727C5ADu)][InlineData(0x3F800055u)][InlineData(0x7F800000u)][InlineData(0xFF800000u)]
    public void MaxChangeRejectionComesFirstAndLeavesParametersUntouched(uint bits)
    {
        var logs=new List<Log>();var p=Pipeline(logs);p.SetExposureParameters(115,.5f,.8f,2);EngineErrorState.ErrorFlagSet=false;
        Assert.Equal(0x03000000u,p.SetExposureParameters(1,F(bits),F(bits),0));Assert.Equal((byte)115,p.MidValue);Assert.Equal(.5f,p.MidPercentile);Assert.Equal(.8f,p.MaxChange);Assert.Equal(2,p.SubSample);
        Assert.Equal("ImagingPipeline.SetExposureParameters.BadMaxChangeFraction",Assert.Single(logs).Key);Assert.True(EngineErrorState.ErrorFlagSet);
    }
    [Fact] public void PercentileAndSubsampleHaveTheirOwnErrorEvents()
    {
        var logs=new List<Log>();var p=Pipeline(logs);Assert.Equal(0x03000000u,p.SetExposureParameters(1,2,.8f,0));Assert.Equal("ImagingPipeline.SetExposureParameters.BadMidPercentile",Assert.Single(logs).Key);
        logs.Clear();Assert.Equal(0x03000000u,p.SetExposureParameters(1,.5f,.8f,0));Assert.Equal("ImagingPipeline.SetExposureParameters.BadSubSample",Assert.Single(logs).Key);Assert.False(p.Configured);
    }
    [Theory][InlineData((byte)0,1u)][InlineData((byte)100,101u)][InlineData((byte)254,255u)][InlineData((byte)255,255u)]
    public void HalfRoundAwayAndOnePastQualifyingBin(byte value,uint expected)
    {var h=new ImageBrightnessHistogram();h.Fill(new(new[]{value},1,1,1,true),1);Assert.Equal(expected,h.Percentile(.5f));}
    [Theory][InlineData(0f,0u)][InlineData(float.NaN,0u)][InlineData(float.NegativeInfinity,0u)][InlineData(float.PositiveInfinity,101u)][InlineData(1f,101u)]
    public void PercentileClampsOrderedInputsBeforeRound(float value,uint expected)
    {var h=new ImageBrightnessHistogram();h.Fill(new(new byte[]{100},1,1,1,true),1);Assert.Equal(expected,h.Percentile(value));}
    [Fact] public void HistogramAccumulatesGridCountAcrossPaddedRowsAndResetClearsAll()
    {
        var h=new ImageBrightnessHistogram();var view=new BrightnessImage(new byte[]{10,1,20,99,2,2,2,99,30,1,40,99},3,3,4,false);
        h.Fill(view,2);Assert.Equal(4,h.Total);Assert.Equal(new[]{10,20,30,40},Enumerable.Range(0,256).Where(i=>h.Bins[i]!=0));h.Fill(view,2);Assert.Equal(8,h.Total);h.Reset();Assert.Equal(0,h.Total);Assert.All(h.Bins,b=>Assert.Equal(0,b));
    }
    [Fact] public void WeightedBytesAreAdditiveAndZeroSkipsImageReadAndTransform()
    {
        var h=new ImageBrightnessHistogram();int calls=0;
        h.Fill(new(new byte[]{100,150},1,2,2,true),new(new byte[]{2,255},1,2,2,true),1,v=>{calls++;return v;});Assert.Equal(257,h.Total);Assert.Equal(2,h.Bins[100]);Assert.Equal(255,h.Bins[150]);Assert.Equal(2,calls);
        h.Fill(new(Array.Empty<byte>(),1,1,1,false),new(new byte[]{0},1,1,1,false),1,_=>throw new InvalidOperationException());Assert.Equal(257,h.Total);
    }
    [Fact] public void MaskMismatchPrecedesStrideCorrectionAndPreservesHistogram()
    {
        var logs=new List<Log>();var h=new ImageBrightnessHistogram{SourceLog=(l,c,k,t)=>logs.Add(new(l,c,k,t))};h.Total=4;h.Bins[1]=4;
        Assert.Equal(1u,h.Fill(new(new byte[6],2,3,3,true),new(new byte[4],1,4,4,true),0));Assert.Equal(4,h.Total);Assert.Equal(4,h.Bins[1]);var e=Assert.Single(logs);Assert.Equal("ImageBrightnessHistogram.FillFromImage.InvalidWeightMaskSize",e.Key);Assert.Equal("Weight mask [4x1] not same size as image [3x2]",e.Text);
    }
    [Fact] public void NonpositiveStrideLogsThenUsesOneWithoutReset()
    {
        var logs=new List<Log>();var h=new ImageBrightnessHistogram{SourceLog=(l,c,k,t)=>logs.Add(new(l,c,k,t))};h.Fill(new(new byte[]{0,1,2},1,3,3,true),0);Assert.Equal(3,h.Total);Assert.Equal("ImageBrightnessHistogram.FillFromImage.InvalidSubSample",Assert.Single(logs).Key);
    }
    [Fact] public void EmptyHistogramWarnsAndSignedOverflowCountReturnsZero()
    {
        var logs=new List<Log>();var h=new ImageBrightnessHistogram{SourceLog=(l,c,k,t)=>logs.Add(new(l,c,k,t))};Assert.Equal(0u,h.Percentile(.5f));Assert.Equal("ImageBrightnessHistogram.ComputePercentile.ZeroTotalCount",Assert.Single(logs).Key);h.Total=int.MinValue;Assert.Equal(0u,h.Percentile(1));
    }
    [Fact] public void ImageMaskRatioUsesWeightedMedianAndAllZeroMaskIsFailure()
    {
        var p=Pipeline();p.SetExposureParameters(115,.5f,.8f,1);var image=new GrayImage(2,1,new byte[]{100,200});var mask=new GrayImage(2,1,new byte[]{255,1});float factor=9;
        Assert.Equal(0u,p.ComputeExposureAdjustment(image,mask,ref factor));Assert.Equal(BitConverter.SingleToUInt32Bits(115f/101f),BitConverter.SingleToUInt32Bits(factor));mask.Fill(0);Assert.Equal(1u,p.ComputeExposureAdjustment(image,mask,ref factor));Assert.Equal(1,factor);
    }
    [Fact] public void ZeroPercentileUsesUpperBoundAndNaNChangeStaysNaN()
    {
        var p=Pipeline();p.SetExposureParameters(115,0,.8f,1);var image=new GrayImage(1,1,new byte[]{100});float factor=0;
        Assert.Equal(0u,p.ComputeExposureAdjustment(image,ref factor));Assert.Equal(0x3FE66666u,BitConverter.SingleToUInt32Bits(factor));p.SetExposureParameters(115,.5f,float.NaN,1);Assert.Equal(0u,p.ComputeExposureAdjustment(image,ref factor));Assert.True(float.IsNaN(factor));
    }
    [Fact] public void ActualVisionWrapperLogsOnlySuccessAndOwnsTheConnectedPipeline()
    {
        using var robot=CozmoRobot.CreateOffline();using var vision=new VisionSystem(robot);var logs=new List<EngineLogEntry>();robot.Engine.LogEntry+=logs.Add;
        Assert.Equal(0u,vision.SetAutoExposureParams(2,115,.5f,.8f));Assert.IsType<ExposurePipeline>(vision.QualityProcessor.Pipeline);var info=Assert.Single(logs);Assert.Equal("VisionSystem",info.Channel);Assert.Equal("VisionSystem.SetAutoExposureParams",info.Name);Assert.Equal("subSample:2 midVal:115 midPerc:0.500 changeFrac:0.800",info.Text);
        logs.Clear();Assert.Equal(0x03000000u,vision.SetAutoExposureParams(0,99,.5f,.8f));Assert.DoesNotContain(logs,e=>e.Level==EngineLogLevel.Info);
    }
}
