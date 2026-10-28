# SPARKS-STACK-1 — finite builder source followup

2026-10-28. Source only; answers released builder's concrete PlaceRel IDock defaults/marker selector and empty-converter callback questions. The frozen `20261028-sparks-stack-1-read.md` remains unchanged. PROJECT_STATE reread; known.py preflight before bounded native reads. Native ELF SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. No implementation/tests/git/official edits. These are explicit call-boundary details of already checked M12 gap2 Q6.4–6.18 and shared helper response source, not a new speculative contract.

## Current recipient records, quoted before ownership claims

```json
{
  "id": "M12-005",
  "title": "Every field of DockWithObject",
  "status": "EXACT_SOURCE",
  "evidence": [
    "word 0 = 0: 0x0063BB34 movs r4,#0 / 0x0063BB7E str r4,[sp,#0x1c]",
    "words 1..3 = IDockAction +0xAC/+0xB0/+0xB4, defaults 60/200/500 at the ctor 0x005502D8 (0x42700000/0x43480000/0x43FA0000)",
    "byte 4 = DockAction +0x80; byte 5 = +0x95; byte 6 = +0xBA (0); byte 7 = +0xBB (0; PickupObjectAction 0x005536EA writes 2, RollObjectAction 0x005565D6 writes 0 (the 5 stored at 0x005565DC is the DockAction byte +0x80, not +0xBB)); byte 8 = +0xC1 (0; PickupObjectAction 0x005536F8 writes 1)",
    "the complete list of +0xBB writers (whole-.text scan, re-analysis/research/20260929-R-VIS-verify-M12-gap2.md item 1): 0x005533F6 AlignWithObjectAction case = 2; 0x005536EA PickupObjectAction = 2; 0x00554E12 PlaceRelObjectAction::InitInternal = 3 iff |B'| >= 1e-5 (movpl, so NaN also sets it); 0x005554D0 PlaceRelObjectAction::SelectDockAction = 0; 0x005565D6 RollObjectAction = 0; 0x0055C5A2 SetDockingMethod = its argument; the ctor default is 0 (the 0x100 word at 0x005503AA). Byte 7 (wire offset 0x13) is DockingMethod = [this+0xBB]; the builder 0x0063BD50 writes +0x10 DockAction, +0x11 bool [+0x95], +0x12 retries [+0xBA], +0x13 method [+0xBB], +0x14 bool [+0xC1]; the +0x95 bool is the IDockAction ctor's last bool (0x00550382), read at 0x00552278"
  ]
}
```

```json
{
  "id": "M12-017",
  "title": "IDockAction::Init setup, handler registration and the docking squint",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x005514FC Init; 0x00551598 GetLocatedObjectByIdHelper; 0x00551640 GetPreActionPoses; 0x005516EA movs r3,#0xc5; 0x00551750 movs r3,#0xda",
    "the handler bodies are the action's own state machine",
    "the AddSquint call 0x00552394 is inside IDockAction::CheckIfDone 0x005521AC (body 0x005521AC..0x005523F6), not Init 0x005514FC (body 0x005514FC..0x00551C8A, no call to PLT 0x4ABA04)",
    "AddSquint args: TrackLayerComponent at robot+0xC0 (0x00552362 ldr.w r5,[r0,#0xc0]); name \"DockSquint\" (literal at 0x005524A0, 10 bytes); f1=0x3F866666=1.05 (0x0055237A/0x00552388); f2=0x3EB33333=0.35 (0x0055237E/0x0055238C); f3=0xC1200000=-10.0 (0x00552378/82/86); return at 0x00552398 strb [r4,#0xf4]",
    "TrackLayerComponent::AddSquint 0x0064F370 -> FaceLayerManager::GenerateSquint 0x0058D738 (PLT 0x4BA164) then AddPersistentLayer 0x4AE9E0; GenerateSquint ignores its float args and clips EyeScaleY=0.35, EyeScaleX=1.05, UpperLidAngle=-10.0 on both eyes",
    "GenerateSquint track: two ProceduralFaceKeyFrames, the second at 250 ms (0xFA) with Reset (0x0058D820/0x0058D82C)",
    "0x00551540..0x00551C14 the Init order; 0x00551C0C ldr r1,[r0,#0x30] / 0x00551C10 blx r1 / 0x00551C14 cbnz r4 (InitInternal is the last step; PlaceRelObjectAction vtable 0x010222F0 slot +0x30)",
    "0x00555435..0x005555B5 SelectDockAction",
    "re-analysis/research/20260929-R-VIS-M12-gap2-extraction.md Q6; re-analysis/research/20260929-R-VIS-verify-M12-gap2.md Q6",
    "0x00551960..0x00551B8C marker choice; 0x00551C0C..0x00551C4C the tail; 0x00529CF4..0x0052AAC6 the wire stores",
    "re-analysis/research/20260929-R-VIS-M12-gap3-extraction.md Q10.1-10.2; re-analysis/research/20260929-R-VIS-verify-M12-gap3.md (10.1, 10.6)"
  ]
}
```

```json
{
  "id": "M12-025",
  "title": "PlaceRelObjectAction: ctor, InitInternal and TransformPlacementOffsetsRelativeObject",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x00554BE0..0x00554D0E ctor; 0x00554DCC..0x00554E30 InitInternal; 0x00554E40..0x00555094 the transform; literals 0x3E860A92, 0xBFC90FDB, 0x3FC90FDB, 0xC0490FDB, 0x40490FDB, 0xC1800005",
    "0x0052A6C2..0x0052A754 the wire factory; 0x0055C7D0 ctor, 0x0055C83A ldr sb,[sp,#0xAC], 0x0055C882 cmp sb,#0; 0x0052A6DC movs r1,#1",
    "re-analysis/research/20260929-R-VIS-M12-M13-gap1-extraction.md Q2; re-analysis/research/20260929-R-VIS-verify-M12-M13-gap1.md priority check 1 and objections A3, A4, A5",
    "re-analysis/research/20260929-R-VIS-M12-gap2-extraction.md Q6, Q7; re-analysis/research/20260929-R-VIS-verify-M12-gap2.md Q6, Q7"
  ]
}
```

```json
{
  "id": "M8-011",
  "title": "The Smart* scope helpers: idle animation, motion profile, track locks, custom light patterns, helper delegation and the reaction locks",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "SmartPushIdleAnimation 0x005be41c: if the idle-animation flag +0xb0 is already set it fails through sVerifyFailedReturnFalse (0x005be448 b.w 0x8cbe5c); otherwise AnimationStreamer::PushIdleAnimation(trigger, name) and +0xb0 = 1 (0x005be464..0x005be478).",
    "SmartRemoveIdleAnimation 0x005bd4c8: when +0xb0 is clear it raises VERIFY(%s): Behavior %s is trying to remove an idle, but none is currently set through sVerifyFailedReturnFalse (cbz r0,#0x5bd508 0x005bd4d4; string 0x005bd51e; b.w 0x8cbe5c 0x005bd526); otherwise AnimationStreamer::RemoveIdleAnimation(name) and +0xb0 = 0 (0x005bd4ec/0x005bd500).",
    "SmartSetMotionProfile 0x005be518 verifies +0xc0 == 0 (sVerifyFailedReturnFalse 0x005be52a), calls PathComponent::SetCustomMotionProfile (0x005be534) and sets +0xc0 = 1. SmartClearMotionProfile 0x005bd584 verifies +0xc0 != 0, calls PathComponent::ClearCustomMotionProfile (0x005bd59c) and clears +0xc0.",
    "SmartLockTracks 0x005be5bc emplaces (name -> mask) into the map at +0xb4; on a new key it calls MovementComponent::LockTracks(mask, name, reason) and returns 1; on an existing key it warns \"Attempted to lock tracks with key named %s but key already exists\" and returns 0 (0x005be600/0x005be624/0x005be636). SmartUnLockTracks 0x005be6e0 looks the name up, calls MovementComponent::UnlockTracks(mask, name), erases the entry and returns 1, or warns and returns 0 (0x005be6ee..0x005be716).",
    "SmartSetCustomLightPattern 0x005be7f0: if the ObjectID is already in the vector at +0xcc it logs on the \"Unnamed\" channel and returns 0; otherwise CubeLightComponent::PlayLightAnim(objectID, trigger, callback, true, lights, timeout) and appends the ObjectID to the vector at +0xd0, returning 1 (0x005be806..0x005be8cc).",
    "SmartRemoveCustomLightPattern 0x005be9b0 searches the vector at +0xcc for the element whose +4 equals objectID+4; not found logs \"No custom light pattern is set for object %d\" and returns 0 (0x005bea4e/0x005bea74); found calls CubeLightComponent::StopLightAnimAndResumePrevious(trigger, objectID) for each trigger and erases the element, returning 1 (0x005be9e8/0x005bea34).",
    "SmartDelegateToHelper 0x005beb10 logs on the \"Behaviors\" channel, warns and calls StopHelperWithoutCallback() when the current helper +0xc8 is already live (0x005beba4/0x005bebe8), then calls BehaviorHelperComponent::DelegateToHelper on [robot+0x264]+0x10 (0x005bec2c); on success it stores the helper as a weak ref at +0xc4/+0xc8 through __add_weak at 0x005bec6e, otherwise logs \"SmartDelegateToHelper.Failed\" (0x005bec8c/0x005becb2).",
    "SmartDisableReactionsWithLock 0x005bce3c appends \"_behaviorLock\" to the name, calls BehaviorManager::DisableReactionsWithLock(manager, name+\"_behaviorLock\", table, true) (0x005bce62) and inserts the original name into the per-behaviour set at +0xa4 (0x005bce7e). SmartRemoveDisableReactionsLock 0x005bd470 appends the same suffix, calls BehaviorManager::RemoveDisableReactionsLock (0x005bd48c) and erases the name from +0xa4 (0x005bd4a4). The manager side is M7-014.",
    "SmartDelegateToHelper 0x005beb10 calls BehaviorHelperComponent::DelegateToHelper, whose body is 0x0056dad8..0x0056db37 (PLT 0x004b29d0) at AIComponent+0x10 ([robot+0x264]+0x10, AIComponent constructed at 0x00569aae/0x00569ab2): it clears stack-maintenance vars, copies the two callbacks to +0x10/+0x28, and on an empty helper stack pushes the helper and stores Robot::GetWorldOriginID at +0x40, returning 1. R-BEH C2 read the component and its helper-stack runtime in full (report re-analysis/research/20260929-R-BEH-M8-framework-gap1-extraction.md); the component and the M7/M15 helper subclasses are still unbuilt, so the record stays IMPLEMENTATION_GAP."
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

## SF1 — actual placement docking defaults and pre-action marker branch

| Step | Exact native result | Primary evidence |
|---|---|---|
| Docking payload defaults | IDock ctor stores halfword1 atC0: C0=true,C1=false. PlaceRel clears ONLY C0byte, leaving C1false. StackA/Bzero retains methodbyteBB=0,low=false chooses DockAction2/type19; actual message field maps to C1 and BB separately. |005503AE/00554C5A..CA6;005554D0;00552278..22AE. M12-005/M12-017/M12-025.|
| Post-dock delay | C4 is a reaction-disable TABLE POINTER, initializednull and consumed only for optional reactionsToSuppress lock. It is NOT a floatwait. Actual Check invokes vtable+44 and forms f32deadline(now+return) in90. Place slot44 returns raw0, interpreted+0f. |005503B6;00551870..189A;005523BA..23CA. Placevtablebase10222F0/addresspoint+8,+44cell102233C=55738B→Thumb0055738A..38C MOVS0/BXLR.|
| Pre-action branch/order | B9=true calls GetPreActionPoses with ActionType1, sourceangle88/default7.5deg andBC input. Nonzeroresult returnsimmediately. Then derivedSelectDockAction; ifnonzero returnit. Then register actualC5/DA/externalhandles and reset82/84 markers3. |0055160C..1648 precheck;0055164C..169A Select;005516E4..17C0 subscriptions;005517C4..17D2 branch. M12-017 Q6 reused, primarychecked.|
| Actual primary marker | On B9=true, use returnedposes[closestIndex].marker: vectorbase spBC, indexspC8, stride1C, markerpointerentry+4. Optional second-marker getter invoked; NULLPRIMARY logs NullDockMarker and returns03000002. Primarycode is markeru16→82. No observed-marker fallback onthisbranch. |0055182A..185A;005519F0..1A2E. B9false branch00551960 onward belongs checkedPickup tuple, not currentPlaceB9true.|
| Secondary marker | Optionalmarkergetter vtable+34 returnsNULL forPlace; secondaryshort84 becomes3. This getter is NOT DockingMethod; BB is the independentmethodbyte above. |Placecell102232C=557383→Thumb00557382..384 MOVS0/BXLR;00551838..185A branchesNULL→3.|
| Remaining setup order | SetupTurnAndVerifyAction(target) after marker assignment; optionalreactiontablelock; interactingcubelight; oldsquintremoval; InitInternalLAST, nonzeroimmediatereturn; preparedchild.Update, only0/RUNNING permits dockActionslock andInitreturn0. ExistingC0false/C8true produces retained nativeTurn(target,selectedprimarycode,Radians0,true,false). |0055186C;00551870..189A;005518AC..1C06;00551C0A..1C5E. M12-017/M12-037 recipients reused.|

A and B independently checked SF1 original instructions/default virtual slots. Do not reuse Pickup's observed-window marker selector merely because both actions share IDock; select the source B9 branch.

## SF2 — EMPTY response converter preserves the actual raw-result callback

Place StartActingWithResponseAnim binds `RespondToPlaceRelResult` at helperC0 and assigns the supplied EMPTY converter atA8 (`005B9488..94BA`). On actual action completion `RespondToResultWithAnim005B7160..7178` passes the original raw ActionResult and boundcallback into template`005B717C`.

`005B71DA` loads converter function pointerB8; `005B71E4 CBZ005B7208` means an empty converter skips converter invocation AND the AI response-director lookup/animation path. At`005B720C..720E`, copy savedcallback; `005B7212..7228` destroys and clears ORIGINAL callback functionpointer first. Then`005B722A..7242` destroys/clears converter. `005B7246..7252` invokes nonempty savedcallback with the ORIGINAL rawActionResult reference(r8) and Robot(sb). No callback means no invocation. The callback's own PlaceRel result table remains P3; this path never substitutes success or an animation completion result. Copy/clear-before-call permits native retry to install a fresh callback without being erased by stale cleanup.

Independent B PRIMARY PASS for SF2 received after its own known-first original-instruction check. No response animation or AI lookup is appropriate for this actual EMPTYconverter, but the real source callback and scalar result delivery are required.


## SF3 — common dockActions lock is independent of optional C4 table

Place keeps C4=null: base`005503B6` and no derived Place constructor store toC4. At`00551870..189A`, null skips the optional `reactionsToSuppress` lock only. The separate COMMON `dockActions` lock remains required for this Place path: after InitInternal returns0, update actual prepared child; `00551C20 ORR result,01000000` / `00551C24 CMP01000000` / `00551C28 BNE failure` admits exactly0 orRUNNING. Then`00551C2A..1C4C` calls actualBehaviorManager.DisableReactionsWithLock("dockActions",tableC55A21,true). Table is 21 enum/bool pairs (42bytes), enum0..20, ONLYindex8=true, everyotherfalse. This lock does not test C4. Init failure or prepared child terminalfailure returns without taking this lock.

Common CheckIfDone squint path`0055233E..2398` is also shared with Place: on rising actual picking/placing latch, check actualAI severeNeed !=1, then call AddSquint and retainhandleF4. Existing shared IDock timing/body/head/latch/face-layer retirement contracts remain unchanged. No extra per-action local success or idle-time squint is added.

SF1–SF3 independent B PRIMARY PASS, including direct42-byte table read and exactlock predicate; frozen main source packet remains unchanged. This supplement names no new status, approval or broad dependency settlement.
