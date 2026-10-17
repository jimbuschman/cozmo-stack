# Research request: narrow checks on five M1/M2 startup and robot-message routines

- **Date:** 2026-10-17
- **Requested by:** manager
- **Kind:** narrow check (AGENTS.md "What we reproduce", 2026-10-17)
- **Answer file:** `re-analysis/research/overnight/20261017-ov126-m1-startup-narrow.md`

## Why this is needed

M1+M2 is the acceptance target. Each routine below sits on a production path the stack serves, but nobody knows
whether it touches anything the stack must reproduce. Do **not** extract each routine in full. For each one, answer
the narrow question, with citations:

> Does it affect (a) what is sent to the robot, (b) what the robot does, (c) what reaches the game/API (events, order,
> payload, readiness), or (d) engine state that something else later reads? If yes, list each such effect with its
> address and, for (d), the reader. If no, say so and cite the instructions that show it (stores, calls, sends).

Unknown is not unnecessary: where a callee is not read, say UNKNOWN and name it, rather than "no effect".

## Routines

| # | Routine | Where it is called | Record |
| --- | --- | --- | --- |
| 1 | UiMessageHandler::Init 0x006601B0 | Engine::Init 0x004EC846..0x004EC896. Known lead: it sets UI+0xF1 at 0x006601FA, which gates UI ProcessMessages (0x0066280A..0x00662810). Name every engine state it sets that later code reads. | M1-054 |
| 2 | CozmoEngineImpl::InitInternal 0x004EDC6C (vtable 0x0101E488 +8) | Engine::Init 0x004EC970 | M1-054 |
| 3 | RobotManager's MessageHandler virtual Init (manager+0x60, vslot called at 0x0052E76A) | RobotManager::Init | M1-054; say which existing M1 transport record (M1-0xx) already covers it, if any |
| 4 | SendSupportInfo | Update state 1, 0x004ED5E2 | M1-056 |
| 5 | The robot WifiFlashID (0xEC) handler | TracePrinter registration (B-M3M4-29; RobotSubscriptions.cs:123 names it unbuilt) | M2-020 |

## Out of scope

No implementation, manifest, inventory or job changes; no hardware. Log text is not needed except where a log is the
only effect (then say so). The robot Trace (0xB0) handler is not asked: the stack logs it as diagnostics.

## Answer format

One table per routine: effect | kind (a–d or none) | citation | reader (for d). Then a one-line verdict per routine:
REQUIRED (effects listed), NO EFFECT (evidence cited) or UNKNOWN (unread callees named). Commit and push only the named
answer; the manager checks it before adoption.
