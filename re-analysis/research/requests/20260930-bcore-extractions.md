# Research request: the extractions B-CORE's verification left open

- **Date:** 2026-09-30
- **Requested by:** manager (Claude)
- **Kind:** extraction
- **Answer file:** `re-analysis/research/20260930-bcore-extractions.md`

## Why

The Opus verification of B-CORE (`research/20260930-B-CORE-verify-{1-2,3-4,5-6}.md`) found steps no row covers. A
DeepSeek job (B-CORE2) is fixing everything that is cited. These items are what it can't build until someone reads
them.

For each item, trace the engine's production path in `resources/lib/armeabi-v7a/libcozmoEngine.so` and give rows:
- the step;
- the address;
- what it does;
- every gate, order and failure result;
- floats as bit patterns.

Mark UNKNOWN where the binary doesn't settle it. Quote each record's current manifest text before you contradict it.

## Items

1. **M3-027, the READ command's Data (+0xE8).**
   - The Data comes from +0xE8 (0x00645386; resend 0x00645CE2; re-request 0x006438AC).
   - Find every writer of +0xE8, its initial value, and what a READ sent after a WRITE carries.
2. **M3-023, the camera start.**
   - What does the engine send when the app starts the camera stream (EnableColorImages, ImageRequest, their fields and
     order)?
   - The stack's `CozmoRobot.StartCamera` (`CozmoRobot.cs:529-534`) has no row. Name the engine and Unity paths that
     correspond to it.
3. **M3-033 / M3-034, the FaceAlbum-read gate.**
   - VisionSystem::Init (0x6B0658) must return 0 before the face-album reads are queued. Trace the gate and what Init
     returns on each path, as rows for a record of its own.
   - Also list the four missing NV sinks (progression, inventory, backup, lab): what each does with its read result.
4. **M3-037, the image buffer.**
   - AddChunk resets end = begin (0x004F1DA2) and calls reserve(0x38400) (0x004F1DAA).
   - Does SetNextImage (0x00652B04, called at 0x00535B9A) copy or move the Robot+0x394 buffer on the way to the decode?
   - So which bytes past a short payload are determined, and which are never written?
5. **M4-003, the action queue.**
   - The app queues SetHeadAngle and SetLiftHeight with `QueueActionPosition.NOW` (`unity/.../Robot.cs:1435, :1628`).
   - Give the complete `ActionQueue::QueueNow` (0x0053E24C) semantics: how the running action is deleted
     (0x0053E2D6 / 0x0053E35E: stop, then unlock), the result it reports, and what messages go out.
   - Also the other positions used by the app (NEXT, AT_END, IN_PARALLEL).
6. **M1-029, the jsoncpp reader,** as rows for an exact port of the reader the firmware JSON goes through:
   - `decodeNumber` (0x008E1CF2..): int, uint and real typing, `-0`, overflow;
   - the full token grammar where it differs from strict JSON (literals, trailing commas, `{"":1,}` at 0x008E0FBA,
     leading zeros, control characters, invalid UTF-8);
   - the depth limit (0x008E09AC, counting the root);
   - where the engine catches a throw from parse (possibly std::terminate, 0x00677A4E).
7. **M1-044 and M1-045, as build rows.**
   - M1-044 is Robot::~Robot's teardown (0x005110D4 onwards, called from 0x0052F2F6). Give its order and what each
     destroyed component does that is visible on the wire or to the game.
   - M1-045 is RobotIdleTimeoutComponent::CreateGoToSleepAnimSequence (0x0052CEA2): the complete action tree, its
     parameters, and when it is triggered.

## Out of scope

Any code, inventory or manifest edit. No branches, commits or pushes.
