using Cozmo.Robot.Behavior;
using Cozmo.Robot;
using Cozmo.Transport;
using Xunit;
namespace Cozmo.Protocol.Tests;

[Collection("M5 process statics")]
public sealed class NativeNeedsMetadataTests
{
    [Fact]
    public void NativeReadFailureAndFutureVersionKeepDefinedErrorSideEffects()
    {
        bool saved=EngineErrorState.ErrorFlagSet;
        try
        {
            var needs=new NeedsManager(()=>0);needs.EnableUncertifiedPersistenceForTests();
            var keys=new List<string>();needs.SourceLog=(level,channel,key,text)=>keys.Add(key);
            EngineErrorState.ErrorFlagSet=false;
            Assert.False(needs.FinishReadFromRobot(Array.Empty<byte>(),-1));
            Assert.False(EngineErrorState.ErrorFlagSet);
            Assert.Equal("NeedsManager.FinishReadFromRobot",keys[^1]);
            Assert.False(needs.FinishReadFromRobot(Array.Empty<byte>(),-4));
            Assert.True(EngineErrorState.ErrorFlagSet);
            Assert.Equal("NeedsManager.FinishReadFromRobot.ReadFailedFinish",keys[^1]);
            EngineErrorState.ErrorFlagSet=false;
            Assert.False(needs.FinishReadFromRobot(new byte[]{6},0));
            Assert.True(EngineErrorState.ErrorFlagSet);
            Assert.Equal("NeedsManager.FinishReadFromRobot.StateFileVersionIsFuture",keys[^1]);
        }
        finally{EngineErrorState.ErrorFlagSet=saved;}
    }
    [Fact]
    public void SourceStarThresholdsKeepActualLevelOrderAndLastFallback()
    {
        string? path=null;for(var d=new DirectoryInfo(AppContext.BaseDirectory);d is not null;d=d.Parent)
        {var candidate=Path.Combine(d.FullName,"re-analysis","obb","assets","cozmo_resources","config","engine","needs_level_config.json");if(File.Exists(candidate)){path=candidate;break;}}
        Assert.NotNull(path);var levels=Assert.IsType<NativeStarThresholds>(NativeStarThresholds.Parse(File.ReadAllText(path!)));
        Assert.Equal(1,levels.GetMax(0));Assert.Equal(1,levels.GetMax(27));Assert.Equal(1,levels.GetMax(28));Assert.Equal(1,levels.GetMax(-1));
        Assert.Equal(0,NativeStarThresholds.Parse("{\"unlockLevels\":[]}")!.GetMax(0));
    }    [Fact]
    public void FullNeedsStateBodyMatchesNativePackLiteral()
    {
        var value=new NativeNeedsStateReport(new uint[]{0,0x3F800000,0xBF800000},new[]{0,1,-1},new[]{true,false,true},0x12345678,2,3,0x11223344,new byte[]{65},0x01020304);
        Assert.Equal("03000000000000803F000080BF030000000001000000FFFFFFFF0301000178563412020000000300000044332211014104030201",Convert.ToHexString(NativeNeedsStateReportCodec.Pack(value)));
    }
    [Fact]
    public void QuantizationAddsSourceHalfBeforeSignedConversion()
    {Assert.Equal(1,NeedsManager.QuantizeLevel(.000006f));Assert.Equal(0,NeedsManager.QuantizeLevel(-.000006f));Assert.Equal(50000,NeedsManager.QuantizeLevel(.5f));}
    [Fact]
    public void LiteralRobotBlobDecodesSourceOffsets()
    {
        var bytes=Convert.FromHexString("05000000D007000000000000A861000050C30000F82401000000000000000000000000000000000000000000000000000000000002000000030000000000010000000000000000000000000000000000000000000000000000D204000000000000010000000E0000006400000000000000");
        Assert.Equal(116,bytes.Length);var value=NeedsStateOnRobot.Unpack(bytes);
        Assert.Equal(2000ul,value.TimeLastWritten);Assert.Equal(new[]{25000,50000,75000},value.CurNeedLevel.Take(3));
        Assert.Equal(2,value.CurNeedsUnlockLevel);Assert.Equal(3,value.NumStarsAwarded);Assert.Equal(1,value.PartIsDamaged[2]);
        Assert.Equal(1234ul,value.TimeLastStarAwarded);Assert.Equal(1,value.OnboardingStageCompleted);Assert.Equal(14,value.ForceNextSong);Assert.Equal(100ul,value.TimeCreated);
    }
}
