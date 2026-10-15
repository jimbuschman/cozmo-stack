using System.Text;
using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Cozmo.Transport;
using Xunit;
namespace Cozmo.Protocol.Tests;
[Collection("SteppedBehavior missing-report statics")]
public sealed class M3M4X14BuilderTests : IDisposable
{
    private readonly bool _error = EngineErrorState.ErrorFlagSet;
    public void Dispose() => EngineErrorState.ErrorFlagSet = _error;
    private static SourceLogSink Capture(List<EngineLogEntry> entries) => (l,c,k,t) => { lock(entries) entries.Add(new(l,c,k,t,new Dictionary<string,string>())); };
    private static string Entry => """
        {"pattern":{"onColors":[[1,2,3,4],[1],[1],[1]],"offColors":[[0,0,0,255],[0],[0],[0]],
        "onPeriod_ms":[1.9,true,null,4],"offPeriod_ms":[2,2,2,2],"transitionOnPeriod_ms":[0,0,0,0],
        "transitionOffPeriod_ms":[0,0,0,0],"offset":[-1.9,0,0,0],"rotationPeriod_ms":3.9,"makeRelative":true},
        "duration_ms":9.9,"canBeOverridden":0}
        """;
    private static void Define(CubeLightAnimations lib, string text, List<EngineLogEntry> entries)
    { Assert.True(Json.TryParseFirst(Encoding.UTF8.GetBytes(text), out var doc)); using(doc) lib.Define(doc.RootElement, null, Capture(entries)); }
    [Fact]
    public void LoaderFirstByteSortedNameScalarEmptyReplaceDefaultsAndConversions()
    {
        var lib = new CubeLightAnimations(); var logs = new List<EngineLogEntry>();
        Define(lib, "{\"z\":[],\"a\":["+Entry+"]}", logs);
        Assert.Equal(new[]{"a"},lib.AnimationNames);
        Assert.Equal("Expecting only one animation per json file, found 2. Will use first: a",Assert.Single(logs).Text);
        Define(lib,"{\"a\":17}",logs); // scalar size==0 replaces the definition with an empty list
        Assert.Equal(new[]{"a"},lib.AnimationNames);
        // Direct loader plus trigger map exercises the public load entry and the same parser.
        var root = M4ControlTests.CubeLightResources();
        try
        {
            File.WriteAllText(Path.Combine(root,CubeLightAnimations.AnimationDirectory,"wakeUp.json"),"{/*comment*/\"wakeUp\":["+Entry.Replace("\"canBeOverridden\":0","\"canBeOverridden\":2")+"]}");
            var loaded = CubeLightAnimations.Load(root,sourceLog:Capture(logs)); var p = Assert.Single(loaded.ForTrigger("WakeUp")!);
            Assert.Equal("wakeUp0",p.DebugName); Assert.True(p.CanBeOverridden); Assert.False(p.MakeRelative);
            Assert.Equal(new LedColor(1,0,0,0),p.Leds[1].OnColor); Assert.Equal(1u,p.Leds[0].OnMs); Assert.Equal(-1,p.Leds[0].OffsetMs);
            Assert.Equal(3u,p.RotationPeriodMs); Assert.Equal(9u,p.DurationMs);
            Assert.Contains(logs,e=>e.Name=="RobotDataLoader.LoadCubeLightAnimations.LoadTime" && e.Channel=="Animations");
            File.WriteAllText(Path.Combine(root,CubeLightAnimations.AnimationDirectory,"extra.bin"),"{\"binAnim\":[]}");
            Assert.Contains("binAnim",CubeLightAnimations.Load(root).AnimationNames);
        }
        finally { Directory.Delete(root,true); }
    }
    [Fact]
    public void LoaderErrorOrderAndFailedDefinitionKeepsPrevious()
    {
        var root = M4ControlTests.CubeLightResources(); var logs = new List<EngineLogEntry>();
        try
        {
            var lib = CubeLightAnimations.Load(root); var old = lib.ForTrigger("WakeUp");
            Define(lib,"{\"wakeUp\":[{}]}",logs); Assert.Same(old,lib.ForTrigger("WakeUp"));
            Assert.Equal("CubeLightAnimationContainer.ParseJsonToAnim.InvalidJson",Assert.Single(logs).Name);
            logs.Clear(); EngineErrorState.ErrorFlagSet=false;
            Define(lib,"{\"wakeUp\":["+Entry.Replace("[[1,2,3,4],[1],[1],[1]]","[]")+"]}",logs);
            Assert.Equal(new[]{"JsonTools.GetColorValuesToArrayOptional.DiffSizes","CubeLightAnimationContainer.ParseJsonToAnim.InvalidPattern"}, logs.Select(e=>e.Name));
            Assert.True(EngineErrorState.ErrorFlagSet); Assert.Same(old,lib.ForTrigger("WakeUp"));
            logs.Clear(); Define(lib,"{}",logs); Assert.Equal("CubeLightAnimationContainer.DefineFromJson.EmptyFile",Assert.Single(logs).Name);
        }
        finally { Directory.Delete(root,true); }
    }
    [Theory]
    [InlineData("[1]")] [InlineData("7")] [InlineData("{\"wakeUp\":{\"x\":1}}")]
    [InlineData("{\"wakeUp\":[1]}")] [InlineData("pattern")]
    [InlineData("wrongUInt")] [InlineData("outUInt")] [InlineData("wrongBool")] [InlineData("outInt")] [InlineData("stack")]
    public void FatalFileIsSkippedKeepsPriorAndContinues(string kind)
    {
        var root=M4ControlTests.CubeLightResources(); var logs=new List<EngineLogEntry>();
        try
        {
            string payload=kind switch
            {
                "pattern"=>"{\"wakeUp\":[{\"pattern\":1,\"duration_ms\":0}]}",
                "wrongUInt"=>"{\"wakeUp\":["+Entry.Replace("9.9","\"bad\"")+"]}",
                "outUInt"=>"{\"wakeUp\":["+Entry.Replace("9.9","4294967296")+"]}",
                "wrongBool"=>"{\"wakeUp\":["+Entry.Replace("\"canBeOverridden\":0","\"canBeOverridden\":\"bad\"")+"]}",
                "outInt"=>"{\"wakeUp\":["+Entry.Replace("-1.9","2147483648")+"]}",
                "stack"=>new string('[',1001)+"0"+new string(']',1001),
                _=>kind,
            };
            // A crafted file traverses the production file reader, every fatal category is covered.
            File.WriteAllText(Path.Combine(root,CubeLightAnimations.AnimationDirectory,"fatal.bin"),payload);
            var stack=new List<string>();var lib=CubeLightAnimations.Load(root,log:stack.Add,sourceLog:Capture(logs));
            Assert.NotNull(lib.ForTrigger("Sleep")); Assert.Contains(stack,e=>e.StartsWith("Stack.CubeLightLoader.SkipFatalFile:") && e.Contains("M1-034 / M4-044"));
            Assert.DoesNotContain(logs,e=>e.Name=="Stack.CubeLightLoader.SkipFatalFile");
            if(kind!="stack")
            {
                var old=lib.ForTrigger("WakeUp"); Define(lib,payload,logs); Assert.Same(old,lib.ForTrigger("WakeUp"));
            }
        }
        finally { Directory.Delete(root,true); }
    }
    [Fact]
    public void BlendWholeWordAndClampedLEDWarningsUseExactWords()
    {
        var zero=new LedColor(0,0,0,0); var current=Enumerable.Repeat(LedPattern.Solid(new LedColor(0x12,0x34,0x56,0x78)),4).ToArray();
        var source=new[]{new CubeLightPattern(new[]{LedPattern.Solid(zero),LedPattern.Solid(new LedColor(0,0,0,1)),LedPattern.Solid(zero),LedPattern.Solid(zero)},0,1,true,false,"")};
        var result=CubeLightComponent.BlendAnimWithCurLights(source,current,out bool changed);
        Assert.True(changed); Assert.Equal(0x12345678u,CubeLightComponent.ColorWord(result[0].Leds[0].OnColor));
        Assert.Equal(1u,CubeLightComponent.ColorWord(result[0].Leds[1].OnColor)); Assert.Equal(zero,source[0].Leds[0].OnColor);
        var logs=new List<EngineLogEntry>(); CubeLightComponent.GetLEDState(current,4,Capture(logs)); CubeLightComponent.GetLEDState(current,-7,Capture(logs));
        Assert.Equal(new[]{"Requested LED index is too large (4 > 3). Returning 3.","LED index should be >= 0, not -7. Using 0."},logs.Select(e=>e.Text));
    }
    [Fact]
    public void ModifierOrsWordsAddsPerLedPeriodsFourRotationAddsAndCopiesRawPoint()
    {
        var source=new[]{new CubeLightPattern(Enumerable.Repeat(new LedPattern(new(0x10,0,0,1),new(0,2,0,0),uint.MaxValue,7,9,11,-1),4).ToArray(),uint.MaxValue,123,false,false,"n")};
        var mods=new[]{new LedPattern(new(0,0x20,0,0),new(0,0,0x40,8),2,3,4,5,6), new LedPattern(new(0,0,0,0),new(0,0,0,0),12,13,14,15,16),new LedPattern(new(0,0,0,0),new(0,0,0,0),22,23,24,25,26),new LedPattern(new(0,0,0,0),new(0,0,0,0),32,33,34,35,36)};
        var point=new System.Numerics.Vector2(BitConverter.Int32BitsToSingle(unchecked((int)0x7fc12345)),BitConverter.Int32BitsToSingle(unchecked((int)0x80000000)));
        var p=CubeLightComponent.ApplyAnimModifier(source,new(mods,2,2,point))[0];
        Assert.Equal(new LedColor(0x10,0x20,0,1),p.Leds[0].OnColor); Assert.Equal(new LedColor(0,2,0x40,8),p.Leds[0].OffColor);
        // Native 0x638AD0 uses mod+4i; each of the five arrays adds its own LED value, wrapping u32.
        Assert.Equal(new uint[]{1,11,21,31},p.Leds.Select(l=>l.OnMs));
        Assert.Equal(new uint[]{10,20,30,40},p.Leds.Select(l=>l.OffMs));
        Assert.Equal(new uint[]{13,23,33,43},p.Leds.Select(l=>l.TransitionOnMs));
        Assert.Equal(new uint[]{16,26,36,46},p.Leds.Select(l=>l.TransitionOffMs));
        Assert.Equal(new[]{5,15,25,35},p.Leds.Select(l=>l.OffsetMs));
        Assert.Equal(7u,p.RotationPeriodMs);Assert.Equal(2,p.RelativeMode);Assert.Equal(0x7fc12345,BitConverter.SingleToInt32Bits(p.RelativePoint.X));
        Assert.Equal(unchecked((int)0x80000000),BitConverter.SingleToInt32Bits(p.RelativePoint.Y)); Assert.Equal(123u,p.DurationMs);Assert.False(p.CanBeOverridden);Assert.Equal("n",p.DebugName);
    }
    [Theory]
    [InlineData("DanceFadeOff","fadeOff_1sec")] [InlineData("Flash","flash")] [InlineData("SpeedTapWin","speedTapWin")]
    public void ShippedZeroAnimationsBlendThroughConnectedProductionPlay(string trigger,string name)
    {
        // The shipped three zero-word animations, native 0x638B8C/98, rather than a synthetic blend gate.
        string root=M4ControlTests.CubeLightResources();
        try
        {
            string shipped=Path.Combine(FindRepo(),"re-analysis","obb","assets","cozmo_resources",CubeLightAnimations.AnimationDirectory);
            string source=Directory.EnumerateFiles(shipped,"*.json",SearchOption.AllDirectories).Single(f=>Path.GetFileNameWithoutExtension(f)==name);
            File.Copy(source,Path.Combine(root,CubeLightAnimations.AnimationDirectory,name+".json"));
            string map=Path.Combine(root,CubeLightAnimations.TriggerMapPath);string json=File.ReadAllText(map);json=json.Replace("\"Pairs\": [","\"Pairs\": [{\"CladEvent\":\""+trigger+"\",\"AnimName\":\""+name+"\"},");File.WriteAllText(map,json);
            using var rig=new M4ControlTests.Rig(root);rig.ToSynced();rig.State();M4ControlTests.ConnectCube(rig,slot:4);
            var component=rig.Robot.Lights.Cubes;var before=component.PlayingForTest(ObjectType.Block_LIGHTCUBE1,2)!.Value;
            Assert.True(component.PlayLightAnim(ObjectType.Block_LIGHTCUBE1,trigger,2),string.Join("\n",rig.Log));
            var playing=component.PlayingForTest(ObjectType.Block_LIGHTCUBE1,2)!.Value;
            Assert.True(playing.Blended);Assert.Equal(before.Name+"+"+name,playing.Name);
            var original=component.Animations.ForTrigger(trigger)!;
            for(int p=0;p<original.Length;p++) for(int i=0;i<4;i++)
                if(CubeLightComponent.ColorWord(original[p].Leds[i].OnColor)==0) Assert.Equal(new LedColor(0,255,0,255),playing.Patterns[p].Leds[i].OnColor);
            Assert.False(component.PlayLightAnim(ObjectType.Block_LIGHTCUBE1,trigger,2));
        }
        finally {Directory.Delete(root,true);}
    }
    [Fact]
    public void NonBlendedZeroDurationClearsAndStopSerializerRunsAfterUpdate()
    {
        string root=M4ControlTests.CubeLightResources();
        try
        {
            using var rig=new M4ControlTests.Rig(root);rig.ToSynced();rig.State();M4ControlTests.ConnectCube(rig);
            var component=rig.Robot.Lights.Cubes;var logs=new List<EngineLogEntry>();rig.Engine.LogEntry+=logs.Add;
            Define(component.Animations,"{\"sleep\":["+Entry.Replace("9.9","0").Replace("[[0,0,0,255],[0],[0],[0]]","[[0,0,0,255],[0,0,0,255],[0,0,0,255],[0,0,0,255]]").Replace("\"canBeOverridden\":0","\"canBeOverridden\":true")+"]}",logs);
            Assert.True(component.PlayLightAnim(ObjectType.Block_LIGHTCUBE1,"Sleep",2));Assert.Equal(1,component.PlayingForTest(ObjectType.Block_LIGHTCUBE1,2)!.Value.Count);
            component.StopLightAnim("Sleep",2,pickDefaultLayer:false);
            var entry=Assert.Single(logs.Where(e=>e.Name=="CubeLightComponent.StopLignAnim.Result"));
            Assert.Equal(EngineLogLevel.Debug,entry.Level);Assert.Matches("^found anim 'Sleep'. Current state: object=-?[0-9]+ layer=State currLayer=State \\[\\]$",entry.Text);
            rig.Robot.VisionModeRecipient!.World.RemoveConnectedActiveObject(0);
            var zero=new LedColor(0,0,0,0);var mod=new CubeLightModifier(Enumerable.Repeat(LedPattern.Solid(zero),4).ToArray(),0,0,default);
            var missing=new List<string>();rig.Engine.StackLogLine+=missing.Add;
            Assert.False(component.PlayLightAnim(ObjectType.Block_LIGHTCUBE1,"Sleep",2,modifier:true,lights:mod));
            Assert.Contains(missing,l=>l.Contains("M4-045 / M15-037"));
            Assert.Contains(logs,l=>l.Name=="CubeLightComponent.BlendAnimWithCurLights.NullActiveObject");
        }
        finally {Directory.Delete(root,true);}
    }
    [Fact]
    public void LiveModifierFeedsBlendLayerOneOverloadAndBackBlendedDiscardsIt()
    {
        string root=M4ControlTests.CubeLightResources();
        try
        {
            using var rig=new M4ControlTests.Rig(root);rig.ToSynced();rig.State();M4ControlTests.ConnectCube(rig);
            var component=rig.Robot.Lights.Cubes;var zero=new LedColor(0,0,0,0);
            var mod=new CubeLightModifier(Enumerable.Repeat(new LedPattern(new(0x80,0,0,0),zero,1,0,0,0,0),4).ToArray(),0,0,default);
            uint now=rig.Engine.Timer.TimeStampMs;
            Assert.True(component.PlayLightAnim(ObjectType.Block_LIGHTCUBE1,"Sleep",modifier:true,lights:mod,timerOffsetMs:-100));
            var p=component.PlayingForTest(ObjectType.Block_LIGHTCUBE1,1)!.Value;
            Assert.True(p.Blended);Assert.Equal(new LedColor(0x80,255,0,255),p.Patterns[0].Leds[0].OnColor);
            // native 0x6386DE ORs duration and signed offset before two wrapping adds: sum zero still stores now.
            Assert.Equal(now,p.Timer);
            Assert.True(component.PlayLightAnim(ObjectType.Block_LIGHTCUBE1,"Sleep",modifier:true,lights:mod));
            p=component.PlayingForTest(ObjectType.Block_LIGHTCUBE1,1)!.Value;
            Assert.False(p.Blended);Assert.Equal(new LedColor(0,255,0,255),p.Patterns[0].Leds[0].OnColor);
            rig.Robot.Lights.Cubes.StopAllAnimsOnLayer(1);
            var logs=new List<EngineLogEntry>();rig.Engine.LogEntry+=logs.Add;
            component.StopLightAnim("Sleep",1,pickDefaultLayer:false);
            Assert.Contains(logs,e=>e.Name=="CubeLightComponent.StopLignAnim.Result" && e.Text.StartsWith("did not find anim 'Sleep'."));
        }
        finally { Directory.Delete(root,true); }
    }
    private static string FindRepo()
    {for(var d=new DirectoryInfo(AppContext.BaseDirectory);d!=null;d=d.Parent)if(File.Exists(Path.Combine(d.FullName,"PROJECT_STATE.md")))return d.FullName;throw new Exception("repo missing");}
}
