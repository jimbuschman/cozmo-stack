using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Xunit;

namespace Cozmo.Protocol.Tests;

public class NativeBouncerControllerTests
{
    private sealed class Actions : NativeBouncerController.IActionOwner
    {
        public bool IsActing { get; private set; }
        public int Trigger { get; private set; }
        public int Displays { get; private set; }
        public Action<int>? OnTrigger;
        private Action? _done;
        public void StartTrigger(int trigger, Action completed) { Trigger = trigger; _done = completed; IsActing = true; OnTrigger?.Invoke(trigger); }
        public void StartDisplay(NativeBouncerGame game) { Displays++; IsActing = true; }
        public void Finish() { IsActing = false; var callback = _done; _done = null; callback?.Invoke(); }
    }

    [Fact]
    public void ReentrantStopDuringPendingSoundCannotStartOldDisplay()
    {
        var actions=new Actions(); bool current=true;
        var controller=new NativeBouncerController(actions,(_,_)=>{});
        controller.Initialize(128,64,new EngineRandom(1){WordSourceForTest=()=>0u},()=>current);
        var face=new NativeBouncerController.FaceInput(true,true,0,1f);
        for(int i=0;i<4;i++){controller.Update(1,0,face);actions.Finish();}
        controller.Update(1,0,face); // display remains acting while game ticks
        for(int i=0;i<127;i++)controller.Update(1,0,face);
        Assert.True(controller.Game.PendingSound);
        actions.Finish(); int displays=actions.Displays;
        actions.OnTrigger=trigger=>{if(trigger==521){current=false;actions.Finish();}};
        controller.Update(1,0,face);
        Assert.Equal(521,actions.Trigger);
        Assert.Equal(displays,actions.Displays);
    }

    [Fact]
    public void StateNameReentryCannotOverwriteReplacementRun()
    {
        var actions=new Actions(); bool old=true; int calls=0;
        NativeBouncerController? controller=null;
        controller=new(actions,(_,_)=>
        {
            if(++calls!=2)return;
            old=false;
            controller!.Initialize(128,64,new EngineRandom(1),()=>true);
        });
        controller.Initialize(128,64,new EngineRandom(1),()=>old);
        controller.Update(1,0,new(true,true,0,0f)); actions.Finish();
        Assert.Equal(NativeBouncerController.State.GetIn,controller.CurrentState);
    }

    [Fact]
    public void EntryTransitionsOnlyOnOwnedCompletionThenRunsGameplayDuringActions()
    {
        var actions = new Actions();
        var transitions = new List<string>();
        var controller = new NativeBouncerController(actions, (a, b) => transitions.Add(a + ">" + b));
        var random = new EngineRandom(1) { WordSourceForTest = () => 0u };
        controller.Initialize(128, 64, random);
        var face = new NativeBouncerController.FaceInput(true, true, 100, 0f);
        foreach (int trigger in new[] { 8, 10, 14, 16 })
        {
            controller.Update(1f, 100, face);
            Assert.Equal(trigger, actions.Trigger);
            var state = controller.CurrentState;
            controller.Update(1f, 100, face);
            Assert.Equal(state, controller.CurrentState);
            actions.Finish();
        }
        Assert.Equal(NativeBouncerController.State.Play, controller.CurrentState);
        Assert.Equal(new[] { "Init>GetIn", "GetIn>IdeaToPlay", "IdeaToPlay>RequestToPlay", "RequestToPlay>WaitToPlay", "WaitToPlay>Play" }, transitions);
        controller.Update(1f, 100, face);
        Assert.Equal(1, actions.Displays);
        float oldX = controller.Game.BallX;
        controller.Update(1f, 100, face);
        Assert.NotEqual(oldX, controller.Game.BallX);
        Assert.Equal(1, actions.Displays);
    }

    [Fact]
    public void TimeoutAndUnsignedFaceAgeAreCheckedBeforeActionWork()
    {
        var actions = new Actions();
        var controller = new NativeBouncerController(actions, (_, _) => { });
        controller.Initialize(128, 64, new EngineRandom(1));
        var face = new NativeBouncerController.FaceInput(true, true, 0, null);
        Assert.Equal(NativeBouncerController.UpdateResult.Complete, controller.Update(60.01f, 0, face));
        Assert.Equal(NativeBouncerController.UpdateResult.Complete, controller.Update(60f, 5001, face));
        Assert.Equal(0, actions.Trigger);
        Assert.Equal(NativeBouncerController.UpdateResult.Running, controller.Update(60f, 5000, face));
        Assert.Equal(8, actions.Trigger);
    }
}
