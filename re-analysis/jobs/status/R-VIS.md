CLAIMED claude-sonnet 2026-09-29

# R-VIS triage (manifest as of 5455f47)

Kinds: compare / missing source (MS) / cross-layer wiring (XL) / still blocked (SB).

## M11-vision
| id | kind | note |
| --- | --- | --- |
| M11-004 | MS | FindLocatedObjectHelper 0x61EB78 return + tie-break; connected lookup by ObjectID (pre-extraction item 2) |
| M11-007 | MS + contradiction | engine never sets Known on first sighting (item 3) |
| M11-017 | MS + contradiction | Detect/GroundPlaneROI/MapComponent (item 6); clear region is ClampQuad quad, squared-length compare; QuadTree Insert/Transform needs a record |
| M11-021 | MS | non-divisible CLAHE branch, ColumnSum<int,uchar> (item 7) |
| M11-033 | MS | RGB2GRAY kernel (item 8) |
| M11-035 | XL | handlers belong to M14/M10/M2 |
| M11-037 | MS | AddLiftOccluder points, BlockConfigurationManager gates (item 4) |
| M11-038 | MS + XL | Broadcast* and CheckMailbox (item 5) |
| M11-040 | MS | vision thread/mailbox (item 9) |
| M11-013 | policy-review C | COMPATIBILITY_POLICY contradicts the code -> IMPLEMENTATION_GAP (item 1) |
| M11-012 | policy-review text fix | provenance/evidence text (item 10) |

## M12-manipulation
| id | kind | note |
| --- | --- | --- |
| M12-008 | XL | needs M13-007's 15 mm FindObjectOnTopOrUnderneath callers |
| M12-011 | MS (depends M12-022) | |
| M12-020 | MS | PlaceRelObjectAction::ComputePlaceRelObjectOffsetPoses (item 12) |
| M12-022 | MS | DriveToObjectAction+0x84, InitHelper goal (item 11) |

## M13-navigation
| id | kind | note |
| --- | --- | --- |
| M13-007 | XL | three 15 mm callers (M11/M12 code) |
| M13-010 | SB | needs M7 mood history (Emotion::GetHistoryValueTicksAgo 0x4BC804); check M7 state |
| M13-014 | MS + XL | TurnTowardsLastFacePoseAction (item 13); +0xD8 soft-spark flag is M8 |

## M14-faces
| id | kind | note |
| --- | --- | --- |
| M14-008 | SB/XL | FaceWorld+0x24 flag needs M14-010 carrier; F8/F9 broadcasts M2/M10 |
| M14-009 | XL/SB | built pieces done; awaits M14-010/011 |
| M14-010 | SB | OKAO boundary (IFaceDetector seam carries no OKAO outputs) |
| M14-011 | SB | per-frame detector->recognizer carrier is M14-010 |
| M14-012 | MS | album byte vector serialization (read full unresolved) |

## Progress
- [x] pre-extraction rows verified (6 verifier passes; reports in research/20260929-R-VIS-verify-pre-*.md)
- [x] M11 gap1 (research/...-M11-gap1-extraction.md) extracted and verified (verify-M11-gap1.md: FAIL on 2 rows, core holds)
- [x] M11 gap2 QuadTree extracted and verified (verify-M11-gap2.md: 3 wrong statements, rest holds)
- [x] M12/M13 gap1 extracted and verified (verify-M12-M13-gap1.md: 4 wrong rows, priority checks hold)
- [ ] fold the checked rows into inventories M11/M12/M13, manifest records, --approve
- [ ] build per subsystem, verify, settle

## Progress (batch commit 1 of round 3)
- Built and verified-with-disclosed-gaps: M11 batch A (BlockWorld ObjectID/pose/localization machinery, M11-044 Insert/UseDiscarded/LocalizeRobot, Delocalize fields, OnTreads conditional clear, Dirty transitions, SetLocalizedTo, computed-state history with 3000 ms cull, firmware-order fix), M12 (dock/pre-action/flip/DriveTo* paths, M12-023..039), M13 (footprint, IsPreActionPoseValid, flip CheckIfDone). Final verifiers: M12 round 6 PASS; M11 rounds 1-4 FAIL each on genuine binary-checked defects, all fixed or recorded as visible gaps; round-5 changes (cliff schedule skip for the Delocalize state, negative test, comments) checked by the manager from the diff, not by a further verifier pass.
- No record was raised to EXACT_SOURCE; M11-044, 050, 051, 053 and others stay IMPLEMENTATION_GAP; M11-052 RECOVERABLE_GAP.
- Left for the next round: M11 batch B (quad tree M11-045/046, M14-007 rebuild, M11-017 entry points, M11-047/048), M13-024..027 builds, M11-038 broadcasts, M11-050 (UpdateVisionMarkers steps), M11-051 (AddRawOdomState gates), M11-052 (Interpolate blend), M11-053 (frame-id-mismatch Delocalize and callees), remaining RECOVERABLE_GAPs (M11-042, M12-024/027/028/032/034/038/039, M13-021/023).
- For the policy review: bionic libm equivalence; engine null-map dereference in M11-017 rows 48-49 not reproduced; DriveAndFlipBlockAction+0x100 uninitialised heap / null weak lock store to 0x140 not reproduced; whether border-walk order (M11-047) must be reproduced; re-approvals used the standing authorisation with no operator checkpoint visible to verifiers; several stand-ins remain labelled (nearer-raw-state pose, marker loop stub, LastImageTimestamp, offline ProcessImage path, 5 s lift wait/Timeout in FlipBlockAction).
