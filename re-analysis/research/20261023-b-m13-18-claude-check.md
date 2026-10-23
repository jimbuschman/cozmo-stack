# Claude check: B-M13-18 (8f562461..76ccce0d) — 2026-10-23

Manager review of the diff and the builder's BLOCKED report. **PASS; the blocker was a test-setup timing flake, fixed
in the same commit as this file.** No record settles (M11-037, M13-038, M13-039 stay IMPLEMENTATION_GAP).

## Content

- Production: one ReportMissing text in `BlockConfigurationManager.Update` now names only what is unbuilt (the native
  configuration-container and tallest-stack producer) and the two known subscribers, A before B. No behaviour change.
- `M13ConfigurationAudioNotifyTests`: the real robot's two BehaviorAudioClients and broadcaster. 0 -> 2 gives one
  dispatch, A then B, and 0xEC3490EC from each; 2 -> 3 dispatches with no audio request; an unchanged height does not
  dispatch; removal dispatches 0 with 0x3BA32BC6 from each. These match H4 (0x00592320..0x0059236C) and notify
  (0x0064DCAA..0x0064DD76) as re-read in `20261023-r-m11-config-subscribers-claude-check.md`. The fixture releases the
  robot, its subscriptions and its worker threads, and asserts it.

## The blocker

On the builder's PC the full suite failed twice: first 2, then 1 different test in `M3M4ReviewFixTests`. Each time the
`Live` setup timed out waiting 5 s for the simulated connection state 2. The push gate's third run passed. Here:
- main before B-M13-18 (b4b83695): 6060/6060;
- main with B-M13-18 (76ccce0d): 6065/6065.

The 1 -> 2 transition runs on the engine's own tick (HandleConnectionResponseMessage, `CozmoEngine.cs:474`), so the
5 s waits fail when that tick is starved under full-suite load on the slower machine. Not a stack defect and not a
leak in the new tests. Fix (test-only): the three connection waits in the `Live` setup now use the same 15 s as its
readiness wait. No assertion or behaviour changed.
