using System.Text.Json;
using Cozmo.Transport;
using Xunit;

namespace Cozmo.Protocol.Tests;

internal static class RecordedRobotInput
{
    internal static IEnumerable<(double At, RobotMessage Message)> ReadInbound(string bundle,
        Func<double, bool> window, double endAt, IReadOnlyCollection<byte> tags, bool discardRetries = false) =>
        Read(bundle, window, endAt, tags, outbound: false, discardRetries);

    internal static IEnumerable<(double At, RobotMessage Message)> ReadOutbound(string bundle,
        Func<double, bool> window, double endAt, IReadOnlyCollection<byte> tags) =>
        Read(bundle, window, endAt, tags, outbound: true, discardRetries: true);

    private static IEnumerable<(double At, RobotMessage Message)> Read(string bundle,
        Func<double, bool> window, double endAt, IReadOnlyCollection<byte> tags, bool outbound, bool discardRetries)
    {
        var seenReliableIds = new HashSet<ushort>();
        using var stream = new FileStream(Path.Combine(bundle, "frames.jsonl"), FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        while (reader.ReadLine() is { } line)
        {
            using var document = JsonDocument.Parse(line);
            var row = document.RootElement;
            double at = row.GetProperty("atMs").GetDouble();
            if (at > endAt) yield break;
            if (!window(at) || row.GetProperty("outbound").GetBoolean() != outbound) continue;
            Assert.True(FrameCodec.TryDecode(Convert.FromBase64String(row.GetProperty("rawBase64").GetString()!),
                out var frame, out var error), error);
            foreach (var message in frame!.Messages)
                if (message.Type is ReliableMessageType.SingleReliableMessage or ReliableMessageType.SingleUnreliableMessage &&
                    message.Payload is { Length: > 0 } bytes && tags.Contains(bytes[0]) &&
                    (!discardRetries || !message.IsReliable || seenReliableIds.Add(message.Seq)))
                    yield return (at, RobotMessage.Parse(bytes));
        }
    }
}
