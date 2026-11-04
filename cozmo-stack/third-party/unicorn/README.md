# Unicorn CPU emulator (native, win-x64)

`unicorn.dll` is the Unicorn 2.1.4 engine, vendored for the embedded ARM emulator in
`cozmo-stack/src/Cozmo.Robot/Emulation/` (job EMU-RUNTIME-1, operator decision 2026-11-03: the shipped proprietary
libraries run inside the stack under an embedded emulator). The stack calls it through P/Invoke
(`Emulation/UnicornNative.cs`); there is no managed binding package.

| | |
| --- | --- |
| Upstream | https://github.com/unicorn-engine/unicorn |
| Version | 2.1.4 (`uc_version` reports API 2.1) |
| Retrieved from | the PyPI wheel `unicorn-2.1.4-cp37-abi3-win_amd64.whl`, file `unicorn/lib/unicorn.dll` |
| Size | 9,598,976 bytes |
| SHA-256 | `010d85dce84b8f6ffaec3d5420f1b5f7768f859eb831be574c7da23e656ee8f1` |
| Wheel RECORD hash | `sha256=AQ2F3OhLj2_67D1UIPG193aPhZ64Mb5XTH2iPmVu6PE` (the same digest, base64url) |
| Needs | `VCRUNTIME140.dll` and the Universal CRT (present on current Windows) |
| Licence | GPLv2 (Unicorn core, QEMU-derived) |

## Why this file and not a NuGet package

The official .NET binding, `UnicornEngine.Unicorn` on NuGet (2.1.3, 2025-03-07; 2.1.4-a40db6c, 2025-09-09), ships
native libraries for linux-x64, linux-arm64, linux-ppc64le and osx-x64 only, with no win-x64 `unicorn.dll`, and it
pulls in FSharp.Core. This DLL is byte-for-byte the one the Python reference (`re-analysis/tools/emu/emu_elf.py`, the
Acapela probe `.scratch/probes/tts_probe.py`) runs, so the C# port and the Python oracle execute the same CPU model.

The stack refuses to start the emulator when the loaded engine does not report API 2.1 (`UnicornNative.EnsureVersion`).

## Use

Personal use at home, not distributed (operator, 2026-11-03).
