using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;
using Xunit;

namespace Cozmo.Protocol.Tests;

// MEET-COZMO-SCORER-INPUTS-1. The engine's factory constructs every configured class unconditionally
// (BehaviorContainer::CreateBehavior: EnrollFace 0x0059CB44 new 0x180 + ctor, InteractWithFaces 0x0059CCC4
// new 0x140 + ctor, RespondToRenameFace 0x0059CF66 new 0x130 + ctor) and ScoringBSRunnableChooser::
// GetDesiredActiveBehavior calls EvaluateScore on each held behaviour (0x0060A436..0x0060A44A) with no
// unavailable result. Expected candidates and flat scores come from the shipped activities_config.json
// MeetCozmo entry (behaviorChooser.behaviors, lines 23-73), never from the stack.
[Collection("SteppedBehavior missing-report statics")]
public sealed class MeetCozmoScorerInputsTests
{
    private static readonly (string Id, Type Owner)[] ShippedMeetCandidates =
    {
        ("DriveOffCharger", typeof(DriveOffChargerBehavior)),
        ("RespondToRenameFace", typeof(NativeRespondToRenameFace)),
        ("EnrollFace", typeof(NativeEnrollFaceBehavior)),
        ("MeetCozmo_InteractWithFaces", typeof(NativeInteractWithFaces)),
        ("MeetCozmo_FindFaces_Socialize", typeof(NativeFindFacesSocialize)),
        ("PutDownBlock", typeof(NativePutDownBlockBehavior)),
    };

    [Fact]
    public void ProductionBindsEveryShippedMeetCandidateSoNoScoreIsUnbuilt()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations: true, sourceNvEmpty: true);
        var chooser = Assert.IsType<ScoringChooser>(f.Runtime.Tree.Single(a => a.Id == "MeetCozmo").Chooser);
        Assert.Equal(ShippedMeetCandidates.Select(c => c.Id), chooser.BehaviorIds);
        Assert.Empty(chooser.Unbound);
        foreach (var (id, owner) in ShippedMeetCandidates)
            Assert.IsType(owner, f.Runtime.Bound[id]);

        var missing = new List<string>();
        void Capture(string text) { if (text.Contains("MEET-COZMO-ADMISSION-1") || text.Contains("M8-003")) missing.Add(text); }
        SteppedBehavior.MissingReported += Capture;
        try
        {
            f.Rig.Robot.ActivateHighLevelActivity("MeetCozmoFindFaces");
            f.Rig.Tick();
            var decision = chooser.GetDesiredActiveBehavior(f.Robot.BehaviorManagerOwner!.Current, 0, f.Runtime.Context, f.Rig.Engine.Timer.SecondsF);
            Assert.False(decision.InputUnavailable);
            Assert.DoesNotContain(decision.Scores, s => s.Note is "Unavailable" or "NativeAbsent" or "not built");
            Assert.Equal(ShippedMeetCandidates.Select(c => c.Id).OrderBy(id => id), decision.Scores.Select(s => s.Id).OrderBy(id => id));
            Assert.Empty(missing);
        }
        finally { SteppedBehavior.MissingReported -= Capture; }
    }

    [Fact]
    public void StagedRenameOutscoresStagedEnrollmentAndIsTheWinnerTheManagerStarts()
    {
        using var f = new PickupPyramidNativeExecutionTests.Fixture(preloadAnimations: true, sourceNvEmpty: true);
        var manager = f.Robot.BehaviorManagerOwner!;
        f.Rig.Robot.ActivateHighLevelActivity("MeetCozmoFindFaces");
        f.Rig.Tick();
        var rename = Assert.IsType<NativeRespondToRenameFace>(f.Runtime.Bound["RespondToRenameFace"]);
        var enroll = Assert.IsType<NativeEnrollFaceBehavior>(f.Runtime.Bound["EnrollFace"]);
        // Stage both requests synchronously (0x8C rename, 0x87 enrollment) without running the manager.
        using (f.Rig.Engine.EnterSynchronousOwner())
        {
            f.Rig.Engine.AnimationMessages.Deliver(0x8C, false, new NativeRenameFaceRequest(7, "Ada", "Bea"));
            f.Rig.Engine.AnimationMessages.Deliver(0x87, false, new NativeEnrollmentRequest("Cy", 0, 0, true, true, true, null));
        }
        Assert.Equal("Bea", rename.StagedName);
        Assert.Equal("Cy", enroll.StagedName);

        var chooser = Assert.IsType<ScoringChooser>(f.Runtime.Tree.Single(a => a.Id == "MeetCozmo").Chooser);
        var decision = chooser.GetDesiredActiveBehavior(manager.Current, 0, f.Runtime.Context, f.Rig.Engine.Timer.SecondsF);
        Assert.False(decision.InputUnavailable);
        Assert.Same(rename, decision.Behavior);
        // Shipped flatScore 3.0 (rename) and 2.0 (enrollment); a non-running challenger adds a draw in [0, 0.1).
        var renameScore = decision.Scores.Single(s => s.Id == "RespondToRenameFace").Score;
        var enrollScore = decision.Scores.Single(s => s.Id == "EnrollFace").Score;
        Assert.InRange(renameScore, 3.0, 3.1);
        Assert.InRange(enrollScore, 2.0, 2.1);

        // Only the winner's Init (NativeRespondToRenameFace.OnStart) claims its staged request.
        for (int i = 0; i < 10 && rename.StagedName.Length != 0; i++) f.Rig.Tick();
        Assert.Equal("", rename.StagedName);
        Assert.Equal("Cy", enroll.StagedName);
        Assert.False(enroll.EngineRunning);
    }
}
