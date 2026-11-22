using Cozmo.Protocol;
namespace Cozmo.Robot;

/// <summary>Immutable object-interaction completion. Unavailable payloads cannot be packed.</summary>
public sealed record ObjectInteractionCompletion(uint Word0,uint Word1,uint Word2,uint Word3,uint Word4,byte Count,bool Flag,bool IsPayloadDefined=true)
{
    internal string? ZeroFilledWordsRecord { get; init; }

    // Operator decision 2026-11-22: apply only at the cited getters that leave words 1–4 uninitialized.
    internal static ObjectInteractionCompletion SingleWithZeroFilledWords(uint objectId, string record) =>
        new(objectId, 0, 0, 0, 0, 1, false) { ZeroFilledWordsRecord = record };

    public byte[] PackBody()
    {
        if(!IsPayloadDefined)throw new InvalidOperationException("M1-034 undefined object interaction payload");
        return new CladWriter().U32(Word0).U32(Word1).U32(Word2).U32(Word3).U32(Word4).U8(Count).U8(Flag?(byte)1:(byte)0).ToArray();
    }
}
