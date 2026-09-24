# Project state

Read first in every session. The manager keeps this file current; the process it follows is the Process section of `AGENTS.md`.

## Now

- **Phase:** setup of the evidence process. **Checkpoint 1 (operator approves the setup) is pending.**
- **Next:** M1 transport inventory, once checkpoint 1 is approved.

## Layer order and review state

The review state of each subsystem is recorded in `re-analysis/fidelity_manifest.json`, and `re-analysis/FIDELITY_GAPS.md` renders it. Every subsystem is **UNREVIEWED**. Their records and their "source read / built" flags were written before this process, and nothing vouches for them yet.

| order | subsystem | review | notes |
| ---: | --- | --- | --- |
| 1 | M1-transport | UNREVIEWED | candidate changes in the working tree (below); 5 of 12 settled records cite no address or file |
| 2 | M2-protocol | UNREVIEWED | |
| 3 | M3-device (camera, display, audio device) | UNREVIEWED | colour camera format is HARDWARE_ONLY |
| 4 | M4-control (motion, sensors, lights, cubes) | UNREVIEWED | the outbound cube-connection path is suspected unrecovered |
| 5 | M5-animation | UNREVIEWED | the live-animation wire lifecycle is suspected incomplete |
| 6 | M6-wwise-bank, M9-wwise-music | UNREVIEWED | singing holds BLOCKED_EXTERNAL Wwise runtime semantics |
| 7 | M7, M8, M10 (behaviour, framework, derived state) | UNREVIEWED | |
| 8 | M11–M15 (vision, manipulation, navigation, faces, freeplay) | UNREVIEWED | |
| – | tools | UNREVIEWED | offline tooling |

The operator can reorder layers 3 and up.

## M1 candidate (uncommitted working tree)

- **What it is:** the M1 repair done with ChatGPT on top of `bcab556`, applied from `~/Downloads/m1-repair-review.patch` and then `m1-final-corrections.patch`. It touches 27 files: `ReliableTransport.cs` (the bulk of it), `ReliableConnection.cs`, `CozmoRobot.cs`, `Cozmo.Transport.csproj`, conformance tools and tests, plus the new `TransportRepairTests.cs`.
- **Reported results:** 48/48 focused tests and 999/999 overall. That report comes from the ChatGPT session and the manager has not re-run it. It has had no hardware acceptance.
- **Treatment:** a candidate implementation. The M1 inventory decides what survives. Nothing is discarded or accepted before then.
- The setup work for this process touches none of those 27 files.

## Hardware

- Only the operator's machine reaches a robot. The manager never plans a step that assumes otherwise.
- New runs go to `re-analysis/acceptance/hardware/<yyyymmdd-hhmmss>-<test-id>/` (AGENTS.md, "Hardware runs").
- **Existing tooling, not yet reviewed under this process:**
  - `hardware-test` in `Cozmo.Conformance` asks the operator for a verdict per check, where this process wants the result judged from a machine-readable bundle.
  - Earlier bundles are in `cozmo-stack/re-analysis/acceptance/` and `cozmo-stack/cozmo-acceptance-*.json`.
  - `HardwareCatalog.cs` maps checks to fidelity ids.
  - Each is to be reviewed alongside the layer it tests.

## Legacy documents

`re-analysis/HANDOFF.md`, `NEXT_MILESTONE.md`, the milestone documents and the claims of "complete offline" predate this process. They are notes, the lowest authority. Where they disagree with an approved inventory, the inventory wins.

The user-level agents in `~/.claude/agents/` (`cozmo-m1-transport-auditor`, `cozmo-m2-protocol-auditor`, `cozmo-proof-auditor`, `cozmo-adversarial-reviewer`) come from an earlier salvage audit and point at a stale scratch directory. The process uses the project agents in `.claude/agents/` instead.

## Accepted commits

| commit | subsystem | what it accepted |
| --- | --- | --- |
| – | – | nothing yet |

## Open decisions for the operator

- Checkpoint 1: approve the setup (AGENTS.md Process section, `.claude/agents/`, the checker changes, this file).
