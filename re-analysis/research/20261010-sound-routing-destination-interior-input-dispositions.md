# Q14-104/131: selected routing destination interior-entry navigation

Checkpoint755; queue5 guard reread. Primary engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1; `20261010-sound-routing-destination-entry-boundaries-native.txt`, `-actual-entries-native.txt`; reproducible `-interior-callers.py/.json`.

This checks only the two already named unknown destinations9D2410 and9F319C, including immediate branches into their body interiors rather than only their exact start. Native9D23F8 ends in a tail9F5274 at9D240C;9D2410 starts with its own push and saves second argumentR1 asR4. Native9F3170 ends with popPC at9F3198;9F319C separately saves constructor destinationR0 asR4. No ordinary fallthrough input from those preceding functions is established.

The interior-range scan yields67 Thumb encoding candidates and no ARM candidate from outside the selected ranges. These are navigation encodings, not67 genuine alternate-entry callers. For concrete false examples,9D23EC is in the preceding function's literal area after popPC9D23E0;9F3164 is in the literal area after the prior branch9F315C;9F318C is actual ARM STR zero,[R0+8] before popPC9F3198. Their Thumb interpretations supply no receiver. This report does not certify every candidate's instruction boundary or prove computed/relative/alternate-mode absence.

Actual9D2410 second-argument member destination and9F319C constructor destination remain UNKNOWN. Local conditional decision contracts stay paid. Stop this specific interior-entry lead without substituting a neighboring class or inventing input admission. Original104/131 PARTIAL;107 native+8 scope CHECKED/25 PARTIAL;140 AUDIBLE/0 INTERNAL unchanged.

out of scope: cleanup/backing allocation, raw storage copying and per-sample arithmetic. No descendant extraction, production/fidelity changes or new family census.
