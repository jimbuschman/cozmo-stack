# HANDOFF CARRY-TREE-FLAG-RESET-1 (Claude worker, 2026-11-14)

Job 26 (my proposal from FIND-ON-TOP-SAME-ROOT-1). Census and implementation reports are worker-local:
`.scratch/CARRY-TREE-FLAG-RESET-1-census.md`, `.scratch/CARRY-TREE-FLAG-RESET-1-impl.md`. Verifier: PASS.

## The defect

The stack-only `NativeCarryingTree` flag was set when a cube was attached to the lift and never cleared. After a
once-carried cube was placed, its native node was correctly re-parented to the world origin, but its public Pose stayed
a parentless, legacy-flagged world view. Effects in scope:
- **Localization:** SetLocalizedTo refused the cube (M11-044 "marker loop withheld" MISSING) where the engine localizes
  to it: robot pose, origin and LocalizedTo differed.
- IsLocWithinBeacon dropped it from in-beacon candidates; some WithRespectTo users hit MISSING.
Wire reports were not affected (they read NativePose).

## The change

The Pose setter's cube/charger branch, which writes the native node in place (SetPose 0x004EF598..0x004EF5A2), now sets
`NativeCarryingTree = (the native parent is not a root)`. So the flag clears exactly when the engine re-parents:
- placement: SetCarriedObjectAsUnattached -> AddRobotRelativeObservation (0x00506B82..0x00506BAA; callers 0x005338A0,
  0x00553CAA/0x00553D2A, 0x005C84CE);
- a sighting while carried: UpdatePoseInInstance (0x00505F38/0x00505F56), then UnSetCarryObject (0x00505FBC).

UnSet alone keeps the lift parent and the world view, as in the engine (UnSetCarryingObjects 0x00633C00 writes no pose).
A second pickup follows the lift again. The write runs under EnterPoseMutation; no new lock. During LocalizeToObject's
rejigger window the gate is held, so no write lands with a stale origin (verifier choice 3).

## Tests

`CarryTreeFlagResetTests` (a)-(e), values hand-derived from cited literals (lift pivot -41/45 at 0x0050FFE0/0x0050FFEA;
4.0 at 0x00632E7C; -12.5 at 0x00632E82): place -> (127,3,49), origin-parented, flag false, (27,3,49) relative to the
robot; localization to it no longer refused; a second pickup follows the lift; UnSet alone stays (127,3,49); a sighting
while carried clears the flag. Negative control: without the line, (a), (b), (c) and (e) fail. All reach: unit (named
substitutions). M13FindOnTopSameRootTests :215/:315 no longer model the stale flag; M11RVisBuildTests M11_043's fixture
locates with a parent-keeping copy (its cubes are lift-parented). Affected classes 439/439, 535/536 then 105/105
(implementer); 366/366 (verifier).

## Records

`unresolved`/`test` only on M12-008 and M11-044. No status changed. No record settled.

## Open (census, not built)

- Q1: whether the engine's observed pose on the sighting path is root-parented (0x00625544 bl 0x004DF628; route to
  0x00505DE0 not traced). The flag follows whatever parent the stack gives, so it is right either way.
- Q2: the legacy Docking.cs:547 branch (carried without an attach) is reachable only from ManipTool's `SetCarrying`.
- Q3: CubeGameBehaviors.cs:630 / FlipBlockAction.cs:190 frames; Q4: delocalization with a carried cube.
