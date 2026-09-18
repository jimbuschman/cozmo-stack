namespace Cozmo.Protocol;

/// <summary>
/// Anki reliable-transport message types (EReliableMessageType). Values are authoritative:
/// verified against libcozmoEngine.so (IsMutlipleMessagesType / IsMessageTypeAlwaysSentUnreliably /
/// HandleSubMessage jump table) and identical to Anki's open-sourced util/transport/reliableMessageTypes.h.
/// The same byte is used both as the frame header type and as the sub-message type inside
/// Multiple*Messages frames.
/// </summary>
public enum ReliableMessageType : byte
{
    Invalid = 0,
    /// <summary>Engine -> robot: open a connection (reliable, seq 1). PyCozmo calls this frame "RESET".</summary>
    ConnectionRequest = 1,
    /// <summary>Robot -> engine: connection accepted (reliable). PyCozmo: "Connect" packet / "RESET ACK" frame.</summary>
    ConnectionResponse = 2,
    /// <summary>Either side: close the connection (reliable).</summary>
    DisconnectRequest = 3,
    /// <summary>One reliable payload (a CLAD EngineToRobot/RobotToEngine message). PyCozmo: "COMMAND".</summary>
    SingleReliableMessage = 4,
    /// <summary>One unreliable payload (not sequenced, never resent). PyCozmo: "EVENT".</summary>
    SingleUnreliableMessage = 5,
    /// <summary>Part of a payload larger than one frame: [partIndex u8 (1-based)][partCount u8][bytes]. Always reliable.</summary>
    MultiPartMessage = 6,
    /// <summary>Frame container: only reliable sub-messages. PyCozmo: "ENGINE" frame type 0x07.</summary>
    MultipleReliableMessages = 7,
    /// <summary>Frame container: only unreliable sub-messages.</summary>
    MultipleUnreliableMessages = 8,
    /// <summary>Frame container: reliable and unreliable sub-messages. PyCozmo: "ROBOT" frame type 0x09.</summary>
    MultipleMixedMessages = 9,
    /// <summary>Pure acknowledgement (no payload). Not used by the Cozmo engine (sSendAckOnReceipt=false); PyCozmo labels 0x0a "KEYFRAME".</summary>
    Ack = 10,
    /// <summary>17-byte <see cref="PingPayload"/>, unreliable. Frame type 0x0b when sent alone.</summary>
    Ping = 11,
    Count = 12,
}

public static class ReliableMessageTypes
{
    /// <summary>Types that never consume a sequence id (official IsMessageTypeAlwaysSentUnreliably: mask 0x7d over 5..11).</summary>
    public static bool IsAlwaysUnreliable(ReliableMessageType t) => t switch
    {
        ReliableMessageType.SingleUnreliableMessage => true,
        ReliableMessageType.MultipleReliableMessages => true,
        ReliableMessageType.MultipleUnreliableMessages => true,
        ReliableMessageType.MultipleMixedMessages => true,
        ReliableMessageType.Ack => true,
        ReliableMessageType.Ping => true,
        _ => false,
    };

    /// <summary>Frame-level containers (official IsMutlipleMessagesType: 7..9).</summary>
    public static bool IsMultiple(ReliableMessageType t) =>
        t is ReliableMessageType.MultipleReliableMessages
          or ReliableMessageType.MultipleUnreliableMessages
          or ReliableMessageType.MultipleMixedMessages;

    /// <summary>Official IsValidMessageType: 1..11.</summary>
    public static bool IsValid(byte t) => t >= 1 && t < (byte)ReliableMessageType.Count;
}

/// <summary>
/// 16-bit looping sequence ids. Official constants (reliableSequenceId.h, confirmed by NextSequenceId in the
/// engine: movw #0xfffe; addne #1; moveq #1): 0 = invalid/"unreliable", valid range 1..65534, wraps 65534 -> 1.
/// 0xFFFF never appears on the wire from the official stack (PyCozmo uses it internally as "OOB").
/// </summary>
public static class SequenceId
{
    public const ushort Invalid = 0;
    public const ushort Min = 1;
    public const ushort Max = 65534;

    public static ushort Next(ushort s) => s == Max ? Min : (ushort)(s + 1);
    public static ushort Previous(ushort s) => s == Min ? Max : (ushort)(s - 1);

    /// <summary>Inclusive range test with wrap-around, exactly as official IsSequenceIdInRange.</summary>
    public static bool InRange(ushort s, ushort min, ushort max) =>
        max >= min ? (s >= min && s <= max) : (s >= min || s <= max);
}
