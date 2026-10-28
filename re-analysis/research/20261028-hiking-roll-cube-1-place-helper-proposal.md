# HIKING-ROLL-CUBE-1 — generic PlaceBlockHelper ownership proposal

2026-10-28, finite root proposal clarification only. No additional native reading, implementation, tests, git or official metadata edits. All frozen source packets remain unchanged. Current manifest titles/status/evidence below were reread directly before ownership conclusions.

Yes: propose a SEPARATE coherent new shared-recipient record for **PlaceBlockHelper lifecycle: constructor/factory ownership, Init RNG turn, unconditional turn-to-Ground delegation, placed callback/status, active Update and inherited Stop applicability**. No ID or status is allocated here.

The exact already checked PB1–PB4 path is generic, not a Hiking_RollCube configuration detail: factory5B5588/allocationF0, constructor5B8C1C..C6A, ShouldCancelDelegates5B8CA6..CA8; Init5B8CB0..D50 draws double magnitude PI/4..PI/2, independent sign draw, narrows once, starts TurnInPlace(angle,false); RespondToTurnAction5B8DF4..E44 ignores raw result and starts sequential Ground Add(false,true); Update5B8E60 returns status; RespondToPlacedAction5B8E64..E68 ignores raw result and stores2. Inherited Stop applicability is the existing checked IHelper source: active owner action cancellation(false,true), then Place slot01025F24→5B5F1C no-op. Reuse the checked shared ActionList, canonical RNG, Turn and Ground runners and helper component/callback lifetime, rather than assigning their entire records to the new helper owner. Source citations/primary independent checks are PB1–PB4 in20261007-q17-checked-additions.md and the frozen20261028-hiking-roll-cube-1-helper-response-clarification.md; no fresh behavior is inferred.

CURRENT scope comparison:

- M12-042 covers **PlaceRelObjectHelper**, with ctor5B8F00 and relative-place callbacks; it does not cover PlaceBlockHelper5B8C1C.
- M15-022 covers GroundAtPose composition and Ground Init/CheckIfDone. It does not cover the helper's preceding RNG turn, separate child scheduling, ignored-result callbacks or status2.
- M12-015 owns only Ground wire fields, not either helper.
- M8-011 owns Smart helper delegation and other named scope primitives; the generic concrete PlaceBlockHelper lifecycle is a distinct observable unit. It must not be buried as an unspecified shared body in a Roll-only title.

Keep three coherent proposals distinct: new Roll own path (class/target/roll helper/roll composite/roll action), this new generic PlaceBlockHelper, and new shared state-light manager recipient. The new Place helper proposal need not duplicate the Roll parent DelegateToPutDown success/failure callbacks, which remain Roll-owned caller joins.

Ground completion getter/full payload publication remains the explicitly inherited **unowned Ground completion bridge**. M12-050 is PlaceRel Verify/completion ONLY; the Ground getter is not silently added to it, nor added by this new Place helper title. Its previously checked M1-034 availability guard and local/scalar completion semantics persist. Formal inventory ownership of that Ground bridge must be visible before any settled record claims the whole API production path through it; this proposal does not turn an inherited unowned bridge into an EXACT_SOURCE claim.

## Current full title/status/evidence

```json
{
  "id": "M12-042",
  "title": "PlaceRelObjectHelper lifecycle: ctor, Init (pre-action check, DriveTo delegation), StartPlaceRelObject (3-attempt counter), RespondToPlaceRelResult, MarkFailedToStackOrPlace",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261014-ov02-m12-028-check.md: ctor 0x005B8F00..0x005B8F82; Init 0x005B8FD0..0x005B90FE; Start 0x005B9168..0x005B930E; Respond 0x005B94C0..0x005B952E; MarkFailed 0x005B93A8..0x005B9460; invokers 0x005B956A, 0x005B95D6."
  ]
}
```

```json
{
  "id": "M15-022",
  "title": "PlaceObjectOnGroundAtPoseAction composition and PlaceObjectOnGroundAction Init/CheckIfDone",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "PlaceObjectOnGroundAction Init sends message 0x44 (0, 0, 0, 100, 200, 500 as raw bits), builds a TurnTowardsObject verify action and calls StopAllMotors; CheckIfDone never reads DockingComponent+5, its result is the verify action's (extraction sections 2.3..2.8; construction of PlaceObjectOnGroundAtPoseAction at 0x005DFF14)",
    "M12-015 owns only the wire message of PlaceObjectOnGround (its evidence does not cover the action)",
    "extraction report re-analysis/research/20261003-R-FIX2-M15-gap1-extraction.md"
  ]
}
```

```json
{
  "id": "M12-015",
  "title": "PlaceObjectOnGround sends three zero offsets and a constant speed triple",
  "status": "EXACT_SOURCE",
  "evidence": [
    "0x00632A88 PlaceObjectOnGround zeroes the offsets (0x00632AA6 str r2,[sp,#0x18]; 0x00632AA8 strd r2,r2,[sp,#0x10])",
    "0x00632B88 builder; 0x00632BAE/0x00632BB2/0x00632BB6 vldr; 0x00C7CD90/94/98 = 100/200/500"
  ]
}
```

```json
{
  "id": "M12-050",
  "title": "PlaceRelObjectAction Verify and GetCompletionUnion: dock-success and carry checks, the saved-carried turn child, high-place lift child, failure cleanup, and the ObjectInteraction completion payload",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "Verify 0x005555B4..0x0055581E: selected dock action 2/3 checks, docking success and carry predicates, TurnTowardsObject saved-carried child, first/second Update sequence, failure cleanup, high-place MoveLift child.",
    "GetCompletionUnion 0x0055515C..0x00555344: the chain it builds is overwritten by the final object-interaction payload at 0x00555324..0x0055533E.",
    "Completion setters 0x0075C3F0..0x0075C436 / 0x0075C460..0x0075C4A6 copy the five 32-bit IDs, count and flag; Pack 0x0075B4F0..0x0075B536 serializes all five IDs, count and flag.",
    "Normal located-target path: IDs [target, 0, 0, phone stack address, 0], count 1, flag false (jobs/trial-m13/SPARKS-STACK-1-COMPLETION-POLICY.md).",
    "Read packet re-analysis/research/20261028-sparks-stack-1-read.md and its independent check; HANDOFF-SPARKS-STACK-1.md; re-analysis/research/20261028-sparks-stack-1-claude-check.md"
  ]
}
```
