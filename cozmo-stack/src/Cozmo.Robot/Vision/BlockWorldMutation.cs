namespace Cozmo.Robot.Vision;

// fidelity: M11-063, M11-037 — the native engine serializes connection/message work and robot updates
// (UpdateRobotConnection 0x004ED62E before UpdateAllRobots 0x004ED648); the host vision worker shares this gate.
public sealed partial class BlockWorld
{
    internal FrameMutationScope EnterPoseMutation() => new(FrameGate);
    internal readonly struct FrameMutationScope : IDisposable
    {
        private readonly IDisposable _scope;
        internal FrameMutationScope(object frameGate) { _scope=LogCallbackDeferral.EnterGated(frameGate); }
        public void Dispose() => _scope.Dispose();
    }

    private static void DeliverPublic<T>(Action<T>? handlers,T value)
    {
        if(handlers is null)return;
        foreach(Action<T> handler in handlers.GetInvocationList())
            LogCallbackDeferral.Run(()=>handler(value),ex=>StackDiagnostics.Write($"M1-034 BlockWorld public event handler skipped: {ex.GetType().Name}: {ex.Message}"));
    }
    private static void DeliverPublic<T1,T2,T3>(Action<T1,T2,T3>? handlers,T1 first,T2 second,T3 third)
    {
        if(handlers is null)return;
        foreach(Action<T1,T2,T3> handler in handlers.GetInvocationList())
            LogCallbackDeferral.Run(()=>handler(first,second,third),ex=>StackDiagnostics.Write($"M1-034 BlockWorld public event handler skipped: {ex.GetType().Name}: {ex.Message}"));
    }
    private void EmitPublicLog(string text)=>DeliverPublic(Log,text);
}
