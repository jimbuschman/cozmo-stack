using Cozmo.Transport;
using Cozmo.Robot.Behavior;
using System.Text;
namespace Cozmo.Robot.Vision;

public sealed partial class VisionSystem
{
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
        => InitializeNativeVision(owner,null);

    // Supplied config is an explicitly labeled source-input unit seam; production uses the engine owner.
    internal NativeVisionStageResult InitializeNativeVision(EngineRobot owner,FirmwareJsonValue? suppliedConfig)
    {
        NativeVisionInitialization? state;int epoch;
        lock(_groundGate)
        {
            state=_nativeInitialization;
            if(state is null||!ReferenceEquals(state.Owner,owner)||owner.Lifetime.Destroying)return NativeVisionStageResult.Missing;
            epoch=++_nativeInitializationEpoch;
        }
        bool Current(){lock(_groundGate)return ReferenceEquals(_nativeInitialization,state)&&ReferenceEquals(_groundOwner,owner)&&!owner.Lifetime.Destroying&&epoch==_nativeInitializationEpoch;}
        NativeVisionStageResult Missing(){if(Current())state.MarkUnavailable();return NativeVisionStageResult.Missing;}
        var engine=_robot.Engine;
        if(suppliedConfig is null && (!engine.VisionConfigurationRead || !engine.VisionConfigurationReadSucceeded && engine.VisionConfiguration.Kind!=FirmwareJsonKind.Null))
        {SteppedBehavior.ReportMissing("M11-059 actual vision configuration input unavailable; Init result is unknown");return Missing();}
        var root=suppliedConfig??engine.VisionConfiguration;
        try
        {
            if(!ReadComponentQualityPrefix(root,Current))return Current()?NativeVisionStageResult.Failure(1):Missing();
            if(!Current())return Missing();
            state.EnterSystem();
            if(!QualityProcessor.Settings.Load(root,QualityProcessor.SourceLog!,Current))
            {if(!Current())return Missing();state.Failure(1);return NativeVisionStageResult.Failure(1);}
            if(!Current())return Missing();
            var exposure=LoadExposureParameters(root,Current);
            if(!exposure.Available)return Missing();
            if(exposure.Result!=0){state.Failure(exposure.Result);return exposure;}
            var performance=InitMember(root,"PerformanceLogging");
            var info=InitMember(performance,"TimeBetweenProfilerInfoPrints_sec");
            if(info is null||info.Kind==FirmwareJsonKind.Null){var error=InitError(Current,"VisionSystem.Init.MissingJsonParameter","TimeBetweenProfilerInfoPrints_sec",1);if(error.Available)state.Failure(error.Result);return error;}
            float infoMs=Json.AsFloat(info)*1000f;
            var das=InitMember(performance,"TimeBetweenProfilerDasLogs_sec");
            if(das is null||das.Kind==FirmwareJsonKind.Null){var error=InitError(Current,"VisionSystem.Init.MissingJsonParameter","TimeBetweenProfilerDasLogs_sec",1);if(error.Available)state.Failure(error.Result);return error;}
            float dasMs=Json.AsFloat(das)*1000f;
            if(!float.IsFinite(infoMs)||!float.IsFinite(dasMs)||(double)infoMs>=9223372036854775808d||(double)infoMs< -9223372036854775808d||(double)dasMs>=9223372036854775808d||(double)dasMs< -9223372036854775808d)
            {SteppedBehavior.ReportMissing("M11-059 profiler period exceptional f32-to-i64 conversion unavailable");return Missing();}
            state.SetProfiler((long)infoMs,(long)dasMs);
            string path=engine.Options.ResourcesPath is {} resources?Path.Combine(resources,"config","engine","vision"):string.Empty;
            state.ReplaceFace(path,root);
            SteppedBehavior.ReportMissing("M11-016 FaceTracker internal Init/algorithm and its possible error diagnostics remain unavailable");
            if(!Current())return Missing();
            state.ReplaceMotion(this,root);
            uint pet=state.Pet.Initialize(root);
            if(!Current())return Missing();
            if(pet!=0){var error=InitError(Current,"VisionSystem.Init.PetTrackerInitFailed",state.Pet.FailedStage??"",pet);if(error.Available)state.Failure(error.Result);return error;}
            var modes=InitMember(root,"InitialVisionModes");
            if(modes is null){var error=InitError(Current,"VisionSystem.Init.MissingJsonParameter","InitialVisionModes",1);if(error.Available)state.Failure(error.Result);return error;}
            if(!TryApplyInitialVisionModes(modes,Current))return Missing();
            var schedules=ApplyNativeInitialSchedules(root,Current);
            if(!schedules.Available)return Missing();
            if(schedules.Result!=0){state.Failure(schedules.Result);return schedules;}
            lock(_groundGate)
            {
                if(!Current())return NativeVisionStageResult.Missing;
                PushNativeSchedule(AllVisionModesSchedule.CopyDefault());
                state.Finish();
            }
            return NativeVisionStageResult.Success;
        }
        catch(Exception e) when(e is JsonLogicError or JsonRuntimeError or OutOfMemoryException)
        {
            SteppedBehavior.ReportMissing("M1-034/M11-059 Vision Init terminating input/allocation unavailable; initialization unit skipped");
            return Missing();
        }
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
