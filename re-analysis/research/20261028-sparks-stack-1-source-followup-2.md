# SPARKS-STACK-1 — second finite source followup

2026-10-28, released builder/verifier questions only. Frozen original read packet and first SF1–3 supplement remain unchanged. PROJECT_STATE reread, known.py before individual native reads; native ELF SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. No implementation, tests, git mutation or official record edits.

Current owning-record quotations, reread before ownership claims:

```json
{
  "id": "M15-022",
  "title": "PlaceObjectOnGroundAtPoseAction composition and PlaceObjectOnGroundAction Init/CheckIfDone",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "PlaceObjectOnGroundAction Init sends message 0x44 (0, 0, 0, 100, 200, 500 as raw bits), builds a TurnTowardsObject verify action and calls StopAllMotors; CheckIfDone never reads DockingComponent+5, its result is the verify action's (extraction sections 2.3..2.8; construction of PlaceObjectOnGroundAtPoseAction at 0x005DFF14)",
    "M12-015 owns only the wire message of PlaceObjectOnGround (its evidence does not cover the action)",
    "extraction report re-analysis/research/20261003-R-FIX2-M15-gap1-extraction.md"
  ]
}
```

```json
{
  "id": "M4-028",
  "title": "The sleep sequence's lift child: MoveLiftToHeightAction(Preset 0, 5.0f, 0) construction",
  "status": "EXACT_SOURCE",
  "evidence": [
    "0x0052CF8E..0x0052CFB0; MoveLiftToHeightAction preset0, tolerance f32 0x40A00000; exact child initialization, locking, stop and completion must be verified in the control layer.",
    "Manager-adopted re-analysis/research/20261005-B-M1M2-blockers-extraction.md T1\u2013T9 and ownership table; instruction companion re-analysis/research/20261005-B-M1M2-blockers-native.txt",
    "Re-read: movs r0,#0xA4; blx 0x4A42A0 0x0052CF8E..0x0052CF90; movs r3,#0; movt r3,#0x40A0; movs r2,#0; blx 0x4A9A48 MoveLiftToHeightAction::MoveLiftToHeightAction(Robot&, Preset, float) 0x0052CF96..0x0052CFA2, body 0x00548B84 (GetPresetHeight 0x00548C14, GetPresetName 0x00548CD4, exports). Handoffs: AddAction blx r7 (the parent's vtable +0x20) 0x0052CFB0 and execution: M8-017; the factory 0x0052CEA2..0x0052CFC0: M5-038; Init/CheckIfDone/acks: M4-016 and M4-002"
  ]
}
```

```json
{
  "id": "M8-015",
  "title": "ActionList/ActionQueue/IAction lifecycle, cancellation and Robot teardown",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x0053E6A8..0x0053E6E0; 0x005409E2..0x00540A40; 0x00541146..0x00541238; 0x0053FAB2; Current Cancel then delete; pending direct deletion preserves existing result. Cancelled0x02000000 except NotStarted0x02000001; stop before unlock, watcher then broadcast. Existing B-ACTIONS candidate must be checked for full teardown effects and concrete descendants.",
    "Manager-adopted re-analysis/research/20261005-B-M1M2-blockers-extraction.md T1\u2013T9 and ownership table; instruction companion re-analysis/research/20261005-B-M1M2-blockers-native.txt",
    "P3/P4 full ActionList/ActionQueue/IAction lifecycle: ActionList 0x0053F580..0x0053F5F2, ActionQueue 0x0053F5F4..0x0053F70C, IActionRunner 0x00540370..0x0054063E, clock/timeout 0x00540D4A..0x00540E80, CheckIfDone 0x005549B8..0x005549F4 (20261006-M1M2-missing-triage.md P3/P4).",
    "re-analysis/research/20261012-m8-play-animation-actions.md rows C2..C7, L14..L23, D5, S2..S4 and re-analysis/research/20261012-m8-play-animation-actions-check.md (the +0x57 gate on L17/L18/L20, the DeleteActionAndIter guard set at queue+0x10, ActionWatcher Start/EndUpdating 0x00540386/0x0054057E/0x00540630, the UpdateInternal status string 0x00540D46..0x00540F1C).",
    "re-analysis/research/overnight/20261014-ov18-m8-015-check.md: L1-L9 0x00540370..0x0054063A, 0x00540D1C..0x00540F9C, 0x00540750..0x005407C0, 0x005409DC..0x00540A44; Q1-Q7 0x0053F580..0x0053F708, 0x0053DCE4..0x0053DD92, 0x0053E24C..0x0053E2F6, 0x0053DF2C..0x0053DFC8, 0x0053DE10..0x0053DE5C, 0x0053E690..0x0053E700; D1-D6, D12 0x0053F9E4..0x0053FB06, 0x00541084..0x00541286, 0x00541BB4..0x00541C04, 0x0054187E..0x005418DE; T2 List Clear 0x0053D916..0x0053D93A, 0x0053F938..0x0053F950, 0x0053FB5C..0x0053FB82; QueueAction front end 0x0053D93C..0x0053DAC8; position table 0x0053DA24..0x0053DB70.",
    "re-analysis/research/overnight/20261015-ov56-58-check.md: teardown mechanics from OV56: ~Robot order 0x005110EE..0x00511156; AbortAll 0x00511952..0x0051198C (0x0063BE2A..0x0063BE36, 0x00517DFE..0x00517E0A); ActionList::Cancel clearing gate and slot order 0x0053DE18..0x0053DE54; ActionQueue::Cancel 0x0053E69A..0x0053E6FA (early exit 0x0053E6E4); QueueNow/QueueAtEnd 0x0053E25A..0x0053E36E; DeleteActionAndIter 0x0053F9E4..0x0053FAB2 (re-entrancy set, INTERRUPTED/no-interface gates); result codes 0x0053FDBA..0x0053FDD4, 0x005409E2..0x00540A40; Clear lock-out 0x0053D91A..0x0053D938, 0x0053DCEA..0x0053DD00, 0x0053DCAA, 0x0053DF40, 0x0053F93C, 0x0053F970..0x0053F9AC; ~ActionList double Clear 0x0053D8F6..0x0053D90E; IBehavior StartActing reject-delete 0x005BDC04, Init failure 0x005BCCC6..0x005BCD06."
  ]
}
```

```json
{
  "id": "M12-042",
  "title": "PlaceRelObjectHelper lifecycle: ctor, Init (pre-action check, DriveTo delegation), StartPlaceRelObject (3-attempt counter), RespondToPlaceRelResult, MarkFailedToStackOrPlace",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261014-ov02-m12-028-check.md: ctor 0x005B8F00..0x005B8F82; Init 0x005B8FD0..0x005B90FE; Start 0x005B9168..0x005B930E; Respond 0x005B94C0..0x005B952E; MarkFailed 0x005B93A8..0x005B9460; invokers 0x005B956A, 0x005B95D6."
  ]
}
```

## SF4 — NoPreDockPoses response is a terminal callback with unchanged helper status

`RespondToPlaceRelResult005B94C0..952E`: result03000010 at`005B94E2..94EA` branches directly to`005B9522`, reads actualRobotAI+264→Whiteboard+18, stores helpertargetF4 towhiteboard70 at`005B952C`, and returnsBXLR`005B952E`. It does NOT call StartPlaceRelObject and does NOT store helperstatus1C. The “remaining results retry” wording in original P3 excludes this explicitly handled result. Existing draft case writingWBthenreturn is correct. Results0→status2,03000000/03000004→status0,02000000unchanged andremainingdefault→StartPlace remain as the original packet. Independently primary checked A+B.

## SF5 — Ground concrete completion is objectInteraction variant0 with undefined unused words

`PlaceObjectOnGroundAction::GetCompletionUnion00554AE0..4AFA` allocates24bytesstack;`00554AE4..4AE6` stores halfword1 atsp14 (Count1,Flagfalse),`00554AEC..4AEE` copies savedcarryscalar[this+7C] tosp0. It does not initialize the other four ID words atsp4..10. `00554AF4` calls actualSet_objectInteractionCompleted(move), variant0; the generated serializer/setter reads allfivewords as checked in the original packet. This is NOT default/no-data variant6, and there is no source-defined fullrawbody to publish by copying zero/-1 fillers.

Apply the existing M1-034 standing rule for original uninitialized payload reads, using the established pickup full-public-payload availability guard: keep variant0 and independently defined internal savedcarry/count1/false plus actualscalarresult/tag/callback lifecycle; mark fullrawpayloadunavailable and visibly log the smallest skipped PUBLIC payload publication. Do not strand helper progress or invent success. Savedcarry7C is initialized-1 by ctor00554638..469C and snapshotted from actualcarry atInit00554794 before release. No later world/carry resampling substitutes for that savedvalue. The operator's prior zero-unused policy explicitly covers source-known NORMAL PlaceRel payloads; do not extend it to Ground implicitly.

A+B independently primary checked this complete short getter and variant0 setter semantics. Current M15-022 owns its explicitly titled Init/Check/AtPose composition; M8-015 owns generic completion preparation/watcher/publication. Neither narrow current claim establishes this concrete Ground getter body. Propose NEW exact GroundGetCompletionUnion producer/availability-guard step for manager allocation, with no ID/status assigned here; the manager may place it beside the separately proposed concrete placement completion owner. Do not silently expand an existing settled record or treat the prior PlaceRel-only proposal as owning Ground automatically.

## SF6 — placement high-verify lift is preset0, tolerance5, variance0

Place Verify constructs `MoveLiftToHeightAction(robot,Preset0,5f)` at`005557EE..55800`, then suppresses child track locking`00555818..81A` and returnsRUNNING. Preset constructor`00548B84..8BA2` callsGetPresetHeight, forwards the supplied5f as floatctor argument3, and supplies floatctor argument4=0. `GetPresetHeight00548C14` uses tableC54684 whose firstpair iskey0/valuebits42000000=32mm.

Float ctor`0054899C..48A80` stores heightat78,toleranceat7C,varianceat80; duration88=0,speed8C=bits41200000(10),accel90=bits41A00000(20). Thus5f is TOLERANCE, not speed. Existing same native LowDock child constructor source under M4-028 is reusable for this exact tuple and native motor Init/Check/ack recipient; its sleep-specific title does not own this new Place caller. A host may use an existing sharednativechild factory with these exact values; names are best-effort diagnostics. No public gameSetLiftHeight factory substitution (which can turn32mm+carrying into Ground) is implied.

A+B independently checked constructor forwarding, field stores and presettable. No new motor arithmetic or acknowledgement semantics are recovered here.

## SF7 — exact new action interruption and timeout slots

Ground vtablebase10222B8/addresspoint+8: slot14cell10222D4=0052B0B3. Place vtablebase10222F0/addresspoint+8: slot14cell102230C=same. Thumbbody`0052B0B2..B4` returns0; both actualCanInterrupt=false. This extends the specific checked table recipients, not a blanketNativeVisionRunner inheritance claim.

Ground timeoutslot2Ccell10222EC andPlacecell1022324 both0052B0C3; Thumb`0052B0C2..C8` returnsbits41F00000=30f. A independently cross-checked B's newcells after known-first tinybody read. Defaulttimeout/framebehavior remains existing IAction runtime, not a new local wait.

## SF8 — selected-object early write question

A+B PRIMARY PASS: remove the draft's new early `World.SelectedObjectId=target`. The actual bounded native position is object lookup`0055158A..515CA`, then B9 preaction query`0055160C..51648`, derivedSelect, subscriptions, marker/setup and InitInternal tail. No selected-state writer/call occurs at the draft's new position. The already checked sharedGetPreActionPoses recipient is a query/geometry path with no authorized early selection mutation, and Place ctor/InitInternal contain only their recorded localparameterstores. Source does not support this added sideeffect. Destruction's existing selection clear cannot establish an earlier producer. This is a bounded Init-position verdict, not a global claim that nothing elsewhere selects objects; no broadercallerhunt was performed or needed.

All SF4–SF8 finite facts independently primary checked A+B. No source UNKNOWN remains in these questions; frozen earlier packets remain unchanged. New concrete Ground completion ownership is proposed explicitly above. No code/tests/official status changes or policy extension were made by extractor.
