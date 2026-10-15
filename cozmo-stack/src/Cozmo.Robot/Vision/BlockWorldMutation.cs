namespace Cozmo.Robot.Vision;

// fidelity: M11-063, M11-037 — the native engine serializes connection/message work and robot updates
// (UpdateRobotConnection 0x004ED62E before UpdateAllRobots 0x004ED648); the host vision worker shares this gate.
public sealed partial class BlockWorld
{
    internal FrameMutationScope EnterPoseMutation() => new(FrameGate);
    internal readonly struct FrameMutationScope : IDisposable
    {
        private readonly object _frameGate;
        internal FrameMutationScope(object frameGate) { _frameGate=frameGate;Monitor.Enter(frameGate); }
        public void Dispose() => Monitor.Exit(_frameGate);
    }
}
