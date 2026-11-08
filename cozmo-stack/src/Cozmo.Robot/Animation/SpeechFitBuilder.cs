using Cozmo.Robot.Behavior;

namespace Cozmo.Robot.Animation;

// SayText fit 0x561636..5616D4, 0x5619F0..561AF8; AppendAnimation 0x577EFC..57805C.
internal static class SpeechFitBuilder
{
    internal static AnimationClip Build(float durationMs, Func<AnimationClip?> choose)
    {
        var keys = new List<Keyframe> { new AudioKeyframe(0, new long[] { 0xBF0BACE1 }, 1f, new[] { 1f }, false) };
        uint latest = 0;
        while (durationMs <= 60000f && latest < durationMs)
        {
            var selected = choose();
            if (selected is null) break; // native retains the inserted audio and any prior appends
            if (selected.Keyframes.Any(k => k is not FaceKeyframe && k is not FaceAnimationKeyframe))
                throw new NotSupportedException("MISSING VOICE-ANIMATION-FIT-1: selected clip has unsupported non-face tracks");
            uint previous = latest;
            uint offset = unchecked(latest + 33u);
            if (offset <= latest) throw new InvalidOperationException("M1-034 VOICE-ANIMATION-FIT-1: fit offset overflow");
            foreach (var key in selected.Keyframes)
            {
                uint at = unchecked(offset + key.TriggerTimeMs);
                if (at < offset) throw new InvalidOperationException("M1-034 VOICE-ANIMATION-FIT-1: fitted keyframe overflow");
                keys.Add(key switch
                {
                    FaceKeyframe face => face with { TriggerTimeMs = at, Pose = face.Pose.Clone() },
                    FaceAnimationKeyframe face => face with { TriggerTimeMs = at },
                    _ => throw new InvalidOperationException()
                });
                if (at > latest) latest = at;
            }
            if (selected.Keyframes.Count == 0)
                throw new InvalidOperationException("M1-034 VOICE-ANIMATION-FIT-1: empty append would not advance the fit");
            if (latest <= previous) throw new InvalidOperationException("M1-034 VOICE-ANIMATION-FIT-1: fit did not advance");
        }
        return new AnimationClip { Name = "SayTextAnimation", IsLive = false,
            Tracks = keys.Aggregate(AnimationTrack.None, (tracks, key) => tracks | key.Track),
            DurationMs = keys.Max(k => k.EndTimeMs), Keyframes = keys.ToArray() };
    }

    internal static AnimationClip? Select(AnimationLibrary library)
    {
        const AnimationTrigger trigger = AnimationTrigger.CozmoSaysSpeakLoop;
        if (!library.HasAnimationForTrigger(trigger)) return null;
        string group = library.GetAnimationForTrigger(trigger);
        string name = library.GetAnimationNameFromGroup(group, strict: false);
        return name.Length == 0 ? null : library.GetAnimation(name);
    }
}
