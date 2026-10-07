using System.Text.Json;
using System.Reflection;
using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

public class AdpStrictMeasurementsTests
{
    [Fact]
    public void MeasureReadyFilterBodiesAgainstShippedArmWithoutChoosingThresholds()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","adp_strict_filters.json")));
        var measurements=new List<object>();
        foreach(var row in doc.RootElement.GetProperty("rows").EnumerateArray())
        {
            float F(uint b)=>BitConverter.Int32BitsToSingle(unchecked((int)b));
            float[] Read(string key)=>row.GetProperty(key).EnumerateArray().Select(x=>F(x.GetUInt32())).ToArray();
            int frames=row.GetProperty("frames").GetInt32(),channels=row.GetProperty("channels").GetInt32(),stride=row.GetProperty("stride").GetInt32();
            var kind=(WwiseVoiceFilterKind)row.GetProperty("kind").GetInt32();
            var band=new WwiseVoiceFilterBand(kind){Current=40f,Target=40f,Steps=8,Countdown=0,Dirty=0,First=0,Bypass=row.GetProperty("bypass").GetByte(),History=Read("history")};
            Read("coefficients").CopyTo(band.F,0);
            var candidate=Read("source");var expected=Read("expected");
            band.Process(candidate,(byte)channels,(ushort)frames,(ushort)stride,row.GetProperty("align").GetUInt32(),22320,8);
            Assert.Equal(expected.Length,candidate.Length);
            int silentMismatch=Enumerable.Range(0,candidate.Length).Count(i=>(candidate[i]==0)!=(expected[i]==0));
            Assert.Equal(0,silentMismatch);
            double energy=0,error=0,max=0;
            for(int i=0;i<candidate.Length;i++){double d=(double)expected[i]-candidate[i];error+=d*d;energy+=(double)expected[i]*expected[i];max=Math.Max(max,Math.Abs(d));}
            var expectedHistory=Read("expected_history");double historyMaximum=0;
            for(int i=0;i<expectedHistory.Length;i++)historyMaximum=Math.Max(historyMaximum,Math.Abs((double)band.History![i]-expectedHistory[i]));
            measurements.Add(new{kind=kind.ToString(),frames,channels,stride,silentMismatch,rms=Math.Sqrt(error/candidate.Length),maximumAbsoluteError=max,historyMaximum,snrDb=error==0?"infinity":(10*Math.Log10(energy/error)).ToString("R",System.Globalization.CultureInfo.InvariantCulture)});
        }
        string? output=Environment.GetEnvironmentVariable("COZMO_ADP_FILTER_MEASUREMENTS");
        if(output!=null)File.WriteAllText(output,JsonSerializer.Serialize(measurements,new JsonSerializerOptions{WriteIndented=true}));
        Assert.Equal(32,measurements.Count);
    }
    [Fact]
    public void MeasureBiquadAgainstShippedArmWithoutChoosingThresholds()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","adp_strict_biquad.json")));
        var measurements=new List<object>();
        foreach(var row in doc.RootElement.GetProperty("rows").EnumerateArray())
        {
            float F(uint b)=>BitConverter.Int32BitsToSingle(unchecked((int)b));
            float[] Read(string key)=>row.GetProperty(key).EnumerateArray().Select(x=>F(x.GetUInt32())).ToArray();
            int frames=row.GetProperty("frames").GetInt32(),channels=row.GetProperty("channels").GetInt32(),stride=row.GetProperty("stride").GetInt32();
            var plugin=(WwiseEqPlugin)Activator.CreateInstance(typeof(WwiseEqPlugin),nonPublic:true)!;
            var type=typeof(WwiseEqPlugin); const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            // Caller-supplied state bench: do not run or claim parameter selection/Init.
            type.GetField("_channels",flags)!.SetValue(plugin,(uint)channels);
            type.GetField("_state",flags)!.SetValue(plugin,Read("history"));
            type.GetField("_stateReady",flags)!.SetValue(plugin,true);
            Read("coefficients").CopyTo((float[])type.GetField("_coef",flags)!.GetValue(plugin)!,0);
            var candidate=Read("source");var expected=Read("expected");
            var state=new WwiseDecodeState{Data=candidate,ChannelConfig=(uint)channels,MaxFrames=(ushort)stride,ValidFrames=(ushort)frames};
            type.GetMethod("Biquad",flags)!.Invoke(plugin,new object[]{state,0});
            Assert.Equal(expected.Length,candidate.Length);
            int silentMismatch=Enumerable.Range(0,candidate.Length).Count(i=>(candidate[i]==0)!=(expected[i]==0));
            Assert.Equal(0,silentMismatch);
            double energy=0,error=0,max=0;
            for(int i=0;i<candidate.Length;i++){double d=(double)expected[i]-candidate[i];error+=d*d;energy+=(double)expected[i]*expected[i];max=Math.Max(max,Math.Abs(d));}
            var actualHistory=plugin.StateSnapshot!;var expectedHistory=Read("expected_history");
            double historyMaximum=0;
            for(int i=0;i<actualHistory.Length;i++)historyMaximum=Math.Max(historyMaximum,Math.Abs((double)actualHistory[i]-expectedHistory[i]));
            measurements.Add(new{frames,channels,stride,silentMismatch,rms=Math.Sqrt(error/candidate.Length),maximumAbsoluteError=max,historyMaximum,snrDb=error==0?"infinity":(10*Math.Log10(energy/error)).ToString("R",System.Globalization.CultureInfo.InvariantCulture)});
        }
        string? output=Environment.GetEnvironmentVariable("COZMO_ADP_BIQUAD_MEASUREMENTS");
        if(output!=null)File.WriteAllText(output,JsonSerializer.Serialize(measurements,new JsonSerializerOptions{WriteIndented=true}));
        Assert.Equal(64,measurements.Count);
    }
    [Fact]
    public void MeasureMixerAgainstShippedArmWithoutChoosingThresholds()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","adp_strict_mixer.json")));
        var measurements=new List<object>();
        foreach(var row in doc.RootElement.GetProperty("rows").EnumerateArray())
        {
            float F(uint b)=>BitConverter.Int32BitsToSingle(unchecked((int)b));
            float[] Read(string key)=>row.GetProperty(key).EnumerateArray().Select(x=>F(x.GetUInt32())).ToArray();
            var source=Read("source"); var candidate=Read("destination"); var expected=Read("expected");
            int frames=row.GetProperty("frames").GetInt32();
            WwiseMixKernels.RampAccumulateA46668(source,candidate,F(row.GetProperty("start").GetUInt32()),F(row.GetProperty("increment").GetUInt32()),frames);
            Assert.Equal(expected.Length,candidate.Length);
            double energy=0,error=0,max=0; int silentMismatch=0;
            for(int i=0;i<frames;i++)
                if((expected[i]==0)!=(candidate[i]==0))silentMismatch++;
            Assert.Equal(0,silentMismatch); // component structure gate, before error measurement
            for(int i=0;i<frames;i++)
            {
                double d=(double)expected[i]-candidate[i]; error+=d*d; energy+=(double)expected[i]*expected[i];max=Math.Max(max,Math.Abs(d));
            }
            measurements.Add(new {frames,silentMismatch,rms=Math.Sqrt(error/frames),maximumAbsoluteError=max,snrDb=error==0?"infinity":(10*Math.Log10(energy/error)).ToString("R",System.Globalization.CultureInfo.InvariantCulture)});
        }
        string? output=Environment.GetEnvironmentVariable("COZMO_ADP_MEASUREMENTS");
        if(output!=null)File.WriteAllText(output,JsonSerializer.Serialize(measurements,new JsonSerializerOptions{WriteIndented=true}));
        Assert.Equal(128,measurements.Count);
        // Measurement only: ADP-1 thresholds are a manager decision, not inferred here.
    }
}
