# CUBE-TAP-WORLD-OWNER-1 + LOCALIZATION-CANDIDATE-OWNERS-1: Claude check (manager, 2026-11-10)

Built by Claude while Codex was out of usage. The steps:
1. **Inventory:** an extractor read all three admission guards; nothing was UNKNOWN (`.scratch/cube-tap-localization-guards-census.md`).
2. **Build:** an implementer built it.
3. **Verify:** an independent verifier failed it on one robot-traffic point.
4. **Correct:** the manager corrected that point.
5. **Re-run:** the affected classes passed (521/521).

## What changed

**The tap filter (M4-023):**

| Step | Address | Before | Now |
| --- | --- | --- | --- |
| Key | 0x00630BF6..0x00630C8C, 0x0063188A | Cube type | World ObjectID, signed int order |
| First tap | 0x006315E8..0x0063163A | Signed | `now >= window` unsigned; until = window = now + 500 |
| Predicate | 0x006316C0..0x006316DC | Signed | `until > now` unsigned, strict; available by World ObjectID |
| Expiry | 0x00631228..0x00631242 | One tick late (`>`) | `until <= now` |
| Expiry marking | 0x0063128A..0x00631360 | One copy, via a connected cube | Every Known located copy, every origin, no connection needed |
| Logs | 0x006315F8, 0x00630CA6, 0x006312EE | MISSING lines | DoubleTap info, UnknownActiveID warning, ExpiredTap debug |

**The localization guards (M11-044):** both tap hooks, the dock target and the tracked object are now bound in
production (before, every hook was unassigned outside tests, so no exclusion ever fired). CouldUse and the
LocalizeToObject head already followed the engine's order.

**The dock target (M12-040):**
- **Where the target is read:** BlockWorld.DockingObjectId now reads the docking component's target.
- **The engine's reset:** ~IDockAction resets it: AbortDocking iff DockingComponent+4, then marker 3 and target −1 (0x00557474..0x0055748C).
- **Where the stack resets:** at the end of every legacy dock action (success, failure, cancel). Before, a finished legacy dock left its target set.

## Verifier findings

1. **Blocking, fixed:** on a cancelled or timed-out legacy dock the robot would have received two AbortDocking
   messages. The legacy DockAsync sends one, then the end reset sent another while picking-or-placing was set. The
   engine sends AbortDocking only from ~IDockAction (0x0055747A..0x0055747C) and Robot::AbortAll (0x0051194C). Now
   the end reset skips its abort when DockAsync already sent one for that dock. A test covers it.
2. **Pre-existing, queued as TAP-FILTER-TICK-POSITION-1:** the stack runs the tap filter's Update after ActionList and
   the path tick. The engine runs it at 0x00513EA0, before AIComponent::Update (0x00513EA8) and ActionList
   (0x005140BC). On an expiry tick, behaviours see a cube Known that the engine already has Dirty. M4-023 is
   EXACT_SOURCE and its `unresolved` now names this.
3. **Queued:**
   - The ExpiredTap log is emitted per copy; the engine logs once per key and only with a connected object.
   - The tap-queue deadline compare is signed.
   - The two-step tap identity lookup diverges only for an active ID the world refused (slot above 4 or an
     InvalidActiveID).
   - The legacy DockAsync abort is unconditional, where the engine gates it on +4 (pre-existing; M12).
   - M4-009's evidence C11.1 ("only writer DockWithObject") is contradicted by the destructor. The evidence is
     frozen, so the correction is in its `unresolved`, for the next approval.

**No record settles:** M11-044 still needs its automatic geometry jobs, M4-023 the tick position, and M12-040 the
rest of its docking state.
