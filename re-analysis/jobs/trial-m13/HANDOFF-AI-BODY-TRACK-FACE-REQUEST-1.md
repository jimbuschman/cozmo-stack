# AI-BODY-TRACK-FACE-REQUEST-1

## Claims and evidence

- The native five-byte TrackToFace body preserves the 32-bit face ID and head-only byte (Pack 0x00708770–0x00708796). Direct G2E 0x26 and nested RobotActionUnion 0x1E are registered by the row at 0x01020348 and converge through 0x005260BE/0x005260D4. Factory 0x0052ACC0–0x0052ACE6 passes the ID unchanged to the signed TrackFace ctor and calls mode 1 only for a set head-only byte. The explicit profile uses 2-degree tolerances, default two-axis mode (mask 5) or head-only (mask 1), no clamp or companion and FLT_MAX duration (0x0056468A–0x00564786, 0x00564B8C–0x00564BAA, 0x00565AD0–0x00565B1A).
- Direct native ingress uses Now and a game tag (0x00525A8C–0x00525AC4). Nested ingress takes its supplied tag/position and optional retry wrapper (0x005279AC–0x00527A8C). The standalone API uses the existing AI AtEnd queue with zero retries and a host tag. It captures the original ready owner, rechecks runtime identity before admission and retains the original external-reaction gate (native ActionList 0x0053D94C–0x0053D982). Cancellation addresses the original queued runner and waits for its unlink.
- The retained action uses signed exact face lookup (0x004F598E–0x004F59AE). Zero and negative IDs are accepted as keys. Missing/stale observations wait, the fresh current-root target drives the checked motor recipients, and head-only never sends a body command (0x00565DBC–0x00565F76, 0x005650E2–0x00565104, 0x005655FC–0x005656B4). The defined no-interface Init failure remains with the runner (0x00565C24–0x00565CE2).
- Matching E2G47 remap changes the stored target ID without rewriting the MovementComponent selection or last image timestamp (0x005660AC–0x0056610C). Variant 3 packs the final signed ID bits (0x00565DA4–0x00565DB4, 0x0075C6B4–0x0075C6BC, 0x0075C938–0x0075C93E). The task reads the prepared completion after the retained watcher and engine-owner barrier. The native cleanup profile is used for actual queue admission and discard, including pending cancellation; no cleanup runs for a never-admitted host runner.

## Files and tests

- Motion.cs exposes QueueAiTrackFaceAsync and AiTrackFaceActionResult and binds the retained runner to the shared queue/completion helper. Vision/NativeTrackFaceRunner.cs resolves the task after concrete and base watcher teardown.
- AiTrackFaceControlTests reaches public admission and the ticked fake robot. An injected detector supplies observations through actual camera and FaceWorld handling; both masks and motor messages are checked. The suite covers stale-frame silence, actual signed remap, missing face wait, raw/typed variant 3, pending versus active cancellation, reaction rejection, no-interface Init, and replacement during head send. The pending predecessor remains in its queue.
- Affected suite passed 223/223 across native and public motion, faces, Meet, vision and OKAO. Final rebuilt public test passed 9/9 with the independent variant-3 bytes and stale-remap assertions. The batch gate is recorded in QUEUE.md.

## MISSING

- Arbitrary original app queue position and retries, Unity/SDK ingress, and the separate RequestGameSimple and PeekABoo producers remain outside this request. The stock recognition/historical pose carrier and physical motor timing remain separate.

## Workers

Reader requested gpt-6.1-sol at medium effort. Builder and independent reviewer used the configured default request. The manager reopened the factory and constructor in the ELF and checked raw and task completion routing. Review checked signed ID handling, original-owner cancellation and the non-manual teardown profile.

## Records

- M14-003 owns the retained action setup and lifecycle. This job adds its bounded explicit ingress and completion delivery. M14-001 owns the actual camera/FaceWorld input, M8-015/M8-022 the action queue and watcher. Claude owns any separate settlement decision.
