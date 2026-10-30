# FIX-DRIVE-IN-DESPERATION-ENERGY-1 — returned-fix builder report

2026-10-30. The original worker continued: reader requested Sol/medium, builder and independent reviewer requested DEFAULT with model/effort omitted; actual backends unobserved. This report was written after the reference and replay and is outside their 9,205 frozen input entries. This is a bounded fix, not hardware acceptance or subsystem settlement.

## Source and production path

A located cube selected after the real Idle completion enters DriveToCube. Its actual `GetNative` selects the target in BlockWorld before obtaining obstacles. A located charger can cause the host's `NativeActionUnavailable` only when it survives the carrying-ID and height filters and its missing footprint becomes a stand-in; not every charger pose does. Previously that host exception escaped through the synchronous RobotCompletedAction subscriber, preventing later subscribers and the ActionQueue deletion-tag cleanup.

Native `GetPreActionPoses` is called at 0x005D995C; 0x005D9960..996C branches to the existing LookAtCube continuation at 0x005D9A54 for its defined result/flag cases. The native engine has no corresponding host-unavailable exception, so the authorized fix does not claim a native failure or fabricate a PAP result. It catches only `NativeActionUnavailable`, reports M12-036, 0x005D995C and the actual reason, skips unavailable PAP/drive, and enters the real LookAtCube continuation. The earlier SelectNativeObject write is retained. The normal PAP-result branches are unchanged.

The unbuilt sentinel SearchForBlockHelper `(-1,1,1)` remains a gap. Native 0x005D97DA..97EE passes an empty failure callable; a real failure creates no behavior callback. Each actual host occurrence now emits a fresh M12-010/M8-011 MISSING saying search motions were skipped; no helper failure, success, result or callback is invented.

Source A `9d716f0da5c847aafdc1d92770fd392adc3e2e076eebbb7dabf31996a33c94b1`, independent B `fdc822e042da66bab272788246930cb47e16e7563e18e8443843d658e4443ae7`; both use engine SHA256 `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`. The manager checked A's seven current manifest snapshots literally. M12-036 remains IMPLEMENTATION_GAP. M12-043's PyramidBase interior-geometry title is not evidence for charger footprint. No existing record is settled or changed; retain the same pending Energy own-lifecycle proposal, without a duplicate.

## Changes and coverage

The owner catches the bounded geometry gap and reports each skipped sentinel occurrence. The tests use supplied manager activation, located cube plus height-overlapping charger, and supplied terminal child results through actual ActionList delivery. They assert the pre-obstacle selection write, later RobotCompletedAction subscriber, deletion guard cleanup, real look continuation, look success/failure gate, final turn and target clear. Two actual wait/drive completion paths check two search-motion warnings. The source-defined same-live-owner post-callback target clear is retained; no blanket generation guard was copied. Retired-owner/host lifetime behavior remains a queued shared uncertainty, not a new safety claim. Native NeedsExpression admission, charger footprint, sentinel helper, planner and other shared downstream obligations remain MISSING or under their existing owners. No hardware was run.

## Validation and provenance

Returned-fix reviewer fix rounds: 0; compiler corrections: 0; post-test harness corrections: 0. One unexecuted proof-setup revision added per-phase native process arrays and direct helper-input hashes. The original Energy job's three review rounds are historical and separate. B's candidate static review passed (`260786c8d747daafd95f5fe978064745820ad6d40dc8f17e66e40a7027d30e88`). Reference1 built/tested/shut down with wrapper exits all 0: 173 passed, 0 failed/skipped across 11 classes, including the eight previously validated DevTurn consumers. Independent compiled Replay1 matched exact class/method/display/outcome/multiplicity for all 173. Both preserved and compared 9,205 source/config/asset/official/helper input entries and 187 bin entries with no differences or exclusions. Original reference shutdown-post array captured transient PID 22112 with null command line; it was not killed, and overall postflight/checkout were empty. Replay's actual pre/post/checkout arrays were empty. Raw logs, TRX, snapshots and original arrays remain in the fix scratch directories. Tests support the exercised paths, not provenance or whole-record closure. One combined full integration gate remains manager-owned.

Actual before bytes/SHA/raw+filtered/index for both files were archived on baseline `81ee75bd051ecb53edaca1967a172054bc969fdb` in `.scratch/fix-drive-in-desperation-energy-1-build/before.json` and `before-0.bin`/`before-1.bin`. No new source/test files were needed. Final freeze `358f3bf692eec69c95ddedcd134ee53b0904079f27161dde81c5be703dcfd059` contains:

| File | SHA256 |
|---|---|
| `cozmo-stack/src/Cozmo.Robot/Behavior/NativeDriveInDesperationBehavior.cs` | `a512a61c339cb328b9720a92f22b0b797ce09273d9e5650f516e0b364f57d8d9` |
| `cozmo-stack/tests/Cozmo.Protocol.Tests/DriveInDesperationNativeTests.cs` | `abfde60a944eb51b8b2cbf2b925fca465cfa77ad8d64efe5df189d76a91a77ae` |

The root-provided untracked job packet is excluded from this commit; no official manifest, inventory, approval, state, review or Claude-check file was edited.
