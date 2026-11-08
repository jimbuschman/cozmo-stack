using System.Runtime.InteropServices;
using Cozmo.Robot;

namespace Cozmo.Conformance;

/// <summary>
/// The engine options a hardware run uses, in one place, with the host inputs the app used to supply. Existing policies:
/// DasDeviceId is the one persisted id of the DAS device file (M15-053, M3-058); LabLocale* are host settings taken from the OS locale
/// (M15-052, M1-058). The needs directory is the stack's own nurture folder, kept across runs.
/// Tests override the clock, the paths and the locale; a test machine's region must not change a result.
/// </summary>
public static class HardwareEngineOptions
{
    public static CozmoEngineOptions Create(string resources,string? filesPath=null,string? needsDirectory=null,string? deviceIdPath=null,
        string? blockPoolPath=null,Func<long>? clock=null,Action<CozmoEngine>? beforeStart=null,string? localeName=null,Action<string>? log=null,string? speechSoundBankDirectory=null)
    {
        var defaults=new CozmoEngineOptions();
        string files=filesPath??defaults.FilesPath;
        string needs=Directory.CreateDirectory(needsDirectory??Path.Combine(files,"nurture")).FullName;
        var (language,country)=Locale(localeName??HostLocaleName(),log??Console.Error.WriteLine);
        return new CozmoEngineOptions{ResourcesPath=resources,FilesPath=files,BlockPoolPath=blockPoolPath??defaults.BlockPoolPath,NeedsDeviceDirectory=needs,
            NeedsSystemClockMicroseconds=clock,DasDeviceId=DasDeviceIdFile.LoadOrCreate(deviceIdPath??DasDeviceIdFile.DefaultPath),
            LabLocaleLanguage=language,LabLocaleCountry=country,BeforeStart=beforeStart,SpeechSoundBankDirectory=speechSoundBankDirectory};
    }
    // Reads the OS setting directly: this solution runs in .NET globalization-invariant mode, where CultureInfo.CurrentCulture is always invariant.
    private static (string? Language,string? Country) Locale(string? name,Action<string> log)
    {
        string[] parts=(name??"").Split('-','_','.').Where(part=>part.Length>0).ToArray();
        string? language=parts.Length>0?parts[0].ToLowerInvariant():null;
        string? country=parts.Skip(1).FirstOrDefault(part=>part.Length==2&&part.All(char.IsLetter))?.ToLowerInvariant();
        if(language is null||country is null)log($"M15-052 PC locale '{name}' has no language or region; the Lab locale input stays MISSING");
        return (language,country);
    }
    private static string? HostLocaleName()
    {
        if(!OperatingSystem.IsWindows())return Environment.GetEnvironmentVariable("LC_ALL") is {Length:>0} all?all:Environment.GetEnvironmentVariable("LANG");
        var buffer=new char[85];int length=GetUserDefaultLocaleName(buffer,buffer.Length);
        return length>1?new string(buffer,0,length-1):null;
    }
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] private static extern int GetUserDefaultLocaleName(char[] name,int size);
}
