# Q14: Thumb MOVW address-construction seed correction

Checkpoint779; scope guard reread. Engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. Research navigation only; every behavioral conclusion retains its primary evidence.

The preceding navigation seeded ARM MOVW but did not start Thumb windows at MOVW. `20261010-sound-thumb-movw-seed-expansion.py/.txt` adds Thumb MOVW low-half seeds for the existing rate, device and GOT ranges, using the immediate16 encoding imm4:i:imm3:imm8. Actual propagated values still need the corresponding MOVT, body boundary and primary consumer; a low half alone is not a pointer.

Compared with778,6855 windows replace4393, a2462-window addition. The run retains exactly the same300 navigation events, with no new or missing event.338 local windows hit the4096-state cap, versus326 before. This is an expanded search, not an exhaustive proof: no new established writer/input is supplied, and the additional capped windows cannot be used to exclude an alias. RC654/direct scalar primary evidence remains intact. Shifted/stack/copied/foreign pointer and unknown-loop limitations remain.

Original043/092/099/120/124/130 stay PARTIAL. out of scope: storage/I-O and per-sample arithmetic; no new DSP/provider family is descended.

107 native+8 scope CHECKED/25 PARTIAL;140 AUDIBLE/0 INTERNAL unchanged. No production, fidelity, inventory or hardware changes.
