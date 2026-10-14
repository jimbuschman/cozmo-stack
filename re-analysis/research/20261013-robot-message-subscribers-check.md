# Opus check of R-SUBSCRIBERS (2026-10-13)

- **Checked:** `research/20261013-robot-message-subscribers.md` (Codex), against libcozmoEngine.so. Read-only
  cozmo-extractor.
- **Adopted:** by the manager on 2026-10-13. Use this file, not the Codex report.
- **What held:** the call-site inventory. There are exactly 35 callers of 0x00519F8C, with none missing and none extra;
  there are no veneer, literal or PIC routes into it. Every tag and robot ID checked matches.
- **What was wrong:** the registration order. The dev test behaviours subscribe in the Robot constructor, before every
  component, and **twice**.

## Registration order at connection

1. **AddRobot** builds the Robot (0x0052EE9C), then the RIC (0x0052EF08 → constructor 0x0052FEF4).
2. **Robot::Robot:**
   - BehaviorManager constructor (0x0050FCAC → 0x005A08D6) → BehaviorContainer (0x0059C324). It calls CreateBehavior
     (0x0059C3C6) for every entry of the behaviour map;
   - then BehaviorSystemManager constructor (0x0050FCBE → 0x005A5812) → a **second** BehaviorContainer from the same map.
     So every behaviour object, and every constructor subscription, exists twice. The BehaviorManager instance comes
     first;
   - the map includes `devBehaviors/factoryTest.json`, `dockingTestSimple.json` and `liftLoadTest.json`, which ship in
     the OBB: LoadBehaviors (0x005206BC) recurses `behaviorSystem/behaviors/` and is called unconditionally from
     LoadNonConfigData (0x0051F452);
   - **the test behaviours' constructor subscriptions:**
     - FactoryTest (0x0059CBF4): D6, F3, B8, EE and D2, ungated;
     - DockingTestSimple (0x0059CA88): D3, only if ctx+0x20 and its +0x60 are non-null (0x005CB18C/0x005CB190);
     - LiftLoadTest (0x0059CD14): DA, through the raw IBehavior overload (0x005D401A..0x005D4038; dispatch
       0x005BF2F8..0x005BF320).
   - then, in order:
     - Path C6 (0x0050FCF6);
     - NVStorage (0x0050FD8C), with the Backup C9 subscription gated on HasExternalInterface (0x00519B34);
     - CubeAccel F5 (0x0050FE08);
     - BlockTapFilter B6/B4/B5 (0x00510198);
     - Impl/TracePrinter B0/CF/EC (0x00510210);
     - then Impl Init, with 37 subscriptions and no gate (0x00510348).
3. **The RIC:** D2, EE and C9 after the Robot. ED on OnNotified(0) (0x0052DE34).

## The per-tag call order on a robot's signal

FT, DT and LL are FactoryTest, DockingTestSimple and LiftLoadTest. Each appears twice, the BehaviorManager instance
first.

| Tag | Order |
|---|---|
| B4 | BlockTapFilter 0x00630934, then Impl 0x00532B0C |
| B5 | BlockTapFilter 0x006309AE, then Impl 0x00532B1E |
| C9 | Backup::RobotConnected 0x00519B78, then Impl HeadID 0x00532C1A, then the RIC 0x0052D28E |
| D2 | FT, FT (0x005D0BD4, a bare `bx lr`), then Impl::HandleFWVersionInfo 0x00532AC4, then the RIC 0x0052D1DE |
| EE | FT, FT (0x005D067A), then Impl 0x00532C2C, then the RIC 0x0052D236 |
| B8 | FT, FT (stores +0x1B3, 0x005D0998), then Impl 0x00532AD6 |
| F3 | FT, FT, then Impl 0x00532AE8 |
| D3 | DT, DT (counters only, 0x005CB35C..0x005CB382), then Impl::HandleDockingStatus 0x00532C84 (it logs; body 0x00536F1C) |
| DA | LL, LL, then each IDockAction (0x0055176C), in Init order |
| D6 | FT, FT only |
| ED | Impl 0x00532CDA, then the RIC lambda(s) |
| C4 | Turn, Head and Lift actions, in constructor order |
| C0 | Impl::HandleCliffEvent only |

## The emitter and the signal

- **Robot ID 0x1CF** (MessageHandler+0x1C, 0x0069D004/0x0069D00E):
  - if the robot ID equals it, every robot's signal for that tag is emitted (0x0069DE32..0x0069DE4E);
  - otherwise the robot's signal is emitted, then the 0x1CF signal (0x0069DE50..0x0069DE74).

  No registration uses 0x1CF. Whether any Broadcast caller passes it is unread.
- **Append** at the tail (0x0051D874..0x0051D8A2).
- **Emit** (0x0069E274..0x0069E2B2):
  - take a ref on the sentinel;
  - call only if +0x18 is non-null;
  - read `next` after the call, take a ref on it, and release the current node;
  - re-read the sentinel.

  So a subscriber appended during an emission **is** reached.
- **Unlink** (0x0051D338..0x0051D3A4): destroy the callable, clear +0x18, and patch the neighbours. The removed node
  keeps its own links.
- **Scoped disconnect:** 0x0051D8F8..0x0051D932.
- **A self-removal happens in production.** The RIC mfgId lambda → SendConnectionResponse (0x0052E3A2) → 0x0052DFA4
  releases all RIC handles back to front. It is safe as ordered.

## The stack against the native order

Broadcast (CozmoEngine.cs:2139-2147) dispatches by fixed class: Robot, then DeviceRoute, then the RIC, then PublicRoute.
It doesn't follow per-tag registration.

1. **B4/B5:**
   - BlockTapFilter's ObjectIDNull warnings (0x00630DE8..0x00630DFA, 0x00630F1C..0x00630F2E, "Could not find match for
     active object ID %d") are missing (Cubes.cs:670-676, 703-707). They are owned by M4-023, an EXACT_SOURCE record,
     so this is a code defect;
   - the tap-filter part runs after Impl's lookup (Cubes.cs:678, 709); natively it runs first.
2. **C9:** Backup::RobotConnected (0x0051A010) is missing (NvStorage.cs:728).
3. **D2:** Impl::HandleFWVersionInfo (0x00533348) isn't subscribed. It belongs before CozmoEngine.cs:1332.
4. **D3:** Impl::HandleDockingStatus (0x00536F1C) is absent.
5. **The six test-behaviour subscribers** don't exist.
6. **C4:** Turn subscribes at run time (ChargerActions.cs:451), so it always comes after Motion.cs:1419.
7. **Emission:** RobotSubscriptions.cs:51 iterates a snapshot, so a node appended during an emission isn't reached.
   Natively it is.
8. **An extra raw subscriber:** CubeReactions.cs:176/194-205 takes raw B4/B5/D7, which the engine has no counterpart
   for.
9. **Matches:** ED; EE Impl before the RIC; C9 Impl before the RIC.

## Unread

- Whether LoadNonConfigData finishes before AddRobot (the load/connect gating).
- The FactoryTest handler bodies (0x005D0864..0x005D0BD4) and LiftLoadTest's DA virtuals (+0x6C/+0x78).
- Broadcast callers that pass 0x1CF.
- The early-unsubscribe paths other than the RIC's.
- Owner or signal destruction during an emit.
- The action and F0..DC lambda addresses (not re-resolved).


**Correction 2026-10-13** (`research/20261013-dispatch-residue-check.md`): the wildcard robot ID is **0x101CF**, not 0x1CF (`movt r3,#1` at 0x0069D008).
