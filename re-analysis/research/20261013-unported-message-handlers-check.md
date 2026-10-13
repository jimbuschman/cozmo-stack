# Opus check of R-HANDLERS (2026-10-13)

- **Checked:** `research/20261013-unported-message-handlers.md` (Codex), against libcozmoEngine.so. Read-only
  cozmo-extractor. Helper output is in `.scratch/rh/`.
- **Adopted:** by the manager on 2026-10-13. This file is the checked source for job B-M3M4-18. Use it, not the Codex
  report.
- **Result:** items 1 and 2 hold as written, and so do item 3's handler bodies. Four facts about what is observable
  while a behaviour isn't running were missing (C1–C5 below).

## 1. Impl::HandleFWVersionInfo (D2, body 0x00533348; M2-019)

1. Copy the 44 payload bytes to Impl+0x0C..+0x37 (0x0053335E..0x00533376), on every message.
2. **Engine-to-robot hash:** memcmp(Impl+0x18, 0x00C51870, 16). The engine's hash is
   `D5 35 42 A1 7B B8 97 69 D8 09 4E CE 5A 96 4A 9E`. On a mismatch:
   - hex text: ConvertMessageBufferToString(buf, 16, 1, true, 0x100) gives " XX" per byte, uppercase, 48 characters
     (0x007FB7CC..0x007FB83A);
   - sWarningF, key `RobotFirmware.VersionMismatch` (0x00533750), format 0x00BE8F1F "Engine to Robot CLAD version hash
     mismatch. Robot's EngineToRobot hash = %s. Engine's EngineToRobot hash = %s." (received, then engine), with empty
     $data;
   - Impl+0x38 = 1. It's sticky: a later match never clears it.
3. **Robot-to-engine hash:** memcmp(Impl+0x28, 0x00C51880, 16). The engine's hash is
   `AB 12 BA BA 15 72 95 40 B4 1D 23 16 7F 24 59 A2`. On a mismatch, the same key, with format 0x00BE8F8E "Robot to
   Engine CLAD version hash mismatch. Robot's RobotToEngine hash = %s. Engine's RobotToEngine hash = %s.", and Impl+0x39
   = 1.
4. If either flag is set, build EngineRobotCLADVersionMismatch (0x005376AC..0x00537756):
   - +0 is the E→R flag and +1 the R→E flag;
   - +4 is the engine's E→R text, +0x10 the engine's R→E, +0x1C the robot's E→R, +0x28 the robot's R→E;
   - a direction that matched this time has empty strings.

   Broadcast it as MessageEngineToGame tag 0x2A through Robot::Broadcast (0x005335B2..0x005335C4). It is delivered
   through the stack's E2G API.
5. Nothing is sent to the robot, and nothing is rejected. The constructor clears +0x38/+0x39 (0x00532952).

Other readers of Impl+0x0C..+0x39 are UNKNOWN (inlined readers were not searched).

## 2. Impl::HandleDockingStatus (D3, body 0x00536F1C; M2-019)

- **The status:** the payload byte at +4, **sign-extended** (`ldrsb`), then EnumToString: 0 → "STATUS_BACKING_UP", 1 →
  "STATUS_DOING_HANNS_MANEUVER", anything else → NULL (0x007C115C..0x007C1172).
- **The event:** sEventF, key `robot.docking.status` (0x00536F94), format `%s` (0x00536FAC), empty $data, no channel.
  It fires on **every** message, with no state written and nothing sent.
- **A NULL %s** is rendered by the phone's libc vsnprintf, which doesn't ship. Bionic prints "(null)". The stack does
  the same, as an EQUIVALENT_IMPLEMENTATION of a system library.

## 3a. FactoryTest (constructor 0x005D0438; M7-027)

**The subscriptions:** D6, F3, B8, EE and D2 (0x005D0628..0x005D068A). There is no active check: each handler runs
whether or not the behaviour is running. The instance is built twice, so each handler runs twice, BehaviorManager's
instance first.

- **D6:** sChanneledInfoF, channel "Unnamed", key `BehaviorFactoryTest.HandleFactoryTestParameter.Recvd` (0x005D08E8),
  format `StationID: %d` (0x005D0920), with payload word 0. Then +0x168 = that word. The constructor default is −1.
- **F3:** +0x33E = 1 if the type is a light cube (1/2/3) or a charger (13). No log.
- **B8:** +0x1B3 = byte[+4] & (byte[+6] == 2). No log.
- **D2:** `bx lr`.
- **EE:**
  - Json::Reader::parse of the payload string, with collectComments = 1 (0x005D09AC..0x005D09E2).
  - **On success:**
    - +0x350 = root["version"].asString(), and +0x344 = root["build"].asString();
    - if version ≠ "F1.5.1" (the global at 0x0105B18C) or build ≠ "FACTORY", then +0x340 = 1.

    No log, and no send.
  - **Throws** (M1-034 departure):
    - a root that isn't null and isn't an object makes operator[] throw (0x008EAE64);
    - an object or array value makes asString throw (0x008E4A04).
  - **Parse false:** EndTest(0x46). See below.
- **EndTest** (0x005D23EE..0x005D2618):
  - **A result is already set:** sWarningF `BehaviorFactoryTest.EndTest.TestAlreadyComplete` (0x005D2688), "Existing
    result %s (new result %s)" (0x005D26B8). It returns.
  - **The first time:**
    1. +0x1C9 = 0x46.
    2. Info "Unnamed" `BehaviorFactoryTest.EndTest.PreWriteResult` (0x005D26E0) `%s`.
    3. The timestamp entry at data+0x44 = now.
    4. **The Robot's ActionList Cancel(−1)** (0x005D24B8), which cancels every action.
    5. +0x16F = (prev ≠ cur), and +0x170 = prev & (result ≠ 1).
    6. Info `BehaviorFactoryTest.EndTest.PassFailStatus` (0x00BF36EB) "prevPassed: %d, currPassed: %d" (0x00BF3716).
    7. The result fields: +0x1D8 = 0; +0x228 = result; +0x1D0/+0x1D4 = time(NULL); +0x224 = +0x168; and 0x48 bytes
       copied to +0x1DC.
    8. QueueWriteToRobot(tag 0x80000010): a push onto the in-memory list at +0x330, with +0x338++. There is no NV call.
    9. +0x33C = 0.
    10. SendQueuedWrites only if cur && !prev. That is impossible for 0x46.
    11. WipeFactory if +0x170, which is unreachable here (C2).
    12. SetCurrState(0x0F): +0x1AC = seconds + 20 if the state changed; +0x128 = 15; timestamp entry 15.
- **C1:** IsRunnableInternal returns (+0x1C9 == 0) (0x005D0C0C..0x005D0C18), so after a parse failure the instance
  stays unrunnable.
- **UpdateInternal is compiled out:** sErrorF `BehaviorFactoryTest.UpdateInternal.NotEnabled` / "Factory test is not
  enabled" (0x005D27A4/0x005D27D4), returning 2.

## 3b. DockingTestSimple (constructor 0x005CAFB0; M7-027)

- **The gate:** context+0x20 (the RobotManager) and its +0x60 (the MessageHandler) are both stored unconditionally
  (0x004EA7A8..0x004EA7AC, 0x0052E4F2..0x0052E4FC). So D3 is always subscribed.
- **D3** (0x005CB35C..0x005CB382): the status byte, unsigned. 0 → ++(+0x25C); 1 → +0x26C = 1. No log.
- **C3:** InitInternal zeroes +0x258, +0x25C, +0x260, +0x268 and +0x26C at the start of every run
  (0x005CB498..0x005CB4AA). So whatever is counted while the behaviour isn't running is never observed.

## 3c. LiftLoadTest (constructor 0x005D3F10; M7-027)

- **The subscription:** DA through IBehavior::SubscribeToTags, ungated (0x005D401A..0x005D4038).
- **The dispatch** (0x005BF2F8..0x005BF320): +0x60, then +0x6C if +0xA1 (running), else +0x78. LiftLoadTest's slots:
  - +0x60 = `bx lr`;
  - +0x6C = HandleWhileRunning (0x005D4FE5);
  - +0x78 = `bx lr`.
- **While running:**
  - +0x224 = 1, +0x225 = hasLoad, +0x220 counts loads, +0x21C counts all;
  - sChanneledDebugF "Unnamed" `BehaviorLiftLoadTest.HandleLiftLoad.HasLoad` (0x00BF398E), "%d / %d" (0x005D5118).
- **While not running:** nothing.

## Constructor side effects (new RECOVERABLE_GAP)

- **DockingTestSimple and LiftLoadTest** each build a RollingFileLogger (pathToResource(scope 2, "dockingTest" /
  "liftLoadTest"), 0x1400000). Its constructor calls FileUtils::CreateDirectory (0x0080DFCC), twice per behaviour.
- **FactoryTest** builds a FactoryTestLogger (0x00671658).

These are unread:
- 0x0080DEFC..0x0080E050, and pathToResource for scope 2;
- 0x005D3F48..0x005D401A;
- 0x00671658.

## What is observable while the behaviours aren't running

| Handler | Observable |
|---|---|
| FT D6 | two Info logs per message (one per instance); +0x168 stored |
| FT F3, B8, D2 | nothing |
| FT EE, parse OK | nothing |
| FT EE, parse fails | per instance: two Infos, **ActionList Cancel(−1)**, an in-memory entry queued, state 15, and +0x1C9 latched (unrunnable). A later failure gives the TestAlreadyComplete warning instead |
| FT EE, throws | the original aborts (M1-034) |
| DT D3, LL DA | nothing |
| Impl D2, D3 | always active (sections 1 and 2) |
