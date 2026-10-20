# B-M5-34 sourced expectations and runnable instructions

Manager: Codex, 2026-10-20. This review supersedes the prompt-only acceptance in
`20261020-b-m5-34-check.md`. That earlier review preserved grading rules which did
not meet the operator's subsequently explicit sourced-expectation requirement.

## Problem and complete path

The operator could not tell what was being tested or what to look for. The harness
also mixed source comparisons, physical observations and capture requirements.
Its output could treat an `OBSERVED_*` contradiction as a successful run.

The bounded path traced was command-line selection/preflight -> check instructions
and readiness -> existing public animation/motion/action calls -> existing engine,
scheduler, stream buffer and transport -> raw captures, API events and diagnostic
hooks -> per-check helper analysis -> caller verdict overrides -> console, bundle
and exit status. All eleven checks, including nested results and overrides, were
included. This was a harness failure; no production-engine defect was established.

## Primary evidence and adopted corrections

Two separate read-only audits checked the shipped native library and current
manifest. Their instruction companions and current-record quotations are saved:

- `20261020-m5-verdict-audit-a.md` and `20261020-m5-verdict-audit-a-native.txt`.
- `20261020-m5-verdict-audit-b.md`, `20261020-m5-verdict-audit-b-native.txt`, and
  `20261020-m5-verdict-audit-b-records.json`.

The manager reread the current manifest and independently decoded the tag,
cancellation sender, neutral deadline, layer face flag, layer end gate, blink
decrement, End action handler, audio sizes and empty-buffer teardown branches.

| Check | Remaining source comparison and boundary |
| --- | --- |
| TAGS | M5-008, `0x0057B2D4..0x0057B2F6`, `0x0057C410..0x0057C424`: fresh outgoing tag sequence. Firmware echoes are observations. The untouched streamer's negative last-stream time and positive-time keepalive gate mean the reference-viewing pause alone does not start keepalive. The comparison does not attribute arbitrary layer starts to particular clips. |
| ABORT | M5-023, `0x0057B3F2..0x0057B410`, `0x00517DE4..0x00517E0A`, empty body at `0x007A47DC`: one cancellation command after the exercised active stream. The 400 ms capture tail is retained, without pretending that the native neutral deadline is measured from wire Abort. |
| KEEP_ALIVE | M5-023 cancellation sender; M5-027 `0x0057D3EE..0x0057D44C` for live burst ordering; M5-028 `0x0057C52A..0x0057C556` for persistent-layer lifecycle. Local durations and observed track/burst counts are coverage requirements. Eye resemblance, visible wiggles and stopping remain observations. |
| LOCKED_HEAD | M4-014 `0x0064014E..0x00640188`, `0x0063FF94..0x0063FFB4`; M5-035 `0x0057C9D4..0x0057C9E2`: outgoing lock/unlock ordering and continued head commands. Received firmware flags, reported motion and physical answers do not gate this comparison. |
| AUDIO | M2-008/009 `0x007BCDAA`, `0x007BCE8A`, union size `0x007ABF0A`; M3-012 `0x0057C6F6..0x0057C7AC`, `0x0057BF84..0x0057BFCC`: framing and joined budget decisions. Sample duration, both audio paths and counter counts are coverage, not guaranteed timing. |
| LAYERS | M5-029 `0x0064F222..0x0064F22C`, `0x0057C63C..0x0057C646`: face output follows the captured frame flag. M5-028 owns the conditional start/end lifecycle. Missing/ambiguous joins remain incomplete; visual shape and exact random trace are not inferred. |
| LIVE_IDLE | M5-030 `0x0057D83E..0x0057D85C`, `0x004F8C12..0x004F8C4A`: packet shape. Nearest delivered head samples and random track occurrences remain observations; a twelve-second sample is not a deadline promise. |
| PARAMETERS | M5-062 `0x0057E89A..0x0057E8B2`; M5-028 `0x0058D382..0x0058D38C`, `0x0058D506..0x0058D550`: parameter readback and per-call timer transitions. There is no 44–45 wall-second visible-blink expectation. Discontinuous hook capture is incomplete evidence. |
| DRIVING | M5-063 `0x004F0D80..0x004F0D8C`, `0x004F0DC8..0x004F0DD4`: Start result selects Loop; matching End completes irrespective of result. M12-024 `0x0055B0BA..0x0055B0C8` owns the action-side wait. End success/echo and navigation success remain separate observations. |
| SLEEP | M1-045 `0x0052CE54..0x0052CE6A`, M5-038 `0x0052CEA2..0x0052CFC0`: native factory/queue boundary. Captured host fallback and missing-path diagnostic are explicitly a known gap, not native sleep execution. |
| TEARDOWN | M5-037 `0x0057AF58`, `0x0057B044..0x0057B0C0`: discard without final flush. Empty queues emit no per-entry marker. M5-059 `0x0057F376..0x0057F37A`, `0x0057FC20..0x0057FC22` owns cache cleanup. Unknown send origin is incomplete evidence. |

## Exact change and fidelity impact

A separate implementer changed only the M5 conformance harness, its focused tests
and operator guide. Each check now states its purpose, automatically recorded
evidence and physical observation task. An offline preview uses the same
descriptions. Readiness/START/END boundaries and raw answers remain explicit.
Source comparisons are saved with record/address citations and checked against
the current record list. A readable summary distinguishes matched comparisons,
mismatches, incomplete evidence, observations and known gaps.

No robot command, sample duration, production engine implementation, frozen
inventory, manifest status or historical hardware bundle was changed. The
operator-approved driving-phase split is preserved. No new inventory approval is
required for correcting these harness comparisons against existing approved
evidence. M5 remains unaccepted and M5-036 remains HARDWARE_ONLY.

## Verification

Independent affected-diff review: PASS (`20261020-b-m5-34-verifier.md`).
The reviewer found and rechecked three corrections: the missing KEEP_ALIVE
record reference, cancellation/teardown mismatches masked by parent coverage
results, and malformed idle-head packets masked by missing track coverage.

Final focused M5Hw suite: 90/90, no skips. Offline --validate: 11 checks and 44
current records, no network. Offline --preview --only ABORT,PARAMETERS passed
and its output was reviewed. Fidelity checker: 706 records passed.
The regression tests exercise real analysis helpers with synthetic capture data;
they are unit/harness coverage, not a hardware run or proof of engine fidelity.
They cover firmware-echo independence, neutral traffic after cancellation,
End-result independence, incomplete blink capture, physical-answer retention,
malformed packets amid missing coverage, and nested mismatch reporting.

Full-suite gate: 5979/5979 passed, zero failures/skips, 6 m 20 s. Run once before
commit with `dotnet test cozmo-stack/Cozmo.sln --blame-hang --blame-hang-timeout 5m`;
local log `.scratch/b-m5-34-full.log`. No hardware was run by Codex.

## Remaining uncertainty

Offline validation cannot establish firmware reactions, display appearance,
physical motion or human usability on the operator's setup. Unknown observations
remain visible for bundle review; they are not converted to invented expected
results. The existing M6 and M8/M12/M13 boundaries remain open. A completed
source comparison covers only its stated relation, never all records listed as
context for that check.
