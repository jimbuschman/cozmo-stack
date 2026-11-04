using System.Runtime.InteropServices;

namespace Cozmo.Robot.Emulation;

/// <summary>
/// Makes the vendored unicorn.dll usable inside a .NET process on Windows.
///
/// <para>QEMU leaves the CPU loop with <c>longjmp</c> to a jmp_buf filled by <c>__intrinsic_setjmp</c> with Frame = 0 (no
/// unwinding). The .NET host executables are built with Control Flow Guard, so the process runs with CFG on, and then
/// VCRUNTIME140's <c>longjmp</c> refuses a Frame-0 buffer with <c>__fastfail(13)</c> (exit code 0xC0000409, observed at
/// VCRUNTIME140.dll+0x19B41, 14.51.36247.0). python.exe is not CFG-enabled, which is why the Python oracle never meets it.</para>
///
/// <para>The fix: unicorn.dll's import slot for <c>longjmp</c> is pointed at a 141-byte x64 routine that does what
/// VCRUNTIME's longjmp does on its non-CET path for a Frame-0 buffer when CFG is off (it has no shadow-stack path, so the host
/// must run without CET shadow stacks: Cozmo.Conformance sets CETCompat false, and Install refuses when they are enforced): restore rbx, rsp, rbp, rsi, rdi, r12-r15, MXCSR, the x87
/// control word and xmm6-xmm15 from the MSVC _JUMP_BUFFER, set eax = val (1 if val is 0) and jump to the saved rip. Only
/// unicorn.dll's own calls go through it. Implementation freedom: the emulated CPU's results do not depend on how the host
/// unwinds QEMU's loop.</para>
/// </summary>
internal static unsafe class UnicornLongjmp
{
    private static readonly byte[] Routine =
    {
        0x89, 0xD0,                                     // mov eax, edx
        0x85, 0xC0,                                     // test eax, eax
        0x75, 0x05,                                     // jnz +5
        0xB8, 0x01, 0x00, 0x00, 0x00,                   // mov eax, 1
        0x48, 0x8B, 0x59, 0x08,                         // mov rbx, [rcx+08h]
        0x48, 0x8B, 0x61, 0x10,                         // mov rsp, [rcx+10h]
        0x48, 0x8B, 0x69, 0x18,                         // mov rbp, [rcx+18h]
        0x48, 0x8B, 0x71, 0x20,                         // mov rsi, [rcx+20h]
        0x48, 0x8B, 0x79, 0x28,                         // mov rdi, [rcx+28h]
        0x4C, 0x8B, 0x61, 0x30,                         // mov r12, [rcx+30h]
        0x4C, 0x8B, 0x69, 0x38,                         // mov r13, [rcx+38h]
        0x4C, 0x8B, 0x71, 0x40,                         // mov r14, [rcx+40h]
        0x4C, 0x8B, 0x79, 0x48,                         // mov r15, [rcx+48h]
        0x0F, 0xAE, 0x51, 0x58,                         // ldmxcsr [rcx+58h]
        0xDB, 0xE2,                                     // fnclex
        0xD9, 0x69, 0x5C,                               // fldcw [rcx+5Ch]
        0xF3, 0x0F, 0x6F, 0x71, 0x60,                   // movdqu xmm6, [rcx+60h]
        0xF3, 0x0F, 0x6F, 0x79, 0x70,                   // movdqu xmm7, [rcx+70h]
        0xF3, 0x44, 0x0F, 0x6F, 0x81, 0x80, 0, 0, 0,    // movdqu xmm8, [rcx+80h]
        0xF3, 0x44, 0x0F, 0x6F, 0x89, 0x90, 0, 0, 0,    // movdqu xmm9, [rcx+90h]
        0xF3, 0x44, 0x0F, 0x6F, 0x91, 0xA0, 0, 0, 0,    // movdqu xmm10, [rcx+A0h]
        0xF3, 0x44, 0x0F, 0x6F, 0x99, 0xB0, 0, 0, 0,    // movdqu xmm11, [rcx+B0h]
        0xF3, 0x44, 0x0F, 0x6F, 0xA1, 0xC0, 0, 0, 0,    // movdqu xmm12, [rcx+C0h]
        0xF3, 0x44, 0x0F, 0x6F, 0xA9, 0xD0, 0, 0, 0,    // movdqu xmm13, [rcx+D0h]
        0xF3, 0x44, 0x0F, 0x6F, 0xB1, 0xE0, 0, 0, 0,    // movdqu xmm14, [rcx+E0h]
        0xF3, 0x44, 0x0F, 0x6F, 0xB9, 0xF0, 0, 0, 0,    // movdqu xmm15, [rcx+F0h]
        0xFF, 0x61, 0x50,                               // jmp qword [rcx+50h]
    };

    private const uint MEM_COMMIT_RESERVE = 0x3000, PAGE_EXECUTE_READWRITE = 0x40, PAGE_EXECUTE_READ = 0x20, PAGE_READWRITE = 0x04;

    [DllImport("kernel32", SetLastError = true)] private static extern void* VirtualAlloc(void* addr, nuint size, uint type, uint protect);
    [DllImport("kernel32", SetLastError = true)] private static extern int VirtualProtect(void* addr, nuint size, uint protect, uint* old);
    [DllImport("kernel32")] private static extern int FlushInstructionCache(nint process, void* addr, nuint size);
    [DllImport("kernel32")] private static extern nint GetCurrentProcess();

    [DllImport("kernel32", SetLastError = true)] private static extern int GetProcessMitigationPolicy(nint process, int policy, uint* buffer, nuint length);
    private const int ProcessUserShadowStackPolicy = 15;

    /// <summary>
    /// Whether CET user-mode shadow stacks are enforced for this process (PROCESS_MITIGATION_USER_SHADOW_STACK_POLICY bit 0,
    /// EnableUserShadowStack). VCRUNTIME's longjmp checks <c>rdsspq</c> and unwinds through RtlUnwindEx when a shadow stack is
    /// active (VCRUNTIME140 14.51.36247.0, 0x1D6DC..0x1D6E7); the routine above has no such path, so it must not run then.
    /// A Windows build without the policy reports failure, which means no shadow stack.
    /// </summary>
    internal static bool UserShadowStackEnforced()
    {
        if (!OperatingSystem.IsWindows()) return false;
        uint flags = 0;
        return GetProcessMitigationPolicy(GetCurrentProcess(), ProcessUserShadowStackPolicy, &flags, sizeof(uint)) != 0 && (flags & 1) != 0;
    }

    private static bool _done;
    private static readonly object Gate = new();

    public static void Install()
    {
        if (!OperatingSystem.IsWindows() || !Environment.Is64BitProcess) return;
        lock (Gate)
        {
            if (_done) return;
            if (UserShadowStackEnforced())
                throw new InvalidOperationException(
                    "The embedded emulator's longjmp replacement reproduces only VCRUNTIME's non-CET path, and this process runs with " +
                    "CET user shadow stacks enforced. Run it from an executable built with <CETCompat>false</CETCompat> " +
                    "(Cozmo.Conformance sets it), not from a CET-compatible host.");
            nint module = NativeLibrary.Load("unicorn", typeof(UnicornLongjmp).Assembly, null);
            ulong* slot = FindImportSlot((byte*)module, "VCRUNTIME140.dll", "longjmp");
            if (slot == null)
                throw new InvalidOperationException("unicorn.dll does not import VCRUNTIME140!longjmp; the vendored build is not the expected one");
            var code = (byte*)VirtualAlloc(null, 0x1000, MEM_COMMIT_RESERVE, PAGE_EXECUTE_READWRITE);
            if (code == null) throw new InvalidOperationException($"VirtualAlloc failed ({Marshal.GetLastWin32Error()})");
            Routine.AsSpan().CopyTo(new Span<byte>(code, Routine.Length));
            uint old;
            VirtualProtect(code, 0x1000, PAGE_EXECUTE_READ, &old);
            FlushInstructionCache(GetCurrentProcess(), code, 0x1000);
            if (VirtualProtect(slot, 8, PAGE_READWRITE, &old) == 0)
                throw new InvalidOperationException($"VirtualProtect on unicorn.dll's import table failed ({Marshal.GetLastWin32Error()})");
            *slot = (ulong)code;
            VirtualProtect(slot, 8, old, &old);
            _done = true;
        }
    }

    /// <summary>The import address table slot of <paramref name="dll"/>!<paramref name="name"/> in a loaded PE32+ image.</summary>
    private static ulong* FindImportSlot(byte* image, string dll, string name)
    {
        int peOff = *(int*)(image + 0x3C);
        byte* opt = image + peOff + 24;
        if (*(ushort*)opt != 0x20B) return null;                         // PE32+
        uint importRva = *(uint*)(opt + 112 + 8 * 1);
        for (byte* d = image + importRva; *(uint*)(d + 12) != 0; d += 20)
        {
            string lib = Marshal.PtrToStringAnsi((nint)(image + *(uint*)(d + 12)))!;
            if (!lib.Equals(dll, StringComparison.OrdinalIgnoreCase)) continue;
            ulong* names = (ulong*)(image + *(uint*)d);                  // OriginalFirstThunk
            ulong* iat = (ulong*)(image + *(uint*)(d + 16));             // FirstThunk
            for (int i = 0; names[i] != 0; i++)
            {
                if ((names[i] & 0x8000000000000000UL) != 0) continue;    // by ordinal
                string fn = Marshal.PtrToStringAnsi((nint)(image + (uint)names[i] + 2))!;
                if (fn == name) return iat + i;
            }
        }
        return null;
    }
}
