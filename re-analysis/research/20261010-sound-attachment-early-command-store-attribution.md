# Q14-104/131: early context and independent command fields

Checkpoint718; guard reread before each step. Primary evidence: `20261010-sound-attachment-early-command-fields-native.txt`, `20261010-sound-attachment-early-command-products-native.txt`; engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1.

| Original store | Destination, exact boundary and open input |
| --- | --- |
| 98213C | Entry98211C retains incomingr0 inr4, calls985A24, zeros38 and40/44, publishes table1039838 (982138+8+6B76F0+8). 997F28 calls this same input then publishes103A5D8 and clears its separate4C/50/54 list. Incoming outer constructor receiver remains conditional; table/default similarity alone is not a node receiver proof. |
| 98221C | Entry9821EC retains incomingr0 inr4, calls985AA0, tests returned receiver8, then copies that object's38 into receiver38 and returns1. Null receiver8 follows separate fallback. Admitted receiver and callback result are UNKNOWN; stop this input lead rather than declaring unreachable. |
| 9822E4 | Exact setter receiver38=incomingr1. Alternate/admitted receiver remains UNKNOWN; no unrelated-field proof from offset alone. |
| 9823C4 / 98240C | Positive998188 allocates independent40-byte command at9981A8, retains it inr4, invokes982370 before linking into outer receiver50/4C. Entry982370 records third/fourth args in8/C, fifth arg in14. Fifth nonnull increments its24, initializes34=4 and state bytes/words, publishes command into that object's48, obtains18 through14→44→8. Fifth null also sets34=4 with different argument-derived clearing. Both original34 stores belong to this separate command on the concrete factory path; null allocation does not fabricate a command. Alternate direct982370 input remains conditional. |
| 982B00 | Positive999BF8 saves998188 result inSB at999C80; after success-dependent dispatch and timing getters, supplies that same command to982A90 (native999CFC..999D08). Entry982A90 publishes30 first, compares command1C with fourth input; when1C is later, signed timing correction uses F64 multiply/divide with signed +/-0.5 before truncation and addition to30. It then publishes incomingr2 into command34, third/fourth-input-dependent2C, and packed3C bit1. Timing arithmetic here remains exact decision arithmetic, not ADP-1 per-sample DSP. Other setter inputs remain conditional. |

This attributes six original raw candidates and does not exhaust alternate register-offset/block-store aliases. Counts107 native+8 scope CHECKED/25 PARTIAL,140 AUDIBLE/0 INTERNAL unchanged. Research only; no production/fidelity edits.
