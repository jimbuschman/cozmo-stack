# Q14 rate/frame: live registers versus actual native arguments

Checkpoint813; guard reread before each step. Engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. Original six rate/frame all-writer/alias/input frontiers only;no new provider family.

The existing310-event navigation list includes14 rate/frame live-at-call register observations (21 total including other selected globals). A register retaining a pointer atBL is not automatically a passed or escaped pointer. This pass pays exactly the two direct-native-callee observationsA430A8 and9E6D48 using individual primary caller/callee instructions;it does not infer the remaining helper arguments.

| Existing observation | Primary argument/alias disposition |
| --- | --- |
|A430A8 liveR3=105243C|A43098 loads5FCFC0;A430A0+8+5FCFC0=1040068,rawGOT105243C. A430A4 dereferences the rate word intoR1;R0 is the existing voiceR7. EntireA548B8..BC isSTRR1,[R0,EC];BXLR. R3 is neither read,stored nor forwarded. The actual scalar copy tovoiceEC is exact;this particular liveR3 observation supplies no rate-pointer escape. This scalar route was already established in the older bridge/rate rows;it is rechecked,not counted as a new writer or invented engine-pointer field.|
|9E6D48 liveR3=1052440|9E6D2C loads659318;9E6D38+8+659318=1040058,rawGOT1052440.9E6D44 dereferences unsigned16 frame count intoR1,whileR0 comes frommanager10. At9E2BD0 PUSH excludesR3;9E2BD4 savesR1 toFP;VPUSH andLDRSL atD8/DC do not readR3.9E2BE0 unconditionally definesR3 from its own PC literal before any incomingR3 read. Therefore this particular frame-pointer observation is not a pointer argument/escape;the exact scalar frame input remains the existing modulator contract. Stop at the definition;no evaluator/sample arithmetic descent.|

-live-register-frontier-native.txt independently captures the full two-instruction setter,only the modulator entry before incomingR3 is killed,and both raw GOT/literal equations. -live-register-witness.json stores instruction bytes and register read/write checks:both incomingR3 checks and both equations pass. No whole modulator algorithm,global immutability,alternate alias or all-input absence proof is claimed.

Remaining exactly:other actual/original writer and alias frontiers,admitted startup/provider values,and any positively bound helper argument still unpaid by existing rows. These two scalar handoffs do not complete the original global proofs.043/092/099/120/124/130 remain PARTIAL/AUDIBLE;14 positive scalar-writer identities and coverage116 CHECKED /24 PARTIAL /0 NOT DONE stay unchanged.140 AUDIBLE /0 INTERNAL. Research only;no production,fidelity,inventory,hardware or Q15 changes. out of scope: per-sample evaluator arithmetic,allocation/registry backing and storage/I-O.
