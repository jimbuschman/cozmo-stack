# Q14-033: voice-region floating/vector store widths

Checkpoint769; scope guard reread. Primary `20261010-sound-voice-policy-vector-store-native.txt`, `-vector-base-native.txt`, `-vector-entry-native.txt`; aligned ARM navigation `-vector-store-navigation.json` bounds the sameA53000..A56600 region as VB758/VM759. Same engine hash. This is a field-overlap check, not DSP-body extraction or an all-alias proof.

All nine candidate instructions reopen as actual scalar VSTR S-register stores, each exactly four bytes. No VSTM/VST1/2/3/4 encoding candidate occurs in this bounded region. The original VW650/VN749 stores, VB758's six STM/STRD destinations and VM759's four memory destinations stay paid; no larger inferred write width is assigned to these nine scalar sites.

| Actual sites | Destination / retained identity |
| --- | --- |
|A551D8/A551EC |A551AC R3=R4+520; writes R4+524 andR4+520. |
|A553F0/A55408 |Direct R4+354 andR4+350. |
|A55428/A5543C |A55138 R3=R4+510, taken unequal branchA55148 ->A55410; writesR4+514 andR4+510. No intervening call/destination redefinition. |
|A5545C/A55474 |Direct R4+344 andR4+340. |
|A55E80 |Actual separate entryA55E6C reserves8bytes and writes exactlySP4..7; reloads signedbyte there, releases stack thenBXLR. Not a voice field publication. |

For the first eight sites, actualA54F1C preserves entryR0 atA54FF4 inR4 before virtualcallA5500C. These are fields of that retained receiver, not a pointer loaded from arbitrary descriptor/backing. Offsets340/344/350/354/510/514/520/524 do not overlap that receiver'sE0/E4 decision words. They update existing VF parameter target/current fields; their value/gate contracts remain those recovered rows, without sample arithmetic descent or reclassification. Do not infer that this receiver can never alias another foreign object address or that no outside-region wrapper/copy can writeE0/E4.

This pays these bounded scalar-store destinations and widths.033 retains original computed/indirect caller and additional writer-alias proof, PARTIAL;107 native+8 scope CHECKED/25 PARTIAL;140 AUDIBLE/0 INTERNAL unchanged. Research only.

out of scope: per-sample DSP arithmetic and backing storage; retain exact parameter-field publication identity/width, not an equivalent control-value computation.
