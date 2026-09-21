using Cozmo.Robot.Animation.Wwise;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// The one shipped sound library the Wwise tests share.
///
/// Loading it means opening a 141 MB archive and indexing six banks. Four test classes need it, and xUnit
/// runs classes in parallel, so a copy per class was four copies open at once — enough extra memory and
/// I/O to starve the behaviour tests, which drive their rigs against a wall-clock timeout. One instance,
/// loaded once, keeps the rest of the suite honest.
/// </summary>
internal static class WwiseAssets
{
    private static IEnumerable<string> Roots()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            yield return Path.Combine(d.FullName, "re-analysis", "obb");
            d = d.Parent;
        }
    }

    /// <summary>The directory holding AudioAssets.zip, which carries the banks, the text tables and the media.</summary>
    public static string? SoundDir { get; } = Roots()
        .Select(r => Path.Combine(r, "assets", "cozmo_resources", "sound"))
        .FirstOrDefault(s => File.Exists(Path.Combine(s, "AudioAssets.zip")));

    /// <summary>The OBB root, for tests that also need the shipped configuration.</summary>
    public static string? ObbRoot { get; } = Roots()
        .FirstOrDefault(r => File.Exists(Path.Combine(r, "assets", "cozmo_resources", "sound", "AudioAssets.zip")));

    private static readonly Lazy<WwiseSoundLibrary?> Shared =
        new(() => SoundDir is { } d ? WwiseSoundLibrary.Load(d) : null, isThreadSafe: true);

    /// <summary>The shared library, or null when the OBB is not unpacked. Never disposed: the process owns it.</summary>
    public static WwiseSoundLibrary? Library => Shared.Value;
}
