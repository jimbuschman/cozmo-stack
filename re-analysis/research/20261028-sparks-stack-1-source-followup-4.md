# SPARKS-STACK-1 — SF11: helper prechecks share the selecting native recipient

2026-10-28. This is a finite follow-through on SF9, requested by the manager and independently checked by A and B. Original packets and SF1–10 files remain unchanged. PROJECT_STATE and current records were reread. Known.py ran before reads of `0x00555928`, `0x005B6F5C` and `0x005B7054`. Authority is the original native ELF, SHA256 `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`. No implementation, tests, git operations or official records were changed.

## Current owning records

```json
{
  "id": "M12-026",
  "title": "PlaceRelObjectAction::ComputePlaceRelObjectOffsetPoses",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x005558F4..0x00556300 the function; 0x005561A0 threshold call; 0x0055E1C6, 0x005B5908..0x005B5918, 0x005B7030..0x005B7054 the callers; 0x005B6F5C..0x005B70DA IsAtPreActionPoseWithVisualVerification",
    "re-analysis/research/20260929-R-VIS-pre-extraction.md Part 3 item 12 (rows 12.1-12.18); re-analysis/research/20260929-R-VIS-verify-pre-items11-13.md (all PASS); re-analysis/research/20260929-R-VIS-M12-M13-gap1-extraction.md Q2 rows for A,B sources",
    "re-analysis/research/20260929-R-VIS-M12-gap2-extraction.md Q3, Q4; re-analysis/research/20260929-R-VIS-verify-M12-gap2.md Q3, Q4"
  ]
}
```

```json
{
  "id": "M12-031",
  "title": "IDockAction::GetPreActionPoses and SelectObject/GetObstacles",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x005508C8..0x00551200",
    "re-analysis/research/20260929-R-VIS-M12-gap2-extraction.md Q10; re-analysis/research/20260929-R-VIS-verify-M12-gap2.md Q10 (PASS)",
    "0x005B6F5C..0x005B70DA, 0x0055EE10..0x0055EE66",
    "re-analysis/research/20260929-R-VIS-M12-gap3-extraction.md Q9, Q3.7; re-analysis/research/20260929-R-VIS-verify-M12-gap3.md"
  ]
}
```

## Exact native joins

| Step | Source behavior | Primary locator |
| --- | --- | --- |
| Outer helper guards | IsAtPreActionPoseWithVisualVerification first looks up the located target with family -1, then casts to ActionableObject. Missing target or failed cast returns `0x03000004`. It then compares `f32(lastObservedTimestamp) + 1000f` with `f32(currentTimestamp)`: a strictly older result returns `0x0300001D`. These failures occur before either pose branch and before selection. | Lookup `0x005B6F72`; cast `0x005B6F92`; null checks `0x005B6F7E` / `0x005B6F9A`; freshness `0x005B6FB8..0x005B6FD0`; stale result `0x005B7022..0x005B7024`. |
| Placement helper branch | Action type 1 takes ComputePlaceRelObjectOffsetPoses with the actual ActionableObject, configured A/B offsets and Robot. | Type check `0x005B7026..0x005B702E`; call `0x005B7054`. |
| Compute's shared query | Compute clears the output in-position bool and pose vector before constructing its pre-action request. The request has object, action type 1, checkNear=false, angle bits `0x3E060A92`, offset word +0x10 zero, byte +0x14 zero and word +0x18 zero. It invokes the SAME native GetPreActionPoses recipient established by SF9. | Output reset `0x0055590A..0x00555926`; request `0x00555928..0x0055594C`; call `0x00555974`. |
| Pickup helper branch | The non-type-1 branch, including native pickup type 0, directly constructs the same request with its actual action type, checkNear=false, the same angle and zero offset/remaining flags. It calls the SAME native GetPreActionPoses. | Request `0x005B7060..0x005B709E`; call `0x005B70A8`. |
| Selection order | GetPreActionPoses still rejects its null or carried target before calling SelectObject. **checkNear=false does not bypass selection.** SelectObject at `0x00550988` remains before GetObstacles at `0x005509A0` and later geometry/height checks. | SF9, `0x005508EC..0x00550988`. |

The freshness comparison must use float32 at each native step. `0x005B6FA8` moves the raw current uint32 timestamp into s4. `0x005B6FB8` loads the raw object timestamp into s0; `0x005B6FBC` converts that uint32 to float32. `0x005B6FC0` independently converts the current uint32 timestamp to float32. `0x005B6FC4` then adds the 1000f literal using VADD.F32, rounding that sum to float32 before `0x005B6FC8` compares it with the already rounded current timestamp. The literal is loaded at `0x005B6FAC`; `0x005B6FD0` takes the fresh branch when the sum is not less than now. Preserve the two conversions, then the float32 addition, then the strict stale comparison. Computing `(double)lastTimestamp + 1000` against an exact integer/double now can change readiness near 2^24 milliseconds and is not this source operation order. This detail belongs only to the actual native helper precheck; no broader math rewrite is proposed.

Therefore the actual native placement helper precheck and pickup helper precheck must both retain this selection side effect after their own outer guards. Wiring selection only in NativePlaceRelRunner.Init is insufficient: selection can already occur during the helper's precheck, including a precheck that later reports not-in-position and delegates to DriveTo. Placement A/B filtering happens after the shared query; it does not justify omitting that earlier writer.

Keep the mutation at these supported native recipients. This does not authorize adding selection mutations to unrelated public geometry APIs, moving them before lookup/type/freshness/carry guards, or inventing a successful precheck. Existing M12-026 and M12-031 already own these joins; this is an explicit missing recipient connection, not a proposed new record or a status change.

B independently checked Compute's shared call and flags; A independently confirmed it and the helper type-0/type-1 dispatch against the original instructions. This reciprocal check preserves the earlier source-first separation. No broader geometry, planner or sibling audit was performed.
