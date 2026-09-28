# M6-002 Vorbis packet driver: inventory (0x00AB6B14..0x00AB6F20) and setup context (0x00AB6380..0x00AB6780)

Read-only extraction. All addresses are libcozmoEngine.so VAs (ARM mode). Instructions were read with
capstone; `re-analysis/decomp/libcozmoEngine/` was used only for navigation. Nothing in the repo was modified
outside `.scratch/m6-vorbis-packet/`.

## Premise corrections (the task's ranges do not line up with the source)

1. **The packet entry is `0x00AB3780`, not `0x00AB6B14`.** `0x00AB6B14` is a separate function that
   `0x00AB3780` reaches by a tail call: `0x00AB3934: b #0x00AB6B14`. The mode/header read, the block-size
   selection, the first-window copy and start-skip/end-trim are all in `0x00AB3780`
   (`0x00AB37FC..0x00AB3934`). `0x00AB6B14` is the per-packet inverse (floor1 inverse1, residue inverse,
   coupling inverse, floor1 inverse2, `mdct_backward`). Ghidra did not create a function for `0x00AB6B14`
   (its `index.tsv` ends `FUN_00ab6788` at `0x00AB6B13` and starts the next at `0x00AB6F34`), which is why the
   decompiler inlined it into `FUN_00ab3780` and why the built C# calls the range "not built".
2. **Window/overlap assembly is not in `0x00AB6B14`.** It is `0x00AB3520`, called from the framing function
   `0x00AB7E40` (`0x00AB7FB4` and `0x00AB7FF0`), which calls the window-combine `0x00AB5A94` per channel
   (`0x00AB3664`). `0x00AB6B14` writes only the per-channel MDCT spectra.
3. **The top-level entry is `0x00AB7E40`** (packet framing, u16 size per packet, V5). It calls
   `0x00AB3780` at `0x00AB7EC8` for each packet and `0x00AB3520` at `0x00AB7FB4` for the window/output.
4. **Setup `0x00AB6380`/`0x00AB63E0` is not called from the packet path.** It is called from the setup cache
   `0x00AB2D74`. It fills the setup struct that `0x00AB3780`/`0x00AB6B14` read through `dsp+0x10`.

## Coverage against M6-002 / corrections C5..C9

The M6-002 record's evidence already names every arithmetic leaf this path calls: floor1 inverse1
`0x00AB8E60`, residue inverse `0x00AB73F8`, coupling inverse `0x00AB6E30`, floor1 inverse2 `0x00AB915C`,
the IMDCT `0x00AB4E34`, window-combine `0x00AB5A94`, the output driver `0x00AB3520`, and the mode / skip /
trim `0x00AB37FC`/`0x00AB3884`. C5/C6/C7 settled the arithmetic inside those leaves; C9/X5 settled the IMDCT.

What no record states is the **driver**: the decoder-state field offsets, the call order, the per-channel and
per-submap loops, the block-size selection, the mapping/submap channel compaction, the first-window copy,
and where the window/overlap driver sits in the sequence. The built `WwiseVorbisNative.cs` names exactly this
as its only unread item (`UnreadArithmetic`, line 176: "the Vorbis packet driver ... is not built, so the
packet inverse call site mdct_backward(n, pcm[channel]) at 0x00AB6EEC ... is not reached"). Therefore the
packet driver is **a NEW set of rows** (new evidence rows for M6-002), not already covered. M6-002 stays
IMPLEMENTATION_GAP (unbuilt); these rows are what B1 must build.

## Production path (call order)

```
0x00AB7E40  framing: for each u16-sized packet -> 0x00AB3780(dsp, packet)
  0x00AB3780  mode read; blockflag/block size; first-window copy; start-skip/end-trim
     tail b 0x00AB6B14(dsp, mapping)
        0x00AB6B14  per channel: 0x00AB8E60 floor1 inverse1
                    mark coupling channels
                    per submap: 0x00AB73F8 residue inverse
                    coupling inverse (inline)
                    per channel: 0x00AB915C floor1 inverse2
                    per channel: 0x00AB4E34 mdct_backward
  (back in 0x00AB7E40) alloc output; 0x00AB3520(dsp, out, n, ch?)
        0x00AB3520  per channel: 0x00AB5A94 window-combine + overlap-add; save overlap
```

## Rows

| step | what the original does | citation | existing record id or NEW | classification |
|---|---|---|---|---|
| P1 | **Packet framing.** The decoder state is `param_1`; the Vorbis source is at `param_1+4` (`piVar8 = param_1+4`, decomp 0xab7e40). Loop while `offset+2 <= total`: read the next packet's size as `u16` (`ldrh r2,[r6,r3]`); if the remaining bytes `< size` set result `{0, 2}` and return (error); call the packet decoder with a 0x10-byte descriptor `{body pointer, size, eofflag}` (`r0 = data+offset+2` is the body pointer, `r2` the u16 size, `[sp+0x14]` the eofflag). | `0x00AB7E78: ldrh r2,[r6,r3]`; `0x00AB7E7C: cmp sb,r2`; `0x00AB7E88: ldr ip,[r4,#8]`; `0x00AB7EC8: bl #0xab3780`; `0x00AB7F20..0x00AB7F28` | M6-002 V5 | EXACT_SOURCE |
| P2 | **Result codes.** After the last packet: store the consumed offset, result 0x2E (NoMoreData); if the last packet was flagged (`param_1[9] != 0`) call `0x00AB3978` (stream reset). When a packet yields samples: allocate `channels * samples * 4` with `FUN_00a7a894`, result 0x2D (DataReady), else 0x11; the end-of-stream call `0x00AB3520(dsp,0,0,0)` returns 0x11/0x2D/0x2E. | `0x00AB7EF4..0x00AB7F14`; `0x00AB7F34..0x00AB7F78`; `0x00AB7FB4`; `0x00AB7FF0..0x00AB800C` | M6-002 V5 | EXACT_SOURCE |
| P3 | **Setup cache context.** A decoded setup is kept in a hash keyed by `hdr+0x78` with a refcount; a miss allocates 0x1C bytes + an arena and runs the block-size check then the setup parse. | `0x00AB2D74..0x00AB2F58` (callers of 0x00AB6380/0x00AB63E0 per decomp) | M6-002 V1 | EXACT_SOURCE |
| P4 | **Block sizes.** `setup+0 = 1<<bs0` (`hdr+0x7C`), `setup+4 = 1<<bs1` (`hdr+0x7D`); the function zeroes 0x30 bytes first. Range check: fail (-0x85) when `bs0 < 64`, `bs0 > bs1` or `bs1 > 8192`. | `0x00AB6398: bl memset`; `0x00AB63A0: lsl r1,r3,r4`; `0x00AB63A4: lsl r3,r3,r6`; `0x00AB63B0: str r1,[r5]`; `0x00AB63AC: str r3,[r5,#4]`; `0x00AB63A8..0x00AB63DC` | M6-002 V2 | EXACT_SOURCE |
| P5 | **Setup header field order** (all bit reads LSB-first through `0x00AB62E0`, mask table `0x01005360`): codebook count `read(8)+1` -> `setup+0x18`; per codebook `read(10)` id -> pointer table (`0x00AB64A4`), unpacked by `0x00ABA188`; floor count `read(6)+1` -> `setup+0x10`; residue count `read(6)+1` -> `setup+0x14`; mapping count `read(6)+1` -> `setup+0xc`; mode count `read(6)+1` -> `setup+8`; each mode `{read(1) blockflag, read(8) mapping}` at `setup+0x1c` stride 2, error if mapping >= mapping count. | `0x00AB6400..0x00AB6408`; `0x00AB64A4/0x00AB64B0`; `0x00AB64C0..0x00AB64D0`; `0x00AB6578..0x00AB6588`; `0x00AB6610..0x00AB6620`; `0x00AB66B0..0x00AB66C0`; `0x00AB6718..0x00AB675C` | M6-002 V3 | EXACT_SOURCE |
| P6 | **Setup array allocations.** codebook array `setup+0x2c` stride 0x3c; floor array `setup+0x24` stride 0x24, each parsed by `0x00AB88D8` (`0x00AB6558`); residue array `setup+0x28` stride 0x1c, each parsed by `0x00AB6F34` (`0x00AB65F8`); mapping array `setup+0x20` stride 0x14, each parsed by `0x00AB6788` (`0x00AB666C`); mode array `setup+0x1c`. | `0x00AB640C..0x00AB6458`; `0x00AB64D4..0x00AB6524`; `0x00AB6558`; `0x00AB658C..0x00AB65C4`; `0x00AB65F8`; `0x00AB6624..0x00AB6634`; `0x00AB666C`; `0x00AB66C4..0x00AB66FC` | M6-002 V3 | EXACT_SOURCE |
| P7 | **Mapping entry layout** (`0x00AB6788`): `memset(entry,0,0x14)`; `+0` submaps (`read(1)==0 ? 1 : read(4)+1`); `+0xc` coupling steps (`read(8)+1`) and `+0x10` mag/ang pairs; `read(2)` reserved must be 0; `+4` mux (`read(4)` per channel when submaps>=2); `+8` submap table, `read(8)` x3 per submap with the first discarded, storing `{floor, residue}` bytes. | `0x00AB6788` body; `0x00AB63E0` call at `0x00AB666C` | M6-002 (mapping setup, C5) | EXACT_SOURCE (field order); the C5 submap-byte claim is confirmed here: floor at `+0`, residue at `+1`, stride 2 |
| P8 | **Residue setup** (`0x00AB6F34`): `memset(entry,0,0x1c)`; `+0` type `read(2)`; `+0xc` begin `read(24)`; `+0x10` end `read(24)`; `+0x14` grouping `read(24)+1`; `+0x18` partitions `read(6)+1`; `+0x19` groupbook `read(8)`; `+0x1a` stages (`read(8)` count of cascades actually present); stage books read per stage. | `0x00AB6F34` body; `0x00AB6F48 bl memset`; `0x00AB6F54..0x00AB6F5C`; call at `0x00AB65F8` | M6-002 V3 / gapG 6.7 | EXACT_SOURCE |
| P9 | **Packet entry prologue.** `r0 = dsp`, `r1 = packet`. `dsp+0x10 = setup`, `dsp+0xc = channels`. Compute the per-channel output stride = `round_up((bs1/2)*4*channels,16)/channels` and store `dsp+0x14[c] = work_base + c*stride` where `work_base = [0x0108E650]` (`r3 = 0x0108E648`, `ldr r5,[r3,#8]`). The stride always uses `bs1` (the long block), independent of the current block flag. | `0x00AB3780: push`; `0x00AB3788: ldr r8,[r0,#0x10]`; `0x00AB3790: ldr r6,[r0,#0xc]`; `0x00AB3794: ldr r3,[pc,#0x1d8]` -> `0x00AB3974 = 0x005DAEA4`; `0x00AB3798: ldr r0,[r8,#4]`; `0x00AB37A8: ldr r5,[r3,#8]`; `0x00AB37BC: bl #0x4b3e70` (idiv); `0x00AB37CC..0x00AB37E0` | NEW | EXACT_SOURCE |
| P10 | **Bit reader state.** `dsp+0 = packet data pointer`, `dsp+8 = packet length`, `dsp+4 = 0` (bit position). The reader `0x00AB62E0(struct, bits)` is LSB-first: `return mask[bits] & (u32 from dsp+0 shifted by dsp+4)`, advances `dsp+0` by `(bits+bitpos)>>3` and `dsp+4 = (bits+bitpos)&7`; `dsp+8` is the remaining-byte counter. | `0x00AB37E8..0x00AB3804`; `0x00AB62E0` body (`0x00AB62E4 ldr pbVar6,[param_1]`; `0x00AB62E8 uVar7=param_1[1]`; `0x00AB62F0 ldr uVar2,[&DAT_01005360+param_2*4]`; `0x00AB6378 str param_1[1]=uVar4&7`) | M6-002 V3 (reader) | EXACT_SOURCE |
| P11 | **Mode / header read.** Exactly one bit: `read_bits(dsp, 1)` -> mode number. No packet-type bit and no prev/next window bits. Mode table entry at `setup+0x1c + mode*2`: byte 0 = blockflag, byte 1 = mapping. | `0x00AB37FC: mov r1,#1`; `0x00AB3808: bl #0xab62e0`; `0x00AB3810: ldr r2,[r8,#0x1c]`; `0x00AB3824: ldrb r2,[r2,r0,lsl #1]`; `0x00AB3924: ldrb r3,[sl,#1]` | M6-002 V6 | EXACT_SOURCE |
| P12 | **Block-size handling.** Save the previous block flag at `dsp+0x24`; read the new block flag to `dsp+0x28`; the previous block size is `setup[old_blockflag]` and the current is `setup[new_blockflag]` (`setup+0` = bs0, `setup+4` = bs1). | `0x00AB380C: ldr r3,[r4,#0x28]`; `0x00AB3818: str r3,[r4,#0x24]`; `0x00AB3820: ldr sb,[r8,r3,lsl #2]`; `0x00AB382C: str r2,[r4,#0x28]`; `0x00AB3894: ldr r3,[r8,r3,lsl #2]`; `0x00AB6B30: ldr r2,[r0,#0x28]`; `0x00AB6B40: ldr r2,[sl,r2,lsl #2]` | NEW | EXACT_SOURCE |
| P13 | **First-window copy.** If `dsp+0x30 == 0`, for each channel `memcpy(dsp+0x18[ch], dsp+0x14[ch] + aligned(old_block_size), aligned(old_block_size))`, then set `dsp+0x30 = 1`. (`dsp+0x18` is the overlap/previous buffer array.) `0x00AB3520` later sets `dsp+0x30 = 1` (`0x00AB36B4`), and `0x00AB6B14` clears it to 0 (`0x00AB6F18`). | `0x00AB3814: ldrb r5,[r4,#0x30]`; `0x00AB381C: cmp r5,#0`; `0x00AB3830: bne #0xab3878`; `0x00AB3844..0x00AB3874`; `0x00AB36B4: strb r2,[r4,#0x30]`; `0x00AB6F18: strb r0,[r4,#0x30]` | NEW | EXACT_SOURCE (the flag's gating intent beyond "previous window saved" is not asserted) |
| P14 | **Start-skip / end-trim.** `dsp+0x2c` = start-skip (u16), `dsp+0x2e` = end-trim (u16). On the first packet (`dsp+0x1c == -1`) `dsp+0x1c = 0`, `dsp+0x20 = 0`, and if `skip >= bs1/2` the packet is dropped (return). Otherwise `dsp+0x20 = new_bs/4 + old_bs/4`; if `skip == 0` skip the trim; if `total < skip` set `dsp+0x1c = total`, `dsp+0x2c = skip-total`, and drop if `skip-total >= bs1/2`; else `dsp+0x1c = skip`, `dsp+0x2c = 0`. If the packet eofflag (`packet+8`) is set, `dsp+0x20 = max(total - trim, returned)`. | `0x00AB3878..0x00AB3910`; `0x00AB3938..0x00AB3970` | gapG 6.12 / M6-002 evidence `0x00AB3244/0x00AB3884` | EXACT_SOURCE |
| P15 | **Tail call to the inverse.** `pop {...,lr}` then compute `r1 = setup+0x20 + mode.mapping*0x14` (mapping array) and `b 0x00AB6B14` with `r0 = dsp`, `r1 = &mapping[mode.mapping]`. This is a tail call: the callee returns directly to the framing caller. | `0x00AB3914..0x00AB3934` (`0x00AB391c ldr r1,[r8,#0x20]`; `0x00AB392c add r3,r3,r3,lsl #2`; `0x00AB3930 add r1,r1,r3,lsl #2`; `0x00AB3934: b #0xab6b14`) | NEW | EXACT_SOURCE |
| P16 | **Inverse prologue.** `r4 = dsp`, `r6 = mapping`, `sl = dsp+0x10 = setup`, `ip = dsp+0xc = channels`. `n = setup[blockflag]` (the current block size) is stored at `[fp-0x2c]`. Four per-channel `u32` stack arrays are allocated: A (`[fp-0x34]`, the per-submap channel-data pointer list), B (`r8`, the per-submap nonzero flags), C (`r7`, the per-channel floor-nonzero flags), D (`[fp-0x30]`, the per-channel floor-value pointers). | `0x00AB6B14: push`; `0x00AB6B20: ldr ip,[r0,#0xc]`; `0x00AB6B2C: ldr sl,[r0,#0x10]`; `0x00AB6B30..0x00AB6B40`; `0x00AB6B48..0x00AB6B70` | NEW | EXACT_SOURCE |
| P17 | **Floor decode (floor1 inverse1), per channel.** For each channel: select its submap floor byte (`submaps<2 ? 0 : mux[ch]<<1`), look up `floor_entry = setup+0x24 + floorbyte*0x24`, alloca `floor+0x1c` (post count) u32, call `0x00AB8E60(dsp, floor_entry, out)`. Store the returned pointer in `D[ch]`; set `C[ch] = (result != 0)`; zero the channel output buffer `dsp+0x14[ch]` for `n*2` bytes (`n/2` floats). | `0x00AB6B98..0x00AB6C18` (`0x00AB6ba8 cmp r3,#1`; `0x00AB6bb8 ldrbgt r1,[r3,sb]`; `0x00AB6bbc lslgt r1,r1,#1`; `0x00AB6bc0 ldrb r1,[lr,r1]`; `0x00AB6bc8 add r1,r2,r1,lsl #2`; `0x00AB6be4 bl #0xab8e60`; `0x00AB6bf4 str r0,[r5,#4]!`; `0x00AB6bfc str r0,[r7,sb,lsl #2]`; `0x00AB6c0c bl #0x4d36dc`) | C7 (floor1 inverse1) for the arithmetic; the driver row NEW | EXACT_SOURCE |
| P18 | **Coupling channel marking.** For each coupling step `{mag,ang}` (backwards from `mapping+0x10 + steps*2`): if `C[mag]` or `C[ang]` is set, set both. | `0x00AB6C24..0x00AB6C7C` (`0x00AB6c2c str r3,[fp,#-0x28]`; `0x00AB6c38 ldr r3,[r6,#0x10]`; `0x00AB6c44..0x00AB6c7c`) | NEW | EXACT_SOURCE |
| P19 | **Residue decode, per submap.** For each submap `s` (`mapping+0`): compact the channels whose `mux[ch]==s` into `A[count]=dsp+0x14[ch]` and `B[count]=C[ch]`; look up `residue_entry = setup+0x28 + submap[s].residue*0x1c`; call `0x00AB73F8(dsp, residue_entry, A, B, count)` (5th arg on the stack). The no-mux path (`mapping+4 == 0`) takes `0x00AB6D68` and compacts all channels. | `0x00AB6C80..0x00AB6DB4` (`0x00AB6ca4 ldr r3,[r6,#4]`; `0x00AB6cc0..0x00AB6d10`; `0x00AB6d20 add r3,r3,r5,lsl #1`; `0x00AB6d28 ldrb lr,[r3,#1]`; `0x00AB6d30 ldr r1,[r2,#0x28]`; `0x00AB6d3c rsb lr,lr,lr,lsl #3`; `0x00AB6d40 add r1,r1,lr,lsl #2`; `0x00AB6d44 bl #0xab73f8`; `0x00AB6d38 str ip,[sp]`) | C5/C6/C7 (residue arithmetic) for the leaf; the driver row NEW | EXACT_SOURCE |
| P20 | **Coupling inverse (inline).** For each coupling step (backwards) over `n/2` samples, with `A = pcm[pair[0]]`, `B = pcm[pair[1]]`: `A>0,B>0 -> (A, A-B)`; `A>0,B<=0 -> (A+B, A)`; `A<=0,B>0 -> (A, A+B)`; `A<=0,B<=0 -> (A-B, A)`. Integer add/sub, no saturation. This exactly matches the built `InverseCoupling` (WwiseVorbisNative.cs:923). | `0x00AB6DC4..0x00AB6E78` (`0x00AB6e30 ldr r3,[r0]`; `0x00AB6e34 ldr r2,[r1]`; `0x00AB6e38 cmp r3,#0`; `0x00AB6e3c rsb r7,r2,r3`; `0x00AB6e48 add r7,r3,r2`; `0x00AB6e50 str r3,[r1]`; `0x00AB6e5c rsb r3,r2,r3`; `0x00AB6e64 str r3,[r0,#-4]`; `0x00AB6f24 str r3,[r1]`; `0x00AB6f28 add r2,r3,r2`; `0x00AB6f2c str r2,[r0]`) | M6-002 / C5 4f / built `InverseCoupling` | EXACT_SOURCE |
| P21 | **Floor curve multiply (floor1 inverse2), per channel.** For each channel: select the floor entry as in P17; call `0x00AB915C(dsp, floor_entry, D[ch], dsp+0x14[ch])`, which multiplies the residue by the floor curve and writes the result into the channel output buffer. | `0x00AB6E7C..0x00AB6EE0` (`0x00AB6e94 ldr r2,[r6]`; `0x00AB6ea8 ldr r1,[sl,#0x24]`; `0x00AB6ec0 ldr r2,[r8,#4]!`; `0x00AB6ed0 add r1,r1,ip,lsl #2`; `0x00AB6ed4 bl #0xab915c`) | C5 6a/6b (floor1 inverse2) for the arithmetic; the driver row NEW | EXACT_SOURCE |
| P22 | **Inverse MDCT, per channel.** For each channel, call `mdct_backward(n, dsp+0x14[ch])` where `n = setup[blockflag]` (the current block size). | `0x00AB6EEC..0x00AB6F10` (`0x00AB6eec ldr r6,[fp,#-0x2c]`; `0x00AB6ef8 mov r0,r6`; `0x00AB6efc ldr r1,[r3,r5,lsl #2]`; `0x00AB6f04 bl #0xab4e34`; `0x00AB6f08 ldr r3,[r4,#0xc]`; `0x00AB6f10 bgt #0xab6ef4`) | M6-002 C9 X5-I1 (call site named); the per-channel loop NEW | EXACT_SOURCE |
| P23 | **Inverse return.** Clear `dsp+0x30 = 0`; restore the stack and return. | `0x00AB6F14: mov r0,#0`; `0x00AB6F18: strb r0,[r4,#0x30]`; `0x00AB6F1C: sub sp,fp,#0x20`; `0x00AB6F20: pop {...pc}` | NEW | EXACT_SOURCE |
| P24 | **Window/overlap driver** (`0x00AB3520`, called from `0x00AB7E40`). `dsp+0x1c` = start skip, `dsp+0x20` = end; `available = dsp+0x20 - dsp+0x1c`; if `param_2 == 0` return `available`. Otherwise select two window pointers from `setup+0`/`setup+4` (`block_size/2` in {128,256,512,1024,2048}; otherwise NULL, which `0x00AB5A94` would dereference). Per channel call `0x00AB5A94(bs0, bs1, dsp+0x24, dsp+0x28, dsp+0x14[ch], dsp+0x18[ch], winA, winB, out + samples*ch*4, channels, skip, end)`, then `memcpy(dsp+0x18[ch], dsp+0x14[ch] + aligned(block_size), aligned(block_size))` (overlap save), then `dsp+0x1c += returned`; set `dsp+0x30 = 1`. | `0x00AB3520` body (`0x00AB3528 ldr lr,[r0,#0x1c]`; `0x00AB3530 ldr fp,[r0,#0x20]`; `0x00AB3564..0x00AB35E4` window selection; `0x00AB3664 bl #0xab5a94`; `0x00AB3698 bl #0x4d37f0`; `0x00AB36B4 strb r2,[r4,#0x30]`; `0x00AB36BC str r7,[r4,#0x1c]`) | M6-002 evidence `0x00AB3520`; C5 8b/9b (window-combine, planar layout) | EXACT_SOURCE for the driver; the window table bytes are C5 |
| P25 | **Window combine** (`0x00AB5A94`). Per channel: `a*wA + b*wB` when both arguments are non-zero, `a*wA - b*wB` for the second-window-only region, the 64-bit mirror (`vrev64.32`+`vswp`), and the single-window negate/copy forms. | `0x00AB5A94` body (C6 5; `0x00AB5A94..0x00AB624B`) | C5 8b / C6 5 | EXACT_SOURCE |
| P26 | **Output layout.** `0x00AB3520` writes the final float output planarly: channel `c` at `out + samples*c*4` (`param_2 + param_3*iVar10*4`); the last channel's index is forced to `channels-1`. The decoder's intermediate buffers `dsp+0x14` are also planar with the long-block stride (P9). | `0x00AB3520` (`0x00AB3618 ldr r2,[r4,#0x14]`; `0x00AB3620 ldr fp,[r2,r5,lsl #2]`; `0x00AB3624 mul ip,r3,ip`; `0x00AB363C add ip,r3,ip,lsl #2`; `0x00AB3644 str ip,[sp,#0x10]`); `0x00AB3780` P9 | M6-002 V8 / C5 9b | EXACT_SOURCE |
| P27 | **Stream reset** (`0x00AB3978`, called from `0x00AB7E40` when the last packet was flagged). Not read in this pass; it is on the end-of-stream path only. | call at `0x00AB7F14` | NEW | RECOVERABLE_GAP: read `0x00AB3978` (its callers are `0x00AB7E40`; the body is `0x00AB3978..0x00AB3D28`) |

## Decoder-state field offsets (dsp), as used by this path

| offset | meaning | citation |
|---|---|---|
| +0x00 | bit-reader data pointer | `0x00AB3800: str r2,[r4]` |
| +0x04 | bit-reader bit position | `0x00AB37F8: str r1,[r4,#4]`; `0x00AB62E8` |
| +0x08 | bit-reader remaining bytes | `0x00AB3804: str r3,[r4,#8]`; `0x00AB62E0` |
| +0x0c | channels | `0x00AB3790: ldr r6,[r0,#0xc]`; `0x00AB6B20` |
| +0x10 | setup struct pointer | `0x00AB3788`; `0x00AB6B2C` |
| +0x14 | per-channel output buffer pointers (planar) | `0x00AB37CC..0x00AB37E0`; `0x00AB6be8`; `0x00AB6ef4` |
| +0x18 | per-channel previous/overlap buffer pointers | `0x00AB384c..0x00AB3854`; `0x00AB3618/0x00AB3628` |
| +0x1c | current start skip (int) | `0x00AB3878`; `0x00AB3528` |
| +0x20 | current end / sample count (int) | `0x00AB38bc`; `0x00AB3530` |
| +0x24 | previous block flag | `0x00AB3818`; `0x00AB362c` |
| +0x28 | current block flag | `0x00AB382c`; `0x00AB3640` |
| +0x2c | start-skip (u16, from vorb header) | `0x00AB3890`; `0x00AB38e0` |
| +0x2e | end-trim (u16, from vorb header) | `0x00AB3900` |
| +0x30 | previous-window-saved flag (u8) | `0x00AB3814`; `0x00AB3874`; `0x00AB6F18`; `0x00AB36B4` |

## Setup struct field offsets (`dsp+0x10`), filled by `0x00AB6380`/`0x00AB63E0`

| offset | meaning | citation |
|---|---|---|
| +0x00 | bs0 = `1<<hdr+0x7C` | `0x00AB63A0/0x00AB63B0` |
| +0x04 | bs1 = `1<<hdr+0x7D` | `0x00AB63A4/0x00AB63AC` |
| +0x08 | mode count | `0x00AB66C0` |
| +0x0c | mapping count | `0x00AB6620` |
| +0x10 | floor count | `0x00AB64D0` |
| +0x14 | residue count | `0x00AB6588` |
| +0x18 | codebook count | `0x00AB6408` |
| +0x1c | mode array, 2 bytes each `{blockflag, mapping}` | `0x00AB66FC`, `0x00AB672C`, `0x00AB6744` |
| +0x20 | mapping array, 0x14 each | `0x00AB6634` |
| +0x24 | floor array, 0x24 each | `0x00AB6524` |
| +0x28 | residue array, 0x1c each | `0x00AB65C4` |
| +0x2c | codebook array, 0x3c each | `0x00AB6458` |

## Existing records contradicted by the source

- **None.** The M6-002/C5..C9 citations checked against the instructions agree. In particular the C5 claim
  that each mapping submap reads three `read(8)` with the first discarded and stores `{floor, residue}` is
  confirmed (`0x00AB6788` body; the packet driver indexes `mapping+8[mux*2]` for the floor byte and
  `mapping+8[mux*2+1]` for the residue byte, `0x00AB6bc0`/`0x00AB6d28`).
- The C9 correction that `0x0108E648` supplies the work pointer is confirmed at `0x00AB3794/0x00AB37A8`
  (`r3 = 0x0108E648`, `ldr r5,[r3,#8]` = `[0x0108E650]`).

## Existing records whose evidence is too weak to keep their status

- **M6-002's title ("Vorbis decoding is the runtime Tremor-lowmem fork: ... 1-bit mode ... planar float,
  skip/trim")** is a claim about the whole decoder. Its evidence cites the arithmetic leaves but no driver
  row, and the built code refuses at the entry point. The record is correctly IMPLEMENTATION_GAP, but its
  `evidence` should gain the driver rows above so a later comparison does not read the title as "the path is
  settled". This is the AGENTS.md "settled record owns its whole production path" rule applied to an
  IMPLEMENTATION_GAP: the packet driver is the unowned part and now has its own rows.

## Open questions for the manager

1. **Are the driver rows part of M6-002 or a new record?** The process says a settled record owns its whole
   path; M6-002 is not settled. The rows can be appended to M6-002's `evidence` (my recommendation) or
   promoted to M6-022 (the packet driver) with M6-002 pointing at it. The built `UnreadArithmetic` string and
   the location `WwiseVorbisNative.cs` should follow whichever is chosen.
2. **`dsp+0x30` gating intent.** The code writes 0 at the end of `0x00AB6B14`, 1 at the end of `0x00AB3520`,
   and copies the previous window when it is 0 at the start of `0x00AB3780`. The literal behaviour is read;
   the intent (why the inverse clears it) is not asserted. If B1 needs it, read `0x00AB3520`'s callers and
   the stream reset `0x00AB3978`.
3. **`0x00AB3978` (stream reset)** is on the end-of-stream path (`0x00AB7F14`) and was not read (P27). It is
   a RECOVERABLE_GAP.
4. **The window default** (`block_size/2` outside {128,256,512,1024,2048} -> NULL) is reachable only for a
   block size outside 256..4096. C6 5g leaves shipped reachability UNKNOWN; the packet driver reads the same
   setup, so this is unchanged.
5. **The 5th argument of `0x00AB73F8`** (`count`, pushed at `0x00AB6d38`) is the per-submap channel count.
   The built `ResidueInverse` signature should take it; the current C# has no packet driver, so this is an
   interface note for B1.

## Artifacts

- `.scratch/m6-vorbis-packet/packet_entry.txt` (0x00AB3780..0x00AB3978)
- `.scratch/m6-vorbis-packet/packet_driver.txt` (0x00AB6B00..0x00AB6F60)
- `.scratch/m6-vorbis-packet/setup.txt` (0x00AB6380..0x00AB6788)
- `.scratch/m6-vorbis-packet/framing.txt` (0x00AB7E40..0x00AB8014)
- `.scratch/m6-vorbis-packet/window.txt` (0x00AB3520..0x00AB3758)


