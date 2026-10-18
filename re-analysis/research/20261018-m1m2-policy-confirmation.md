# M1+M2 policy confirmation (Opus, read-only), 2026-10-18, HEAD 29eb1064

- **Checked:** 19 policy/equivalent records against AGENTS.md (2026-10-17 "What we reproduce", ADP-1, app boundary,
  approved departures, exclusion rules). `fidelity.py --check` passed (697). No tests run (text confirmation).
- **Result:** 8 CONFIRMED, 10 NEEDS TEXT, 1 NEEDS OPERATOR → **manager decision instead (below)**: M1-042's missing
  GetBlockPool is an app input to supply, not an exclusion, so no operator question is needed. None is NOT A POLICY.

| record | verdict | basis / what to append |
| --- | --- | --- |
| M1-013 | CONFIRMED | (e) host timer; periods and scheduling are M1-024/M1-010/M1-021; jitter only |
| M1-014 | CONFIRMED | (e) host threads; single FIFO dispatch keeps arrival order; topology/priority M1-047 |
| M1-022 | NEEDS TEXT | M1-048 is now EXACT_SOURCE (U1–U7). Policy covers only Poll(0)+ReceiveFrom for MSG_DONTWAIT, SocketError for errno, the handle for the fd, a throwing Close as a failed close, no getaddrinfo |
| M1-034 | NEEDS TEXT | basis since 2026-10-17: approved departure M1-034 + standing rule (2026-10-13; hangs 2026-10-16), not "Exact, always". The M1-029 JSON throw terminates (HandleFirmwareVersion 0x0052D470..0x0052DC64 has only the terminate pad 0x0052DA44 and cleanup resume 0x0052DB5E), so it falls under the standing rule, not "destination unknown"; fix the `CozmoEngine.cs`:1427-1429 comment |
| M1-036 | CONFIRMED | (a) approved departure; crash upload is app machinery |
| M1-037 | NEEDS TEXT | M1-049 is now COMPATIBILITY_POLICY; basis: app boundary (Android network callbacks), operator D5 |
| M1-038 | CONFIRMED | (a) approved departure + standing rule (freed pointer) |
| M1-039 | NEEDS TEXT | `effect` keeps "the original never sees it", which `authority` calls contradicted. State the basis (lead, authority 6, unchecked: Linux `__udp4_lib_err` drops ICMP errors on an unconnected socket without IP_RECVERR, and B11 sets none, 0x00839D18) or the disclosed difference (if the phone surfaced it: native warns and ends that update's drain; the stack continues silently) |
| M1-040 | CONFIRMED | (a) approved departure |
| M1-042 | NEEDS OPERATOR → manager: build | Unity A3 sends GetBlockPool (0xE7) on the first Success before the response callback (`RobotEngineManager.cs`:223-232, 373-378; `BlockPoolTracker.cs`:86-95); BlockFilter::HandleGameEvents 0x00619E7C → 0x00619F00 → `b.w 0x8CCA4C` → PLT 0x4B7AF4 SendBlockPoolData (0x0061AE54..: info log, E2G BlockPoolDataMessage, no robot send). The stack never sends it (only caller `CubeConnections.cs`:544 UpdateDiscovering), so `CozmoEngine.BlockPoolData` misses the connect-time delivery. Also stale: the FTUE gate is fixed to not-first-time (`CozmoRobot.cs`:431); volume only on Success (Unity: every response while CurrentRobot exists) |
| M1-043 | NEEDS TEXT | the stack is the native errno==EAGAIN branch (0x0083AA98 → 0x0083AAC4 beq → end of drain); no host errno read. Not reproduced: the ReadFailed warning (log only) and a stale-107 reopen (phone thread state). Fix `ReliableTransport.cs`:1326,1388 comments (still say HARDWARE_ONLY) |
| M1-049 | NEEDS TEXT | not built by decision: the host address-change event (M1-037) replaces the N1/N2 Java gates; N3's handle-agnostic signal maps to NetworkBindSignal. Provenance "need comparison" stale |
| M1-051 | NEEDS TEXT | Enter/OnConnectionSuccess writers unreachable; ExitMode (0x0065E2A4..) is built and reachable via public `CozmoEngine.ExitSdkMode` (`SdkExit.cs`:27) under M1-025; SdkWasConnected never set, reset branch unreachable. Lead: whether a public SDK-exit API fits "SDK not supported" (M1-025) |
| M1-052 | CONFIRMED | (c) SdkConnections never filled; callbacks never run |
| M1-057 | CONFIRMED | (c) no app sender (unity/ has only generated classes); SDK not supported |
| M1-058 | CONFIRMED | (b) named host inputs; native gates exact; missing root fails clearly |
| M1-059 | CONFIRMED | (c) manager exclusion 2026-10-17 with reason; readiness gate kept |
| M2-017 | CONFIRMED | (a) approved departure |
| M2-021 | NEEDS TEXT | wait point = state 3 delivered to the host (IsReadyDelivered), same completion tick as the ratio-1 EngineLoadingDataStatus (0x004ED60E before 0x004ED612); the cited test `ConnectAsyncWithoutEngineOptionsReachesCancellationInsteadOfConstructionFailure` was replaced by `ConnectAsyncWithoutResourcesFailsStartupBeforeCancellation`; the test field starts with "; " |

Disassembly checked: 0x0083AA80..0x0083AB40; 0x00619E7C..0x0061A1EC (tbh: 0xE5 → Enable, 0xE7 → SendBlockPoolData);
0x0061AE54..0x0061B014; 0x0052D470..0x0052DC64; veneers 0x8CCA4C → 0x4B7AF4, 0x8CCA3C → 0x4B7B18. Every cited test
name exists except the one on M2-021.
