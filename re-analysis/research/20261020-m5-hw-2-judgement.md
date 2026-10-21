# M5-HW second run: startup failure

Manager: Codex, 2026-10-20, continuing the operator-authorized M5 handoff after
Claude's usage limit. Bundle supplied by the operator:
`C:\Users\jbuschman\Downloads\20261020-180933-M5-HW-bde6cd`.
Run commit: `1bd606265157ef133337e634ab7ea023536725b8`.

**NOT ACCEPTED.** The test connected before the app-side loading prerequisite,
and its per-Robot neutral-face initialization failed. The missing neutral
reference is a real setup defect. The harness should have prevented this order
and stopped before asking the operator to evaluate a run with invalid setup.
No engine behavior, inventory, manifest status, or verification is changed by
this judgement. The original bundle is preserved unchanged outside the repo.

## Reproducible bundle facts

`20261020-m5-hw-2-startup-replay.py` reads the original bundle without changing it.
Its saved output is `20261020-m5-hw-2-startup-witness.json`: original evidence
hashes, the matching saved source hashes, raw event excerpts with line numbers,
per-check verdicts, and the actual driving result. All six saved source copies
match the hashes in the run's result.

| Host capture time | Evidence in events.jsonl |
| --- | --- |
| 3466.9059 ms | Warning: unknown response `NeutralFace`. |
| 3467.2817 ms | Error: unknown animation group, empty name. |
| 3467.4510 ms | Error: `AnimationStreamer.Constructor.BadNeutralAnimGroup`. |
| 3811.9637 ms | Loader reads `anim_neutral_eyes_01`. |
| 3884.0726 ms | Loader reads `ag_neutral_face`. |
| 6798.2826 ms | Harness records assets/calibration ready, after the failed constructor lookup. |

The run's neutral reference says the loaded neutral clip/first face is
unavailable; there is no `neutral-reference.svg`. `result.json` has `errors: []`
and `SUMMARY.txt` says `Run errors: None`. Those fields omitted the engine
startup errors and made the run's setup failure easy to miss.

## Path and classification

Bounded path: hardware CLI -> CozmoRobot.Create/resource startup -> direct
ConnectToRobot game input -> native/C# AddRobot -> per-Robot streamer creation ->
one-time neutral trigger/group/clip lookup and reset -> asynchronous resource
completion -> delivered readiness -> harness reference/checks -> saved verdicts.

The independent extraction is `20261020-m5-startup-extraction.md` with native
instruction and current-manifest companions. The manager reread current M2-021
and M5-010 before adopting the correction:

- **M2-021, COMPATIBILITY_POLICY:** "App input: when ConnectToRobot is sent (the
  app waits for EngineLoadingDataStatus ratio >= 1); the engine has no
  load-before-connect gate". Native handler `0x004ED020..0x004ED07C`, AddRobot
  `0x004ED1E0..0x004ED1F2`; app `StartupManager.cs:322-341`,
  `ConnectionFlowController.cs:670-673`. The current record expressly confirms
  `IsReadyDelivered` as the stack's host wait point. CozmoRobot.ConnectAsync and
  M1M2-HW already wait; the three affected direct-connect harnesses did not.
- **M5-010, EXACT_SOURCE:** "Neutral face: the first ProceduralFace keyframe of
  the first clip of ag_neutral_face; reset data and layer base; replayed after
  abort-to-nothing and RemoveIdle". Constructor `0x0057A0D4..0x0057A288`, reset
  `0x0058359C`, shipped trigger map. Current unresolved history explicitly
  settles once-per-Robot loading and no load-completion retry. The engine's
  missing-neutral diagnostic and retained prior reset behavior are defined;
  adding an engine retry would contradict the recovered path.

Classification: **test/harness bug**, repair under existing approved records.
No new inventory approval is required. Implementation is separate from
extraction. B-M5-35 completes the interrupted harness changes and verifies them.

## What this run does and does not establish

Seven stored check labels are MATCH, two INCOMPLETE, one OBSERVATION and one
KNOWN_GAP. The outgoing measurements are retained as observations; those labels
do not establish a valid M5 acceptance run. Neutral-dependent results need a
correctly initialized rerun. We do not erase a captured contradiction merely
because setup was invalid.

The missing neutral explains the missing reference and failed initialization.
It does **not** establish that every face picture is absent or wrong: explicit
animation frames can still supply face data. The operator's asymmetry, upper-right
position, blinking and continuing-animation reports remain observations. This
judgement does not claim the setup repair alone resolves LAYERS' failure to drain
or KEEP_ALIVE's incomplete persistent-face coverage.

Claude's pasted handoff said driving succeeded. The actual DRIVING result is
**FailedTraversingPath**, with `successfulTraversalCoverage: false` and drive
action result `67108866`. Its MATCH covers the selected animation phase path;
successful traversal remains open for M12/M13 as previously agreed. It is not
closed by this run.

The handoff also reports early-connect logs in the previously accepted M3/M4
run. That run's affected acceptance claims require a separate bounded impact
check; this M5 fix neither silently withdraws nor reaffirms them. No repository-
wide audit or other layer implementation is started here.
