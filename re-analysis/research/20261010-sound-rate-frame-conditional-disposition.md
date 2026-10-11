# Q14 rate/frame proof: conditional alias candidate rejection

Checkpoint772; scope guard reread. Same engine hash; `20261010-sound-rate-frame-conditional-expansion.py/.txt` adds retained skipped-predicate states for selected nonbranch instructions to771. Navigation318 events include all314 previous events plus four apparent sites98D524/528/540/55C.281 local windows hit4096state cap, so this is explicitly incomplete and cannot establish absence.

Primary `-conditional-native.txt`, `-conditional-slots-native.txt` rejects the added setting identity. CMP98D50C sets flags. NE sequence loads6B2B2C at98D510, then98D514+8+6B2B2C=1040048. EQ sequence loads6B2B1C at98D51C, then98D524+8+6B2B1C=1040048. Intervening conditional LDR/STR instructions do not change flags. Both actual cases select rawslot1040048=1052418, a pool-setting value consumed inR8, not frame-global1052440.

The forked navigation invented the impossible combination NE literal retained while EQ literal skipped but EQ indexed load executed. That produces1040058=1052440 and the four new events. Flags cannot satisfy that sequence. Reject those candidates, not the native source. Also985CAC immediately overwrites incomingR3 atits firstLDR before any use. A7A988 zeroR1 returns1 withoutR3 use; nonzeroR1 overwritesR3 atA7A990 before use. No formal global output-pointer input is supplied at98D540/55C even under the false navigation value.

The real pool/free and lifetime recipients are existing contracts; no allocator body or new callback family is traced. Next useful tooling correction is to retain shared predicate constraints across unchanged flags, rather than independently toggling each conditional instruction. This is a navigation limitation, not a native gap or control-value equivalence.

043/092/099/120/124/130 remain PARTIAL for original unrestricted writer/alias and zero/provider questions.107 native+8 scope CHECKED/25 PARTIAL;140 AUDIBLE/0 INTERNAL unchanged. Research only.

out of scope: pool/free backing and sample DSP; retain only actual pool identity and nonconsumed pointer boundary.
