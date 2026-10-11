# Q14 rate/frame: concrete9D5B64 stack alias and three further live reads

Checkpoint804; scope guard reread before each step. Engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. Coverage116 CHECKED /24 PARTIAL;140 AUDIBLE /0 INTERNAL. Follows803's positive rate-pointerSP18 save only, not an arbitrary recipient-family audit.

Primary `20261010-sound-rate-stack-alias-owner-boundaries-native.txt` and `-local-native.txt`; reproducible `-local-frontiers.py/.json` and `-control-path-check.json`. The local control body starts9D5934 LDR,9D5938 PUSH and9D595C SUB SP,SP,54. The861-node decoded graph follows conservative conditional/direct edges from that entry, retains six A11590 parameter-value queries only at their existing interface, and stops at actual returns9D6598/9D668C after ADD SP,SP,54. Out-of-line arms through9D66E8 branch back inside, before NOP/literals. No per-sample processing or new callee is descended into.

| Exact alias step | Primary contract |
| --- | --- |
|Publication9D5B64|Save rate-global address105243C from the803-confirmed GOT load into localSP18.9D5B6C dereferences originalR0 and overwrites it with the scalar;9D5B74 stores that scalar in separateSP1C.|
|First reload9D5C4C/50|After parameter queryA11590 at9D5C48, reloadR3 fromSP18, immediately LDRR3,[R3], then scalarSP1C store9D5C54. The pointer is consumed by the read and replaced by its live value, rather than passed onward as a rate-pointer argument.|
|Second reload9D5E50/58|After parameter queryA11590 at9D5E4C, reloadR3 fromSP18, read object18 separately, then LDRR3,[R3] and scalarSP1C store9D5E5C. This is a second live rate read; do not reuse the value from the previous query phase.|
|Third reload9D6180/8C|ReloadR2 fromSP18, read scalar object28/1C, then LDRR2,[R2].VMOV/VCVT consumes the newly read rate scalar for control-value conversion. No pointer write or pointer argument transfer through this reload.|
|Stack-address query boundary|The six queries at9D5A3C/5C48/5E4C/6074/6290/6478 passR3=SP38, with selectors8/A/D/C/B/9 respectively. They pass the existing scope structure, not SP18 or the rate-global pointer. Their existing parameter-query contract remains the interface; independent global mutation through other inputs is not excluded.|
|Local initialization/control check|Reconstruct all decoded edges rather than the single stored predecessor tree. Removing9D5B64 disconnects entry from all three SP18 reloads, so its initialization dominates each within this graph. No SP-indexed memory instruction or successor outside the decoded graph occurs. Stack adjustments are the one entry allocation and two return restorations. Local slot18 has one direct publication and these three explicit reads.|

This pays the named local alias/read frontier, without claiming arbitrary caller-SP,unknown memory aliases,global mutation or all-writer absence. The three additional PRIMARY rate dereferences9D5C50/9D5E58/9D618C are beyond the generic scanner's310 events because that scanner does not propagate this stack slot. The saved pointer survives queries, while the scalar rate is re-read separately afterward. No new direct global writer is established; fourteen already known scalar writers remain positive.

043/092/099/120/124/130 remain PARTIAL for their original broader writer/alias,zero-divisor/provider and startup-input obligations. No fixed rate/frame policy or immutable-rate assumption is introduced. Q15 stays behind Q14.

out of scope: backing allocation/copy/free and per-sample DSP; parameter/time conversion address interfaces remain exact. Research only; no production,fidelity,inventory or hardware changes.
