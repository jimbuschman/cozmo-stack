# MARKER-WITHHOLD-PER-MARKER-1 merged: no effect (manager, 2026-11-14)

Worker commit a2254d09 (records only). The result is recorded in M11-013's `unresolved`.

**The difference:** ObservableObject::SetMarkerAsObserved skips only the KnownMarker whose GetWithRespectTo(camera)
fails (`blx 0x4A40FC` at 0x00876E5E; `cmp r0,#1`; `bne.w 0x00876F76` at 0x00876E64, re-read 2026-11-13). The stack
withholds the whole selection when any marker fails.

**Why it has no effect:** GetWithRespectTo fails only when the two poses have different roots. An object's KnownMarkers
are all parented to the object's own pose, so they share one root. Against one camera, the check therefore passes or
fails for every marker of that object together, and skipping each failed marker is the same as withholding the whole
selection. A mixed case would need markers of one object on different roots, which the engine's object construction
never produces (one KnownMarker per code, all children of the object pose). Only the log differs when all fail
(the engine's Warning versus the stack's visible MISSING); log wording never blocks.

**Verdict: PASS.** Nothing built; no status changed; no record settled.

**Open, not taken up now:** the worker asks whether the one-camera-per-call interface (BlockWorld.cs:2002) needs a row.
Not checked against the binary here; it goes to the next M11-013 job rather than being answered from memory.
