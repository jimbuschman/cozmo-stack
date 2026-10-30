using Cozmo.Robot.Animation;
using Cozmo.Robot;
using Cozmo.Protocol;
using Cozmo.Robot.Behavior;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public class NativeBouncerFrameTests
{
    [Fact]
    public void PublicFaceReadinessWaitsForAssetsWhileProceduralFrameSurvivesPublication()
    {
        string root=Directory.CreateTempSubdirectory("bouncer-assets-").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(root,"animations"));
            File.WriteAllText(Path.Combine(root,"animations","c.json"),"{\"c\":[]}");
            using var robot=CozmoRobot.CreateOffline(engineOptions:new CozmoEngineOptions{ResourcesPath=root});
            var manager=robot.Animations.ProceduralFaceManager;
            Assert.Null(robot.Animations.FaceAnimations);
            var pixels=new byte[128*64];pixels[1]=255;manager.SetProceduralFrame33(pixels);
            var expected=Assert.Single(manager.Variants("_PROCEDURAL_")!);
            bool? absentDuringBackpack=null;
            robot.Engine.LogEntry+=entry=>
            {
                if(entry.Name=="RobotDataLoader.LoadBackpackLightAnimations.LoadTime")
                    absentDuringBackpack=robot.Animations.FaceAnimations is null;
            };
            robot.Animations.LoadFrom(root);
            Assert.True(absentDuringBackpack);
            Assert.Same(manager,robot.Animations.FaceAnimations);
            var after=Assert.Single(manager.Variants("_PROCEDURAL_")!);
            Assert.Equal(expected.EvenRowsCleared,after.EvenRowsCleared);
            Assert.Equal(expected.OddRowsCleared,after.OddRowsCleared);
        }
        finally { Directory.Delete(root,true); }
    }
    [Fact]
    public void OrdinaryTriggerDestructionStillReleasesReservedTag()
    {
        using var rig=new Rig();
        var action=new TriggerAnimationAction(rig.Robot,new(AnimationTrigger.BouncerGetIn,1,true,0,60f,false),_=>{});
        uint tag=action.Tag;Assert.True(ActionRunnerTagCounter.Global.IsInUse(tag));
        action.Prep();action.WatcherEnding();
        Assert.False(ActionRunnerTagCounter.Global.IsInUse(tag));
    }
    [Fact]
    public void ReentrantCancelAtDirectActionDiagnosticDoesNotStartStaleStream()
    {
        using var rig=new M4ControlTests.Rig(); rig.ToSynced(); rig.State(); rig.Robot.Animations.ManualTicking=true;
        var list=rig.Engine.Robot!.ActionList; NativeSetFaceImageAction? face=null;
        long before=rig.Robot.Animations.Generation;
        face=new NativeSetFaceImageAction(rig.Robot,new byte[128*64],_=>list.Cancel(face!.Tag));
        Assert.Equal(0,list.QueueAction(QueueActionPosition.Now,face));
        rig.Tick(10);
        Assert.Equal(before,rig.Robot.Animations.Generation);
    }
    [Fact]
    public void TwoOwnedSetFaceActionsStreamDistinctNativeFramesAndCompleteOnRobotEvents()
    {
        // Rig's CreateOffline/InitOfflineLink supplies pre-ready link flags; this does not test NV readiness.
        // StartProduction drives the real action list and streamer; the subsequent state is valid input.
        // Simulated firmware acknowledges only delivered wire starts/ends and reports their consumed budget.
        using var signals=SignalTestContext.Install(); using var rig=new Rig();
        rig.Robot.Engine.StartProduction(); rig.State();
        var owner=rig.Robot.Engine.Robot!;
        string data=Path.Combine(AppContext.BaseDirectory,"TestData","BouncerNative");
        var first=File.ReadAllBytes(Path.Combine(data,"frame_initial.gray"));
        var second=File.ReadAllBytes(Path.Combine(data,"frame_edges.gray"));
        var face=new NativeSetFaceImageAction(rig.Robot,first);
        Assert.False(face.CanInterrupt());
        var unrelated=new NativeSetFaceImageAction(rig.Robot,new byte[128*64]);
        var missing=new List<string>(); void Capture(string message)=>missing.Add(message);
        SteppedBehavior.MissingReported+=Capture;
        try
        {
        Assert.Null(face.Child);
        Assert.True(face.Tag < unrelated.Tag);
        int bytesPlayed=0,audioPlayed=0;
        void Run(NativeSetFaceImageAction action,byte[] expected)
        {
            Assert.Equal(0,owner.ActionList.QueueAction(QueueActionPosition.Now,action));
            byte tag=0; int parity=-1; bool matched=false,childSeen=false;
            for(int i=0;i<1000 && action.State!=0;i++)
            {
                if(action.Child is { } child)
                { childSeen=true;Assert.Equal(0x1A,child.Type);Assert.True(unrelated.Tag<child.Tag); }
                var messages=rig.Pump(3);
                // Pump can initialize and emit both messages in one call; observe before the first end ACK.
                if(action.Child is { } pumpedChild)
                { childSeen=true;Assert.Equal(0x1A,pumpedChild.Type);Assert.True(unrelated.Tag<pumpedChild.Tag); }
                foreach(var message in messages)
                {
                    byte wireTag=(byte)message.Id;
                    if(wireTag is >=0x90 and <=0x9B || message is AudioSilence)bytesPlayed+=message.ToBytes().Length;
                    if(message is AudioSample or AudioSilence or EndOfAnimation)audioPlayed++;
                    if(message is StartOfAnimation start)
                    {
                        tag=start.ToBytes()[1];parity=rig.Robot.Animations.Scheduler.FirstScanLines.FaceAnimation;
                        rig.Send(new AnimationStarted { AnimId=tag });
                    }
                    if(message is FaceImage image && parity>=0 && image.Image.SequenceEqual(FaceAnimationFrame.FromCanvas(expected).ForScanLine(parity)))matched=true;
                    if(message is EndOfAnimation)rig.Send(new AnimationEnded { AnimId=tag });
                }
                rig.Send(new AnimationState { Timestamp=rig.T,NumAnimBytesPlayed=bytesPlayed,NumAudioFramesPlayed=audioPlayed,EnabledAnimTracks=7,Tag=tag });
            }
            Assert.True(childSeen);Assert.NotEqual((byte)0,tag);Assert.True(matched);
            Assert.Equal(0u,action.State);
        }
        Run(face,first);
        Run(new NativeSetFaceImageAction(rig.Robot,second),second);
        Assert.Contains(missing,s=>s.Contains("M8-020 direct PlayAnimationAction") && s.Contains("completion union"));
        unrelated.WatcherEnding();
        }
        finally { SteppedBehavior.MissingReported-=Capture; }
    }
    private sealed class Sink : IAnimationSink
    {
        internal int FinishedCount;
        public void Face(FaceBitmap bitmap) { }
        public void Audio(byte[]? frame) { }
        public void Head(sbyte angle,uint duration) { }
        public void Lift(byte height,uint duration) { }
        public void Body(BodyKeyframe keyframe) { }
        public void AnimationStarted(byte tag) { }
        public void AnimationEnded() { }
        public void BodyStop() { }
        public void Lights(LightsKeyframe keyframe) { }
        public void Event(string id) { }
        public void Finished(string name,bool completed) { if(completed) FinishedCount++; }
    }

    [Fact]
    public void StaticLiveOneFrameCompletesThroughTheStreamer()
    {
        var library=new FaceAnimationLibrary();
        library.SetProceduralFrame33(new byte[128*64]);
        var sink=new Sink();
        var scheduler=new AnimationScheduler(sink) { FaceAnimationVariants=library.Variants };
        var clip=new AnimationClip { Name="one frame test", IsLive=true, Tracks=AnimationTrack.Face,
            Keyframes=new Keyframe[] { new FaceAnimationKeyframe(0,"_PROCEDURAL_") } };
        Assert.NotNull(scheduler.Play(clip,0));
        for(int t=0;t<=165;t+=33)scheduler.Advance(t);
        Assert.Equal(1,sink.FinishedCount);
    }
    [Fact]
    public void OneFrameLiveClipConsumesItsFrameAndEnds()
    {
        var clip=new AnimationClip { IsLive=true, Keyframes=new Keyframe[] { new FaceAnimationKeyframe(0,"_PROCEDURAL_") } };
        var animation=StreamAnimation.Of(clip);
        animation.Init();
        Assert.True(animation.HasFramesLeft);
        animation.FaceAnim.MoveToNext();
        Assert.False(animation.HasFramesLeft);
        Assert.Equal(0,animation.FaceAnim.Count);
    }
    [Fact]
    public void ProceduralFrameIsReplacedUsingThresholdAndBothScanlineVariants()
    {
        var frames=new FaceAnimationLibrary();
        var pixels=new byte[128*64]; pixels[0]=128; pixels[1]=129; pixels[128]=255;
        frames.SetProceduralFrame33(pixels);
        var actual=Assert.Single(frames.Variants("_PROCEDURAL_")!);
        var expected=FaceAnimationFrame.FromCanvas(pixels.Select(v=>v>128?(byte)255:(byte)0).ToArray());
        Assert.Equal(expected.EvenRowsCleared,actual.EvenRowsCleared);
        Assert.Equal(expected.OddRowsCleared,actual.OddRowsCleared);
        frames.SetProceduralFrame33(new byte[128*64]);
        Assert.Single(frames.Variants("_PROCEDURAL_")!);
    }
}
