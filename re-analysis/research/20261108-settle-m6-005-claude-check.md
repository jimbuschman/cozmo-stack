# M6-005 Wwise name hash: settled EXACT_SOURCE (Claude check, manager, 2026-11-08)

**Verdict: M6-005 settled EXACT_SOURCE.** `WwiseHash.Of` (cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseHash.cs)
matches `AK::SoundEngine::GetIDFromString` 0x0099DB84 (ARM mode) step for step for every name the stack hashes.

## The native body, re-disassembled (ARM)

- **Null name:** 0x0099DB84..0x0099DB88 `subs r1,r0,#0; beq 0x0099DC4C` returns 0.
- **Copy:** `strlen` (0x0099DB98) gives L; `strncpy` copies `min(L+1, 0x103)` bytes into a 0x108-byte stack buffer
  (0x0099DB9C..0x0099DBC0) and stores a NUL after them (0x0099DBD8).
- **Lowercase:** over L bytes, a byte b with `b - 0x41 <= 0x19` (unsigned) becomes `b + 0x20`: ASCII 'A'..'Z' only
  (0x0099DBE4..0x0099DC00).
- **Hash:** start 0x811C9DC5 (0x0099DC08/0x0099DC1C); per byte the shift chain 0x0099DC20..0x0099DC38 multiplies by
  `h*(1+2^16)`, then `h + 2*`, `h + 8*`, `h + 8*`, `h + 2*`, which is `h * (2^24 + 403) = h * 16777619`, then
  `eor` with the byte (0x0099DC3C). Multiply then xor: FNV-1, not FNV-1a. L = 0 returns the offset basis
  (0x0099DC0C..0x0099DC14).

## The stack

The same constants, the same ASCII-only lowercasing, multiply then xor, stop at NUL, and at most 0x102 bytes. For
L <= 0x102 the native hashes exactly the L copied bytes, and so does the stack.

## Branches outside what the stack produces

- **L >= 0x103:** the native hashes L bytes of a buffer holding 0x103 copied bytes and a NUL, so it reads past the copy
  into the stack frame and beyond. That is undefined (a read past the object): the M1-034 standing rule covers it. The
  stack hashes the first 0x102 bytes and makes no claim about the native result. No shipped name comes near that
  length.
- **Null name** (native returns 0): C# callers never pass null.
- **Non-ASCII:** the native hashes the bytes; the stack hashes each UTF-16 char truncated to a byte. The production
  caller (SingingBehavior.cs:98) passes the `audioSwitchGroup`/`audioSwitch` names of shipped behaviour JSON, all
  ASCII. The two other callers are Cozmo.Conformance tooling.

## Whole path

M6-005 is a pure function. How each caller uses the id belongs to the caller's record (M9-002, M9-004 for singing).

## Tests (not circular)

`WwiseHashTests`: the six shipped bank names reproduce the BKHD ids of the shipped banks; two shipped Singing switch
names; case insensitivity; FNV-1a rejected.

## Progress

24 records settled (was 23).
