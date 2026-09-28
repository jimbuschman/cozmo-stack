# M6-022 bounded gap pass — bus-output and Perform-group callees

Scope: rows V18, V21, V25..V28 of M6-022 (correction C11). Engine `resources/lib/armeabi-v7a/libcozmoEngine.so` (3.4.0-1204, ARM mode). All citations are instruction addresses in that `.so`; the Ghidra tree was used for navigation only.

## Headline findings

1. **`0xA50120` is not a function.** It is a branch target inside `FUN_00a4fef8` (entry `0x00A4FEF8`). The index lists no entry there. The row already calls it a "path"; this report confirms it and gives the branch.
2. **`0xA4E974`'s Ghidra decomp is collapsed** (it prints only `0xA4E7CC(); 0xA68AB4(...)` and 26 "unreachable block removed" warnings). The real 868-byte body contains the whole create/init/reset flow; it was read from the instructions.
3. **`bus+slot+0x138` is the out-buffer pointer, not an "in-place flag".** The in-place decision is the byte `sp+0x34` returned by `0x9CF644`. `0xA4E7CC` frees and zeroes `+0x138`; the not-in-place branch allocates and stores a buffer there. B1's label is a misnomer (the value is 0 exactly when in-place).
4. **The PBI Term slot is polymorphic.** Base PBI vtable `0x103B768`: `+4 = 0x009FF54C` (destructor), `+0x10 = 0xA029DC` (Term). `ContinuousPBI` vtable `0x103D3B0`: `+4 = 0xA6A018`, `+0x10 = 0xA6ACC0`. The flush dispatches through the object's own vtable, so a continuous PBI runs `0xA6ACC0`, not `0xA029DC`.
5. **The flush's `[item+8]==4` is the message *code*, not the "reason".** `0xA38600` stores `{next, obj, code, reason, extra}`; `0xA01800` queues `code=4` and `reason=caller's r1`. The row's phrase "reason 4" is off by one field.
6. **`0xA437E0` / `0xA4B4B0` are confirmed absent** from both `0x9D3644` and `0x9D3864` (no `bl`/`b`/literal anywhere in either body). C11 is right; B1-V27's claim is contradicted.
7. **`0x9FDD90` confirms B1** with one correction: the tail callee on completion is `0x9FD910`, and the per-target gate is `([target+0x3c] & 4) == 0`.
8. **The index size for `0xA4FEF8` (4028) undercounts.** Reachable code runs to `0x00A50FD4` (epilogue at `0xA50EFC`; branch targets up to `0xA50FD0`); the next indexed entry is `0xA50FDC`. Actual body ~4324 bytes.

---

## Q1 — V18 non-FX bus-output path `0xA50120`

### Answer

`0xA4FEF8(bus, out)` first decides whether the bus has an insert-FX object:

- `0xA4FF0C` `ldrb r7,[r0,#0x1b8]`; `0xA4FF1C` `and r3,r7,#0xc`; `0xA4FF24` `cmp r3,#4`; `0xA4FF2C` `beq 0xa4ff40`.
  - If `(bus+0x1B8 & 0xC) == 4`: **no** SetInsertFx call.
  - Else `0xA4FF30` `tst r7,#4`; `0xA4FF34` `moveq r1,#0xf`; `0xA4FF38` `movne r1,#0`; `0xA4FF3C` `bl 0xa4f754`. So `mask = (bus+0x1B8 & 4) == 0 ? 0xF : 0`. (Confirms the B1/C11 mask claim.)
- `0xA4FF40` `ldr r3,[r3,#0x1a8]`; `0xA4FF48` `cmp r3,#0`; `0xA4FF4C` `beq 0xa50120`.
- `0xA4FF50` `ldr r0,[r3,#0xc]`; `0xA4FF58` `beq 0xa50120`.
- Otherwise the **FX path**: `0xA4FF5C..0xA4FF6C` calls `[[bus+0x1A8]+0xC]->vt+0x2C(bus+0x60)`; then `0xA4FF74` `bl 0xa4fd84`; `0xA4FFB0` `bl 0xa4d994`; `0xA4FFB4..0xA4FFBC` copies `[bus+0x80],[bus+0x84]` to `buffer+0x10/+0x14`; `0xA50014..0xA50028` calls `[[bus+0x1A8]+0xC]->vt+0x30(buffer)`.

The **non-FX path** starts at `0xA50120` (reached by either `beq` above, and also whenever the SetInsertFx block was skipped):

```
; 0xA50120
r5 = bus
buffer = 0xA4FD84(bus)              ; 0xA50128  -> bus output buffer
r3 = [bus+0x1bc]
*out = buffer                       ; 0xA5013C
if r3 == 1: goto 0xA50EA4
; else (0xA50144):
r3 = [buffer+4]
local.word0 = r3                    ; 0xA50154 stores at fp-0x68
0xA4D994(bus, &local)               ; 0xA50164
[bus+0x80..0x87] -> buffer+0x10..0x17   ; 0xA50168..0xA50170
goto 0xA50EE0

0xA50EA4:                           ; [bus+0x1bc] == 1
[bus+0x1b8] = ([bus+0x1b8] & 0xfd) | (([bus+0x1b8] & 1) << 1)   ; 0xA50EBC
r3 = [buffer+4]; local.word0 = r3
0xA4D994(bus, &local)
[bus+0x80..0x87] -> buffer+0x10..0x17
; fall through to 0xA50EE0

0xA50EE0:
r6 = *out = buffer
sl = [r6+0x18]
if sl == 0: return                  ; 0xA50EFC epilogue
[fp-0xC8] = 0                       ; "no insert FX" flag
goto 0xA50044                       ; shared bus-mix kernel
```

The only behaviour the non-FX path lacks versus the FX path is the insert-FX object's `vt+0x2C` / `vt+0x30` bracketing. `[fp-0xC8]` is 1 on the FX path (`0xA5003C`) and 0 here; it is tested at `0xA500D0` (`ldr r3,[fp,#-0xc8]; cmp r3,#0; bne 0xa500e8`) to decide whether to loop once more or return at `0xA500DC`.

The shared bus-mix kernel (`0xA50044..0xA50FD0`) then mixes the child list `buffer+0x18`. It is large NEON. Loop heads observed: `0xA50044` (per-child gain), `0xA501E8`, `0xA507D0`, `0xA50B58`, `0xA50CB0`; the kernel ends at `0xA50FD0` (`b 0xa50cb0`). `0xA500A4` tests `[fp-0xa4] & 0x10` and calls `0xA52164`; `0xA500C0` reads `[bus+0xc1]&0x1f` and calls `0x9C806C` with `[bus+0x48]`, `[[out]+0x18]`, `[[out]+4]` (`0xA50178..0xA501A0`).

### Citations

- Branch/mask: `0x00A4FF0C..0x00A4FF3C`; FX object call `0x00A4FF40..0x00A4FF6C`, `0x00A50014..0x00A50028`.
- Non-FX prologue: `0x00A50120..0x00A50174`.
- `[bus+0x1bc]==1` branch: `0x00A50EA4..0x00A50EDC`.
- Merge/return: `0x00A50EE0..0x00A50F04`.
- Flag test: `0x00A500D0`, `0x00A500E8`; shared kernel `0x00A50044..0x00A50FD0`.

### Classification

- Branch decision, mask, non-FX prologue, merge, return: **EXACT_SOURCE**.
- The shared bus-mix NEON kernel `0xA50044..0xA50FD0`: **RECOVERABLE_GAP** for instruction-level transliteration (loop heads and the entry tests were read; the ~1000-instruction vector body was not transliterated in this pass). Note: row V18's cited evidence is only the branch addresses (`0xA4FEF8..0xA4FF3C`, `0xA4F964`, `0xA4FF30`, `0xA4FF74`, `0xA4FFB0`), so V18's `EXACT_SOURCE` covers the branch, not the kernel.

---

## Q2 — V18 `0xA4F754` (SetInsertFx) and `0xA4E974`

### Answer

`0xA4F754(bus, mask)` (body `0xA4F754..0xA4F9AB`):

```
uVar2 = 0xA68B28(bus+0x4C)          ; 0xA4F760/0xA4F768; virtual -> tail 0x9C5974
bVar5 = [bus+0x1B8]                 ; 0xA4F76C
uVar4 = [bus+0x64]                  ; 0xA4F774 default slot value
if ((bVar5 & 1) != uVar2) { [bus+0x1B8] = (bVar5 & 0xFE) | (uVar2 & 1) | 8 }  ; 0xA4F794..0xA4F7B0
local = { [global 0x10400xx], [bus+0x64], ... }    ; 0xA4F7B4..0xA4F7EC (uRam0106243c etc.)
for i = 0..3:                        ; 0xA4F7F0 loop, slot = bus + i*0x1C
  slotbuf = [slot+0x138]             ; 0xA4F7F0
  if (slotbuf != 0 && ([slot+0xE0] & 2) == 0 && ([bus+0x1B8] & 2) == 0) {
      uVar4 = [slot+0x13C]; uVar8 = uVar4 & 0xFF
  }
  if (carry || ((mask >> i) & 1)) {  ; 0xA4F828..0xA4F830
      0xA4E974(bus, i, &local)       ; 0xA4F964
      if ((local.low8) != uVar8) carry = 1
  } else {
      if (slotbuf == 0 || ([slot+0xE0] & 1) != 0 || ([bus+0x1B8] & 1) != 0) goto compare  ; 0xA4F834..0xA4F858
      local = [slot+0x13C]
      compare low byte; if differ carry = 1
  }
  compare high nibble: if ((local>>8 ^ uVar4>>8) & 0xF) != 0 carry = 1   ; 0xA4F928..0xA4F934
  carry |= ((local ^ uVar4) & 0xFFFFF000) != 0                          ; 0xA4F940..0xA4F950
; after 4 slots:
if ([bus+0x4C] == 0 || ([bus+0x4C]+0x46 & 0x80) == 0) {                 ; 0xA4F878..0xA4F890
    [bus+0xC0] &= 0xFD; [bus+0xA0] = 0
    [bus+0x9C] = 0x42C80000 (100.0f); [bus+0x94] = 0x3F000000 (0.5f); [bus+0x98] = 0x3F800000 (1.0f)
} else {
    [bus+0xC0] |= 2
    local = {0,0,0,0xFF,0xFF,0,0}; 0x9FAEE8([bus+0x4C], &local, bus+0x94)   ; 0xA4F970..0xA4F9A4
}
0xA67B9C(bus+0x34, uVar2, (char)[bus+0x44])    ; 0xA4F8D0
[bus+0xC0] &= 0xF7
[bus+0x1B8] = ([bus+0x1B8] & 0xF7) | 4          ; 0xA4F8FC
[bus+0xA4] = 0x42CA0000 (101.0f)                ; 0xA4F900
bus->vt+0x20(bus)                               ; 0xA4F904..0xA4F908
```

Per-slot create/init/reset/bypass flow — `0xA4E974(bus, i, local)` (body `0xA4E974..0xA4ECD4`):

```
0xA4E7CC(bus, i)                    ; 0xA4E990 drop the old slot
0xA68AB4(bus+0x4C, i, &out)         ; 0xA4E9A8 lookup slot object (virtual [bus+0x4C]->vt+0xE8)
ip = out; if ip == 0: return
[bus+i*0x1C+0xD4] = [ip+0x10]       ; 0xA4E9D4
if [ip+0x14] == 0: ip->vt+0xC(ip); return            ; 0xA4E9E0 -> 0xA4EACC
local_req = caller local (3 words)  ; 0xA4E9FC
[sp+0x2C]=0; [sp+0x30]=0; [sp+0x34]=1 (in-place default); [sp+0x35]=0; [sp+0x36]=0  ; 0xA4E9F8..0xA4EA1C
if 0x9CF644(bus+i*0x1C+0xC8, ip, 0, 1, &local_req) == 0: goto cleanup   ; 0xA4EA20
[bus+i*0x1C+0xE0] bit0 = [sp+0x1C] bit0              ; 0xA4EA34..0xA4EA44
obj = 0xA7A7F4(global, 0x24)        ; 0xA4EA4C
if obj == 0: [bus+i*0x1C+0xDC]=0; goto cleanup       ; 0xA4EA54/0xA4EADC
0xA6C25C(obj, bus, i, bus+0x4C)     ; 0xA4EA64
[bus+i*0x1C+0xDC] = obj             ; 0xA4EA74
if 0x9CC2AC([bus+i*0x1C+0xD4], bus+i*0x1C+0xD8, &sp+0x2C) != 1: goto cleanup   ; 0xA4EA88
if 0x9CC4D8([ip+0x10], 3, &sp+0x2C) != 0: goto cleanup        ; 0xA4EAF4
if [sp+0x36] != 0: goto cleanup     ; async flag must be 0    ; 0xA4EB00..0xA4EB08
effect = [bus+i*0x1C+0xD8]
if effect->vt+0x1C(effect, [global], [bus+i*0x1C+0xDC], [bus+i*0x1C+0xCC], &sp+0x20) != 1: goto cleanup
                                    ; 0xA4EB30 Init
if [sp+0x34] == 0:                  ; not in-place            ; 0xA4EB40
    n = [sp+0x24] * (global_u16 & 0xFFFF)
    buf = 0xA7A894(global, n*4)     ; 0xA4EC94 alloc out buffer
    if buf == 0: goto cleanup
    memset(buf, 0, n*4)             ; 0xA4ECAC
    [bus+i*0x1C+0x138] = buf        ; 0xA4ECC4
    [bus+i*0x1C+0x144] = global_u16 ; 0xA4ECC8
    [bus+i*0x1C+0x146] = 0          ; 0xA4ECCC
    [bus+i*0x1C+0x13C] = [sp+0x24]  ; 0xA4ECD0
    [bus+i*0x1C+0x140] = 0x11       ; 0xA4EC84
; 0xA4EB4C..0xA4EBF0: compare sp+0x20 against the caller local, field by field:
;   word0 == local[0]; byte +4 == local[4]; byte +5 low nibble == local[5]&0xF;
;   word +4 masked 0xFF0/0xF equal; byte +8 low 6 bits == local[8]&0x3F;
;   half +8 masked 0x3F equal; byte +0xA low 3 bits == local[0xA]&7.
;   Any mismatch -> cleanup (0xA4EA98).
if effect->vt+0xC(effect) != 1: goto cleanup       ; 0xA4EC08 Reset
if ([bus+i*0x1C+0xE0] & 1) != 0: return            ; 0xA4EC20
if ([bus+0x1B8] & 1) != 0: return                  ; 0xA4EC2C
; copy sp+0x20 (word,word,half,byte) back into the caller local  ; 0xA4EC3C..0xA4EC54
cleanup 0xA4EA98:
0xA4E7CC(bus, i)
ip = [sp+0x18]; if ip != 0: ip->vt+0xC(ip)         ; 0xA4EAB0..0xA4EAB8
```

`0xA4E7CC(bus, i)` (body `0xA4E7CC..0xA4E934`) is the slot drop:
- `[bus+i*0x1C+0xD8]` (effect) → `effect->vt+8` (Term); clear `+0xD8`.
- `[bus+i*0x1C+0xDC]` (0x24-byte object) → `obj->vt+0`; `0xA7A988` free; clear `+0xDC`.
- `[bus+i*0x1C+0xD4] = -1`; `0x9CF820(bus+i*0x1C+0xC8)`.
- `[bus+i*0x1C+0x150]` → `obj->vt+0`; free; clear.
- `[bus+i*0x1C+0x138]` (out buffer) → `0xA7A914` free; clear `+0x138`; `+0x140 = 0x2B`; `+0x144 = +0x146 = 0`; clears the `+0x13C` format word.

Slot-object fields (from `0xA68AB4` result `ip`):
- `+0x10` = creation-info/plugin descriptor pointer, used as the first arg of `0x9CC2AC` and `0x9CC4D8` and stored at `bus+i*0x1C+0xD4`.
- `+0x14` = validity/active word; 0 → Reset and return.
- vtable `+0xC` = Reset.
- vtable `+0xE8` (on `[bus+0x4C]`) = the slot lookup.
Effect object (`bus[i].+0xD8`): `vt+0x1C` = Init, `vt+0xC` = Reset, `vt+8` = Term.
0x24-byte object (`bus[i].+0xDC`): `vt+0` = destructor.
Bus per-slot fields: `+0xC8` format/reset target, `+0xCC` Init arg, `+0xD4`, `+0xD8`, `+0xDC`, `+0xE0` flags (bit0, bit1), `+0x138` out buffer, `+0x13C` format word, `+0x140` state (`0x11` not-in-place / `0x2B` after drop), `+0x144` u16 count, `+0x146` u16 0, `+0x150` another object destroyed by `0xA4E7CC`.

### Citations

- `0xA4F754` body `0x00A4F754..0x00A4F9AB`; slot create call `0x00A4F958..0x00A4F968`; per-slot tests `0x00A4F7F0..0x00A4F950`; tail `0x00A4F878..0x00A4F908`.
- `0xA4E974` body `0x00A4E974..0x00A4ECD4`; create `0x00A4EA88`; type-3 `0x00A4EAF4`; async gate `0x00A4EB00`; Init `0x00A4EB30`; in-place gate `0x00A4EB40`; out-buffer alloc `0x00A4EC94..0x00A4ECD0`; Reset `0x00A4EC08`; copy-back `0x00A4EC3C..0x00A4EC54`.
- `0xA4E7CC` body `0x00A4E7CC..0x00A4E934`.
- `0xA68AB4` dispatch `0x00A68AB4..0x00A68ACC`; `0xA68B28` `0x00A68B28..0x00A68B34`.

### Classification

- `0xA4F754`, `0xA4E974`, `0xA4E7CC`, the per-slot field layout, and the call order: **EXACT_SOURCE**.
- **The FX-slot object's identity is RECOVERABLE_GAP.** `ip` comes from `[bus+0x4C]->vt+0xE8` (dispatched by `0xA68AB4`); `[bus+0x4C]` is only known as the object whose `vt+0xE8` yields `ip` and whose `0x9C5974` yields the bypass bit. No symbol/RTTI. The fields and calls are given above.

---

## Q3 — V21 PBI-notification flush `0xA38420`

### Answer

`0xA38420()` (body `0xA38420..0xA385D7`) is a loop over the notification queue. Queue head/count globals resolved from the GOT-relative literals: count `0x108DE90`, head `0x108DE7C`.

```
loop:
  if count == 0: return
  node = head
  0xA0188C(node[1], node[2], node[3], node[4])   ; 0xA38474..0xA38480 generic handler
  if node[2] == 4:                               ; 0xA38484..0xA3848C
      pbi = node[1]
      unlink pbi from the 0x108DEC8 list           ; 0xA38490..0xA38508
      0x9D3470(pbi)                                ; 0xA384CC
      pbi->vt+0x10(pbi, 0)                         ; 0xA384D0..0xA384E0  Term
      global = [GOT 0x00607B50 slot]               ; 0xA384E4..0xA384F8
      pbi->vt+4(pbi)                               ; 0xA384FC
      0xA7A988(global, pbi)                        ; 0xA38508
  ; pop node from the queue, pool-free or 0xA7A988, count--   ; 0xA3845C..0xA3859C
  goto loop
```

`0x9D3470(pbi)` (body `0x9D3470..0x9D353F`) walks the list at `0x108DA10` and unlinks the node whose `[node+1] == pbi`; pool-frees or `0xA7A988(global, node)`; decrements `0x108DA24`. (This is the PBI scheduler list, not the queue.)

`0xA38600(obj, code, reason, extra)` (queue append, `0xA38600..0xA38678`) stores into a node `{+0=next=0, +4=obj, +8=code, +0xc=reason, +0x10=extra}` and links it at `0x108DE7C`. `0xA01800(obj, reason)` (`0xA01800..0xA01814`) sets `[obj+0x154]=0`, then tail-calls `0xA38600(obj, 4, reason, 0)`. So the `==4` tested by the flush is the **code** (the "Term" message type); the reason is `node[3]`.

PBI vtable slots (dumped from the `.so`):
- base PBI vtable `0x103B768`: `+0x00 = 0x009FF7B8`, `+0x04 = 0x009FF54C`, `+0x08 = 0x009FFA48`, `+0x0C = 0x00A0285C`, `+0x10 = 0x00A029DC`, `+0x14 = 0x009FF41C`, ...
- `ContinuousPBI` vtable `0x103D3B0`: `+0x04 = 0x00A6A018`, `+0x10 = 0x00A6ACC0`.

`0x009FF54C` (the `+4` "delete") is a destructor, not the memory free: it resets the subobject vtables (`[obj]=base+8`, `[obj+8]=base+0x70`, `[obj+0xC]=base+0x7C`), frees `[obj+0x1F0]` if set, releases `[obj+0x12C]` via `0x9A6988`, destroys `obj+0xEC` via `0xA1C65C` and `obj+0xC` via `0x9BC554`, and returns. The flush then does the actual `0xA7A988` free.

`0xA029DC` (the base `+0x10` Term; body `0xA029DC..0xA02E14`, 1080 bytes) tears down: `0xA01684`; removes `[pbi+0x144]` and `[pbi+0x148]` transitions via `0xA36618`; clears `pbi+0x1BC` bit1; if `[pbi+0x140] != 0` calls `0xA04DE8(playingID, pbi)` (the playing-ID count decrement of M6-008 D3.2); `0xA1C660(pbi+0xEC, pbi+0x14)`; frees `pbi+0x10C`; removes the voice from `[[pbi+0xE0]+0x30]`; frees `pbi+0x1E8` via `0xA3E27C`; frees `pbi+0x150` if `[pbi+0x150]+0xD` bit0; removes `pbi+0xAC` from its list; `0xA19F60(pbi+0xC, ...)`; decrements the game-object refcount `[[pbi+0x14]+0x7C]` and frees it if it reaches 0; `0x9BDB18(pbi+0xC)`; then calls `[[pbi+0xE0]+0]`'s `vt+0xC`.

For a `ContinuousPBI` the flush's `vt+0x10` is `0xA6ACC0` (the container-end path of M6-008 2.5), so the base `0xA029DC` is only run for the base PBI class.

### Citations

- Flush `0x00A38420..0x00A385D7`; handler `0x00A38480`; code-4 test `0x00A38484..0x00A3848C`; unlink `0x00A38490..0x00A38508`; `0x9D3470` call `0x00A384CC`; Term call `0x00A384DC`; delete call `0x00A384FC`; free `0x00A38508`; pop/count `0x00A3850C..0x00A3859C`.
- `0x9D3470` body `0x009D3470..0x009D353F`.
- Queue `0x00A38600..0x00A38678`; `0x00A01800..0x00A01814`.
- Vtables: raw words at `0x0103B768` and `0x0103D3B0` (dumped from the ELF).
- `0x009FF54C..0x009FF5BC`; `0xA029DC..0xA02E14`.

### Classification

- Flush, queue, unlink, vtable slot identities, destructor and base Term: **EXACT_SOURCE**.
- `ContinuousPBI` `+0x10` target `0xA6ACC0`: **EXACT_SOURCE** for the slot value; its body is already owned by M6-008 2.5 (not re-read here).

---

## Q4 — V25 group member 1 `0xA3587C`

### Answer

`0xA3587C(obj)` (body `0xA3587C..0xA358B0`):

```
r1 = [obj+0x20]
if r1 == 0: return
[obj+0x24] = 0                       ; 0xA35898
0xA7A988(global, [obj+0x20])         ; 0xA358A0/0xA358A4 free
[obj+0x20] = 0                       ; 0xA358A8
[obj+0x28] = 0                       ; 0xA358AC
```

The caller `0xA3693C` reaches it in the state-6 case (`[item+0x30]==6`, `0xA369E8`): it calls `0xA3587C(item)`, then `0xA35878(item)`, then `0xA7A988(global, item)` (free), then removes the item from the manager array (`0xA36A10..0xA36A48`). `0xA3693C`'s other states: 4/1 → `0xA35998(item, tick)` (`0xA369A0`); 2 → `[item+0x1C]=tick`, `[item+0x30]=3` (`0xA369D8`); 6 → the above.

### Citations

- `0x00A3587C..0x00A358B0`.
- Caller state dispatch `0x00A3696C..0x00A369E8`; free `0x00A36A0C`; array removal `0x00A36A10..0x00A36A48`.
- `0xA3693C` body `0x00A3693C..0x00A36AB8`; `0xA36AC4` body `0x00A36AC4..0x00A36AE8`.

### Classification

- **EXACT_SOURCE**. Class name remains UNKNOWN.

---

## Q5 — V26 group member 2 `0x9FDD90`

### Answer

`0x9FDD90(obj, tick)` (body `0x9FDD90..0x9FDE48`; `0x9FDE4C` is the next function):

```
val = [obj+0x40] + (float)tick * [obj+0x3c]     ; 0x9FDD94..0x9FDDA4
if val < 1.0:                                    ; 0x9FDDAC..0x9FDDB4
    t = (val > 0.0) ? val : 0.0                  ; 0x9FDE34..0x9FDE40 (lower clamp 0.0)
else:
    t = 1.0                                      ; 0x9FDDA8 upper clamp 1.0
out0 = [obj+0x48] + t * [obj+0x54]               ; 0x9FDDC8/0x9FDDE0
out1 = [obj+0x4c] + t * [obj+0x58]               ; 0x9FDDD4/0x9FDDE4
out2 = [obj+0x50] + t * [obj+0x5c]               ; 0x9FDDDC/0x9FDDE8
count = [obj+0x24]                               ; 0x9FDDB8
arr   = [obj+0x20]                               ; 0x9FDDC0
if count != 0:
  for p in arr[0..count-1]:                      ; 0x9FDDF0 loop
      target = [ [p] + 0xd0 ]
      if ([target+0x3c] & 4) == 0:               ; 0x9FDDFC..0x9FDE08
          [target+0x18] = out0
          [target+0x1c] = out1
          [target+0x20] = out2
if tick < [obj+0x34]: return                     ; 0x9FDE20..0x9FDE28
0x9FD910(obj, tick)                              ; 0x9FDE30 tail call
```

This **confirms** B1: `val = [obj+0x40] + tick*[obj+0x3c]`; coefficients base `[obj+0x48/+0x4c/+0x50]`, slopes `[obj+0x54/+0x58/+0x5c]`; target array `[obj+0x20]` count `[obj+0x24]`. Correction/addition: the clamp is `[0.0, 1.0]` (not a general clamp), the per-target write is gated on `([target+0x3c] & 4) == 0`, and the completion tail is `0x9FD910` when `tick >= [obj+0x34]`.

### Citations

- `0x009FDD90..0x009FDE48`; value `0x009FDD94..0x009FDDA4`; clamp `0x009FDDAC..0x009FDDB4`, `0x009FDE34..0x009FDE40`; coefficients `0x009FDDC8..0x009FDDE8`; target loop `0x009FDDF0..0x009FDE1C`; tail `0x009FDE20..0x009FDE30`.

### Classification

- **EXACT_SOURCE** for the body read.
- `0x9FD910`'s body was not read (**RECOVERABLE_GAP**); it is the on-completion handler for `tick >= [obj+0x34]`.

---

## Q6 — V27 group member 3 `0x9D3C98` → `0x9D3644` / `0x9D3864`

### Answer

`0x9D3C98()` (body `0x9D3C98..0x9D3CB8`):

```
if byte[0x108DA34] != 0:        ; 0x9D3C9C..0x9D3CAC (gate)
    0x9D3644()                  ; 0x9D3CB0
0x9D3864()                      ; 0x9D3CB8 tail
```

`0x9D3644()` (body `0x9D3644..0x9D3830`) walks the list at **`0x108DA10`** (`0x9D3644..0x9D3658` resolves the GOT slot to `0x108DA10`; `ldr r4,[r3,#4]`):

```
for node in list:
    sl = [node+4]                          ; 0x9D36B0
    if [sl+0x154] != 0: next               ; 0x9D36B4..0x9D36BC
    if [node+0xC] > 1: next                ; 0x9D36C0..0x9D36C8
    if ([sl+0x1BC] & 0x20) && [sl+0x1F8] == -1:   ; 0x9D36CC..0x9D36E0
        unlink node; pool-free or 0xA7A988(global,node); count--
        0xA01800(sl, 1)                    ; 0x9D3814
        free node; next
    if 0xA4304C(node+4) == 1: next         ; 0x9D36E4..0x9D36F0
    unlink node; pool-free or 0xA7A988(global,node); count--   ; 0x9D36F4..0x9D3760
byte[0x108DA34] = 0                         ; 0x9D3764..0x9D3770
```

`0x9D3864()` (body `0x9D3864..0x9D3C58`) walks the same `0x108DA10` list and, for each node `sb` (object `[sb+4]`), runs a type dispatch and two teardown callees:

```
for sb in list:
    if ([sb+0xD] & 1): r4 = [[sb+4]+0x154]; goto dispatch   ; 0x9D38E0..0x9D38E8, 0x9D3A94
    if [sb+0xC] <= 1: next                                   ; 0x9D38EC..0x9D38F4
    if 0xA41854(sb+4) == 0: unlink/free (0x9D3BC4); next     ; 0x9D38F8..0x9D3904
    r4 = [[sb+4]+0x154]                                      ; 0x9D3A94
    ; inner walk over a nested list also calls 0xA431A8:
    ;   if [obj+0x154] != 0: r = 0xA431A8(obj, [obj+0x154]);
    ;     r == 2 -> continue; r == 0x3F -> r6=1; else set [sb+0xD] bit0   ; 0x9D39B0..0x9D39F8
    ; dispatch on [sb+0xC] (jump table at 0x9D3AA4..0x9D3AC0):
    ;   0 -> 0xA54480(r4)                                    ; 0x9D3BB8
    ;   1 -> 0xA54480(r4); then r4->vt+0x4C                   ; 0x9D3B9C..0x9D3BB0
    ;   2 -> r4->vt+0x4C                                     ; 0x9D3B88
    ;   3 -> r4->vt+0x50                                     ; 0x9D3AC4
    ;   4 -> r4->vt+0x54                                     ; 0x9D3B70
    ;   5 -> r4->vt+0x58                                     ; 0x9D3B5C
    ; then unlink/free (0x9D3AD4..0x9D3B48)
```

Teardown callees (bodies read):

- `0xA01800(obj, reason)` (`0xA01800..0xA01814`): `[obj+0x154]=0`; `0xA38600(obj, 4, reason, 0)`.
- `0xA41854(objptr)` (`0xA41854..0xA41908`): lookup. Walks the list at `[[GOT]+0x14]`, matching `[obj]` against `[cand+0xD4]+0xC` (or, when `[objptr+8]==4`, `[cand+0xD8]+0xC`); on match, if `[cand+0xDC] != 0` sets `[objptr+9] |= 1`, returns `cand`; otherwise falls to `[[obj]+0x154]` and returns it when `[objptr+8]==4`, else 0.
- `0xA431A8(obj, voice)` (`0xA431A8..0xA43258`): `r = 0xA544BC(voice, obj)`; if `r == 0x3F` return; unlink `voice` from the list at `[[GOT]+4]` (`voice+0xD0` next); if `r == 1` tail-call `0xA42DEC(voice, obj)`, else `0x9D40C4(voice, 1)` and return 2.
- `0xA54480(obj)` (`0xA54480..0xA544B8`): `s = [obj+0xDC]`; if `s == 2` return; if `s != 0` call `obj->vt+0x48`; if `s == 0` call `0xA56478([obj+0xD4])` and set `[obj+0xDC]=1`.
- `0xA4304C(objptr)` (`0xA4304C..0xA43194`, already in inventory gap 1.5): returns **1** on the new-voice-created path (`0xA43130..0xA4315C`), **2** on alloc failure (`0xA4316C`), **5** on the matched-existing-voice path (`0xA43128`), and the `0xA42DEC` result on the existing-voice path (`0xA43174..0xA43194`). This is why `0x9D3644`'s `cmp r0,#1` keeps the node.

**B1-V27 contradiction confirmed:** neither `0xA437E0` nor `0xA4B4B0` appears in either body (no branch or literal). C11 is correct.

### Citations

- `0x9D3C98..0x9D3CB8`; gate `0x009D3C9C..0x009D3CB0`.
- `0x9D3644..0x9D3830`; list head `0x009D3654` (`ldr r4,[r3,#4]` → `0x108DA10`); 0xA4304C call `0x009D36E8`; 0xA01800 call `0x009D3814`; gate clear `0x009D376C`.
- `0x9D3864..0x9D3C58`; 0xA41854 `0x009D38FC`; 0xA431A8 `0x009D39D8`; 0xA54480 `0x009D3BA0` and `0x009D3BBC`; dispatch table `0x009D3AA4..0x009D3AC0`.
- `0xA01800..0xA01814`; `0xA41854..0xA41908`; `0xA431A8..0xA43258`; `0xA54480..0xA544B8`; `0xA4304C..0xA43194`.
- Absence of `0xA437E0`/`0xA4B4B0`: full scans of the two disassembly dumps.

### Classification

- Per-list walks and teardown order: **EXACT_SOURCE** for the instruction flow read.
- Class names: UNKNOWN (unchanged).
- `0xA4304C`'s internal voice-attach body is inventory gap 1.5 (not re-read); its return values are cited above.
- Nested callees `0x9DF1C0`/`0x9DF554`/`0x9D5414`/`0x9DECD8`/`0x9DEB60`/`0x9DEA7C`/`0x9DE978`/`0x9DF288`/`0x9DF0BC` (used by `0x9D8A24`, see Q7) are not read: **RECOVERABLE_GAP**.

---

## Q7 — V28 group member 4 `0x9E6D2C`

### Answer

`0x9E6D2C(manager)` (body `0x9E6D2C..0x9E6E10`):

```
0x9E2BD0([manager+0x10], (u16)[GOT global])     ; 0x9E6D40..0x9E6D48, before the lock
lock = manager + 0x8C
pthread_mutex_lock(lock)                         ; 0x9E6D54..0x9E6D5C (0x4D3064)
count = [manager+0x94]                           ; 0x9E6D64
if count == 0: goto unlock
bucket = [manager+0x90]                          ; 0x9E6DD4
; find first non-empty bucket, then for each bucket node:
for node in bucket:                              ; 0x9E6D7C loop
    0x9D8A24(node)                               ; 0x9E6D88
    node = [node+4]
; advance to the next non-empty bucket while index < count   ; 0x9E6D9C..0x9E6E10
unlock:
pthread_mutex_unlock(lock)                       ; 0x9E6DB0..0x9E6DB4 (0x4D3070)
0x9E2AE4([manager+0x10])                         ; 0x9E6DBC..0x9E6DC0 tail
```

Bucket hash: array `[manager+0x90]`, count `[manager+0x94]`; each node `+4` = next; `0x9D8A24(node)` is called per node.

`0x9E2AE4(list)` (body `0x9E2AE4..0x9E2BC8`):

```
node = [list+8]; if node == 0: return
; pass 1: collect the dead nodes (0x9E2510(node) != 0) into a chain r4, unlinking each
for node in [list+8]:
    if 0x9E2510(node) != 0:
        unlink node from [list+8] / [list+4] / [list]; [list]--
        prepend node to r4
; pass 2: for each collected node:
    next = [node+4]
    0x9E21FC(node)                               ; 0x9E2B80
    [node+0x40] = [node+0x40] - 1                ; 0x9E2B84..0x9E2B8C
    if [node+0x40] != 0: continue                ; 0x9E2B90..0x9E2B94
    node->vt+0(node)                             ; 0x9E2BA0..0x9E2BB0
    0xA7A988(global, node)                       ; 0x9E2BBC
```

Refcount rule: `[node+0x40]` is decremented once per purge candidate; the node's `vt+0` destructor and the `0xA7A988` free run only when it reaches 0.

`0x9E2510(node)` (`0x9E2510..0x9E2550`) is the dead predicate: return 1 if `[node+8]==0`, or `[node+0x44]==3`, or (`[node+0x48] <= 1` and `[node+0x50]==0`); else 0.

`0x9E21FC(node)` (body `0x9E21FC..0x9E24AC`) is the per-node resource release (not the struct free):
- `sub = [node+8]`; `[node+0x44]=3`; `0x9D7EA4(sub)`.
- On the normal path (`0x9E2230`): `[node+8]=0`; `[node+0xC]=0`; `[node+0x28]=0`; `[node+0x2C]=0`; `[node+0x24]=[node+0x25]=0xFF`; then for `[node+0x14]` entries at `[node+0x10]`, call each element's `vt+0xC` (`0x9E2264..0x9E228C`); free the `[node+0x10]` array via `0xA7A988`; `[node+0x14]=0`, `[node+0x18]=0`; if `[node+0x20]!=0`, `[node+0x20]=0` then `0xA3E27C(it)`; `[node+0x1C]=0`; `[node+0x30]=0`.
- The `0x9E22E4` branch handles the `0x9D7EA4 != 0` case (uses `0xA01280`, `0x9E8028`, `0xA63A6C`, `0x9AB1F4`, `0x9AB8AC`, `0x9F4A64`) and frees `[node+0x48]`-keyed sub-lists.

`0x9D8A24(node)` (body `0x9D8A24..0x9D8F54`) is a deep recursive-style purge of nested container sub-lists. Structure: a 7-level nesting of the same pattern `{ base=[node+0x2C]/... ; count at +0x30 ; element stride 0x1C ; flag byte at element+8 ; child array at element+0xC, count element+0x10, stride 0x1C }`. At each level it tests the flag `[el+8]`, calls `0x9D7738(el+4, scratch, node)` to release, and calls the per-type destructors `0x9DF1C0`, `0x9DF554`, `0x9D5414`, `0x9DECD8`, `0x9DEB60`, `0x9DEA7C`, `0x9DE978`, `0x9DF288`, `0x9DF0BC`; arrays are freed with `0xA7A988`. It ends at `0x9D8F00` (return). The nested callees were not read (**RECOVERABLE_GAP**).

`0x9E2BD0(manager+0x10, u16)` (entry `0x9E2BD0`, body to `0x9E52F3`, 9876 bytes) is a per-voice modulator/transition curve evaluator plus pool allocator, not part of the purge proper:
- It clears `+0x1C/+8/+0x14` on every node of the lists at `[arg+0x20]` and `[arg+0x14]` (`0x9E2BDC..0x9E2C48`).
- It iterates the voice list `[arg+8]` (`0x9E2C4C..`), updates each voice's `+0x3C` accumulator by the tick `param_2`, and evaluates five curve shapes (switch cases 0..4) into per-voice modulator records, using constants `0x473B8000`, `0x40C90FDB` (2π), `0x3E22F983` (1/2π) at `0x9E2C68..0x9E2C80`.
- It allocates/reuses pools at `[arg+0x14]` and `[arg+0x20]` via `0xA7A894`/`0xA7A914`.
- It calls `0x9E52F8(voice, tick, record, pool)` for the non-zero branch (`0x9E52F8` is 2956 bytes, not read).

### Citations

- `0x9E6D2C..0x9E6E10`; `0x9E2BD0` call `0x009E6D48`; lock `0x009E6D5C`; count `0x009E6D64`; bucket `0x009E6DD4`; `0x9D8A24` call `0x009E6D88`; unlock `0x009E6DB4`; tail `0x009E6DC0`.
- `0x9E2AE4..0x9E2BC8`; `0x9E2510` call `0x009E2B48`; refcount `0x009E2B84`; vt+0 `0x009E2BB0`; free `0x009E2BBC`.
- `0x9E21FC..0x9E24AC`; `0x9D8A24..0x9D8F54`; `0x9E2510..0x9E2550`.
- `0x9E2BD0` entry `0x009E2BD0..0x009E2C80`; constants `0x009E2C68..0x009E2C80`.

### Classification

- `0x9E6D2C`, bucket hash, `0x9E2AE4`, refcount, `0x9E21FC`, `0x9E2510`, `0x9D8A24` control flow: **EXACT_SOURCE**.
- `0x9E2BD0` full instruction-level body (2469 instructions): **RECOVERABLE_GAP**. The Ghidra decomp (2038 lines) was read for its shape, and the entry/constant instructions were checked; the five curve kernels were not transliterated from the instructions. `0x9E52F8` (2956 bytes) is also unread.
- `0x9D8A24`'s nested destructors: **RECOVERABLE_GAP** as listed.

---

## Existing records contradicted by the source

- **B1-V27 (proposed M6-022)** — "`0xA437E0`/`0xA4B4B0` are called by `0x9D3644`/`0x9D3864`" is contradicted. Neither address appears in either body (`0x9D3644..0x9D3830`, `0x9D3864..0x9D3C58`). C11's correction stands.
- **B1 label "`+0x138` in-place flag"** is contradicted by `0xA4E7CC` (`0x00A4E7CC..0x00A4E934`, frees and zeroes `+0x138`) and `0xA4EC94..0xA4ECD0` (allocates and stores the out buffer at `+0x138`). `+0x138` is the out-buffer pointer; the in-place decision is `sp+0x34` from `0x9CF644` (`0xA4EB40`).
- **V21's phrasing "`[item+8]==4` (Term) -> ... `vt+0x10`, `vt+4`"** is imprecise: `0xA38600` stores `{next, obj, code, reason, extra}`, so `node[2]` is the code; the Term/delete dispatch itself is correct.

## Existing records whose evidence is too weak to keep their status

- **M6-022 / V18 (`EXACT_SOURCE`)** cites only the branch decisions (`0xA4FEF8..0xA4FF3C`, `0xA4F964`, `0xA4FF30`, `0xA4FF74`, `0xA4FFB0`). The shared bus-mix NEON kernel `0xA50044..0xA50FD0` (the actual sample mixing) has no citation in the row and was not transliterated here. Either the row should be narrowed to the branch, or the kernel needs its own read/record.
- **M6-022 / V27** lists six teardown callees but only `0xA4304C` had a description; this pass adds the other four, plus the exact walk and the `0xA4304C` return-value semantics. Its `EXACT_SOURCE` now has a citation basis.
- **M6-022 / V26** said only "a 3-component interpolation"; the row's own citation `0x9FDD90..0x9FDE30` does not cover the clamp or the target gate, now supplied.
- **M6-022 / V28** said "`0x9E2BD0(manager+0x10)`" without noting it is a 9876-byte modulator evaluator; its `EXACT_SOURCE` is unsupported for that callee until read.

## Open questions for the manager

1. **V18 kernel ownership.** Does the shared bus-mix kernel `0xA50044..0xA50FD0` get its own record/read, or does V18 stay narrowed to the branch decision? (The row currently claims `EXACT_SOURCE` for a path whose mixing body is uncited.)
2. **`0x9E2BD0` / `0x9E52F8`.** These are 9876 + 2956 bytes of curve/pool arithmetic. Do they belong to M6-022 (Perform group member 4) or to a separate modulator record? They need their own pass either way.
3. **Index size of `0xA4FEF8`.** The index's 4028 bytes undercounts the reachable body (`0xA4FEF8..0xA50FD4`); should the decomp/index be regenerated, or is this known?
4. **`ContinuousPBI` Term in the flush.** Confirm the intent that the flush's `vt+0x10` on a continuous PBI runs `0xA6ACC0` (container-end) rather than the base `0xA029DC`; this changes which teardown path M6-008 2.5/2.9 owns.
5. **`0xA41854` semantics.** It is a lookup that mutates `[obj+9]` bit0 when `[found+0xDC] != 0`; its record ownership and the meaning of the `+8 == 4` type test are not established.
6. **`0xA4304C` return-value contract.** Inventory gap 1.5 says "return 5" on match; `0x9D3644` treats only `== 1` as "keep". Confirm the full return contract so the teardown rule is not misread.
