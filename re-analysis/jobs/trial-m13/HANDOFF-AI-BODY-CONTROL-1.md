# AI-BODY-CONTROL-1

## Scope and source checks

The host API supplies one external head-angle command. The queue position
is an explicit host choice. Existing engine owners keep personality,
reaction, safety and idle-life decisions.

- 0x00527A74–0x00527A8C sets the app tag, passes the message queue
  position, and passes zero retries. AtEnd (4) is dispatched through
  0x0053DB04 and 0x0053E16C–0x0053E248. External tags use the app range
  1 through 1,000,000 (Unity Robot.cs 586, 800, 1451 and native 0x00540098).
- The head factory 0x0052ABE0–0x0052AC24 and constructor 0x00547EAC set
  the same parameters, action type 0x12 and head track. The existing
  action owns the matching acknowledgement, stop checks and timeout.
  Head destructor 0x00548478 removes its own subscription. Base
  0x00541084–0x00541238 stops only a moving track with the matching
  lock tag, then unlocks before watcher delivery.
- Queue admission observes native external-action disable 0x0053D94C.
  The first full state gates native execution at 0x00513C5C. Behavior
  manager updates at 0x00513EE6 before ActionList at 0x005140BC.
  AtEnd does not confer a lease. A reaction or ordinary engine action
  can supersede a queued or active command according to native rules.
- The tag cancel path 0x00528CF0→0x0053E704 checks the current action
  and pending list. A pending deletion keeps native NOT_STARTED
  0x02000001. A current cancellation uses 0x02000000. Native completion
  variant 6 is empty for head type 0x12 (TBB at 0x0053FEEC→0x0053FF0E).
  0x0053FA4C–0x0053FA66 snapshots the completion before teardown;
  0x0053FAAA–0x0053FAB2 sends it after unlink.
- DriveStraight is outside this slice. Its pending-action destructor
  0x00547530–0x00547558 may abort a shared active path unrelated to
  the queued runner. No claim of safe scoped cancellation was made.

Reader requested gpt-6.1-sol medium. Builder and reviewer use the
configured default. Actual backends unobserved. Independent review passed after scoping completion variant and guarding reentrant and stale cancellation.

## Build and test reach

CozmoMotion.QueueAiHeadAngleAsync in Motion.cs supplies only a head angle.
It uses a finite-input check, ready and validated original robot owner, and
the existing head action. The result carries admission and the exact
native terminal value, with owner retirement captured when the runner
is destroyed. The async result crosses the engine owner barrier after
native queue unlink. Existing motion calls retain their completion behavior.

ActionList.cs guards AtEnd admission and confirms the exact runner before
a cancellation reaches the native tag path. IActionRunner.cs reserves
the tag atomically so another engine cannot win between lookup and assignment.

AiHeadControlTests.cs uses canonical fake transport and checks head wire and
ack, 5A completion variant, cancellation before and after start, native
reaction refusal, owner replacement during a clipping log, tag reuse,
and original-owner teardown. The final action, motion and AI suite passed
149/149. There is no new arbitration layer or idle restart. Existing queue
and behavior owners retain control.

MISSING: other motor/action requests, full AI observation facade, and a general
ownership contract for shared-path cancellation. This scope is the single
head command, not an SDK or a broader movement policy. No status, approval,
or hardware claim changes.

Batch full gate passed at bd4c818e with 7821/7821 tests and zero skips. READY FOR CLAUDE.
