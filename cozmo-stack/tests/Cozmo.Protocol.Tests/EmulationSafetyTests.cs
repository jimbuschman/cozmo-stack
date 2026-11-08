using System.Runtime.CompilerServices;
using Cozmo.Robot.Emulation;
using Xunit;

namespace Cozmo.Protocol.Tests;

public sealed class EmulationSafetyTests
{
    private static ArmMachine Machine() => new("", Array.Empty<string>());

    private static uint Code(ArmMachine machine, params uint[] instructions)
    {
        uint address = machine.StaticAlloc((uint)instructions.Length * 4);
        for (int i = 0; i < instructions.Length; i++) machine.W32(address + 4u * (uint)i, instructions[i]);
        return address;
    }

    [Fact]
    public void InfiniteGuestLoopTimesOutAndPermanentlyFaultsMachine()
    {
        using var machine = Machine();
        machine.CallTimeout = TimeSpan.FromMilliseconds(300);
        uint loop = Code(machine, 0xEAFFFFFE);
        var failure = Assert.Throws<EmulationAbort>(() => machine.Call(loop));
        Assert.Contains("M1-034", failure.Message);
        Assert.Contains($"0x{loop:X}", failure.Message);
        var again = Assert.Throws<EmulationAbort>(() => machine.Call(loop));
        Assert.Same(failure, again.InnerException);
    }

    [Fact]
    public void NestedLoopSharesDeadlineAndReportsNestedCall()
    {
        using var machine = Machine();
        machine.CallTimeout = TimeSpan.FromMilliseconds(300);
        uint loop = Code(machine, 0xEAFFFFFE);
        machine.Libc["nested"] = m => m.NestedCall(loop);
        uint entry = machine.Stub("nested");
        var failure = Assert.Throws<EmulationAbort>(() => machine.Call(entry));
        Assert.Contains($"0x{loop:X}", failure.Message);
        Assert.Contains("M1-034", failure.Message);
    }

    [Fact]
    public void OuterDeadlineSurvivesSuccessfulNestedCall()
    {
        using var machine = Machine();
        machine.CallTimeout = TimeSpan.FromMilliseconds(300);
        uint nested = Code(machine, 0xE12FFF1E); // bx lr
        bool nestedReturned = false;
        machine.Libc["nested"] = m =>
        {
            uint result = m.NestedCall(nested);
            nestedReturned = true;
            return result;
        };
        uint entry = Code(machine, 0xE12FFF30, 0xEAFFFFFE); // blx r0; loop
        var failure = Assert.Throws<EmulationAbort>(() => machine.Call(entry, new[] { machine.Stub("nested") }));
        Assert.True(nestedReturned);
        Assert.Contains($"0x{entry:X}", failure.Message);
        Assert.Contains("M1-034", failure.Message);
    }

    [Fact]
    public void CaughtNestedFailureStillFaultsOuterCall()
    {
        using var machine = Machine();
        machine.CallTimeout = TimeSpan.FromMilliseconds(300);
        uint loop = Code(machine, 0xEAFFFFFE);
        machine.Libc["nested"] = m =>
        {
            try { m.NestedCall(loop); }
            catch (EmulationAbort) { }
            return 0;
        };
        var failure = Assert.Throws<EmulationAbort>(() => machine.Call(machine.Stub("nested")));
        Assert.Contains("faulted", failure.Message);
        Assert.Throws<EmulationAbort>(() => machine.Call(loop));
    }

    [Fact]
    public void CaughtDeepFailureCannotReturnFromIntermediateNestedCall()
    {
        using var machine = Machine();
        machine.CallTimeout = TimeSpan.FromMilliseconds(300);
        uint loop = Code(machine, 0xEAFFFFFE);
        bool caught = false;
        bool intermediateReturned = false;
        machine.Libc["inner"] = m =>
        {
            try { m.NestedCall(loop); }
            catch (EmulationAbort) { caught = true; }
            return 0;
        };
        uint inner = machine.Stub("inner");
        machine.Libc["outer"] = m =>
        {
            uint result = m.NestedCall(inner);
            intermediateReturned = true;
            return result;
        };

        var failure = Assert.Throws<EmulationAbort>(() => machine.Call(machine.Stub("outer")));
        Assert.True(caught);
        Assert.False(intermediateReturned);
        Assert.Contains("faulted", failure.Message);
        Assert.Throws<EmulationAbort>(() => machine.Call(loop));
    }

    [Fact]
    public void DisposeAndReentrantCallAreRejectedDuringImport()
    {
        using var machine = Machine();
        uint good = Code(machine, 0xE12FFF1E);
        machine.Libc["checks"] = m =>
        {
            Assert.Throws<InvalidOperationException>(() => m.Dispose());
            Assert.Throws<InvalidOperationException>(() => m.Call(good));
            return m.NestedCall(good);
        };
        machine.Call(machine.Stub("checks"));
        machine.Dispose();
        Assert.Throws<ObjectDisposedException>(() => machine.R8(good));
        Assert.Throws<ObjectDisposedException>(() => machine.Call(good));
        Assert.Throws<ObjectDisposedException>(() => machine.HookAddress(good, _ => 0));
    }

    [Fact]
    public void ThrownHostImportFaultsMachine()
    {
        using var machine = Machine();
        var expected = new InvalidOperationException("import failed");
        machine.Libc["fail"] = _ => throw expected;
        var thrown = Assert.Throws<InvalidOperationException>(() => machine.Call(machine.Stub("fail")));
        Assert.Same(expected, thrown);
        Assert.Same(expected, Assert.Throws<EmulationAbort>(() => machine.Call(machine.Stub("fail"))).InnerException);
    }

    [Fact]
    public void SlowReturningImportExceedsDeadlineEvenAtReturnSentinel()
    {
        using var machine = Machine();
        machine.CallTimeout = TimeSpan.FromMilliseconds(100);
        machine.Libc["slow"] = _ => { Thread.Sleep(300); return 0; };
        uint entry = machine.Stub("slow");
        var failure = Assert.Throws<EmulationAbort>(() => machine.Call(entry));
        Assert.Contains("M1-034", failure.Message);
        Assert.Contains($"0x{entry:X}", failure.Message);
        Assert.Same(failure, Assert.Throws<EmulationAbort>(() => machine.Call(entry)).InnerException);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference UnownedMachine()
    {
        var machine = Machine();
        return new WeakReference(machine);
    }

    [Fact]
    public void NativeOwnerCanBeFinalizedWithoutDispose()
    {
        var weak = UnownedMachine();
        for (int i = 0; weak.IsAlive && i < 5; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        Assert.False(weak.IsAlive);
    }

    [Theory]
    [InlineData("", "x", -1)]
    [InlineData("", "%%", -1)]
    [InlineData("", "%c", -1)]
    [InlineData("", "%[a]", -1)]
    [InlineData("x", "y", 0)]
    [InlineData("", " ", 0)]
    [InlineData("1", "%*d%d", -1)]
    [InlineData("", "%n%d", -1)]
    [InlineData("1", "%d%d", 1)]
    public void ScanfDistinguishesInputFailure(string input, string format, int expected)
    {
        Assert.Equal(expected, ScanfSubset.Scan(input, format).Count);
    }

    [Fact]
    public void ScanfAcceptsWidthBeforeSuppressionAndShortCharacters()
    {
        var (count, values) = ScanfSubset.Scan("1234", "%2*d %d");
        Assert.Equal(1, count);
        Assert.Equal(34, (int)values[0].Integer);
        var (shortCount, chars) = ScanfSubset.Scan("a", "%2c");
        Assert.Equal(1, shortCount);
        Assert.Equal("a", chars[0].Text);
    }

    [Fact]
    public void AcapelaAtoiSaturatesAndSetsErrnoOnlyOnOverflow()
    {
        var lib = ShippedLibraries.FindLibDir();
        if (lib is null) return;
        using var native = new AcapelaNative(lib);
        var machine = native.Machine;
        uint input = machine.StaticAlloc(128);
        const uint atoi = 0x3001FBB4; // libacattsandroid.so PLT, through relocated GOT
        void Check(string text, int expected, uint expectedErrno)
        {
            machine.Write(input, System.Text.Encoding.ASCII.GetBytes(text + "\0"));
            machine.W32(machine.ErrnoAddress, 7);
            Assert.Equal(expected, unchecked((int)machine.Call(atoi, new[] { input })));
            Assert.Equal(expectedErrno, machine.R32(machine.ErrnoAddress));
        }
        Check("2147483647", int.MaxValue, 7);
        Check("2147483648", int.MaxValue, 34);
        Check("-2147483648", int.MinValue, 7);
        Check("-2147483649", int.MinValue, 34);
        Check("999999999999999999999999999", int.MaxValue, 34);
        Check("  +42x", 42, 7);
    }

    [Fact]
    public void AcapelaSscanfPltWritesOutputsAndRetainsPercentNOnInputFailure()
    {
        var lib = ShippedLibraries.FindLibDir();
        if (lib is null) return;
        using var native = new AcapelaNative(lib);
        var machine = native.Machine;
        uint input = machine.StaticAlloc(128);
        uint format = machine.StaticAlloc(128);
        uint output = machine.StaticAlloc(16);
        const uint sscanf = 0x3001FBCC; // libacattsandroid.so PLT, through relocated GOT
        void WriteString(uint address, string value) =>
            machine.Write(address, System.Text.Encoding.ASCII.GetBytes(value + "\0"));

        WriteString(input, "1234");
        WriteString(format, "%2*d %d");
        machine.W32(output, 0xAABBCCDD);
        Assert.Equal(1u, machine.Call(sscanf, new[] { input, format, output }));
        Assert.Equal(34u, machine.R32(output));

        WriteString(input, "");
        WriteString(format, "%n%d");
        machine.W32(output, 0xAABBCCDD);
        machine.W32(output + 4, 0x11223344);
        Assert.Equal(uint.MaxValue, machine.Call(sscanf, new[] { input, format, output, output + 4 }));
        Assert.Equal(0u, machine.R32(output));
        Assert.Equal(0x11223344u, machine.R32(output + 4));
    }
}
