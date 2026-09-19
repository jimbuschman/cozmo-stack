using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// The Wwise name hash, checked against the shipped banks themselves. This is the piece that makes the
/// switch-state audio the 39 Singing behaviours need resolvable at all, so it is worth pinning hard.
/// </summary>
public class WwiseHashTests
{
    /// <summary>
    /// Each bank's BKHD id is the hash of its file name. Six banks, six exact matches, which is what
    /// establishes the algorithm rather than merely being consistent with it.
    /// </summary>
    [Theory]
    [InlineData("Music", 3991942870u)]
    [InlineData("SFX", 393239870u)]
    [InlineData("Cozmo", 2386142475u)]
    [InlineData("UI", 1551306167u)]
    [InlineData("Init", 1355168291u)]
    [InlineData("Dev_Debug", 1453038702u)]
    public void TheHashReproducesTheShippedBankIds(string name, uint expected)
        => Assert.Equal(expected, WwiseHash.Of(name));

    /// <summary>It is case-insensitive, because Wwise lower-cases before hashing.</summary>
    [Fact]
    public void TheHashIgnoresCase()
    {
        Assert.Equal(WwiseHash.Of("Cozmo"), WwiseHash.Of("COZMO"));
        Assert.Equal(WwiseHash.Of("Dev_Debug"), WwiseHash.Of("dev_debug"));
    }

    /// <summary>
    /// The switch names a Singing behaviour carries hash to stable ids. These are recorded so that a
    /// future change to the hash is caught, not because the ids have been matched to bank objects yet.
    /// </summary>
    [Theory]
    [InlineData("Cozmo_Sings_80Bpm", 3366294904u)]
    [InlineData("Cozmo_Sings_Aba_Daba", 2234458138u)]
    public void TheSingingSwitchNamesHashToStableIds(string name, uint expected)
        => Assert.Equal(expected, WwiseHash.Of(name));

    /// <summary>It is FNV-1, not FNV-1a: multiply first, then xor. The two differ for every input.</summary>
    [Fact]
    public void ItIsFnv1RatherThanFnv1a()
    {
        uint fnv1a = 2166136261;
        foreach (char c in "music") { fnv1a ^= c; fnv1a *= 16777619; }
        Assert.NotEqual(fnv1a, WwiseHash.Of("Music"));
        Assert.Equal(3991942870u, WwiseHash.Of("Music"));
    }

    /// <summary>
    /// Every bank this build ships hashes to its own id, checked against the files rather than a list.
    /// </summary>
    [Fact]
    public void EveryShippedBankIdIsItsNameHashed()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        string? meta = null;
        while (d is not null && meta is null)
        {
            var candidate = Path.Combine(d.FullName, "re-analysis", "obb", "sound_meta");
            if (Directory.Exists(candidate)) meta = candidate;
            d = d.Parent;
        }
        if (meta is null) return;

        int checkedBanks = 0;
        foreach (var f in Directory.EnumerateFiles(meta, "*.bnk", SearchOption.AllDirectories))
        {
            var bank = WwiseBank.Parse(File.ReadAllBytes(f), Path.GetFileName(f));
            Assert.Equal(WwiseHash.Of(Path.GetFileNameWithoutExtension(f)), bank.BankId);
            checkedBanks++;
        }
        Assert.Equal(6, checkedBanks);
    }
}
