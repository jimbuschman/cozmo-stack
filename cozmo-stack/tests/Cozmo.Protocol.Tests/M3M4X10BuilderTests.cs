using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using Cozmo.Robot;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public class M3M4X10BuilderTests : IDisposable
{
    private readonly bool _error = EngineErrorState.ErrorFlagSet;
    public void Dispose() => EngineErrorState.ErrorFlagSet = _error;
    private static string Pattern(string period, string offset, string colour = "[null,true,0.5,false]")
    {
        string colours="["+string.Join(",",Enumerable.Repeat(colour,5))+"]";
        string periods="["+string.Join(",",Enumerable.Repeat(period,5))+"]";
        string offsets="["+string.Join(",",Enumerable.Repeat(offset,5))+"]";
        return "{\"charging\":{\"onColors\":"+colours+",\"offColors\":"+colours+",\"onPeriod_ms\":"+periods+",\"offPeriod_ms\":"+periods+",\"transitionOnPeriod_ms\":"+periods+",\"transitionOffPeriod_ms\":"+periods+",\"offset\":"+offsets+"}}";
    }
    private static string Root() => Directory.CreateTempSubdirectory("b34x10-").FullName;
    private static string FileAt(string root)
    { string dir=Path.Combine(root,BackpackLightAnimations.ResourceDirectory);Directory.CreateDirectory(dir);return Path.Combine(dir,"p.json"); }
    private static SourceLogSink Capture(ConcurrentQueue<EngineLogEntry> q) => (l,c,k,t)=>q.Enqueue(new(l,c,k,t,new Dictionary<string,string>()));

    // getline(istream, doc, (char)-1), 0x008E094E; truncation makes the quoted member incomplete.
    [Fact]
    public void BackpackDocumentEndsAtFirstFFByteInsideString()
    {
        string root=Root();
        try
        {
            byte[] prefix=Encoding.UTF8.GetBytes("{\"charging\":\"a");
            byte[] suffix=Encoding.UTF8.GetBytes("b\"}");
            File.WriteAllBytes(FileAt(root),prefix.Concat(new byte[]{255}).Concat(suffix).ToArray());
            var entries=new ConcurrentQueue<EngineLogEntry>(); var stack=new List<string>();
            var lib=BackpackLightAnimations.Load(root,stack.Add,Capture(entries));
            Assert.DoesNotContain("Charging",lib.Names);
            Assert.Single(entries,e=>e.Name=="DataPlatform.readAsJson" && e.Level==EngineLogLevel.Error);
            Assert.DoesNotContain(stack,s=>s.StartsWith("event handler threw:"));
        }
        finally {Directory.Delete(root,true);}
    }

    [Theory]
    [InlineData("null","null",0u,0)]
    [InlineData("true","false",1u,0)]
    [InlineData("false","true",0u,1)]
    [InlineData("12.75","-12.75",12u,-12)]
    [InlineData("4294967295","-2147483648",4294967295u,-2147483648)]
    public void BackpackUsesJsonCppUnsignedSignedAndFloatScalarConversions(string period,string offset,uint expectedPeriod,int expectedOffset)
    {
        string root=Root();
        try
        {
            File.WriteAllText(FileAt(root),Pattern(period,offset));
            var lib=BackpackLightAnimations.Load(root);
            var leds=lib.Get("Charging")!;Assert.Equal(5,leds.Length);
            Assert.All(leds,l=>{Assert.Equal(expectedPeriod,l.OnMs);Assert.Equal(expectedPeriod,l.TransitionOffMs);Assert.Equal(expectedOffset,l.OffsetMs);Assert.Equal(new LedColor(0,255,127,0),l.OnColor);});
        }
        finally {Directory.Delete(root,true);}
    }

    [Theory]
    [InlineData("-1","0","[0,0,0,0]")]
    [InlineData("4294967296","0","[0,0,0,0]")]
    [InlineData("0","2147483648","[0,0,0,0]")]
    [InlineData("0","-2147483649","[0,0,0,0]")]
    [InlineData("0","0","[\"bad\",0,0,0]")]
    public void BackpackConversionThrowSkipsFileWithoutInventedEngineError(string period,string offset,string colour)
    {
        string root=Root();
        try
        {
            File.WriteAllText(FileAt(root),Pattern(period,offset,colour));
            var entries=new ConcurrentQueue<EngineLogEntry>();var stack=new List<string>();
            var lib=BackpackLightAnimations.Load(root,stack.Add,Capture(entries));
            Assert.DoesNotContain("Charging",lib.Names);
            Assert.Contains(stack,l=>l.StartsWith("event handler threw: JsonLogicError:"));
            Assert.DoesNotContain(entries,e=>e.Name=="BackpackLightAnimationContainer.AddBackpackLightStateValues.InvalidJson");
        }
        finally {Directory.Delete(root,true);}
    }

    // Atomic mutation must precede callbacks; the second thread must see the first thread's locator.
    [Fact]
    public async Task BackpackConcurrentStartsHaveOneInvalidLocatorAndKeepMutationOrder()
    {
        var sent=new List<RobotMessage>();
        var body=new BodyLightComponent(m=>{sent.Add(m);return true;},new BackpackLightAnimations());
        using var firstLogged=new ManualResetEventSlim();using var releaseFirst=new ManualResetEventSlim();
        int count=0;
        body.SourceLog=(l,c,k,t)=>{if(k.EndsWith("InvalidLocator")){Assert.Equal(EngineLogLevel.Info,l);if(Interlocked.Increment(ref count)==1){firstLogged.Set();Assert.True(releaseFirst.Wait(TimeSpan.FromSeconds(5)));}}};
        var first=Task.Run(()=>body.StartLooping(Enumerable.Repeat(LedPattern.Solid(LedColor.Red),5).ToArray(),2));
        Assert.True(firstLogged.Wait(TimeSpan.FromSeconds(5)));
        var second=Task.Run(()=>body.StartLooping(Enumerable.Repeat(LedPattern.Solid(LedColor.Blue),5).ToArray(),2));
        try {await second.WaitAsync(TimeSpan.FromSeconds(5));} finally {releaseFirst.Set();}
        await first.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1,count);
        body.Update(new RobotState(),_=>{});
        var middle=Assert.Single(sent.OfType<BackpackLightsMiddle>());
        Assert.Equal((ushort)0x801F,middle.Field0[0].OnColor);

    }

    // Native queue/warn/store 0x006B229C..0x006B230C, plus the adopted callback isolation rule.
    [Fact]
    public async Task CameraConcurrentRequestsProduceOneWarningForTheActualPredecessor()
    {
        using var robot=CozmoRobot.CreateOffline();var camera=robot.CameraSettings;
        var method=camera.GetType().GetMethod("SetNextCameraParams",BindingFlags.Instance|BindingFlags.NonPublic)!;
        camera.GetType().GetProperty("NextCameraParams")!.SetValue(camera,null);
        var warnings=new ConcurrentQueue<EngineLogEntry>();robot.Engine.LogEntry+=e=>{if(e.Name=="VisionSystem.SetNextCameraParams.OverwritingPreviousParams")warnings.Enqueue(e);};
        using var start=new ManualResetEventSlim();
        var first=Task.Run(()=>{start.Wait();method.Invoke(camera,new object[]{10,1f});});
        var second=Task.Run(()=>{start.Wait();method.Invoke(camera,new object[]{20,2f});});
        start.Set();await Task.WhenAll(first,second).WaitAsync(TimeSpan.FromSeconds(5));
        var warning=Assert.Single(warnings);Assert.Equal(EngineLogLevel.Warning,warning.Level);Assert.Null(warning.Channel);
        var final=camera.NextCameraParams!.Value;
        Assert.EndsWith(final.Item1==20 ? "Params already requested (10ms,1.00) but not sent. Replacing with (20ms,2.00)" : "Params already requested (20ms,2.00) but not sent. Replacing with (10ms,1.00)",warning.Text);
    }
}

