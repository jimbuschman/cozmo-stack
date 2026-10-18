using System.Collections;
using System.Reflection;
using System.Text;
using Cozmo.Robot;
using Cozmo.Robot.Behavior;
using Cozmo.Robot.Manipulation;
using Cozmo.Robot.Vision;
using Cozmo.Transport;
using Xunit;
namespace Cozmo.Protocol.Tests;
[Collection("M5 process statics")]
public sealed class LabWritebackCheckedRowsTests:IDisposable
{
    private readonly bool _error=EngineErrorState.ErrorFlagSet;
    public void Dispose()=>EngineErrorState.ErrorFlagSet=_error;
    private static byte[] B(string text)=>Encoding.UTF8.GetBytes(text);
    private static LabAssignmentDef A(string key="a",string user="ignored",string variation="b")=>new(B(key),B(user),B(variation));
    private static CozmoRobot Robot()=>CozmoRobot.CreateForTest(new M4ControlTests.FakePort(),()=>1_000_000_000);
    // Observation of actual queued requests; no replacement queue or transport scheduling model.
    private static List<(byte Op,uint Tag,byte[] Data,object? Callback,bool Broadcast)> Queue(NvStorageComponent nv)
    {
        var raw=(IEnumerable)typeof(NvStorageComponent).GetField("_queue",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(nv)!;
        return raw.Cast<object>().Select(x=>
        {
            object? P(string name)=>x.GetType().GetProperty(name)!.GetValue(x);
            return ((byte)P("Op")!,(uint)P("Tag")!,(byte[])P("Data")!,P("Callback"),(bool)P("Broadcast")!);
        }).ToList();
    }
    [Fact] public void ProjectionKeepsRawNulBytesOrderAndIgnoresUser()
    {
        Assert.Equal(new byte[]{1,1,97,1,98},CozmoExperiments.PackAssignments(new[]{A()}));
        Assert.Equal(new byte[]{1,3,97,0,98,2,0,99},CozmoExperiments.PackAssignments(new[]{new LabAssignmentDef(new byte[]{97,0,98},B("omitted"),new byte[]{0,99})}));
        Assert.Equal(new byte[]{0},CozmoExperiments.PackAssignments(Array.Empty<LabAssignmentDef>()));
    }
    [Fact] public void WidthTruncationRetainsFullAllocationZeroTailAndAllEntries()
    {
        var data=CozmoExperiments.PackAssignments(new[]{new LabAssignmentDef(Enumerable.Repeat((byte)97,257).ToArray(),B("ignored"),B("v"))});
        Assert.Equal(261,data.Length);Assert.Equal(new byte[]{1,1,97,1,118},data[..5]);Assert.All(data[5..],v=>Assert.Equal((byte)0,v));
        var many=CozmoExperiments.PackAssignments(Enumerable.Repeat(A(),256).ToArray());Assert.Equal(1025,many.Length);Assert.Equal((byte)0,many[0]);Assert.Equal(new byte[]{1,97,1,98},many[^4..]);
    }
    [Fact] public void EqualPairsWithChangedUserDoNothing()
    {
        using var robot=Robot();var lab=robot.Engine.Experiments;var logs=new List<EngineLogEntry>();robot.Engine.LogEntry+=logs.Add;
        var cache=new[]{new LabAssignmentPair(B("a"),B("b"))};lab.SetAssignmentVectorsForTest(new[]{A(user:"different")},cache);lab.PossiblyWriteLabAssignmentsToRobot();
        Assert.Same(cache,lab.CachedAssignments);Assert.Empty(Queue(robot.Engine.NvStorage!));Assert.DoesNotContain(logs,l=>l.Name=="CozmoExperiments.PossiblyWriteLabAssignmentsToRobot");
    }
    [Fact] public void NoRobotStillLogsAndRefreshesCacheDeepCopyWithoutRetry()
    {
        using var robot=Robot();var lab=robot.Engine.Experiments;var logs=new List<EngineLogEntry>();robot.Engine.LogEntry+=logs.Add;
        var assignment=A();lab.SetAssignmentVectorsForTest(new[]{assignment},Array.Empty<LabAssignmentPair>());lab.PossiblyWriteLabAssignmentsToRobot();
        Assert.Empty(Queue(robot.Engine.NvStorage!));Assert.Equal(B("a"),Assert.Single(lab.CachedAssignments!).Key);Assert.NotSame(assignment.Key,lab.CachedAssignments![0].Key);
        var log=Assert.Single(logs.Where(l=>l.Name=="CozmoExperiments.PossiblyWriteLabAssignmentsToRobot"));Assert.Equal(EngineLogLevel.Info,log.Level);Assert.Equal("Unnamed",log.Channel);Assert.Equal("Writing updated lab assignments to robot",log.Text);
        robot.Engine.Robots.AddRobot(1,false);lab.PossiblyWriteLabAssignmentsToRobot();Assert.Empty(Queue(robot.Engine.NvStorage!));
    }
    [Fact] public void RealWriterQueuesEraseAndPaddedPayloadWithNoCallbackOrBroadcast()
    {
        using var robot=Robot();robot.Engine.Robots.AddRobot(1,false);var lab=robot.Engine.Experiments;
        lab.SetAssignmentVectorsForTest(new[]{A()},Array.Empty<LabAssignmentPair>());lab.PossiblyWriteLabAssignmentsToRobot();
        var q=Queue(robot.Engine.NvStorage!).Where(x=>x.Tag==0x196000).ToArray();Assert.Equal(2,q.Length);Assert.Equal(NvStorageComponent.OpErase,q[0].Op);Assert.Equal(NvStorageComponent.OpWrite,q[1].Op);
        Assert.Equal(new byte[]{1,1,97,1,98,0,0,0},q[1].Data);Assert.Null(q[1].Callback);Assert.False(q[1].Broadcast);Assert.Single(lab.CachedAssignments!);
        lab.PossiblyWriteLabAssignmentsToRobot();Assert.Equal(2,Queue(robot.Engine.NvStorage!).Count(x=>x.Tag==0x196000));
    }
    [Fact] public void ImmediateNvRejectionStillRefreshesCacheAndSetsErrorFlag()
    {
        using var robot=Robot();robot.Engine.Robots.AddRobot(1,false);var lab=robot.Engine.Experiments;var logs=new List<EngineLogEntry>();robot.Engine.LogEntry+=logs.Add;
        lab.SetAssignmentVectorsForTest(new[]{new LabAssignmentDef(new byte[5000],Array.Empty<byte>(),B("v"))},Array.Empty<LabAssignmentPair>());EngineErrorState.ErrorFlagSet=false;
        lab.PossiblyWriteLabAssignmentsToRobot();Assert.True(EngineErrorState.ErrorFlagSet);Assert.Single(lab.CachedAssignments!);Assert.Empty(Queue(robot.Engine.NvStorage!).Where(x=>x.Tag==0x196000));
        var error=Assert.Single(logs.Where(l=>l.Name=="CozmoExperiments.WriteLabAssignmentsToRobot.Failed"));Assert.Equal(EngineLogLevel.Error,error.Level);Assert.EndsWith("Write failed",error.Text);
        int before=logs.Count;lab.PossiblyWriteLabAssignmentsToRobot();Assert.Equal(before,logs.Count);
    }
    [Theory][InlineData("a","changed")][InlineData("changed","b")]
    public void KeyOrVariationChangeTriggersWrite(string key,string variation)
    {using var robot=Robot();var lab=robot.Engine.Experiments;lab.SetAssignmentVectorsForTest(new[]{A(key,variation:variation)},new[]{new LabAssignmentPair(B("a"),B("b"))});lab.PossiblyWriteLabAssignmentsToRobot();Assert.Equal(B(key),lab.CachedAssignments![0].Key);Assert.Equal(B(variation),lab.CachedAssignments[0].Variation);}
    [Fact] public void PairOrderIsSignificant()
    {using var robot=Robot();var lab=robot.Engine.Experiments;lab.SetAssignmentVectorsForTest(new[]{A("z"),A("a")},new[]{new LabAssignmentPair(B("a"),B("b")),new LabAssignmentPair(B("z"),B("b"))});lab.PossiblyWriteLabAssignmentsToRobot();Assert.Equal(B("z"),lab.CachedAssignments![0].Key);}

    // reach: unit (explicit uncertified persistence adapter; epoch/economy production join MISSING at M15-030/M15-014).
    [Fact] public void NeedsQueueFailureReachesOwnedWritebackBeforeResolution()
    {
        using var robot=Robot();var lab=robot.Engine.Experiments;lab.SetAssignmentVectorsForTest(new[]{A()},Array.Empty<LabAssignmentPair>());
        var needs=new NeedsManager(()=>0){PossiblyWriteLabAssignments=lab.PossiblyWriteLabAssignmentsToRobot}; needs.EnableUncertifiedPersistenceForTests(); // Legacy unit persistence model only.needs.InitAfterSerialNumberAcquired(5);Assert.Single(lab.CachedAssignments!);
    }
    [Theory][InlineData(1)][InlineData(2)][InlineData(3)]
    public void PaddingPastActualBufferIsZeroWithOneStackDiagnostic(int size)
    {
        using var robot=Robot();var nv=robot.Engine.NvStorage!;var diagnostics=new List<string>();var source=new List<EngineLogEntry>();robot.Engine.LogEntry+=source.Add;StackDiagnostics.Reported+=diagnostics.Add;
        try
        {
            byte[] data=Enumerable.Range(1,size).Select(i=>(byte)i).ToArray();Assert.Equal(1,nv.Write(0x196000,data,null));Assert.Equal(1,nv.Write(0x196000,data,null));
            var writes=Queue(nv).Where(x=>x.Op==NvStorageComponent.OpWrite).ToArray();Assert.Equal(2,writes.Length);Assert.All(writes,w=>Assert.Equal(data.Concat(new byte[4-size]),w.Data));
            Assert.Single(diagnostics.Where(s=>s.Contains("M3-043")&&s.Contains("zero")));Assert.DoesNotContain(source,l=>l.Text.Contains("OV59")||l.Text.Contains("pad past"));
        }
        finally{StackDiagnostics.Reported-=diagnostics.Add;}
    }

    // reach: unit (explicit uncertified persistence adapter; epoch/economy production join MISSING at M15-030/M15-014).
    [Fact] public void NeedsReadCallbackAlsoReachesWritebackBeforeResolution()
    {
        using var robot=Robot();var lab=robot.Engine.Experiments;lab.SetAssignmentVectorsForTest(new[]{A()},Array.Empty<LabAssignmentPair>());
        var needs=new NeedsManager(()=>0){PossiblyWriteLabAssignments=lab.PossiblyWriteLabAssignmentsToRobot}; needs.EnableUncertifiedPersistenceForTests(); // Legacy unit persistence model only.
        typeof(NeedsManager).GetMethod("OnRobotRead",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(needs,new object[]{new NvResult(-1,Array.Empty<byte>())});Assert.Single(lab.CachedAssignments!);
    }
    [Fact] public void EmptyActiveVectorWritesOneByteThenNvPadsRatherThanDroppingWrite()
    {
        using var robot=Robot();robot.Engine.Robots.AddRobot(1,false);var lab=robot.Engine.Experiments;lab.SetAssignmentVectorsForTest(Array.Empty<LabAssignmentDef>(),new[]{new LabAssignmentPair(B("old"),B("v"))});lab.PossiblyWriteLabAssignmentsToRobot();
        Assert.Equal(new byte[4],Assert.Single(Queue(robot.Engine.NvStorage!).Where(x=>x.Op==NvStorageComponent.OpWrite&&x.Tag==0x196000)).Data);Assert.Empty(lab.CachedAssignments!);
    }
    [Fact] public void UnbuiltAssignmentProducersStayUnknownAtRealOwner()
    {
        using var robot=Robot();var lab=robot.Engine.Experiments;var missing=new List<string>();SteppedBehavior.MissingReported+=missing.Add;
        try{lab.PossiblyWriteLabAssignmentsToRobot();Assert.Null(lab.CachedAssignments);Assert.Empty(Queue(robot.Engine.NvStorage!));Assert.Contains(missing,s=>s.Contains("M15-050/M15-052/M15-036"));}
        finally{SteppedBehavior.MissingReported-=missing.Add;}
    }
    [Fact] public void FirstRobotUsesStoredIdOrderRatherThanSortedOrConnectedRobotGuess()
    {using var robot=Robot();var first=robot.Engine.Robots.AddRobot(7,false);robot.Engine.Robots.AddRobot(1,false);Assert.Same(first,robot.Engine.Robots.GetFirstRobot());}
    [Fact] public void PyramidRoleGettersPreserveOutputOnInvalidAndCopyAllUintBits()
    {
        using var robot=Robot();using var vision=new VisionSystem(robot,CameraCalibration.Nominal()){Enabled=false};using var m=new ManipulationSystem(robot,vision);var behavior=new BuildPyramidBaseBehavior(m);uint value=91;
        Assert.False(behavior.TryGetBaseBlockId(ref value));Assert.Equal(91u,value);
        foreach(var name in new[]{"BaseBlockId","StaticBlockId","TopBlockId"})typeof(BuildPyramidBaseBehavior).GetProperty(name)!.SetValue(behavior,0xF0000000u);
        Assert.True(behavior.TryGetBaseBlockId(ref value));Assert.Equal(0xF0000000u,value);value=2;Assert.True(behavior.TryGetStaticBlockId(ref value));Assert.Equal(0xF0000000u,value);value=3;Assert.True(behavior.TryGetTopBlockId(ref value));Assert.Equal(0xF0000000u,value);
        foreach(var name in new[]{"BaseBlockId","StaticBlockId","TopBlockId"})typeof(BuildPyramidBaseBehavior).GetProperty(name)!.SetValue(behavior,uint.MaxValue);
        value=73;Assert.False(behavior.TryGetBaseBlockId(ref value));Assert.False(behavior.TryGetStaticBlockId(ref value));Assert.False(behavior.TryGetTopBlockId(ref value));Assert.Equal(73u,value);
    }
    [Fact] public void UnrepresentedFirstRobotNvOwnerDoesNotBorrowPrimaryRobotQueue()
    {
        using var robot=Robot();robot.Engine.Robots.AddRobot(7,false);robot.Engine.Robots.AddRobot(1,false);var lab=robot.Engine.Experiments;lab.SetAssignmentVectorsForTest(new[]{A()},Array.Empty<LabAssignmentPair>());var missing=new List<string>();SteppedBehavior.MissingReported+=missing.Add;
        try{lab.PossiblyWriteLabAssignmentsToRobot();Assert.Empty(Queue(robot.Engine.NvStorage!).Where(x=>x.Tag==0x196000));Assert.Single(lab.CachedAssignments!);Assert.Contains(missing,s=>s.Contains("M15-050 first robot+0x260")&&s.Contains("primary"));}
        finally{SteppedBehavior.MissingReported-=missing.Add;}
    }
    [Fact] public void RoundedCopyPreservesAvailableBytesPastCountWithoutPolicyDiagnostic()
    {
        using var robot=Robot();var nv=robot.Engine.NvStorage!;var diagnostics=new List<string>();StackDiagnostics.Reported+=diagnostics.Add;
        try{Assert.Equal(1,nv.Write(0x196000,new byte[]{1,2,3,4},1,null));Assert.Equal(new byte[]{1,2,3,4},Assert.Single(Queue(nv).Where(x=>x.Op==NvStorageComponent.OpWrite)).Data);Assert.DoesNotContain(diagnostics,s=>s.Contains("M3-043"));}
        finally{StackDiagnostics.Reported-=diagnostics.Add;}
    }
}
