# HANDOFF DOCK-MARKER-WRT-SKIP-1 (Claude worker, 2026-11-11)

The census is `.scratch/DOCK-MARKER-WRT-SKIP-1-census.md`, rows S1..S19.

## What changed

IDockAction::Init's marker choice (0x00551960..0x00551BE4) now has one implementation in the stack. Before this job,
DockActions.cs:426 composed a parentless pose and threw MISSING M11-066 whenever two or more markers were observed.

| Step | Address | Stack |
| --- | --- | --- |
| S7/S8: best = FLT_MAX, chosen = null, a temp pose per iteration | 0x00551ACA (0x7F7FFFFF from 0x00551DD4), 0x00551AD0, 0x00551AEA..0x00551AF4 | `NativeDockMarkerSelection.Closest` |
| S9: GetWithRespectTo(marker pose [marker+4], Robot::GetPose()), with the marker parented to the object pose by AddMarker (0x00876860) | 0x00551B06..0x00551B12 | same |
| S12: d2 = f32 (x·x + y·y) + z·z | 0x00551B1E..0x00551B3C | same |
| S13: d2 < f32(best·best); NaN loses | 0x00551B3E, `bpl` | same |
| S14: best = sqrtf(d2), chosen = this marker | 0x00551B4C..0x00551B8C | same |
| S15: skip with info "Actions" / "IDockAction.Init.GetMarkerWRTRobot" / "Failed to get marker %s's pose wrt to robot" | 0x00551B90..0x00551BD2 | same; called from DockActions, NativePickupObjectRunner and NativePlaceRelRunner (PickupObjectAction and PlaceRelObjectAction vtable slot +0x1C = IDockAction::Init) |
| S17: no winner, so GetCodeName(null) reads address 0 | 0x00551BE4 → 0x0055199A..0x005519A0 → 0x0087E1D0 | M1-034 skip with a visible log naming M1-034 and M12-017 |

**S17 in the stack:**
- **DockActionBase** returns BadMarker 0x03000002, the stack's earlier null-marker result.
- **The native runners** keep their NativeActionUnavailable throw.

**Behaviour change:**
- Two or more observed markers on a cube no longer throw; the nearer is chosen by the engine's rule.
- When every marker fails, the stack returns BadMarker instead of throwing M11-066.
- When every d2 is NaN, the result is BadMarker as before.

**Without a marker code-name input:** production never sets `BlockWorld.MarkerCodeNameInput` (M11-013). So S15 reports a
visible MISSING naming GetCodeName 0x0087E1D0 in place of the log line; no name is invented.

**Unsupported comment replaced:** the "cannot fail here" comment now cites S19 (see Open).

## Tests

All `reach: unit`. No production-reach test exists, because no fixture produces a two-marker sighting through RunAsync.

**M12RVisBuildTests**, replacing the parentless, self-derived test:
- `M12_017_TwoObservedMarkersOnAParentedCubePickTheNearerWithoutThrowing`
- `M12_017_AnEqualDistanceLaterMarkerWinsThroughTheSqrtResquare`
- `M12_017_AMarkerInAnotherRootIsLoggedAndSkippedAndALaterMarkerWins`
- `M12_017_NoWinningMarkerIsAVisibleM1_034SkipAndTheActionDoesNotSucceed`
- The class now joins the "SteppedBehavior missing-report statics" collection, because the S15 test subscribes to that
  static report (verifier item 3).

**HikingBringBeaconCandidatesTests:**
- `PickupLeafLogsEachMarkerWhosePoseWrtRobotFails` (new).
- Reach labels were added or normalised on the two existing ObservedMarker* tests now cited by M12-017.

**Results:**
- implementer: 468/468 affected;
- verifier: 411/411;
- full gate: see the commit.

## Verifier: PASS (no blocking findings)

| # | Finding | Class | Resolution |
| --- | --- | --- | --- |
| 1 | The S15 tests re-parent single markers to an unrelated root, which neither system produces; production can only fail every marker, through the parentless robot fallback | queued | Recorded in M12-017 and in the test labels. |
| 2 | `ObservedMarkerNoWinner…` had no reach label | queued | Fixed. |
| 3 | A possible flake: a reset/subscribe race on the static MISSING report | queued | Fixed by the collection. |
| 4 | S16 BeginDockingToMarker and S3 NoMarkers logs are not emitted (pre-existing, log only) | queued | Recorded in M12-017; not built. |
| 5 | M12-017 did not name the M11-065 arithmetic dependency | queued | Fixed. |
| 6 | The two stack S17 outcomes differ (BadMarker vs NativeActionUnavailable) | observation | The manager's call whether to align them. |
| 7 | S19 and Q-C need their own records before M12-017 is settled | observation | For the manager. |
| 8 | The no-code-name MISSING is deduplicated once per process | observation | Log only. |

## Open

- **S19 (UNKNOWN, RECOVERABLE_GAP):** can a current-origin object's pose be rooted elsewhere, so that S15 runs in the
  engine? This needs a read of the origin-move and re-parent writers (M11).
- **Q-C:** marker poses of non-cube, non-charger types are unparented (BlockWorld.cs:224). No dockable type of that kind
  was found in scope.
- **S2:** it still has two implementations, DockActionBase.GetObservedMarkers and NativeObservedMarkers.Get.

## Records

- **M12-017, M12-037:** `unresolved` and `test` only. No settlement.
