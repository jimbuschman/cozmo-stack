# B-M6b-1 — M6-002 Vorbis stream reset `0x00AB3978` (read-only extraction)

Read-only extraction from `resources/lib/armeabi-v7a/libcozmoEngine.so` (ARM mode, Wwise 2016.2 statically linked). All addresses are ELF VAs. Ghidra was used only to navigate; every citation was read from the instructions. Nothing in the repo was modified.

## Premise corrections

1. **The cited body range is wrong.** `0x00AB3978` is 92 bytes: `0x00AB3978..0x00AB39D3`, ending `pop {r3,r4,r5,pc}` at `0x00AB39D0`. `re-analysis/decomp/libcozmoEngine/index.tsv:50695` records `00ab3978  92  ... FUN_00ab3978`; the next function starts at `0x00AB39D8` (4 bytes of padding at `0x00AB39D4`). The cited `..0x00AB3D28` also contains the *next* function (an MDCT/butterfly body from `0x00AB39D8`), which is not part of the reset.
2. **The only caller is `0x00AB7E40`**, and the only callee is `memcpy` (PLT stub `0x004D37F0`).
3. **It is not a generic decoder wipe.** It performs exactly the same per-channel overlap-save that the window driver `0x00AB3520` performs at `0x00AB3670..0x00AB36B4`, then sets `dsp+0x30 = 1`. It never touches `dsp+0x14` (the internal work buffers) or the caller's PCM pointer.

## Rows

The decoder state `dsp` is `r4`. The function is a single loop over channels, then a flag write.

| # | what the original does | citation | classification |
|---|---|---|---|
| R1 | prologue: `r4 = dsp`, channel index `r5 = 0` | `0x00AB3978: push {r3,r4,r5,lr}`; `0x00AB397C: mov r4,r0`; `0x00AB3980: mov r5,#0` | EXACT_SOURCE |
| R2 | `n = setup[dsp+0x28]` where `setup = dsp+0x10`; confirms `dsp+0x10` = setup pointer, `dsp+0x28` = **current** block flag, `setup+0x00`/`+0x04` = bs0/bs1 | `0x00AB3984: ldr r1,[r4,#0x28]`; `0x00AB3988: ldr r2,[r4,#0x10]`; `0x00AB3994: ldr r2,[r2,r1,lsl #2]` | EXACT_SOURCE |
| R3 | `aligned = (n < 0 ? n+3 : n) & ~3` (multiple of 4); for a real power-of-two block size `aligned == n` | `0x00AB399C: cmp r2,#0`; `0x00AB39A0: add r3,r2,#3`; `0x00AB39AC: movlt r2,r3`; `0x00AB39B0: bic r2,r2,#3` | EXACT_SOURCE |
| R4 | `src = output[ch] + aligned`, where `output = *(dsp+0x14)` is the per-channel output-pointer array | `0x00AB398C: ldr r3,[r4,#0x14]`; `0x00AB3998: ldr r1,[r3,r5,lsl #2]`; `0x00AB39B4: add r1,r1,r2` | EXACT_SOURCE |
| R5 | `dst = overlap[ch]`, where `overlap = *(dsp+0x18)` | `0x00AB3990: ldr r0,[r4,#0x18]`; `0x00AB39A4: ldr r0,[r0,r5,lsl #2]` | EXACT_SOURCE |
| R6 | `memcpy(dst, src, aligned)` — copies the tail `aligned` bytes of each channel's internal buffer into its overlap buffer | `0x00AB39B8: bl #0x4d37f0` (`memcpy`) | EXACT_SOURCE |
| R7 | `ch++`; repeat while `ch < dsp+0x0c` (channels) | `0x00AB39A8: add r5,r5,#1`; `0x00AB39BC: ldr r3,[r4,#0xc]`; `0x00AB39C0: cmp r5,r3`; `0x00AB39C4: blt #0xab3984` | EXACT_SOURCE |
| R8 | `dsp+0x30 = 1` (previous-window-saved flag) | `0x00AB39C8: mov r3,#1`; `0x00AB39CC: strb r3,[r4,#0x30]` | EXACT_SOURCE |
| R9 | return | `0x00AB39D0: pop {r3,r4,r5,pc}` | EXACT_SOURCE |

## What it does to a single complete, non-looping decode

**It does not change the already-produced PCM.** It reads only `dsp+0x0c/0x10/0x14/0x18/0x28` and writes only `dsp+0x18[c]` (through `memcpy`) and `dsp+0x30`. It never writes `dsp+0x14[c]` and never writes the PCM pointer the framing passes out.

It overwrites each channel's overlap buffer with the last `aligned(n)` bytes of its internal buffer and marks the overlap valid. This is byte-for-byte the same overlap-save the window driver does at `0x00AB3670..0x00AB36B4`, except it does not advance `dsp+0x1c`. Its effect is to (re)prime the previous-window/overlap state for a later decode that uses this `dsp`. Whether a shipped `.wem` path performs a subsequent decode that consumes the primed overlap (loop/seek/replay) is a caller question this function cannot settle (UNKNOWN).

## The caller's gate

The reset is reached from the framing loop-termination path (`0x00AB7EF4`), not from an eofflag test:

- `0x00AB7EF4: ldr r2,[r4,#0x24]` → `r2 = dsp+0x14`
- `0x00AB7F04: ldr r3,[r2]` → `r3 = output[0]`
- `0x00AB7F08: cmp r3,#0`; `0x00AB7F0C: beq #0xab7f18` → skip the reset when `output[0] == 0`
- `0x00AB7F10: add r0,r4,#0x10`; `0x00AB7F14: bl #0xab3978`

So the gate is `dsp+0x14[0] != 0` (decoder set up at least once). The eofflag is separate: `eofflag = (packet_end_offset == total_length) && (framing+0x54 & 1)` (`0x00AB7E98..0x00AB7EBC`); when set, `framing+8 = 4` (`0x00AB7EB8`). When the flagged packet yields samples the framing returns on the output path `0x00AB7F34`; the reset then runs on the next invocation (which breaks at `0x00AB7E90`).

## Records contradicted / too weak

- **P27's body range** (`0x00AB3978..0x00AB3D28`) is wrong; correct to `0x00AB3978..0x00AB39D3`.
- **P2's phrase "if the last packet was flagged (`param_1[9] != 0`) call 0x00AB3978"** is imprecise: the gate is `dsp+0x14[0] != 0`, and the eofflag lives in the descriptor / `framing+8`, not `param_1[9]`.
- The first-window copy in `0x00AB3780` indexes `setup[old block flag]`; the reset indexes `setup[current block flag]` (`dsp+0x28`).

## Offsets confirmed

`+0x0c` channels, `+0x10` setup pointer, `+0x14` per-channel output-pointer array (pointer-to-array), `+0x18` overlap-pointer array, `+0x28` current block flag, `+0x30` previous-window flag — all match the gap-2 table.