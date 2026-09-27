# M6-002 Vorbis decoder arithmetic — extractor inventory

Read-only pass. Scope: the ten arithmetic steps the M6-002 build left as `MISSING`, plus the BlockSizes shift edge.
Engine: `resources/lib/armeabi-v7a/libcozmoEngine.so` (Wwise region is ARM mode). All addresses are VAs.
Tools/dumps: `.scratch/m6-vorbis/` (`t.py`, `floor1.txt`, `mapping.txt`, `makedecode.txt`, `decodemap.txt`,
`residue.txt`, `decodev.txt`, `floor1inv.txt`, `floortable2.txt`, `imdct.txt`, `imdct2.txt`, `overlap.txt`,
`blockin.txt`, `floors_out.txt`). Nothing outside `.scratch/` was written.

## How the questions were read
- The setup walk is `0x00AB63E0` (V3): codebooks (10-bit ids, 0x3C bytes each), floors (0x24 each),
  residues (0x1C each), mappings (0x14 each), modes. `ci` field offsets established by that walk:
  +0x08 modes, +0x0C mappings, +0x10 floors, +0x14 residues, +0x18 codebooks, +0x1C mode array,
  +0x20 mapping array, +0x24 floor array, +0x28 residue array, +0x2C codebook array.
- The decode path is the mapping inverse `0x00AB6B14` (called through the registry, no direct `bl`):
  floor inverse1 `0x00AB8E60` -> residue inverse `0x00AB73F8` -> coupling `0x00AB6E30..0xAB6E68` ->
  floor1 inverse2 `0x00AB915C` -> IMDCT `0x00AB4E34`. The block output is `0x00AB3780` (`vorbis_synthesis_blockin`
  analogue) -> windowed combine `0x00AB5A94` (called at `0x00AB3664`) -> per-channel planar output `0x00AB3520`.

## Row table

| step | what the original does | citation | existing record id or NEW | classification |
|---|---|---|---|---|
| 1a | **Floor1 setup, partitions.** `partitions = read(5)`, stored at floor+0x18; allocate `partitions` bytes for `partitionclass[]` at floor+4; `for j: partitionclass[j] = read(4)` and track `maxclass`. | 0x00AB88EC `mov r1,#5; bl 0xab62e0`; 0x00AB88FC `str r0,[r4,#0x18]`; 0x00AB8954 `mov r1,#4; bl 0xab62e0`; 0x00AB8960 `strb r0,[r8,r6]` | M6-002 / gapG (V3 row cites only "floor type not read") | EXACT_SOURCE (NEW detail) |
| 1b | **Floor1 setup, classes.** `maxclass+1` classes, **11 bytes each**, at floor+0: `dim@+0 = read(3)+1`, `subs@+1 = read(2)`, `book@+2 = (subs? read(8) : 0)`, `subbook[k]@+3.. = read(8)-1` for `k < 1<<subs`. Valid: `book < ci->books` (ci+0x18), `subbook[k] < books` or `== 0xFF` (i.e. read(8)==0). Class size arithmetic `5*n` then `n + 10*n` = 11*n. | 0x00AB8990 `add r0,r3,r3,lsl#2` / 0x00AB8998 `add r3,r3,r0,lsl#1`; 0x00AB89E8..0xAB8A08 (dim, subs); 0x00AB8C74 `mov r1,#8; bl 0xab62e0` / 0x00AB8C88 `strb r0,[r3,#2]`; 0x00AB8A78 `bl 0xab62e0` / 0x00AB8A88 `strb r0,[fp,sl,#3]`; 0x00AB8A44 `ldr r2,[r7,#0x18]` / 0x00AB8A54 `cmp r3,r2`; 0x00AB8AAC..0xAB8AB4 check 0xFF | M6-002 / NEW | EXACT_SOURCE |
| 1c | **Floor1 setup, multiplier / rangebits / x-list.** `mult = read(2)+1` at floor+0x20; `rangebits = read(4)`; `count = sum(class_dim[partitionclass[j]])`; allocate postlist as `u16[count+2]` at floor+8 (and a `count+2`-byte side array at floor+0x0C, plus two `count`-byte arrays at floor+0x10 and floor+0x14); `posts = count+2` at floor+0x1C; `for k: postlist[k+2] = read(rangebits)`, valid if `< 1<<rangebits`; finally `postlist[0]=0`, `postlist[1]=1<<rangebits`. | 0x00AB8AE8 `mov r1,#2; bl 0xab62e0`; 0x00AB8AF0 `add r3,r0,#1`; 0x00AB8AF8 `str r3,[r4,#0x20]`; 0x00AB8AFC `mov r1,#4; bl 0xab62e0`; 0x00AB8B20..0xAB8B48 (count); 0x00AB8C34 `add r6,fp,#2; lsl r6,r6,#1`; 0x00AB8C48 `bl 0xab62e0`; 0x00AB8C60 `strh r0,[sl,r6]`; 0x00AB8C64 `cmp r2,r8` (r8 = 1<<rangebits); 0x00AB8D18 `strh r8,[r3,#2]`; 0x00AB8D1C `strh r2,[r3]`; 0x00AB8D24 `str r7,[r4,#0x1c]` | M6-002 / gapG 6.6 | EXACT_SOURCE |
| 1d | **Floor1 setup, look arrays.** After postlist: zero the `count+2`-byte array at floor+0x0C and call `0x00AB8018(floor+0x0C, postlist, posts)` (sort/init helper, not read); then build `low_neighbor@floor+0x14[k]` and `high_neighbor@floor+0x10[k]` for each post index k=0..count-1 by scanning the earlier posts. | 0x00AB8D28..0x00AB8D40 (zero loop); 0x00AB8D44 `uxth r2,r2; ldr r0,[r4,#0xc]; ldr r1,[r4,#8]; bl 0xab8018`; 0x00AB8D68..0x00AB8E14; 0x00AB8DF8 `strb lr,[r3,r8]` (low); 0x00AB8E00 `strb ip,[r3,r8]` (high) | M6-002 / NEW | partial EXACT_SOURCE for the fields; `0x00AB8018` internals RECOVERABLE_GAP (read 0x00AB8018..0x00AB8400) |
| 1e | **Floor entry layout (0x24 bytes).** +0x00 class base, +0x04 partitionclass, +0x08 postlist, +0x0C sorted-index array, +0x10 high_neighbor, +0x14 low_neighbor, +0x18 partitions, +0x1C posts(count+2), +0x20 multiplier. `rangebits` is not stored (postlist[1] carries `1<<rangebits`). **No floor-type field is read** (the Wwise-stripped header assumes floor 1). | store sites above; struct size from 0x00AB64D4 `add r2,r6,r6,lsl#3; lsls r2,r2,#2` = 0x24*count | M6-002 / NEW | EXACT_SOURCE |
| 2a | **Mapping setup, submaps/coupling.** `flag = read(1)`; if 0 -> submaps=1 else `read(4)+1`; `flag2 = read(1)`; if set -> `coupling_steps = read(8)+1`, allocate 2 bytes/step at map+0x10, `for j: mag = read(ilog(channels-1))`, `ang = read(ilog(channels-1))`, validate `mag!=ang`, `mag<channels`, `ang<channels`; then `read(2)` must be 0 (reserved). | 0x00AB67AC..0x00AB67C4 (submaps bit); 0x00AB6AB4 `mov r1,#4; bl 0xab62e0` / 0x00AB6AC0 `add r0,r0,#1`; 0x00AB67C8..0x00AB67D8 (coupling bit); 0x00AB67E0 `mov r1,#8; bl 0xab62e0`; 0x00AB6868 `lsrs r4,r4,#1` (ilog); 0x00AB687C / 0x00AB68B0 (mag/ang reads); 0x00AB68C0..0xAB68DC validation; 0x00AB6934 `mov r1,#2; bl 0xab62e0` | M6-002 / V3 | EXACT_SOURCE |
| 2b | **Mapping setup, mux.** If `submaps>1`, allocate `channels` bytes at map+4 and `for j: mux[j] = read(4)` (valid `< submaps`). | 0x00AB6940..0x00AB6974; 0x00AB69B4 `mov r1,#4; bl 0xab62e0`; 0x00AB69C4 `cmp r2,r3` | M6-002 / V3 | EXACT_SOURCE |
| 2c | **Mapping setup, per-submap floor/residue — three 8-bit reads.** Allocate `2*submaps` bytes at map+8. Per submap the decoder consumes **three** `read(8)` values: the first is the spec's unused **time submap** (discarded), the second is `floorsubmap[i]` (stored at +0, checked vs `ci->floors`=ci+0x10), the third is `residuesubmap[i]` (stored at +1, checked vs `ci->residues`=ci+0x14). Field order is `{floor, residue}` interleaved. | 0x00AB69E0..0x00AB6A30 (alloc); 0x00AB6A70 `bl 0xab62e0` (time, result dropped); 0x00AB6A80 `bl 0xab62e0`; 0x00AB6A8C `strb r0,[r7,r4,lsl#1]`; 0x00AB6A98 `ldr r2,[sb,#0x10]`; 0x00AB6AA8 `cmp r3,r2`; 0x00AB6A3C `bl 0xab62e0`; 0x00AB6A40 `strb r0,[r7,#1]`; 0x00AB6A48 `ldr r2,[sb,#0x14]`; 0x00AB6A54 `cmp r3,r2` | M6-002 / V3 | EXACT_SOURCE (this is a NEW detail: the redundant third read is required) |
| 2d | **Mapping entry layout (0x14 bytes).** +0x00 submaps, +0x04 mux, +0x08 submap {floor,residue}, +0x0C coupling_steps, +0x10 coupling_mag/ang pairs. Matches libvorbis `vorbis_info_mapping0`. | 0x00AB67A0 `mov r2,#0x14; bl 0x4d36dc` (memset) | M6-002 / NEW | EXACT_SOURCE |
| 3a | **`decode_map` (0x00AB9BB0) tree walk.** Bit reader state (pointer, bit position, bit count) is loaded; `n = read(s->dec_first... )`? The walk: node starts 0; `node = bit + 2*node`; `entry = t[node]` (32-bit `ldr`, `[s->t + node*4]`); if `entry < 0` continue, else stop. `s->t` is at codebook+0x10. | 0x00AB9BB4 `ldm r1,{r6,r7}`; 0x00AB9BBC `ldrb r4,[r6]` (bit window); 0x00AB9C34 `add ip,r5,ip,lsl#1`; 0x00AB9C40 `ldr ip,[sb,ip,lsl#2]`; 0x00AB9C48 `cmp ip,#0; bge 0xab9c28` | M6-002 / gapG 6.5 | EXACT_SOURCE |
| 3b | **Leaf unpacking.** On a non-negative entry: `packed = entry & 0x7FFFFFFF`; `q_bits` from codebook+0x30; `mask = (1<<q_bits)-1`; `for i in 0..dim-1: v[i] = packed & mask; packed >>= q_bits` (`dim` from codebook+0x00). So a leaf entry packs `dim` values of `q_bits` bits, low value first; negative (bit31) entries are internal nodes. | 0x00AB9C4C `bic ip,ip,#0x80000000`; 0x00AB9C50 `ldr r8,[r0,#0x30]`; 0x00AB9C64 `mvn sb,#0`; 0x00AB9C6C `mvn sb,sb,lsl r8`; 0x00AB9C74 `ldr r7,[r0]`; 0x00AB9C94 `and r1,ip,sb`; 0x00AB9CA0 `lsr ip,ip,r8` | M6-002 / gapG 6.5 | EXACT_SOURCE |
| 3c | **`_make_decode_table` (0x00AB96EC).** Builds the decode table the walk above consumes. It allocates at `codebook+0x10` (0x00AB97A0 and 0x00AB9A44) and fills it with 16-bit leaf words (`strh`), moving bit 31 down to bit 15 (`orr ip,ip,r5,lsr#16`); the alternate no-lookup path (`codebook+0x14==4`) builds a byte table. The 32-bit heap-index scratch is a stack array at `sp+0x10`. | 0x00AB96EC (entry); 0x00AB9770..0x00AB97A0 (alloc + store ptr at +0x10); 0x00AB97E4 `strh r6,[lr,r5]!`; 0x00AB9944..0xAB994C `and r5,ip,#0x80000000; orr ip,ip,r5,lsr#16; strh ip,[r2,lr]`; 0x00AB9A0C.. (lookup-type-4 path); 0x00AB9B20 `strb r0,[r2,r3]` | M6-002 / NEW | RECOVERABLE_GAP — decode_map's consumption is settled (3a/3b) but the builder's two-array widths and heap-index arithmetic are not reconciled (see Open questions #1). Read 0x00AB96EC..0x00AB9BB0 fully against codebook.c |
| 4a | **Residue inverse, type 0/1 entry (0x00AB770C).** `n = min(pcmend/2, info->end) - info->begin`; compact the nonzero channels into a channel-pointer array; allocate per-channel partword arrays. | 0x00AB770C..0x00AB7728 (`cmp r2,r1; rsble r0,r3,r2; rsbgt r0,r3,r1`); 0x00AB774C..0xAB7764 (compaction); 0x00AB7774..0xAB77D8 (partword arrays) | M6-002 / gapG 6.8 | EXACT_SOURCE for the entry |
| 4b | **Residue inverse, class/partword walk.** For each stage `s` (0..info+0x1A-1) and each partition group: decode the group's class word with `0x00ABA840`, split it per channel with `udiv 0x4BE310` and `mls`, then for each present stage (`cascades[pw] & (1<<s)`) take `book = stagebooks[(pw<<3)+s]` and call `decodev_add`. Point passed is `-8`. | 0x00AB7838/0x00AB7C84 `bl 0xaba840`; 0x00AB7CC8 `bl 0x4be310`; 0x00AB7CD8 `mls r7,sb,r3,r7`; 0x00AB7D38 `ldrb r3,[r3,sb]` + 0x00AB7D4C `tst r2,r8`; 0x00AB7D70 `ldrb r0,[r0,r3,lsl#3]`; 0x00AB7D84 `mvn ip,#7`; 0x00AB7D98 `bl 0xabaa6c` | M6-002 / gapG 6.8 | EXACT_SOURCE (control flow); the stage/partition book-mask arithmetic is only partly transcribed — RECOVERABLE_GAP for the inner offsets, read 0xAB7808..0xAB7E00 |
| 4c | **Residue inverse, type 2 (0x00AB745C).** `max = pcmend*ch/2`, early-out if no nonzero channel, `spp /= ch`, `beginoff = begin/ch`; calls `decodevv_add` (0x00ABABB8) with point `-8` and **no channel count** (the callee toggles channel 0/1, so it is hard-wired to 2 channels). | 0x00AB745C..0x00AB7508; 0x00AB76B8 `mvn r2,#7`; 0x00AB76D4 `bl 0xababb8` | M6-002 / gapG 6.9 | EXACT_SOURCE |
| 4d | **`decodev_add` (0x00ABAA6C).** `for i in 0..n-1: decode_map(book, tmp, opb, -8); for j in 0..dim-1: out[i+j] += tmp[j]` (int32, no saturation). | 0x00ABAAAC `bl 0xab9bb0`; 0x00ABAAB4..0xABAAC8 (`ldr r2,[r6]; ldr r1,[sp]; add; str`) then 0xABAAD0..0xABAB88 for dim 2..8 | M6-002 / gapG 6.8 | EXACT_SOURCE |
| 4e | **`decodevv_add` (0x00ABABB8).** Same decode_map loop; adds `tmp[j]` to `out[ch][i+offset]` and advances `ch ^= 1` between j's; channel index is only 0/1. Point `-8`. | 0x00ABABE8 `bl 0xab9bb0`; 0x00ABABEC..0xABAC04 (ch0 add); 0x00ABAC0C `eor r5,r5,#1`; repeats | M6-002 / gapG 6.9 | EXACT_SOURCE |
| 4f | **Coupling inverse (point -8 vectors), applied before inverse2.** Iterate coupling steps in reverse; `M=pcm[mag]`, `A=pcm[ang]`; `if M>0 { if A>0 {A=M-A; M=M} else {A=M; M=M+A} } else { if A>0 {A=M+A; M=M} else {A=M; M=M-A} }`. Integer add/sub on the fixed-point vectors, no scaling. | 0x00AB6E30..0x00AB6E68 (0x00AB6E30 `ldr r3,[r0]; ldr r2,[r1]`; 0x00AB6E3C `rsb r7,r2,r3`; 0x00AB6E48 `add r7,r3,r2`; 0x00AB6E5C `rsb r3,r2,r3`; stores `str r7,[r1]`, `str r3,[r1]`, `str r2,[r0]`, `str r3,[r0,#-4]`); 0x00AB6F24 `str r3,[r1]; add r2,r3,r2; str r2,[r0]` | M6-002 / gapG 6.8 | EXACT_SOURCE |
| 5 | **Floor dB table (0x01058BF0), 256 floats.** Byte-exact to Tremor's integer `FLOOR_fromdB_LOOKUP / 2^15` stored as float. `[i]*32768` recovers the integer table: 229,244,259,276,...,2147483648 ([255] float 65536.0). Full transcription in Appendix A. | 0x01058BF0; consumed via GOT 0x1040270 at 0x00AB9224 `ldr lr,[pc,lr]` | M6-002 / gapG 6.11 | EXACT_SOURCE (NEW: all 256 values transcribed) |
| 6a | **Floor1 inverse2 / render_line (0x00AB915C), declined-value test.** A memory value `memo[x]` is "declined" if `memo[x] != (memo[x] & 0x7FFF)` (bit 15 set); declined posts are skipped and not rendered. | 0x00AB91CC `ubfx r1,r2,#0,#0xf`; 0x00AB91D0 `cmp r2,r1`; 0x00AB91D4 `bne 0xab91bc` | M6-002 / NEW | EXACT_SOURCE |
| 6b | **render_line arithmetic.** `base = (y_prev*mult - y*mult) / (x - x_prev)` (signed `bl 0x4b3e70`); `sy = (dy<0)?base-1:base+1`; `ady = |dy| - |base*adx|`; multiply `out[x_prev] *= table[y_prev]`; then for `x = x_prev+1 .. x1-1`: `err += ady; if (err >= adx) { err -= adx; y += sy } else y += base; out[x] *= table[y]`. All in `f32`; `y` is the integer post value times `mult`. | 0x00AB9208 `bl 0x4b3e70`; 0x00AB9210/0x00AB921C (abs dy); 0x00AB9238 `sublt sl,r0,#1` / 0x00AB923C `addge sl,r0,#1` (sy); 0x00AB9250 `rsb r7,r3,r7` (ady); 0x00AB9260..0x00AB9268 (out[x0] *= table[y0]); 0x00AB9288 `add r3,r7,r3`; 0x00AB9294 `addgt r4,r0,r4`; 0x00AB9298 `addle r4,r4,sl`; 0x00AB929C `movle r3,ip`; 0x00AB92AC `vmul.f32 s15,s14,s15` | M6-002 / gapG 6.8 | EXACT_SOURCE |
| 7a | **Float MDCT entry (0x00AB4E34).** `shift = 13 - ilog2(n)` computed from the transform size; pre-symmetry helper `bl 0x00AB3D28`; butterfly helper `bl 0x00AB3FCC`; the NEON butterfly/post-rotation loop uses per-stage trig tables loaded through GOT 0x1040230..0x1040260; unit trig constants `0x3F3504F3` (0.70710677), `0x3EC3EF15`, `0x3F6C835E`. Second (short-block) path re-calls the helpers at 0x00AB5A78/0x00AB5A8C. | 0x00AB4E78..0x00AB4E94 (`mov r5,#4; lsr; tst; ...; rsb r3,r5,#0xd`); 0x00AB4EB8 `bl 0xab3d28`; 0x00AB4ECC `bl 0xab3fcc`; 0x00AB4FD0..0x00AB4FE0 (trig `vld1.32 {dX[]}`); 0x00AB5008.. (loop); 0x00AB5A78/0x00AB5A8C | M6-002 / V7 | EXACT_SOURCE for the structure |
| 7b | **Normalisation claim (gapG 6.11).** The only `2^-24` (`0x33800000`) constants in the Wwise region are the literal pool 0x00AB3CF8..0x00AB3D04, belonging to the function at **0x00AB39D8** (`vmov.f32 q0,#0.5` at 0x00AB3A70, `4 x vmul.f32 qN,qN,q1` applied at 0x00AB3CB4..0x00AB3CC4). That function has **no ARM caller and no absolute pointer reference in the image**; the decode entry 0x00AB4E34 contains no `0x33800000` and no scale constant. | 0x00AB3CF8 `0x33800000`; 0x00AB3AB0/0x00AB3AB4 `vldr d2/d3,[pc->0xab3cf8/0xab3cfc]`; 0x00AB3CB4 `vmul.f32 q8,q8,q1`; xref scan of the ARM region (`.scratch/m6-vorbis/xref2.py`) finds no caller for 0x00AB39D8 | M6-002 / gapG 6.11 | **UNKNOWN / potential contradiction.** The `2^8 * 2^16 * 2^-24 = 1` chain is not established for the decode path. See Open questions #2 |
| 8a | **Window tables (0x01054490).** Byte-for-byte identical to libvorbis `window.c` `vwin256`, `vwin512`, `vwin1024`, `vwin2048`, `vwin4096` (verified by extracting the reference arrays and matching the full byte strings). Layout and VAs: vwin256 0x01054490 (128 floats), vwin512 0x0104690 (256), vwin1024 0x0104A90 (512), vwin2048 0x0105290 (1024), vwin4096 0x0106290 (2048). `vwin64`, `vwin128`, `vwin8192` are **absent**. | table starts as cited; selection code 0x00AB3564..0x00AB3738 switches on `blocksize/2` in {0x80,0x100,0x200,0x400,0x800} and points at 0x1054490/0x1054690/0x1054a90/0x1055290/0x1056290; default at 0x00AB3728 `mov r8,#0` | M6-002 / V7 | EXACT_SOURCE (NEW: the table set is 5 windows, not 8) |
| 8b | **Window selection edge.** `blocksize/2` outside {128,256,512,1024,2048} (i.e. blocksize outside 256..4096) takes the default path, which sets the window pointer to 0. The header accepts blocksizes 64..8192, so a declared blocksize of 64/128/8192 would reach 0. | 0x00AB356C `cmp r1,#0x200; beq/bgt`; 0x00AB3578 `cmp r1,#0x80`; 0x00AB3580 `cmp r1,#0x100`; 0x00AB3728 `mov r8,#0` | M6-002 / NEW | EXACT_SOURCE for the dispatch; the consequence of a 0 window is UNKNOWN (read 0x00AB3728 onward, `sb` default at 0x00AB3710) |
| 9 | **Windowed combine / overlap (0x00AB5A94).** NEON loop over f32. Two forms: `out = a*wA + b*wB` (0x00AB5BA4..0x00AB5BB0: `vmul q9=q10*q9; vmul q8=q8*q10; vadd q8=q8+q9`) with 64-bit reversals (`vrev64.32`, `vswp`) for the mirrored right window, and `out = a*w - b*w2` (`vnmls.f32` at 0x00AB5D64) on the branch where only one window applies. Per-channel, in place. | 0x00AB5A94 entry; 0x00AB5BA4..0x00AB5BBC; 0x00AB5C28 `vmul.f32 s15,s14,s15`; 0x00AB5D64 `vnmls.f32 s15,s13,s14`; called at 0x00AB3664 | M6-002 / V8 | partial EXACT_SOURCE — the vectorized mul/add order is read, but the exact per-window branch selection is RECOVERABLE_GAP (read 0x00AB5A94..0x00AB6038 with the caller args at 0x00AB3630..0x00AB3660) |
| 9b | **Planar-float output layout.** `0x00AB3520` allocates `n/2 * 4 * channels` and stores per-channel base pointers at `[vd+0x14][c]` (`str r5,[r2,r3,lsl#2]`), then for each channel indexes the per-channel pointer arrays. Channel `c` is at `base + c*maxFrames`. | 0x00AB37AC..0x00AB37E4 (alloc + per-channel pointers); 0x00AB35EC/0x00AB3658 `ldr rX,[...,r5,lsl#2]` | M6-002 / V8 | EXACT_SOURCE |
| 10a | **End-trim setter (0x00AB3244).** `(dsp, skip, trim)`: store `skip` (u16) at +0x2C, `trim` (u16) at +0x2E, set +0x1C and +0x20 to -1 (invalid). | 0x00AB3248 `strh r1,[r0,#0x2c]`; 0x00AB3254 `strh r2,[r3,#0x2e]`; 0x00AB3258 `str ip,[r3,#0x20]`; 0x00AB325C `str ip,[r3,#0x1c]` | M6-002 / gapG 6.12 | EXACT_SOURCE |
| 10b | **End-trim consumption (0x00AB3884..0x00AB3910).** In the block-input path: `current = [dsp+0x20]` (newly computed pcm_current), `returned = [dsp+0x1c]` (pcm_returned); if `[vb+8]` (eofflag) is set: `current = max(current - trim, returned)` and store to `[dsp+0x20]`. `trim = [dsp+0x2e]`. | 0x00AB3900 `ldrh r3,[r4,#0x2e]`; 0x00AB3904 `rsb r3,r3,sb`; 0x00AB3908 `cmp r3,r2`; 0x00AB390C `movlt r3,r2`; 0x00AB3910 `str r3,[r4,#0x20]`; 0x00AB38F4 `ldrb r3,[r7,#8]`; 0x00AB38B8/0x00AB38BC (current = n/4 + sb/4) | M6-002 / gapG 6.12 | EXACT_SOURCE |
| 10c | **Start-skip consumption (leading samples).** `skip = [dsp+0x2C]`; if skip>0: `pcm_returned = min(current, skip)`; if `current >= skip` skip is cleared, else the surplus `skip-current` is stored back and the function emits nothing until `(skip-current) < blocksize/4`. | 0x00AB38C0 `beq 0xab38f4`; 0x00AB38C4 `cmp sb,r2`; 0x00AB38C8 `str r2,[r4,#0x1c]`; 0x00AB38D4 `rsb r2,sb,r2`; 0x00AB38E0 `strh r2,[r4,#0x2c]`; 0x00AB38E4..0xAB38EC; 0x00AB395C `strh r1,[r4,#0x2c]` (skip=0) | M6-002 / gapG 6.12 | EXACT_SOURCE |
| 11 | **BlockSizes shift-count edge (0x00AB63A0).** `1 << bs0pow` and `1 << bs1pow` use ARM register `lsl`; an exponent >= 32 yields 0. Checks: `bs0size >= 64`, `bs0size <= bs1size`, `bs1size <= 8192`, else return `-0x85` (`mvn r0,#0x84`). So `bs0pow>=32` -> 0 -> "<64" -> error; `bs1pow>=32` -> 0 -> passes the 8192 check but fails `bs0<=bs1` if `bs0size>0`. Also `bs0pow<6` -> 1..32 < 64 -> error. | 0x00AB639C `mov r3,#1`; 0x00AB63A0 `lsl r1,r3,r4`; 0x00AB63A4 `lsl r3,r3,r6`; 0x00AB63A8 `cmp r1,#0x3f`; 0x00AB63BC `cmp r1,r3`; 0x00AB63C4 `cmp r3,#0x2000`; 0x00AB63D4 `mvnne r0,#0x84` | M6-002 / V2 | EXACT_SOURCE |

## Appendix A — floor table 0x01058BF0 (256 f32, transcribed)

```
0.006988525390625f, 0.0074462890625f, 0.007904052734375f, 0.0084228515625f,
0.00897216796875f, 0.009552001953125f, 0.01019287109375f, 0.010833740234375f,
0.01153564453125f, 0.012298583984375f, 0.013092041015625f, 0.013946533203125f,
0.014862060546875f, 0.01580810546875f, 0.016845703125f, 0.0179443359375f,
0.01910400390625f, 0.020355224609375f, 0.02166748046875f, 0.0230712890625f,
0.02459716796875f, 0.02618408203125f, 0.02789306640625f, 0.029693603515625f,
0.0316162109375f, 0.03369140625f, 0.035858154296875f, 0.0382080078125f,
0.040679931640625f, 0.0433349609375f, 0.046142578125f, 0.04913330078125f,
0.052337646484375f, 0.05572509765625f, 0.059356689453125f, 0.063232421875f,
0.06732177734375f, 0.07171630859375f, 0.07635498046875f, 0.081329345703125f,
0.08660888671875f, 0.092254638671875f, 0.098236083984375f, 0.1046142578125f,
0.111419677734375f, 0.11865234375f, 0.126373291015625f, 0.13458251953125f,
0.143310546875f, 0.15264892578125f, 0.162567138671875f, 0.173126220703125f,
0.18438720703125f, 0.19635009765625f, 0.2091064453125f, 0.22271728515625f,
0.2371826171875f, 0.252593994140625f, 0.269012451171875f, 0.2864990234375f,
0.30511474609375f, 0.324920654296875f, 0.346038818359375f, 0.3685302734375f,
0.392486572265625f, 0.417999267578125f, 0.445159912109375f, 0.474090576171875f,
0.5048828125f, 0.537689208984375f, 0.5726318359375f, 0.60986328125f,
0.649505615234375f, 0.69171142578125f, 0.736663818359375f, 0.784515380859375f,
0.83551025390625f, 0.889801025390625f, 0.9476318359375f, 1.00921630859375f,
1.074798583984375f, 1.144622802734375f, 1.219024658203125f, 1.298248291015625f,
1.382598876953125f, 1.472442626953125f, 1.568145751953125f, 1.6700439453125f,
1.778594970703125f, 1.8941650390625f, 2.017242431640625f, 2.148345947265625f,
2.2879638671875f, 2.4366455078125f, 2.595001220703125f, 2.763641357421875f,
2.9432373046875f, 3.134490966796875f, 3.33819580078125f, 3.55511474609375f,
3.786163330078125f, 4.032196044921875f, 4.29425048828125f, 4.57330322265625f,
4.870513916015625f, 5.18701171875f, 5.52410888671875f, 5.883087158203125f,
6.265411376953125f, 6.67254638671875f, 7.106170654296875f, 7.5679931640625f,
8.059783935546875f, 8.58355712890625f, 9.141357421875f, 9.735443115234375f,
10.36810302734375f, 11.0418701171875f, 11.759429931640625f, 12.52362060546875f,
13.337493896484375f, 14.2042236328125f, 15.127288818359375f, 16.1103515625f,
17.15728759765625f, 18.27227783203125f, 19.459716796875f, 20.72430419921875f,
22.07110595703125f, 23.505401611328125f, 25.03289794921875f, 26.659698486328125f,
28.392181396484375f, 30.237274169921875f, 32.2022705078125f, 34.294952392578125f,
36.52362060546875f, 38.897125244140625f, 41.424896240234375f, 44.116912841796875f,
46.98388671875f, 50.037139892578125f, 53.288848876953125f, 56.751861572265625f,
60.439910888671875f, 64.36764526367188f, 68.55059814453125f, 73.00540161132812f,
77.74972534179688f, 82.80233764648438f, 88.18328857421875f, 93.9139404296875f,
100.01699829101562f, 106.51666259765625f, 113.438720703125f, 120.81060791015625f,
128.66156005859375f, 137.022705078125f, 145.92721557617188f, 155.410400390625f,
165.50982666015625f, 176.26559448242188f, 187.72030639648438f, 199.91943359375f,
212.91134643554688f, 226.74752807617188f, 241.48284912109375f, 257.1757507324219f,
273.88848876953125f, 291.6872863769531f, 310.6427917480469f, 330.8301086425781f,
352.3293151855469f, 375.22564697265625f, 399.60992431640625f, 425.5788269042969f,
453.2353515625f, 482.6891174316406f, 514.0569458007812f, 547.4633178710938f,
583.04052734375f, 620.9298095703125f, 661.2813110351562f, 704.2550659179688f,
750.0215454101562f, 798.76220703125f, 850.6702270507812f, 905.9515991210938f,
964.825439453125f, 1027.5252685546875f, 1094.299560546875f, 1165.413330078125f,
1241.1484375f, 1321.8052978515625f, 1407.7037353515625f, 1499.1842041015625f,
1596.609619140625f, 1700.3663330078125f, 1810.86572265625f, 1928.5458984375f,
2053.873779296875f, 2187.345947265625f, 2329.491943359375f, 2480.87548828125f,
2642.096923828125f, 2813.795166015625f, 2996.6513671875f, 3191.390625f,
3398.785400390625f, 3619.657470703125f, 3854.88330078125f, 4105.3955078125f,
4372.18701171875f, 4656.31640625f, 4958.91015625f, 5281.16796875f,
5624.3681640625f, 5989.87109375f, 6379.12646484375f, 6793.67822265625f,
7235.169921875f, 7705.35205078125f, 8206.0888671875f, 8739.3662109375f,
9307.2998046875f, 9912.140625f, 10556.2880859375f, 11242.294921875f,
11972.8818359375f, 12750.947265625f, 13579.5751953125f, 14462.0537109375f,
15401.87890625f, 16402.779296875f, 17468.724609375f, 18603.94140625f,
19812.9296875f, 21100.486328125f, 22471.712890625f, 23932.052734375f,
25487.291015625f, 27143.599609375f, 28907.544921875f, 30786.119140625f,
32786.7734375f, 34917.4453125f, 37186.57421875f, 39603.16796875f,
42176.8046875f, 44917.69140625f, 47836.6953125f, 50945.39453125f,
54256.11328125f, 57781.98046875f, 61536.98046875f, 65536.0f,
```

## Existing records contradicted by the source
1. **gapG 6.11 / M6-002's "output is nominally ±1.0" chain.** The record says "The IMDCT stage 0x00AB39D8 multiplies its outputs by q1 = 2^-24". The decode MDCT is 0x00AB4E34; 0x00AB39D8 has no caller in the ARM region and no pointer reference (scan in `.scratch/m6-vorbis/xref2.py`, `ptrscan.py`), and 0x00AB4E34 contains no `2^-24`. The stated factor chain is not established. Evidence: `.scratch/m6-vorbis/imdct.txt`, `imdct2.txt`, `constscan.py`, `xref2.py`.
2. **V7's "Windows are libvorbis float vwin tables at 0x01054490"** is right in substance but the set is incomplete: only vwin256/vwin512/vwin1024/vwin2048/vwin4096 are present; vwin64/vwin128/vwin8192 are absent (`cmpwin.py`). This is a new fact, not a contradiction, but the row should say "the five windows actually shipped".

## Existing records whose evidence is too weak to keep their status
- **gapG 6.11 (RECOVERABLE_GAP):** its factor chain rests on a function that is unreferenced in the decode path. It should stay RECOVERABLE_GAP but with the updated evidence and the explicit caveat that 0x00AB39D8's role (forward transform? encoder-only?) is unestablished.
- **gapG 6.8 ("Both types call decodev_add"):** confirmed for type 1; type 0 is unexercised by shipped media and its branch at 0x00AB770C is the same code path, so the row holds, but it was read only at the entry.
- **M6-002's "decodev_add a[i++] += v[j]"**: confirmed but the index walk is `out[i+j] += tmp[j]` with `dim` consecutive entries per codeword, not a single increment (4d). The wording is loose; the record should say "dim values per codeword".
- **`make_decode_table`**: not previously claimed; the builder remains RECOVERABLE_GAP.

## Open questions for the manager
1. **`_make_decode_table` reconciliation (Q3).** `decode_map` reads a **32-bit** signed entry at `[codebook+0x10]` with a `LSL #2` index, but `_make_decode_table` 0x00AB96EC stores **16-bit** halfwords to its table pointer (`strh r6,[lr,r5]!` 0x00AB97E4; `strh ip,[r2,lr]` 0x00AB994C) and writes its 32-bit index scratch to a stack array (`str r3,[r4,r1,lsl#2]` 0x00AB97EC). Something is off either in the struct offsets or in the two table widths. A targeted read of 0x00AB96EC..0x00AB9BB0 and of the codebook unpack 0x00ABA188 (both call sites 0x00ABA3EC, 0x00ABA77C) is needed before the decoder's decode table can be built.
2. **IMDCT normalisation (Q7).** Identify 0x00AB39D8 (who reaches it; is it the forward `mdct_forward`?). If the decode MDCT 0x00AB4E34 really has no scale, then either the residue/floor factors differ from `2^8`/`2^16`, or a scale is applied elsewhere (e.g. the transform look init or the window combine). Resolve before claiming output scale.
3. **Floor look helper 0x00AB8018 (Q1d).** It builds the `[floor+0x0C]` array consumed by `floor1_inverse2` as render order. Name it/read it to complete the floor1 setup, and confirm the `look` struct offsets used by 0x00AB915C ([look+0x10], [look+0x1C],[look+0x20],[look+0x28], [look+8],[look+0x0C]).
4. **Window default path.** What happens when `blocksize/2` is outside the five present windows (0x00AB3728 sets the window pointer to 0)? Shipped media may never hit it, but the setup accepts 64..8192; decide whether this is RECOVERABLE_GAP or an unexercised branch.
5. **Mode count / residue types.** All shipped setups have mode count 2 and mono->type 1 / stereo->type 2 (gapG 6.10), so the 1-bit mode read and the type-2 two-channel hard-wiring are safe for shipped data; the header's general case is not settled and should stay marked as a deviation.

## Notes on scope
- Read-only; nothing outside `.scratch/` was written.
- I did not run `fidelity.py` or any repo generator; no repo file was modified.
- The C# was used only to see which behaviours need an answer.
- Scratch evidence: `.scratch/m6-vorbis/` (disassembly dumps and comparison scripts listed at the top of this report).
