using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Cozmo.Conformance;
using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Xunit;
using Xunit.Abstractions;

namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class LiveNarrationRecordedReplayTests(ITestOutputHelper output)
{
    // Display replay only. No engine reactions or action results are injected,
    // and no physical transport is constructed. Names are the recorded native
    // completion payload, not a new draw from the animation group.
    [Fact]
    public void RecordedPickupReportsBothClipsAndAsksAboutTheirActualTracks()
    {
        string? bundle=Environment.GetEnvironmentVariable("COZMO_NARRATION_REPLAY_BUNDLE");
        if(string.IsNullOrEmpty(bundle))return;
        var events=File.ReadLines(Path.Combine(bundle,"events.jsonl")).Select(line=>JsonNode.Parse(line)!.AsObject()).ToArray();
        var phase=Assert.Single(events.Where(row=>row["kind"]?.GetValue<string>()=="phase-end"&&row["phase"]?.GetValue<string>()=="pickup"));
        double from=phase["startMs"]!.GetValue<double>(),to=phase["endMs"]!.GetValue<double>();
        var acks=new List<(double At,byte Tag,bool Started)>();
        foreach(string line in File.ReadLines(Path.Combine(bundle,"messages.jsonl")))
        {
            var row=JsonNode.Parse(line)!.AsObject();double at=row["atMs"]!.GetValue<double>();
            if(at>to)break;
            if(row["outbound"]!.GetValue<bool>()||at<from||row["tag"]!.GetValue<string>() is not ("0xCA" or "0xCB"))continue;
            var message=RobotMessage.Parse(Convert.FromHexString(row["payloadHex"]!.GetValue<string>()));
            if(message is AnimationStarted start)acks.Add((at,start.AnimId,true));
            else if(message is AnimationEnded end)acks.Add((at,end.AnimId,false));
        }
        var initializations=new Dictionary<uint,double>();
        var pattern=new Regex(@"^Preconditions for PlayAnimation.* \[(\d+)\] successfully met\.$",RegexOptions.CultureInvariant);
        foreach(var row in events.Where(row=>row["name"]?.GetValue<string>()=="IAction.Update.PreconditionsMet"))
        {
            var match=pattern.Match(row["text"]!.GetValue<string>());
            if(match.Success)initializations[uint.Parse(match.Groups[1].Value)]=row["atMs"]!.GetValue<double>();
        }
        var library=AnimationLibrary.Open(PickupPyramidNativeExecutionTests.Resources());
        var lines=new List<string>();
        using var narrator=new LiveNarration(line=>{lines.Add(line);output.WriteLine(line);},()=>to,automaticFlush:false);
        var facts=new List<(double At,System.Action Deliver)>();
        foreach(var row in events.Where(row=>row["kind"]?.GetValue<string>()=="behavior-transition"&&row["atMs"]!.GetValue<double>()<=to))
        {
            double at=row["atMs"]!.GetValue<double>();
            var report=new BehaviorTransitionReport(row["OldId"]!.GetValue<byte>(),row["NewId"]!.GetValue<byte>(),
                row["OldClass"]!.GetValue<byte>(),row["OldExecutable"]!.GetValue<byte>(),
                row["NewClass"]!.GetValue<byte>(),row["NewExecutable"]!.GetValue<byte>(),row["DisplayKey"]!.GetValue<string>());
            facts.Add((at,()=>narrator.ObserveBehavior(at,report)));
        }
        foreach(var completion in events.Where(row=>row["kind"]?.GetValue<string>()=="action-completed"
            &&row["AnimationName"] is not null&&row["atMs"]!.GetValue<double>()>=from&&row["atMs"]!.GetValue<double>()<=to))
        {
            uint action=completion["Tag"]!.GetValue<uint>();double ended=completion["atMs"]!.GetValue<double>();
            if(!initializations.TryGetValue(action,out double initialized))continue;
            // Native preconditions and completion delimit one PlayAnimation
            // action. Refuse ambiguous CA ownership rather than guessing a tag.
            var starts=acks.Where(ack=>ack.Started&&ack.At>=initialized&&ack.At<=ended).ToArray();
            if(starts.Length==0&&initialized<from)continue; // Already running before the recorded step.
            Assert.True(starts.Length==1,$"Action {action} has {starts.Length} start candidates");
            string name=completion["AnimationName"]!.GetValue<string>();
            var clip=library.GetClip(name);
            AnimationTrack tracks=clip.Keyframes.Aggregate(AnimationTrack.None,(mask,keyframe)=>mask|keyframe.Track);
            var info=new LiveNarration.ClipInfo(starts[0].Tag,name,clip.DurationMs,tracks);
            facts.Add((initialized,()=>narrator.RegisterClip(initialized,info)));
        }
        foreach(var ack in acks)
        {
            var observed=ack;
            facts.Add((ack.At,()=>{if(observed.Started)narrator.RobotStarted(observed.At,observed.Tag);else narrator.RobotEnded(observed.At,observed.Tag);}));
        }
        foreach(var fact in facts.OrderBy(fact=>fact.At))fact.Deliver();
        narrator.Flush();
        var check=PersonalityHw.Steps.Single(step=>step.Id=="pickup").Check!;
        double trigger=PersonalityHw.ObservationStartedAt(check,events,from,to)!.Value;
        Assert.True(PersonalityHw.ObservationCompleted(check,events,from,to));
        var pickups=narrator.Playbacks(trigger,to).Where(p=>p.Behavior==nameof(BehaviorID.ReactToPickup)
            &&p.Clip.Name.StartsWith("anim_reacttocliff_pickup_",StringComparison.Ordinal)).ToArray();
        Assert.Equal(new byte[]{9,11},pickups.Select(p=>p.Clip.Tag));
        Assert.Equal(new[]{"anim_reacttocliff_pickup_03","anim_reacttocliff_pickup_06"},pickups.Select(p=>p.Clip.Name));
        Assert.All(pickups,play=>{Assert.Equal("ended",play.Ending);Assert.True(play.EndedAt>play.StartedAt);
            Assert.True(play.Clip.Tracks.HasFlag(AnimationTrack.Audio));Assert.True(play.Clip.Tracks.HasFlag(AnimationTrack.Head));Assert.True(play.Clip.Tracks.HasFlag(AnimationTrack.Lift));});
        string question=narrator.ObservationQuestion(trigger,to,"Did you observe the pickup reaction?")+" [yes/no/partly]";
        output.WriteLine(question);
        Assert.Contains("anim_reacttocliff_pickup_03",question);Assert.Contains("anim_reacttocliff_pickup_06",question);
        Assert.Contains("sound",question);Assert.Contains("head",question);Assert.Contains("lift",question);
        Assert.DoesNotContain("lookinplaceforfaces",question);
        Assert.Contains("[yes/no/partly]",question);
    }
}
