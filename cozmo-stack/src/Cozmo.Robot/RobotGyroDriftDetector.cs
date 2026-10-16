using Cozmo.Protocol;
using Cozmo.Robot.Behavior;

namespace Cozmo.Robot;

// fidelity: M10-015, M10-014
// Checked detector bodies. The live UFRS history/pose admission recipient is still MISSING in Sensors.
internal sealed class RobotGyroDriftDetector
{
    private readonly EngineRobot _robot;
    internal bool DriftReported, BiasReported;
    internal uint DriftFrame, DriftStart, DriftCount, BiasStart, BiasCount;
    internal Radians StartYaw = new();
    internal float Baseline, Sum, Minimum, Maximum, HighPass, PreviousMagnitude;
    internal readonly float[] Filtered = new float[3], Minima = new float[3], Maxima = new float[3]; // ctor minima=0, not reset +inf.
    private static float F(int bits)=>BitConverter.Int32BitsToSingle(bits);
    private static readonly float DriftLimit=F(0x3E32B8C2),StableLimit=F(0x3C8EFA35),YawLimit=F(0x3C0EFA35),Degrees=F(0x42652EE1);
    private static readonly float HighPassFactor=F(0x3F4CCCCD),BiasLimit=F(0x3AE4C388),SpreadLimit=F(0x3A64C388),RawFactor=F(0x3D4CCCD0),OldFactor=F(0x3F733333);
    internal RobotGyroDriftDetector(EngineRobot robot)=>_robot=robot;

    // Inputs are native Movement+9, Cliff+5, R+0x314 and R+0x2B0, not inferred packet substitutes.
    internal void DetectGyroDrift(RobotState state,bool moving,bool cliff,byte headCalibrated,uint frame)
    {
        if(DriftReported)return;
        float z=state.Gyro.Z;
        if(MathF.Abs(z)>DriftLimit||moving||cliff||headCalibrated!=1){DriftStart=0;return;}
        if(DriftStart==0)
        {
            DriftFrame=frame;StartYaw=ReadYaw();Baseline=Sum=Minimum=Maximum=z;DriftStart=state.Timestamp;DriftCount=1;return;
        }
        if(MathF.Abs(Baseline-z)>StableLimit||DriftFrame!=frame){DriftStart=0;return;}
        if(unchecked(state.Timestamp-DriftStart)<5001)
        {
            if(z>Maximum)Maximum=z;if(z<Minimum)Minimum=z;
            float sum=z+Sum;DriftCount=unchecked(DriftCount+1);Sum=sum;return;
        }
        float displacement=MathF.Abs((StartYaw-ReadYaw()).Value);
        if(!(displacement>YawLimit)){DriftStart=0;return;} // ble rejects unordered too.
        float data=displacement*Degrees;
        float mean=Sum/(float)DriftCount;mean*=Degrees;
        float minimum=Minimum*Degrees,maximum=Maximum*Degrees;
        // M11-083: missing $data prevents construction of this exact warning, not permission for a placeholder KV.
        SteppedBehavior.ReportMissing($"M10-015/M11-083 drift to_string(float) 0x0052C63A: Warning robot.detect_gyro_drift.drift_detected at 0x0052C6B6 withheld until $data bytes exist; data/mean/min/max f32 bits {Bits(data)}/{Bits(mean)}/{Bits(minimum)}/{Bits(maximum)}");
        DriftReported=true;DriftStart=0;
    }
    private Radians ReadYaw()
    {
        using var diagnostics=Vision.Pose3d.UseSourceLog((l,c,k,t)=>_robot.Engine.EmitSourceLog(l,c,k,t),_robot.Engine.Das.ForceFlushNow);
        Radians yaw=new();
        LogCallbackDeferral.RunGated(_robot.PoseTreeGate,()=>yaw.Value=(float)_robot.GetCheckedMainPose().AngleAroundZ); // helper already constructs/rescales Radians.
        return yaw;
    }
    private static string Bits(float value)=>BitConverter.SingleToInt32Bits(value).ToString("X8",System.Globalization.CultureInfo.InvariantCulture);

    internal void DetectBias(RobotState state,float rawMagnitude)
    {
        if(BiasReported)return;
        float difference=rawMagnitude-PreviousMagnitude;difference+=HighPass;HighPass=difference*HighPassFactor;PreviousMagnitude=rawMagnitude;
        if((state.Status&1)!=0||MathF.Abs(HighPass)>50f)
        {if(BiasStart!=0)ResetWindow();return;}
        if(BiasStart==0)BiasStart=state.Timestamp;
        else if(unchecked(state.Timestamp-BiasStart)>=5001)
        {
            if(unchecked((int)BiasCount)>=50)
                for(int axis=0;axis<3;axis++)
                {
                    float min=Minima[axis],max=Maxima[axis];
                    if(!(MathF.Abs(min)>BiasLimit)||float.IsNegative(min)!=float.IsNegative(max)||!(MathF.Abs(max)>BiasLimit)||!(max-min<SpreadLimit))continue;
                    string name=axis==0?"x":axis==1?"y":"z";
                    float minDegrees=min*Degrees,maxDegrees=max*Degrees;
                    float roundedMin=MathF.Round(minDegrees*1000f,MidpointRounding.AwayFromZero),roundedMax=MathF.Round(maxDegrees*1000f,MidpointRounding.AwayFromZero);
                    SteppedBehavior.ReportMissing($"M10-015/M11-083 bias to_string(float) 0x0052C90E/0x0052C926: Event robot.detect_gyro_bias.bias_detected at 0x0052C9BC withheld until min,max value bytes exist; axis={name}, rounded f32 bits {Bits(roundedMin)}/{Bits(roundedMax)}");
                    _robot.Engine.EmitSourceLog(EngineLogLevel.Warning,null,"RobotGyroDriftDetector.BiasDetected",FormattableString.Invariant($"Gyro bias detected on {name} axis (min={(double)minDegrees:F2} deg/sec, max={(double)maxDegrees:F2} deg/sec)"));
                    BiasReported=true; // no break; remaining axes still report.
                }
            ResetWindow(); // even a reporting call continues to the current sample below.
        }
        BiasCount=unchecked(BiasCount+1);
        float[] raw={state.Gyro.X,state.Gyro.Y,state.Gyro.Z};
        for(int axis=0;axis<3;axis++)
        {
            float current=raw[axis]*RawFactor,old=Filtered[axis]*OldFactor;float value=old+current;Filtered[axis]=value;
            if(value<Minima[axis])Minima[axis]=value;if(Maxima[axis]<value)Maxima[axis]=value;
        }
    }
    private void ResetWindow()
    {Array.Fill(Minima,float.PositiveInfinity);Array.Fill(Maxima,0f);BiasStart=0;BiasCount=0;}
}
