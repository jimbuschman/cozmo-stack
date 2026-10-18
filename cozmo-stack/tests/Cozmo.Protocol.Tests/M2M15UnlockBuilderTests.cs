using System.Text;
using System.Text.RegularExpressions;
using Cozmo.Robot;
using Cozmo.Transport;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class M2M15UnlockBuilderTests
{
    private sealed class Fixture:IDisposable
    {
        internal readonly BackupTestResources Files=new();internal readonly M4ControlTests.Rig Rig;
        internal CozmoEngine Engine=>Rig.Engine;internal string Path=>System.IO.Path.Combine(Engine.Options.ResourcesPath!,"config/engine/unlock_config_nurture.json");
        internal readonly List<EngineLogEntry> Logs=new();internal readonly List<string> Stack=new();internal readonly List<BackupUnlockStatus> Replies=new();
        private readonly bool _error=EngineErrorState.ErrorFlagSet;
        internal Fixture()
        {
            BackupFiles.ClearDefaultsForTest();Cozmo.Robot.Behavior.SteppedBehavior.ResetMissingForTests();
            Rig=new(engineOptions:Files.Options());Engine.LogEntry+=Logs.Add;Engine.StackLogLine+=Stack.Add;
            Engine.BackupReport+=r=>{if(r is BackupUnlockStatus unlock)Replies.Add(unlock);};EngineErrorState.ErrorFlagSet=false;
        }
        internal BackupUnlockStatus Query(){Rig.Robot.RequestUnlockDataFromBackup();Rig.Tick();return Replies[^1];}
        public void Dispose(){Rig.Dispose();Files.Dispose();BackupFiles.ClearDefaultsForTest();NativeUnlockNames.StderrForTest=null;EngineErrorState.ErrorFlagSet=_error;}
    }

    // reach: production — ConnectToRobot -> RIC -> actual progression Init; valid defaults set may already be warm.
    [Theory] [InlineData(false)] [InlineData(true)]
    public void ConnectionInitIgnoresFalseAndKeepsItsFreshPartialRootForOverrides(bool warm)
    {
        using var f=new Fixture();
        if(warm)
        {
            File.WriteAllText(f.Path,"{\"defaultUnlocks\":[\"PickupCube\"]}");
            Assert.Equal(new uint[]{6},f.Query().UnlockIds);
        }
        File.WriteAllText(f.Path,"{\"defaultUnlocks\":[\"StackTwoCubes\"],\"freeplayOverrides\":[]");
        f.Rig.ToSynced();
        Assert.NotNull(f.Engine.Robot!.Progression);
        Assert.Equal(new uint[]{warm?6u:10u},BackupFiles.CurrentDefaults());
        Assert.False(f.Engine.BackupFiles.ProgressionOverridesNeedRecovery);
        Assert.True(f.Engine.NvStorage!.InFlightTag==0x182000 || f.Engine.NvStorage.QueuedTags.Contains(0x182000u));
        Assert.Contains(f.Logs,e=>e.Name=="ProgressionUnlockComponent.InitDefaults.UnlockConfigJsonNotFound");
    }

    // reach: unit (substitutes raw lookup/root inputs; production use is InitConfig 0x0064BB68, exercised separately below).
    [Fact]
    public void EveryNativeNamePairMatchesTheIndependentCheckedReportTable()
    {
        var path=System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory,"../../../../../../re-analysis/research/20261013-small-reads-2.md"));
        var rows=Regex.Matches(File.ReadAllText(path),@"(?m)^\| `([^`]+)` \| (\d+) \| 0x[0-9a-f]+ \| 0x[0-9a-f]+ \|");
        Assert.Equal(81,rows.Count);
        foreach(Match row in rows)Assert.Equal(uint.Parse(row.Groups[2].Value,System.Globalization.CultureInfo.InvariantCulture),NativeUnlockNames.FromBytes(Encoding.UTF8.GetBytes(row.Groups[1].Value)));
    }

    // reach: unit (substitutes raw lookup inputs; production use is InitConfig 0x0064BB68, exercised separately below).
    [Theory] [InlineData("unknown")] [InlineData("3")] [InlineData("All ")] [InlineData("All, Defaults")]
    public void UnknownNameWritesTheExactStderrLineAndReturnsInvalid(string name)
    {
        var lines=new List<byte[]>();NativeUnlockNames.StderrForTest=lines.Add;
        try{Assert.Equal(0u,NativeUnlockNames.FromBytes(Encoding.UTF8.GetBytes(name)));Assert.Equal(Encoding.UTF8.GetBytes($"error: string '{name}' is not a valid UnlockId value\n"),Assert.Single(lines));}
        finally{NativeUnlockNames.StderrForTest=null;}
    }

    // reach: unit (substitutes raw lookup/root inputs; production use is InitConfig 0x0064BB68, exercised separately below).
    [Fact]
    public void MissDiagnosticPreservesRawNameBytesIncludingNulAndInvalidUtf8()
    {
        var lines=new List<byte[]>();NativeUnlockNames.StderrForTest=lines.Add;byte[] name={0x61,0,0xFF};
        try{Assert.Equal(0u,NativeUnlockNames.FromBytes(name));Assert.Equal("error: string '"u8.ToArray().Concat(name).Concat("' is not a valid UnlockId value\n"u8.ToArray()),Assert.Single(lines));}
        finally{NativeUnlockNames.StderrForTest=null;}
    }

    // reach: production — public backup request -> engine tick -> defaults fallback -> InitConfig.
    [Fact]
    public void MissingConfigContinuesToBothInitErrorsAndReturnsEmptyDefaults()
    {
        using var f=new Fixture();File.Delete(f.Path);Assert.Empty(f.Query().UnlockIds);
        Assert.Equal(new[]{"DataPlatform.readAsJson","ProgressionUnlockComponent.InitDefaults.UnlockConfigJsonNotFound","ProgressionUnlockComponent.InitDefaults.MissingDefaults"},f.Logs.Where(e=>e.Level==EngineLogLevel.Error).Select(e=>e.Name));
        Assert.True(EngineErrorState.ErrorFlagSet);Assert.DoesNotContain(f.Stack,s=>s.Contains("conditional Debug text is not built"));
    }

    // reach: production — public backup request -> engine tick -> defaults fallback -> InitConfig.
    [Fact]
    public void FailedReadKeepsACompletedDefaultsArrayAndTheCallerIgnoresFalse()
    {
        using var f=new Fixture();File.WriteAllText(f.Path,"{\"defaultUnlocks\":[\"PickupCube\",\"StackTwoCubes\"]");
        Assert.Equal(new uint[]{6,10},f.Query().UnlockIds);
        Assert.Contains(f.Logs,e=>e.Name=="DataPlatform.readAsJson");Assert.Contains(f.Logs,e=>e.Name=="ProgressionUnlockComponent.InitDefaults.UnlockConfigJsonNotFound");
        Assert.DoesNotContain(f.Logs,e=>e.Name=="ProgressionUnlockComponent.InitDefaults.MissingDefaults");
        Assert.Equal(2,f.Logs.Count(e=>e.Name=="ProgressionUnlockComponent.DefaultValue"));
    }

    // reach: unit (substitutes raw lookup/root inputs; production use is InitConfig 0x0064BB68, exercised separately below).
    [Fact]
    public void MissingMemberIsInsertedOnlyOnAnObjectRoot()
    {
        using var f=new Fixture();File.WriteAllText(f.Path,"{\"other\":1}");var root=new FirmwareJsonValue();
        Assert.False(BackupFiles.InitUnlockConfig(f.Engine,f.Engine.Options,root));
        string key=Convert.ToHexString("defaultUnlocks"u8);Assert.True(root.Members.ContainsKey(key));Assert.Equal(FirmwareJsonKind.Null,root.Members[key].Kind);
        File.Delete(f.Path);root=new();Assert.False(BackupFiles.InitUnlockConfig(f.Engine,f.Engine.Options,root));Assert.Equal(FirmwareJsonKind.Null,root.Kind);
    }

    // reach: unit (substitutes raw lookup/root inputs; production use is InitConfig 0x0064BB68, exercised separately below).
    [Fact]
    public void EmptyOrInvalidFirstTokenKeepsTheSuppliedRootPayload()
    {
        using var f=new Fixture();var files=new BackupFiles(f.Engine);var root=FirmwareJsonValue.FromReal(1.5);
        File.Delete(f.Path);Assert.False(files.ReadJson(f.Path,root,"M15-044"));Assert.Equal(FirmwareJsonKind.Real,root.Kind);Assert.Equal(1.5,root.Real);
        File.WriteAllText(f.Path,"?");Assert.False(files.ReadJson(f.Path,root,"M15-044"));Assert.Equal(FirmwareJsonKind.Real,root.Kind);Assert.Equal(1.5,root.Real);
    }

    // reach: production — public backup request -> engine tick -> defaults fallback -> InitConfig.
    [Fact]
    public void NonStringElementsLogErrorsAndUnknownStringStillInsertsInvalid()
    {
        using var f=new Fixture();var stderr=new List<byte[]>();NativeUnlockNames.StderrForTest=stderr.Add;
        File.WriteAllText(f.Path,"{\"defaultUnlocks\":[123,null,{},[],false,\"unknown\",\"PickupCube\"]}");
        Assert.Equal(new uint[]{0,6},f.Query().UnlockIds);Assert.Equal(5,f.Logs.Count(e=>e.Name=="ProgressionUnlockComponent.InitDefaults.InvalidDefaultData"));
        Assert.Equal(new[]{"Invalid defaults to true","PickupCube defaults to true"},f.Logs.Where(e=>e.Name=="ProgressionUnlockComponent.DefaultValue").Select(e=>e.Text));Assert.Single(stderr);
    }

    // reach: production — public backup request -> engine tick -> defaults fallback -> InitConfig.
    [Theory] [InlineData("0")] [InlineData("true")] [InlineData("[]")] [InlineData("\"s\"")]
    public void NonObjectNonNullRootSkipsTheTerminatingConfigUnit(string json)
    {
        using var f=new Fixture();File.WriteAllText(f.Path,json);Assert.Empty(f.Query().UnlockIds);
        Assert.Contains(f.Stack,s=>s.Contains("M1-034 / M15-044")&&s.Contains("0x008EABA6"));
        Assert.DoesNotContain(f.Logs,e=>e.Name=="ProgressionUnlockComponent.InitDefaults.MissingDefaults");
    }

    // reach: unit (substitutes raw lookup/root inputs; production use is InitConfig 0x0064BB68, exercised separately below).
    [Fact]
    public void NonemptySetGuardRunsAfterTheReadAndBeforeTheRootMemberAccess()
    {
        using var f=new Fixture();File.WriteAllText(f.Path,"{\"defaultUnlocks\":[\"PickupCube\"]}");Assert.Equal(new uint[]{6},f.Query().UnlockIds);
        File.WriteAllText(f.Path,"[]");f.Stack.Clear();Assert.True(BackupFiles.InitUnlockConfig(f.Engine,f.Engine.Options,new()));Assert.Empty(f.Stack);
        File.Delete(f.Path);f.Logs.Clear();Assert.False(BackupFiles.InitUnlockConfig(f.Engine,f.Engine.Options,new()));
        Assert.Equal(2,f.Logs.Count(e=>e.Level==EngineLogLevel.Error));Assert.DoesNotContain(f.Logs,e=>e.Name=="ProgressionUnlockComponent.InitDefaults.MissingDefaults");
    }

    // reach: unit (substitutes raw lookup/root inputs; production use is InitConfig 0x0064BB68, exercised separately below).
    [Fact]
    public void NullContextOrPlatformReturnsBeforeReadingOrChangingRoot()
    {
        using var f=new Fixture();var root=FirmwareJsonValue.FromReal(2);
        Assert.False(BackupFiles.InitUnlockConfig(null,f.Engine.Options,root));Assert.False(BackupFiles.InitUnlockConfig(f.Engine,null,root));
        Assert.Empty(f.Logs);Assert.Equal(FirmwareJsonKind.Real,root.Kind);Assert.Equal(2,root.Real);
    }

    // reach: production — public backup request -> engine tick -> defaults fallback -> InitConfig.
    [Fact]
    public void ReaderDepthOver1000SkipsTheFileAndKeepsDefaultsEmpty()
    {
        using var f=new Fixture();File.WriteAllText(f.Path,new string('[',1001)+"0"+new string(']',1001));Assert.Empty(f.Query().UnlockIds);
        Assert.Contains(f.Stack,s=>s.Contains("M1-034 / M15-044")&&s.Contains("0x008E0D92"));
    }
}
