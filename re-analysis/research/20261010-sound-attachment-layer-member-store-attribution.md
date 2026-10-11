# Q14-104/131: Layer member stores and unresolved cleanup input

Checkpoint683; decisions only; scope guard reread before each step. Primary `20261010-sound-attachment-layer-member-stores-native.txt`, `20261010-sound-attachment-layer-member-inputs-native.txt`, immediate caller navigation `.py/.json`, aligned pointer references `.json`. Engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. Existing LCP2–5 establish pool geometry and actual N80 publication, rather than assigning a class from neighboring addresses.

| Original candidate | Exact destination and disposition |
| --- | --- |
| 9D0FAC | LCP3 popped4C-byte pool slot r4; raw0 to member38 before embedded handle initialization and N80 publication9D0FC4..FE0. Not the owning node38. |
| 9D1098 | Same saved member SPC, already published at N80; member34 receives raw request70 before member1C publication and manager notification (LCP5). Not an inferred parent-pointer update. |
| 9D1AB4 | Reload same saved member SPC after separately allocating8*(old capacity+1) bytes at9D1A54. Null allocation returns to9D1760; nonnull result r5 published to member38, then new capacity to40. Existing old buffer/count are loaded from that member38/3C. |
| 9D1DE0 | Parallel growth path9D1D68..E4: separately allocate8*(old capacity+1), null joins9D1760; reload SPC member, publish new buffer38 before capacity40. No node outputbus assignment in this selected path. |
| 9D232C | Actual9D224C entry saves owner inr5;9D22B0 loads r4 from owner80 and9D22C8 unlinks it by next0 before callbacks. Selected member38 nonnull clears member3C before release, then clears38/40. This is the removed list member, not owner38. |
| 9D2490 | Actual9D2410 entry saves incoming second argument r1 inr4; selected38nonnull clears3C before release, then38/40. **Incoming destination remains UNKNOWN.** Similar fields and cleanup are not proof it is the same member. Immediate ARM/Thumb branch and aligned raw-pointer navigation find no reference; neither proves unreachable or excludes a copied/computed entry. |

out of scope: allocator, buffer copy and release implementations cannot settle which node is attached; stop their descendants.

Five specific destinations paid; the sixth is retained as a concrete input question: identify an admitted9D2410 caller's second argument before excluding its9D2490 write from node/bus alias proof. Original104/131 remain PARTIAL, including9FBB30 output destination,9F319C overlapping constructor destination and the existing alias questions. Counts107 native+8 scope CHECKED/25 PARTIAL;140 AUDIBLE/0 INTERNAL. No production/fidelity/inventory/hardware/Q15 edits.
