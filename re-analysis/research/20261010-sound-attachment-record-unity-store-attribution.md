# Q14-104/131: six record-buffer unity stores near bus code

Checkpoint679; decisions only; guard reread before each step. Primary `20261010-sound-attachment-bus-core-store-candidates-native.txt` and `20261010-sound-attachment-bus-core-store-inputs-native.txt`; engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. Proximity to bus code is navigation only; actual destination comes from instructions.

| Candidates | Concrete record destination / raw publication |
| --- | --- |
| 9C01AC/9C01B0 | SB is the selected array descriptor. Read oldcount4 at9C0230; spare capacity routes9C0164, loads base0, increments descriptor4, then computes base+76*oldcount intor5 at9C0168..178. Raw3F800000 supplied by9C0194 is written to selected record34/38. This is a physical record slot, not the incoming owner/node. |
| 9C02D4/9C02DC | Growth path computes76*(oldcapacity+1) at9C0240..260, allocates separately9C026C, saves returnedpointer at9C0270/274 and takes it intor4 at9C0294. Raw3F800000 loaded9C029C is written to new record34/38 before later copying old contents. Not audio-node parent/output-bus publication. |
| 9C05F4/9C05F8 | Second growth allocation size76*requestedcount at9C0578/580/588, returnedpointer keptinr0 at9C0598..A0 then copied tor5 at9C05D0. Raw3F800000 supplied9C05C4 is written to new record34/38, together with other explicit record defaults. Separate buffer allocation/record geometry, not node identity inferred from a field number. |

These are exact raw scalar initialization stores. Do not replace unity with zero, call them per-sample DSP, infer a bus class from the address neighborhood or claim all record consumers harmless. The allocator/copy machinery is out of scope; no descent. Original104/131 remain PARTIAL for actual node/bus writer aliases and admitted destinations9FBB30/9F319C. Counts107 native+8 scope CHECKED/25 PARTIAL;140 AUDIBLE/0 INTERNAL. No production/fidelity/inventory/hardware/Q15 edits.
