using System.Diagnostics;
using Cozmo.Robot.Emulation;
using Xunit;
using Xunit.Abstractions;

namespace Cozmo.Protocol.Tests;

/// <summary>
/// Acapela speech under the embedded emulator (job EMU-RUNTIME-1). The expected samples are the Python probe's output
/// (<c>.scratch/probes/tts_probe.py</c> over <c>re-analysis/tools/emu/emu_elf.py</c>, the same Unicorn 2.1.4 build):
/// <c>Fixtures/tts/hello_cozmo.wav</c>, sha256 b08bca69da1ad493..., 70,992 samples at 22050 Hz, byte-identical over three
/// probe runs (research/20261103-okao-acapela-probes.md, Acapela section). The libraries and voices are the user's own
/// (resources/lib/armeabi-v7a, re-analysis/obb); without them these tests return early.
/// reach: unit (the emulator and the Java-order driver are called directly; no SayText path exists yet).
/// </summary>
[Trait("Category", "Emulator")]
public sealed class AcapelaTtsTests
{
    private readonly ITestOutputHelper _out;
    public AcapelaTtsTests(ITestOutputHelper output) => _out = output;

    /// <summary>The probe's text: "Hello, I am Cozmo." with the engine's cozmo string (tts_config.json android.en,
    /// research note: ctor 0x0069FA7C) substituted for "Cozmo".</summary>
    internal const string ProbeText = "Hello, I am \\Prn=k_h O z m @U\\.";

    internal static string? TtsDir()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
        {
            var p = Path.Combine(d.FullName, "re-analysis", "obb", "assets", "cozmo_resources", "tts");
            if (Directory.Exists(Path.Combine(p, "Voices"))) return p;
        }
        return null;
    }

    private static string RepoRoot()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (Directory.Exists(Path.Combine(d.FullName, "cozmo-stack", "src"))) return d.FullName;
        throw new DirectoryNotFoundException("repo root above " + AppContext.BaseDirectory);
    }

    internal static short[] ProbeSamples()
    {
        var wav = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "tts", "hello_cozmo.wav"));
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(wav, 0, 4));
        int p = 12;
        while (System.Text.Encoding.ASCII.GetString(wav, p, 4) != "data") p += 8 + BitConverter.ToInt32(wav, p + 4);
        int n = BitConverter.ToInt32(wav, p + 4) / 2;
        var s = new short[n];
        Buffer.BlockCopy(wav, p + 8, s, 0, n * 2);
        return s;
    }

    /// <summary>The longjmp replacement has no CET shadow-stack path: the shadow-stack query works here, and the stack's own
    /// executable is built without CETCOMPAT (its EX_DLLCHARACTERISTICS debug entry is 0), so its process never enforces them.</summary>
    [Fact]
    public void The_host_runs_without_CET_shadow_stacks()
    {
        if (!OperatingSystem.IsWindows()) return;
        Assert.False(Cozmo.Robot.Emulation.UnicornLongjmp.UserShadowStackEnforced());
        var proj = System.Xml.Linq.XDocument.Load(System.IO.Path.Combine(RepoRoot(), "cozmo-stack", "src", "Cozmo.Conformance", "Cozmo.Conformance.csproj"));
        Assert.Contains(proj.Descendants("CETCompat"), e => e.Value.Trim() == "false");
    }

    [Fact]
    public void Synthesis_matches_the_python_probe_bit_for_bit()
    {
        var lib = ShippedLibraries.FindLibDir();
        var tts = TtsDir();
        if (lib is null || tts is null) return;
        var expected = ProbeSamples();
        Assert.Equal(70992, expected.Length);
        var sw = Stopwatch.StartNew();
        using var t = new AcapelaTts(lib);
        t.LoadVoice(tts, "Ryan");
        var load = sw.Elapsed;
        sw.Restart();
        var got = t.Synthesize(ProbeText, 50, 100, 100);
        var synth = sw.Elapsed;
        _out.WriteLine($"load {load.TotalSeconds:F2} s, synth {synth.TotalSeconds:F2} s for {got.Length / 22050.0:F2} s audio; speak={t.LastSpeakResult}");
        Assert.Equal(22050, t.VoiceFrequency);
        Assert.Equal(expected.Length, got.Length);
        Assert.Equal(expected, got);
    }

    /// <summary>The engine's credential getters, run under the emulator, give the values the research note reads from the
    /// instructions: GetUserid = 0x6BA483B0 ^ 0x0ACADADA (0x0069F8DC..F6), GetPassword = 0x0AC94A41 ^ 0x0ACADADA
    /// (0x0069F8F8..912), GetLicense the 0xE5-byte string at 0x00C00713 (0x0069F914)
    /// (research/20261103-okao-acapela-probes.md, Acapela "Credentials").</summary>
    [Fact]
    public void The_engine_decodes_the_credentials_the_instructions_encode()
    {
        var lib = ShippedLibraries.FindLibDir();
        if (lib is null) return;
        var c = AcapelaCredentials.FromEngine(lib);
        Assert.Equal(0x6BA483B0u ^ 0x0ACADADAu, c.UserId);
        Assert.Equal(0x0AC94A41u ^ 0x0ACADADAu, c.Password);
        Assert.Equal(0xE5, c.License.Length);
    }

    /// <summary>With a wrong password the library refuses the voice: the probe saw nLoadVoice return -12
    /// (research note, Acapela "Probe": "With bogus credentials the load returns -12, so the licence is enforced").</summary>
    [Fact]
    public void Wrong_credentials_make_the_voice_load_fail_with_minus_12()
    {
        var lib = ShippedLibraries.FindLibDir();
        var tts = TtsDir();
        if (lib is null || tts is null) return;
        var good = AcapelaCredentials.FromEngine(lib);
        var bad = good with { Password = good.Password ^ 0x1 };
        using var t = new AcapelaTts(lib);
        var e = Assert.Throws<AcapelaTtsException>(() => t.LoadVoice(tts, "Ryan", bad));
        Assert.Equal("nLoadVoice", e.Step);
        Assert.Equal(-12, e.Code);
    }

    /// <summary>A library whose bytes differ from the pinned build is refused before anything runs.</summary>
    [Fact]
    public void A_library_that_is_not_the_pinned_build_is_refused()
    {
        var lib = ShippedLibraries.FindLibDir();
        if (lib is null) return;
        var dir = Directory.CreateTempSubdirectory("emu-pin-");
        try
        {
            var raw = File.ReadAllBytes(Path.Combine(lib, "libc++_shared.so"));
            raw[^1] ^= 0xFF;
            File.WriteAllBytes(Path.Combine(dir.FullName, "libc++_shared.so"), raw);
            var e = Assert.Throws<InvalidDataException>(() => new ArmMachine(dir.FullName, new[] { "libc++_shared.so" }));
            Assert.Contains("8ac5090bbd0be7401af5fff6044ded6f1e6ced692fcd3581fa3cb937b519a16a", e.Message);
        }
        finally
        {
            dir.Delete(true);
        }
    }

    /// <summary>The ELF reader sees libacattsandroid.so's dynamic relocations as lief does (the Python loader's source):
    /// 2913 RELATIVE, 77 JUMP_SLOT, 17 ABS32, 7 GLOB_DAT, one INIT_ARRAY entry (DT_INIT_ARRAY 0x1E4600, DT_INIT_ARRAYSZ 4).</summary>
    [Fact]
    public void The_elf_reader_finds_the_relocations_lief_finds()
    {
        var lib = ShippedLibraries.FindLibDir();
        if (lib is null) return;
        var elf = new ElfImage(ShippedLibraries.ReadPinned(lib, "libacattsandroid.so"));
        var byType = elf.Relocations.GroupBy(r => r.Type).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(2913, byType[ElfImage.R_ARM_RELATIVE]);
        Assert.Equal(77, byType[ElfImage.R_ARM_JUMP_SLOT]);
        Assert.Equal(17, byType[ElfImage.R_ARM_ABS32]);
        Assert.Equal(7, byType[ElfImage.R_ARM_GLOB_DAT]);
        Assert.Single(elf.InitArray);
        Assert.Equal(2499, elf.DynamicSymbols.Count);
    }
}
