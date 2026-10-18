# M1M2-HW, 2026-10-18 17:05: manager judgement

**Verdict: M1-transport and M2-protocol ACCEPTED** (manager, 2026-10-18; accepted commit 4f184775, the clean main the
run was built from). Robot 172.31.1.1, firmware 2457 (DEVELOPMENT build, git-rev 1f716924…, accepted under M1-040),
serial 0x0240e889, body hardware 4. Command: `m1m2-hw 172.31.1.1 --obb …\re-analysis\obb`. No exceptions, no warnings.

The tool's own summary said `REVIEW_REQUIRED_SOME_NOT_AS_SOURCE` because of two tool bugs, fixed after this run
(`scripts/m1m2-hw-draft.md`, "Corrections after the first run"). Judged from the raw files, every check is as source.

| Check | Tool verdict | Judgement from the raw files |
| --- | --- | --- |
| STARTUP | OBSERVED_STARTUP_AS_SOURCE | as source: 0→1, 1→2, 2→3; 997 distinct names (no `_PROCEDURAL_`), then EndOfMessage(0), then 2→3; progress non-decreasing to 1.0; delivered readiness |
| CONNECT | OBSERVED_CONNECT_AS_SOURCE | as source: Success, time sync, first full state, ready to stream; one BlockPoolData after the response (M1-042) |
| LATENCY | OBSERVED_LATENCY_DIFFERS (tool bug) | **as source.** 18 reports in 12 s, median period 660.004 ms (659.2–660.7) against 11 × 60 ms; wifi/extSend/send 0; unity, sdk and image avg/min/max (0, +inf, −inf); image current 0; recvQueue finite (e.g. 27.8/1.5/57.2 ms); 0 while paused, 9 after resume. The tool compared serialized JSON, where `+inf` is escaped as `+inf` |
| DISCONNECT | OBSERVED_DISCONNECT_REQUEST_SENT | as source (plus the recorded motor-stop decision note) |
| NEEDS_READ | OBSERVED_NEEDS_DIFFERS (tool bug) | **as source.** Each connection reads 0x194000 twice, exactly in M3-033's order: 0x182000, 0x195000, 0x184000, 0x183000 (robot construction), the eight backup-manager reads 0x180000, 0x181000, 0x182000, 0x183000, 0x184000, 0x194000, 0x195000, 0x196000, then CameraCalib 0x80000001 (Length 1), Lab 0x196000 and Needs 0x194000 in the mfgId lambda, then ready to stream. Identical on the second connection. No WRITE, ERASE or WIPE of any tag. The tool expected one read |
| WIRE | OBSERVED_LINK | M1-033 observation (below) |
| CONNECT_ASYNC | OBSERVED_CONNECT_ASYNC_AS_SOURCE | as source: Success through the real `ConnectAsync`; readiness delivered before the response; full startup seen through BeforeStart; BlockPoolData |

## What the robot showed

- **The needs record:** 0x194000 holds no record (2 × 1024 bytes of 0xFF: InvalidHeader → NV_NOT_FOUND, as for every
  non-factory tag on this robot). This is native case 1: the original would write a fresh needs record to the robot (and
  the device file); the stack, by the B-M1-10 gate, writes nothing and logs the M15-030/M15-014 MISSING once. That is the
  only robot-traffic difference, recorded on M15-014/M15-030. The saved `needs/nv-0x194000.bin` is therefore not a v5
  record; the hand-encoded v5 fixture (B-M1-10 check A4) stays unchecked against a real blob.
- **M1-033 (robot-side transport):** outbound 1208 frames (672 type-7 packed frames with up to 7 reliable sub-messages,
  max 134 bytes; 468 pings; reliable seq 1..1347; 125 resends; no frame errors); inbound 1829 type-9 frames (max 1397
  bytes against the 1406 bound, 626 over 1000 bytes, up to 11 sub-messages; 23 resends). The DisconnectRequest packed in
  a type-7 frame was honoured. **Not exercised:** sequence wraparound (about 65534 reliable messages) and outbound frames
  near the size bound; recorded on M1-033.
- **Robot messages watched:** 13 × 0xB0 Trace and 1 × 0xEC WifiFlashID (decoded and logged, M2-020). No 0xBA/0xBB ramp,
  0xC3 poke or log-only B1/B2/B7/BC/BD messages arrived; the tap prompt was answered blank, so whether firmware 2457
  sends RobotPoked for a tap is still unknown (M2-024's handlers are built from source either way). No unknown tags.

## Acceptance against "Done for a layer" (PROJECT_STATE.md)

- every live-path M1/M2 record is EXACT_SOURCE, a confirmed policy or EQUIVALENT, and M1-033 (HARDWARE_ONLY) is settled
  by robot runs (2026-10-09 and this one), with what was not exercised recorded;
- the production path the layers serve ran that way on the operator's robot (above);
- the operator ran the layer's hardware script (this bundle);
- `fidelity.py --check` passes with both subsystems ACCEPTED.
