# M11-044-LOCALIZE-SAME-ORIGIN-1

## Claims and evidence

- This job restores the independently supported localization send from the existing SetNewPose and Delocalize writers. Native AddVisionOnlyStateToHistory sets Robot+0x2C6 and increments its frame before history insertion at 0x00515144–0x0051517A. The existing SetNewPose writer continues to mark the pending state.
- Robot::Update first waits for the first full state at 0x00513C5C. A calibrated vision update failure returns before the send at 0x00513C76–0x00513C9A. Otherwise 0x00513CBE–0x00513CCC attempts the pending update and clears it, before the map update at 0x00513CD4. The stack protects an intervening write made by a synchronous host callback and fences retired owners.
- SendAbsLocalizationUpdate at 0x00514710 reads the latest vision-only sample at 0x00531DD4–0x00531E32. Empty history warns with Robot.SendAbsLocUpdate.NoVizPoseFound and sends nothing. The sampled timestamp and frame feed 0x00514768–0x0051477C. The packer validates the flattened parent's origin ID, then sends timestamp, frame, origin, X, Y and yaw at 0x00512734–0x005127B6 using the existing 0x45 serializer. Missing parent follows M1-034. The manager executed the original sender with a sample whose timestamp/frame differed from the robot's current fields, and verified the empty-history branch.
- The direct startup sync message remains separate. Native Delocalize calls SetNewPose at 0x00510B4C and, when synchronized, sends immediately at 0x00510BF4. The pending writer still feeds the next eligible update.

## Files and tests

- CozmoEngine.cs binds the tick after the vision gate. NativeDelocalization.cs owns the pending send and its owner/pose protection. NativeVisionOnlyHistory.cs copies the most recent native sample from the retained history.
- The manager independently executed the original same-origin tail at 0x005159B6–0x00515A8C as a separate evidence probe. It verified the optional borrowed-state write, current pose update, marker recipient, and success flag. The earlier AddVisionOnly write can already have set the flag when a later step fails.

- VisionLocalizationWireTests exercises SetNewPose through the attached robot tick, a newest sample that differs from the current frame/timestamp, the first-state and failed-vision gates, empty history, a failed transport attempt and the distinct immediate/next-tick Delocalize sends. The builder affected filter passed 455/455 and the manager independently passed 357/357. The verifier checked the narrowed sender and original gate order.

## MISSING and records

- **CUBE-TAP-WORLD-OWNER-1** closes the original tap filter's world-ID ownership at 0x00630B78/0x00630C8C and unsigned deadline predicate at 0x006316B8–0x006316E0. Both localization guards use this predicate through 0x0050D19A and 0x0051552E. The current type-keyed state and signed comparison do not establish exact admission.
- **LOCALIZATION-CANDIDATE-OWNERS-1** closes the retained Docking+0xC and MovementComponent+0x20 exclusion owners at 0x0050D18C–0x0050D1B8 before enabling automatic corrections.
- **LOCALIZE-SAME-ORIGIN-GEOMETRY-1** completes the automatic keyed observation history, relative/reparented geometry, vision-only insertion, current MAIN update and marker recipient once admission is source-backed. This job's automatic geometry draft was removed from production because those guards are missing. **LOCALIZE-CROSS-ORIGIN-1** owns Rejigger/Flatten and origin updates at 0x0051590C–0x005159B2. **LOCALIZE-TO-MAT-HISTORY-1** owns the separate LocalizeToMat tail at 0x0051612C–0x00516142.
- M11-044 remains a settlement target. No fidelity status, approval or hardware flag changed.
