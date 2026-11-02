using Cozmo.Protocol;
using Cozmo.Robot.Vision;
using Xunit;
namespace Cozmo.Protocol.Tests;
public sealed class NativeImageImuHistoryTests
{
    private static ImageImuData Row(uint id,byte line)=>new(){ImageId=id,Line2Number=line};
    [Fact]
    public void AssociationMutatesAllMatchingEntriesAndCapturesIndependentSnapshot()
    {
        var history=new NativeImageImuHistory();foreach(byte line in new byte[]{0,120,240,255})history.Add(Row(42,line));
        history.Associate(42,1000,240);var captured=history.Snapshot();
        Assert.Equal(new uint[]{935,967,1000,1004},captured.Select(x=>x.ComputedTimestamp));
        history.Associate(42,2000,240);history.Add(Row(43,0));
        Assert.Equal(new uint[]{935,967,1000,1004},captured.Select(x=>x.ComputedTimestamp));
    }
    [Fact]
    public void CapAndPriorImageAssociationFollowInsertionOrder()
    {
        var history=new NativeImageImuHistory();for(uint id=1;id<=22;id++)history.Add(Row(id,0));
        Assert.Equal(Enumerable.Range(3,20).Select(i=>(uint)i),history.Snapshot().Select(x=>x.ImageId));
        var other=new NativeImageImuHistory();other.Add(Row(41,0));other.Add(Row(41,120));other.Associate(41,900,240);other.Add(Row(41,240));other.Associate(42,1000,240);
        Assert.Equal(new uint[]{835,867,899},other.Snapshot().Select(x=>x.ComputedTimestamp));
    }
    [Fact]
    public void StrictAfterAndZeroCandidateCasesMatchSource()
    {
        var rows=new[]{new NativeImageImu(1,0,0,0,0,935),new NativeImageImu(1,0,0,0,0,967),new NativeImageImu(1,0,0,0,0,1000)};
        NativeImageImu before=default,after=default;
        Assert.True(NativeImageImuHistory.TryBracket(rows,967,ref before,ref after));Assert.Equal(967u,before.ComputedTimestamp);Assert.Equal(1000u,after.ComputedTimestamp);
        var savedBefore=before;var savedAfter=after;
        Assert.False(NativeImageImuHistory.TryBracket(rows,1000,ref before,ref after));Assert.Equal(savedBefore,before);Assert.Equal(savedAfter,after);
        var zero=new[]{rows[0] with {ComputedTimestamp=900},rows[1] with {ComputedTimestamp=0}};
        Assert.True(NativeImageImuHistory.TryBracket(zero,950,ref before,ref after));Assert.False(NativeImageImuHistory.TryBracket(zero,965,ref before,ref after));
    }
}
