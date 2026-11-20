using System.Diagnostics;
using System.Runtime.InteropServices;
using static Cozmo.Robot.Emulation.UnicornNative;
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

    [Fact]
    public void AcceleratedThirdOrderFilterMatchesThePinnedArmInstructionPath()
    {
        string? lib = ShippedLibraries.FindLibDir();
        if (lib is null) return;
        var inputs = new List<(int Order, double Input, double Alpha, double Scale, double[] Coefficients, double[] State, uint Fpscr, bool Fallback)>();
        var random = new Random(689);
        foreach (int order in new[] { 2, 10, 30, 63 })
        {
            for (int i = 0; i < 40; i++)
            {
                double[] coefficients = Enumerable.Range(0, order + 1).Select(_ => random.NextDouble() - 0.5).ToArray();
                double[] state = Enumerable.Range(0, 3 * (order + 2)).Select(_ => random.NextDouble() - 0.5).ToArray();
                uint fpscr = i % 2 == 0 ? 0u : 0x80000010;
                inputs.Add((order, random.NextDouble() - 0.5, 0.5, 0.75, coefficients, state, fpscr, false));
            }
        }
        inputs.Add((2, -0.0, 0.0, 1.0, [0, 0, 0], new double[12], 0, false));
        inputs.Add((2, 0.5, 0.5, 0.75, [0, 1, 2], Enumerable.Repeat(0.7, 12).ToArray(), 0x400000, true));
        inputs.Add((2, double.Epsilon, 0.5, 0.75, [0, 1, 2], new double[12], 0, true));
        inputs.Add((2, double.NaN, 0.5, 0.75, [0, 1, 2], new double[12], 0, true));

        var oracle = Run(accelerated: false);
        var actual = Run(accelerated: true);
        for (int i = 0; i < inputs.Count; i++)
        {
            Assert.Equal(oracle[i].Registers, actual[i].Registers);
            Assert.Equal(oracle[i].History, actual[i].History);
        }

        (ulong[] Registers, byte[] History)[] Run(bool accelerated)
        {
            using var machine = new ArmMachine(lib, [AcapelaNative.Library]);
            var library = machine.Library(AcapelaNative.Library);
            uint address = library.Base + AcapelaArmAcceleration.MglsaThirdOrder;
            int nativeEntries = 0;
            machine.ObserveAddress(address + 4, _ => nativeEntries++);
            if (accelerated) AcapelaArmAcceleration.Install(machine, library);
            uint coefficientsAddress = machine.StaticAlloc(8 * 64 + 4) + 4;
            uint stateAddress = machine.StaticAlloc(8 * 3 * 65 + 4) + 4;
            var results = new List<(ulong[] Registers, byte[] History)>();
            foreach (var input in inputs)
            {
                machine.Write(coefficientsAddress, MemoryMarshal.AsBytes(input.Coefficients.AsSpan()));
                machine.Write(stateAddress, MemoryMarshal.AsBytes(input.State.AsSpan()));
                ulong x = BitConverter.DoubleToUInt64Bits(input.Input);
                ulong alpha = BitConverter.DoubleToUInt64Bits(input.Alpha);
                ulong scale = BitConverter.DoubleToUInt64Bits(input.Scale);
                int before = nativeEntries;
                machine.Call(address | 1, [(uint)x, (uint)(x >> 32), coefficientsAddress, (uint)input.Order],
                    [(uint)alpha, (uint)(alpha >> 32), (uint)scale, (uint)(scale >> 32), 3, stateAddress], fpscr: input.Fpscr);
                Assert.Equal(!accelerated || input.Fallback ? before + 1 : before, nativeEntries);
                ulong[] registers = [machine.Reg(0), machine.Reg(1), machine.Reg(12),
                    machine.ReadReg(UC_ARM_REG_CPSR) & 0xF0000000, machine.ReadReg(UC_ARM_REG_FPSCR),
                    .. Enumerable.Range(1, 7).Select(i => machine.ReadReg(UC_ARM_REG_D8 - 8 + i))];
                results.Add((registers, machine.Read(stateAddress, (uint)input.State.Length * 8)));
            }
            return results.ToArray();
        }
    }

    [Fact]
    public void AcceleratedButterworthAndMemoryCopiesMatchThePinnedArmInstructions()
    {
        string? lib = ShippedLibraries.FindLibDir();
        if (lib is null) return;
        var oracle = Run(false);
        var actual = Run(true);
        Assert.Equal(oracle.Count, actual.Count);
        for (int i = 0; i < oracle.Count; i++)
        {
            Assert.Equal(oracle[i].Registers, actual[i].Registers);
            Assert.Equal(oracle[i].Memory, actual[i].Memory);
        }

        List<(ulong[] Registers, byte[] Memory)> Run(bool accelerated)
        {
            using var machine = new ArmMachine(lib, [AcapelaNative.Library]);
            machine.Relocate();
            var library = machine.Library(AcapelaNative.Library);
            if (accelerated) AcapelaArmAcceleration.Install(machine, library);
            var results = new List<(ulong[], byte[])>();
            uint table = machine.StaticAlloc(48);
            uint samples = machine.StaticAlloc(144 + 4) + 4;
            var random = new Random(470);
            for (int row = 0; row < 2; row++)
                for (int column = 0; column < 3; column++)
                    machine.W32(table + (uint)(row * 16 + column * 4), samples + (uint)((row * 3 + column) * 24));

            // Six arrays of three doubles. Include both sticky-flag and
            // guest-fallback cases (a different rounding mode).
            for (int n = 0; n < 60; n++)
            {
                double[] data = Enumerable.Range(0, 18).Select(_ => random.NextDouble() - 0.5).ToArray();
                machine.Write(samples, MemoryMarshal.AsBytes(data.AsSpan()));
                ulong input = BitConverter.DoubleToUInt64Bits(random.NextDouble() - 0.5);
                uint fpscr = (n % 3) switch { 0 => 0u, 1 => 0x10u, _ => 0x400000u };
                machine.Call((library.Base + AcapelaArmAcceleration.Butterworth) | 1,
                    [(uint)input, (uint)(input >> 32), table], fpscr: fpscr);
                results.Add(([.. Registers(0, 1, 2, 3), machine.ReadReg(UC_ARM_REG_FPSCR),
                    .. Enumerable.Range(3, 5).Select(i => machine.ReadReg(UC_ARM_REG_D8 - 8 + i))],
                    machine.Read(samples, sizeof(double) * 18)));
            }

            byte[] original = Enumerable.Range(0, 64).Select(n => (byte)(n * 3)).ToArray();
            uint region = machine.StaticAlloc(64);
            foreach (int destination in new[] { 3, 8, 13 })
            {
                foreach (uint count in new[] { 0u, 1u, 16u, 31u })
                {
                    machine.Write(region, original);
                    machine.Call((library.Base + 0x0002C57A) | 1, [region + (uint)destination, region + 8, count]);
                    results.Add((Registers(0, 1, 2, 3), machine.Read(region, 64)));
                }
            }

            uint queue = machine.StaticAlloc(36);
            foreach (bool write in new[] { false, true })
            {
                foreach (uint bytes in new[] { 2u, 4u, 8u })
                {
                    foreach (uint flags in new[] { 1u, 0x11u, 0x21u })
                    {
                        machine.Write(region, original);
                        uint read = write ? region : region + bytes * 2;
                        uint send = write ? region + bytes * 2 : region;
                        uint[] fields = [0, flags, bytes, 3, read, send, region, 0, 0];
                        machine.Write(queue, MemoryMarshal.AsBytes(fields.AsSpan()));
                        uint offset = write ? 0x000E19D2u : 0x000E1936u;
                        machine.Call((library.Base + offset) | 1, [queue, region + 48]);
                        results.Add((Registers(0, 1, 2, 3, 12), [.. machine.Read(queue, 36), .. machine.Read(region, 64)]));
                    }
                }
            }
            return results;

            ulong[] Registers(params int[] ids) => [.. ids.Select(id => (ulong)machine.Reg(id)),
                machine.ReadReg(UC_ARM_REG_CPSR) & 0xF0000000];
        }
    }

    [Fact]
    public void AcceleratedCoefficientLoopPreservesItsResumeRegistersAndAliasedInputs()
    {
        string? lib = ShippedLibraries.FindLibDir();
        if (lib is null) return;
        var oracle = Run(false);
        var actual = Run(true);
        for (int i = 0; i < oracle.Count; i++)
        {
            Assert.Equal(oracle[i].Registers, actual[i].Registers);
            Assert.Equal(oracle[i].Memory, actual[i].Memory);
        }

        List<(ulong[] Registers, byte[] Memory)> Run(bool accelerated)
        {
            using var machine = new ArmMachine(lib, [AcapelaNative.Library]);
            var library = machine.Library(AcapelaNative.Library);
            if (accelerated) AcapelaArmAcceleration.Install(machine, library);
            uint wrapper = machine.StaticAlloc(12);
            machine.W32(wrapper, 0xE1A04000);     // mov r4, r0
            machine.W32(wrapper + 4, 0xE51FF004); // ldr pc, [pc, #-4]
            machine.W32(wrapper + 8, (library.Base + 0x000E252C) | 1);
            machine.HookAddress(library.Base + 0x000E254E, _ => StubResult.Void);
            uint context = machine.StaticAlloc(0x1D4);
            machine.W32(context + 0x1D0, 4); // five double elements
            uint data = machine.StaticAlloc(80);
            var results = new List<(ulong[], byte[])>();
            foreach (uint sourceOffset in new[] { 0u, 8u, 40u })
            {
                double[] initial = [-0.0, 0.25, -0.5, 0.75, -1.03, 0.11, -0.22, 0.33, -0.44, 0.55];
                machine.Write(data, MemoryMarshal.AsBytes(initial.AsSpan()));
                uint[] stack = new uint[9];
                stack[8] = data + sourceOffset;
                machine.Call(wrapper, [context, 0, data], stack);
                results.Add(([.. new[] { 0, 1, 2, 3, 12 }.Select(id => (ulong)machine.Reg(id)),
                    machine.ReadReg(UC_ARM_REG_CPSR) & 0xF0000000, machine.ReadReg(UC_ARM_REG_FPSCR),
                    machine.ReadReg(UC_ARM_REG_D8 - 2), machine.ReadReg(UC_ARM_REG_D8 - 1)], machine.Read(data, 80)));
            }
            return results;
        }
    }

    [Fact]
    public void BatchedNoiseLoopRetainsTheNativeSeedsFilterHistoryAndSamples()
    {
        string? lib = ShippedLibraries.FindLibDir();
        if (lib is null) return;
        foreach (uint fpscr in new[] { 0u, 0x10u, 0x400000u })
        {
            var oracle = Run(false, fpscr);
            var actual = Run(true, fpscr);
            Assert.Equal(oracle.Registers, actual.Registers);
            Assert.Equal(oracle.Memory, actual.Memory);
        }

        (ulong[] Registers, byte[] Memory) Run(bool accelerated, uint fpscr)
        {
            using var machine = new ArmMachine(lib, [AcapelaNative.Library]);
            var library = machine.Library(AcapelaNative.Library);
            if (accelerated) AcapelaArmAcceleration.Install(machine, library);
            uint wrapper = machine.StaticAlloc(20);
            machine.W32(wrapper, 0xE1A05000);      // mov r5, r0
            machine.W32(wrapper + 4, 0xE1A06001);  // mov r6, r1
            machine.W32(wrapper + 8, 0xE1A07002);  // mov r7, r2
            machine.W32(wrapper + 12, 0xE51FF004); // ldr pc, [pc, #-4]
            machine.W32(wrapper + 16, (library.Base + 0x000E2C76) | 1);
            machine.HookAddress(library.Base + 0x000E2C9E, m =>
            {
                m.SetPc(ArmMachine.RetAddr);
                return StubResult.NoReturn;
            });

            uint seed = machine.StaticAlloc(8);
            machine.W32(seed, 10917);
            machine.W32(seed + 4, 1877);
            uint context = machine.StaticAlloc(0x188), table = machine.StaticAlloc(48);
            uint data = machine.StaticAlloc(144), output = machine.StaticAlloc(8 * 183);
            machine.W32(context + 0x6C, table);
            machine.W32(context + 0x130, output);
            machine.W32(context + 0x184, 183);
            for (int row = 0; row < 2; row++)
                for (int column = 0; column < 3; column++)
                    machine.W32(table + (uint)(row * 16 + column * 4), data + (uint)((row * 3 + column) * 24));
            double[] initial = [0.3, 0.4, -0.2, 0.2, 0.1, -0.1, -0.17, 0.26, 0.07,
                0.25, -0.4, 0.3, 0.1, -0.2, 0.4, 0.14, 0.01, -0.12];
            machine.Write(data, MemoryMarshal.AsBytes(initial.AsSpan()));
            machine.Call(wrapper, [seed, context, seed + 4], fpscr: fpscr);
            return ([.. Enumerable.Range(0, 5).Select(i => (ulong)machine.Reg(i)), machine.Lr,
                    machine.ReadReg(UC_ARM_REG_CPSR) & 0xF0000000, machine.ReadReg(UC_ARM_REG_FPSCR),
                    .. Enumerable.Range(3, 5).Select(i => machine.ReadReg(UC_ARM_REG_D8 - 8 + i))],
                [.. machine.Read(seed, 8), .. machine.Read(data, 144), .. machine.Read(output, 8 * 183)]);
        }
    }

    // Original guest execution with the block hook: 128736 samples, this SHA256.
    // FACE-GREETING-REPLAY-1's recorded Name_Normal scalar 1.1 gives Android rate 45.
    [Fact]
    public void RecordedLongNameRetainsTheOriginalNativeSamples()
    {
        var lib = ShippedLibraries.FindLibDir();
        var tts = TtsDir();
        if (lib is null || tts is null) return;
        using var native = new AcapelaTts(lib, javaGlue: true);
        native.LoadVoice(tts, "Ryan");
        var samples = native.Synthesize("Test Robin 30c00a", 45, 100, 100);
        Assert.Equal(128736, samples.Length);
        var bytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(samples.AsSpan());
        Assert.Equal("A44DF02968C651A40C5E0815D904A23EF3A9778E1E7088C01A2C0FFE3861E76C",
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)));
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
