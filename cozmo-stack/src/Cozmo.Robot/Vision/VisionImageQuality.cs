using Cozmo.Transport;
using Cozmo.Robot.Behavior;
using System.Text;
namespace Cozmo.Robot.Vision;

public sealed partial class VisionSystem
{
    internal Func<string,uint?>? StockFaceCreateOverrideForTest;
    private int _missingMarkerLibraryReported;
    // fidelity: M11-080, M11-057
    internal readonly ImageQualityProcessor QualityProcessor=new();
    internal Func<IReadOnlyList<ImageQualityRectangle>>? QualityRectangleInput;
    private bool TryCheckFrameQuality(GrayImage image,IReadOnlyList<ImageQualityRectangle> frameRectangles,Func<bool> current,out uint status,out ImageQualityProposal proposal)
    {
        status=0;proposal=default;
        SteppedBehavior.ReportMissing("M11-080 mode-7 Profiler.Tic/Toc CheckingImageQuality 0x006B55F6/0x006B560E remain unbuilt timing producers; no Stopwatch timer substituted");
        if(!current())return false;
        // The optional provider is an explicit test seam; production uses the current Update's own vector.
        var rectangles=QualityProcessor.Settings.MeterFromDetections ? QualityRectangleInput?.Invoke() ?? frameRectangles : Array.Empty<ImageQualityRectangle>();
        if(!current())return false;
        return QualityProcessor.TryCheck(image,rectangles,_robot.CameraSettings.QualityState,ModeEnableMask,out status,out proposal,current);
    }

    private static FirmwareJsonValue? InitMember(FirmwareJsonValue? root,string key)
    {
        if(root is null||root.Kind==FirmwareJsonKind.Null)return null;
        if(root.Kind!=FirmwareJsonKind.Object)throw new JsonLogicError("Json::Value::find requires objectValue");
        return root.Members.GetValueOrDefault(Convert.ToHexString(Encoding.UTF8.GetBytes(key)));
    }
    private NativeVisionStageResult InitError(Func<bool> current,string key,string text,uint result)
    {
        _robot.Engine.EmitSourceLog(EngineLogLevel.Error,null,key,text);
        if(!current())return NativeVisionStageResult.Missing;
        EngineErrorState.StoreAndMaybeBreak();return NativeVisionStageResult.Failure(result);
    }
    private NativeVisionStageResult LoadExposureParameters(FirmwareJsonValue root,Func<bool> current)
    {
        var section=InitMember(root,"ImageQuality");
        FirmwareJsonValue? Get(string key)=>InitMember(section,key);
        var midValue=Get("MidValue");
        if(midValue is null || midValue.Kind==FirmwareJsonKind.Null)return InitError(current,"VisionSystem.Init.MissingJsonParameter","MidValue",1);
        byte mid=unchecked((byte)Json.AsUInt(midValue));
        var changeValue=Get("MaxChangeFraction");
        if(changeValue is null || changeValue.Kind==FirmwareJsonKind.Null)return InitError(current,"VisionSystem.Init.MissingJsonParameter","MaxChangeFraction",1);
        float change=Json.AsFloat(changeValue);
        var subValue=Get("SubSample");
        if(subValue is null || subValue.Kind==FirmwareJsonKind.Null)return InitError(current,"VisionSystem.Init.MissingJsonParameter","SubSample",1);
        int sub=Json.AsInt(subValue);
        uint status=SetAutoExposureParams(sub,mid,QualityProcessor.Settings.Mid,change,current);
        if(!current())return NativeVisionStageResult.Missing;
        return status==0?NativeVisionStageResult.Success:InitError(current,"VisionSystem.Init.SetExposureParametersFailed","",status);
    }

    internal NativeVisionStageResult InitializeNativeVision(EngineRobot owner)
        => InitializeNativeVision(owner,null,false);

    // Supplied config is an explicitly labeled source-input unit seam; production uses the engine owner.
    internal NativeVisionStageResult InitializeNativeVision(EngineRobot owner,FirmwareJsonValue? suppliedConfig)
        => InitializeNativeVision(owner,suppliedConfig,false);

    internal NativeVisionStageResult InitializeNativeVision(EngineRobot owner,FirmwareJsonValue? suppliedConfig,bool deferComponentCompletion)
    {
        var outcome=InitializeNativeVisionCore(owner,suppliedConfig);
        if(outcome.Available && outcome.Result==0 && !deferComponentCompletion && ReferenceEquals(_robot.Engine.Robot,owner))
            CompleteNativeVisionComponent(owner,outcome.Epoch);
        return outcome;
    }

    private NativeVisionStageResult InitializeNativeVisionCore(EngineRobot owner,FirmwareJsonValue? suppliedConfig)
    {
        NativeVisionInitialization? state;int epoch;
        lock(_groundGate)
        {
            state=_nativeInitialization;
            if(state is null||!ReferenceEquals(state.Owner,owner)||owner.Lifetime.Destroying)return NativeVisionStageResult.Missing;
            epoch=++_nativeInitializationEpoch;
            state.ComponentInitialized=false;
            state.ComponentAvailabilityKnown=true;
        }
        bool Current(){lock(_groundGate)return !_disposed && ReferenceEquals(_nativeInitialization,state)&&ReferenceEquals(_groundOwner,owner)&&!owner.Lifetime.Destroying&&epoch==_nativeInitializationEpoch;}
        bool systemEntered=false;int faceEpoch=0;
        NativeVisionStageResult Missing(){lock(_groundGate){if(Current()){if(systemEntered)state.MarkUnavailable();state.ComponentAvailabilityKnown=false;}}return NativeVisionStageResult.Missing;}
        var engine=_robot.Engine;
        NativeVisionStageResult SystemFailure(NativeVisionStageResult result)
        {
            if(!result.Available || result.Result==0)return result;
            engine.EmitSourceLog(EngineLogLevel.Error,null,"VisionComponent.Init.VisionSystemInitFailed","");
            if(!Current())return NativeVisionStageResult.Missing;
            EngineErrorState.StoreAndMaybeBreak();
            return result with { Epoch=epoch };
        }
        if(!Current())return Missing();
        if(!Detector.Decoder.HasLibrary && Interlocked.CompareExchange(ref _missingMarkerLibraryReported,1,0)==0)
        {
            engine.EmitStackLog("MISSING: M11-001 marker library is unavailable; extract it from libcozmoEngine.so with re-analysis/tools/extract_marker_library.py and rebuild, or set COZMO_MARKER_LIBRARY to the extracted file. Marker decoding is unavailable.");
            if(!Current())return Missing();
        }
        if(suppliedConfig is null && (!engine.VisionConfigurationRead || !engine.VisionConfigurationReadSucceeded && engine.VisionConfiguration.Kind!=FirmwareJsonKind.Null))
        {SteppedBehavior.ReportMissing("M11-059 actual vision configuration input unavailable; Init result is unknown");return Missing();}
        var root=suppliedConfig??engine.VisionConfiguration;
        try
        {
            if(!ReadComponentQualityPrefix(root,Current))return Current()?NativeVisionStageResult.Failure(1):Missing();
            if(!Current())return Missing();
            lock(_groundGate)
            {
                if(!Current())return Missing();
                state.EnterSystem();systemEntered=true;faceEpoch=++_nativeFaceEpoch;
            }
            if(!QualityProcessor.Settings.Load(root,QualityProcessor.SourceLog!,Current))
            {if(!Current())return Missing();state.Failure(1);return SystemFailure(NativeVisionStageResult.Failure(1));}
            if(!Current())return Missing();
            var exposure=LoadExposureParameters(root,Current);
            if(!exposure.Available)return Missing();
            if(exposure.Result!=0){state.Failure(exposure.Result);return SystemFailure(exposure);}
            var performance=InitMember(root,"PerformanceLogging");
            var info=InitMember(performance,"TimeBetweenProfilerInfoPrints_sec");
            if(info is null||info.Kind==FirmwareJsonKind.Null){var error=InitError(Current,"VisionSystem.Init.MissingJsonParameter","TimeBetweenProfilerInfoPrints_sec",1);if(error.Available)state.Failure(error.Result);return SystemFailure(error);}
            float infoMs=Json.AsFloat(info)*1000f;
            var das=InitMember(performance,"TimeBetweenProfilerDasLogs_sec");
            if(das is null||das.Kind==FirmwareJsonKind.Null){var error=InitError(Current,"VisionSystem.Init.MissingJsonParameter","TimeBetweenProfilerDasLogs_sec",1);if(error.Available)state.Failure(error.Result);return SystemFailure(error);}
            float dasMs=Json.AsFloat(das)*1000f;
            if(!float.IsFinite(infoMs)||!float.IsFinite(dasMs)||(double)infoMs>=9223372036854775808d||(double)infoMs< -9223372036854775808d||(double)dasMs>=9223372036854775808d||(double)dasMs< -9223372036854775808d)
            {SteppedBehavior.ReportMissing("M11-059 profiler period exceptional f32-to-i64 conversion unavailable");return Missing();}
            state.SetProfiler((long)infoMs,(long)dasMs);
            string path=engine.Options.ResourcesPath is {} resources?Path.Combine(resources,"config","engine","vision"):string.Empty;
            state.ReplaceFace(path,root);
            if(suppliedConfig is null && _stockRecognizer && ReferenceEquals(FaceDetector,_stockFaceDetector))
                InitializeStockFace(state,root,path,faceEpoch,Current);
            if(!Current())return Missing();
            state.ReplaceMotion(this,root);
            uint pet;
            string? petLibraries = suppliedConfig is null ? Emulation.ShippedLibraries.FindLibDir() : null;
            if (petLibraries is null)
            {
                RetireStockPet(state,Current);
                if (suppliedConfig is null) SteppedBehavior.ReportMissing("M11-016/M11-080 shipped pet guest libraries unavailable; stock pet detector withheld");
                pet=state.Pet.Initialize(root);
            }
            else
            {
                pet=InitializeStockPet(state,root,petLibraries,Current);
            }
            if(!Current())return Missing();
            if(pet!=0){var error=InitError(Current,"VisionSystem.Init.PetTrackerInitFailed",state.Pet.FailedStage??"",pet);if(error.Available)state.Failure(error.Result);return SystemFailure(error);}
            var modes=InitMember(root,"InitialVisionModes");
            if(modes is null){var error=InitError(Current,"VisionSystem.Init.MissingJsonParameter","InitialVisionModes",1);if(error.Available)state.Failure(error.Result);return SystemFailure(error);}
            if(!TryApplyInitialVisionModes(modes,Current))return Missing();
            var schedules=ApplyNativeInitialSchedules(root,Current);
            if(!schedules.Available)return Missing();
            if(schedules.Result!=0){state.Failure(schedules.Result);return SystemFailure(schedules);}
            lock(_groundGate)
            {
                if(!Current())return NativeVisionStageResult.Missing;
                PushNativeSchedule(AllVisionModesSchedule.CopyDefault());
                if(suppliedConfig is not null && state.StockFace is not null)state.StockFaceEpoch=faceEpoch;
                state.Finish();
            }
            return NativeVisionStageResult.Success with { Epoch=epoch };
        }
        catch(Exception e) when(e is JsonLogicError or JsonRuntimeError or OutOfMemoryException)
        {
            SteppedBehavior.ReportMissing("M1-034/M11-059 Vision Init terminating input/allocation unavailable; initialization unit skipped");
            return Missing();
        }
    }

    private void InitializeStockFace(NativeVisionInitialization state,FirmwareJsonValue root,string path,int epoch,Func<bool> current)
    {
        var originalStock=_stockFaceDetector;
        var detector=CreateStockFaceDetector();
        var recognizer=CreateFaceRecognizer(new NativeOkaoFaceRecognizer(detector));
        bool adopted=false;
        try
        {
            _robot.Engine.EmitSourceLog(EngineLogLevel.Info,"Unnamed","VisionSystem.Init.InstantiatingFaceTracker",$"With model path {path}.");
            if(!current())return;
            var face=InitMember(root,"FaceDetection");var recognition=InitMember(root,"FaceRecognition");
            var mode=InitMember(face,"DetectionMode");var run=InitMember(recognition,"RunMode");
            if(face?.Kind!=FirmwareJsonKind.Object || face.Members.Count!=1 ||
               recognition?.Kind!=FirmwareJsonKind.Object || recognition.Members.Count!=1 ||
               mode?.Kind!=FirmwareJsonKind.String || !mode.Bytes.AsSpan().SequenceEqual("video"u8) ||
               run?.Kind!=FirmwareJsonKind.String || !run.Bytes.AsSpan().SequenceEqual("asynchronous"u8))
            {
                detector.SetStockStatus(3);
                SteppedBehavior.ReportMissing("M11-016 nonstock face detector or recognition configuration unavailable");
            }
            else
            {
                try
                {
                    detector.CreateStagedStock(StockFaceCreateOverrideForTest);
                    var version=detector.UseSession(machine=>machine.StockVersion());
                    _robot.Engine.EmitSourceLog(EngineLogLevel.Info,"Unnamed","FaceTrackerImpl.Init.FaceLibVersion",$"Initializing with FaceLibVision version {version.Major}.{version.Minor}");
                    if(!current())return;
                    detector.UseSession(machine=>{machine.InitializeStockHandles();return 0;});
                    recognizer.InitializeStock();
                    detector.SetStockStatus(1);
                    _robot.Engine.EmitSourceLog(EngineLogLevel.Info,"Unnamed","FaceTrackerImpl.Init.Success","FaceLib Vision handles created successfully.");
                }
                catch(Emulation.FaceGuestFailure failure)
                {
                    detector.SetStockStatus(2);
                    if(failure.Key.StartsWith("MISSING ",StringComparison.Ordinal))SteppedBehavior.ReportMissing(failure.Key);
                    else _robot.Engine.EmitSourceLog(EngineLogLevel.Error,null,failure.Key,"");
                    if(!current())return;
                    EngineErrorState.StoreAndMaybeBreak();
                    _robot.Engine.EmitSourceLog(EngineLogLevel.Error,null,"FaceTrackerImpl.Constructor.InitFailed","");
                    if(!current())return;
                    EngineErrorState.StoreAndMaybeBreak();
                }
                catch(Exception e) when(e is not OutOfMemoryException)
                {
                    detector.SetStockStatus(3);
                    SteppedBehavior.ReportMissing($"M11-016 stock face guest unavailable: {e.Message}");
                }
            }
            if(!current())return;
            OkaoFaceDetector oldDetector;FaceRecognizer oldRecognizer;
            lock(_groundGate)
            {
                if(!current() || !ReferenceEquals(_stockFaceDetector,originalStock) || !ReferenceEquals(FaceDetector,originalStock))return;
                oldDetector=_stockFaceDetector;oldRecognizer=Recognizer;
                _stockFaceDetector=detector;FaceDetector=detector;Recognizer=recognizer;
                state.StockFace=detector;state.StockFaceRecognizer=recognizer;state.StockFaceEpoch=epoch;adopted=true;
            }
            _=Task.Run(()=>RetireFaceSession(oldDetector,oldRecognizer));
            _robot.Engine.EmitSourceLog(EngineLogLevel.Info,"Unnamed","VisionSystem.Init.DoneInstantiatingFaceTracker","");
        }
        finally { if(!adopted) _=Task.Run(()=>RetireFaceSession(detector,recognizer)); }
    }

    private void RetireFaceSession(OkaoFaceDetector detector,FaceRecognizer recognizer)
    {
        bool albumFailed=false,shutdownSkipped=false;
        try
        {
            lock(_busy)
            {
                try
                {
                    detector.RetireRecognition();
                    if(detector.NativeUsable)albumFailed=recognizer.ShutdownStockDeferred();
                    else shutdownSkipped=true;
                }
                finally { detector.Dispose(); }
            }
        }
        catch(Exception e) { SteppedBehavior.ReportMissing($"M11-055 face guest retirement failed: {e.Message}"); }
        if(shutdownSkipped)SteppedBehavior.ReportMissing("M1-034 M14-011 faulted recognition owner skipped guest shutdown");
        if(albumFailed)
        {
            _robot.Engine.EmitSourceLog(EngineLogLevel.Error,null,"FaceRecognizer.Shutdown.FaceLibAlbumHandleDeleteFail","");
            EngineErrorState.StoreAndMaybeBreak();
        }
        ReportFaceCleanup(detector);
    }

    private void ReportFaceCleanup(OkaoFaceDetector detector)
    {
        foreach(string operation in detector.TakeCleanupFailures())
        {
            _robot.Engine.EmitSourceLog(EngineLogLevel.Error,null,operation,"");
            EngineErrorState.StoreAndMaybeBreak();
        }
    }

    internal bool NativeVisionAttemptCurrent(EngineRobot owner,int epoch)
    {
        lock(_groundGate) return !_disposed && _nativeInitialization is { } state &&
            ReferenceEquals(state.Owner,owner) && ReferenceEquals(_groundOwner,owner) &&
            !owner.Lifetime.Destroying && _nativeInitializationEpoch==epoch;
    }

    internal bool NativeVisionOwnerCurrent(EngineRobot owner)
    {
        lock(_groundGate) return !_disposed && ReferenceEquals(_groundOwner,owner) &&
            ReferenceEquals(_nativeInitialization?.Owner,owner) && !owner.Lifetime.Destroying;
    }

    internal bool CompleteNativeVisionComponent(EngineRobot owner,int epoch)
    {
        lock(_groundGate)
        {
            if(!NativeVisionAttemptCurrent(owner,epoch) || !_nativeInitialization!.Initialized)return false;
            _nativeInitialization!.ComponentInitialized=true;
            _nativeInitialization.ComponentAvailabilityKnown=true;
            return true;
        }
    }

    private void RetireStockPet(NativeVisionInitialization state,Func<bool> current)
    {
        OkaoPetDetector? prior;
        lock (_groundGate)
        {
            if (!current()) return;
            prior=state.StockPet;
            state.StockPet=null;
        }
        prior?.Retire();
    }

    private uint InitializeStockPet(NativeVisionInitialization state,FirmwareJsonValue root,string libraries,Func<bool> current)
    {
        Emulation.OkaoPetDetection? guest=null;
        var petInitialization=new NativeVisionPetInitialization();
        RetireStockPet(state,current);
        if(!current())return 1;
        bool Adopt(bool ready)
        {
            var detector=new OkaoPetDetector(guest!);
            detector.Warning=(key,text)=>{if(current())_robot.Engine.EmitSourceLog(EngineLogLevel.Warning,null,key,text);};
            detector.Error=key=>{_robot.Engine.EmitSourceLog(EngineLogLevel.Error,null,key,"");Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();};
            if(ready)detector.MarkReady();
            OkaoPetDetector? previous;
            lock(_groundGate)
            {
                if(!current())return false;
                previous=state.StockPet;
                state.StockPet=detector;
                if(ready)petInitialization.MarkNativeInitialized(guest!.VersionMajor,guest.VersionMinor);
                state.Pet=petInitialization;
                guest=null;
            }
            previous?.Retire();
            return true;
        }
        try
        {
            guest=new Emulation.OkaoPetDetection(libraries);
            _robot.Engine.EmitSourceLog(EngineLogLevel.Info,"Unnamed","PetTracker.OmcvVersion",
                $"Initializing with FaceLibVision version {guest.VersionMajor}.{guest.VersionMinor}");
            if(!current()){guest.Dispose();return 1;}
            uint parsed=petInitialization.ReadParameters(root);
            if(parsed!=0)
            {
                string stage=petInitialization.FailedStage??"";
                string key=stage=="PetTracker"?"PetTracker.Init.MissingConfigGroup":"PetTracker.Init.MissingParameter";
                string text=stage=="PetTracker"?"Expecting 'PetTracker'":$"PetTracker.{stage}";
                _robot.Engine.EmitSourceLog(EngineLogLevel.Error,null,key,text);
                lock(_groundGate) { if(current())state.Pet=petInitialization; }
                if(current())Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();
                guest.Dispose();return parsed;
            }
            guest.Initialize(petInitialization.Parameters);
            if(!Adopt(true)){guest?.Dispose();return 1;}
            return 0;
        }
        catch(Emulation.PetGuestFailure failure)
        {
            _robot.Engine.EmitSourceLog(EngineLogLevel.Error,null,failure.Key,failure.Message);
            if(current())Cozmo.Transport.EngineErrorState.StoreAndMaybeBreak();
            // A partially initialized pair stays attached to this owner until its normal retirement.
            if(guest is not null)Adopt(false);
            guest?.Dispose();
            return failure.Result;
        }
        catch(Exception e) when(e is not JsonLogicError and not JsonRuntimeError and not OutOfMemoryException)
        {
            guest?.Dispose();
            RetireStockPet(state,current);
            SteppedBehavior.ReportMissing($"M11-016/M11-080 stock pet guest unavailable: {e.Message}");
            if(!current())return 1;
            uint result=petInitialization.Initialize(root); // unrelated vision continues with the existing host-only Init result
            lock(_groundGate) { if(current())state.Pet=petInitialization; }
            return result;
        }
        finally { guest?.Dispose(); }
    }
    // ImageCache admission is evaluated for the owner captured at frame handoff.
    private bool NativeDecodedAdmission(EngineRobot? owner,int removal)
    {
        bool known, initialized;
        lock(_groundGate)
        {
            if(owner is null || owner.Lifetime.Destroying || !ReferenceEquals(_groundOwner,owner) ||
               !ReferenceEquals(_robot.Engine.Robot,owner) || removal!=Volatile.Read(ref _removals) ||
               _nativeInitialization is null || !ReferenceEquals(_nativeInitialization.Owner,owner))
            { known=false; initialized=false; }
            else { known=_nativeInitialization.AvailabilityKnown; initialized=_nativeInitialization.Initialized; }
        }
        if(!known)
        {
            SteppedBehavior.ReportMissing("M11-059 decoded frame admission: original worker initialization is unavailable");
            return false;
        }
        if(!initialized)
        {
            _robot.Engine.EmitSourceLog(EngineLogLevel.Error,null,"VisionSystem.Update.NotInitialized","Vision system is not initialized");
            return false;
        }
        return true;
    }}
