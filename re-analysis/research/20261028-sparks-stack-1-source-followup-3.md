# SPARKS-STACK-1 — explicit SF8 correction and helper scheduling

2026-10-28. This source-only supplement answers two finite builder questions. The original read packet and SF1–8 files remain unchanged. This document explicitly supersedes one incorrect SF8 interpretation; it does not contradict or change an approved inventory. PROJECT_STATE was reread and known.py was run before the current native reads. Native ELF SHA256: `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`. No code, tests, git operations or official records were changed.

## Current owning records

These current title, status and evidence fields were reread before assigning ownership:

```json
{
  "id": "M12-031",
  "title": "IDockAction::GetPreActionPoses and SelectObject/GetObstacles",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x005508C8..0x00551200",
    "re-analysis/research/20260929-R-VIS-M12-gap2-extraction.md Q10; re-analysis/research/20260929-R-VIS-verify-M12-gap2.md Q10 (PASS)",
    "0x005B6F5C..0x005B70DA, 0x0055EE10..0x0055EE66",
    "re-analysis/research/20260929-R-VIS-M12-gap3-extraction.md Q9, Q3.7; re-analysis/research/20260929-R-VIS-verify-M12-gap3.md"
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

## SF9 — correction: the pre-action helper selects the target after its guards

**SF8's statement that the shared GetPreActionPoses recipient was a pure query was wrong and is superseded here.** The prior checked `20261028-pickup-children-1-read.md`, step 5, already preserved the actual selection call. Current M12-031 owns this recipient. This corrects our extraction interpretation; it does not require a new operator checkpoint.

The added direct `World.SelectedObjectId = target` at the beginning of Place.Init is still in the wrong position. The native IDock caller first looks up the located object and casts it to ActionableObject (`0x00551590..0x005515CA`). With B9 true it then invokes GetPreActionPoses (`0x0055160C..0x00551648`). Inside that native recipient:

1. `0x005508EC..0x005508F2` rejects a null candidate with result `0x03000004`, before selection.
2. `0x005508F4..0x00550906` compares the candidate's numeric ID with the actual CarryingComponent ID at +8. Equality also returns `0x03000004` before selection.
3. The non-null, non-carried branch at `0x0055097A..0x00550988` calls `BlockWorld::SelectObject` with the candidate's ObjectID wrapper. This occurs **before** GetObstacles at `0x005509A0` and the later pose and height checks. There is no additional height gate before this writer in the callee's normal prefix.

Preserve selection at this guarded native recipient, including when a later pose calculation fails. Do not move it before these guards, omit it entirely, or add an equivalent mutation to unrelated public geometry queries.

The selected-state writer is `BlockWorld::SelectObject`, `0x00627148..0x006271EC`. It rechecks the located ID with family -1 at `0x00627154`. A missing ID logs InvalidID and returns false, preserving the previous selection. A valid ID clears the prior selected scalar at +0x60 to -1 when necessary (`0x0062715A..0x00627164`), stores the requested ID (`0x00627166..0x00627168`), logs and returns true. The caller at `0x00550988` ignores that bool and proceeds to GetObstacles. This writer does not emit a RobotDeleted notification.

A and B independently checked the prefix, selection recipient and order. The earlier conclusion that destructor cleanup alone cannot prove an Init writer remains valid.

## SF10 — helper failure callbacks precede the following manager phase

The existing checked M8 framework row 659 and helper extraction already establish this order. A and B independently reread these finite joins:

| Phase | Native source |
| --- | --- |
| AI before behavior manager | Robot.Update calls AI.Update at `0x00513EAC`, then BehaviorManager.Update at `0x00513EE6` on the normal branch that reaches the manager. Existing robot and vision gates remain in force. |
| Helper component inside AI | AI.Update `0x00569F24..0x00569F4C` runs analyzer, whiteboard, severe needs, HelperComponent at `0x00569F40`, then FreeplayDataTracker. |
| Terminal failure callback | After terminal helpers are stopped and popped and the stack is empty, status 0 selects and copies the failure callback at `0x0056E124..0x0056E12E`. `0x0056E134` clears stack maintenance callbacks **before** `0x0056E140` invokes the saved Robot& callback. Status 2 selects the success callback at `0x0056E114..0x0056E11E`. Other statuses do not create a completion callback. |

MarkFailed and storing helper status 0 do not themselves invoke the class failure callback synchronously. When failure status is available at the AI phase, the actual helper component can invoke the class's recovery callback before StackBlocks.UpdateInternal runs in the following manager phase. Do not move helper processing after manager validation or invoke the callback directly from MarkFailed to force a desired outcome.

This order does **not** guarantee that Ground recovery survives the subsequent S8 validity check. The callback may start recovery, then the manager's own StackBlocks validation may stop or cancel it because the actual cached valid set, whiteboard failure or physical relationship fails the sourced gate. Tests must preserve the cache timestamps and supplied scene, firmware and phase inputs. They must not bypass validation or force Ground completion merely because the failure callback ran.

SF9 and SF10 have independent B PRIMARY PASS. No broader scheduler or selection audit was performed. Existing owners and inherited boundaries remain unchanged; no official status was settled.
