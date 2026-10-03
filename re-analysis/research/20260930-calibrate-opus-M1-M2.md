# Independent Opus-audit calibration: M1 transport and M2 protocol

- **Date:** 2026-09-30
- **Kind:** independent extraction / audit calibration
- **Baseline:** `re-analysis/research/20260929-audit-complete.md`
- **HEAD audited:** `5a1fa5b450493cca4cda2ea756043272a40ee0ac`
- **Scope:** the 25 M1 and 15 M2 records whose status is `EXACT_SOURCE` in the current manifest.
- **Scope discipline:** research only. I did not edit code, inventories, or the manifest, and did not build or run the C#; I inspected the test bodies and their expected-value provenance.

For every record I re-read the current manifest entry and approved inventory rows, checked every cited instruction/data address in the shipped `libcozmoEngine.so`, compared the C# at the record location and all live fidelity-tagged callers, traced the complete production path (including gates, order, failure values, and wire bytes), and classified the named tests as source-derived or code-derived. Decimal-looking native floating constants were compared by IEEE-754 bits.

## M1-transport

### M1-001 — HOLDS

B1's Unity callers and B3/B4 at 0x0069DE84..0x0069DE9E/0x0069D0CE establish the two addresses and ports; `TransportConstants` and the live `RobotLink` selection use exactly those values, and the two tests assert the source values rather than reusing the implementation.

### M1-002 — HOLDS

B5/B6, R1/R3, B17 and CA3..CA7 all match the frame/UDP receive code: `43 4F 5A 03`, no CRC, the ten-byte `52 45 01` header, prefix-before-truncation ordering, error codes, continued drain, and raw bad-RE forwarding. The frame and repair tests contain independent source byte strings and failure-order expectations.

### M1-003 — HOLDS

R5..R8 and the TBB dispatch at 0x00837446/table 0x0083744A match the enum sets, single-versus-container packing, and type 1..11 handlers in the live receive walk. The tests pin the sets and dispatch effects from the binary, including connection deletion on type 3.

### M1-004 — HOLDS

`SequenceId.Next`, `Previous`, and `InRange` reproduce R14/R15 at 0x00836748..0x0083678C, including 65534 -> 1 and the wrapped inclusive range. The tests use boundary values taken from the source.

### M1-005 — HOLDS

`TransportOptions.EngineDefaults` reproduces the behavior-affecting B13/default table: binary64 33.3 = `0x4040A66666666666`, 32.3 = `0x4040266666666666`, 2.0, 5000.0 and 1.0, the packet counts and all nine boolean/int gates. `NetTimeStamp` follows 0x008355F8..0x0083564C, including integer ns/1000 before binary64 `0x3F50624DD2F1A9FC`. The live connection and transport consume these options. The source's standing `MaxAckRoundTripsToTrack=100` sizes diagnostic accumulators that this stack does not model; it does not alter the claimed send/receive path. Test coverage is incomplete, as noted below, but no source/code contradiction was found.

### M1-006 — HOLDS

R2/R20/R21/R25..R29 and CA1/CA2 match the live queue: forced reliability for oversize unreliable messages, one immediate attempt, strict pacing/resend comparisons, queue-order header ids, forward-then-backward packing, seq-0 deletion, no retry cap, and fresh ack in every header. The 33.3/32.3/2.0/1.0 operands retain the M1-005 binary64 bits. `ConnectionTests` uses source-sized packets and explicit clock boundaries rather than calling a second copy of the production selection logic.

### M1-007 — HOLDS

R4/R9..R12/R16/R17 match the receive path: no buffering, only the next reliable sequence advances, mixed out-of-range frames are still walked, earlier submessages survive a later bad type/size, and the exact error codes are counted. The repair tests construct the decisive wire frames directly.

### M1-008 — HOLDS

`PingPayload` is the exact 17-byte `{f64,u32,u32,u8}` layout from 0x00835C00..0x00835C62, and `SendPing` queues unreliable type `0x0B`, flag 1, with the second timestamp reading identified by C13. The wire tests assert the independent byte layout and same-call send behavior.

### M1-009 — HOLDS

R22/R23/CA13 and C14 match the splitter and per-connection assembler: `maxNet-12`, one-based u8 indices/count, zero-size one-part output, `<3` rejection, no reset on mismatch, no size bound, identical flag/time on every part, and clearing only after completion. The two isolation tests prove ownership by connection and destruction isolation from source-observable effects.

### M1-010 — HOLDS

R35/B14, R34, CA8/CA9 and CA18/CA19 match construction, 2 ms scheduling, receive-before-connection-update order, timeout result, and ascending `TransportAddress` iteration. The tests force the ack-before-resend boundary and address ordering from the native rows.

### M1-011 — HOLDS

`ReceivePing` follows 0x00835C7C..0x00835D30: short payload ignored, local count incremented, peer maxima monotonic, only reply packets measure `now-timeSent` (including negative), and requests are not echoed under the engine gate. The tests independently exercise request/reply and negative RTT.

### M1-012 — HOLDS

B5/B7/B8/B9/R24 establish 1420 total, 1416 UDP payload, 1406 reliable payload, and the separate 1472-byte stack buffer behavior. The live split bound is 1406 and the tests hit it from both the single-frame and multipart sides.

### M1-016 — HOLDS

The call at 0x00837814 and body 0x00835AEA..0x00835B9C match the C#: every valid known-connection frame refreshes receive time; nonzero ack repeatedly removes `pending[0]` while the ack is in the pending sequence range; no resend occurs because the configured count is zero. This includes R43's counterintuitive removal of a leading unsent seq-0 entry. The manifest's named test does not cover R43; see the test finding below.

### M1-017 — HOLDS

The live `Update` order and idle-ping gates match R32/R33 at 0x00836518..0x008365A8: empty queue, previous send required, strict `now > lastSend + 33.3`, and `now >= lastPing + 33.3`, followed by one send attempt and timeout. The boundary tests use a controlled source-oriented clock.

### M1-018 — HOLDS

R13/CA23/CA19 match creation only for type 1 (or a container whose first submessage is type 1), silent empty-container non-creation, pre-ack rejection of other unknown-source frames, and the full address key. The tests construct each decisive frame/address case directly.

### M1-019 — HOLDS

R38..R40, B21 and CA14/CA20..CA22 match all six entry points, posted FIFO disconnect-before-connect, at-most-one type-3 attempt then deletion, no-op missing-connection disconnect, start/stop socket rules, and marker callbacks. The named tests use observable frames/socket state and explicit call order, not duplicated production helpers.

### M1-020 — HOLDS

R36/B15/CA17 establish that production remains asynchronous and that sync mode is only the offline seam. The C# constructor and queue paths preserve this split; the tests separately prove direct sync execution and posted async execution.

### M1-021 — HOLDS

G1.1..G1.10 match the executor: first run one 2 ms period later, due work copied to the immediate FIFO, repeat re-armed from the observed due-pass time, no catch-up synthesis, one execution thread, no overlap, and backlog back-to-back. The fake-scheduler tests assert those source semantics independently.

### M1-023 — HOLDS

G4.1..G4.12 match registration, the fd-open gate, reset flag, next-update close/reopen/clear order, failed-close result, and Java bind/unbind emitters/retry count. The tests drive the live reset seam and assert socket operations in native order.

### M1-026 — HOLDS

B28/R42/CB29/CD13 match the app send path: connected/unfiltered only, every robot message reliable, flush/hot false, immediate transport posting, and silent failure result. The engine-layer tests observe the actual transport call and refusal return.

### M1-027 — HOLDS

B27 and CC12..CC17/CC32..CC36 match filter-before-unpack, size and consumed-length rejection, fatal-error broadcast/clear/disconnect/loop-stop order, nonfatal pass-through, and one-byte unknown-tag delivery. The tests feed wire messages through the production receive path with source-defined expected order/results.

### M1-028 — HOLDS

B29, CB7..CB21/CB31/CB34, CD15/CD16 and E1..E6 match the rebuilt live handshake: subscriptions and gates, result computation, no timers/retries, validation before manufacturing request, unknown colour `0xFF`, response-before-lab/Needs edges, and duplicate firmware/mfg-id behavior. The current tests exercise failure-with-link-retained, full success order, colour failure, and duplicate suppression from the cited rows.

### M1-030 — HOLDS

B30/CB27/CB30/CC33 match both-direction pre-validation allowlists, silent filtering, no-RIC pass-through, and validation/reset points. Tests send the exact allowed and rejected tags through the live handler.

### M1-032 — HOLDS

G2.1..G2.9 match the creation-time receive stamp, its sole refresh by a valid known-connection frame, strict `now > lastRecv + 5000.0`, async start point, and per-tick deletion path. The timeout tests use a controlled clock and assert the source boundary/effect.

### M1-035 — HOLDS

R37 and CA10..CA12/CA15 match copied payload ownership, posted timestamp, one mutex-protected FIFO executor for sends/actions/ticks, direct time 0.0 in sync mode, and queue-time stats value propagation. The tests force FIFO interleavings and mutate the caller buffer after posting, so their oracle is external to the implementation.

**M1 comparison:** the 2026-09-29 Opus audit reported 24/30 holds, with five records subsequently demoted and M1-028 then rebuilt. Against today's manifest set, this audit finds **25/25 HOLDS and no new source-fidelity defect**. It agrees with Opus on every unchanged settled record and with the later M1-028 repair, while identifying two missing source-derived regression cases (M1-005 and M1-016) that do not presently contradict the production code.

## M2-protocol

### M2-001 — HOLDS

S4/S5 and D8..D13 match `CladWriter`/`CladReader`: native little-endian fields, bool 0/1 write and nonzero read, truncated u16 count on write, non-sticky all-or-nothing reads, element-at-a-time variable arrays, and prefixless fixed arrays. Each named test supplies independent bytes/counts from the native rows.

### M2-003 — HOLDS

The rebuilt conversions match 0x00516F64..0x00516F8E and 0x005170B0..0x00517104 in binary32: 66.0 `0x42840000`, 45.0 `0x42340000`, lower clamp 32.0 `0x42000000`, upper gate 92.0 `0x42B80000`, and inverse literal `0x3F364D93`. Angle-to-height has no clamp; height-to-angle's max-first expression maps NaN to 32 before the `<92` branch. The tests pin the native bits and NaN behavior, not the C# literal.

### M2-004 — HOLDS

`SyncTime` is `{u32 timestamp,f32 0xC1A00000}` exactly as built at 0x0051524C..0x00515270 and typed by the `vcmp.f32` at 0x007A3B0A. The live post-success send uses this builder; tests assert the literal wire word and order.

### M2-005 — HOLDS

`LightState.Rgb` reproduces the bit expression at 0x00638052..0x00638070 and the equivalent body-light path: RGB 5-5-5 at bits 10/5/0 and bit 15 iff alpha is nonzero. The property test transcribes the native expression over an independent 32-bit colour word, so it is not a round-trip/copy of `Rgb`.

### M2-006 — HOLDS

The 0xEE codec and live firmware consumer match 0x007B907E and 0x0052D48C..0x0052D5B6: u16 robot id followed by a u16-count byte vector, with JSON beginning at message+4 and no wire hash fields. The test builds independent bytes and receives them through dispatch.

### M2-007 — HOLDS

The generated `AbsLocalizationUpdate` order matches 0x00512734..0x0051279E and its Pack/Size functions: timestamp first, pose frame id, parent id, x/y, and binary32 heading radians last. The live corrected-pose path constructs that type; tests pin the final float field and full source size.

### M2-008 — HOLDS

`RobotMessage.ToBytes` and typed constructors match S1/S2/S3/S7: tag first, then member, size `1+member`, including empty and inline scalar members. Tests compare literal tag/member bytes for four source shapes and the independent M2-009 sizes.

### M2-009 — HOLDS

All 42 current outbound codecs match the Appendix A Pack/Size/operator rows and the listed bool/f32 type corrections; the production builders serialize through these generated types. The size dictionary and reflection/type assertions are transcribed from the binary tables rather than calculated from the implementation.

### M2-010 — HOLDS

D1..D7 match the live dispatch: fresh `0xFF` tag, one-byte read, TBH range 0xB0..0xF5, 56 codecs, and out-of-range plus 0xCC/0xDF..0xEB consuming only the tag. Tests independently enumerate the 14 holes and 56 expected codec tags and exercise kept/dropped lengths.

### M2-011 — HOLDS

The reader and `ProcessMessages` reproduce D1/D7/D8/D10/D12: only `bytesConsumed == length` is delivered, failed reads are non-sticky, variable/fixed arrays stop at first failure, and later small fields may consume bytes after an earlier failed large read. Tests use crafted byte sequences for all four non-obvious outcomes.

### M2-012 — HOLDS

All 56 inbound codecs match Appendix B's Unpack addresses, sizes, and the listed bool/PrintTrace type corrections; production dispatch uses the same parser table. The independent size map and explicit nonzero-bool cases exercise the live parsers and reject a trailing byte.

### M2-013 — HOLDS

`FallingStopped` and the sensor event preserve `{u32 timestamp,u32 duration_ms,f32 impactIntensity}` from 0x007B0EB6 and 0x00535040..0x00535186, including the last word's binary32 interpretation. The test supplies independent wire bits and checks the production event fields.

### M2-014 — HOLDS

The 0xB8 codec and docking consumer match 0x007C1AC6, 0x00533780..0x005338A2 and table 0x01034984: u32, nonzero bool, signed i8 docking result, then BlockStatus 0/1/2. Tests use `0xFF` to prove signedness and independently assert enum values.

### M2-015 — HOLDS

All cited builders preserve caller-supplied float words and byte order verbatim at 0x0063F174/0x00640700..0x00640940; there is no conversion or clamping in the live builder path. Tests use distinct binary32 values for every field, while the separately owned M4 test supplies app defaults.

### M2-016 — HOLDS

The generated 0xF2 codec matches 0x007C60B4 and all cited consumers: u32/u32/i32/u8/i8/u8/u8/i16/u16-count bytes. The test uses high-bit values in every signedness-sensitive slot and asserts the parsed CLR types/values from independent wire bytes.

**M2 comparison:** the 2026-09-29 Opus audit reported 14/15 holds and correctly isolated M2-003. That record was subsequently rebuilt with `0x3F364D93` and the native NaN gate; this audit finds the current set **15/15 HOLDS, with no new defect**, so the Opus result and the later repair are reproduced.

## Test-quality findings (non-status-changing)

1. **M1-005 lacks a source-derived regression for its main claim.** The manifest names only the two `NetTimeStamp` tests. Neither pins the 14 Init-written tunables (nor the two behavior-affecting standing defaults) against B13/CA; the third standing default, `MaxAckRoundTripsToTrack=100`, is diagnostic capacity absent with the unmodelled accumulator. The behavior-affecting production values nevertheless match the source today.
2. **M1-016 lacks a regression for R43.** Its sole named test, `AContainerEndingInAPartialSubMessageHeaderIsASizeOverrunAfterTheHeader`, reaches ack processing incidentally but does not put an unsent seq-0 entry ahead of acked reliable entries. The production `RemoveAt(0)` loop does reproduce R43 today.

No named test in M2 was circular for the behavior it claims. The large layout tests use independently transcribed native size/type tables; they do invoke the production codecs, but do not derive their expected sizes or field types from those codecs.

## Calibration conclusion

This sample does not reveal a missed behavioral/source-fidelity defect in the Opus audit. Its two contemporaneous findings that are relevant to today's settled set were correctly separated: M1-028 needed a wider handshake path and M2-003 needed exact binary32/NaN behavior; both current implementations now match their augmented source rows. The remaining calibration lesson is narrower: an otherwise correct audit can still leave a settled record with no decisive source-derived regression, as M1-005 and M1-016 demonstrate.
