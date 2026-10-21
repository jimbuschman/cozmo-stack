# R-M8-FEED-SURVIVOR — one concrete cancellation survivor

Independent read-only job; answer `research/20261021-m8-feeding-clear-survivor.md`.
Read current M8-024 and search research/inventory first, especially adopted OV111,
the M2-022 local5A subscriber census and B5 of
`research/20261021-m8-actionlist-completion-settling.md`.

Narrow question: can the supported FeedingEat result-insensitive completion
callback leave an action alive after Robot AbortAll and owner teardown, to be
destroyed by ActionList.Clear? Follow one concrete state2 candidate through tag,
queue key, current versus pending position, saved successor and owner lifetime.
AbortAll is 0x00511120; current deletion precedes pending-front capture at
0x0053E6B6..C0, whereas last pending deletion follows saved-successor capture at
0x0053FA86..FAB2. A callback which queues is not itself proof of survival.

Establish reachable prerequisites and the emitted API/robot effects and their
order if it survives. If it does not, explain the exact deletion/lifetime gate.
Bound the conclusion to this candidate; do not infer absence of every survivor,
invent a synthetic production callback, or mandate a native container copy.
Reuse known Clear mechanics. Do not expand into the entire behavior census or
typed animation completion payload (already answered independently).

Return native citations, current record title/status/evidence, production path,
remaining unknowns and an independent regression scenario if supported. Use
`decomp/libcozmoEngine/` for navigation and actual instructions as evidence.
Research files only; no implementation/tests, policy decisions, manifest edits,
approval, hardware or settlement. Publish under the research lane rules.
