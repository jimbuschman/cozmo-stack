# Trial queue (isolated branch only)

Operator scope: M13-017, M13-040, M13-003, M13-030 drive-off/planner. Up to three useful bounded implementation jobs. No official status changes or subsystem acceptance. Main QUEUE is history only.

| Job | State | Purpose | Evidence/review |
| --- | --- | --- | --- |
| CM13-01 | CODE VERIFIED, integrated9d52255b; full6267/6267 PASS; Claude/HW outstanding | Native current-map type3 FindIf/payload planner source | independent20261026-cm13-01-codex-check.md PASS; builder98/98, reviewer10+6; no HW |
| CM13-02 | CODE VERIFIED, integrated3d2021a8; full6267/6267 PASS; Claude/HW outstanding | Wire drive-off objective4 to real E2G0x8B API/local delivery | adopted RC3 and narrow serializer/broadcast checks; independent review pending |

Deferred: native configuration-container producer M12-049; broader pose lifetime M11-073/084; native ActivitySparked objective recipient M15-059 and mood attachment. Neither is dismissed or marked no-effect.

Superseded work rejected: repeat root geometry/growth/shift/merge, severe-needs manager lifetime, timestamp seed/cache gate, Markerless charger footprints, and unrelated get-out API admission.


No further jobs released. Two bounded jobs complete with independent code review; no subsystem acceptance. Final required gate PASS on36817201 after documented first-attempt teardown abort/fixb2891e6c. Production ownership/pose/configuration/mood/recipient gaps remain in HANDOFF.md and PROPOSALS.md.

## Round 2 (released by the Claude manager, 2026-10-26)

CM13-01/02 Claude check PASS (`research/20261026-cm13-01-02-claude-check.md`); merged to main at 1f7567ea.

| Job | State | Purpose |
| --- | --- | --- |
| CM13-03 | CODE VERIFIED; FULL GATE FAIL; Claude pending | Runtime ownership b1c1169d: Phase1/build independent PASS; full6274 pass/1 fail/0 skip (KnockOver cause UNKNOWN). Upstream gaps visible. Branch `codex-m13-manager-2`; see HANDOFF-2.md. |
