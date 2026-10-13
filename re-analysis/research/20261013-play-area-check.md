# Opus check of R-PLAY-AREA (2026-10-13)

- **Checked:** `research/20261013-play-area-and-localize-failures.md` (Codex), against libcozmoEngine.so. Read-only
  cozmo-extractor.
- **Adopted:** by the manager on 2026-10-13. This file is the checked source for job B-M3M4-16 item 4.
- **Result:** no wrong value in the Codex report. One nuance is corrected (ClearDescendants), and several omissions are
  filled.

## P: the play-area event (M11-064)

- **P1, the gate:** if float(+4 + +8) < now (`vcmpe`/`bpl`, 0x00624F0E..0x00624F1E; an unordered compare skips), store
  now in +4 (0x00624F22). The constructor sets +4 = 0 and +8 = 60.0f.
- **P2, the value:** MemoryMap vt+0x1C = `GetExploredRegionAreaM2` (slot 0x01030980 → 0x006816E8). It returns the
  double at MemoryMap+0x40, which is processor+0x30, the explored area. No traversal.
- **P3, the event:** sEventF "robot.play_area_size" (0x00625314), "%.2f" (0x0062532C), with that double and empty
  metadata (0x00624F34..0x00624F4E). It comes before the list clear (0x00624F78) and the flag set (0x00624F7E).

**How the cached value is maintained** (M11-045's processor bookkeeping; these bodies are now read):

- **The type-change callback** (0x00688E50..0x006890A6):
  - newNoArea = (newType == 0) or (has children);
  - oldNoArea comes from the caller;
  - if oldNoArea and not newNoArea, add (S·0.001f)² (float, widened to double); if not oldNoArea and newNoArea,
    subtract it. S = node+0x18; the constant is 0x3A83126F;
  - **the edge area** (+0x38): subtract when old == 9 and new ≠ 9; add when new == 9 and old ≠ 9. There is no leaf
    test;
  - **the type sets:** mask 0x7C8 erases the old type and inserts the new one. InvalidateBorders always runs.
- **ForceSet** (0x00687518..0x006875E4):
  - oldNoArea is taken before the swap;
  - an incoming type 5 becomes a new MemoryMapData with type byte 1, both timestamps = the incoming +0xC;
  - the callback runs only when the type byte changed.
- **OnNodeDestroyed** (0x0068928C..0x0068934C):
  - subtracts the area if the children vector is empty and the type ≠ 0;
  - subtracts the edge area for type 9, with no leaf test;
  - then the set erase and InvalidateBorders.
- **Subdivide** (0x00687BEC..0x00687DC2):
  - the children are force-set to the parent's content, in vector order;
  - then the parent is force-set to Unknown;
  - the parent's earlier credit is never removed (its callback sees oldNoArea = newNoArea = 1). This follows from the
    bodies, and is the original's behaviour: reproduce it.
- **ClearDescendants** (0x00687E1C..0x00687E76):
  - pass 1, in vector order: recurse, then OnNodeDestroyed(child), on the already-emptied child, so it counts as a leaf;
  - pass 2 pops back to front and frees.

  *(Correction: the frees come after all the callbacks at that level.)*
- **TryAutoMerge** (0x00687F2A..0x006880AC): all children must be leaves and all contents equal, with no early exit.
  Then ClearDescendants, then ForceSet the parent.
- **SwapChildrenAndContent** (0x00687600..0x00687678):
  - swap the children vectors, and re-point each child's +0x7C parent;
  - then ForceSet(this, other.content) and ForceSet(other, the old this.content), each using the post-swap children for
    oldNoArea.
- **DestroyNodes** (0x0068768A..0x006876D2): per element, in order: recurse, OnNodeDestroyed(child), then reset and
  free. The child's vector is left full of nulls, so a non-leaf child never subtracts area here.
- **The callers of ForceSet** also include ShiftRoot (19 sites), UpgradeRootLevel and QuadTreeNode::Transform
  (0x0068846E). Their bodies, and so their area effect, are unread (M11-048).
- **Only three stores to processor+0x30** were found in 0x00680000..0x00690000: 0x00688E42, 0x00688EBE and 0x006892FE.

## L: the LocalizeToObject failure returns (M11-044's internals)

Every failure is sErrorF with an empty metadata vector, errG, and a conditional break. E = the existing object; O =
the observed one.

| # | Condition | Key / format | Return | Reachable with a robot |
|---|---|---|---|---|
| 1 | E null | ExistingObjectPieceNullPointer 0x00BE6B23 / "" | 1 | no (E is dereferenced before the call) |
| 2 | vt+0x18(E) ≠ 1, or ShouldIgnoreMovementDueToDoubleTap | UnlocalizedObject 0x00BE6B59 / 0x00BE6B82 (E+0x18) | 1 | UNKNOWN |
| 3 | O and GetComputedStateAt(O+0x1C) ≠ 0 | CouldNotFindHistoricalPose 0x00515BF8 / "Time %d" 0x00BE6C34, plus the KeyNotFound warning 0x00531D9C | the lookup result | UNKNOWN, leaning no |
| 4 | O null and robot-wrt-E fails | ExistingObjectOriginMismatch 0x00BE6BC9 / 0x00BE6BFD | 1 | no (Insert dereferences O) |
| 5 | historical pose wrt O fails | ObjectPoseOriginMisMatch 0x00BE6C3C / 0x00BE6C6C | 1 | UNKNOWN |
| 6 | AddVisionOnlyStateToHistory ≠ 0 | FailedAddingVisionOnlyPoseToHistory 0x00BE6CA2 / "" | propagated | NonFlattenedPose: no; TooOld 0x00BE8C0E/0x00531314: UNKNOWN; EmplaceFailed: dead |
| 7 | UpdateCurrPoseFromHistory false | FailedUpdateCurrPoseFromHistory 0x00BE6D82 / "" (plus the info GetWrtParentFailed 0x00513760) | 1 | UNKNOWN |
| 8 | SetLocalizedTo(E) ≠ 0 | SetLocalizedToFail 0x00BE6DB9 / "" (IdNotSet; MarkerOriginProblem 0x00512610) | propagated | UNKNOWN |

**Other details:**
- **The debug log:** "Robot.LocalizeToObject", only when E.ID ≠ Robot+0x2B8 (0x005154C4..0x005154F4).
- **The flattened height:** when Robot+0x2C4 is set, z = 0.1·z_new + 0.9·z_robot (0x0051570A..0x00515760).
- **Side effects on the failure path:** AddVisionOnlyStateToHistory increments Robot+0x2B0 and sets +0x2C6 = 1 before
  its own call (0x00515144..0x00515152), so they persist when row 6 fails.
- **Rejigger:**
  - it runs only when HasSameRootAs is false (0x0051584A..0x00515850), after the RejiggeringOrigins event (0x005158A8);
  - a failure goes through sVerifyFailedReturnFalse (0x00515926) and skips UpdateObjectOrigins, UpdateMapOrigins,
    UpdateFaceOrigins and Flatten.
- **SetPose:** only if history is non-null (0x005159B6..0x005159C8).
- **Success:** r7 = 0 and +0x2C6 = 1 (0x00515A88..0x00515A8C).

**LocalizeRobot** (0x0050D1CC..0x0050D4DA):
- an empty map returns 0;
- the single path is taken iff the map size is 1 and the key is the current origin;
- **single path:** LocalizeFailure is sErrorF, key 0x0050D54C, "Failed to localize to %s object %d." (0x0050D58C), with
  O's type and E's ID; errG; it returns the raw code;
- **multi path:**
  - a failed pair: CrossFrameLocalizeFailure, sWarningF, key 0x00BE6450, both arguments from E; fp = 1; continue;
  - a missing pair: MissingMatchedObjectInCurrentFrame, sWarningF, with O's ID;
  - StoringMatchedObjectID: info, channel "BlockWorld", key 0x00BE631E;
  - it returns `fp & 1`.

**Propagation** (new rows, M11-064):
1. AddAndUpdateObjects returns LocalizeRobot's result (0x00621304..0x00621320).
2. UpdateObservedMarkers: AddAndUpdateFailed (0x00BF9269) with errG, then the early return that leaves +0x74 = 1 (see
   `research/20261013-blockworld-flag-and-map-helper.md`).
3. UpdateVisionMarkers: sWarningF "VisionComponent.UpdateVisionResults.BlockWorldUpdateFailed" (0x00BFD9E0, format "",
   0x00654E12). It still calls DockingComponent::UpdateDockingErrorSignal when there are markers
   (0x00654E38..0x00654E4A), and returns non-zero.
4. UpdateAllResults: sErrorF "VisionComponent.UpdateAllResults.LocalHandlerFailed" (0x00654C7C), "%s Failed"
   (0x00654CB0) with "DetectingMarkers" (table 0x01034234), then errG, fp = 1 (0x006544EE), and continue. What fp does
   afterwards is unread.

## D: the robot-message dispatch to HandleCliffEvent

- **ProcessMessages:**
  - the enabled gate (0x0069D870), then RobotConnectionManager::Update;
  - the PopData loop exits unless the result is exactly 1;
  - per message: the count, the first robot, the empty-payload error, ShouldFilterMessage;
  - unpack with tag 0xFF; a size mismatch drops the message.
- **The tag:** TBH (0x007B1AE6..0x007B1B0C). 0xC0 → the CliffEvent constructor. Only 0xD9 is special-cased; everything
  else goes to Broadcast(robot ID) (0x0069DAB2..0x0069DABA).
- **Broadcast:** AnkiEvent {now, tag, union} → the emitter (0x0069DC82..0x0069DCB4).
  - The emitter: with a wildcard ID, every signal; otherwise the robot's signal, then the wildcard signal
    (0x0069DE24..0x0069DE74).
  - Subscribers run in registration order, synchronously (0x0051D874..0x0051D8A2; 0x0069E274..0x0069E2B2).
- **HandleCliffEvent's registration:** tag 0xC0 (0x00532B80..0x00532B8A).
- **The order against other 0xC0 subscribers is UNKNOWN.** 0x00519F8C has about 35 callers (IBehavior::SubscribeToTags
  0x005BCAA0, BlockTapFilterComponent, CubeAccelComponent, the actions).
