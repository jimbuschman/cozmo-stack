# EMU-RUNTIME-1: Claude check (manager, 2026-11-04)

**Verdict: PASS after one blocking fix, which the manager made. No record is settled or tagged: this is the runtime only, with no
production caller yet.**

The operator decided on 2026-11-03 that the shipped Acapela and OKAO libraries run embedded in the C# stack (option 1, not a Python
helper), and on 2026-11-04 that the vendored unicorn.dll and the voice fixture may go on GitHub. Personal use; see the memory
note in llm-interface-direction.

Built by a Claude implementer and checked by a separate verifier, who disassembled the longjmp routine against VCRUNTIME140
14.51.36247.0 and re-ran the Python probe independently.

## What it is

- **`cozmo-stack/src/Cozmo.Robot/Emulation/`** holds a C# port of `re-analysis/tools/emu/emu_elf.py` over unicorn.dll 2.1.4
  (P/Invoke):
  - an ELF32 reader with relocations;
  - SHA-256-pinned shipped libraries;
  - ArmMachine: heap, stubs, Call/NestedCall, NEON;
  - the libc stand-ins and a scanf subset;
  - AcapelaTts: credentials decoded by running the engine's GetUserid/GetPassword/GetLicense (0x0069F8DC, 0x0069F8F8,
    0x0069F914), the JNI natives in Java order, a read-only VFS.
- **Vendored** `cozmo-stack/third-party/unicorn/unicorn.dll` (GPLv2; hash in its README).

## Proof

- **Bit-exact:** `AcapelaTtsTests` synthesises the probe's text to 70,992 samples, bit-identical to the Python probe's
  `hello_cozmo.wav`. The verifier re-ran the probe and got the same sha256.
- **Licence check:** a wrong password fails the voice load with -12, a code that comes from the library itself.
- **Speed:** the voice loads in about 2 s; 3.2 s of speech takes about 8 s, about 2.5 times slower than real time.

## Blocking finding, fixed by the manager

The routine replacing VCRUNTIME's longjmp, which is needed because the .NET hosts run with Control Flow Guard, has no CET
shadow-stack path. VCRUNTIME checks `rdsspq` at 0x1D6DC..0x1D6E7.

- **The exe:** `Cozmo.Conformance.csproj` now sets `<CETCompat>false</CETCompat>`. The built exe's EX_DLLCHARACTERISTICS entry is
  now 0; it was 1.
- **The guard:** `UnicornLongjmp.Install` queries ProcessUserShadowStackPolicy and refuses with a clear message if shadow stacks
  are enforced.
- **The test:** `The_host_runs_without_CET_shadow_stacks`.

## Queued (verifier, not blocking; for the next job)

- Instruction budget or timeout per call (the hang rule).
- A faulted flag after an EmulationAbort, and a finalizer for native memory.
- atoi saturation.
- sscanf: EOF before the first conversion, and a width before `*`.
- Before SayText wiring:
  - nStop around nSpeak (Java speak/stop);
  - searchVoices' nGetVoiceInfo;
  - qsort ties (bionic is unstable for 7 or more elements);
  - srand48/lrand48 (seeded from time at 0x310FA..0x3110A; not exercised by this phrase).
- Runtime hash check of unicorn.dll.
- Records: the Java glue as a COMPATIBILITY_POLICY input, and M14-006, before any production caller.

## Progress

22 records settled, 2 of 178 finished (unchanged).
