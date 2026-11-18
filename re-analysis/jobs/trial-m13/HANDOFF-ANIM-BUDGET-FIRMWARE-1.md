# ANIM-BUDGET-FIRMWARE-1

READY FOR CLAUDE on codex-m13-manager-2; clean Windows main gate remains blocked.
SCOPE.md A: authored animation and animating personality behaviors. Operator
approved the smaller firmware-2457 budget and confirmed the available capture
copies as the corpus. No new robot acceptance is claimed.

## Change

Default CozmoEngineOptions.AnimationStreamBudgetBytes = **7370**; **8192** selects
the native constant. Both consoles accept --animation-budget-bytes, 1..30000,
validated before connecting. Offline copies preserve the selected value. Existing
public stream/scheduler constructors, the hardware-options factory, and the
five-field animation-result constructor/deconstruction remain available; PlaybackTag
is an additive init property.

Measurement: research/20261118-anim-budget-corpus.md; 28 copies, 26 raw captures,
12 BufferFull traces; two older captures lack frames. Default = largest clean
observed value below the smallest pre-error peak (8117 below 8125), minus native
AudioSample+Start 747. Clean runs also reached 8190. No universal capacity or
allocator overhead is inferred.

| Complete path | Primary evidence / implementation |
| --- | --- |
| Wire Size, lifetime prefix | 0x007ABB90, 0x007BCDAA; emu_anim_buffer.py executes sizes. Line 27390 matches 479685/5407 streamed, 472279/5395 played. |
| Budget input | Native ADD.W r5,r0,#8192 at 0x0057C706. Live construction supplies the host constant; FIFO, clamps, signed arithmetic, 14-frame allowance, counters, failed-send and direct-End rules stay native. |
| Setting to constructors | Options -> CozmoAnimations -> AnimationScheduler -> StreamSendBuffer. Standalone/native-conformance default stays 8192; live default is host policy. |
| Firmware diagnostic | PrintTrace unpack 0x007D4BEA; shipped AnkiLogStringTables.json nameTable[379] BufferFull. Args remain uninterpreted. |
| Clip to CA/CB | InitStream identity 0x0057B674; synchronous MessageInternal after native delivery, before action-list completion. Selection is not playback; reused tags retain their started instance. |
| Successful End | Scheduler SendGateObservation plus MessageHandler successful 0x9A observation covers scheduler and direct API sends. Failed sends never satisfy it. |
| CB, result and host report | Native 0x0054541C..0x005454F4 / 0x005441C0 keeps SUCCESS. Host monitor flags an un-aborted started tag ending before successful End; prior host abort is not mislabelled. |
| Result and narration | Read-only PlaybackTag matches owned early ends by call time/tag/name. Raw BufferFull during the call is included, with unknown identity null. isError/truncatedByRobot preserve engineResult=0/SUCCESS. Narration says cut off by robot buffer. |

PrintTrace has no animation tag. When selected and started stream disagree,
association is null; when they agree tag/name are observed playback context,
not a decoded firmware tag. Issue publication is queued after CA/CB; its internal
model is immediately queryable to avoid racing the action result. Monitor
callbacks change no robot state, native result, packet or counter.

## Checks and boundaries

Native 8192: critical allowance 786/2, admits packet, after 41/1, totals 480430/5408.
--byte-budget 7370 substitutes only the literal-add operation, leaving native
branches/clamps/send code: allowance 0/2, held packet, totals unchanged 479685/5407.
Matching early CB still gives native SUCCESS.

The actual C# counter replay covers 229000..249500 ms with captured prefix:
pre-error peaks **8151 / 7339**. Successful captured sends are fixed offers, and
F1 including the original flush is supplied feedback. This is a byte-gate replay,
not a firmware prediction or proof that tag 13 plays fully. The original
ANIM-BUFFER-OVERFLOW-1 physical result is not relabelled.

Validation: **484/484** original affected checks passed before the complete full
run. Full Windows suite, immutable reviewed snapshot, all 15 private cases enabled:
**8716 passed, 8 failed, 8724 total**, 42.1428 minutes. Ten firmware-policy tests
and thirteen MCP client cases all pass inside that full run.

Six failures are the live operator MCP's UDP 47817 conflicts. One is the same
private named-face 247000..255800 greeting timeout seen before this job. The eighth
was SourceLogMigrationTests' native log golden still constructing a default robot:
it expected -808 (8192-9000), correctly got -1630 (7370-9000). The test now explicitly
selects and checks **both** budgets, preserving its native golden and log-after-lock
assertions. The expanded follow-up, including every class naming the changed
budget methods/properties plus sender, authored, freeplay, bridge, MCP, narration,
manifest and host-options checks, passes **567/567**.

No production source or reviewed input changed after the complete full run; the
only subsequent C# change is that native/policy test fixture. Production assemblies
after the follow-up are byte-identical to the full-run snapshot. No post-correction
clean full pass is claimed; main still requires a clean full gate. The live MCP
process remains running.

Both console validation modes exited 0 without a connection. Source hashes were
checked, corpus remeasurement exactly matches the numeric fixture, both native
8192/7370 oracle modes pass, fidelity check passes (764 records), diff check passes.
Ignored logs: .scratch/anim-firmware-focused4.log,
.scratch/m13-firmware-reviewed-full-98fcada6.log,
.scratch/anim-firmware-final-followup.log,
.scratch/anim-firmware-counter-replay.log,
.scratch/anim-firmware-native-8192.log, .scratch/anim-firmware-native-7370.log.

Fidelity: new **M1-063 COMPATIBILITY_POLICY** explicitly operator-authorized beside
M1-040. M1 inventory/snapshot extended only for that policy; all prior frozen
records, review state and accepted commit stay unchanged. No broad reapproval or
source settlement. M3-012 unresolved states the policy boundary; its native
algorithm remains source-backed. PROJECT_STATE.md/SCOPE.md are unchanged.

Remaining uncertainty: firmware allocation/flush stays MISSING in the oracle.
The empirical guard needs operator robot recheck. Two older bundles without
frames remain unknown. No hardware bundle is committed.
