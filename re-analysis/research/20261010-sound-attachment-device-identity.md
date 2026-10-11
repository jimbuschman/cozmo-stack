# Q14-104/131: positive device descriptor identity

Checkpoint625; scope guard reread before each step; decisions only. Primary `20261010-sound-attachment-device-identity-native.txt`, engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. Existing DA498B positively owns this exact device constructor; this pass uses fresh native bytes to reconcile the candidate9EB250 with its actual descriptor input.

| Step | Primary | Exact identity / disposition |
| --- | --- | --- |
| AD625A caller ABI |9EB520..55C;DA498A | Wrapper supplies9EB10C(keylo,keyhi,originaldescriptor,originalupperIdentity) and exact stacklistenerMask/aux/root/0. Third argument is the descriptor pointer saved from incomingr0, not an audio-node output destination. |
| AD625B positive allocation/table |9EB10C..9EB1D8;raw9EB4B0;raw103B490..4B8 | Copy originaldescriptor's five words before allocation90. Result pointerr4 has table103B498, calculated atactualADD9EB1C0+8+6502C8+8.103B490/494 are zero metadata, not the published table. Allocation result is distinct from original descriptor and all nine positively supplied audio-node products. |
| AD625C candidate field |9EB158..9EB250;DA498B | Save originaldescriptorword0 inlocalFP-5C; reload itr1 at9EB20C; store atnewdevice34 at9EB250. Same descriptor words are copied todevice38..48. Thus this candidate is device-format/default descriptor publication, not parent34 or bus38; no pointer lookup, new ancestry or normalized routing value inferred. Actual device build/format/admission/listener decisions remain DA498/DF; no new device body descent. |

AX624's provisional table address was corrected from metadata103B490 to published103B498; its resolver-global calculation was independently corrected to108DAE8. Primary literal words/PC arithmetic are explicit so these labels cannot stand in for constructor identity. Research-only corrections, no production behavior changes.

104/131 remain PARTIAL for their original attachment writer/runtime-input proof. AW623's9FBB30 output alias stays UNKNOWN; no positivecaller from navigation, no unreachable claim. This pass removes the specific9EB250 ambiguity using the actual caller/descriptor path, rather than tracing unrelated device internals. out of scope: allocation/backing implementation, resolver string storage, excluded coordinate payload and per-sample DSP. Counts100 native+8 scope CHECKED/32 PARTIAL/140;140 AUDIBLE/0 INTERNAL.
