# Job board

Read `README.md` first. Each job's status is in `status/<job>.md`: no file means the job is open.

## Three windows, each running a chain unattended

Each window is its own clone, so builds never collide. Each runs one `scripts/run-job.ps1` chain. A chain stops at the first job that doesn't end DONE.

| window | clone | chain |
| --- | --- | --- |
| 1 | `cozmo-stack-w1` | [X3](X3.md) → [X4](X4.md) → [I-M8](I-M8.md) → [B-M8](B-M8.md) → [I-M7](I-M7.md) → [B-M7](B-M7.md) → [I-M9](I-M9.md) → [B-M9](B-M9.md) → [I-M15](I-M15.md) → [B-M15](B-M15.md) |
| 2 | `cozmo-stack` (the main clone) | [X5](X5.md) → [I-M6](I-M6.md) → [B1](B1.md), which BLOCKED correctly: the live voice engine had no record → [I-M6b](I-M6b.md) → [B-M6b](B-M6b.md) |
| 3 | `cozmo-stack-w3` | [X1](X1.md) → [X2](X2.md) → [I-M11](I-M11.md) → [B-M11](B-M11.md) → [I-M12](I-M12.md) → [B-M12](B-M12.md) → [I-M13](I-M13.md) → [B-M13](B-M13.md) → [I-M14](I-M14.md) → [B-M14](B-M14.md) |

## Job types

- **X, extraction:** read-only. Writes reports to `re-analysis/research/`.
- **I, integration:** checks the citations, closes the gaps, writes and approves the layer's inventory and records.
- **B, build:** builds the layer from its approved inventory, wires it into the live path, verifies, and settles records.

## After both chains

Window 1's behaviour layers (M7, M8, M9, M15) are built before window 3's vision, manipulation, navigation and faces (M11–M14) exist. So some behaviour records stay IMPLEMENTATION_GAP, naming those layers (SD3). Once both chains are DONE, a final pass builds those cross-layer gaps.
