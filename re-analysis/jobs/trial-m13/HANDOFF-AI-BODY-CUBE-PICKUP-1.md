# AI-BODY-CUBE-PICKUP-1

## Request and action

- The direct game message (tag 0x12, 0x525DB6–0x525DC8) and nested RobotActionUnion (tag 0x0C, 0x525DA0–0x525DB2) converge at 0x52A354. The factory branches on request +0x35 at 0x52A384. The new host request supplies an explicit signed-range world ObjectID and selects direct or drive. It uses the nested queue route's explicit position, zero retries and caller tag (0x52796C–0x527A8C), through the stack's AtEnd AI admission. The negative selected-object sentinel, caller-supplied angle and custom profile are outside this bounded request.
- Direct builds PickupObjectAction (0x553648), disables the pre-action pose test and uses ANY_CODE for the verification turn (0x52A408–0x52A438). DockWithObject still uses the selected observed marker. The drive branch builds the checked zero-approach DriveToPickupObjectAction compound (0x52A38A–0x52A3F0, 0x55C464–0x55C548) and proxies the pickup leaf's completion. The bounded profile passes false at +0x36 (tracks 7, wire flag +0x95 clear) and true at +0x37 (check above). The shared dock speeds are 60/200/500 and the Pickup constructor sets method 2 and +0xC1 true (0x55030A–0x55031E, 0x550382–0x5503AA, 0x5536EA–0x5536F8).
- The retained leaf resolves the current world ID, chooses low or high from the target height relative to actual MAIN, prepares the marker and top-space checks, and sends 0x42 (0x553A1C–0x553BE0, 0x551960–0x5521AC, 0x63BA44–0x63BD50). Firmware's B8 stores success before attaching the raw target and marker (0x533790–0x533850). Busy rise and fall, body stillness, load/motion timestamps and the saved-pose turn govern verification. The terminal tail uses actual carrying and the source ambiguity rule (0x5521AC–0x5523F4, 0x553BE0–0x5542C4).
- Pickup teardown retires the verify child and owned pose, then deselects and aborts active shared path/dock on the original robot, and clears the dock marker and target. The native destructor does this even if the action is still pending (0x553750–0x553790, 0x557448–0x55748C). Only reaction-lock removal has the later state guard (0x557514–0x55753C). The public task reports after unlink and cancels the exact runner on the original list.

## Live docking correction

- The canonical marker pass calls the correction after applying a nonempty set of admitted markers to the world (0x654DEC–0x654E4A). The producer checks the retained target, the body-Z IMU bracket at 0x41B758B4, exactly one stored marker with the retained code, and the computed pose at the same image timestamp. The relative transform must succeed (0x63BE9C–0x63C148, 0x656384–0x656466). It uses the original marker and pose ownership without substituting the legacy PnP or nearest-pose path.
- The 0x48 packet writes timestamp, f32 X minus the slot offset, f32 Y plus the slot offset, raw relative Z, and clamped yaw plus the double-precision pi/2 and offset narrowed once (0x63C14A–0x63C1CE). Clamp 0x877330 uses the signed dominant root axis, 40-degree gate and local rotation correction, with the native small-angle and cross-length guards. The two undefined stack bytes pack as zero under M12-014/SD2 (0x7C0B74–0x7C0B80). The send uses the original retained robot (0x63C548–0x63C57A).

## Files and test paths

- `Motion.cs` supplies the host request and retained queue completion. `CompoundActions.cs`, `NativeDriveToPickupBeaconRunner.cs`, `NativeDriveToObjectRunner.cs`, `DriveActions.cs`, `NativePathComponent.cs`, `DrivingAnimationHandler.cs` and `ManipulationSystem.cs` carry the exact action and path owner through queue, update and teardown.
- `NativePickupObjectRunner.cs`, `DockActions.cs`, `DockActionSubscriptions.cs`, `ChargerActions.cs`, `NativeVisionRunners.cs`, `NativeObjectRunners.cs` and `Lights.cs` retain the pickup preparation, verification turn and cleanup. The scoped dock lock reads the same 0xC55A21 table as ground placement (0x551C42–0x551C4C).
- `Docking.cs`, `NativeDockingGeometry.cs`, `VisionSystem.cs`, `CarryingSetter.cs` and `LiftGeometry.cs` bind the live marker/pose correction and B8 attachment to the admitted owner.
- `AiPickupControlTests` uses public Motion admission, a ticked action list, rendered markers and injected firmware messages. It exercises the emitted 0x42, queue and cancellation ownership, and replacement during both the carry wire send and carry notification.
- `NativeDockingCorrectionTests` reaches 0x48 through the live marker update, feeds IMU history and exact image timestamps, checks timestamp and wire flags, and checks clamp and yaw in a separate geometry corpus. The manager ran the shipped ARM clamp and yaw against a ten-case finite corpus, including root/local cases with a native pose parent chain.
- `ScopedDriveToObjectOwnershipTests` ticks retained drive runners with stale-parent and replacement-path scenarios and checks the successor's wire and path state.

The affected suite passed 439/439, including the new pickup, docking and drive-ownership tests, existing manipulation, path and vision routes, and fidelity validation.

- The full-gate fixture correction in `EngineAppLayerTests.cs` keeps the path and driving-animation continuation epochs outside the reconstructed-device field comparison and asserts that disconnect advances the path epoch. The focused gate correction run passed 11/11.

## MISSING

- Physical pickup outcome and packet timing on real firmware require an operator capture. Automated coverage drives the fake robot and the actual stack's request and marker routes.
- The live 0x48 camera translation has no independently captured end-to-end XYZ oracle. The packet admission and the native clamp/yaw corpus cover separate boundaries. The phone's libm remains under M13-023(e).
- Request angle, custom profile, selected-object fallback and other game-message flags remain outside this explicit host request. Broader paths in the named records remain open as specified there.

## Workers

Reader requested gpt-6.1-sol at medium effort. Builders and reviewer used the configured default request. Runtime model identity was not observed. Source-review corrections addressed B8 and teardown reentry, stale path writes and the interacting-lights callback. The manager resolved the reaction-lock type mismatch using the original 0xC55A21 table. The two integrated carry-replacement test failures were old-fixture cleanup dereferences and passed with captured components.

## Records

- **M8-015 and M4-014.** The bounded public pickup uses the original list, native type and track mask. Generic queue and lock claims remain separate.
- **M12-005, M12-006, M12-007, M12-008, M12-017, M12-018, M12-034 and M12-037.** The pickup leaf uses the retained dock request, carry state, teardown and verifier. Broader docking classes, flags, unsupported passive geometry and hardware-only results stay distinct.
- **M12-013, M12-014, M12-019 and M11-006.** The live native correction binds the same-timestamp owner inputs and documented clamp. The legacy host correction is separate. The phone's unshipped libm remains the existing numeric boundary. The broader world-clamp call sites are outside this job.
- **M15-026 and M13-023.** The selected compound reuses the checked zero-approach drive-to-pickup tuple. The broader Bring Beacon lifecycle and numerical library boundary stay with their own records.
