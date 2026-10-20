using System.Collections.Concurrent;
using System.Text.Json;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Transport;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public class M5X11BuilderTests : IDisposable
{
    private readonly bool _error=EngineErrorState.ErrorFlagSet;
    public void Dispose()=>EngineErrorState.ErrorFlagSet=_error;
    private record Entry(EngineLogLevel Level,string? Channel,string Key,string Text,bool Error);
    private static SourceLogSink Capture(ConcurrentQueue<Entry> q)=>(l,c,k,t)=>q.Enqueue(new(l,c,k,t,EngineErrorState.ErrorFlagSet));
    private static AnimationClip Parse(string frames,ConcurrentQueue<Entry> entries)
    {using var doc=JsonDocument.Parse("{\"c\":["+frames+"]}");return JsonClipLoader.LoadDocument(doc,null,Capture(entries))!;}

    // reach: unit (direct loader/streamer/helper or authored fixture inputs; complete robot entry and native producers are not claimed).
    [Fact]
    public void MissingTriggerHasChildErrorFlagThenParentTailWhileNullTriggerIsAccepted()
    {
        var entries=new ConcurrentQueue<Entry>();EngineErrorState.ErrorFlagSet=false;
        var clip=Parse("{\"Name\":\"RecordHeadingKeyFrame\"}",entries);
        Assert.True(clip.LoadTruncated);
        Assert.Equal(new[]{"IKeyFrame.ReadFromJson","Animation.DefineFromJson.AddKeyFrameFailure"},entries.Where(e=>e.Level==EngineLogLevel.Error).Select(e=>e.Key));
        var child=Assert.Single(entries,e=>e.Key=="IKeyFrame.ReadFromJson");Assert.Equal("c: Expecting 'triggerTime_ms' field in KeyFrame Json",child.Text);Assert.False(child.Error);Assert.Null(child.Channel);
        Assert.True(Assert.Single(entries,e=>e.Key=="Animation.DefineFromJson.AddKeyFrameFailure").Error);
        entries.Clear();clip=Parse("{\"Name\":\"RecordHeadingKeyFrame\",\"triggerTime_ms\":null}",entries);
        Assert.Equal(0u,Assert.Single(clip.Keyframes).TriggerTimeMs);Assert.False(clip.LoadTruncated);Assert.DoesNotContain(entries,e=>e.Level==EngineLogLevel.Error);
    }

    // reach: unit (direct loader/streamer/helper or authored fixture inputs; complete robot entry and native producers are not claimed).
    [Theory]
    [InlineData("")] [InlineData(",\"audioEventId\":null")]
    public void MissingOrNullAudioIdIsScalarZero(string id)
    {
        var entries=new ConcurrentQueue<Entry>();var clip=Parse("{\"Name\":\"RobotAudioKeyFrame\",\"triggerTime_ms\":0"+id+"}",entries);
        var audio=Assert.IsType<AudioKeyframe>(Assert.Single(clip.Keyframes));Assert.Equal(new long[]{0},audio.EventIds);Assert.Equal(new[]{1f},audio.Probabilities);Assert.DoesNotContain(entries,e=>e.Level==EngineLogLevel.Error);
    }

    // reach: unit (direct loader/streamer/helper or authored fixture inputs; complete robot entry and native producers are not claimed).
    [Fact]
    public void EmptyProbabilityVectorUsesOneOverIdCountAndNullElementsConvert()
    {
        var entries=new ConcurrentQueue<Entry>();
        var clip=Parse("{\"Name\":\"RobotAudioKeyFrame\",\"triggerTime_ms\":0,\"audioEventId\":[null,4294967297],\"probability\":[]}",entries);
        var audio=Assert.IsType<AudioKeyframe>(Assert.Single(clip.Keyframes));Assert.Equal(new long[]{0,1},audio.EventIds);Assert.Equal(new[]{0.5f,0.5f},audio.Probabilities);
        entries.Clear();clip=Parse("{\"Name\":\"RobotAudioKeyFrame\",\"triggerTime_ms\":0,\"audioEventId\":[null,2],\"probability\":[null,1]}",entries);
        audio=Assert.IsType<AudioKeyframe>(Assert.Single(clip.Keyframes));Assert.Equal(new[]{0f,1f},audio.Probabilities);Assert.False(clip.LoadTruncated);Assert.DoesNotContain(entries,e=>e.Level==EngineLogLevel.Error);
    }

    // reach: unit (direct loader/streamer/helper or authored fixture inputs; complete robot entry and native producers are not claimed).
    [Fact]
    public void NullColorElementsBecomeZeroAndBadLengthWarnsBeforeElementConversion()
    {
        var entries=new ConcurrentQueue<Entry>();
        string Frame(string back)=>"{\"Name\":\"BackpackLightsKeyFrame\",\"triggerTime_ms\":0,\"durationTime_ms\":1,\"Back\":"+back+",\"Front\":[0,0,0],\"Middle\":[0,0,0],\"Left\":[0,0,0],\"Right\":[0,0,0]}";
        var clip=Parse(Frame("[null,1,null,null]"),entries);var lights=Assert.IsType<LightsKeyframe>(Assert.Single(clip.Keyframes));Assert.Equal(0x00FF0000u,lights.LoadedColorWords![3]);
        entries.Clear();clip=Parse(Frame("[\"bad\",null]"),entries);
        Assert.True(clip.LoadTruncated);Assert.Empty(clip.Keyframes);
        Assert.Equal(new[]{"JsonTools.GetColorOptional","BackpackLightsKeyFrame.SetMembersFromJson","Animation.DefineFromJson.AddKeyFrameFailure"},entries.Where(e=>e.Level is EngineLogLevel.Warning or EngineLogLevel.Error).Select(e=>e.Key));
        Assert.Equal("Expecting color in Json to be a string or 3 or 4 element array",Assert.Single(entries,e=>e.Key=="JsonTools.GetColorOptional").Text);
    }

    [Fact]
    public void ProceduralEyeVectorReadsNullThroughTheSameFloatGetter()
    {
        var entries=new ConcurrentQueue<Entry>();string eye="["+string.Join(",",Enumerable.Repeat("null",19))+"]";
        var clip=Parse("{\"Name\":\"ProceduralFaceKeyFrame\",\"triggerTime_ms\":0,\"leftEye\":"+eye+",\"rightEye\":"+eye+"}",entries);
        var face=Assert.IsType<FaceKeyframe>(Assert.Single(clip.Keyframes));Assert.All(face.Pose.Left.ToArray(),v=>Assert.Equal(0f,v));Assert.All(face.Pose.Right.ToArray(),v=>Assert.Equal(0f,v));Assert.DoesNotContain(entries,e=>e.Level==EngineLogLevel.Error);
    }

    // reach: unit (direct loader/streamer/helper or authored fixture inputs; complete robot entry and native producers are not claimed).
    [Theory]
    [InlineData("neutral")] [InlineData("idle")] [InlineData("replay")]
    public void LibraryLookupMissIsKeyedAndPrecedesTheThreeCallerTails(string caller)
    {
        string root=Directory.CreateTempSubdirectory("m511-").FullName;Directory.CreateDirectory(Path.Combine(root,"animations"));
        try
        {
            var entries=new ConcurrentQueue<Entry>();var lib=AnimationLibrary.Open(root,sourceLog:Capture(entries));
            var scheduler=new AnimationScheduler(new Sink()){Catalog=new ForwardCatalog(lib),SourceLog=Capture(entries)};
            entries.Clear();EngineErrorState.ErrorFlagSet=false;
            if(caller=="neutral")scheduler.LoadNeutralFace();
            else if(caller=="idle"){scheduler.PushIdleAnimation(AnimationTrigger.AcknowledgeObject,"test");scheduler.Advance(0);}
            else{scheduler.Play(new AnimationClip{Name="missing"},0);entries.Clear();EngineErrorState.ErrorFlagSet=false;scheduler.ReplayLastAnimation(0);}
            var miss=Assert.Single(entries,e=>e.Key=="CannedAnimationContainer.GetAnimation_Const.InvalidName");
            Assert.Equal(EngineLogLevel.Error,miss.Level);Assert.Null(miss.Channel);Assert.Equal("Animation requested for unknown animation 'missing'.",miss.Text);Assert.False(miss.Error);Assert.True(EngineErrorState.ErrorFlagSet);
            if(caller!="replay")
            {
                string tail=caller=="neutral" ? "AnimationStreamer.Constructor.NeutralFaceDataNotFound" : "AnimationStreamer.Update.InvalidIdleAnimationFromGroup";
                Assert.True(Assert.Single(entries,e=>e.Key==tail).Error);
                Assert.True(entries.ToList().FindIndex(e=>e.Key==miss.Key)<entries.ToList().FindIndex(e=>e.Key==tail));
            }
        }
        finally{Directory.Delete(root,true);}
    }
    private sealed class ForwardCatalog(AnimationLibrary library):IAnimationCatalog
    {
        public bool HasAnimationForTrigger(AnimationTrigger trigger)=>true;
        public string GetAnimationForTrigger(AnimationTrigger trigger)=>"g";
        public string GetAnimationNameFromGroup(string group,bool strict)=>"missing";
        public (string Name,int Count) GetFirstAnimationName(string group)=>("missing",1);
        public AnimationClip? GetAnimation(string name)=>library.GetAnimation(name);
    }
    private sealed class Sink:IAnimationSink
    {
        public void Face(FaceBitmap b){} public void Audio(byte[]? b){} public void Head(sbyte a,uint d){} public void Lift(byte h,uint d){} public void Body(BodyKeyframe b){} public void BodyStop(){} public void Lights(LightsKeyframe b){} public void Event(string s){} public void AnimationStarted(byte b){} public void AnimationEnded(){} public void Finished(string s,bool b){}
    }
}
