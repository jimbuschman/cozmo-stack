# Q14-104/131: two remaining queue entry input boundaries

Checkpoint753; queue5 scope guard reread. Native engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. Primary `20261010-sound-remaining-queue-entry-native.txt`; navigation `20261010-sound-remaining-queue-entry-references.json` and CQP751 immediate-call scan.

| Exact entry | Current disposition |
| --- | --- |
|9ADC30 | Real ARM entry retains incomingR0 inR5 at9ADC4C on nonempty list path; matching/filter controls are incomingR1/R2.9AB048 occurs before branch-specific unlink/tail34/free-head38/count44 update. No positive immediate caller, aligned raw entry word, named symbol or symbol relocation was found in these scans. Actual receiver and supplied controls remain UNKNOWN. This does not prove absence of relative/computed/copied calls or an unreachable method. |
|9AFE08 | Real ARM entry retains incomingR0 inR5 at9AFE0C, matches suppliedR1 against record28, and performs action-type/query/completion controls before unlink/release. Those conditional bodies are already AW730; no positive immediate caller, aligned raw entry word, named symbol or symbol relocation was found here. Actual receiver and supplied control remain UNKNOWN, with relative/computed/copied inputs unproved. |

Stop both specific navigation leads at their named UNKNOWN inputs; do not manufacture fixed-global receiver identity from field offsets. out of scope: backing/free-list storage arithmetic and per-sample processing. Original104/131 remain PARTIAL; counts107 native+8 scope CHECKED/25 PARTIAL,140 AUDIBLE/0 INTERNAL unchanged. No production/fidelity changes.
