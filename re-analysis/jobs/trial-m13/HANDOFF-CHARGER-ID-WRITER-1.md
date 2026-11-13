# HANDOFF CHARGER-ID-WRITER-1 (Claude worker, 2026-11-13)

From SETTLEMENT-READINESS.md (M4-009's one behaviour gap). Census and implementation reports are worker-local:
`.scratch/CHARGER-ID-WRITER-1-census.md`, `.scratch/CHARGER-ID-WRITER-1-impl.md`.

## The engine

- **The writer:** MountChargerAction::Init (0x0054E0CC) looks up the located charger (0x0054E106..0x0054E110, family 4)
  and checks its type is 0xD (0x0054E114..0x0054E11A). Only then does it store the action's charger ObjectID (+0x7C, set
  by the ctor at 0x0054E07C) into robot+0x334: 0x0054E11C ldr Robot*, 0x0054E11E ldr [action+0x7C], **0x0054E120 str.w**.
  Then ConfigureAlign (0x0054E126). The InvalidCharger branch (0x0054E12E..0x0054E16C) does not write.
- **Only two writers:** the Robot ctor's -1 (0x005100AC) and 0x0054E120 (census sweep, re-checked by the verifier).
  Nothing resets it.
- **Readers:** Moved (0x00533EA6..0x00533EAC) and Stopped (0x00534794..0x0053479A) drop the charger's report after the
  "Charger sending garbage move messages" info. UpAxisChanged (0x00534D50..0x00534F3C) has no filter. A third reader,
  Robot::SetPoseOnCharger (0x00516916), is unbuilt in the stack (visible MISSING, ChargerActions.cs:481; M13-008).

## The change

- `MountChargerAction` (ChargerActions.cs): the M13-008 MISSING is replaced by the store, after the BadObject return and
  before the align.
- `Cubes.ChargerObjectId` (Cubes.cs): -1 at construction; written with Volatile.Write from the action's thread, read with
  Volatile.Read under the message thread's `_gate`. No lock added.

## Tests

- `NavigationTests.M13_008_MountInitStoresTheChargerIdAndTheMovementFilterDropsItsReports` (reach: unit; the action is
  constructed directly): -1 before; the charger's ID after Init and after Success; Moved and Stopped through the robot's
  real dispatch are dropped (no broadcast, no CubeMoved, both Charger infos, no ObjectMovedOrStopped info); UpAxisChanged
  is broadcast once.
- `RFixBatch2Tests.M13_008_AMissingChargerFailsInitWithBadObject`: stays -1 after BadObject.

Results: affected classes 210/210 (verifier). For the full gate, see the commit.

## Verifier: PASS

| Finding | Class | Resolution |
| --- | --- | --- |
| Q1: three "no effect" assertions (Moving, IsMoving, PoseState) cannot fail as placed (Moved and Stopped are both sent first; the connect already marks Dirty). The drop is proven by the other assertions | queued (test strength) | Not changed; a later batch can assert after Moved alone |
| Q2: the ObjectID-sharing question was called an open RECOVERABLE_GAP, but connected-after-located is M11-041's (0x006237DA/0x006237DE, ported) and located-after-connected is M11-013's | queued | Fixed in M4-009's note and the test comment |
| Q3: M4-009's note didn't say no production path constructs the action | queued | Fixed |
| Q4: SetPoseOnCharger is a third reader, unbuilt | observation | Already in M13-008's "Still open" |
| O1: no production entry constructs MountChargerAction; the standalone DockingTestSimple adapter gains a side effect (later charger moves dropped) | observation, **manager decision** | See below |

## For the manager

- **Production entry:** in the engine only app-sent game actions build MountChargerAction (RobotActionUnion factory
  0x0052A2D6; DriveToAndMountChargerAction 0x0054E87A / 0x0052A2AE). The stack has no C# entry for it, so on the robot
  runtime the field stays -1 and the filter never fires. Whether the stack should offer one (a C# API feature) is yours
  or the operator's.
- **M4-009 title wording:** "charger filter" covers Moved/Stopped only.

## Records

`unresolved`/`test` only on M13-008 (writer built; the store is 0x0054E120, 0x0054E11C only loads Robot*) and M4-009
(input writer built; reach; the ObjectID-sharing ownership). No status changed. No record settled.
