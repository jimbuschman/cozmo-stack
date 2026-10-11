# Q14-104/131: conditional bank callback fields and excluded hash stores

Checkpoint735; guard reread before each step. Primary evidence: bank-final-fields-native.txt and bank-final-inputs-native.txt (20261010-sound-attachment- prefix). Engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1.

| Original candidates | Disposition and retained boundary |
| --- | --- |
| 9B9ED4 / 9B9EDC | Actual9B9DC8 secondargumentr1→SL; it publishes savedSB into38 and a GOT-derived raw pointer into34, sets receiver4 bit0 before queue notification9A96E4/9AF778. Earlier retained-buffer3C/40/44 cleanup is separate. Admitted receiver and raw pointer's type remain UNKNOWN; do not label parent/bus from offsets or declare absence. |
| 9BA6F8 / 9BA704 | Actual9BA624 secondargumentr1→SB, rawinputs retainedR8/R7 publish34/38, then receiver4 bit0 and queue notification. Exact value/publication order retained. Admitted receiver/inputs remain UNKNOWN; no fabricated routing identity. |
| 9BAC30 / 9BAC34 / 9BACB4 / 9BACD0 / 9BB1D0 / 9BB1E4 / 9BB1E8 | out of scope: hash/rehash backing-array reset/replacement/failure restoration34/38/3C; stop without tracing infrastructure. FP here is positive-offset object recipient, not assumed stack frame. |

This does not exclude the nearby callback/notification decisions. Counts107 native+8 scope CHECKED/25 PARTIAL,140 AUDIBLE/0 INTERNAL unchanged. Research only.
