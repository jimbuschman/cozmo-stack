using Cozmo.Protocol;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
namespace Cozmo.Robot;

// fidelity: M12-002
internal sealed partial class PathComponent
{
    internal bool NativeSpeedChooserEnabled=true;
    private RetainedSubscription? _speedChooserSubscription;
    private void AttachSpeedChooser(EngineRobot owner)
    {
        _speedChooserSubscription?.Dispose();_speedChooserSubscription=null;NativeSpeedChooserEnabled=true;
        if(owner.HasExternalInterface)_speedChooserSubscription=owner.Engine.AnimationMessages.SubscribeMessage(0xE1,false,p=>
        {if(p is SetEnableSpeedChooserMessage message)NativeSpeedChooserEnabled=message.Enable;});
    }
    // 53B9C0: empty vector skips the chooser body/RNG, first strict nearest 3D norm wins.
    internal PathMotionProfile ChooseNativeProfile(EngineRobot owner,IReadOnlyList<Pose3d> goals)
    {
        var profile=PathMotionProfile.Default;
        if(goals.Count==0)return profile;
        var chosen=goals[0];float best=float.MaxValue;
        foreach(var goal in goals)
        {
            using var relative=new Pose3d("");
            // The source ignores the conversion return and uses its output object.
            goal.TryWithRespectTo(owner.GetCheckedMainPose(),relative);
            float d=Norm(relative.Translation);if(d<best){best=d;chosen=goal;}
        }
        if(!NativeSpeedChooserEnabled)return profile;
        float accel=(float)owner.Engine.ContextRandom.RandDblInRange(80d,100d);
        float decel=100f-accel;decel=decel+80f;
        using var selectedRelative=new Pose3d("");chosen.TryWithRespectTo(owner.GetCheckedMainPose(),selectedRelative);
        float distance=Norm(selectedRelative.Translation);
        float scaled=distance*(200f-160f);scaled=scaled/300f;float speed=160f+scaled;
        if(!(speed>160f))speed=160f;
        if(!(speed<200f))speed=200f;
        return profile with{SpeedMmps=speed,AccelMmps2=accel,DecelMmps2=decel,ReverseSpeedMmps=.75f*speed}; //53B8DA stores HEX0x24, not decimal24.
    }
    private static float Norm(Vec3 t)
    {float x=(float)t.X,y=(float)t.Y,z=(float)t.Z;float sum=x*x;sum=sum+y*y;sum=sum+z*z;return MathF.Sqrt(sum);}
}
