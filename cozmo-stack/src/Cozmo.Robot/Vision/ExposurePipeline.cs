using System.Globalization;
using Cozmo.Transport;
namespace Cozmo.Robot.Vision;

// A borrowed 8-bit Mat view; GrayImage production inputs are continuous. No copying or gamma conversion.
internal readonly record struct BrightnessImage(byte[] Data,int Rows,int Cols,int Step,bool Continuous)
{
    internal static BrightnessImage From(GrayImage image)=>new(image.Pixels,image.Height,image.Width,image.Width,true);
}
// fidelity: M11-081 — histogram0x008731F0..0x008736DA, counters are signed32 native words.
internal sealed class ImageBrightnessHistogram
{
    internal readonly int[] Bins=new int[256];
    internal int Total;
    internal SourceLogSink? SourceLog;
    internal void Reset(){Array.Clear(Bins);Total=0;}
    private void Error(string key,string text){SourceLog?.Invoke(EngineLogLevel.Error,null,key,text);EngineErrorState.StoreAndMaybeBreak();}
    private int Stride(int sub,bool weighted)
    {
        if(sub>0)return sub;
        Error(weighted?"ImageBrightnessHistogram.FillFromImage.InvalidSubSampleWithWeights":"ImageBrightnessHistogram.FillFromImage.InvalidSubSample",FormattableString.Invariant($"{sub} not > 0. Will use 1 instead."));return 1;
    }
    internal uint Fill(BrightnessImage image,int sub,Func<byte,byte>? transform=null)
    {
        sub=Stride(sub,false);int rows=image.Rows,cols=image.Cols;
        if(image.Continuous&&sub==1){cols=unchecked(rows*cols);rows=1;}
        for(int y=0;y<rows;y=unchecked(y+sub))for(int x=0;x<cols;x=unchecked(x+sub))
        {byte value=image.Data[unchecked(y*image.Step+x)];if(transform is not null)value=transform(value);Bins[value]=unchecked(Bins[value]+1);}
        int rowCount=unchecked(sub-1+rows)/sub,colCount=unchecked(sub-1+cols)/sub;
        Total=unchecked(Total+rowCount*colCount);return 0;
    }
    internal uint Fill(BrightnessImage image,BrightnessImage mask,int sub,Func<byte,byte>? transform=null)
    {
        if(image.Rows!=mask.Rows||image.Cols!=mask.Cols)
        {
            Error("ImageBrightnessHistogram.FillFromImage.InvalidWeightMaskSize",FormattableString.Invariant($"Weight mask [{mask.Cols}x{mask.Rows}] not same size as image [{image.Cols}x{image.Rows}]"));return 1;
        }
        sub=Stride(sub,true);int rows=image.Rows,cols=image.Cols;
        if(image.Continuous&&mask.Continuous&&sub==1){cols=unchecked(rows*cols);rows=1;}
        for(int y=0;y<rows;y=unchecked(y+sub))for(int x=0;x<cols;x=unchecked(x+sub))
        {
            byte weight=mask.Data[unchecked(y*mask.Step+x)];if(weight==0)continue;
            byte value=image.Data[unchecked(y*image.Step+x)];if(transform is not null)value=transform(value);
            Bins[value]=unchecked(Bins[value]+weight);Total=unchecked(Total+weight);
        }
        return 0;
    }
    internal uint Percentile(float p)
    {
        if(Total==0)
        {SourceLog?.Invoke(EngineLogLevel.Warning,null,"ImageBrightnessHistogram.ComputePercentile.ZeroTotalCount","Returning 0 for p="+PoseDiagnostics.Fixed(p)+" because histogram is empty");return 0;}
        float lo=0;if(p>0)lo=p;float bounded=1;if(lo<1)bounded=lo;
        float product=bounded*(float)Total;
        int target=ImageQualityProcessor.ArmSigned(MathF.Round(product,MidpointRounding.AwayFromZero));
        if(target<1)return 0;
        int sum=0;uint result=0;
        do{byte j=unchecked((byte)result);if(j==255)return 255;result++;sum=unchecked(sum+Bins[j]);}while(sum<target);
        return unchecked((byte)result);
    }
}

// fidelity: M11-081 — same imaging-pipeline owner read by ImageQualityProcessor.
internal sealed class ExposurePipeline:IImageQualityPipeline
{
    internal readonly ImageBrightnessHistogram Histogram=new();
    private readonly SourceLogSink _source;
    internal byte MidValue {get;private set;}
    internal float MidPercentile {get;private set;}
    internal float MaxChange {get;private set;}
    internal int SubSample {get;private set;}
    internal bool Configured {get;private set;}
    internal ExposurePipeline(SourceLogSink source){_source=source;Histogram.SourceLog=source;}
    private void Error(string key,string text){_source(EngineLogLevel.Error,null,key,text);EngineErrorState.StoreAndMaybeBreak();}
    internal uint SetExposureParameters(byte mid,float percentile,float maxChange,int sub)
    {
        float lower=BitConverter.UInt32BitsToSingle(0xB727C5AC),upper=BitConverter.UInt32BitsToSingle(0x3F800054);
        if(maxChange<lower||maxChange>upper){Error("ImagingPipeline.SetExposureParameters.BadMaxChangeFraction",PoseDiagnostics.Fixed(maxChange)+" not on interval [0,1]");return 0x03000000;}
        if(percentile<lower||percentile>upper){Error("ImagingPipeline.SetExposureParameters.BadMidPercentile",PoseDiagnostics.Fixed(percentile)+" not on interval [0,1]");return 0x03000000;}
        if(sub<=0){Error("ImagingPipeline.SetExposureParameters.BadSubSample",FormattableString.Invariant($"{sub} not > 0"));return 0x03000000;}
        MidValue=mid;MidPercentile=percentile;MaxChange=maxChange;SubSample=sub;Configured=true;return 0;
    }
    public uint ComputePercentile(float percentile)=>Histogram.Percentile(percentile);
    public uint ComputeExposureAdjustment(GrayImage image,GrayImage? mask,ref float factor)
    {
        // No native constructor defaults are inferred when config initialization never succeeded.
        if(!Configured)throw new NotSupportedException("M11-081 exposure parameters have no established constructor defaults/config recipient");
        factor=1;Histogram.Reset();
        uint status=mask is null?Histogram.Fill(BrightnessImage.From(image),SubSample):Histogram.Fill(BrightnessImage.From(image),BrightnessImage.From(mask),SubSample);
        if(status!=0){Error("ImagingPipeline.ComputeNewExposure.FillHistogramFailed","");return status;}
        if(Histogram.Total==0){_source(EngineLogLevel.Warning,null,"ImagingPipeline.ComputeNewExposure.EmptyHistogram","");return 1;}
        uint b=Histogram.Percentile(MidPercentile);
        if(b==0){factor=MaxChange+1f;return 0;}
        float ratio=(float)MidValue/(float)b;factor=ratio;
        float lower=1f-MaxChange,upper=MaxChange+1f;if(ratio>lower)lower=ratio;if(lower<upper)upper=lower;
        factor=upper;return 0;
    }
    internal uint ComputeExposureAdjustment(GrayImage image,ref float factor)=>ComputeExposureAdjustment(image,null,ref factor);
}

public sealed partial class VisionSystem
{
    private ExposurePipeline? _exposurePipeline;
    private static string Fixed3(float v)=>float.IsFinite(v)?((double)v).ToString("F3",CultureInfo.InvariantCulture):PoseDiagnostics.Fixed(v);
    // fidelity: M11-081 — wrapper argument order at0x006B1858, success-only Info.
    internal uint SetAutoExposureParams(int sub,byte mid,float percentile,float maxChange)
    {
        var pipeline=_exposurePipeline??=new((l,c,k,t)=>_robot.Engine.EmitSourceLog(l,c,k,t));
        uint status=pipeline.SetExposureParameters(mid,percentile,maxChange,sub);
        if(status==0)
        {
            QualityProcessor.Pipeline=pipeline;
            _robot.Engine.EmitSourceLog(EngineLogLevel.Info,"VisionSystem","VisionSystem.SetAutoExposureParams",FormattableString.Invariant($"subSample:{sub} midVal:{mid} midPerc:{Fixed3(percentile)} changeFrac:{Fixed3(maxChange)}"));
        }
        return status;
    }
}
