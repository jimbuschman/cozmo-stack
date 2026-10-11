# Q14-104/131: note-state, PBI timing and MIDI reader fields

Checkpoint706; scope guard reread before each step. Primary: attachment-midi-member-fields-native and attachment-midi-member-inputs-native companions; engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. Ten original candidates. Existing positive MAT/NSP/MH/RI/CWT ownership rows reused; Q15 document is not expanded.

| Candidates | Actual destination and boundary |
| --- | --- |
| A3DEB4/A3DED4/A3DEE0 | NSP actualA3DE10 retains independently allocated MAT4 note-state inr5. Second pool defaults freehead34=0, then separate30-byte blockA3DEBC publishes44 evennull; nonnull freehead34=block, capacity38=4. Not common node parent/outputbus. Exact init failure remains MAT4 boolean result; no all-state initialization inferred. |
| A3E0C0/A3E13C | NSP actualA3DF44 retains note-state inr4. Selected active list2C moves members to freehead34 with native head/tail/count order; capacity38 clears after spill/backing cleanup. Existing note/PBI releases remain decision-relevant and retain their own rows. out of scope: pool backing/free implementation; stop descendants. |
| A3E520 | MAT4 actualA3E460 allocates64-byte note-stateA3E4E8 for status90/nonzero velocity. Returnedr3 is separate state; zero38 among explicit fields before eventV8 publication/NSP initialization. Null allocation takes original event/time/failure route. No whole-state memset or node attachment. |
| A3E98C | MH4 actualA3E920 walks event8.2C links; entry8 is PBI r5. PBI34null stores event18 raw timing to PBI38; nonnull invokes9E805C instead. This is an exact timing control, not per-sample DSP and not node outputbus. Context query/entry state/stop controls follow as existing MH4/5. |
| A3ED24/A3EF30/A3F074 | CWT1 constructs embedded MIDI reader90 viaA3ECC8. Constructor clears raw tempo34; RI3 first-headerA3EECC and restartA3EFCC assemble input bytes2..5 into raw word34. Reader ownership distinct from node parent; exact signed division, timing ratios and cursor/flag gates retain RI and later reader rows. Unknown division/global timing inputs remain open, no serialized-zero absence proof. |

All known destination claims are bounded to existing positive products. No new MIDI family, indirect alias absence, status upgrade or production acceptance follows. Original104/131 remain PARTIAL; counts107 native+8 scope CHECKED/25 PARTIAL,140 AUDIBLE/0 INTERNAL. Research only.
