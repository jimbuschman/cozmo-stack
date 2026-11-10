# Codex pet report, tracked face, motion history and localization batch: Claude check (manager, 2026-11-10)

Merged from `codex-m13-manager-2` at 9c6a1cae. Codex merged main into its branch (no rebase). Its gate on 667ebd55
passed 8304/8304. The first gate on 4d00a96f failed 11 legacy cache-seeding fixtures; the fixtures were migrated to
native inputs and production code did not change.

## PET-REPORT-SETTLEMENT-1: PASS; M14-014 → EXACT_SOURCE, M14-004 → EXACT_SOURCE

**PetWorld::Update 0x0050B7F4..0x0050BAA0, re-disassembled:**

| Step | Address | Behaviour |
| --- | --- | --- |
| Prior-map lookup | 0x0050B838..0x0050B874 | Lower-bound search of the old map by signed ID |
| Known ID | 0x0050B878..0x0050B8C2 | Copies the new input, count = old + 1 (0x0050B8A8..0x0050B8AE), inserts with no IMU test |
| Absent ID | 0x0050B8C8..0x0050B8F4 | Rotating-too-fast test on robot+0x258 with the input timestamp, body 0x3F060A92, head 0x3E32B8C2, mode 5 if the tracked flag (+0x19) is set, else 0 |
| Accepted absent ID | 0x0050B8F6..0x0050B93A | Inserted with count 1; a rejected one is skipped and doesn't stop the loop |
| Map swap | 0x0050B96C..0x0050B99E | The new map replaces the old; missing IDs disappear |
| Report loop | 0x0050B9A2..0x0050BA9A | Signed ID order. Untracked entry (+0x25 == 0): VERIFY count == 1 (0x0050B9B2..0x0050B9C6), then robot.vision.detected_pet with the type. Every entry: RobotObservedPet from id, timestamp, count, score, rect, type (0x0050BA2A..0x0050BA60), Broadcast 0x0050BA68 |

`PetWorld.UpdateNative` (Faces.cs) and the queued stock pet path (VisionSystem.cs) follow each step, including the
first-wins insert for a duplicate ID and the VERIFY's error effects. The Broadcast gate and recipient order were
checked on 2026-11-09. StockPetDetectorTests drives the shipped detector corpus through the original owner.

**M14-014 inventory correction:** the record was a RECOVERABLE_GAP ("body and payload unread") in the approved M14
inventory. The body is now read, so its provenance, authority and evidence were rewritten from the table above, and
M14-faces was re-approved under the standing authorisation for cited inventory corrections. M14-faces now holds no
live-path RECOVERABLE_GAP, so `source_investigation_exhausted` is true.

**ReactionTriggerStrategyPetInitialDetection::ShouldTriggerBehaviorInternal 0x00611AE4, re-disassembled:**
- RecentlyReacted (0x00611AF0): if true, log, UpdateReactedTo (0x00611B36) and return false.
- For each pet in PetWorld: a reacted-to ID is logged and skipped (0x00611B6E..0x00611BB6); otherwise count > 2
  (`cmp r1,#2; bgt`, 0x00611BB8) inserts it into the ordered target set (0x00611C0A).
- Targets present and robot+0x34A (on charger) set: log and refuse (0x00611C32..).

`PetInitialDetectionStrategy` matches, with the f32 cooldown (RecentlyReacted 0x00611DD0..0x00611E14). InitReactedTo
0x00611FA4 has no caller and is not on the path.

**Separate records:** stock detection M11-016; IMU input and history M11-033/M11-098. Supplied non-stock pet profiles
keep the legacy host adapter, a stack input outside M14-014. The host PetObserved callback is a stack-only view.

## TRACKED-FACE-SETTLEMENT-2: PASS, no settlement

I re-disassembled DetectFaces 0x006B37BC..0x006B37EC: the pose is composed with respect to the root, the parent is
cleared (0x006B37E4) and the detached transform stored at +0xFC (0x006B37EC). The public HeadPose now carries it.

M14-001 stays IMPLEMENTATION_GAP. Two things remain:
1. **libm (cosf at 0x0087DCD6, sqrt in the cache distance):** this does not need a parity proof. By the M13-023(e)
   precedent (M12-019, M14-005, M14-003), a path whose only difference is the host's libm settles as
   EQUIVALENT_IMPLEMENTATION.
2. **The legacy steering readers** (manual Interact, face turn and fine-tune, the flat candidate selector, the flip
   side check) still read the host projection. The handoff calls them a host compatibility path, but they drive robot
   behaviour, so each must be either a native reader or shown to be outside M14-001's supported path.

TRACKED-FACE-LIBM-PROOF-1 is rescoped to item 2 and ends with an EQUIVALENT_IMPLEMENTATION proposal.

## M11-097-TRACK-MOTION-HISTORY-1: PASS (source only)

No supported constructor or queue caller for TrackMotionAction was found; M11-097 keeps the MISSING join. No code
changed.

## M11-044-LOCALIZATION-MARKER-CAMERA-1 and M11-044-LOCALIZE-SAME-ORIGIN-1: PASS, no settlement

I re-disassembled Robot::Update 0x00513C5C..0x00513CD4: the first-state gate (+0x34E), the calibrated vision update
whose failure returns before the send, then the pending flag +0x2C6, the send (0x00513CC6) and the clear
(0x00513CCC), before the map update. The stack follows this order. M11-044 stays open; the five follow-up jobs
Codex proposed are released in its order.

## VISION-INIT-SETTLEMENT-2

Not started by Codex; it stays RELEASED.

**Records settled in this check: 2. Total: 36.**
