# Q14-024 shared-context provider recipients, checkpoint841

Guard reread before each step. Scope question: can the recipient choose a descriptor ID or change a parameter, lifecycle or routing decision? Provider and callback interfaces qualify; backing and sample bodies do not.

Primary: `20261011-sound-shared-context-provider-native.txt`, engine SHA256 `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`. Immediate-branch and aligned raw-pointer navigation is reproducible in `20261011-sound-shared-context-provider-callers.py/.json`; neither is an indirect/computed/relative-call absence proof. Existing rows PR1–4, DA498, LDEF292/LDRV293/LF291, VF6 and IC4 supply the prior concrete products and publication/Init contracts.

| Positive site | Exact disposition |
| --- | --- |
| 9CBC00 | PR4 registration callback: entry20 nonnull calls99F148, then invokes entry20 with R0=returned context, R1=1, R2=entry24. Return ignored; next entry reloaded afterward. Native4DD90C Hijack record (type3/company12C/ID1) and4DD97C WavePortal record (type2/company12C/ID64) each explicitly store zero to20/24 and18/1C. Therefore these two constructor-defined records skip this callback. This does not exclude another record, later mutation, or loader-supplied callback. |
| 9EC634 | Actual tail to99F148. Raw103B4A0 is slot8 of actual device table103B498, published by DA498. Receiver is the device object, not the singleton and not a descriptor. Returned context is the same provider. |
| A51710 | Actual tail to99F148. Raw103C35C/103C4D4 are main-table slot6C of line tables103C2F0/103C468, whose actual constructor publications are LDEF292/LDRV293/LF291. This is a context-return interface, not a descriptor lookup itself. |
| A6C2C4 | Actual tail to99F148. Raw103D4F8 is slot8 of wrapper context103D4F0. NativeA6C22C publishes that exact table: PC A6C238+8+5D1260+50=103D4F0; stores slot4, voice14 and zeros8/C/10. VF6 supplies its selected wrapper Init recipient. |
| A6C2C0 | Actual tail to99F148. Raw103D540 is slot8 of bus FX context103D538. NativeA6C25C publishes that exact table: PC A6C280+8+5D1218+98=103D538; stores slot4, bus14, zero8/C/10, copies supplied three words to18/1C/20. IC4 supplies this concrete product and selected Init route. |
| A7900C/A79014 | Reuse760 actual source-provider/thunk; A79010 first adjusts R0 by14, then tail-callsA7900C. No new descriptor use inferred from this already-paid route. |
| Apparent ThumbA790CA | False mixed-mode navigation candidate: actual ARM A790C8 is indexed LDR, A790CC loads allocator receiver and A790D0 calls free. out of scope: provider workspace backing release. |

99F148 returns actual108D8D0 context with table101BFA0; original760 owns construction/RNG54. None of these return-only interfaces supplies a descriptor ID or calls context4C/50. The two positively constructed callback-null records establish only their own callback gate; do not turn them into an all-callback or all-descriptor unreachability claim.

Q14-024 remains PARTIAL: exact admitted caller/input of context4C=9A6CE4→A3B9AC and50=9A6CDC→A3BB94, plus semantic recipient of the trailing descriptor fields, remains UNKNOWN. Next decision step is a positive plug-in/context consumer that supplies the requested ID and uses the returned fields; do not repeat the seven paid provider branches or their storage bodies. No new obligation family and no extra completion credit.

out of scope: allocation, registration-table backing and provider workspace release internals; per-sample DSP arithmetic. No production, fidelity, inventory, PROJECT_STATE, hardware or Q15 changes.117 CHECKED (109 native+8 scope)/23 PARTIAL/0 NOT DONE;140 AUDIBLE/0 INTERNAL.
