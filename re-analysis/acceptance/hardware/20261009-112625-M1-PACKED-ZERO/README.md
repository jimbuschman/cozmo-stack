# DRAFT — M1 packed-frame and phone zero-datagram acceptance

Manager review required before the operator runs this script. It has not been run on hardware. It uses Python's standard library and never commits, pushes, changes the manifest or labels either record settled.

The script tests **M1-033**, HARDWARE_ONLY, “Robot-side transport behaviour”, specifically its unresolved acceptance of packed types 7/8/9; and collects evidence for **M1-043**, HARDWARE_ONLY, “Whether a 0-byte UDP read warns”, specifically its unresolved phone-thread errno. Current evidence for M1-043 is `0083AA98 -> 0083AAC4..0083AB24`: zero read takes the error path, warns unless errno=11, reopens if errno=107, and ends the drain. The script does not substitute a Windows receive for this path. Records are validated against and copied from the current manifest on every run.

## Physical setup and prerequisites

For packed trials, connect the operator's computer to the robot Wi-Fi, close other robot clients including the official app, place the robot stationary on a clear level surface, and keep its power on. The target is the physical robot UDP port5551. This harness sends connection requests, transport pings, short read-only IMURequest messages (tag4A, u32 LengthMs=1), and disconnects; no SyncTime, movement, lift, head, sound or firmware-update commands. Manager should check that this payload is valid for the target firmware before authorizing the run. No automatic hardware launch is part of this draft commit.

For zero-datagram trials, the **official shipped engine must run on the Android phone**. Establish its actual bound UDP endpoint and arrange inbound reachability from the operator's computer. Use a separate phone session after the packed test closes its client. Do not target the robot: M1-043 concerns the phone engine's read. A foreign-source zero datagram can exercise the pre-dispatch recvmsg path, but the native trace must establish that it reached the correct socket. If the phone Wi-Fi isolates peers, record INCONCLUSIVE; do not infer receipt from sendto success.

The manager must arrange an independent Android native trace collector before the zero injection. The collector is an explicit remaining prerequisite of this draft, not a provided or invented instrument. Its trace must identify app version, engine hash/load base, OS/device, socket fd/local endpoint, receive-thread id, monotonic/UTC clock alignment and each recvmsg's return value, flags and errno **in that thread**. Sample errno without changing it, both before and immediately after the zero return; a hook's helper calls must preserve errno. Trace the warning branch, close/reopen branch and end-of-drain, including the preceding receive calls. Normal logcat by itself cannot prove no warning or the stale errno. Archive collector source/version hash and raw trace alongside the bundle before manager review.

Zero trials must include natural production state and, if an instrument can safely and reproducibly establish them, separate runs preceded by EAGAIN (11), another observed errno, and ENOTCONN (107). Artificially seeding errno verifies the branch only; it does not establish the natural phone state. Never claim these cases ran when the collector cannot establish them. Each invocation has its own bundle. The harness injects one real zero-length datagram and never guesses a desired errno.

## Operator commands after manager approval

From the repository root, examples (replace the labels/endpoints with observed values):

```powershell
python re-analysis/acceptance/hardware/scripts/m1-packed-zero-draft.py --mode packed --firmware-label "robot version from capture; operator notes"
python re-analysis/acceptance/hardware/scripts/m1-packed-zero-draft.py --mode zero --firmware-label "robot/Android/app versions" --phone-ip PHONE_IP --phone-port BOUND_ENGINE_PORT --phone-trace LIVE_TRACE_FILE
```

`--mode both` is available only if both endpoints are reachable and the phone's engine is not competing with this harness for the robot connection. Prefer separate invocations. `--phone-trace` copies a live collector file after injection and a two-second flush window. The operator must check that the copied file contains the injection; if collection finishes later, add its completed raw trace, collector source/hash and a written provenance note to the same bundle before review. No copied trace is automatically adjudicated. If collection needs longer, omit the option and attach the completed files to the bundle afterwards.

## Sequence, verdicts and controls

Six fresh connections: single-frame control then packed trial for each of7,8,9. Each has a five-second connection wait,250ms initial drain, probe, two-second observation, disconnect,500ms separation. Normal keepalive pings carry received reliable acknowledgements every roughly33.3ms; sends are separated by at least3ms. Connection-request retries reuse seq1. Probe messages are **never retried as single frames** within the packed trial: that would make a later ACK ambiguous. Incoming reliable sequence acceptance advances only over a contiguous prefix. This is a bounded test harness, not a replacement production transport.

- Type7 packs two reliable IMURequest messages, reliable ids2 and3. Require a peer ACK3 after the packed send and a working same-payload single-frame control. This proves reliable transport consumption through both ids, not both IMU application effects.
- Type8 packs two unreliable pings with distinct binary64 timestamps. Require both exact timestamp echoes and a working same-payload single-frame control. Do not require isReply: prior M1-033 firmware evidence showed clear isReply on echoes.
- Type9 packs reliable IMURequest2, unreliable ping, reliable IMURequest3. Require ACK3 **and** the probe's exact timestamp echo, plus the single-frame control. This tests the mixed container's reliable and unreliable portions.

`OBSERVED_ACCEPTANCE` means only those criteria on this firmware and these small frames. A missing ACK, echo, connection or control is **INCONCLUSIVE**, not proven rejection. Inspect captures for packet loss, sequence gaps, competing clients, link timeout or bad probe semantics. There is no unconditional PASS and no overall record settlement. Repetition, maximum-size frames, alternate submessage orders/counts and wraparound are outside this bounded run.

M1-043 always starts **INCONCLUSIVE** pending native-trace correlation. The manager records the observed errno, whether the warning occurred, whether reopen occurred, and whether the tick's drain ended. A Windows socket result, successful send, absence of logcat text or robot echo cannot settle it. The initial errno is runtime state, so one phone observation does not prove a universal value across devices, sessions or prior syscalls.

## Self-contained bundle

The script creates exactly one new `re-analysis/acceptance/hardware/<yyyymmdd-hhmmss>-M1-PACKED-ZERO/` directory per invocation; collision fails instead of overwriting. It includes:

- `result.json`: machine-readable per-trial observations, record ids, uncertainties, REVIEW_REQUIRED or ERROR overall; interrupted/error runs keep a result.
- `frames.jsonl`: timestamped raw UDP datagrams in/out, including the actual zero-length outbound datagram, phase, length and endpoint. These are application socket captures, not an independent network pcap or proof of arrival at the phone. Add independent pcap if available.
- `events.jsonl`: socket endpoints, decoding issues, zero-send result, aborts.
- `env.json`: start/end UTC, host/Python, arguments/operator version label, git HEAD/status, script SHA-256 and optional phone trace hash.
- `records.json`: current complete M1-033/M1-043 manifest records.
- exact script copy, this README, and `hashes.json` covering the files at script completion. The hash list does not hash itself. Supplementary collector/pcap files must have their own hashes/provenance note; do not silently edit original raw files or represent later additions as part of the original hash list.

The operator copies this bundle back. Only the manager judges and decides whether to commit it. Script source is a review draft; its commit is not a hardware result.
