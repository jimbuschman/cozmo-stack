# Job board

Read `README.md` first. Each job's status is in `status/<job>.md`: no file means the job is open.

## Three windows, each running a chain unattended

Each window is its own clone, so builds never collide. Each runs one `scripts/run-job.ps1` chain. A chain stops at the first job that doesn't end DONE.

| window | clone | chain |
| --- | --- | --- |
| 1 | `cozmo-stack-w1` | [X3](X3.md) → [X4](X4.md) → [I-M8](I-M8.md) → [B-M8](B-M8.md) → [I-M7](I-M7.md) → [B-M7](B-M7.md) → [I-M9](I-M9.md) → [B-M9](B-M9.md) → [I-M15](I-M15.md) → [B-M15](B-M15.md) |
| 2 | `cozmo-stack` (the main clone) | [X5](X5.md) → [I-M6](I-M6.md) → [B1](B1.md), which BLOCKED correctly: the live voice engine had no record → [I-M6b](I-M6b.md) → [B-M6b](B-M6b.md), which built the voice-engine skeleton and blocked as too large → [B-M6b-1](B-M6b-1.md) → [B-M6b-2](B-M6b-2.md) → [B-M6b-3](B-M6b-3.md) |
| 3 | `cozmo-stack-w3` | [X1](X1.md) → [X2](X2.md) → [I-M11](I-M11.md) → [B-M11](B-M11.md) → [I-M12](I-M12.md) → [B-M12](B-M12.md) → [I-M13](I-M13.md) → [B-M13](B-M13.md) → [I-M14](I-M14.md) → [B-M14](B-M14.md) |

## Round 2 (2026-09-29): the three chains above are DONE

| window | clone | chain |
| --- | --- | --- |
| 1 | `cozmo-stack-w1` | [G-M7](G-M7.md) → [G-M15](G-M15.md) |
| 2 | `cozmo-stack` (the main clone) | [B-M6b-3](B-M6b-3.md) |
| 3 | `cozmo-stack-w3` | [G-M14](G-M14.md) |

G jobs fold one checked extraction (Codex's overnight reports) into an approved subsystem, then build it.

## Round 3 (2026-09-29): the remaining gaps, after round 2

Every layer is built once, and 99 IMPLEMENTATION_GAP records are left. The R jobs close them layer group by layer
group ([R.md](R.md)). Each window continues with its round-3 chain when its round-2 chain ends. The chains are ordered
so that no two windows work on the same layers at once. R-VIS moved to a Sonnet worker on 2026-09-29 (operator), so window 1 runs R-BEH only. R-ANIM moved to window 2 (idle after B-M6b-3 blocked), so it runs alongside R-DEV.

| window | clone | chain |
| --- | --- | --- |
| 1 | `cozmo-stack-w1` | [R-BEH](R-BEH.md) (M7, M8), DONE. Paused to save credits; [R-BEH2](R-BEH2.md) goes to the next free Sonnet, with Codex pre-extracting |
| 4 (Sonnet in Claude Code, on the manager's machine) | `cozmo-stack-r` | [B-M6b-4](B-M6b-4.md) (the M6 live-audio wiring) |
| 5 (a second Sonnet, on the manager's machine) | `cozmo-stack-r2` | [R-VIS](R-VIS.md) (M11, M12, M13, M14), once G-M14 is DONE |
| 2 | `cozmo-stack` (the main clone) | [R-ANIM](R-ANIM.md) (M5, M10), DONE → [R-M15](R-M15.md) (M15) → [R-M9](R-M9.md) and [R-M6](R-M6.md), which wait for B-M6b-4 |
| 3 | `cozmo-stack-w3` | [R-DEV](R-DEV.md) (M1, M2, M3, M4) |

Codex, meanwhile: `re-analysis/research/requests/20260929-M9-singing-gaps.md`, then
`20260929-policy-review.md` (the 32 COMPATIBILITY_POLICY and EQUIVALENT_IMPLEMENTATION records against "Exact,
always").

## Job types

- **X, extraction:** read-only. Writes reports to `re-analysis/research/`.
- **I, integration:** checks the citations, closes the gaps, writes and approves the layer's inventory and records.
- **B, build:** builds the layer from its approved inventory, wires it into the live path, verifies, and settles records.

## After both chains

Window 1's behaviour layers (M7, M8, M9, M15) are built before window 3's vision, manipulation, navigation and faces (M11–M14) exist. So some behaviour records stay IMPLEMENTATION_GAP, naming those layers (SD3). Once both chains are DONE, a final pass builds those cross-layer gaps.
