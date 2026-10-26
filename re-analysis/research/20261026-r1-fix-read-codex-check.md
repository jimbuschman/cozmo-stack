# R1 FIX independent read check

PASS — bounded subscriber-only host-diagnostic correction. No engine extraction or fidelity status change is needed. This is a read/scope verdict, not an implementation or test verdict.

Current origin/main REVIEW.md at 4892b66d says R1 is **FIX**: “`_trace` is locked, but `Step` is invoked outside the lock, and two subscribers append to a plain `List` with no lock”. It identifies ManipTool Say and Reactions Line, calls the race blocking, and explicitly permits “locking the subscribers”. My earlier R1 review missed these plain-list consumers; approval of completion-thread diagnostics did not establish subscriber thread safety.

## Checked production paths

- FaceBehaviors.cs ActionBehavior.RunAction (20–45): completed task continuation logs full fault/cancellation before selecting the supplied fallback and posting completion. SteppedBehavior.Log (1179–1182) locks trace append, then invokes Step outside the state gate.
- ManipTool.cs: Step subscriptions (68, 137, 149, 205) feed Say (32), which appends to shared List<string> (31). Say also receives ordinary control-flow lines and vision/world/manip/follower/robot.Message callbacks. Finish passes the live list into WriteAcceptance (227).
- Reactions.cs: every registered SteppedBehavior shares Line (175–179) through Step (184); manager and sensor callbacks also call Line (187–192). The List<object> is shared (174), and acceptance currently passes it live (278).
- M13DriveOffHw.cs Event (442) already enqueues into ConcurrentQueue; this is not another plain-list sink requiring this correction.
- ManipulationBehaviors.cs (446–447) forwards child Step into parent Log. Generic callback locking would add unnecessary cross-behavior lock-order/reentrancy concerns.

## Approved build boundary

Use the same synchronized journal mechanism for each tool's shared sink. Serialize every append and associated console output through that sink; obtain an independent array/snapshot under the same synchronization before acceptance serialization. All writers must use the synchronized route, including non-Step producers. Do not hold its lock across acceptance file writing or arbitrary behavior callbacks.

No generic SteppedBehavior.Log dispatch lock, scheduler refactor, callback catch, or FaceBehaviors continuation change is required. A per-instance behavior lock alone cannot protect a journal shared by multiple behaviors and other event producers. Preserve late failure visibility after stop/replacement, successful result, supplied fallback, diagnostic-before-fallback order, and existing Post/tick/stale-callback semantics. This remains host debugging, with no engine event-thread guarantee.

Tests must attach the production shared journal route to a minimal behavior invoking actual RunAction/continuation/Post/Update. Complete fault and cancellation from a worker while ordinary tool lines write to the same journal, verify retained full diagnostics and safe snapshots, and retain tick/stale-guard coverage. Release all deterministic barriers in finally and await workers before fixture/event cleanup; use no timing sleeps. Concurrent ordering between independent producers is not predetermined.

No code or tests changed or executed for this read check. Actual selected model/reasoning telemetry is unobservable.
