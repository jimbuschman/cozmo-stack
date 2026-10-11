# Q14: flag-setting address arithmetic navigation

Checkpoint791; queue5 scope guard reread before each step. Engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. Decisions only; this checks a named limitation in the rate/frame writer census, not an extra function family.

`20261010-sound-rate-frame-flag-arithmetic-expansion.py` extends785 independently: explicit ADDS/SUBS/MOVS spellings are normalized only when the decoded instruction ID is ADD/SUB/MOV. Raw ARM rotated immediates retain785's decoder. Predicate splitting happens before normalization; `ins.update_flags` still resets flags to all possibilities afterward. No successful imported return, missing pointer value or precise S-bit condition is invented.

Primary `-flag-arithmetic-witness-native.txt` and `-witness.json` reopen four existing source instructions: ARM9FEC8C SUBS R5,R0,0; A573E8 SUBS R1,R0,0; Thumb8D6C22 ADDS R1,R4,4; 8D81C6 MOVS R0,0. Their actual byte encodings, base instruction IDs and updateFlags=true are recorded. The ARM zero-subtracts preserve the input bit pattern while setting flags; the selected Thumb forms respectively offset the input and establish zero. These are navigation witnesses, not new rate writers. Allocation/phone-runtime callees stop at their existing interfaces.

The same6855 candidate windows produce the same300 events, with zero added or removed address/mode/kind/effective-address keys versus785. The local-state-cap count increases from338 to434 because preserving more values explores more possible paths. This is an explicit limitation: matching event sets do not make capped paths exhaustive. `-comparison.json` records the counts and exact empty differences.

Q14-043/092/099/120/124/130 remain PARTIAL for the original unrestricted writer/alias and actual zero/provider input proofs. Selected flag-setting address arithmetic is now modelled conservatively; unknown loops, shifted/indexed constructions, stack/copy/foreign aliases, unmodelled flag operations, window bounds and the434 caps remain unpaid as complete proofs. Stop this finite negative lead; do not call it writer absence or unreachability.

out of scope: backing/I-O and per-sample DSP. Coverage remains108 native+8 scope CHECKED/24 PARTIAL/0 NOT DONE;140 AUDIBLE/0 INTERNAL. Research only; no production, fidelity, inventory, hardware or Q15 changes.
