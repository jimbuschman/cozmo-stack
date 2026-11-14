# M13-007-CALLER-CENSUS-1 merged (manager, 2026-11-14)

Worker commit 2a850d56 (records only). Census: `re-analysis/jobs/trial-m13/INVENTORY-NOTE-M13-007-CALLER-CENSUS-1.md`.

**Re-read in the `.so`:**

| Address | What | Matches the census |
| --- | --- | --- |
| 0x00507216..0x00507220 | MarkObjectUnknown: ignoreIDs gets the current object's ID (`r7+0x14`) | yes (C1) |
| 0x00507224..0x00507240 | then `movs r0,#6` / `movs r0,#7` inserted into the ignore-family set (r8) | yes (C1: families 6/7 excluded) |
| 0x0063C6A2..0x0063C6BE | CanInteractWithObjectHelper: GetWithRespectTo(obj pose, robot pose `bl 0x4EA398`), `cmp r0,#1; bne` → return false | yes (D-1) |

**Verdict: PASS.** All eleven call sites match the engine in tolerance, onTop and filter. C3 (PlaceRel's completion
union) has no effect (its result is overwritten, M12-050); C2 is unreachable on both sides (every external
MarkObjectDirty passes propagate 0). Nothing built.

**Follow-ups:**
- **D-1 → CAN-INTERACT-ROOT-CHECK-1 (released):** the stack's Docking.CanInteractWithObjectHelper lacks the engine's
  root check, so for an object in another origin it would throw from WithRespectTo where the engine returns false.
  Reachability is not yet known. Owned by M12-012.
- **D-2:** the stack's host BlockConfigurationManager is HOST-CONFIG-SAME-ROOT-1, held for the operator.
- **D-3** (Neighbor throws on a null pose node): goes with CAN-INTERACT-ROOT-CHECK-1.

**M13-007 settlement:** its own steps now all match, but its path runs through M13-023, M13-024, M13-025 and M11-105,
all still IMPLEMENTATION_GAP, and its unresolved has accumulated stale text. FOOTPRINT-GROUP-SETTLEMENT-READINESS-1
lists what remains across the group so they can be settled together.

No record settled.
