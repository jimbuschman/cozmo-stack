# B-M6b-4 batch 4b: citation check of `20260929-B-M6b-4-batch4b-limits.md`

Independent `cozmo-verifier` pass (own capstone disassembly of `libcozmoEngine.so`, own HIRC parse of all 2360 Sounds and
31 ActorMixers). Verdict: rows hold except the items below; every other row (sect 0 to 11, incl. the 20+ sample) PASS.

## FAIL corrections (replace the report text)
1. **C2 (`0xA377BC..0xA37860`)**: `cand == 0` returns **0**, not 1 (`0xA37838 cmp r0,#0; beq 0xA3772C`, the epilogue after `0xA37728 mov r0,#1` returns with r0 = 0). The function returns 1 only after `0xA01CA4(cand,3)` (`0xA37840..0xA37848`, `b 0xA37728`). A 0 fails the Play (P3). Dormant on shipped data.
2. **V3**: the callers of `0xA37100` are `0x9FA11C` (G3, reason 1), `0x9FA9CC` (O4, reason 1) and `0xA382BC` (C7, reason 2: `mov ip,#2; str ip,[sp,#0xC]`). `0x9FA2CC` and `0x9FA3F8` are in the unreferenced functions (G4).
3. **P1a**: the Switch `vt+0x130` data word is `0x103BEF8` (vptr `0x103BDC8` + `0x130`); the slot value `0x980EF4` is right.
4. **L5**: subscriber vtable table start `0x101C238`: `+8 = 0x9FD294`, `+0x10 = 0x9FD2AC` (not `+0x14`; `+0x14` is `0x9FD28C`). The stored vptr is `0x101C240`. Cosmetic.
5. **C4**: a third `[G+0x48]++` site is inline in `0xA379D8` at `0xA380D0..0xA380DC`.

## Omissions the verifier found
- P7 success tail `0xA37FE8..0xA380E0`: `pbi vt+0xC` (`0xA0285C`) result gates the Play (`0xA37FF8 cmp r0,#1; bne 0xA37DD0`); then `0xA023D4`, `0xA01918`, `0x9E85C8`, `0xA00618`, `0xA0067C`; `0xA38098 cmp r0,#1; bne 0xA37AD8` can fail the Play after the PBI is in the lists; last, the PBI is appended to the global PBI list (`0xA380A4..0xA380E0`, `[G+0x50]`, `[G+0x4C]`, `[G+0x48]++`).
- P6 failure path `0xA37E7C..0xA37EA0`: calls `0xA04D48(mgr,playingId,pbi,..)` when `[params+0x24] != 0` and a PBI exists, before Term; Term releases it via `0xA04DE8`.
- `0xA379D8` second branch `0xA37C90..0xA37C9C`: `ldrh [node+0x5C+0x16] == 8` (external-source handling `0xA37E64`, `0xA1ED48..0xA38148`); the field is probably 0 on shipped data (`0x9B9C90` memsets it; other writers of `node+0x72` not scanned): UNVERIFIED.
- C7 `0xA381F4` first returns 1 if `n = [G+0x48]+1-[G] <= [G+0x2C]` (`0xA3827C..0xA38288`).
- K7 `0xA44BB4..0xA44BC8`: a non-zero mix-result byte `[sp+0x38]` forces `sl = 1` (the stop) regardless of the PBI test.
- K6 `0x9FF83C..0x9FF844`: with `[pbi+0xAC] != 0` a `[r1+0x18]&2` test goes to `0x9FF87C` (3D path; not shipped). O6 `0x9FAAC8..0x9FAAD8`: entry pointer 0 skips to `0x9FABC0`.
- K5: shipped Play actions with a non-zero fade-in exist (6 in Cozmo.bnk, 11 in SFX.bnk), so the K4 re-aim is live for them; the Play fade-in item id is `0x01000000` (`0xA007A0`), the stop item `0x02000000`.
- The delivered report has no "section 12"; the census is the verifier's own (below).
- 8.6 `params+0x7C`: music/MIDI builder stores (`0x97DE30`, `0x98DD3C`) are off the Sound path; the Sound caller is only `0xA1D470 -> 0xA379D8`, `0xA62BF8` stores 0.

## Census (verifier's own parse)
Sounds 2360 (Cozmo 2231, SFX 101, UI 14, Dev 14); ActorMixers 31; RanSeq 468, Switch 21, Layer 6. Advanced-settings byte 4 (the `vt+0x130` argument, `0x9ED794 ldrb r7`) is 0 in all 2886 nodes. RTPC param ids present {0,2,3} only. 13 nodes carry a max-instances field (SFX 10, UI 1, Cozmo 2): Cozmo 62050212 (max 1, byte0 = 4, global) covers 1872 Sounds, 66225135 (max 5, byte0 = 12) covers 11. No node has the virtual bit, so no walk returns 0x50. Effective bus 1723505802 for 2211 Cozmo Sounds, 2476517424 for 20; all 15 Init buses have max instances 0; duck entries only on 2459405053, 1534528548, 3829101743. Priority prop 7 only on SFX 153471530, 506971044 (0x42C60000) and 571039165 (0x428C0000). Init STMG max voices 256.

## Not verified (declared gaps, RECOVERABLE_GAP)
`0x9F7390..0x9F82EC`, `0xA17724/0xA17878`, the `0x9F36A0` move loop, `0x9F4D40` tail and `0x9F4F28`, `0xA56BC4..0xA56D04`, the distance path `0xA37BA8..0xA37C5C` and `0x9F1F80`, `0xA03618`, Thumb-side callers of `0x9A0EA4` and `0x9FA244/0x9FA344`, K11.

# Check #2: `20260929-B-M6b-4-batch4b-missing.md` (rows 1.1..10.3)

Independent verifier (own capstone disassembly; re-ran the extractor's four Unicorn emulations of `0x9F3274`, `0x9F3528`, `0x9F36A0`, `0xA37100`, 0 mismatches). Every row PASS except the items below; floats all match the binary
(`0x9BEC80..0x9BED28` polynomial uses non-fused vmla: `float(c + float(m*a))`, no FMA).

Contradicted or imprecise:
- F1 (1.7): the `e == 0` slot path (`0x9FABC0`) removes the map slot and then falls to `0x9FAC18..0x9FAC2C`: re-reads `[node+0x30]`, stores `count-1`, and when `L` is non-null goes to the idle test `0x9FAAEC` (may call `0x9F4D40`); a failed re-search goes to `0x9FAAE8` (idle test with the old `L`). The row's "only removed from the map" is wrong.
- F2 (1.8): in `0x9F4D40` `[L+0xC] = 0` is stored unconditionally (`0x9F4DB0`, before the `[L] != 0` test); only `[L+4] = 0`, `[L+8] = 0` and the free are conditional (`0x9F4DB4..0x9F4DD4`).
- F3 (1.11): "bit5 already set: nothing runs" applies to the `1BD` part only (`0xA016D8 bne 0xA01760`). The `1BE` bit5 part (decrement each list `+0x22`, then `0xA370E4` = `[G+0]--`, `G = 0x108DE78`) runs first regardless.
- F4 (3.1): `0x9BEB30`'s third argument is `r2 = [[params+0x78]+8]` (`0xA37CE4..0xA37CFC`), 0 on the `[params+0]==0` branch (`0xA37FD0 -> 0xA37D04`), which the action executor takes (`[sp+0x1C]=0` at `0xA62B80`); not `node[+8]`. Stored to `[sp+0x28]`, used at `0x9BEEC4` (`0x9BC66C(pbi, r1)`).
- F5 (5.1): one more inline bus writer of `[node+0x46]` bit2: `0x9C3728` (`bfi`, `strb` at `0x9C372C`). Conclusion unchanged.
- F6 (6.6): `0x9D3558` also clears bit0 of `[node+0xD]` (`0x9D35C8`) and stores the flag argument at `+0xC`.

Omissions (adopted in C29): O1 `0xA37DAC..0xA37DBC` a 0x50 from the walk or `sl` (neither is 2) sets `1BE |= 4` before the `[sp+0x1C]` branch (later scans `0xA37100` `0xA3717C/0xA37220`, `0xA37880` `0xA37918`, `0xA36E9C` skip PBIs with `1BE & 0x2C`); O2 `0x9BEB30` also returns the `0x9BC66C` result (`0x9BEED4`, returned at `0x9BED8C`), the `0x9BE898 == 2` return (`0x9BEBEC -> 0x9BED84`) and the `0xA11F98 != 1` return (`0x9BEEB8`); O3 Term between steps 12 and 13 (`0xA02B94..0xA02BC4`): `r7 = [[pbi+0x150]+0xD]&1`; `r7 != 0` and `[pbi+0x150] != 0` -> destroy it with `0xA1E8F4` and pool-free (`[pbi+0x150]` is the ctor's r3 = the Play caller's r1, `0xA00208`); O4 `0x9BC5A8(pbi+0xC)` (first step of `0xA0285C`) pushes the ctx onto the limiter ctx list head `[[node+0x30]+0xC]` (next in ctx `+0x24`) when `[node+0x30] != 0`, then `0xA19ECC`; O5 `0xA0067C` with `r5 != 0`: `[pbi+0x168] = 0` (`0xA00788`); with no transition `0xA36268`, `1BE |= 0x40` (`0xA00808`), store `[pbi+0x144]`, `vt+0x50(pbi,0xE,time)` (`0xA00828`), and a zero transition -> `vt+0x14` (`0xA0084C`); join: `0xA366AC(mgr,[144])` if `[144] != 0`, `0x9BDA28(ctx,1)`, `0x9E808C` if `[pbi+0x34] != 0` (`0xA00728`); O6 `0xA548C0` first step `[pbi+4]&0x100000` and `[blk+0x18] != -1` -> `0xA05574(mgr,[pbi+0x140],blk+0x18)` (`0xA548D8..0xA54900`), `0xA54F1C` `[1F8] == 0` branches to `0xA5531C` (`0xA54F4C`, unread); O7 a third reposition-loop copy at `0x9FF9B8`; O8 `0x9F3274` is also called from `0xA0054C` inside `0xA00494` (from `0xA39610`: vt+0x90 walk with block prio 0, go `[pbi+0x14]`, array `pbi+0x1EC`, 3, 0, 0, 1; appends `&G+0x20`; `[list+0x22]++` when `1BE` bit5), and `0xA0228C` (callers `0xA54648`, `0xA55484`, `0xA55A58`, `0xA55BE8`) sets `1BE` bit5, `[list+0x22]++`, `[G]++`, undone by `0xA022E8`; O9 NaN priority: an unordered compare leaves r2 = 0, so a NaN is "found at mid" (remove, `0x9F35D0..0x9F35E4`) or "insert at mid" (`0x9F3318..0x9F3330`); O10 `0xA44D4C` runs a device loop before the voice pass (`0xA44D7C..0xA44DD4`, `vt+0x2C` then `vt+0x30` per device, 8-tick gate) and does not skip the pass.
