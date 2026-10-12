# Opus check of R-M4-CLIFF (2026-10-12)

- **Checked:** `research/20261012-m4-cliff-and-names.md` (Codex), against libcozmoEngine.so, libc++_shared.so, libDAS.so
  and the Java sources. Read-only cozmo-extractor.
- **Adopted:** by the manager on 2026-10-12. This file, not the Codex report, is the checked source for job B-M3M4-13.

## M4-037: pose-origin root names (fully read)

- **The warning.** It fires when a located ID differs from the connected ID (0x0053403E).
  - Key: the caller's prefix + "ActiveObjectInDifferentFramesWithDifferentIDs" (0xBE95B3).
  - Format: "First object=%d in '%s'. This object=%d in '%s'." (0xBE95E1).
  - Arguments: vec[0]'s ID (+0x18), its root name, this object's ID, its root name (0x0053406A..0x005340C6).
- **FindRoot** (0x0084520C) walks the node's parent at +0x30 for at most 1000 steps (0x008452C4, `cmp r5,#0x3e8`).
  - On overflow: `sErrorF("LoopBoundOverflow", {}, "%s:%d",
    "../../../../coretech/common/include/anki/common/basestation/math/poseTreeNode.h", 325)`, then errG and break.
- **GetName** (0x00844956) returns the string at node+0x38.
- **How origins are named:**
  - The PoseOriginList constructor sets next-ID = 1 (0x0084792C).
  - AddOriginWithID names the origin "Origin" + to_string(id) (0x00847BD8). It sets current = id and
    next = max(id+1, next). A duplicate ID goes to sVerifyFailedReturnFalse.
  - Delocalize calls AddNewOrigin (0x00510A66). The robot constructor calls Delocalize(false) (0x00510324), so the
    first origin is "Origin1".
  - Rejigger appends "_REJ" (0x00847F20) and Flatten appends "_FLT" (0x00847FD8). Both rename child nodes, which
    FindRoot walks past.
- **UNKNOWN:** the runtime IDs, which depend on the delocalization history.

## M4-043: the Save.Error text (fully read; the record's title was wrong)

- **The throw.** Save sets the exception mask to -1 and calls clear(rdstate) (0x0061B0B6..0x0061B0BA). It opens with
  mode 0x10. A failed open sets rdstate | 4, then clear (0x0061B580..0x0061B598), and libc++ clear (0x3B6D0) throws.
- **What is logged.** The catch calls PLT 0x004A8C98, whose GOT slot 0x01041C14 is `_ZNKSt9exception4whatEv`. That is
  std::exception::what with no virtual dispatch. libc++ 0x7C3C4 returns "std::exception" (0x7C3C8).
- **So Save.Error logs exactly "std::exception".** The full call is
  `sErrorF("BlockFilter.Save.Error" 0x0061B368, {}, "%s" 0x0061B380, "std::exception")`, followed by:
  1. errG=1 and a conditional break (0x0061B286..0x0061B29A);
  2. ~failure;
  3. __cxa_end_catch.
- **Not read:** which writes inside the try block can throw, the handler for selector != 1 (0x0061B314), and which types
  map to selector 1.

## M4-038: the detected-cliff path (read up to the BlockWorld/MemoryMap helpers)

**ComputeCliffPose** (0x00634980..0x00634A50):
- It tests the flags byte at event+4 first.
- It calls ComputeStateAt(event timestamp, out, true) on robot+0x390 (0x00634996..0x006349A6), which is owned by
  M11-051/053. On status 0 it copies the pose and returns 1.
- There are no float constants and no sensor geometry.
- **NoCliff:** sWarningF "CliffSensorComponent.ComputeCliffPose.NoCliff" (0xBFA513) with text "detectedFlags == 0!..."
  (0xBFA541); no errG; returns 0. HandleCliffEvent can't reach it, because it calls only with non-zero flags
  (0x0053557E).
- **NoHistoricalPose:** sErrorF (key 0x00634AAC) "Could not retrieve historical pose for timestamp %u" (0x00634AE4),
  then errG=1 (0x006349F2), a conditional break, and return 0.

**The caller** (HandleCliffEvent):
- It requires a result of exactly 1 (0x005355B8) before calling AddCliff, and ignores AddCliff's result.
- **Detected:** sChanneledInfoF, channel "Unnamed" (0xBE3FEC), key "RobotImplMessaging.HandleCliffEvent.Detected"
  (0xBE9368), format "at %.3f,%.3f. DetectedFlags = 0x%02X" (0x005357F4). The arguments are transform +0x20 and +0x24
  as doubles, then the flags byte.
- **Failure:** sErrorF "RobotImplMessaging.HandleCliffEvent.ComputeCliffPoseFailed" (0x00535788) with "Failed computing
  cliff pose!" (0x005357C4), then errG and break.

**AddCliff** (0x006249FE..0x00624A08) = AddMarkerlessObject(pose, type 15), returning 0. The steps:
1. **Construct.** Family 6, type 15, activeID -1, factoryID 0 (0x0050296A..0x00502982). The size is (20, 40, 50):
   0x41A00000, 0x42200000, 0x42480000 (0x0050271E..0x0050274A). +0x1C = robot+0x2C.
2. **Place.** The local pose is (0, 0, size.z·0.5). The candidate is input·local, reparented to the parent of
   Robot::GetPose(), then InitPose(state 1).
   - Robot::GetPose (0x004EA398..0x004EA46C) checks two things first:
     - "Robot.GetPose.BadPoseRootOrWorldOriginID" / "VERIFY(%s): WorldOriginID:%d(%s), RootID:%d" /
       "_pose.GetRootID() == GetWorldOriginID()";
     - "Robot.GetPose.PoseOriginNotWorldOrigin" / "VERIFY(%s): WorldOrigin: %s, Pose: %s" /
       "_pose.HasSameRootAs(GetWorldOrigin())".
3. **Overlap with an existing cliff.** BlockWorld+0x40 is looked up by the current origin ID, then family 6 with
   operator[], which inserts an empty entry if there is none (0x0062249A).
   - FindOverlappingObjects (0x0061FDFC) and its predicate (0x0061FD8C) require the same type and family, and
     IsSameAs == 1.
   - The tolerances come from the existing object: distance size·0.5 = (10, 20, 25) (0x00502B08), angle pi/4
     (0x004E0290).
   - On a match, the existing object's +0x1C = timestamp; the candidate is dropped and the call returns 0.
4. **Intersection.** When robot+0x2B8 != -1, families {2, 6} form the *allowed* set (filter+0x3C;
   0x0061EC60..0x0061EC96). Otherwise every family counts.
   - Only the current origin is searched (+0x6C/+0x6D = 0, 0x00622522).
   - The call is FindLocatedIntersectingObjects(candidate, vec, 0.0f, filter) (0x0062255E).
   - A hit drops the candidate. **The Codex report had this set backwards.**
5. **Store.** SetID, then AddLocatedObject, keyed by root ID (0x00622AD6):
   - DeleteIntersectingObjects with allowed types {14} and padding 0 (0x00622C68..0x00622C84);
   - +0x54 = the VizManager (0x00622D24..0x00622D30);
   - sChanneledInfoF("BlockWorld", "BlockWorld.AddLocatedObject", "Adding new %s%s object and ID=%d ActID=%d FacID=0x%x
     at (%.1f, %.1f, %.1f), in frame %s.") (0xBF8EF7), where the first %s is "" (IsActive returns 0);
   - sEventF("robot.object_located", {}, "%s", EnumToString(type)) (0x00622E1C).
6. **Broadcast.** BroadcastObjectPoseChanged(obj, null, 0) (0x00622E4E):
   - OnObjectPoseChanged, with the FirstPoseForObject / MultipleChanges logs behind BlockWorld+0x74;
   - BlockConfigurationManager::SetObjectPoseChanged (0x00624892);
   - MapComponent::UpdateObjectPose, which returns for family 6 (0x0067EA9C);
   - a tail call to 0x008CAD1C.
7. **Navigation map.** BlockWorld+0x54 = 1. MemoryMapData_Cliff((R·X).xy, robot+0x2C) (0x00684984). The quad is
   MarkerlessObject::GetBoundingQuadXY(pose, 0.0) (0x00502612..0x00502650). Then BlockWorld+0x58 =
   (u32)fmax(ts, helper+0x38()) (0x006226EA..).

**Not read** (new records below):
- IsSameAs;
- the intersecting predicate (0x00622908..0x0062297C);
- GetBoundingQuad<float>;
- SetID;
- InitPose / SetPose(state 1);
- the 0x008CAD1C tail;
- DeleteIntersectingObjects;
- the MemoryMap insert / QuadTree and helper+0x38.

## M3-055: the transfer path and `$player_id`

- **The transfer tasks.** Context+0x2C is a DasTransferTask and context+0x30 a GameLogTransferTask (an HTTP upload, with
  no record until now), created and registered unconditionally (0x004EA7BE..0x004EA892).
- **The trigger chain.** Java BackgroundConnectivity → JNI "received transfer request" (0x00667908..0x00667938) →
  cozmo_execute_background_transfers (returns if the engine is null, 0x006669F8) → ExecuteTransfers →
  StartDataTransfer (0x006A38D4).
  - StartDataTransfer has an in-flight gate and a Dispatch::Sync, and waits on a condition variable. The counter is
    unread.
  - The DAS task's OnTransferReady flushes (0x006A25FE). Its callback logs "das.upload.fail" / "das.upload" with
    "background", then completes.
- **The trigger is an app input.** It fires only from the Java alarm, and only when Wi-Fi is up and an HTTP GET to
  google.com returns 200 (BackgroundConnectivity.java:48-91). A phone with no connection never calls it.
- **`$player_id`.** The value is the lowercase hex MD5 of the device-ID string's bytes (0x665F52..0x665FAC, "%02x"
  0x0080F7F0). That string comes from libDAS platform+0x1C, which is filled by the Java `DAS.getDeviceID`: the first line
  of a file, otherwise a new UUID that it saves (DAS.java:159-242).
- **0x0052F2D8** is `sSetGlobal("$session_id", null)`, not `$player_id` (literal 0x0052F3E4).

## Manager decisions

- **M4-037 and M4-043:** fully read; they move to IMPLEMENTATION_GAP. M4-043's title is corrected to the binary's
  non-virtual std::exception::what.
- **M4-038:** split under the cross-layer rule.
  - Its own part (ComputeCliffPose, the caller's logs, AddMarkerlessObject steps 1–7 as rows) becomes
    IMPLEMENTATION_GAP.
  - The unread helpers go to new RECOVERABLE_GAP records: BlockWorld helpers under M11-vision, the MemoryMap insert
    under M13-navigation.
  - ComputeStateAt stays with M11-051/053.
  - The M4 build emits visible MISSINGs naming those records at the handoffs.
- **M3-059, the background-transfer trigger** (COMPATIBILITY_POLICY, scope rule): the stack never calls
  ExecuteBackgroundTransfers. This is the original's own behaviour on a phone with no connection, which is the stance
  M3-051 already takes. The DasTransferTask and GameLogTransferTask paths are therefore unreachable.
- **M3-058, the device ID:** the stack keeps a persisted device-ID string in its own settings, created once as a new
  UUID. The MD5 and the sSetGlobal are exact (M3-055).
