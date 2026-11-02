using Cozmo.Transport;
using Cozmo.Robot.Behavior;
using System.Text;

namespace Cozmo.Robot.Vision;

// fidelity: M11-059
// Finite PetTracker::Init result producer only; detector/classifier remains unavailable.
internal sealed class NativeVisionPetInitialization
{
    internal sealed class OwnedHandle
    {
        internal OwnedHandle(byte[] storage)=>Storage=storage;
        internal byte[] Storage { get; }
    }
    internal delegate byte[] Allocate(int size);
    internal Allocate Allocator { get; set; } = size=>new byte[size];
    internal byte VersionMajor { get; private set; }
    internal byte VersionMinor { get; private set; }
    internal bool Initialized { get; private set; }
    internal Dictionary<string,int> Parameters { get; }=new(StringComparer.Ordinal);
    internal OwnedHandle? Common { get; private set; }
    internal OwnedHandle? Result { get; private set; }
    internal uint AngleMask { get; private set; }
    internal int HandleMode { get; private set; }
    internal int InitialCycle,NewCycle,MinFaceSize,MaxFaceSize,Threshold,Lost,Steadiness;
    internal string? FailedStage { get; private set; }

    private static FirmwareJsonValue? Find(FirmwareJsonValue? value,string key)
    {
        if(value is null||value.Kind==FirmwareJsonKind.Null)return null;
        if(value.Kind!=FirmwareJsonKind.Object)throw new JsonLogicError("Json::Value::find requires objectValue");
        return value.Members.GetValueOrDefault(Convert.ToHexString(Encoding.UTF8.GetBytes(key)));
    }
    internal uint Initialize(FirmwareJsonValue root)
    {
        Initialized=false;FailedStage=null;Parameters.Clear(); // diagnostic copy of this invocation's local map
        // Actual nonnull version outputs: 2,0 (93392C -> 934050).
        VersionMajor=2;VersionMinor=0;
        var pet=Find(root,"PetTracker");
        if(pet is null||pet.Kind==FirmwareJsonKind.Null)return Fail("PetTracker",0x03000000);
        foreach(string key in new[]{"MaxPets","MinFaceSize","MaxFaceSize","InitialSearchCycle","NewSearchCycle","DetectionThreshold","TrackLostCount","TrackSteadiness"})
        {
            var value=Find(pet,key);
            if(value is null||value.Kind==FirmwareJsonKind.Null)return Fail(key,0x03000000);
            Parameters[key]=Json.AsInt(value);
        }
        int max=Parameters["MaxPets"];
        if(max<1||max>35){Common=null;return Fail("CommonHandle",0x01000000);}
        try
        {
            var storage=Allocator(51472);
            if(storage is null||storage.Length<51472){Common=null;return Fail("CommonHandle",0x01000000);}
            Common=new OwnedHandle(storage);
            HandleMode=1;
        }
        catch(OutOfMemoryException){Common=null;return Fail("CommonHandle",0x01000000);}
        try
        {
            var storage=Allocator(0x7C0);
            if(storage is null||storage.Length<0x7C0){Result=null;return Fail("ResultHandle",0x01000000);}
            Result=new OwnedHandle(storage);
        }
        catch(OutOfMemoryException){Result=null;return Fail("ResultHandle",0x01000000);}
        // Fixed native angle selector 0 / mask 0x803803 passes the checked validator.
        AngleMask=0x803803;
        int first=Parameters["InitialSearchCycle"],next=Parameters["NewSearchCycle"];
        if(first<1||first>45||next<5||next>45)return Fail("SearchCycles",0x03000000);
        InitialCycle=first;NewCycle=next;
        int min=Parameters["MinFaceSize"],maxSize=Parameters["MaxFaceSize"];
        if(min<20||min>8192||maxSize<20||maxSize>8192||min>maxSize)return Fail("Size",0x03000000);
        MinFaceSize=min;MaxFaceSize=maxSize;
        int threshold=Parameters["DetectionThreshold"];
        if(threshold<1||threshold>1000)return Fail("Threshold",0x03000000);
        Threshold=threshold;
        int lost=Parameters["TrackLostCount"];
        if(lost<0||lost>15)return Fail("Lost",0x03000000);
        Lost=lost;
        int steady=Parameters["TrackSteadiness"];
        if(steady<0||steady>30)return Fail("Steadiness",0x03000000);
        Steadiness=steady;Initialized=true;return 0;
    }
    private uint Fail(string stage,uint result){FailedStage=stage;return result;}
}

// The result token belongs to one original worker owner. Later algorithms remain separate.
internal sealed class NativeVisionInitialization
{
    internal NativeVisionInitialization(EngineRobot owner){Owner=owner;}
    internal EngineRobot Owner { get; }
    private volatile bool _availabilityKnown=true;
    private volatile bool _initialized;
    internal bool AvailabilityKnown => _availabilityKnown;
    internal bool Initialized => _initialized;
    internal bool Reset338 { get; private set; }
    internal bool Reset328 { get; private set; }
    internal NativeVisionPetInitialization Pet { get; }=new();
    internal NativeVisionFaceConstructor? Face { get; private set; }
    internal NativeVisionMotionConstructor? Motion { get; private set; }
    internal long ProfilerInfoMs { get; private set; }
    internal long ProfilerDasMs { get; private set; }
    internal double ClaheClipLimit { get; private set; }
    internal int ClaheTilesX { get; private set; }
    internal int ClaheTilesY { get; private set; }
    internal uint? LastResult { get; private set; }
    internal void EnterSystem(){_availabilityKnown=false;Reset338=false;_initialized=false;Reset328=false;LastResult=null;_availabilityKnown=true;}
    internal void SetProfiler(long info,long das){ProfilerInfoMs=info;ProfilerDasMs=das;}
    internal void ReplaceFace(string modelPath,FirmwareJsonValue config)=>Face=new(modelPath,config);
    internal void ReplaceMotion(VisionSystem camera,FirmwareJsonValue config)=>Motion=new(camera,config);
    internal void Failure(uint result){LastResult=result;_availabilityKnown=true;}
    internal void MarkUnavailable(){_availabilityKnown=false;}
    internal void Finish(){ClaheClipLimit=32;ClaheTilesX=4;ClaheTilesY=4;_initialized=true;LastResult=0;_availabilityKnown=true;}
}

// Native FaceTracker receives path/config, not the worker camera; its algorithm remains unavailable.
internal sealed class NativeVisionFaceConstructor(string modelPath,FirmwareJsonValue config)
{
    internal string ModelPath { get; }=modelPath;
    // Wrapper and embedded recognizer receive the full input; Impl+48 copies FaceDetection.
    internal FirmwareJsonValue RecognizerInput { get; }=Copy(config);
    internal FirmwareJsonValue? Config { get; }=FaceConfig(config);
    internal Cozmo.Robot.Animation.EngineRandom Random { get; }=new(1);
    private static FirmwareJsonValue? FaceConfig(FirmwareJsonValue root)
    {
        if(root.Kind!=FirmwareJsonKind.Object)return null;
        return root.Members.TryGetValue(Convert.ToHexString(Encoding.UTF8.GetBytes("FaceDetection")),out var value)?Copy(value):null;
    }
    private static FirmwareJsonValue Copy(FirmwareJsonValue source)
    {
        var value=new FirmwareJsonValue{Kind=source.Kind,Signed=source.Signed,Unsigned=source.Unsigned,Real=source.Real,Boolean=source.Boolean,Bytes=source.Bytes.ToArray()};
        foreach(var item in source.Members)value.Members.Add(item.Key,Copy(item.Value));
        foreach(var item in source.Elements)value.Elements.Add(Copy(item));
        return value;
    }
}

internal sealed class NativeVisionMotionConstructor(VisionSystem camera,FirmwareJsonValue config)
{
    internal VisionSystem Camera { get; }=camera;
    internal FirmwareJsonValue Config { get; }=config;
    internal NativeMotionPrevious Previous { get; }=new();
    internal NativeMotionImageCache ImageCache { get; }=new();
    internal uint Timestamp { get; }=0;
}

internal readonly record struct NativeVisionStageResult(bool Available,uint Result)
{
    internal static NativeVisionStageResult Success=>new(true,0);
    internal static NativeVisionStageResult Failure(uint value)=>new(true,value);
    internal static NativeVisionStageResult Missing=>new(false,0);
}

public sealed partial class VisionSystem
{
    internal static bool NativeScheduleIsInt(FirmwareJsonValue item) =>
        item.Kind==FirmwareJsonKind.Int && item.Signed>=int.MinValue && item.Signed<=int.MaxValue ||
        item.Kind==FirmwareJsonKind.UInt && item.Unsigned<=int.MaxValue ||
        item.Kind==FirmwareJsonKind.Real && double.IsFinite(item.Real) && item.Real>=int.MinValue && item.Real<=int.MaxValue && Math.Truncate(item.Real)==item.Real;
    internal NativeVisionStageResult ApplyNativeInitialSchedules(FirmwareJsonValue root,Func<bool> current)
    {
        static FirmwareJsonValue? Member(FirmwareJsonValue? value,string key)
        {
            if(value is null||value.Kind==FirmwareJsonKind.Null)return null;
            if(value.Kind!=FirmwareJsonKind.Object)throw new JsonLogicError("Json::Value::find requires objectValue");
            return value.Members.GetValueOrDefault(Convert.ToHexString(Encoding.UTF8.GetBytes(key)));
        }
        var group=Member(root,"InitialModeSchedules");
        if(group is null)
        {
            _robot.Engine.EmitSourceLog(EngineLogLevel.Error,null,"VisionSystem.Init.MissingJsonParameter","InitialModeSchedules");
            if(!current())return NativeVisionStageResult.Missing;EngineErrorState.StoreAndMaybeBreak();return NativeVisionStageResult.Failure(1);
        }
        try
        {
            for(int mode=0;mode<AllVisionModesSchedule.ModeCount;mode++)
            {
                if(!current())return NativeVisionStageResult.Missing;
                var item=Member(group,((VisionMode)mode).ToString());
                if(item is null)continue;
                bool[] values;
                if(item.Kind==FirmwareJsonKind.Array)
                {
                    if(item.Elements.Count==0)
                    {SteppedBehavior.ReportMissing("M1-034/M11-059 empty initial schedule reaches undefined native vector use; schedule unit skipped");return NativeVisionStageResult.Missing;}
                    values=item.Elements.Select(ValueAsBool).ToArray();
                }
                else if(NativeScheduleIsInt(item))
                {
                    int count=Json.AsInt(item);
                    if(count<0){SteppedBehavior.ReportMissing("M1-034/M11-059 negative initial schedule allocation is undefined; schedule unit skipped");return NativeVisionStageResult.Missing;}
                    values=count==0?new[]{false}:new bool[count];
                    if(count>0)values[0]=true;
                }
                else if(item.Kind==FirmwareJsonKind.Boolean)values=new[]{item.Boolean};
                else
                {
                    _robot.Engine.EmitSourceLog(EngineLogLevel.Error,null,"VisionSystem.Init.InvalidSchedule",((VisionMode)mode).ToString());
                    if(!current())return NativeVisionStageResult.Missing;EngineErrorState.StoreAndMaybeBreak();return NativeVisionStageResult.Failure(1);
                }
                if(!current())return NativeVisionStageResult.Missing;
                AllVisionModesSchedule.SetDefault(mode,new VisionModeSchedule(values));
            }
            return NativeVisionStageResult.Success;
        }
        catch(OutOfMemoryException)
        {SteppedBehavior.ReportMissing("M1-034/M11-059 initial schedule allocation unavailable; schedule unit skipped");return NativeVisionStageResult.Missing;}
        catch(JsonLogicError)
        {SteppedBehavior.ReportMissing("M1-034/M11-059 initial schedule JsonCpp terminating input; schedule unit skipped");return NativeVisionStageResult.Missing;}
    }
    private static bool ValueAsBool(FirmwareJsonValue value)=>value.Kind switch
    {
        FirmwareJsonKind.Null=>false,FirmwareJsonKind.Boolean=>value.Boolean,
        FirmwareJsonKind.Int=>value.Signed!=0,FirmwareJsonKind.UInt=>value.Unsigned!=0,
        FirmwareJsonKind.Real=>value.Real!=0,_=>throw new JsonLogicError("asBool invalid kind")
    };
}
