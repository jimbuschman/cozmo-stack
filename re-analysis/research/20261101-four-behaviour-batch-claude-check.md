# Four-job batch: Claude check (manager, 2026-11-01)

**Verdict: PASS, merged. M15-108 and M15-109 added and settled. One stall found, not caused by this batch, ordered as
NEEDS-PROOF-1.**

Jobs: TRIGGER-MAP-BIND-1, EARNED-SPARKS-1, REACT-TO-OBSTACLE-1, SPARKS-FIRE-TRUCK-ALARM-1, from
`codex-m13-manager-2 70555611`. Codex's full gate was 7430/7430 at `9298bf6d`.

## Checked directly in the `.so`

| Claim | Addresses | Result |
| --- | --- | --- |
| Trigger map read when each action is constructed, group captured then | 0x00544332..0x00544350, 0x0052FA70/76, 0x0054437A..80 | Matches. Robot, context, +0x20 RobotManager, Has, then Get |
| EarnedSparks predicate reads Needs pending | 0x005DAEC2..0x005DAECA | Matches: context +0x34, +0x3D8 |
| EarnedSparks Init, no pending test | 0x005DAECC..0x005DAF38 | Matches: lift-safe action, trigger 0xA4, loops 1, true, 0, 60f, false |
| EarnedSparks Resume, Stop, carry | 0x005DAF6C, 0x005DAF70..7C, 0x005DAF82 | Matches. Stop communicates only when pending is set |
| Communication | 0x00696E64..0x00696E90 | Matches, with one address note below |
| Trigger names | table 0x01032690 | 0xA4 is EarnedSparks, 417 is ReactToObstacle |
| PlayAnim single trigger action | 0x005C014E..0x005C01A6 | Matches: lift-safe, config trigger, +0x128 loops, true, 0, 60f, false |
| PlayAnim completion calls listeners only | 0x005C0254..0x005C0292 | Matches |
| FireTruck owner, predicate, Init | 0x005DAF88, 0x005DAFA4, 0x005DAFB4..0x005DB026 | Matches: busy returns 1 with no write; otherwise log, set flag, broadcast, return 0 |
| Trick flag | 0x005137A2..0x005137AE | Matches: Robot +0x354 |
| Robot::Broadcast returns false with no interface | 0x00511DE8..0x00511DFC | Matches |
| Spark end predicate constant false, FLT_MAX ceiling | 0x005B4E98..A2, 0x005B4EC2 | Matches |
| Parent conjunction | 0x005AE35C..0x005AE376 | Matches. A provably false WantsToEnd makes the conjunction false whatever pending is |

Address note: a null external interface faults at the load 0x00696E7E, not 0x00696E80 as the log text says. Log text is
never blocking; corrected in the next job touching it.

Not read here: the common Update and Stop order (owned by M8-001), and StopTrick's caller, which stays UNKNOWN as the
handoff says. A word search found only one 32-bit match outside code, at 0x00021088 in the dynamic tables.

## Plain probe on the production load order

Five simulated minutes, `Fixture(preloadAnimations:true,pumpAnimations:true)`, with animation replies only to real
outgoing Start and End.

- **Left alone:** idle 6.5 s of 300 s, 198 animation starts on the wire. Entered: FindFaces_socialize x7,
  Hiking_FirstLookIntro x11, Hiking_ThinkAboutBeacons x1, Hiking_LookInPlaceForUnknown x1, NothingToDo_BoredAnim x10,
  NothingToDo_Idle x45. Clip times now vary, 0.5 to 17 s.
- **The 37.5 s hold is settled as a rig defect.** With the rig clock and pump right, it does not appear. The 99.5% busy
  figure in the earlier check was inflated by it; the true figure is 97.8%.
- **One needs action stalls the loop.** With `RegisterNeedsActionCompleted("SeeFace")` at t=30 s, the robot goes idle for
  the rest of the run: 268.9 s idle of 300 s, last behaviour at t=31 s. In another run the same action during FindFaces
  left FindFaces running to the end. `Fall` behaved the same way.
- **Not a regression.** Main before this merge, same probe: 269.4 s idle, with "strict priority reached unavailable
  readiness: EarnedSparks".

**Cause.** `RegisterNeedsActionCompleted` marks native pending as unknown on every attempt, and unknown pending stops the
EarnedSparks readiness and the parent's end test. But SeeFace and Fall have `freeplaySparksRewardWeight` 0 in
`needs_action_config.json`; 70 of the 85 actions do. The reward gate order (`20261031-m15-071-gates-claude-check.md`)
needs a non-zero weight before any reward, so those actions cannot change pending. The proof is being dropped too
widely. A face seen or a fall on a real robot hits this, so the `m13-idle-hw` guide now warns about it.

## Findings

No blocking finding in the four jobs. Each one held on addresses, on production construction, and on the plain probe.

**FireTruckAlarm on a robot with unlock 83.** The flag stays set after one run because the app cleared it, and that input
is unknown. It then stops offering itself, which follows the source. Whether the stack should supply the app's trick is
an app-boundary question, and it can wait until StopTrick's caller is known.

## Records

- **M15-108** BehaviorEarnedSparks lifecycle: EXACT_SOURCE on the lifecycle alone. The bodies above were read whole. The
  pending producer and the amount stay with M15-071 and M15-093.
- **M15-109** BehaviorFireTruckAlarm lifecycle: EXACT_SOURCE on the engine's side of the handoff.
- **M8-005** stays open: only the ReactToObstacle profile was built. **M15-071** stays open.

## Progress

2 records in 4 jobs. Totals: 11 settled across 30 start-path jobs, 1 of 178 behaviours finished, 751 records, EXACT 270.
