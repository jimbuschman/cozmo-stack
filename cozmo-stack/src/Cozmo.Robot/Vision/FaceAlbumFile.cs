using System.Globalization;
using System.Text;

namespace Cozmo.Robot.Vision;

internal sealed record FileLoadedFace(long SinceEnrolled,long SinceUpdated,long SinceSeen,int Id,byte[] Name);
public sealed record LoadedKnownFaceReport(long SecondsSinceEnrolled,long SecondsSinceUpdated,long SecondsSinceSeen,int FaceId,string Name);

public sealed partial class FaceRecognizer
{
    internal void EraseUnreadyForFile()
    {
        lock(_gate){_faces.Clear();_albumEntryToFaceId.Clear();_trackToFace.Clear();}
    }
    // 0x8678D8: erase the recognizer's maps and guest album; FaceWorld is separate.
    internal bool EraseAllForFile(Func<bool> current)
    {
        using var gate=LogCallbackDeferral.EnterGated(_gate);
        if(!current() || _common is null || _album is null || !_okao.IsAvailable)return false;
        foreach(var face in _faces.Values.OrderBy(face=>face.FaceID).ToArray())
        {
            RemoveUserCore(face,currentTrackOnly:true,orderedErase:true);
            if(!current())return false;
        }
        _faces.Clear();_albumEntryToFaceId.Clear();
        if(_okao.ClearAlbum(_album)!=0)
            LogCallbackDeferral.Run(()=>{SourceLog?.Invoke(EngineLogLevel.Error,null,"FaceRecognizer.EraseAllFaces.FaceLibClearAlbumFailed","");Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();});
        return current();
    }

    // 0x8693FC: file metadata and the lexical report list are independent of installation.
    internal (bool Available,int Result,IReadOnlyList<FileLoadedFace> Reports) LoadFileAlbum(string directory,Func<bool> current)
    {
        using var gate=LogCallbackDeferral.EnterGated(_gate);
        var reports=new List<FileLoadedFace>();
        if(!current() || _common is null || _album is null || !_okao.IsAvailable)return (true,1,reports);
        byte[] bytes;
        try { bytes=File.ReadAllBytes(directory+"/data.bin"); }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { LogCallbackDeferral.Run(()=>SourceLog?.Invoke(EngineLogLevel.Warning,null,"FaceRecognizer.LoadAlbum.FileOpenFail",$"Filename: {directory}/data.bin"));return (true,1,reports); }
        if(!current())return (false,1,reports);
        var (loaded,restored)=RestoreNativeCandidate(bytes);
        if(loaded is null)return (true,1,reports);
        bool transferred=false;
        try
        {
            var parsed=new Dictionary<int,EnrolledFaceEntry>();
            if(restored)
            {
            byte[] metadata;
            try { metadata=File.ReadAllBytes(directory+"/enrollData.json"); }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            { metadata=Array.Empty<byte>(); }
            var root=new FirmwareJsonValue();
            if(Json.TryParseInto(metadata,root))
            {
                if(root.Kind is not (FirmwareJsonKind.Object or FirmwareJsonKind.Null))throw new JsonLogicError("file enrollment root is not an object");
                long now=Now;
                foreach(var member in root.Members.OrderBy(pair=>pair.Key,StringComparer.Ordinal))
                {
                    string text=Encoding.Latin1.GetString(Convert.FromHexString(member.Key));
                    var match=System.Text.RegularExpressions.Regex.Match(text,@"^[ \t\n\r\f\v]*[+-]?[0-9]+");
                    if(!match.Success || !int.TryParse(match.Value,NumberStyles.Integer,CultureInfo.InvariantCulture,out int id))
                        throw new FormatException("file enrollment face ID is outside native stoi input");
                    var entry=ParseFileEntry(id,member.Value,now);
                    long last=entry.AlbumEntries.Count==0?0:Math.Max(0,entry.AlbumEntries.Values.Max(value=>value.LastSeenMicros));
                    reports.Add(new FileLoadedFace(unchecked(now-entry.Timestamp28Micros)/MicrosPerSecond,
                        unchecked(now-entry.Timestamp30Micros)/MicrosPerSecond,unchecked(now-last)/MicrosPerSecond,id,Encoding.Latin1.GetBytes(entry.Name)));
                    parsed.TryAdd(id,entry);
                }
            }
            else LogCallbackDeferral.Run(()=>SourceLog?.Invoke(EngineLogLevel.Warning,null,"FaceRecognizer.LoadAlbum.EnrollDataFileReadFail",""));
            }
            if(!current())return (false,1,reports);
            var entries=parsed.Values.ToList();
            if(!CheckConsistency(loaded,entries,deferLog:true))return (true,1,reports);
            if(!current())return (false,1,reports);
            int install=InstallNativeCandidate(loaded,current,out transferred);
            if(install!=0)return (current(),1,reports);
            if(!current())return (false,1,reports);
            _albumEntryToFaceId.Clear();_faces.Clear();_trackToFace.Clear();
            foreach(var entry in entries)
            {
                _faces.TryAdd(entry.FaceID,entry);
                foreach(int id in entry.AlbumEntries.Keys)_albumEntryToFaceId.TryAdd(id,entry.FaceID);
            }
            return (true,0,reports);
        }
        catch(Exception ex) when(ex is JsonLogicError or JsonRuntimeError or FormatException or OverflowException)
        {
            Behavior.SteppedBehavior.ReportMissing($"M1-034 M14-011 terminating file enrollment input at 0x86980A/0x85FEE4 skipped: {ex.Message}");
            reports.Clear();
            return (true,1,reports);
        }
        finally { if(!transferred)_okao.DeleteAlbumHandle(loaded); }
    }

    // SetSerializedAlbum 0x867384 retains the returned candidate even if a later count fails.
    private (OkaoAlbumHandle? Candidate,bool Success) RestoreNativeCandidate(byte[] bytes)
    {
        if(_common is null || bytes.Length==0 || _okao is not NativeOkaoFaceRecognizer native)return (null,false);
        var loaded=_okao.RestoreAlbum(_common,bytes,bytes.Length,out _);
        if(loaded is null)return (null,false);
        if(_okao.GetRegisteredUserNum(loaded,out _)!=0)return (loaded,false);
        if(native.GetRegisteredAllDataNum(loaded,out _)!=0)
        {
            LogCallbackDeferral.Run(()=>SourceLog?.Invoke(EngineLogLevel.Warning,null,"FaceRecognizer.SetSerializedAlbum.GetNumDataFailed",""));
            return (loaded,false);
        }
        return (loaded,true);
    }

    // 0x868948..868BE4: equal dimensions transfer; a smaller candidate is copied then deleted.
    private int InstallNativeCandidate(OkaoAlbumHandle loaded,Func<bool> current,out bool transferred)
    {
        transferred=false;
        if(_album is null || _okao.GetAlbumMaxNum(_album,out int currentUsers,out int currentData)!=0 ||
           _okao.GetAlbumMaxNum(loaded,out int loadedUsers,out int loadedData)!=0)return 1;
        if(loadedUsers>currentUsers || loadedData>currentData)return 1;
        if(loadedUsers==currentUsers && loadedData==currentData)
        {
            var old=_album;_album=loaded;transferred=true;
            _okao.DeleteAlbumHandle(old);return 0;
        }
        if(_okao.ClearAlbum(_album)!=0)return 1;
        for(int entry=0;entry<loadedUsers;entry++)
            for(int slot=0;slot<loadedData;slot++)
            {
                if(!current() || _okao.IsRegistered(loaded,entry,slot,out int registered)!=0)return 1;
                if(registered==0)continue;
                if(_okao.GetFeatureFromAlbum(loaded,entry,slot,out var feature)!=0 ||
                   _okao.RegisterData(_album,feature,entry,slot)!=0)return 1;
            }
        return 0;
    }

    internal (bool Success,IReadOnlyList<FileLoadedFace> Reports) RestoreStockNv(byte[] albumData,byte[] enrollmentData,Func<bool> current)
    {
        using var gate=LogCallbackDeferral.EnterGated(_gate);
        var empty=Array.Empty<FileLoadedFace>();
        if(!current() || _common is null || _album is null || _okao is not NativeOkaoFaceRecognizer native || !native.IsAvailable || albumData.Length==0)return (false,empty);
        var (loaded,restored)=RestoreNativeCandidate(albumData);
        if(loaded is null)return (false,empty);
        bool transferred=false;
        try
        {
            if(!restored)
            {
                LogCallbackDeferral.Run(()=>SourceLog?.Invoke(EngineLogLevel.Warning,null,"FaceRecognizer.SetSerializedData.SetSerializedAlbumFail",""));
                return (false,empty);
            }
            var entries=DeserializeEnrollment(enrollmentData,()=>Now,out int nextFaceId);
            if(entries is null || !current() || !CheckConsistency(loaded,entries,deferLog:true) || !current())return (false,empty);
            if(InstallNativeCandidate(loaded,current,out transferred)!=0 || !current())return (false,empty);
            _nextFaceId=nextFaceId;_albumEntryToFaceId.Clear();_faces.Clear();_trackToFace.Clear();
            foreach(var entry in entries)
            {
                _faces[entry.FaceID]=entry;
                foreach(int id in entry.AlbumEntries.Keys)_albumEntryToFaceId[id]=entry.FaceID;
            }
            long now=Now;
            var reports=entries.OrderBy(entry=>entry.FaceID).Select(entry=>
            {
                long last=entry.AlbumEntries.Count==0?0:Math.Max(0,entry.AlbumEntries.Values.Max(value=>value.LastSeenMicros));
                return new FileLoadedFace(unchecked(now-entry.Timestamp28Micros)/MicrosPerSecond,
                    unchecked(now-entry.Timestamp30Micros)/MicrosPerSecond,unchecked(now-last)/MicrosPerSecond,
                    entry.FaceID,Encoding.Latin1.GetBytes(entry.Name));
            }).ToArray();
            return (true,reports);
        }
        finally {if(!transferred)_okao.DeleteAlbumHandle(loaded);}
    }

    internal void NotifyStockFacesLoaded(IReadOnlyList<FileLoadedFace> reports,Func<bool> current)
    {
        if(FacesLoaded is not {} handlers)return;
        var values=reports.Select(report=>(report.Id,Encoding.Latin1.GetString(report.Name))).ToArray();
        foreach(Action<IReadOnlyList<(int FaceID,string Name)>> handler in handlers.GetInvocationList())
        {
            if(!current())return;
            try {handler(values);}catch(Exception error){Behavior.SteppedBehavior.ReportMissing("M1-034 M14-011 FacesLoaded handler skipped: "+error.Message);}
        }
    }

    private static EnrolledFaceEntry ParseFileEntry(int id,FirmwareJsonValue value,long now)
    {
        FirmwareJsonValue Get(string key)=>Json.Member(value,key);
        long Seconds(FirmwareJsonValue field)=>field.Kind switch
        {
            FirmwareJsonKind.Null=>0, FirmwareJsonKind.Int=>field.Signed,
            FirmwareJsonKind.UInt when field.Unsigned<=long.MaxValue=>(long)field.Unsigned,
            FirmwareJsonKind.Boolean=>field.Boolean?1:0,
            FirmwareJsonKind.Real when double.IsFinite(field.Real) && field.Real>=long.MinValue && field.Real<long.MaxValue=>(long)field.Real,
            _=>throw new JsonLogicError("file enrollment time is not a signed integer")
        };
        var entry=new EnrolledFaceEntry { FaceID=id,Score=1000,CurrentAlbumEntryId=-1,
            Timestamp28Micros=Math.Min(unchecked(Seconds(Get("enrollmentTime"))*MicrosPerSecond),now),
            Timestamp30Micros=Math.Min(unchecked(Seconds(Get("lastDataUpdateTime"))*MicrosPerSecond),now),
            };
        byte[] nameBytes = Json.AsStringBytes(Get("name"));
        entry.SetNativeName(Encoding.Latin1.GetString(nameBytes), nameBytes);
        var items=Get("albumEntries");
        if(items.Kind!=FirmwareJsonKind.Array)return entry;
        bool first=true;
        foreach(var item in items.Elements)
        {
            if(item.Kind!=FirmwareJsonKind.Object || !item.Members.ContainsKey(Convert.ToHexString("albumEntry"u8)) ||
               !item.Members.ContainsKey(Convert.ToHexString("lastSeenTime"u8)))
            { entry.AlbumEntries.Clear();break; }
            int album=Json.AsInt(Json.Member(item,"albumEntry"));
            if(first){entry.CurrentAlbumEntryId=album;first=false;}
            if(album==-1)continue;
            long last=unchecked(Seconds(Json.Member(item,"lastSeenTime"))*MicrosPerSecond);
            entry.AlbumEntries[album]=new EnrolledFaceAlbumEntry{AlbumEntryId=album,LastSeenMicros=last,SessionOnly=entry.CurrentAlbumEntryId==album};
        }
        return entry;
    }
}

public sealed partial class VisionSystem
{
    public event Action<LoadedKnownFaceReport>? LoadedKnownFace;

    internal bool RestoreStockNv(EngineRobot owner,int epoch,byte[] album,byte[] enrollment)
    {
        var recognizer=Recognizer;int removal=Volatile.Read(ref _removals);
        bool Current()=>_stockRecognizer && !owner.Lifetime.Destroying && ReferenceEquals(_robot.Engine.Robot,owner) &&
            NativeVisionAttemptCurrent(owner,epoch) && removal==Volatile.Read(ref _removals) &&
            ReferenceEquals(Recognizer,recognizer) && recognizer.OwnsStockDetector(_stockFaceDetector);
        if(!Current())return false;
        if(!recognizer.IsAvailable && _stockFaceDetector.StockStatus==2)
        {
            _robot.Engine.EmitSourceLog(EngineLogLevel.Warning,null,"VisionComponent.LoadFaceAlbumFromRobot.Failure",$"Failed setting {album.Length}-byte album data and {enrollment.Length}-byte enroll data");
            return false;
        }
        if(!recognizer.IsAvailable)
        {Behavior.SteppedBehavior.ReportMissing("M14-011 stock NV restore guest unavailable");return false;}
        (bool Success,IReadOnlyList<FileLoadedFace> Reports) result;
        try
        {
            using(LogCallbackDeferral.EnterGated(_busy))
            {
                if(!Current())return false;
                result=recognizer.RestoreStockNv(album,enrollment,Current);
            }
        }
        catch(Exception ex) when(ex is not OutOfMemoryException)
        {Behavior.SteppedBehavior.ReportMissing($"M14-011 stock NV restore guest failed: {ex.Message}");return false;}
        if(!Current())return false;
        if(!result.Success)
        {
            _robot.Engine.EmitSourceLog(EngineLogLevel.Warning,null,"VisionComponent.LoadFaceAlbumFromRobot.Failure",$"Failed setting {album.Length}-byte album data and {enrollment.Length}-byte enroll data");
            return false;
        }
        _robot.Engine.EmitSourceLog(EngineLogLevel.Info,"Unnamed","VisionComponent.LoadFaceAlbumFromRobot.Success",$"Finished setting {album.Length}-byte album data and {enrollment.Length}-byte enroll data");
        if(!Current() || !PublishAlbumErase(owner,Current))return false;
        foreach(var face in result.Reports)if(!PublishLoadedFace(owner,face,Current))return false;
        recognizer.NotifyStockFacesLoaded(result.Reports,Current);
        return Current();
    }

    internal bool StartupFileAlbum(EngineRobot owner,string directory,int epoch)
    {
        var recognizer=Recognizer;int removal=Volatile.Read(ref _removals);var nv=_robot.Engine.NvStorage;
        bool Current()=>nv is not null && ReferenceEquals(nv,_robot.Engine.NvStorage) &&
            removal==Volatile.Read(ref _removals) && NativeVisionAttemptCurrent(owner,epoch) &&
            ReferenceEquals(Recognizer,recognizer) && recognizer.OwnsStockDetector(_stockFaceDetector);
        if(!Current())
        { Behavior.SteppedBehavior.ReportMissing("M11-059 file album recognizer or original owner unavailable");return false; }
        if(!recognizer.IsAvailable && _stockFaceDetector.StockStatus==2)
        {
            recognizer.EraseUnreadyForFile();
            _robot.Engine.EmitSourceLog(EngineLogLevel.Error,null,"FaceRecognizer.EraseAllFaces.FaceLibClearAlbumFailed","");
            if(!Current())return false;
            Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();
            _robot.Engine.EmitSourceLog(EngineLogLevel.Error,null,"FaceRecognizer.GetSerializedAlbum.NotInitialized","");
            if(!Current())return false;
            _robot.Engine.EmitSourceLog(EngineLogLevel.Error,null,"FaceRecognizer.GetSerializedData.GetSerializedAlbumFail","");
            if(!Current())return false;
            _robot.Engine.EmitSourceLog(EngineLogLevel.Warning,null,"VisionComponent.SaveFaceAlbumToRobot.GetSerializedFaceDataFailed","GetSerializedFaceData failed");
            if(!Current() || !PublishAlbumErase(owner,Current))return false;
            _robot.Engine.EmitSourceLog(EngineLogLevel.Error,null,"FaceTrackerImpl.LoadAlbum.NotInitialized","");
            if(!Current() || !PublishAlbumErase(owner,Current))return false;
            _robot.Engine.EmitSourceLog(EngineLogLevel.Warning,null,"VisionComponent.Init.LoadFaceAlbumFromFileFailed",$"AlbumFile: {directory}");
            return Current();
        }
        if(!recognizer.IsAvailable)
        { Behavior.SteppedBehavior.ReportMissing("M11-059 file album host recognizer unavailable");return false; }
        try
        {
            using(LogCallbackDeferral.EnterGated(_busy))
            {
                if(!Current() || !recognizer.EraseAllForFile(Current))return false;
            }
            if(!Current())return false;
            byte[] album,enrollment;
            using(LogCallbackDeferral.EnterGated(_busy))
            {
                if(!Current())return false;
                (album,enrollment)=recognizer.ExportStockData();
            }
            if(!Current())return false;
            if(album.Length!=0 || enrollment.Length!=0)
            { Behavior.SteppedBehavior.ReportMissing("M11-059 erased file album produced nonempty save data");return false; }
            bool SavedCurrent()=>!owner.Lifetime.Destroying && ReferenceEquals(_robot.Engine.Robot,owner) && ReferenceEquals(_robot.Engine.NvStorage,nv);
            void Saved(NvResult result)
            {
                if(!SavedCurrent())return;
                if(result.Result!=0)
                {
                    _robot.Engine.EmitSourceLog(EngineLogLevel.Warning,null,"VisionComponent.SaveFaceAlbumToRobot.NVWriteFailed",$"NVResult = {NvStorageComponent.NvResultName(result.Result)}");
                    if(!SavedCurrent())return;
                    if(owner.HasExternalInterface)
                    {
                        var report=new EngineErrorCodeReport(3);
                        _robot.Engine.DeliverAnimationReportToApp(0x7C,new byte[]{3});
                        _robot.Engine.AnimationMessages.DeliverIsolated(0x7C,true,report,SavedCurrent,FileHandlerFailed);
                    }
                    if(SavedCurrent())Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();
                }
            }
            if(nv!.Erase(FaceAlbumNvTag,Saved)==0 || !Current())return false;
            if(nv.Erase(FaceEnrollmentNvTag,Saved)==0 || !Current())return false;
            if(!PublishAlbumErase(owner,Current))return false;
            (bool Available,int Result,IReadOnlyList<FileLoadedFace> Reports) loaded;
            using(LogCallbackDeferral.EnterGated(_busy))
            {
                if(!Current())return false;
                loaded=recognizer.LoadFileAlbum(directory,Current);
            }
            if(!loaded.Available)return false;
            if(!Current() || !PublishAlbumErase(owner,Current))return false;
            foreach(var face in loaded.Reports)
            {
                if(!PublishLoadedFace(owner,face,Current))return false;
            }
            if(loaded.Result!=0 && Current())
                _robot.Engine.EmitSourceLog(EngineLogLevel.Warning,null,"VisionComponent.Init.LoadFaceAlbumFromFileFailed",$"AlbumFile: {directory}");
            return Current();
        }
        catch(Exception ex) when(ex is not OutOfMemoryException)
        { Behavior.SteppedBehavior.ReportMissing($"M11-059 file album host/guest operation unavailable: {ex.Message}");return false; }
    }

    private bool PublishAlbumErase(EngineRobot owner,Func<bool> current)
    {
        if(!current())return false;
        if(!owner.HasExternalInterface)return true;
        _robot.Engine.DeliverAnimationReportToApp(0x58,ReadOnlyMemory<byte>.Empty,_robot.Engine.CaptureAppEvent(EnrolledFacesErased));
        _robot.Engine.AnimationMessages.DeliverIsolated(0x58,true,null,current,FileHandlerFailed);
        if(!current())return false;
        if(EnrolledFacesErasedInternal is {} handlers)
            foreach(Action handler in handlers.GetInvocationList())
            {if(!current())return false;try {handler();}catch(Exception ex){FileHandlerFailed(ex);}}
        return current();
    }

    private bool PublishLoadedFace(EngineRobot owner,FileLoadedFace face,Func<bool> current)
    {
        if(!current())return false;
        _robot.Engine.EmitSourceLog(EngineLogLevel.Info,"VisionComponent","VisionComponent.BroadcastLoadedNamesAndIDs",FormattableString.Invariant($"broadcasting loaded face id: {face.Id}"));
        if(!current())return false;
        if(!owner.HasExternalInterface)return true;
        string name=Encoding.Latin1.GetString(face.Name);
        var report=new LoadedKnownFaceReport(face.SinceEnrolled,face.SinceUpdated,face.SinceSeen,face.Id,name);
        using var bytes=new MemoryStream();using(var writer=new BinaryWriter(bytes,Encoding.UTF8,true))
        {writer.Write(face.SinceEnrolled);writer.Write(face.SinceUpdated);writer.Write(face.SinceSeen);writer.Write(face.Id);byte length=unchecked((byte)face.Name.Length);writer.Write(length);writer.Write(face.Name,0,length);}
        var detailed=_robot.Engine.CaptureAppEvent(LoadedKnownFace,report);
        var legacy=_robot.Engine.CaptureAppEvent(LoadedFaceName,face.Id,name);
        _robot.Engine.DeliverAnimationReportToApp(0x1C,bytes.ToArray(),()=>{detailed?.Invoke();legacy?.Invoke();});
        _robot.Engine.AnimationMessages.DeliverIsolated(0x1C,true,report,current,FileHandlerFailed);
        if(!current())return false;
        if(LoadedFaceNameInternal is {} handlers)
            foreach(Action<int,string> handler in handlers.GetInvocationList())
            { if(!current())return false;try {handler(face.Id,name);}catch(Exception ex){FileHandlerFailed(ex);} }
        return current();
    }

    private void FileHandlerFailed(Exception error)=>_robot.Engine.EmitStackLog("M1-034 face album handler skipped: "+error.Message);
}
