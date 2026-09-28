WAITING B-M6b-1 2026-09-28 18:26 -05:00 opencode

## Why

B-M6b-2's **Needs** line requires B-M6b-1 DONE. B-M6b-1 is `BLOCKED` on `origin/main` (commit `873e2d0`), not DONE: the exact bad instruction inside the generated IMDCT kernel (`0x00AB4E34..0x00AB5A1C`, M6-002) is not established, and no production or test change was retained. Its blocker report is `re-analysis/research/20260928-B-M6b-1-numeric-localization.md`.

B-M6b-2 builds the voice pass, bus pass (bus metering, C12), per-voice DSP chain and the four Perform group members on top of the M6-022 skeleton (`427d27a`). That composition needs the M6-002 Vorbis packet driver (B-M6b-1's scope) to decode a shipped source before it can render a posted event to the output buffer in tests, so the gate is a real dependency, not a formality.

Per the job file's **Needs** line, this job is set `WAITING B-M6b-1` and ends. No files were changed except this status file; nothing was claimed beyond it.

## Resume

When B-M6b-1 is DONE, claim B-M6b-2 (write `CLAIMED <date time>` here) and continue from step 2. The M6-022 skeleton and the M6-017 lifecycle are at `427d27a`; correction C12 is `8ddc45f`.