using System.Reflection;
using System.Text.Json;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Xunit;
using Xunit.Abstractions;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class AnimationFirmwareBudgetTests(ITestOutputHelper output)
{
    [Fact]
    public void DefaultIsTheRecordedCleanFrontierWithTheNativeAudioAndStartMargin()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","animation_budget_firmware.json")));
        var policy=doc.RootElement.GetProperty("policy_selection");
        Assert.Equal(28,policy.GetProperty("capture_count").GetInt32());
        Assert.Equal(26,policy.GetProperty("raw_capture_count").GetInt32());
        Assert.Equal(12,policy.GetProperty("buffer_full_count").GetInt32());
        Assert.Equal(8125,policy.GetProperty("lowest_failure_window_peak_bytes").GetInt32());
        Assert.Equal(8117,policy.GetProperty("largest_clean_observed_below_failure_floor").GetInt32());
        Assert.Equal(747,policy.GetProperty("margin_bytes").GetInt32());
        Assert.Equal(7370,new CozmoEngineOptions().AnimationStreamBudgetBytes);
        Assert.Equal(7370,policy.GetProperty("selected_budget_bytes").GetInt32());
        // The data overlaps: successful runs reached 8190. This is an empirical
        // guard, not proof that firmware capacity is a serialized-byte threshold.
        Assert.Equal(8190,policy.GetProperty("clean_run_peak_max").GetInt32());
        foreach(var run in doc.RootElement.GetProperty("bundles").EnumerateArray())
            if(run.TryGetProperty("buffer_full",out var failures))
                foreach(var failure in failures.EnumerateArray())
                    Assert.Equal(failure.GetProperty("peak_last_100ms").GetInt32(),failure.GetProperty("peak_last_1000ms").GetInt32());
    }

    [Fact]
    public void CriticalCapturedAudioIsNativeAdmittedButPolicyHeldWithTheSameFrameGateAndFifo()
    {
        // Native oracle emu_anim_buffer.py: line 27390, at 230863.8567 ms,
        // streamed 479685/5407, played 472279/5395, AudioSample Size 745.
        foreach(int budget in new[]{8192,7370})
        {
            var stream=new StreamSendBuffer(budget);
            typeof(StreamSendBuffer).GetField("_bytesStreamed",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(stream,479685);
            typeof(StreamSendBuffer).GetField("_framesStreamed",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(stream,5407);
            stream.UpdateAmountToSend(472279,5395);
            Assert.Equal(2,stream.NumAudioFramesToSend);
            Assert.Equal(budget==8192?786:0,stream.NumBytesToSend);
            int sent=0;
            stream.Buffer(null,new AudioSample(),_=>{sent++;return true;});
            Assert.Equal(budget==8192?StreamSendBuffer.DrainResult.Empty:StreamSendBuffer.DrainResult.BudgetStop,stream.Drain());
            Assert.Equal(budget==8192?1:0,sent);
            if(budget==7370)
            {
                Assert.Equal(1,stream.Count);
                // Original later feedback is a supplied gate input, not a claim
                // that firmware would have emitted its old flush on a new send schedule.
                stream.UpdateAmountToSend(479674,5407);
                Assert.Equal(StreamSendBuffer.DrainResult.Empty,stream.Drain());
            }
            Assert.Equal(480430,stream.BytesStreamed);
            Assert.Equal(5408,stream.FramesStreamed);
        }
    }

    [Fact]
    public void LiveRobotUsesHostBudgetAndOriginal8192RemainsSelectable()
    {
        using var guarded=CozmoRobot.CreateOffline();
        Assert.Equal(7370,guarded.Animations.Scheduler.Stream.ByteBudget);
        using var native=CozmoRobot.CreateOffline(engineOptions:new CozmoEngineOptions{AnimationStreamBudgetBytes=8192});
        Assert.Equal(8192,native.Animations.Scheduler.Stream.ByteBudget);
        Assert.Equal(8192,new StreamSendBuffer().ByteBudget);
    }

    [Fact]
    public void ExistingPublicConstructionAndFiveValueResultRemainAvailable()
    {
        Assert.NotNull(typeof(StreamSendBuffer).GetConstructor(Type.EmptyTypes));
        Assert.NotNull(typeof(AnimationScheduler).GetConstructor([typeof(IAnimationSink),typeof(Random),typeof(EngineRandom)]));
        var result=new AiAnimationActionResult(true,0,"native",false,"clip"){PlaybackTag=7};
        var (admitted,code,detail,retired,name)=result;
        Assert.True(admitted);Assert.Equal(0u,code);Assert.Equal("native",detail);
        Assert.False(retired);Assert.Equal("clip",name);Assert.Equal((byte)7,result.PlaybackTag);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(30001)]
    public void InvalidHostBudgetFailsClearly(int value) =>
        Assert.Throws<ArgumentOutOfRangeException>(()=>new StreamSendBuffer(value));

    private static AnimationClipInitializedObservation Clip(byte tag,string name,long at=1)=>new(at,tag,name,1000,AnimationTrack.Head);
    private static SendGateObservation End(bool sent)=>new(0,0,0,0x9A,1,true,0,0,true,sent,true);

    [Fact]
    public void OnlySuccessfulEndSendPreventsEarlyEndDiagnosticAndHostAbortIsNotMislabelled()
    {
        using var monitor=new AnimationPlaybackMonitor();
        monitor.ObserveDiagnostic(Clip(1,"first"));
        monitor.ObserveMessage(new AnimationStarted{AnimId=1});
        monitor.ObserveDiagnostic(End(false));
        monitor.ObserveMessage(new AnimationEnded{AnimId=1});
        Assert.True(Assert.Single(monitor.IssuesSince(0,1,"first")).EndedBeforeEndSent);
        monitor.ObserveDiagnostic(Clip(2,"normal"));
        monitor.ObserveMessage(new AnimationStarted{AnimId=2});
        monitor.ObserveDiagnostic(End(true));
        monitor.ObserveMessage(new AnimationEnded{AnimId=2});
        Assert.Empty(monitor.IssuesSince(0,2,"normal"));
        monitor.ObserveDiagnostic(Clip(3,"cancelled"));
        monitor.ObserveMessage(new AnimationStarted{AnimId=3});
        monitor.ObserveHostAbort(3);
        monitor.ObserveMessage(new AnimationEnded{AnimId=3});
        Assert.Empty(monitor.IssuesSince(0,3,"cancelled"));
    }

    [Fact]
    public void ReusedTagKeepsOldEndStateAndBufferFullDoesNotBorrowAnUnstartedStreamsIdentity()
    {
        using var monitor=new AnimationPlaybackMonitor();
        monitor.ObserveDiagnostic(Clip(1,"old",1));
        monitor.ObserveMessage(new AnimationStarted{AnimId=1});
        monitor.ObserveDiagnostic(End(true));
        monitor.ObserveDiagnostic(Clip(1,"new",2));
        monitor.ObserveMessage(new AnimationEnded{AnimId=1});
        Assert.Empty(monitor.IssuesSince(0,1,null));
        var trace=new PrintTrace{FormatId=6,NameId=379,Level=2,Args=[707,747]};
        monitor.ObserveMessage(trace);
        trace.Args[0]=0;
        var unknown=Assert.Single(monitor.IssuesSince(0,null,null));
        Assert.Null(unknown.Tag);Assert.Null(unknown.Name);
        Assert.Equal(new[]{707,747},Assert.Single(unknown.Traces).Args);
        monitor.ObserveMessage(new AnimationStarted{AnimId=1});
        monitor.ObserveMessage(new AnimationEnded{AnimId=1});
        var own=Assert.Single(monitor.IssuesSince(2,1,"new"),i=>i.Tag==1);
        Assert.Equal("new",own.Name);Assert.True(own.EndedBeforeEndSent);
    }
    [Fact]
    public void PrivateLlmWindowReplaysActualByteGateWithRecordedFeedbackWithoutPredictingFirmware()
    {
        string? bundle=Environment.GetEnvironmentVariable("COZMO_LLM_SESSION_BUNDLE");
        if(string.IsNullOrEmpty(bundle))return;
        var native=new StreamSendBuffer(8192);
        var guarded=new StreamSendBuffer(7370);
        long[] high=[-1,-1];
        HashSet<long>[] seen=[new(),new()];
        int played=0,playedFrames=0,lineNumber=0;
        var recent=new Queue<(double At,int Native,int Guarded)>();
        var points=new List<object>();
        int critical=0;
        using var file=new FileStream(Path.Combine(bundle,"frames.jsonl"),FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
        using var reader=new StreamReader(file);
        while(reader.ReadLine() is {} line)
        {
            lineNumber++;
            using var doc=JsonDocument.Parse(line);
            var row=doc.RootElement;double at=row.GetProperty("atMs").GetDouble();
            if(at>249500)break;
            bool outbound=row.GetProperty("outbound").GetBoolean();int direction=outbound?1:0;
            Assert.True(FrameCodec.TryDecode(Convert.FromBase64String(row.GetProperty("rawBase64").GetString()!),out var frame,out var error),error);
            foreach(var message in frame!.Messages)
            {
                if(message.Seq!=0)
                {
                    long previous=high[direction],absolute=message.Seq;
                    if(previous>=0)
                    {
                        long delta=message.Seq-previous%65534;
                        if(delta>32767)delta-=65534;
                        else if(delta< -32767)delta+=65534;
                        absolute=previous+delta;
                    }
                    if(!seen[direction].Add(absolute))continue;
                    high[direction]=Math.Max(previous,absolute);
                }
                if(message.Type is not (ReliableMessageType.SingleReliableMessage or ReliableMessageType.SingleUnreliableMessage))continue;
                byte[] payload=message.Payload;
                if(payload.Length==0)continue;
                byte tag=payload[0];
                if(outbound && tag is >=0x8E and <=0x9B)
                {
                    if(lineNumber==27390)
                    {
                        critical++;
                        Assert.Equal((479685,5407,472279,5395,745),
                            (native.BytesStreamed,native.FramesStreamed,played,playedFrames,payload.Length));
                    }
                    foreach(var stream in new[]{native,guarded})
                    {
                        if(tag==0x9A)stream.SendDirect(payload.Length,true,()=>true);
                        else stream.Buffer(null,payload.Length,tag is 0x8E or 0x8F,()=>true);
                        stream.UpdateAmountToSend(played,playedFrames);
                        stream.Drain();
                    }
                }
                else if(!outbound && tag==0xF1)
                {
                    var state=Assert.IsType<AnimationState>(RobotMessage.Parse(payload));
                    played=state.NumAnimBytesPlayed;playedFrames=state.NumAudioFramesPlayed;
                    foreach(var stream in new[]{native,guarded})
                    {stream.UpdateAmountToSend(played,playedFrames);stream.Drain();}
                }
                else if(!outbound && tag==0xB0 && at>=229000 && RobotMessage.Parse(payload) is PrintTrace{NameId:379} trace)
                {
                    int nativePeak=recent.Max(v=>v.Native),policyPeak=recent.Max(v=>v.Guarded);
                    Assert.Equal(8151,nativePeak);
                    Assert.True(policyPeak<8125,$"Policy peak {policyPeak} crossed measured pre-error floor");
                    points.Add(new {atMs=at,nativePeakLast500Ms=nativePeak,policyPeakLast500Ms=policyPeak,
                        nativeInFlight=native.BytesStreamed-played,policyInFlight=guarded.BytesStreamed-played,
                        traceArgs=trace.Args});
                }
                if(outbound && tag is >=0x8E and <=0x9B || !outbound && tag==0xF1)
                {
                    recent.Enqueue((at,native.BytesStreamed-played,guarded.BytesStreamed-played));
                    while(recent.Count>0 && recent.Peek().At<at-500)recent.Dequeue();
                }
            }
        }
        Assert.Equal(1,critical);Assert.Single(points);
        output.WriteLine("COUNTER_REPLAY "+JsonSerializer.Serialize(new
        {
            points,
            boundary="Captured successful sends are fixed offers; F1 counters, including the original flush, are supplied inputs. This is a byte-gate replay, not a new firmware outcome or proof that tag 13 plays in full."
        }));
    }

}
