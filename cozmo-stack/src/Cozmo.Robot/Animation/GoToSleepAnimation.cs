using Cozmo.Robot.Behavior;

namespace Cozmo.Robot.Animation;

// fidelity: M5-038
/// <summary>The checked factory's M8 TriggerAnimationAction constructor arguments.</summary>
internal sealed record SleepAnimationArguments(AnimationTrigger Trigger, int NumLoops, bool Interrupt,
    AnimationTrack Locks, float TimeoutSeconds, bool Strict);

internal static class GoToSleepAnimation
{
    internal static CompoundActionParallel? CreateOwned(CozmoRobot host, EngineRobot owner,
        Func<SleepAnimationArguments, ActionRunner?>? factoryOverride)
    {
        return Create(host, factoryOverride ?? BuildTrigger, owner);

        ActionRunner BuildTrigger(SleepAnimationArguments args)
        {
            // 0x0052CEDE, 0x0052CF14 and 0x0052CF4A use the ordinary trigger-action constructor.
            var child = new TriggerAnimationAction(host, new DrivingAnimationRequest(
                args.Trigger, args.NumLoops, args.Interrupt, (int)args.Locks, args.TimeoutSeconds, args.Strict));
            child.SetScopedOwner(owner);
            return child;
        }
    }

    private static CompoundActionParallel? Create(CozmoRobot host, Func<SleepAnimationArguments, ActionRunner?>? triggerFactory, EngineRobot? owner)
    {
        var sequence = new CompoundActionSequential(() => host.Engine.Timer.SecondsF);
        foreach (ushort trigger in new ushort[] { 0xD2, 0xD5, 0xD4 })
        {
            var args = new SleepAnimationArguments((AnimationTrigger)trigger, 1, true, AnimationTrack.None,
                BitConverter.Int32BitsToSingle(0x42700000), false);
            var child = triggerFactory?.Invoke(args);
            if (child is null)
            {
                host.Engine.EmitStackLog("MISSING: M5-038 -> M8 TriggerAnimationAction concrete ActionRunner lifecycle (factory call 0x0052CEDE); no checked reusable recipient");
                sequence.RetireNeverAdmittedTree();
                return null;
            }
            sequence.AddAction(child, false, false);
        }
        var parallel = new CompoundActionParallel(() => host.Engine.Timer.SecondsF);
        parallel.AddAction(sequence, false, false);
        host.Motion.AddGoToSleepLiftChild(parallel, owner); // M4-028 exact preset-0 / tolerance-5 child.
        return parallel;
    }

    internal static CompoundActionParallel? Create(CozmoRobot host, Func<SleepAnimationArguments, ActionRunner?>? factory)
        => Create(host, factory, null);
}
