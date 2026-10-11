# Q14-104/131: concrete same-offset storage/position candidates

Checkpoint624; guard reread before each step. Primary `20261010-sound-attachment-storage-attribution-native.txt`, engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. AW623's actual node writes remain distinct from the following positively identified destinations.

| Candidate | Native primary | One-line disposition |
| --- | --- | --- |
|9E7C84/88 |9E7BF4..9E7C94 | Base r3 is newly allocated50-byte backing at9E7C20/28, published inowner18;34/38 initialize an embedded buffer, not audio-node parent/bus. out of scope: buffer storage initialization/backing. |
|9E88E8/EC |9E8840..9E88F4 | Base r0 is a separate50-byte allocation at9E8890, published at allocated20-byte owner18; writes initialize its embedded buffer. out of scope: buffer backing; no node attachment callback. |
|9EB7A0/E8 |9EB750..9EB7F8 | Base r5 is PC-relative global108DAE8 (9EB758+8+6A2388),34 stores string-index length/decrement while2C publishes copied inputstring. out of scope: file-resolver path string storage; no descent into parsing/open descendants. |
|9FD360/364 |9FEC60..9FECD0;9FD318..9FD378 |74-byte separately allocated position entry passed directly to9FD318, then published as manager pointerarray element;34/38 are position entry state, not node fields. out of scope: excluded positioning payload and storage backing. |
|9FD760 |9FD4D4..9FD51C;9FD748..9FD788;RA475C | Same position entry uses34 as tick+duration endpoint; retained start admission2/25, tag1 and activation-order decisions already RA475C. out of scope: intervening coordinate/path calculation, not ordinary parent attachment. |

9EB250 uses a newly allocated90-byte object from9EB14C, then writes34 from the saved constructor argument; it installs103B498 (literal9EB4B0 via9EB1C0+8+6502C8+8), distinct from the positively supplied nine audio-node/two bus products. Its specific object/caller identity must be checked before removing it as a node alias; no class is inferred from allocation size alone.

9FBB30 has no direct ARM BL/B candidate in95E540..A6CF00 or absolute-word reference in ELF LOAD mappings in this bounded navigation scan. This is not proof of unreachability or of the output destination. That concrete input ambiguity stays UNKNOWN, as do relative/indirect/Thumb aliases. Navigation failure stops this item, rather than creating a plausible route. Continue the positive9EB250 constructor/object identity next, then original explicit writer/input proof.

104/131 remain PARTIAL. Counts100 native+8 scope CHECKED/32 PARTIAL/140;140 AUDIBLE/0 INTERNAL. No production/fidelity/inventory/PROJECT_STATE/hardware/Q15 edits.
