using Cozmo.Robot;

namespace Cozmo.Bridge;

public sealed partial class CozmoBridge
{
    private readonly SemaphoreSlim faceRequests = new(1);

    private Task<CommandResult> TeachFace(string name, int saveId, CancellationToken token) =>
        RequestFaceReport<FaceEnrollmentCompletedReport>(
            handler => Robot.Engine.FaceEnrollmentCompleted += handler,
            handler => Robot.Engine.FaceEnrollmentCompleted -= handler,
            // Wrong-face completion uses the other face's ID and name (StopInternal at 0x005FE2F4).
            report => report.Result == (byte)FaceEnrollmentResultName.SawWrongFace ||
                (report.Name == name && (saveId == 0 || report.Id == saveId)),
            () =>
            {
                // The app stages a named enrollment request and selects the Meet activity.
                // G2E 0x87 (0x00737CEE..0x00737D3A), E2G 0x56 (0x005FE520..0x005FE528).
                Robot.ActivateHighLevelActivity("MeetCozmoFindFaces");
                Robot.SetFaceToEnroll(name, saveId);
            },
            report => new("completed", null, report.Result, Enum.GetName((FaceEnrollmentResultName)report.Result), Report: report),
            TimeSpan.FromMinutes(2), token, Robot.CancelFaceEnrollment);

    private Task<CommandResult> RenameFace(int id, string oldName, string newName, CancellationToken token) =>
        RequestFaceReport<FaceRenamedReport>(
            handler => Robot.Engine.FaceRenamed += handler,
            handler => Robot.Engine.FaceRenamed -= handler,
            report => report.Id == id && report.Name == newName,
            () => Robot.UpdateEnrolledFaceById(id, oldName, newName), CommandResult.Confirmed, TimeSpan.FromSeconds(10), token);

    private Task<CommandResult> EraseFace(int id, CancellationToken token) =>
        RequestFaceReport<FaceErasedReport>(
            handler => Robot.Engine.FaceErased += handler,
            handler => Robot.Engine.FaceErased -= handler,
            report => report.Id == id,
            () => Robot.EraseEnrolledFaceById(id), CommandResult.Confirmed, TimeSpan.FromSeconds(10), token);

    private async Task<CommandResult> RequestFaceReport<T>(Action<Action<T>> subscribe, Action<Action<T>> unsubscribe,
        Predicate<T> matches, Action request, Func<T, CommandResult> result, TimeSpan timeout, CancellationToken token,
        Action? cancel = null)
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
            try
            {
                request();
                var report = await completion.Task.WaitAsync(timeout, token).ConfigureAwait(false);
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
            finally { unsubscribe(Receive); }
        }
        finally { faceRequests.Release(); }
    }

    private Task<CommandResult> SetFreeplay(bool enabled, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!Robot.Engine.IsReadyDelivered) return Task.FromResult(CommandResult.NotReady());
        Robot.SetEnableFreeplayActivity(enabled);
        return Task.FromResult(CommandResult.Posted());
    }
}
