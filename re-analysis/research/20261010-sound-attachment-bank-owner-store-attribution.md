# Q14-104/131: bank lifetime handoff and excluded hash backing fields

Checkpoint732; guard reread before each step. Primary evidence: bank-owner-fields-native.txt, bank-owner-inputs-native.txt and bank-owner-product-native.txt (20261010-sound-attachment- prefix). Engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1.

| Original candidates | Disposition |
| --- | --- |
| 9B493C / 9B498C | Actual9B47D8 incomingr0→R5, incomingr1→R6. Atomic48 reference decrement under manager mutex precedes lifetime branches; only count<=0 reaches cleanup. Supplied-buffer34 and size38 are handed toA68150(globalowner+7C) before34 receivesR6. These are exact source/bank lifetime decisions. Known owner input proof from earlier teardown research is retained; alternate indirect receiver/input beyond that proof remains conditional. No arbitrary HIRC parent classification. |
| 9B4D0C / 9B4D28 / 9B4DAC / 9B4DC4 / 9B5018 / 9B502C / 9B5034 | out of scope: hash/rehash backing-array stores in9B49A4 (receiver34 backing,38 capacity/slot count,3C associated size;0.9 load-factor gate at9B4A58..84); stop, no hash/allocator descent. |

The excluded seven backing candidates do not exclude reference-count/buffer-handoff decisions and do not settle whole parent/bus alias obligations. Counts107 native+8 scope CHECKED/25 PARTIAL,140 AUDIBLE/0 INTERNAL unchanged. Research only.
