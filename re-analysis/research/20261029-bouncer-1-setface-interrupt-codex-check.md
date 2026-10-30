# BOUNCER-1 SetFace interrupt predicate — independent check

2026-10-29. Bounded check of `20261029-bouncer-1-setface-interrupt-unknown.md`; same reviewer, default model/effort requested, backend unobserved. `known.py` preceded instruction reads. No code, official record, .NET, git mutation or hardware.

**PASS.** In `libcozmoEngine.so`, SetFaceAction's installed effective vtable slot +0x14 at 0x01022DFC contains relocated word 0x0052B0B3; IAction and IActionRunner corresponding slots 0x010214BC and 0x01021498 contain the same word. Thumb code at 0x0052B0B2 is `movs r0,#0; bx lr`. IActionRunner::Interrupt 0x00540254..0x00540262 loads vptr+0x14, calls it, and returns 0 unless the result equals 1. Thus this outer action's predicate is false; cancellation is separate and no full lifecycle claim follows.

The supplement's literal M8-015 title/status/evidence/unresolved quotation matches the current manifest; it remains IMPLEMENTATION_GAP. No native source correction or settlement is made.
