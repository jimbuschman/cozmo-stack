# Drive-off threshold regression — DONE (builder, 2026-10-22)

Operator-directed bounded build, baseline 33bcf83d. Two new production-ingress tests in
`cozmo-stack/tests/Cozmo.Protocol.Tests/M13DriveOffThresholdTests.cs`. No production change, manifest change,
settlement, hardware run or acceptance claim. The missing groundwork file was explicitly waived by the operator;
the existing checked sources below supply the expectations.

## Path and evidence

Fake transport receives RobotState -> synchronized sensor dispatch -> SetOnCharger -> the real shared-world
ObserveChargerFromContacts and ordered contact/platform effects -> native state-history admission ->
CliffThresholdSchedule/UpdateCurrPoseFromHistory -> frame-match schedule -> engine tick's world-footprint platform
step -> real SetCliffDetectThreshold sends and queued public platform/contact reports. Each input uses the current
origin and frame; each asserts LastFullStateResult=0 and the resulting owned pose X. No SetPose, origin rejection,
world insertion, history-result fixture or action-result substitution supplies the tested state.

- M4-019 SC4/SC4e/C7: constructor initializes accumulator -1 and one-shot false; first matching state sends 50;
  0x00512E8A loads 50.0f (0x42480000), 0x00512E96 BLE retains 50 at exactly 50 mm, and 0x00512E9C..EA6 sends
  400 once above 50. Read the checked M4-control rows and existing native instruction dump
  `research/20261006-M3M4-rows-native.txt`; no new extraction or behavioral inventory was created.
- M4-019 C8 / M13-017: platform step 0x00513CD8..0x00513E2A; setter 0x00511D44..0x00511DCA queues the platform
  event at 0x00511D8A before the threshold send at 0x00511DA0. The tests observe the existing broadcast counter
  at the actual send and then the delivered public event. Host callbacks drain later; these tests do not claim
  callback execution precedes the wire send.
- M13-040: contact observation is checked in `research/20261021-b-m13-9-b-m10-2-claude-check.md` and
  `research/20261022-b-m13-10-b-m13-11-claude-check.md`.
- Charger footprint uses ObservableObject::GetBoundingQuadXY 0x0087713A, as confirmed by
  `research/20261022-r-m13-017-rest-claude-check.md`; no Markerless/Ramp/HumanHead path is substituted.

## Tests and old-code check

1. `DistanceRestores400OnlyAbove50WithoutAPlatformExitThenFootprintExitReportsFalse`: contact on/off at X=0,
   then X=25.6,49,50 retains 50; X=51 sends only 400 while platform stays true. X=85 still overlaps; X=87 exits,
   publishes platform false and sends another 400. Subsequent X=100 sends neither again.
2. `UsedDistanceScheduleLeaves50UntilFootprintExitAfterContactsDrop`: admitted X=0,60 first consumes the lifetime
   one-shot, then contact on/off at X=60. X=85.6,110,145 retains 50, including after another 50 mm of travel;
   X=147 exits and sends 400 with platform false. Docking never resets the one-shot (SC4e). X=160 adds no send.

Both are reach: production for the threshold/contact/platform telemetry path. Straight zero-heading movement means
drive-centre displacement equals X displacement (the fixed -20 mm offset cancels). The charger dock transform and
robot back extent put separation between the deliberately non-boundary samples +85 and +87 mm. These tests check
observed states and outputs, not an independently recomputed call to the footprint helper as their oracle.

Current baseline already passes, so no stack correction is justified. For the old-code check, replaced only
SetOnCharger with its exact historical method from 0138af17^ (166a2e6a), ran the new tests, and restored Sensors.cs
byte-for-byte in a finally block. Both fail at the immediate contact-off assertion: Expected 50, Actual 400.
This is an isolated historical-method rollback, not a claim that the whole historical checkout was rebuilt.
The old omission is the already-fixed contact-charger observation; the tests now cover its admission/pose/footprint
continuation rather than substituting an owned pose and rejecting the packet's origin.

## Validation and limits

- Historical method: 2/2 fail with Expected 50 / Actual 400. Logs in ignored `.scratch/m13-drive-off-old-details.log`.
- Restored current code: 33/33 pass, zero skipped, filter M13DriveOffThresholdTests | M13ContactChargerTests |
  RobotStateHistoryAdmissionTests. Log `.scratch/m13-drive-off-focused.log`.
- `fidelity.py --check`: PASS, 707 records. `git diff --check`: PASS.
- Production hooks/seams/MISSINGs added: none. Existing SendFault is a read-only observer returning null to keep
  the normal transport send. No source log keys changed; log-key pre-check has no production input files.
- Added outside requested scope: none. No PROJECT_STATE or queue ownership change.
- No MISSING needed to implement these bounded threshold expectations. Existing shared configuration/map/cache
  and behavior-action/completion gaps remain outside these tests; they are not settled or claimed effect-free.
- No physical DriveOffCharger action, full M13-017 fidelity, arbitrary-rotation arithmetic or hardware success is
  established by synthetic incoming telemetry. Manifest evidence/status/test fields are unchanged.
- Commit: this report and the test file are in the same commit (`git log --follow --oneline --` this path).
  Push gate validation is reported in the builder's final response.
