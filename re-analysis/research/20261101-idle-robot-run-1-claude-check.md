# First left-alone robot run: Claude check (manager, 2026-11-01)

**Verdict: the run worked and found a defect the simulation could not. Cozmo was idle for 176 of 240 s, nearly all of it
Hiking, which holds on every visit. Nothing settled; no status changed.**

Bundle: `re-analysis/acceptance/hardware/20261101-163229-M13-IDLE-HW-b909fc` at commit `b7db7111`. Kept here: result, summary, stack log and
provenance. `events.jsonl`, `messages.jsonl` and `frames.jsonl` (95 MB) stay on the operator's machine.

Operator's notes: kept busy "partly"; still "usually during the playalone and hiking sections"; "only seems like maybe one
action worked". The bundle agrees with all three.

## What happened

| t (s, host) | Activity | Behaviour |
| --- | --- | --- |
| 9.3 | Socialize | DriveOffCharger, 2.6 s: he started on the charger and drove off |
| 11.9 | Socialize | FindFaces_socialize, 21.4 s |
| 33.4 | Hiking | none, 60 s |
| 93.4 | Socialize | FindFaces_socialize, 20.2 s |
| 113.7 | Hiking | none, 60 s |
| 173.8 | Socialize | FindFaces_socialize, 19.6 s |
| 193.4 | Hiking | none, until the end of the recording |

Startup was clean: ready in 3843 ms, 0 error-level logs, no run errors. 19 animations played through both start and end
handlers. No needs action fired, so the NEEDS-PROOF-1 stall was not what stopped him here.

## Cause

At 33.4 s the stack reported "M11-035/M15 Pounce no-motion execution needs original owner, ordinary Spark state and known
no-frame/no-unsupported-event history", then "M8-013 scored chooser input unavailable; retaining current behavior" 3495
times, the only repeating message.

`NativeHikingPounce.cs:59` is unavailable whenever `MotionFrameInputs` is non-zero. The job built only the no-frame path,
as M15-107 says. A real robot always has camera frames, so on every real Hiking visit Pounce's readiness is unavailable,
and the scored chooser holds rather than guess. The whole activity then does nothing until the activity timer ends at 60 s.

The simulation has no camera frames, so it takes the one path a robot never takes. That is why the 300 s test and my own
probes showed Hiking running. **A left-alone test with no frames cannot speak for a robot.**

The operator's "one action" is right: FindFaces was the only thing doing visible work after the drive off.

## What the run does establish

- DriveOffCharger ran from a real charger start for the second time, through the canonical parent.
- FindFaces_socialize ran on a robot for the first time: three full scans, about 20 s each, and it ended normally each
  time. M15-103 stays open: its sensor phase order is still open and the wire sequence has not been compared to source.
- The activity timer moved the robot out of the hold each time, so the hold is 60 s, not permanent.

No record changes. M15-105 (Spark only) and M15-106 (Hiking look) did not run.

## Follow-up

POUNCE-REAL-FRAMES-1 goes ahead of the other queued work. NEEDS-PROOF-1 stays ahead of it, since Codex is already reading
it and a face will stall a real robot too. Neither replaces the other, and a second robot run needs both.
