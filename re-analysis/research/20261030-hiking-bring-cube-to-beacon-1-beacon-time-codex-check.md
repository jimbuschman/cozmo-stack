# Beacon failure time: narrow independent check (2026-10-30)

Requested by the HIKING-BRING-CUBE-TO-BEACON-1 manager. This checks only the conflicting field descriptions, not the whole behavior. `known.py` was run for 0x0059C314, 0x005DF148 and BaseStationTimer::GetCurrentTimeInSeconds before inspection. The inspected `resources/lib/armeabi-v7a/libcozmoEngine.so` SHA-256 is `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`.

## Primary instructions

`AIBeacon::FailedToFindLocation` 0x0059C314 saves its receiver in r4; 0x0059C318 calls the PLT for `BaseStationTimer::getInstance()`, 0x0059C31C calls the PLT for `BaseStationTimer::GetCurrentTimeInSeconds() const`, and 0x0059C320 stores r0 at `[r4,#0x10]`. A general-register store by itself does not distinguish integer from float bits.

The Bring runnable path obtains the active beacon at 0x005DF13E, checks null at 0x005DF142..144, then `vldr s16,[r0,#0x10]` at 0x005DF148. At 0x005DF14C..166 it compares/negates and compares the value with f32 instructions; 0x005DF170..174 calls the same timer, 0x005DF178..188 adds the configured cooldown and another float to s16, and moves the returned bits to s2. The f32 comparison at 0x005DF18C and branch at 0x005DF194 govern the cooldown. Thus the existing timestamp's interpretation is materially float: treating its bits as a numerical integer would change whether this behavior is runnable.

## Current approved contracts and code

- Current manifest M13-006, **EXACT_SOURCE**, title “The whiteboard keeps a list of beacons and the active one is the first”, evidence describes “a float radius at +0xC, an int at +0x10”; it also says `FailedToFindLocationInBeacon 0x0056C3F0 forwards to AIBeacon::FailedToFindLocation`. The approved M13 inventory row N25 repeats “int at +0x10”. Its approval snapshot dated 2026-10-28 matches the current inventory SHA-256 `af3e094c69caf38b0558f2225a9b9f8b41a74cb03e178b8a547583b75a18a99d`. The field-type annotation is contradicted by the above consumer; the list/first-active claim is not assessed here.
- Current manifest and approved M15-023, **IMPLEMENTATION_GAP**, title “The NoFreePoses branch: FailedToFindLocationInBeacon stamp, the log, the 'HikingNoLocationAtBeacon' event and the IsRunnable cooldown”, expressly say “stamps [beacon+0x10] (an f32 time) with BaseStationTimer::GetCurrentTimeInSeconds()” and cite the 0x005DF148 f32 cooldown read. M15 inventory correction A4 line 846 explicitly says “`AIBeacon+0x10` is an f32 timestamp (M13-006 calls it an int)”. The approval snapshot dated 2026-10-29 matches current inventory SHA-256 `a88c97d42bc0b23296e759bb499a4f6c0ecf814af287b9995684ecb68d597c41`.
- Current `AIWhiteboard.cs:16-29` already stores `FailedToFindLocationTimeSec` as `float`, tagged M15-023; `:393-395` stamps it from the clock. This observation is limited to the field and writer, not a verification of the full production path.

## Judgment

The approved M15-023 contract already expressly owns the f32 timestamp and Bring cooldown, and the approved inventory acknowledges the contradictory M13 wording. The builder may use that existing M15-023 contract for this field; this narrow discrepancy does not require a new behavioral contract or a fresh operator approval before that use. M13-006's frozen evidence and N25 remain inaccurate and must be referred to the manager/Claude for an official correction through the approval process; no official field is silently changed here. This does not authorize any other unverified behavior or settle M15-023. The full reader report and independent source review are still pending.
