using Cozmo.Robot.Behavior;
using System.Text;
namespace Cozmo.Robot.Vision;

// fidelity: M11-098
internal sealed class NativeMotionDetector
{
    private readonly FirmwareJsonValue _config;
    private readonly Action<string> _log;
    private readonly Func<bool> _current;
    private readonly NativeMotionPrevious _previous=new();
    private NativePeripheralMotion? _periphery;
    private uint _lastComparison;
    internal uint ComparisonCount { get; private set; }
    internal bool HasPreviousImage=>_previous.Gray is not null||_previous.Rgb is not null;
    internal NativeMotionDetector(FirmwareJsonValue config,Action<string> log,Func<bool> current){_config=config;_log=log;_current=current;}
    // null is unavailable/terminating host input, 1 is native defined missing-parameter failure.
    private uint? EnsurePeriphery(int width,int height)
    {
        if(_periphery is not null)return 0;
        try
        {
            static FirmwareJsonValue? Member(FirmwareJsonValue node,string key)
            {
                if(node.Kind==FirmwareJsonKind.Null)return null;
                if(node.Kind!=FirmwareJsonKind.Object)throw new JsonLogicError("Json::Value::find requires objectValue");
                return node.Members.GetValueOrDefault(Convert.ToHexString(Encoding.UTF8.GetBytes(key)));
            }
            var group=Member(_config,"MotionDetector");
            if(group is null){_log("MotionDetector.Init.MissingJsonParameter");if(_current())Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();return 1;}
            float Get(string key){var v=Member(group,key);if(v is null||v.Kind==FirmwareJsonKind.Null)throw new KeyNotFoundException(key);return Json.AsFloat(v);}
            float horizontal=Get("HorizontalSize"),vertical=Get("VerticalSize"),increase=Get("IncreaseFactor"),decrease=Get("DecreaseFactor"),max=Get("MaxValue"),stability=Get("CentroidStability");
            _periphery=new(width,height,horizontal,vertical,increase,decrease,max,stability);return 0;
        }
        catch(KeyNotFoundException e)
        {_log("MotionDetector.Init.MissingJsonParameter "+e.Message);if(_current())Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();return 1;}
        catch(Exception e) when(e is JsonLogicError or JsonRuntimeError)
        {SteppedBehavior.ReportMissing("M1-034/M11-098 MotionDetector terminating config conversion skipped");return null;}
    }
    internal bool Detect(NativeMotionImage half,NativeMotionPoseState poses,int fullColumns,int fullRows,List<NativeObservedMotion> reports)
    {
        var init=EnsurePeriphery(half.Columns,half.Rows);
        if(init is null)return false;
        if(init!=0)return _current(); // caller ignores native Result1 and sets processed mode bit
        var previous=half.Channels==1?_previous.Gray:_previous.Rgb;
        bool filtered=half.Channels==1?_previous.GrayFiltered:_previous.RgbFiltered;
        var current=poses.Current;var prior=poses.Previous;
        if(current is null)return false;
        if(prior is null || previous is null || !NativeMotionPoseState.HeadSame(current,prior) || !NativeMotionPoseState.BodySame(current,prior) ||
            unchecked(half.Timestamp-_lastComparison)<=500 || (current.State.Status&0x8200)!=0x0200)
        {_previous.Set(half,false);return true;}
        _lastComparison=half.Timestamp;
        ComparisonCount=unchecked(ComparisonCount+1);
        if(!NativeMotionRatio.TryBoxFilter(half))return false;
        if(!filtered && !NativeMotionRatio.TryBoxFilter(previous))return false;
        var ratio=NativeMotionRatio.Compare(previous,half);
        var mask=new NativeMotionImage(half.Rows,half.Columns,1,half.Columns,ratio.Mask,half.Timestamp);
        var kernel=NativeMotionKernel.Ellipse10();
        if(!NativeMotionMorphology.TryApply(mask,mask,10,10,kernel,5,5,NativeMotionMorphOperation.Close))return false;
        var components=new List<NativeMotionComponent>();
        if(!NativeMotionComponents.TryAppend(mask,components,out _))return false;
        var peripheral=_periphery!.Reduce(components,2f);
        float thresholdValue=(float)(mask.Rows*mask.Columns)*BitConverter.UInt32BitsToSingle(0x3B91A2B4);
        uint threshold=(uint)MathF.Round(thresholdValue,MidpointRounding.AwayFromZero);
        var image=ratio.Count>threshold?NativeMotionGroundMath.Quantile(mask,.5f,.5f):default;
        NativeMotionGround.Result ground=default;
        if(current.GroundValid&&prior.GroundValid)ground=NativeMotionGround.Extract(mask,current,fullColumns,fullRows,2f,_log);
        bool imagePresent=image.Count>0,groundPresent=ground.Fraction>0;
        static short Quant(float value)=>unchecked((short)Animation.AnimationScheduler.VcvtS32(MathF.Round(value,MidpointRounding.AwayFromZero)));
        if(imagePresent||groundPresent)
        {
            var report=new NativeObservedMotion(half.Timestamp,imagePresent?(float)image.Count/(mask.Rows*mask.Columns):0,
                imagePresent?Quant(image.X*2):(short)0,imagePresent?Quant(image.Y*2):(short)0,
                groundPresent?ground.Fraction:0,groundPresent?Quant(ground.X):(short)0,groundPresent?Quant(ground.Y):(short)0,
                peripheral.Top,peripheral.TopX,peripheral.TopY,peripheral.Left,peripheral.LeftX,peripheral.LeftY,peripheral.Right,peripheral.RightX,peripheral.RightY);
            reports.Add(report);reports.Add(report); // helper append and outer append are distinct native nodes
        }
        else if(peripheral.Any)
            SteppedBehavior.ReportMissing("M1-034/M11-098 peripheral-only append reads an uninitialized native report prefix; that append skipped");
        _previous.Set(half,true);return true;
    }
}
