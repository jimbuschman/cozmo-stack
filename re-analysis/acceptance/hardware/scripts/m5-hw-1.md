# M5 animation and face test — operator guide

This run checks the C# stack's animation messages and engine state, and records what the real robot does.
You do not decide whether Cozmo "looks right". The program compares the evidence it can source automatically;
you answer concrete questions about what you saw. Unknown firmware behavior remains an observation.

## Run

From the repository root, with the PC connected to Cozmo's Wi-Fi:

```powershell
dotnet run --project cozmo-stack/src/Cozmo.Conformance -- m5-hw --resources re-analysis/obb/assets/cozmo_resources --allow-drive
```

Put Cozmo on the **floor**, off the charger, with at least 30 cm clear around him. The head and lift move,
the face/backpack may change, sound may play, and this command permits wheel motion and a 6 cm drive attempt.
Allow a few minutes plus reading/answer time. Ctrl+C stops the run and preserves its evidence.

Before each check, the program says **what it tests**, **what it records automatically**, and **your part**.
For observations, read the preview, get a clear view, then press Enter. Watch from **START** to **END**.
Answer afterward using a numbered choice. Do not include motion after END in an earlier answer.

To review the exact shared check descriptions and observation previews without a robot:

```powershell
dotnet run --project cozmo-stack/src/Cozmo.Conformance -- m5-hw --preview
```

`--preview` prints the source references too. `--validate` checks the current fidelity references without
resources or network access. Neither command creates a robot or an acceptance bundle.

## What you watch

| Part | What to watch | What to report |
| --- | --- | --- |
| ABORT; first part of KEEP_ALIVE | Eyes, whole head and front lifting arms during a short animation and its stop request | Four separate answers: did the original animation stop; did eye outlines resemble the displayed reference; did you see a blink; did you see an eye dart? The exact animation expression is not prescribed. |
| KEEP_ALIVE live idle | Wheels and whole body for 10 seconds | Whether you saw a small shift/turn. Head or lifting-arm movement alone does not count. No random wiggle is required for a passing visual answer. |
| LOCKED_HEAD | Angle of the whole head during the marked clip | Whether it stayed at its starting angle. Ignore the eye picture and motion after UNLOCK. |
| LAYERS | Face screen and the five backpack lights | Whether the picture changed and whether the lights changed, separately. No exact distortion or simultaneous change is prescribed. |
| PARAMETERS | Eyes for 60 seconds | Whether you saw at least two blinks. If you measured gaps, put them in the notes. **No 44–45 second wall-clock gap is expected.** The program checks the engine's timer separately. |

A **blink** is the eyes briefly closing and reopening. An **eye dart** is the eye shapes sliding inside the
screen; the whole head moving is different. A later blink/dart does not establish that a cancelled animation continued.

For the eye-shape comparison, open the printed `neutral-reference.svg` path in a browser before starting.
Compare the two eye outlines, not color or thin display lines. This picture is generated from the loaded neutral
animation; it is a comparison aid, not proof of what the firmware displays. Missing/different reference appearance
is retained and never silently converted into an engine failure or a passing answer.

**not-seen** means you watched but saw none. **unclear** means you watched but could not tell.
**not-observed** means you missed that part. Free-text notes follow each observation. All answers are retained;
none is an instruction to guess the desired result.

TAGS, AUDIO, LIVE_IDLE, DRIVING, SLEEP and TEARDOWN say **NO ANSWER NEEDED**. The program records their evidence.
You do not judge sound quality, packet counters, successful navigation, or a sleeping expression.

## What each result means

Each check prints a plain-language summary, also saved in `SUMMARY.txt`:

- **MATCH:** captured evidence matched the stated source-backed comparison. It is not M5 acceptance.
- **MISMATCH:** captured evidence contradicted a stated comparison; the saved detail needs review.
- **INCOMPLETE:** setup or required evidence was missing. This does not establish an engine defect.
- **OBSERVATION:** measurements saved without a source-backed pass/fail claim.
- **KNOWN_GAP:** the missing implementation path remains open; its diagnostic was observed.

Physical answers and your notes appear alongside the result, explicitly ungraded. For example, a matched outgoing
stop command can coexist with "original animation stopped: unclear". That uncertainty remains for review.

Local time windows, minimum sample counts, random track occurrence and observation availability are **coverage
requirements**, not engine timing guarantees. A capture can identify a real malformed message even when other
coverage is missing. Mismatch results are not successful exit statuses just because their internal name begins OBSERVED.

## Automatic comparisons and limits

The exact comparisons, current records and native citations are embedded in each check's `sourceComparisons` in
`result.json`. The live prompts omit addresses so you can concentrate on the test.

| Check | Automatic comparison / evidence | Source and limit |
| --- | --- | --- |
| TAGS | First three captured non-idle outgoing tags compared with 1,2,3. Robot replies saved separately. | M5-008 `0x0057B2D4..0x0057B2F6`; layer starts share the allocator, so tags alone do not attribute starts to clips. M5-036 firmware echoes remain observations. |
| ABORT | One valid empty-body cancellation command for the observed active stream. | M5-023 `0x0057B3F2..0x0057B410`, `0x00517DE4..0x00517E0A`, body size `0x007A47DC`. The 400 ms tail is sampling only: last-stream time for neutral replay is unavailable. No assumed face/audio count or quiet interval is graded. |
| KEEP_ALIVE | Cancellation command, ordered Start-FF/End idle bursts, then conditional persistent-face lifecycle. | M5-023; M5-027 `0x0057D3EE..0x0057D44C`; M5-028 `0x0057C52A..0x0057C556`, `0x0057C606..0x0057C646`. Face occurrence in two seconds and burst count in ten seconds are coverage, not guarantees. |
| LOCKED_HEAD | Disable head mask1 before clip, continuing head commands, Enable mask1 after unlock. | M4-014 `0x0064014E..0x00640188`, `0x0063FF94..0x0063FFB4`; M5-035 `0x0057C9D4..0x0057C9E2`. Head movement, firmware track masks and sample counts remain observations. |
| AUDIO | 744-byte sample body / empty silence body plus tag; joined send-budget refresh/admission/accounting. | M2-008/009 `0x007BCDAA`, `0x007BCE8A`, `0x007ABF0A`; M3-012 `0x0057C6F6..0x0057C7AC`, `0x0057BF84..0x0057BFCC`. Both audio paths, five-second span and counter samples are coverage requirements. M6 audio semantics remain unresolved. |
| LAYERS | One face exactly when the joined frame's face-output flag is set; captured lifecycle/drain. | M5-029 `0x0064F1D4..0x0064F22C`, `0x0057C63C..0x0057C646`; M5-028 layer lifecycle above. Unknown joins are incomplete; this does not independently verify random generation or visible appearance. |
| LIVE_IDLE | Packet shape checked; nearest delivered head input and track occurrence measured. | M5-030 `0x0057D83E..0x0057D85C`, `0x004F8C12..0x004F8C4A`. Nearest delivered state is not exact build-time state; no promise of head/body/lift activity within twelve seconds. |
| PARAMETERS | Requested uncapped values read back; actual KeepFaceAlive timer-call transitions checked. | M5-062 `0x0057E89A..0x0057E8B2`, `0x0057C1EC`; M5-028 `0x0058D382..0x0058D38C`, `0x0058D48C..0x0058D550`. Subtract60 **per call**, not wall milliseconds. Missing/discontinuous calls are incomplete capture. |
| DRIVING | Start result selects Loop; matching End action completion precedes drive completion regardless of End result. | M5-063 `0x004F0D80..0x004F0D8C`, `0x004F0E14..0x004F0E2A`, `0x004F0DC8..0x004F0DD4`; boundary M12-024 `0x0055B0BA..0x0055B0C8`. Firmware End echoes and successful traversal are separate observations. M12/M13 acceptance still needs a successful drive. |
| SLEEP | Host fallback and known-gap diagnostic captured. | M1-045 `0x0052CE54..0x0052CE6A`; M5-038 `0x0052CEA2..0x0052CFC0`. Native factory/queue behavior is not claimed reproduced by this diagnostic. |
| TEARDOWN | Actual disposal interval, discarded queued entries, no evidenced streamer final flush; cache cleanup. | M5-037 `0x0057AF58`, `0x0057B044..0x0057B0C0`; M5-059 `0x0057F376..0x0057F37A`, `0x0057FC20..0x0057FC22`, `0x0057FA14..0x0057FA2E`. Empty queues need no discard log; unknown send origins remain incomplete. |

## Saved evidence and options

The final output gives the folder to return for review:
`re-analysis/acceptance/hardware/<date-time>-M5-HW-<unique suffix>/`.

It contains `SUMMARY.txt`, machine-readable `result.json`, raw `frames.jsonl`, deduplicated `messages.jsonl`,
`events.jsonl`, read-only engine `diagnostics.jsonl`, resource/source/binary hashes, and the neutral reference.
Capture timestamps are host times, not firmware execution times. No record is settled and no acceptance is performed.

`--only TAGS,ABORT` selects a diagnostic subset; omitted checks are explicitly incomplete. `--out <directory>`
changes the bundle parent. `--yes` skips readiness/answer prompts and saves not-observed answers. Without
`--allow-drive`, live-idle/drive coverage remains incomplete. A ten-minute watchdog bounds the run; disposal has
a five-second host limit. Exit 0 means no recorded mismatch/incomplete check or run error, not layer acceptance;
known gaps and physical observations remain in the report.

This guide supersedes earlier M5-HW instructions that required firmware echoes, resting-eye resemblance,
a visible wiggle, quiet cancellation-tail counts, or a 44–45 second physical blink interval as engine expectations.
