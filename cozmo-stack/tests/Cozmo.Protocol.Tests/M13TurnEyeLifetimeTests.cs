using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class M13TurnEyeLifetimeTests
{
    private sealed class Release(Action callback) : IDisposable
    {
        public void Dispose() => callback();
    }
    private sealed class Input : IPanTiltRobot, INativePanTiltServices, INativeMotorAckSource
    {
        public object StateGate { get; } = new();
        public bool OnTreads => true;
        public uint OriginId => 1;
        public float PoseAngleAroundZ { get; set; }
        public float HeadAngle => 0;
        public bool BodyMoving { get; set; }
        public bool HeadMoving => false;
        public string OffTreadsName => "OnTreads";
        public SourceLogSink SourceLog => (_,_,_,_) => { };
        internal readonly List<string> Calls = new();
        internal Action? OnEyeRemoval, OnOwnerRelease;
        public IDisposable SubscribeMotorAck(Action<byte> callback) => new Release(() =>
        {
            Calls.Add("C4");
            OnOwnerRelease?.Invoke();
        });
        public uint TurnInPlace(float a,float s,float accel,float tol,ushort half,bool absolute,out byte id) { id=7; return 0; }
        public uint MoveHeadToAngle(float a,float s,float accel,float duration,out byte id) { id=8; return 0; }
        public void RemoveKeepFaceAlive(int duration) => Calls.Add($"keep:{duration}");
        public byte AddEyeShift(byte tag,float x) { Calls.Add($"add:{tag}"); return 9; }
        public void RemoveEyeShift(byte tag,int duration)
        {
            Calls.Add($"remove:{tag}:{duration}");
            OnEyeRemoval?.Invoke();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddedLayerIsRemovedOnceBeforeC4AndBaseTagRelease(bool terminalFailure)
    {
        var input=new Input(); using var turn=new TurnInPlaceAction(input,1,false);
        Assert.Equal(0u,turn.Init()); Assert.Equal((byte)9,turn.Byte0xD9);
        Assert.Equal(new[]{"keep:33","add:0"},input.Calls);
        if(terminalFailure)
        {
            // A terminal no-progress result can retain the eye layer until the owning scope destroys the action.
            turn.HandleMotorActionAck(7);
            input.BodyMoving=true;
            Assert.Equal(PanTiltResult.Running,turn.CheckIfDone());
            input.BodyMoving=false;
            Assert.Equal(PanTiltResult.MotorStoppedMakingProgress,turn.CheckIfDone());
            Assert.Equal((byte)9,turn.Byte0xD9);
        }
        input.Calls.Clear();
        input.OnEyeRemoval=()=>
        {
            Assert.True(System.Threading.Monitor.IsEntered(input.StateGate));
            Assert.Equal((byte)9,turn.Byte0xD9);
            Assert.True(ActionRunnerTagCounter.Global.IsInUse(turn.Tag));
        };
        input.OnOwnerRelease=()=>
        {
            Assert.Equal((byte)0,turn.Byte0xD9);
            Assert.Equal(new[]{"remove:9:0","C4"},input.Calls);
            Assert.True(ActionRunnerTagCounter.Global.IsInUse(turn.Tag));
        };
        turn.Dispose(); turn.Dispose();
        Assert.Equal(new[]{"remove:9:0","C4"},input.Calls);
        Assert.False(ActionRunnerTagCounter.Global.IsInUse(turn.Tag));
    }

    [Theory]
    [InlineData(false,0)]
    [InlineData(false,9)]
    [InlineData(true,0)]
    public void EitherZeroGuardSkipsOnlyTheEyeRemoval(bool useEyes,int tag)
    {
        var input=new Input(); using var turn=new TurnInPlaceAction(input,1,false);
        turn.Byte0xD8=useEyes;
        typeof(TurnInPlaceAction).GetProperty(nameof(TurnInPlaceAction.Byte0xD9))!.SetValue(turn,(byte)tag);
        turn.Dispose();
        Assert.Equal(new[]{"C4"},input.Calls);
        Assert.Equal((byte)tag,turn.Byte0xD9); // No zero store on the skipped branch.
        Assert.False(ActionRunnerTagCounter.Global.IsInUse(turn.Tag));
    }

    [Fact]
    public void CompletionRemovalAt99DoesNotRepeatAtDestruction()
    {
        var input=new Input(); using var turn=new TurnInPlaceAction(input,1,false);
        turn.Init(); turn.HandleMotorActionAck(7);input.PoseAngleAroundZ=1;
        Assert.Equal(0u,turn.CheckIfDone());
        Assert.Equal((byte)0,turn.Byte0xD9);
        turn.Dispose();
        Assert.Equal(new[]{"keep:33","add:0","remove:9:99","C4"},input.Calls);
    }
}
