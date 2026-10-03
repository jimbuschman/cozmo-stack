using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// The test seam for the live path (R-BEH2 batch 2, M7-017). The engine has no pusher of ProceduralLive (0x198): the only
/// route onto the idle stack is <c>AnimationStreamer::PushIdleAnimation</c>, so a test that needs the live animation to stream
/// pushes it there. <see cref="AnimationScheduler.StreamLive"/> no longer pushes it.
/// </summary>
internal static class LiveIdleSeam
{
    /// <summary>
    /// ProceduralLive on top of the idle stack through PushIdleAnimation, and all three tracks reported locked
    /// (AreAnyTracksLocked, the MovementComponent lock set), so the streamer's own UpdateLiveAnimation (0x0057D5F8) generates no
    /// keyframe of its own beside the ones the test appends.
    /// </summary>
    public static void PushLiveQuietly(this AnimationScheduler s, string lockName = "test-live")
    {
        s.LiveIdleInputs.LockedTracks = () => 7;
        s.PushIdleAnimation(AnimationTrigger.ProceduralLive, lockName);
    }
}
