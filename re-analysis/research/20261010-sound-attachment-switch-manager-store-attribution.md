# Q14-104/131: switch-group field destinations

Checkpoint702; scope guard reread before each step. Primary: attachment-switch-manager-stores-native, attachment-switch-manager-inputs-native and attachment-switch-manager-entry-native companions. Engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. Seven original candidates; existing SM9/10, OR145 and SW482 reused.

| Candidates | Actual destination and boundary |
| --- | --- |
| A30E08/A30E34 | SM9 actualA30D98 allocates40 bytes atA30DDC, saves result r7, null branchesA30FAC. Nonnull initializes group38/count and34/base to0 and embedded28 table101C2D8. Incoming manager is r5, distinct from freshly allocated group. GroupID0 is not rejected; existing group returns unchanged. |
| A31858/A31884 | ActualA317DC retains incoming manager r6 and ID r4; missing bucket/key allocates40 bytesA3182C, saves result r7, null branchesA31944. Separate group product initializes38/34 to0 and embedded28 table101C2D8. No all-caller alias absence or common-node type inferred. |
| A31E50 | ActualA31C68 saves incoming group r0 inr8. Positive SM9 product has scoped array34/count38; nonzero key searches unsigned key18 in1C-stride records. Selected record retirement/ordered movement precedes live count38 decrement. This is group membership, not outputbus38. Key0 takes existing OR145 default-active branch; no wildcard bulk removal substituted. |
| A32238/A3224C | ActualA31F1C saves incoming group in fp (NOT a stack-frame base). SW482 supplies selected group retirement. fp34 is owned scoped-value array; count38 clears before free and base34/capacity3C clear after free. Default/binding detachment prefix stays exact SW482; trailing backing cleanup stops at its existing exclusion. |

out of scope: hash/bucket and owned array backing/free mechanisms; stop descendants. Scope selection, default presence, membership/notification and allocation-result decisions retain their exact existing rows. Alternative incoming destinations beyond the supplied products remain UNKNOWN, not unreachable.

Original104/131 remain PARTIAL. Counts107 native+8 scope CHECKED/25 PARTIAL;140 AUDIBLE/0 INTERNAL. Research only; no production/fidelity/inventory/hardware/Q15 change.
