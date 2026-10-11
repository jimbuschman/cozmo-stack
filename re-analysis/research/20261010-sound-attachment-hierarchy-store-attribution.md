# Q14-104/131: modulator hierarchy and stack destinations

Checkpoint687; guard reread before each step. Primary `20261010-sound-attachment-remaining-d-stores-native.txt`, `20261010-sound-attachment-hierarchy-store-inputs-native.txt`; engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. Existing MT/HI/IC selected hierarchy rows reused. Five original raw candidates.

| Candidate | Positive destination |
| --- | --- |
| 9D96CC | Actual9D94D4 saves incoming objectr0 inr7, publishes modulator-base103B1B8 at94F0+8+661CB8+8. Selected2C/30 array cleanup clears logical count before buffer release, then base2C/capacity34. This is constructor-defined modulator storage, not ordinary node parent34; arbitrary incoming aliases are not declared absent. |
| 9DD028 | HI2 first-level hierarchy insertion. Actual parent2C array base and count30 produce spare positionbase+1C*oldcount; backward step subtracts1C before store34, so its destination is next slot's identity18, not an object's parent34. Oldslotidentity18 is copied to nextslot18, while child ownership and state fields are moved separately. |
| 9DD158 | HI3 second-level insertion uses parentC base/count10 and same1C stride/backward ownership transfer; store34 after subtract1C again writes nextslot18. No whole hierarchy closure or array-caller alias absence is inferred. |
| 9DE18C | r2=SP80 then predecrement34, actual destinationSP4C. Returned9DFA30 pointer is saved on stack before9D5414. No node34 write. |
| 9DF984 | Actual9DF8F0 container base0/count4/capacity8 produces spare1C-stride slot. After backward1C step, copies sourceidentity18 to nextslot18 via store34; this is an adjacent array slot, not parent34. Other transferred child/state fields retain existing HI/IC decisions. |

out of scope: buffer release, copying mechanics and allocation implementation; stop descendants after destination attribution.

Original104/131 stay PARTIAL for actual admitted destinations and alias questions. Counts107 native+8 scope CHECKED/25 PARTIAL,140 AUDIBLE/0 INTERNAL. No production/fidelity/inventory/hardware/Q15 edits.
