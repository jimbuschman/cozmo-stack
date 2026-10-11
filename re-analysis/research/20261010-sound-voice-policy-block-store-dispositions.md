# Q14-033: regional voice-policy block-store attribution

Checkpoint758; queue5 scope guard reread. Primary engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1; `20261010-sound-voice-policy-block-store-native.txt`, byte/relocation navigation `-block-store-navigation.json`. This follows the explicitly named multiword-store gap left by VW650/VN749 within the same related ARM methodsA53000..A56600. No new family census.

| Actual block store | Proven destination |
| --- | --- |
|A538C0 STM IP,{R0,R1,R2} |A538A4 setsR2=SP38;A538B0 copies it toIP. Three words read from PBI+158 viaA538B8/BC are copied to this stack descriptor. No direct voiceE0/E4 destination. |
|A53AB8 STM IP,{R0,R1,R2} |A53A98/AA0 likewise setsIP=SP38;A53AAC/B4 loads three words fromPBI+158. It is a stack descriptor before a separate live44 query; no direct voiceE0/E4 write. |
|A54A68 STM R4,{R0,R1,R2} |Actual VoiceInit entryA54A30;A54A58 setsR4=SP34. SourcePBI+158 is formed atA54A50;A54A64 loads three words;A54A68 writes exactlySP34..3F, thenA54A78 passes it to existing selected-format setupA5321C. No whole-voice zero/default initialization inferred. |
|A55018 STMIB SP,{SL,FP} |A55000/04 supply two local byte addressesSP2E/SP2F. The instruction publishes argument words atSP4/8; it does not store through those addresses or to the voice. |
|A55724 STM SP,{R0,SL} |Argument words go toSP0/4 before existingA4BC58 delivery. No effective voiceE0/E4 block destination. |
|A55C90 STRD R0,R1,[SP] |LocalSP0/4 receive raw words1/0 after stack reservation; the next receiver load is separate. No direct voiceE0/E4 block store. |

All six regional multiword encodings have fresh primary destination attribution. The initial supplementary lookup resolved seven PLT entries; VM759 fixes explicit ARM rotated-immediate decoding, resolves all twelve named memory relocations and finds four actual regional memset calls. Their actual destination/extent and one constant-zero layout gate are reconciled in `20261010-sound-voice-policy-memory-dispositions.md`. The JSON retains actual instruction bytes/slot equations. No internal-copy-wrapper or global absence claim follows. The addresses and values passed to existing decision interfaces remain exact; their backing/copy implementations are not descended into.

Original033 remains PARTIAL for the explicitly requested indirect VoiceInit/re-init and E0/E4 live-writer alias proof beyond known paths. Six stack stores are not six new obligations, a global absence proof or a DSP waiver. Existing output aliasesA559C0/A55B50 and four actual policy writes remain VW650.107 native+8 scope CHECKED/25 PARTIAL;140 AUDIBLE/0 INTERNAL unchanged.

out of scope: unrelated stack/backing mechanics and per-sample DSP. No production/fidelity/inventory/hardware changes.
