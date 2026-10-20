# M5-HW v1 â€” animation and face hardware observations

This is the operator script for the eleven checks in the adopted 2026-10-19 M5 settling pass B.
The executable is `Cozmo.Conformance m5-hw`. It creates evidence, never commits, never edits the manifest,
and never marks M5 ACCEPTED. M5-036 stays HARDWARE_ONLY until the manager judges a robot run.

## Offline validation (Codex, 2026-10-20)

Independent read-only review: PASS, including a focused re-review of the final driving-completion correlation
and sleep diagnostic predicate. The checks follow the production API/engine/streamer/transport path; no production
behavior or fidelity status changed.

- Build and `m5-hw --validate`: passed, 11 checks and 28 current manifest records, no network opened.
- `fidelity.py --check`: passed, 706 records.
- Full suite: 5,911 passed, one failure, zero skipped. The failure was the repository source scan rejecting the
  sleep reader's bare diagnostic-word match; the reader now matches the actual M5-038 stack-log prefix.
- Final focused run after that correction and the last driving-correlation change: 25/25 passed
  (`M5HwTests` plus `M11DelocalizationB8Tests.NoSourceFileWritesAMissingIntoTheEngineLogStream`).

The full suite was not repeated after the focused correction. No hardware run or commit was made.
Remaining uncertainty is the real robot's behavior and the explicitly embedded higher-layer gaps, not a
source-fidelity claim inferred from these offline tests.

## Prepare and run

From the repository root, build and perform the offline preflight:

```powershell
dotnet build cozmo-stack/src/Cozmo.Conformance/Cozmo.Conformance.csproj
dotnet run --no-build --project cozmo-stack/src/Cozmo.Conformance -- m5-hw --validate
```

`--validate` reads the current manifest and all referenced records; it does not create a robot, open a socket,
or require extracted assets. Unknown/missing record IDs stop preflight. It prints unresolved cross-layer records.

Connect the PC to Cozmo's Wi-Fi. Put Cozmo on the **floor**, off the charger, with at least 30 cm clear on all sides.
The head and lift move, face and backpack animate, and audio may play. With `--allow-drive`, live idle may wiggle
his wheels and the driving check requests a 60 mm forward goal. Keep hands clear and use Ctrl+C to stop the run.
The engine's cliff behavior is not a substitute for the floor setup.

```powershell
dotnet run --no-build --project cozmo-stack/src/Cozmo.Conformance -- m5-hw --resources re-analysis/obb/assets/cozmo_resources --allow-drive
```

If the extracted resources are elsewhere, replace that path. An optional robot IP precedes the flags; the default
is the same production robot address used by the existing hardware tools. Expect about three minutes plus loading
and operator answers. Answer the observation prompts; numbers and case-insensitive choices work, and spaces can
replace hyphens. Ctrl+C cancels bounded waits and attempts Dispose, preserving the bundle. A ten-minute watchdog
cancels prompts/waits. Dispose is bounded to five seconds; a timeout is an error and invalidates teardown evidence.

Without `--allow-drive`, KEEP_ALIVE's live-idle part, LIVE_IDLE and DRIVING are explicitly incomplete.
`--yes` suppresses prompts and records missing human observations; it is not an acceptance shortcut.
`--only TAGS,ABORT` selects a diagnostic subset; every omitted check remains INCONCLUSIVE in the bundle.
`--out <directory>` changes the parent output directory. The default is:

`re-analysis/acceptance/hardware/<yyyymmdd-hhmmss>-M5-HW-<unique suffix>/`

## Check coverage and judgment

| Check | Stimulus and evidence | Source boundary |
| --- | --- | --- |
| TAGS | Three short shipped clips without body tracks, through PlayTracked; Start tags 1,2,3 and chronological started/ended echoes. | M5-008 increment 0x0057B660..0x0057B672; robot echo meaning M5-036. |
| ABORT | Cancel a long shipped clip after 750 ms. Count exactly one 0x8D, no End, at most one audio/face marker within 400 ms after the ordered wire Abort (pre-command backlog is separate); preserve full counter/echo tail and operator observation. | M5-023 A22/A23/A25; pending built frame can drain. Robot semantics M5-036. |
| KEEP_ALIVE | Same cancellation, two-second neutral tail; with drive enabled, ten seconds of ProceduralLive with ordered Start FF/End bursts; then a separate ten-second no-idle persistent-eye-dart face window, requiring face output and no End while its prerequisites hold. | A31 0x0057CF6A..0x0057CFF2 measures the 0.5 s gate from last stream activity, **not cancel time**. A29/L8 burst idle; M5-027/030. Persistent layers: M5-028/029, 0x0057C530..0x0057C556. |
| LOCKED_HEAD | Lock HEAD_TRACK (mask 1; mask 4 is BODY_TRACK, corrected by the manager 2026-10-20), play shipped head clip, unlock; retain 0x93, exactly one EnableAnimTracks and physical observation. | M4-047 plus robot-side M5-036. RobotState samples let the reviewer compare reported motion. |
| AUDIO | Shipped >=5.5 s audio clip without body; Wwise bank source if available, sample/silence lengths, >=5 s span, rate, stream/play counters, drops and budget snapshots. | C9 <=14 frames/8192 bytes; direct End adds a frame outside C9. M6 runtime uncertainty remains embedded. |
| LAYERS | Temporarily suppress keepalive with a 600 s timeout, short nonlive play removes persistent keepalive layers, then one finite host distortion pulse. Require face per audio frame, backpack changes, Start/End and drained layers; restore settings. | A12 removes keepalive; M5-031 TLC.Update/AddGlitch, Q1.8/Q1.9. The provider and temporary timeout are explicit harness controls, not M7 behavior claims. |
| LIVE_IDLE | Twelve seconds of default ProceduralLive; compare streamed signed head angles with preceding Motion.HeadAngleRad samples and default +/-6 degrees; require body/lift wiggles. | M5-030 0x0057D838..0x0057D84E, A34 defaults. Uncalibrated input is not forced; B-M5-23 covers it offline. |
| PARAMETERS | Public message setter sets blink min/max to 44000/45000, confirms readback, observes for 60 s; restores defaults. | M5-062 0x0057E8A2 calls base setter uncapped. A narrow valid min<max window makes spacing measurable; equal bounds trigger native fallback; an already scheduled blink may fire first. |
| DRIVING | With drive consent, actual ManipulationSystem / DriveToPose.RunAsync 60 mm forward; record animation transitions, API action completion order, result and trace; 15 s timeout and stop. | M5-063 default 0x95/0x91/0x8D triplet; M12-024/M8/M7/M15 unresolved dependencies remain explicit. Failure to reach the triplet is incomplete evidence, not a replacement implementation. |
| SLEEP | Public StartIdleTimeout(0.2,-1), observe visible MISSING and GoToSleepRequested; cancel timer. Capture new animation starts and logs for absence of sleep-child actions. | M5-038/M1-045; D2/D5/D4 are factory requests, not robot message IDs. The M8 action gap stays open. |
| TEARDOWN | Dispose during a long clip; verify stale TestAnim.json deleted on construction and Dispose; observe no data after Dispose completion. | M5-037/053/055/059. Teardown batch may contain stop/disconnect; M1-006 pacing may prevent a DisconnectRequest reaching the wire. |

All stimulus intervals and the drive distance are harness choices, not recovered engine constants.
Clip selection is deterministic by duration then name, excluding body tracks; selected names, durations and track
sets are recorded. Missing suitable assets produce INCONCLUSIVE. No fabricated clips replace shipped assets.

## Evidence bundle

- `result.json`: all eleven checks, exact current fidelity records (including unresolved cross-layer uncertainty),
  arguments, resource root, timestamps, commit, errors and provenance hashes.
- `frames.jsonl`: every raw datagram and parse error, with host capture timestamp and direction.
- `messages.jsonl`: decoded submessage payload hex, reliable retransmissions deduplicated within a bounded sequence
  window; raw frames remain authoritative. Timings are host capture times, not firmware execution times.
- `events.jsonl`: startup diagnostics subscribed through BeforeStart, connection response, robot/animation state,
  stream counters, API action completion reports, play/cancel calls, errors and check boundaries.
- `provenance/`: script, harness sources, entrypoint, manifest and adopted settling report, with SHA-256 hashes.
- `binaries/`: exact loaded Cozmo assemblies and their hashes; `resource-hashes.json` identifies animation/group/sound files.

Counters are lifetime counters, not reset per play. Delivery-time stream counters are not atomic budget snapshots:
review snapshots above 14/8192 using raw order and End boundaries before deciding there is a production defect.
Head samples likewise precede wire capture, not necessarily frame construction; queue lag can explain apparent
+/-6 outliers. Observe robot stop, echo tags, played counters and drops together for M5-036.

`OBSERVED_*` describes captured evidence and its rule; it is not PASS or ACCEPTED. A malformed observation remains
visible and requires review. `INCONCLUSIVE` names missing prerequisites, skipped motion, absent messages or incomplete
human observations. Exit 1 means at least one incomplete check or run error; 2 is a preflight/argument failure.
Return the **whole bundle folder** to the manager. The script does not classify hardware failures or modify production
behavior. The manager traces any failing path and reviews evidence before deciding corrections or acceptance.


## Dated correction — B-M5-24, 2026-10-20

The original no-End claim for ProceduralLive was a harness error (checked first-run judgement section 1).
KEEP_ALIVE now retains chronological FF Start/End pairs, burst spans and restart gaps. It requires repeated
bursts in a ten-second harness window; an open final burst is recorded. Invalid ordering remains visible.
These wire observations do not certify exact restart timing: review generation, engine ticks, drained FIFO and
audio completion against 0x0057D414..0x0057D44C. No scheduler behavior changed.

A distinct observation primes a short nonlive clip, waits for no explicit play, idle top Count and the normal
KeepAliveEyeDart face layer, then captures ten seconds. Preconditions are sampled via existing public properties
about every 60 ms (not an atomic production hook). It requires face output and no End/Abort in that window;
Start may precede the window. Failure to observe the prerequisites is INCONCLUSIVE. This is the persistent-layer
case, not a no-End rule for all idle states. Hardware-only M5-036 and operator observations remain unresolved.
Historical run results and the validation history above are preserved. An operator rerun is required.

## Dated correction — B-M5-25, 2026-10-20

Abort leftovers are now anchored after the actual outbound Abort submessage, with raw frame line, ordinal,
reliable sequence and connection epoch retained. A timestamp alone cannot order data within a datagram.
The 400 ms quiet interval starts at wire Abort; pre-command transport backlog is reported separately.
Reliable resends count once; raw frames retain all copies. Candidate neutral Start traffic is retained separately.
The native last-stream engine clock is not available through the allowed public observations: no exact
neutral deadline is inferred from cancel-call time. The historical first-run observations remain unchanged.
Operator rerun and independent judgment are required.
