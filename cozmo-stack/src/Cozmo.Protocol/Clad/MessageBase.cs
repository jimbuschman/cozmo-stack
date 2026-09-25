namespace Cozmo.Protocol;

/// <summary>
/// A CLAD robot message: one byte tag (<see cref="RobotMessageId"/>, the EngineToRobot/RobotToEngine union tag)
/// followed by the packed struct. The concrete classes and their codecs are generated from
/// re-analysis/protocol/cozmo_robot_protocol.json; see <see cref="MessageCatalog"/> for each message's
/// layout confidence and hardware-verification status.
/// </summary>
public abstract class RobotMessage
{
    public abstract RobotMessageId Id { get; }
    public abstract void WriteBody(CladWriter w);

    public MessageInfo? Info => MessageCatalog.Lookup((byte)Id);

    // fidelity: M2-008
    /// <summary>
    /// The EngineToRobot/RobotToEngine union as the engine packs it: the 1-byte tag first (EngineToRobot::Pack
    /// 0x007AB6B8..0x007AB6C4), then the member, so the size is 1 + the member's size (EngineToRobot::Size
    /// 0x007ABB98). Each generated class carries its own tag, as each typed union constructor writes it (S7).
    /// </summary>
    public byte[] ToBytes()
    {
        var w = new CladWriter().U8((byte)Id);
        WriteBody(w);
        return w.ToArray();
    }

    /// <summary>
    /// Parses tag + body. A message we cannot decode (unknown tag, short body, or a layout still marked
    /// unresolved) becomes a <see cref="RawRobotMessage"/> so the bytes are preserved rather than guessed at.
    /// </summary>
    public static RobotMessage Parse(ReadOnlyMemory<byte> data)
    {
        if (data.Length == 0) throw new FormatException("empty robot message");
        var id = (RobotMessageId)data.Span[0];
        var body = data[1..];
        if (GeneratedMessages.Parsers.TryGetValue(id, out var parse))
        {
            try
            {
                var r = new CladReader(body);
                var m = parse(r);
                if (r.Remaining == 0) return m;
                return new RawRobotMessage(id, body.ToArray(), $"decoded but {r.Remaining} trailing byte(s) left over");
            }
            catch (FormatException e) { return new RawRobotMessage(id, body.ToArray(), e.Message); }
            catch (InvalidOperationException e) { return new RawRobotMessage(id, body.ToArray(), e.Message); }
        }
        return new RawRobotMessage(id, body.ToArray(), "no codec for this tag");
    }

    public override string ToString() => $"{Info?.CladType ?? "?"}(0x{(byte)Id:x2})";
}

/// <summary>A message kept as opaque bytes: unknown tag or a body we could not decode. Round-trips exactly.</summary>
public sealed class RawRobotMessage : RobotMessage
{
    public override RobotMessageId Id { get; }
    public byte[] Body { get; }
    public string? ParseNote { get; }

    public RawRobotMessage(RobotMessageId id, byte[] body, string? note = null)
    {
        Id = id; Body = body; ParseNote = note;
    }

    public override void WriteBody(CladWriter w) => w.Bytes(Body);

    public override string ToString() =>
        $"{Info?.CladType ?? "Unknown"}(0x{(byte)Id:x2}) raw[{Body.Length}] {Hex.Dump(Body, 32)}" +
        (ParseNote is null ? "" : $" !{ParseNote}");
}
