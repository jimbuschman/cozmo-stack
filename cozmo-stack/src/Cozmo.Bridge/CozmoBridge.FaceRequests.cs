using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Vision;

namespace Cozmo.Bridge;

public sealed partial class CozmoBridge
{
    private enum FaceRequestKind { Search, ComeHere, Respond }

    // Our policy: accept only a native pose in this origin observed in the last two
    // seconds of robot time. Preserve signed IDs and handle timestamp wraparound.
    private static int? RecentFace(EngineRobot owner, FaceWorld faces)
    {
        if (owner.StoredState is not { } state) return null;
        return faces.GetNativeFaceIds(owner).Select(id => faces.GetNativeFace(owner, id))
            .Where(face => face is { NativeTrackingTranslation: not null, NativeTrackingRotation: not null } &&
                face.HeadPose.Node is not null && unchecked(state.Timestamp - face.LastObservedTimestamp) <= 2000)
            .OrderBy(face => unchecked(state.Timestamp - face!.LastObservedTimestamp)).ThenBy(face => face!.Id)
            .Select(face => (int?)face!.Id).FirstOrDefault();
    }

    private async Task<CommandResult> FaceRequest(FaceRequestKind kind, CancellationToken token)
    {
        var engine = Robot.Engine;
        if (!engine.Options.C2.FaceRequests)
            return new("not-admitted", false, null, null, "C2:face-requests disabled");
        void Log(string text) => engine.EmitStackLog($"{CozmoC2Settings.FaceRequestsName}: {kind}: {text}");
        CommandResult Refuse(string reason)
        {
            Log(reason);
            return new("not-admitted", false, null, null, reason);
        }
        var owner = engine.Robot;
        if (owner?.NativeBehaviorRuntime is not { IsRetired: false, Manipulation: { } m } runtime)
            return CommandResult.NotReady();
        bool Current() => ReferenceEquals(engine.Robot, owner) && !owner.Lifetime.Destroying && !runtime.IsRetired;
        Log("checking for a fresh face");
        if (RecentFace(owner, m.Vision.Faces) is null)
        {
            // VC_SearchForFace refuses any known face at 0x005C9170..80. The shipped
            // FindFaces_socialize scan has its own runnable gate without that condition.
            Log("requesting native FindFaces scan");
            var failure = await runtime.ScanForC2Goal("FindFaces_socialize",
                () => RecentFace(owner, m.Vision.Faces) is not null, token).ConfigureAwait(false);
            if (failure is not null) return Refuse(failure);
        }
        int id;
        Task<AiTurnToFaceActionResult> turn;
        using (engine.EnterSynchronousOwner())
        {
            token.ThrowIfCancellationRequested();
            if (!Current() || RecentFace(owner, m.Vision.Faces) is not { } fresh) return Refuse("fresh face unavailable");
            id = fresh;
            Log($"selected fresh face {id}");
            if (kind == FaceRequestKind.Search) return CommandResult.Confirmed(new { faceId = id });
            if (kind == FaceRequestKind.ComeHere)
            {
                // Native DriveToFace 0x005DA5BC..616 checks the cached timestamp
                // and selects its own smart ID. Check that native cache selects
                // this fresh face before submitting the normal one-run request.
                using var cached = new Pose3d("");
                var origin = owner.GetNativeOrigin(owner.CurrentOriginId);
                uint stamp = m.Vision.Faces.CopyNativeLastFaceTimestamp(origin, true, cached);
                if (!PoseTreeNode.HasSameRoot(cached.Node, origin.Node) ||
                    m.Vision.Faces.GetNativeFaceIds(owner, stamp).FirstOrDefault() != id)
                    return Refuse("native DriveToFace has no matching fresh cached target");
                if (!runtime.RequestHostBehavior("VC_ComeHere", null, out _))
                    return Refuse("native DriveToFace unavailable");
                Log($"submitted native DriveToFace for {id}");
                return CommandResult.Posted() with { Report = new { faceId = id, behavior = "VC_ComeHere" } };
            }
            // TurnTowardsFace 0x0054BE6C may report native success with its
            // face-pose flag clear. Only an actual turn target permits the reply.
            Log($"turning to face {id}");
            turn = Robot.Motion.QueueAiTurnToFaceAsync(id, MathF.PI, token);
        }
        var turned = await turn.ConfigureAwait(false);
        if (!turned.Admitted || turned.OwnerRetired || turned.EngineResult != 0 || turned.FacePoseAvailable != true)
            return Refuse($"could not turn to face {id}: {turned.Detail}");
        Task<CommandResult> response;
        using (engine.EnterSynchronousOwner())
        {
            token.ThrowIfCancellationRequested();
            if (!Current()) return Refuse("robot owner retired after face turn");
            Log($"playing response to face {id}");
            response = FromAction(Robot.Motion.QueueAiAnimationAsync(AnimationTrigger.VC_HowAreYouDoing_AllGood,
                1, true, false, false, false, token));
        }
        var result = await response.ConfigureAwait(false);
        Log($"response ended: {result.Detail}");
        return result with { Report = new { faceId = id, animation = result.Report } };
    }
}
