# Claude spot-check of the free model's research reviews (2026-10-22)

Read-only Opus verifier pass over every Contradicted verdict in the 2026-10-22 reviews of rows 29 (R-M8-COMPLETION),
33 (R-M8-PROFILE) and 45 (R-M10-OWNER-AVAILABILITY), plus four Confirmed verdicts as calibration. Manager re-checked
row 33's first item with `known.py`.

## Contradicted verdicts: all overruled; the original answers stand

| Row | Claim (original answer) | Reviewer said | Verdict |
| --- | --- | --- | --- |
| 33 | TurnInPlaceAction::SetMotionProfile 0x00545D36..4E (flag +CC; profile +C/+10 -> +C4/+C8) | inside SetAccel | answer right (0x00545D36 is SetMotionProfile, 26 B) |
| 33 | DriveStraightAction::SetMotionProfile 0x0054759E..5D4 (sign of +78 picks profile+0 or -profile+24) | inside SetDecel | answer right |
| 33 | IDockAction::SetMotionProfile 0x0055043E..45C (+18/+1C/+20 -> +AC/+B0/+B4) | inside SetAccel | answer right |
| 33 | BehaviorDriveInDesperation Init 0x005D8ECA passes config+4 to SmartSet (0x005D8ED8) | is the destructor | answer right |
| 33 | DriveToPose factory entry 0x0052A11C | not a function | answer right (prologue at 0x0052A11C; registration pointer not found) |
| 33 | HandlePlanComplete / ApplyMotionProfile 0x005091EC (left UNKNOWN by reviewer) | UNKNOWN | answer right |
| 33 | DriveToPose dtor 0x0055A5FC..660; base clear 0x005410EC..F6 (left UNKNOWN) | UNKNOWN | answer right |
| 29 | Concrete getter 0x005441DE..420E copies this+78, Set_animationCompleted | outside CheckIfDone | answer right (0x005441DE is GetCompletionUnion) |
| 29 | 0x0075C544 is the rvalue animation setter | "Play concrete getter" | answer right (ActionCompletedUnion::Set_animationCompleted) |
| 45 | FallingStopped >1000 -> Needs 17 (0x005350AA..C2) | (no contradiction) | agree; literal 1000.0 is at 0x005352A8, not 0x005352AC |

**Root cause:** the reviewer took Thumb symbol values with bit 0 set (0x00545D15, 0x00547593, 0x00550433,
0x005D8EBD, 0x005441C1) as function starts, so each real function seemed to lie inside the previous one. It also
mislabelled 0x00540AA8 (GetRobotCompletedActionMessage, called "Cancel").

## Confirmed verdicts: 4 of 4 hold

Pose-config C6 (0x00616D5C..78); pose-config threshold (0x00616E0E..EC6; verdict right, but the review's prose
mislabels which branch runs the membership loop: it is the not-located branch, 0x00616E30 -> 0x00616E98, as the
original answer says); external admission E1-E6 (0x005A20A8..2110); E8 (0x005A2F54..64).

## Consequences

- No build changes: the original answers' rows for M8-020, M13-022, M8-011, M8-015 and M12-032 stand. Builds use the
  original answers where a review says Contradicted.
- The reviewer's Confirmed verdicts are usable with spot-checks; its Contradicted verdicts and its prose labels are
  not, until each is checked in the binary.
- `.opencode/agent/cozmo-one-job.md` gains a Thumb-address rule.
