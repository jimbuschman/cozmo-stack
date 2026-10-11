# Q14: Scalar store spans across rate/frame boundaries

Checkpoint784; scope guard reread. Engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1.

`20261010-sound-rate-frame-scalar-width-expansion.py/.txt` extends779's known-base scalar write check beyond the starting address. For STRD and VSTR D it checks8 bytes, VSTR S/ordinary STR4, STRH2 and STRB1. A new scalar-write-overlap event would be emitted only when the start is outside the target but a later byte overlaps105243C..4F or108DF90..9B; existing events are preserved. This complements770's STM/VSTM and771's full-register VST forms, rather than claiming every store encoding or copied pointer modeled.

The6855 windows retain exactly the same300 events and338 state caps; `-scalar-width-comparison.json` preserves the empty added/missing sets. No positive wide scalar writer is introduced. Exclusive doubleword forms, unknown/shifted bases, stack/copy/foreign aliases and capped paths are not excluded by this limited model. Actual fourteen scalar writes remain supported by their earlier primary captures; missing forms/aliases are not filled by zero defaults.

Original043/092/099/120/124/130 remain PARTIAL. out of scope: storage/I-O and per-sample DSP; no new callee descent.

107 native+8 scope CHECKED/25 PARTIAL;140 AUDIBLE/0 INTERNAL unchanged. Research only; no production, fidelity, inventory or hardware edits.
