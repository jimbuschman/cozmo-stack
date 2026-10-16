using Cozmo.Transport;
using Cozmo.Robot.Behavior;
using System.Text;
namespace Cozmo.Robot.Vision;

internal readonly record struct ImageQualityRectangle(int X,int Y,int Width,int Height);
public readonly record struct ImageQualityProposal(byte Quality,int Exposure,float Gain);

// M11-081 remains a separate implementation obligation (OV53 checked; B-M11-11 builds it).
internal interface IImageQualityPipeline
{
    uint ComputeExposureAdjustment(GrayImage image,GrayImage? mask,ref float factor);
    uint ComputePercentile(float percentile);
}

// fidelity: M11-080
internal sealed class ImageQualitySettings
{
    internal static readonly ImageQualitySettings Globals=new();
    internal byte TooBright=230,TooDark=15;
    internal bool MeterFromDetections=true;
    internal float Low=BitConverter.UInt32BitsToSingle(0x3DCCCCCD),Mid=BitConverter.UInt32BitsToSingle(0x3F000000),High=BitConverter.UInt32BitsToSingle(0x3F666666);
    internal bool Load(FirmwareJsonValue root,SourceLogSink source)
    {
        var previous=(TooBright,TooDark,MeterFromDetections,Low,Mid,High);
        try { return LoadFields(root,source); }
        catch(JsonLogicError)
        {
            (TooBright,TooDark,MeterFromDetections,Low,Mid,High)=previous;
            throw; // M1-034 terminating JsonCpp conversion: caller skips this config unit.
        }
    }
    private bool LoadFields(FirmwareJsonValue root,SourceLogSink source)
    {
        static FirmwareJsonValue? Find(FirmwareJsonValue r,string k)
        {
            if(r.Kind is not (FirmwareJsonKind.Object or FirmwareJsonKind.Null))throw new JsonLogicError("Json::Value::find requires objectValue");
            return r.Members.GetValueOrDefault(Convert.ToHexString(Encoding.UTF8.GetBytes(k)));
        }
        var section=Find(root,"ImageQuality");
        if(section is null) { Error("VisionSystem.Init.MissingImageQualityConfigField","");return false; }
        foreach(string key in new[]{"TooBrightValue","TooDarkValue","MeterFromDetections","LowPercentile","MidPercentile","HighPercentile"})
        {
            var value=Find(section,key);
            if(value is null || value.Kind==FirmwareJsonKind.Null) { Error("VisionSystem.Init.MissingJsonParameter",key);return false; }
            switch(key)
            {
                case "TooBrightValue":TooBright=unchecked((byte)Json.AsUInt(value));break;
                case "TooDarkValue":TooDark=unchecked((byte)Json.AsUInt(value));break;
                case "MeterFromDetections":MeterFromDetections=value.Kind switch
                {
                    FirmwareJsonKind.Null=>false,FirmwareJsonKind.Boolean=>value.Boolean,
                    FirmwareJsonKind.Int=>value.Signed!=0,FirmwareJsonKind.UInt=>value.Unsigned!=0,
                    FirmwareJsonKind.Real=>value.Real!=0,_=>throw new JsonLogicError("Value is not convertible to bool.")
                };break;
                case "LowPercentile":Low=Json.AsFloat(value);break;
                case "MidPercentile":Mid=Json.AsFloat(value);break;
                case "HighPercentile":High=Json.AsFloat(value);break;
            }
        }
        return true;
        void Error(string key,string text) { source(EngineLogLevel.Error,null,key,text);EngineErrorState.StoreAndMaybeBreak(); }
    }
}

// fidelity: M11-080, M11-057
internal sealed class ImageQualityProcessor
{
    internal ImageQualitySettings Settings=ImageQualitySettings.Globals;
    internal IImageQualityPipeline? Pipeline;
    internal SourceLogSink? SourceLog;
    internal static int Quantize(int exposure,int maxExposure)
    {
        int remainder=exposure-10*(exposure/10);
        if(remainder==0)return exposure;
        int next=unchecked(exposure+(10-remainder));
        int ceiling=10*(maxExposure/10);
        return next<ceiling?next:ceiling;
    }
    internal static int ArmSigned(float value)
    {
        if(float.IsNaN(value))return 0;
        if(value>=2147483648f)return int.MaxValue;
        if(value<=-2147483648f)return int.MinValue;
        return (int)value;
    }
    private static uint ArmUnsigned(float value)
    {
        if(!(value>0))return 0;
        if(value>=4294967296f)return uint.MaxValue;
        return (uint)value;
    }
    internal bool TryMask(GrayImage image,IReadOnlyList<ImageQualityRectangle> rectangles,out GrayImage? mask)
    {
        mask=null;
        if(!Settings.MeterFromDetections||rectangles.Count==0)return true;
        int area=0,total=unchecked(image.Width*image.Height);
        foreach(var rect in rectangles)area=unchecked(area+rect.Width*rect.Height);
        if(unchecked(2*area)>=total)return true;
        float weight=(float)area*255f;
        weight=weight/(float)total;
        byte outside=unchecked((byte)ArmUnsigned(weight)),inside=unchecked((byte)~outside);
        // Only the established in-bounds ROI recipient is used. Clip/error behavior has no checked row here.
        foreach(var rect in rectangles)
            if(rect.X<0||rect.Y<0||rect.Width<0||rect.Height<0||(long)rect.X+rect.Width>image.Width||(long)rect.Y+rect.Height>image.Height)
            {SteppedBehavior.ReportMissing("M11-080 GetROI 0x006B2E36 clipping/malformed rectangle recipient not built; mask unit skipped without inventing clipping");return false;}
        mask=new GrayImage(image.Width,image.Height);mask.Fill(outside);
        foreach(var rect in rectangles)
            for(int y=rect.Y;y<rect.Y+rect.Height;y++)for(int x=rect.X;x<rect.X+rect.Width;x++)mask[x,y]=inside;
        return true;
    }
    // false means missing input/recipient, not a fabricated native nonzero Result.
    internal bool TryCheck(GrayImage image,IReadOnlyList<ImageQualityRectangle> rectangles,CameraSettings.QualityCameraState state,int modes,out uint status,out ImageQualityProposal proposal)
    {
        status=0;proposal=default;
        if(Pipeline is null)
        {SteppedBehavior.ReportMissing("M11-081 ComputeExposureAdjustment/ComputePercentile checked by OV53 but not built (B-M11-11), 0x0085F748 / 0x0085FA02 / 0x00873624; no approximate imaging factor supplied");return false;}
        if(!TryMask(image,rectangles,out var mask))return false;
        float factor=1f;
        status=Pipeline.ComputeExposureAdjustment(image,mask,ref factor);
        if(status!=0)
        {
            SourceLog?.Invoke(EngineLogLevel.Warning,null,"VisionSystem.CheckImageQuality.ComputeNewExposureFailed",FormattableString.Invariant($"Detection Quads={rectangles.Count}"));
            return true;
        }
        proposal=Propose(factor,state,modes,Pipeline.ComputePercentile);
        return true;
    }
    internal ImageQualityProposal Propose(float factor,CameraSettings.QualityCameraState state,int modes,Func<float,uint> percentile)
    {
        int e=state.Exposure;
        float g=state.Gain;
        byte quality=1;
        float lower=BitConverter.UInt32BitsToSingle(0x3F7FFF58),upper=BitConverter.UInt32BitsToSingle(0x3F800054),epsilon=BitConverter.UInt32BitsToSingle(0x3727C5AC);
        if(factor<lower)
        {
            if(e>state.MinExposure)e=Math.Max(state.MinExposure,ArmSigned(MathF.Round(factor*(float)e,MidpointRounding.AwayFromZero)));
            else if(g>state.MinGain+epsilon)
            {
                float product=factor*g;
                g=state.MinGain<product?product:state.MinGain;
            }
            else if(percentile(Settings.Low)>Settings.TooBright)quality=3;
        }
        else if(factor>upper)
        {
            // Native ble includes unordered: a NaN current gain takes the exposure side.
            if(g<state.MaxGain-epsilon)
            {
                float product=factor*g;
                g=product<state.MaxGain?product:state.MaxGain;
            }
            else if(e<state.MaxExposure)e=Math.Min(state.MaxExposure,ArmSigned(MathF.Round(factor*(float)e,MidpointRounding.AwayFromZero)));
            else if(percentile(Settings.High)<Settings.TooDark)quality=2;
        }
        if((modes&(1<<14))!=0)e=Quantize(e,state.MaxExposure);
        return new(quality,e,g);
    }
}
