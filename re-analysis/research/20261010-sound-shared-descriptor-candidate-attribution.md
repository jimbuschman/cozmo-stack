# Q14-024: candidate descriptor-caller attribution

Checkpoint644 (DC644), decisions only. Scope guard reread before each step. Primary `20261010-sound-shared-descriptor-candidate-attribution-native.txt`, same engine SHA256 `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`. Candidate navigation `20261010-sound-shared-descriptor-slot-call-candidates.json` is not receiver binding or absence proof.

SI562's concrete frame-context4C/50 wrappers return interior STMG descriptors. Matching virtual-slot offsets in unrelated objects do not recover their caller. Navigation found67 ARM load/BLX candidates and24 Thumb candidates. A quick return-register-field-read filter still included false candidates from adjacent function entries or alternate branches. The following primary checks reject those substitutions; Q14-024 remains PARTIAL.

| Candidate | Primary evidence | Attribution / stop boundary |
| --- | --- | --- |
|9AC5B8 |9AC5A4..5E0 | ActualR1 is a stack command descriptor pointer, and receiver is incoming node; returns directly after call. The alleged returned-pointer read9AC5D8 is on the separate mode0 branch, reading a global before9AB8AC, not dereferencing the virtual result. This is not frame-context ID lookup. |
|996DA0 |996D60..DCC | Pass the caller object inR1 and0 inR2 to incomingR1's live4C; subsequent path returns original caller object. Read996DC8 begins a distinct getter after a BXLR. Not a descriptor result read. |
|988050 |988030..8080 | Resolved child receives incoming command pointerR1. Function ends at988060; later988078 is next function's parent30 read. Not a returned descriptor. |
|98DDE0 |98DDD0..DE08 | After current object's live50 call, reload its64 member intoFP, then follow list4/objecttable5C. The next R0 dereference reads a newly loaded list object, not the virtual result. |
|9D2B40/A2DD78 |9D2B38..2B64;A2DD70..DDA0 | Child virtual4C dispatch finishes reverse traversal and returns before the next function's object80/CC read. Neither following load is a returned descriptor consumer. |
|A6E5A4 |A6E598..5D8 | Call target comes from a selected node object and stack command fields. AfterwardR0 is overwritten from stack/global before9AB8AC; later dereference is not the returned descriptor. |

No callee descent follows these offset matches: they are already-known node/control interfaces, not evidence of the specific frame-context getter. This pass does not assign new semantic names to STMG payloads, waive decision arithmetic as DSP, or infer caller absence/unreachability from the limited encoded search.

Remaining exactly: bind an actual101BFA0 frame-context4C/50 caller's shipped ID, interior-descriptor field reads and resulting parameter/selection/lifecycle decision. The limited candidate scan leaves copied pointers, different instruction sequences, indirect products and branches unresolved. out of scope: registry/hash/backing/free infrastructure and per-sample arithmetic.

Counts unchanged:103 native CHECKED +8 scope CHECKED /29 PARTIAL /140 obligations;140 AUDIBLE /0 INTERNAL. No production/manifest/hardware change.
