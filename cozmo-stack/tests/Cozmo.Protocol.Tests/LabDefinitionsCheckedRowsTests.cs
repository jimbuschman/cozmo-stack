using System.Text;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Transport;
using Xunit;
namespace Cozmo.Protocol.Tests;
[Collection("M5 process statics")]
public sealed class LabDefinitionsCheckedRowsTests
{
    private static string Shipped()
    {
        for(var d=new DirectoryInfo(AppContext.BaseDirectory);d is not null;d=d.Parent)
        {string p=Path.Combine(d.FullName,"re-analysis/obb/assets/cozmo_resources/config/experiments.json");if(File.Exists(p))return p;}
        throw new InvalidOperationException("shipped experiments.json required");
    }
    private sealed class Fixture:IDisposable
    {
        internal readonly BackupTestResources Resources=new();
        internal readonly CozmoEngine Engine;
        internal readonly List<EngineLogEntry> Logs=new();
        internal readonly List<string> Missing=new();
        private readonly bool _error=EngineErrorState.ErrorFlagSet;
        internal CozmoExperiments Lab=>Engine.Experiments;
        internal Fixture(string? id="stable-device-id")
        {
            SteppedBehavior.ResetMissingForTests(); // isolate once-per-process diagnostics from earlier real startups
            SteppedBehavior.MissingReported+=Missing.Add;
            Engine=new(new M4ControlTests.FakePort(),Resources.Options(new CozmoEngineOptions{DasDeviceId=id,DasDeviceIdPath=Path.Combine(Resources.Root,"device-id")}),()=>1_000_000_000);
            Engine.LogEntry+=Logs.Add;
        }
        internal bool Load(string text)=>Lab.Load(Encoding.UTF8.GetBytes(text));
        public void Dispose(){SteppedBehavior.MissingReported-=Missing.Add;Engine.Dispose();Resources.Dispose();EngineErrorState.ErrorFlagSet=_error;}
    }
    private static string Text(byte[]? value)=>Encoding.UTF8.GetString(value!);
    // reach: production (actual engine constructor and host settings; definition/enable prefix only).
    [Fact] public void ActualEngineStartupLoadsThreeShippedDefinitionsInOrderBeforeEnable()
    {
        using var f=new Fixture();var d=f.Lab.Definition;Assert.True(f.Lab.Enabled);Assert.Equal("stable-device-id",f.Lab.DeviceIdInput);
        Assert.Equal("cozmo",Text(d.ProjectId));Assert.Equal(2u,d.Version);Assert.Equal(1u,d.Revision);
        var e=d.Experiments!;Assert.Equal(new[]{"report_test_auto","unconnected_decay_rates","preholiday_tuning"},e.Select(x=>Text(x.Key)));
        Assert.Equal(new byte?[]{1,0,0},e.Select(x=>x.ActivationMode));Assert.Equal(new byte?[]{50,100,100},e.Select(x=>x.Population));
        Assert.Equal(new[]{"control","variationA","variationB"},e[1].Variations!.Select(x=>Text(x.Key)));Assert.Equal(new byte?[]{34,33,33},e[1].Variations!.Select(x=>x.Population));
        Assert.Equal("2017-09-21T00:00:00Z",Text(e[1].Start));Assert.Equal("2018-01-01T00:00:00Z",Text(e[2].Stop));
        Assert.Equal(new[]{"app_user_d0"},e[1].AudienceTags!.Select(Text));Assert.Empty(e[0].AudienceTags!);
        Assert.Null(f.Lab.NeedsState8Input); // unit reports this missing pre-init input; reporting is deduplicated across fixtures
        f.Lab.Init();var log=Assert.Single(f.Logs);Assert.Equal(EngineLogLevel.Info,log.Level);Assert.Equal("Unnamed",log.Channel);Assert.Equal("AnkiLab.Enable",log.Name);Assert.Equal("Enabling the lab",log.Text);
    }
    private sealed class FlushStream:MemoryStream {internal int Flushes;public override void Flush(){Flushes++;base.Flush();}}
    // reach: unit (direct definition-loader inputs; activation join MISSING at M15-0626A5328).
    [Fact] public void UnknownModeWritesRawCerrThenManualWithoutADasError()
    {
        using var f=new Fixture();using var stream=new FlushStream();f.Lab.CerrOverride=stream;
        Assert.True(f.Load("{\"experiments\":[{\"activation_mode\":\"Automatic\"}]}"));
        Assert.Equal((byte)0,f.Lab.Definition.Experiments![0].ActivationMode);
        Assert.Equal("error: string 'Automatic' is not a valid ActivationMode value\n",Encoding.UTF8.GetString(stream.ToArray()));Assert.Equal(1,stream.Flushes);Assert.Empty(f.Logs);
    }
    // reach: unit (direct definition-loader inputs; activation join MISSING at M15-0626A5328).
    [Fact] public void InvalidSyntaxLogsExactErrorAndDoesNotApplyPartialRoot()
    {
        using var f=new Fixture();var old=f.Lab.Definition;EngineErrorState.ErrorFlagSet=false;
        Assert.False(f.Load("{\"meta\":{\"project_id\":\"changed\"},\"experiments\":["));Assert.Same(old,f.Lab.Definition);
        var e=Assert.Single(f.Logs);Assert.Equal(EngineLogLevel.Error,e.Level);Assert.Null(e.Channel);Assert.Equal("AnkiLab.Load",e.Name);Assert.EndsWith("Invalid json format in AnkiLab file",e.Text);Assert.Empty(e.Data);Assert.True(EngineErrorState.ErrorFlagSet);
    }
    // reach: unit (direct definition-loader inputs; activation join MISSING at M15-0626A5328).
    [Fact] public void PresentArraysResizeInOrderAndMissingMembersKeepPriorValues()
    {
        using var f=new Fixture();Assert.True(f.Load("{\"meta\":{\"revision\":9},\"experiments\":[{\"key\":\"new\",\"variations\":[{\"pop_frac_pct\":511}]}]}"));
        var d=f.Lab.Definition;Assert.Equal("cozmo",Text(d.ProjectId));Assert.Equal(2u,d.Version);Assert.Equal(9u,d.Revision);var e=Assert.Single(d.Experiments!);Assert.Equal("new",Text(e.Key));Assert.Equal((byte)1,e.ActivationMode);
        var v=Assert.Single(e.Variations!);Assert.Equal("control",Text(v.Key));Assert.Equal((byte)255,v.Population);
        Assert.True(f.Load("{}"));Assert.Same(d,f.Lab.Definition);Assert.True(f.Load("{\"experiments\":[]}"));Assert.Empty(f.Lab.Definition.Experiments!);
    }
    // reach: unit (direct definition-loader inputs; activation join MISSING at M15-0626A5328).
    [Fact] public void PopulationNarrowsAsUIntToByteWithoutClamping()
    {using var f=new Fixture();Assert.True(f.Load("{\"experiments\":[{\"pop_frac_pct\":300}]}"));Assert.Equal((byte)44,f.Lab.Definition.Experiments![0].Population);}
    // reach: unit (direct definition-loader inputs; activation join MISSING at M15-0626A5328).
    [Theory]
    [InlineData("true")]
    [InlineData("{\"meta\":{\"revision\":-1}}")]
    [InlineData("{\"experiments\":{\"x\":1}}")]
    [InlineData("{\"experiments\":[{\"key\":\"changed\",\"variations\":[{\"pop_frac_pct\":{}}]}]}")]
    public void TerminatingJsonConversionsSkipWholeFileAndKeepPriorDefinition(string text)
    {
        using var f=new Fixture();var old=f.Lab.Definition;var diagnostics=new List<string>();StackDiagnostics.Reported+=diagnostics.Add;
        try{Assert.False(f.Load(text));Assert.Same(old,f.Lab.Definition);Assert.Contains(diagnostics,s=>s.Contains("M1-034/M15-049")&&s.Contains("skipping"));Assert.Empty(f.Logs);}
        finally{StackDiagnostics.Reported-=diagnostics.Add;}
    }
    // reach: unit (direct definition-loader inputs; activation join MISSING at M15-0626A5328).
    [Fact] public void NoExplicitIdLeavesInputMissingWithoutInventingAnId()
    {using var f=new Fixture(id:null);Assert.Null(f.Lab.DeviceIdInput);Assert.Contains(f.Missing,s=>s.Contains("M15-053")&&s.Contains("no explicit stable"));}
    // reach: unit (direct definition-loader inputs; activation join MISSING at M15-0626A5328).
    [Fact] public void ExplicitEmptyIdIsPreservedLikeTheAppInput()
    {using var f=new Fixture(id:"");Assert.Equal("",f.Lab.DeviceIdInput);Assert.DoesNotContain(f.Missing,s=>s.Contains("M15-053"));}
    // reach: unit (direct definition-loader inputs; activation join MISSING at M15-0626A5328).
    [Fact] public void ExplicitDeviceInputAlsoFeedsDasWithoutGeneratingASecondIdentity()
    {
        using var f=new Fixture();f.Engine.StartOffline(autoTick:false);
        Assert.Equal("stable-device-id",f.Lab.DeviceIdInput);
        Assert.Equal("699c2738e3b4bd524b44e671d5b95b1f",f.Engine.Das.GetGlobalsForThisRun()["$player_id"]);
        Assert.False(File.Exists(f.Engine.Options.DasDeviceIdPath));
    }
    // reach: unit (direct definition-loader inputs; activation join MISSING at M15-0626A5328).
    [Fact] public void NonemptyForcedVariationStopsAtUnreadDescendantInsteadOfGuessing()
    {using var f=new Fixture();var old=f.Lab.Definition;Assert.False(f.Load("{\"experiments\":[{\"forced_variations\":[{}]}]}"));Assert.Same(old,f.Lab.Definition);Assert.Contains(f.Missing,s=>s.Contains("VariantCondition"));Assert.Empty(f.Logs);}
}
