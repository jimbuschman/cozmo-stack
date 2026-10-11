# Q14-104/131: early independent context list fields

Checkpoint719; guard reread before each step. Primary bytes: `20261010-sound-attachment-early-state-fields-native.txt`, `20261010-sound-attachment-early-state-inputs-native.txt`, `20261010-sound-attachment-early-state-product-native.txt`; engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1.

| Original candidates | Positive destination and exact decision order |
| --- | --- |
| 98315C / 983160 | 97E5EC allocates separate60-byte product at97E614, keeps it inr7, passes it to983110 at97E630. Constructor inputr0→r4, table101BE88 from98311C+8+698D5C+8, zeros34/38 before embedded44 initializationA3D1E4 and subscription registrationA29228. No node parent/bus pointer is used for these defaults on this product path. |
| 983258 / 9832B4 | Actual983204 preserves same constructor/destructor receiver inr6, reads its34 head and publishes next before releasing element4 and the element. After loop, A292E8 removes subscriptions before34 is cleared. Conditional direct destructor receivers are not exhausted. |
| 9833F4 / 9837CC | Actual9833B8/983790 preserve incomingr0 inr6 and advance its34 list head before releasing element4 and backing element. These duplicate local contracts do not independently prove alternate incoming receiver identities. |
| 983978 / 98397C | Actual983834 saves incomingr0 atSP4, allocates separate28-byte element at983860 intoR4 and18-byte auxiliary at983880 intoR6. Null products take failure branches. It records associated object4 and timing/property fields, increments associated24 before replacing element4, retains timing getter decisions, and publishes initialized element to outer34 (new head pointing at old head or zero). Destination outer receiver remains conditional; element field contents alone do not prove the outer object. |

AW718 follow-up: positive97E680..688 supplies the independent60 product inr1 to9822E4, with retained caller second argument inr5 as setterr0. This proves one setter use publishing context38; that caller argument's admitted identity and alternate uses remain UNKNOWN. AW718's conditional setter limit is narrowed, not replaced with a universal absence claim.

Decision publication/retirement order is retained. out of scope: backing allocation/free arithmetic and storage infrastructure; no descent. Counts107 native+8 scope CHECKED/25 PARTIAL,140 AUDIBLE/0 INTERNAL unchanged. Research only.
