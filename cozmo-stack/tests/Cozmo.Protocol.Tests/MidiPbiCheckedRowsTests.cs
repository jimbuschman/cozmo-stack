using Cozmo.Robot.Animation.Wwise;
using Cozmo.Robot.Behavior;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("SteppedBehavior missing-report statics")]
public sealed class MidiPbiCheckedRowsTests
{
    [Theory]
    [InlineData(0x90,1,true)]
    [InlineData(0x90,127,true)]
    [InlineData(0x90,255,true)]
    [InlineData(0x90,0,false)]
    [InlineData(0x80,127,false)]
    [InlineData(0,127,false)]
    public void ActualPbiFactoryPreservesAllFourBytesAndNamesTheUnboundPublication(int status,int velocity,bool publishes)
    {
        SteppedBehavior.ResetMissingForTests();var missing=new List<string>();void On(string s)=>missing.Add(s);SteppedBehavior.MissingReported+=On;
        try
        {
            var p=new WwisePlayInitParams{SoundSpecial84=(byte)status,SoundSpecial85=2,SoundSpecial86=60,SoundSpecial87=(byte)velocity};
            var pbi=new WwisePlaybackBridge().CreatePbi(p,17,new object(),false);
            p.SoundSpecial87=3; // constructor copied the word; it does not borrow mutable request bytes
            Assert.Equal((byte)status,pbi.Field1E4);Assert.Equal(2,pbi.Field1E5);Assert.Equal((byte)60,pbi.Field1E6);Assert.Equal((byte)velocity,pbi.Field1E7);
            Assert.Equal(1f,pbi.Fade168);Assert.Equal(1f,pbi.Fade16C);Assert.Equal(1f,pbi.MuteFade40);Assert.Equal(0f,pbi.Volume3C);
            Assert.Equal(publishes,missing.Any(s=>s.Contains("PBI ctor source0x81")));
            Assert.DoesNotContain(missing,s=>s.Contains("velocity input is absent"));
        }
        finally{SteppedBehavior.MissingReported-=On;}
    }
    [Fact]
    public void UnknownVelocityDoesNotBecomeZeroAndOnlyTheNoteOnPathNeedsItsPublicationGate()
    {
        SteppedBehavior.ResetMissingForTests();var missing=new List<string>();void On(string s)=>missing.Add(s);SteppedBehavior.MissingReported+=On;
        try
        {
            var p=new WwisePlayInitParams{SoundSpecial84=0x90,SoundSpecial86=60};var pbi=new WwisePlaybackBridge().CreatePbi(p,17,new object(),false);
            Assert.Null(pbi.Field1E7);Assert.Contains(missing,s=>s.Contains("M9-014")&&s.Contains("velocity input is absent"));
            missing.Clear();p.SoundSpecial84=0;var ordinary=new WwisePlaybackBridge().CreatePbi(p,17,new object(),false);Assert.Null(ordinary.Field1E7);Assert.Empty(missing);
        }
        finally{SteppedBehavior.MissingReported-=On;}
    }
}
