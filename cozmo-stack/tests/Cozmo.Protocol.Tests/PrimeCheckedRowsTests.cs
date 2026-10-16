using System.Reflection;
using Cozmo.Robot;
using Xunit;
namespace Cozmo.Protocol.Tests;
public sealed class PrimeCheckedRowsTests
{
    // libc++ table at 0x9B480; frozen outputs for inputs 1..100, independent of either port's table.
    [Fact] public void BothConsumersMatchAllHundredSmallInputs()
    {
        uint[] expected={2,2,3,5,5,7,7,11,11,11,11,13,13,17,17,17,17,19,19,23,23,23,23,29,29,29,29,29,29,31,31,37,37,37,37,37,37,41,41,41,41,43,43,47,47,47,47,53,53,53,53,53,53,59,59,59,59,59,59,61,61,67,67,67,67,67,67,71,71,71,71,73,73,79,79,79,79,79,79,83,83,83,83,89,89,89,89,89,89,97,97,97,97,97,97,97,97,101,101,101};
        Assert.Equal(100,expected.Length);
        for(uint n=1;n<=100;n++){Assert.Equal(expected[n-1],ActiveObjectTable<int>.NextPrime(n));Assert.Equal(expected[n-1],BackupNativeMap.NextPrime(n));}
        Assert.Equal(0u,ActiveObjectTable<int>.NextPrime(0));Assert.Equal(0u,BackupNativeMap.NextPrime(0));
    }
    [Theory][InlineData(44521u,44531u)][InlineData(44522u,44531u)][InlineData(47053u,47057u)][InlineData(0xFFFFFFFBu,0xFFFFFFFBu)]
    public void NativeWheelBoundaryOutputs(uint input,uint expected)
    {Assert.Equal(expected,ActiveObjectTable<int>.NextPrime(input));Assert.Equal(expected,BackupNativeMap.NextPrime(input));}
    [Theory][InlineData(0xFFFFFFFCu)][InlineData(0xFFFFFFFDu)][InlineData(0xFFFFFFFEu)][InlineData(0xFFFFFFFFu)]
    public void NativeOverflowBoundary(uint input)
    {
        Assert.Equal("__next_prime overflow",Assert.Throws<OverflowException>(()=>ActiveObjectTable<int>.NextPrime(input)).Message);
        Assert.Equal("__next_prime overflow",Assert.Throws<OverflowException>(()=>BackupNativeMap.NextPrime(input)).Message);
    }
    private static uint Buckets(ActiveObjectTable<int> map)=>(uint)typeof(ActiveObjectTable<int>).GetProperty("BucketCount",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(map)!;
    private static void Rehash(ActiveObjectTable<int> map,uint n)=>typeof(ActiveObjectTable<int>).GetMethod("Rehash",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(map,new object[]{n});
    [Fact] public void ActualInsertionGrowthMatchesCheckedNativeSequence()
    {
        var active=new ActiveObjectTable<int>();var backup=new BackupNativeMap();var growth=new List<uint>();uint old=0;
        for(uint i=0;i<1600;i++)
        {
            active.Set(i,(int)i);backup.Put(i,Array.Empty<byte>());Assert.Equal(Buckets(active),backup.Buckets);
            if(Buckets(active)!=old){old=Buckets(active);growth.Add(old);}
        }
        Assert.Equal(new uint[]{2,5,11,23,47,97,197,397,797,1597,3203},growth);
        for(uint i=0;i<1600;i++){Assert.True(active.TryGet(i,out var v));Assert.Equal((int)i,v);}
    }
    [Theory][InlineData(197u,17u)][InlineData(128u,32u)]
    public void ShrinkFloorFollowsPreviousPrimeOrPowerOfTwoBucketKind(uint initial,uint expected)
    {
        var map=new ActiveObjectTable<int>();for(uint i=0;i<17;i++)map.Set(i,(int)i);
        Rehash(map,initial);Assert.Equal(initial,Buckets(map));Rehash(map,1);Assert.Equal(expected,Buckets(map));Assert.Equal(17,map.Count);
        for(uint i=0;i<17;i++){Assert.True(map.TryGet(i,out var v));Assert.Equal((int)i,v);}
    }
    [Fact] public void RehashPowersOfTwoBypassPrimeLookup()
    {var map=new ActiveObjectTable<int>();Rehash(map,4);Assert.Equal(4u,Buckets(map));Assert.Equal(5u,ActiveObjectTable<int>.NextPrime(4));Rehash(map,0);Assert.Equal(0u,Buckets(map));}
}
