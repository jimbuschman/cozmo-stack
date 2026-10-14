# Opus check of R-DISPATCH-REST (2026-10-13)

- **Checked:** `research/20261013-dispatch-residue.md` (Codex), against libcozmoEngine.so. Read-only cozmo-extractor;
  helpers are in `.scratch/dr/`.
- **Adopted:** by the manager on 2026-10-13. This file is the checked source for job B-M3M4-25. Use the Codex report
  together with the corrections below.
- **Result:** the main findings hold.

## Corrections

- **The wildcard robot ID is 0x101CF, not 0x1CF** (`movw r3,#0x1cf; movt r3,#1` at 0x0069D004..0x0069D00E). This
  corrects `research/20261013-robot-message-subscribers-check.md`, M2-018's notes and M2-020's title.
- **The all-signals branch** (0x0069DE32..0x0069DE4E) is **unreachable**:
  - the only Broadcast caller is ProcessMessages (0x0069DABA), with GetFirstRobot()->ID;
  - the only robot ID ever created is 1 (0x004ED07A).

  The wildcard lookup after the robot's own signal (0x0069DE66..0x0069DE74) runs on every message, and always misses.
- **IDockAction's D0** (0x00550414) is a trap (`fede`), not a bare return. The release is in D2, 0x00557448.
- **FirmwareUpdater::Update** calls VerifyActiveRobots (0x006785B2) first, when the state isn't 1 or 2.

## Confirmed

- **Handle mechanics:** append at the tail; Reset locks the weak token, finds the node and unlinks it (0x0051D8F8..0x0051D932);
  unlink destroys the callable, clears +0x18, patches the neighbours and releases (0x0051D338..0x0051D3A4).
- **The 35 owner releases,** as tabled in the report and the check:
  - the RIC's reverse clear (0x0052DFA4..0x0052DFC8), with its six SendConnectionResponse paths;
  - the action destructors (PlayAnimationAction: D5, CB, CA; Turn/Head/Lift; IDock D2: +0xE4, then DA, then C5);
  - IBehavior, DockingTestSimple, FactoryTest, BlockTapFilter, CubeAccel, NVStorage, Path and FirmwareUpdater.
- **The re-Init order (new):** a re-Init appends the new node **before** releasing the old one (0x00543FCA..0x00543FE2,
  0x00551706..0x00551784), so the action moves to the tail of its tag.
- **During an emit:**
  - a later node that is removed is skipped;
  - self-removal is safe;
  - the RIC's reverse clear is safe (this confirms M1-028 E1..E6);
  - the signal destructor doesn't wait;
  - **no production trigger frees the active signal or MessageHandler mid-emit.** If one is ever found, it falls under
    the M1-034 standing rule (freed memory).
- **Loading before connecting:**
  - the engine states go 1 → 2 (loading thread) → 3 (joined; ratio 1.0 sent in the same Update);
  - the **ConnectToRobot handler has no state or loaded gate** (0x004ED020..0x004ED07C), and neither has AddRobot;
  - the app sends ConnectToRobot only after ratio ≥ 1 (StartupManager.cs:322-341; ConnectionFlowController.cs:670-673).

  So **when to connect is an app input** (COMPATIBILITY_POLICY). An early connect would race the loader.
- **The readers of Impl+0x0C..+0x39:**
  - only D2 writes them;
  - DockingTestSimple::InitInternal (0x005CB516) and LiftLoadTest::InitInternal (0x005D419C) log the IDs and both CLAD
    hashes in hex, each byte as an int followed by " " (labels 0x005CBB1C..0x005CBB5C);
  - +0x18..+0x37 are uninitialised until D2 arrives.

  These are reached only when those behaviours run. Whether any chooser can activate them is unread (M7-027).

## Unread

- Whether the C4, C5, DA, CA, CB, D5 and EF handlers can synchronously destroy another subscriber of the same tag.
- Whether IActionRunner re-Inits a live action.
- Where ConnectToRobot is registered (0x004EE94A → PLT 0x004A544C).
- Whether ~Robot's r5 at 0x0051120E is the BlockTapFilter.
- ProcessMessages with no robot (0x0069D8D8).
