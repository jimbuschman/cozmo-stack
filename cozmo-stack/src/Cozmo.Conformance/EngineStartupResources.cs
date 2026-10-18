using Cozmo.Robot;
namespace Cozmo.Conformance;
/// <summary>Resources input shared by live tools. No implicit repository or phone paths.</summary>
public static class EngineStartupResources
{
    public static CozmoEngineOptions Options(string[] args,string? existingRoot=null)=>
        OptionsForRoot(Arg(args,"--resources")??existingRoot??Arg(args,"--obb")??Arg(args,"--assets"),Arg(args,"--state-dir"));
    public static CozmoEngineOptions OptionsForRoot(string? root,string? needsDeviceDirectory=null)
    {
        if(string.IsNullOrWhiteSpace(root))throw new ArgumentException("Engine startup needs the extracted resources. Supply --resources <cozmo_resources>, or this tool's --obb/--assets option.");
        string full=Path.GetFullPath(root);
        if(Path.GetFileName(full.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)).Equals("assets",StringComparison.OrdinalIgnoreCase)
            && Directory.Exists(Path.Combine(full,"animations")))full=Directory.GetParent(full)!.FullName;
        foreach(string candidate in new[]{full,Path.Combine(full,"cozmo_resources"),Path.Combine(full,"assets","cozmo_resources")})
            if(Directory.Exists(Path.Combine(candidate,"assets","animations"))||Directory.Exists(Path.Combine(candidate,"animations")))
                return new CozmoEngineOptions{ResourcesPath=candidate,NeedsDeviceDirectory=needsDeviceDirectory};
        throw new ArgumentException($"No animation resources under '{full}'. Supply --resources <cozmo_resources>.");
    }
    private static string? Arg(string[] args,string flag)
    {int i=Array.IndexOf(args,flag);return i>=0&&i+1<args.Length?args[i+1]:null;}
}
