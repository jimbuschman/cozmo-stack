# M1-LINK hardware run: PASS

Robot 172.31.1.1:5551, started 2026-09-24 11:24:12 -05:00. Command: `cozmo-conformance m1-link-check`.

## What was run

`m1-link-check` (cozmo-stack/src/Cozmo.Conformance/LinkCheck.cs) drives `ReliableTransport` directly through its public API. It sends transport-level frames only: ConnectionRequest, DisconnectRequest and the transport's own idle pings. No CLAD message is sent, so the robot gets no SyncTime, no firmware check and no app-layer setup. The sequence and its timings are fixed:

1. Construct the production transport (async mode, engine defaults) and `Start`. Record the local UDP port this process bound, from the OS (`netstat -ano -p UDP, filtered by this process id`).
2. `Connect`, then wait up to 5000 ms for the ConnectionResponse (the `Connected` event).
3. Hold the link idle for 30000 ms.
4. `Disconnect`, issued 8 ms after an outbound frame so that the 2.0 ms send-spacing gate cannot block the single type-3 send. Confirm the type 3 and the deleted connection, then wait 2000 ms.
5. `Connect` again on the same transport, wait up to 5000 ms, confirm the local port is unchanged, then hold 5000 ms.
6. `Disconnect` (timed as in step 4), then `Stop`, wait 1000 ms, then `Dispose`, and wait 500 ms.

## Files

- `result.json`: the verdict. `overall` is PASS only if every entry in `checks` has `pass: true`. Each check has an `id`, `title`, `pass`, `measured` (the values it was judged on), `expected` (the rule, in words), `rows` (the M1 inventory rows it rests on, in re-analysis/inventory/M1-transport.md) and `records` (fidelity manifest ids). A skipped check (after a failed connect) is `pass: false` with `measured.skipped`. `reports` holds the error counters, which are reported and not judged. `observations` is M1-033 (HARDWARE_ONLY): robot-side behaviour recorded with `verdict: null`. `records` gives each cited record's manifest status at run time. `phases` and `marksMs` give the times, in ms since the run began.
- `frames.jsonl`: one line per datagram the transport's frame trace reported, in both directions. Each line has the time `t` (ms since start), `phase`, `dir` (out/in), the header (type, seqMin, seqMax, ack), the sub-messages (type, seq, size, and the parsed ping or the CLAD tag), and the raw datagram as `hex`.
- `events.jsonl`: transport warnings, receiver events (R40 markers, with the CLAD tag for data), Connected/Disconnected, state changes (link state, the timed-out flag, whether the peer has a connection; polled every 2 ms), phases, and check verdicts.
- `counters.json`: frame, byte and warning counts, the transport's error counters by code, handler faults, and snapshots of the connection's own counters at the end of each hold.
- `env.json`: git HEAD and dirty state, SHA-256 of the tool and transport sources and of the assemblies that ran, OS, .NET, robot IP/port, local port with the netstat lines it came from, start/end times, the fixed timings, and the transport options.

## Limits of what the bundle shows

- The transport drops a datagram from an address with no connection ("unconnected source") and a truncated datagram before its frame trace. Such datagrams appear only as warnings in `events.jsonl`, with the source and frame type but without the bytes. This matters after each Disconnect.
- An outbound time is taken just after sendto returns. An inbound time is taken when the transport's update processed the datagram, up to one 2 ms update after it arrived.
- Connection snapshots are read without the transport lock, and are diagnostics only.
- A PASS is hardware verification. It does not raise the provenance of any record.
