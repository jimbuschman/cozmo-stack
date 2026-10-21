# R-M13-017 source review (Codex, 2026-10-21)

Reviewed `20261021-m13-017-located-charger.md` (8626fcfd), against working HEAD
`da6dd692`, current manifest and ARMv7/Thumb instructions in
`resources/lib/armeabi-v7a/libcozmoEngine.so`. Operator authorized ongoing Codex
reviews on 2026-10-21. No implementation, test run or hardware run.

**Verdict: contact-observation contract checked with one behavioral correction
(A9 below). The original A9 sentence must not be built.** The central missing
production join is confirmed. This is not M13-017 settlement, inventory approval,
whole-world equivalence or approval of the report's exhaustive mount caller census.

## Checked path and evidence

| Path | Independent check |
| --- | --- |
| A1–A2 ingress | Sync gate `0x0051293C..942`; status bit12 extraction `0x00512AAC/AAE`, call `0x00512AB4`. At `0x0051199C` input is compared with 1; `0x005119A8` branches away only for false. Dock-pose/lookup/observation precede the old-contact test `0x00511BA8/BB0`. Thus **every true state** observes, using the already-owned robot pose; rising-only creation would be wrong. |
| A3 pose | `0x004EA310/316` constructs pi bits `0x40490FDB`; Z-axis call `0x004EA320`; translation stores `0x004EA32A..334` give (30,0,0); robot pose getter `0x004EA336`; parent/name arguments `0x004EA356`, ctor `0x004EA35A`. Binary string at `0x004EA388` is ChargerDockPose. |
| A4–A5 identity | Family4/type13 insertions `0x00511A1C..A3C`; located first-result call `0x00511A4C..A52`. Default origin mode0 compares current root `0x0061EBF2..0x0061EC26`. Missing located object: factory(13,-1,0) `0x00511AD6..AE2`; connected lookup `0x00511AF6..B02`; copy ID `0x00511B2C/B2E`, otherwise virtual SetID `0x00511B36..B40`. Factory charger branch `0x004E4B0E..B36`; charger vtable SetID relocation at `0x0101E1E8` resolves to `0x004EF468`, unique slot at `0x0101E220` resolves to `0x004E3882` returning1. Type map allocation/reuse `0x004EF4BC..F528` confirms no fixed charger ID. |
| A6–A8 observation | `0x00511B4A` supplies state1 to `0x00506B70`. Fresh read through `0x00506C48`: copy, reparent to robot, flatten and assign; lookup by ID; existing SetPoseHelper with -1/state1 or virtual SetPose and AddLocatedObject. `0x005060AE/B0` bypasses localization clear for Known. Confirmer count1 at `0x00506C30`, reference assignment C38, existing object timestamp C3E. No visual timestamp synthesis. |
| A8 insertion | AddLocatedObject `0x00622AC0..EB0`: connected IDs, ghost type4 guard, allowed deletion type14 and zero padding `0x00622C68..C84`, origin/family/type/ID insertion `0x00622C88..D20`, addition stamp `0x00622D24..D30`, then null old-pose/state0 broadcast `0x00622E42..E4E`. Actionable SetPose clears pre-dock cache `0x004E01E4..226`; common assignment/state/name and distance>=0 gate `0x004EF590..646` preserve distance at -1. |
| A9 notifications | World `0x00506F98`, map `0x00506FA8`, state-change tail `0x00506FB8` in that order. State unchanged returns at `0x00506296..29A`; changed nonzero new state gets the additional map call `0x005062B4`. Active-object test then light recipient `0x005062B8..2D6`. **Corrected light branch below.** |
| A10–A11 contact edges | `0x00511B50..BA4` cleanup before old-contact gate. Rising: platform call BB6, DAS BCA, ChargerEvent constructor BFA and external send C0A, contact store C14. False path `0x00511A66..ACC` sends false edge before store and neither deletes charger nor clears platform. Generated constructors `0x00723F5C..F64` / `0x00724070..078` confirm tags37/39 and byte payloads. |
| P1–P4 platform and threshold | Full setter `0x00511D44..DCA`: argument OR contacts, store34A, change gate, app event, threshold50/400, pause3. Platform tick gates `0x00513CD8..CE8`; family literal resolves to value4 at `0x00C4A878`; located query then real charger/robot quad intersection `0x00513DF4..0x00513E2A`. No charger or no overlap requests false; contacts can still keep platform true. Threshold cache equality at `0x0063427E` branches to **send**, `0x006342C8..CE`; send args reliable1/hot0 `0x00634364..368`. |
| D2–D4 supplementary mount slice | `0x00516870..0x00516A02` independently read: saved ID lookup/cast, missing return1, docked pose then SetNewPose, result propagation/logs. Backup contact caller `0x0054E7B0..BE` ignores result and selects success. Saved-ID write is `0x0054E120`, not 11C. SetNewPose `0x005126EC..722` and AddVisionOnlyState `0x0051513C..18A` confirm pose/history frame update. **Not checked here:** D1's assertion that this is the sole direct caller across the whole binary. Do not promote that census from this check. |

## Correction A9 — new state chooses the light trigger

The report says “old state 1 selects trigger 0x25”. This is contradicted by the
caller and callee together:

- `0x0050628E mov r4,r2` retains old state; `0x00506290 ldrb r5,[r7,#0x24]`
  loads the object's **new** state.
- `0x005062CA mov r2,r4` passes old; `0x005062CC mov r3,r5` passes new.
- At `0x00639834` equal states return; `0x00639840 cmp r3,#1` tests **new**.
  Equal gives trigger0x25 at `0x00639844`; otherwise trigger1 at `0x0063987A`.
  Both use layer2 (`0x0063984A/4E`, `0x00639880/84`).

Correct rule: unchanged state gives no light action; changed state with **new
Known** requests trigger0x25/layer2; other changed states request trigger1/layer2.
New charger Invalid→Known reaches the former. Actual output still depends on the
existing light owner's ID lookup and behavior; do not claim a radio send solely
from this request. Current `Lights.cs:1045` uses the new-state rule already.

Minor citation correction: A5's `movs r3,#0` is at `0x00511AFE`, not B00 (B00 is
`mov r2,r6`). The connected-query flag and behavior are correctly described.

## Current implementation and record boundaries

`Sensors.cs:743` has only edge/platform/event/contact stores; the contact-on
world observation is absent. `Vision/BlockWorld.cs:3044` is the existing carried
object helper: it assumes located and hardcodes Dirty, so it cannot substitute
for the Known/new-object branch. `CozmoRobot.cs:289..298` and
`VisionSystem.cs:67,110..111` establish the shared world/current pose/platform
hook. AddLocatedObject and object-ID owners exist; their existence is not a
whole-path fidelity claim. Preserve insertion timestamp, cache invalidation,
map/obstacle/light consequences when integrating; do not bypass them to make the
platform test pass. Mount's SetPoseOnCharger MISSING remains a separate item.

Current manifest re-read:

- **M13-017 — IMPLEMENTATION_GAP**, “BehaviorDriveOffCharger: runnable on the charger,
  the drive distance and the leaving-the-contacts update”. Evidence cites ctor
  `0x005C0980`, runnable `0x005C0B10`, Init `0x005C0B18`, drive `0x005C0BB8`, Update
  `0x005C0DA8`; it incorrectly labels +34A contacts. Those behavior rows do not yet
  own this new contact-observation contract. +338 contacts/+34A platform correction
  and the contact-ingress evidence must enter an approved inventory before building.
- **M4-019 — EXACT_SOURCE**, the cliff sensor/50–400–150 schedule record: existing
  C8 platform/threshold and geometry handoff are consistent with this check. No
  reopened distance bug, status change or acceptance reversal.
- **M13-009 — IMPLEMENTATION_GAP**, charger dimensions/docked/pre-dock pose, evidence
  includes GetRobotDockedPose `0x004EA1A0`: opposite directed pose from A3; do not reuse
  it as the robot-relative charger pose without the newly sourced direction.
- **M13-008 — IMPLEMENTATION_GAP**, mount/reverse/retry, evidence `0x0054E7B8`:
  supplementary mount body stays a separate build/approval scope.
- **M12-045 — IMPLEMENTATION_GAP**, UnSetCarryingObjects and state2 relative observation,
  evidence B1–B7 `0x00506B78..0x00506C48` / `0x0050609C..0x005060FA`: carried-only
  new-object unreachability does not exclude the contact path.
- **M11-013 — IMPLEMENTATION_GAP**, observed-object creation/ID/confirmation,
  evidence `0x004EF468..526`, `0x00622ADE..E4E`: reusable helpers, not evidence that
  Sensors invokes them. Map/pose recipients retain M11-062/063/065–067/074 ownership.

Next manager unit: integrate the checked contact rows (with corrected A9), choose
an explicit owning record, correct the platform terminology, and prepare the
inventory approval checkpoint. No builder release from the unamended research.
The report's proposed received-state regressions are appropriate, with the
new-state light oracle corrected and real map/pose owners retained. Mount test6
belongs to its separate scope. No tests were run; none prove source fidelity here.

Remaining uncertainty: shared pose/map/obstacle/light implementation gaps, full
world-side equivalence, actual robot behavior, and the unverified exhaustive D1
caller census. No whole M13-017 settlement or hardware verification is claimed.
