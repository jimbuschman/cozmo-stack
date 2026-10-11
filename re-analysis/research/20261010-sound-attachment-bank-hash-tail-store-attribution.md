# Q14-104/131: bank hash exclusions, frame local and positive58 product

Checkpoint733; guard reread before each step. Primary evidence: bank-hash-tail-fields-native.txt, bank-hash-tail-inputs-native.txt and bank-hash-tail-entries-native.txt (20261010-sound-attachment- prefix). Engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1.

| Original candidates | Disposition |
| --- | --- |
| 9B5600 | Actual9B54D0 setsFP=SP+20; negative34 is frame local. Pointer returnA68804/lock decisions are not settled by this frame attribution. |
| 9B5CC8 / 9B5CD8 | out of scope: hash backing-array cleanup34/38/3C in9B5C10; stop without tracing rehash/free bodies. |
| 9B71D8 / 9B71DC / 9B7270 / 9B7284 / 9B7484 / 9B74A0 / 9B74A4 | out of scope: hash/rehash backing-array reset/replacement/failure stores34/38/3C in9B6C34; stop without tracing infrastructure. |
| 9B7714 / 9B7718 | Positive9B74D8 path requests58 bytes at9B7688, alloc9B76A0→SL, null9B7A3C; original34/38 cleared among product14..44 defaults before raw parameter/packed flag publication. This independent product identity is positive; does not prove alternate9B1830 constructor inputs or bank completeness. |

Counts107 native+8 scope CHECKED/25 PARTIAL,140 AUDIBLE/0 INTERNAL unchanged. Research only; original attachment aliases stay PARTIAL.
