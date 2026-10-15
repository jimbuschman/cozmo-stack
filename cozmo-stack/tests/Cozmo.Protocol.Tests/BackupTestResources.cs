using Cozmo.Robot;
namespace Cozmo.Protocol.Tests;

// Queue fixtures explicitly load the shipped backup config; production has no hard-coded eight-tag fallback.
internal sealed class BackupTestResources : IDisposable
{
    internal string Root { get; } = Directory.CreateTempSubdirectory("cozmo-backup-test-").FullName;
    internal static string ShippedConfig()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
        {
            string p = Path.Combine(d.FullName,"re-analysis/obb/assets/cozmo_resources/config/engine/backup_config.json");
            if (File.Exists(p)) return p;
        }
        throw new InvalidOperationException("Shipped backup_config.json required");
    }
    internal CozmoEngineOptions Options(CozmoEngineOptions? options = null)
    {
        options ??= new CozmoEngineOptions { BlockPoolPath = "" };
        string resources = Path.Combine(Root,"resources"); Directory.CreateDirectory(Path.Combine(resources,"config/engine"));
        File.Copy(ShippedConfig(),Path.Combine(resources,"config/engine/backup_config.json"),true);
        return new CozmoEngineOptions {
            ResourcesPath = options.ResourcesPath ?? resources, FilesPath = Root, BlockPoolPath = options.BlockPoolPath,
            DataCollectionEnabled = options.DataCollectionEnabled, DasDeviceIdPath = options.DasDeviceIdPath,
            AnimationCachePath = options.AnimationCachePath, VerboseLogDisplay = options.VerboseLogDisplay,
            DataPlatformPersistentPath = options.DataPlatformPersistentPath, RobotVolume = options.RobotVolume,
        };
    }
    public void Dispose() => Directory.Delete(Root,true);
}
