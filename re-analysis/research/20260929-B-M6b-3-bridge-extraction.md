# Bridge extraction: the Play -> PBI -> voice -> source creation path (B-M6b-3)

**Scope:** the shipped Cozmo Sound path first (container paths second). Read-only extraction from
`resources/lib/armeabi-v7a/libcozmoEngine.so` (3.4.0-1204, ARM mode). All addresses are ELF VAs.
`re-analysis/decomp/libcozmoEngine/` and `re-analysis/tools/arm_disasm.py` were used only to
navigate; every behaviour below is cited to instructions in the `.so`. The frozen M6 inventory and
the prior research reports are claims to check, not evidence.

**Classification** uses the manifest statuses. `NEW` means no existing record covers the row.
Where a prior report's claim is confirmed, the existing record id is named; where it is corrected,
the correction is stated on the row and again in the "contradicted" section.

---

## Headline: premise corrections (read these first)

1. **The voice-attach drain is `0x9D3644`, not `0xA38420`.** The PBI start list is the linked list
   at `0x108DA10` (enqueue `0x9D3558`). It is drained by `0x9D3644`, called from `0x9D3C98`
   (`0x9D3CB0`), which calls `0xA4304C` at `0x9D36E8`. `0xA38420` is a *different* queue: the PBI
   notification/Term flush at `0x108DE7C` (enqueue `0xA38600`/`0xA01800`). In `Perform` the
   voice-attach drain `0x9D3C98` runs at `0x9AFA7C`, before LEngine `0xA57FF8` (`0x9AFA90`) and the
   flush `0xA38420` (`0x9AFA94`). The task premise "the drain in 0xA38420 ... creates/attaches the
   voice" is wrong: `0xA38420` tears the PBI down (code 4 = Term), it does not attach a voice.

2. **The PBI creator is the target node's `vt+0x14`, not an action method.** In `0xA62A1C` the play
   params struct is at `sp+0x1c`; the target node is stored at `sp+0x20` = params+4
   (`0xA62B90 str r6,[sp,#0x20]`). `0xA379D8` then calls `[params+4]->vt+0x14` at `0xA37CC8`. The
   queued action (the old second argument) is read only to *fill* the params struct
   (`0xA62B04..0xA62B9C`); it is not the object whose `+0x14` is called.

3. **The base PBI vtable is `0x103B768`** (stored by the ctor `0xA000E8` at `0xA00174`). `0x1039D98`
   is a *derived* PBI class set by `0x9883AC`, used only by the `0x97D9A0` creation path (containers
   / the `0x99B8AC` path). The Sound path goes `0xA02EC8 -> 0xA000E8`, so it uses `0x103B768`.

4. **`0xA01E24`'s mode is `(flags & 0x7f) >> 2`** (5 bits), and the plugin is
   `[[pbi+0x150]+0x14]`; the descriptor pointer is `[pbi+0x150]`.

---

## Item 1 - the node `PlayInternal` dispatch (`vt+0x128`)

`0xA62A1C` is the Play-execute helper (unindexed by Ghidra; the Play execute vfunc is
`0xA62D38`). It builds the play params on the stack at `sp+0x10..sp+0x148`, with the params base
`sp+0x1c`, resolves the target node, and calls the node's `+0x128` slot.

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| 1.1 | Target node resolved from the action's target id + isBus via `0xA6168C` -> `0x9A7EB0`; null -> error `0x0F`. The node is stored at params+4 (`sp+0x20`). | `0xA62A2C bl #0xA6168C`; `0xA62A30 subs r6,r0,#0`; `0xA62A34 moveq r4,#0xf`; `0xA62B90 str r6,[sp,#0x20]` | M6-006 gapA 1.11 | EXACT_SOURCE |
| 1.2 | `node->vt+0x128(node, params)` is the PlayInternal call. | `0xA62D14 ldr r3,[r6]`; `0xA62D20 ldr r3,[r3,#0x128]`; `0xA62D24 blx r3` | M6-006 gapA 1.11 | EXACT_SOURCE |
| 1.3 | **Sound** vtable `0x103BAD0` (create `0xA1D814` stores it at `0xA1D854`), `+0x128` = `0x103BBF8` -> **`0xA1D448`**. | `0xA1D84C add r2,r3,#8`; `0xA1D854 str r2,[r4]`; `0x103BBF8: 0x00A1D448` | NEW | EXACT_SOURCE |
| 1.4 | **RanSeq** vtable `0x103B860` (create `0xA07114` stores it at `0xA071BC`), `+0x128` = `0x103B988` -> **`0xA0AFDC`**. | `0xA07198 add r2,r1,#8`; `0xA071BC str r2,[r4]`; `0x103B988: 0x00A0AFDC` | M6-006 gapA 3.3 (body) + NEW (vtable) | EXACT_SOURCE |
| 1.5 | **Switch** vtable `0x103BDC8` (create `0xA2D9A8` stores it at `0xA2D9FC`), `+0x128` = `0x103BEF0` -> **`0xA2C730`**. | `0xA2D9F4 add r2,r3,#8`; `0xA2D9FC str r2,[r4]`; `0x103BEF0: 0x00A2C730` | M6-006 gapA 4.1 (body) + NEW (vtable) | EXACT_SOURCE |
| 1.6 | **ActorMixer** vtable `0x103CF88` (create `0xA66950` stores it at `0xA6699C`), `+0x128` = `0x103D0B0` -> **`0xA667D0`**. | `0xA66990 add r1,r3,#8`; `0xA6699C str r1,[r4]`; `0x103D0B0: 0x00A667D0` | NEW | EXACT_SOURCE |
| 1.7 | **Layer** vtable `0x103B050` (create `0x9D273C` stores it at `0x9D278C`), `+0x128` = `0x103B178` -> **`0x9D0758`**. | `0x9D2780 add r2,r3,#8`; `0x9D278C str r2,[r4]`; `0x103B178: 0x009D0758` | NEW | EXACT_SOURCE |
| 1.8 | **MusicSegment / MusicTrack** `+0x128`: not read (types 10..13 belong to M9). | HIRC dispatch table `0x9B3390..` type 10..13 slots not read | UNKNOWN | UNKNOWN |
| 1.9 | The PBI creator is the node's `vt+0x14` (not `+0x128`). For all node classes except RanSeq it is **`0xA02EC8`**; RanSeq's is **`0xA0B22C`** (which picks base vs ContinuousPBI on `params[0]==1`). | Sound `0x103BAE4: 0x00A02EC8`; RanSeq `0x103B874: 0x00A0B22C`; Switch `0x103BDDC: 0x00A02EC8`; ActorMixer `0x103CF9C: 0x00A02EC8`; Layer `0x103B064: 0x00A02EC8`; `0xA0B22C` decomp | NEW | EXACT_SOURCE |

**Note:** the `+0x128` values are the *stored* vtable slot (`[node]` + 0x128); the class names are
Wwise labels only. `0x103B7C8` (a nearby vtable) is a different object (its `+0x90` = `0x9ED2CC`,
the common base method); the Switch container vtable is `0x103BDC8`.

---

## Item 2 - the Sound `PlayInternal` body (`0xA1D448`) and PBI creation

### 2a. `0xA1D448(node, params)` body

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| 2a.1 | If `params+0x84 == 0x90` and `params+0x87 != 0`, take the special path (`0xA1D47C`); otherwise go to the normal path. | `0xA1D450 ldrb r3,[r1,#0x84]`; `0xA1D45C cmp r3,#0x90`; `0xA1D460 beq #0xA1D47C`; `0xA1D47C ldrb r3,[r1,#0x87]`; `0xA1D484 beq #0xA1D464` | NEW | EXACT_SOURCE |
| 2a.2 | **Normal path:** call `0xA379D8(node, node+0x5c, params)`. `node+0x5c` is the node's embedded source descriptor. | `0xA1D464 mov r2,r4`; `0xA1D468 mov r0,r5`; `0xA1D46C add r1,r5,#0x5c`; `0xA1D470 bl #0xA379D8` | NEW | EXACT_SOURCE |
| 2a.3 | **Special path** (`params+0x84==0x90`): `0x9EE230`; read `params+8`, `+0x85`, `+0x86`; RTPC-set `0x85` and `0x84` (the latter `*440.0`) via `0xA149C8`; `0x9EE174`; then node->`vt+8` on a 0x10-byte list node (`0xA7A7F4(0x10)`), with `+0xc`/`+0xd` = 0xFF. | `0xA1D488 bl #0x9EE230`; `0xA1D4F8 bl #0xA149C8`; `0xA1D520 bl #0x4D6778`; `0xA1D540 bl #0xA149C8`; `0xA1D548 bl #0x9EE174`; `0xA1D608 bl #0xA7A7F4`; `0xA1D5BC ldr r3,[r5]`; `0xA1D5C4 ldr r3,[r3,#8]`; `0xA1D5C8 blx r3` | NEW | EXACT_SOURCE for the code; **the semantics of the `0x90`/`0x87` branch and the `0x85`/`0x84`/`0x86` parameter meanings are UNKNOWN** |
| 2a.4 | The `0x90` branch returns `0x52` when `0x9EE230` != 1, otherwise the normal path is *not* taken and it returns after the list insert. | `0xA1D5CC cmp r8,#1`; `0xA1D5D0 movne r0,#0x52`; `0xA1D5D4 beq #0xA1D464` | NEW | EXACT_SOURCE |

### 2b. `0xA379D8(node, descriptor, params)` - the play helper

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| 2b.1 | Reads `params+4` (the target node) and calls `node->vt+0x14(node, node, local_84, params, gains)`; the result is the PBI. This is the PBI creation. | `0xA379E8 ldr sl,[r2,#4]`; `0xA37CA0 ldr r0,[r4,#4]`; `0xA37CB8 ldr ip,[r0]`; `0xA37CC4 ldr ip,[ip,#0x14]`; `0xA37CC8 blx ip` | NEW | EXACT_SOURCE |
| 2b.2 | PBI parameter/source init: `0x9BEB30(pbi+0xC, gain, uVar5, local_7c, &local_70, params+0x8C, &local_71)`; result must be 1 or it aborts. | `0xA37D1C bl #0x9BEB30`; `0xA37D20 cmp r0,#1`; `0xA37D28 bne #0xA37E78` | NEW | EXACT_SOURCE |
| 2b.3 | Builds a 0x20-byte play-param block `{params+0x70? , params+0x1ec, ...}` and calls `node->vt+0x90(node, &block, 1)`. | `0xA37D2C..0xA37D90`; `0xA37D88 ldr r6,[fp,#0x90]`; `0xA37D94 blx r6` | NEW | EXACT_SOURCE (the `vt+0x90` semantics are the node's own; not named) |
| 2b.4 | PBI Play: `0xA00618(pbi)`, then `0xA0067C(pbi, params+0xc, (params+0x70==1), 0)`. | `0xA38078 bl #0xA00618`; `0xA3807C sub r2,r6,#1`; `0xA38090 lsr r2,r2,#5`; `0xA38094 bl #0xA0067C` | NEW | EXACT_SOURCE |

### 2c. PBI creation (`0xA02EC8` -> `0xA000E8`)

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| 2c.1 | `0xA02EC8(node, node, srcBuf, params, gains)` allocates **0x1FC** bytes (`0xA7A7F4`) and calls `0xA000E8(pbi, params, node, srcBuf, gains, 0)`. | `0xA02EEC bl #0xA7A7F4` (r1=#0x1FC at `0xA02EDC`); `0xA02F00 mov r1,r7`; `0xA02F04 mov r2,r6`; `0xA02F10 mov r3,r5`; `0xA02F14 bl #0xA000E8` | NEW | EXACT_SOURCE |
| 2c.2 | **PBI ctor argument list:** `param_1` = PBI storage; `param_2` = play params struct (`sp+0x1c`); `param_3` = target node; `param_4` = source buffer (`local_84` from `0xA1ED48` / `node+0x5c`); `param_5` = gain array (`local_60`); `param_6` = 0 (base) / nonzero (continuous). | `0xA02F00..0xA02F14`; decomp `FUN_00a000e8` signature | NEW | EXACT_SOURCE |
| 2c.3 | Stores the base PBI vtable `0x103B768`; interfaces at `pbi+8 = 0x103B7D0`, `pbi+0xC = 0x103B7DC`. | `0xA00174 str ip,[r4]`; `0xA0017C str r0,[r4,#8]`; `0xA00184 str r3,[r4,#0xc]` | M6-022 C11 (PBI vtable base) | EXACT_SOURCE |
| 2c.4 | `pbi+0x140` (playing id) = `params+0x24`. | `0xA0019C str r1,[r4,#0x140]` | gapG 1.3 | EXACT_SOURCE |
| 2c.5 | `pbi+0x150` (source descriptor) = `param_4` (the `local_84` source buffer / `node+0x5c`); `pbi+0x14C` = `params+4` (the target node). | `0xA00208 str r7,[r4,#0x150]`; `0xA00204 str lr,[r4,#0x14c]`; decomp lines 67-68 | NEW | EXACT_SOURCE |
| 2c.6 | `pbi+0x164` (resampling ratio) = 1.0; `pbi+0x168`/`pbi+0x16C` (fade values) = 1.0. | `0xA00228 str sb,[r4,#0x164]`; `0xA0022C str sb,[r4,#0x168]`; `0xA00230 str sb,[r4,#0x16c]` (sb = 0x3F800000) | gapG 1.3 / 2.5 | EXACT_SOURCE |
| 2c.7 | `pbi+0x1D8` (start offset) = `params+0x74` (the queued sub-frame remainder / InitialDelay samples). | `0xA002EC str r8,[r4,#0x1d8]` | gapG 1.3 | EXACT_SOURCE |
| 2c.8 | Chain id: if `params+0x7C == 0`, `pbi+0x1C8 = global 0x1052434++`; else `pbi+0x1C8 = params+0x7C` and `pbi+0x1BE |= 8`. | `0xA00334 str r3,[r4,#0x1c8]`; `0xA00410 str r3,[r4,#0x1c8]`; `0xA00418 strb r2,[r4,#0x1be]`; decomp lines 111..119 | gapG 1.3 | EXACT_SOURCE |
| 2c.9 | `pbi+0x1BE` cleared to bits 6..7 and re-set from `params+0x128` bits 4..6; `pbi+0x1BD` = `(param_6!=0)<<7 | 0x44`; `pbi+0x1BF` bit2 = `params+0x128` bit2. | `0xA00288 strb r3,[r4,#0x1bd]`; `0xA002B0 strb r1,[r4,#0x1be]`; `0xA002D0 strb r2,[r4,#0x1bf]` | gapG 1.3 (partial) | EXACT_SOURCE |
| 2c.10 | `pbi+0x1F8` (a voice/chain link) written; `pbi+0x1E4` = `params+0x84`; `pbi+0x1F0`/`+0x1F4` zeroed. | `0xA00318 str r7,[r4,#0x1f8]`; `0xA002FC..0xA00314` | NEW | EXACT_SOURCE for the stores; **the meaning of `+0x1F8`/`+0x1E4` is not named** |
| 2c.11 | Source-format placeholders: `pbi+0x15C`/`pbi+0x15D` set from `params+0x10`/`params+0x11`-like bytes (`0xA001F0`/`0xA001F8`), then overwritten by the source StartStream (Item 5). | `0xA001F0 strb ip,[r4,#0x15c]`; `0xA001F8 strb r2,[r4,#0x15d]`; `0xA0034C`/`0xA00354` | NEW | EXACT_SOURCE |
| 2c.12 | `pbi+0x18`/`pbi+0x28`/`pbi+0x20`/`pbi+0x24` set from `pbi+0x140` and the RTPC key (`0xA003C8 str r4,[r4,#0x28]`; `0xA003CC str r2,[r4,#0x18]`; `0xA003A4/0xA003A8 strb`). | `0xA003A4..0xA00400` | NEW | EXACT_SOURCE for the stores; the field roles are the RTPC key/game object (gapA 1.11) |

### 2d. PBI vtable `0x103B768` (base) - slots read

| offset | target | role (label) | citation |
|---|---|---|---|
| +0x00 | `0x9FF7B8` | destructor | `0x103B768` |
| +0x04 | `0x9FF54C` | delete (frees sub-objects; the flush does the actual free) | `0x103B76C`; M6-022 Q3 |
| +0x0C | `0xA0285C` | (called from `0xA379D8` as `pbi->vt+0xC`) | `0x103B774` |
| +0x10 | `0xA029DC` | Term | `0x103B778`; M6-022 Q3 |
| +0x14 | `0x9FF41C` | TransitionUpdate | `0x103B77C`; gapC 5.3 |
| +0x44 | `0x9FFAD4` | CalcEffectiveParams | `0x103B7AC` |
| +0x50 | `0x9FF4D0` | (called from PBI Play with `(0xe, iVar1)`) | `0x103B7B8` |
| +0x6C | `0x9FF6B8` | (called from CalcEffectiveParams as `node->vt+0x6c`) | `0x103B7D4` |

`ContinuousPBI` vtable `0x103D3B0`: `+0x04 = 0xA6A018`, `+0x10 = 0xA6ACC0`, `+0x44 = 0x9FFAD4`
(M6-022 Q3; confirmed).

---

## Item 3 - `CalcEffectiveParams 0x9FFAD4` (PBI `vt+0x44`)

Callers: the PBI `vt+0x44` slot (`0x103B768+0x44` = `0x9FFAD4`; the derived `0x988658` thunks it).
`param_1` = PBI, `param_2` = the node/params block (or NULL).

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| 3.1 | Bus from `0x9BDA6C(pbi+0xC)`; if `param_2==0`, the bus changed, or `pbi+0xE9` bit2 set: reset the effective params to 0 and `pbi+0x40 = 1.0`. | `0x9FFAD4`; `0x9FFB10 ldrb r2,[r4,#0x58]`; `0x9FFB24 str ip,[r4,#0x40]`; `0x9FFB48 str r5,[r4,#0x3c]`; `0x9FFB4C/50/54` | gapC 1.1 | EXACT_SOURCE |
| 3.2 | Otherwise `memcpy(pbi+0x3c, param_2, 0x5c)`; copy the randomizer/ranges from `param_2+0x5c..` and set `pbi+0x118..+0x128`. | `0x9FFC... memcpy`; decomp lines 108..119 | gapC 1.6 | EXACT_SOURCE |
| 3.3 | Node-chain + bus accumulation: `0x9FBE74(pbi+0xe0, pbi+0x14, pbi+0xb4, pbi+0xdc)`. | `0x9FFC68 bl #0x9FBE74` | gapA 6.1 / gapC 1.2 | EXACT_SOURCE |
| 3.4 | `pbi+0xc4 = 101.0`; then if `pbi+0xe0 != param_2`: `pbi+0xe0->vt+0xac(pbi+0xe0, pbi+0x3c, uVar6, pbi+0x10c, pbi+0x14, ranges, param_2, 1, param_2)`. This is GetAudioParameters; it sums Volume/Pitch/LPF/HPF into `pbi+0x3c/+0x44/+0x48/+0x4c`. | `0x9FFCEC ldr sb,[lr,#0xac]`; `0x9FFCF0 addeq ip,r4,#0x118` | gapA 6.1 / gapC 1.3 | EXACT_SOURCE |
| 3.5 | Compose: `pbi+0x9c = pbi+0x48 + pbi+0x124`; `pbi+0xa4 = pbi+0x4c + pbi+0x128`; `pbi+0x98 = pbi+0x3c`; `pbi+0x48 += pbi+0xa0`; `pbi+0x4c += pbi+0xa8`; `pbi+0x44 += pbi+0x120`. | `0x9FFD14..0x9FFD74` | gapC 1.3 | EXACT_SOURCE |
| 3.6 | Mute/fade product: product of the 0xC-stride muted-map entries at `pbi+0x10c`, times `pbi+0x168 * pbi+0x16C`; `pbi+0x40 = max(0, product)`. | `0x9FFDD0..0x9FFE18`; `0x9FFE18 vstr s15,[r4,#0x40]` | gapC 1.11 | EXACT_SOURCE |
| 3.7 | `pbi+0x64 += bus volume` (`fVar17`); `pbi+0x3c += pbi+0x118`; then `0x9F6B94(&local, pbi+0xe0, pbi+0x14)` and the RTPC/`pbi+0x1CC` update. | `0x9FFDEC vldr s11,[r4,#0x118]`; `0x9FFE0C vstr s13,[r4,#0x3c]`; `0x9FFE1C bl #0x9F6B94`; `0x9FFE24..` | gapC 1.6 | EXACT_SOURCE |
| 3.8 | Marks the params dirty: `pbi+0x1bc |= 1`, `pbi+0xe8 |= 0x20`. | `0x9FFE7C..` (decomp lines 274-275) | gapC 1.6 | EXACT_SOURCE |

**Field map (established):** `pbi+0x3C` Volume (dB), `pbi+0x44` Pitch (cents), `pbi+0x48` LPF,
`pbi+0x4C` HPF, `pbi+0x40` mute/fade linear factor. `pbi+0x158` is the source **format** word,
written by the source StartStream (Item 5), not by CalcEffectiveParams.

---

## Item 4 - voice creation / attach (`0xA4304C`)

**Caller:** `0x9D3644` (the `0x108DA10` drain) at `0x9D36E8`, reached from `0x9D3C98` at
`0x9D3CB0`. `0x9D3C98` is a `Perform` group member (`0x9AFA7C`). `0xA4304C` has no other caller.

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| 4.1 | If `pbi+0x1C8 != 0`, walk the live-voice list `0x108DF68` (next `voice+0xD0`) and compare `[voice+8]+0x1BC` with `pbi+0x1C8`. Match: `AddSrc(voice, pbi, 0)`, `0xA01878(pbi)`, return 5. | `0xA43058 ldr r2,[r4,#0x1c8]`; `0xA43064..0xA43100`; `0xA430E8 ldr r3,[r3,#0x1bc]`; `0xA43114 mov r1,r4`; `0xA4311C bl #0xA558AC`; `0xA43128 mov r0,#5` | gapG 1.5 | EXACT_SOURCE |
| 4.2 | No match: allocate **0x540** bytes (`0xA7A7F4`). Alloc failure: `0xA01800(pbi,1)`, return 2. | `0xA43078 ldr r3,[pc,#0x11c]`; `0xA4307C mov r1,#0x540`; `0xA43088 bl #0xA7A7F4`; `0xA43160..0xA4316C` | gapG 1.5 | EXACT_SOURCE |
| 4.3 | Voice ctor/init `0xA54650(voice)`: zeroes the voice, sets the voice vtable `0x103C790`, and inits the filter/gain/resampler sub-objects. | `0xA43094 bl #0xA54650`; `0xA54650` decomp (`*param_1 = &PTR_FUN_0103c790`) | NEW | EXACT_SOURCE |
| 4.4 | `0xA548B8(voice, engine)` sets `voice+0xEC = engine`. | `0xA430A8 bl #0xA548B8`; `0xA548B8` body `str r1,[r0,#0xec]` | NEW | EXACT_SOURCE |
| 4.5 | `AddSrc(voice, pbi, 1)`; return 0x3F: link the new voice into the list (`voice+0xD0 = 0`, `0x108DA30`/`0x108DA2C`), return 1. Return 1: `0xA42DEC(voice,pbi)`; return 2/other: `0x9D40C4(voice, ...)`. | `0xA430B8 bl #0xA558AC`; `0xA430BC cmp r0,#0x3f`; `0xA43130..0xA4315C`; `0xA43174 bl #0xA42DEC`; `0xA430DC bl #0x9D40C4` | gapG 1.5 | EXACT_SOURCE |

**Voice vtable `0x103C790` slots read (labels only):** `+0x44 = 0xA53EA8` (Term, gapD D3.4),
`+0x48 = 0xA533FC` (Stop), `+0x6C` used by `0xA54A30`. The class name is UNKNOWN.

**Fields the voice copies from the PBI (via `AddSrc`):** `voice+0xD4` (current source) or `voice+0xD8`
(pending), `voice+8` = `source+0xC+0xC` (the bus connection owner), `voice+0xE0`/`voice+0xE4`
(next-source index/code), `voice+0xCD` bit0, and `voice+0x1C` (output gain) from the PBI mute/fade
product. `voice+0x100` (pitch node), `voice+0x380` (gain/ramp), `voice+0x1C0` (filter A),
`voice+0x390` (filter B) are initialised in `0xA54A30`/`0xA44630`. | `0xA55934 str r5,[r4,#0xd4]`;
`0xA55948 str r3,[r4,#8]`; `0xA559C4 bl #0xA01768`; `0xA559D0 str r0,[r4,#0xe4]`;
`0xA54A30` decomp | gapG 1.5, M6-022 | EXACT_SOURCE

---

## Item 5 - source creation and format

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| 5.1 | `AddSrc 0xA558AC(voice, pbi, bActive)`: `0xA01E24(pbi, &mode, &plugin)`, then `0xA562B8(mode, plugin, pbi)`. | `0xA558CC bl #0xA01E24`; `0xA558D8 bl #0xA562B8` | gapG 1.6 | EXACT_SOURCE |
| 5.2 | `0xA01E24(pbi,&mode,&plugin)`: `plugin = [[pbi+0x150]+0x14]`; `mode = ([[pbi+0x150]+0xc] & 0x7f) >> 2`; if `pbi+0x1dc` and `pbi+0x1e0` are non-zero, `0x9CD340` refines the plugin (-> `0x10001`) and/or mode (-> 3). | `0xA01E24 ldr r3,[r0,#0x150]`; `0xA01E30 ldr lr,[r3,#0x14]`; `0xA01E34 ldrb r3,[r3,#0xc]`; `0xA01E3C ubfx r3,r3,#2,#5`; `0xA01E40 str r3,[r1]`; `0xA01E44 str lr,[r2]`; `0xA01E5C bl #0x9CD340` | M6-002 / gapG 4.3 | EXACT_SOURCE |
| 5.3 | **Factory `0xA562B8(mode, plugin, pbi)`:** `mode==2` -> alloc 0x70 + `0xA78D10`; `plugin>>16==1` (PCM) -> `0xA76140` (mode 1) / `0xA72D04` (mode 3); `plugin>>16==2` (ADPCM) -> `0xA74244` (mode 1) / `0xA72A2C` (mode 3); `plugin>>16>2` -> registered-plugin list at `0x108D9DC` (3-word entries `{id, fnMode1, fnOtherwise}`); `mode==0` or unknown -> 0. | `0xA562BC cmp r0,#2`; `0xA562E0 lsr r1,r1,#0x10`; `0xA562F0 cmp r1,#2`; `0xA562E8 beq` (PCM); `0xA5633C..0xA5634C` (registered); `0xA562D4 cmp r3,#0` | M6-002 gapG 4.3 | EXACT_SOURCE |
| 5.4 | Shipped plugin ids: `0x00040001` Vorbis (plugin>>16=4 -> registered path), `0x00020001` IMA ADPCM (>>16=2). No bank uses `0x00010001` (PCM). | bank census in `re-analysis/research/20260928-B-M6b-source-classes.md` §1c (bank bytes, authority 3) | M6-002/M6-003 | EXACT_SOURCE (bank data) |
| 5.5 | `AddSrc` then: `0xA56650(source, pbi+0x1dc, pbi+0x1e0)` (StartStream); on 1/0x3F store `voice+0xD8` (bActive=0) or `voice+0xD4` (bActive=1), set `voice+8 = [source+0xC]+0xC`, clear `pbi+0x1BE` bit3. | `0xA5590C ldr r1,[r7,#0x1dc]`; `0xA55910 ldr r2,[r7,#0x1e0]`; `0xA55914 bl #0xA56650`; `0xA55934 str r5,[r4,#0xd4]`; `0xA55948 str r3,[r4,#8]`; `0xA5593C ldr r3,[r5,#0xc]` | gapG 1.6 | EXACT_SOURCE |
| 5.6 | **Sound SetInitialValues `0xA1DA08`:** `0x9B9C90(&srcBlock, &reader)` reads the Sound source block (u32 plugin, u8 stream, u32 sourceId, u32 size, u8 bits); then `0xA1EA68(node+0x5c, ...)` copies it to `node+0x5c`; then `0x9F6EF8` reads NodeBase. | `0xA1DA08`; `0xA1DA24 bl #0x9B9C90`; `0xA1DA28 cmp local_20`; `0xA1DA30 bl #0xA1EA68`; `0xA1DA34 bl #0x9F6EF8` | M6-001 gapA 2.4/2.8 | EXACT_SOURCE |
| 5.7 | The source block layout the reader consumes: `u32 plugin, u8 stream, u32 sourceId, u32 size, u8 bits`; for `plugin&0xF in {2,5}` a `u32 size` + size bytes. | `0x9B9CC8 ldr lr,[r2],#4`; `0x9B9CE0 ldrb lr,[r3,#4]`; `0x9B9CC4 ldr r1,[r3,#5]`; `0x9B9CCC ldr sb,[r3,#9]`; `0x9B9CFC ldrb r1,[r3,#0xd]`; `0x9B9D4C ldr r2,[r3,#0xe]`; `0x9B9D50 add r3,r3,#0x12` | M6-001 gapA 2.4 | EXACT_SOURCE |
| 5.8 | The stream-byte -> mode mapping: stream 0 -> 3, stream 1/2 -> 1 (only for codec plugins `plugin&0xF==1`); source plugins leave mode 0. | `0x9B9CD0 and ip,lr,#0xf`; `0x9B9CD8 cmp ip,#1`; `0x9B9D80 cmp lr,#0`; `0x9B9DA0 mov r2,#3`; `0x9B9DA8 bfi r3,r2,#2,#5`; `0x9B9DBC bfi r3,ip,#2,#5` | M6-002 gapG 4.3 | EXACT_SOURCE |
| 5.9 | **`pbi+0x158` (source format word) writes:** Vorbis StartStream `0xAB0BF0 str r1,[lr,#0x158]` (`lr=[source+0xC]` = the PBI; `r1=[fmt+4]`); ADPCM type-3 StartStream `0xA72744 str lr,[sb,#0x158]` (`lr=[fmt+4]`). Vorbis also writes `pbi+0x15C` channels (`0xAB0C20`), `pbi+0x15D` config (`0xAB0C04`); ADPCM t3 writes `+0x15C`/`+0x15D` at `0xA72760`/`0xA72768`. | `0xAB0BDC ldr r1,[ip,#4]`; `0xAB0BF0 str r1,[lr,#0x158]`; `0xA72720 ldr lr,[r3,#4]`; `0xA72744 str lr,[sb,#0x158]` | NEW | EXACT_SOURCE |
| 5.10 | ADPCM type-1 StartStream `0xA7538C`: no write to `pbi+0x158` found in the body (`0xA7538C..0xA75680`). | scan of `0xA7538C..0xA75680` for `#0x158` = 0 hits | NEW | **RECOVERABLE_GAP**: read where the type-1 class obtains its format (the ctor `0xA74504` and the render `0xA73D34` read `pbi+0x158`) |

---

## Item 6 - the start-list / PBI Play

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| 6.1 | `0xA0067C(pbi, arg2, flag, arg4)`: if `arg2[0] != 0`, set up the fade-in transition (`0xA36268` / `0xA366F4`) with `pbi+0x51` as the transition slot; `pbi+0x5a = 0`; call `pbi->vt+0x50(pbi, 0xe, iVar1)`. | `0xA0067C`; `0xA00800 bl #0xA36268`; `0xA007B0 bl #0xA366F4`; decomp lines 23-47 | gapG 1.4 | EXACT_SOURCE |
| 6.2 | If `flag==0` and `pbi+0x1ba & 7 != 1`: enqueue start-list **type 0** via `0x9D3558(0, pbi)`. Else set `pbi+0x1BC |= 0x80` and enqueue **type 1** via `0x9D3558(1, pbi)`; then `0xA366AC`, `0x9BDA28(pbi+0xC, 1)`, `0x9E808C`. | `0xA006BC mov r0,r8`; `0xA006C0 mov r1,r4`; `0xA006C4 bl #0x9D3558`; `0xA006EC orr r3,r3,#0x80`; `0xA006F0 strb r3,[r4,#0x1bc]`; `0xA006F4 bl #0x9D3558`; `0xA00714/20/30` | gapG 1.4 | EXACT_SOURCE |
| 6.3 | `0x9D3558(type, pbi)` enqueues a 0x10-byte node `{next, pbi, tick}` into the list head `0x108DA10` / tail `0x108DA14`; count `0x108DA24`++; if `type < 2` set the gate byte `0x108DA34 = 1`. | `0x9D3558` body; `0x9D3570..0x9D3598`; `0x9D35A4 str`; `0x9D35A8 strb r?,[...]` | NEW | EXACT_SOURCE |
| 6.4 | `0x9D3C98` (Perform group member 3) gates on `0x108DA34`: if set, `0x9D3644`; then always `0x9D3864`. | `0x9D3C9C ldr r3,[pc,#0x18]`; `0x9D3CA4 ldrb r3,[r3,#0x28]`; `0x9D3CAC beq #0x9D3CB4`; `0x9D3CB0 bl #0x9D3644`; `0x9D3CB8 b #0x9D3864` | M6-022 (Perform) | EXACT_SOURCE |
| 6.5 | `0x9D3644` walks `0x108DA10`; for each node whose PBI `[node+4]` has `+0x154==0`, `[node+0xc]<=1`, and not (`pbi+0x1BC&0x20` and `pbi+0x1F8==-1`): call `0xA4304C(node+4)`; keep the node if it returns 1, else unlink/free and `0xA01800(pbi,1)`. | `0x9D3644`; `0x9D3654 ldr r4,[r3,#4]`; `0x9D36B0 ldr sl,[r4,#4]`; `0x9D36B4 ldr r2,[sl,#0x154]`; `0x9D36C0 ldrb r2,[r4,#0xc]`; `0x9D36E4 add r0,r4,#4`; `0x9D36E8 bl #0xA4304C`; `0x9D36EC cmp r0,#1` | M6-022 Q6 (gap1) | EXACT_SOURCE |
| 6.6 | Perform order: `0x9FF308`, **`0x9D3C98`** (`0x9AFA7C`), `0x9E6D2C`, LEngine `0xA57FF8` (`0x9AFA90`), PBI flush `0xA38420` (`0x9AFA94`), then `mgr+0x4C++`. | `0x9AFA78 bl #0x9FF308`; `0x9AFA7C bl #0x9D3C98`; `0x9AFA8C bl #0x9E6D2C`; `0x9AFA90 bl #0xA57FF8`; `0x9AFA94 bl #0xA38420`; `0x9AFAA0 ldr r3,[r4,#0x4c]` | M6-017 D1.6 | EXACT_SOURCE |
| 6.7 | `0xA38420` is **not** the voice-attach drain: it drains the PBI notification queue (`0x108DE7C`/`0x108DE90`) and on code 4 runs `0x9D3470`, `pbi->vt+0x10` (Term), `pbi->vt+4`, `0xA7A988`. | `0xA38420`; `0xA38484` code test; `0xA384CC bl #0x9D3470`; `0xA384DC` Term; `0xA384FC` delete | M6-022 Q3 | EXACT_SOURCE |

---

## Item 7 - genuinely UNKNOWN / RECOVERABLE_GAP

1. **Sound PlayInternal special branch (`0xA1D448` when `params+0x84 == 0x90` and `+0x87 != 0`).**
   The code is read (`0xA1D47C..0xA1D638`), but the Wwise meaning of `params+0x84`/`0x87`/`0x85`/`0x86`
   and of the RTPC ids `0x84`/`0x85` is not in the binary. **UNKNOWN.** Addresses tried:
   `0xA1D47C..0xA1D638`.
2. **`params+4` is the node (confirmed), but the full play-params struct layout (base `sp+0x1c`,
   `0xA62A1C..0xA62D28`) is only partly mapped.** The fields read by `0xA000E8`/`0xA379D8`
   (`+4`, `+0xC`, `+0x14`, `+0x24`, `+0x70`, `+0x74`, `+0x7C`, `+0x84`, `+0x128`, `+0x1dc`,
   `+0x1e0`) are cited, but the whole struct is not enumerated. **RECOVERABLE_GAP**: read the
   `0xA62A1C` stack-struct writer in full (`0xA62A1C..0xA62D38`).
3. **PBI `+0x1F8` / `+0x1E4` / `+0x14C` meanings** (stores at `0xA00318`, `0xA002FC`, `0xA00204`).
   **UNKNOWN**; not named by any reader I read.
4. **`pbi+0x158` for the ADPCM type-1 class (`0xA7538C`).** No write found in the StartStream body;
   the render `0xA73D34` reads `[pbi+0x158]`. **RECOVERABLE_GAP**: read the type-1 ctor `0xA74504`
   and the type-1 prefetch path.
5. **The PBI vtable slot names** (`0x103B768`) beyond Term/delete/TransitionUpdate/
   CalcEffectiveParams are labels only; the class name is **UNKNOWN** (no RTTI/symbols).
6. **MusicSegment/MusicTrack `+0x128`** (M9) not read. **UNKNOWN**.
7. **`0xA0B22C`'s continuous selection** (`params[0]==1` -> `0xA6A880` ContinuousPBI) is read, but
   what sets `params[0]` and the ContinuousPBI render/play path is M6-008's scope. **UNKNOWN** here.
8. **`0xA01E24`'s `0x9CD340` refinement** (the `pbi+0x1dc`/`pbi+0x1e0` stream-format check that can
   override the plugin to `0x10001` or mode to 3) is read only at the call site; `0x9CD340`'s
   internals are **RECOVERABLE_GAP** (it is the media-format/stream reader).
9. **The writer of the PBI `+0x14` RTPC key** (task asks for it) was not located in the creation
   path read; `0xA000E8` sets `+0x18`/`+0x28` from the playing id/game object, and `0x9BC90C`
   (called at `0xA000E8` `0xA00130`) is the RTPC-key init. **RECOVERABLE_GAP**: read `0x9BC90C`.

---

## Existing records contradicted or too weak

- **M6-022 C11 (the live voice/bus engine record) does not own this path.** It lists `0xA4304C`,
  `0xA558AC`, `0xA56650` as callees but has no row for the PBI creation (`0xA000E8`), the node
  `vt+0x14` creator, the `0x9D3558` start list, or the `0x9D3644` drain. The task's own premise
  ("the frozen M6 inventory has no rows for this creation path") is correct.
- **M6-006 (control path)** ends at "then PlayInternal" (gapA 1.11) and does not name the node
  `+0x128` targets or the PBI creation. The `0xA62A1C` citation is confirmed; the record is a
  partial.
- **The task premise "the drain in 0xA38420"** is contradicted: `0xA38420` is the PBI Term/notification
  flush (`0x108DE7C`), not the start-list drain. The start-list (`0x108DA10`, enqueue `0x9D3558`)
  is drained by `0x9D3644` via `0x9D3C98`. Citations: `0xA38484` (code 4 test) vs `0x9D36E8`
  (`0xA4304C`).
- **The task premise that the PBI creator is reached from the Sound PlayInternal via the action** is
  corrected: it is `params+4` = the target node (`0xA62B90`), and `0xA37CC4` calls `node->vt+0x14`.
- **`re-analysis/research/20260928-B-M6b-source-classes.md` headline 3** ("the task table's
  `vt+0x30` column is not the dispatcher's slot") is confirmed for the render slots; this pass adds
  the `vt+0x14` PBI-creator slot and the `vt+0x128` PlayInternal slot, which that report did not
  tabulate.

## Open questions for the manager

1. **New record id.** The creation path (Sound PlayInternal, node `vt+0x14` PBI creator, the base
   PBI ctor `0xA000E8`, `CalcEffectiveParams` ownership, the voice ctor `0xA54650`, `AddSrc`
   `0xA558AC`, the `0x9D3558`/`0x9D3644` start list) is a coherent subsystem. It should be a new
   M6 record (M6-025?) rather than folded into M6-022, because M6-022's title is the *live voice and
   bus engine* and its evidence does not name this path.
2. **`CalcEffectiveParams` ownership.** It is PBI `vt+0x44` and calls the node chain's
   `vt+0xac` (GetAudioParameters, M6-010). Does it belong to the new creation record or to M6-010?
3. **The `0x90` Sound branch.** If the operator's hardware run can be made to exercise a Sound whose
   `params+0x84 == 0x90`, a capture could name it; otherwise it stays UNKNOWN.
4. **The ADPCM type-1 `pbi+0x158` writer** is the one field the shipped ADPCM path needs that this
   pass did not find; it is a RECOVERABLE_GAP.
