# Q14-033: regional memory-call destinations and corrected PLT decoding

Checkpoint759; queue5 guard reread. Primary engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1; `20261010-sound-voice-policy-memory-input-native.txt`, `-memory-receiver-native.txt`, `-layout-query-native.txt`; raw instruction/relocation witnesses retained in `-block-store-navigation.json`.

The PLT lookup originally used Capstone's immediate operand while dropping its separately rendered rotate operand. Actual ARM `#124,#20` encodes ROR32(124,20)=7C000, not124. Correct decoding uses imm8 rotated right by 2*rotate4 from instruction bytes. It maps all twelve named memory relocation slots and identifies four actual calls to memset PLT4D36DC. Its equation4D36DC+8+B00000+7C000+89C=104FF80, with actual ARM_JUMP_SLOT `memset`. This corrects VN758's limited seven-entry screen; neither pass certifies internal copy wrappers or global call absence.

| Actual call | Decision/interface disposition |
| --- | --- |
|A530D4 | Existing G8/RS11 output-padding path. Supplied layout queryA4721C is exactly MOV R0,0;BX LR; callerA530B8 tests its result and branches toA530EC on zero. Thus the contiguous-pad branch containing this call is unreachable from this actual query, a positive value/branch proof. Do not use search absence. |
|A5311C | Selected planar-pad loop: destination=[self88] plus per-plane stride4*U16(self94), length4*selected positive rounded delay count, fill0. Timing/count/first-output flag and silent-region geometry remain G8/RS11 exact controls. This is output sample-buffer filling, not a formed self+E0/E4 alias; it does not authorize changing delay or silent start/end. |
|A53D98 | A53D68 requests9C bytes viaA7A7F4; successful returned productR0 is retainedFP, null skips. memset uses that same returned pointer, length9C, fill from saved rawR3. Separate selected FX helper product initialization; no whole-voice E0/E4 default is inferred. Stop allocation body at its interface. |
|A54BF8 | Actual VoiceInit likewise requests9C bytes atA54BC8/A54BE0; successR0 retainedFP, null joinsA54EC8 before fill. memset uses same returned pointer, length9C, saved raw fillR3. It initializes that separate helper product rather than the original voice receiver. No inferred constructor zero-fill of policy fields. |

All four regional memory-call candidates now have primary dispositions; six block-store destinations remain VN758. The finite region still does not prove all composed/escaped/indirect aliases or VoiceInit callers.033 PARTIAL;107 native+8 scope CHECKED/25 PARTIAL;140 AUDIBLE/0 INTERNAL unchanged. No production/fidelity/hardware changes.

out of scope: backing allocation and the sample-write implementation; retain exact padding count, planar stride, first-output gate and silent-region boundaries. No allocator or per-sample descendant extraction.
