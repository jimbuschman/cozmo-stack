using System.Text.Json;
using Cozmo.Robot;
using Xunit;
namespace Cozmo.Protocol.Tests;
public sealed class M11FixedFloatCheckedRowsTests
{
    [Fact] public void BundledFPathMatchesIndependentNativeFloatCorpus()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","m11_083_fixed_float_oracle.json")));
        Assert.Equal("8ac5090bbd0be7401af5fff6044ded6f1e6ced692fcd3581fa3cb937b519a16a",doc.RootElement.GetProperty("libcxx_sha256").GetString());
        var mismatches=new List<string>();int count=0;
        foreach(var row in doc.RootElement.GetProperty("rows").EnumerateArray())
        {
            uint bits=Convert.ToUInt32(row.GetProperty("bits").GetString(),16);float input=BitConverter.UInt32BitsToSingle(bits);
            var expected=row.GetProperty("expected").GetString();var actual=LibcxxPrintf.FormatFixedFloat(input);count++;
            if(expected!=actual)mismatches.Add($"{bits:X8}: expected {expected}; got {actual}");
        }
        Assert.Equal(168,count);Assert.Empty(mismatches);
    }
    [Theory]
    [InlineData(0x3C000000u,"0.007812")]
    [InlineData(0x3CC00000u,"0.023438")]
    [InlineData(0xBC000000u,"-0.007812")]
    [InlineData(0xBCC00000u,"-0.023438")]
    [InlineData(0x80000000u,"-0.000000")]
    [InlineData(0x80000001u,"-0.000000")]
    [InlineData(0x7F7FFFFFu,"340282346638528859811704183484516925440.000000")]
    [InlineData(0xFFC00001u,"nan")]
    public void CheckedF6OutputsKeepHalfEvenSignAndFixedNotation(uint bits,string expected)
        =>Assert.Equal(expected,LibcxxPrintf.FormatFixedFloat(BitConverter.UInt32BitsToSingle(bits)));
    [Fact] public void StreamGEntryRetainsItsSeparateConversion()
    {
        Assert.Equal("0.0078125",LibcxxPrintf.FormatFloat(1f/128));
        Assert.Equal("0.007812",LibcxxPrintf.FormatFixedFloat(1f/128));
    }
}
