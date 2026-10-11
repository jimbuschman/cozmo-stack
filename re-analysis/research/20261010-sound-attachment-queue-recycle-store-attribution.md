# Q14-104/131: six more conditional queue retirement/cleanup receivers

Checkpoint728; guard reread before each step. Primary evidence: queue-recycle-fields-native.txt, queue-recycle-inputs-native.txt, queue-recycle-corrected-entry-native.txt and AW727 queue-retirement-fields-native.txt (20261010-sound-attachment- prefix). Engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1.

| Original candidates | Actual receiver input and retained order |
| --- | --- |
| 9AC4F4 / 9AC500 | Actual9AC3AC incomingr0→R5 at9AC3B8. Distinct9AB048 call precedes unlink; active head30/predecessor0, conditional tail34, removed element0→old free head38, free head38→element, decrement44. Source hierarchy lookupA6168C and parent34/bus38 reads are filters, not these writes' destination proof. |
| 9AC7A0 / 9AC7AC | Actual9AC6DC incomingr0→SB; distinct9AB048-before-unlink contract with same explicit retirement ordering. |
| 9AC954 / 9AC960 | Actual9AC7BC incomingr0→SL;9AB048-before-unlink with same explicit ordering. |
| 9ACDD0 / 9ACDDC | Actual9ACCD8 incomingr0→R5; completionA04F54 precedes unlink, tail/free-head/count44 publication before virtual release. 9ACB08 navigation prefix is a different function and does not establish this receiver. |
| 9AD0F4 / 9AD10C / 9AD184 | Actual9AD064 incomingr0→R4 (AW725). Cleanup walks active30, conditional last-tail34→0, links each element into free-head38, decrements localcount44 before loop-final44/30 publication. Later cleanup clears3C/40/30/34 after backing release. Keep distinction from completion-bearing retirement functions; no fabricated completion callback. |
| 9AD6A4 / 9AD6B0 | Actual9AD4B0 incomingr0→SL; completionA04F54 precedes unlink, tail/free-head/count44 publication before associated virtual release. |

GQT750 now positively binds9AD064 to the fixed108D870 Term input; its raw recycle loop follows the earlier completion-bearing helper gates. The other five admitted outer receivers and all alternate aliases beyond the named known products remain conditional/UNKNOWN. Similar queue offsets do not prove same product globally. Stop input leads without declaring unreachable. out of scope: backing release arithmetic; no descent. Counts107 native+8 scope CHECKED/25 PARTIAL,140 AUDIBLE/0 INTERNAL unchanged. Research only.

CQP751 now binds known fixed108D870 calls for the other five methods9AC3AC/9AC6DC/9AC7BC/9ACCD8/9AD4B0. Their alternate caller/global-alias/control inputs remain conditional; the older blanket UNKNOWN is narrowed to those explicitly remaining inputs.
