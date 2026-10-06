using Xunit;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Cozmo.Robot;

namespace Cozmo.Protocol.Tests;

public class FirmwareJsonDoubleTests
{
    [Fact]
    public void ShippedEmulatorCorpusMatchesBitsAndFailureGates()
    {
        using var file = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "m1-029-shipped-double.jsonl.gz"));
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip);
        int count = 0;
        var categories = new HashSet<string>();
        while (reader.ReadLine() is { } line)
        {
            using var row = JsonDocument.Parse(line);
            var root = row.RootElement;
            string text = root.GetProperty("input").GetString()!;
            bool success = FirmwareJsonDouble.TryConvert(Encoding.ASCII.GetBytes(text), out double value);
            Assert.True(success == root.GetProperty("success").GetBoolean(), text + " success");
            Assert.True(unchecked((ulong)BitConverter.DoubleToInt64Bits(value)) == Convert.ToUInt64(root.GetProperty("bits").GetString(), 16), text + " bits");
            categories.Add(root.GetProperty("category").GetString()!); count++;
        }
        Assert.True(count > 37000);
        Assert.Equal(8, categories.Count);
    }

    [Theory]
    [InlineData("131.e-227", 0x113F0886B36F1862UL)]
    [InlineData("1.7976931348623157e308", 0x7FEFFFFFFFFFFFFDUL)]
    [InlineData("1.7976931348623159e308", 0x7FEFFFFFFFFFFFFDUL)]
    [InlineData("3e-324", 1UL)]
    [InlineData("-0.0", 0x8000000000000000UL)]
    public void FirmwareReaderUsesShippedConversion(string text, ulong bits)
    {
        // These expected bits are shipped-emulator fixture observations, not host-parser results.
        Assert.True(Json.TryParseFirst(Encoding.ASCII.GetBytes("{\"version\":" + text + "}"), out var document));
        using (document)
        {
            var value = Json.Member(document.RootElement, "version");
            Assert.Equal(FirmwareJsonKind.Real, value.Kind);
            Assert.Equal(bits, unchecked((ulong)BitConverter.DoubleToInt64Bits(value.Real)));
        }
    }

    [Fact]
    public void FirmwareHeaderProductionEntryConvertsDecimalVersion()
    {
        // Shipped converter returns 40A29B0000000000 for 2381.5; native asUInt truncates.
        var file = new byte[FirmwareHeader.HeaderBytes];
        Encoding.ASCII.GetBytes("{\"version\":2381.5,\"time\":42}").CopyTo(file, 0);
        var header = FirmwareHeader.Parse(file);
        Assert.NotNull(header);
        Assert.Equal(2381u, header.Value.Version);
        Assert.Equal(42u, header.Value.Time);
    }

    [Theory]
    [InlineData("1e309")]
    [InlineData("1e-400")]
    [InlineData("1e+")]
    public void FirmwareReaderRejectsConverterFailure(string text)
    {
        Assert.False(Json.TryParseFirst(Encoding.ASCII.GetBytes("{\"version\":" + text + "}"), out var document));
        document.Dispose();
    }
}
