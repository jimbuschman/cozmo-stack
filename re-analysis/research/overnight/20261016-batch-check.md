# Opus checks of OV68, OV76, OV78, OV79, OV80–OV83, OV86, OV87 and OV91–OV93, 2026-10-16

- **Checked:** three read-only cozmo-extractor passes, against libcozmoEngine.so (Thumb, capstone with skipdata).
  Scripts are in the session scratchpad: `chkm11\`, `chk8083\` and `chkmisc\`.
- **Adopted:** by the manager on 2026-10-16.

## Research-model quality

| Answer | Model | Verdict |
| --- | --- | --- |
| OV68, OV76, OV78, OV79, OV80, OV81, OV82, OV83 | Codex | Hold on every row re-read; some omissions |
| OV86 | Nemotron | Contradicted in several places; the census wasn't done |
| OV87 | Nemotron | Weak: it cites Ghidra rows, and its "dead code" claim is wrong |
| OV91, OV92, OV93 | Nemotron | Plausibility-filled: wrong function identities, inverted branches, wrong constants |

Together with OV88, OV89 and OV90 (the other checks of 2026-10-16), **eight of the nine Nemotron answers in the 2026-10-16
batch failed.** OV85 was the only usable one. **Research goes back to Codex.** Nemotron output is not used as
evidence.

## M11: poses and vision

- **M11-088 moves to IMPLEMENTATION_GAP** for the ActiveCube and Charger +0x50/+0x5C slots, and the three
  invalid-state branches in UpdateObjectPose:
  - invalid → invalid: the BothStatesAreInvalid error (0x0067EAF6);
  - invalid → valid: AddObservableObject (0x0067EACA);
  - valid → invalid: Remove, with the old root ID (0x0067EC3C..0x0067EC4C).

  **The slots** (from relocations): the Block quad thunk 0x004E690D, the base quad 0x0087713B, the markerless quad
  0x00502C33; +0x5C is 0x004E3883 (returns 1), 0x004E02AF (returns 0) or 0x004F04FB (CustomObject +0x9C).

  **IsPoseTreeHigh** gets the robot-relative pose (0x0067EDA2).

  **Unreachable:** CustomObject (SDK), and passive blocks in the world (there's no witness).

  **A new RECOVERABLE_GAP:** a census of the +0x50 callers, including the markerless quad (0x00502C32), which AddCliff,
  AddProxObstacle and AddCollisionObstacle reach.
- **M11-073 stays RECOVERABLE_GAP,** narrowed. **Row 18 is unreachable:** CreateFixedCustomObject is reached only
  from the SDK handler 0x0061E21A and the dev behaviour 0x005CC1C0. The operator= fact: assignment deep-copies the
  source's parent into the destination's own node (0x0084594A..0x00845A28).
- **M11-004 is split.**
  - **The non-unique closest-match and its fallback (D8–D14) are unreachable in scope.** In the shipped app, only SDK
    custom objects are non-unique.
  - **Corrections:** the evidence citation is 0x006280DA. The angle is never tested on this route.
  - **The stack's built predicate** differs from native in 9 ways (listed in the extractor report). It's marked
    unreachable, not corrected. Its comments and the two contradicting tests are fixed (job B-M11-15).
  - **Live-walker rows still to build:**
    - the connected walker copies the filter (0x0061F086);
    - a null slot warns and continues (0x0061EDE4/0x0061F1E2);
    - the origin is read once per bucket.
- **M11-084 stays RECOVERABLE_GAP.** OV86 isn't evidence.
  - **New rows:**
    - `PoseBase.SetParent.UnownedParent`, a VERIFY that attaches anyway (0x00844A26..0x00844A7A);
    - PoseOriginList::Flatten re-parenting (0x00847F34..0x00847FBE, called from LocalizeToObject 0x005159B2);
    - the Rejigger caller (0x0051590C).
  - **The node layout:** a quaternion at +0, translation at +0x20, parent at +0x30/+0x34, name at +0x38, ID at +0x44,
    owner count at +0x48.
  - **Copying allocates a new node.** NotLastOwner means a child, or a shared_ptr, outlives the last wrapper.
- **M11-089 moves to IMPLEMENTATION_GAP** for:
  - E1 and E3;
  - PickGoals G1–G4 (0x005E682C..0x005E6C0A);
  - IsRunnable R1–R4 (0x005E6520..0x005E6824, 0x005E6C20..0x005E6C7E);
  - GenerateVantagePoints V1–V3 (0x005E6C94..0x005E7006). The order is 0°, then −step, then +step. The ray's first point
    is P + n·(behind + 55.9) and its second is P − n·(front + 32.1);
  - the config map (0x005E6236..0x005E62CC; shipped values false, 105, 110, 15, 0, 10, 20, 1);
  - the sort's main body: ascending, nearest first (0x005E8350..0x005E8712).

  **HasBorders has no caller** in the shipped engine. A full direct, PLT, relocation and vt+0x28 scan found none. It's
  recorded as unreachable and not built.

  **A new RECOVERABLE_GAP:** the four libc++ sort helpers, and the rest of BehaviorVisitInterestingEdge's lifecycle
  (InitInternal 0x005E7018 .. Flag* 0x005E815A).
- **M11-090 moves to IMPLEMENTATION_GAP,** as notes:
  - **(a) The capture finalizers** fold into M8-027. A full census of six signal kinds finds every callable trivial.
  - **(b) The allocator is memory-only.** The engine imports only Mat::deallocate and no UMat API. The route:
    Mat::release (core 0x00024090) → deallocate (0x000816B4) → unmap (0x0007F8E6) → StdMatAllocator::deallocate
    (0x00080CB4).
  - **(c) Block_Cube1x1 is live:** it's made in the ctors of BehaviorCheckForStackAtInterval (0x005D725A) and
    BehaviorAcknowledgeObject (0x0060301C). Ramp, Bridge, FlatMat, Platform, HumanHead and Block_2x1 are created only by
    their own clone functions, so they're unreachable.

## M15: freeplay

- **M15-055 moves to IMPLEMENTATION_GAP** for F1, the forwarding (0x005AD9D0..0x005AD9DE). Its title's spark-state part
  belongs to M15-059.
  - **A new RECOVERABLE_GAP:** ActivityFreeplay's put-down kick-out handler. Its body is read (0x005AD9F8..0x005ADA7E,
    Info `ActivityFreeplay.RobotOffTreadsStateChanged.KickingOutActivityOnPutDown`); its subscription site isn't.
  - **M8-012 note:** BehaviorManager::Update discards the activity Update's return value (0x005A2F70..0x005A2F8A). A
    missing activity dereferences null at 0x005A2F7E (M1-034; reachability depends on app input).
- **M15-056 moves to IMPLEMENTATION_GAP:**
  - **Feeding F1–F5:**
    - the subscription (0x005AB030..0x005AB0A8), with no interface check (M1-034 at 0x005AB03A, unreachable in the
      stack);
    - connect and disconnect (0x005AC9CC..0x005ACAE2);
    - state 2 (0x005A6100..0x005A6206);
    - **the deselect order:** SetControllerState(2) for each controller, then destroy the map, then release the handles
      backward (0x005ABB5C..0x005ABC46).
  - **GuardDog G1–G4:** while it isn't running, the handler is a `bx lr`, so a disconnect is ignored. While running,
    any disconnect makes it StopActing and set +0x11C = 8 (0x005F4E30..0x005F4EB8).
  - **CubeAccel A1–A4.**
  - **A new RECOVERABLE_GAP:** the rest of the Feeding lifecycle, SetCubeLights, and RemoveListener 0x006356AC.
- **M15-052 moves to IMPLEMENTATION_GAP** for A1–A13, plus new rows:
  - **the init order:** InitExperiments runs before NeedsManager::Init (0x004EC9D6);
  - **the AB-disable predicate** (0x007EE968..0x007EE986);
  - **tag registration:** "app_user_d0", plus dynamic language and country tags (0x006A517C..0x006A5256,
    0x006A53CC..0x006A551A);
  - **the app_user_d0 predicate:** 0 ≤ now − NeedsState+8/1e6 < 86400 (0x006A531E..0x006A536A);
  - **the tag cache,** computed once (0x007EF370..0x007EF37C);
  - **per-robot activation on HandleRobotSetBodyID,** with the decimal body ID as the user (0x0053705A..0x0053706C,
    0x006A5744..0x006A575E). It's exact by default.
  - **Contradicted:** M15-052's evidence says report_test_auto has tag "app_user_d0". In the shipped experiments.json its
    tags are `[]`.
  - **The locale** (language and country) is an app input, which the stack supplies under the app-boundary rule.
  - **A new RECOVERABLE_GAP:**
    - NeedsState+8 at InitExperiments time;
    - FindExperiment, FindAssignment and FindVariation (PLT 0x004CA6F4, 0x004CA700, 0x004CA76C);
    - the AssignmentDef signal subscribers;
    - where +0x4C and +0x58 come from;
    - an M1-034 point at 0x007ECA9C.
- **M15-045 moves to IMPLEMENTATION_GAP** for the destructor bodies:
  - the member map (0x00569A88..0x00569B3E);
  - BehaviorHelperComponent (0x00569D30..0x00569D7E);
  - ~vector<BorderSegment> (0x00569F86..0x00569FB8);
  - the ProcessInfo tree (0x00569FBA..0x0056A006);
  - the ObjectInteractionCacheEntry (0x0056A06E..0x0056A13E);
  - the SevereNeeds, AIWhiteboard and RequestGame handle containers;
  - Workout.

  **A new RECOVERABLE_GAP:** the concrete std::function and IHelper types, the BorderSegment pointee, the AIBeacon,
  PossibleObject and FailureInfo destructors, and the BehaviorEventAnimResponseDirector D0.

## Other subsystems

- **M8-027 stays RECOVERABLE_GAP.**
  - **The E2G signal mechanism** is read (dtor 0x006634B8..0x006634E4, unlink 0x006634F4..0x0066352C, node release
    0x0066352E..0x00663562, dispatch 0x00663C74..0x00663CB8). The E2G ProtoHandle release (0x00664708) **is** reached,
    through the ScopedHandleContainer dtor.
  - **The release slots of the 8 kinds:** 0x004EF138, 0x0051D8F8, 0x00591550, 0x0064E618, 0x00664708, 0x00664EB8,
    0x006A3EFC and 0x0083BED0.
  - **Still open:** the 9 non-trivial void() callables, and which of them the void()/Dispatch signals hold.
- **M12-046 moves to IMPLEMENTATION_GAP,** narrowed to SetCarryingObject (0x00633E78..0x00633F0E):
  - null → sErrorF NullCarryObject, then `_errG`, break, and no state change;
  - otherwise state and ID; SetLocalizedTo(null) if this was the localization object; then SendSetCarryState(2 or 1)
    (0x00632C44..0x00632C6E, tag 0x49).

  The other four items are already owned by M11-062, M10-001, M11-053 and M11-044.
- **M1-054 moves to IMPLEMENTATION_GAP:**
  - **ReInit:** an Info, not an sEvent; +0x10 = 0 unconditionally (0x004EC7B4..0x004EC80E);
  - **the isMember gates,** in the order AdvertisingHostIP, RobotAdvertisingPort, UiAdvertisingPort. A miss gives
    sErrorF and return 1;
  - **UiMessageHandler::Init:** a **non-zero** result gives an error and returns it;
  - **InitInternal:** vtable 0x0101E488 +8 = 0x004EDC6C; a non-zero result gives sErrorF "CozomEngine.Init" (the binary's
    own typo);
  - **then** LoadRobotConfigs, InitExperiments, NeedsManager::Init, …, RobotManager::Init;
  - **BroadcastAvailableAnimations** (0x0052E89C..0x0052E916; names 0x00588190..0x0058826A, sends 0x0052F878..0x0052F95C).
  - **A new IMPLEMENTATION_GAP:** CozmoEngine::Update calls BroadcastAvailableAnimations, then SetEngineState(3), when
    DoNonConfigDataLoading returns 1 (0x004ED5F0..0x004ED60E). Whether the first broadcast sends zero names is
    RECOVERABLE.
  - **The engine config JSON** is an app input (COMPATIBILITY_POLICY).
