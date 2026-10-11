# Q14-104/131: music timing scalars and embedded table stores

Checkpoint722; guard reread before each step. Primary evidence: music-scalar-fields-native.txt, music-scalar-inputs-native.txt and music-scalar-product-native.txt (20261010-sound-attachment- prefix). Engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1.

| Original candidates | Destination and boundary |
| --- | --- |
| 99286C | Actual9927D8 loads receiver from incomingr0+90; null returns1 without updates. Incoming descriptor raw three words first copied to20/24/28, descriptor10→1C, descriptorC low5→packed18;28 converted signed with raw rate/1000, descriptor14/18/1C copied2C/30/34, then copied34 converted likewise. Concrete incoming90 receiver remains UNKNOWN. This is timing control arithmetic, not per-sample DSP. |
| 992BC0 | Actual992B1C incomingr0→R4 direct receiver; incomingr1 dereferenced descriptor→R5, signed descriptor1C converted raw rate/1000 then published34 after earlier field/cursor writes. Admitted direct receiver remains UNKNOWN; do not equate it to another parser from similar fields. |
| 9935DC | Actual993550 incomingr0→R5 direct receiver, same copy-before-convert order as9927D8 without90/null indirection. Admitted direct receiver remains UNKNOWN. |
| 998394 / 99843C / 9984C4 | These publish table101BF28 at embedded receiver38, followed respectively by99D5DC cleanup or previously99D5F4 initialization. Actual constructors/destructors99846C/998314/9983BC preserve incomingr0 inR4; writeback STR at998394/43C advancesR0 to embedded38 before cleanup. Positive989EF8 allocates separate5C product at989F2C, passes it to99846C at989F40, initializes/retains, and succeeds only9985B0==1; failure calls986560/985CAC. Thus supplied ctor destination is independent product's embedded table, not output-bus ID. Alternate constructors/destructors remain conditional; this does not prove every receiver. |

No per-sample arithmetic descent. Counts107 native+8 scope CHECKED/25 PARTIAL,140 AUDIBLE/0 INTERNAL unchanged. Research only.
