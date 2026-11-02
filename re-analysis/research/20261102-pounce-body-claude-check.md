# Pounce running body: Claude check (manager, 2026-11-02)

**Verdict: M15-107 settled as EQUIVALENT_IMPLEMENTATION (libm `atanf` only). Four differences found and fixed. Not yet a
finished behaviour: the robot pounce predates these fixes, so it counts after the next robot run.**

Lead: `re-analysis/research/20261102-pounce-body-reading-codex.md` on `codex-m13-manager-2`. Its claims were checked in the
`.so`; nothing below rests on it alone except where stated.

## Differences found and fixed

| Difference | Engine | Fix |
| --- | --- | --- |
| Motion radius 240 mm, running and not running | `movt r1,#0x42F0` then store to +0x11C at 0x005F7FA2..0x005F7FAC is 120f, the only write to +0x11C in the class; read at 0x005F9CD8 | 120 mm |
| Extra head-down action after the initial search | Callback literal at 0x005F89BE resolves to `TransitionToWaitForMotion` | Goes straight to the wait |
| Initial reaction tested absolute pitch | 0x005F8BC4 calls `IsFingerCaught`, which subtracts +0x128 at 0x005F8D20 and compares at 0x005F8DA4 | Uses the retained baseline |
| Creep count not reset by a pounce | 0x005F8B36..0x005F8B38 after StartActing | Reset after the action starts |

Two smaller changes: Resume clears the pending point (0x005F8580, Codex's D7, no observable effect established), and the
validator refuses `skipGetOutAnim` and `oddsOfPouncingOnTurn`, which shipped Hiking does not set (others do, and those
configs are not built).

The new tests are `PounceResetsCreepCountAfterStartActing` and `InitialReactionComparesPitchToTheRetainedBaseline`. The
radius boundary test now uses 120 and 121.

## Read directly

| Path | Addresses | Result |
| --- | --- | --- |
| Motion while running: area, boredom stamp, state 6, sqrt, radius, accepted stores, StopActing(true,false) | 0x005F9C34..0x005F9CE4, 0x005F9E1A..0x005F9E92 | Matches after the radius fix |
| Far motion and 4 s expiry | 0x005F9CE8..0x005F9D08, 0x005F9D60..0x005F9E18 | Matches. The native count test before the compare guards a log, and the C# gate has the same effect |
| After the wait: pending, 3 s bored, 5 s rotate, 30 s limit, order | 0x005F91D4..0x005F9292 | Matches, including Hiking's misspelled `TimeBeforeRotate_Sec` leaving 5 s |
| Wait stores | 0x005F8F16..0x005F8F20 | Matches |
| Turn: stamp, pan, odds with 0.2 and range 0 to 1, 65, -50 and 10, creep increment, pan flags | 0x005F9336..0x005F9466, 0x0085E8E4..0x0085E95E | Matches. `RandDblInRange(0,1)` and `RandDbl(1)` give the same value |
| Rotate: stamp, 20 to 45, probability, sign, accumulation, odds gate | 0x005F8FFA..0x005F913A | Matches. Hiking's odds are the default, so the positive branch is not taken |
| Result: helper, 0x197 or 0x193, count reset, callback choice, objective 23 only when caught | 0x005F97C6..0x005F98E2 | Matches |
| Backup: negated distance, 80, true | 0x005F99F8..0x005F9A32 | Matches |
| Streamline branches | +0xD9 is `alwaysStreamline`, which the validator refuses. +0xD8 at 0x005BCCB0..0x005BCCC2 is 0 at spark 0x55, which `Supported` requires | Both false on this path, so Codex's D6 is unreachable here |

Rests on Codex's reading, not re-read: Init and the initial pounce, Creep and Pounce entry, get-out, Stop, Cleanup, and the
lift-delay lambdas. Earlier tests pin these and the robot run went through them.

## Shared boundaries

Not part of this record: the action runner, driving animations, animation chooser, DriveStraight and the pan action. They are
owned by their own records. `atanf` is the phone's libm, which is why this is EQUIVALENT_IMPLEMENTATION, as with M13-026.

## Records

- **M15-107** EQUIVALENT_IMPLEMENTATION, libm `atanf` only.

## Progress

15 records settled, 1 of 178 finished. Hiking_PounceOnMotion becomes the second finished behaviour once a robot run passes
on this code.
