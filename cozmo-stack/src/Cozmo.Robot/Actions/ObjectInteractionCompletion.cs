using Cozmo.Protocol;
namespace Cozmo.Robot;

/// <summary>Immutable source object-interaction completion. Undefined native words are never published.</summary>
public sealed record ObjectInteractionCompletion(uint Word0,uint Word1,uint Word2,uint Word3,uint Word4,byte Count,bool Flag,bool IsPayloadDefined=true)
{
    public byte[] PackBody()
    {
        if(!IsPayloadDefined)throw new InvalidOperationException("M1-034 undefined object interaction payload");
        return new CladWriter().U32(Word0).U32(Word1).U32(Word2).U32(Word3).U32(Word4).U8(Count).U8(Flag?(byte)1:(byte)0).ToArray();
    }
}
