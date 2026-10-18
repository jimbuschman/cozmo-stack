| Job item | Coverage | Checked citation / next check |
| --- | --- | --- |
| 1. App delivery buffer and synchronous local emit | SUPPORTED; dispatch implementation is free within the stated boundary | Manager-adopted `20261017-b-m2m15-2-check.md`, finding 1; `20261014-b-m11-1-check.md`, queued App delivery; Broadcast 0x006625B0..0x0066262A and port buffer 0x0065D8B8..0x0065D992. |
| 2. Every inline site | SUPPORTED for the concrete sites below; MISSING for a claim covering all host callbacks | Three raw publishers plus typed AnimationEvent, AnimationAvailable and AnimationAnnouncementsEnded. The job's checked rows establish the buffering boundary, not the complete source contract for every public event. Recheck sites after prerequisite B-M1-4. |
| 3. Reentrancy/deadlock/order regressions | SUPPORTED; tests still to be implemented | Reviewed 0x5F trigger: `CozmoAnimations.cs`:188-197 inside `AnimationScheduler.AbortLocked`; native Abort 0x0057B3F2..0x0057B414. Test the production delivery path and local wire/state effects, not a queue helper alone. |

Preflight for `re-analysis/jobs/B-DELIV-1.md`, 2026-10-17, against main at 7cee6fcb.
Authority 6: a research handoff for the manager to check, not an implementation or a fidelity promotion.
Only this answer file changes. No code, manifest, inventory, job or status changes; no hardware run.

## Checked basis and narrow native corroboration

The job's finding is explicitly adopted and raised to blocking in
`re-analysis/research/20261017-b-m2m15-2-check.md`, finding 1. Its earlier counterpart is
`re-analysis/research/20261014-b-m11-1-check.md`, queued App delivery, which adopts the E4/E5 handoff issue.
The earlier extraction is `20261013-delocalize-callees.md`, E2-E5, with corrections in its adopted `*-check.md`.

I read the full native ranges 0x00661594..0x006616B2 (DeliverToGame),
0x0065D8B8..0x0065D992 (port write) and 0x006625B0..0x0066262A (rvalue Broadcast), including their error tails:

| Step | Instructions / checked support | Result for this job |
| --- | --- | --- |
| App handoff precedes local emit | Broadcast loads slot +0x48 at 0x006625CA and calls it at 0x006625CE; local dispatch follows at 0x006625FA. Adopted B-M2M15-2 finding 1 and Supported section. | App enqueue first; engine-local delivery remains in place and synchronous. Host handler completion is not a prerequisite for that local emit. |
| Delivery counter and destination handoff | Increment +0xFC at 0x006615C4..0x006615CA; pack and size at 0x006615EE/0x006615F4; destination slot +0x28 at 0x0066165E..0x00661662, with null destination skip and all-destination loop through 0x0066166C. Earlier E3. | Keep the existing publication count/content semantics; do not move the count to host callback completion or multiply it for typed/raw views. |
| Direct port stores bytes rather than invoking app handlers | Lock at 0x0065D8C8; overflow/capacity checks 0x0065D8CC..0x0065D8DA; copy at 0x0065D944, byte count at 0x0065D94C; unlock at 0x0065D950. Earlier E4/E5. | A queued message must retain its publication-time payload. A reference to later-mutated storage would not establish content preservation. |
| Native buffer failure behavior | Existing overflow drops at 0x0065D8D0; insufficient capacity sets overflow at 0x0065D8E2, then logs. Earlier E5. | These phone-port capacities and overflow machinery are not instructions to add a C# dropping policy. AGENTS.md permits different plumbing with the same supported observations. |

The checked rows support an app/engine boundary, not a particular C# queue class, thread,
subscription snapshot rule or drain frequency. The job itself permits end-of-tick or API-owned dispatch.
There is no basis here for claiming an exact native C# thread affinity or callback latency.

## Item 2: current publisher census

Paths below are under `cozmo-stack/src/Cozmo.Robot/`; line numbers are at the revision above.
Searching all C# files for `EngineToGameMessage`, `DeliverAnimationReportToApp` and `DeliverNurture`
finds exactly the three raw host invocation sites below. Reading their callers also exposes the typed sites.

| Site / caller | Current host call and following engine work | Coverage / checked boundary |
| --- | --- | --- |
| `NativeDelocalization.cs`:167-175, called at :99 | Raw 0x32 empty body; then `AnimationMessages.Deliver(0x32,true)` at :101. | SUPPORTED: adopted B-M11-1 queued App delivery; delocalize-callees checked E1-E4. Queue app delivery without deferring local subscribers or moving the delocalization stages. |
| `AnimationEventReport.cs`:23-27; `Animation/CozmoAnimations.cs`:188-197 | Raw 0x5F with four-byte tag; then direct reliable AbortAnimation 0x8D and local 0x5F delivery. Called from AbortLocked under scheduler `_gate` before stream replacement completes. | SUPPORTED: adopted B-M2M15-2 finding 1; M5-023 A22/A23. The robot abort and local action callback must not be postponed with the host callback. |
| `AnimationEventReport.cs`:12-20 | Raw 0x60 from the same helper, then typed `AnimationEvent`, then local 0x60 delivery. | SUPPORTED boundary: `20261013-small-reads-2-check.md`, D5, 0x0054561C..0x0054569E and 0x00545750..0x00545792. Both raw and typed host views need the safe boundary; changing only the helper leaves the typed callback inline. |
| `NurtureRestoration.cs`:142-148 | Raw `EngineToGameMessage`, then local `AnimationMessages.Deliver(tag,true,report)`. Current callers: 0x6E at :32, 0x6D at :61, 0x70 at :116, 0x6F at :123, and announcement End's 0x9A. | SUPPORTED application of the shared boundary; the job does not authorize changing any nurture producer or payload. Raw publications must participate in the same ordering as animation and delocalization. |
| `AnimationAnnouncements.cs`:74-80 (`Available`) | Typed `AnimationAvailable`, then local 0x5D. No raw CLAD output on this branch. | SUPPORTED boundary: M1-055 evidence, adopted `overnight/20261017-ov124-check.md` and batch check. Preserve typed name bytes; adding a raw serializer would exceed this job. |
| `AnimationAnnouncements.cs`:84-90 (`End`) | Typed `AnimationAnnouncementsEnded`, then `DeliverNurture(0x9A,...)`, which currently invokes raw host handlers and local delivery. | SUPPORTED boundary: same checked source, EndOfMessage(0). Treat typed/raw views as views of this publication; do not enqueue the local emit or accidentally count/send two native messages. |
| B-M1-4's future readiness/state/progress publishers | Not present as live producers at this revision; B-DELIV-1 explicitly runs after B-M1-4. | MISSING until predecessor's final code is read. M1-056 evidence names UpdateEngineState at 0x004EDC12..0x004EDC2E and loading report at 0x004ED7C0..0x004ED7E6. Include the resulting host sites in the builder's census. |
| `Animation/CozmoAnimations.cs`:123, :181, :283 (`RobotAnimationSink.AnimationEvent` / `CozmoAnimations.Event`) | An additional public string callback raised from scheduler keyframe work (:1521/:1523), distinct from engine E2G 0x60. | MISSING checked API contract in the two sources named by this job. It is not established as a native Broadcast publication. Name its disposition; do not silently call a search of raw E2G handlers a census of every public host callback. |

Internal hooks and `AnimationMessages.Deliver` are not host app delivery merely because they use C# delegates.
In particular, `InitTimeAnimationAnnouncementsInternal` is an internal join, with public Init-time announcements
excluded under M1-055; the retained unknown tag-only consumer is not permission to turn it into a public queued event.
The manager-adopted OV125/OV126 check retains that consumer as UNKNOWN.

## Item 3: reviewable regression obligations

No tests were run or added by this read-only preflight. The builder's tests must establish:

1. Through a live engine/scheduler trigger, a 0x5F host handler can start another animation after the preceding
   mutation finishes. Assert bounded callbacks and completion, native message order, robot 0x8D output and local
   action-state effects. Merely calling an enqueue helper does not reproduce the reviewed defect.
2. A host handler can wait for another thread performing work that needs scheduler `_gate`, with a bounded timeout
   so a failure is reported instead of hanging the suite. While the host handler is paused, demonstrate that the
   relevant engine work can acquire the gate. Moving dispatch outside just a private buffer lock is insufficient.
3. Interleaved 0x32, 0x5F, 0x60 and announcement/nurture publications retain their publication order. Check typed
   callbacks as well as raw callbacks, and that local emits still happen synchronously at the producer after app
   enqueue. Host dispatch must occur at a boundary safe from the reviewed incomplete mutation; native enqueue
   ordering alone does not establish a total ordering between concurrent app consumption and all local callbacks.
   Payload contents must survive the interval before dispatch.

The job says a blocking host must not stall a tick **under `_gate`**. It also expressly permits an end-of-tick drain:
that does not by itself promise the tick thread continues executing while its own handler blocks. Test lock freedom
and the stated ordering, and report the chosen API scheduling contract instead of inventing an unconditional
native timing guarantee. Drain reentrancy must not expose a still-mutating engine or reorder earlier publications.

## Current records and unresolved boundaries

These are the current manifest claims, read rather than recalled:

- **M5-023, EXACT_SOURCE:** "Abort: AnimationAborted broadcast -> AbortAnimation 0x8D reliable; state cleared; buffer
  kept and flushed by the next no-animation Update unless InitStream drops it; no EndOfAnimation". Evidence includes
  A22 0x0057B3F2..0x0057B426 and A23 0x005276E8..0x0052771A, 0x0052C22E -> 0x00517DDE..0x00517E0A.
- **M8-020, IMPLEMENTATION_GAP:** "PlayAnimationAction: construction, Init, robot/game animation handlers,
  CheckIfDone, animationCompleted, destructor streamer stop". Evidence includes E2G 0x5F callback 0x005457F5 and
  AnimationEvent forward 0x00545750, plus the checked animation-action report.
- **M11-070, IMPLEMENTATION_GAP:** "Delocalize callees: AnyRemainingLocalizableObjects(uint), SetNewPose/SetPose/
  AddVisionOnlyStateToHistory with GetDefaultRobotState, the AddInExistingPose default entry, and VizManager
  (no output)". Evidence cites the delocalize-callees check. Its title is not a separate claim settling all app plumbing.
- **M1-055 and M1-056 remain IMPLEMENTATION_GAP.** Their checked readiness/announcement evidence supplies the
  predecessor's API publications, not an already-built host dispatcher.

No status change follows from this preflight. No direct contradiction was found in the job's buffering requirement.
The bounded implementation is supported; the remaining UNKNOWNs are the post-B-M1-4 site census, the separate
string-event API contract, and host scheduling/subscription/lifetime details not settled by the cited native handoff.
The builder must name these boundaries rather than claiming every public callback or the whole subsystem settled.
