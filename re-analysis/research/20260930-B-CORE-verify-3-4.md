# B-CORE batches 3 and 4: Opus verification

- **Date:** 2026-09-30
- **Verifies:** afbb769 (batch 3), 32216ca (4a), 99a51f6 (4b); code at HEAD 6bfaec0
- **Verifier:** Opus `cozmo-verifier`, from the manager session. It read the binary for every decision below.
- **Result:** FAIL overall. **4 settle** (M4-011, M4-020, M4-025, M2-003). M4-020 was then reverted by the batch-1/2
  verification, which found an invented log string; see `20260930-B-CORE-verify-1-2.md`. **11 are not yet**: 5 have code defects,
  and 6 are blocked by unbuilt or deferred paths or by record text. Each record's `unresolved` carries its defect.

## Defects

1. **M4-001, the head clip** (`Motion.cs:482, 487`).
   - The engine clips through `Anki::operator<` (0x0084CD12), which is `operator>` with its arguments swapped
     (0x0084CC90..0x0084CCD0): true only when `a-b > 0` and `!IsNear(a, b, 0x3727C5AC)`.
   - So a target within 1e-5 past a limit is sent unclipped and without a warning. The C# uses a plain `<`/`>`.
2. **M4-001, RescaleRadians** (`Motion.cs:449-450`).
   - The engine converts to an integer and back: `vcvt.s32.f32` / `vcvt.f32.s32` at 0x0084C91E..0x0084C922.
   - At ±inf the engine's conversion saturates, so the angle is clipped with a warning. The C# computes inf − inf and
     sends NaN. Huge finite values differ too.
3. **M4-003, the timeout teardown** (`Motion.cs:778-782`).
   - **Order:** ~IActionRunner sends StopHead/StopLift (0x00541146 / 0x0054116C) before the lock release
     (0x0054120C..0x0054122A). The C# unlocks first (EnableAnimTracks), then stops.
   - **Gate:** the engine stops a track only when `AreAllTracksLockedBy(track, to_string(id))` (0x00541138 /
     0x0054115E). The C# stops whenever the track is moving, so a +0x56 carry lift gets a StopLift the engine never
     sends.
4. **M4-003, a citation.** 0x005408EC is `IActionRunner::UnlockTracks`, called only from the IAction constructor
   (0x00540CB0) and `IAction::Reset` (0x00540D02). The release at the action's end is inline in ~IActionRunner.
5. **M4-003, NEEDS EXTRACTION: the queue position.**
   - The app queues SetHeadAngle and SetLiftHeight with `QueueActionPosition.NOW` (`unity/.../Robot.cs:1435, :1628`).
   - `ActionQueue::QueueNow` (0x0053E24C) deletes the running action first (0x0053E2D6 / 0x0053E35E), so a repeated
     game move never gets 0x03000019. The C# fails it.
   - No record owns the queue semantics.
6. **M4-016, the timeout.**
   - **Result:** the engine fails with 0x03000018 (0x00540E80). The C# returns `TimedOut` with no EngineResult.
   - **Clock:** the engine's timer starts at the first UpdateInternal on BaseStationTimer (0x00540D4A..0x00540D64)
     and is tested before Init and CheckIfDone (0x00540D9E..0x00540DAA). The C# uses a wall-clock `Task.Delay`.
7. **M1-024, the clock width** (`CozmoEngine.cs:1740`, `Needs.cs`).
   - The engine passes the float at +0x10 (0x0084BCA8, stored after `vcvt.f32.f64` at 0x0084BC80), and
     `NeedsManager::Update(float)` computes in f32 (0x00695CBE, 0x00695CD6). The C# uses double.
   - The engine stores `now` to +0x3AC before the pause test (0x00695CA8), and ApplyDecayAllNeeds reads it back
     (0x00695D4C, 0x00695D8A, 0x00695DA8). The C# keeps no stored time.
   - **Circular test:** `EngineAppLayerTests.cs:370` expects `Timer.Seconds`, the implementation's own clock.
8. **Not built:**
   - M1-044 (Robot::~Robot teardown) and M1-045 (the idle go-to-sleep action) are named but not built;
   - M1-015, M1-025 and M1-031 defer to them;
   - M1-025's ExitSdkMode E7..E9 is also not built.
9. **Record text:**
   - M4-008's IMU and cube battery units are uncited;
   - M4-010 has no test.

## The two open questions

- **The lock owner (M4-003).**
  - The owner is `to_string(+0x60 id)` (thunk 0x004F0F4C); the name at +0x48 is only the debug reason.
  - The stack's single `ActionRunnerWho` makes no difference to the lock bookkeeping: at most one head or lift
    action per track, a +0x56 action takes no lock, and animations use unique owners.
  - It matters only through the ~IActionRunner stop gate (defect 3).
- **SetPhysicalRobot's cbz (0x00513926).**
  - It skips BlockFilter::Init when DataPlatform (`[[robot]+8]`) is null. That is unreachable in the shipped app:
    `cozmo_startup` calls `DataPlatform::readAsJson` (0x0066603C) before `StartRun` (0x006661EA).
  - M4-011 settles, with the gate recorded in its `unresolved`.

## Other notes

- `Cubes.cs:229-251`: since M4-025, `DiscoverAsync`'s watch never fires, so it always waits out its timeout. This is a
  regression in the stack's API, not in fidelity.
- `FreeplayTests.TheNeedsManagerUpdatesOnTheEngineTickNotTheFreeplayTick` returns early, and passes, without the OBB.
- `M4_016_MA17_TheDefaultActionTimeoutIs30Seconds` checks the constant only, not the live entry.
