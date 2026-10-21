# M5 animation and face test — operator guide

## What this test is for

The program makes Cozmo animate, records every message the stack sends him, and checks those messages against
the original Cozmo engine. **That checking is automatic.** In four of the eleven parts you are asked what you
saw. Your answers are saved as notes; they never make anything pass or fail, so any honest answer is fine.

## Run

PC on Cozmo's Wi-Fi. Cozmo on the **floor**, off the charger, 30 cm clear around him. From the repository root:

```powershell
dotnet run --project cozmo-stack/src/Cozmo.Conformance -- m5-hw --resources re-analysis/obb/assets/cozmo_resources --allow-drive
```

To see exactly what the run will show you, without a robot:

```powershell
dotnet run --project cozmo-stack/src/Cozmo.Conformance -- m5-hw --preview
```

The run first connects (up to a minute, nothing to watch). If connecting fails, it stops before any part and
says why. Then the parts run in order. Each one starts with a header like
`=== PART 3 of 11: KEEP_ALIVE (about 30 s) ===` saying what Cozmo will do, what the program checks, and your job.

## The parts

| # | Part | Cozmo does | Your job |
| --- | --- | --- | --- |
| 1 | TAGS | plays one short face animation three times | nothing |
| 2 | ABORT | starts a long animation and stops it after ~¾ s | nothing |
| 3 | KEEP_ALIVE | stops an animation again, 10 s of idle wiggles, 10 s face-only idle | **watch the eyes (~3 s), then the wheels (10 s)** |
| 4 | LOCKED_HEAD | locks the head, plays an animation that normally moves it | **watch the head** |
| 5 | AUDIO | plays an animation with sound | nothing |
| 6 | LAYERS | the face screen glitches briefly | **watch the face and the backpack lights** |
| 7 | LIVE_IDLE | 12 s of idle wiggles | nothing |
| 8 | PARAMETERS | sets blinking to about once per 44 s, then sits for 60 s | **count the blinks** |
| 9 | DRIVING | tries to drive 6 cm forward | nothing |
| 10 | SLEEP | asks to go to sleep (known missing piece; nothing visible should happen) | nothing |
| 11 | TEARDOWN | disconnects in the middle of an animation | nothing |

About 4 minutes plus connecting and your answers.

## When you watch

1. The program says what to look at and for how long, and lists the questions it will ask.
2. Get a clear view and press Enter. `>>> WATCH NOW <<<` appears.
3. Watch until `>>> STOP WATCHING <<<`.
4. Answer each question: `1` yes, `2` no, `3` not sure, `4` I missed it. For blinks, type the number you counted.
5. Optionally type anything odd you noticed, or just press Enter.

## Reading the results

After each part, and again in `SUMMARY.txt`, there is one line:

- **OK:** the stack sent what the original engine sends.
- **PROBLEM:** the stack sent something the engine would not. Needs review.
- **COULD NOT CHECK:** setup or evidence was missing; the reason is shown. Not a failure by itself.
- **KNOWN MISSING PIECE:** reported as expected (SLEEP).
- **SAVED FOR REVIEW:** recorded only; nothing to pass or fail (LIVE_IDLE).

The run ends with one `OVERALL:` line. Either way, send the results folder back for review.

## Reference for review: what each part compares, and its engine source

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
changes the bundle parent. `--yes` skips the prompts and saves "not-observed" answers. Without
`--allow-drive`, live-idle/drive coverage remains incomplete. A ten-minute watchdog bounds the run; disposal has
a five-second host limit. Exit 0 means no recorded mismatch/incomplete check or run error, not layer acceptance;
known gaps and physical observations remain in the report.

