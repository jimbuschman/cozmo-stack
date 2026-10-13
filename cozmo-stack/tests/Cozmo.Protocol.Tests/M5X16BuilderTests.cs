using Cozmo.Protocol;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Cozmo.Transport;
using Xunit;
namespace Cozmo.Protocol.Tests;
[Collection("M5 process statics")]
public sealed class M5X16BuilderTests : IDisposable
{
    private const string Invalid="AnimationGroupContainer.GetAnimationGroup_Const.InvalidName";
    private readonly bool _error=EngineErrorState.ErrorFlagSet;
    private readonly string _root=Directory.CreateTempSubdirectory("m516-").FullName;
    public void Dispose(){EngineErrorState.ErrorFlagSet=_error;Directory.Delete(_root,true);}
    private string Resources()
    {
        Directory.CreateDirectory(Path.Combine(_root,"animationGroups"));
        File.WriteAllText(Path.Combine(_root,"animationGroups","empty.json"),"{\"Animations\":[]}");
        string maps=Directory.CreateDirectory(Path.Combine(_root,"animationGroupMaps")).FullName;
        File.WriteAllText(Path.Combine(maps,"AnimationTriggerMap.json"),"{\"Pairs\":[{\"CladEvent\":\"NeutralFace\",\"AnimName\":\"missing\"}]}");
        return _root;
    }
    private CozmoRobot Robot(Action<string>? beforeStat=null)=>CozmoRobot.CreateForTest(new Port(),()=>0,
        new CozmoEngineOptions{DasDeviceIdPath=Path.Combine(_root,"device-id")},beforeAnimationFileStat:beforeStat);
    [Fact]
    public void SourceLookupLogsOnceThenErrorFlagWhilePresenceAndEmptyRemainSilent()
    {
        var entries=new List<EngineLogEntry>();var lib=AnimationLibrary.Open(Resources(),sourceLog:(l,c,k,t)=>entries.Add(new(l,c,k,t,new Dictionary<string,string>())));
        entries.Clear();EngineErrorState.ErrorFlagSet=false;
        Assert.Null(lib.GetGroup("absent"));Assert.Empty(entries);Assert.False(EngineErrorState.ErrorFlagSet);
        Assert.Equal("",lib.GetAnimationNameFromGroup("empty",false));Assert.Empty(entries);Assert.False(EngineErrorState.ErrorFlagSet);
        bool? flagAtError=null;lib.SourceLog=(l,c,k,t)=>{flagAtError=EngineErrorState.ErrorFlagSet;entries.Add(new(l,c,k,t,new Dictionary<string,string>()));};
        Assert.Equal("",lib.GetAnimationNameFromGroup("absent",false));
        var error=Assert.Single(entries);Assert.Equal(Invalid,error.Name);Assert.Equal(EngineLogLevel.Error,error.Level);Assert.Null(error.Channel);
        Assert.Equal("AnimationGroup requested for unknown animation group 'absent'.",error.Text);Assert.Equal(false,flagAtError);Assert.True(EngineErrorState.ErrorFlagSet);
    }
    [Fact]
    public void TriggerInitAndIdleEachReachOneNativeLoggingLookup()
    {
        string root=Resources();using var robot=Robot();robot.Animations.LoadFrom(root);
        var entries=new List<EngineLogEntry>();robot.Engine.LogEntry+=entries.Add;
        var probe=new TriggerProbe();probe.StartAsync(new BehaviorContext{Robot=robot,Triggers=AnimationTriggerMap.Load(root)},new BehaviorScope(),default).GetAwaiter().GetResult();
        Assert.Single(entries.Where(e=>e.Name==Invalid));
        entries.Clear();var scheduler=robot.Animations.Scheduler;scheduler.PushIdleAnimation(AnimationTrigger.NeutralFace,"test");scheduler.Advance(0);
        Assert.Single(entries.Where(e=>e.Name==Invalid));
    }
    [Fact]
    public void RobotConstructedDuringLoadGetsDefaultTriggerSinkAndOrderedMissLogs()
    {
        Resources();CozmoRobot? robot=null;bool created=false;var entries=new List<EngineLogEntry>();
        robot=Robot(file=>{if(!created&&file.EndsWith("empty.json",StringComparison.Ordinal)){created=true;robot!.Engine.Robots.AddRobot(1,false);}});
        using(robot)
        {
            robot.Engine.LogEntry+=entries.Add;robot.Animations.LoadFrom(_root);Assert.True(created);
            Assert.Equal(new[]{"AnimationTriggerResponsesContainer::GetResponse",Invalid,"AnimationStreamer.Constructor.BadNeutralAnimGroup"},
                entries.Where(e=>e.Name==Invalid||e.Name=="AnimationTriggerResponsesContainer::GetResponse"||e.Name=="AnimationStreamer.Constructor.BadNeutralAnimGroup").Select(e=>e.Name));
            Assert.Contains(entries,e=>e.Name==Invalid&&e.Text.EndsWith("AnimationGroup requested for unknown animation group ''."));
        }
    }
    [Fact]
    public void FacelessNeutralConstructionLogsSkipButRemovalDoesNot()
    {
        var scheduler=new AnimationScheduler(new Sink()){Catalog=new FacelessCatalog()};var missing=new List<string>();scheduler.Log=missing.Add;
        scheduler.LoadNeutralFace();Assert.Contains(missing,l=>l.Contains("M1-034 / M5-010"));
        missing.Clear();scheduler.ResetToConstructed();Assert.DoesNotContain(missing,l=>l.Contains("M1-034 / M5-010"));
    }
    [Fact]
    public void ActivityLifecycleNamesActualOwningRecordAndDoesNotFakeHandlerStack()
    {
        using var robot=Robot();var activity=new Activity{Id="a",Strategy=new ActivityStrategy()};var missing=new List<string>();
        activity.OnSelected(1,line=>robot.Engine.EmitStackLog(line));robot.Engine.StackLogLine+=missing.Add;
        activity.OnDeselected(2,line=>robot.Engine.EmitStackLog(line));
        Assert.Contains(missing,l=>l.Contains("M5-063 / M15-006")&&l.Contains("RemoveDrivingAnimations 0x005B3450"));
        missing.Clear();activity.OnSelected(3,missing.Add);Assert.Contains(missing,l=>l.Contains("PushDrivingAnimations 0x005B319A"));
        Assert.Empty(robot.Animations.DrivingAnimations.Entries);Assert.Equal(5,robot.Animations.DrivingAnimations.State);
    }
    private sealed class TriggerProbe:SteppedBehavior
    {
        public TriggerProbe():base("probe","Dev"){UnlockIdentifier=()=>NoUnlockId;ActiveSparkIdentifier=()=>NoUnlockId;}
        protected override void OnStart()=>RunTriggerAction(0,AnimationTrigger.NeutralFace,_=>{},AnimationTrack.None,null,1,false);
    }
    private sealed class FacelessCatalog:IAnimationCatalog
    {
        public bool HasAnimationForTrigger(AnimationTrigger t)=>true;public string GetAnimationForTrigger(AnimationTrigger t)=>"g";
        public (string Name,int Count) GetFirstAnimationName(string g)=>("n",1);public string GetAnimationNameFromGroup(string g,bool s)=>"n";
        public AnimationClip? GetAnimation(string n)=>new(){Name=n,Keyframes=Array.Empty<Keyframe>()};
    }
    private sealed class Sink:IAnimationSink
    {
        public void Face(FaceBitmap f){}public void Audio(byte[]? a){}public void Body(BodyKeyframe k){}public void BodyStop(){}
        public void Head(sbyte angle,uint duration){}public void Lift(byte height,uint duration){}public void Lights(LightsKeyframe k){}
        public void AnimationStarted(byte tag){}public void AnimationEnded(){}public void Event(string e){}public void Finished(string name,bool interrupted){}
    }
    private sealed class Port:IEngineTransport
    {
        public bool TimedOut=>false;public event Action<ReceiverEvent>? Received{add{}remove{}}
        public void Start(){}public void Connect(System.Net.IPAddress ip,bool simulated){}public void Disconnect(System.Net.IPEndPoint e){}public void SendData(byte[] d){}
    }
}
