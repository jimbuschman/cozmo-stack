# KNOCKOVER-CUBES-1 — approved source-contract correction

2026-10-28. Official inventory, manifest, approvals and statuses are unchanged. This document records a contradiction; it does not overwrite the earlier approved evidence.

## Runtime streamline flag (+0xD8)

Current M13-014 is IMPLEMENTATION_GAP, “Knock over a stack: BehaviorKnockOverCubes flow, DriveAndFlipBlockAction and IDriveToInteractWithObject”. Its evidence says +0xD8 is “1 when the behaviour was entered by a soft spark switch and 0 for a hard switch or no current behaviour”. Approved `inventory/M13-navigation.md` Q8 also labels +0x5C as `0="soft", 1="hard"` and consequently interprets XOR1 as a soft-spark flag.

A extraction and independent B binary check prove the opposite scalar meaning. Before tracing, both reused known.py and existing M7 evidence. `SetRequestedSpark` copies its bool argument into requested+0x64 at `005A3EB2`; nonzero selects native literal `00BF0B78`, “soft”, at `005A3ED4/005A3ED8`, while zero selects `00BF0B7D`, “hard”. `SwitchToRequestedSpark` reads requested+0x64 at `005A421A` and stores active+0x5C at `005A4226`. `IBehavior::Init` reads active spark+0x58 at `005BCCAE`: sentinel0x55 produces +0xD8=0; otherwise it loads active+0x5C at `005BCCB4`, XORs1 at `005BCCB8` and stores +0xD8 at `005BCCC2`. Thus +0xD8 is true for an active **hard** spark and false for a soft spark or no active spark.

The already recovered KnockOver branches test that same flag together with JSON alwaysStreamline+0xD9: Init skips reaching (`005C31B4..005C31D0`); knock construction forces maxTurn0 (`005C34EA..005C350A`); reaction skips its animation (`005C3972..005C397C`). This changes observable robot motion and animation. It is not a logging-only label correction.

The manager requested approval because the trial requires a checkpoint when primary source contradicts an approved contract. The operator explicitly answered **“Approve corrected contract and fix”** to the KnockOverCubes hard/soft question. Build may use this corrected source contract after independent whole-packet verification. Claude owns formal Q8 and M13-014 evidence reconciliation and any settlement. This approval is separate from the earlier M12-007 pickup correction.

## Face-cache boolean: stale implementation choice

M12-035 currently IMPLEMENTATION_GAP, “The flip-block +0x150 installers and DriveAndFlipBlockAction::GetPossiblePoses”, has an unresolved legacy `CHOICE` mapping `GetLastObservedFace(pose,true)` to namedOnly. Its approved M12 inventory row55 specifies the literal true call without assigning that meaning; no operator policy authorizes namedOnly.

Existing checked PutDown cache contract (read row34), independently rechecked at `004F5AE2..004F5B38`, says true requires the cached pose to belong to the current Robot origin, returning false on a different origin (`004F5B0A/004F5B0C`). False instead flattens and reparents to current origin (`004F5B32`). Naming gates cache production (>3 observations or named), not this query argument. Canonical `CopyNativeLastFace` already supplies this exact boolean contract. The new native Flip consumer must call it with true; it must not use the public namedOnly adapter. This resolves a recorded implementation choice within the literal approved call, rather than reinterpreting an operator policy. Preserve the old official unresolved text for Claude to reconcile; do not silently claim it was correct.
