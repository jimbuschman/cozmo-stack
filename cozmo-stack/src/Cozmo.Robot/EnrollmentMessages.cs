using System.Buffers.Binary;
using System.Text;

namespace Cozmo.Robot;

// G2E 0x87, 0x00737CEE..0x00737D3A. The app supplies observed ID zero.
internal sealed record NativeEnrollmentRequest(string Name, int ObservedId, int SaveId, bool Save, bool SayName, bool UseMusic);
internal sealed record NativeRenameFaceRequest(int Id, string OldName, string NewName);

/// <summary>An enrolled face renamed by the native recognizer (E2G 0x1D).</summary>
public sealed record FaceRenamedReport(int Id, string Name);

/// <summary>The result, face ID, and name delivered by FaceEnrollmentCompleted (E2G 0x56).</summary>
public sealed record FaceEnrollmentCompletedReport(byte Result, int Id, string Name);

public sealed partial class CozmoRobot
{
    /// <summary>Requests a rename of an enrolled face; Meet activity admission remains separate.</summary>
    public void UpdateEnrolledFaceById(int id,string oldName,string newName)
    {
        ArgumentNullException.ThrowIfNull(oldName);ArgumentNullException.ThrowIfNull(newName);
        if(Encoding.UTF8.GetByteCount(oldName)>byte.MaxValue || Encoding.UTF8.GetByteCount(newName)>byte.MaxValue)
            throw new ArgumentException("The UTF-8 name exceeds the CLAD string length.");
        var owner=Engine.Robot;var request=new NativeRenameFaceRequest(id,oldName,newName);
        Engine.PostGameMessage(()=>
        {
            if(owner is not null && !owner.Lifetime.Destroying && ReferenceEquals(Engine.Robot,owner))
                Engine.AnimationMessages.Deliver(0x8C,false,request);
        });
    }
    /// <summary>Requests named enrollment for the current robot. Activity admission is separate.</summary>
    public void SetFaceToEnroll(string name, int saveId = 0)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (Encoding.UTF8.GetByteCount(name) > byte.MaxValue)
            throw new ArgumentException("The UTF-8 name exceeds the CLAD string length.", nameof(name));
        var owner = Engine.Robot;
        var request = new NativeEnrollmentRequest(name, 0, saveId, true, true, true);
        Engine.PostGameMessage(() =>
        {
            if (owner is not null && !owner.Lifetime.Destroying && ReferenceEquals(Engine.Robot, owner))
                Engine.AnimationMessages.Deliver(0x87, false, request);
        });
    }

    /// <summary>Requests cancellation of enrollment on the current robot.</summary>
    public void CancelFaceEnrollment()
    {
        var owner = Engine.Robot;
        Engine.PostGameMessage(() =>
        {
            if (owner is not null && !owner.Lifetime.Destroying && ReferenceEquals(Engine.Robot, owner))
                Engine.AnimationMessages.Deliver(0x88, false);
        });
    }
}

public sealed partial class CozmoEngine
{
    /// <summary>Raised after a successful enrolled-face rename.</summary>
    public event Action<FaceRenamedReport>? FaceRenamed;
    internal Action? CaptureFaceRenamed(FaceRenamedReport report)=>CaptureAppEvent(FaceRenamed,report);
    /// <summary>Raised when the engine reports a completed face-enrollment attempt.</summary>
    public event Action<FaceEnrollmentCompletedReport>? FaceEnrollmentCompleted;

    internal Action? CaptureEnrollmentCompleted(FaceEnrollmentCompletedReport report)
        => CaptureAppEvent(FaceEnrollmentCompleted, report);
}

public sealed partial class EngineRobot
{
    internal void PublishFaceRenamed(int id,string name)
    {
        if(Lifetime.Destroying || !ReferenceEquals(Engine.Robot,this) || !HasExternalInterface)return;
        byte[] bytes=Encoding.UTF8.GetBytes(name);int length=unchecked((byte)bytes.Length);
        byte[] body=new byte[5+length];BinaryPrimitives.WriteInt32LittleEndian(body,id);body[4]=(byte)length;
        bytes.AsSpan(0,length).CopyTo(body.AsSpan(5));
        var report=new FaceRenamedReport(id,name);
        Engine.DeliverAnimationReportToApp(0x1D,body,Engine.CaptureFaceRenamed(report));
        Engine.AnimationMessages.Deliver(0x1D,true,report);
    }
    // 0x005FE520..0x005FE528; E2G pack 0x0071568E..0x007156C8.
    internal void PublishEnrollmentResult(byte result, int id, string name)
    {
        if (Lifetime.Destroying || !ReferenceEquals(Engine.Robot, this) || !HasExternalInterface) return;
        byte[] nameBytes = Encoding.UTF8.GetBytes(name);
        int length = unchecked((byte)nameBytes.Length);
        byte[] body = new byte[6 + length];
        body[0] = result;
        BinaryPrimitives.WriteInt32LittleEndian(body.AsSpan(1), id);
        body[5] = (byte)length;
        nameBytes.AsSpan(0, length).CopyTo(body.AsSpan(6));
        var report = new FaceEnrollmentCompletedReport(result, id, name);
        Engine.DeliverAnimationReportToApp(0x56, body, Engine.CaptureEnrollmentCompleted(report));
        Engine.AnimationMessages.Deliver(0x56, true, report);
    }
}
