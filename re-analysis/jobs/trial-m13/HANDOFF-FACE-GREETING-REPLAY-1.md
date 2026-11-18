# FACE-GREETING-REPLAY-1 — READY FOR CLAUDE

The face pose, motor and observation checks complete. Native speech preparation
took 31.339 s, exceeding the unchanged parent 30 s (vtable 0x01020528 ->
0x0052B0C2; check 0x00540D94..0x00540DA2). SetAction 0x0054B934 does not reset it.

Replaced ArmMachine's managed callback on every guest basic block with a
synchronized weak-owner watchdog at the same host deadline. Nested deadlines,
instruction budgets, fault latches, import checks, finalization and disposal
remain guarded. Engine clocks/timeout, speech, images, RNG and replay assertions
are unchanged. Original long-name PCM remains 128736 samples, SHA256
A44DF02968C651A40C5E0815D904A23EF3A9778E1E7088C01A2C0FFE3861E76C.

History: callback introduced a439506d; this private test first exists at
5d640ba3 and already fails there on this host. No reproduced good private-case
revision exists for a valid later-regression bisect. Controlled callback removal
at HEAD makes the complete replay pass with identical PCM; no first-bad result
is invented. Evidence/path table: research/20261118-face-greeting-replay.md.

Focused 37/37 pass. Full suite with all 15 private cases: **8725 passed / 6 failed /
8731 total**, 40.3739 minutes. All three private face cases pass, including the
late named greeting (37 s). The six failures are only the known live MCP UDP
47817 conflict; MCP was left running. **No clean full pass is claimed.**
Log .scratch/m13-face-greeting-full-a3625bba.log; immutable snapshot
bin/Debug/m13-face-greeting-full-a3625bba. Fidelity 764 and diff checks pass.

No hardware run/bundles or manifest/inventory/approval/SCOPE/PROJECT_STATE edits.
M14-006 remains BLOCKED_EXTERNAL; no scope-finished or provenance upgrade.
Claude: spot-read the native timer/SetAction path and watchdog lifecycle/fault
handling before merge; physical acceptance remains the operator's work.
