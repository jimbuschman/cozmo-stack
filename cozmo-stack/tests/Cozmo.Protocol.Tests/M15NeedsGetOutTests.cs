using System.Reflection;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class M15NeedsGetOutTests
{
    private sealed class Fixture : IDisposable
    {
        internal readonly M4ControlTests.Rig Rig = new();
        private readonly string _root = Directory.CreateTempSubdirectory("needs-get-out-").FullName;
        private readonly List<ActionRunner> _actions = new();
        internal CozmoRobot Robot => Rig.Robot;
        internal SevereNeedsComponent Severe => Robot.Engine.Needs.SevereNeeds;
        internal Fixture()
        {
            try
            {
                Rig.ToSynced();
                Directory.CreateDirectory(Path.Combine(_root,"animations"));
                File.WriteAllText(Path.Combine(_root,"animations","a.json"),"{\"a\":[]}");
                Directory.CreateDirectory(Path.Combine(_root,"animationGroups"));
                File.WriteAllText(Path.Combine(_root,"animationGroups","a.json"),"{\"Animations\":[{\"Name\":\"a\",\"Weight\":1,\"Mood\":\"Default\",\"CooldownTime_Sec\":0}]}");
                Directory.CreateDirectory(Path.Combine(_root,"animationGroupMaps"));
                File.WriteAllText(Path.Combine(_root,"animationGroupMaps","AnimationTriggerMap.json"),
                    "{\"Pairs\":["+string.Join(",",new[]{0x141,0x135,0x13A}.Select(t=>"{\"CladEvent\":\""+((AnimationTrigger)t)+"\",\"AnimName\":\"a\"}"))+"]}");
                Robot.Animations.ManualTicking=true;
                Robot.Animations.LoadFrom(_root);
            }
            catch { Dispose(); throw; }
        }
        internal ActionRunner New()
        {
            // Reflection keeps old-code runs executable: old code has no such concrete action, rather than a compile error.
            var type=typeof(CozmoRobot).Assembly.GetType("Cozmo.Robot.Animation.PlayNeedsGetOutAnimIfNeeded");
            Assert.NotNull(type);
            var action=(ActionRunner)Activator.CreateInstance(type!,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{Robot},null)!;
            _actions.Add(action);return action;
        }
        internal static DrivingAnimationRequest Request(ActionRunner action)=>(DrivingAnimationRequest)action.GetType().BaseType!
            .GetProperty("Request",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(action)!;
        internal void Send(RobotMessage message)=>Robot.Engine.Broadcast(message);
        public void Dispose()
        {
            try { foreach(var action in _actions){action.Prep();action.WatcherEnding();} }
            finally { try { Rig.Dispose(); } finally { Directory.Delete(_root,true); } }
        }
    }

    [Theory] [InlineData(0,0x141)] [InlineData(1,0x135)] [InlineData(2,0x13A)]
    public void InitReadsTheCurrentSelectionAndUsesTheCheckedTrigger(int selected,int trigger)
    {
        // reach: unit (direct concrete Init; authored one-clip groups; app union20 admission MISSING at 0x005279A8).
        // Old: no get-out action/trigger exists (reflection returns null). Row73 0x00544FA2/0x005450BA..E4.
        using var f=new Fixture();var action=f.New();
        var initial=Fixture.Request(action);
        Assert.Equal((0x23F,1,true,0,0x42700000,false),((int)initial.Trigger,initial.Argument2,initial.Argument3,initial.Argument4,BitConverter.SingleToInt32Bits(initial.Argument5),initial.Argument6));
        Assert.Equal((0x1A,0u,"PlayNeedsGetOut"),(action.Type,action.RequiredTrackMask,action.NativeName));
        f.Robot.Engine.Needs.SetSevereExpressed((NeedId)selected,true);
        Assert.Equal(0,action.Init());Assert.Equal(trigger,(int)Fixture.Request(action).Trigger);
        Assert.Equal(selected,f.Severe.Selected); // Init itself never clears.
    }

    [Fact]
    public void NoneSucceedsWithCountAndNeverStartsAnAnimation()
    {
        // reach: unit (direct Init/Check; missing app union20 join). Old: action absent, not a successful no-animation action.
        using var f=new Fixture();var action=f.New();long generation=f.Robot.Animations.Generation;
        Assert.Equal(0,action.Init());Assert.Equal(0u,action.CheckIfDone());
        Assert.Equal(0x23F,(int)Fixture.Request(action).Trigger);Assert.Equal(generation,f.Robot.Animations.Generation);
        Assert.Equal(0,f.Robot.Engine.AnimationMessages.Count(0x5F,true));
    }

    [Fact]
    public void FirstCheckAfterStartClearsOnceAndEndedWinsOverFailed()
    {
        // reach: unit (direct Init/Check, real CA/CB/5F callbacks; authored assets; app admission MISSING).
        // Old: no concrete action clears the selected need; Energy stays1. 0x005451C8..0x0054520E.
        using var f=new Fixture();var action=f.New();f.Robot.Engine.Needs.SetSevereExpressed(NeedId.Energy,true);
        Assert.Equal(0,action.Init());int clears=0;f.Severe.RemoveReactions=_=>clears++;
        Assert.Equal(0x01000000u,action.CheckIfDone());Assert.Equal(1,f.Severe.Selected);Assert.Equal(0,clears);
        byte tag=f.Robot.Animations.CurrentTag;f.Send(new AnimationStarted{AnimId=tag});
        Assert.Equal(1,f.Severe.Selected); // callback only stores started; Check does the Clear.
        Assert.Equal(0x01000000u,action.CheckIfDone());Assert.Equal(3,f.Severe.Selected);Assert.Equal(1,clears);
        f.Robot.Engine.Needs.SetSevereExpressed(NeedId.Play,true);
        Assert.Equal(0x01000000u,action.CheckIfDone());Assert.Equal(2,f.Severe.Selected);Assert.Equal(1,clears);
        f.Robot.Engine.AnimationMessages.Deliver(0x5F,true,(uint)tag);
        Assert.Equal(0x03000001u,action.CheckIfDone());
        f.Send(new AnimationEnded{AnimId=tag});Assert.Equal(0u,action.CheckIfDone());
        action.Prep();action.WatcherEnding();Assert.Equal(2,f.Severe.Selected);Assert.Equal(1,clears);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void CleanupBeforeStartClearsTheCurrentNeedEvenWithoutInit(bool initialize)
    {
        // reach: unit (direct cancellation/destructor; app union20 admission MISSING).
        // Old: no get-out cleanup; current Play remains2. 0x0054504C..62 checks current selection, not Init's.
        using var f=new Fixture();var action=f.New();f.Robot.Engine.Needs.SetSevereExpressed(NeedId.Energy,true);
        if(initialize)Assert.Equal(0,action.Init());
        f.Robot.Engine.Needs.SetSevereExpressed(NeedId.Play,true);action.Cancel();action.Prep();action.WatcherEnding();
        Assert.Equal(3,f.Severe.Selected);
    }

    [Fact]
    public void PublicEntryReportsTheMissingAppAdmissionWithoutInventingATag()
    {
        // reach: production API entry, gap diagnostic only; NOT proof of tag/RobotCompletedAction delivery.
        // Old: public method absent (null). Checked row73 permits MISSING when animation API admission does not exist.
        using var f=new Fixture();var method=typeof(CozmoRobot).GetMethod("PlayNeedsGetOutAnimIfNeeded");Assert.NotNull(method);
        var error=Assert.Throws<TargetInvocationException>(()=>method!.Invoke(f.Robot,null));
        Assert.IsType<InvalidOperationException>(error.InnerException);Assert.Contains("QueueSingleAction union20 admission",error.InnerException!.Message);
    }
}
