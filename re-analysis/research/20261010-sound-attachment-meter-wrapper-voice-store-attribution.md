# Q14-104/131: meter coefficient banks, FX wrappers and voice defaults

Checkpoint710; guard reread before each step. Primary: attachment-meter-voice-fields-native/input-native (input companion filename is attachment-meter-voice-inputs-native), attachment-meter-wrapper-products-native/selected-inputs-native and meter-first-bank-input-native companions; engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. Sixteen original candidates. MC667/VF/VI191/VD490 reused.

| Candidates | Actual destination and boundary |
| --- | --- |
| A51B78/A51B7C/A51C9C/A51CA4 | MC667 mask10 first selected filterbank meter18: ip loadedA51AE0 then adds per-channel offsetr6. Positive MC667 separately allocatesB0*channels. Writes34/38 expand coefficient words into bank; later alternative retains same selectedip. Parameter construction/order remain exact MC667, not ordinary-node fields. |
| A51EE0/A51EE4/A52004/A5200C | Second bank meter14: ip loadedA51E48 then adds per-channel offsetr7; same B0-stride separate ownership. Parameter/default/design publication retained MC667. out of scope: per-sample meter/filter recurrence; stop descendants. No equivalence test or production arithmetic settlement claimed. |
| A53E34/A53E3C | Voice FX recreation selects9C-byte wrapper viaA53D7C; returnedfpA53D80. Formatword34 and state38 initialized in separately allocated wrapper, not a stack frame despite fpregister. Earlier34-byte allocationA53B6C is a different branch, not the receiver for these stores. Existing wrapper Init/cleanup decisions retained. |
| A54C8C/A54C94 | VF3 actualA54A30 allocates9C atA54BE0, fp=result; fields34/38 are buffer format/state defaults before VF4 Init/publication. No voice/node attachment assigned. |
| A546C0/A546D4 | ActualA54650 voice ctor retainsincomingr0→r4. Known A4304C voice factory independently allocates540 then invokes ctor. Voice38=0,34=FFFFFFFF distinct from bank node fields; VI191/VD490 exact field/table/source ordering retained. |
| A541E4/A54200 | ActualA541A0 incomingr0 writes38=0,34=FFFFFFFF. Conditional local defaults only: admitted receiver/product not established. Similarity toA54650 fields is not destination proof; keep input UNKNOWN. |

out of scope: allocator/backing and per-sample recurrence bodies; stop descendants. ActualA541A0 receiver is an explicit unpaid104/131 input, alongsideA39674. No all-indirect alias closure inferred. Original104/131 remain PARTIAL; counts107 native+8 scope CHECKED/25 PARTIAL,140 AUDIBLE/0 INTERNAL. Research only.
