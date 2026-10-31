# ChatGPT check 5, choosers and scoring: Claude check (manager, 2026-10-31)

**Verdict: the formula holds, with 14 rows SAME, and 3 of the flagged rows are real, shipped-reachable and silent. Nothing settled; no
status changed.** The report is `research/20261031-check-5-chatgpt-extraction.md`. Its 37 rows are 15 DIFFERENT or MISSING, 14 SAME, 8
UNKNOWN. Rows named here were read in the C#, and rows 7, 8 and 9 in the `.so`. Other rows were not re-read.

## Real differences (for the fix job)

| Rows | Difference | Evidence | Records |
|---|---|---|---|
| 7 | **Candidate order.** The engine walks a `std::map` keyed by the BehaviorID byte (+0x3C; begin at chooser +0x1C, tree walk 0x0060A5A2..0x0060A5BE). `ScoringChooser` walks JSON order (`Activities.cs:316`). Each positive non-running candidate takes one RNG draw, so draws attach to different behaviours, and exact ties keep a different first candidate. All nine scoring choosers. | `.so` | M15-002, M8-013 |
| 8 | **PounceOnMotion score.** When not running and +0x14C + +0x16C < now, the inherited score is multiplied by +0x154 (0x005F83CA..0x005F8404). `Evaluate` has no class override. Hiking_PounceOnMotion and PounceOnMotion_Socialize use it. | `.so`, `Activities.cs:86-120` | M8-003 |
| 9 | **LookInPlaceMemoryMap running score.** When +0x164 >= +0x13C, +0x140 is subtracted from the running score (0x005E4F30..0x005E4F48). No stack override. Hiking_LookInPlaceForUnknown uses it. | `.so` | M8-003 |

## Not blockers

- **Row 11, closed:** scoring lives on the behaviour in the engine, so the last config read would win. A scan of all shipped choosers
  finds 42 behaviours listed in more than one chooser, every one with identical scoring. No observable difference with shipped data.
- **Rows 10, 12:** the running test uses the id, not +0xA1. Same reasoning as check 3 row 18; no case found.
- **Rows 13..16, 37..39:** the Selection chooser. One shipped chooser has this type, against the report's "only Scoring and
  StrictPriority", and M15-002 already records its message dispatch as unbuilt. Whether shipped running reaches it is open. Row 15 (no
  OnSelected or OnDeselected process request) is new detail for that work.
- **Row 17:** `StopOnUnavailable` is a deliberate hold for unbuilt behaviours. Remove it when those are built.
- **Rows 18..21:** unreachable with shipped data, as reported.

## Method note

Silent differences turned up again in older code, this time in class-specific overrides. The report says it did not check other
subclass overrides. That list is the follow-up, not a reason to widen this job.
