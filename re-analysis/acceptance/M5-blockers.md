# M5 acceptance: what stands in the way (manager, 2026-10-19)

- **Target:** M5-animation reaches ACCEPTED, under "Done for a layer" in `PROJECT_STATE.md`, judged by AGENTS.md
  "What we reproduce" (2026-10-17).
- **Examined:** `main` at 3799cdcd. Sources: the manifest, `jobs/QUEUE.md`, `jobs/STATUS.md`, the check files named below.
- **Labels:** **[F]** checked against the current files; **[E]** estimate; **[U]** unknown.

## Records

[F] 65 records. 30 EXACT_SOURCE; 8 COMPATIBILITY_POLICY (M5-020, 042, 047, 048, 049, 057, 060, 061); 3
EQUIVALENT_IMPLEMENTATION (M5-003, M5-021, M5-032: bionic libm only); 1 HARDWARE_ONLY (M5-036, verification NONE: no
robot run has settled it); **23 IMPLEMENTATION_GAP**, no RECOVERABLE_GAP.

[F] Every one of the 23 says "built, awaiting strong verification". The last M5 build chain ends in B-M5-22
(30576f80, trigger-response loader), which has **no check yet**; B-M5-21 before it FAILed. Earlier chains end in Opus
PASSes (B-M5-11, B-M5-20). No settling pass has run over the records since the M5 final passes of 2026-10-12, and much
was built after them (B-M5-13..22, B-M3M4-24, B-M3M4-36's world model).

| Group | Records | Known remaining pieces (from `unresolved`; to be confirmed by the pass) |
| --- | --- | --- |
| Clip and asset loading | M5-001, M5-009, M5-013, M5-016, M5-051, M5-052, M5-054, M5-065 | B-M5-22 unchecked (M5-034, M5-051); readAsJson reader-error text G5b (log only, best effort); PNG colour/16-bit decode (no shipped file); M5-013 +0x28 writers 0x004F9770/0x004F97DE [U] |
| Group choice, cooldown | M5-011, M5-014 | SimpleMood input is M7's; cooldown time is M7-026's |
| Streaming, idle, neutral | M5-008, M5-010, M5-027, M5-030, M5-062, M5-038 | M5-010 reopened by the B-M5-13 check (reconnect SetResetData; constructor read) [U whether closed]; M5-008 MISSING verify callstack 0x0080DA08 / provider flush 0x0080DA24; M5-038 factory recipient is M8's (cross-layer split) |
| Driving animations | M5-063 | action-side call sites are M12-024's |
| Teardown | M5-037, M5-044, M5-053, M5-055, M5-059, M5-064 | native handle ownership MISSINGs (0x00511524..0x00511542, 0x0057AF6C, 0x0057AFD4); engine+0x38 type [U]; ~CozmoEngine order is a host input |
| Robot-side | M5-036 | HARDWARE_ONLY: needs the M5 hardware run |

Logs: M5-013's BufferFaceToSend key/text (B-LOG-1, HELD as cleanup) is not on the acceptance path.

## Sequence

**Update (2026-10-19): B-M5-22 Opus check PASS** (`research/20261019-b-m5-22-check.md`); six queued items
(log order, a depth-limit reload count, two tests, reach labels, duplicate test entries, ResourceDirectory candidates)
go to the next M5 job. Next: settling pass A.

1. **Now:** the Opus check of B-M5-22 (`research/20261019-b-m5-22-check.md`).
2. Two read-only Opus settling passes at one HEAD, run one after the other (A: loading, group choice, face; B:
   streaming, idle, neutral, driving, teardown, plus the policy confirmation and the hardware coverage list). Output:
   `research/20261019-m5-settling-a.md`, `-b.md`.
3. The manager adopts them: settle what holds, split cross-layer pieces, jobs for what doesn't, research requests for
   the narrow unread pieces.
4. Builds and their checks; settle.
5. The M5 hardware script (M5-036 and the pass-B coverage list); the operator's run; ACCEPTED.
