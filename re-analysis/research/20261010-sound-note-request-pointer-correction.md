# Q14-110 / Q14-135: exact note request pointer correction

Checkpoint652; scope guard reread before each step; decisions only; request `requests/20261007-codex-queue-5.md`. Primary `20261007-singing-node-submit-request-native.txt` and fresh MP646 companion `20261010-sound-midi-alternate-poster-native.txt`; engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1.

The old NS3 row in `20261007-singing-decision-rows.md` incorrectly described status!=90 as using a descriptor pointer from event18. Actual instructions:

| Primary | Exact value/pointer |
| --- | --- |
| A3DCBC/A3DCDC | Load event18 into r4, then store it atsp90. Descriptor originsp1C makes this request74, the timing field; it is not an input request pointer. |
| A3DCD8/A3DCEC/A3DD0C | Compare saved status with90. Non90 executesADDNE r4,sp,1C thenBNE toA3DDDC. Thus the ordinary status path submits localP. |
| A3DD10..DD1C | For90, load velocityspA3 then unconditionallyADD r4,sp,1C before the zero-velocity test. Both zero and positive velocity use the same localP; only positive velocity performs preparation. |
| A3DDCC..DE00 | Successful preparation updates request flag; commondelay gate receivesr1=r4. Raw1 reloads actualtarget table and dispatches128 withr1=r4 again. No intervening reassignment or alternative request pointer. |

The shared NS3 evidence row is corrected in place. MP646's positive packed-event writer→localrequest84/87→prepared concrete Sound policy path remains supported; this correction strengthens the explicit pointer handoff and does not create a new caller or producer. Preserve exact partial initialization and all non1 cleanup behavior; no whole-request zero/default substitution.

Counts105 native+8 scope CHECKED/27 PARTIAL;140 AUDIBLE/0 INTERNAL. No production, manifest, inventory, hardware or Q15 scheduling/acceptance change. out of scope: owned request-array allocator/free mechanics.
