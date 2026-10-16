using System.Text;
using Cozmo.Transport;
using Cozmo.Robot.Behavior;
namespace Cozmo.Robot;
public sealed partial class CozmoEngine
{
    // fidelity: M11-082 — RobotDataLoader+118 starts null; source failure tail does not clear it.
    internal FirmwareJsonValue VisionConfiguration {get;private set;}=new();
    internal bool VisionConfigurationRead {get;private set;}
    internal bool VisionConfigurationReadSucceeded {get;private set;}
    internal const string VisionConfigurationPath="config/engine/vision_config.json";
    private readonly HashSet<string> _visionConfigMissingScope=new();
    private void VisionConfigMissing(string text)=>SteppedBehavior.ReportMissing(text,_visionConfigMissingScope);
    private static FirmwareJsonValue Copy(FirmwareJsonValue source)
    {
        var copy=new FirmwareJsonValue{Kind=source.Kind,Signed=source.Signed,Unsigned=source.Unsigned,Real=source.Real,Boolean=source.Boolean,Bytes=source.Bytes.ToArray()};
        foreach(var entry in source.Members)copy.Members[entry.Key]=Copy(entry.Value);
        foreach(var entry in source.Elements)copy.Elements.Add(Copy(entry));return copy;
    }
    internal void LoadVisionConfiguration()
    {
        if(Options.ResourcesPath is not {} root)
        {VisionConfigMissing("M11-082 no DataPlatform scope1 resource root for vision config; no invented file input");return;}
        string path=root+"/"+VisionConfigurationPath;byte[] input;
        try{input=File.ReadAllBytes(path);}catch(Exception e)when(e is IOException or UnauthorizedAccessException){input=Array.Empty<byte>();}
        VisionConfigurationRead=true;
        var previous=Copy(VisionConfiguration);
        try
        {
            VisionConfigurationReadSucceeded=Json.TryParseInto(input,VisionConfiguration); // never clear on false.
            if(!VisionConfigurationReadSucceeded)
            {
                EmitSourceLog(EngineLogLevel.Error,null,"DataPlatform.readAsJson",$"Failed to read [{path}]");EngineErrorState.StoreAndMaybeBreak();
                VisionConfigMissing("M11-082/M5-051 readAsJson conditional Unnamed Debug0x0084C124..0x0084C15C: exact Json reader error bytes remain unread; no invented Debug text");
                EmitSourceLog(EngineLogLevel.Error,null,"RobotDataLoader.VisionConfigJsonNotFound",$"Vision Json config file {VisionConfigurationPath} not found or failed to parse");EngineErrorState.StoreAndMaybeBreak();
            }
        }
        catch(JsonRuntimeError)
        {VisionConfiguration=previous;StackDiagnostics.Write("M1-034/M11-082 vision config parser depth throw0x008E0D7A: skip this file; prior loader state retained");}
        catch(JsonMissingSource e){VisionConfiguration=previous;VisionConfigurationReadSucceeded=false;VisionConfigMissing("M11-082 vision reader dependency: "+e.Message);}
        // Both branches converge here. Do not invent the unbuilt continuation or abandon later startup.
        VisionConfigMissing("M11-082 LoadGameRequestAndTrickConfigs call0x005226EC after vision read (including failure): native recipient unbuilt; later startup continues");
    }
}
