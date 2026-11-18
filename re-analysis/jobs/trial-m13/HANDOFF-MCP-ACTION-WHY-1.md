# MCP-ACTION-WHY-1 — READY FOR CLAUDE

Failed command results now include `why`: the unchanged engine result/name,
per-command diagnostic snapshot, and a plain host hint. This is reporting only;
no controller, native result, thresholds, geometry, motion, or robot traffic changed.
The MCP fallback also explains local argument/readiness errors without inventing
an engine result. Successful actions retain their existing response shape.

Path: MCP tools/call -> CozmoBridge.ExecuteAsync -> existing command/QueueAi API ->
synchronous app activity handoff and action admission -> ActionList/native runner ->
existing completion -> FromAction -> why enrichment -> MCP text + structuredContent
and the existing isError decision. The capture binds the requested appended runner
by reference under the engine owner, after any Freeplay activity handoff; queued
calls cannot inherit another runner's pose/path/firmware observations. Subscriptions
end on command return or bridge disposal. Snapshots and hints do not run extra pose
queries or readiness/acceptance checks.

| Captured input | Primary comparison / existing producer | Reporting |
| --- | --- | --- |
| Final pre-action offset/threshold/yaw | IDockAction 0x00550FD2..0x00551196: positive thresholds, offsets <= thresholds, then abs(yaw) strictly below angle + slack; NativeDriveToObjectRunner final check 0x00559AA0 and existing CozmoStack.DriveToObject.PoseCheck | Exact raw diagnostic and parsed invariant values; excess on X/Y, or yaw vs limit. Yaw remains untested outside the box. X/Y are robot-main-parent axes, not a fabricated charger-relative sideways correction. |
| Backup pitch | BackupOntoChargerAction 0x0054E7A8..0x0054E7EE: contacts first; pitch < BE860A92 -> 0x0400000A, before straight-drive completion | Mount's retained trace supplies the actual compared pitch and cutoff, even after retirement; never a later sensor sample. Retry 0x04000006 remains the final result where the engine retries. |
| Docking signal | PickAndPlaceResult unpack 0x007C1AC6, handler 0x00533780 | Raw timestamp, success, signed docking result and block status. Explicitly untagged observation during the command, not proof of its failure cause or a guessed recovery instruction. |
| Path planner | Existing NativePathComponent.SetStatus/Update log observations; planner CheckPlanningStatus getter and drive status | Bounded raw transition/planner logs and numeric status at observation. No guessed threshold, obstacle clearance or remedy. |

Hints only interpret recorded comparisons for matching terminal results. Refusal,
cancellation, unknown/other failures keep the existing detail; absent diagnostics
are explicit (`checkAvailable: false`). Ignored/retried/advanced sequential checks are marked superseded. An earlier
check cannot turn cancellation
into a position-error explanation. Robot buffer cutoff keeps native SUCCESS and
uses the existing playbackIssues evidence with host error reporting.

Tests use the in-process serialized JSON-RPC client and committed numeric
mount_charger_llm_native.json. The first/third geometry rows enter at the recorded
post-route final-comparison boundary. The pitch row enters after alignment/turn:
test reflection installs the real private BackupRunner/Mount stage, then runs its
actual pitch check, native retry-window decision, real forward retry drive with
firmware path-start/completion inputs, and normal parent retirement. It does not
inject a terminal action result, invent a docking reply, or claim camera/localisation
or physical-mount replay. Expected numbers remain from the shipped ARM oracle.

All three replies expose: Y excess ~12.22 mm; yaw -10.8 vs 7.5 degrees; pitch -15.9
vs -15.0 degrees. Additional tests cover queued-command isolation, signed untagged
dock observations, unchanged success, cancellation, roll continuing after a real
ignored out-of-position approach, and refusal for pickup/roll/stack/go-to-pose. Focused tests: 91/91. Full suite: **8735 passed / 6 failed / 8741 total**, 33.0795 minutes.
The six failures are the known TransportRepairTests that need UDP 47817; the live
MCP server still owns it (PID 107304). No clean full pass is claimed; these failures
still block main. All 15 available private replay cases were enabled.

Fidelity check: 764 records passes. No manifest/inventory/approval/SCOPE/PROJECT_STATE
change, status upgrade, hardware run, hardware bundle or generated bundle committed.
Remaining: Claude source spot-read and operator live MCP check. Full suite log:
.scratch/m13-mcp-why-final-full-c6c3067d.log; immutable test snapshot bin/Debug/
m13-mcp-why-final-full-c6c3067d, all 15 available private replay cases enabled.

An earlier partial full run was stopped after the ignored-check review finding;
it is not claimed as a complete run. No operator MCP process was stopped.

Cozmo.Robot snapshot comparison: all 51,682 method/field IL/constant entries
match the preceding tested build. File hashes differ and the assembly build
revision metadata names different HEADs; byte identity is not claimed.
