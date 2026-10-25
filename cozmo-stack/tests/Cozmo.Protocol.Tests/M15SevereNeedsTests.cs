using System.Reflection;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class M15SevereNeedsTests
{
    // Reflection lets the same tests execute the old NeedsManager API without a new-component compile stub.
    private static object? Owner(NeedsManager needs)=>typeof(NeedsManager).GetProperty("SevereNeeds",BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(needs);
    private static void Input(NeedsManager needs,string name,object value)=>Owner(needs)?.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(Owner(needs),value);
    private static int Selected(NeedsManager needs)=>Owner(needs) is {} o?(int)o.GetType().GetProperty("Selected")!.GetValue(o)!:3;
    private static void Update(NeedsManager needs)=>Owner(needs)?.GetType().GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(Owner(needs),null);

    [Theory] [InlineData(1,0x134,0x133,0x132,0x137)] [InlineData(0,0x140,0x13F,0x13E,0x143)]
    public void SetCallsDrivingIdleAndTheCheckedTableBeforePublishing(int id,int start,int loop,int end,int idle)
    {
        // reach: unit (substituted recipient callbacks; actual NeedsManager/SevereNeeds owner and Set).
        // Old: no recipient calls, only HashSet.Add. Checked 0x005730E2/3152/31A0 precede scalar store31B4.
        var needs=new NeedsManager(()=>0);using var owner=Owner(needs) as IDisposable;
        var calls=new List<string>();var values=new List<int>();
        Input(needs,"PushDriving",(Action<AnimationTrigger,AnimationTrigger,AnimationTrigger,string>)((s,l,e,n)=>{Assert.Equal((start,loop,end),((int)s,(int)l,(int)e));Assert.Equal("severe_need_component_lock",n);calls.Add("driving");values.Add(Selected(needs));}));
        Input(needs,"PushIdle",(Action<AnimationTrigger,string>)((t,n)=>{Assert.Equal(idle,(int)t);Assert.Equal("severe_need_component_lock",n);calls.Add("idle");values.Add(Selected(needs));}));
        Input(needs,"DisableReactions",(Action<string,IReadOnlyList<bool>,bool>)((n,t,stop)=>{Assert.Equal("severe_need_component_lock",n);Assert.True(stop);Assert.Equal("011111011011101111010",string.Concat(t.Select(b=>b?'1':'0')));calls.Add("reactions");values.Add(Selected(needs));}));
        needs.SetSevereExpressed((NeedId)id,true);needs.SetSevereExpressed((NeedId)id,true);
        Assert.Equal(new[]{"driving","idle","reactions","driving","idle","reactions"},calls);
        Assert.Equal(new[]{3,3,3,id,id,id},values);Assert.Equal(id,Selected(needs));
    }

    [Fact]
    public void PlayOnlyDisablesItsFalseTableAndOverwritesTheOneSelection()
    {
        // reach: unit (recipient inputs substituted). Old: no reaction calls; Energy and Play could both stay expressed.
        var needs=new NeedsManager(()=>0);using var owner=Owner(needs) as IDisposable;int calls=0;
        needs.SetSevereExpressed(NeedId.Energy,true);
        Input(needs,"PushDriving",(Action<AnimationTrigger,AnimationTrigger,AnimationTrigger,string>)((_,_,_,_)=>Assert.Fail("Play has no driving entry")));
        Input(needs,"PushIdle",(Action<AnimationTrigger,string>)((_,_)=>Assert.Fail("Play has no idle entry")));
        Input(needs,"DisableReactions",(Action<string,IReadOnlyList<bool>,bool>)((_,table,stop)=>{calls++;Assert.Equal(1,Selected(needs));Assert.True(stop);Assert.Equal(21,table.Count);Assert.All(table,Assert.False);}));
        needs.SetSevereExpressed(NeedId.Play,true);
        Assert.Equal(1,calls);Assert.False(needs.IsSevereExpressed(NeedId.Energy));Assert.True(needs.IsSevereExpressed(NeedId.Play));
    }

    [Theory] [InlineData(3)] [InlineData(1)]
    public void ClearAlwaysRemovesAllThreeBeforePublishingNone(int initial)
    {
        // reach: unit (substitutes removal recipients). Old: HashSet.Remove, zero recipient calls even from3.
        var needs=new NeedsManager(()=>0);using var owner=Owner(needs) as IDisposable;
        if(initial!=3)needs.SetSevereExpressed((NeedId)initial,true);
        var calls=new List<string>();
        foreach(var (field,label) in new[]{("RemoveDriving","driving"),("RemoveIdle","idle"),("RemoveReactions","reactions")})
            Input(needs,field,(Action<string>)(name=>{Assert.Equal("severe_need_component_lock",name);Assert.Equal(initial,Selected(needs));calls.Add(label);}));
        needs.SetSevereExpressed(NeedId.Energy,false);
        Assert.Equal(new[]{"driving","idle","reactions"},calls);Assert.Equal(3,Selected(needs));
    }

    private sealed class Live : IDisposable
    {
        internal readonly M4ControlTests.Rig Rig=new();
        internal readonly ManipulationSystem M;
        internal readonly FreeplayStack Stack;
        internal NeedsManager Needs=>Stack.Needs;
        internal Live()
        {
            try
            {
                Rig.ToSynced();Rig.State();M=new(Rig.Robot,Rig.Robot.Vision);
                try
                {
                    var d=new DirectoryInfo(AppContext.BaseDirectory);
                    while(d is not null&&!Directory.Exists(Path.Combine(d.FullName,"re-analysis","obb","assets")))d=d.Parent;
                    Assert.NotNull(d);
                    Stack=FreeplayStack.Create(Path.Combine(d!.FullName,"re-analysis","obb"),Rig.Robot,
                        new BehaviorContext{Robot=Rig.Robot,Triggers=new AnimationTriggerMap()},()=>Rig.Engine.Timer.Seconds,Rig.Robot.Vision,M,withReactions:false);
                }
                catch{M.Dispose();throw;}
            }
            catch{Rig.Dispose();throw;}
        }
        public void Dispose(){try{Stack.Dispose();}finally{try{M.Dispose();}finally{Rig.Dispose();}}}
    }

    [Theory] [InlineData(true,true,0)] [InlineData(false,true,1)] [InlineData(false,false,2)]
    public void WakeApiQueriesBothNeedsAndPrefersRepair(bool repair,bool energy,int expected)
    {
        // reach: production API -> queued G2E166 -> gated subscription -> actual Set recipients/AI update.
        // Query callback observes real NeedsState. Old: no G2E166 subscriber, zero queries, unchanged Play flag.
        using var f=new Live();f.Needs.SetLevel(NeedId.Play,0);f.Needs.SetLevel(NeedId.Repair,repair?0:1);f.Needs.SetLevel(NeedId.Energy,energy?0:1);
        f.Needs.SetSevereExpressed(NeedId.Play,true);var queries=new List<NeedId>();
        Input(f.Needs,"IsAtBracket",(Func<NeedId,NeedBracketId,bool>)((n,b)=>{queries.Add(n);return f.Needs.State.IsNeedAtBracket(n,b);}));
        f.Rig.Robot.NotifyCozmoWakeup();f.Rig.Tick();
        Assert.Equal(new[]{NeedId.Repair,NeedId.Energy},queries.Take(2));
        Assert.True(f.Needs.IsSevereExpressed((NeedId)expected));
        Assert.False(f.Needs.IsSevereExpressed(expected==2?NeedId.Energy:NeedId.Play));
        Assert.Equal(expected,Selected(f.Needs));
    }

    [Fact]
    public void WakeWorksBeforeFreeplayAndTheNativeAiSlotStillClearsIt()
    {
        // reach: production API/G2E/native robot lifetime/AI slot, no Freeplay handle or substituted scalar.
        // Old: no166 subscriber, no severe driving entry and no AI-phase clearing owner.
        using var rig=new M4ControlTests.Rig();rig.ToSynced();rig.State();
        var needs=rig.Engine.Needs;needs.SetLevel(NeedId.Repair,1);needs.SetLevel(NeedId.Energy,0);
        rig.Robot.NotifyCozmoWakeup();rig.Tick();
        Assert.True(needs.IsSevereExpressed(NeedId.Energy));
        Assert.Contains(rig.Robot.Animations.DrivingAnimations.Entries,e=>e.LockName=="severe_need_component_lock");
        needs.SetLevel(NeedId.Energy,1);Assert.True(needs.IsSevereExpressed(NeedId.Energy));
        rig.Tick();Assert.False(needs.IsSevereExpressed(NeedId.Energy));
        Assert.DoesNotContain(rig.Robot.Animations.DrivingAnimations.Entries,e=>e.LockName=="severe_need_component_lock");
        // The absent reaction manager is diagnosed by Set/Clear; no fake reaction state is supplied.
    }

    [Fact]
    public void InterruptedGetInSetsCriticalNeedAndStopDoesNotResetTheSameSelection()
    {
        // reach: unit (OnStop invoked without playing an animation; real owner/condition). Stop lifecycle/action join is substituted.
        // Old: _played=false and Interrupted => no Energy flag. Native 0x005BFF02 has no stop-reason gate.
        var needs=new NeedsManager(()=>0);using var owner=Owner(needs) as IDisposable;
        needs.SetLevel(NeedId.Energy,0);
        var b=new PlayAnimOnNeedsChangeBehavior(needs,"get-in",NeedId.Energy,Array.Empty<AnimationTrigger>());
        var stop=typeof(PlayAnimOnNeedsChangeBehavior).GetMethod("OnStop",BindingFlags.NonPublic|BindingFlags.Instance)!;
        int sets=0;Input(needs,"DisableReactions",(Action<string,IReadOnlyList<bool>,bool>)((_,_,_)=>sets++));
        stop.Invoke(b,new object[]{BehaviorStopReason.Interrupted});
        Assert.True(needs.IsSevereExpressed(NeedId.Energy));Assert.Equal(1,sets);
        stop.Invoke(b,new object[]{BehaviorStopReason.Completed});Assert.Equal(1,sets);
        needs.SetLevel(NeedId.Energy,1);needs.SetSevereExpressed(NeedId.Play,true);
        stop.Invoke(b,new object[]{BehaviorStopReason.Completed});Assert.True(needs.IsSevereExpressed(NeedId.Play));Assert.Equal(2,sets);
    }

    [Fact]
    public void AiClearsOnlyTheSelectedNeedBeforeTheNextBehaviorInit()
    {
        // reach: production engine AI phase after real NeedsState changes, then behavior Init using live scalar.
        // Old: Energy flag already removed by SetLevel; no AI Clear recipients and no driving override at Init.
        using var f=new Live();f.Needs.SetLevel(NeedId.Energy,0);f.Needs.SetSevereExpressed(NeedId.Energy,true);
        f.Needs.SetLevel(NeedId.Energy,1);f.Needs.SetLevel(NeedId.Repair,0);
        Assert.True(f.Needs.IsSevereExpressed(NeedId.Energy)); // bracket changes alone cannot clear Selected
        f.Rig.Tick();Assert.Equal(3,Selected(f.Needs));Assert.False(f.Needs.IsSevereExpressed(NeedId.Repair));
        Assert.DoesNotContain(f.Rig.Robot.Animations.DrivingAnimations.Entries,e=>e.LockName=="severe_need_component_lock");
        var drive=f.Stack.Bound.Values.OfType<DriveOffChargerBehavior>().First(b=>b.Id=="DriveOffCharger");
        using var scope=new BehaviorScope();drive.StartAsync(f.Stack.Context,scope,default).GetAwaiter().GetResult();
        try{Assert.Equal(1,drive.DrivingAnimationsFlag);Assert.Contains(f.Rig.Robot.Animations.DrivingAnimations.Entries,e=>e.LockName==drive.Id);}
        finally{drive.Stop(BehaviorStopReason.Interrupted);}
    }

    [Fact]
    public void DriveOffAndTheSevereLockUseIndependentNamesAndTheSharedSelection()
    {
        // reach: production shared owner/real drive-off Init and driving handler; no substituted scalar.
        // Old: live severe gate unbound -> zero own-name entries and DrivingAnimationsFlag0.
        using var f=new Live();var handler=f.Rig.Robot.Animations.DrivingAnimations;
        var drive=f.Stack.Bound.Values.OfType<DriveOffChargerBehavior>().First(b=>b.Id=="DriveOffCharger");
        using(var scope=new BehaviorScope())
        {
            drive.StartAsync(f.Stack.Context,scope,default).GetAwaiter().GetResult();
            try{Assert.Equal(1,drive.DrivingAnimationsFlag);Assert.Contains(handler.Entries,e=>e.LockName==drive.Id&&e.Animations==new DrivingAnimations((AnimationTrigger)0x97,(AnimationTrigger)0x93,(AnimationTrigger)0x8F));}
            finally{drive.Stop(BehaviorStopReason.Interrupted);}
        }
        f.Needs.SetLevel(NeedId.Energy,0);f.Needs.SetSevereExpressed(NeedId.Energy,true);
        using(var scope=new BehaviorScope())
        {
            drive.StartAsync(f.Stack.Context,scope,default).GetAwaiter().GetResult();
            try{Assert.Equal(0,drive.DrivingAnimationsFlag);Assert.DoesNotContain(handler.Entries,e=>e.LockName==drive.Id);}
            finally{drive.Stop(BehaviorStopReason.Interrupted);}
        }
        Assert.Contains(handler.Entries,e=>e.LockName=="severe_need_component_lock");
        Assert.Equal(NeedId.Energy,f.M.AiExpressedNeedValue!());Assert.Equal(NeedId.Energy,f.Stack.Context.AiExpressedNeedValue!());
    }
}
