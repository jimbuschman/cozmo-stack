using System.Net;
using System.Text.Json.Nodes;
using Cozmo.Conformance;
using Cozmo.Robot;
using Cozmo.Robot.Vision;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

public sealed class M15CubeBundleTests
{
    [Fact]
    public void SkillsBundleRetainsNamedNumbersAndEvidenceWhenOneFileCannotBeWritten()
    {
        string root = RepositoryRoot();
        var dir = Directory.CreateTempSubdirectory("cozmo-skills-bundle-");
        try
        {
            Directory.CreateDirectory(Path.Combine(dir.FullName, "messages.jsonl"));
            var run = new M15CubeRun(root, dir.FullName, IPAddress.Loopback, root, [], ["--skills"], true, false)
                { Skills = true };
            run.Event("sample", new JsonObject
            {
                ["nan"] = double.NaN, ["pos"] = float.PositiveInfinity, ["neg"] = double.NegativeInfinity, ["finite"] = 1.25
            });
            run.RecordObjectObservation(new RobotObservedObjectReport(71, (uint)ObjectFamily.Charger, (uint)ObjectType.Charger_Basic,
                9, float.NaN, 10, 20, 30, new EngineGamePose(84, -5, 0, 1, 0, 0, 0, 6), 0, true));
            var signal = new DockingErrorSignal { Timestamp = 72, XDist = 84, YDist = -5, ZDist = 3, Angle = .125f };
            byte[] payload = signal.ToBytes();
            var frame = Frame.Single(new SubMessage(ReliableMessageType.SingleUnreliableMessage, payload, 0), 0);
            byte[] bytes = FrameCodec.Encode(frame);
            run.RecordFrame(new FrameEvent(true, DateTime.UtcNow, bytes, FrameCodec.Decode(bytes), null));

            run.WriteBundle();

            var evt = ReadJson("events.jsonl");
            Assert.Equal("NaN", evt["nan"]!.GetValue<string>());
            Assert.Equal("Infinity", evt["pos"]!.GetValue<string>());
            Assert.Equal("-Infinity", evt["neg"]!.GetValue<string>());
            Assert.Equal(1.25, evt["finite"]!.GetValue<double>());
            Assert.Equal("NaN", ReadJson("charger-observations.jsonl")["report"]!["ImageX"]!.GetValue<string>());
            Assert.Equal(9u, ReadJson("charger-observations.jsonl")["report"]!["ObjectId"]!.GetValue<uint>());
            var correction = ReadJson("docking-error-signals.jsonl");
            Assert.Equal("0x48", correction["tag"]!.GetValue<string>());
            Assert.Equal(72u, correction["robotTimestamp"]!.GetValue<uint>());
            Assert.Equal(84f, correction["xMm"]!.GetValue<float>());
            Assert.Equal(-5f, correction["yMm"]!.GetValue<float>());
            Assert.Equal(Convert.ToHexString(payload), correction["payloadHex"]!.GetValue<string>());
            Assert.Equal(1, correction["frameLine"]!.GetValue<int>());
            Assert.True(File.Exists(Path.Combine(dir.FullName, "stack-log.txt")));
            Assert.True(File.Exists(Path.Combine(dir.FullName, "README.txt")));
            Assert.Equal(5, ReadJson("result.json")["provenance"]!.AsArray().Count);
            Assert.Contains("Could not write messages.jsonl", ReadJson("result.json")["errors"]!.AsArray()[0]!.GetValue<string>());
            Assert.Contains("Could not write messages.jsonl", Assert.Single(File.ReadAllLines(Path.Combine(dir.FullName, "write-errors.txt"))));
        }
        finally { dir.Delete(recursive: true); }

        JsonNode ReadJson(string name) => JsonNode.Parse(File.ReadAllText(Path.Combine(dir.FullName, name)))!;
    }

    private static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "re-analysis", "fidelity_manifest.json"))) return dir.FullName;
        throw new InvalidOperationException("The test lacks the repository's fidelity manifest");
    }
}
