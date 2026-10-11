# Q14-119: Thumb stop-offset store candidates

Checkpoint669; decisions only; guard reread before each step. Engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. Primary `20261010-sound-stop-offset-thumb-stores.json`, `20261010-sound-stop-offset-thumb-store-candidates-native.txt`, `20261010-sound-stop-offset-thumb-store-tail-native.txt`, `20261010-sound-stop-offset-thumb-store-product.json`. This extends SW614's ARM navigation to aligned Thumb32 immediate word/byte/halfword/doubleword encodings for offsets1F8..1FB, only for the original explicit writer question.

| Candidate | Exact disposition |
| --- | --- |
| 5C1924 STR r1,[r4,1F8] | Real Thumb constructor store: r4=incoming object, zero supplied at5C1922. Subsequent actual5C1928 PC-relative address103F1C4 has ARM_GLOB_DAT naming `_ZTVN4Anki5Cozmo17BehaviorFindFacesE`; constructor installs resolvedtable+8 at object0. This is BehaviorFindFaces, not a PBI product. No unrelated behaviour-body descent. |
| 6B85E6 STRD r0,r0,[SP,1F8] | Actual base is SP, and preceding6B85E4 supplies0. Stack-local initialization, not PBI+1F8. |
| 797D22 | Candidate starts in the second halfword of actual797D20 BLX4A40A8, followed by the literal island797D24..30. Not an instruction boundary. |
| 7ADC68 | Candidate starts in the second halfword of actual7ADC66 BLX4A4FF0. Not an instruction boundary. |
| 850044/850048 | Raw words007EE9CE/007EE9C8 occur among inline literals and State_c.Import strings after prior function's nonreturning exception call; explicit words/string island captured. They are data, not PBI writes. No absent-disassembly-only argument. |

No new PBI writer is established. Actual six ARM writer/consumer contracts remain SW614 joined to SO541/SJ542/SF543/BI425/HN518. Original119 stays PARTIAL for relative/indirect/aliased writer and music-operation input/callback proof. This finite encoding check is not all-write absence: register-index, adjusted-base, bulk/copy and other aliases remain UNKNOWN. Stop these six rejected candidates rather than inventing another source path.

out of scope: unrelated stack calculations/storage backing and per-sample DSP. No production/fidelity/inventory/hardware/Q15 edits. Counts107 native+8 scope CHECKED/25 PARTIAL;140 AUDIBLE/0 INTERNAL.
