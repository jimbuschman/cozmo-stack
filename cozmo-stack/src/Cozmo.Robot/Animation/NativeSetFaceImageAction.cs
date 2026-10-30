using Cozmo.Robot.Behavior;

namespace Cozmo.Robot.Animation;

// SetFaceAction's image path (00563898, 00563D96..00563FA0, 005643EA).
// The child owns actual streaming notifications and inherits the M8-020 gaps.
internal sealed class NativeSetFaceImageAction : ActionRunner
{
    private readonly CozmoRobot _robot;
    private readonly byte[] _pixels;
    private readonly Action<string>? _missing;
    private TriggerAnimationAction? _child;
    internal TriggerAnimationAction? Child => _child;
    internal NativeSetFaceImageAction(CozmoRobot robot, byte[] pixels, Action<string>? missing=null) : base(5, 0)
    { _robot=robot; _pixels=(byte[])pixels.Clone(); _missing=missing; }
    protected override float EngineClockSeconds => _robot.Engine.Timer.SecondsF;
    public override bool CanInterrupt()=>false; // SetFace vtable +0x14 -> 0x0052B0B2
    public override int Init()
    {
        var library=_robot.Animations.ProceduralFaceManager;
        library.SetProceduralFrame33(_pixels);
        var clip=new AnimationClip { Name="SetFaceImageAnimation", IsLive=true,
            Keyframes=new Keyframe[] { new FaceAnimationKeyframe(0,JsonClipLoader.ProceduralAnimName) },
            Tracks=AnimationTrack.Face, DurationMs=33 };
        const string frontier="M8-020 direct PlayAnimationAction: completion union and cached Animation pointer/destructor lifecycle remain incomplete";
        if(_missing is { } missing)missing(frontier);else SteppedBehavior.ReportMissing(frontier);
        if(State==EngineActionResult.Cancelled || DestructionObserved)return 0x03000000;
        _child=new TriggerAnimationAction(_robot,clip,()=>State!=EngineActionResult.Cancelled && !DestructionObserved,_missing,diagnosticReported:true);
        return 0;
    }
    public override uint CheckIfDone() => _child?.Update() ?? 0x03000000u;
    public override void WatcherEnding()
    {
        if(_child is {} child){child.Prep();child.WatcherEnding();_child=null;}
        base.WatcherEnding();
    }
}
