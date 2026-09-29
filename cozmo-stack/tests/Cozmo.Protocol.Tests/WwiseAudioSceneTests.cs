using Cozmo.Robot.Animation.Wwise;
using Xunit;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// M6-024, bank and scene loading call sites (rows F1-F3). Expected values are the rows' own: the ordered
/// six-bank list <c>Init.bnk, Music.bnk, UI.bnk, SFX.bnk, Cozmo.bnk, Dev_Debug.bnk</c> and the scene
/// <c>InitScene</c> (F1), loaded from the sound root or the OBB <c>AudioAssets.zip</c> (F2/F3). The tests
/// build their own banks, so they run without the shipped OBB.
/// </summary>
public class WwiseAudioSceneTests
{
    /// <summary>A minimal but valid bank: BKHD version 120 and a bank id, consumed exactly by the reader.</summary>
    private static byte[] Bank(uint bankId)
    {
        var body = new List<byte>();
        body.AddRange(BitConverter.GetBytes(120u));     // version
        body.AddRange(BitConverter.GetBytes(bankId));
        body.AddRange(BitConverter.GetBytes(0u));
        body.AddRange(BitConverter.GetBytes(0u));       // feedback flag clear

        var file = new List<byte>();
        file.AddRange("BKHD"u8.ToArray());
        file.AddRange(BitConverter.GetBytes((uint)body.Count));
        file.AddRange(body);
        return file.ToArray();
    }

    /// <summary>Row F1: the scene name and the six banks, in the ctor's order.</summary>
    [Fact]
    public void TheSceneIsTheRecoveredSixBanksInOrderAndInitScene()
    {
        Assert.Equal("InitScene", WwiseAudioScene.SceneName);
        Assert.Equal(
            new[] { "Init.bnk", "Music.bnk", "UI.bnk", "SFX.bnk", "Cozmo.bnk", "Dev_Debug.bnk" },
            WwiseAudioScene.BankNames);
    }

    /// <summary>
    /// Rows F1/F2: the scene loader loads exactly the recovered six, in the recovered order, and ignores
    /// any other bank in the directory.
    /// </summary>
    [Fact]
    public void TheSceneLoaderLoadsExactlyTheRecoveredBanksInOrder()
    {
        var dir = Directory.CreateTempSubdirectory("wwise-scene");
        try
        {
            // the six plus an extra, written in a shuffled order
            var onDisk = new[]
            {
                "Dev_Debug.bnk", "UI.bnk", "Extra.bnk", "Init.bnk", "Cozmo.bnk", "Music.bnk", "SFX.bnk",
            };
            for (int i = 0; i < onDisk.Length; i++)
                File.WriteAllBytes(Path.Combine(dir.FullName, onDisk[i]), Bank((uint)(100 + i)));

            using var lib = WwiseSoundLibrary.LoadScene(dir.FullName);

            // the source literals (row F1), not the implementation constant, so a wrong constant fails here
            Assert.Equal(
                new[] { "Init.bnk", "Music.bnk", "UI.bnk", "SFX.bnk", "Cozmo.bnk", "Dev_Debug.bnk" },
                lib.Banks.Select(b => b.Name));
        }
        finally { dir.Delete(true); }
    }

    /// <summary>
    /// Row F3: the production <see cref="WwiseSoundLibrary.Load"/> uses the recovered scene set and order
    /// when the six are present, rather than a directory-order sweep of every <c>.bnk</c>.
    /// </summary>
    [Fact]
    public void TheProductionLoadUsesTheRecoveredSceneSetWhenTheSixArePresent()
    {
        var dir = Directory.CreateTempSubdirectory("wwise-scene");
        try
        {
            var onDisk = new[]
            {
                "Dev_Debug.bnk", "UI.bnk", "Extra.bnk", "Init.bnk", "Cozmo.bnk", "Music.bnk", "SFX.bnk",
            };
            for (int i = 0; i < onDisk.Length; i++)
                File.WriteAllBytes(Path.Combine(dir.FullName, onDisk[i]), Bank((uint)(200 + i)));

            using var lib = WwiseSoundLibrary.Load(dir.FullName);

            // the source literals (row F1), not the implementation constant, so a wrong constant fails here
            Assert.Equal(
                new[] { "Init.bnk", "Music.bnk", "UI.bnk", "SFX.bnk", "Cozmo.bnk", "Dev_Debug.bnk" },
                lib.Banks.Select(b => b.Name));
        }
        finally { dir.Delete(true); }
    }

    /// <summary>
    /// The general loader still sweeps a plain directory that holds none of the scene's banks, which is what
    /// the tests that build their own banks depend on.
    /// </summary>
    [Fact]
    public void APlainDirectoryOfBanksIsStillLoadedByTheSweep()
    {
        var dir = Directory.CreateTempSubdirectory("wwise-plain");
        try
        {
            File.WriteAllBytes(Path.Combine(dir.FullName, "T.bnk"), Bank(1));

            using var lib = WwiseSoundLibrary.Load(dir.FullName);

            var bank = Assert.Single(lib.Banks);
            Assert.Equal("T.bnk", bank.Name);
            Assert.Equal(1u, bank.BankId);
        }
        finally { dir.Delete(true); }
    }

    /// <summary>
    /// The scene loader keeps the recovered order and drops the rest even when only some of the six are
    /// present, so "exactly those six" is not "everything that happens to be there".
    /// </summary>
    [Fact]
    public void TheSceneLoaderDropsBanksOutsideTheRecoveredSet()
    {
        var dir = Directory.CreateTempSubdirectory("wwise-scene-partial");
        try
        {
            File.WriteAllBytes(Path.Combine(dir.FullName, "Cozmo.bnk"), Bank(2));
            File.WriteAllBytes(Path.Combine(dir.FullName, "Init.bnk"), Bank(1));
            File.WriteAllBytes(Path.Combine(dir.FullName, "Extra.bnk"), Bank(3));

            using var lib = WwiseSoundLibrary.LoadScene(dir.FullName);

            Assert.Equal(new[] { "Init.bnk", "Cozmo.bnk" }, lib.Banks.Select(b => b.Name));
        }
        finally { dir.Delete(true); }
    }
}