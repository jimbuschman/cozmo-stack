# ChatGPT queue 2 (items 1-8): Claude check (manager, 2026-10-31)

**Verdict: quotes PASS; eight interpretations are wrong or misleading; three requested reads were left open and are closed here.
Nothing settled; no status changed.** The report is `research/20261031-queue-2-chatgpt-extraction.md`. Use this note where the two
differ. Same binary hash. Checks were samples, weighted to conditions, labels and every row marked UNKNOWN. A row not named here was
not re-read.

## Corrections

**1. M8-013, step 4.05: a failed lookup clears the selection.** The report says a failed lookup "does not enter normal behavior
selection". It does. At 0x0060ABD4, r7 is zeroed and control reaches the normal store. The old process is disabled and +0x2C/+0x30
become null. The count at +0x3C was already overwritten before the null test (0x0060AA88, 0x0060AAC8), so a failed lookup also
replaces the run count.

**2. M8-013, step 4.06.** `cmp r4,#0` at 0x0060ABFC tests the new control block for the reference-count increment, not the new
behaviour pointer. The report also misses the real guard: 0x0060ABD6..0x0060ABDA compares old +0x2C with the new pointer. If they are
equal, both SetProcessEnabled calls are skipped.

**3. Item 3, step 3.11: the "incoming code 8" is not an incoming value.** The function is
`HandleMessage<RobotOffTreadsStateChanged>` (0x005AD9F8). r0 is `current activity +0x34`, the running sub-activity's ActivityID. The
incoming field is the byte at message +0, tested at 0x005ADA00. The whole gate is under "Closed reads" below.

**4. Item 2, step 2.09: the filter table address is wrong.** The loop reads r4, and `0x0061685C ldr r4,[pc,#0x2c0]` with
`0x00616870 add r4,pc` gives **0x00C77784, values 2 and 1: LightCube and Block**. The report's 0x00C77770 holds 0x45, 0 and is not the
table. The earlier `20261031-queue-1-8-claude-check.md` passed this address by comparing bytes only; that line is wrong too.

**5. Item 2, step 2.19: the call has two arguments.** `IsRestingAtHeight(f32, f32)` receives r1 = 0.0 and r2 = 20.0
(0x0061851E..0x00618526), so it is not `IsRestingAtHeight(20.0f)`. The callee was not read, so which argument is height and which
tolerance stays open.

**6. Item 2, step 2.06 omits a call.** `0x00616DBE` calls
`PublicStateBroadcaster::NotifyBroadcasterOfConfigurationManagerUpdate`, on Robot +0x40, after UpdateLastConfigCheckBlockPoses and
before the rebuild flag clears. That call can reach the API, so it must be traced.

**7. Items 7 and 8: "robot ready" is wrong throughout.** 0x004A56F8 is `Robot::HasExternalInterface`. Steps 7.07, 7.14 and 8.16 gate
on the external interface being present, not on any robot state. Do not build these as a readiness check.

**8. Item 6, type table.** StrategyGeneric +0x18/+0x20 are not "root and sentinel". The layout is below. The report misses the
callables at +0x28 and +0x50 and the writers of the +0x80 latch. Step 6.13 lists three strategy types; the factory has eight.

**Smaller labels.** Step 1.04's "base initialization" is `BehaviorBuildPyramidBase::ResetMemberVars`. Step 4.08 gives the wrong
address for `blt` (it is 0x0060ADAA; `bne` is 0x0060ADBC).

## Closed reads

### M8-013: chooser

- **Lookup:** Robot +0x44 is the BehaviorManager. The container is a tree walked in ascending key order, so ByExecutableType returns
  the first match in BehaviorID order (0x0059C838..0x0059C86A; IBehavior +0x6C).
- **Constructor:** +0x3C = -1, +0x40 = 0, tags 0x94 then 0x93, and +0x34 = FindBehaviorByID(0xB2), **Wait** (0x0060A984..0x0060A99A).
  So +0x34 is the Wait fallback, and it is set whether or not the external interface exists.
- **SetProcessEnabled:** IBehavior **+0x1C is the EProcess**; 0 means none. Robot +0x264 is the AIComponent, and its +0x08 is the
  analyzer. The request name is the literal `SelectionBSRunnableChooser` (0x0060AF04). OnSelected re-enables the current selection
  (0x0060AF20). OnDeselected (0x0060AF64) was not read.
- **GetDesiredActiveBehavior** runs this for +0x2C, then the same for +0x34, and returns the first result of exactly 1 (or an empty
  pointer if neither gives 1). `running` is IBehavior +0xA1, and +0x40 holds last tick's `running`:

```text
if b is null:    ok = 0, running = 0
elif b+0xA1 != 0: ok = 1, running = 1
else:            ok = IsRunnable(robot), running = 0
if b == [+0x2C]:
    if count == 0: ok = 0                        (latch untouched)
    else:
        if count >= 1 and latch != 0 and not running:
            count -= 1; ok = ok && (count != 0)
        latch = running
```

  So a run is counted when `running` falls, -1 means unlimited, and 0 means do not run. This is a transcription of
  0x0060AD6E..0x0060ADCC and 0x0060ADE2..0x0060AE40, not a reading of intent. IBehavior +0xA1 is named only by its use here.

### AIInformationAnalyzer

**Object (0x18 bytes):** +0x00 map of EProcess to ProcessInfo; **+0x0C vector of BorderRegion**, which the report left unnamed.

**The process body was not read in the report.** `AIInfoAnalysisProcCalculateInterestingRegions` 0x00574FCC clears +0x0C, gets the
memory map from Robot +0x25C (MapComponent::GetCurrentMemoryMapHelper), makes two virtual calls with type arguments 9 then 6 and a
predicate, and appends each non-empty result. The virtual slot, predicates and meaning of 9 and 6 are open.

**Readers and callers, by scan of Robot +0x264 then +0x08:**
- IBehavior::IsRunnableBase 0x005BD7D4: if +0x1C is nonzero, the process must be running. This ties the chooser to runnability: a
  selected behaviour with a process is runnable only because SetProcessEnabled enabled it, or something else did.
- IActivity::OnSelected and OnDeselected (0x005B31C8, 0x005B346A), for an activity-owned process (+0x48).
- BehaviorVisitInterestingEdge: PickGoals reads +0x0C (0x005E686A); InitInternal and StopInternal also use the analyzer.
- AIComponent::Update calls analyzer Update (0x00569F2C).

The scan matches a fixed instruction shape, so it is a lower bound.

### Wants-to-run strategies

- **Base:** +0x04 Robot, +0x08 handle vector, +0x14 strategy type from JSON. WantsToRun 0x006134B4 only forwards to the virtual
  internal call, so the base adds no gate.
- **Factory 0x00614710** has eight types: AlwaysRun, ExpressNeedsTransition, Generic, InNeedsBracket, ObstacleDetected,
  PlacedOnCharger, RobotShaken, RobotPlacedOnSlope. Only three were read.
- **Bracket query:** Robot +0 is the CozmoContext, and context +0x34 is the NeedsManager. Note that Robot +0x34 is BlockWorld: same
  number, different base. The veneer 0x008CC9EC resolves to `NeedsState::IsNeedAtBracket` 0x0069CD80. It calls UpdateCurNeedsBrackets
  first, then returns stored bracket == requested. An absent need takes an error path that was not traced.
- **ExpressNeedsTransition:** AIComponent +0x30 is the SevereNeedsComponent (0x00569B3E), and +0x14 is M15-072's live selected need,
  default 3 (NeedId Count). Bracket 3 is Critical. Wants to run if the need is Critical and +0x14 differs from the need.
- **IsRunnableBase also gates on this:** IBehavior +0x74 must be 3 or equal SevereNeedsComponent +0x14 (0x005BD7E6..0x005BD7F6).
- **StrategyGeneric layout:**

| Offset | Field |
| --- | --- |
| +0x18 / +0x1C / +0x20 | E2G tag set: begin, root, size |
| +0x28, pointer at +0x38 | E2G event filter callable |
| +0x40 / +0x44 / +0x48 | G2E tag set: begin, root, size |
| +0x50, pointer at +0x60 | G2E event filter callable |
| +0x68, pointer at +0x78 | ShouldTrigger callable |
| +0x80 | event latch |

  If either tag set is non-empty, the callable is not used. The result is `callable result | latch`, and the latch clears each call,
  as the report says. AlwaysHandleInternal (0x00613998 E2G, 0x00613ABC G2E) sets the latch when the event's tag is in the set and
  the filter is absent or returns 1. A tag not in the set takes a diagnostic path.

### Inventory

**Write completion: the operator() is 0x0063D974** (vtable 0x0102EF7C +0x18). The report's 0x0063D5B6 is a G2E tag 216 subscription
that ends in AddInventoryAmount, as the report half suspected.

1. **Clears both +0x110 and +0x111**, whatever the result (`strh` 0x0063D984).
2. If NVResult > -1, tail-calls SendInventoryAllToGame (0x0063D992).
3. Otherwise logs an error and sets a global flag; no message.

**Effect:** Update (0x0063CDCC) retries a pending write only when +0x111 is clear and +0x110 is set. A change throttled while a write
is in flight sets +0x110, and completion then clears it. That change is not written until the next change. Reproduce this as read.

**SetInventoryAmount 0x0063CE9C:** if +0x104 is clear, the change is dropped with a debug log. Otherwise it stores
`min(amount, cap)` (no cap if cap is -1), then TryWrite. It sends no message itself.

**All triggers of SendInventoryAllToGame:** read callback (0x0063D7EE, 0x0063D860), write success (0x0063D992), InventoryRequestGet
(0x0063D3A0), and a subscription lambda (0x0063D6B8). NV_NOT_FOUND is the -1 branch.

**AddInventoryAmount callers:** AwardSparks, InventoryRequestAdd, `BehaviorManager::SelectUIRequestGameBehavior` (0x005A33E2, spends),
and BehaviorManager::HandleMessage (0x005A3CCA).

### Unlocks

**Write completion: the operator() is 0x0064CBE8** (vtable 0x0102F678 +0x18). The closure is +0x04 component, +0x08 UnlockId, +0x0C
bool. If NVResult > -1 and HasExternalInterface is 1, it broadcasts **RequestSetUnlockResult {unlockID, unlocked}** through vtable
+0x1C. On failure it logs an error and sets a global flag, with no message and **no rollback**: the unlock stays in memory.

**NotifyGameDefaultUnlocksSet 0x0064C0FC** sends **UnlockedDefaults {defaultUnlocks[]}**, with no HasExternalInterface gate.

**SendUnlockStatus callers:** two lambdas, 0x0064CF58 and 0x0064D05E, and neither is SetUnlock. So a SetUnlock does not send
UnlockStatus. The first follows an NV read into the set; the second follows an assign from defaults. Their entry points were not
identified.

**SetUnlock callers:** RequestSetUnlock, its own loops, and ProcessLevelRewards (0x006995CA).

**Default set check:** the per-ID value for id 2 is membership in the default set.

### Freeplay

- **Keys** match offsets: `faceAndCubeActivityName` +0x68, `faceOnlyActivityName` +0x69, `cubeOnlyActivityName` +0x6A,
  `noFaceNoCubeActivityName` +0x6B, each through ActivityIDFromString. Shipped: Socialize, Socialize, PlayAlone, Hiking
  (`activities_config.json:116-119`).
- **Face input:** FaceWorld::GetLastObservedFace(pose, true) non-zero and Robot::IsPoseInWorldOrigin(pose) (0x005ADF96..0x005ADFA4).
- **Cube input:** BlockWorld::FindLocatedObjectHelper with a filter, non-null. Families table 0x00C61F50 = 1, 2 (Block, LightCube).
  The rest of the filter was not read.
- **+0x91:** no store to +0x8C..+0x91 outside the constructor memset in the class's address range, other than the +0x90 writes the
  report lists. +0x91 reads as 0 unless another class writes it. Build it as an always-zero override only with that caveat recorded.
- **+0x80** is the current activity pointer, and +0x92/+0x94 hold the sub-activity ID and name (0x005AD8CE..0x005AD972).
- **OffTreads gate (0x005AD9F8):** when the message byte is 0, a current activity exists, its ID is not 7 or 8 (NeedsSevereLowEnergy,
  NeedsSevereLowRepair), it isn't +0x91, and its +0x4C is 0x55: log, IActivity::OnDeselected, clear +0x80. The comparison with +0x91
  is skipped for ID 8.

## Checked and matching

- **Item 1:** the 0x15C census (ctor, InitInternal, callback, UpdateInternal), the two transition branches and StopWithout... at
  0x005DD110. It answers the open point from the M12-049 note: +0x15C makes the next InitInternal go to TransitionToReactingToPyramid
  if the full-pyramid list is non-empty, and makes UpdateInternal stop. The whole-`.text` sweep was not repeated.
- **Item 2:** the update call order, the prune arguments, the first-match and base-outer loop shape 0x006186E4..0x0061872C, and the
  rebuild flag store at manager +0x0C (0x00616DCC).
- **Item 3:** the selection branches 0x005AE07E..0x005AE0AA and the single direct caller 0x005A3FD0.
- **Item 4:** the tag dispatch and payload reads; Unity agrees: behaviorID, numRuns, Size 5.
- **Item 6:** 6.05-6.08, 6.10-6.12.
- **Item 7:** 7.04-7.12, 7.14-7.16, 7.18 (other than the label). NVResult -1 is NV_NOT_FOUND.
- **Item 8:** 8.01-8.11, 8.12-8.15, 8.17-8.19 (other than the label). Unity agrees on RequestSetUnlockResult (Size 5) and
  UnlockedDefaults.

## Still open

- BlockWorld::FindLocatedMatchingObjects order, so the final full-pyramid list order stays open.
- The configuration broadcast at 0x00616DBE.
- CalculateActivityFreeplayFromObjects's caller.
- The analyzer's map call and the five unread strategy types.
- OnDeselected of the chooser; the entry points of the two UnlockStatus lambdas.

## New gaps with no record

- **Inventory write path** (raised 2026-10-31, not yet decided): cap, throttle, NV write, the completion above.
- **Unlock write path** (new): RequestSetUnlock, SetUnlock, the NV write to tag 0x182000, RequestSetUnlockResult and UnlockedDefaults.
  M15-034 covers only the read. The NV write is robot traffic.

Both go to the operator.

## Method note

This queue had the two new rules (name the flag setter, name the base object). They worked: no flag-source or wrong-base error this
time. Errors moved to what a citation can't show: a row summarising a path instead of following it (correction 1), a value named
without tracing it (correction 3), and an address read from a data dump without deriving it (correction 4, which Claude's last check
also passed). Its "Done" also meant less than the table implied: the three things asked for by name in items 5, 7 and 8 were left
UNKNOWN, and all three took under a screen of disassembly to close. Veneers and vtables stopped it; those are the leads to give next
time.
