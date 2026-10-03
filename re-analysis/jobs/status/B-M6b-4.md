CLAIMED Claude (Sonnet 5.5) 2026-09-29 11:18

## Progress log (Sonnet 5.5 in Claude Code; no record is settled by this job, CHECKLIST section 6 as of 2026-09-30)

| batch | commit | records touched | MISSING / open |
| --- | --- | --- | --- |
| 4a: voice-to-bus connection creation, deferred voice list, AddSrc flow, device list (C24-C27) | ee3187a (pushed) | M6-025 (IMPLEMENTATION_GAP, unresolved names residuals) | AddSrc pre-step bodies `0x9BCA68`, `0xA0228C`, the sibling `0xA55AAC..0xA55D18`; FX registration question; frame-count writer `[0x1052440]` |
| 4b: playback-limit walker, removal/Term undo, list ops, below byte, voice-pass stop (C28-C29) | e124f1a (pushed as part of origin 6bfaec0) | M6-026 (IMPLEMENTATION_GAP, "built, awaiting strong verification:") | seams: `0xA03618`, `0xA1EC54`, `0xA054D8`, post-mix `0xA55CC4/0xA5495C`, pending-source `0xA55D04/0xA55A84/0xA54A30/0xA56478`, `0x9BC66C/0x9BE898/0xA11F98`, pbi+0x64 producers `0x9C54E8/0x9C39DC`, game-object +0x64, tail `0xA023D4/0xA01918/0x9E85C8`, `0x9BEB30` [sp+0x1C]==0 with below; queued cleanup: Word64 doc wording, WwisePlayParams doc citations, lin-pool test silent skip. Not wired into production (CozmoRobot does not construct the bridge/limiter/voice pass yet). |

**PAUSED 2026-09-30 (operator request).** Batch 4c (0x9BEB30 context init, 0x9CD340 RIFF parse, fade-in, container PlayInternal bodies) was in progress and was stopped mid-build. The unverified partial work is on branch `b-m6b-4c-wip` (not on main, not pushed): `WwiseRiffParser.cs` (new), `emu/emu_riff.py` (new), partial edits to `WwiseEventRuntime.cs` and `WwisePlayingInstance.cs`. It has not been built, tested, verified or fidelity-checked, so it must not be merged as is. To resume: check out that branch, build, and hand the diff back to `cozmo-implementer` with the batch 4c scope in B-M6b-4.md step 4; nothing from it is a source claim yet.

## C30 step 1 (2026-10-02): C# defects fixed

Commit: see git log ("B-M6b-4 C30 step 1"). No record settled; no status or .approved.json changed. Verifier: FAIL once (`[line+0x30]` is the line, not the bus; AddSrc untested), fixed, re-verified PASS. fidelity --check clean; full suite 2692/2692.

Built: StartStream raw int with ([source+0xC]+0x1DC, +0x1E0) and the 0xA56650 latch (`WwiseVoiceSourceStart.StartA56650`); NotReadyCheck per 0xA544BC..0xA545DC (±0.5f vcvt truncation, 0xA54580 path, udf on null [voice+8]); linker-owned Init 0xA4F0EC and 0x9EA23C table (seam only `0xA22A3C`); Reserve result 2; VoicePass required collaborators. Oracles: `re-analysis/tools/emu/emu_{notready,line_init,device_table,descriptor_reserve}.py`.

Inventory text to correct (queued; the manager edits the inventory): C24.3 (Init order, unread steps, FX-holder gate); C24.5 (growth by 1, initial capacity 0, table unchanged on growth failure); C30.1(b) (0xA0428C args are mgr, playingId, node id); C30.1(d) (0xA56650 args come from [source+0xC], not [voice+8]).

MISSING / visible: 0xA4F0EC steps with no row (0x9C8108, 0x9C817C, +0x1B8 bits, +0x58, +0x5C=1/frames, 0xA19ECC, 0xA68A44, 0xA68B28) as seam `LineInitUnreadSteps`; FX-holder gate (0xA68B38, [line+0x28]!=4); 0xA7A894 with 0-byte request; device entry [E+0x20]/[E+0x2C]; failure result of source classes 0xAB0448/0xAB1550 vt+0x28; writer of pbi+0x1DC/+0x1E0 (0xA1EC54, unread; unset throws); other 0xA56650 callers 0xA52C18, 0xA54948, 0xA554F8, 0xA55AC4; 0xA44948 prelude ([[G]+4]!=0 -> 0xA44BD0 clock store to [G+0x20]); 0x9D3CC0 takes r0=u16[0x1052440]; WwiseMixBus.Buffer is mono float[frames]; shared AllocationFails hook on linker. The live path stops at the unread 0xA1EC54 until it is read.
