# Q14: ARM rotated-immediate propagation

Checkpoint785; scope guard reread. Engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1.

`20261010-sound-rate-frame-rotated-immediate-expansion.py/.txt` independently extends779 to decode ARM immediate ADD/SUB/MOV from the instruction word: ROR(imm8,2*rotate4). This handles both expanded and extra-rotation Capstone operands. Source example4D36E0 is ADD IP,IP,#124,#20: treating its operands as a three-term addition, or124 alone, would lose the actual7C000 value. Thumb immediates retain their existing decoding.

Primary `-rotated-immediate-witness-native.txt` and bit-level `-witness.json` check four existing source instructions:4D36DC givesB00000,4D36E0 gives7C000,9F31FC gives40000000 and9F3200 gives20000000. These reproduce759's PLT address components and the record constructor's exact bit constants; no imported result is synthesized. This is navigation validation, not a production/equivalence test.

The6855 windows retain exactly the same300 events and338 caps; `-rotated-immediate-comparison.json` records no added or missing event. Known flags/callee ABIs remain bounded; S-suffix arithmetic, shifted bases, stack/copies, unknown loops, external mutation and caps are not a complete alias proof. Original rate/frame groups043/092/099/120/124/130 stay PARTIAL. out of scope: backing/I-O and per-sample DSP.

107 native+8 scope CHECKED/25 PARTIAL;140 AUDIBLE/0 INTERNAL unchanged. Research only; no production, fidelity, inventory or hardware edits.
