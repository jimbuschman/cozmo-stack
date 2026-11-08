using Cozmo.Robot.Animation;
using Cozmo.Robot.Manipulation;

namespace Cozmo.Robot.Behavior;

// The phase-4 action construction at 0x5FD210..0x5FD3A8. The caller owns
// the preceding fresh-enrollment music request and the retained action queue.
internal static class NativeEnrollmentSpeech
{
    internal static CompoundActionSequential? Create(ManipulationSystem system, EngineRobot owner,
        string name, bool merge, Func<bool> current)
    {
        string? resources = owner.Engine.Options.ResourcesPath;
        if (resources is null) throw new NativeActionUnavailable("M14-006 enrollment speech resources unavailable");
        SpeechMetadata metadata;
        try { metadata = SpeechMetadata.Load(resources); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or InvalidOperationException or System.Text.Json.JsonException)
        { throw new NativeActionUnavailable("M14-006 enrollment speech metadata unavailable: " + ex.Message); }

        var sequence = new CompoundActionSequential(() => owner.Engine.Timer.SecondsF);
        bool complete = false;
        try
        {
            if (!AddOrdinary((AnimationTrigger)0x10D)) return null;
            if (merge)
            {
                if (!AddSpeech("Name_Normal", (AnimationTrigger)0x112)) return null;
            }
            else
            {
                // Both provider operations are constructed now, before the
                // first child can be updated by the sequence.
                if (!AddSpeech("Name_FirstIntroduction_1", (AnimationTrigger)0x10F)) return null;
                if (!AddSpeech("Name_FirstIntroduction_2", (AnimationTrigger)0x110)) return null;
                if (!AddOrdinary((AnimationTrigger)0x111)) return null;
            }
            if (!current()) return null;
            complete = true;
            return sequence;
        }
        finally
        {
            if (!complete) sequence.RetireNeverAdmittedTree();
        }

        bool Add(ActionRunner child)
        {
            if (!current()) { child.RetireNeverAdmittedTree(); return false; }
            if (sequence.AddActionGuarded(child, false, current)) return true;
            child.RetireNeverAdmittedTree();
            return false;
        }
        bool AddOrdinary(AnimationTrigger trigger)
        {
            var child = new TriggerAnimationAction(system.Robot, new(trigger, 1, true, 0, 60f, false));
            if (!current()) { child.RetireNeverAdmittedTree(); return false; }
            child.UseOrdinaryAuthoredAudio(owner);
            return Add(child);
        }
        bool AddSpeech(string intent, AnimationTrigger trigger)
        {
            if (!metadata.TrySelect(intent, name, owner.Engine.ContextRandom, out var selection))
                throw new NativeActionUnavailable("M14-006 enrollment SayText intent unavailable: " + intent);
            if (!current()) return false;
            var child = new SayTextAction(system.Robot, owner, name, selection.Style, selection.Duration, selection.Pitch);
            if (!current()) { child.RetireNeverAdmittedTree(); return false; }
            child.SetCannedTrigger(trigger);
            return Add(child);
        }
    }
}
