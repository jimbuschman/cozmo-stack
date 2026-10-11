# Q14 rate/frame: fair local-worklist navigation and ten new reads

Checkpoint803; scope guard reread before each step. Engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. Coverage116 CHECKED /24 PARTIAL;140 AUDIBLE /0 INTERNAL.

`20261010-sound-rate-frame-fair-worklist-navigation.py/.txt` changes797's worklist from last-in-first-out to first-in-first-out, preserving the same selected instruction/value/flag model,768-byte window,6855 seeds and4096-state cap. An unresolved loop can consume a finite LIFO search before already-queued exits are visited. Breadth-first scheduling visits states at each edge depth before deeper repeats, improving fairness without proving convergence or loop admission. Both runs still cap434 windows; no exhaustive writer, input or alias claim.

`-fair-worklist-comparison.json` compares exact(address,mode,kind,effective) keys:300 baseline events,310 new,ten additions and zero missing. New events are five GOT-pointer/read pairs, all ARM, not additional writes or argument escapes. Primary `-fair-worklist-new-read-native.txt` independently reopens the literal loads, PC additions, exact memory instructions and GOT words. Every path forms GOT base104028C by ARM PC+8; slot1040068 holds105243C (rate),slot1040058 holds1052440 (frame).

| New pair | Exact primary address/value join | Scoped disposition |
| --- | --- | --- |
|987A1C/987A28|9877F8 loads6B8A7C;987808+8+literal=104028C;987A00 loadsFFFFFDDC;GOT1040068->105243C;LDR actual rateword|Rate scalar goes throughVMOV/VCVT into parameter/time conversions; no per-sample body is followed. No rate writer.|
|9D5B54/9D5B6C|9D5974 loads66A904;9D5980+8+literal=104028C;9D5B4C loadsFFFFFDDC;GOT1040068->105243C;LDR actual rateword|9D5B64 ALSO saves global pointer into localSP18, before9D5B6C dereference and raw scalarSP1C store9D5B74. This positive stack alias needs its own lifetime/use check; it is not silently discarded as a scalar. No new direct writer.|
|A420C0/A420C8|A41E64 loads5FE40C;A41E78+8+literal=104028C;A420B8 loadsFFFFFDCC;GOT1040058->1052440;LDRH actual frameU16|Frame feeds signed negation/F32 conversion and expf parameter coefficient stored toobject1DC atA420E4; this scalar-parameter setup is not per-sample DSP execution. No frame writer.|
|A615C0/A615C8|A613F8 loads5DEE84;A61400+8+literal=104028C;A615B0 loadsFFFFFDDC;GOT1040068->105243C;LDR actual rateword|Signed input sign-extension and rate multiplication form64-bit numerator,division by1000 interface,then scalar publicationA615D8. Arithmetic controls authored parameter/time values; backing storage and imported division body are not descended into.|
|A61638/A61640|SameGOT base;A61628 loadsFFFFFDDC;GOT1040068->105243C;LDR actual rateword|Rate retained inR5 for two signed64 numerator/1000 conversions, with scalar writes to authored value records. No global pointer publication/writer through this pair.|

The nearest Ghidra index entry9D5700 ends at9D5918's POP and is NOT the owner of9D5974. Literal words9D5920..30 follow; primary separateLDR/PUSH at9D5934/38 establishes the new control body. No stale index label is used as owner evidence. The initially inspected unrelated GOT words1040050/1040040 are not substituted for the corrected actual58/68 slots.

Still open:original043/092/099/120/124/130 live writer/relative/indirect/stack/copy alias proof, admitted zero-divisor inputs and provider/startup input boundaries. Existing fourteen direct scalar writers remain positive; the ten newly visible reads do not complete those proofs. Next bounded positive step:follow9D5B64's exactSP18 pointer lifetime/uses. These obligations remain PARTIAL; Q15 stays behind Q14.

out of scope: allocator/free/copy implementations,per-sample DSP arithmetic and excluded payload bodies; only control-address/value interfaces were reopened and no new callee was followed. Research only; no production/fidelity/inventory/hardware changes.
