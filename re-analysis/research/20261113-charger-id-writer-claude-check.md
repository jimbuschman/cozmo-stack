# CHARGER-ID-WRITER-1 merged (manager, 2026-11-13)

Worker job commit d70d0e93 (full gate 8373/8373, verifier PASS). Handoff:
`re-analysis/jobs/trial-m13/HANDOFF-CHARGER-ID-WRITER-1.md`.

**Re-read in the `.so`:**

| Address | What | Matches the job |
| --- | --- | --- |
| 0x0054E110..0x0054E11A | the located-charger lookup; `cbz` to 0x0054E12E (no object); `ldr [r0,#0x4C]; cmp #0xD; bne` 0x0054E12E (not a charger) | yes: the store only after both checks |
| 0x0054E11C..0x0054E120 | `ldr r0,[r4,#4]` (Robot*); `ldr r1,[r4,#0x7C]` (the action's charger ID); `str.w r1,[r0,#0x334]` | yes: `Cubes.ChargerObjectId` written in Init |
| 0x0054E126 | then ConfigureAlign | yes: the store precedes the align |
| 0x00533EA6..0x00533EAC | Moved: `ldr [r6,#0x334]`, compared with the object's ID `[r7,#0x18]` | yes: the filter's read |

**Verdict: PASS.** The writer and both filter reads match. The robot+0x334 field changes only through this store and
the Robot ctor's -1.

**Records:** M4-009 has been EXACT_SOURCE since 2026-10-19. This job builds the input its filter reads, so its
"M13-008 writer remains unwired" note no longer holds and the record stays settled. M13-008 stays IMPLEMENTATION_GAP:
its third reader, Robot::SetPoseOnCharger (0x00516916), is unbuilt. No record settled.

**Reach, for the operator:** in the engine only app-sent game actions build MountChargerAction (the RobotActionUnion
factory 0x0052A2D6; DriveToAndMountChargerAction 0x0054E87A). The stack has no C# entry for it yet, so on the robot
runtime the field stays -1, as it does in the engine when the app never mounts. Whether to offer "drive to and mount
the charger" as a C# API action is the operator's decision; it is not a defect.

**Queued (test strength, verifier Q1):** the three "no effect" assertions in the M13-008 test run after both Moved and
Stopped, so they cannot fail as placed; assert them after Moved alone in a later batch.
