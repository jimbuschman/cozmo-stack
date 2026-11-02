using System.Text;
using Cozmo.Robot;
using Cozmo.Protocol;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class LabOwnedIntegrationTests
{
    private static byte[] B(string s)=>Encoding.UTF8.GetBytes(s);
    [Theory]
    [InlineData("02","|,|")]
    [InlineData("0201610162","a|b,a|b")]
    [InlineData("020341","A|,A|")]
    [InlineData("020161016200","a|b,|b")]
    public void NativePartialDecoderRetainsTemporaryAcrossEntries(string hex,string expected)
    {
        var actual=CozmoExperiments.DecodeAssignments(Convert.FromHexString(hex),Array.Empty<LabAssignmentPair>());
        Assert.Equal(expected,string.Join(",",actual.Select(x=>Encoding.ASCII.GetString(x.Key)+"|"+Encoding.ASCII.GetString(x.Variation))));
    }
    [Theory]
    [InlineData(172800,false)][InlineData(10,true)]
    public void PreInitNeedsStateDrivesAndRetainsSourceQualifiedCache(long seconds,bool expectedDayZero)
    {
        var pre=new LabPreInitNeedsRecipient();int clockReads=0;
        var tags=new LabAudienceTags{Log=(_,_,_,_,_)=>{}};
        tags.RegisterEngineTags(pre.ReadTimeCreated,()=>{clockReads++;return seconds;},()=>"EN",()=>"US");
        var first=tags.GetQualified().Select(Encoding.ASCII.GetString).ToArray();
        Assert.Equal(expectedDayZero,first.Contains("app_user_d0"));
        Assert.Contains("locale_language_en",first);Assert.Contains("locale_country_us",first);
        Assert.Equal(new[]{0,1,2},pre.Levels.Keys);Assert.All(pre.Levels.Values,x=>Assert.Equal(0f,x));
        Assert.All(pre.Brackets.Values,x=>Assert.Equal(-1,x));
        pre.Attach(new Cozmo.Robot.Behavior.NeedsManager(()=>0));
        Assert.Equal(first,tags.GetQualified().Select(Encoding.ASCII.GetString));Assert.Equal(1,clockReads);
    }
    [Fact]
    public void ConstructedLabOwnerFindsRawFirstEntryAndPreservesIdentityThroughAssignment()
    {
        using var robot=CozmoRobot.CreateOffline();var exp=new CozmoExperiments(robot.Engine);var lab=exp.Lab;
        lab.Assign(new(B("a"),B("u"),B("one")));var first=lab.FindAssignment!(0x40,B("a"),B("u"));Assert.NotNull(first);
        lab.Assign(new(B("a"),B("u"),B("two")));Assert.Same(first,lab.FindAssignment!(0x40,B("a"),B("u")));
        Assert.Equal(B("two"),first!.Variation);Assert.Equal(B("two"),Assert.Single(exp.ActiveAssignmentsForTest!).Variation);
        lab.Assign(new(B("a\0b"),B("u"),B("v")));Assert.NotNull(lab.FindAssignment(0x40,B("a\0b"),B("u")));
        Assert.Null(lab.FindAssignment(0x40,B("a"),B("u\0")));
    }
    [Fact]
    public void DefinitionLookupsUseFirstRawKeyAndVariationIdentity()
    {
        using var robot=CozmoRobot.CreateOffline();var exp=new CozmoExperiments(robot.Engine);
        Assert.True(exp.Load(B("{\"experiments\":[{\"key\":\"a\",\"variations\":[{\"key\":\"v\"},{\"key\":\"v\"}]},{\"key\":\"a\\u0000b\"},{\"key\":\"a\"}]}")));
        var definitions=exp.Definition.Experiments!;
        Assert.Same(definitions[0],exp.Lab.FindExperiment!(B("a")));
        Assert.Same(definitions[1],exp.Lab.FindExperiment!(new byte[]{97,0,98}));
        Assert.Null(exp.Lab.FindExperiment!(B("A")));
        Assert.Same(definitions[0].Variations![0],exp.Lab.FindVariation!(definitions[0],B("v")));
    }
    [Fact]
    public void SourceRestoreUsesSerialUserAndSignalsEveryAcceptedPair()
    {
        using var robot=CozmoRobot.CreateOffline();var exp=new CozmoExperiments(robot.Engine);
        Assert.True(exp.Load(B("{\"experiments\":[{\"key\":\"k\",\"activation_mode\":\"manual\",\"start_time_utc_iso8601\":\"\",\"stop_time_utc_iso8601\":\"\",\"pause_time_utc_iso8601\":\"\",\"resume_time_utc_iso8601\":\"\",\"pop_frac_pct\":100,\"audience_tags\":[],\"variations\":[{\"key\":\"v\",\"pop_frac_pct\":100}]}]}")));
        exp.Lab.ClockMicroseconds=()=>1_000_000;int count=0;
        exp.Lab.AssignmentSignal64!.Connect(_=>count++);
        exp.RestoreLoadedAssignments(37,new NvResult(0,Convert.FromHexString("02016B0176016B0176")));
        Assert.Equal(2,count);var assignment=Assert.Single(exp.ActiveAssignmentsForTest!);
        Assert.Equal(B("k"),assignment.Key);Assert.Equal(B("37"),assignment.User);Assert.Equal(B("v"),assignment.Variation);
        Assert.Equal(2,exp.CachedAssignments!.Count);
    }
    [Fact]
    public void RealNvReadRestoresSourcePairThroughOriginalContextSubscriber()
    {
        using var resources=new BackupTestResources();
        var options=resources.Options(new CozmoEngineOptions{DasDeviceId="device",LabLocaleLanguage="en",LabLocaleCountry="us"});
        using var robot=CozmoRobot.CreateForTest(new M4ControlTests.FakePort(),()=>0,options);
        var engine=robot.Engine;var owner=engine.Robots.AddRobot(CozmoEngine.RobotId,withRic:false)!;var exp=engine.Experiments;
        Assert.True(exp.Load(B("{\"experiments\":[{\"key\":\"k\",\"activation_mode\":\"manual\",\"start_time_utc_iso8601\":\"\",\"stop_time_utc_iso8601\":\"\",\"pause_time_utc_iso8601\":\"\",\"resume_time_utc_iso8601\":\"\",\"pop_frac_pct\":100,\"audience_tags\":[],\"variations\":[{\"key\":\"v\",\"pop_frac_pct\":100}]}]}")));
        exp.Lab.ClockMicroseconds=()=>1_000_000;var commands=new List<NVCommand>();
        owner.SendFault=message=>{if(message is NVCommand command)commands.Add(command);return true;};
        exp.ReadLabAssignmentsFromRobot(37);Assert.Single(engine.NvStorage!.QueuedTags.Where(t=>t==0x196000));engine.NvStorage.Update();
        Assert.Contains(commands,c=>c.Tag==0x196000&&c.Op==NvStorageComponent.OpRead);
        byte[] blob=new byte[64];BitConverter.GetBytes(0x435A4D4Fu).CopyTo(blob,0);BitConverter.GetBytes(5u).CopyTo(blob,8);
        Convert.FromHexString("01016B0176").CopyTo(blob,16);
        owner.DeliverMessage(new NVOpResult{Tag=0x196000,Op=NvStorageComponent.OpRead,Result=0,Data=blob});
        Assert.Contains(exp.ActiveAssignmentsForTest!,a=>a.Key.SequenceEqual(B("k"))&&a.User.SequenceEqual(B("37"))&&a.Variation.SequenceEqual(B("v")));
        Assert.Equal(B("k"),Assert.Single(exp.CachedAssignments!).Key);
    }    [Fact]
    public void ActualContextAbCallbacksUseOwnedLabAndLiveFirstRobotBodyId()
    {
        using var resources=new BackupTestResources();
        var options=resources.Options(new CozmoEngineOptions{DasDeviceId="a\0b",LabLocaleLanguage="EN",LabLocaleCountry="US"});
        using var robot=CozmoRobot.CreateForTest(new M4ControlTests.FakePort(),()=>0,options);
        var exp=robot.Engine.Experiments;
        var register=Assert.IsType<Action<Action<LabActivationRuntime>?>>(exp.Ab.First105D948);
        var user=Assert.IsType<Func<byte[]?>>(exp.Ab.Second105D960);
        Assert.Equal(B("a"),user());
        register(lab=>lab.Assign(new(B("k"),B("u"),B("v"))));
        Assert.Contains(exp.ActiveAssignmentsForTest!,a=>a.Key.SequenceEqual(B("k"))&&a.User.SequenceEqual(B("u")));
        var owner=robot.Engine.Robots.AddRobot(CozmoEngine.RobotId,withRic:false)!;
        owner.DeliverMessage(new ManufacturingID{SerialNumber=uint.MaxValue});
        Assert.Equal(B("4294967295"),user());
        robot.Engine.Robots.RemoveRobot(CozmoEngine.RobotId,wasConnecting:false);Assert.Equal(B("a"),user());
    }
    [Fact]
    public void MissingBodyActivationWithdrawsHistoryWithoutClearingRetainedEntries()
    {
        using var robot=CozmoRobot.CreateOffline();var exp=new CozmoExperiments(robot.Engine);
        exp.Lab.Assign(new(B("k"),B("u"),B("v")));Assert.Single(exp.ActiveAssignmentsForTest!);
        exp.AutoActivateForBodyId(7);Assert.Null(exp.ActiveAssignmentsForTest);
        Assert.Single(exp.Lab.Assignments40!);
        exp.Lab.Assign(new(B("other"),B("u"),B("v")));Assert.Null(exp.ActiveAssignmentsForTest);
        Assert.Equal(2,exp.Lab.Assignments40!.Count);
    }
    [Fact]
    public void WritebackReloadsLiveVectorAfterSourceLog()
    {
        using var robot=CozmoRobot.CreateOffline();var exp=new CozmoExperiments(robot.Engine);
        exp.SetAssignmentVectorsForTest(new[]{new LabAssignmentDef(B("k"),B("u"),B("old"))},Array.Empty<LabAssignmentPair>());
        robot.Engine.LogEntry+=e=>{if(e.Name=="CozmoExperiments.PossiblyWriteLabAssignmentsToRobot")exp.SetAssignmentVectorsForTest(new[]{new LabAssignmentDef(B("k"),B("u"),B("new"))},Array.Empty<LabAssignmentPair>());};
        exp.PossiblyWriteLabAssignmentsToRobot();Assert.Equal(B("new"),Assert.Single(exp.CachedAssignments!).Variation);
    }
    [Fact]
    public void SignalDisconnectDuringDispatchStopsLaterNode()
    {
        var signal=new LabAssignmentSignal{Release4EB0CA=n=>n.RefCount20--};var calls=new List<string>();
        LabAssignmentSignalNode? second=null;signal.Connect(_=>{calls.Add("first");signal.Disconnect(second!);});second=signal.Connect(_=>calls.Add("second"));
        signal.Dispatch(Array.Empty<LabAssignmentDef>());Assert.Equal(new[]{"first"},calls);
    }
}
