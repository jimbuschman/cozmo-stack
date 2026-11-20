using System.Diagnostics;
using System.Text.Json;
using Cozmo.Bridge;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Emulation;
using Xunit;
using Xunit.Abstractions;
using Fixture=Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class PhysicalReactionRecordedReplayTests(ITestOutputHelper output)
{
    private sealed record Packet(double At,RobotMessage Message);
    private sealed class Gate():ActionRunner(8,0)
    {
        protected override string ActionName=>"initial native reaction gate";
        public override bool CanInterrupt()=>false;
        public override uint CheckIfDone()=>EngineActionResult.Running;
    }
    [Theory]
    [InlineData("slope",554800,569500)]
    [InlineData("impact-low",67000,76000)]
    [InlineData("impact-high",540800,549000)]
    [InlineData("cliff",112900,138400)]
    [InlineData("cliff-preempt",68000,71000)]
    [InlineData("cliff-late",79000,82500)]
    [InlineData("unexpected",592800,599000)]
    public void RecordedWindow(string label,double from,double to)
    {
        var bundle=Environment.GetEnvironmentVariable("COZMO_REACTIONS_REPLAY_BUNDLE");
        if(string.IsNullOrWhiteSpace(bundle))return;
        var packets=new List<Packet>();byte capturedLocks=0;
        foreach(string line in File.ReadLines(Path.Combine(bundle,"messages.jsonl")))
        {
            using var doc=JsonDocument.Parse(line);var r=doc.RootElement;
            bool outbound=r.GetProperty("outbound").GetBoolean();string? tag=r.GetProperty("tag").GetString();
            double at=r.GetProperty("atMs").GetDouble();if(at>to)continue;
            if(outbound && tag is "0x9D" or "0x9E")
            {
                byte mask=Convert.FromHexString(r.GetProperty("payloadHex").GetString()!)[1];
                if(at<from){capturedLocks=tag=="0x9D"?(byte)(capturedLocks|mask):(byte)(capturedLocks&~mask);continue;}
                packets.Add(new(at,tag=="0x9D"?new DisableAnimTracks{Field0=mask}:new EnableAnimTracks{Field0=mask}));
            }
            else if(!outbound && at>=from && tag is "0xF0" or "0xF1" or "0xF4" or "0xD1" or "0xC0" or "0xC1" or "0xD4" or "0xDD" or "0xDE")
                packets.Add(new(at,RobotMessage.Parse(Convert.FromHexString(r.GetProperty("payloadHex").GetString()!))));
        }
        Assert.NotEmpty(packets);
        using var sink=new SilentSink();
        var options=new CozmoEngineOptions{ResourcesPath=PickupPyramidNativeExecutionTests.Resources(),BlockPoolPath="",SpeechSoundBankDirectory=GraphRig.FindMeta(),
            SpeechAudioArchivePath=Path.Combine(WwiseAssets.SoundDir!,"AudioAssets.zip")};
        using var f=new Fixture(preloadAnimations:true,pumpAnimations:true,sourceNvEmpty:true,keepNeedsFull:true,
            engineOptions:options,engineCreated:e=>e.RuntimeAudioSinkForTest=()=>sink,bodyHwVersion:4);
        var rig=f.Rig;var manager=f.Robot.BehaviorManagerOwner!;
        using var bridge=label=="unexpected"?new CozmoBridge(rig.Robot):null;
        rig.Robot.SetEnableFreeplayActivity(false);rig.Tick(4000);manager.Activity=null;
        f.Robot.ActionList.QueueAction(QueueActionPosition.Now,new Gate());
        manager.CheckReactions(rig.Engine.Timer.Seconds);f.Robot.ActionList.Clear();
        double anchor=rig.Engine.Timer.TimeStampMs;
        double At()=>from+rig.Engine.Timer.TimeStampMs-anchor;
        var trace=new List<string>();var clips=new List<(string? Owner,AnimationClipInitializedObservation Clip)>();
        var reports=new List<ActionCompletionReport>();var reactions=new List<ReactionTrigger>();var gaps=new List<string>();
        var rightingMarkers=new HashSet<string>();
        var responseSnapshots=new List<(int Obstacles,byte LocalizationPending)>();
        rig.Robot.Sensors.UnexpectedMovementDetectedInternal+=_=>responseSnapshots.Add((
            f.M.World.LocatedObjects.Count(o=>o.Type==ObjectType.CollisionObstacle),f.Robot.VisionPoseUpdated));
        SteppedBehavior.ResetMissingForTests();
        rig.Engine.StackLogLine+=line=>
        {
            if(!line.Contains("MISSING",StringComparison.Ordinal))return;
            gaps.Add(line);
            // The recorded OnBack successor starts after the cliff is interrupted. Its
            // separate legacy righting path is outside the five reaction classes in this job.
            // Never permit this marker for one of the newly native physical reactions.
            // SwitchToBehaviorBase initializes the incoming body before publishing Current.
            // Its own engine-running flag identifies this constructor/run boundary.
            if(label=="cliff-late" && manager.Reactions.Any(r=>r.Behavior is ReactToRobotOnBackBehavior {EngineRunning:true}) && line.StartsWith(
                "MISSING: M8-015 host action producer at IBehavior::StartActing 0x005BDACC",StringComparison.Ordinal))rightingMarkers.Add(line);
        };
        foreach(var behavior in manager.Reactions.Select(r=>r.Behavior).OfType<SteppedBehavior>().Distinct())
            behavior.Step+=text=>trace.Add($"{At():F1} {behavior.Id}: {text}");
        manager.ReactionTriggered+=r=>{reactions.Add(r.Trigger);trace.Add($"{At():F1} REACTION {r.Trigger}");};
        rig.Engine.ActionCompleted+=a=>{reports.Add(a);trace.Add($"{At():F1} RESULT {a.Tag} {a.Result:X8} {a.AnimationName}");};
        rig.Robot.Animations.Scheduler.Diagnostic+=o=>{if(o is AnimationClipInitializedObservation clip){clips.Add((manager.Current?.Id,clip));trace.Add($"{At():F1} ANIM owner={manager.Current?.Id} {o}");}};
        // Declared context model: the recording's aggregate motion locks, reconstructed from
        // LockTracks640098 / UnlockTracks63FE5C wire transitions. Original owners/normal actions
        // are not replayed. The new reaction still owns and releases its own real native locks.
        byte appliedLocks=0;
        void ApplyContextLocks()
        {
            byte wanted=(byte)(capturedLocks&7),add=(byte)(wanted&~appliedLocks),remove=(byte)(appliedLocks&~wanted);
            if(add!=0)rig.Robot.Motion.LockTracks(add,"captured-context","recorded aggregate context");
            if(remove!=0)rig.Robot.Motion.UnlockTracks(remove,"captured-context");
            appliedLocks=wanted;
        }
        ApplyContextLocks();
        // Explicit firmware model: each counted audio/silence/end frame is consumed at
        // 744/22320 seconds. C11/C12's exact byte/frame accounting comes from send diagnostics.
        // Replenishing all sent frames immediately would incorrectly run clips ~14x too fast.
        var stream=rig.Robot.Animations.Scheduler.Stream;
        int bytesPlayed=stream.BytesStreamed,framesPlayed=stream.FramesStreamed;
        double frameDue=from;var consumed=new Queue<(double Due,int Bytes,int Frames)>();
        var wireFeedbackDue=new Queue<double>();var feedback=new Queue<(double Due,RobotMessage Message)>();
        rig.Robot.Animations.Scheduler.Diagnostic+=o=>
        {
            if(o is not SendGateObservation {Sent:true} send)return;
            if(send.CountsAsAudioFrame)frameDue=Math.Max(frameDue,At())+1000.0*744/22320;
            consumed.Enqueue((Math.Max(frameDue,At()),send.Size,send.CountsAsAudioFrame?1:0));
            if(send.MessageId is 0x9B or 0x9A)wireFeedbackDue.Enqueue(Math.Max(frameDue,At()));
        };
        int mark=rig.Mark(),replayStart=mark;byte activeTag=0;int index=0;
        var wall=Stopwatch.StartNew();
        for(double elapsed=0;elapsed<=to-from;elapsed+=60)
        {
            // PCM device completion has an independent real clock, as in WinMmPcmSink.
            if(elapsed>wall.Elapsed.TotalMilliseconds)Thread.Sleep((int)(elapsed-wall.Elapsed.TotalMilliseconds));
            while(index<packets.Count && packets[index].At<=from+elapsed)
            {
                var message=packets[index++].Message;
                if(message is DisableAnimTracks disable){capturedLocks|=disable.Field0;ApplyContextLocks();continue;}
                if(message is EnableAnimTracks enable){capturedLocks&=(byte)~enable.Field0;ApplyContextLocks();continue;}
                if(message is RobotState state){state.PoseOriginId=f.Robot.CurrentOriginId;state.PoseFrameId=f.Robot.PoseFrameId;}
                if(message is AnimationState animation)
                {
                    // Old cumulative counters belong to a different Robot. Consume actual replay
                    // output at the declared firmware-model frame rate, at recorded F1 boundaries.
                    // Preserve captured tag/tracks (classifier B3,63E398), timestamp and drops;
                    // native57C6F6 still computes its finite byte/frame budgets from this feedback.
                    while(consumed.TryPeek(out var c)&&c.Due<=from+elapsed)
                    {consumed.Dequeue();bytesPlayed+=c.Bytes;framesPlayed+=c.Frames;}
                    animation.NumAnimBytesPlayed=bytesPlayed;
                    animation.NumAudioFramesPlayed=framesPlayed;
                }
                rig.Data(message);
                if(message is FallingStarted or FallingStopped or MotorCalibration)
                {
                    var impact=Assert.IsType<ReactToImpactBehavior>(Assert.Single(manager.Reactions,r=>r.Strategy.Trigger==ReactionTrigger.RobotFalling).Behavior);
                    trace.Add($"{At():F1} INPUT {message.GetType().Name}: impact={impact.ImpactRecorded} stopped={impact.FallingStoppedSeen} calibrated={impact.CalibratedRecorded}");
                }
            }
            // Acknowledge actual new stream tags at modeled playback, not when the host
            // has merely sent its ahead-of-playback buffer. Never reuse captured CA/CB tags.
            foreach(var message in rig.SentSince(mark))
                if(message is StartOfAnimation start){activeTag=start.AnimId;feedback.Enqueue((wireFeedbackDue.Dequeue(),new AnimationStarted{AnimId=activeTag}));}
                else if(message is EndOfAnimation)
                {double due=wireFeedbackDue.Dequeue();if(activeTag!=0)feedback.Enqueue((due,new AnimationEnded{AnimId=activeTag}));activeTag=0;}
            while(feedback.TryPeek(out var ack)&&ack.Due<=At()){feedback.Dequeue();rig.Data(ack.Message);}
            mark=rig.Mark();rig.Tick(60);
        }
        foreach(var line in trace)output.WriteLine(line);
        foreach(var gap in gaps.Distinct())output.WriteLine(gap);
        rig.Engine.DrainAppDelivery();
        // Source-family membership does not make a second RNG draw or require the old run's RNG state.
        HashSet<string> Family(params int[] triggers)=>triggers.SelectMany(t=>
            rig.Robot.Animations.Library!.GetGroup(rig.Robot.Animations.DrivingTriggerMap!.GroupFor((AnimationTrigger)t)!)!.Entries.Select(e=>e.Name)).ToHashSet();
        void SentReaction(string owner,HashSet<string> family,int minimum=1)
        {
            var selected=clips.Where(c=>c.Owner==owner&&family.Contains(c.Clip.Name)).ToArray();
            Assert.True(selected.Length>=minimum,$"{owner}: expected at least {minimum} authored clips, got {selected.Length}");
            var starts=rig.SentSince(replayStart).OfType<StartOfAnimation>().Select(s=>s.AnimId).ToHashSet();
            Assert.Contains(selected,c=>starts.Contains(c.Clip.Tag));
        }
        switch(label)
        {
            case "slope":
                Assert.Contains(ReactionTrigger.RobotPlacedOnSlope,reactions);
                SentReaction("ReactToPlacedOnSlope",Family(0x145,0x139,0x1A8),3);
                Assert.Contains(reports,r=>r.Result==0&&r.AnimationName is {} name&&Family(0x145,0x139,0x1A8).Contains(name));
                break;
            case "impact-low":
                Assert.Contains(ReactionTrigger.RobotFalling,reactions);
                Assert.DoesNotContain(clips,c=>c.Owner=="ReactToImpact"); // 606348 only animates >1000f; recorded 346.9243164.
                Assert.Contains(reports,r=>r.Result==0&&r.AnimationName is null);
                break;
            case "impact-high":
                Assert.Contains(ReactionTrigger.RobotFalling,reactions);
                SentReaction("ReactToImpact",Family(0x1A0)); // second recorded impact 1888.013916 >1000f.
                Assert.Contains(reports,r=>r.Result==0&&r.AnimationName is {} name&&Family(0x1A0).Contains(name));
                break;
            case "cliff":
                Assert.Contains(ReactionTrigger.CliffDetected,reactions);
                SentReaction("ReactToCliff",Family(0x19E));
                SentReaction("ReactToCliff",Family(0x13D,0x131,0x19D));
                break;
            case "cliff-late":
                Assert.Contains(ReactionTrigger.CliffDetected,reactions);
                SentReaction("ReactToCliff",Family(0x19E));
                Assert.Contains(ReactionTrigger.RobotOnBack,reactions);
                Assert.Contains(reports,r=>r.Result==EngineActionResult.Cancelled);
                break;
            case "cliff-preempt":
                Assert.Contains(ReactionTrigger.CliffDetected,reactions);
                Assert.Contains(ReactionTrigger.RobotFalling,reactions);
                Assert.DoesNotContain(clips,c=>c.Owner=="ReactToCliff");
                break;
            case "unexpected":
                Assert.Contains(ReactionTrigger.UnexpectedMovement,reactions);
                SentReaction("ReactToUnexpectedMovement",Family(0x1AE,0x1AD,0x1AC));
                Assert.Contains(ReactionTrigger.RobotPickedUp,reactions); // actual F0 preemption; no artificial shielding.
                Assert.Contains(reports,r=>r.Result==EngineActionResult.Cancelled&&r.AnimationName is {} name&&Family(0x1AE,0x1AD,0x1AC).Contains(name));
                // A later pickup delocalizes the robot and can remove the old origin's objects.
                // Inspect the response before that independent captured event, not at window end.
                Assert.Contains(responseSnapshots,s=>s.Obstacles>0&&s.LocalizationPending!=0);
                Assert.Contains(rig.SentSince(replayStart),m=>m is AbsoluteLocalizationUpdate);
                Assert.Contains(bridge!.ReadEvents().GetProperty("events").EnumerateArray(), entry =>
                    entry.GetProperty("type").GetString()=="unexpected-movement" &&
                    entry.GetProperty("significant").GetBoolean());
                break;
        }
        Assert.DoesNotContain(gaps,g=>!RemainingSharedMarkers.Any(prefix=>g.StartsWith(prefix,StringComparison.Ordinal))&&!rightingMarkers.Contains(g));
    }

    private static readonly string[] RemainingSharedMarkers=
    [
        "MISSING: M7-019 RobotCliffEventFinished tag0x36 native local subscriber continuity after E2G emit0x00662590",
        "MISSING: M11-077: RemoveOwner is built (0x008448B4..0x00844914)",
        "MISSING: M13-039 client needs cache +0x2C read at 0x005924C4",
        "MISSING: M13-039 client needs cache +0x30 read at 0x00592514",
        "MISSING: M13-039 client needs cache +0x34 read at 0x00592560",
        "MISSING: M8-015 Queue.Update RUNNING VizManager.SetText0x0053F65C",
        "MISSING: M8-015 Queue.Update terminal VizManager.SetText(empty)0x0053F696",
        "MISSING: Robot::Update's behaviour debug string (robot+0x4C",
        "MISSING: M8-021 trigger DAS blacklist/event diagnostic coverage remains incomplete",
        "MISSING: M8-023/M8-027 ProtoHandle callable destruction0x0066350E",
        "MISSING: M15-014 common action device-write input unavailable at 0x0069652E",
        "MISSING: M15-093 daily-star check skipped: the robot/device copies are not resolved",
        "MISSING: M15-093 LocalNotifications: persisted notification determinant unavailable",
    ];

    private sealed class SilentSink:IPlatformPcmSink
    {
        private readonly object gate=new();private readonly AutoResetEvent ready=new(false);
        private readonly Queue<(ulong Token,double Due)> pending=new();private readonly Queue<ulong> done=new();
        private readonly Stopwatch clock=Stopwatch.StartNew();private readonly Timer timer;
        private double tail;private bool disposed;
        internal SilentSink()=>timer=new Timer(_=>Complete(),null,1,1);
        public WaitHandle CompletionReady=>ready;
        public void Submit(ulong token,byte[] pcm)
        {lock(gate){if(disposed)return;tail=Math.Max(tail,clock.Elapsed.TotalMilliseconds)+pcm.Length/192.0;pending.Enqueue((token,tail));}}
        private void Complete()
        {lock(gate){if(disposed)return;bool any=false;while(pending.TryPeek(out var p)&&p.Due<=clock.Elapsed.TotalMilliseconds){pending.Dequeue();done.Enqueue(p.Token);any=true;}if(any)ready.Set();}}
        public IReadOnlyList<ulong> DrainCompleted(){lock(gate){var result=done.ToArray();done.Clear();return result;}}
        public void Dispose(){lock(gate){if(disposed)return;disposed=true;timer.Dispose();ready.Dispose();}}
    }
}
