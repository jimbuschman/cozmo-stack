using System.Text.Json;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Manipulation;

namespace Cozmo.Robot.Behavior;

// Factory 0x0059CFA6, ctor 0x005C9158: IBehavior(config), then the SearchForFace vtable.
// No derived config fields. The shipped VC_SearchForFace.json supplies only common fields.
internal sealed class NativeSearchForFaceBehavior : SteppedBehavior, IDisposable
{
    private readonly EngineRobot _robot;
    private readonly ManipulationSystem _manipulation;
    private long _generation;
    private bool _retired;

    internal int NativeState { get; private set; }

    internal NativeSearchForFaceBehavior(BehaviorFactoryContext source, JsonElement config)
        : base(BehaviorConfigLoader.ExtractIdFromConfig(config).ToString(), "SearchForFace")
    {
        _robot = source.NativeRobot!;
        _manipulation = source.Manipulation!;
    }

    private bool OwnerCurrent => !_retired && !_robot.Lifetime.Destroying &&
        ReferenceEquals(_robot.Engine.Robot, _robot) &&
        _robot.NativeBehaviorRuntime is { } runtime &&
        ReferenceEquals(runtime.Manipulation, _manipulation) &&
        runtime.Bound.TryGetValue(Id, out var actual) && ReferenceEquals(actual, this);

    protected override IDisposable? EnterHostExecutionOwner(BehaviorContext? context) => _robot.Engine.EnterSynchronousOwner();
    protected override void HostRunStarting() => Interlocked.Increment(ref _generation);
    protected override long CaptureHostExecutionVersion() => Interlocked.Read(ref _generation);
    protected override bool HostExecutionVersionCurrent(long version) => OwnerCurrent && version == Interlocked.Read(ref _generation);
    protected override bool HostStopContinuationCurrent(long version) => !_retired && Interlocked.Read(ref _generation) == unchecked(version + 1);
    protected override bool HostResumeContinuationCurrent(long version) => OwnerCurrent &&
        (Interlocked.Read(ref _generation) == version || Interlocked.Read(ref _generation) == unchecked(version + 1));

    // Vtable 0x01027920 +0x20/+0x24/+0x28: 0x005BF04C/0x0059EC12/0x005C93CC.
    protected override bool RunnableGate20(BehaviorContext context) => false;
    protected override bool RunnableGate24(BehaviorContext context) => false;
    protected override bool RunnableGate28(BehaviorContext context) => false;
    protected override bool KeepsRunningWithoutAction => HasCurrentAction;

    public override bool IsRunnable(BehaviorContext context)
    {
        using var owner = EnterHostExecutionOwner(context);
        return OwnerCurrent && IsRunnableBase(context) && IsRunnableInternal(context);
    }

    // 0x005C9170..0x005C9180: HasAnyFaces(0, false), inverted.
    protected override bool IsRunnableInternal(BehaviorContext context) => !_manipulation.Vision.Faces.HasAnyFaces(0, false) ||
        ReadinessBlocked("a face already exists in the current origin; native search has no freshness filter (0x005C9170..80)");

    protected override void OnStart()
    {
        if (!OwnerCurrent || _manipulation.Vision.Faces.HasAnyFaces(0, false))
        {
            InitFailed = true; // 0x005C9188..0x005C9196.
            return;
        }
        Transition(0, AnimationTrigger.ComeHere_SearchForFace);
    }

    protected override void OnUpdate()
    {
        // 0x005C929A..0x005C92BC: only Searching tests for a face. Cancel without a callback.
        if (NativeState == 0 && _manipulation.Vision.Faces.HasAnyFaces(0, false))
        {
            long version = CaptureHostExecutionVersion();
            StopActing(false, false);
            if (HostExecutionVersionCurrent(version))
                Transition(1, AnimationTrigger.ComeHere_SearchForFace_FoundFace);
        }
        // The inherited UpdateInternal tail (0x005BDA56) ends when there is no action.
    }

    private void Transition(int state, AnimationTrigger trigger)
    {
        // Searching 0x005C91CA..0x005C9214, Found 0x005C92F2..0x005C933E:
        // state, name, TriggerAnimationAction(robot, trigger 0x65/0x66, 1, true, 0, 60f, false),
        // StartActing with an empty completion callback.
        NativeState = state;
        long version = CaptureHostExecutionVersion();
        if (!SetStateName(state == 0 ? "SearchingForFace" : "FoundFace", () => HostExecutionVersionCurrent(version)))
            return;
        var action = new TriggerAnimationAction(_manipulation.Robot, new(trigger, 1, true, 0, 60f, false));
        StartActing(action, null, () => HostExecutionVersionCurrent(version));
    }

    protected override void OnStop(BehaviorStopReason reason) => Interlocked.Increment(ref _generation);
    public void Dispose()
    {
        _retired = true;
        Interlocked.Increment(ref _generation);
    }
}
