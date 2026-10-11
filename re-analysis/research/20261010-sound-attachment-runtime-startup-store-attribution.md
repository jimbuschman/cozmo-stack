# Q14-104/131: independent startup managers and frame locals

Checkpoint724; guard reread before each step. Primary evidence: runtime-startup-fields-native.txt, runtime-startup-inputs-native.txt and runtime-startup-product-native.txt (20261010-sound-attachment- prefix). Engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1.

| Original candidates | Positive destination |
| --- | --- |
| 99E770 / 99E774 | Independent118-byte startup product from99E6AC/B0→R7; initializes repeated records with mutex construction at0/14/28/3C. RawR4 goes into product34/38, alongside2C/30 after mutex28 initialization. These fields are the product's separate record words, not writes through a parent-node pointer. Publication/initialization order retained, no synchronization-body descent. |
| 99EC18 / 99EC1C | Independent50-byte product from99EBC4/D0→R6; clears/initializes the whole product with4D36DC before explicit rawR4 stores8..4C, including34/38, then publishes product into outerR7+94. Original destinations positive regardless of outer owner identity. |
| 99ED68 / 99ED6C | Independent3C-byte product from99ED14/20→R6; rawR4 initializes34/38 alongside its collection/default fields, then initializes embedded10 mutex. Positive product identity, not node parent/bus link. |
| 99F34C / 99F374 | Actual99F2E8 establishesFP=SP+20 at99F2F0. Negative34/38 are frame locals even after dynamic SP allocation at99F354. Not a parent/bus destination. |

out of scope: allocation and synchronization infrastructure bodies; no descent. No universal alias or startup-completeness claim. Counts107 native+8 scope CHECKED/25 PARTIAL,140 AUDIBLE/0 INTERNAL unchanged. Research only.
