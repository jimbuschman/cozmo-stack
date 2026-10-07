using System.Reflection;
using System.Text.Json;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Xunit;

namespace Cozmo.Protocol.Tests;

public class NeedsUnpauseNativeTests
{
    [Fact]
    public void ExitSdkUnpauseUsesStoredTickAndNativeDeadlineGate()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","needs_unpause_native.json")));
        foreach(var row in doc.RootElement.GetProperty("rows").EnumerateArray())
        {
            float F(string key)=>BitConverter.Int32BitsToSingle(unchecked((int)row.GetProperty(key).GetUInt32()));
            static uint Bits(float value)=>BitConverter.SingleToUInt32Bits(value);
            double clock=0;
            var needs=new NeedsManager(()=>clock);
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            T Get<T>(string field)=>(T)typeof(NeedsManager).GetField(field,flags)!.GetValue(needs)!;
            typeof(NeedsManager).GetField("_nextDecaySec",flags)!.SetValue(needs,F("next_before"));
            needs.Update(F("pause_tick"));
            var starts=Get<Dictionary<NeedId,float>>("_fullnessStartSec");
            var deadlines=Get<Dictionary<NeedId,float>>("_fullnessDeadlineSec");
            foreach(var need in new[]{NeedId.Repair,NeedId.Energy,NeedId.Play})
            {
                starts[need]=F("start_before");
                if(row.GetProperty("deadline_present").GetBoolean())deadlines[need]=F("deadline_before");
            }
            clock=999.125; // deliberately disagrees with the stored binary32 tick
            needs.SetPaused(true);
            Assert.Equal(row.GetProperty("pause_at").GetUInt32(),Bits(Get<float>("_pausedAtSec")));
            Assert.Equal(row.GetProperty("remaining").GetUInt32(),Bits(Get<float>("_pausedRemainingSec")));
            needs.Update(F("unpause_tick")); // stores +0x3AC even while paused
            clock=2000.25;
            using var robot=CozmoRobot.CreateOffline();
            robot.Engine.NeedsSetPaused=needs.SetPaused; // real recipient, test-only context binding
            robot.Engine.ExitSdkMode(false,false);
            robot.Engine.Tick();
            Assert.False(needs.IsPaused);
            Assert.Equal(row.GetProperty("expected_next").GetUInt32(),Bits(Get<float>("_nextDecaySec")));
            var last=Get<Dictionary<NeedId,float>>("_lastDecaySec");
            foreach(var need in new[]{NeedId.Repair,NeedId.Energy,NeedId.Play})
            {
                uint Expected(string key)=>row.GetProperty(key)[(int)need].GetUInt32();
                double ExpectedDouble(string key)=>BitConverter.Int32BitsToSingle(unchecked((int)Expected(key)));
                Assert.Equal(Expected("expected_last_decay"),Bits(last[need]));
                Assert.Equal(ExpectedDouble("expected_pause_start"),needs.NeedPauseStartSec(need));
                Assert.Equal(ExpectedDouble("expected_bracket"),needs.BracketChangedSec(need));
                Assert.Equal(Expected("expected_start"),Bits(starts[need]));
                Assert.Equal(Expected("expected_deadline"),Bits(deadlines.GetValueOrDefault(need)));
                Assert.Equal(row.GetProperty("deadline_present").GetBoolean(),deadlines.ContainsKey(need));
            }
        }
    }
}
