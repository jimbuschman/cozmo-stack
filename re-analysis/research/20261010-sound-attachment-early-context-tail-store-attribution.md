# Q14-104/131: context retirement, locals and command defaults

Checkpoint720; guard reread before each step. Primary evidence: early-context-tail-native.txt, early-context-tail-inputs-native.txt and early-context-unlink-native.txt (20261010-sound-attachment- prefix). Engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1.

| Original candidate | Disposition and remaining input |
| --- | --- |
| 984334 | Actual984124 saves incomingr0 atSP14 and reads its34 list. The retire loop obtains next from current element0 intoSL before unlink. At984320..344 it reloads saved owner, publishes next into owner34 before backing element release. Owner input remains conditional, compatible with AW719's separate context list but not proved by field overlap alone. |
| 98438C | Actual984350 incomingr0→r6 reads its34 and publishes next at98438C before element4 release and backing element release. Alternate/admitted outer receiver remains UNKNOWN. |
| 98B398 / 98B39C | Actual98B250 explicitly setsFP=SP+20 at98B254; negative38/34 are stack locals. No parent or bus destination. Preserve surrounding decision calls; no frame-slot alias inference. |
| 98C914 | Actual98C8BC requests50 bytes and receives independent product at98C8D4; null returns. Original incomingr0 retainedr4 becomes product4 with conditional24 increment. Product38 defaults0; packed48 low3 clearing, byte40, timing8/C zeros and3C=4 remain exact ordered defaults. |
| 98C96C | Actual98C934 uses incomingr0 as receiver, incomingr1 as associated object4 with conditional24 increment, same38/packed48/40/8/C/3C defaults. The matching layout alone does not establish that all receiver inputs are98C8BC products; UNKNOWN admitted input, stop that lead. |
| 98CC34 | Actual98CC14 stores seventh argument (entry stack+8, currentSP+1C) into receiver38, sets48 bits1/2, stores eighth argument into3C and fifth/sixth into30/34 before ninth-argument gate. These raw authored timing/parameter decisions are exact; admitted setter receiver remains UNKNOWN, not unreachable. |

out of scope: backing allocation/free arithmetic; no descent. Original104/131 parent/bus aliases remain PARTIAL, counts107 native+8 scope CHECKED/25 PARTIAL,140 AUDIBLE/0 INTERNAL. Research only.
