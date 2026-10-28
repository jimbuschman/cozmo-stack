# SPARKS-KNOCKOVER-1 — finite builder source questions

2026-10-28. Supplement to `20261028-sparks-knockover-1-read.md`, adopted at be3df0ea. That frozen packet is unchanged. Answers the manager's four finite source questions raised during isolated implementation. No code, tests, git, status, inventory, approval, Claude or hardware changes. Actual model/effort telemetry unavailable.

Primary ELF: `resources/lib/armeabi-v7a/libcozmoEngine.so`. `known.py` preceded the new traces of Freeplay desired/post-null, reward callee, StrictPriority chooser hook addresses. Existing checked OV67 and M8/M15 source rows were reused first. Independent B checked SF1–SF4 against the ELF and confirmed them by messages before this supplement was written. This is source authorization for these finite corrections, not a claim that the implementation is verified.

## Current owning records, quoted before reliance

```json
{
  "id": "M15-006",
  "title": "Freeplay activity selection and selected/deselected lifecycle",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "ActivityFreeplay::PickNewActivityForSpark 0x005ADC44",
    "ActivityFreeplay::GetDesiredActiveBehaviorInternal 0x005AE29C",
    "IActivity::OnSelected 0x005B312C",
    "IActivity::OnDeselected 0x005B33B8",
    "IActivity::GetDesiredActiveBehavior 0x005B387C"
  ]
}
```

```json
{
  "id": "M8-013",
  "title": "The behaviour choosers and the activity selection dispatch",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "ScoringBSRunnableChooser::GetDesiredActiveBehavior 0x0060a44a walks its behaviour map, calls EvaluateScore(robot) on each (0x0060a44a), skips scores <= 0 (vcmpe s0,#0 0x0060a452), adds a running-duration graph bonus for a running behaviour (GraphEvaluator2d::EvaluateY 0x0060a474) and a RandomGenerator::RandDbl tie-break for a non-running one (0x0060a4a8), and keeps the maximum. It logs \"behavior '%s' has score of %f, so is interrupting running behavior '%s' which scored %f\" (string 0x0060a60a; sChanneledInfoF 0x0060a61c) and warns \"Looks like more than one behavior returned IsRunning(). One of them is '%s'\" (0x0060a550).",
    "SelectionBSRunnableChooser::GetDesiredActiveBehavior 0x0060ad64 chooses between the behaviour at +0x2c and the one at +0x34, gated by a countdown at +0x3c and a latch at +0x40; a running behaviour short-circuits the countdown (movs r6,#1 0x0060ad84; subs r1,#1 0x0060adbe; str r1,[r5,#0x3c] 0x0060adc0; strb.w r0,[r5,#0x40] 0x0060adc8).",
    "StrictPriorityBSRunnableChooser::GetDesiredActiveBehavior 0x0060b23e walks its behaviour vector at +0x1c..+0x20; a behaviour with +0xa1 (running) non-zero is returned immediately, otherwise the first behaviour for which IBehavior::IsRunnable(robot) == 1 is returned; if none, it returns a null shared_ptr (0x0060b242..0x0060b266). No scoring and no tie-break.",
    "IActivity::GetDesiredActiveBehavior 0x005b387c returns the activity's current behaviour when it equals the caller's; otherwise it calls the activity's GetDesiredActiveBehaviorInternal at vtable+0x30 (0x005b38c2) and, when the activity holds an interlude behaviour at +0x28 and the result differs, calls the interlude's method at vtable+0x28 (0x005b38f6), logging \"Activity %s is inserting interlude %s between behaviors %s and %s\" (0x005b399c).",
    "IActivity::GetDesiredActiveBehaviorInternal 0x005b3a6c reads the chooser at +0x24; a null chooser warns \"VERIFY(%s): ChooseNextBehaviorInternal called without behavior chooser overwritten\" and returns a null behaviour; otherwise it calls the chooser's vtable+0x28 (0x005b3a70/0x005b3aa2/0x005b3a94).",
    "The concrete activities implement GetDesiredActiveBehaviorInternal: ActivityFreeplay 0x005ae29c, ActivitySocialize 0x005b023c, ActivitySparked 0x005b1d18, ActivityStrictPriority 0x005b267c, ActivityBuildPyramid 0x005a8854, ActivityFeeding 0x005abdd8. Their bodies belong to M7/M15.",
    "SelectionBSRunnableChooser::GetDesiredActiveBehavior 0x0060ad64 full algorithm: +0x2c is the behaviour named by the last ExecuteBehaviorByID/ByExecutableType game message (set 0x0060ac2c/0x0060ac30), +0x34 is the Wait behaviour (BehaviorID 0xb2 resolved in the ctor 0x0060a988/0x0060a99a), +0x3c is the message's numRuns (ctor writes -1 unlimited, 0x0060a880/0x0060a884; message stores 0x0060aa88/0x0060aac8), +0x40 is a byte latch holding the previous call's running state (0x0060adc8/0x0060ae3a). It returns +0x2c while it is running or IsRunnable (0x0060ad96) and numRuns has not run out, decrementing +0x3c once per running->stopped edge; otherwise the same for +0x34 (0x0060ae08) but its countdown block runs only when +0x34 == +0x2c (0x0060ae10/0x0060ae14); otherwise a null shared_ptr (0x0060ae64).",
    "The selection chooser calls IBehavior::IsRunnable (PLT 0x004b0f54), not IsRunnableScored; IsRunnableScored has no engine caller."
  ]
}
```

```json
{
  "id": "M15-059",
  "title": "ActivitySparked lifecycle: OnSelected (HardSparkStartedByEngine tag 131), OnDeselected (StopAllAnims), objective/observed, Update/checks/end/interrupt, get-in/out, Complete (meta.upgrade_replay_* events, HardSparkEndedByEngine tag 132)",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261015-ov65-67-check.md: OnSelected 0x005B1518..0x005B16DC, 0x005B171C; OnDeselected 0x005B1780..0x005B179E; objective/observed 0x005B181E..0x005B1866; Update/check/end/interrupt 0x005B186C..0x005B1A16; get-in/out 0x005B1D18..0x005B1F2A, hard max 0x005B1F94..0x005B2066; Complete 0x005B1A90..0x005B1C2E, 0x005B2094."
  ]
}
```

```json
{
  "id": "M15-071",
  "title": "SparksRewardCommunicatedToUser: the FreeplaySparksAwarded game broadcast and its amount",
  "status": "RECOVERABLE_GAP",
  "evidence": [
    "SparksRewardCommunicatedToUser 0x00696E64 clears the pending flag (+0x3D8, 0x00696E78) and builds FreeplaySparksAwarded from the amount at +0x3DC (0x00696E82..0x00696E88), then calls the E2G Broadcast(&&) through the external interface vtable +0x1C (0x00696E90). It has no pending-flag test (research/20261018-b-m1-7-check-delivery.md finding 1).",
    "Callers named in Needs.cs: IActivity::OnDeselected 0x005B3548, BehaviorEarnedSparks::StopInternal 0x005DAF7C, and RegisterNeedsActionCompleted; whether they guard the call on the pending flag is unread.",
    "The stack's public SparksRewardAwarded (Needs.cs:828) is queued with no payload and returns early when !SparksRewardPending (Needs.cs:841)."
  ]
}
```

## SF1 — a null Spark desired behavior does not itself deselect its activity

**Finding:** automatic `OnDeselected`/clearing the selected owner/clearing manager activity merely because Sparked.Desired returns null is not established by source. The source first enters the parent picker. Missing that picker is an earlier boundary than deselection.

| Step | Native instructions and result |
|---|---|
| Null result | `0x005AE68C cbz r5,0x005AE70A`; the null branch logs NoBehaviorChosenWhileRunning at `0x005AE736`. |
| Parent re-pick setup | `0x005AE75C mov.w r8,#0`; `0x005AE760 movs r3,#0`; `0x005AE762 b 0x005AE40C`. No cleanup call precedes this branch. |
| Picker boundary | `0x005AE40C..0x005AE468` reads active/requested spark fields; `0x005AE46E blx 0x004B19E0` calls PickNewActivityForSpark with the actual Freeplay owner and Robot. |
| Later conditional cleanup | Within the existing checked picker, the changed-candidate path emits its transition diagnostics, then `0x005ADE02` calls the old activity's OnDeselected, `0x005ADE08` stores the new selected pointer, and a nonnull candidate receives SetActivityID/OnSelected at `0x005ADE14/0x005ADE1E`. |
| Counterexample to unconditional cleanup | NoActivitySelected at `0x005ADE26..0x005ADE66` logs and returns 0 without the `0x005ADE02` old-owner deselection. This suffices to reject unconditional cleanup at the null caller. No broader picker reconstruction was undertaken. |

The own Sparked state-4 Complete path still returns null without changing to state 5 (`0x005B1E1C..0x005B1E24`). The reaction fast-complete path separately writes state 5. Do not add an artificial once flag or state-5 write to repair missing parent scheduling. At the missing parent picker boundary the actual owner and its state remain retained; a labeled supplied native deselection can exercise the recovered cleanup independently. Do not invent a fallback activity, claim raw admission/picker completion, or imply that leaf null is an immediate Stop request. The own end-on-next-action request and later manager behavior stop retain their already checked scheduling.

This refines H8's exact boundary; H8 did not authorize unconditional deselection. It is an implementation bug if the draft automatically retires the activity before the unbuilt picker.

## SF2 — pending reward is consumed before specialized deselection

The common OnDeselected order relevant to this activity is:

1. Remove tracked reaction locks (`0x005B34B4..0x005B34C6`).
2. Remove smart-owned idle if flag +0x50 is set (`0x005B34EA`).
3. Clear/release cached interlude shared pointer +0x2C/+0x30 (`0x005B34EE..0x005B34F6`).
4. Read the actual NeedsManager pending byte +0x3D8 (`0x005B34FA..0x005B3502`). If set, log the native pending-reward diagnostic and call `SparksRewardCommunicatedToUser` at `0x005B3548`.
5. Emit common exit diagnostics, then invoke specialized OnDeselectedInternal through vtable +0x2C at `0x005B365C`.
6. The specialized Sparked hook performs ResetLightsAndAnimations, optional delegate deselection, then the actual CubeLightComponent.StopAllAnims wrapper (`0x005B1786/0x005B1792/0x005B179E`). The wrapper's single layer-1/all-objects call is frozen H12.

Direct callee `0x00696E64..0x00696EA8` has **no pending test**: `0x00696E78` clears +0x3D8, `0x00696E82..0x00696E88` constructs FreeplaySparksAwarded from amount +0x3DC, and `0x00696E90` broadcasts it through the external interface. The source-backed pending guard for this caller is the common activity branch, not a newly inferred callee rule.

Current `Behavior/Needs.cs` already exposes `SparksRewardPending` and `SparksRewardCommunicatedToUser()`, which clears that flag and queues the existing no-payload SparksRewardAwarded observer. The recovered activity caller can forward to that existing recipient under its own pending guard, in the source order above. This does **not** establish complete reward delivery: the amount +0x3DC, native FreeplaySparksAwarded payload and other callers' guards remain the explicit current **M15-071 RECOVERABLE_GAP**. Its manifest rejects the old “app economy” wording as an exclusion. Do not invent an amount, silently settle M15-071, globally change the callee's no-pending behavior from this one caller, or present the no-payload observer as the full native report.

## SF3 — exact completion diagnostics and their order

There is one selected final event, not an additional event literally called “result.” Frozen S25's phrase “result event” means whichever final key is selected below.

| Event | Native literal / condition | Text and fields |
|---|---|---|
| `meta.upgrade_replay_cancel` | Literal `0x005B1C8C`, selected when manager cancel byte +0x65 or activity interruption flag +0xB4 is nonzero. | Text = native EnumToString(active UnlockId). One field: `$data` = `soft` iff active != 0x55 and activeSoft != 0; otherwise `hard`. |
| `meta.upgrade_replay_success` | Literal `0x005B1CC4`, noncancel success: target repetitions == 0 or signed count >= signed target. | Same text and `$data` rule. |
| `meta.upgrade_replay_timeout` | Literal `0x005B1CA8`, noncancel failure and observed set size == 0. | Same text and `$data` rule. |
| `meta.upgrade_replay_fail` | Literal `0x005B1CE0`, noncancel failure and observed set size != 0. Includes soft failure, even when the extra event below is skipped. | Same text and `$data` rule. |
| `meta.upgrade_replay_fail_cubes_observed` | Literal `0x00BF1F57`. Extra event only for noncancel/noninterrupted failure with observed set size != 0, and active == 0x55 or activeSoft == 0. | Text = native EnumToString(active UnlockId); `$data` = unsigned decimal observed set size. |

Primary decision instructions: `0x005B1A9A..0x005B1AE0`; hard/soft extra-event gate `0x005B1AE2..0x005B1AEE`. Native extra event call is **first**, at `0x005B1B42`; selected final event call is **second**, at `0x005B1BC8`. Both precede the completion's conditional request clearing and HardSparkEnded reply. Text conversions occur at `0x005B1B36` and `0x005B1BBE`; `$data` literal is `0x005B1CFC`, hard/soft literals `0x005B1D08/0x005B1D10`. The extra event's count conversion is `to_string(unsigned)` at `0x005B1AF4`. No extra generic “result” key is emitted.

The exact native parser `BehaviorObjectiveFromString` exists; the current bounded own config's `KnockedOverBlocks` = 13 is already source established. These finite answers do not recover a general mapping for every other Spark objective. An implementation supporting only this configured owner must keep other objective/config cases explicitly unavailable instead of guessing an enum or claiming that this one entry is the whole parser. No sibling behavior or broader objective-map extraction was authorized here.

## SF4 — actual StrictPriority lifecycle hooks are empty

The actual StrictPriorityBSRunnableChooser vtable starts at `0x0102CD0C`; its address point is +8 = `0x0102CD14`. The common activity's chooser OnSelected call is at `0x005B314A`, using vtable +0x20; its OnDeselected call is `0x005B33D6`, using +0x24.

| Slot | Exact table cell | Native target body |
|---|---|---|
| +0x20 | `0x0102CD34`, raw Thumb pointer `0x0060A7BF` | `0x0060A7BE: bx lr` |
| +0x24 | `0x0102CD38`, raw Thumb pointer `0x0060A7C1` | `0x0060A7C0: bx lr` |

These two hooks have no state/API/robot effect for **this actual StrictPriority chooser**. No lifecycle call is allowed to acquire invented state from a host strategy callback. This is not a no-effect assertion about other chooser classes or the Spark strategy as a whole. No broader chooser audit was performed. Independent B directly reread both table cells and both two-byte bodies.

## Verification and boundaries

Independent source checker B confirmed SF1–SF4 from primary instructions/data before this supplement. Required regression behavior is source-derived: leaf null must hit the parent picker boundary without early deselection; explicit deselection must consume pending reward before own Reset/cube-light cleanup; diagnostics must use the selected reason and source event order; the actual StrictPriority hooks add no state transition. Tests should name supplied admission/deselection and retained M15-071/raw-picker boundaries honestly.

Frozen packet remains unchanged. This supplement does not settle any fidelity record, alter an approved contract, or upgrade a gap from a hardware/test result. Builder changes and their independent verification remain the manager's responsibility.
