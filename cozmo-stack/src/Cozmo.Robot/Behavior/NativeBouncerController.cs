using Cozmo.Robot.Animation;

namespace Cozmo.Robot.Behavior;

// BOUNCER-1 own state machine, supplied-input boundary only. Production must bind
// the actual owned action lifecycle, face field, and image renderer before use.
internal sealed class NativeBouncerController
{
    internal enum State { Init, GetIn, IdeaToPlay, RequestToPlay, WaitToPlay, Play, Timeout, ShowScore, GetOut, Complete }
    internal enum UpdateResult { Running = 1, Complete = 2 }
    internal readonly record struct FaceInput(bool Valid, bool Found, uint LastObserved, float? DirectionRadians);

    // The callback is the native void callback: it is invoked by the owning
    // action lifecycle when completion is delivered, including action failure.
    internal interface IActionOwner
    {
        bool IsActing { get; }
        void StartTrigger(int trigger, Action completed);
        void StartDisplay(NativeBouncerGame game);
    }

    private readonly IActionOwner _actions;
    private readonly Action<string, string> _stateName;
    private Func<bool> _current = static ()=>false;
    internal NativeBouncerGame Game { get; } = new();
    internal State CurrentState { get; private set; }

    internal NativeBouncerController(IActionOwner actions, Action<string, string> stateName)
    { _actions = actions; _stateName = stateName; }

    // 005F14CE..005F15AE. Lock/idle scope is owned by the native lifecycle.
    internal void Initialize(uint width, uint height, EngineRandom robotRandom, Func<bool>? current = null)
    {
        _current=current ?? (static ()=>true);
        var run=_current;
        if(!run())return;
        Game.Initialize(width, height, robotRandom);
        Transition(State.GetIn,run);
    }

    private void Transition(State next,Func<bool> run)
    {
        if(!run())return;
        if (next == CurrentState) return;
        _stateName(CurrentState.ToString(), next.ToString()); // 005F11FA before state store
        if(!run())return;
        CurrentState = next;
    }

    private void Start(int trigger, State next,Func<bool> run) => _actions.StartTrigger(trigger, () => Transition(next,run));

    // 005F19A4..005F1ABE. Caller supplies the actual image clock and selected
    // SmartFaceID lookup; the optional direction is required only in Play.
    internal UpdateResult Update(float runningSeconds, uint imageTimestamp, FaceInput face)
    {
        var run=_current;
        if(!run())return UpdateResult.Running;
        if (runningSeconds > 60f || !face.Valid || !face.Found || unchecked(imageTimestamp - face.LastObserved) >= 5001u)
            return UpdateResult.Complete;
        switch (CurrentState)
        {
            case State.GetIn: if (!_actions.IsActing) Start(8, State.IdeaToPlay,run); break;
            case State.IdeaToPlay: if (!_actions.IsActing) Start(10, State.RequestToPlay,run); break;
            case State.RequestToPlay: if (!_actions.IsActing) Start(14, State.WaitToPlay,run); break;
            case State.WaitToPlay: if (!_actions.IsActing) Start(16, State.Play,run); break;
            case State.Play:
                if (face.DirectionRadians is not { } direction)
                    throw new InvalidOperationException("MISSING: Bouncer tracked face +0xDC direction input");
                Game.UpdatePaddle(direction);
                Game.UpdateBall();
                if (!_actions.IsActing && Game.PendingSound)
                {
                    Game.ClearPendingSound();
                    Start(521, State.Play,run);
                }
                if(!run())return UpdateResult.Running;
                if (!_actions.IsActing) _actions.StartDisplay(Game);
                break;
            case State.Timeout: if (!_actions.IsActing) Start(15, State.ShowScore,run); break;
            case State.ShowScore: if (!_actions.IsActing) Start(11, State.GetOut,run); break;
            case State.GetOut: if (!_actions.IsActing) Start(9, State.Complete,run); break;
            case State.Complete: if (!_actions.IsActing) return UpdateResult.Complete; break;
        }
        return UpdateResult.Running;
    }
}
