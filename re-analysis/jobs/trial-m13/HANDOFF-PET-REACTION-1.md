# PET-REACTION-1

## Claims and evidence

- The shipped PetInitialDetection map names ReactToPet. Factory class 0x43 at 0x0059C92E/0x0059D1D2–0x0059D1F2 creates 0x00606E98, and the mapping registers a listener on that same configured instance at 0x005A0526, 0x0060B5F4, 0x00611E70 and 0x00607550. The attached strategy and behavior now share that owner. The standalone adapter remains separate.
- Admission at 0x00611AE4–0x00611CB2 checks binary32 last+60, adds current IDs during cooldown, otherwise selects unique signed IDs with count greater than two and filters the charger before assigning the target set. Enable and disable at 0x00612038–0x008CC9CC→0x00611E1C add current IDs without resetting time. Forced fallback at 0x006117FC–0x006118E2 offers all current IDs and keeps prior targets when empty. The configured runnable gate is active or has candidates at 0x00606ED8. BeginIteration takes the first still-present signed ID at 0x00606FBA–0x0060700C. A found zero is invalid and does not fall through to a later ID.
- At 0x00607498–0x006074BC cat and dog consume RandInt(100), with draws under 20 choosing sneeze. The trigger table at 0x006074C0 is 0x186/0x181/0x182. At 0x00607048–0x0060708A the same robot RNG supplies the 10–15 s draw, converted to f32 before adding the engine time. The actual owner's sequential action at 0x006070CE–0x00607192 owns image-point turn (center in f32 and original observation time), ordinary authored animation with 1 loop/mask 0/60 s, then any-pet tracking with update timeout 3 s. All child failures propagate through the retained sequence.
- Any-pet tracker type 0x26 at 0x0056833C–0x005683D4 uses shared head/body mask 5, saves and restores eye dart, and samples the original timer. Each update picks the first signed ID from PetWorld wildcard at 0x005684D8–0x005684F2 and consumes only a newer timestamp before the history/calibration query at 0x0056854C–0x00568590. Failure is no information, without retrying the consumed stamp. Valid output refreshes the timeout anchor and sends head before body with the original tolerances and off-treads body gate at 0x0056502A–0x005656DC. No-information success requires a positive anchor and elapsed f32 time strictly above three seconds at 0x00565428–0x005654B4. The completion union uses variant 3 and ID zero at 0x00568482–0x00568492. Teardown restores eye dart, stops body then head for an admitted track, and retires the original driving handler at 0x005647B4–0x00564852.
- EndIteration at 0x00607364–0x006073C2 and StopInternal at 0x00607430–0x00607494 notify with the full candidate set before clearing it. The listener at 0x00611E80–0x00611F28 rebuilds reacted IDs from current pets and those targets, then records time. The retained action callback and behavior manager lifecycle preserve separate End and Stop deliveries.

## Files and tests

- Behavior/BehaviorContainer.cs, Behavior/BehaviorManager.cs, Behavior/Behaviors.cs, Behavior/FaceBehaviors.cs and Behavior/NativeBehaviorRuntime.cs bind the installed behavior and strategy to one original owner. Behavior/NativeReactToPetBehavior.cs implements target selection, RNG, compound ownership and listener edges.
- Vision/Faces.cs exposes the original native pet map to the configured owners. Vision/NativeImagePointRunner.cs shares the checked original-owner angle query and keeps its action-specific failure, and Vision/NativeTrackAnyPetRunner.cs applies it to any-pet tracking. M7BatchThreeBTests includes the recovered class in the factory census.
- The manager ran the original ARM enable callback through the mixed Thumb/ARM veneer with supplied map nodes, the binary32 cooldown around the strict boundary and NaNs, and animation selection at draws 19/20. A separate ARM run used the original PetWorld set and lookup with a supplied angle helper to verify signed first choice, no stale retry, and consuming a failed timestamp.
- NativePetReactionTests exercises the configured manager, original-owner target set and stop, real manager lock transitions, forced fallback, tracker timestamp consumption and strict timeout, and queued camera observations feeding real action-list sequencing. The authored playback fixture drives matching firmware start/end and stream acknowledgements and checks non-silent audio within the owned clip before the tracker consumes a pet timestamp.

- The builder passed 113/113 affected and 6/6 final focused tests. The manager independently passed 106/106, then 6/6 after matching the no-pet diagnostic to source channeled Info at 0x00568516. The lock fixture verifies target mutation and unchanged cooldown while leaving the separate common runnable gate to the installed action-list fixture.

## MISSING

- The pet-specific event corpus is wider than the fixture. All 28 Cat/Dog/Misc clips have authored audio references, and a local asset check resolved four cat event IDs through Cozmo.bnk Play actions to their random-sequence containers. The tested clip proves ordinary authored audio reaches the robot stream. The remaining clip/media variants, Android atan2f bit equality, and physical behavior remain open. No hardware run was made.

## Workers

- Reader used gpt-6.1-sol at medium effort for admission and execution. Builder and reviewer used defaults. The manager independently checked the class factory, mixed veneer, RNG threshold, signed lookup and source timestamps.

## Records

- M14-004 owns strategy admission and the pet input boundary. M7-018 owns the factory and M14-005 owns image-point geometry. M8-001, M8-017 and M8-021 own the shared behavior/compound/animation paths. No fidelity status or approval changed.
