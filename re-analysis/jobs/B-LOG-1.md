# Job B-LOG-1: six engine-log sites with wrong key, text or channel (found by the log-key pre-check)

- **Agent:** Codex, as builder. Follow `jobs/CODEX-BUILDER.md` rules 1–15.
- **Status file:** `status/B-LOG-1.md`.
- **Runs after B-M12-7.**
- **Checked rows:** `research/20261016-logkey-sites-check.md`, rows L1–L6 (an Opus check; it is the checked evidence).

## Items

Each site gets the native level, channel, key, format text and argument order exactly, under the native condition.
Expected values in the tests come from the rows, never from the code.

1. **L1** (`Sensors.cs:903`, M10-011):
   - channel "Unnamed", key "Robot.HandleFallingStarted.FallingStarted", text "timestamp: %u";
   - logged before the game FallingStarted.
2. **L2** (`CompoundActions.cs:187`, M8-017):
   - key "ICompoundAction.GetCompletionUnion.InvalidProxyTag", text "CompletionData with proxy tag=%d not found", tag
     printed signed, under the native condition.
   - Move the site's `// fidelity:` tag from M7-020 to M8-017.
   - The two UsingProxy Debug logs (0x0054F2F2, 0x0054F37E) have their arguments untraced: report each as a MISSING
     with its address. **Don't guess the arguments.**
3. **L3** (`AnimationScheduler.cs:1654`, M5-013):
   - key "ProceduralFaceKeyFrame.GetStreamMessageHelper", text with the trailing period;
   - set the error flag (`_errG`) as the engine does.
   - Whether `payload is null` is the same condition as CompressRLE ≠ 0 is unread. Keep the stack's condition, and
     note it as a MISSING naming 0x0057C216.
4. **L4** (`Mood.cs:552`, M7-013):
   - key "MoodManager.BadTimeStep", format "TimeStep %f (%f-%f) is < %f - clamping!", with the arguments dt, now,
     last, 1e-4f widened;
   - condition dt < 1e-4f with NaN not logging; dt = 1e-4f when last == 0, so that case never logs; then the clamp.
   - `%f` is printf's six-decimal rounding: use the stack's existing printf-%f helper if there is one; otherwise report
     a MISSING.
5. **L5/L6** (M10-003):
   - move the log into the shared RobotReactedToId: channel "ReactionTriggers", key
     "ReactionTriggerStrategyPoseDifference.ReactionIdInvalid", the native text, the id printed **signed**, only on a
     map miss;
   - read GetLastImageTimeStamp first;
   - FinishedReactingToFace then does no logging of its own.
   - Whether the face strategy ever fills the map is unread. Don't add or remove fills.

**Then run `logkey_check.py` on every file you touched. It must report no ABSENT keys.**

## Out of scope

- Status changes.

**Manifest:** append only.
