# Job board

Read `README.md` first. Each job's status is in `status/<job>.md`: no file means the job is open.

| job | type | agent | what | depends on |
| --- | --- | --- | --- | --- |
| [X1](X1.md) | extraction | Codex | M11 vision, then M12 manipulation | |
| [X2](X2.md) | extraction | Codex, after X1 | M13 navigation, then M14 faces | |
| [X3](X3.md) | extraction | opencode window 1 | M8 framework, then M7 behaviours | |
| [X4](X4.md) | extraction | opencode window 1, after X3 | M9 singing, then M15 freeplay | |
| [X5](X5.md) | extraction | opencode window 2 | M6 exact IMDCT, then M6-020 | |
| [B1](B1.md) | build | opencode window 2 (build clone) | close M6: the exact IMDCT, and the runtime wired into the live audio path | X5, and its approved inventory correction |

**The integrator (Claude)** turns each finished report into an approved inventory and adds the next build job for that layer here. The build order follows the layer dependencies: M11 → M12 → M13 → M14, and M8 → M7 → M9 → M15.
