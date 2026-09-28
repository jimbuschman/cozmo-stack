# M6b — source class identity and the `vt+0x30` / `vt+0x4C` bodies

Read-only extraction from `resources/lib/armeabi-v7a/libcozmoEngine.so` (3.4.0-1204, ARM mode; Wwise 2016.2 at 0x0095E540..0x00AE2E40). All addresses are ELF VAs; for `.text`/`.rodata` VA == file offset, for the data region VA >= 0x1019200 the file offset is VA-0x1000. Ghidra was used only to navigate. Citations are instructions in the `.so`.

## Headline (four corrections to the task premise)

1. **The factory dispatch is `(mode, plugin>>16)`, but the `r1>>16` mapping is 1=PCM, 2=ADPCM, and >2 = the registered-plugin path.** The bank plugin ids are `0x00020001` (IMA ADPCM) and `0x00040001` (Vorbis), so ADPCM is `>>16 == 2` and Vorbis is `>>16 == 4`. The task table's "`r1>>16==0/2`" for the ADPCM classes is wrong; it is `==2`.
2. **Vorbis is not one of the six factory classes.** `plugin>>16 == 4` falls through to `0x9CC3EC`, the `g_pAKPluginList` lookup, which creates the Vorbis class (stored vtable `0x103E0B8` streamed / `0x103E138` in-memory). No shipped source uses the PCM classes (`0x00010001` appears in no bank), and no bank uses `plugin>>16 == 1`.
3. **The task table's `vt+0x30` column is not the dispatcher's slot for four of the classes.** The dispatcher reads `[source_vtable + 0x30]` where `source_vtable` is the pointer the ctor stores. The ctors store `vtable_base + 8` for the ADPCM classes (`0xA74244` → `0x103D840`; `0xA72A2C` → `0x103D6C0`) and `0xA72D04` → `0x103D740`, but `vtable_base` for `0xA76140`/`0xA78D10`. So the real render slots are `0xA73D34` (ADPCM t1), `0xA72554` (ADPCM t3), `0xA72BA0` (PCM t3), not the listed `0xA7538C`/`0xA72674`/`0xA73128`. The listed `0xA7538C`/`0xA72674` are the **StartStream** at `vt+0x28` (gapG 4.4's addresses); `0xA73128` is `vt+0x2C`.
4. **The `vt+0x4C` body for the PCM/ADPCM/Vorbis classes is `0xA72B14`, not `0xA566B8`.** `0xA566B8` is `vt+0x44`, `0xA566C0` is the base class's `vt+0x4C`, `0xA566C8` is the mode-2 class's `vt+0x4C`.

Also: `0xA4479C` stores the **frame size** `0x400` (from global `0x1052440`) at `params+0xC`, not `0x108DF98` (that global is not read at this site).

---

## Q1 — which source type the shipped events use; the factory dispatch

### 1a. The factory `0xA562B8(param_1 = mode, param_2 = plugin, param_3 = PBI)`

| condition | action | citation |
|---|---|---|
| `param_1 == 2` | alloc 0x70, `0xA78D10` | 0xA562BC `cmp r0,#2`; 0xA562D0 `beq 0xa56388`; 0xA5638C `mov r1,#0x70`; 0xA563A8 `bl 0xa78d10` |
| `param_1 == 0` | return 0 | 0xA562D4 `cmp r3,#0`; 0xA562D8 `beq 0xa563b4`; 0xA563B4 `mov r4,r3` |
| else `t = param_2 >> 16` | | 0xA562E0 `lsr r1,r1,#0x10` |
| `t == 0` | return 0 | 0xA562E4 `cmp r1,#1`; 0xA562EC `blo 0xa56330`; 0xA56330 `mov r4,#0` |
| `t == 1` (PCM), `mode == 1` | alloc 0x6C, `0xA76140` | 0xA562E8 `beq 0xa56350`; 0xA56350 `cmp r3,#1`; 0xA56354 `beq 0xa563e4`; 0xA563E8 `mov r1,#0x6c`; 0xA56404 `bl 0xa76140` |
| `t == 1`, `mode == 3` | alloc 0x40, `0xA72D04` | 0xA56358 `cmp r3,#3`; 0xA5635C `bne 0xa56330`; 0xA56364 `mov r1,#0x40`; 0xA56380 `bl 0xa72d04` |
| `t == 2` (ADPCM), `mode == 1` | alloc 0x70, `0xA74244` | 0xA562F0 `cmp r1,#2`; 0xA562F4 `bne 0xa5633c`; 0xA562F8 `cmp r3,#1`; 0xA562FC `beq 0xa563bc`; 0xA563C0 `mov r1,#0x70`; 0xA563DC `bl 0xa74244` |
| `t == 2`, `mode == 3` | alloc 0x48, `0xA72A2C` | 0xA56300 `cmp r3,#3`; 0xA56304 `bne 0xa56330`; 0xA5630C `mov r1,#0x48`; 0xA56328 `bl 0xa72a2c` |
| `t > 2` | `0x9CC3EC(plugin, mode, param_3)` registered-plugin lookup | 0xA5633C `mov r0,r2`; 0xA56340 `mov r1,r3`; 0xA56344 `mov r2,ip`; 0xA5634C `b 0x9cc3ec` |

`0x9CC3EC` walks `g_pAKPluginList` (head `[0x108D9DC]`, count `[0x108D9E0]`, 0xC-byte entries `{id, fnForMode1, fnOtherwise}`): 0x9CC3F4 `ldr r4,[r3]`, 0x9CC3F8 `ldm r3,{r4,lr}`, 0x9CC40C `ldr r3,[r4]`/`cmp r2,r3`, 0x9CC448 `cmp r1,#1`, 0x9CC450 `ldr r3,[r4,#8]` (mode != 1), 0x9CC45C `ldr r3,[r4,#4]` (mode == 1).

### 1b. Where `mode` and `plugin` come from

AddSrc `0xA558AC` calls `0xA01E24(pbi, &mode, &plugin)` (0xA558CC `bl 0xa01e24`), then `0xA562B8(mode, plugin, pbi)` (0xA558D8 `bl 0xa562b8`).

`0xA01E24`:
- `r3 = [pbi+0x150]` (0xA01E24 `ldr r3,[r0,#0x150]`) — the source descriptor embedded in the Sound node at `soundNode+0x5c`.
- `plugin = [r3+0x14]` (0xA01E30 `ldr lr,[r3,#0x14]`; 0xA01E44 `str lr,[r2]`). This is the raw HIRC Sound source plugin id.
- `mode = ([r3+0xc] >> 2) & 0x1F` (0xA01E34 `ldrb r3,[r3,#0xc]`; 0xA01E3C `ubfx r3,r3,#2,#5`; 0xA01E40 `str r3,[r1]`). This is the source-flags byte bits 2..6.

The descriptor is filled by the Sound source reader `0x9B9C90` (caller Sound::SetInitialValues `0xA1DA38`), which writes its dest struct; `0xA1EA68` copies it to `soundNode+0x5c`:
- dest+4 (plugin) → `node+0x70` (0xA1EA68 `str r6,[r4,#0x14]` with r4=node+0x5c).
- dest+0x14 (flags) → `node+0x68` (0xA1EA68 `stm r4,{r0,r1,r2,r3}` after `ldm r5,{r0,r1,r2,r3}`; the flags byte is the 4th word).
- `node+0x5c+0x14` = plugin, `node+0x5c+0xc` = flags; matches `0xA01E24`'s `+0x14`/`+0xc`.

`0x9B9C90` sets the mode bits (2..6 of the flags byte) only for **codec** plugins (`plugin & 0xF == 1`):
- `0x9B9CD0 and ip,lr,#0xf`; `0x9B9CD8 cmp ip,#1`; `0x9B9D2C beq 0x9b9d80`.
- `0x9B9D80 cmp lr,#0` (stream byte); `0x9B9D84 beq 0x9b9d9c` → `0x9B9DA0 mov r2,#3`; `0x9B9DA8 bfi r3,r2,#2,#5` → **mode = 3 when stream == 0**.
- else `0x9B9D88 sub lr,lr,#1`; `0x9B9D90 bls 0x9b9db4` (stream 1 or 2) → `0x9B9DBC bfi r3,ip,#2,#5` → **mode = ip = 1**.
- stream > 2 → return 2 (0x9B9D94).
- Source plugins (`plugin & 0xF == 2 or 5`, e.g. Sine `0x00640002`, WavePortal `0x006412C2`) take `0x9B9D4C` (0x9B9D38 `beq 0x9b9d4c`), which reads the plugin-param block and **does not write the mode bits**; mode stays 0.

### 1c. Shipped bank plugin ids (parsed from `AudioAssets.zip` HIRC type-2 bodies)

| bank | plugin id | count | stream bytes |
|---|---|---|---|
| Cozmo.bnk | `0x00040001` Vorbis | 1871 | 27 × 0, 1826 × 1, 18 × 2 |
| Cozmo.bnk | `0x00020001` IMA ADPCM | 333 | 333 × 1 |
| Cozmo.bnk | `0x006412C2` WavePortal | 15 | 0 |
| Cozmo.bnk | `0x00650002` Silence | 11 | 0 |
| Cozmo.bnk | `0x00640002` Sine | 1 | 0 |
| SFX.bnk | `0x00040001` / `0x00640002` | 97 / 4 | 1,0,2 |
| UI.bnk | `0x00040001` / `0x00640002` | 13 / 1 | 0,2 |
| Dev_Debug.bnk | `0x00660002` ToneGen / `0x00640002` / `0x006412C2` | 9 / 3 / 2 | 0 |

The Sound source block (reader `0x9B9C90`) is `u32 plugin, u8 stream, u32 sourceId, u32 size, u8 bits` (`0x9B9CC8 ldr lr,[r2],#4`; `0x9B9CE0 ldrb lr,[r3,#4]`; `0x9B9CC4 ldr r1,[r3,#5]`; `0x9B9CCC ldr sb,[r3,#9]`; `0x9B9CFC ldrb r1,[r3,#0xd]`), then for `plugin & 0xF in {2,5}` a `u32 size` + size bytes (`0x9B9D4C ldr r2,[r3,#0xe]`; `0x9B9D50 add r3,r3,#0x12`). This matches `WwiseHierarchy.ReadSound` (line 447) and the runtime.

`plugin>>16` therefore is the Wwise codec/plugin class id in the high half of the id: `0x0001` PCM, `0x0002` ADPCM, `0x0004` Vorbis, `0x0010` MIDI, `0x6400` Sine, `0x6412` WavePortal, `0x6500` Silence, `0x6600` ToneGen. (`WwiseMidi.cs:214` independently records `0x00100001` as the MIDI id.)

### 1d. Mapping for the shipped robot-audio types

| shipped type | plugin | stream | mode | selected class | stored vtable | render `vt+0x30` | StartStream `vt+0x28` |
|---|---|---|---|---|---|---|---|
| IMA ADPCM | `0x00020001` | 1 | 1 | `0xA74244` | `0x103D840` | `0xA73D34` | `0xA7538C` |
| IMA ADPCM | `0x00020001` | 0 | 3 | `0xA72A2C` | `0x103D6C0` | `0xA72554` | `0xA72674` |
| Vorbis | `0x00040001` | 1 or 2 | 1 | registered (`0x9CC3EC`) | `0x103E0B8` | `0xAB0448` | `0xAB0B20` |
| Vorbis | `0x00040001` | 0 | 3 | registered (`0x9CC3EC`) | `0x103E138` | `0xAB1550` | `0xAB22D4` |
| PCM | `0x00010001` | — | 1 / 3 | `0xA76140` / `0xA72D04` | `0x103D950` / `0x103D740` | `0xA75E34` / `0xA72BA0` | `0xA7538C` / `0xA72D7C` |
| mode 2 | any | — | 2 | `0xA78D10` | `0x103DA28` | `0xA78510` | `0xA78964` |

- **No bank contains `0x00010001`**, so the PCM classes are not shipped.
- **Mono vs stereo does not select a class.** The factory sees only `(mode, plugin)`; the channel count enters later through the format (`[src+0x38]`, set by `0xA7329C` from `[PBI+0x1B8]`, and `pbi+0x158/+0x15C`). The same class handles both, and the stereo→mono 0.70710677 is applied by the mixer (`M6-012`).
- For the robot-audio path the ADPCM items are all stream 1 → `0xA74244`; the Vorbis items are 1826 stream 1 + 18 stream 2 → streamed `0xAB0448`, and 27 stream 0 → in-memory `0xAB1550`.

### 1e. The six ctors and their exact stored vtables

| ctor | alloc | base ctor it calls | stored vtable | citation |
|---|---|---|---|---|
| `0xA78D10` | 0x70 | `0xA5627C` | `0x103DA28` | 0xA78D18 `bl 0xa5627c`; decomp `*param_1 = &PTR_LAB_0103da28` |
| `0xA76140` | 0x6C | `0xA74504` | `0x103D950` | 0xA76148 `bl 0xa74504`; 0xA76164 `add r3,r3,#8`; 0xA7616C `str r3,[r4]` |
| `0xA72D04` | 0x40 | `0xA7329C` | `0x103D740` | 0xA72D0C `bl 0xa7329c`; 0xA72D1C `add r3,r3,#8`; 0xA72D20 `str r3,[r4]` |
| `0xA74244` | 0x70 | `0xA74504` | `0x103D840` | 0xA7424C `bl 0xa74504`; 0xA74268 `add r3,r3,#8`; 0xA74270 `str r3,[r4]` |
| `0xA72A2C` | 0x48 | `0xA7329C` | `0x103D6C0` | 0xA72A34 `bl 0xa7329c`; 0xA72A50 `add r3,r3,#8`; 0xA72A54 `str r3,[r4]` |
| `0xA5627C` | — | — | `0x103C844` (base) | decomp; base ctor body below |

**Classification: EXACT_SOURCE** for the dispatch, the caller chain, the mode/plugin origin and the bank ids. The source-plugin (`plugin & 0xF == 2/5`) mode-2 selection path is **UNKNOWN**: the Sound reader leaves the mode bits 0 for those, and no other writer of bits 2..6 = 2 was found; the `0xA78D10` class is therefore reached only if some path sets mode 2 (the WavePortal's own feeder `0x9B4294` calls the factory with mode 3, plugin `0x00040001` — a Vorbis decoder, not the mode-2 class).

---

## Q2 — the `vt+0x4C` bodies

`0xA54F1C` reads `source = [voice+0xD4]` (0xA54F20 `ldr r5,[r0,#0xd4]`), then `r3 = [source]` and `r3 = [r3,#0x4c]` (0xA54FF0 `ldr r3,[r5]`; 0xA55008 `ldr r3,[r3,#0x4c]`; 0xA5500C `blx r3`).

The `vt+0x4C` slot for each class:

| class | `vt+0x4C` | body |
|---|---|---|
| PCM t1 (`0x103D950`) | `0xA72B14` | returns `([source+0xC]+0x1BE) >> 6 & 1` |
| PCM t3 (`0x103D740`) | `0xA72B14` | same |
| ADPCM t1 (`0x103D840`) | `0xA72B14` | same |
| ADPCM t3 (`0x103D6C0`) | `0xA72B14` | same |
| Vorbis streamed (`0x103E0B8`) | `0xA72B14` | same |
| Vorbis in-memory (`0x103E138`) | `0xA72B14` | same |
| mode2 (`0x103DA28`) | `0xA566C8` | same |
| base (`0x103C844`) | `0xA566C0` | `mov r0,#0; bx lr` |

The three bodies the task names:

**`0xA566B8`** (this is `vt+0x44`, not `vt+0x4C`):
```
0xA566B8  mov r0, #0
0xA566BC  bx  lr
```
Returns 0.

**`0xA566C0`** (base `vt+0x4C`):
```
0xA566C0  mov r0, #0
0xA566C4  bx  lr
```
Returns 0.

**`0xA566C8`** (mode-2 `vt+0x4C`):
```
0xA566C8  ldr  r3, [r0, #0xc]      ; r3 = [source+0xC] (the PBI, not the bus — see Q4)
0xA566CC  ldrb r0, [r3, #0x1be]    ; byte at [PBI+0x1BE]
0xA566D0  ubfx r0, r0, #6, #1      ; bit 6
0xA566D4  bx   lr
```
Returns bit 6 of the byte at `[source+0xC]+0x1BE`.

**`0xA72B14`** (the codec classes' `vt+0x4C`) is the same computation:
```
0xA72B14  ldr  r3, [r0, #0xc]
0xA72B18  ldrb r0, [r3, #0x1be]
0xA72B1C  ubfx r0, r0, #6, #1
0xA72B20  bx   lr
```

**Classification: EXACT_SOURCE** for all four bodies. Note `0xA72B14` is byte-identical in effect to `0xA566C8`; the task's "three relevant classes" (`0xA566B8`/`0xA566C0`/`0xA566C8`) do not include the one the shipped codec sources actually use.

---

## Q3 — the `vt+0x30` render bodies (the slot `0xA44630` calls)

The call site (0xA44630 voice render dispatcher):
```
0xA4478C  ldr  r0, [r7, #0xd4]     ; r0 = source = [voice+0xD4]
0xA44790  mov  r1, r5              ; r1 = params
0xA44794  ldr  r2, [r4]            ; r2 = [0x1052440] = frame size 0x400
0xA44798  ldr  r3, [r0]            ; r3 = source vtable
0xA4479C  strh r2, [r5, #0xc]      ; params+0xC = 0x400
0xA447A0  ldr  r3, [r3, #0x30]     ; vt+0x30
0xA447A4  blx  r3
```
Correction: `0xA4475C ldr r4,[pc,#0x1dc]` (lit `0x5FB8F0`) and `0xA44760 ldr r4,[pc,r4]` load GOT slot `0x1040058` = `0x1052440`; `0xA44794 ldr r2,[r4]` reads `0x400`. `0x108DF98` is not read here.

The `vt+0x30` slot (stored vtable + 0x30) for each class:

| class | stored vtable | `vt+0x30` render | `vt+0x28` StartStream |
|---|---|---|---|
| PCM t1 | `0x103D950` | `0xA75E34` | `0xA7538C` |
| PCM t3 | `0x103D740` | `0xA72BA0` | `0xA72D7C` |
| ADPCM t1 | `0x103D840` | `0xA73D34` | `0xA7538C` |
| ADPCM t3 | `0x103D6C0` | `0xA72554` | `0xA72674` |
| mode2 | `0x103DA28` | `0xA78510` | `0xA78964` |
| Vorbis streamed | `0x103E0B8` | `0xAB0448` | `0xAB0B20` |
| Vorbis in-memory | `0x103E138` | `0xAB1550` | `0xAB22D4` |

### `0xA73D34` — IMA ADPCM streamed (the shipped ADPCM render)

Called with `r0 = source`, `r1 = params`. Reads `[src+0x10] bit1` (0xA73D34 `ldrb r3,[r0,#0x10]`; 0xA73D3C `tst r3,#2`).

If bit1 set (a "reset/seek" path):
- `r6=[src+0x3C]` (stream object), `r7=[src+0x44]` (frames remaining); calls `[r6]vt+0x28` (0xA73D5C-0xA73D68) to get a count; on `0x2D/0x2E` calls `[r6]vt+0x2C` and accumulates `r7`; a `0x11`/short result goes to the tail; otherwise falls through.
- if `[src+0xC]+4 bit 0x400000` (the PBI's `+4` flags) is set → tail; else falls into the normal path.

Normal path (bit1 clear):
- `[src+0xC]+4 bit 0x400000` set → 0xA73FD0 sets `r5=0x2D` (DataReady).
- `[src+0x44] == 0` → `0xA74970(src)` (refill); if it returns `0x2D` continue, else store `[params+0x28]` and return.
- `fp=[src+0xC]` (the PBI); `r3=fp+0x160`, `r8 = (u16[fp+0x160]>>6)&0x3FF` (0xA73DD8 `ubfx r8,r8,#6,#0xa`) = the block-aligned frame count.
- `r5=[fp+0x15C]` = channels; `r0=[0xA7A894]`-style alloc of `channels * blockFrames * 2` (0xA73DFC `mul r1,ip,r1`; 0xA73E04 `bl 0xa7a894`); store at `[src+0x64]`.
- `blockFrames = r8 << 6` (0xA73E24 `lsl r2,r1,#6`).
- If `[src+0x6C] != 0` (a partial previous block), copy it (0xA740B4-0xA740CC `bl 0x4d37f0`) and adjust offsets.
- Per channel c: `r0 = [src+0x40] + c*0x24`, `r2 = r6` blocks, `bl 0xA7A194` (0xA73E98-0xA73EA0) — the ADPCM block decoder `0xA7A194`, `[sp]=channels` as the output stride. `r8 += 2` per channel.
- Then computes the byte offsets: `r0 = (blockFrames*r6 + src+0x64) - src+0x64` (0xA73ECC `mla r0,r3,r6,sl`), `r8 = r6*[src+0x60]` (0xA73EE4 `mul r8,r6,r3`), advances `[src+0x40]/[src+0x44]/[src+0x48]` by the consumed bytes (0xA73EFC-0xA73F04), `r6 = u16(r0)`.
- If `[src+0x44] < [src+0x60]` (remaining < block size) and `[src+0x68]==0`, allocates a carry buffer `[src+0x68]` (0xA74190 `bl 0xa7a7f4`) and stores the partial block (0xA7419C-0xA741B8). Otherwise `[src+0x6C]=r2` (partial count) and memcpy (0xA73F1C-0xA73F24).
- Tail: `0xA73490(src, decoded, r6, [fp+0x158], [fp+0x160+4], params)` (0xA73F64-0xA73F84). On success `[src+0x5E]` bit1 is cleared/set.
- The `0x2B` (`0xA73D5C`) path re-reads the stream and loops; `0x11`/NoMoreData sets `[params+0x28]`.

`r5` (result) is written to `[params+0x28]` at 0xA7408C (`str r5,[sb,#0x28]`), where `sb=r1=params`.

**Classification: EXACT_SOURCE** for the control flow, field offsets and call order; the decoder arithmetic is M6-003 (`0xA7A194`).

### `0xA72554` — IMA ADPCM in-memory (mode 3)

Called with `r0=source`, `r1=params`. `fp=[src+0xC]+0x15C` (channels); allocates `channels * frameSize * 2` at `[src+0x44]` (0xA72584-0xA72598); on null sets `[params+0x28]=2` (0xA725A4). Reads `[src+0x38]` (u16), `[params+0xC]` (frames), `[src+0x18]` (start), `[src+0x14]`/`[src+0x28]` (total), computes `r7 = min(...)` in 64-frame units (`0xA725E0 lsr r7,r7,#6`, `0xA725E4 lsl sb,r7,#6`). Per channel: `r0 = [src+0x3c] + c*0x24`, `r2=r7`, `bl 0xA7A194` (0xA725F8-0xA72618). Then `r7 = [src+0x3c] + blockFrames*frameUnits` (0xA72644 `mla r7,r4,r7,lr`), stores `[src+0x3c]=r7`, and calls `0xA73490(src, buf, sb, [fp+0x158], [sp+0xc]+4, params)` (0xA72654-0xA72658).

### `0xAB0448` — Vorbis streamed (the shipped Vorbis render)

```
0xAB0448  ldrh r3, [r0, #0x38]     ; r3 = u16 [src+0x38] (config)
0xAB0450  cmp  r3, #1
0xAB045C  ldrne r3, [r0, #0xa0]    ; non-mono: r3 = [src+0xA0]
0xAB0464  ldrne r2, [r0, #0x98]
0xAB0468  add  r0, r0, #0x3c
0xAB046C  ldreq r1, [r0, #-0x20]   ; mono: r1 = [src+0x1C]
0xAB0470  ldrne r1, [r0, #0x90]    ; non-mono: r1 = [src+0xCC]? (r0=src+0x3c)
0xAB0474  addne r3, r3, r2
0xAB0478  ldreq r3, [r0, #0x90]    ; r3 = [src+0xCC]
0xAB047C  ldr  r2, [r0, #0x8c]     ; r2 = [src+0xC8]
0xAB0480  add  r3, r1, r3
0xAB0484  ldrh r1, [r4, #0xa8]
0xAB0488  rsb  r3, r2, r3
0xAB048C  str  r3, [r0, #0x50]     ; [src+0x8C] = ...
0xAB0490  mov  r3, #1
0xAB0494  strb r3, [r4, #0x90]     ; [src+0x90] = 1
0xAB0498  add  r3, r4, #0x80
0xAB049C  bl   0xab7e40            ; framing/parse -> produces PCM
0xAB04A0  ldr  r3, [r4, #0x40]
0xAB04A4  cmp  r3, #2
0xAB04A8  str  r3, [r5, #0x28]     ; params+0x28 = result
0xAB04AC  beq  0xab04e8            ; result 2 -> return
0xAB04B0  ldr  lr, [r4, #0x3c]
0xAB04B4  mov  r0, r4
0xAB04B8  ldr  r6, [r4, #0x84]
0xAB04BC  ldr  ip, [r4, #0xc8]
0xAB04C0  ldr  r7, [r4, #0x48]
0xAB04C4  uxth r2, lr
0xAB04C8  ldr  r1, [r4, #0x80]
0xAB04CC  ldr  r3, [r4, #0xbc]
0xAB04D0  add  ip, ip, r7
0xAB04D4  str  r5, [sp, #4]        ; params
0xAB04D8  str  lr, [r4, #0x88]
0xAB04DC  str  ip, [r4, #0xc8]
0xAB04E0  str  r6, [sp]            ; rate
0xAB04E4  bl   0xa73490            ; emit
0xAB04E8  pop
```
So the Vorbis streamed render is a thin wrapper: set up the decoder state (`src+0x80..+0xA8`, `+0xC8`), call the framing/parse `0xAB7E40` (M6-002 gap 2), then `0xA73490(src, [src+0x80], frames=[src+0x3C], rate=[src+0xBC], [src+0x84], params)`. The helpers at `0xAB04F0`, `0xAB05E4` are the `vt+0x08`/`vt+0x0C` slots (setup/seek).

### `0xAB1550` — Vorbis in-memory (mode 3)

Large (~0x4B0 bytes). It reads `[src+0x10] bit1` (seek), keeps an internal read buffer at `[src+0xEC]`/`[src+0xF0]`/`[src+0xF4]`/`[src+0xF8]` and copies from the stream, then calls the framing `0xAB7E40` at 0xAB18A4 (`add r0,r4,#0x60`, `r3=r4+0xa4`, `r1=u16[src+0xCC]`) and emits through `0xA73490` at 0xAB1964 (`r0=src`, `r1=[src+0xA4]`, `r2=u16[src+0x60]`, `r3=[src+0xE0]`, `[sp]=[src+0xA8]`, `[sp+4]=params`). The result is `[src+0x64]` → `[params+0x28]` (0xAB18A8-0xAB18B8). Same two leaves as the streamed class.

### `0xA78510` — mode-2 (plugin/external source) render

```
0xA78510  push {r3,r4,r5,r6,r7,lr}
0xA78514  ldr  r3, [r0, #0x6c]     ; r3 = [src+0x6C] (stream object)
0xA78518  cmp  r3, #0
0xA7851C  beq  0xa785a4            ; no stream -> fill params with a 0x2B placeholder
0xA78520  ldrh ip, [r1, #0xc]      ; params+0xC = frames
0xA78524  cmp  ip, #0
0xA78528  moveq r3, #0x11          ; no frames -> result 0x11
0xA7852C  streq r3, [r1, #0x28]
0xA78530  popeq
0xA78534  ldr  r2, [r0, #0x1c]     ; r2 = [src+0x1C]
0xA78538  mov  r4, r1
0xA7853C  mov  r5, r0
0xA78540  ldr  r6, [r0, #0x48]
0xA78544  cmp  r2, #0
0xA78548  beq  0xa785e8
0xA7854C  str  r2, [r1]            ; params+0 = [src+0x1C]
0xA78554  str  r6, [r1, #4]        ; params+4 = [src+0x48]
0xA78558  strh r2, [r1, #0xe]      ; params+0xE = 0
0xA7855C  ldr  r2, [r3]
0xA78560  mov  r0, r3
0xA78564  mov  r1, r4
0xA78568  mov  r3, #0x2b
0xA7856C  str  r3, [r4, #8]        ; params+8 = 0x2B
0xA78570  add  r5, r5, #0x1c
0xA78574  ldr  r3, [r2, #0x34]     ; stream vt+0x34
0xA78578  blx  r3
0xA7857C  ldr  r3, [r4, #8]
0xA78584  str  r3, [r4, #0x28]     ; params+0x28 = result
0xA78588  ldm  ip!, {r0,r1,r2,r3}  ; copy 10 words of params into [src+0x1C..]
...
0xA785A4  ; no stream: zero/placeholder params, result 0x2B, +0x28 = 2
0xA785E8  ; [src+0x4E] bit2 set -> 0xA69A70(src, frames, [src+0x48]); else alloc buffer
```
The mode-2 class is the external/streamed source wrapper (an object at `[src+0x6C]` with `vt+0x34`), and it copies the resulting buffer into the source's own 0x28-byte params area at `src+0x1C`. `0xA78510` is the one render slot the task listed correctly.

### `0xA75E34` — PCM t1 render; `0xA72BA0` — PCM t3 render

- `0xA72BA0` is the short PCM case: `r3=[src+0x38]`, `r2=u16[params+0xC]`, `r0=[src+0xC]`; it picks `[src+0x28]`/`[src+0x14]` by `[src+0x38]==1`, computes `frames = min(r2+[src+0x18], ...)`, and tail-calls `0xA73490(src, [src+0x3C], frames, [PBI+0x158], [[PBI+0x160]+4], params)` (0xA72BA0-0xA72C08). No decoder.
- `0xA75E34` is the long PCM t1 case (seek/format handling, 0xA75E34-0xA76134), same `0xA73490` leaf.

**Classification: EXACT_SOURCE** for the render control flow, the slot table and the field offsets. The decoder leaves (`0xA7A194`, `0xAB7E40`, `0xAB6B14`) are already recorded under M6-003 and M6-002 and are not re-derived here. The `0xA78D10`/mode-2 render is EXACT_SOURCE for the wrapper but its stream object (`[src+0x6C]`'s class) is UNKNOWN (no RTTI).

---

## Q4 — source-class field layout

From the base ctors and the render bodies. `[source+0xC]` is the factory's third argument, which AddSrc passes as the PBI (`0xA558AC` call at 0xA558D8 passes `param_2`; AddSrc's `param_2` is the PBI at `0xA4304C ldr r4,[r0]`). The inventory's C1 label "bus = [source+0xC]" is therefore wrong: it is the **playing instance (PBI)**, and the bytes read at `[source+0xC]+0x1B8/+0x1BE` are PBI fields.

### Common base (`0xA5627C`, used by `0xA78D10` and both `0xA7329C`/`0xA74504`)

| offset | type | meaning | citation |
|---|---|---|---|
| +0x00 | ptr | vtable | 0xA5629C `str r2,[r0]` |
| +0x04 | u32 | 0 | 0xA562A4 `str lr,[r0,#4]` |
| +0x08 | u32 | 0 (later set to `[src+0xC]+0xC` by AddSrc) | 0xA562AC `str lr,[r0,#8]`; 0xA558AC `str iVar5,[param_1+8]` |
| +0x0C | ptr | PBI (ctor arg) | 0xA56284 `str r1,[r0,#0xc]` |
| +0x10 | u8 | flags: bit0 = StartStream succeeded latch (0xA56650/0xA56664), bit1 = a second state | 0xA562A8 `strb r1,[r0,#0x10]`; M6-002 gapG 4.1 |

### `0xA7329C` (PCM t3 / ADPCM t3 base) and `0xA74504` (PCM t1 / ADPCM t1 base)

| offset | type | meaning | citation |
|---|---|---|---|
| +0x14 | u32 | 0 (total frames) | 0xA732B4 `str r5,[r4,#0x14]` |
| +0x18 | u32 | 0 (frames delivered) | 0xA732B8 `str r5,[r4,#0x18]` |
| +0x1C..+0x28 | u32×4 | 0 | 0xA732C4/CC/D0/D4 |
| +0x2C | obj | init `0x9D4ACC` (a lock/queue; `0x9D4C24` is called on it from `0xA73490`) | 0xA732B0 `add r0,r4,#0x2c`; 0xA732D8 `bl 0x9d4acc`; 0xA734D8 `bl 0x9d4c24` |
| +0x34 | u32 | 0 | 0xA732E0 `str r5,[r4,#0x34]` |
| +0x38 | u16 | channel config: `[[src+0xC]+0x1B8]` or 1 | 0xA732DC `ldr r3,[r4,#0xc]`; 0xA732F4 `ldrhne r3,[r3]`; 0xA732F8 `strh r3,[r4,#0x38]` |
| +0x3C..+0x58 | u32×8 | 0 (PCM t1/ADPCM t1 only) | 0xA74524/3C/40/44/48/4C/50/54 |
| +0x5C | u16 | 0 | 0xA74558 `strh r3,[r4,#0x5c]` |
| +0x5E | u8 | flags, low nibble cleared by `0xA74504`; bit1 = "last block complete"; bit2 = "have stream object" | 0xA74520 `and r1,r1,#0xf0`; 0xA73F2C/0xA73F5C |

### ADPCM t1 (`0xA74244`) additional

| offset | meaning | citation |
|---|---|---|
| +0x60 | current block size in bytes (0xA73E34 `ldr r3,[r4,#0x60]`) | 0xA7425C `str r2,[r4,#0x64]` sets +0x64; ctor sets +0x64/+0x68/+0x6C = 0 |
| +0x64 | decode buffer pointer | 0xA7425C; 0xA73E10 `str r0,[r4,#0x64]` |
| +0x68 | carry/partial-block buffer | 0xA73F10 `ldr r0,[r4,#0x68]`; 0xA741A8 `str r0,[r4,#0x68]` |
| +0x6C | partial-block frame count (u16) | 0xA73E18 `ldrh r3,[r4,#0x6c]` |
| +0x3C | stream object | 0xA73D50 `ldr r6,[r0,#0x3c]` |
| +0x40 | current input pointer | 0xA73F04 `str r1,[r4,#0x40]` |
| +0x44 | remaining frames | 0xA73F00 `str r2,[r4,#0x44]` |
| +0x48 | output pointer | 0xA73EFC `str r6,[r4,#0x48]` |

### ADPCM t3 (`0xA72A2C`) additional

| offset | meaning | citation |
|---|---|---|
| +0x3C | input/output buffer | 0xA72A44 `str r2,[r4,#0x3c]`; 0xA725F8 `ldr r2,[r6,#0x3c]`; 0xA72654 `str r7,[r6,#0x3c]` |
| +0x40 | frame count (u16) | 0xA72600 `ldrh r3,[r6,#0x40]` |
| +0x44 | alloc buffer | 0xA72A4C `str r2,[r4,#0x44]`; 0xA725A0 `str r0,[r6,#0x44]` |

### Vorbis classes (`0x103E0B8`, `0x103E138`)

| offset | meaning | citation |
|---|---|---|
| +0x38 | u16 config (1 = mono) | 0xAB0448 `ldrh r3,[r0,#0x38]`; 0xAB07EC `ldrh r3,[r4,#0x38]` |
| +0x3C | frame count / output | 0xAB04B0 `ldr lr,[r4,#0x3c]`; 0xAB04D8 `str lr,[r4,#0x88]` |
| +0x40 | result code (0x2D/0x2E/2) | 0xAB04A0 `ldr r3,[r4,#0x40]` |
| +0x48 | frames this block | 0xAB04C0 `ldr r7,[r4,#0x48]` |
| +0x80 | decode output pointer | 0xAB04C8 `ldr r1,[r4,#0x80]` |
| +0x84 | sample rate/format word | 0xAB04B8 `ldr r6,[r4,#0x84]` |
| +0x88 | last frame count | 0xAB04D8 |
| +0x8C | offset | 0xAB048C `str r3,[r0,#0x50]` (r0=src+0x3C) |
| +0x90 | ready flag | 0xAB0494 `strb r3,[r4,#0x90]` |
| +0x98/+0xA0/+0xA4/+0xA8 | decode state / buffers | 0xAB045C/64; 0xAB19B8 `ldr r1,[r4,#0xec]` etc. |
| +0xB0/+0xB4 | input read position / flag | 0xAB1864 `str r3,[r4,#0xb0]`; 0xAB1880 `strb r6,[r4,#0xb4]` |
| +0xBC | rate | 0xAB04CC `ldr r3,[r4,#0xbc]` |
| +0xC8 | stream offset accumulator | 0xAB047C `ldr r2,[r0,#0x8c]` (r0=src+0x3C); 0xAB04DC `str ip,[r4,#0xc8]` |
| +0xCC | internal buffer size/offset | 0xAB0478 `ldreq r3,[r0,#0x90]` |
| +0xEC/+0xF0/+0xF4/+0xF8 | in-memory read buffer / count / phase / flag | 0xAB1690-0xAB16E8 |

### The `params` (voice buffer) struct the renders write

From `0xA73490` (the shared emit) and the dispatcher:

| offset | type | meaning | citation |
|---|---|---|---|
| +0x00 | ptr | data pointer | 0xA734B4 `str r1,[r4]` |
| +0x04 | u32 | rate/format (3rd arg) | 0xA734BC `str r3,[r4,#4]` |
| +0x08 | u32 | status (0x2B etc.) | 0xA7856C `str r3,[r4,#8]` |
| +0x0C | u16 | valid frames (set to 0x400 before the call) | 0xA4479C `strh r2,[r5,#0xc]`; 0xA734B8 `strh r6,[r4,#0xc]` |
| +0x0E | u16 | max frames | 0xA734C0 `strh r6,[r4,#0xe]` |
| +0x18 | u32 | start sample | 0xA734F4 `str r3,[r4,#0x18]` |
| +0x20 | u32 | total/end sample | 0xA734F8 `str r2,[r4,#0x20]` |
| +0x24 | u32 | pitch/step | 0xA734F0 `str r7,[r4,#0x24]` |
| +0x28 | u32 | result code (0x2D DataReady, 0x2E NoMoreData, 2, 0x11) | 0xA73510/0xA7351C/0xA7408C |

**Classification: EXACT_SOURCE** for the offsets above (each is a read/write instruction). The `0x9D4ACC`/`0x9D4C24` object at `src+0x2C` and the exact `params+4` semantics are RECOVERABLE_GAP (read `0x9D4ACC`/`0x9D4C24`); they do not change the shipped signal path read here.

---

## Existing records contradicted or too weak (relative to this scope)

- **M6-022 V7/C1:** "bus = [source+0xC]" is contradicted. `[source+0xC]` is the PBI (the factory's 3rd arg = AddSrc's `param_2`), which is why `0xA54F1C` reads `[+0x1F8]`, `[+0x54]`, `[+0x11C]`, `[+0x140]`, `[+0x164]`, `[+0x68]`, `[+0x58]` from it (all PBI fields). The bus is reached elsewhere (the aux-send walk uses the bus node, not `[source+0xC]`). Citation: 0xA56284 `str r1,[r0,#0xc]`; 0xA558D8 `bl 0xa562b8` with `param_2` = the PBI.
- **Task premise / any record that lists the six factory vtables as the shipped source classes:** the six do not include Vorbis. `plugin>>16 == 4` reaches `0x9CC3EC`. The Vorbis source classes' vtables are `0x103E0B8`/`0x103E138` (M6-002 gap-2 territory), and their render slots `0xAB0448`/`0xAB1550` are not in M6-022's callee list. Citation: 0xA562F0 `cmp r1,#2`; 0xA562F4 `bne 0xa5633c`; 0x9CC40C/0x9CC448/0x9CC45C.
- **Task premise: `strh [params+0xC] = 0x108DF98`.** Contradicted; it is `0x400` from `0x1052440`. Citation: 0xA44760 `ldr r4,[pc,r4]` → `[0x1040058]=0x1052440`; 0xA44794 `ldr r2,[r4]`; 0xA4479C `strh r2,[r5,#0xc]`.
- **Task table's `vt+0x30`/`vt+0x4c` columns for `0xA74244`/`0xA72A2C`/`0xA72D04`:** too weak / off by 8 (see headline 3). The actual stored vtables and slots are in Q1e/Q3. The list's `0xA7538C`/`0xA72674` are StartStream (`vt+0x28`); `0xA73128` is `vt+0x2C`. Citations: 0xA74268 `add r3,r3,#8`; the vtable dumps.
- **M6-022 V8's callee list does not name `0xA73D34`/`0xA72554`/`0xAB0448`/`0xAB1550`.** Those are the actual `vt+0x30` bodies of the shipped source classes; `0xA7538C` and `0xA72674`, which the task names, are the `vt+0x28` StartStream bodies already covered by gapG 4.4.

## Open questions for the manager

1. **The mode-2 (`0xA78D10`) selection path is UNKNOWN.** The Sound reader leaves the mode bits 0 for source plugins (`plugin & 0xF == 2/5`), and the only literal-mode factory calls are AddSrc (mode from the PBI) and the WavePortal feeder `0x9B4294` (mode 3, Vorbis). No writer of bits 2..6 == 2 was found. Is the mode-2 class used by any shipped robot-audio event? If not, it can stay out of the robot-audio record. If the WavePortal path matters, read `0x992C48` (the other `0x9B9C90` caller) and the WavePortal registration create.
2. **Vorbis source classes belong to M6-002's driver, not M6-022's six.** The manager should decide whether M6-022's `unresolved` should name the registered-plugin path (`0x9CC3EC` → `0x103E0B8`/`0x103E138`, renders `0xAB0448`/`0xAB1550`) as its own step, since the shipped robot voice is Vorbis-first (1826 stream 1 + 27 stream 0 in Cozmo.bnk) and the six built-in classes cover only the 333 ADPCM.
3. **`[source+0xC]` = PBI, not bus.** C1/V7's label needs correcting wherever it is cited (M6-022 V7, and any M6-011/`0xA4BC58` note that says bus).
4. **`params+4` and `src+0x2C` (`0x9D4ACC`)** are the two field semantics left as RECOVERABLE_GAP; neither changes the shipped sample values as far as the bodies read.
