# Final M1 corrections: call and side-effect accounting

2026-10-09. Bounded CODEX-BUILDER rule 10 accounting for the final Opus corrections; no settlement. Primary ELF SHA256 `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`. Reopened instructions, logger bodies, import identities and literals are in `20261009-M1-final-correction-native.txt`. Earlier manager-adopted rows remain the authority for unchanged calls. This is not a new inventory approval for the newly extracted M3 IMU implementation.

## M1-050

| Call / gate | Current counterpart |
| --- | --- |
| RemoveRobot virtual +0x30 at 0x0052F2AC; relocation 0x0102FE4C resolves UiMessageHandler::OnRobotDisconnected 0x0066331A | `NotifyExternalRobotDisconnected`, same call position, default no-op; public report event removed. Internal nullable observation is test-only. |
| SDK +0xE2 / +0xE1 gates; ExitMode calls 0x0066332C/0x00663340 -> 0x0065E2A4, UpdateIsSdkCommunicationEnabled 0x00663332 -> 0x00663292, tail 0x0066334A -> veneer 0x008CD3AC | Unreachable with SDK unsupported, M1-051/-052. No game message/report is generated. |

## M1-053

| Call / side effect | Current counterpart |
| --- | --- |
| Robot::Update 0x0052F6E4, HasReceivedRobotState 0x0052F6EA, GetRobotState 0x0052F706 | Existing tick update, first-full-state gate and `PublishRobotState` projection. |
| MessageEngineToGame constructor 0x0052F70E, virtual +0x1C Broadcast 0x0052F716, ClearCurrent 0x0052F71A | Immutable outgoing snapshot, `RobotStatePublished` API fanout, managed transient message lifetime. |
| 0x0052F720..0x0052F774: static u16 0x0105101C increments/wraps, compares >=11, info call 0x0052F74A, then counter zero | Process-static `RobotStatePublicationCounter`; per-engine private counter seam only in fixtures. Channel `Unnamed`, tag `RobotManager.UpdateAllRobots`, format `Not sending robot %d state (none available).` |
| UnknownOriginID GOT 0x0103E978 -> uint0 at 0x00C97B20; warning 0x00518122 | After root-pose read, `warning: Robot.GetRobotState.BadOriginID`; native format at 0x00BE3F00 is empty. Projection continues. |
| Root pose / ToPoseStruct3d / GetTransform, quaternion angle, Radians construction/rescale | Existing owning localization supplier and checked `EngineRobotState` projection/angle helpers; P2a-P2m rows and reopened full target transcript. |
| Head, carrying/top, tracking/localized IDs, Vision timestamp and game/status getters | Existing owning-component publication delegates. Supplier gaps remain M4/M10/M11/M12/M13; none is settled here. |
| sinf / atan2f | Undefined ELF imports: ordinary non-shipped phone-libm boundary. Host MathF equivalent arithmetic is explicitly assumed; engine width, operation order and operand selection remain checked exact obligations. |
| delete temporary log vectors at 0x0052F76C / 0x00518144, message temporaries, exception cleanup and stack canary | Empty native diagnostic vectors/transient message storage map to managed storage. Managed exception containment is M1-034. There is no retained file/resource in these correction ranges. |

## M1-047

| Call / side effect | Current counterpart |
| --- | --- |
| 0x008334DA sched_get_priority_min, 0x008334E2 max, 0x00833528 pthread_setschedparam | Existing host scheduler interface, M1-014. Checked f32 interpolation/request and topology unchanged. |
| 0x0083353C errno / 0x00833542 strerror | Host `GetErrorText`, M1-014 representation. |
| 0x00833556 sErrorF -> 0x0080D13C | Error tag `SetThreadPriority.Failed`, format `Error: %s (res=%d) setting thread policy:priority %d:%d`; host error text/result followed by engine policy/priority. |
| 0x0083358C _errG store, 0x00833590 debug gate/call | Shared `EngineErrorState.StoreAndMaybeBreak`. Helper PLT 0x004A4114 -> 0x0080DAB4 is exactly `bx lr`; no trap is invented. |
| 0x008335B2 sChanneledInfoF -> 0x0080D42C | Result0: channel `Unnamed`, tag `SetThreadPriority.Success`, format `Changed thread policy:priority from %d:%d to %d:%d)`, arguments -1,-1,policy,priority. EPERM1/default2 skip diagnostic. |
| 0x00833578/0x008335D4 delete transient vectors | Managed transient diagnostic storage; no file/stream or counter call. |

## M1-048

| Call / side effect | Current counterpart |
| --- | --- |
| CloseSocket virtual close 0x008395DA | Host Socket.Close / CloseHook, M1-022; descriptor captured before close. |
| Info 0x008395FC -> 0x0080D42C | Channel `Network`, tag `UDPTransport.CloseSocket.Success`, format `Socket %d closed successfully`; emitted only for an existing descriptor and successful close. |
| errno/strerror 0x00839634/3A/40, error 0x00839656 | Fresh operation error representation, M1-022. Tag `UDPTransport.CloseSocket.Failed`, format `Unable to close socket %d (res = %d), errno = %d '%s'`; failed host close maps to result -1. |
| _errG 0x0083968C, gate/call 0x00839690 | Log then shared process error flag, then shipped no-op debug helper. Native fd -1 / stored port47817 result bookkeeping remains unchanged. |
| Receive virtual syscall 0x0083AA94, address constructor 0x0083AAA4, HandleReceivedMessage 0x0083AAB6 | Existing host nonblocking adapter, endpoint representation and `ProcessDatagramLocked`; M1-022 and checked receive/CLAD owners. |
| errno reads 0x0083AABE/AAD4/AADA/AB1C, strerror 0x0083AAE0, ReadFailed warning 0x0083AAF6 | Tag `UDPTransport.ReadFailed`, format `recvmsg(_socketId = %d _port = %d) returned %zd, errno = %d '%s'`, pre-close fd/port and result -1. Only errno/descriptor arguments are host representations under M1-022; WouldBlock silent, Windows ConnectionReset M1-039. Stale errno after <=0 result remains M1-043 uncertainty. |
| ENOTCONN close 0x0083AB28, open 0x0083AB34 | Existing `CloseSocketLocked` then `OpenSocketLocked` only on close success; stored port from close. |
| Fresh errno/strerror 0x0083AB44/4A/50 and error 0x0083AB64 | Tag `UDPTransport.ReadFailed.ReopenSocketFailed`, format `Error: Reopening closed socket failed. errno = %d '%s'`; uses error after failed open/cleanup, not the prior receive error. |
| Fresh errno/strerror 0x0083AB9C/ABA2/ABA8 and error 0x0083ABB6 | Tag `UDPTransport.ReadFailed.CloseSocketFailed`, format `Error: Closing unconnected socket failed. errno = %d '%s'`; uses failed close error. |
| Shared _errG store 0x0083ABEA, gate 0x0083ABEC / helper 0x0083ABF0 | Each failure diagnostic queues its log, error-flag store and no-op helper in that order. _errBreakOnError at0x01051E78 initially1; _errG at0x0105DD34 is shared with existing higher-layer error paths. |
| Delete transient vectors 0x00839620/78, 0x0083AB18/86/ABD8; unwind/stack canary | Managed transient log storage, M1-034 exception containment; no file resource. |
| U1/U2 OpenSocket first close, interface/resolver/string/freeaddrinfo, socket/setsockopt/bind calls | Existing host socket adapter and M1-022 resolver mapping (getaddrinfo has no separate host step). Resolver-only diagnostics have no emitting host operation in that approved mapping; no additional exact resolver claim is made. Open/broadcast/bind failures now preserve exact source tags/formats and store the common error flag; host normalization applies only to arguments. The four source literal pairs are 0x00C289FE/0x00C28A1C, 0x00C28A7E/0x00C28A9E, 0x00C28BEC/0x00C28C0E, 0x00C28C87/0x00C28CAA. EADDRINUSE warning retains the unbound descriptor and success result. |
| U5 send accounting, send syscall, short-send error 0x0083A3A4, non-IP error 0x0083A608, send-failure warning 0x0083A5C0, NetTime and +0x88 store | Existing `RawSendLocked`/`SendFailedLocked` adapters, counters/error6/rate-limit branches. Short-send and non-IP error paths now store _errG before the no-op gate; warning does not set it. Exact formats are `Bytes %zd != bufferSize %u`, `Error: UDP can only send to IP addresses!` with tag `UDPTransport.SendData.NonIpAddress`, and `sendto '%s' returned %zd, errno = %d '%s' (%u sends failed), now = %.1f`. Only endpoint/error/syscall-result argument representations remain M1-022; absent descriptor maps to NotSocket. The warning snapshots error6 count and time. Literals 0x0083A42C/0x0083A450, 0x0083A6D4/0x0083A6F8, 0x00C28D72 were independently reopened before building. |
| U7 Update reset close/open and read-loop calls | Existing reset consumer and stored-port/drain order; M1-023 remains flag writer. |

Native error helpers all resolve to the reopened logger bodies; syscall/errno/stdio implementations not shipped in the APK remain external boundaries. The error flag has one backing owner; `DesiredFaceDistortionComponent.ErrorFlagSet` remains an alias, preserving its public behavior.

## M1-046 and M1-029

No new unchecked IMU logger was built. M1-046's retained-vector retirement remains the checked U1-U8 implementation. The omitted filebuf close at0x00532A64 and its reachable open/final-close descendants are explicitly owned by new RECOVERABLE_GAP M3-041, with the complete current record and I1-I11 rows in `20261009-M1-046-imu-reachability.md`. This is a real file-resource gap and must be checked/built before complete Messaging retirement can be claimed.

M1-029's new change only replaces the old non-finite missing-source diagnostic with a labeled unreachable guard. The manager checked Reader token dispatch/error and overflow rejection; allocation/length and escaping exceptions retain the manager's J4/J5/M1-034 dispositions. No non-finite mapping is invented. The complete existing Reader/converter production histories, checked build rows, native instructions and emulator corpus results are now explicitly included in its verification packet; independent strong review remains pending.
