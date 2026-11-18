# LIVE-NARRATION-1

Shared `LiveNarration` now runs in every `m15-cube-hw` mode and in `cozmo-mcp` (stderr only; stdout stays MCP JSON-RPC). It prints timestamped posture, behavior/reaction changes, acknowledged animation names/tracks/durations, action results, cube events and face identity/loss/enrollment events. Repeated identical sighting identities and equal-endpoint behavior reports do not flood the display; the original evidence streams remain intact.

Animation identity comes from a read-only InitStream diagnostic snapshot (0x0057B674): tag, exact clip name, present stream tracks and native last-keyframe end time (0x00578D44..0x00578E3A). The snapshot is queued under the existing diagnostic contract, with console/file writing on a separate host timer. It is not announced as a start. R2E CA/CB drive actual start/end narration (PlayAnimation handlers 0x005452F0 / 0x0054541C); E2G 0x5F drives abort narration (0x005457D8..0x00545862). Playback keeps its start-time identity/behavior across replacement, tag reuse and delayed end-after-abort notices. Unmatched tags show unavailable identity/duration/tracks; missing end reports are not described as proof of continued playback. Tracks describe authored content, not measured physical motion.

Personality changes:

- Every non-idle step now waits for its named engine behavior/reaction or cube event before asking. The original pickup/back/side/face/shaken/cliff steps use the shipped `reactionTrigger_behavior_map.json` and native E2G 0x87 report. A stop before the matching start cannot finish the window. Missing triggers and still-running behaviors at the printed limit are stated.
- Enter starts a step but cannot end its trigger wait. The observation question names robot-started clips after the requested trigger and accepts yes/no/partly. The result stores text `answer`; the older nullable `observed` is true/false/null, with partly null. Engine-report checks remain separate from operator observations.
- Cube-event steps retain a minimum five-second response observation and wait for known nonzero-duration clips to finish, bounded by the 45-second step window. Five seconds is an explicit host observation policy, not a recovered native reaction threshold. Tap source is M4-023 (0x00630C4C..0x00630C72 / 0x00631178..0x00631192); movement M4-009 (0x00534190..0x005341B0). No reaction is invented for a tap.
- Narration lines and initialized identities are retained as `live-narration` JSONL events with report times; run provenance now includes the narration and personality source files without duplicate entries.

Offline display replay: `re-analysis/tools/replay-live-narration.ps1 -Bundle <20261117-121734-PERSONALITY-HW-1-77ccf7 directory>`. It reads that recording's pickup phase (43329..61604 ms), native selected names from typed action completions, and actual CA/CB messages. A selected name is associated only when exactly one CA lies inside its recorded PlayAnimation precondition/completion span. Ambiguous association fails the replay. It does not choose an animation-group member again, construct a physical transport, rerun the reaction, or inject successful engine results.

The replay prints:

```
[00:48] ReactToPickup: playing anim_reacttocliff_pickup_03 (3.3 s, sound+face+head+lift+body)
[00:51] ReactToPickup: ended anim_reacttocliff_pickup_03 after 3.3 s
[00:53] ReactToPickup: playing anim_reacttocliff_pickup_06 (3.0 s, sound+face+head+lift+body+lights)
[00:56] ReactToPickup: ended anim_reacttocliff_pickup_06 after 3.2 s
```

Its new question includes both selected clips and their tracks with [yes/no/partly]. The older recording lacks selected names for intervening non-action tags 6/8/10/12; these stay explicitly unavailable, not guessed to be neutral clips. New live runs capture InitStream identities directly. No recording, frame or hardware bundle is committed.

Validation: 32 focused tests passed, including the recorded display replay, delayed/reused-tag identity, abort/end reporting, no announcement before CA, trigger/stop ordering, partly input, cube-event settling, MCP and personality regressions. Existing M5HwHookTests.ASubscriberChangesNoOutboundByteOrderOrRandomDraw passed with the new observation. The replay script passed. Offline personality, skills, commands, freeplay-control and MCP configuration checks passed; no robot connected. Fidelity check passed (763 records). Full suite **8647/8647 passed** in 26.5443 minutes, with all six private narration/charger/face replay cases active.

Fidelity impact: no status, frozen evidence, verification or scope acceptance changes. This is host presentation/observation work plus read-only instrumentation; it does not repair physical reactions or certify their hardware result. TEST-FAULT-GUARD-1 is next as requested.
