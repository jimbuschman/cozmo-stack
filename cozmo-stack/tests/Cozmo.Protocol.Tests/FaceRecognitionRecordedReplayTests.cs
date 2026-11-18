using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Emulation;
using Cozmo.Robot.Vision;
using Xunit;
using Xunit.Abstractions;
using Fixture = Cozmo.Protocol.Tests.PickupPyramidNativeExecutionTests.Fixture;

namespace Cozmo.Protocol.Tests;

// Private recordings stay outside git. Set COZMO_FACE_REPLAY_BUNDLE to the
// 20261117-052405-AI-COMMANDS-HW-1-bdcbda directory to run these integration cases.
// No typed face, recognition result, reaction target, or speech result is injected.
[Collection("SteppedBehavior missing-report statics")]
public sealed class FaceRecognitionRecordedReplayTests(ITestOutputHelper output)
{
    private sealed record Packet(double At, RobotMessage Message);

    [Theory]
    [InlineData(false,120650,139100)]
    [InlineData(true,120650,139100)]
    [InlineData(true,247000,255800)]
    public void CapturedCameraUsesTheNativeAlbumAndNamedReaction(bool named,double from,double to)
    {
        string? bundle=Environment.GetEnvironmentVariable("COZMO_FACE_REPLAY_BUNDLE");
        if(string.IsNullOrEmpty(bundle))return; // Explicit optional private-input replay, like shipped-library tests.
        var packets=new List<Packet>();var writes=new Dictionary<uint,byte[]>();byte[]? calibration=null;
        using var hash=System.Security.Cryptography.SHA256.Create();
        using(var input=File.OpenRead(Path.Combine(bundle,"messages.jsonl")))
            output.WriteLine("messages SHA256="+Convert.ToHexString(hash.ComputeHash(input)));
        foreach(string line in File.ReadLines(Path.Combine(bundle,"messages.jsonl")))
        {
            using var row=JsonDocument.Parse(line);var r=row.RootElement;
            double at=r.GetProperty("atMs").GetDouble();bool outbound=r.GetProperty("outbound").GetBoolean();
            string tag=r.GetProperty("tag").GetString()!;
            if(!outbound && tag=="0xCD" && at<30000)
            {
                var reply=(NVOpResult)RobotMessage.Parse(Convert.FromHexString(r.GetProperty("payloadHex").GetString()!));
                if(reply.Tag==CameraCalibration.NvEntryTag && reply.Result==0)calibration=reply.Data;
            }
            if(!outbound && at>=from && at<=to && tag is "0xF0" or "0xF2" or "0xF4" or "0xD1")
                packets.Add(new(at,RobotMessage.Parse(Convert.FromHexString(r.GetProperty("payloadHex").GetString()!))));
            // The successful teach-face writes finish at 255799 ms, before rename/erase.
            // Replaying that album with an earlier camera window is an explicit
            // changed initial state, not a claim that the album existed at startup.
            if(named && outbound && tag=="0x81" && at is >255000 and <256000)
            {
                byte[] bytes=Convert.FromHexString(r.GetProperty("payloadHex").GetString()!);
                if(bytes[9]==1)writes[BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(1))]=bytes[13..];
            }
        }
        Assert.NotEmpty(packets);
        byte[] Payload(uint tag)
        {
            var bytes=new List<byte>();
            for(uint next=tag;writes.TryGetValue(next,out var chunk);next+=(uint)chunk.Length)bytes.AddRange(chunk);
            byte[] raw=bytes.ToArray();Assert.True(raw.Length>=16);
            Assert.Equal(0x435A4D4Fu,BinaryPrimitives.ReadUInt32LittleEndian(raw));
            int length=checked((int)BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(8)));
            Assert.Equal(length+16,raw.Length);return raw[16..];
        }
        var options=new CozmoEngineOptions{ResourcesPath=FixtureResources(),BlockPoolPath="",
            SpeechSoundBankDirectory=GraphRig.FindMeta(),LabLocaleLanguage="en",
            SpeechAudioArchivePath=Path.Combine(WwiseAssets.SoundDir!,"AudioAssets.zip")};
        Assert.NotNull(calibration);
        byte[]? album=named?Payload(0x184000):null,enrollment=named?Payload(0x183000):null;
        var sink=new SilentSink();
        using var f=new Fixture(sourceNvEmpty:true,preloadAnimations:named,pumpAnimations:named,engineOptions:options,
            sourceNvRaw:tag=>tag==CameraCalibration.NvEntryTag?calibration:null,
            sourceNvStored:tag=>tag==0x184000?album:tag==0x183000?enrollment:null,
            engineCreated:engine=>{if(named)engine.RuntimeAudioSinkForTest=()=>sink;});
        var vision=f.M.Vision;
        string? name=named?Assert.Single(vision.Recognizer.EnrolledFaces).Name:null;
        var faces=new List<RobotObservedFaceReport>();var changes=new List<RobotChangedObservedFaceIdReport>();
        var trace=new ConcurrentQueue<string>();var completed=new List<RobotCompletedAction>();
        var fault=new ConcurrentQueue<Exception>();
        GrayImage? namedImage=null;var images=new ConcurrentDictionary<uint,GrayImage>();
        var admittedImages=new ConcurrentDictionary<uint,GrayImage>();uint? namedFeatureTimestamp=null;
        f.Rig.Robot.Camera.FrameReceived+=frame=>{if(frame.TryDecodeGray(out var image,out _) && image is not null)images[frame.ImageId]=image;};
        vision.FrameProcessed+=result=>
        {if(images.TryGetValue(result.ImageId,out var image))admittedImages[result.Timestamp]=image;};
        if(vision.FaceDetector is OkaoFaceDetector detector)detector.RecognitionFeatureConsumedForTest=(timestamp,track)=>
        {if(namedFeatureTimestamp is null && vision.Recognizer.EnrolledFaces.Any(face=>face.CurrentTrack==track && face.Name==name))namedFeatureTimestamp=timestamp;};
        f.Rig.Engine.RobotObservedFace+=faces.Add;
        f.Rig.Robot.HandlerFaulted+=fault.Enqueue;
        f.Rig.Engine.RobotChangedObservedFaceId+=changes.Add;
        f.Rig.Engine.StackLogLine+=trace.Enqueue;
        f.Robot.ActionList.RegisterActionEndedCallback(completed.Add);
        f.Rig.Engine.LogEntry+=e=>{if(e.Name.Contains("Action",StringComparison.Ordinal)||e.Name.Contains("Face",StringComparison.Ordinal)||e.Name.Contains("Animation",StringComparison.Ordinal)||e.Name=="IBehavior.BehaviorObjectiveAchieved")trace.Enqueue(e.Name+": "+e.Text);};
        SteppedBehavior.ResetMissingForTests();
        vision.Enabled=true;f.Rig.Robot.SetEnableFreeplayActivity(false);f.Rig.Tick();
        uint offset=packets.Select(p=>p.Message).OfType<RobotState>().First().Timestamp-f.Robot.StoredState!.Timestamp-30;
        int index=0;double first=packets[0].At;var wall=Stopwatch.StartNew();
        for(double tick=first;index<packets.Count;tick+=60)
        {
            sink.Complete();
            while(wall.Elapsed.TotalMilliseconds<tick-first)Thread.Sleep(1);
            while(index<packets.Count && packets[index].At<=tick)
            {
                var message=packets[index++].Message;
                if(message is RobotState state)
                {state.Timestamp-=offset;state.PoseOriginId=f.Robot.CurrentOriginId;state.PoseFrameId=f.Robot.PoseFrameId;}
                else if(message is ImageChunk image && image.FrameTimestamp!=0)image.FrameTimestamp-=offset;
                f.Rig.Data(message);
            }
            f.Rig.Tick();vision.WaitForProcessorIdle();f.Rig.Tick(0);f.Rig.Engine.DrainAppDelivery();
            // React while the face is in view. The rest of this window includes
            // the subject leaving; waiting until then tests a different scene.
            if(from>200000 && faces.Any(face=>face.FaceId==1 && face.Name==name))break;
        }
        output.WriteLine($"window={from}..{to}; named album={named}; faces={faces.Count}; named={faces.Count(x=>!string.IsNullOrEmpty(x.Name))}; IDs={string.Join(',',faces.Select(x=>x.FaceId).Distinct())}");
        Assert.NotEmpty(faces);
        if(from<200000)
        {
            // This window PRECEDES teach-face. Loading the later album is a
            // counterfactual observation, not a new expected native deadline.
            if(!named)Assert.DoesNotContain(faces,x=>!string.IsNullOrEmpty(x.Name));
            else Assert.All(faces.Where(x=>!string.IsNullOrEmpty(x.Name)),face=>{Assert.Equal(1,face.FaceId);Assert.Equal(name,face.Name);});
            foreach(string line in trace.Where(x=>x.StartsWith("MISSING",StringComparison.Ordinal)))output.WriteLine(line);
            Assert.DoesNotContain(trace,line=>line.StartsWith("MISSING",StringComparison.Ordinal)&&line.Contains("M14-",StringComparison.Ordinal));
            Assert.Empty(fault);
            if(!named)sink.Dispose();
            return;
        }
        Assert.Contains(faces,x=>x.FaceId==1 && x.Name==name);
        Assert.Contains(changes,x=>x.OldId<0 && x.NewId==1);
        Assert.NotNull(namedFeatureTimestamp);
        namedImage=admittedImages[namedFeatureTimestamp.Value];
        output.WriteLine($"repeat admitted feature image at robot timestamp {namedFeatureTimestamp}");
        var manager=f.Robot.BehaviorManagerOwner!;
        f.Rig.ContinueRobotTimestampAfterReplay(f.Robot.StoredState!.Timestamp);
        f.Head=f.Robot.StoredState.HeadAngle;f.Lift=f.Robot.StoredState.LiftAngle;
        f.X=f.Robot.StoredState.Pose.X;f.Y=f.Robot.StoredState.Pose.Y;f.Yaw=f.Robot.StoredState.Pose.Angle;
        // Robot replies to newly generated commands cannot come from the old
        // recording. Supply ordinary motor/animation ACKs and repeat the last
        // recognized pixels with fresh frame/IMU timestamps for verification.
        f.Robot.ActionList.Clear();f.Robot.ActionList.QueueAction(QueueActionPosition.Now,new Idle(),0);
        manager.CheckReactions(f.Rig.Engine.Timer.Seconds);
        var behavior=Assert.IsType<AcknowledgeFaceBehavior>(manager.Current);
        SayTextAction? say=null;TriggerAnimationAction? animation=null;NativeLastFaceRunner? turn=null;
        var started=new HashSet<byte>();var wireStarts=new HashSet<byte>();var wireEnds=new HashSet<byte>();byte? wireTag=null;
        int mark=f.Rig.Mark(),readMark=mark;bool audio=false,ended=false;
        // Admit a fresh camera observation while the first turn is pending,
        // before acknowledging its motor commands (Observe accepts state 0).
        f.Rig.DefaultTickMs=0;f.Rig.Tick();f.Rig.Tick();
        var stationary=f.Rig.MakeState(RobotStatusFlag.HeadInPos|RobotStatusFlag.LiftInPos,
            origin:f.Robot.CurrentOriginId,frame:f.Robot.PoseFrameId,x:f.X,y:f.Y,head:f.Head,liftAngle:f.Lift);
        stationary.Pose.Angle=f.Yaw;f.Rig.Data(stationary);
        f.Rig.Tick();f.NativeCameraFrame(49999,namedImage.Pixels);f.Rig.Tick();
        var imageTimes=packets.Where(p=>p.Message is ImageChunk).GroupBy(p=>((ImageChunk)p.Message).ImageId).Select(g=>g.First().At).OrderBy(t=>t).ToArray();
        var spacings=imageTimes.Zip(imageTimes.Skip(1),(a,b)=>b-a).OrderBy(t=>t).ToArray();
        double cameraInterval=spacings[spacings.Length/2];
        wall.Restart();double last=0,nextCamera=0,nextDiagnostic=0;uint frameId=50000;
        while(wall.Elapsed<TimeSpan.FromSeconds(60) && !ended)
        {
            double now=wall.Elapsed.TotalMilliseconds;f.Rig.DefaultTickMs=0;f.Rig.Tick(now-last);last=now;
            sink.Complete();var stream=f.Rig.Robot.Animations.Scheduler.Stream;
            f.Rig.Data(new AnimationState{NumAudioFramesPlayed=stream.FramesStreamed,NumAnimBytesPlayed=stream.BytesStreamed});
            f.Motors();
            if(now>=nextCamera)
            {
                nextCamera=now+cameraInterval;
                f.NativeCameraFrame(frameId++,namedImage!.Pixels);
                trace.Enqueue($"repeated image {vision.LastResult?.ImageId} faces={string.Join(',',vision.LastFaces.Select(x=>x.Id))} head={f.Head}");
            }
            if(behavior.OwnedRunner is NativeLastFaceRunner current)
            {turn=current;if(current.ChildForTest is SayTextAction speech){say=speech;animation=speech.ChildForTest??animation;}}
            var sent=f.Rig.SentSince(readMark);readMark+=sent.Count;
            foreach(var message in sent)
            {
                if(message is StartOfAnimation start){wireTag=start.AnimId;wireStarts.Add(start.AnimId);}
                if(message is AudioSample samples && animation is not null && started.Contains(animation.AnimationTagForTest) && samples.Samples.Any(b=>b!=0xFF && b!=0x7F))audio=true;
                if(message is EndOfAnimation && wireTag is {} tag)wireEnds.Add(tag);
            }
            if(animation is { Initialized:true } && animation.AnimationTagForTest is >0 and var ownedTag)
            {
                if(wireStarts.Contains(ownedTag) && started.Add(ownedTag))f.Rig.Data(new AnimationStarted{AnimId=ownedTag});
                if(started.Contains(ownedTag) && wireEnds.Remove(ownedTag))f.Rig.Data(new AnimationEnded{AnimId=ownedTag});
            }
            if(say is not null && now>=nextDiagnostic)
            {
                nextDiagnostic=now+5000;
                var lease=(CannedSpeechLease?)typeof(SayTextAction).GetField("_cannedSession",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(say);
                var path=lease?.Path;
                trace.Enqueue($"audio path at {now:F0}: clip={animation?.GetAnimationCompletionName()} stream={f.Rig.Robot.Animations.Scheduler.StreamTimeMs} state={path?.State} callbacks={path?.CallbacksReceived}/{path?.PostedCount} cursor={path?.EventCursor}; events="+string.Join(',',path?.Events.Select(e=>$"{e.EventId:X8}:{e.State}@{e.TriggerTimeMs}")??[]));
            }
            f.Rig.Engine.DrainAppDelivery();
            ended=turn is not null && completed.Any(c=>c.Tag==turn.Tag && c.Result==0);
            Thread.Sleep(10);
        }
        f.Rig.DefaultTickMs=60;
        for(int i=0;i<10 && behavior.EngineRunning;i++)f.Motors();
        f.Rig.Engine.DrainAppDelivery();
        output.WriteLine($"turn={turn?.FaceState}; verified={turn?.VerifiedId}; say={say?.Text}; animation={animation?.Request.Trigger}; audio={audio}; ended={ended}; starts={f.Rig.SentSince(mark).OfType<StartOfAnimation>().Count()}; ends={f.Rig.SentSince(mark).OfType<EndOfAnimation>().Count()}");
        output.WriteLine("wire="+string.Join(',',f.Rig.SentSince(mark).Where(x=>x is StartOfAnimation or EndOfAnimation).Select(x=>x is StartOfAnimation start?"start "+start.AnimId:"end")));
        foreach(string line in trace.Where(x=>x.Contains("MISSING")||x.Contains("SayText")||x.Contains("TimedOut")||x.Contains("ActionCompleted")||x.Contains("PlayAnimation.")||x.Contains("ResetFaceTracker")||x.Contains("audio path")))output.WriteLine(line);
        output.WriteLine("continuation IDs="+string.Join(',',faces.TakeLast(15).Select(x=>x.FaceId)));
        Assert.Empty(fault);
        Assert.DoesNotContain(trace,line=>line.StartsWith("MISSING",StringComparison.Ordinal)&&line.Contains("M14-",StringComparison.Ordinal));
        Assert.NotNull(say);Assert.Equal(name,say.Text);
        Assert.NotNull(animation);Assert.Equal(AnimationTrigger.AcknowledgeFaceNamed,animation.Request.Trigger);
        Assert.True(audio);Assert.True(ended);Assert.Equal(EngineActionResult.Success,say.State);
        Assert.Null(behavior.OwnedRunner);
        Assert.True(vision.Faces.GetNativeFace(f.Robot,1)!.TurnedTowards);
        Assert.Contains(trace,line=>line.StartsWith("IBehavior.BehaviorObjectiveAchieved",StringComparison.Ordinal)&&line.Contains("Objective:ReactedAcknowledgedFace",StringComparison.Ordinal));
    }
    private static string FixtureResources()=>PickupPyramidNativeExecutionTests.Resources();
    private sealed class Idle():ActionRunner(8,0)
    {
        protected override string ActionName=>"supplied interruptible idle";
        public override bool CanInterrupt()=>true;
        public override uint CheckIfDone()=>EngineActionResult.Running;
    }
    private sealed class SilentSink:IPlatformPcmSink
    {
        private readonly object gate=new();private readonly AutoResetEvent ready=new(false);
        private readonly Queue<(ulong Token,double Due)> pending=new();private readonly Queue<ulong> done=new();
        private readonly Stopwatch clock=Stopwatch.StartNew();private readonly Timer timer;
        private double tail;private bool disposed;
        internal SilentSink()=>timer=new Timer(_=>Complete(),null,1,1);
        public WaitHandle CompletionReady=>ready;
        public void Submit(ulong token,byte[] pcm)
        {
            // WinMmPcmSink's actual format is 48000 Hz stereo signed-16:
            // 192000 bytes/second. Device completion runs independently of DT/PT.
            lock(gate){if(disposed)return;tail=Math.Max(tail,clock.Elapsed.TotalMilliseconds)+pcm.Length/192.0;pending.Enqueue((token,tail));}
        }
        internal void Complete()
        {
            lock(gate)
            {
                if(disposed)return;bool completed=false;
                while(pending.TryPeek(out var item) && item.Due<=clock.Elapsed.TotalMilliseconds)
                {pending.Dequeue();done.Enqueue(item.Token);completed=true;}
                if(completed)ready.Set();
            }
        }
        public IReadOnlyList<ulong> DrainCompleted(){lock(gate){var result=done.ToArray();done.Clear();return result;}}
        public void Dispose(){lock(gate){if(disposed)return;disposed=true;timer.Dispose();ready.Dispose();}}
    }
}
