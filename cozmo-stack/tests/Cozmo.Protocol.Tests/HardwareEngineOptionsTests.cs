using Cozmo.Conformance;
using Cozmo.Robot;
using Xunit;

namespace Cozmo.Protocol.Tests;

// M15-053, M3-058 (one persisted id), M15-052, M1-058 (host culture). reach: unit, the options object a hardware run starts from.
public sealed class HardwareEngineOptionsTests:IDisposable
{
    private readonly string _root=Directory.CreateTempSubdirectory("cozmo-hardware-options-").FullName;
    public void Dispose()=>Directory.Delete(_root,true);
    private CozmoEngineOptions Create(string locale,List<string>? log=null)=>HardwareEngineOptions.Create("resources",filesPath:_root,deviceIdPath:Path.Combine(_root,"device-id"),localeName:locale,log:log is null?null:log.Add);
    [Fact]
    public void SuppliesEveryHostInputAndLowercasesTheLocale()
    {
        var options=Create("en-GB");
        Assert.Equal("en",options.LabLocaleLanguage);Assert.Equal("gb",options.LabLocaleCountry);Assert.False(string.IsNullOrEmpty(options.DasDeviceId));
        Assert.Equal(Path.Combine(_root,"nurture"),options.NeedsDeviceDirectory);Assert.True(Directory.Exists(options.NeedsDeviceDirectory));
    }
    [Fact]
    public void DeviceIdIsCreatedOnceAndIsTheDasFile()
    {
        string id=Create("en-US").DasDeviceId!;
        Assert.Equal(id,Create("en-US").DasDeviceId);Assert.Equal(id,DasDeviceIdFile.LoadOrCreate(Path.Combine(_root,"device-id")));
    }
    [Fact]
    public void ScriptTagsAndUnixNamesFindTheRegion()
    {
        Assert.Equal("cn",Create("zh-Hans-CN").LabLocaleCountry);var unix=Create("de_DE.UTF-8");Assert.Equal("de",unix.LabLocaleLanguage);Assert.Equal("de",unix.LabLocaleCountry);
    }
    [Fact]
    public void CultureWithNoRegionIsLoggedAndLeftMissing()
    {
        var log=new List<string>();var options=Create("en",log);
        Assert.Null(options.LabLocaleCountry);Assert.Contains(log,line=>line.Contains("M15-052"));
    }
}
