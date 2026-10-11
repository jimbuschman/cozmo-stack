# Q14-104/131: five conditional queue retirement owners

Checkpoint727; guard reread before each step. Primary evidence: queue-retirement-fields-native.txt and queue-retirement-inputs-native.txt (20261010-sound-attachment- prefix). Engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1.

| Original candidates | Local receiver proof |
| --- | --- |
| 9AB410 / 9AB41C | Actual9AB1F4 incomingr0→SB at9AB228 after mode(r3-2)<=1 gate. Tail34/free-head38 update same owner. |
| 9AB72C / 9AB738 | Actual9AB498 incomingr0→R8 at9AB4AC; tail34/free-head38 update same owner. |
| 9ABB80 / 9ABB8C | Actual9AB8AC incomingr0→R6 at9AB8B8; tail34/free-head38 update same owner. |
| 9AC100 / 9AC10C | Actual9ABEA4 incomingr0→R8 thenR7 at9ABEDC for relevant nonempty branch; tail34/free-head38 update same owner. |
| 9AC26C / 9AC278 | Actual9AC1D8 incomingr0→R7; tail34/free-head38 update same owner after9AB048 call. |

First four positive local retirement contracts callA04F54 with payload28 before unlink. Each then takes next from element0, updates active head30 or predecessor0, conditionally updates tail34, links priorfree-head38 into removed element0, publishes removed element38, decrements44 before associated-object virtual release. Fifth retains its distinct9AB048-before-unlink contract; do not claim identical completion call from layout alone. Admitted outer receiver identity for all five beyond these local inputs remains UNKNOWN; alternate aliases are not exhausted. Stop each input lead without declaring it harmless/unreachable. Candidates9AC4F4/500 captured for navigation are not attributed here.

out of scope: backing release arithmetic; no descent. Counts107 native+8 scope CHECKED/25 PARTIAL,140 AUDIBLE/0 INTERNAL unchanged. Research only.
