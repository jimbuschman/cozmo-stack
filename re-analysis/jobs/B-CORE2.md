# Job B-CORE2: fix what the Opus verification of B-CORE found

**Agent:** opencode (DeepSeek), as cozmo-manager, window 3. **Type:** build. The format is B-CORE's
(`re-analysis/jobs/B-CORE.md`): `CHECKLIST.md` applies, and **this job never settles a record** (section 6). Each
record ends IMPLEMENTATION_GAP, with `unresolved` starting "built, awaiting strong verification:".

## Your rows

The rows are the Opus verification reports, which cite every address:
- `re-analysis/research/20260930-B-CORE-verify-1-2.md`
- `re-analysis/research/20260930-B-CORE-verify-3-4.md`
- `re-analysis/research/20260930-B-CORE-verify-5-6.md`

Each record's `unresolved` also starts with its verdict, "Opus verification of B-CORE". Build only what those texts
cite. Anything they mark NEEDS EXTRACTION is **out of scope**: Codex is extracting it
(`research/requests/20260930-bcore-extractions.md`).

## Before you start

1. Read `re-analysis/jobs/README.md`, `AGENTS.md`, `.opencode/agent/cozmo-manager.md` and `re-analysis/jobs/CHECKLIST.md`.
   Give the checklist to `@cozmo-implementer` and `@cozmo-verifier` with every batch.
2. **Claim the job:** write `status/B-CORE2.md` with `CLAIMED <date time>`, then commit and push it alone.

## The batches

1. **The on-idle tick** (M3-026, M3-030, M1-041, M3-033).
   - On-idle callbacks run only from `NVStorageComponent::Update`'s state-0 path (0x006456EC) and from
     `AddOneShotOnIdleCallback` (0x00645C32). A completion only calls `SetState(0)`.
   - So ready-to-stream and streaming open on the next Update, after the streamer check (0x0051410C..0x0051411E runs
     before 0x0051416A).
   - Fix `NvStorage.cs:591` and `CozmoEngine.cs:1168/1179`, and the test at `EngineAppLayerTests.cs:1223-1225`.
   - `AddOneShotOnIdleCallback` runs its callback at once when the component is idle (0x00645C32).
2. **The connection reads** (M3-033, M3-022).
   - **One calibration read.** Remove the parallel 0x80000001 read (`VisionSystem.ReadCalibrationAsync`,
     `VisionSystem.cs:346-359`). The tools (FreeplayTool.cs:110, ManipTool.cs:52, Reactions.cs:163, VisionTool.cs:137,
     CoreChecks.cs:303/364) wait for the connection's own read to complete instead.
   - **The Needs read.** Queue it synchronously where the engine does, from the mfgId handling (0x0052E3B2 →
     0x006943F8), not at FreeplayStack creation (`FreeplayStack.cs:155`).
   - **The log order.** Fix the calibration log order and content (0x0065ACE8, then 0x0065AD5A..0x0065AD9C;
     `Camera.cs:994-1000`).
3. **The NV sink, bound and logs** (M3-030, M3-025, M3-031, M3-034, M3-035).
   - **The sink.** Reassembly writes into the sink as chunks arrive, so it keeps its blobs after a timeout.
   - **The bound.** Stop at 1000 chunks with LoopBoundOverflow (0x00643770..0x00643798).
   - **The logs.** Add the missing ones: TagIsTooSmall and FactoryTagNotFound (0x00644274, 0x006442EC); NumRetriesExceeded
     (0x00645D34); ReadOpFailed on every negative result (0x006434E4); ReadFaceEnrollDataNotFound/Fail (0x0065A8F0,
     0x0065A916).
   - **The face album.** Clear `ConnectionFaceAlbumResult` on RemoveRobot.
   - **The M3-035 test** goes through RemoveRobot → ResetDevices.
4. **Head, lift and action timeout** (M4-001, M4-003, M4-016, M4-020).
   - **The head clip** uses `Anki::operator<`/`>` with the 1e-5 IsNear (0x0084CC90..0x0084CCD0, bits 0x3727C5AC).
   - **RescaleRadians** keeps the `vcvt.s32.f32`/`vcvt.f32.s32` round trip (0x0084C91E..0x0084C922), saturating like
     ARM.
   - **The teardown.** It stops the track (0x00541146/0x0054116C), gated on
     `AreAllTracksLockedBy(track, to_string(id))` (0x00541138/0x0054115E), **before** the unlock
     (0x0054120C..0x0054122A). The lock owner is `to_string(+0x60 id)`.
   - **The timeout** fails with 0x03000018 (0x00540E80). It is timed on the engine's clock from the first
     UpdateInternal (0x00540D4A..0x00540D64) and tested before Init and CheckIfDone (0x00540D9E..0x00540DAA).
   - **The citation.** Fix M4-003's 0x005408EC citation: that address is UnlockTracks from the ctor and Reset.
   - **The failed-send log** is Robot::SendMessage's own warning (0x005134F4). Fix `EngineAppLayerTests.cs:849`.
5. **Width and smaller items** (M1-024, M1-041, M2-002, M3-018, M3-010).
   - **The Needs tick** is float: the float at +0x10, and `NeedsManager::Update(float)` stores now at +0x3AC before
     the pause test, which ApplyDecayAllNeeds reads. Fix the circular test at `EngineAppLayerTests.cs:370`.
   - **The SyncTime-ack deadline** is compared in f32 (0x00513C02..0x00513C14).
   - **PlaceObjectOnGroundAction::CheckIfDone** has a status-bit 0x4 gate (0x005549B0..0x005549D0).
   - **The IsColor(0) log text** is `"VERIFY(false): NoneImageEncoding"` from `sVerifyFailedReturnFalse`
     (0x004F2130). It is logged where IsColor is called in VisionSystem::Update (0x006B4B7C).
   - **The M3-010 test** goes through PopFrame.

**Out of scope, waiting on Codex:**
- the jsoncpp reader (M1-029);
- +0xE8 (M3-027);
- StartCamera (M3-023);
- the face-album gate record;
- M3-037;
- QueueNow (M4-003's queue part);
- M1-044 and M1-045;
- the codec (M3-001, M3-018).

## Gates

Before each commit:
- `@cozmo-verifier` gives PASS on the diff;
- `python re-analysis/tools/fidelity.py --check` passes;
- the full suite passes once.

Push each batch. Keep a progress log in `status/B-CORE2.md`. Finish with `DONE <date time>`, the commits, and every
MISSING.
