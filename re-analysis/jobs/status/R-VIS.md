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
