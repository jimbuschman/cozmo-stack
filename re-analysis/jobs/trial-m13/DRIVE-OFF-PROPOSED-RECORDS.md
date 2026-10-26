# Drive-off pilot: record proposals, not official changes

Preparation for the single pilot handoff. Implementation and independent build check are still pending. No official ID, status, evidence, approval or acceptance state is changed here. Claude assigns/settles records only after its external check. The full current record quotations and whole-path inventory are in `research/20261026-driveoff-pilot-read.md`; this file never replaces the manifest.

## New owning record needed: Socialize objective recipient

Proposed scope/title: ActivitySocialize objective subscription, objective4 handling and pre-first-selection undefined-state guard. Do not fold it into M15-059, which owns ActivitySparked.

Primary evidence: ctor005AFD1C leaves phase+94 uninitialized while subscribing at005AFDF6..DFC; recipient005B07C2 reads that field. Under the standing M1-034 rule, skip/log only this pre-initialization recipient unit, preserving prior state. Do not initialize native phase0 by assumption. Real OnSelected005AFF60/62 initializes phase0 before Populate005AFF66. GetDesired005B039A rejects an empty objective map; phase4 is entered only after a chosen map entry005B05B2; matching zero completion changes phase5 at005B08AC/AE. Shipped Socialize objectives exclude4. Deselect005B3658/5C resolves vtable01025690+2C to no-op005A7380 and preserves map/phase; the only repopulation caller resets phase first. Thus objective4 has no mutation in defined initialized states, including deselected/reselected states.

The native subscriber must be accounted for at the actual activity owner's construction/external-interface/subscription lifetime. A semantic unit observing initialization is sufficient for this source-proven objective4 slice; it does not certify other objective values, changed configurations, map/population behavior or the entire Socialize class. Generic host Activity absence is not evidence of no native subscriber. Any unbound actual owner remains explicitly MISSING. Independent pre-code checks are in `research/20261026-driveoff-pilot-read-codex-check.md`.

## App input proposal: explicit host activity admission

AGENTS.md's approved app boundary lets the standalone stack supply the app's inputs. Preserve explicit existing Freeplay/host activity admission; construction of robot-owned managers/configs does not itself authorize selecting Freeplay, clearing GameControl or inventing an app command. Internal composition may change while the same admitted input reaches the native chooser/manager lifecycle.

Record the exact host entry/input and producer in the final handoff once implemented. This is a proposed named compatibility input, not a claimed new native startup default or permission to exclude a behavior/API. Existing behavior-control policies must be reread before attaching this to an official record. Configured activity entry still has to publish real native state: OnSelected005B3142 time, chooser005B314A, unconditional UpdateActivity005B3154; native IDs Feeding2/MeetCozmo6/PutDownDispatch13. Their trigger-absent configs execute no activity driving/idle pushes, and deselection does not clear activity state.

## Public configuration adapter: operator D1 approved stack extension

Operator D1 in REVIEW.md on origin/main5ae4d66a chooses option1 (2026-10-26): retain the WHOLE adapter as a documented stack extension, separate from engine fidelity, and remove BLOCKED.md. Proposed compatibility record/title: Public block-configuration Cache/Stacks/Bases/Pyramids, cached midpoint, FirstSeenSec and ConfigurationSeen are standalone stack extensions, decided by the operator2026-10-26. Record complete actual host all-pairs candidate/key/time/event/snapshot/disappearance semantics in the final handoff; preserve them. This proposal assigns no official ID/status.

The adapter MUST NOT feed native engine state OR ANY behavior decision. In particular migrate the old CubeGameBehaviors.cs846 FirstSeenSec decision to actual BehaviorOnConfigSeen005DB2D4..396 per-type native cache count growth, stored float stamp, dt<4.99999 and abs(stamp-now)<1e-5. Verify ALL source consumers, not just this named example. No fake zero geometry, new throw, live-query timing change or supported-feature removal is authorized. Native cache fidelity does not certify this different host extension as recovered engine behavior.

## Settlement candidates, conditional on the final build check

The source inventory identifies M13-017, M13-040 and M7-026 as potential complete-record candidates after ALL their actual pilot joins pass source/code/production checks. M8-027 is a separate confirmation candidate: its current residual is memory-only, so no allocation/retain-count implementation job is justified. These are not proposed approvals today. Exact final record-wide comparisons and evidence belong in HANDOFF-DRIVE-OFF-PILOT.md.

M13-013's derived contacts-retry action, M12-002/032's other path/planner branches, M7-018's other factories, other objectives/Sparked lifecycle, BuildPyramid callbacks and other vision processors are not completed by their bounded drive-off slices. The final handoff must state exact residuals against CURRENT records instead of silently certifying those whole records. Existing firmware C6/animation feedback uncertainty remains input/hardware provenance; no offline pass turns it into hardware verification. Robot runs never block moving to the next behavior.
