# Request: three M5 reads (M5-055, M5-056, M5-058)

- **Answer file:** `re-analysis/research/20261012-m5-teardown-reads.md`.
- **Read first:**
  - the current manifest records M5-055, M5-056 and M5-058;
  - `research/20261012-m5-settling-2.md`;
  - `research/20261012-m5-x9-extraction.md` sections 4a and 4b.
- **Rules:**
  - research only;
  - **one coverage table per item, first**;
  - no fidelity claims;
  - nothing leaves this machine.
- **Why:** these three RECOVERABLE_GAPs block M5's acceptance. Each is a bounded read in
  `resources/lib/armeabi-v7a/libcozmoEngine.so`.

## 1. M5-055: the ~CozmoEngine member destruction order

- Read `CozmoEngine::~CozmoEngine` and list, in order, every member it destroys, with the destructor address and the
  member offset. Include the base and member subobjects the compiler destroys after the body.
- For each destructor that matters to M5, say when it runs relative to the others:
  - the RobotManager, and the Robots it deletes;
  - the RobotDataLoader (its +0x278 abort store at 0x0051F0D4..0x0051F0E6, and the join of the load thread);
  - the UiMessageHandler, and the signal objects that the streamer's and DrivingAnimationHandler's subscriptions
    point into.
- **The question to answer:** when a Robot's streamer and its handler release their subscriptions during engine
  shutdown, is the UiMessageHandler still alive, so that the weak lock succeeds and the unlink happens? Or is it gone?

## 2. M5-056: the E2G subscription container and its release

- `UiMessageHandler::Subscribe(EngineToGameTag)` at 0x006626CC creates the container through 0x0066411C. Read
  0x0066411C:
  - what it allocates, and the layout of the container and its ProtoHandle;
  - how the handle is linked into the signal.
- Read the container's `on_zero_shared` and its destructor: exactly what the last release does (the unlink, the weak
  lock, the order). Compare it with the G2E path (0x00662750 → 0x006648CC), which the earlier extraction read, and say
  where they differ.

## 3. M5-058: FaceAnimationKeyFrame +0x28 writers

- Find every store to offset +0x28 of a FaceAnimationKeyFrame object, beyond the constructor 0x00564290 (which zeroes
  it).
- The known sites that load the vtable (GOT 0x0103E9D8) are listed in the settling-2 file, under D7. Check each one's
  callers, and any code that takes a FaceAnimationKeyFrame pointer or reference and writes `[rX,#0x28]`.
- Methods: a vtable-pointer scan, a scan for `strb`/`str` at `#0x28` in functions that receive one of these objects,
  and the SetFaceAction, CannedAnimationContainer and AppendTrack paths.
- **The answer is one of two:** "never written non-zero natively", with the coverage that proves it; or the writer(s),
  with their addresses and conditions.

## Report

- Per item: the coverage table, the answer, and what stays UNKNOWN.
