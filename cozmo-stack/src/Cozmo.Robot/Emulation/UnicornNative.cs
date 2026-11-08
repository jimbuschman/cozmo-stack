using System.Runtime.InteropServices;

namespace Cozmo.Robot.Emulation;

/// <summary>
/// P/Invoke surface of the vendored Unicorn 2.1.4 engine (<c>third-party/unicorn/unicorn.dll</c>, see its README).
/// Only what the ARM machine uses. Constants are Unicorn 2.1.4's <c>unicorn_const.py</c> / <c>arm_const.py</c>.
/// </summary>
internal static unsafe partial class UnicornNative
{
    private const string Dll = "unicorn";

    public const int UC_ARCH_ARM = 1;
    public const int UC_MODE_ARM = 0;
    public const uint UC_PROT_ALL = 7;

    public const int UC_HOOK_CODE = 4;
    public const int UC_HOOK_BLOCK = 8;
    public const int UC_HOOK_MEM_READ_UNMAPPED = 16;
    public const int UC_HOOK_MEM_WRITE_UNMAPPED = 32;
    public const int UC_HOOK_MEM_FETCH_UNMAPPED = 64;

    public const int UC_ARM_REG_CPSR = 3;
    public const int UC_ARM_REG_FPEXC = 4;
    public const int UC_ARM_REG_FPSCR = 6;
    public const int UC_ARM_REG_LR = 10;
    public const int UC_ARM_REG_PC = 11;
    public const int UC_ARM_REG_SP = 12;
    public const int UC_ARM_REG_D8 = 22;
    public const int UC_ARM_REG_R0 = 66;
    public const int UC_ARM_REG_R4 = 70;
    public const int UC_ARM_REG_R12 = 78;
    public const int UC_ARM_REG_C1_C0_2 = 111;

    [LibraryImport(Dll)] public static partial uint uc_version(uint* major, uint* minor);
    [LibraryImport(Dll)] public static partial int uc_open(int arch, int mode, nint* uc);
    [LibraryImport(Dll)] public static partial int uc_close(nint uc);
    [LibraryImport(Dll)] public static partial nint uc_strerror(int code);
    [LibraryImport(Dll)] public static partial int uc_mem_map_ptr(nint uc, ulong address, nuint size, uint perms, void* ptr);
    [LibraryImport(Dll)] public static partial int uc_reg_read(nint uc, int regid, void* value);
    [LibraryImport(Dll)] public static partial int uc_reg_write(nint uc, int regid, void* value);
    [LibraryImport(Dll)] public static partial int uc_emu_start(nint uc, ulong begin, ulong until, ulong timeout, nuint count);
    [LibraryImport(Dll)] public static partial int uc_emu_stop(nint uc);
    [LibraryImport(Dll)] public static partial int uc_context_alloc(nint uc, nint* context);
    [LibraryImport(Dll)] public static partial int uc_context_save(nint uc, nint context);
    [LibraryImport(Dll)] public static partial int uc_context_restore(nint uc, nint context);
    [LibraryImport(Dll)] public static partial int uc_context_free(nint context);
    [LibraryImport(Dll)] public static partial int uc_hook_add(nint uc, nuint* hh, int type, void* callback, void* userData, ulong begin, ulong end);

    public static string ErrorText(int code)
    {
        var p = uc_strerror(code);
        return p == 0 ? $"uc_err {code}" : $"{Marshal.PtrToStringAnsi(p)} (uc_err {code})";
    }

    public static void Check(int code, string what)
    {
        if (code != 0) throw new EmulationAbort($"unicorn {what}: {ErrorText(code)}");
    }

    private static bool _versionChecked;

    /// <summary>Refuses to run on any engine but the vendored 2.1 build the Python oracle uses.</summary>
    public static void EnsureVersion()
    {
        if (_versionChecked) return;
        UnicornLongjmp.Install();
        uint major, minor;
        uc_version(&major, &minor);
        if (major != 2 || minor != 1)
            throw new InvalidOperationException(
                $"unicorn.dll reports API {major}.{minor}; the emulator is pinned to the vendored Unicorn 2.1.4 (third-party/unicorn/README.md).");
        _versionChecked = true;
    }
}

/// <summary>The emulated code reached a point the machine does not model: abort(), an unmodelled import, an unmapped access,
/// a stand-in refusing its input. Mirrors <c>emu_elf.Abort</c>; nothing is silently faked.</summary>
public class EmulationAbort : Exception
{
    public EmulationAbort(string message) : base(message) { }
    public EmulationAbort(string message, Exception inner) : base(message, inner) { }
}

/// <summary>The emulated code called <c>__cxa_throw</c> (the C++ unwinder is not run). Mirrors <c>emu_elf.CxxThrow</c>.</summary>
public sealed class EmulatedCxxThrow : EmulationAbort
{
    public EmulatedCxxThrow(string typeName) : base($"C++ throw: {typeName}") { TypeName = typeName; }
    public string TypeName { get; }
}
