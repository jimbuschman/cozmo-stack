# R-M8-TEARDOWN-CLOCK — post-removal timer readers

Request: `requests/20261021-m8-teardown-timer-readers.md`. Examined HEADd257817c. Independent research pending checking; no production or fidelity edits.

## Coverage

| Step | Source | Result / boundary |
| --- | --- | --- |
| Engine removes timer before robots | 0x004EC6B4, context release0x004EC702; adopted context/RemoveRobots path | Confirmed engine-destruction boundary; not ordinary disconnect |
| Recreated clock | getInstance0x0084BB9C..0x0084BBD6; removeInstance0x0084BBE4..0x0084BC04; timestamp0x0084BCC0..0x0084BCD4 | Fresh lookup after removal allocates zero time fields |
| Concrete destructor reader | Hiccup D1 0x0061024C..0x00610292, reached through binding vector0x005A47FE | Concrete transitive robot/behavior destructor reader |
| Timer-dependent consequence | unsigned test0x00610264/266, SendDasEvent0x006102B8..0x00610398 | Diagnostic event gate/payload only in this bounded routine; no RobotHiccupsChanged call |
| Old alias | D1 and SendDasEvent call getInstance each time | Neither reader retains the removed timer; universal old-alias census UNKNOWN |
| Other concrete state write | FeedingEat transition0x005D62F6..0x005D6308, earlier survivor report | Conditional local5A teardown path stores duration+fresh-now; future consumption before removal UNKNOWN |
| C# reach | CozmoEngine.Dispose/Timer; RobotLifetime; ReactionTable and ReactionStrategies | Timer retained; native Hiccup strategy/owner still absent |
| Minimum correction/oracle | Named reader and real lifecycle, below | No blanket clock reset justified as a supported robot/API fix |

## Finding

A concrete post-removal destructor clock reader exists: **ReactionTriggerStrategyHiccup::~ReactionTriggerStrategyHiccup**. This closes B4's “no concrete transitive reader established” boundary for that strategy. It does not by itself establish changed robot traffic, API delivery/readiness, or downstream state in scope1–4. Its clock-dependent branch only calls SendDasEvent; the unconditional global clear and base teardown do not depend on time.

There is also a conditional teardown completion path that writes clock-dependent FeedingEat/controller state (the earlier survivor answer). It is a useful separate lead, but this report does not promote an expiring object's deadline into a demonstrated later robot/API effect. Therefore a supported observable failure caused by keeping the engine clock remains **UNKNOWN**, rather than an invented justification for resetting all clocks/tests.

## Native path and instructions

Reuse the adopted Robot/context destruction order and B4 from `20261021-m8-actionlist-completion-settling.md`. Fresh read0x004EC6A0..0x004EC712 confirms removeInstance call0x004EC6B4, followed by member cleanup, then context virtual destruction0x004EC702. Context destroys RobotManager's robots; ~Robot releases BehaviorManager at its adopted owner step. The strategy binding vector's destructor at0x005A47D8..0x005A4812 walks entries in reverse, releases the behavior shared control first, then invokes strategy deleting destructor at0x005A47FE. The Hiccup strategy's D1 and this ownership edge were already checked in `overnight/20261014-ov19-m8-016-check.md`, D1; reused here, not a new whole teardown census.

Timer:
- removeInstance loads the singleton, calls its deleting destructor if present (0x0084BBF0..F4), then writes null to the singleton (0x0084BC02). A retained raw pointer would be dangling, not a valid alternate live clock. No such use is found in the named readers.
- getInstance returns the existing singleton, or allocates0x28 bytes and initializes time words to zero at0x0084BBB8/BBBE/BBC6/BBCA, publishes it at0x0084BBD0.
- GetCurrentTimeStamp loads the double at+8, multiplies by1000.0 (double literal at0x0084BCD8), converts unsigned32 and returns (0x0084BCC4..D4). Seconds getter0x0084BCA8 loads+0x10.
- Recreated time remains zero until UpdateTime. This answer does not census every intervening destructor/callback for a possible UpdateTime call; zero at allocation is established, zero at every arbitrary later teardown instruction is not assumed globally.

Hiccup destructor:
1. Installs its vtable.
2. 0x0061025A calls getInstance;0x0061025E calls GetCurrentTimeStamp. There is no this-member saved timer pointer.
3. Loads threshold+0x2C at0x00610262; compares unsigned now to threshold at0x00610264; **bls**0x00610266 skips the event when now<=threshold.
4. Otherwise calls SendDasEvent(false) at0x0061026C.
5. Clears global _this at0x00610278 regardless of the branch, then tail-calls IReactionTriggerStrategy base0x0060B6F4 at0x00610280. The unwind tail through0x00610292 also invokes the base.

Thus for a fresh clock still at0 and any u32 threshold, the event is skipped. With an old retained clock T>threshold the event would be emitted. T<=threshold gives no diagnostic difference. No assumed initial threshold or active strategy state is needed to express the predicate; establishing an actual live instance/state is still its producer's obligation.

SendDasEvent:
- Entire normal/unwind body0x006102B8..0x00610398 freshly read; trailing0x00610398..0x006103D4 is literal/string data.
- Calls getInstance/timestamp again at0x006102C0/C4. Loads start+0x54 at0x006102C8. Zero start yields elapsed0; nonzero start yields wrapping u32 now-start at0x006102D0.
- Converts elapsed to string and builds a single $data entry (literal0x00610398).
- False parameter selects SELF_CURED at0x006103A0; true selects PLAYER_CURED0x006103AC.
- Calls sEventF at0x00610326, key robot.hiccups.ended0x006103BC, format %s0x006103D0. Remaining code frees temporary strings/vector.
- There is no CureHiccups, ResetHiccups, whiteboard clear, RobotHiccupsChanged emission or robot send in this body. Those different functions must not be inferred from this event's name. Base teardown is unconditional, so its effects are not selected by the timer predicate.

This is diagnostic behavior under the operator's logging rule. It is evidence for the bounded diagnostic branch, not a blocking unsupported robot/API behavior. No policy exclusion is newly made.

## Conditional state reader — distinct from the diagnostic

Reuse the complete pending-P/local5A witness and its conditions from `20261021-m8-feeding-clear-survivor.md`. During ~Robot.AbortAll, deleting pending placing action P can synchronously invoke running FeedingEat's result-insensitive callback. On the valid animation/controller branch it enters TransitionToEating before behavior owners release.

Fresh0x005D62F6..0x005D633E:
- getInstance0x005D62F6;
- seconds getter0x005D62FC;
- binary32 duration+now0x005D6304;
- store behavior deadline+0x124 at0x005D6308;
- allocate/queue eating action afterwards, without a tick.

The previous report also establishes StartedEating→controller timestamp/state. Those writes can differ between zero and retained time. But their consumers are future updates, and the traced removal performs no further normal behavior/controller tick before the owners die; E remains uninitialized and is cleared. A retained listener/controller alias surviving teardown, a callback that consults the new deadline synchronously, or a later emitted timestamp-dependent report is **UNKNOWN** here. Do not infer a wire change from a numerical deadline difference in a dying owner. Exact shipped animation/controller prerequisites remain those stated in the earlier report, not fabricated successful inputs.

## Current production reach and ownership

- `cozmo-stack/src/Cozmo.Robot/CozmoEngine.cs` Timer is one get-only BaseStationTimer. Dispose stops the runner and calls Robots.RemoveRobots without resetting/replacing it. Ordinary clock delegates still capture/read that object.
- `Actions/IActionRunner.cs` EngineClockSeconds uses Watcher.ClockSeconds; this is a normal tick clock route, not evidence of a base-destructor timer read. Previous direct action/streamer scan remains bounded; no new universal absence assertion.
- `Behavior/Behaviors.cs` reaction registration explicitly omits Hiccup strategy. The Hiccup PlayAnim behavior is not ReactionTriggerStrategyHiccup. `RobotLifetime.cs` still records absent native manager/BSM finalization owners. There is no supported production Hiccup destructor recipient whose observed behavior can currently be fixed by resetting Timer.
- The previous FeedingEat answer records the absent full FeedingEat/ActivityFeeding owner/factory/local-completion integration. Host scripted requeue tests do not close that source/production boundary.

Current record ownership:
- M1-044, EXACT_SOURCE, “RemoveRobot ordered lifetime interfaces, owner nulling and post-destruction membership cleanup”: owns ordered robot removal, not a claim that its ancestor's timer reset proves every higher-layer effect.
- M8-015: runner/queue teardown and its transitive local completion joins.
- M8-016 / M8-023: behavior-manager/activity/strategy owner destruction.
- **M10-016**, IMPLEMENTATION_GAP: “ReactionTriggerStrategyHiccup destructor: robot.hiccups.ended SELF_CURED DAS event when hiccups are active, and the _this clear”. Its current evidence cites OV19 D1,0x0061024C..0x00610280, SendDasEvent0x006102B8..0x0061035C and binding-vector0x005A47FE. Its unresolved explicitly says production Hiccup strategy, source-backed threshold/start/global lifetime and binding ownership are absent. This report confirms that limitation and gives the clock boundary; it does not settle the record.
- Engine singleton lifetime remains the B4 ancestor issue. It must not be disguised as an action-timeout regression or applied to ordinary RemoveRobot.

## Smallest disposition and oracle

No robot/API correction is established by the Hiccup diagnostic alone. When the strategy's real ctor/state writers and owner are adopted/built, supply the appropriate engine-destruction clock lifetime at that boundary. Do not construct an orphan destructor observer with guessed threshold/start or reset every test's time. Existing logging wording/level differences alone do not block acceptance.

Independent oracle, not run:
1. Native emulator with real strategy constructor/writers and destruction owner; use nonzero engine time and known threshold/start reached through actual setters.
2. Compare engine destruction (remove singleton first) against ordinary robot removal (singleton retained). Record the actual getInstance pointer/value at the reader and whether UpdateTime intervenes.
3. Capture diagnostic gate and elapsed text, unconditional global clear, and actual robot/API outputs separately. At fresh-now0 expect no event for all u32 thresholds; with retained T>threshold expect SELF_CURED elapsed as above.
4. For FeedingEat, only extend the existing real production witness if a retained consumer is identified. Demonstrate its reader before claiming a deadline changes traffic or API state.

Remaining UNKNOWNs: intervening UpdateTime callouts outside the bounded chain; any other retained old-timer raw alias (M1-034 if actually dangling); timer-dependent behavior in other destructor/local subscribers; survival/consumption of FeedingEat deadline/controller timestamps; final host delivery after dispatcher retirement. None requires a whole teardown census for this request, and none is filled by a blanket reset.

Research only. No tests/hardware run, no implementation change, no manifest impact or settlement.
