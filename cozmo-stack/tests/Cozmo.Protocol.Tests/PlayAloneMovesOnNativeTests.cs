using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Animation;
using Xunit;
using Xunit.Abstractions;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class PlayAloneMovesOnNativeTests
{
    private sealed class PlaybackWitness
    {
        private readonly Dictionary<long,SteppedBehavior.PlaybackObservation> _registrations=new();
        internal void Register(SteppedBehavior.PlaybackObservation value)=>_registrations[value.Generation]=value;
        internal SteppedBehavior.PlaybackObservation? Match(long generation,byte tag)
            =>_registrations.TryGetValue(generation,out var value) && value.Tag==tag ? value : null;
        internal bool WrongTag(long generation,byte tag)=>_registrations.TryGetValue(generation,out var value) && value.Tag!=tag;
        internal SteppedBehavior.PlaybackObservation? MatchWire(AnimationScheduler.StartWireObservation captured,AnimationScheduler.StartWireObservation sent,byte tag)
        {
            if(captured!=sent || sent.Tag!=tag)return null;
            var playback=Match(sent.Generation,tag);
            return playback is { } own && own.Clip==sent.Clip ? own : null;
        }
    }
    private static bool UniqueStartInScope(IReadOnlyList<(RobotMessage Message,AnimationScheduler.StartWireObservation? Start)> sent,long queueId)
        =>sent.Count(x=>x.Message is StartOfAnimation && x.Start?.QueueId==queueId)==1;
    private readonly ITestOutputHelper _output;
    public PlayAloneMovesOnNativeTests(ITestOutputHelper output)=>_output=output;
    [Theory]
    [InlineData(5489u)] [InlineData(123u)]
    public void CanonicalEmptyWorldFreeplayMovesPastPlayAloneOverFiveMinutes(uint seed)
    {
        // Offline firmware acknowledges actual wire commands. SeeFace and FallingStopped are supplied inputs;
        // no activity, child, need level or reaction is forced. The nonzero source RNG seed is a replay input.
        string needsDirectory=Directory.CreateTempSubdirectory("cozmo-playalone-").FullName;
        using var cleanup=new TemporaryDirectory(needsDirectory);
        // Production inputs: the actions below reach UpdateStarsState and Generate (0x0069636E, 0x00696378), which read them.
        using var f=new Fixture(preloadAnimations:true,pumpAnimations:true,engineOptions:NativeRewardCanonicalTests.SourceOptions(needsDirectory,()=>NativeRewardCanonicalTests.FixedClockMicros,1f,0),sourceNvEmpty:true);
        f.Rig.Engine.ContextRandom.SetSeed(seed);
        var original=f.Robot;Assert.Null(original.SendFault);var parent=f.Runtime.FreeplayObserver;var manager=original.BehaviorManagerOwner!;
        var find=Assert.IsType<NativeFindFacesSocialize>(f.Runtime.Bound["FindFaces_socialize"]);
        double start=f.Rig.Engine.Timer.Seconds;
        var transitions=new List<string>();var missing=new List<string>();void Observe(string value)=>missing.Add($"t={f.Rig.Engine.Timer.Seconds-start:F2} child={parent.Current?.Id} behavior={manager.Current?.Id} {value}");
        SteppedBehavior.ResetMissingForTests();SteppedBehavior.MissingReported+=Observe;
        bool sawSocialize=false,sawPlayAlone=false,sawLater=false,sawFind=false,findStopped=false;
        byte activeAnimation=0;int sentMark=f.Rig.Mark();string previous="";string? firstLater=null;
        var introClips=new HashSet<string>();var idleClips=new HashSet<string>();
        var witness=new PlaybackWitness();var matched=new List<(SteppedBehavior.PlaybackObservation Playback,AnimationScheduler.StartWireObservation Wire,bool AfterSee,bool AfterFall)>();
        var unmatched=new List<string>();var mismatches=new List<string>();
        var owners=f.Runtime.Bound.Values.OfType<SteppedBehavior>().Distinct().ToArray();
        foreach(var owner in owners)owner.CapturePlaybackObservationsForTest=true;
        f.Rig.Robot.Animations.Scheduler.CaptureStartWireForTest=true;
        bool seeSent=false,fallSent=false,fallApplied=false,changedAfterSee=false,changedAfterFall=false;
        uint cameraId=1;double nextCamera=start+1;
        string? seeBefore=null,fallBefore=null;double seeAt=double.PositiveInfinity,fallAt=double.PositiveInfinity;int rewards=0;int seeWireMark=int.MaxValue,fallWireMark=int.MaxValue;var stack=new List<string>();
        var eventTrace=new List<string>();
        void OnStack(string line){if(line.Contains("M15-071 Freeplay native pending"))stack.Add(line);}
        void OnNeeds(string line){if(line.StartsWith("needs.action_completed Fall:")){fallApplied=true;fallAt=f.Rig.Engine.Timer.Seconds;fallWireMark=f.Rig.Mark();eventTrace.Add($"t={f.Rig.Engine.Timer.Seconds-start:F2} Fall applied current={manager.Current?.Id} paused={f.Rig.Engine.Needs.IsPaused} connected={f.Rig.Engine.Needs.Connected}");}}
        void OnFallAction(int action)=>eventTrace.Add($"t={f.Rig.Engine.Timer.Seconds-start:F2} sensor NeedsAction={action}");
        void OnFallReport(FallingStoppedReport report)=>eventTrace.Add($"t={f.Rig.Engine.Timer.Seconds-start:F2} sensor report intensity={report.ImpactIntensity}");
        f.Rig.Engine.StackLogLine+=OnStack;f.Rig.Engine.Needs.Log+=OnNeeds;
        f.Rig.Robot.Sensors.NeedsActionCompleted+=OnFallAction;f.Rig.Robot.Sensors.FallingStoppedInternal+=OnFallReport;
        using var reward=f.Rig.Engine.AnimationMessages.SubscribeMessage(0x65,true,_=>rewards++);
        try
        {
            f.Rig.Robot.ActivateHighLevelActivity("Freeplay");
            while(f.Rig.Engine.Timer.Seconds-start<300)
            {
                f.Motors();
                if(f.Rig.Engine.Timer.Seconds>=nextCamera)
                {f.NativeCameraFrame(cameraId,f.Rig.Engine.Timer.Seconds-start>=30?(byte)150:(cameraId&1)==0?(byte)200:(byte)100);cameraId++;nextCamera+=1;} // a still scene lets Pounce get bored (M15-107)
                Assert.Same(original,f.Rig.Engine.Robot);Assert.False(original.Lifetime.Destroying);
                foreach(var owner in owners)foreach(var playback in owner.DrainPlaybackObservationsForTest())witness.Register(playback);
                var successful=f.Rig.Robot.Animations.Scheduler.DrainSentStartWireForTest().ToDictionary(x=>x.QueueId);
                int batchMark=sentMark;var sent=f.Rig.TakeObservedSince(ref sentMark);

                for(int i=0;i<sent.Count;i++)
                {
                    var message=sent[i].Message;
                    if(message is StartOfAnimation begin)
                    {
                        activeAnimation=begin.AnimId;
                        if(sent[i].Start is { } context && UniqueStartInScope(sent,context.QueueId) && successful.TryGetValue(context.QueueId,out var success))
                        {
                            var playback=witness.MatchWire(context,success,begin.AnimId);
                            if(success.Tag!=begin.AnimId || context!=success || witness.WrongTag(success.Generation,begin.AnimId))
                                mismatches.Add($"queue={context.QueueId} tag={begin.AnimId}");
                            else if(playback is { } own)
                                matched.Add((own,success,batchMark+i>=seeWireMark,batchMark+i>=fallWireMark));
                            else if(witness.Match(success.Generation,begin.AnimId) is { } known && known.Clip!=success.Clip)
                                mismatches.Add($"queue={context.QueueId} registeredClip={known.Clip} sentClip={success.Clip}");
                            else unmatched.Add($"queue={context.QueueId} gen={success.Generation} tag={begin.AnimId}");
                        }
                        else if(sent[i].Start is { } ambiguous && !UniqueStartInScope(sent,ambiguous.QueueId))mismatches.Add($"ambiguous queue={ambiguous.QueueId}");
                        else unmatched.Add($"unowned tag={begin.AnimId}");
                        f.Rig.Data(new AnimationStarted{AnimId=activeAnimation});
                    }
                    else if(message is EndOfAnimation)f.Rig.Data(new AnimationEnded{AnimId=activeAnimation});
                }
                string child=parent.Current?.Id??"<null>";string current=manager.Current?.Id??"<null>";
                double elapsed=f.Rig.Engine.Timer.Seconds-start;
                if(!seeSent && elapsed>=80)
                {
                    seeSent=true;seeBefore=current;seeAt=f.Rig.Engine.Timer.Seconds;seeWireMark=f.Rig.Mark();
                    eventTrace.Add($"t={elapsed:F2} SeeFace current={current} paused={f.Rig.Engine.Needs.IsPaused} connected={f.Rig.Engine.Needs.Connected} ownerSame={ReferenceEquals(f.Rig.Engine.Robot,original)}");
                    // Labeled source-action input; this does not synthesize a face or select a child.
                    Assert.True(f.Rig.Engine.Needs.RegisterNeedsActionCompleted("SeeFace"));
                    Assert.True(f.Rig.Engine.Needs.TryGetNativePending(out _)); // weight 0 never reaches the pending store (0x0069638A..A4)
                }
                if(!fallSent && elapsed>=160)
                {
                    fallSent=true;fallBefore=current;
                    eventTrace.Add($"t={elapsed:F2} FallingStopped sent current={current} paused={f.Rig.Engine.Needs.IsPaused} connected={f.Rig.Engine.Needs.Connected} ownerSame={ReferenceEquals(f.Rig.Engine.Robot,original)}");
                    // Actual robot ingress, with supplied impact above the checked 1000 threshold.
                    f.Rig.Data(new FallingStopped{Timestamp=123,DurationMs=100,ImpactIntensity=1500f});
                }
                string state=child+"/"+current;
                if(state!=previous){transitions.Add($"t={f.Rig.Engine.Timer.Seconds-start:F2} {state}");previous=state;}
                sawSocialize|=child=="Socialize";sawPlayAlone|=child=="PlayAlone";
                sawFind|=ReferenceEquals(manager.Current,find);if(sawFind&&!find.EngineRunning)findStopped=true;
                // Require a real running configured candidate owned by the later selected child's chooser.
                if(sawPlayAlone && findStopped && child!="PlayAlone" && parent.Current is {} selected && manager.Current is SteppedBehavior {EngineRunning:true,OwnedRunner: {} runner} running &&
                   (selected.Chooser?.BehaviorIds.Contains(running.Id)==true || selected.InterludeChooser?.BehaviorIds.Contains(running.Id)==true) &&
                   f.Runtime.Bound.TryGetValue(running.Id,out var actual) && ReferenceEquals(actual,running))
                {sawLater=true;firstLater ??= $"t={f.Rig.Engine.Timer.Seconds-start:F2} child={child} behavior={running.Id} runner={runner.GetType().Name}";}
            }
            Assert.True(f.M.Vision.NativeInitializationForTest?.Motion?.Detector.ComparisonCount>0,
                "Supplied F4/F2 frames never reached the native motion comparison gates");
            foreach(var item in matched)
            {
                var playback=item.Playback;var owner=playback.Owner;
                Assert.True(f.Runtime.Bound.TryGetValue(owner.Id,out var actualOwner)&&ReferenceEquals(actualOwner,owner));
                if(owner.Id=="Hiking_FirstLookIntro")introClips.Add(playback.Clip);
                if(owner.Id=="NothingToDo_Idle")idleClips.Add(playback.Clip);
                if(item.AfterSee && playback.StartedAt>seeAt && playback.Running && playback.HasAction && owner is PlayAnimBehavior && owner.Id!=seeBefore)changedAfterSee=true;
                if(item.AfterFall && playback.StartedAt>fallAt && playback.Running && playback.HasAction && owner is PlayAnimBehavior && owner.Id!=fallBefore)changedAfterFall=true;
            }
            string evidence=$"seeSent={seeSent} fallSent={fallSent} fallApplied={fallApplied} changedAfterSee={changedAfterSee} changedAfterFall={changedAfterFall} seeBefore={seeBefore} fallBefore={fallBefore}; events="+string.Join(" | ",eventTrace)+"; transitions="+string.Join(" | ",transitions)+"; decisions="+string.Join(" | ",parent.Decisions.TakeLast(30))+"; missing="+string.Join(" | ",missing);
            _output.WriteLine($"seed={seed} seeSent={seeSent} fallSent={fallSent} fallApplied={fallApplied} changedAfterSee={changedAfterSee} changedAfterFall={changedAfterFall} seeBefore={seeBefore} fallBefore={fallBefore}; events="+string.Join(" | ",eventTrace));
            Assert.True(sawSocialize && sawFind,evidence);Assert.True(sawPlayAlone,evidence);Assert.True(findStopped,evidence);Assert.True(sawLater,evidence);
            var library=f.Rig.Robot.Animations.Library!;
            var introGroup=library.GetGroup("ag_hiking_getin")!;var idleGroup=library.GetGroup("ag_hiking_lookaround")!;
            // The streamer's idle clip can also start while one of these behaviours is current, before or after its own action.
            Assert.Empty(mismatches);Assert.NotEmpty(introClips);Assert.All(introClips,clip=>Assert.Contains(introGroup.Entries,e=>e.Name==clip));
            Assert.NotEmpty(idleClips);Assert.All(idleClips,clip=>Assert.Contains(idleGroup.Entries,e=>e.Name==clip));
            _output.WriteLine("Matched own starts: "+string.Join(" | ",matched.Select(x=>$"owner={x.Playback.Owner.Id} handle={x.Playback.Handle} epoch={x.Playback.CallbackEpoch}/{x.Playback.ActionEpoch} queue={x.Wire.QueueId} generation={x.Wire.Generation} tag={x.Wire.Tag} clip={x.Wire.Clip}")));
            _output.WriteLine("Unmatched wire starts: "+string.Join(" | ",unmatched));
            Assert.True(seeSent,evidence);Assert.True(fallSent,evidence);Assert.True(fallApplied,evidence);
            Assert.True(changedAfterSee,evidence);Assert.True(changedAfterFall,evidence);
            Assert.True(f.Rig.Engine.Needs.TryGetNativePending(out _),evidence);Assert.Empty(stack);_=rewards;
            _output.WriteLine("First later owned action: "+firstLater);
        }
        finally {f.Rig.Robot.Animations.Scheduler.CaptureStartWireForTest=false;foreach(var owner in owners)owner.CapturePlaybackObservationsForTest=false;SteppedBehavior.MissingReported-=Observe;f.Rig.Engine.StackLogLine-=OnStack;f.Rig.Engine.Needs.Log-=OnNeeds;f.Rig.Robot.Sensors.NeedsActionCompleted-=OnFallAction;f.Rig.Robot.Sensors.FallingStoppedInternal-=OnFallReport;}
    }
    [Fact]
    public void PlaybackWitnessRequiresExactGenerationAndTagAcrossReuse()
    {
        using var f=new Fixture();
        var owner=Assert.IsAssignableFrom<SteppedBehavior>(f.Runtime.Bound["Hiking_FirstLookIntro"]);
        var witness=new PlaybackWitness();
        var first=new SteppedBehavior.PlaybackObservation(owner,11,2,3,7,100,"clip-a",1,true,true);
        var later=new SteppedBehavior.PlaybackObservation(owner,12,4,5,7,355,"clip-b",2,true,true);
        witness.Register(first);witness.Register(later);
        Assert.Null(witness.Match(100,8)); // same current clip or owner does not repair a tag mismatch
        Assert.Null(witness.Match(101,7)); // unrelated neutral generation
        Assert.Equal(first,witness.Match(100,7));Assert.Equal(later,witness.Match(355,7));
        var oldWire=new AnimationScheduler.StartWireObservation(1,100,7,"clip-a");
        var newWire=new AnimationScheduler.StartWireObservation(2,355,7,"clip-b");
        Assert.Null(witness.MatchWire(oldWire,newWire,7));
        Assert.Null(witness.MatchWire(oldWire,oldWire,8));
        Assert.Equal(first,witness.MatchWire(oldWire,oldWire,7));
        Assert.Equal(later,witness.MatchWire(newWire,newWire,7));
        var duplicated=new List<(RobotMessage Message,AnimationScheduler.StartWireObservation? Start)>
        { (new StartOfAnimation{AnimId=7},oldWire),(new StartOfAnimation{AnimId=7},oldWire) };
        Assert.False(UniqueStartInScope(duplicated,oldWire.QueueId));
    }

    [Fact]
    public void ActualOwnerWireStartAndReusedTagKeepDistinctBufferedIdentity()
    {
        using var f=new Fixture(preloadAnimations:true,pumpAnimations:true);
        // M3-033 / Robot.Update 0x00513CD8: complete actual queued NV commands before the streaming gate is tested.
        var nv=f.Rig.Engine.NvStorage!;var startup=new List<string>();
        for(int i=0;i<100 && (!nv.IsIdle || !f.Robot.ReadyToStream);i++)
        {
            if(nv.InFlightTag is {} inFlight)
            {
                var command=f.Rig.SentSince(0).OfType<NVCommand>().Last(c=>c.Tag==inFlight);
                startup.Add($"reply={command.Tag:X}/{command.Op}");
                f.Rig.Data(new NVOpResult{Tag=command.Tag,Op=command.Op,Result=command.Op==NvStorageComponent.OpRead?(sbyte)-1:(sbyte)0,Length=0,Data=Array.Empty<byte>()});
            }
            f.Motors();
        }
        Assert.True(f.Robot.FirstFullStateHandled);Assert.True(f.Robot.TimeSynced);
        Assert.True(nv.IsIdle && f.Robot.ReadyToStream,$"idle={nv.IsIdle} ready={f.Robot.ReadyToStream} inFlight={nv.InFlightTag}; {string.Join(" | ",startup)}");
        f.Motors();Assert.True(f.Robot.AnimationStreamingOpen);
        var manager=f.Robot.BehaviorManagerOwner!;
        var owner=Assert.IsAssignableFrom<SteppedBehavior>(f.Runtime.Bound["Hiking_FirstLookIntro"]);
        var scheduler=f.Rig.Robot.Animations.Scheduler;Assert.Null(f.Robot.SendFault);
        owner.CapturePlaybackObservationsForTest=true;scheduler.CaptureStartWireForTest=true;
        var witness=new PlaybackWitness();int mark=f.Rig.Mark();
        Assert.True(manager.StartAsync(owner.Id,f.Rig.Engine.Timer.SecondsF).GetAwaiter().GetResult());
        QueuedSelection.Keep(f.Rig.Robot,manager,owner.Id);
        byte active=0;
        var diagnostic=new List<string>();string priorState="";
        (AnimationScheduler.StartWireObservation Wire,SteppedBehavior.PlaybackObservation? Own) NextStart(Func<AnimationScheduler.StartWireObservation,SteppedBehavior.PlaybackObservation?,bool> wanted)
        {
            for(int tick=0;tick<250;tick++)
            {
                f.Motors();foreach(var value in owner.DrainPlaybackObservationsForTest()) { witness.Register(value);diagnostic.Add($"ticket gen={value.Generation} tag={value.Tag} clip={value.Clip} handle={value.Handle} epoch={value.CallbackEpoch}/{value.ActionEpoch}"); }
                var successes=scheduler.DrainSentStartWireForTest().ToDictionary(x=>x.QueueId);
                var sent=f.Rig.TakeObservedSince(ref mark);
                string state=$"current={manager.Current?.Id} ownerRunning={owner.EngineRunning} action={owner.HasCurrentAction} runner={owner.OwnedRunner?.GetType().Name} timeSynced={f.Robot.TimeSynced} ready={f.Robot.ReadyToStream} open={f.Robot.AnimationStreamingOpen} nv={f.Rig.Engine.NvStorage?.InFlightTag} playing={f.Rig.Robot.Animations.Playing} tag={f.Rig.Robot.Animations.CurrentTag} gen={f.Rig.Robot.Animations.Generation} callbackPending={owner.AwaitingCompletedAnimationCallback}";
                if(state!=priorState){diagnostic.Add($"tick={tick} t={f.Rig.Engine.Timer.SecondsF}: {state}");priorState=state;}
                foreach(var success in successes.Values)diagnostic.Add($"sent identity queue={success.QueueId} gen={success.Generation} tag={success.Tag} clip={success.Clip}");
                foreach(var item in sent)
                {
                    if(item.Message is StartOfAnimation start)
                    {
                        diagnostic.Add($"wire start tag={start.AnimId} scope={item.Start}");
                        active=start.AnimId;f.Rig.Data(new AnimationStarted{AnimId=active});
                        if(item.Start is { } scope && UniqueStartInScope(sent,scope.QueueId) && successes.TryGetValue(scope.QueueId,out var success))
                        {
                            var own=witness.MatchWire(scope,success,start.AnimId);
                            if(wanted(success,own))return(success,own);
                        }
                    }
                    else if(item.Message is EndOfAnimation)f.Rig.Data(new AnimationEnded{AnimId=active});
                }
            }
            string[] logs;lock(f.Rig.Log)logs=f.Rig.Log.TakeLast(30).ToArray();
            throw new Xunit.Sdk.XunitException("No actual successful StartOfAnimation wire message; "+string.Join(" | ",diagnostic.TakeLast(60))+"; logs="+string.Join(" | ",logs));
        }
        var first=NextStart((_,own)=>own is { } value && ReferenceEquals(value.Owner,owner));Assert.NotNull(first.Own);Assert.Same(owner,first.Own!.Value.Owner);
        f.Rig.Robot.ExecuteBehaviorById(byte.MaxValue,-1);
        manager.Stop(BehaviorStopReason.Interrupted,f.Rig.Engine.Timer.SecondsF);
        // Unit-only supplied counter state exercises byte reuse; no chooser or firmware result is forced.
        var counter=typeof(AnimationScheduler).GetField("_tagCounter",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;
        counter.SetValue(scheduler,unchecked((byte)(first.Wire.Tag-1)));
        var neutral=Assert.IsType<AnimationTicket>(f.Rig.Robot.Animations.PlayTracked("anim_neutral_eyes_01"));
        var second=NextStart((wire,_)=>wire.Generation==neutral.Generation);Assert.Equal(first.Wire.Tag,second.Wire.Tag);
        Assert.NotEqual(first.Wire.Generation,second.Wire.Generation);Assert.NotEqual(first.Wire.QueueId,second.Wire.QueueId);
        Assert.Equal(neutral.Generation,second.Wire.Generation);Assert.Null(second.Own);
    }
    [Fact]
    public async Task CompletedTicketCallbackBlocksOnlyTheNextModelTick()
    {
        using var f=new Fixture(pumpAnimations:true);
        var owner=Assert.IsAssignableFrom<SteppedBehavior>(f.Runtime.Bound["FindFaces_socialize"]);
        var pending=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        owner.SetCompletionObservationForTest(Task.CompletedTask,pending.Task);
        var newerRunning=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        owner.SetCompletionObservationForTest(newerRunning.Task,new TaskCompletionSource().Task);
        using var entered=new ManualResetEventSlim();f.BeforeCompletedAnimationWaitForTest=b=>{if(ReferenceEquals(b,owner))entered.Set();};
        long before=f.Rig.NowNs;var step=Task.Run(()=>f.Motors());
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));Assert.Equal(before,f.Rig.NowNs);
        pending.SetResult();await step.WaitAsync(TimeSpan.FromSeconds(5));Assert.Equal(before+60_000_000,f.Rig.NowNs);
        var running=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        owner.SetCompletionObservationForTest(running.Task,new TaskCompletionSource().Task);
        f.BeforeCompletedAnimationWaitForTest=b=>{if(ReferenceEquals(b,owner))throw new InvalidOperationException("running ticket must not be awaited");};
        before=f.Rig.NowNs;f.Motors();Assert.Equal(before+60_000_000,f.Rig.NowNs);
        owner.SetCompletionObservationForTest(Task.CompletedTask,Task.FromException(new InvalidOperationException("already faulted")));
        f.BeforeCompletedAnimationWaitForTest=null;
        var error=Assert.Throws<InvalidOperationException>(()=>f.WaitCompletedAnimationCallbacks());
        Assert.Equal("already faulted",error.Message);
    }
    private sealed class TemporaryDirectory(string path):IDisposable
    {public void Dispose(){try{Directory.Delete(path,true);}catch(IOException){}}}
}
