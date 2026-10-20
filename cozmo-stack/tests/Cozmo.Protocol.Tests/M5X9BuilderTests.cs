using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Cozmo.Robot;
using Cozmo.Robot.Animation;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public class M5X9BuilderTests : IDisposable
{
    private readonly bool _error = EngineErrorState.ErrorFlagSet;
    public void Dispose() => EngineErrorState.ErrorFlagSet = _error;
    private record Entry(EngineLogLevel Level, string? Channel, string Key, string Text);
    private static SourceLogSink Capture(ConcurrentQueue<Entry> entries) => (l, c, k, t) => entries.Enqueue(new(l, c, k, t));
    private static string Root()
    { string p = Path.Combine(Path.GetTempPath(), "m59-" + Guid.NewGuid()); Directory.CreateDirectory(Path.Combine(p, "animations")); return p; }
    private static void Check(IEnumerable<Entry> entries, string key, EngineLogLevel level, string? channel, string text)
    { var e = Assert.Single(entries, e => e.Key == key); Assert.Equal(level, e.Level); Assert.Equal(channel, e.Channel); Assert.Equal(text, e.Text); }
    private static AnimationClip? Json(string text, ConcurrentQueue<Entry> entries, Action<string>? stack = null)
    { using var doc = JsonDocument.Parse(text); return JsonClipLoader.LoadDocument(doc, stack, Capture(entries)); }

    [Fact]
    public void OptionalNumericBoolConvertsAndExplicitNullRadiusUsesMacro()
    {
        // B-M5-9 1/2: 0x008E61BE asInt(bool); 0x004FB91C optional<short>, null -> macro.
        var entries = new ConcurrentQueue<Entry>();
        var c = Json("""{"c":[{"Name":"BodyMotionKeyFrame","triggerTime_ms":0,"durationTime_ms":true,"speed":true,"radius_mm":true}]}""", entries)!;
        var body = Assert.IsType<BodyKeyframe>(Assert.Single(c.Keyframes));
        Assert.Equal(1u, body.DurationTimeMs); Assert.Equal((short)1, body.Speed); Assert.Equal((short)1, body.EncodedRadius); Assert.DoesNotContain(entries,e => e.Level is EngineLogLevel.Error or EngineLogLevel.Warning);
        entries.Clear();
        c = Json("""{"c":[{"Name":"BodyMotionKeyFrame","triggerTime_ms":0,"durationTime_ms":1,"speed":1,"radius_mm":null}]}""", entries)!;
        Assert.True(c.LoadTruncated);
        Check(entries, "IKeyFrame.GetMemberFromJsonMacro", EngineLogLevel.Error, null, "Failed to get 'radius_mm' from Json file.");
        Assert.DoesNotContain(entries, e => e.Key.EndsWith("MissingRadius"));
    }

    // reach: unit (direct loader/streamer/helper or authored fixture inputs; complete robot entry and native producers are not claimed).
    [Theory]
    [InlineData("\"bad\"")] [InlineData("[]")] [InlineData("{}")]
    public void NumericIncompatibleTypesSkipWholeFileWithoutFakeEngineError(string value)
    {
        string root = Root();
        try
        {
            File.WriteAllText(Path.Combine(root, "animations", "bad.json"), "{\"bad\":[{\"Name\":\"BodyMotionKeyFrame\",\"triggerTime_ms\":0,\"durationTime_ms\":" + value + ",\"speed\":1,\"radius_mm\":0}]}");
            File.WriteAllText(Path.Combine(root, "animations", "good.json"), "{\"good\":[]}");
            var entries = new ConcurrentQueue<Entry>(); var stack = new ConcurrentQueue<string>(); EngineErrorState.ErrorFlagSet = false;
            var lib = AnimationLibrary.Open(root, stack.Enqueue, Capture(entries));
            Assert.False(lib.HasClip("bad")); Assert.True(lib.HasClip("good"));
            Assert.DoesNotContain(entries, e => e.Level == EngineLogLevel.Error); Assert.False(EngineErrorState.ErrorFlagSet);
            Assert.Contains(stack, s => s.StartsWith("Loader file skipped (M1-034): JsonCpp fatal conversion"));
        }
        finally { Directory.Delete(root, true); }
    }

    // reach: unit (direct loader/streamer/helper or authored fixture inputs; complete robot entry and native producers are not claimed).
    [Theory]
    [InlineData("5", "Animation.DefineFromJson.FrameMissing", "frame 1 of 'c' animation is missing or incorrect type.")]
    [InlineData("{}", "Animation.DefineFromJson.FrameNameMissing", "Missing 'Name' field for frame 1 of 'c' animation.")]
    [InlineData("{\"Name\":\"Wrong\"}", "Animation.DefineFromJson.UnrecognizedFrameName", "Frame 1 in 'c' animation has unrecognized name 'Wrong'.")]
    public void EarlyParentFailuresHaveOnlyTheirOwnTail(string frame, string key, string text)
    {
        var entries = new ConcurrentQueue<Entry>(); EngineErrorState.ErrorFlagSet = false;
        var c = Json("{\"c\":[{\"Name\":\"RecordHeadingKeyFrame\",\"triggerTime_ms\":0}," + frame + ",{\"Name\":\"RecordHeadingKeyFrame\",\"triggerTime_ms\":2}]}", entries)!;
        Assert.Single(c.Keyframes); Assert.True(c.LoadTruncated); Assert.True(EngineErrorState.ErrorFlagSet);
        Check(entries, key, EngineLogLevel.Error, null, text); Assert.Single(entries.Where(e => e.Level == EngineLogLevel.Error));
    }

    // reach: unit (direct loader/streamer/helper or authored fixture inputs; complete robot entry and native producers are not claimed).
    [Fact]
    public void TrackFailureUsesTheParentTailAndMissingTriggerStaysVisible()
    {
        var entries = new ConcurrentQueue<Entry>(); var stack = new List<string>();
        var clip = Json("""{"c":[{"Name":"RecordHeadingKeyFrame"}]}""", entries, stack.Add)!;
        Assert.True(clip.LoadTruncated); Assert.DoesNotContain(stack, l => l.Contains("MISSING: M5-001 IKeyFrame.ReadFromJson"));
        Check(entries, "IKeyFrame.ReadFromJson", EngineLogLevel.Error, null, "c: Expecting 'triggerTime_ms' field in KeyFrame Json");
        Check(entries, "Animation.DefineFromJson.AddKeyFrameFailure", EngineLogLevel.Error, null, "Adding RecordHeadingKeyFrame frame 0 failed.");
    }

    [Theory]
    [InlineData("Count")]
    [InlineData("Count\\u0000x")]   // cut at the first NUL (strlen 0x0058C5AC), so still Count -> BadType
    public void BadMoodDoesNotSetErrorBeforeParentAndGroupContainerTailIsExact(string mood)
    {
        string root = Root(); Directory.CreateDirectory(Path.Combine(root, "animationGroups"));
        try
        {
            File.WriteAllText(Path.Combine(root, "animations", "c.json"), "{\"c\":[]}");
            File.WriteAllText(Path.Combine(root, "animationGroups", "g.json"), "{\"Animations\":[{\"Name\":\"c\",\"Weight\":1,\"Mood\":\""+mood+"\"}]}");
            EngineErrorState.ErrorFlagSet = false; var seen = new ConcurrentQueue<(string Key, bool Error)>(); var entries = new ConcurrentQueue<Entry>();
            var lib = AnimationLibrary.Open(root, sourceLog: (l, c, k, t) => { entries.Enqueue(new(l,c,k,t)); if (k.Contains("BadType") || k.Contains("AddEntryFailure")) seen.Enqueue((k, EngineErrorState.ErrorFlagSet)); });
            Assert.Equal(new[] { ("SimpleMoodScorer.ReadFromJson.BadType", false), ("AnimationGroup.DefineFromJson.AddEntryFailure", false) }, seen);
            Assert.True(EngineErrorState.ErrorFlagSet); Assert.Empty(lib.GetGroup("g")!.Entries);
            Check(entries, "AnimationGroupContainer.DefineAnimationGroupFromJson", EngineLogLevel.Error, null, "Failed to define animation group 'g' from Json.");
            Assert.True(entries.ToList().FindIndex(e => e.Key.EndsWith("AddEntryFailure")) < entries.ToList().FindIndex(e => e.Key == "AnimationGroupContainer.DefineAnimationGroupFromJson"));
            entries.Clear(); File.WriteAllText(Path.Combine(root, "animationGroups", "g.json"), "{\"Other\":1}");
            AnimationLibrary.Open(root, sourceLog: Capture(entries));
            Check(entries, "AnimationGroupContainer.DefineAnimationGroupFromJson", EngineLogLevel.Error, null, "Failed to define animation group 'g' from Json.");
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void GroupLoaderSkipsEmptyValuesAcceptsBinSuffixAndInfoPrecedesDefinition()
    {
        string root = Root(); string groups = Path.Combine(root, "animationGroups"); Directory.CreateDirectory(groups);
        try
        {
            File.WriteAllText(Path.Combine(groups, "empty.json"), "{}");
            File.WriteAllText(Path.Combine(groups, "array.json"), "[]");
            File.WriteAllText(Path.Combine(groups, "g.bin"), "{\"Animations\":[]}");
            var entries = new ConcurrentQueue<Entry>(); var lib = AnimationLibrary.Open(root, sourceLog: Capture(entries));
            Assert.Equal(new[] { "g" }, lib.GroupNames);
            Check(entries, "RobotDataLoader.LoadAnimationGroupFile.LoadingSpecificAnimGroupFromJson", EngineLogLevel.Info, "Animations", $"Loading 'g' from {Path.Combine(groups,"g.bin")}");
            Assert.DoesNotContain(entries, e => e.Key == "AnimationGroup.DefineFromJson.NoAnimations");
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void GroupAbortSkipsReadAndDefinitionAndDispatchHasFourContiguousRuns()
    {
        string root = Root(); Directory.CreateDirectory(Path.Combine(root, "animationGroups"));
        try
        {
            File.WriteAllText(Path.Combine(root, "animationGroups", "g.json"), "{\"Animations\":[]}");
            var entries = new ConcurrentQueue<Entry>();
            var lib = AnimationLibrary.OpenForLoad(root, null, Capture(entries), null, () => true, null, null, loaderCreated: lib => lib.AbortLoading());
            Assert.Empty(lib.GroupNames); Assert.DoesNotContain(entries, e => e.Key.Contains("LoadingSpecificAnimGroup"));
            var threads = new ConcurrentDictionary<int, List<int>>(); int caller = Environment.CurrentManagedThreadId;
            AnimationLibrary.DispatchFiles(Enumerable.Range(0, 7).Select(i => i.ToString()).ToArray(), s => { var list = threads.GetOrAdd(Environment.CurrentManagedThreadId, _ => new()); lock(list) list.Add(int.Parse(s)); });
            Assert.Equal(new[] { 6 }, threads[caller]);
            Assert.Equal(new[] { "0,1", "2,3", "4,5", "6" }, threads.Values.Select(l => string.Join(",", l)).OrderBy(s => s));
        }
        finally { Directory.Delete(root, true); }
    }

    // reach: unit (direct loader/streamer/helper or authored fixture inputs; complete robot entry and native producers are not claimed).
    [Fact]
    public void ReadAsJsonErrorFlagThenConditionalDebugWithOwnedTextArgument()
    {
        // G5a text is a supplied %s argument. This does not claim recovery of G5b reader text.
        var entries = new ConcurrentQueue<Entry>(); var stack = new List<string>();
        EngineErrorState.ErrorFlagSet = false;
        AnimationLibrary.ReportReadAsJsonFailure("file", "reader argument\n", stack.Add, (l,c,k,t) => { if(l == EngineLogLevel.Debug) Assert.True(EngineErrorState.ErrorFlagSet); entries.Enqueue(new(l,c,k,t)); });
        var a = entries.ToArray(); Assert.Equal(2, a.Length);
        Assert.Equal(new Entry(EngineLogLevel.Error, null, "DataPlatform.readAsJson", "Failed to read [file]"), a[0]);
        Assert.Equal(new Entry(EngineLogLevel.Debug, "Unnamed", "DataPlatform.readAsJson", "Json reader errors [reader argument\n]"), a[1]);
        entries.Clear(); AnimationLibrary.ReportReadAsJsonFailure("file", "", stack.Add, Capture(entries)); Assert.Single(entries);
        entries.Clear(); AnimationLibrary.ReportReadAsJsonFailure("file", null, stack.Add, Capture(entries)); Assert.Single(entries); Assert.Single(stack);
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)]
    [InlineData(9)] [InlineData(10)] [InlineData(11)] [InlineData(12)] [InlineData(13)] [InlineData(14)] [InlineData(15)]
    public void ResultsEveryPopulationIsReverseFirstInsertion(int mask)
    {
        var entries = new ConcurrentQueue<Entry>(); var stack = new List<string>();
        AnimationLibrary.EmitCollectResults(Enumerable.Range(0,4).Select(t => (mask & (1<<t)) != 0 ? 10 + t : 0).ToArray(), true, stack.Add, Capture(entries));
        Assert.Equal(new[] { 1,3,2,0 }.Where(t => (mask & (1<<t)) != 0), entries.Select(e => int.Parse(e.Text[^1..])));
        Assert.All(entries, e => { Assert.Equal(EngineLogLevel.Info,e.Level); Assert.Equal("Animations",e.Channel); Assert.Equal($"Found {10 + int.Parse(e.Text[^1..])} animation files of type {e.Text[^1..]}",e.Text); }); Assert.Empty(stack);
    }

    // reach: unit (direct loader/streamer/helper or authored fixture inputs; complete robot entry and native producers are not claimed).
    [Fact]
    public void EagerFaceLoadUsesByteSortGapFillAndBothShippedGrayscaleDepths()
    {
        string root = Root(); string dir = Path.Combine(root,"faceAnimations","f"); Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllBytes(Path.Combine(dir,"f_0.png"), Png(128,64,8,128));
            File.WriteAllBytes(Path.Combine(dir,"f_10.png"), Png(128,64,1,255));
            File.WriteAllBytes(Path.Combine(dir,"f_2.png"), Png(128,64,8,0));
            var library = FaceAnimationLibrary.Open(root);
            File.Delete(Path.Combine(dir,"f_10.png")); // Lookup cannot perform another image read.
            var frames = library.Variants("f")!;
            Assert.Equal(11,frames.Count); Assert.All(frames.Skip(1).Take(8), f => { Assert.Empty(f.EvenRowsCleared); Assert.Empty(f.OddRowsCleared); });
            Assert.Equal(0,library.Frames("f")![0][0,0]); Assert.NotEqual(0,library.Frames("f")![9][0,0]); Assert.Equal(0,library.Frames("f")![10][0,0]);
        }
        finally { Directory.Delete(root,true); }
    }

    // reach: unit (direct loader/streamer/helper or authored fixture inputs; complete robot entry and native producers are not claimed).
    [Theory]
    [InlineData("a", "Could not find '.' in frame filename a")]
    [InlineData("a.png", "Could not find '_' in frame filename a.png")]
    [InlineData("a_.png", "Unexpected relative positions for '.' and '_' in frame filename a_.png")]
    [InlineData("a_-1.png", "Found negative frame number (-1) for filename 'a_-1.png'.")]
    [InlineData("a_x.png", "Could not get frame number from substring 'x' of filename 'a_x.png'.")]
    public void FaceNameAndNumberErrorAbandonTheAnimation(string filename, string text)
    {
        string root=Root(); string dir=Path.Combine(root,"faceAnimations","f"); Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllBytes(Path.Combine(dir,filename),Png(128,64,8,255)); File.WriteAllBytes(Path.Combine(dir,"z_0.png"),Png(128,64,8,255));
            var entries=new ConcurrentQueue<Entry>(); var library=FaceAnimationLibrary.Open(root,sourceLog:Capture(entries));
            Assert.Empty(library.Variants("f")!); Check(entries,"FaceAnimationManager.ReadFaceAnimationDir",EngineLogLevel.Error,null,text);
        }
        finally { Directory.Delete(root,true); }
    }

    // reach: unit (direct loader/streamer/helper or authored fixture inputs; complete robot entry and native producers are not claimed).
    [Fact]
    public void FaceSizeFailureContinuesReservedNameRejectsAndOutOfRangeIsIsolated()
    {
        string root=Root(); string dir=Path.Combine(root,"faceAnimations","f"); Directory.CreateDirectory(dir); Directory.CreateDirectory(Path.Combine(root,"faceAnimations","_PROCEDURAL_"));
        try
        {
            File.WriteAllBytes(Path.Combine(dir,"f_0.png"),Png(2,3,8,255));
            File.WriteAllBytes(Path.Combine(dir,"f_2147483648.png"),Png(128,64,8,255));
            File.WriteAllBytes(Path.Combine(dir,"f_3.png"),Png(128,64,8,255));
            var entries=new ConcurrentQueue<Entry>(); var stack=new ConcurrentQueue<string>(); var library=FaceAnimationLibrary.Open(root,stack.Enqueue,Capture(entries));
            Assert.False(library.Has("_PROCEDURAL_")); Check(entries,"FaceAnimationManager.ReadFaceAnimationDir.ReservedName",EngineLogLevel.Error,null,"'_PROCEDURAL_' is a reserved face animation name. Ignoring.");
            Check(entries,"FaceAnimationManager.ReadFaceAnimationDir",EngineLogLevel.Error,null,$"Image in {Path.Combine(dir,"f_0.png")} is 2x3 instead of 128x64.");
            Assert.Equal(3,library.Variants("f")!.Count); Assert.Contains(stack,s=>s.StartsWith("Loader file skipped (M1-034): face frame stoi out_of_range"));
        }
        finally { Directory.Delete(root,true); }
    }

    [Fact]
    public void SourceHandlerReleaseOrderUsesSoleOwnedRecipientsAndClearsEachOnce()
    {
        using var robot=CozmoRobot.CreateOffline(); var released=new List<byte>();
        robot.Engine.AnimationSubscriptionFactory=(tag,e2g)=>new Owner(()=>released.Add(tag));
        var driving=new AnimationSubscriptionOwners(robot.Engine,new byte[]{0x5A,0xB5,0xB6},true);
        var streamer=new AnimationSubscriptionOwners(robot.Engine,new byte[]{0xAE,0xAF,0xAD,0xC4},false);
        driving.Release(); streamer.Release(); driving.Release(); streamer.Release();
        Assert.Equal(new byte[]{0xB6,0xB5,0x5A,0xC4,0xAD,0xAF,0xAE},released);
    }
    private sealed class Owner(Action release):IDisposable { public void Dispose()=>release(); }

    // reach: unit (direct loader/streamer/helper or authored fixture inputs; complete robot entry and native producers are not claimed).
    [Fact]
    public void VerifyHasNoCallstackWritesAndFlushSeesErrorFlag()
    {
        var clip=new AnimationClip { Name="live",Keyframes=Array.Empty<Keyframe>() };
        var table=(ConditionalWeakTable<AnimationClip,StreamAnimation>)typeof(StreamAnimation).GetField("ForClip",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
        table.Add(clip,new StreamAnimation("live",true));
        var entries=new ConcurrentQueue<Entry>(); var stack=new List<string>(); int flushed=0;
        var scheduler=new AnimationScheduler(new Sink()){SourceLog=Capture(entries),Log=stack.Add,FlushSourceLogger=()=>{Assert.True(EngineErrorState.ErrorFlagSet);flushed++;}};
        EngineErrorState.ErrorFlagSet=false; scheduler.Play(clip,0,numLoops:3);
        Assert.Equal(1,flushed); Check(entries,"AnimationStreamer.SetStreamingAnimation.LoopingLiveAnimation",EngineLogLevel.Error,null,"VERIFY(numLoops == 1): Animation live is marked 'live' but numLoops=3. Clamping to 1.");
        Assert.DoesNotContain(stack,s=>s.Contains("callstack"));
    }

    // reach: unit (direct loader/streamer/helper or authored fixture inputs; complete robot entry and native producers are not claimed).
    [Fact]
    public void FrameGateAndFlushIgnoreHostCallbacksWhenRobotCountIsZero()
    {
        var entries=new ConcurrentQueue<Entry>(); var scheduler=new AnimationScheduler(new Sink()){SourceLog=Capture(entries)};
        scheduler.Stream.Buffer(null,0,false,()=>false);
        var anim=new StreamAnimation("a",false); anim.AddNew(new HeadKeyframe(0,1,0,0)); anim.Init();
        var gate=typeof(AnimationScheduler).GetMethod("ShouldProcessAnimationFrame",BindingFlags.Instance|BindingFlags.NonPublic)!;
        Assert.True((bool)gate.Invoke(scheduler,new object[]{anim})!); Assert.Equal(0,scheduler.Advance(0)); Assert.Empty(entries);
    }

    // reach: unit (direct loader/streamer/helper or authored fixture inputs; complete robot entry and native producers are not claimed).
    [Fact]
    public void FaceNameMissWarningIsSourceKeyedAndAuthoredJsonOverrideIsZero()
    {
        var entries=new ConcurrentQueue<Entry>(); var clip=Json("""{"c":[{"Name":"FaceAnimationKeyFrame","triggerTime_ms":0,"animName":"absent"}]}""",entries)!;
        // Native F01 memclr4 at 0x005642C6..0x005642CC establishes literal byte zero;
        // JSON call 0x0057734C and insertion copies never introduce a nonzero flag.
        Assert.Equal((byte)0,StreamAnimation.Of(clip).FaceAnim.Current!.FaceAnimationOverride);
        entries.Clear(); var scheduler=new AnimationScheduler(new Sink()){SourceLog=Capture(entries),FaceAnimationVariants=_=>null}; scheduler.Play(clip,0); scheduler.Advance(0);
        var warnings = entries.Where(e => e.Level == EngineLogLevel.Warning).ToArray();
        Assert.Equal(new[] { "FaceAnimationManager.GetAnimationByName.UnknownName", "FaceAnimationManager.GetNumFrames", "FaceAnimationManager.GetAnimationByName.UnknownName", "FaceAnimationManager.GetNumFrames" }, warnings.Select(e => e.Key));
        Assert.All(warnings, e => { Assert.Null(e.Channel); Assert.Equal("Unknown animation requested: absent", e.Text); });
    }


    [Fact]
    public void AuthoredFlatBufferOverrideIsZeroAfterDefinitionAndInsertion()
    {
        // CozmoAnim schema: root.clips[0], clip{name,keyframes}, keyframes.faceAnimations[0].
        var bytes=new List<byte>(new byte[4]); var vtables=new Dictionary<int,int>();
        void Put(int at,byte[] data){for(int i=0;i<data.Length;i++)bytes[at+i]=data[i];}
        int Table(int count)
        {
            int vt=bytes.Count;bytes.AddRange(new byte[4+2*count]);Put(vt,BitConverter.GetBytes((ushort)(4+2*count)));Put(vt+2,BitConverter.GetBytes((ushort)(4+4*count)));
            while(bytes.Count%4!=0)bytes.Add(0);int t=bytes.Count;bytes.AddRange(new byte[4+4*count]);Put(t,BitConverter.GetBytes(t-vt));vtables[t]=vt;return t;
        }
        int Slot(int table,int id){Put(vtables[table]+4+2*id,BitConverter.GetBytes((ushort)(4+4*id)));return table+4+4*id;}
        void Pointer(int slot,int target)=>Put(slot,BitConverter.GetBytes((uint)(target-slot)));
        int Vector(){while(bytes.Count%4!=0)bytes.Add(0);int at=bytes.Count;bytes.AddRange(BitConverter.GetBytes(1));bytes.AddRange(new byte[4]);return at;}
        int Text(string text){while(bytes.Count%4!=0)bytes.Add(0);int at=bytes.Count;var b=Encoding.UTF8.GetBytes(text);bytes.AddRange(BitConverter.GetBytes(b.Length));bytes.AddRange(b);bytes.Add(0);return at;}
        int root=Table(1);Put(0,BitConverter.GetBytes(root));int clips=Vector();Pointer(Slot(root,0),clips);
        int clip=Table(2);Pointer(clips+4,clip);Pointer(Slot(clip,0),Text("flat"));
        int keyframes=Table(6);Pointer(Slot(clip,1),keyframes);int faces=Vector();Pointer(Slot(keyframes,5),faces);
        int face=Table(2);Pointer(faces+4,face);Pointer(Slot(face,1),Text("sprite"));
        var parsed=Assert.Single(AnimationLibrary.Parse(bytes.ToArray()));
        Assert.IsType<FaceAnimationKeyframe>(Assert.Single(parsed.Keyframes));
        // F01 constructor -> FlatBuf caller 0x0057669C -> byte-preserving insertion.
        Assert.Equal((byte)0,StreamAnimation.Of(parsed).FaceAnim.Current!.FaceAnimationOverride);
    }

    // reach: production - live robot loader/constructor or public input with actual component wiring; fixture assets supplied where stated.
    [Fact]
    public void LiveLoaderOwnsAThreadAndFaceDecodeFollowsBackpackLoad()
    {
        string root=Root();Directory.CreateDirectory(Path.Combine(root,"faceAnimations","f"));
        try
        {
            File.WriteAllText(Path.Combine(root,"animations","c.json"),"{\"c\":[]}");
            File.WriteAllBytes(Path.Combine(root,"faceAnimations","f","f_0.png"),Png(128,64,1,255));
            using var robot=CozmoRobot.CreateOffline(engineOptions:new CozmoEngineOptions{ResourcesPath=root}); int caller=Environment.CurrentManagedThreadId;
            var threads=new ConcurrentQueue<int>(); var events=new ConcurrentQueue<string>(); bool? faceAbsentAtBackpack=null;
            robot.Engine.LogEntry+=e=>{if(e.Name=="RobotDataLoader.LoadAnimations.CannedAnimationsCount")threads.Enqueue(Environment.CurrentManagedThreadId);events.Enqueue(e.Name??"");if(e.Name=="RobotDataLoader.LoadBackpackLightAnimations.LoadTime")faceAbsentAtBackpack=robot.Animations.FaceAnimations is null;};
            robot.Animations.LoadFrom(root);
            Assert.NotEqual(caller,Assert.Single(threads));
            Assert.Contains("RobotDataLoader.LoadBackpackLightAnimations.LoadTime",events);Assert.True(faceAbsentAtBackpack);
            Assert.Single(robot.Animations.FaceAnimations!.Variants("f")!);
        }
        finally{Directory.Delete(root,true);}
    }

    [Fact]
    public void StreamerDestructionReleasesSetupBeforeLiveAndParameterLast()
    {
        var order=new List<string>();var scheduler=new AnimationScheduler(new Sink());
        scheduler.StreamLive(new HeadKeyframe(0,1,0,0),0);
        var live=(StreamAnimation)typeof(AnimationScheduler).GetField("_live",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(scheduler)!;
        scheduler.ReleaseSetupHandlers=()=>{Assert.False(live.Head.IsEmpty);order.Add("setup");};
        scheduler.ReleaseParameterSubscription=()=>{Assert.True(live.Head.IsEmpty);order.Add("parameter");};
        scheduler.Destroy();Assert.Equal(new[]{"setup","parameter"},order);
    }
    [Theory]
    [InlineData("Sad\\u0000x", SimpleMood.Sad)]   // cut at the first NUL (asCString/strlen 0x0058C59C..0x0058C5AC)
    [InlineData("Default\\u0000", SimpleMood.Default)]
    public void MoodIsCutAtTheFirstNul(string mood, SimpleMood expected)
    {
        string root=Root(); Directory.CreateDirectory(Path.Combine(root,"animationGroups"));
        try
        {
            File.WriteAllText(Path.Combine(root,"animations","c.json"),"{\"c\":[]}");
            File.WriteAllText(Path.Combine(root,"animationGroups","g.json"),"{\"Animations\":[{\"Name\":\"c\",\"Weight\":1,\"Mood\":\""+mood+"\"}]}");
            var entries=new ConcurrentQueue<Entry>(); EngineErrorState.ErrorFlagSet=false;
            var lib=AnimationLibrary.Open(root,sourceLog:Capture(entries));
            Assert.Equal(expected,Assert.Single(lib.GetGroup("g")!.Entries).MoodType);
            Assert.DoesNotContain(entries,e=>e.Level is EngineLogLevel.Warning or EngineLogLevel.Error);
        }
        finally { Directory.Delete(root,true); }
    }

    [Theory]
    [InlineData("invalid")] [InlineData("0")]
    public void UnknownMoodIsHappyWithoutEngineDiagnostics(string mood)
    {
        string root=Root(); Directory.CreateDirectory(Path.Combine(root,"animationGroups"));
        try
        {
            File.WriteAllText(Path.Combine(root,"animations","c.json"),"{\"c\":[]}");
            File.WriteAllText(Path.Combine(root,"animationGroups","g.json"),"{\"Animations\":[{\"Name\":\"c\",\"Weight\":1,\"Mood\":\""+mood+"\"}]}");
            var entries=new ConcurrentQueue<Entry>(); EngineErrorState.ErrorFlagSet=false;
            var lib=AnimationLibrary.Open(root,sourceLog:Capture(entries));
            Assert.Equal(SimpleMood.Happy,Assert.Single(lib.GetGroup("g")!.Entries).MoodType);
            Assert.DoesNotContain(entries,e=>e.Level is EngineLogLevel.Warning or EngineLogLevel.Error);
            Assert.False(EngineErrorState.ErrorFlagSet);
        }
        finally { Directory.Delete(root,true); }
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"Name\":\"absent\"}")]
    [InlineData("{\"Name\":\"c\"}")]
    [InlineData("{\"Name\":\"c\",\"Weight\":1}")]
    [InlineData("{\"Name\":\"c\",\"Weight\":1,\"Mood\":\"Count\"}")]
    [InlineData("{\"Name\":\"c\",\"Weight\":1,\"Mood\":\"Happy\",\"UseHeadAngle\":true}")]
    public void EveryGroupRejectionGetsContainerTailAndRetainsValidEntries(string rejected)
    {
        string root=Root(); Directory.CreateDirectory(Path.Combine(root,"animationGroups"));
        try
        {
            File.WriteAllText(Path.Combine(root,"animations","c.json"),"{\"c\":[]}");
            File.WriteAllText(Path.Combine(root,"animationGroups","g.json"),"{\"Animations\":["+rejected+",{\"Name\":\"c\",\"Weight\":1,\"Mood\":\"Happy\"}]}");
            var entries=new ConcurrentQueue<Entry>();
            var lib=AnimationLibrary.Open(root,sourceLog:Capture(entries));
            Assert.Equal("c",Assert.Single(lib.GetGroup("g")!.Entries).Name);
            var tail=entries.Where(e=>e.Key is "AnimationGroup.DefineFromJson.AddEntryFailure" or "AnimationGroupContainer.DefineAnimationGroupFromJson").ToArray();
            Assert.Equal(new[]{"AnimationGroup.DefineFromJson.AddEntryFailure","AnimationGroupContainer.DefineAnimationGroupFromJson"},tail.Select(e=>e.Key));
            Assert.Equal("Adding animation 0 failed.",tail[0].Text);
            Assert.Equal("Failed to define animation group 'g' from Json.",tail[1].Text);
            Assert.All(tail,e=>{Assert.Equal(EngineLogLevel.Error,e.Level);Assert.Null(e.Channel);});
        }
        finally { Directory.Delete(root,true); }
    }

    // reach: unit (direct loader/streamer/helper or authored fixture inputs; complete robot entry and native producers are not claimed).
    [Theory]
    [InlineData("-9223372036854775808","-9223372036854775808")]
    [InlineData("18446744073709551615","18446744073709551615")]
    [InlineData("1.0","1.0")]
    [InlineData("1.25","1.25")]
    [InlineData("1e20","1e+20")]
    [InlineData("1e-7","9.9999999999999995e-08")]
    [InlineData("true","true")] [InlineData("false","false")]
    public void FaceAndDeviceAudioNamesUseShippedScalarStringConversion(string value,string expected)
    {
        string root=Root();
        try
        {
            File.WriteAllText(Path.Combine(root,"animations","c.json"),"{\"c\":[{\"Name\":\"FaceAnimationKeyFrame\",\"triggerTime_ms\":0,\"animName\":"+value+"},{\"Name\":\"DeviceAudioKeyFrame\",\"triggerTime_ms\":0,\"audioName\":"+value+"}]}");
            var entries=new ConcurrentQueue<Entry>(); var lib=AnimationLibrary.Open(root,sourceLog:Capture(entries));
            var clip=lib.GetClip("c");
            Assert.Equal(expected,Assert.Single(clip.Keyframes.OfType<FaceAnimationKeyframe>()).AnimName);
            Assert.Equal(expected,Assert.Single(clip.Keyframes.OfType<DeviceAudioKeyframe>()).AudioName);
            Assert.False(clip.LoadTruncated); Assert.DoesNotContain(entries,e=>e.Level==EngineLogLevel.Error);
        }
        finally { Directory.Delete(root,true); }
    }

    // reach: unit (direct loader/streamer/helper or authored fixture inputs; complete robot entry and native producers are not claimed).
    [Theory]
    [InlineData("FaceAnimationKeyFrame","animName","[]")]
    [InlineData("FaceAnimationKeyFrame","animName","{}")]
    [InlineData("DeviceAudioKeyFrame","audioName","[]")]
    [InlineData("DeviceAudioKeyFrame","audioName","{}")]
    public void AggregateNameThrowsSkipWholeFile(string type,string member,string value)
    {
        string root=Root();
        try
        {
            File.WriteAllText(Path.Combine(root,"animations","c.json"),"{\"c\":[{\"Name\":\"RecordHeadingKeyFrame\",\"triggerTime_ms\":0},{\"Name\":\""+type+"\",\"triggerTime_ms\":1,\""+member+"\":"+value+"}]}");
            var entries=new ConcurrentQueue<Entry>();var stack=new ConcurrentQueue<string>();
            var lib=AnimationLibrary.Open(root,stack.Enqueue,Capture(entries));
            Assert.False(lib.HasClip("c"));Assert.DoesNotContain(entries,e=>e.Level==EngineLogLevel.Error);
            Assert.Contains(stack,l=>l.StartsWith("Loader file skipped (M1-034): JsonCpp fatal conversion"));
        }
        finally { Directory.Delete(root,true); }
    }

    // reach: unit (direct loader/streamer/helper or authored fixture inputs; complete robot entry and native producers are not claimed).
    [Theory]
    [InlineData(16,0,0)] [InlineData(8,2,0)] [InlineData(8,0,1)]
    public void UnsupportedPngSkipsWithStackMissingAndNoEngineSizeError(int depth,int colour,int interlace)
    {
        string root=Root();string dir=Path.Combine(root,"faceAnimations","f");Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllBytes(Path.Combine(dir,"f_0.png"),Png(128,64,depth,255,colour,interlace));
            File.WriteAllBytes(Path.Combine(dir,"f_1.png"),Png(128,64,8,255));
            var entries=new ConcurrentQueue<Entry>();var stack=new ConcurrentQueue<string>();
            var lib=FaceAnimationLibrary.Open(root,stack.Enqueue,Capture(entries));
            Assert.Single(lib.Variants("f")!);
            Assert.Contains(stack,l=>l.Contains("unsupported by host decoder"));
            Assert.DoesNotContain(entries,e=>e.Level==EngineLogLevel.Error);
        }
        finally { Directory.Delete(root,true); }
    }

    // reach: unit (direct loader/streamer/helper or authored fixture inputs; complete robot entry and native producers are not claimed).
    [Theory]
    [InlineData(999,false)] [InlineData(1000,true)] [InlineData(1001,true)]
    public void JsonDepthFatalUsesActiveValueDepthInsteadOfExceptionText(int containers,bool fatal)
    {
        string root=Root();
        try
        {
            File.WriteAllText(Path.Combine(root,"animations","deep.json"),new string('[',containers)+"0"+new string(']',containers));
            var entries=new ConcurrentQueue<Entry>();var stack=new ConcurrentQueue<string>();
            AnimationLibrary.Open(root,stack.Enqueue,Capture(entries));
            Assert.Equal(fatal,stack.Any(l=>l.Contains("stack-limit fatal path")));
            Assert.DoesNotContain(entries,e=>e.Key=="DataPlatform.readAsJson");
        }
        finally { Directory.Delete(root,true); }
    }

    // reach: unit (direct loader/streamer/helper or authored fixture inputs; complete robot entry and native producers are not claimed).
    [Fact]
    public void MalformedSecondBinaryTableRollsBackFirstRegistration()
    {
        // FlatBuffer fixture: root clips[2]; first empty clip is valid, second has an invalid keyframe pointer.
        var bytes=new List<byte>(new byte[4]);var vtables=new Dictionary<int,int>();
        void Put(int at,byte[] data){for(int i=0;i<data.Length;i++)bytes[at+i]=data[i];}
        int Table(int count)
        {
            int vt=bytes.Count;bytes.AddRange(new byte[4+2*count]);Put(vt,BitConverter.GetBytes((ushort)(4+2*count)));Put(vt+2,BitConverter.GetBytes((ushort)(4+4*count)));
            while(bytes.Count%4!=0)bytes.Add(0);int t=bytes.Count;bytes.AddRange(new byte[4+4*count]);Put(t,BitConverter.GetBytes(t-vt));vtables[t]=vt;return t;
        }
        int Slot(int t,int id){Put(vtables[t]+4+2*id,BitConverter.GetBytes((ushort)(4+4*id)));return t+4+4*id;}
        void Pointer(int slot,int target)=>Put(slot,BitConverter.GetBytes((uint)(target-slot)));
        int Text(string text){while(bytes.Count%4!=0)bytes.Add(0);int at=bytes.Count;byte[] b=Encoding.UTF8.GetBytes(text);bytes.AddRange(BitConverter.GetBytes(b.Length));bytes.AddRange(b);bytes.Add(0);return at;}
        int rt=Table(1);Put(0,BitConverter.GetBytes(rt));while(bytes.Count%4!=0)bytes.Add(0);
        int vector=bytes.Count;bytes.AddRange(BitConverter.GetBytes(2));bytes.AddRange(new byte[8]);Pointer(Slot(rt,0),vector);
        int first=Table(2);Pointer(vector+4,first);Pointer(Slot(first,0),Text("first"));
        int second=Table(2);Pointer(vector+8,second);Pointer(Slot(second,0),Text("second"));Put(Slot(second,1),BitConverter.GetBytes(0x100000u));
        string root=Root();
        try
        {
            File.WriteAllBytes(Path.Combine(root,"animations","bad.bin"),bytes.ToArray());
            var entries=new ConcurrentQueue<Entry>();var stack=new ConcurrentQueue<string>();
            var lib=AnimationLibrary.Open(root,stack.Enqueue,Capture(entries));
            Assert.False(lib.HasClip("first"));Assert.False(lib.HasClip("second"));
            Assert.Equal(new[]{"Loading 'first'","Loading 'second'"},entries.Where(e=>e.Key.EndsWith("LoadingSpecificAnimFromBinary")).Select(e=>e.Text.Split(" from ")[0]));
            Assert.Contains(stack,l=>l.Contains("malformed non-empty binary"));Assert.DoesNotContain(entries,e=>e.Level==EngineLogLevel.Error);
            Assert.Null(lib.GetAnimation("first"));
            Check(entries,"CannedAnimationContainer.GetAnimation_Const.InvalidName",EngineLogLevel.Error,null,"Animation requested for unknown animation 'first'.");
        }
        finally { Directory.Delete(root,true); }
    }

    private sealed class Sink:IAnimationSink
    {
        public void Face(FaceBitmap b){} public void Audio(byte[]? b){} public void Head(sbyte a,uint d){} public void Lift(byte h,uint d){} public void Body(BodyKeyframe b){} public void BodyStop(){} public void Lights(LightsKeyframe b){} public void Event(string s){} public void AnimationStarted(byte b){} public void AnimationEnded(){} public void Finished(string s,bool b){}
    }
    // Independent PNG fixture: unfiltered rows, zlib and standard CRC32, not the production decoder.
    private static byte[] Png(int width,int height,int depth,byte value,int colour=0,int interlace=0)
    {
        using var result=new MemoryStream(); result.Write(new byte[]{137,80,78,71,13,10,26,10});
        void Chunk(string type,byte[] data)
        {
            byte[] name=Encoding.ASCII.GetBytes(type), length=new byte[4]; BinaryPrimitives.WriteInt32BigEndian(length,data.Length); result.Write(length); result.Write(name); result.Write(data);
            uint crc=0xffffffff; foreach(byte b in name.Concat(data)){crc^=b;for(int j=0;j<8;j++)crc=(crc>>1)^((crc&1)!=0?0xedb88320u:0u);} BinaryPrimitives.WriteUInt32BigEndian(length,~crc); result.Write(length);
        }
        byte[] header=new byte[13]; BinaryPrimitives.WriteInt32BigEndian(header,width); BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4),height);header[8]=(byte)depth;header[9]=(byte)colour;header[12]=(byte)interlace;
        Chunk("IHDR",header); using var compressed=new MemoryStream();
        using(var z=new ZLibStream(compressed,CompressionLevel.SmallestSize,true)) for(int y=0;y<height;y++){z.WriteByte(0);for(int x=0;x<(width*depth+7)/8;x++)z.WriteByte(value);}
        Chunk("IDAT",compressed.ToArray());Chunk("IEND",Array.Empty<byte>());return result.ToArray();
    }
}
