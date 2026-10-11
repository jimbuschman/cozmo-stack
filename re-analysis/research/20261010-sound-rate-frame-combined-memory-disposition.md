# Q14: combined address arithmetic and memory-width navigation

Checkpoint797; queue5 scope guard reread before each step. Engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. Decisions only; this addresses a concrete composition limitation in the still-open settings writer/alias proof.

`20261010-sound-rate-frame-combined-memory-navigation.py/.txt` combines791's selected flag-setting operations/785 ARM rotated immediates with784's scalar write-span overlap in one scan. Running the corrections independently did not examine a path needing both. It also corrects literal/GOT load propagation: LDRB/LDRSB consume1 byte, LDRH/LDRSH2, LDR/selected VLDR S4; signed loads sign-extend then retain the32-bit bit pattern. Unsupported doubleword/other transfer forms do not acquire a guessed low-word pointer value. Writeback/stack/copy/shifted aliases remain limited, not synthesized.

`-memory-width-witness-native.txt` and `-witness.json` reopen actual source encodings:9CE40C LDRB R6,[FP],1;9CE418 LDRH R5,[FP];A085B4 LDRSH R8,[R4,6];A55E84 LDRSB R0,[SP,4]. Their byte widths, signedness and actual writeback are preserved. No input memory word is supplied by these encoding witnesses. The9EC5CC capture is SXTH register normalization, not a signed memory load; it is not conflated with LDRSH. Existing parameter/MIDI/lifecycle contracts remain separate from address tracking. No DSP-kernel callee is reopened.

The combined6855 windows produce300 events and434 local-state caps. `-combined-memory-comparison.json` compares address/mode/kind/effective-address keys independently with791 and784:zero additions and zero removals against either. The434 capped paths and768-byte bounds remain visible; the matching sets do not prove exhaustive pointer construction, successful callback admission, or writer absence.

043/092/099/120/124/130 remain PARTIAL for their explicitly requested unrestricted writer/alias and actual zero/provider inputs. The finite combined-decoder lead is paid; exclusive doubleword stores, unsupported transfers/flags, unknown loops, shifted bases, stack/copy/foreign pointers, runtime mutation, computed callers and capped/window-external paths remain UNKNOWN as complete proofs. Stop this finite negative lead rather than reclassifying the actual input.

out of scope: backing/I-O and per-sample DSP. Coverage unchanged108 native+8 scope CHECKED/24 PARTIAL/0 NOT DONE;140 AUDIBLE/0 INTERNAL. Research only; no production/fidelity/inventory/hardware or Q15 changes.
