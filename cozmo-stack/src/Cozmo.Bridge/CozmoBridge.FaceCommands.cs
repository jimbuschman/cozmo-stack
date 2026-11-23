using Cozmo.Robot;

namespace Cozmo.Bridge;

public sealed partial class CozmoBridge
{
    private readonly SemaphoreSlim faceRequests = new(1);

    // fidelity: M1-064 — app exit supplies the enrolment music cleanup.
    private Task<CommandResult> TeachFace(string name, int saveId, CancellationToken token)
    {
        var music = new EnrollmentMusicScope();
        EngineRobot? enrollmentOwner = null;
        long? meetAdmission = null;
        var engine = Robot.Engine;
        void CloseEnrollment()
        {
            music.Close();
            if (enrollmentOwner is not { } owner || !ReferenceEquals(engine.Robot, owner) || owner.Lifetime.Destroying ||
                meetAdmission is not { } admission || owner.NativeBehaviorRuntime is not { } runtime ||
                runtime.MeetCozmoAdmission != admission) return;

            // Host app input for closing the Meet Cozmo view. FaceEnrollmentGame.cs:178–185
            // leads through GameBase.cs:779–805 and ChallengeManager.cs:224–232 to
            // NeedsHub_2018.cs:265–302, 321–330, 507–510. The Hub first resets
            // to Selection/Wait (Robot.cs:643–662), then enters Freeplay
            // (Robot.cs:1767–1784). The host supplies both activity steps at close:
            // G2E 0x92 (0x005A4FA2), 0x93 Wait/-1 (0x0060AAAE), then 0x92 Freeplay.
            if (Robot.DeliverHostFreeplayInput(owner, false))
                Robot.DeliverHostFreeplayInput(owner, true);
        }

        return RequestFaceReport<FaceEnrollmentCompletedReport>(
            handler => Robot.Engine.FaceEnrollmentCompleted += handler,
            handler => Robot.Engine.FaceEnrollmentCompleted -= handler,
            // Wrong-face completion uses the other face's ID and name (StopInternal at 0x005FE2F4).
            report => report.Result == (byte)FaceEnrollmentResultName.SawWrongFace ||
                (report.Name == name && (saveId == 0 || report.Id == saveId)),
            () =>
            {
                enrollmentOwner = engine.Robot;
                var owner = enrollmentOwner;
                // The app selects the Meet activity before G2E 0x87
                // (0x00737CEE..0x00737D3A). Keep the admission scoped to this request.
                engine.PostGameMessage(() =>
                {
                    if (owner is null || token.IsCancellationRequested || owner.Lifetime.Destroying ||
                        !ReferenceEquals(engine.Robot, owner) || owner.NativeBehaviorRuntime is not { } runtime) return;
                    runtime.AdmitHostActivity("MeetCozmoFindFaces");
                    meetAdmission = runtime.MeetCozmoAdmission;
                    if (ReferenceEquals(engine.Robot, owner) && meetAdmission.HasValue)
                        Robot.SetFaceToEnroll(name, saveId, music,
                            () => !token.IsCancellationRequested && runtime.MeetCozmoAdmission == meetAdmission);
                });
            },
            report => new CommandResult("completed", null, report.Result, Enum.GetName((FaceEnrollmentResultName)report.Result),
                Detail: report.Result == 0 ? "Face enrolled." : $"Enrollment ended: {Enum.GetName((FaceEnrollmentResultName)report.Result) ?? report.Result.ToString()}.", Report: report)
            {
                // Host wording follows the unchanged E2G 0x56 result, ID and name
                // (0x005FE520..0x005FE528); it never promotes a failure to success.
                FaceOperation = new { operation = "enroll", outcome = report.Result == 0 ? "enrolled" : "failed",
                    faceId = report.Id, name = report.Name, requestedName = name },
            },
            TimeSpan.FromMinutes(2), token, Robot.CancelFaceEnrollment,
            reported =>
            {
                if (reported) engine.InvokeEngineRequest(() => { CloseEnrollment(); return true; });
                else engine.PostGameMessage(CloseEnrollment);
            });
    }

    private Task<CommandResult> RenameFace(int id, string oldName, string newName, CancellationToken token) =>
        RequestFaceReport<FaceRenamedReport>(
            handler => Robot.Engine.FaceRenamed += handler,
            handler => Robot.Engine.FaceRenamed -= handler,
            report => report.Id == id && report.Name == newName,
            () => Robot.UpdateEnrolledFaceById(id, oldName, newName), report => CommandResult.Confirmed(report) with
            {
                Detail = "Face renamed.",
                FaceOperation = new { operation = "rename", outcome = "renamed", faceId = report.Id, name = report.Name },
            }, TimeSpan.FromSeconds(10), token);

    private Task<CommandResult> EraseFace(int id, CancellationToken token)
    {
        string? lastKnownName = null;
        return RequestFaceReport<FaceErasedReport>(
            handler => Robot.Engine.FaceErased += handler,
            handler => Robot.Engine.FaceErased -= handler,
            report => report.Id == id,
            () =>
            {
                lastKnownName = Robot.Vision.Recognizer.EnrolledFaces.FirstOrDefault(face => face.FaceID == id)?.Name;
                Robot.EraseEnrolledFaceById(id);
            }, report => CommandResult.Confirmed(report) with
            {
                Detail = "Face enrollment erased; continued sightings can register an unnamed guest, possibly reusing the ID.",
                // EraseFace builds an empty report name at 0x00657A98..0x00657ABE.
                // Keep that native report intact; label this pre-request album snapshot.
                FaceOperation = new { operation = "erase", outcome = "erased", faceId = report.Id, lastKnownName },
            }, TimeSpan.FromSeconds(10), token);
    }

    private async Task<CommandResult> RequestFaceReport<T>(Action<Action<T>> subscribe, Action<Action<T>> unsubscribe,
        Predicate<T> matches, Action request, Func<T, CommandResult> result, TimeSpan timeout, CancellationToken token,
        Action? cancel = null, Action<bool>? onExit = null)
    {
        await faceRequests.WaitAsync(token).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            token.ThrowIfCancellationRequested();
            var owner = Robot.Engine.Robot;
            if (owner is null || !Robot.Engine.IsReadyDelivered) return CommandResult.NotReady();

            var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            void Receive(T report)
            {
                if (ReferenceEquals(Robot.Engine.Robot, owner) && matches(report)) completion.TrySetResult(report);
            }

            subscribe(Receive);
            bool reported = false;
            try
            {
                request();
                var report = await completion.Task.WaitAsync(timeout, token).ConfigureAwait(false);
                reported = true;
                return result(report);
            }
            catch (TimeoutException)
            {
                if (ReferenceEquals(Robot.Engine.Robot, owner)) cancel?.Invoke();
                return CommandResult.NoReport();
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                if (ReferenceEquals(Robot.Engine.Robot, owner)) cancel?.Invoke();
                throw;
            }
            finally
            {
                unsubscribe(Receive);
                onExit?.Invoke(reported);
            }
        }
        finally { faceRequests.Release(); }
    }

    private Task<CommandResult> SetFreeplay(bool enabled, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return Task.FromResult(Robot.TryApplyHostFreeplaySetting(enabled) ? CommandResult.Posted() : CommandResult.NotReady());
    }

    // The app's sleep-dialog cancel (PauseManager.cs:392, 403-411, 365-379), supplied by the host.
    private async Task<CommandResult> Wake(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!Robot.Engine.IsReadyDelivered) return CommandResult.NotReady();
        bool pending = await Robot.Engine.CancelChargerSleepAsync().WaitAsync(token).ConfigureAwait(false);
        return pending
            ? CommandResult.Posted()
            : new CommandResult("no-sleep-pending", false, null, null,
                "No going-to-sleep notice is pending; the app shows no sleep dialog, so there is nothing to cancel");
    }
}
