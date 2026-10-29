# SPARKS-POP-A-WHEELIE-1 — post-gate current record update

Fresh main1268d9b3bd0dd04dbcbe471c71cfd65f7d03fe23, read before final ownership proposal. Metadata-only; operator approved ordinary Pop scope, M13 re-approved. Current718 records upstream; completed job gate used717/main8cc93b12. No code changed and no result is retroactively claimed for718.

```json
{
  "id": "M13-041",
  "title": "BehaviorPopAWheelie configured lifecycle (ordinary instance): cache 8 target choice, get-in, pre-dock reaction lock and cliff, DriveToPopAWheelieAction, and PopAWheelieAction Select/Verify/completion",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "Factory 0x0059CEA6; ctor 0x005C7430; target refresh 0x005C7734; cache 0x0056FA30/0x0056FC7E/0x0056FE18, predicate 0x00570928..0x00570980 (families 2/1, failure 3 time 30 / distance 60 / PI, then CanPickUpObjectFromGround 0x0063C880).",
    "Get-in 0x005C7496..0x005C757A: TurnTowardsObject(PI,false,false) then LiftSafe 0x18A; callback 0x005C7B52..0x005C7B64 updates the previous target only on result 0.",
    "DriveToPopAWheelieAction 0x005C7758..0x005C78D4 / 0x0055D1E8..0x0055D26C: pre-action 4, PI/2, sayName true with 0x18B/0x18C.",
    "Pre-dock reaction table 0x00C6883D and cliff off 0x005C7BC8..0x005C7C10; Select 0x00552730..0x0055287E (height > 33.85 rejects, carrying rejects); Verify 0x0055295C..0x00552A72 (dock action 9, dock success, Radians pitch < 1 -> 0x0400000A); completion getter 0x0055268E..0x0055269C (four unwritten ID words).",
    "Reset 0x005C76B0..0x005C76F0 clears the target and the latch before cliff on. Read packets re-analysis/research/20261028-pop-a-wheelie-1-read.md and -followup-1.md with their independent checks; HANDOFF-POP-A-WHEELIE-1.md; re-analysis/research/20261028-pop-a-wheelie-1-claude-check.md"
  ],
  "unresolved": "Built and checked in POP-A-WHEELIE-1 but not settled. Not this record: the retry/result/reset path (M13-015, still partial while the manual/public callers use the legacy PopAWheelieBehavior adapter), the shared IDrive/IDock/Turn/animation/ActionList/geometry records. The completion getter's four unwritten ID words are uninitialised in the original: under the M1-034 standing rule the stack withholds only the full public payload with a visible MISSING and keeps the defined fields. SparksPopAWheelie is not built (visible gap); widen this record when it is."
}
```

M13-041 now owns the former generic Pop complement for the ORDINARY instance, excluding M13-015/shared recipients. No new or per-Sparks record is proposed. This job adds exact Sparks caller coverage; propose operator/Claude widening of existing041 when reviewed. Current ordinary041 behavior was built/checked in the previous job, but real041 fidelity tag and completion-guard diagnostic attribution are still required before proposing it settled. Queue that nonbehavioral bookkeeping for the priorityFIX batch with current-record validation; do not edit the just-tested frozen code. Existing M13-015 actual manual/API gap remains. Formal statuses/title/evidence/approvals stay Claude/operator-owned.
