using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static Cozmo.Robot.Emulation.UnicornNative;

namespace Cozmo.Robot.Emulation;

/// <summary>Host acceleration for measured hot blocks in the pinned libacattsandroid.so.
/// Guarded inputs use the same ARM instructions through the guest fallback.
/// </summary>
internal static class AcapelaArmAcceleration
{
    internal const uint MglsaThirdOrder = 0x000F2810;
    private const uint Inexact = 0x10;
    private const uint UnsupportedFpscr = ~(0xF8000000u | 0x9Fu);

    internal const uint Butterworth = 0x000F2B28;
    private const uint AccumulateCoefficients = 0x000E252C;

    internal static void Install(ArmMachine machine, LoadedLibrary library)
    {
        uint code = machine.MapCodePageAfter(library);
        // The copied first four bytes at these pinned starts are complete and
        // PC-independent. The fallback executes them before rejoining the guest.
        Redirect(MglsaThirdOrder, MglsaThirdOrderHook);
        Redirect(Butterworth, ButterworthHook);
        machine.HookAddress(library.Base + 0x000E2C76, m => FillNoiseHook(m, library.Base));
        machine.HookAddress(library.Base + AccumulateCoefficients,
            m => AccumulateCoefficientsHook(m, library.Base + 0x000E254F));
        Redirect(0x0002C57A, MoveBytesHook);
        Redirect(0x000E1936, m => QueueCopyHook(m, write: false));
        Redirect(0x000E19D2, m => QueueCopyHook(m, write: true));

        void Redirect(uint offset, ImportHandler accelerator)
        {
            uint entry = library.Base + offset;
            uint fast = code;
            uint fallback = code + 4;
            code += 12;
            machine.W16(fast, 0x4770); // bx lr
            machine.Copy(fallback, entry, 4);
            ThumbBranch(machine, fallback + 4, entry + 4);
            ThumbBranch(machine, entry, fast);
            machine.HookAddress(fast, m =>
            {
                if (accelerator(m).Kind == StubResult.K.NoReturn)
                    m.SetPc(fallback | 1);
                // On success, let the guest execute BX LR. Writing PC through
                // Unicorn would leave translated code at every hot return.
                return StubResult.NoReturn;
            });
        }
    }

    private static void ThumbBranch(ArmMachine machine, uint instruction, uint target)
    {
        int displacement = checked((int)((long)target - instruction - 4));
        if ((displacement & 1) != 0 || displacement is < -0x1000000 or >= 0x1000000)
            throw new ArgumentOutOfRangeException(nameof(target));
        uint bits = unchecked((uint)displacement);
        uint sign = (bits >> 24) & 1, i1 = (bits >> 23) & 1, i2 = (bits >> 22) & 1;
        uint j1 = 1 ^ i1 ^ sign, j2 = 1 ^ i2 ^ sign;
        machine.W16(instruction, (ushort)(0xF000 | (sign << 10) | ((bits >> 12) & 0x3FF)));
        machine.W16(instruction + 2, (ushort)(0x9000 | (j1 << 13) | (j2 << 11) | ((bits >> 1) & 0x7FF)));
    }

    private const int D0 = UC_ARM_REG_D8 - 8;
    private static readonly int[] VfpCallRegisters = [UC_ARM_REG_R0, UC_ARM_REG_R0 + 1, UC_ARM_REG_R0 + 2,
        UC_ARM_REG_R0 + 3, UC_ARM_REG_SP, UC_ARM_REG_CPSR, UC_ARM_REG_FPSCR];
    private static readonly int[] MglsaReturns = [UC_ARM_REG_R0, UC_ARM_REG_R0 + 1, UC_ARM_REG_R12,
        D0 + 1, D0 + 2, D0 + 3, D0 + 4, D0 + 5, D0 + 6, D0 + 7, UC_ARM_REG_CPSR, UC_ARM_REG_FPSCR];
    private static readonly int[] ButterworthReturns = [UC_ARM_REG_R0, UC_ARM_REG_R0 + 1, UC_ARM_REG_R0 + 2,
        UC_ARM_REG_R0 + 3, D0 + 3, D0 + 4, D0 + 5, D0 + 6, D0 + 7, UC_ARM_REG_CPSR, UC_ARM_REG_FPSCR];

    private static StubResult MglsaThirdOrderHook(ArmMachine machine)
    {
        Span<ulong> registers = stackalloc ulong[7];
        machine.ReadRegisters(VfpCallRegisters, registers);
        uint fpscr = (uint)registers[6];
        int order = unchecked((int)registers[3]);
        if ((fpscr & UnsupportedFpscr) != 0 || order is < 2 or > 128)
            return StubResult.NoReturn;

        uint sp = (uint)registers[4];
        double input = ArmMachine.Dbl((uint)registers[0], (uint)registers[1]);
        uint coefficientsAddress = (uint)registers[2];
        int stride = order + 2;
        uint coefficientsBytes = (uint)(order + 1) * sizeof(double);
        uint historyBytes = (uint)(3 * stride) * sizeof(double);
        double alpha, scale;
        Span<double> history;
        ReadOnlySpan<double> coefficients;
        try
        {
            var args = machine.Span(sp, 24);
            alpha = MemoryMarshal.Read<double>(args);
            scale = MemoryMarshal.Read<double>(args[8..]);
            uint historyAddress = MemoryMarshal.Read<uint>(args[20..]);
            // The guest reads overlapping arrays in instruction order. Use that
            // path if this call cannot operate on a private history copy.
            // Acapela's coefficient/history carriers are word-aligned. The
            // Windows x64 host permits unaligned double loads at those addresses.
            if (((historyAddress | coefficientsAddress) & 3) != 0 ||
                Overlaps(historyAddress, historyBytes, coefficientsAddress, coefficientsBytes) ||
                Overlaps(historyAddress, historyBytes, sp - 24, 48) ||
                Overlaps(coefficientsAddress, coefficientsBytes, sp - 24, 24))
                return StubResult.NoReturn;
            history = MemoryMarshal.Cast<byte, double>(machine.Span(historyAddress, historyBytes));
            coefficients = MemoryMarshal.Cast<byte, double>(machine.Span(coefficientsAddress, coefficientsBytes));
        }
        catch (EmulationAbort)
        {
            return StubResult.NoReturn;
        }

        if (!SafeRange(input) || !SafeRange(alpha) || !SafeRange(scale)) return StubResult.NoReturn;
        foreach (double value in coefficients)
            if (!SafeRange(value)) return StubResult.NoReturn;

        Span<double> scratch = stackalloc double[3 * stride];
        history.CopyTo(scratch);
        foreach (double value in scratch)
            if (!SafeRange(value)) return StubResult.NoReturn;

        // libacattsandroid.so 0xF2810..0xF28AA. Each VMLA/VMLS rounds the
        // multiply before the add/subtract, as Unicorn 2.1.4 gen_VMLA_dp/gen_VMLS_dp do.
        var fp = new VfpArithmetic(fpscr);
        double delta = 0, sum = 0, next = 0, delay = 0;
        for (int stage = 0; stage < 3; stage++)
        {
            var state = scratch.Slice(stage * stride, stride);
            delay = fp.Multiply(scale, state[0]);                       // F282C..F2830
            sum = fp.Add(delay, fp.Multiply(alpha, state[1]));         // F2834..F284E
            state[1] = sum;
            input = fp.Add(input, -fp.Multiply(sum, coefficients[1]));// F2852..F2856
            delay = fp.Add(delay, fp.Multiply(alpha, state[2]));       // F285A..F2862
            sum = 0;
            for (int index = 2; index <= order; index++)
            {
                double later = state[index + 1];
                double current = state[index];
                state[index] = delay;
                delta = fp.Add(later, -delay);                         // F2870..F287E
                next = fp.Add(current, fp.Multiply(alpha, delta));     // F2886
                sum = fp.Add(sum, fp.Multiply(next, coefficients[index])); // F2882..F288A
                delay = next;
            }
            input = fp.Add(input, -sum);                              // F2894..F28A2
            state[order + 1] = delay;
            state[0] = input;
        }
        if (!fp.Supported) return StubResult.NoReturn;

        scratch.CopyTo(history);
        ulong bits = BitConverter.DoubleToUInt64Bits(input);
        // Last SUBS decrements the stage counter from one to zero.
        ReadOnlySpan<ulong> results = [(uint)bits, bits >> 32, (uint)stride * sizeof(double),
            BitConverter.DoubleToUInt64Bits(delta), BitConverter.DoubleToUInt64Bits(alpha),
            BitConverter.DoubleToUInt64Bits(scale), BitConverter.DoubleToUInt64Bits(sum),
            BitConverter.DoubleToUInt64Bits(next), BitConverter.DoubleToUInt64Bits(delay), bits,
            (registers[5] & 0x0FFFFFFF) | 0x60000000, fp.Status];
        int updated = fp.Status == fpscr ? MglsaReturns.Length - 1 : MglsaReturns.Length;
        machine.WriteRegisters(MglsaReturns.AsSpan(0, updated), results[..updated]);
        return StubResult.Void;
    }

    private static StubResult ButterworthHook(ArmMachine machine)
    {
        Span<ulong> registers = stackalloc ulong[7];
        machine.ReadRegisters(VfpCallRegisters, registers);
        uint fpscr = (uint)registers[6];
        if ((fpscr & UnsupportedFpscr) != 0) return StubResult.NoReturn;
        double input = ArmMachine.Dbl((uint)registers[0], (uint)registers[1]);
        uint table = (uint)registers[2];
        uint spill = (uint)registers[4] - 16;
        Span<uint> addresses = stackalloc uint[6];
        Span<double> delay = stackalloc double[6];
        Span<double> coefficients = stackalloc double[12];
        if (!SafeRange(input) || !ReadButterworth(machine, table, spill, addresses, delay, coefficients))
            return StubResult.NoReturn;

        var fp = new VfpArithmetic(fpscr);
        var result = FilterNoise(input, delay, coefficients, ref fp);
        if (!fp.Supported) return StubResult.NoReturn;
        WriteButterworth(machine, addresses, delay);
        ulong bits = BitConverter.DoubleToUInt64Bits(result.Output);
        ReadOnlySpan<ulong> results = [(uint)bits, bits >> 32, table + 48, table + 48,
            BitConverter.DoubleToUInt64Bits(coefficients[8]), BitConverter.DoubleToUInt64Bits(result.LastState),
            BitConverter.DoubleToUInt64Bits(result.Feedforward), bits, BitConverter.DoubleToUInt64Bits(result.Feedback),
            (registers[5] & 0x0FFFFFFF) | 0x60000000, fp.Status];
        int updated = fp.Status == fpscr ? ButterworthReturns.Length - 1 : ButterworthReturns.Length;
        machine.WriteRegisters(ButterworthReturns.AsSpan(0, updated), results[..updated]);
        return StubResult.Void;
    }

    private static readonly int[] NoiseLoopRegisters = [UC_ARM_REG_R4, UC_ARM_REG_R4 + 1, UC_ARM_REG_R4 + 2,
        UC_ARM_REG_R4 + 3, UC_ARM_REG_SP, UC_ARM_REG_CPSR, UC_ARM_REG_FPSCR];
    private static readonly int[] NoiseLoopReturns = [UC_ARM_REG_R0, UC_ARM_REG_R0 + 1, UC_ARM_REG_R0 + 2,
        UC_ARM_REG_R0 + 3, UC_ARM_REG_R4, D0 + 3, D0 + 4, D0 + 5, D0 + 6, D0 + 7,
        UC_ARM_REG_CPSR, UC_ARM_REG_LR, UC_ARM_REG_FPSCR];

    private static StubResult FillNoiseHook(ArmMachine machine, uint libraryBase)
    {
        Span<ulong> registers = stackalloc ulong[7];
        machine.ReadRegisters(NoiseLoopRegisters, registers);
        uint fpscr = (uint)registers[6];
        if (registers[0] != 0 || (fpscr & UnsupportedFpscr) != 0) return StubResult.NoReturn;
        uint lowAddress = (uint)registers[1], context = (uint)registers[2], highAddress = (uint)registers[3];
        uint spill = (uint)registers[4] - 16;
        if (Overlaps(lowAddress, 4, highAddress, 4) || ((lowAddress | highAddress) & 3) != 0 ||
            Overlaps(lowAddress, 4, spill, 16) || Overlaps(highAddress, 4, spill, 16))
            return StubResult.NoReturn;

        Span<uint> addresses = stackalloc uint[6];
        Span<double> delay = stackalloc double[6];
        Span<double> coefficients = stackalloc double[12];
        uint low, high, table, count;
        Span<double> output;
        try
        {
            var fields = MemoryMarshal.Cast<byte, uint>(machine.Span(context, 0x188));
            count = fields[0x184 / 4];
            if (count is 0 or > 1024) return StubResult.NoReturn;
            uint outputAddress = fields[0x130 / 4], bytes = count * sizeof(double);
            table = fields[0x6C / 4];
            if ((outputAddress & 3) != 0 || Overlaps(outputAddress, bytes, spill, 16) ||
                Overlaps(outputAddress, bytes, table, 48) || Overlaps(outputAddress, bytes, lowAddress, 4) ||
                Overlaps(outputAddress, bytes, highAddress, 4) || Overlaps(lowAddress, 4, table, 48) ||
                Overlaps(highAddress, 4, table, 48) || !ReadButterworth(machine, table, spill, addresses, delay, coefficients))
                return StubResult.NoReturn;

            // The guest reloads these pointers and the loop bound at each sample.
            if (ChangesInput(context + 0x6C) || ChangesInput(context + 0x130) || ChangesInput(context + 0x184))
                return StubResult.NoReturn;
            bool ChangesInput(uint address) => Overlaps(outputAddress, bytes, address, 4) ||
                Overlaps(lowAddress, 4, address, 4) || Overlaps(highAddress, 4, address, 4);

            for (int i = 0; i < addresses.Length; i++)
            {
                uint address = addresses[i];
                if (Overlaps(outputAddress, bytes, address, 24) || Overlaps(lowAddress, 4, address, 24) ||
                    Overlaps(highAddress, 4, address, 24) || i % 3 == 2 &&
                    (Overlaps(address, 24, context + 0x6C, 4) || Overlaps(address, 24, context + 0x130, 4) ||
                    Overlaps(address, 24, context + 0x184, 4)))
                    return StubResult.NoReturn;
            }
            low = machine.R32(lowAddress);
            high = machine.R32(highAddress);
            output = MemoryMarshal.Cast<byte, double>(machine.Span(outputAddress, bytes));
        }
        catch (EmulationAbort)
        {
            return StubResult.NoReturn;
        }

        // 0xE2C76..0xE2C9C calls urandom (0xF2E4A..0xF2E6E) and
        // butterdf4 once per sample. Use the same 32-bit multiply-add and
        // two seed writes, and keep the filter's state across samples.
        var fp = new VfpArithmetic(fpscr);
        Span<double> scratch = stackalloc double[(int)count];
        FilterOutput result = default;
        for (int i = 0; i < scratch.Length; i++)
        {
            uint combined = unchecked(0x5A7Bu * low + high);
            high = combined >> 16;
            low = combined & 0xFFFFu;
            result = FilterNoise(low >> 11, delay, coefficients, ref fp);
            scratch[i] = result.Output;
        }
        if (!fp.Supported) return StubResult.NoReturn;

        machine.W32(lowAddress, low);
        machine.W32(highAddress, high);
        WriteButterworth(machine, addresses, delay);
        scratch.CopyTo(output);
        ulong bits = BitConverter.DoubleToUInt64Bits(result.Output);
        ReadOnlySpan<ulong> results = [(uint)bits, bits >> 32, table + 48, count, count,
            BitConverter.DoubleToUInt64Bits(coefficients[8]), BitConverter.DoubleToUInt64Bits(result.LastState),
            BitConverter.DoubleToUInt64Bits(result.Feedforward), bits, BitConverter.DoubleToUInt64Bits(result.Feedback),
            (registers[5] & 0x0FFFFFFF) | 0x60000000, libraryBase + 0x000E2C8F, fp.Status];
        int updated = fp.Status == fpscr ? NoiseLoopReturns.Length - 1 : NoiseLoopReturns.Length;
        machine.WriteRegisters(NoiseLoopReturns.AsSpan(0, updated), results[..updated]);
        machine.SetPc(libraryBase + 0x000E2C9F);
        return StubResult.NoReturn;
    }

    private static bool ReadButterworth(ArmMachine machine, uint table, uint spill, Span<uint> addresses,
        Span<double> delay, Span<double> coefficients)
    {
        if ((table & 3) != 0 || table > uint.MaxValue - 48 || Overlaps(table, 48, spill, 16)) return false;
        try
        {
            var pointers = MemoryMarshal.Cast<byte, uint>(machine.Span(table, 48));
            for (int stage = 0; stage < 2; stage++)
            {
                uint denominator = pointers[4 * stage], numerator = pointers[4 * stage + 1];
                uint history = pointers[4 * stage + 2];
                if (((denominator | numerator | history) & 3) != 0 || Overlaps(history, 24, table, 48) ||
                    Overlaps(history, 24, spill, 16) || Overlaps(denominator, 24, spill, 16) ||
                    Overlaps(numerator, 24, spill, 16) || Overlaps(pointers[2], 24, denominator, 24) ||
                    Overlaps(pointers[2], 24, numerator, 24) || Overlaps(pointers[6], 24, denominator, 24) ||
                    Overlaps(pointers[6], 24, numerator, 24))
                    return false;

                addresses[stage * 3] = denominator;
                addresses[stage * 3 + 1] = numerator;
                addresses[stage * 3 + 2] = history;
                MemoryMarshal.Cast<byte, double>(machine.Span(history, 24)).CopyTo(delay.Slice(stage * 3, 3));
                MemoryMarshal.Cast<byte, double>(machine.Span(denominator, 24)).CopyTo(coefficients.Slice(stage * 6, 3));
                MemoryMarshal.Cast<byte, double>(machine.Span(numerator, 24)).CopyTo(coefficients.Slice(stage * 6 + 3, 3));
            }
        }
        catch (EmulationAbort)
        {
            return false;
        }
        if (Overlaps(addresses[2], 24, addresses[5], 24)) return false;
        foreach (double value in coefficients)
            if (!SafeRange(value)) return false;
        foreach (double value in delay)
            if (!SafeRange(value)) return false;
        return true;
    }

    private static void WriteButterworth(ArmMachine machine, ReadOnlySpan<uint> addresses, ReadOnlySpan<double> delay)
    {
        MemoryMarshal.AsBytes(delay[..3]).CopyTo(machine.Span(addresses[2], 24));
        MemoryMarshal.AsBytes(delay[3..]).CopyTo(machine.Span(addresses[5], 24));
    }

    private readonly record struct FilterOutput(double Output, double LastState, double Feedforward, double Feedback);

    private static FilterOutput FilterNoise(double input, Span<double> delay, ReadOnlySpan<double> coefficients,
        ref VfpArithmetic fp)
    {
        double feedforward = 0, feedback = 0, lastState = 0;
        // 0xF2B28..0xF2B94 walks two 16-byte rows of three pointers.
        for (int stage = 0; stage < 2; stage++)
        {
            var state = delay.Slice(stage * 3, 3);
            var taps = coefficients.Slice(stage * 6, 6);
            feedback = fp.Multiply(input, taps[0]); // F2B4C..F2B56
            feedforward = 0;
            lastState = state[2];
            double earlier = state[0], later = state[1];
            for (int index = 1; index <= 2; index++)
            {
                feedforward = fp.Add(feedforward, fp.Multiply(taps[index + 3], earlier));
                feedback = fp.Add(feedback, -fp.Multiply(earlier, taps[index]));
                state[index] = earlier;
                earlier = later;
            }
            input = fp.Add(feedforward, feedback);
            state[0] = feedback;
        }
        return new(input, lastState, feedforward, feedback);
    }

    private static StubResult AccumulateCoefficientsHook(ArmMachine machine, uint continuation)
    {
        uint fpscr = (uint)machine.ReadReg(UC_ARM_REG_FPSCR);
        if ((fpscr & UnsupportedFpscr) != 0) return StubResult.NoReturn;
        uint destination = machine.Reg(2), source, orderAddress = machine.Reg(4) + 0x1D0;
        int count;
        Span<double> output;
        ReadOnlySpan<double> input;
        try
        {
            int order = unchecked((int)machine.R32(orderAddress));
            if (order is < 0 or > 128) return StubResult.NoReturn;
            count = order + 1;
            source = machine.R32(machine.Sp + 0x20);
            uint bytes = (uint)count * sizeof(double);
            if (((source | destination) & 3) != 0 || Overlaps(destination, bytes, orderAddress, 4) ||
                source != destination && Overlaps(source, bytes, destination, bytes))
                return StubResult.NoReturn;
            output = MemoryMarshal.Cast<byte, double>(machine.Span(destination, bytes));
            input = MemoryMarshal.Cast<byte, double>(machine.Span(source, bytes));
        }
        catch (EmulationAbort)
        {
            return StubResult.NoReturn;
        }

        // 0xE252C..0xE254C loads the vector base and accumulates order+1 doubles.
        var fp = new VfpArithmetic(fpscr);
        Span<double> scratch = stackalloc double[count];
        for (int i = 0; i < count; i++)
        {
            if (!SafeRange(output[i]) || !SafeRange(input[i])) return StubResult.NoReturn;
            scratch[i] = fp.Add(output[i], input[i]);
        }
        if (!fp.Supported) return StubResult.NoReturn;

        WriteDoubleRegister(machine, 6, input[^1]);
        WriteDoubleRegister(machine, 7, scratch[^1]);
        scratch.CopyTo(output);
        uint advance = (uint)count * sizeof(double);
        machine.SetReg(0, (uint)count - 1);
        machine.SetReg(1, source + advance);
        machine.SetReg(2, destination + advance);
        machine.SetReg(3, (uint)count);
        machine.SetReg(12, source);
        SetConditionFlags(machine, 0x20000000); // final CMP count, order
        if (fp.Status != fpscr) machine.WriteReg(UC_ARM_REG_FPSCR, fp.Status);
        machine.SetPc(continuation);
        return StubResult.NoReturn;
    }

    private static StubResult MoveBytesHook(ArmMachine machine)
    {
        // The library's own memmove at 0x2C57A..0x2C5A8 is byte-at-a-time
        // in both directions. Use the same overlap-safe copying as libc.
        uint destination = machine.Reg(0), source = machine.Reg(1), count = machine.Reg(2);
        if (source == destination) return StubResult.NoReturn;
        if (count > int.MaxValue || Overlaps(destination, count, machine.Sp - 8, 8) ||
            Overlaps(source, count, machine.Sp - 8, 8))
            return StubResult.NoReturn;
        try { machine.Copy(destination, source, count); }
        catch (EmulationAbort) { return StubResult.NoReturn; }
        if (source < destination)
        {
            machine.SetReg(2, uint.MaxValue);
            machine.SetReg(3, destination);
            SetConditionFlags(machine, 0x80000000);
        }
        else
        {
            machine.SetReg(3, count);
            SetConditionFlags(machine, 0x60000000);
        }
        return StubResult.Void;
    }

    private static readonly int[] QueueRegisters = [UC_ARM_REG_R0, UC_ARM_REG_R0 + 1, UC_ARM_REG_SP, UC_ARM_REG_CPSR, UC_ARM_REG_R0 + 3];
    private static readonly int[] QueueReturns = [UC_ARM_REG_R0, UC_ARM_REG_R0 + 1, UC_ARM_REG_R0 + 2,
        UC_ARM_REG_R0 + 3, UC_ARM_REG_R12, UC_ARM_REG_CPSR];

    private static StubResult QueueCopyHook(ArmMachine machine, bool write)
    {
        Span<ulong> registers = stackalloc ulong[5];
        machine.ReadRegisters(QueueRegisters, registers);
        uint queue = (uint)registers[0], item = (uint)registers[1], sp = (uint)registers[2];
        if (queue == 0 || item == 0 || (queue & 3) != 0) return StubResult.NoReturn;
        int cursorIndex = write ? 5 : 4;
        uint blocked = write ? 0x22u : 0x10u;
        uint readyFlag = write ? 0x20u : 0x10u;
        uint flags, next, other, destination, source, bytes;
        try
        {
            var fields = MemoryMarshal.Cast<byte, uint>(machine.Span(queue, 0x24));
            flags = fields[1];
            bytes = fields[2];
            uint begin = fields[6], cursor = fields[cursorIndex];
            other = fields[write ? 4 : 5];
            ulong end = (ulong)begin + (ulong)fields[3] * bytes;
            int spilled = write ? 16 : 24;
            if ((flags & 1) == 0 || (flags & blocked) != 0 || fields[7] != 0 ||
                bytes is not (2 or 4) || end > uint.MaxValue || cursor < begin || cursor >= end ||
                other < begin || other >= end || end - begin > int.MaxValue ||
                Overlaps(item, bytes, queue, 0x24) || Overlaps(cursor, bytes, queue, 0x24) ||
                Overlaps(item, bytes, sp - (uint)spilled, (uint)spilled) ||
                Overlaps(cursor, bytes, sp - (uint)spilled, (uint)spilled))
                return StubResult.NoReturn;

            // 0xE198C and 0xE1A18 use memcpy through PLT 0x1F9A4 (GOT 0x1E4EDC).
            // The common path uses two- and four-byte items with no copy callback.
            destination = write ? cursor : item;
            source = write ? item : cursor;
            machine.Copy(destination, source, bytes);

            // 0xE199A..0xE19C4 and 0xE1A1C..0xE1A46, respectively.
            next = cursor + bytes;
            if (next >= end) next = begin;
            if (next == other) flags |= readyFlag;
            flags &= ~(write ? 0x10u : 0x20u);
            fields[cursorIndex] = next;
            fields[1] = flags;
        }
        catch (EmulationAbort)
        {
            return StubResult.NoReturn;
        }

        // ARM memcpy preserves r0 and leaves r1 advanced on its forward path.
        uint sourceAfter = destination > source && destination < (ulong)source + bytes ? source : source + bytes;
        uint conditionFlags = next >= other ? 0x60000000u : 0x40000000u;
        if (((next ^ other) & (next ^ unchecked(next - other)) & 0x80000000) != 0)
            conditionFlags |= 0x10000000;
        machine.WriteRegisters(QueueReturns,
            [0, sourceAfter, next, write ? flags : registers[4], destination,
                (registers[3] & 0x0FFFFFFF) | conditionFlags]);
        return StubResult.Void;
    }

    private static void SetConditionFlags(ArmMachine machine, uint flags) =>
        machine.WriteReg(UC_ARM_REG_CPSR, (machine.ReadReg(UC_ARM_REG_CPSR) & 0x0FFFFFFF) | flags);

    private static void WriteDoubleRegister(ArmMachine machine, int register, double value) =>
        machine.WriteReg(UC_ARM_REG_D8 - 8 + register, BitConverter.DoubleToUInt64Bits(value));

    private static bool Overlaps(uint first, uint firstLength, uint second, uint secondLength) =>
        first < (ulong)second + secondLength && second < (ulong)first + firstLength;

    // Bound exponents well inside the normal range. Products, sums and the
    // inexact residuals cannot underflow or overflow inside these bounds.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool SafeRange(double value)
    {
        ulong abs = BitConverter.DoubleToUInt64Bits(value) & 0x7FFFFFFFFFFFFFFF;
        return abs == 0 || abs is >= 0x2D30000000000000 and < 0x52C0000000000000; // exponents -300..300
    }

    private struct VfpArithmetic(uint status)
    {
        internal uint Status = status;
        internal bool Supported = true;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal double Multiply(double left, double right)
        {
            double value = left * right;
            if (!SafeRange(value)) Supported = false;
            if ((Status & Inexact) == 0 && Math.FusedMultiplyAdd(left, right, -value) != 0)
                Status |= Inexact;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal double Add(double left, double right)
        {
            double value = left + right;
            if (!SafeRange(value)) Supported = false;
            if ((Status & Inexact) == 0)
            {
                double part = value - left;
                double error = (left - (value - part)) + (right - part);
                if (error != 0) Status |= Inexact;
            }
            return value;
        }
    }
}
