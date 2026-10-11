# Q14-028/029: observer wrapper interior-entry inputs

Checkpoint781; scope guard reread. Engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. Reproducible `20261010-sound-observer-interior-callers.py/.json` scans external immediate branches into9A03DC..9A0408 and9A040C..9A0448, rather than exact entry only. Primary `-interior-native.txt` and `-interior-boundaries-native.txt` reopen every resulting candidate locally.

Eighteen Thumb encodings, no ARM candidate. The sixteen instruction encodings are pieces of the actual ARM words below; the two remaining encodings are literal words loaded by9A0300/9A04CC. None establishes an observer caller or changes its argument mapping.

| Candidate | Primary containing word | Disposition |
| --- | --- | --- |
| 0x99fcca | 0099FCC8: mov          r0, #1 | Part of aligned ARM instruction |
| 0x99fcd6 | 0099FCD4: mov          r1, #2 | Part of aligned ARM instruction |
| 0x99feda | 0099FED8: add          r3, r3, #0x6c | Part of aligned ARM instruction |
| 0x99ff7e | 0099FF7C: subs         r4, r0, #0 | Part of aligned ARM instruction |
| 0x9a00aa | 009A00A8: strh         r7, [r0, #0xc] | Part of aligned ARM instruction |
| 0x9a00ae | 009A00AC: strh         r6, [r0, #0xe] | Part of aligned ARM instruction |
| 0x9a00ba | 009A00B8: ldrex        r3, [r2] | Part of aligned ARM instruction |
| 0x9a00e2 | 009A00E0: mov          r4, r1 | Part of aligned ARM instruction |
| 0x9a00ea | 009A00E8: mov          r5, r0 | Part of aligned ARM instruction |
| 0x9a00ee | 009A00EC: mov          r0, r4 | Part of aligned ARM instruction |
| 0x9a00fe | 009A00FC: mov          r4, r0 | Part of aligned ARM instruction |
| 0x9a013a | 009A0138: cmp          r4, r2 | Part of aligned ARM instruction |
| 0x9a030a | 009A0308: add          r4, pc, r4 | Part of aligned ARM instruction |
| 0x9a0350 | 009A0350: rsbeq        sp, lr, r8, asr r5 | PC literal; actual load at9A0300 |
| 0x9a0520 | 009A0520: rsbeq        sp, lr, ip, lsl #7 | PC literal; actual load at9A04CC |
| 0x9a08d2 | 009A08D0: ldr          r3, [pc, #0x58] ; word[009A0930]=0069F7C4 | Part of aligned ARM instruction |
| 0x9a08da | 009A08D8: ldr          r2, [pc, #0x54] ; word[009A0934]=0069F7C4 | Part of aligned ARM instruction |
| 0x9a08ea | 009A08E8: str          r4, [r2] | Part of aligned ARM instruction |

The predecessor returns9A03D4, followed by literal9A03D8, before distinct wrapper9A03DC. That wrapper either tails9C7B6C or returns2 at9A0404; its literal9A0408 precedes separate wrapper9A040C. The latter tails9C7D54 after restoring its own push frame, or returns2 through9A0444; literal9A0448 then next PUSH9A044C. No ordinary preceding fallthrough admits the wrapper. OR613 registry mutations, callback exposure/line flag propagation and MC667 meter construction remain paid. No new observer callback body is supplied.

Exactly open: admitted shipped wrapper/key/callback/raw thirdword and corresponding callback-body input, or complete bounded exclusion proof. Neither this finite immediate-branch check nor the earlier exact/raw/export/pointer search proves computed/copied/relative or external inputs absent.028/029 stay PARTIAL, AUDIBLE.107 native+8 scope CHECKED/25 PARTIAL;140 AUDIBLE/0 INTERNAL unchanged. Research only.

out of scope: registry backing/allocator/copy/free and per-sample meter/filter arithmetic; stop those descendants.
