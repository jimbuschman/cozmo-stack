using Cozmo.Robot.Behavior;

namespace Cozmo.Robot.Animation;

// fidelity: M5-038
/// <summary>The checked factory's M8 TriggerAnimationAction constructor arguments.</summary>
internal sealed record SleepAnimationArguments(AnimationTrigger Trigger, int NumLoops, bool Interrupt,
    AnimationTrack Locks, float TimeoutSeconds, bool Strict);

internal static class GoToSleepAnimation
{
    // Concrete TriggerAnimationAction lifecycle belongs to M8. The existing behaviour helper is an asynchronous
    // stand-in, not an ActionRunner that the source compound can own; do not silently substitute it.
    internal static CompoundActionParallel? Create(CozmoRobot host, Func<SleepAnimationArguments, ActionRunner?>? triggerFactory)
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
                sequence.WatcherEnding();
                return null;
            }
            sequence.AddAction(child, false, false);
        }
        var parallel = new CompoundActionParallel(() => host.Engine.Timer.SecondsF);
        parallel.AddAction(sequence, false, false);
        host.Motion.AddGoToSleepLiftChild(parallel); // M4-028 exact preset-0 / tolerance-5 child.
        return parallel;
    }
}
