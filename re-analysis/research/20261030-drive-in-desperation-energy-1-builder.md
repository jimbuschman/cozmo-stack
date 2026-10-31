# DRIVE-IN-DESPERATION-ENERGY-1 — builder report

2026-10-30. Requested worker startup: DEFAULT model and effort omitted; actual backend unobserved. This report was written after the frozen reference/replay inventories; it was not among those 9,159 inputs. This is a bounded build, not a completed behavior or hardware acceptance.

## Entry, evidence and build

The shipped Needs_SevereLowEnergyState config binds through BehaviorContainer factory 0x0059CA9A to the Energy-only native owner. Its checked path includes base admission and the class 1/0/0 gates, initialization/profile, wait, cube cooldown and farthest/first-tie selection, no-cube round count, context RNG and full-orientation random drive, PAP/turn/animation children, action callbacks, treads interruption and teardown. Primary evidence is reader A `5a05bd530b926c81fb8c6c6da309c36e74f4a692d23d713524ad1a195280f6d3` and independent B `96016919e84e2ad628108e7f064c74508307814f731d04b47c2095e6fc6f838e18b5de7cf4e2989e1`, engine SHA256 `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`. Source question 1's profile join was resolved by A `9d260fb1631164d079cb7511d07359c03d77557534d3a1f7fa46a2d7c8c5e05e` and B `e4a9e22a5f3bc89fc744d7aa3445bd360f6ef8dbf459d9f92f50074b5dfa46d8`; source question 2's RNG/angle owner and float sequence resolved by A `815100205d87c58c9b1d7bab114ecb8d702a8169ce5ba82b0563f37c502a439f` and B `aa1366f239c2caca4440c121736430bae10c17b641d04c2f19388a0374c573a6`. Approved M7 shell rows held; no correction was needed.

The profile copies the ten floats and custom byte to the actual shared PathComponent slot before enabling it; Stop clears its enabled byte after cancellation, preserving stored values. The random pose draws angle/sign/distance from the shared context generator, uses factor `0x3C8EFA35`, rescales f32 radians, composes the full quaternion and translates forward. Source-defined same-live-owner post-call writes are retained. Repair's legacy standalone path remains unchanged. FreeplayTool now prints an explicit MISSING warning when its separate standalone custom driver binds the legacy Energy implementation; the XML description identifies that candidate and its source differences. This diagnostic/doc change does not change robot behavior.

## Remaining MISSING and fidelity impact

- **M8-013:** native NeedsExpression parent admission is not bound. Energy admission preserves the previous activity and reports this boundary. The supplied-manager activation tests do not establish parent admission.
- **M12-010/M8-011:** sentinel SearchForBlockHelper `(-1,1,1)` is unbuilt; the owner reports the gap and does not synthesize a helper or result. Its `ID=-1` search-success/most-recent-object continuation is therefore not established in this build.
- Shared planner, pose, perception, action completion and typed-report owners remain under their current records; no whole existing record is proposed for closure. Propose one new Energy own-lifecycle record only, with these dependencies explicit. No official manifest, inventory, approval or status was changed.
- The legacy standalone Energy implementation is a reachable custom driver with known source differences; the new CLI warning makes that visible. Neither it nor canonical Energy is claimed fully finished.
- Explicit regression gaps: PAP error/in-position and look/request outcome branches; cooldown NaN and root mismatch; concrete retired-owner M1-034 cases. No hardware was run.

## Review and validation

Three reviewer fix rounds, including the late standalone diagnostic; one root-directed source question 1, source question 2 resolved as above; one post-test harness correction; zero compiler corrections. Reference1 built successfully but had 687 pass/1 fail/0 skip; B and manager classified the failure as a test seam ignored by actual NativeBaseInputs. The test was corrected to supply canonical CarryingOwner and Sensors state, without changing production gates. The failed TRX is retained in scratch.

Reference2 and independent Replay1 passed exactly 688 cases across 20 classes using the same compiled inputs, before the later diagnostic-only patch. Reference3 built/tested/shut down with exits 0/0/0 and passed 12/12 targeted cases. Its wrapper exited 1 because postflight recorded an unidentified `dotnet.exe` PID 15708 with null command line; it was not killed. A separate manager follow-up later found no processes. Independent Replay2 subsequently passed exactly 12 cases, same compiled inputs, with test/shutdown/wrapper exits 0 and actual pre/post/checkout arrays empty. Input and binary snapshots in each passing replay were unchanged: 9,159 inputs and 187 bin files, no exclusions. The metadata report itself was written afterward. Tests demonstrate the cited exercised paths; they do not establish provenance or close the listed gaps. One full integration suite remains the manager's gate.

## Frozen files and provenance

Final ten source/test files were frozen in scratch `boundary-freeze.json` (SHA256 `942885bfe67a34aeb4155d39273899029f3510416b3d5624b42e9fc1760ca566`). The actual before-byte archives and raw/filtered Git blob IDs were captured before each first edit in `before.json`, `extra-before.json`, `robotpath-before.json`, `runtime-before.json`, `m7-before.json`, `harness1-before.json` and `boundary-before.json`, with corresponding `.bin` files. `before.json` records genuine prior absence of the new native owner and its test. These scratch artifacts are not part of this commit.

| File | SHA256 |
|---|---|
| `cozmo-stack/src/Cozmo.Robot/Behavior/BehaviorContainer.cs` | `5c087317aca46164b7133318b8315ffc7790bad03d28d9b9bd1d4ed64b8d6f73` |
| `cozmo-stack/src/Cozmo.Robot/Behavior/IBehavior.cs` | `d9e197124a2ae69e3214b29251ee5483f7d098d53924ddad9edb1c2e0c2efe21` |
| `cozmo-stack/src/Cozmo.Robot/NativePathComponent.cs` | `cbbb08dda75e21532c78e0a0b466d05bc1c791cd91e8b85192d79ae14fba4a72` |
| `cozmo-stack/src/Cozmo.Robot/Manipulation/RobotPath.cs` | `ccafbe17daa4287ca79a7a65a496879082697c291017c76ac6f03f4769a372d5` |
| `cozmo-stack/src/Cozmo.Robot/Behavior/NativeDriveInDesperationBehavior.cs` | `2de78015399ff321ec3ab304271edb86595039d8842b14b9be72740b880e6d48` |
| `cozmo-stack/tests/Cozmo.Protocol.Tests/DriveInDesperationNativeTests.cs` | `1d0e5404609a08934b9ab089cc62687ad0e00bbb48f3499f7bc9437a712d4fff` |
| `cozmo-stack/src/Cozmo.Robot/Behavior/NativeBehaviorRuntime.cs` | `030a7b6d7dc81de74b9589622e2e73d74cfdf194baa738a08ca79df6ebaf5730` |
| `cozmo-stack/tests/Cozmo.Protocol.Tests/M7BatchThreeBTests.cs` | `7529dfe6afc9f93db2dd557aa639708cc47564c40a7c02332d9fd9f11e7ea083` |
| `cozmo-stack/src/Cozmo.Robot/Behavior/ExplorerBehaviors.cs` | `6db2db84bec1db595f00d1952222a123eec4ae24ac8eba4aa1a5a04a69d41452` |
| `cozmo-stack/src/Cozmo.Conformance/FreeplayTool.cs` | `aa6fba9a32d457eefd7b5dd61fd3acae0e854d8d8c146096f42e56ab1dddd198` |
