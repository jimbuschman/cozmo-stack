# Q14 rate/frame helper linkage and available shipped definitions

Checkpoint814; guard reread before each step. Engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. Exactly the existing positive rate/frame live-register observations;no unrelated API or provider family census.

Primary -live-helper-plt-native.txt and -symbol-native.txt capture the three existing linkage stubs and their ELF relocation identities. Reproducible -live-helper-witness.py/.json independently decodes raw ARM rotated immediates to calculate each slot and checks ARM_JUMP_SLOT/name. Raw4A4010 is the initial resolver word,not a resolved numerical function address;all three symbols are undefined in this engine (section0/value0).

| Existing linkage | Exact ELF interface identity |
| --- | --- |
|PLT4B3E70 ->GOT104575C|__aeabi_idiv|
|PLT4BE310 ->GOT1048E3C|__aeabi_uidiv|
|PLT4BAA40 ->GOT1047B4C|__aeabi_l2f|

The existing rate/frame list has14 register observations at13 unique calls.813 accounted for two native calls by primary incomingR3 read-before-definition checks. The remaining12 register observations at11 calls select these three helper interfaces:five signed-divide calls,five unsigned-divide calls,and one long-long conversion with bothR2/R3 live. The witness preserves every original call address,register,target value and seed. A retained register is still not automatically a pointer argument.

External cross-check ONLY (authority6):[Arm's RTABI32](https://raw.githubusercontent.com/ARM-software/abi-aa/main/rtabi32/rtabi32.rst),integer-division and integer-to-floating conversion sections,defines the two division functions with two32-bit scalar operands and the conversion with one64-bit scalar operand. Their standard base interfaces consumeR0/R1;R2/R3 are scratch,not extra pointer parameters. This supports a standard-ABI interpretation of the linkage names,not a primary proof of the actual bound helper's implementation/memory effects or an engine alias-absence claim. No implementation is inferred from that document.

Primary -live-helper-definitions.py/.json searches ONLY those three names in the28 shippedarmeabi-v7a ELF dynamic symbol tables,records each file SHA256,definition value/state bit,size,binding,visibility and dependencies. There areTEN exported definitions,so 'the numerical helper code does not ship' would be unsupported:

| Shipped library | Defined helper entries (state bit retained) |
| --- | --- |
|libacattsandroid.so|uidiv3FEE5;idivF4409|
|libBlueDoveMediaRender.so|idivDE35|
|libDAS.so|uidiv28BC5;l2f29181(size124)|
|libgnustl_shared.so|uidiv9B371;idiv9B405|
|libopencv_imgcodecs.so|l2fB6969(size124)|
|libstlport_shared.so|uidiv4EDFC;idiv4EEC4|

The engine's DT_NEEDED list directly includeslibDAS.so,libacattsandroid.so,andlibopencv_imgcodecs.so as well asphone libraries. That establishes available shipped source;it does NOT select a runtime winner,prove equivalence between providers,or classify these calls as unshipped phone implementation. Zero-size symbols are entry witnesses,not inferred extents. Keep the selected-provider/input frontier UNKNOWN unless a bounded source proof establishes it or a provider-independent argument-use proof covers the actual candidates.

Original six rate/frame all-writer/alias/startup-input proofs remain PARTIAL/AUDIBLE. No new scalar writer,global immutability,helper binding policy or fidelity status is claimed. Coverage116 CHECKED /24 PARTIAL /0 NOT DONE;140 AUDIBLE /0 INTERNAL. Research only;Q15 stays behindQ14. out of scope: dynamic resolver implementation,storage/I-O and per-sample arithmetic;only linkage metadata was read,not resolver or helper bodies.
