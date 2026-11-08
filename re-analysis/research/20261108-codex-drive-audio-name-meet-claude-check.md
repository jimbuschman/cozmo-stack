# Codex public drive, shared audio, stock name and Meet batch: Claude check (manager, 2026-11-08)

**Verdict: all four PASS and merge (branch tip 64915828). MEET-COZMO-ADMISSION-1 is partial by design. No record
settled, none added.**

Codex gate: `scripts/test-full.sh` on f36ae996, 7918/7918, no skips. The first attempt failed two stale fixtures
(the factory table without the now-built AcknowledgeFace, and an "unbuilt Meet" assertion); both were corrected
without dropping their address checks.

## AI-BODY-DRIVE-PUBLIC-1: PASS

- **Public API:** `CozmoMotion.QueueAiDriveStraightAsync(distanceMm, speedMmps, shouldPlayAnimation = true, token)`
  returns `AiDriveActionResult`. It refuses non-finite input, uses the existing readiness checks, the original
  manipulation owner, AtEnd and zero retries, and keeps the engine's cancellation behaviour (manager decision, previous
  batch). The XML comment says so.
- **Spot-checked:** the admission gate at 0x0053D94C..0x0053D970 reads the robot's reaction-disabled byte (+0x2C7) and
  tests the tag range against 1,000,000 (0xF4240). The new tag allocator wraps within 1..1,000,000.

## VOICE-SHARED-ROBOT-AUDIO-1: PASS

- **Spot-checked:** the shared abort at 0x0059678E..0x005967B8 makes its Wwise stop calls on the stored object and
  sets state 4, as built.
- **Evidence:** two shipped AcknowledgeFaceNamed clips, a stop and a replay through one owner match a separate
  original-engine run (four SHA-256 hashes in the handoff), reproduced independently by Codex's manager. No banks or
  libraries committed.

## VOICE-CANNED-NAME-DELIVERY-1: PASS

- **Built:** the stock AcknowledgeFace reaction turns to a recognised face and says the name through Acapela, inside
  the full authored clip, with the shared native audio. SayName is reported before the face is marked turned, then
  objective 0x1A. The one-time NamedFaceInitialGreeting branch is reachable.
- **Not exercised:** OKAO recognition-to-name in this whole sequence (the test supplies a named detector result), and
  the physical robot.

## MEET-COZMO-ADMISSION-1: PASS (partial)

- **Spot-checked:** the chooser at 0x0060A44A..0x0060A4F6. A score at or below zero skips the entry. A running
  behaviour gets an f32 bonus. A non-running one draws from the RNG, adds in f64 and rounds to f32, as built.
- **Holds visibly** when Interact wins, because its Init (0x005C2154) is MISSING. There is no runner-up substitution.

## Next jobs (manager order)

From Codex's BLOCKED.md: AI-BODY-TURN-CONTROL-1, then MEET-COZMO-INTERACT-1, then MEET-COZMO-NAMED-ENROLLMENT-1.

## Progress

22 records settled (unchanged), 2 of 178 finished (unchanged), 761 records (unchanged).
