# Q14-104/131: known manager retirement tables and buffer-transfer entry correction

Checkpoint747; queue5 scope guard reread before every step. Primary `20261010-sound-bank-manager-retirement-product-native.txt`, `-retirement-table-native.txt`, `-derived-retirement-native.txt`, `-derived-delete-native.txt`, and `20261010-sound-bank-owner-retirement-receiver-native.txt`. SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. Reuse positive BP740 startup allocator/constructor/publication, rather than invent new recipients from field offsets.

| Step | Primary evidence and exact limit |
| --- | --- |
| Known ordinary product | BP740 actual98 allocation/9B1B50 and fixed108D8D8 publication. Fresh9B1B60+8+688E70+8=103A9E0; table0=9B0A60,4=9B14B4. Thus AW731's two retirement bodies have positively known manager table ownership; they are not unidentified node parent/output-bus setters. This does not establish every runtime invocation or alternate receiver. |
| Known derived product | BP740 actualC0 allocation/A35154 calls9B1B50 then replaces table by103BF48. Actual0=A34D38,4=A34D68. These preserve entry R0 asR4, clean own A0 member, restore R0=R4 and invoke base9B0A60 atA34D58/A34D88; deleting variant frees only after that base callback. Thus the same base retirement fields also have the known derived-manager product; no guessed base adjustment. |
| Ordinary base retirement | Actual9B0A68+8+689F68+8=103A9E0;9B14BC+8+689514+8 likewise. Both preserveR4, first retire embedded7C atA67E28; if54 nonnull clear58 before release54; if34 nonnull clear38 before release34; mutex2C/base4 retirement follows.9B14B4 then frees outer;9B0A60 returns it.34/38 here are this known manager's backing/count, not a sound-node parent/output-bus pair. out of scope: allocator, backing and mutex internals; do not trace them. |
| Actual buffer-transfer entry | **Correction to AW731:**9B1AFC belongs to entry9B1AAC, not9B1A4C.9B1A4C reads14 and returns at9B1AA0 or loops its alternate release tail9B1AA4/AA8;9B1AAC begins a separate34-nonnull guarded function with its own push at9B1AB8 and R4=R0 at9B1ABC.9B1AF4 delivers saved34/38 toA68150(owner+7C) before9B1AFC clears34. Actual9B1AAC input remains UNKNOWN; the wrong preceding function's receiver is not substituted. |
| Positive bank-owner parallel path | BC739's58 product uses101C028.0=9B47D8; fresh9B47DC retains its ownR5, and9B4904..4930 supplies that record34/38 pair to the existing completion input after the reference-count/removal gate. This inline path is not a direct invocation of9B1AAC or9B1830; don't upgrade their separate inputs from similar instructions. |

The known table/product joins narrow the previously broad UNKNOWN receiver claim for9B0A60/9B14B4 to alternate products/actual dispatch inputs beyond BP740's known products.9B1830's separate constructor receiver remains UNKNOWN. Correct9B1AAC remains an explicit input frontier. Original104/131 remain PARTIAL for the other named conditional inputs and complete alias census; no all-writers claim from these table matches.

Counts107 native+8 scope CHECKED/25 PARTIAL,140 AUDIBLE/0 INTERNAL unchanged. No production/fidelity/inventory edits; research only.

Specific dual-alignment immediate ARM/Thumb navigation for corrected9B1AAC and9B1830 (`20261010-sound-bank-record-retirement-input-callers.py/.json`) returns no candidates. This leaves their actual inputs UNKNOWN; no indirect/relative/copied-input absence claim. Stop these leads.

BMT748 now supplies the actual known-product Term slot0 receiver after the required singleton reload. Deleting slot4 invocation and alternate reloaded products remain conditional.
