# I-M6b citation check: B1 voice-engine rows B1-V1..V30

Read-only verifier pass, 2026-09-28. Checked every row of
`re-analysis/research/20260927-B1-voice-engine-extraction.md` against the instructions in
`resources/lib/armeabi-v7a/libcozmoEngine.so` (3.4.0-1204, ARM mode). GOT base confirmed as
`0x104028C` from both `0x9AF8C4+0x6909C8` and `0xA44D60+0x5FB52C`, plus raw-word reads of the
GOT slots. The Ghidra tree is a navigation aid only; every verdict below is from the `.so`.

## Verdicts

| row | verdict | what the instructions actually show |
|---|---|---|
| B1-V1 | PASS | `0xA57FF8: push {r3,lr}; bl 0xA57D64; pop {r3,lr}; b 0xA44D4C`. Exactly the wrapper claimed. |
| B1-V2 | PARTIAL | Bail checks, the `0x593E58` call, the two `0xA58008` calls and `0x9EA66C` all match. But `vt+0x84` is called **three** times (`0xA57E64`, `0xA57E9C`, `0xA57ED0`), not "x2"; Appendix 1 lists only two. Appendix 1 also calls `0xA58008` a "3-instruction thunk" — it is a 14-instruction trampoline. The `vt+0x98` claim is correct. |
| B1-V3 | PASS | `[r5-0x1D4]`→`0x10400B8`→`0x108DAFC`, head `[r3+8]`, next `[r4+4]`, `vt+0x2c`/`vt+0x30` on `[node+0x70]`; throttle uses `[r5-0x12C]`→`0x1040160`→`0x108DA9C` and `[r5-0x1BC]`→`0x10400D0`→`0x108D870`, tick `+0x4c`, `bls` skip when last≠0 && cur−last≤8. All confirmed. |
| B1-V4 | PASS | `0xA44DD8..`: `[r5-0x1CC]`=`0x10400C0`→`0x108DB08`, `ldrb`; if 0 `r4=1`, else `[r5-0x1C4]`=`0x10400C8`→`0x1052430` low byte = 1. `bl 0xA44948` then `b 0xA44C18` with `r0=r4`. Both paths give 1. |
| B1-V5 | PASS | `bl 0x9D3CC0` (with `ldrh`), `bl 0xA43D24`, `bl 0xA39564`; `0xA43D24` base `0x108DF54`, head `[+0x14]`=`0x108DF68`, `bl 0xA55750` for state==1, constants `0x3D4CCCCD`/`0xC2140000`, stores `+0x88`/`+0x8C`. |
| B1-V6 | PASS | base `0x108DF54`, head `+0x14`, next `+0xD0`, state `+0xDC`, `+0xD4`→`+0xC` bus, `+0xD8` pending; count `[base+0xC]` decremented at `0xA44B28`. |
| B1-V7 | PASS | `0xA44B50: bl 0xA54F1C` with `r0=voice`, `r1=sp+0xc`; `0xA44B58: tst r0, [sp+4]`. |
| B1-V8 | PASS | Order and operands match: `vt+0x38` (`0xA44670`), `vt+0x3c` (`0xA446B8`), `0xA4C60C` (`0xA446E8`), `0xA56E00` (`0xA446F4`), `0xA548C0` (`0xA44700`), `0xA53134` (`0xA44738`), `0xA52D4C` (`0xA4477C`), `vt+0x30` on `[r7+0xD4]` (`0xA447A4`), `0xA03E8C` (`0xA447D4`), two `0xA4FBEC` (`0xA448A8`, `0xA448EC`). Note the `r0` at entry is the params buffer; `r7=[r0+0x30]` is the source. |
| B1-V9 | PARTIAL | `0xA548C0` reads `[r0+0xD4]` then `[r3+0xC]` into `r4`; the row calls that object the **bus**. The `vt+0x58` call is on `r4` (`0xA54904`), i.e. **on the bus, not the source**. Everything else (0x100000 bit at `[r4+4]`, `params+0x18 != -1` → `0xA05574`, store into `+0xE` with `strhlo`, `+0x2C=1`, `0xA56650` on `[r6+0xD8]`) matches. |
| B1-V10 | PASS | `r0=[arg0+4]`, `vt+0x20`, store `[r1+0xC]`→`[r4+0x48]`; `0xA47384` at `0xA53180`; pitch flag from `[bus+0x1BE] & 0x380`; `[r4+0x6E]==0 && [r4+0xB8]!=0`→`params+0x28=0x11`; else tail-call `0xA52D4C`. |
| B1-V11 | PARTIAL | The copy `0xA52D88..0xA52DA4` moves **10 words = 40 bytes**, not a "24-byte" buffer. `[source+0x6E]`, `0xA47178` at `0xA52E30`, `0xA5268C` at `0xA52E50`, `0xA4721C`/`0xA47224` at `0xA52EDC`/`0xA52F94` all match. The `0xA69A70` call is at `0xA5303C`, not `0xA53038`. |
| B1-V12 | PASS | Slots at `[r7+0x36C+4*slot]` (slot 4..1 → `0x37C..0x370`), `vt+0x38`/`vt+0x3c`; bypass bytes `[bus+0x1BD]`/`[bus+0x1BE]` read at `0xA52DBC`/`0xA52DF0`. |
| B1-V13 | PASS | `0xA446E8` (`0xA4C60C(r7+0x1C0)`), `0xA446F4` (`0xA56E00(r7+0x380)`). |
| B1-V14 | PASS | Flow matches: valid-frames-0 return, `[r0+0x1BC]==4`→1, `[r0+0x68]=0x2D`, zero-pad from buffer+valid, `[r0+0x1A8]`/`[+0xC]` → `vt+0x28` at `0xA4FD04`, else `0xA45E9C` at `0xA4FD6C` with the products from `0xA4FD5C`/`0xA4FD60`. Note the title's arg names `(source, voice, bus, gains)` are inverted relative to the body: `r0` is the bus, `r1` is the source buffer. |
| B1-V15 | PASS | `0xA44ABC bl 0xA55D04(voice,0)` (also clears `[voice+0xD8]`), `0xA44AD0 bl 0xA55A84(voice,pending,1,0)`, return==1 → `0xA44BE8 bl 0xA54A30`, return==1 → `0xA44BF8 bl 0xA56478(pending)`. |
| B1-V16 | PARTIAL | `vt+0x48` at `0xA44AE8`, state==2 unlink at `0xA44AF8..0xA44B2C`, `0x9D40C4` at `0xA44B30`. But **no connection-destruction call exists** in `0xA44AF8..0xA44B30` (only the two `bl` sites in the whole range are `0xA44AE8 blx r3` and `0xA44B30 bl 0x9D40C4`); the "its connections are destroyed in the same pass" claim is not supported by the cited range. |
| B1-V17 | PASS | arg!=0 branch, last-to-first array walk, `0xA4FEF8` (`0xA44CD8`), `[bus+0x1C8]`→`0xA4F9E0` (`0xA44CAC`), else validFrames→device-list walk matching `[bus+0x28]/[bus+0x2C]`→`0x9E9E78` (`0xA44D2C`), `0xA4F36C` (`0xA44CB8`); converge on `0x9E9F08` walk, `[g]=[g+4]`, `0xA43F64` (`0xA44C6C`). |
| B1-V18 | PARTIAL | `([r0+0x1B8]&0xC)!=4`→`0xA4F754` (`0xA4FF3C`), then `[r0+0x1A8]`/`[+0xC]`→`vt+0x2C`, `0xA4FD84` (`0xA4FF74`), `0xA4D994` (`0xA4FFB0`), else `0xA50120`. But the `0xA4E974`-per-slot / `0x9CC2AC` / type-3 `0x9CC4D8` / `vt+0x1C` detail lives inside `0xA4F754` (its `bl 0xA4E974` is at `0xA4F964`), **outside** the cited range. Also the mask is `0xF` only when bit2 is clear (`0xA4FF30 tst r7,#4; moveq r1,#0xf; movne r1,#0`). |
| B1-V19 | PASS | `[r0+0x1BC]==1` gates the `0..3` loop (`0xA4FD90`); in-place `vt+0x20` on `r0+0x60`, out-of-place when `[slot+0x138]` set (`0xA4FE74 mov r8,r7`); bypass → `vt+0xC` Reset (`0xA4FEC8`). |
| B1-V20 | PASS | Predicate `[bus+0x1BC]!=1 && [bus+0x1C0]==0` at `0xA44030..0xA44044`; destroy path `0xA43FBC bl 0xA4F6F0` (conditional on `[bus+0x1C8]!=0`), `0xA4EED8`, `0xA7A988`, array removal; keep path clears `[bus+0x1CC]` bit0 (`0xA44058 bfc r3,#0,#1`). |
| B1-V21 | PASS | `0xA0188C` at `0xA38480`, `[item+8]==4` at `0xA38488`, `0x9D3470` at `0xA384CC`, `vt+0x10` at `0xA384E0`, `vt+4` at `0xA384FC`, `0xA7A988` at `0xA38508`. |
| B1-V22 | PASS | `0x9D4778: b 0x9EC38C`; `0x9EC38C: mov r0,#0; b 0x9EBE6C`. |
| B1-V23 | **FAIL** | The gate logic is inverted. `0x9AF91C: bne 0x9AFAC0` means `byte[0x108DB08]` is read **when it is non-zero**, and `0x9AFACC: cmp r3,#0; 0x9AFAD0: bne 0x9AF920` gives `count=1` **when `byte[0x1052430]==0`** (`0x9AFAD8: mov r5,#1`), clock when it is 1. The row says "if it is 0 then read byte[0x1052430] (==1) -> count = 1 else clock-paced". Both the condition and the outcome are reversed. `0x9A9B40`'s helper has the identical (correct) structure at `0x9A9B5C/0x9A9B70/0x9A9C48`. The runtime conclusion — flag `0x108DAF0` is zero so the device path is taken — is unaffected **by this row**, but see the gap1 report: the gate bytes have writers, so that conclusion does not stand either. |
| B1-V24 | PASS (as a reader/no-writer scan) | GOT `0x10400BC`→`0x108DAF0`, `0x10400C0`→`0x108DB08`, `0x10400C8`→`0x1052430`; file size `0x1058208`, first two beyond it; `0x1052430` bytes `01 00 00 00`. Readers `0x9AF900`, `0x9A9B54`, `0x9B0848`, `0x9A9C38`, `0x9AFAC0` all confirmed. A full-file scan found **no `movw`/`movt`** to any of them and each exact address word occurs once, at its GOT slot. Base+offset writers are not excluded. The gap1 report finds those writers. |
| B1-V25 | PARTIAL | `0xA36AC4` calls `0xA3693C` twice; the first call is `0xA3693C(manager, tick, manager)` (`0xA36AC8: mov r2,r0`), not `(manager, tick)` — the row omits the third arg. The tail call is `(manager, tick, manager+0xC)` (`0xA36AE0 add r2,r4,#0xC`). `0xA3693C` state machine matches (state 4/1→`0xA35998`, state 2→`[+0x1C]=tick`/`[+0x30]=3`, state 6→`0xA3587C`+free). |
| B1-V26 | PASS | `0x9FF308` walks `[r0]`/`[r0+4]`, `[item]==1`→`0x9FDD90` (`0x9FF34C`). `0x9FDD90`: `s14=[r0+0x40]+tick*[r0+0x3C]`, coefficients `+0x48..0x5C`, stores `s14/s13/s12` to `[target+0x18/0x1C/0x20]` for `[r0+0x20]` count `[r0+0x24]`. GOT `0x10400E4`→`0x108D8E8`. |
| B1-V27 | PARTIAL | `0x9D3C98` gate + `bl 0x9D3644` + `b 0x9D3864` correct. But `0xA437E0` and `0xA4B4B0` are **not called anywhere in `0x9D3644` or `0x9D3864`**. The calls actually present are `0xA4304C` (`0x9D36E8`), `0xA01800` (`0x9D3814`), `0xA41854` (`0x9D38FC`), `0xA7A988` (several), `0xA431A8` (`0x9D39D8`), `0xA54480` (`0x9D3BA0`, `0x9D3BBC`). Two of the six listed targets are unsupported by the cited ranges. |
| B1-V28 | PASS | `0x9E6D2C` calls `0x9E2BD0` (`0x9E6D48`), locks `[global]+0x8C` via `0x4D3064` (`0x9E6D5C`), walks bucket array `+0x90`/count `+0x94` calling `0x9D8A24`, unlocks `0x4D3070` (`0x9E6DB4`), tail `0x9E2AE4`. `0x9E2AE4` uses `0x9E21FC` (`0x9E2B7C`), refcount `+0x40` (`0x9E2B84`), `vt+0` (`0x9E2BB0`), `0xA7A988`. GOT `0x10400E8`→`0x108D8DC`. |
| B1-V29 | PASS | Exact order `0xA36AC4(tick+1)` (`0xAFA64`), `0x9FF308` (`0xAFA78`), `0x9D3C98` (`0xAFA7C`), `0x9E6D2C` (`0xAFA8C`), `0xA57FF8` (`0xAFA90`), `0xA38420` (`0xAFA94`), `0x99DA54(0x10)` (`0xAFA9C`), `tick++` (`0xAFAA8`). GOT slots `0x1040080`→`0x108D8EC`, `0x10400E4`→`0x108D8E8`, `0x10400E8`→`0x108D8DC`. |
| B1-V30 | PASS | `0xA4FD6C: bl 0xA45E9C`; gains product at `0xA4FD18..0xA4FD68` (`vmul` chain). |

## Rows that needed re-extraction (and the gap1 report's correction)

- **B1-V23** — FAIL: gate conditions/outcomes inverted against `0x9AF8F4..0x9AFADC` (and the same error in the `0x9A9B40` helper description). Corrected by gap1 G10.
- **B1-V24** — the reader scan passes, but its no-writer conclusion is contradicted by gap1 G1-G8: the three gate bytes are fields of the output-device state struct at `0x108DAE8` and all have writers. The "always device path / arg=1" conclusion is not established; the branch is dynamic and its runtime value is HARDWARE_ONLY.
- **B1-V27** — PARTIAL: `0xA437E0` and `0xA4B4B0` are absent from both cited functions; the call list does not match. gap1 C13/group-member rows keep only the supported targets.
- **B1-V9** — PARTIAL: `vt+0x58` is dispatched on the bus object `[[source+0xD4]+0xC]`, not the source.
- **B1-V11** — PARTIAL: the copy is 40 bytes, not 24; the `0xA69A70` call is at `0xA5303C`.
- **B1-V16** — PARTIAL: connection destruction is not present in the cited range.
- **B1-V18** — PARTIAL: the `0xA4E974`/`0x9CC2AC`/`0x9CC4D8` detail is inside `0xA4F754`, outside the cited range.
- **B1-V25** — PARTIAL: the first `0xA3693C` call's third argument (`manager`) is missing.
- **B1-V2** — PARTIAL: `vt+0x84` is called three times, not twice; `0xA58008` is not a 3-instruction thunk.

Everything else (B1-V1, V3–V8, V10, V12–V15, V17, V19–V22, V24 as a reader scan, V26, V28–V30) is supported by the instructions opened.