# ChatGPT queue 4: Claude check (manager, 2026-10-31)

**Verdict: the cleanest report so far. No correction changes behaviour; the corrections are wrong owners and labels, and several
UNKNOWNs closed below. Nothing settled; no status changed.** The report is `research/20261031-queue-4-chatgpt-extraction.md`. Use this
note where the two differ. Seven items were marked Partial and item 8 Done. Checks targeted conditions, owners, keys, open points and
item 8's Done. A row not named here was not re-read.

## Corrections

**1. Item 2 census and item 6 row 6.04: NeedsManager is not at Robot +0x34.** The code is `ldr r0,[robot]` then `ldr r0,[r0,#0x34]`
(0x005AE35C..0x005AE360, 0x006136E0..0x006136E4), so it is **[Robot+0]+0x34**. Robot +0x34 is BlockWorld, as row 1.10 says. "RobotImpl"
is not a type in the binary.

**2. Item 2 census: IBehavior +0xA1 is the running flag,** not "readiness/restriction" (`Behavior %s is already running`, queue 3).
So row 2.26 reads: keep the returned behaviour if it is already running.

**3. Item 2 census: the FreeplayDataTracker row has the wrong owner.** +0x2C is an AIComponent offset: [Robot+0x264]+0x2C is the
tracker.

**4. Item 5: Tree offsets.** +0x20 and +0x48 are set **sizes**, not roots. The roots are +0x1C and +0x44, as the report's own rows 5.12
and 5.18 load them. The empty test gives the same result.

**5. Item 8, Done with one gap:** the rows jump from 0x0057BCC6 to 0x0057BD36. The skipped code warns when the removed entry was not
the top of the stack (`cmp sl,r5` at 0x0057BCC8, sWarningF at 0x0057BD02). It is log only and M5 A30 already has it. The report's
outcomes hold and agree with M5 A30 and M5-027. In "top animation", read the **new** top; 0x23F is Count.

## Closed beyond the report

- **Item 1, names:** family 1 is Block and family 2 is LightCube. `0x004E02B2` is libc++ string init, not an unknown operation.
- **Item 2, the retry is bounded: at most one.** Both `b 0x005AE40C` (0x005AE708, 0x005AE762) sit after the keep path, and all five
  branches into the keep path (0x005AE2C8, 0x005AE308, 0x005AE358, 0x005AE368, 0x005AE37C) come before 0x005AE40C. Selection never
  re-enters it. This closes the report's UNKNOWN 4.
- **Item 2, names (M15-freeplay rows 20-21):** BehaviorManager +0x58 is the current spark, +0x60 the desired spark, +0x65 the
  spark-reselected flag. Row 2.06 is therefore: current spark is none and desired spark is set.
- **Item 3, the strategy arguments:** IActivity +0x54 is the selection time and +0x58 the deselection time (queue 3). So the calls
  are WantsToStart(robot, deselected, selected) and WantsToEnd(robot, selected).
- **Item 4, the queue 3 open point closes: the gate is live.** BehaviorVisitInterestingEdge's ctor sets process 1,
  CalculateInterestingRegions (0x005E5FB2..0x005E5FB4, in `C2`; string checked), and one shipped behaviour has that class. A byte sweep for
  `str`/`str.w` to +0x1C found 21 stores in Behavior functions besides the stack. Of those read, 0x005BBC08 and 0x005E5FB4 are the only
  writes to the behaviour itself; 0x005EECD6 (BehaviorSinging) writes a stack-loaded object and 0x005E2292 a config struct. The remaining
  register stores were not read individually, so the list is not proven complete.
- **Item 5:** no shipped config uses `strategyType: Generic`, and the ctor parses nothing, so a plain Generic from JSON would never run.
  The class is used as ObstacleDetected's base, which installs +0x68 (queue 3 note), and by ReactionTriggerStrategyGeneric.
- **Item 6, the rule:** bracket 3 is **Critical**. ExpressNeedsTransition wants to run when the need is at Critical and it is not
  already SevereNeedsComponent +0x14. The getter is `NeedsManager::GetCurNeedsStateMutable` (0x004A5740); its body is unread.
- **Item 7:**
  - The StartActing callback at 0x005EFEBC is `TransitionToS1_FaceOnLeft`: `add r0,pc` at 0x005EFEB6 gives GOT 0x0103F488 by relocation.
  - Slot +0x20 is `ICompoundAction::AddAction(IActionRunner*, bool, bool)`, by relocation.
  - Checked: every `_rad` key really is multiplied by pi/180 (0x005EFA26). This looks like an original quirk; reproduce it.
- **Item 8, the queue 3 open point closes:** OnDeselected's plain-name removal never matches `Activity_<name>`, so it only warns (no
  match), and SmartRemoveIdleAnimation does the real removal. It has no effect apart from the log.

## Checked and matching

- **Item 1:** F and O and the four outcomes (0x005ADF96..0x005AE0AA). `F` is stored at sp+4 through the `b` at 0x005ADFA8.
- **Item 2:** head, row 2.06, keep path. The branch census above agrees with the report's outcomes, and M15-freeplay rows 20-21 agree.
- **Item 6:** the rule's logic and `need`/`needBracket` shipped (3 InNeedsBracket, 1 ExpressNeedsTransition).
- **Item 7:** the parse functions (ParseBool, ParseFloat, ParseUint8).
- **Item 8:** the 1000-entry scan bound, string `Activity_` at 0x00C49CD0.
- **Not re-read:** items 3 and 5 row by row, item 7's other keys.

## Still open

- **ActivityFreeplay +0x68..+0x6B:** which config keys fill them. Not written in the ctor; CreateFromConfig 0x005AD478 is the next place.
- IActivity::GetDesiredActiveBehavior 0x005B387C; the concrete IActivityStrategy classes behind WantsToStart and WantsToEnd.
- SwitchToRequestedSpark 0x005A4198; SetFreeplayPauseFlag; GetCurNeedsStateMutable.

## Method note

All four new rules worked: each gate ended in a stated rule, addresses were virtual, keys came from the key argument, and sampled
quotes began on instruction boundaries. Remaining weaknesses are small: owner labels drift when a chain passes through Robot+0, it left
two relocation lookups as UNKNOWN, and it cannot see CLAD names or the repo's inventory. Claude's own tool also failed again: capstone's
`dis()` stops at inline data, which is why the queue 3 search for +0x1C writers missed a store this report found. Use byte sweeps for
"find all" searches.
