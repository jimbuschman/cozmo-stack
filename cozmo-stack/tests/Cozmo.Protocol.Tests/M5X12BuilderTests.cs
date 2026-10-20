using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Robot.Behavior;
using Xunit;
namespace Cozmo.Protocol.Tests;
[Collection("M5 process statics")]
public class M5X12BuilderTests
{
    // reach: unit (direct loader/streamer/helper or authored fixture inputs; complete robot entry and native producers are not claimed).
    [Fact]
    public void UnmappedNullIdleTailReportsApprovedSkipAndStreamerStillPlays()
    {
        string root=Directory.CreateTempSubdirectory("m512-").FullName;
        Directory.CreateDirectory(Path.Combine(root,"animations"));
        try
        {
            var library=AnimationLibrary.Open(root);var stack=new List<string>();var engine=new List<string>();
            var scheduler=new AnimationScheduler(new Sink()){Catalog=library,Log=stack.Add,SourceLog=(l,c,k,t)=>engine.Add(k)};
            scheduler.PushIdleAnimation(AnimationTrigger.ReactToMotorCalibration,"test");stack.Clear();engine.Clear();
            int idleMs=scheduler.IdleMsForTest;
            scheduler.Advance(0);scheduler.Advance(60);scheduler.Advance(120);
            // one line for the whole stay in this state, not one per tick
            Assert.Equal("Idle tail skipped (M1-034): original dereferences null for unmapped idle trigger ReactToMotorCalibration (M5-027, 0x0057D3FE -> 0x00577BC6)",Assert.Single(stack));
            Assert.Empty(engine);
            // the +0x44 += 60 at 0x0057D442..0x0057D446 is part of the skipped tail
            Assert.Equal(idleMs,scheduler.IdleMsForTest);
            var handle=scheduler.Play(new AnimationClip{Name="after",Keyframes=new Keyframe[]{new HeadKeyframe(0,100,0,0)}},0);
            Assert.NotNull(handle);Assert.True(handle.IsRunning);scheduler.Advance(0);Assert.True(handle.IsRunning);
        }
        finally{Directory.Delete(root,true);}
    }
    private sealed class Sink:IAnimationSink
    {
        public void Face(FaceBitmap b){} public void Audio(byte[]? b){} public void Head(sbyte a,uint d){} public void Lift(byte h,uint d){} public void Body(BodyKeyframe b){} public void BodyStop(){} public void Lights(LightsKeyframe b){} public void Event(string s){} public void AnimationStarted(byte b){} public void AnimationEnded(){} public void Finished(string s,bool b){}
    }
}
