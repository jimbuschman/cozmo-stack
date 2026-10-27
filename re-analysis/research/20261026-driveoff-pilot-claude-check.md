# Drive-off pilot, first handoff: Claude check (2026-10-26)

Branch `codex-m13-manager-2` (code f03c7d23 + c16d4711; test fix 5c25c9a7), merged to main at a7f07e8b. Codex independent checks and its
full gate (6361/6361, 0 skipped) are the build evidence; this check spot-checks claims against the binary.

**PASS, merged.**

- **DrivingAnimationHandler::UpdateCurrDrivingAnimations 0x004F0D2C..0x004F0D74:** with an empty override stack it
  calls MoodManager::GetSimpleMood on Robot+0x440's owner; 1 (Sad) selects +0x2C, else NeedsState::AreNeedsMet == 1
  selects +0x38, else +0x20; a non-empty stack takes its top. Matches the handoff claim (Sad 94/90/8C, met 96/92/8E).
- **MoodManager::GetSimpleMood 0x0067C084..0x0067C0BF:** Happy (+0x18) > 0.33 gives 0; Confident (+0x78) < -0.29 or
  Happy < -0.33 gives 1, applied after, so Sad wins; otherwise Default. `Mood.cs` GetSimpleMood matches.

**Settled: M7-026** (IMPLEMENTATION_GAP -> EXACT_SOURCE). GetSimpleMood checked above; the +0x130 clock binding
(0x0058A95C reads, Update 0x0067B67E writes) rests on the Codex build check.

**Not settled: M8-027.** The handoff says the eight ProtoHandle release functions prove memory-only teardown. Each one
(0x004EF138, 0x0051D8F8, 0x00591550, 0x0064E618, 0x00664708, 0x00664EB8, 0x006A3EFC, 0x0083BED0) calls an unlink
helper (for example 0x006634F4) that destroys the stored callable, so what teardown does depends on each subscriber
callable's destructor. Those are not enumerated, which is exactly what the record's unresolved still says. To settle:
list the callable types held at teardown and show each destructor touches none of AGENTS.md 1-4.


**Second handoff (6d8d4006) proposals, checked 2026-10-26 night (REVIEW.md N3). Settled:**

- **M13-017:** ctor 0x005C0980 (96 + extra), IsRunnableInternal 0x005C0B10 (+0x34A), Init 0x005C0B18 (lock,
  severe-needs 3 push, +0x355 wait), TransitionToDrivingForward 0x005C0BB8 (platform gate), Update 0x005C0DA8, Stop
  0x005C0D90 and the completion 0x005C0E9C (result 0 only: objective 4, then the Robot+0x440 emotion event) match
  `ChargerBehaviors.cs`.
- **M13-040:** SetOnCharger 0x00511990: family 4 / type 13 located find, else create plus connected-ID copy or new ID,
  AddRobotRelativeObservation on every true input, then the +0x338 rising edge and the store. Matches `Sensors.cs`
  SetOnCharger and `BlockWorld.ObserveChargerFromContacts`.
- **M7-023:** Robot teardown nulls +0x250 (0x00511150) before deleting the mood (0x005111B6) and nulls +0x440 after;
  MoodManager dtor 0x0067AE14 gates on +0x158/+0x12C/ActionList, then +0x14C, +0x140, +0x134, +0x120 and the nine
  0x20 entries from 0x100 down. `MoodLifetime.cs` keeps that order.
- **M11-060:** producer 0x006B229C (warn if pending, store +0xA8/+0xA4, set +0xA0); consumer 0x006B50C8..0x006B50D8
  (copy to +0x98/+0x9C, clear). Matches `Camera.cs` / `CameraQuality.cs`.

**Not settled: M12-043** (its own midpoint and container joins still report MISSING) and **M8-027** (above).

**Test fix 5c25c9a7:** the M15-022 lock tests ran the action's 30 s timeout on the transport clock that pumping
advances; under full-suite load the pump spent it. They now use a fixed action clock (they grade locks, not time),
with stronger assertions and a new regression test. Test-only.
