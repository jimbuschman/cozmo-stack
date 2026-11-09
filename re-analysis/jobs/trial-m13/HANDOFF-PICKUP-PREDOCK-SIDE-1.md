# PICKUP-PREDOCK-SIDE-1

## Claims and evidence

- Public drive pickup follows the bounded 0x52A384–0x52A3F0 factory route into DriveToPickupObject at 0x0055C464. The original Unity Robot.cs pickup default has the optional approach angle disabled. The default pose function 0x00558C80 copies every surviving pose into the returned vector. The nearest planar index in 0x005508C8 (distance loop at 0x00550DC8) supplies the in-position checks. The drive recipient passes the whole vector to SetGoals at 0x0055934A. The separate closest-only helper at 0x00558BCE is not this path.
- LIGHTCUBE2's table at 0x00C45CA0 has masks 05/05/05/05/00/0F in Front/Back/Left/Right/Top/Bottom order. The Docking bit test is 0x004E5970–0x004E597A. The orientation gate at 0x004DF2DC–0x004DF318 rejects a root-frame rotation outside the upright tolerance (0x3E0930A4 at 0x004DF61C). The current-pose path at 0x004DF850 and copy-with-push at 0x0050DF00 retain the 75 mm maximum for these starts.
- In both captures Top faced the starting robot and its mask excludes the near candidate. The enabled lateral rotations fail the upright test. Only Bottom marker 11, rotation 2, survives given the recorded poses and no additional blocker. The original engine's sole candidate is on the far side. Choosing the closest candidate earlier would make the same choice.
- Run 014f35 observation 35917.5654 ms and robot state frame 1644 reconstruct MAIN (398.815, 20.140), FORWARD (418.772, 21.448), heading −3.076146709. Captured 0x3F is (420, 20, −3.076146603).
- Run e9520f observation 36058.3907 ms and robot state frame 2525 reconstruct MAIN (438.166, −16.244), FORWARD (458.154, −16.933), heading +3.107129105. Captured 0x3F is (460, −20, +3.107129097). Both wire positions agree with 10 mm lattice quantization. No stack-specific goal-choice discrepancy was found. The run records the hazard of that source-defined approach.

## Files and tests

- PickupPredockSideReplayTests.cs feeds the recorded cube pose and latest preceding 0xF0 MAIN pose into the scoped DriveToObject goal function using a shared root and no additional obstacles. The test checks the single Bottom goal, recovered rotation and distance, expected continuous coordinates and proximity to the recorded point turn. It reaches live pre-action goal generation. It supplies the capture inputs at the world-pose boundary.
- Related pickup tests passed 44/44. Both final captured-state cases passed after replacing the initial synthetic robot start. The batch gate is recorded in QUEUE.md.

## MISSING

- Full source-world obstacles and marker history were unavailable. The replay does not reconstruct an official-app camera pose, replay the planner, or test robot motion. The observed fall remains physical evidence.
- A host restriction on this defined native approach needs an operator policy decision. Main's hardware script now makes the drive round opt-in (ad3151c1).

## Workers

Reader requested gpt-6.1-sol at medium effort. Builder and independent reviewer used the configured default request. The manager re-opened the mask, upright branch and SetGoals call in the shipped ELF and decoded both preceding 0xF0 states from the captures.

## Records

- M12-021 and M12-029 cover the face masks and default pose-vector recipient. The test exercises their narrow live route.
- M12-001, M12-022, M12-031 and M12-036 own broader geometry, drive setup, nearest/threshold and obstacle behavior. M13-018, M13-033 and M13-034 retain the broader planner paths. M15-026 owns the larger DriveToPickup recipient. No record settlement is proposed.
