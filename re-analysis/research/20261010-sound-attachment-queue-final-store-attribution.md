# Q14-104/131: fixed global queue recipient and direct cleanup input

Checkpoint729; guard reread before each step. Primary evidence: queue-final-fields-native.txt, queue-final-inputs-native.txt, queue-final-owner-native.txt, queue-final-owner-source-native.txt and queue-global-owner-slot-native.txt (20261010-sound-attachment- prefix). Engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1.

| Original candidates | Destination and exact order |
| --- | --- |
| 9ADB44 / 9ADB50 | Actual9AD8F0 computes GOT base9AD904+8+692980=104028C inSB. Mode branch9AD984..998 loads rawoffsetFFFFFE44→GOT10400D0→108D870 queue pointerR7. Nonzero filter path9AD9B8 moves thisR7 toR5;9AB048 precedes unlink, conditionaltail34, removed element0→oldfree-head38, freehead38→element, count44 decrement. Thus this receiver is the fixedglobal queue pointed at108D870, supplied independent78 startup product inAW725. It is not incomingr0; do not confuse earlierR5 action-list cursor with this destination. |
| 9ADC00 / 9ADC0C | Same mode/GOT product; zero-filter path9ADB78 moves globalR7→SL, distinct9AB048-before-unlink then conditionaltail34/free-head38/count44. This is the same positively bound global queue recipient. |
| 9ADD28 / 9ADD34 | Actual9ADC30 incomingr0→R5 at9ADC4C, explicit direct queue cleanup/filter contract.9AB048 precedes unlink and conditionaltail34/free-head38/count44. Admitted incomingreceiver/alternatecalls remain UNKNOWN; matching global layout is not proof. |

No allocator descent. Counts107 native+8 scope CHECKED/25 PARTIAL,140 AUDIBLE/0 INTERNAL unchanged. Research only.
