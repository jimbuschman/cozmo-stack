using System.Text;
using Cozmo.Robot.Behavior;

namespace Cozmo.Robot.Vision
{

internal sealed record NativeLoadFaceAlbumFileRequest(VisionSystem Vision,EngineRobot Owner,FaceRecognizer Recognizer,
    int InitEpoch,int Removal,string Path,bool Relative);
// G2E 0x8A EraseAllEnrolledFaces carries no fields; the request names the vision owner it was posted for.
internal sealed record NativeEraseAllFacesRequest(VisionSystem Vision,EngineRobot Owner);

public sealed partial class VisionSystem
{
    // fidelity: M14-012
    // G2E 0x8A handler 0x00658076 -> VisionComponent::EraseAllFaces 0x006514FC: under the vision gate the recognizer's
    // EraseAllFaces (0x008678D8), then the ordinary SaveFaceAlbumToRobot (both vectors empty: erase 0x184000, then
    // 0x183000 only if the first was admitted), then one RobotErasedAllEnrolledFaces 0x58, without waiting for NV replies.
    private void HandleEraseAllFacesRequest(NativeEraseAllFacesRequest request)
    {
        var owner=request.Owner;var recognizer=Recognizer;int removal=Volatile.Read(ref _removals);
        bool Current()=>!_disposed && _stockRecognizer && owner.HasExternalInterface && !owner.Lifetime.Destroying &&
            ReferenceEquals(_robot.Engine.Robot,owner) && removal==Volatile.Read(ref _removals) &&
            ReferenceEquals(Recognizer,recognizer) && recognizer.OwnsStockDetector(_stockFaceDetector);
        if(!_stockRecognizer)
        {SteppedBehavior.ReportMissing("M14-012 EraseAllEnrolledFaces needs the stock recognizer's native EraseAllFaces and album export; an injected recognizer has neither");return;}
        if(!Current())return;
        if(!recognizer.IsAvailable && _stockFaceDetector.StockStatus==2)
        {
            // The engine's EraseAllFaces has no availability check (0x006514FC..0x00651538). With the face library's init failed (the state the
            // stack models as StockStatus 2; the same sequence as the startup file branch, FaceAlbumFile.cs, M11-059): the recognizer's erase fails,
            // the save cannot export (GetSerializedFaceDataFailed, no NV traffic, 0x00657168..0x006571D8), and the 0x58 is still broadcast.
            recognizer.EraseUnreadyForFile();
            _robot.Engine.EmitSourceLog(EngineLogLevel.Error,null,"FaceRecognizer.EraseAllFaces.FaceLibClearAlbumFailed","");
            if(!Current())return;
            Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();
            _robot.Engine.EmitSourceLog(EngineLogLevel.Error,null,"FaceRecognizer.GetSerializedAlbum.NotInitialized","");
            if(!Current())return;
            _robot.Engine.EmitSourceLog(EngineLogLevel.Error,null,"FaceRecognizer.GetSerializedData.GetSerializedAlbumFail","");
            if(!Current())return;
            _robot.Engine.EmitSourceLog(EngineLogLevel.Warning,null,"VisionComponent.SaveFaceAlbumToRobot.GetSerializedFaceDataFailed","GetSerializedFaceData failed");
            if(!Current())return;
            PublishAlbumErase(owner,Current);
            return;
        }
        if(!recognizer.IsAvailable)
        {SteppedBehavior.ReportMissing("M14-012 EraseAllEnrolledFaces with the recognizer guest unavailable for a reason other than a failed face-library init (a host/guest-library input): not built");return;}
        using(LogCallbackDeferral.EnterGated(_busy))
        {
            if(!Current() || !recognizer.EraseAllForFile(Current))return;
        }
        if(!Current())return;
        try { SaveFaceAlbumToRobot(owner,recognizer,removal,_robot.Engine.NvStorage,null,null); }
        catch(Exception error) when(error is not OutOfMemoryException) { FileHandlerFailed(error); }
        if(!Current())return;
        PublishAlbumErase(owner,Current);
    }

    // Test-only rejection of a real admission; null leaves the original NV call intact.
    internal Func<byte,uint,bool>? FileSaveRejectForTest;

    internal NativeLoadFaceAlbumFileRequest? CaptureFileAlbumRequest(EngineRobot owner,string path,bool relative)
    {
        lock(_groundGate)
        {
            if(_disposed || !_stockRecognizer || !Recognizer.OwnsStockDetector(_stockFaceDetector) || owner.Lifetime.Destroying || !ReferenceEquals(_robot.Engine.Robot,owner) ||
               !ReferenceEquals(_groundOwner,owner) || _nativeInitialization is not { } state ||
               !ReferenceEquals(state.Owner,owner))return null;
            return new(this,owner,Recognizer,_nativeInitializationEpoch,Volatile.Read(ref _removals),path,relative);
        }
    }

    // FullFilePath 0x80315C and scope-1 pathToResource 0x84BE66: textual, not Path.Combine.
    internal static string? ResolveFileAlbumRequest(string path,bool relative,string? resources)
    {
        if(!relative)return path;
        if(resources is null)return null;
        string suffix=path;
        if(suffix.StartsWith('/'))suffix=suffix[1..];
        if(suffix.EndsWith('/'))suffix=suffix[..^1];
        string subpath="config/basestation/faceAlbums"+(path.Length==0?"":"/"+suffix);
        return resources+"/"+subpath;
    }

    private void HandleFileAlbumRequest(NativeLoadFaceAlbumFileRequest request)
    {
        var owner=request.Owner;var recognizer=request.Recognizer;var nv=_robot.Engine.NvStorage;
        bool Current()=>!_disposed && owner.HasExternalInterface && !owner.Lifetime.Destroying &&
            ReferenceEquals(_robot.Engine.Robot,owner) && NativeVisionAttemptCurrent(owner,request.InitEpoch) &&
            request.Removal==Volatile.Read(ref _removals) && ReferenceEquals(Recognizer,recognizer) &&
            recognizer.OwnsStockDetector(_stockFaceDetector) && nv is not null && ReferenceEquals(_robot.Engine.NvStorage,nv);
        if(!Current())return;
        string? directory=ResolveFileAlbumRequest(request.Path,request.Relative,_robot.Engine.Options.ResourcesPath);
        if(directory is null){SteppedBehavior.ReportMissing("M14-012 file album request needs the resource scope root");return;}
        if(!recognizer.IsAvailable && _stockFaceDetector.StockStatus!=2)
        {SteppedBehavior.ReportMissing("M14-012 file album request guest unavailable");return;}
        try
        {
            (bool Available,int Result,IReadOnlyList<FileLoadedFace> Reports) loaded;
            using(LogCallbackDeferral.EnterGated(_busy))
            {
                if(!Current())return;
                loaded=recognizer.LoadFileAlbum(directory,Current);
            }
            if(!Current() || !loaded.Available)return;
            if(loaded.Result!=0)
            {
                _robot.Engine.EmitSourceLog(EngineLogLevel.Warning,null,"VisionComponent.LoadFaceAlbum.LoadFromFileFailed",$"AlbumFile: {directory}");
                return;
            }
            if(!BeginFileAlbumSave(owner,recognizer,nv!,Current) || !Current())return;
            if(!PublishAlbumErase(owner,Current))return;
            foreach(var report in loaded.Reports)if(!PublishLoadedFace(owner,report,Current))return;
        }
        catch(Exception error) when(error is not OutOfMemoryException)
        {SteppedBehavior.ReportMissing($"M14-012 file album request unavailable: {error.Message}");}
    }

    private bool BeginFileAlbumSave(EngineRobot owner,FaceRecognizer recognizer,NvStorageComponent nv,Func<bool> current)
    {
        byte[] album,enrollment;
        using(LogCallbackDeferral.EnterGated(_busy))
        {
            if(!current() || !recognizer.IsAvailable)return false;
            (album,enrollment)=recognizer.ExportStockData();
        }
        if(!current())return false;
        if(FaceAlbumSaveTooLarge(album,enrollment))return false;
        bool CompletionCurrent()=>!owner.Lifetime.Destroying && ReferenceEquals(_robot.Engine.Robot,owner) && ReferenceEquals(_robot.Engine.NvStorage,nv);
        void Completed(NvResult result)=>CompleteFaceAlbumSave(owner,result,CompletionCurrent,null,true);
        return AdmitFaceAlbumSave(nv,album,enrollment,current,Completed,Completed,true);
    }

    // fidelity: M14-012
    // SaveFaceAlbumToRobot 0x006571DA..0x0065720E: each size against GetMaxSizeForEntryTag, album first; a size at or
    // above it fails through sErrorF and _errG (0x00657256..0x006572FE) and returns before any NV request.
    private bool FaceAlbumSaveTooLarge(byte[] album,byte[] enrollment)
    {
        int albumMax=NvStorageComponent.MaxSizeForEntryTag(FaceAlbumNvTag);
        int enrollmentMax=NvStorageComponent.MaxSizeForEntryTag(FaceEnrollmentNvTag);
        if(album.Length>=albumMax)
        {
            _robot.Engine.EmitSourceLog(EngineLogLevel.Error,null,"VisionComponent.SaveFaceAlbumToRobot.AlbumDataTooLarge",$"Album data is {album.Length} max size is {albumMax}");
            Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();
            return true;
        }
        if(enrollment.Length>=enrollmentMax)
        {
            _robot.Engine.EmitSourceLog(EngineLogLevel.Error,null,"VisionComponent.SaveFaceAlbumToRobot.EnrollDataTooLarge",$"Enroll data is {enrollment.Length} max size is {enrollmentMax}");
            Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();
            return true;
        }
        return false;
    }

    private bool AdmitFaceAlbumSave(NvStorageComponent nv,byte[] album,byte[] enrollment,Func<bool> current,
        Action<NvResult> albumCompleted,Action<NvResult> enrollmentCompleted,bool request)
    {
        int Admit(byte operation,uint tag,byte[]? bytes,Action<NvResult> completed)
        {
            if(!current())return 0;
            if(request && FileSaveRejectForTest?.Invoke(operation,tag)==true)return 0;
            if(!current())return 0;
            return operation==NvStorageComponent.OpErase ? nv.Erase(tag,completed) : nv.Write(tag,bytes!,completed);
        }
        bool erase=album.Length==0 && enrollment.Length==0;
        byte operation=erase?NvStorageComponent.OpErase:NvStorageComponent.OpWrite;
        if(!erase)
        {
            static byte[] Pad(byte[] value)
            {
                int length=(value.Length+3)&~3;
                if(length==value.Length)return value;
                var padded=new byte[length];value.CopyTo(padded,0);return padded;
            }
            album=Pad(album);enrollment=Pad(enrollment);
        }
        void Initiated()
        {
            if(request && !erase && current())
                _robot.Engine.EmitSourceLog(EngineLogLevel.Info,"Unnamed","VisionComponent.SaveFaceAlbumToRobot.Initiated",$"Initiated save of {album.Length}-byte album data and {enrollment.Length}-byte enroll data to NVStorage");
        }
        if(erase && request)
        {
            _robot.Engine.EmitSourceLog(EngineLogLevel.Info,"Unnamed","VisionComponent.SaveFaceAlbumToRobot.EmptyAlbumData","Will erase robot album data");
            if(!current())return false;
        }
        if(Admit(operation,FaceAlbumNvTag,album,albumCompleted)==0)
        {
            if(request && current())_robot.Engine.EmitSourceLog(EngineLogLevel.Warning,null,erase?"VisionComponent.SaveFaceAlbumToRobot.SendEraseAlbumDataFail":"VisionComponent.SaveFaceAlbumToRobot.SendWriteAlbumDataFail","");
            Initiated();
            return false;
        }
        if(!current())return false;
        if(Admit(operation,FaceEnrollmentNvTag,enrollment,enrollmentCompleted)==0)
        {
            if(request && current())_robot.Engine.EmitSourceLog(EngineLogLevel.Warning,null,erase?"VisionComponent.SaveFaceAlbumToRobot.SendEraseEnrollDataFail":"VisionComponent.SaveFaceAlbumToRobot.SendWriteEnrollDataFail","");
            Initiated();
            return erase && current();
        }
        Initiated();
        return current();
    }

    private void CompleteFaceAlbumSave(EngineRobot owner,NvResult result,Func<bool> current,Action<NvResult>? callback,bool isolateLocal)
    {
        if(!current())return;
        if(result.Result!=0)
        {
            _robot.Engine.EmitSourceLog(EngineLogLevel.Warning,null,"VisionComponent.SaveFaceAlbumToRobot.NVWriteFailed",$"NVResult = {NvStorageComponent.NvResultName(result.Result)}");
            if(!current())return;
            if(owner.HasExternalInterface)
            {
                var report=new EngineErrorCodeReport(3);
                _robot.Engine.DeliverAnimationReportToApp(0x7C,new byte[]{3});
                if(isolateLocal)_robot.Engine.AnimationMessages.DeliverIsolated(0x7C,true,report,current,FileHandlerFailed);
                else _robot.Engine.AnimationMessages.Deliver(0x7C,true,report);
            }
            if(isolateLocal && !current())return;
            Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();
        }
        if(current())callback?.Invoke(result);
    }
}

}

namespace Cozmo.Robot
{
    public sealed partial class CozmoRobot
    {
        /// <summary>Requests loading a face album from a path, relative to resources by default.</summary>
        public void LoadFaceAlbumFromFile(string path,bool relativeToResources=true)
        {
            ArgumentNullException.ThrowIfNull(path);
            byte[] encoded;
            try { encoded=new UTF8Encoding(false,true).GetBytes(path); }
            catch(EncoderFallbackException) { SteppedBehavior.ReportMissing("M14-012 file album request path is not representable as UTF-8");return; }
            if(path.Contains('\0')){SteppedBehavior.ReportMissing("M14-012 file album request path contains NUL");return;}
            if(encoded.Length>byte.MaxValue)throw new ArgumentException("The UTF-8 path exceeds the CLAD string length.",nameof(path));
            var owner=Engine.Robot;
            if(owner is null || _vision?.CaptureFileAlbumRequest(owner,path,relativeToResources) is not {} request)
            {SteppedBehavior.ReportMissing("M14-012 file album request original owner or stock recipient unavailable");return;}
            Engine.PostGameMessage(()=>
            {
                if(!owner.Lifetime.Destroying && ReferenceEquals(Engine.Robot,owner) && owner.HasExternalInterface)
                    Engine.AnimationMessages.Deliver(0x8F,false,request);
            });
        }

        // fidelity: M14-012
        /// <summary>Requests erasure of every enrolled face (G2E 0x8A EraseAllEnrolledFaces).</summary>
        public void EraseAllEnrolledFaces()
        {
            var owner=Engine.Robot;var vision=_vision;
            if(owner is null || vision is null)
            {SteppedBehavior.ReportMissing("M14-012 EraseAllEnrolledFaces needs a robot owner and its vision component");return;}
            var request=new Vision.NativeEraseAllFacesRequest(vision,owner);
            Engine.PostGameMessage(()=>
            {
                // 0x0065019E: VisionComponent subscribes 0x8A only when the external interface exists.
                if(!owner.Lifetime.Destroying && ReferenceEquals(Engine.Robot,owner) && owner.HasExternalInterface)
                    Engine.AnimationMessages.Deliver(0x8A,false,request);
            });
        }
    }
}
