using System.Text.Json.Nodes;
using Cozmo.Conformance;
using Cozmo.Protocol;
using Xunit;

namespace Cozmo.Protocol.Tests;

public class M5HwAbortTests
{
    static string Verdict(JsonObject r) => r["verdict"]!.GetValue<string>();
    static M5Hw.WireMessage Out(double at, byte[] payload, int line, int ordinal, ushort seq) =>
        new(at, true, payload, line, ordinal, seq);

    // reach: unit (immutable raw hardware capture replay; no robot, scheduler or new acceptance run).
    [Fact]
    public void FirstRunDatagram564BacklogPrecedesAbortAnd569HasOneLeftoverFrame()
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "re-analysis", "jobs"))) root = root.Parent;
        Assert.NotNull(root);
        string path = Path.Combine(root.FullName, "re-analysis", "acceptance", "hardware", "20261020-074223-M5-HW-2cc1cf", "frames.jsonl");
        var frames = File.ReadLines(path).Select((line, index) =>
        {
            var j = JsonNode.Parse(line)!;
            return new M5HwAnalysis.CapturedFrame(index + 1, j["atMs"]!.GetValue<double>(),
                j["outbound"]!.GetValue<bool>(), FrameCodec.Decode(Convert.FromBase64String(j["rawBase64"]!.GetValue<string>())));
        }).TakeWhile(f => f.AtMs <= 8134.5489);
        var wire = M5HwAnalysis.ReadWire(frames).ToArray();
        var abort = Assert.Single(wire.Where(m => m.FrameLine == 564 && m.Payload[0] == 0x8D));
        Assert.Equal((ushort)395, abort.ReliableSequence);
        Assert.True(abort.SubmessageOrdinal > 0);
        // Source-checked replay in the judgement: audio391/face392 BEFORE Abort395, audio396/face397 AFTER.
        var result = M5Hw.GradeAbort(wire, 7715.5276, abort.AtMs + 400);
        Assert.Equal("OBSERVED_CANCEL_WIRE_SHAPE", Verdict(result));
        Assert.Equal(1, result["audioFrameCount"]!.GetValue<int>());
        Assert.Equal(1, result["faceCount"]!.GetValue<int>());
        Assert.Contains(result["preCommandBacklog"]!.AsArray(), m => m!["reliableSequence"]!.GetValue<ushort>() == 391);
        Assert.Contains(result["postAbortMessages"]!.AsArray(), m => m!["frameLine"]!.GetValue<int>() == 569 && m["reliableSequence"]!.GetValue<ushort>() == 396);
    }

    // reach: unit (synthetic same-datagram order/retransmissions substitute hardware transport).
    [Fact]
    public void OrdinalNotTimestampSeparatesSameDatagramDataAndRetransmissionCountsOnce()
    {
        var wire = new[]
        {
            Out(10, new byte[]{0x9B,1}, 1, 0, 1),
            Out(100, new byte[]{0x8F}, 2, 0, 2), Out(100, new byte[]{0x97,1}, 2, 1, 3),
            Out(100, new byte[]{0x8D}, 2, 2, 4),
            Out(100, new byte[]{0x8F}, 2, 3, 5), Out(100, new byte[]{0x97,1}, 2, 4, 6),
            Out(200, new byte[]{0x8D}, 3, 0, 4), Out(200, new byte[]{0x8F}, 3, 1, 5),
            Out(200, new byte[]{0x97,1}, 3, 2, 6)
        };
        var result = M5Hw.GradeAbort(wire, 90, 500);
        Assert.Equal("OBSERVED_CANCEL_WIRE_SHAPE", Verdict(result));
        Assert.Equal(2, result["preCommandBacklog"]!.AsArray().Count);
        Assert.Equal(2, result["postAbortMessages"]!.AsArray().Count);
        Assert.Equal(1, result["abortCount"]!.GetValue<int>());
        Assert.Equal("INCONCLUSIVE", Verdict(M5Hw.GradeAbort(wire.Append(Out(250, new byte[]{0x8F}, 4, 0, 7)), 90, 500)));
        Assert.Equal("INCONCLUSIVE", Verdict(M5Hw.GradeAbort(wire.Append(Out(250, new byte[]{0x9A}, 4, 0, 7)), 90, 500)));
        Assert.Equal("INCONCLUSIVE", Verdict(M5Hw.GradeAbort(wire, 90, 490)));
    }

    // reach: unit (synthetic encoded/decoded reliable frames; no hardware traffic).
    [Fact]
    public void WireReaderRetainsRawLineOrdinalSequenceAndDeduplicatesResends()
    {
        var frame = FrameCodec.Decode(FrameCodec.Encode(Frame.Multiple(new[] {
            SubMessage.Data(new byte[]{0x8F}, true, 391), SubMessage.Data(new byte[]{0x97,1}, true, 392),
            SubMessage.Data(new byte[]{0x8D}, true, 393) }, 0)));
        var rows = M5HwAnalysis.ReadWire(new[] { new M5HwAnalysis.CapturedFrame(564, 100, true, frame),
            new M5HwAnalysis.CapturedFrame(565, 200, true, frame) }).ToArray();
        Assert.Equal(3, rows.Length);
        Assert.All(rows, m => Assert.Equal(564, m.FrameLine));
        Assert.Equal(new[] {0,1,2}, rows.Select(m => m.SubmessageOrdinal));
        Assert.Equal(new ushort?[] {391,392,393}, rows.Select(m => m.ReliableSequence));
    }
}
