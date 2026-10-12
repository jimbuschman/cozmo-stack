using System.Text;
using Cozmo.Robot;
using Cozmo.Transport;
using Xunit;
namespace Cozmo.Protocol.Tests;
[Collection("M5 process statics")]
public class M3M4X11BuilderTests : IDisposable
{
    private readonly bool _error=EngineErrorState.ErrorFlagSet;
    public void Dispose()=>EngineErrorState.ErrorFlagSet=_error;
    private static string Pattern(string colors="[1,0,0,1]",string period="0",bool offset=true)
    {
        string a="["+string.Join(",",Enumerable.Repeat(colors,5))+"]";
        string periods="["+string.Join(",",Enumerable.Repeat(period,5))+"]";
        return "{\"onColors\":"+a+",\"offColors\":"+a+",\"onPeriod_ms\":"+periods+",\"offPeriod_ms\":[0,0,0,0,0],\"transitionOnPeriod_ms\":[0,0,0,0,0],\"transitionOffPeriod_ms\":[0,0,0,0,0]"+(offset ? ",\"offset\":[0,0,0,0,0]" : "")+"}";
    }
    private static string All(string pattern)=>"{\"charging\":"+pattern+",\"charged\":"+pattern+",\"badCharger\":"+pattern+"}";
    private static (string Root,string Dir) Root()
    {string root=Directory.CreateTempSubdirectory("b34x11-").FullName;string dir=Path.Combine(root,BackpackLightAnimations.ResourceDirectory);Directory.CreateDirectory(dir);return(root,dir);}
    private static SourceLogSink Capture(List<EngineLogEntry> entries)=>(l,c,k,t)=>entries.Add(new(l,c,k,t,new Dictionary<string,string>()));

    [Fact]
    public void ColorThrowPrecedesMissingOffsetValidityAndLeavesErrorFlagClear()
    {
        var(root,dir)=Root();
        try
        {
            File.WriteAllText(Path.Combine(dir,"p.json"),All(Pattern("[\"bad\",0,0,1]",offset:false)));
            var stack=new List<string>();var engine=new List<EngineLogEntry>();EngineErrorState.ErrorFlagSet=false;
            var lib=BackpackLightAnimations.Load(root,stack.Add,Capture(engine));
            Assert.Equal(new[]{"OffCharger"},lib.Names);Assert.False(EngineErrorState.ErrorFlagSet);
            Assert.DoesNotContain(engine,e=>e.Level==EngineLogLevel.Error);
            Assert.Contains(stack,l=>l=="event handler threw: JsonLogicError: Value is not convertible to float.");
        }
        finally{Directory.Delete(root,true);}
    }

    [Fact]
    public void ExistingChargingNameStillConvertsAndBadRedefinitionStopsLaterStates()
    {
        var(root,dir)=Root();
        try
        {
            File.WriteAllText(Path.Combine(dir,"a.json"),"{}");File.WriteAllText(Path.Combine(dir,"b.json"),"{}");
            int sequence=0;var stack=new List<string>();var engine=new List<EngineLogEntry>();
            var lib=BackpackLightAnimations.Load(root,stack.Add,Capture(engine),deferLoaderLogs:false,beforeStat:f=>{
                // Assign good/bad payloads by the actual collected order, not an assumed filesystem order.
                File.WriteAllText(f,sequence++==0 ? "{\"charging\":"+Pattern()+"}" : "{\"charging\":"+Pattern(period:"-1")+",\"charged\":"+Pattern()+",\"badCharger\":"+Pattern()+"}");
            });
            Assert.Contains("Charging",lib.Names);Assert.DoesNotContain("Charged",lib.Names);Assert.DoesNotContain("BadCharger",lib.Names);
            Assert.Contains(stack,l=>l=="event handler threw: JsonLogicError: LargestInt out of UInt range");
            Assert.Equal(2,engine.Count(e=>e.Name=="BackpackLightAnimationContainer.AddBackpackLightStateValues.InvalidJson")); // only the first file's absent states
        }
        finally{Directory.Delete(root,true);}
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void AllColorElementsConvertBeforeTheFirstPeriod(bool offColors)
    {
        var(root,dir)=Root();
        try
        {
            string pattern=Pattern(period:"-1");
            string good="[[1,0,0,1],[1,0,0,1],[1,0,0,1],[1,0,0,1],[1,0,0,1]]";
            string bad="[[1,0,0,1],[1,0,0,1],[\"bad\",0,0,1],[1,0,0,1],[1,0,0,1]]";
            string field=offColors ? "offColors" : "onColors";
            pattern=pattern.Replace("\""+field+"\":"+good,"\""+field+"\":"+bad);
            File.WriteAllText(Path.Combine(dir,"p.json"),All(pattern));var stack=new List<string>();
            BackpackLightAnimations.Load(root,stack.Add);
            Assert.Contains(stack,l=>l=="event handler threw: JsonLogicError: Value is not convertible to float.");
            Assert.DoesNotContain(stack,l=>l.Contains("LargestInt out of UInt range"));
        }
        finally{Directory.Delete(root,true);}
    }

    [Theory]
    [InlineData("[1,0,0]",255,0)] [InlineData("null",0,0)]
    public void JsonCppIndexSuppliesMissingOrNullColorChannels(string color,int red,int alpha)
    {
        var(root,dir)=Root();
        try
        {
            File.WriteAllText(Path.Combine(dir,"p.json"),All(Pattern(color)));
            var engine=new List<EngineLogEntry>();var lib=BackpackLightAnimations.Load(root,null,Capture(engine));
            Assert.All(lib.Get("Charging")!,l=>Assert.Equal(new LedColor((byte)red,0,0,(byte)alpha),l.OnColor));
            Assert.DoesNotContain(engine,e=>e.Level==EngineLogLevel.Error);
        }
        finally{Directory.Delete(root,true);}
    }

    [Fact]
    public void NumericColorEntryThrowsJsonLogicErrorAndSkipsFile()
    {
        var(root,dir)=Root();
        try
        {
            File.WriteAllText(Path.Combine(dir,"p.json"),All(Pattern("1")));
            var stack=new List<string>();var lib=BackpackLightAnimations.Load(root,stack.Add);
            Assert.Equal(new[]{"OffCharger"},lib.Names);
            Assert.Contains(stack,l=>l=="event handler threw: JsonLogicError: in Json::Value::operator[](ArrayIndex)const: requires arrayValue");
            Assert.DoesNotContain(stack,l=>l.Contains("ArgumentOutOfRangeException"));
        }
        finally{Directory.Delete(root,true);}
    }
}
