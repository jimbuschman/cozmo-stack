# NEEDS-PROOF-1 and RESPOND-TO-PYRAMID-BASE-1: Claude check (manager, 2026-11-01)

**Verdict: PASS, merged. M15-110 added and settled. SeeFace and Fall no longer stall the loop. A weighted action still
does, so REWARD-GATE-1 is ordered.**

From `codex-m13-manager-2 5c91e5a0`. Codex's full gate was 7461/7461 at `93c9e802`. Codex held RESPOND-TO-PYRAMID-BASE-1
back for priority only. It sits under NEEDS-PROOF-1's branch tip, so I checked it here rather than unpick the branch.

## NEEDS-PROOF-1, checked directly in the `.so`

| Claim | Addresses | Result |
| --- | --- | --- |
| Paused and no-Robot returns come first | 0x006960E0..0x006960F8 | Matches |
| Native order: SendNeedsState, stars, notifications, weight, gate, reward | 0x00696366..0x006963B6 | Matches |
| Weight test: magnitude below the literal skips; equality and unordered continue | 0x00696386..0x006963A4 | Matches. The C# `!(Abs<eps)` is the same test |
| Old pending communicated, then new amount and pending written | 0x00696402, 0x0069640A, 0x00696410 | Matches |
| Constructor clears amount and key storage, pending false | 0x00692216..0x0069221E | Matches |
| Communication clears pending with no guard | 0x00696E78 | Matches, and the fault address now reads 0x00696E7E |
| Fall: strictly above 1000 registers action 0x11 on context +0x34 | 0x005350AA..0x005350C2 | Matches |

Not read here: the UpdateStarsState and LocalNotifications bodies, relied on for not touching +0x3D8. The 2026-10-18
census of communication callers and the 2026-10-31 gate check support that; neither is a full write census of +0x3D8.

## RESPOND-TO-PYRAMID-BASE-1, checked directly

| Claim | Addresses | Result |
| --- | --- | --- |
| Readiness: now, elapsed, stores previous, count, strict growth, recount | 0x005DB2D4..0x005DB358 | Matches. The native loop runs over the config set, and the factory admits exactly one entry |
| Result: elapsed below the literal and growth within epsilon | 0x005DB358..0x005DB396 | Matches. Literals 0x409FFFEB and 0x3727C5AC read |
| Init resets the index, one lift-safe action, empty callback | 0x005DB3AC..0x005DB414 | Matches: first trigger, 1, true, 0, 60f, false |
| Trigger 20 | table 0x010326E0 | BuildPyramidReactToBase |
| Resume | 0x005DB4DC | Returns 0 |

## Plain probe on the production load order

Five simulated minutes, one action at t=30 s. No camera frames, so this says nothing about the Hiking hold.

| Case | Idle of 300 s | Behaviours after t=30 s |
| --- | --- | --- |
| Left alone | 6.0 s | Ordinary cycle |
| SeeFace | 6.2 s | Ordinary cycle |
| Fall | 6.1 s | Ordinary cycle |
| RollACube | 0 s, but FindFaces_socialize x1 for the whole run | None: he never leaves FindFaces |

**The finding.** The 15 weighted actions are CozmoSings, FistBump, GatherCubes, GuardDogLose, GuardDogWin, HiccupsEndBad,
HiccupsEndGood, KnockDownCubes, PeekAboo, PopAWheelie, Pounce, PyramidCompleted, RollACube, StackCube and Workout. The job
reports this honestly with a named MISSING, and it is what I asked for as a minimum. The recovery it names, an explicit
`SparksRewardCommunicatedToUser` call, has no production caller while pending is unknown: `Activity` deselection reports
missing instead of calling it. So Freeplay stops at the first roll, stack, knock-over, wheelie, workout or pounce. Pounce
is on that list, so POUNCE-REAL-FRAMES-1 alone only moves the stall.

## Findings

No blocking finding in either job. REWARD-GATE-1 covers the stall.

## Records

- **M15-110** BehaviorOnConfigSeen configured for RespondToPyramidBase: EXACT_SOURCE on its lifecycle. Natural BuildPyramid
  selection is not claimed.
- M15-071, M15-093, M15-094, M15-046 and M10-011 stay open. M10-011's FallingStarted payload is the correction the handoff
  names.

## Progress

1 record in 2 jobs. Totals: 12 settled across 32 start-path jobs, 1 of 178 behaviours finished, 752 records, EXACT 271.
