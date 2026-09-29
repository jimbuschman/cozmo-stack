# B-M6b-2: the `0xA54F1C` ramp-target production path (`[sp+0x2e]`, `sp+0x30..0x3c`) and `0xA4BC58`'s argument list

Read-only extraction from `resources/lib/armeabi-v7a/libcozmoEngine.so` (3.4.0-1204, ARM mode).
Every row was read from the raw instruction stream with `re-analysis/tools/arm_disasm.py`; the
Ghidra tree (`re-analysis/decomp/libcozmoEngine/00a4/00a4bc58.c`, `00a5/00a54f1c.c`) was navigation
only. Addresses are ELF VAs (`.text` VA == file offset). Existing reports, the inventory and the
manifest are claims, not evidence.

Scope: the `0xA54F1C` call site `0xA55058`, the post-call gate/copy `0xA5505C..0xA5508C`, the four
ramps' use of `sp+0x30..0x3c`, and `0xA4BC58` in full. The downstream state machine is C16/C17.

## Frame facts (read from the instructions)

- `0xA4BC58` prologue: `0xA4BC58 push {r4,r5,r6,r7,r8,sb,sl,fp,lr}` (0x24 bytes),
  `0xA4BC60 vpush {d8}` (8 bytes), `0xA4BC80 sub sp,sp,#0x24`. With entry `sp=S`, the incoming
  stack arguments land at `[sp+0x50]` (arg5) `[sp+0x54]` (arg6) `[sp+0x58]` (arg7) `[sp+0x5c]`
  (arg8) `[sp+0x60]` (arg9) `[sp+0x64]` (arg10) `[sp+0x68]` (arg11) `[sp+0x6c]` (arg12). Confirmed
  by `0xA4BC8C ldrb r8,[sp,#0x50]`, `0xA4BC98 ldr r4,[sp,#0x58]`, `0xA4BC84/94/A0/A8` (arg8..11)
  and `0xA4BF38 ldr r3,[sp,#0x6c]` (arg12).
- `0xA54F1C` caller frame: `0xA55000 add sl,sp,#0x2e`, `0xA55004 add fp,sp,#0x2f`;
  `0xA54FF4 mov r4,r0` (voice), `0xA54F2C ldr r6,[r5,#0xc]` (`r5=[voice+0xD4]`=source, so
  `r6=[source+0xC]`), `0xA54F28 ldr r8,[r0,#0xf0]` (id), `0xA54F54 add sb,r6,#0xc`.

## Table

| step | what the original does | citation | record | classification |
|---|---|---|---|---|
| S1 | Caller reserves two output bytes in its own frame: `sl = sp+0x2e`, `fp = sp+0x2f`. | `0xA55000 add sl,sp,#0x2e`; `0xA55004 add fp,sp,#0x2f` | NEW | EXACT_SOURCE |
| S2 | Caller calls `source->vt+0x4C(source)` (`r0`=source); its return is kept for the callee's arg5. | `0xA55008 ldr r3,[r3,#0x4c]`; `0xA5500C blx r3`; `0xA55050 str r0,[sp]` | NEW | EXACT_SOURCE |
| S3 | Caller builds the call: `r1 = sb = [source+0xC]+0xC`; `r2 = id = [voice+0xF0]`; `r3 = gain` (s16); `[sp+4]=sl=&sp+0x2e` and `[sp+8]=fp=&sp+0x2f` via `stmib`; `[sp+0xc]/[sp+0x10]/[sp+0x14]/[sp+0x18]` = `&sp+0x30/&sp+0x34/&sp+0x38/&sp+0x3c`; `[sp+0x1c]` = `([bus+4]&0x10) ? &sp+0x48 : 0`; `[sp]=vt+0x4C return`. | `0xA55014 mov r1,sb`; `0xA55020 mov r2,r8`; `0xA55018 stmib sp,{sl,fp}`; `0xA5502C/30`, `0xA55034/38`, `0xA5503C/40`, `0xA55044/48`; `0xA5501C ands r3,r3,#0x10`; `0xA55024 addne r3,sp,#0x48`; `0xA55028 str r3,[sp,#0x1c]`; `0xA5504C vmov r3,s16`; `0xA55050 str r0,[sp]` | NEW | EXACT_SOURCE |
| S4 | `bl 0xA4BC58`. | `0xA55058 bl #0xa4bc58` | M6-022 (C12/C17) | EXACT_SOURCE |
| S5 | **`0xA4BC58` zeroes all four float outputs unconditionally at entry** (`*param_8..*param_11 = 0`), i.e. the caller's `sp+0x30/34/38/3c`. `lr` is 0 from `0xA4BC5C mov lr,#0`. | `0xA4BC5C mov lr,#0`; `0xA4BC84 ldr r3,[sp,#0x5c]`; `0xA4BC90 str lr,[r3]`; `0xA4BC94/9C`; `0xA4BCA0/A4`; `0xA4BCA8/AC` | NEW | EXACT_SOURCE |
| S6 | Callee register setup: `sl=voice` (param_1), `r7=param_2` (the caller's `sb = [source+0xC]+0xC`), `r6=id&0xff`, `s16=gain`, `r8=low byte of arg5` (the `source->vt+0x4C` return), `r4=arg7=&caller_sp+0x2f`, `sb=1`. | `0xA4BC64 mov sl,r0`; `0xA4BC6C mov r7,r1`; `0xA4BC74 uxtb r6,r2`; `0xA4BC78 vmov s16,r3`; `0xA4BC8C ldrb r8,[sp,#0x50]`; `0xA4BC98 ldr r4,[sp,#0x58]`; `0xA4BC70 mov sb,#1` | NEW | EXACT_SOURCE |
| S7 | **Aggregation over the connection list `[voice+0x28]`** (next `+0x28`): `r5` starts 1 and is set to **0** iff some connection `c` has `([c+0x6c] & 2) == 0`. `sb` accumulates bit2 of `[c+0x6c]`; `fp` tracks bit1 of the last examined connection. Empty list leaves `r5=1`, `fp=0`. | `0xA4BC68 ldr r0,[r0,#0x28]`; `0xA4BCB4 mov r5,sb`; `0xA4BCD0 ldrb r3,[r0,#0x6c]`; `0xA4BCD4 and r3,r3,#2`; `0xA4BCD8 cmp r3,#0`; `0xA4BCDC moveq r5,#0`; `0xA4BCE8/EC/F0`; `0xA4BC80 beq 0xA4C198`; `0xA4C19C mov r5,sb` | NEW | EXACT_SOURCE |
| S8 | `voice->vt+0x3C(voice)` is called; `r3 = [voice+0xCD]`. | `0xA4BCF4 ldr r3,[sl]`; `0xA4BCF8 mov r0,sl`; `0xA4BCFC ldr r3,[r3,#0x3c]`; `0xA4BD00 blx r3`; `0xA4BD04 ldrb r3,[sl,#0xcd]` | NEW | EXACT_SOURCE |
| S9 | **If `voice->vt+0x3C` returns non-zero, `r5` is forced to 1 on every sub-path** (`0xA4BD20` when `sb==0`, `0xA4C070`, `0xA4C080`). If it returns 0, `r5` is left at the S7 aggregation value on every sub-path. | `0xA4BD08 cmp r0,#0`; `0xA4BD0C beq 0xA4C000`; `0xA4BD1C cmp sb,#0`; `0xA4BD20 moveq r5,#1`; `0xA4C058`; `0xA4C070 mov r5,#1`; `0xA4C080 mov r5,#1` | NEW | EXACT_SOURCE |
| S10 | If `id != 0`, all four outputs are set to **100.0** (`0x42C80000`); if `id == 0` the code skips to the epilogue and they stay 0. | `0xA4BD74 cmp r6,#0`; `0xA4BD7C beq 0xA4BFD4`; `0xA4BD84 mov r3,#0`; `0xA4BD8C movt r3,#0x42c8`; `0xA4BD90/9C/A4/AC str r3,[...]` | M6-022 (C12) | EXACT_SOURCE |
| S11 | Per connection `fp`: set `[fp+0x50]=[r7+0x3c]`, `[fp+0x54]=sb` (=0), `[fp+0x58]=[r7+0x40]`, `[fp+0x5c]=sb` (=0); call `0xA5975C` (or `0xA5B9D0`+`0xA5993C`); then reduce each output to the **minimum** of the connection's `+0x50/+0x54/+0x58/+0x5c` (the four outputs are the per-connection A-LPF/B-LPF/A-HPF/B-HPF minima). | `0xA4BE80 ldr r2,[r7,#0x3c]`; `0xA4BE88 ldr r3,[r7,#0x40]`; `0xA4BE90 str sb,[fp,#0x54]`; `0xA4BE94 str r2,[fp,#0x50]`; `0xA4BE9C str r3,[fp,#0x58]`; `0xA4BEA0 str sb,[fp,#0x5c]`; `0xA4BEC4 bl 0xA5975C`; `0xA4BEC8..0xA4BEE0` (min +0x50); `0xA4BEE4..0xA4BEFC` (+0x54); `0xA4BF00..0xA4BF18` (+0x58); `0xA4BF1C..0xA4BF34` (+0x5c) | M6-022 / inventory gap 1.2 | EXACT_SOURCE |
| S12 | **Epilogue writes `[sp+0x2e]`: `*param_6 = r5`.** `r3 = [sp+0x54]` (arg6 = `&caller_sp+0x2e`), `strb r5,[r3]`. `r5` is 0 or 1. | `0xA4BFEC ldr r3,[sp,#0x54]`; `0xA4BFF0 strb r5,[r3]`; `0xA4BFF4..0xA4BFFC` | NEW | EXACT_SOURCE |
| S13 | Caller reads `[sp+0x2e]`; if zero it branches to `0xA55090`, skipping the copy. | `0xA5505C ldrb r3,[sp,#0x2e]`; `0xA55060 cmp r3,#0`; `0xA55064 beq 0xA55090` | NEW | EXACT_SOURCE |
| S14 | **The copy** (only when `[sp+0x2e] != 0`): `sp+0x30 = [voice+0x344]`, `sp+0x34 = [voice+0x514]`, `sp+0x38 = [voice+0x354]`, `sp+0x3c = [voice+0x524]`. (`r2=voice+0x510`, `r3=voice+0x520`, `ldr r1,[r2,#4]`, `ldr r2,[r3,#4]`.) | `0xA55068 add r2,r4,#0x510`; `0xA5506C add r3,r4,#0x520`; `0xA55070 ldr r0,[r4,#0x344]`; `0xA55074 ldr r1,[r2,#4]`; `0xA55078 ldr r2,[r3,#4]`; `0xA5507C ldr r3,[r4,#0x354]`; `0xA55080 str r0,[sp,#0x30]`; `0xA55084 str r1,[sp,#0x34]`; `0xA55088 str r2,[sp,#0x3c]`; `0xA5508C str r3,[sp,#0x38]` | NEW | EXACT_SOURCE |
| S15 | The four ramps use `sp+0x30/34/38/3c` as the compared (new) targets: ramp1 vs `[voice+0x344]`, ramp2 vs `[voice+0x514]`, ramp3 vs `[voice+0x354]`, ramp4 vs `[voice+0x524]`. | `0xA550D8 vldr s15,[sp,#0x30]`; `0xA550F8 vldr s14,[r4,#0x344]`; `0xA55108 vldr s15,[sp,#0x34]`; `0xA5513C vldr s14,[r3,#4]`; `0xA5514C vldr s15,[sp,#0x38]`; `0xA5516C vldr s14,[r4,#0x354]`; `0xA5517C vldr s15,[sp,#0x3c]`; `0xA551B0 vldr s14,[r3,#4]` | M6-022 (C17 V7-m) | EXACT_SOURCE |
| S16 | A second `0xA4BC58` call exists at `0xA5572C` (the `0xA555F0` completion path). It passes the same `&sp+0x2e`/`&sp+0x2f` (arg6/arg7) but all four float outputs point to **`sp+0x40`**, not `sp+0x30..0x3c`; it therefore rewrites `[sp+0x2e]` after the ramps have already run. | `0xA55724 stm sp,{r0,sl}`; `0xA5570C add r3,sp,#0x40`; `0xA55710/14/18 str r3,[sp,#0xc/0x10/0x14]`; `0xA5572C bl #0xa4bc58` | M6-022 (C15/C17) | EXACT_SOURCE |

## Direct answers

**Q1 - who writes `[sp+0x2e]`.** It is the **6th argument (`param_6`), a pointer to the caller's byte**, written back by `0xA4BC58` at `0xA4BFF0 strb r5,[r3]` with `r3 = [sp+0x54]` (`0xA4BFEC`). It is not a callee local. Value: `r5` is exactly 0 or 1. The second call at `0xA5572C` writes it again through the same pointer (`0xA55724 stm sp,{r0,sl}` stores `sl=&sp+0x2e` as arg6). The value read at the ramp gate `0xA5505C` is the **first** call's output.

The first call returns 0 exactly when **all three** hold:
1. the connection list `[voice+0x28]` is non-empty, and
2. some connection `c` has `([c+0x6c] & 2) == 0` (`0xA4BCDC moveq r5,#0`), and
3. `voice->vt+0x3C(voice)` returns 0 (`0xA4BD08/0C`); every non-zero return forces `r5=1` (`0xA4BD20`, `0xA4C070`, `0xA4C080`).

Otherwise it is 1. There is no `str ... [sp,#0x2e]` anywhere in `0xA54F1C`; the only writers are the two `0xA4BC58` calls.

**Q2 - the four values `sp+0x30..0x3c`.** The caller never writes values there; it only stores their addresses in the argument slots (`0xA5502C..0xA55048`). `0xA4BC58` initialises them to 0 unconditionally (S5), then (id != 0) to 100.0 (S10), then to the per-connection minima of `conn+0x50/54/58/5c` (S11). When `[sp+0x2e]==0` the copy (S14) is skipped and they hold whatever `0xA4BC58` left: the connection minima when the per-connection min loop ran, otherwise 0. They are never left at their pre-call uninitialised contents.

**Q3 - `0xA4BC58` argument list.** `param_1 r0 = voice` (in); `param_2 r1 = caller's sb = [source+0xC]+0xC` (in); `param_3 r2 = id = [voice+0xF0]` (in); `param_4 r3 = gain` float (in); `param_5 [sp+0] = source->vt+0x4C return` (in); `param_6 [sp+4] = &caller_sp+0x2e` (out byte 0/1); `param_7 [sp+8] = &caller_sp+0x2f` (out byte 0/1, "P2F"); `param_8..11 [sp+0xc..0x18] = &caller_sp+0x30/0x34/0x38/0x3c` (out floats); `param_12 [sp+0x1c] = ([bus+4]&0x10) ? &[bus+0x140] : 0` (in context pointer, passed to `0xA5D70C`). The four float outputs are the running minima of `[conn+0x50]`, `[conn+0x54]`, `[conn+0x58]`, `[conn+0x5c]`, starting at 100.0 and zeroed first; the inventory gap 1.2 names them A-LPF, B-LPF, A-HPF, B-HPF. So yes: they are the connection-gain minima, not something else.

**Q4 - the copy.** Condition `0xA5505C ldrb r3,[sp,#0x2e]` / `0xA55060 cmp r3,#0` / `0xA55064 beq 0xA55090`. Body `0xA55068..0xA5508C` as row S14. The four fields are `+0x344`, `+0x514`, `+0x354`, `+0x524` into `sp+0x30`, `sp+0x34`, `sp+0x38`, `sp+0x3c` respectively.

## UNKNOWN

1. **The Wwise meaning of `[sp+0x2e]` and `[sp+0x2f]`.** The values and writers are exact; the binary carries no name. M6-022 already lists the semantic names of `E4`/`E0`/`P2F` as UNKNOWN; `[sp+0x2e]` is a sixth such flag. The mechanism is: `[sp+0x2e]!=0` re-syncs the ramp targets to the stored voice targets (so the ramp gates compare equal and no ramp moves); `[sp+0x2e]==0` lets the connection minima become the new ramp targets.
2. **The meaning of connection bit1 `[conn+0x6c] & 2`** that decides `[sp+0x2e]`. Inventory gap 3.3 calls it "below volume threshold" from `0xA4B068..0xA4B0A4`; the threshold value and the exact semantic remain as recorded there. This pass reads the branch, not the name.
3. **Whether the shipped Cozmo media exercises the `[sp+0x2e]==0` (copy-skip) path**, and each of its sub-branches. The `.so` alone cannot decide this; it needs the banks/capture. C17 already marks the analogous `P2F==0` reachability UNKNOWN.
4. **The identity of `param_2` (the caller's `sb`)**. The report calls it "bus+0xC" and the source-classes pass says `[source+0xC]` is the PBI; the object class is not named in the binary. Not required for the four questions.
5. **`param_12`'s consumer semantics** (`0xA5D70C` with `&[bus+0x140]` or 0). The argument is exact; the callee's meaning is not read here.

## Existing records contradicted / too weak

- **`re-analysis/research/20260928-B-M6b-voice-callees.md` Q4 line 162 - contradicted.** It says the copy is "`[voice+0x344]`, `[voice+0x524]`, `[voice+0x524]`, `[voice+0x354]` into `sp+0x30/+0x34/+0x38/+0x3C`". The instructions are `sp+0x30=[voice+0x344]`, `sp+0x34=[voice+0x514]`, `sp+0x38=[voice+0x354]`, `sp+0x3c=[voice+0x524]` (`0xA55074 ldr r1,[r2,#4]` with `r2=voice+0x510`; `0xA55078 ldr r2,[r3,#4]` with `r3=voice+0x520`). The report's own lines 170-171 already use `[voice+0x514?]`/`[voice+0x524]`, so line 162 is the stale one. C17 resolved the `?` to `+0x514`.
- **`20260928-B-M6b-voice-callees.md` Q4 line 253 - overstated.** It lists "[sp+0x5c], [sp+0x60], [sp+0x64], [sp+0x68] and [sp+0x6c]" as "the four output floats" and says they are initialised to 0. Only `[sp+0x5c..0x68]` (param_8..11) are outputs and are zeroed; `[sp+0x6c]` is `param_12`, an input pointer, and is not zeroed.
- **`20260928-B-M6b-voice-callees.md` Q4 line 264 - contradicted.** It says `0xA4BE80` copies "`[voice+0x3c]/[voice+0x40]`". The operands are `[r7,#0x3c]`/`[r7,#0x40]` where `r7 = param_2` (the caller's `sb = [source+0xC]+0xC`, set at `0xA4BC6C mov r7,r1`), not `voice`. (The inventory gap 1.2 says `ctx+0x3C/+0x40`; that is the callee's `param_2`.)
- **M6-022 provenance is incomplete for this sub-path.** Its evidence cites C17 for "the four parameter ramps", but C17's V7-m only covers the ramp bodies and their gates; it does not name who writes `sp+0x30..0x3c` or the `[sp+0x2e]` gate. Per the "a settled record owns its whole production path" rule, the ramp-target producer (S5, S7, S9-S14) needs its own rows before M6-022's ramp claim is fully backed. This pass supplies them.
- **Inventory `M6-wwise-bank.md` gap 3.3 (line 846) - now closable.** It says "the meaning of the output byte `[sp+0x54]` for the caller is not read"; the writer (`0xA4BFF0`), the pointer (arg6) and the exact condition are read here. Note its separate claim "A skip leaves the previous gains in place; it does not zero them" refers to the per-connection gain/pan loop, not to the four float outputs, which are zeroed unconditionally at entry.

## Open questions for the manager

1. Should the `[sp+0x2e]` flag get a manifest row of its own under M6-022 (writer, condition, two-call aliasing), or be folded into V7-j/V7-m? The condition is exact but the name is not in the binary.
2. Should the second call's `sp+0x40` aliasing (S16) get its own row, since it is part of `0xA4BC58`'s in-function production path and re-writes `[sp+0x2e]`/`[sp+0x2f]` after the ramps?
3. Is the `voice-callees.md` line 162/253/264 correction a research-report edit (authority 6), or only a note? No manifest status changes are needed for the corrections themselves.
