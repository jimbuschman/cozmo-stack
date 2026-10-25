# Claude check: research rows 68-70 (last image timestamp, Rejigger/Flatten, explored area)

- **Checked:** `research/20261025-m11-last-image-timestamp.md` (602f0ea3), `research/20261025-m11-rejigger.md`
  (68972eae) and `research/20261025-m11-explored-area.md` (e85666d5). The manager (Opus) re-read the cited instructions
  with Capstone/LIEF, resolved the PLT targets from `.rel.plt`, and resolved the vtable slots from the dynamic
  relocations. Binary: `resources/lib/armeabi-v7a/libcozmoEngine.so` 3.4.0-1204.
- **Verdict: PASS for all three.** Each PARTIAL is honestly bounded: universal indirect-caller or alias closure, and
  nothing more. No record settles; the manifest is unchanged. No exclusion or "no effect" decision is made here.

## Row 68: R-M11-LAST-IMAGE-TS

**Re-read and confirmed:**
- `0x00516EC0 ldr.w r0,[r0,#0x258]`; `b.w` to the VisionComponent getter `0x006527A2 ldr.w r0,[r0,#0xe8]; bx lr`. The
  getter returns VisionComponent+0xE8, a u32.
- **Constructor:** `0x006500B4 movs r5,#0`, `0x0065013A strd r5,r5,[r6,#0xe4]`.
- **Rollback:** `0x00652B36 cmp r3,r0`, `0x00652B38 bhs`, `0x00652B74 movs r0,#0`, `0x00652B76 strd r0,r0,[r4,#0xe4]`.
- **Result tail:** `0x006549A2..0x006549AA` (E8 = result timestamp, EC = new − old).
- **FakeImageProcessed:** `0x00657C2E str.w sb,[r5,#0xe8]`.
- **Border:** `0x00680396 blx 0x4AC6F4` resolves to `Robot::GetLastImageTimeStamp`; `0x006803A2 str r0,[sp]` is the
  fifth argument; `0x006803A6/A8` pass types 9 and 10; `0x006803AA blx r7`.
- **Explicit quad:** `0x0067E6EA` getter, `0x0067E6F8 strd r0,r0,[sp,#0xc]` (both stamps), `0x0067E71C blx r3`.
- **Callback before tail:** `0x00654652 blx 0x4BA4AC` (`VisionComponent::UpdateOverheadEdges`) comes before
  `0x006549AA`, and the loop returns through `0x00654A74` (`VisionSystem::CheckMailbox`).

**What a builder must keep:** during result N's overhead callback, the border stamp is the timestamp of result N−1
(0 before the first result, or after a rollback). It is not result N's. The stack sets
`CameraSettings.LastProcessedImageTimestamp = result.Timestamp` *before* raising `FrameProcessed`
(`VisionSystem.cs:1184-1186`), so wiring that value straight into `ImageTimestamp` would be off by one result. The
builder must sample the pre-tail value.

**Open, and owned by the answer:**
- FakeImageProcessed's callers and thread are UNKNOWN. That needs closing before the every-writer census is called
  complete; no exclusion is assumed.
- The decode→cache timestamp copy (item 3d) is PARTIAL.

## Row 69: R-M11-REJIGGER

**Rejigger 0x00847D7C, re-read in full:**
- **Unknown ID** (`0x00847D98 cbz`) or **new == current** (`0x00847DA0 cmp r0,r7`): log, `0x00847DD2 movs r0,#1`, no
  state change.
- **Success:**
  - old = find(currentID) (`0x00847DD8`, key at list+0x10);
  - the transform is copied into old: four doubles at `0x00847E1A..E2A`, three words at `0x00847E34..E40`;
  - `0x00847E48` SetParent(old, new);
  - the name is old + `"_REJ"` (`0x00847E58 adr` → 0x00847F20, bytes `_REJ\0`);
  - list+0x14/+0x10 = new pose/ID (`0x00847E7A/E7E`);
  - return 0.
- No map entry is erased.

**Flatten 0x00847F34:**
- GetOriginByID target; in-order successor walk.
- Filters: HasParent == 1 (`0x00847F5C`), IsChildOf == 0 (`0x00847F68`). IsChildOf `0x00844FD4/D6` compares the
  immediate parent node only.
- GetWithRespectTo(P, target, P) == 1 (`0x00847F70/74`) → name + `"_FLT"` (`0x00847F4C addw` → 0x00847FD8, bytes
  `_FLT\0`).
- Void return.

**LocalizeToObject:**
- `0x005158D8` old ID → r6; inverse (`0x4A802C` GetInverse) and PreComposeWith (`0x4A7D8C`) of the sp+0x44 transform;
  `0x0051590C` Rejigger.
- On failure, log at `0x00515926`, then `b 0x005159B6` (skips all four).
- On success, `0x0051598A` new ID → r7. The calls run in this order:
  1. `0x00515992` BlockWorld::UpdateObjectOrigins(r6, r7);
  2. `0x0051599E` MapComponent::UpdateMapOrigins;
  3. `0x005159A8` FaceWorld;
  4. `0x005159B2` Flatten(r7).
- No call's return value gates the next.

**Correction of the request:** 0x0051590C is Rejigger. The map call is 0x0051599E. The report says so. The stack's
MISSING text (`BlockWorld.cs:1120`) says the map caller "cannot run before successful M11-044 Rejigger" with
0x0051590C, which agrees with this.

The transform order (newRobot ∘ inverse(oldRobot)) rests on the adopted PR R26 PreComposeWith semantics. It was not
re-derived here.

## Row 70: R-M11-EXPLORED-AREA

**Re-read and confirmed:**
- **Gate:** `0x00624F0E vadd.f32`, `0x00624F16 vcmpe.f32`, `0x00624F1E bpl 0x00624F74`.
- **Reader:**
  - `0x00624F2A` GetCurrentMemoryMapHelper;
  - `0x00624F30 ldr r1,[r1,#0x1c]`, `0x00624F32 blx r1`;
  - `0x00624F4A vstr d0,[sp]`;
  - `0x00624F4E blx 0x4A4F90` resolves to `Util::sEventF`;
  - key `robot.play_area_size` at 0x00625314, format `%.2f` at 0x0062532C.
- **Vtable:** slot 0x01030980 (address point 0x01030964 + 0x1C) has the symbol relocation `GetExploredRegionAreaM2`.
  Its body `0x006816E8 vldr d0,[r0,#0x40]` is one cached f64, with no sum and no unit conversion at read time. Slot
  0x0103099C (+0x38) holds raw 0x00681935. That body, `0x00681934 ldr.w r0,[r0,#0xdc]; ldr r0,[r0,#0xc]`, is a u32
  timestamp.
- **+0x38 readers:**
  - the planner, `0x004FD50E ldr r1,[r1,#0x38]` / `0x004FD516 cmp` / `0x004FD518 blo` (a reimport decision);
  - GatheringAccurateEdge uses +0x20 (`0x005E76A2`), the InterestingEdge area, which is a different value.

**Consequences:**
- The only explored-area reader found is the DAS event. Under M3-055 the engine's calls into libDAS stay exact, so this
  value is not free to drop on this evidence. Anything more than a recorded DAS-only use needs the closure the answer
  lists, or a manager decision recorded on M11-064/M11-045. Neither is made here.
- **The stack's diagnostic is mislabelled:** `MemoryMap.cs:101` says "explored-area helper+0x38". The explored getter is
  virtual +0x1C. +0x38 is the root-content timestamp that the planner and BlockWorld+0x58 read. Queued as Q1 below.
  It's a label fix, and the MISSING stays visible.

## Queued

- **Q1:** relabel the `MemoryMap.cs:101` MISSING as explored-area virtual +0x1C (0x006816E8, MemoryMap+0x40). Fold it
  into the next M11 builder job that touches MemoryMap.
- **Q2 (lead for the M13 planner records):** check whether the stack reproduces the +0x38 root-content timestamp at its
  readers (0x004FD50E planner reimport; 0x006211F4/0x006226F6/0x00625E98 BlockWorld). Check M13-030's current text
  before acting.
- **Q3 (for the row-68 builder job):** sample the pre-tail processed timestamp, not the value `VisionSystem.cs:1184`
  has already advanced.
