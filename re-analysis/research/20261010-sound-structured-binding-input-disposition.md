# Q14-132: structured input entry and subscription-slice navigation

Checkpoint799; queue5 guard reread before each step. Engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. Decisions only. out of scope: coordinate/cone/positioning payloads, backing/hash/allocator bodies and per-sample DSP; no descent into them.

The original remaining input is the A67830 object/input supplying record IDs/properties/types to A67384, then actual A11F98 recipients. The660 exact-entry check remains paid. `20261010-sound-structured-binding-input-callers.py/.json` examines only an entry/prologue slice[A67830,A67844) and the in-scope subscription loop[A67A68,A67AF4). Sources within the whole existingA67830..A67B94 owner region are excluded, so its own branches are not called outside admission. The intervening positioning payload is not a target slice and is not extracted.

Fifteen encodings result:two ARM-looking,thirteen Thumb-looking. `-input-false-native.txt` positively decodes89D69C as Thumb CMP R1,1 followed by BLT89D6B0, and8C12C8 as Thumb LDR R2,[SP,C] followed by MLA at8C12CA. Neither supplies a structured input or entersA67AA8/A67ADE.

`-candidate-source-native.txt` and `-candidate-word-witness.json` reopen the thirteen aligned containing ARM words with local source slices, entry/prologue and return context. These are byte/instruction interpretations, not a complete mixed-mode/indirect-source input proof:

| Apparent Thumb source | Actual containing ARM word |
| --- | --- |
|A67356|A67354 BIC R2,R2,1F|
|A673DA|A673D8 CMP R2,0|
|A6744A|A67448 CMP R0,0|
|A6754A|A67548 ADD SP,SP,1C|
|A67576|A67574 ADDS R2,R2,0|
|A67596|A67594 ADD R2,R3,8|
|A676E2|A676E0 AND R4,R4,7F|
|A67702|A67700 LDRH SB,[IP,5]|
|A67D2A|A67D28 STR R2,[R4]|
|A67D2E|A67D2C STR R3,[R4,10]|
|A67F56|A67F54 STR R4,[SL]|
|A67F6A|A67F68 LDR SB,[PC,98]|
|A67F7E|A67F7C LDR R0,[SB]|

No surrounding excluded payload values or semantics are reconstructed from those slices. The physical packed-record/removal/append/failure decisions stay PI611; no array/hash/copy callee body is followed. Unlike794's whole local predecessor witnesses, these limited word/slice checks do not certify every alternative source-entry mode or copied/indirect branch absent. No positive admitted supplier is established by any encoding.

Q14-132 remains PARTIAL: actual structured object/input ID and property/type values (or the required bounded exclusion), then their A11F98 decision recipient. Six-bank zero type14 remains the finite authored input result; it does not prove runtime absence. Stop this negative lead before an arbitrary recipient family or a positioning-payload audit.

Coverage unchanged108 native+8 scope CHECKED/24 PARTIAL/0 NOT DONE;140 AUDIBLE/0 INTERNAL. Research only; no production/fidelity/inventory/hardware or Q15 change.
