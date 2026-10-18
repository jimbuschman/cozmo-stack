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
> payload, readiness), or (d) a state change that feeds (a), (b) or (c)? If yes, list each such effect with its
> address. For (d), name the reader **and** the supported effect it leads to. If no, say so and cite the instructions
> that show it (stores, calls, sends).

An internal reader alone does not make a state change REQUIRED: it is REQUIRED only when the chain reaches (a), (b) or
(c), and the answer names that effect. Where the chain is not followed to an end, say UNKNOWN and name the unread
link, rather than REQUIRED or "no effect". Unknown is not unnecessary.

## Routines

| # | Routine | Where it is called | Record |
| --- | --- | --- | --- |
| 1 | UiMessageHandler::Init 0x006601B0 | Engine::Init 0x004EC846..0x004EC896. Known lead: it sets UI+0xF1 at 0x006601FA, which gates UI ProcessMessages (0x0066280A..0x00662810). Name every engine state it sets that later code reads. | M1-054 |
| 2 | CozmoEngineImpl::InitInternal 0x004EDC6C (vtable 0x0101E488 +8) | Engine::Init 0x004EC970 | M1-054 |
| 3 | RobotManager's MessageHandler virtual Init (manager+0x60, vslot called at 0x0052E76A) | RobotManager::Init | M1-054; say which existing M1 transport record (M1-0xx) already covers it, if any |
| 4 | SendSupportInfo | Update state 1, 0x004ED5E2 | M1-056 |
| 5 | The robot WifiFlashID (0xEC) handler | TracePrinter registration (B-M3M4-29; RobotSubscriptions.cs:123 names it unbuilt) | M2-020 |
| 6 | The local consumers of the **Init-time** AnimationAvailable/EndOfMessage | BroadcastAvailableAnimations at 0x0052E76E sends through E2G slot +0x1C (0x0052F8BA, 0x0052F940). UiMessageHandler::Broadcast (0x006625B0) also runs a local event dispatch (0x006625FA). List every local subscriber to E2G AnimationAvailable and EndOfMessage (tag 0x9A), as the M2-022 answer did for tag 0x60. For each: does the Init-time delivery (zero names, then EndOfMessage) cause an effect in (a)–(d) that the later post-loading broadcast would not also cause? | M1-055 |

## Out of scope

No implementation, manifest, inventory or job changes; no hardware. Log text is not needed except where a log is the
only effect (then say so). The robot Trace (0xB0) handler is not asked: the stack logs it as diagnostics.

## Answer format

One table per routine: effect | kind (a–d or none) | citation | reader and supported effect (for d). Then a one-line
verdict per routine: REQUIRED (the supported effects named), NO EFFECT (evidence cited) or UNKNOWN (the unread links
named). Commit and push only the named
answer; the manager checks it before adoption.
