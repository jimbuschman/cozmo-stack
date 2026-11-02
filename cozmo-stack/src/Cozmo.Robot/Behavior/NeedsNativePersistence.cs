using Cozmo.Transport;
using System.Text;

namespace Cozmo.Robot.Behavior;

public sealed partial class NeedsManager
{
    // Constructor +3EC begins as "Unknown (unknown)". The normal unsuffixed
    // initializer has no tuning update (AttemptActivateTuningExperiment is BX LR).
    // The distinct runtime setter/caller remains a named M15-014 frontier.
    private byte[]? _nativeReportVariation=NativeNeedsStateReportCodec.InitialVariation.ToArray();
    private bool TryGetNativeReportVariation(out byte[] variation)
    {
        if(_nativeReportVariation is not {} current){variation=Array.Empty<byte>();return false;}
        variation=current.ToArray();
        return true;
    }
    private long _nativeDeviceTimestampMicros;
    private bool _nativeDeviceInputUnavailable;
    // 0x6998B4: read actual source keys into a complete candidate before replacing the current owner.
    private bool LoadNativeDevice(string path,bool applyElapsed)
    {
        try
        {
            if(!Json.TryParseFirst(File.ReadAllBytes(path),out var doc))return false;
            using(doc)
            {
                var root=doc.RootElement;
                int version=Json.AsInt(Json.Member(root,"_StateFileVersion"));if(version>=6)return false;
                long Seconds(string key)=>unchecked(NativeJsonInt64(Json.Member(root,key))*1_000_000L);
                var candidate=new NeedsState(Config);candidate.CopyNativeFrom(State);
                candidate.NativeDateTimeMicros=Seconds("_DateTime");
                candidate.NativeLastDisconnectMicros=version>=3?Seconds("TimeLastDisconnect"):0;
                candidate.NativeLastBackgroundMicros=version>=3?Seconds("TimeLastAppBackgrounded"):0;
                candidate.NativeOpenAppAfterDisconnect=version>=3?Json.AsInt(Json.Member(root,"OpenAppAfterDisconnect")):0;
                candidate.NativeSerial=Json.AsUInt(Json.Member(root,"_SerialNumber"));
                candidate.NativeUnlockLevel=Json.AsInt(Json.Member(root,"CurNeedsUnlockLevel"));
                candidate.NativeStarsAwarded=Json.AsInt(Json.Member(root,"NumStarsAwarded"));
                candidate.NativeStarsForNextUnlock=Json.AsInt(Json.Member(root,"NumStarsForNextUnlock"));
                var levels=Json.Member(root,"CurNeedLevel");
                foreach(int id in State.NativeLevels.Keys.OrderBy(id=>id).ToArray())
                {
                    if((uint)id>2){_nativeDeviceInputUnavailable=true;SteppedBehavior.ReportMissing("M15-014 NeedsId name outside recovered device input");return false;}
                    candidate.StoreLevel((NeedId)id,(float)Json.AsInt(Json.Member(levels,((NeedId)id).ToString()))/100000f);
                }
                var parts=Json.Member(root,"PartIsDamaged");
                foreach(int id in State.PartDamage.Keys.OrderBy(id=>id).ToArray())
                {
                    if((uint)id>2){_nativeDeviceInputUnavailable=true;SteppedBehavior.ReportMissing("M15-014 repairable-part name outside recovered device input");return false;}
                    string name=id switch{0=>"Head",1=>"Lift",_=>"Treads"};candidate.SetNativePartDamage(id,NativeJsonBool(Json.Member(parts,name))?(byte)1:(byte)0);
                }
                candidate.NativeLastStarMicros=version>=2?Seconds("TimeLastStarAwarded"):0;
                candidate.ForceNextSong=version>=4?unchecked((int)NativeUnlockNames.FromBytes(Json.AsStringBytes(Json.Member(root,"ForceNextSong")))):0;
                candidate.NativeTimeCreatedMicros=version>=5?Seconds("TimeCreated"):0;
                candidate.UpdateCurNeedsBrackets();State.CopyNativeFrom(candidate);
                _serial=State.NativeSerial;_lastDisconnectSec=State.NativeLastDisconnectMicros/1_000_000.0;_lastAppBackgroundSec=State.NativeLastBackgroundMicros/1_000_000.0;
                _openAppAfterDisconnect=State.NativeOpenAppAfterDisconnect;_nativeDeviceTimestampMicros=State.NativeDateTimeMicros;
                _stateDateTimeSec=State.NativeDateTimeMicros/1_000_000.0;_deviceTimestampSnapshotSec=_stateDateTimeSec;
                _deviceVersionUpdated=version<5;_deviceDataPresent=true;_deviceCopy=SnapshotState(State.NativeDateTimeMicros/1_000_000);
                if(applyElapsed)ApplyNativeElapsed(false);
                return true;
            }
        }
        catch(Exception e) when(e is JsonLogicError or JsonRuntimeError)
        {SteppedBehavior.ReportMissing("M1-034/M15-014 skipped terminating JsonCpp Needs device file unit; prior state retained: "+e.Message);return false;}
        catch(Exception e) when(e is FileNotFoundException or DirectoryNotFoundException)
        {return false;}
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or JsonMissingSource)
        {_nativeDeviceInputUnavailable=true;SteppedBehavior.ReportMissing("M15-014 Needs device file input unavailable; prior state retained: "+e.Message);return false;}
    }
    private static long NativeJsonInt64(FirmwareJsonValue value)=>value.Kind switch
    {
        FirmwareJsonKind.Null=>0,FirmwareJsonKind.Boolean=>value.Boolean?1:0,FirmwareJsonKind.Int=>value.Signed,
        FirmwareJsonKind.UInt when value.Unsigned<=long.MaxValue=>(long)value.Unsigned,
        _=>throw new JsonMissingSource("asLargestInt conversion outside bounded Needs file input")
    };
    private static bool NativeJsonBool(FirmwareJsonValue value)=>value.Kind switch
    {
        FirmwareJsonKind.Null=>false,FirmwareJsonKind.Boolean=>value.Boolean,FirmwareJsonKind.Int=>value.Signed!=0,
        FirmwareJsonKind.UInt=>value.Unsigned!=0,FirmwareJsonKind.Real=>value.Real!=0,
        _=>throw new JsonLogicError("Value is not convertible to bool")
    };
    private bool WriteNativeDevice(bool refresh)
    {
        long now=NativeSystemClockMicros(); // 0x693BC0, even when refresh=false
        if(_nativeSerialUnresolved)return true; // the State carries this robot's serial but not yet its data; see ResolveNativeCopies
        if(refresh)State.NativeDateTimeMicros=now;
        if(DeviceDirectory is null){SteppedBehavior.ReportMissing("M15-014 NeedsDeviceDirectory unavailable for device write");return false;}
        if(!NativeMetadataAvailable){SteppedBehavior.ReportMissing("M15-014 star config unavailable for device write");return false;}
        string? song=NativeUnlockNames.Lookup(State.ForceNextSong);
        if(song is null){SteppedBehavior.ReportMissing("M15-014 ForceNextSong enum name unavailable for device write");return false;}
        var levels=new Dictionary<string,int>();
        foreach(var p in State.NativeLevels.OrderBy(entry=>entry.Key))
        {
            if((uint)p.Key>2){SteppedBehavior.ReportMissing("M15-014 NeedsId name outside recovered device output");return false;}
            levels[((NeedId)p.Key).ToString()]=QuantizeLevel(p.Value);
        }
        var parts=new Dictionary<string,bool>();
        foreach(var p in State.PartDamage.OrderBy(entry=>entry.Key))
        {
            if((uint)p.Key>2){SteppedBehavior.ReportMissing("M15-014 repairable-part name outside recovered device output");return false;}
            parts[p.Key switch{0=>"Head",1=>"Lift",_=>"Treads"}]=p.Value!=0;
        }
        var doc=new Dictionary<string,object>
        {
            ["_StateFileVersion"]=5,["_DateTime"]=State.NativeDateTimeMicros/1_000_000L,["TimeCreated"]=State.NativeTimeCreatedMicros/1_000_000L,
            ["TimeLastDisconnect"]=State.NativeLastDisconnectMicros/1_000_000L,["TimeLastAppBackgrounded"]=State.NativeLastBackgroundMicros/1_000_000L,
            ["OpenAppAfterDisconnect"]=State.NativeOpenAppAfterDisconnect,["_SerialNumber"]=State.NativeSerial,
            ["CurNeedsUnlockLevel"]=State.NativeUnlockLevel,["NumStarsAwarded"]=State.NativeStarsAwarded,["NumStarsForNextUnlock"]=State.NativeStarsForNextUnlock,
            ["CurNeedLevel"]=levels,["PartIsDamaged"]=parts,["TimeLastStarAwarded"]=State.NativeLastStarMicros/1_000_000L,["ForceNextSong"]=song
        };
        try{File.WriteAllText(Path.Combine(DeviceDirectory,FixedFileName),System.Text.Json.JsonSerializer.Serialize(doc));}
        catch(Exception e) when(e is IOException or UnauthorizedAccessException)
        {SourceLogDispatch.GetSink(SourceLog,Log)?.Invoke(EngineLogLevel.Error,null,"NeedsManager.WriteToDevice","Write failed: "+e.Message);EngineErrorState.StoreAndMaybeBreak();}
        return true;
    }
    private void ProtectRobotData()
    {
        _nativeRobotDataProtected=true;
        State.NativeSerial=_previousSerial; // the State is not this robot's, so the serial must not persist or pass to a reconnect
        SourceLogDispatch.GetSink(SourceLog,Log)?.Invoke(EngineLogLevel.Error,null,"NeedsManager.RobotDataProtected","The robot's needs could not be read; no needs are saved to the robot or this PC until a read works");
    }
    // 0x694608: the original context and source-selected robot stay live through every outward callback.
    private void ResolveNativeCopies()
    {
        var engine=HostEngine;
        if(engine is null||!engine.NeedsContextAlive||!ReferenceEquals(engine.Needs,this)){SteppedBehavior.ReportMissing("M15-014 original Needs/context owner unavailable at resolver");return;}
        bool Current()=>engine.NeedsContextAlive&&ReferenceEquals(engine.Needs,this);
        if(PossiblyWriteLabAssignments is {} lab)lab();
        else {engine.NativeLocalNotifications?.MarkHistoryUnavailable();SteppedBehavior.ReportMissing("M15-050 Lab writeback unavailable at Needs resolver 0x00694622");return;}
        if(!Current())return;
        if(!engine.Experiments.NativeAssignmentHistoryAvailable){engine.NativeLocalNotifications?.MarkHistoryUnavailable();SteppedBehavior.ReportMissing("M15-050/M15-014 Lab precursor history unavailable; Needs resolver continuation withheld");return;}
        if(_nativeDeviceInputUnavailable){engine.NativeLocalNotifications?.MarkHistoryUnavailable();SteppedBehavior.ReportMissing("M15-014 Needs device input history unavailable; resolver continuation withheld");return;}
        _nativeRobotDataProtected=false;
        bool r=_robotReadSucceeded,d=_deviceDataPresent,rewrite=_robotRewriteNeeded;
        bool deviceWrite=false,robotWrite=false,changed=false,apply=false,clearDisconnect=false,clearBackground=false,noAction=false;
        if(!r&&!d)
        {
            noAction=true;
            // Approved departure (operator, 2026-11-02): a robot read that failed for any reason but NV_NOT_FOUND says nothing
            // about what the robot holds, so neither copy is written. The engine writes both (0x00694820, 0x00694EE4). A device
            // file written here would look newer than the robot on the next run and overwrite it anyway.
            if(_nativeRobotReadInconclusive)ProtectRobotData();
            else {deviceWrite=true;robotWrite=true;}
        }
        else if(!r&&d)
        {
            if(_previousSerial!=_serial)
            {
                bool alternate=TryLoadAlternateDeviceFile(_serial);if(!Current())return;if(_nativeDeviceInputUnavailable){engine.NativeLocalNotifications?.MarkHistoryUnavailable();return;}
                // The startup file holds defaults under no serial, so without this robot's own file the device copy says nothing
                // about it either: same approved departure as below.
                if(_nativeRobotReadInconclusive&&!alternate)ProtectRobotData();
                else {deviceWrite=alternate;robotWrite=true;}
                changed=true;
            }
            else robotWrite=true;
        }
        else if(r&&!d){deviceWrite=true;robotWrite=rewrite;changed=true;apply=true;clearDisconnect=true;}
        else if(_previousSerial!=_nativeRobotCandidate!.NativeSerial)
        {deviceWrite=true;robotWrite=rewrite;changed=true;apply=true;clearDisconnect=true;clearBackground=true;}
        else if(_nativeRobotCandidate!.NativeDateTimeMicros>_nativeDeviceTimestampMicros)
        {deviceWrite=true;robotWrite=rewrite;changed=true;apply=true;}
        else if(_nativeDeviceTimestampMicros>_nativeRobotCandidate.NativeDateTimeMicros)robotWrite=true;
        else robotWrite=rewrite;
        if(clearDisconnect)State.NativeLastDisconnectMicros=0;
        if(clearBackground)State.NativeLastBackgroundMicros=0;
        if(changed)
        {
            if(!engine.NeedsContextExternalInterfaceAvailable)SteppedBehavior.ReportMissing("M1-034/M15-014 skipped null RobotChangedFromLastSession interface dereference at 0x00694AAE");
            else engine.DeliverNurture(0x6B,Array.Empty<byte>(),null,RobotChangedFromLastSession);
            if(!Current())return;
        }
        if(apply)
        {
            long disconnect=State.NativeLastDisconnectMicros,background=State.NativeLastBackgroundMicros;
            State.CopyNativeFrom(_nativeRobotCandidate!);
            State.NativeLastDisconnectMicros=disconnect;State.NativeLastBackgroundMicros=background;
            ApplyNativeElapsed(false);if(!Current())return;
            SendNeedsStateToGame(NeedsActionId.Decay);if(!Current())return;
        }
        else if(noAction){SendNeedsStateToGame(NeedsActionId.NoAction);if(!Current())return;}
        SourceLogDispatch.GetSink(SourceLog,Log)?.Invoke(EngineLogLevel.Info,"NeedsSystem","NeedsManager.SendNeedsOnboardingToGame",$"Stage {_nativeOnboardingStage}");
        if(!Current())return;
        int stage=_nativeOnboardingStage;
        if(!engine.NeedsContextExternalInterfaceAvailable)SteppedBehavior.ReportMissing("M1-034/M15-014 skipped null onboarding interface dereference at 0x006953DC");
        else {byte[] body=new byte[4];System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(body,stage);engine.DeliverNurture(0x69,body,stage);}
        if(!Current())return;
        _nativeCopiesResolved=!_nativeRobotDataProtected; // every native robot write is reached only after this resolver (0x00694608)
        _nativeSerialUnresolved=_nativeRobotDataProtected;
        if(State.NativeLastDisconnectMicros!=0)_=NativeSystemClockMicros(); // source elapsed diagnostic samples separately
        long writeTime=NativeSystemClockMicros(); // 0x694D9E, always once for both requested writes
        if(deviceWrite)
        {
            State.NativeDateTimeMicros=writeTime;if(!WriteNativeDevice(false)){engine.NativeLocalNotifications?.MarkHistoryUnavailable();return;}if(!Current())return;
            _deviceDataPresent=true;_deviceVersionUpdated=false;
        }
        if(robotWrite){_robotRewriteNeeded=false;if(!StartNativeRobotWrite(writeTime)){engine.NativeLocalNotifications?.MarkHistoryUnavailable();return;}if(!Current())return;}
        if(TryLocalNotificationsGenerate is {} nativeNotifications){if(!nativeNotifications())return;}
        else if(LocalNotificationsGenerate is {} notifications)notifications();
        else {engine.NativeLocalNotifications?.MarkHistoryUnavailable();SteppedBehavior.ReportMissing("M15-014/M15-093 LocalNotifications.Generate recipient unavailable at 0x00694EEC");}
    }
    // 0x69449C: only the original connected robot's queue can deliver this callback.
    private int? StartNativeRobotRead(uint tag)
    {
        var engine=HostEngine;var owner=_nativeConnectedRobot;
        // The managed NV facade addresses Engine.Robot; it cannot represent another first robot.
        if(!_robotConnected||engine is null||owner is null||owner.Lifetime.Destroying||!ReferenceEquals(engine.Robot,owner)||NvStorage is not {} nv)
        {SteppedBehavior.ReportMissing("M15-014 original Needs robot/NV owner unavailable at 0x0069449C");return null;}
        int accepted=nv.Read(tag,result=>
        {
            if(!engine.NeedsContextAlive||owner.Lifetime.Destroying||!ReferenceEquals(_nativeConnectedRobot,owner))return;
            OnRobotRead(result);
        });
        if(accepted==0&&engine.NeedsContextAlive&&!owner.Lifetime.Destroying&&ReferenceEquals(_nativeConnectedRobot,owner))
        {SourceLogDispatch.GetSink(SourceLog,Log)?.Invoke(EngineLogLevel.Error,null,"NeedsManager.StartReadFromRobot","Read failed");_awaitingRobotData=false;}
        return accepted;
    }
    // 0x695494: caller supplies the already sampled system time; the second sample is retained by the callback but not observed by FinishWrite.
    private bool StartNativeRobotWrite(long writeMicros)
    {
        var engine=HostEngine;
        if(engine is null)return false;
        if(!_robotConnected||_nativeConnectedRobot is null)return true;
        if(_nativeConnectedRobot.Lifetime.Destroying)return false;
        if(!NativeRobotWriteAllowed())return true;
        if(_awaitingRobotData){SourceLogDispatch.GetSink(SourceLog,Log)?.Invoke(EngineLogLevel.Warning,null,"NeedsManager.StartWriteToRobot","Aborting writing needs state to robot, because we are reading needs state from robot");return true;}
        _nativeLastRobotWriteMicros=writeMicros;
        SourceLogDispatch.GetSink(SourceLog,Log)?.Invoke(EngineLogLevel.Info,"NeedsSystem","NeedsManager.StartWriteToRobot","");
        if(!engine.NeedsContextAlive||!ReferenceEquals(engine.Needs,this))return false;
        _=NativeSystemClockMicros(); // source callback capture; selected completion does not read it
        byte[]? body=BuildNativeRobotBlob(writeMicros);if(body is null)return false;
        var owner=_nativeConnectedRobot; // 0x695692: reload Needs+4 after log and payload construction
        if(!engine.NeedsContextAlive||owner is null||owner.Lifetime.Destroying||!ReferenceEquals(engine.Robot,owner)||NvStorage is not {} nv)
        {SteppedBehavior.ReportMissing("M15-014 reloaded Needs robot/NV owner unavailable at 0x006956A2");return false;}
        int accepted=nv.Write(NeedsNvKey,body,result=>
        {
            if(!engine.NeedsContextAlive||owner.Lifetime.Destroying||!ReferenceEquals(_nativeConnectedRobot,owner))return;
            if(result.Result>=0)return;
            SourceLogDispatch.GetSink(SourceLog,Log)?.Invoke(EngineLogLevel.Error,null,"NeedsManager.FinishWriteToRobot.WriteFailed","Write failed with "+NvStorageComponent.NvResultName(result.Result));
            if(engine.NeedsContextAlive&&!owner.Lifetime.Destroying&&ReferenceEquals(_nativeConnectedRobot,owner))EngineErrorState.StoreAndMaybeBreak();
        },false);
        if(accepted==0&&engine.NeedsContextAlive&&!owner.Lifetime.Destroying&&ReferenceEquals(_nativeConnectedRobot,owner))
        {SourceLogDispatch.GetSink(SourceLog,Log)?.Invoke(EngineLogLevel.Error,null,"NeedsManager.StartWriteToRobot.WriteFailed","Write failed");if(engine.NeedsContextAlive&&!owner.Lifetime.Destroying&&ReferenceEquals(_nativeConnectedRobot,owner))EngineErrorState.StoreAndMaybeBreak();}
        return engine.NeedsContextAlive&&ReferenceEquals(engine.Needs,this);
    }
    // Native never writes before InitAfterReadFromRobotAttempt 0x00694608 has run (callers 0x00696536, 0x00696BE2, 0x00698602).
    // State that never took the robot's copy must not replace what the robot has stored.
    private bool NativeRobotWriteAllowed()
    {
        if(_nativeCopiesResolved)return true;
        SteppedBehavior.ReportMissing("M15-014 robot needs write skipped: the robot/device copies were not resolved on this connection (0x00694608 not completed); NV 0x194000 left as stored");return false;
    }
    private bool PossiblyStartNativeRobotWrite(bool force)
    {
        var owner=_nativeConnectedRobot;if(!_robotConnected||owner is null)return true;if(owner.Lifetime.Destroying)return false;
        long now=NativeSystemClockMicros();
        if(unchecked(now-_nativeLastRobotWriteMicros)>600_999_999L||force)return StartNativeRobotWrite(now);
        return true;
    }
    private void ApplyNativeElapsed(bool connected)
    {
        long difference=unchecked(NativeSystemClockMicros()-State.NativeDateTimeMicros);
        float elapsed=(float)(difference/1_000_000L);
        foreach(var need in new[]{NeedId.Repair,NeedId.Energy,NeedId.Play})
        {
            _lastDecaySec[need]=_nowSec-elapsed;
            if(_fullnessDeadlineSec.TryGetValue(need,out var deadline)&&deadline!=0)
            {
                _fullnessDeadlineSec[need]=(float)deadline-elapsed;
                if(_fullnessStartSec.TryGetValue(need,out var start))_fullnessStartSec[need]=(float)start-elapsed;
            }
        }
        ApplyDecayAllNeeds(connected);
    }
}
