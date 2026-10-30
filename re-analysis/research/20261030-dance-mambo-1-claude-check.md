# DANCE-MAMBO-1: Claude check (manager, 2026-10-30)

**Verdict: PASS. Merge pending.** Handoff 29dc3a4f (full gate 6972 PASS). **Nothing settled:** no test reaches Dance
through production, and no record yet owns D1-D12. Verifier disassembly: factory 0x0059CA34..4E; ctor; vtable address
point 0x0102A0E4; lock table 0xC6F1C8 {1,2,3,8,10,20}; Init 0x005ED460..700; candidates 0xC6F1B0; the action tuple
0x005C0174..90; listeners 0x005C0552; fade 0x005EDFCA; StopAllAnims 0x00638D58.

## Queued

1. `NativeDanceBehavior.cs:90`: the MISSING says the request caller is UNKNOWN. The engine ingress is known
   (HandleExecuteBehavior 0x0060AA58, tag 0x94); only the sender, the app, is absent. Correct the text.
2. The M7-014 `unresolved` still lists Dance among the arbiter-wide classes (manager, with the record work).
3. Tests: the `words >= 202` lower bound can't detect a cap above 100; assert the runnable gates, the six-lock list and
   the action tuple.
4. LoopBoundOverflow 0x005ED8C8: diagnostic only, once per process, and the native error flag isn't stored.
5. The proposed lifecycle record goes to the operator.
