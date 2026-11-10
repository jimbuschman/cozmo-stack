using System.Text;
using Cozmo.Robot.Behavior;

namespace Cozmo.Robot.Vision
{

internal sealed record NativeLoadFaceAlbumFileRequest(VisionSystem Vision,EngineRobot Owner,FaceRecognizer Recognizer,
    int InitEpoch,int Removal,string Path,bool Relative);

public sealed partial class VisionSystem
{
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
        int albumMax=NvStorageComponent.MaxSizeForEntryTag(FaceAlbumNvTag);
        int enrollmentMax=NvStorageComponent.MaxSizeForEntryTag(FaceEnrollmentNvTag);
        if(album.Length>=albumMax)
        {
            _robot.Engine.EmitSourceLog(EngineLogLevel.Error,null,"VisionComponent.SaveFaceAlbumToRobot.AlbumDataTooLarge",$"Album data is {album.Length} max size is {albumMax}");
            return false;
        }
        if(enrollment.Length>=enrollmentMax)
        {
            _robot.Engine.EmitSourceLog(EngineLogLevel.Error,null,"VisionComponent.SaveFaceAlbumToRobot.EnrollDataTooLarge",$"Enroll data is {enrollment.Length} max size is {enrollmentMax}");
            return false;
        }
        bool CompletionCurrent()=>!owner.Lifetime.Destroying && ReferenceEquals(_robot.Engine.Robot,owner) && ReferenceEquals(_robot.Engine.NvStorage,nv);
        void Completed(NvResult result)=>CompleteFaceAlbumSave(owner,result,CompletionCurrent,null,true);
        return AdmitFaceAlbumSave(nv,album,enrollment,current,Completed,Completed,true);
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
    }
}
