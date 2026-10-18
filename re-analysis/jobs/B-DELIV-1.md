# Job B-DELIV-1: app delivery never runs host handlers under engine locks

- **Agent:** Codex, as builder. Follow `jobs/CODEX-BUILDER.md` rules 1–15 and AGENTS.md "What we reproduce".
- **Status file:** `status/B-DELIV-1.md`.
- **Runs after B-M1-4.**
- **Checked rows:** `research/20261017-b-m2m15-2-check.md` finding 1 (raised to blocking by the manager), and the
  queued B-M11-8 item (`NativeDelocalization.cs`:170-172).

## Why

Native app delivery goes through UiMessageHandler::Broadcast(&&) 0x006625B0 → vtable +0x48 DeliverToGame 0x00661594,
which buffers the message for the app. No app code runs inside engine code. The stack runs host `EngineToGameMessage`
handlers inline, in places under engine locks and mid-mutation (`Animation/CozmoAnimations.cs`:188-197 inside
`AnimationScheduler.AbortLocked` under `_gate`). A host handler that plays an animation on AnimationAborted (0x5F)
recurses without bound; one that waits on a thread needing the lock deadlocks.

## Items

1. Route app (C# API) delivery through one buffer, filled where the engine calls Broadcast/DeliverToGame and drained to
   host handlers outside every engine lock (for example at the end of the engine tick step, or on the API's own
   dispatch), preserving message order exactly. The local in-engine emit that follows DeliverToGame stays synchronous
   and in its native place (app before local is about order of enqueue vs local emit; keep it).
2. Cover every inline delivery site, including `CozmoAnimations.cs`:188-197 and `NativeDelocalization.cs`:170-172. List
   each site in the status file.
3. Tests: a host handler that plays an animation on 0x5F neither recurses nor deadlocks, and sees the messages in native
   order; a handler that blocks briefly does not stall the engine tick under `_gate`.

## Out of scope

- Status changes; any change to message content or order.

**Manifest:** append only.
