using System.Text.Json;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;

namespace Cozmo.Robot.Behavior;

// DEV-TURN-IN-PLACE-TEST-1: own path 0x005CA550..0x005CAD80; caller bypass remains UNKNOWN.
// Shared reaction, action and completion owners remain M7-014, M8-001/015/017 and M13-022 gaps.
internal sealed class NativeDevTurnInPlaceTestBehavior : SteppedBehavior, IDisposable
{
    internal sealed class Test(float angle, float speed, float accel, float tolerance)
    {
        internal float Angle = angle, Speed = speed, Accel = accel, Tolerance = tolerance;
        internal byte Runs;
    }
    private static readonly ReactionLockTable AllReactions = new("DevTurnInPlaceTest", "0xC60D40", Enumerable.Range(0, 21).ToArray());
    private readonly ManipulationSystem _m;
    private readonly EngineRobot _robot;
    private readonly List<Test> _tests = new();
    private readonly float _gap;
    private readonly byte _runs;
    private readonly bool _loop;
    private int _index, _retired;
    private long _generation;
    internal IReadOnlyList<Test> Tests => _tests;
    internal int Index => _index;
    internal Action<CompoundActionSequential>? ActionConstructedForTest;

    internal NativeDevTurnInPlaceTestBehavior(BehaviorFactoryContext source, JsonElement config)
        : base(BehaviorConfigLoader.ExtractIdFromConfig(config).ToString(), "DevTurnInPlaceTest")
    {
        _m = source.Manipulation!; _robot = source.NativeRobot!;
        _loop = config.GetProperty("loopForever").GetBoolean();
        _gap = config.GetProperty("gapBetweenTests_s").GetSingle();
        _runs = config.GetProperty("nRunsPerTest").GetByte();
        foreach (var item in config.GetProperty("tests").EnumerateArray())
            _tests.Add(new(item.GetProperty("angle_deg").GetSingle(), item.GetProperty("speed_deg_per_sec").GetSingle(),
                item.GetProperty("accel_deg_per_sec2").GetSingle(), item.GetProperty("tol_deg").GetSingle()));
        _robot.Engine.EmitSourceLog(EngineLogLevel.Info,"Behaviors","BehaviorDevTurnInPlaceTest.LoadFromJson",$"Loaded {_tests.Count} tests from config file.");
        ReportMissing("DevTurnInPlaceTest: actual initial caller bypassing IsRunnable is UNKNOWN; shared reaction/action completion owners remain M7-014/M8-001/M8-015/M8-017/M13-022 gaps");
    }
    protected override IDisposable? EnterHostExecutionOwner(BehaviorContext? context) => _m.Robot.Engine.EnterSynchronousOwner();
    protected override void HostRunStarting() => Interlocked.Increment(ref _generation);
    protected override long CaptureHostExecutionVersion() => Interlocked.Read(ref _generation);
    protected override bool HostExecutionVersionCurrent(long v) => Volatile.Read(ref _retired) == 0 && Interlocked.Read(ref _generation) == v;
    protected override bool HostStopContinuationCurrent(long v) => Volatile.Read(ref _retired) == 0 && Interlocked.Read(ref _generation) == v;
    protected override bool HostResumeContinuationCurrent(long v) => Volatile.Read(ref _retired) == 0 && (Interlocked.Read(ref _generation) == v || Interlocked.Read(ref _generation) == unchecked(v + 1));
    protected override bool RunnableGate20(BehaviorContext context) => false;
    protected override bool RunnableGate24(BehaviorContext context) => false;
    protected override bool RunnableGate28(BehaviorContext context) => false;
    protected override bool IsRunnableInternal(BehaviorContext context) => false;
    protected override bool KeepsRunningWithoutAction => HasCurrentAction;

    internal void ResetTests()
    {
        _index = 0;
        foreach (var test in _tests) test.Runs = 0;
    }
    protected override void OnStart()
    {
        long epoch = CaptureHostExecutionVersion();
        ResetTests();
        if (!Scope.SmartDisableReactionsWithLockGuarded(Id, AllReactions, () => HostExecutionVersionCurrent(epoch))) return;
        GenerateAndStart(epoch);
    }
    private void GenerateAndStart(long epoch)
    {
        if (!HostExecutionVersionCurrent(epoch)) return;
        if ((uint)_index >= (uint)_tests.Count)
        {
            ReportMissing("M1-034 DevTurnInPlaceTest: skip unchecked test read at 0x005CA984; preserve prior state");
            return;
        }
        var test = _tests[_index];
        float factor = BitConverter.UInt32BitsToSingle(0x3C8EFA35);
        var turn = new NativeTurnInPlaceRunner(_m, test.Angle * factor, test.Accel * factor, test.Speed * factor, test.Tolerance * factor);
        var sequence = new CompoundActionSequential(() => _robot.Engine.Timer.SecondsF);
        sequence.AddAction(turn, false, false);
        sequence.AddAction(new NativeTimedWaitRunner(_m, _gap), false, false);
        ActionConstructedForTest?.Invoke(sequence);
        StartActing(sequence, _ => Completed(epoch), () => HostExecutionVersionCurrent(epoch));
    }
    private void Completed(long epoch)
    {
        if (!HostExecutionVersionCurrent(epoch)) return;
        var test = _tests[_index];
        test.Runs = unchecked((byte)(test.Runs + 1));
        if (_runs == 0)
        {
            ReportMissing("DevTurnInPlaceTest: nRunsPerTest=0 reaches unknown uidivmod behavior at 0x005CAC60; callback progression unavailable");
            return;
        }
        if (test.Runs % _runs == 0) ++_index;
        if (_index >= _tests.Count)
        {
            if (!_loop) return;
            _index = 0;
        }
        GenerateAndStart(epoch);
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _retired, 1) != 0) return;
        Interlocked.Increment(ref _generation); RetireActionCompletionOwner();
    }
}