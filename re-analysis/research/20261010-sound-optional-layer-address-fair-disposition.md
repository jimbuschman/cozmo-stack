# Q14-021: optional entry address-navigation corrections

Checkpoint805; scope guard reread before each step. Engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. Coverage116 CHECKED /24 PARTIAL;140 AUDIBLE /0 INTERNAL. Exactly the five original optional AudioSample input entries58DB54,58DCC8,58EDFC,58EE4C,58C968.

The665 pointer-navigation script overwrote native.loads with EXECINSTR sections, even though native.read/word use that SAME list for all file-byte mappings. This correctly restricts seed enumeration but incorrectly makes non-executable data reads fail. `20261010-sound-optional-layer-data-map-witness.json` independently probes the actual bytes:original LOAD mapping reads exported-symbol value15198=58DB55,AddLayer GOT1043B9C=4A4010 (initial resolver value),and1040068=105243C; executable-only mapping raises unmapped for all three. The GOT resolver value is not a completed binding or an admitted optional caller.

`20261010-sound-optional-layer-address-fair-navigation.py/.txt` retains original ELF32 LOADs for data reads and uses a separate EXECINSTR section list for seeds. It also pays665's explicitly omitted selected Thumb MOVW seeds, using the later validated ARM/Thumb PC/add/arithmetic,rotated immediate,flag,width-aware load and FIFO model. ELF .got/.got.plt metadata supplies full bounds103E78C..1051000 rather than665's partial assumed103FF00..1040400 interval. This is static byte/address navigation; no loader implementation or runtime interposition rule is inferred.

`-address-fair-comparison.json` records14885 candidate windows versus665's12,791 capped windows,zero formed-target/argument/indirect-target events in either bounded model. Full-GOT seed windows are included so restored data mapping can be used. The zero-event result does not mean there is no runtime caller:raw file GOT values do not materialize dynamic provider bindings, caps and unmodeled arbitrary address/stack/copy/shifted/indirect/input paths remain explicit.

OL664's positive58EF4A RemovePersistentLayer->AddLayer PLT interface still holds independently of this navigation correction. Its exact matched-manager/tag/control,partial sample initialization and AddLayer-before-persistent-erase order remain paid. OL649/OL661 finite companion/managed checks and the five exported definitions likewise remain evidence only for their exact scopes.

Still open exactly:admitted shipped manager/tag/control and prior populated matching audio layer into RemovePersistentLayer,or another original optional producer. No candidate from this correction supplies that input. Stop this address lead at UNKNOWN; do not descend into optional creators/generators without a positive producer. Q14-021 remains PARTIAL and AUDIBLE;Q15 stays behind Q14.

out of scope: ELF/phone loader implementation,file lookup/storage/backing and per-sample DSP/noise. Research only; no production,fidelity,inventory or hardware changes.
