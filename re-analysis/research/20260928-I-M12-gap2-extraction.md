# I-M12 gap pass 2 — F1/F2/F3 correction (read-only)

Job: re-analysis/jobs/I-M12.md, gap pass 2. Subsystem: M12-manipulation.
Agent: opencode (extractor), window 3. Date: 2026-09-28.
Binary: `resources/lib/armeabi-v7a/libcozmoEngine.so`, sha256
`02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1` (3.4.0-1204), Thumb-2.
Prior report: `re-analysis/research/20260928-I-M12-gap1-extraction.md`.
Method: every address below was opened in the `.so` with capstone (Thumb) and the ELF symbol
table; the Ghidra decompilation was navigation only. Addresses are file VAs. Read-only; only
this file and `.scratch/I-M12-gap2/` were written.

---

## F1 (contradicted) — G3.8's PathMotionProfile consumer

**Confirmed exactly:**

| fact | evidence |
|---|---|
| `0x0064ACC4` is `PathComponent::StartDrivingToPose(vector<Pose3d> const&, shared_ptr<unsigned char>, bool)`, not `UpdatePlanning` | symbol table `0x0064ACC4`; entry `0x0064ACC4 push.w {r4,r5,r6,r7,r8,sb,sl,fp,lr}`; `index.tsv` line `0064acc4 624 OK ... StartDrivingToPose` |
| the only call to `SpeedChooser::GetPathMotionProfile` (PLT `0x4B9DBC`) is at `0x0064ADE0`, inside `StartDrivingToPose` | `0x0064ADE0 blx #0x4b9dbc ; PLT ...GetPathMotionProfile`; whole-`.text` scan for callers of `0x4B9DBC` returns exactly one hit, `0x0064ADE0` |
| `PathComponent::UpdatePlanning` is `0x006495E0` (main body to `0x0064972E pop {r4,pc}`; `0x00649730..0x0064975C` are its exception landing pads, then unwind tables/rodata to `0x0064980B`) and contains **no** call to `0x4B9DBC` or to StartDrivingToPose's PLT `0x4ABEE4` | entry `0x006495E0 push {r4,lr}`; `index.tsv` line `006495e0 338 OK ... UpdatePlanning`; full disassembly in `.scratch/I-M12-gap2/` |
| `UpdatePlanning` does **not** call `StartDrivingToPose` (directly or indirectly). Its only caller is `PathComponent::Update` at `0x006494DA`. Its callees are `ReplanWithFallbackPlanner` (`0x006495FE blx 0x4B9D20`), `SetDriveToPoseStatus` (`0x0064960A`, `0x006496EA`, `0x006496CA`, `0x006496AE`), `Abort` (`0x006496B0 blx 0x4A7AC8`), `VizManager::ErasePath` (`0x006496C2 blx 0x4A41A4`), `sChanneledInfoF`, and on planner status 2 a tail call `0x006496DE b.w #0x8CCF1C` (Thumb veneer `0x8CCF1C bx pc` / ARM `ldr ip,[pc]` at `0x8CCF20` with literal `0x8CCF28 = 0xFFBECE00` → target `0x4B9D2C`, the PLT stub for `PathComponent::HandlePlanComplete`). | `0x006494DA blx #0x4b9d08` is the only caller of PLT `0x4B9D08`; callers scan of `0x4ABEE4` returns exactly one hit, `0x0055A998` |

**Correct production path from `StartDrivingToPose` to the PathMotionProfile:**

| step | what the original does | citation | class |
|---|---|---|---|
| F1.1 | `DriveToPoseAction::Init` calls `PathComponent::StartDrivingToPose`; this is the only call site. | `DriveToPoseAction::Init` `0x0055A86C`; `0x0055A998 blx #0x4abee4 ; PLT StartDrivingToPose` | EXACT_SOURCE |
| F1.2 | `StartDrivingToPose` calls `SpeedChooser::GetPathMotionProfile`; this is the only call site of that function. | `0x0064ADE0 blx #0x4b9dbc`; `GetPathMotionProfile` `0x0053B798` | EXACT_SOURCE |
| F1.3 | If no custom profile is set (`[this+0x48]==0`) it copies the 41-byte returned profile into the path component's profile slot `[this+0x4C]`; a custom profile skips the copy. | `0x0064ADD2 ldrb.w r0,[r4,#0x48]`; `0x0064ADD6 cbnz r0,#0x64adf2`; `0x0064ADE4 ldr r0,[r4,#0x4c]`; copy `0x0064ADE6..0x0064ADF0` | EXACT_SOURCE |
| F1.4 | The slot is the same one `SetCustomMotionProfile`/`GetCustomMotionProfile` use. | `0x0064AB90 ldr r0,[r7,#0x4c]` (Set); `0x0064AC6A ldr r0,[r4,#0x4c]` (Get) | EXACT_SOURCE |
| F1.5 | When the planner reports status 2, `PathComponent::UpdatePlanning` tail-calls `PathComponent::HandlePlanComplete`. | `0x006496DE b.w #0x8CCF1C` (veneer to PLT `0x4B9D2C`); `UpdatePlanning` `0x006495E0` | EXACT_SOURCE |
| F1.6 | `HandlePlanComplete` reads `[this+0x4C]` and passes it as the `PathMotionProfile const*` argument to `IPathPlanner::GetCompletePath`; this is that function's only call site. | `0x00649F76 ldr r1,[r4,#0x4c]`; `0x00649F7E str r1,[sp]`; `0x00649F82 blx #0x4b9d68 ; PLT IPathPlanner::GetCompletePath(Pose3d const&, Planning::Path&, unsigned char&, PathMotionProfile const*)` | EXACT_SOURCE |
| F1.7 | The planner's returned `Path` is handed to `PathComponent::ExecutePath`, which packs and sends it; the segments are packed by `PathDolerOuter::Dole`. | `0x0064A0A4 blx #0x4b9d74 ; PLT ExecutePath`; `ExecutePath` `0x0064A340` (`0x0064A436 blx 0x4B9D8C`); `PathDolerOuter::Dole` `0x00507E4C` | EXACT_SOURCE |

So `UpdatePlanning` and `StartDrivingToPose` are two ends of one path that meet at `[PathComponent+0x4C]`:
`PathComponent::Update → UpdatePlanning → (status 2) HandlePlanComplete → GetCompletePath(profile) → ExecutePath → PathDolerOuter::Dole → wire`,
with the profile loaded into `[PathComponent+0x4C]` by `DriveToPoseAction::Init → StartDrivingToPose → GetPathMotionProfile`.

**Is M12-002's "path packing and constants" live-path wiring established by it?**
Partially, and honestly:
- The **DriveToPose constants** (the `PathMotionProfile` default table at `0x00C535E0`, G3.7) are wired: produced by `GetPathMotionProfile`, stored at `[PathComponent+0x4C]`, and consumed by the planner in `GetCompletePath`, whose `Path` reaches `ExecutePath`/`PathDolerOuter::Dole`. That is a live path to the wire.
- **Not established by this path:** the **DriveToObject constants**. `DriveToObjectAction` never reaches `StartDrivingToPose`; the only caller of `StartDrivingToPose` is `DriveToPoseAction::Init`. `DriveToObjectAction` has its own constructor defaults (G3.9) and reaches poses through `DriveToObjectAction::GetPossiblePoses`.
- **Unread step:** what a concrete `IPathPlanner::GetCompletePath` implementation does with the profile (it is a virtual; the lattice/fallback planner bodies are M13). The profile is passed, but its internal effect on the produced `PathSegment` fields is not read here.

`G3.8` as written ("consumed by `PathComponent::UpdatePlanning`") is contradicted: the consumer is `StartDrivingToPose` at `0x0064ADE0`, and `UpdatePlanning` is not a caller of either `StartDrivingToPose` or `GetPathMotionProfile`.

---

## F2 (minor) — G1.6's type-2 4th-arg zero

**Confirmed:** `0x004E5ABA` is inside the type-1 branch, not type 2.
- type-1 branch is `0x004E5A4C..0x004E5AEE` (`0x004E5A4C add r6,sp,#0x38`; `0x004E5AEE b #0x004E5D4A`). `0x004E5ABA mov.w r8,#0` sits there; type 1's 4th ctor arg is 40.0, written by `0x004E5ADA movt r1,#0x4220` then `0x004E5AE0 str r1,[sp]`, and the ctor is called at `0x004E5AE4`.
- type-2 branch is entered at `0x004E5AF0`. Its 4th-arg zero is set by `0x004E5B5A mov.w r8,#0` and stored by `0x004E5B80 str.w r8,[sp]`, immediately before `0x004E5B86 blx ...PreActionPose ctor`.

**Corrected citation for G1.6's 4th arg:** `mov.w r8,#0` at `0x004E5B5A` (not `0x004E5ABA`), `str.w r8,[sp]` at `0x004E5B80`. The pose translation -49.0 cited at `0x004E5B0C` (`movt r0,#0xc244`) is correct and unchanged.

---

## F3 (precision) — PreActionPose+0x18: compaction vs value consumer

**Verifier's compaction finding is confirmed.** In `IDockAction::GetPreActionPoses` (`0x005508C8`) the vector compaction moves the whole 0x1C-byte element:
`0x00550B3C mov r6,r5`; `0x00550B52 ldrd r0,r1,[r6,#0x30]`; `0x00550B56 strd r0,r1,[r6,#0x14]`; `0x00550B5A adds r6,#0x1c`.
`r6` is the current element, so `r6+0x30` is the next element's `+0x14` and `r6+0x14` is the current element's `+0x14`: the 8 bytes `+0x14..+0x1B`, which include `+0x18`, are moved as a unit. The second compaction path (`0x00550B82 ldrd r0,r1,[r6,#0x1c]` … `0x00550B96/0x00550B9A`) does the same. This is a struct move, not a value consumer.

**But there *is* a value consumer, and it is on the M12 live path.** `ActionableObject::GetCurrentPreActionPoses` (`0x004DF850`):
- builds a local `vector<PreActionPose>` at `sp+0xBC` (`0x004DF8C8 blx #0x4a4138 ; PLT vector<PreActionPose>::insert`);
- iterates it with `sl = [sp+0xBC]` (`0x004DF8EE ldrd sl,r6,[sp,#0xbc]`) and stride `0x004DFDC4 add.w sl,sl,#0x1c`;
- reads element`+0x18` and uses it as a value in arithmetic:
  - `0x004DF9DE vldr s28,[sl,#0x18]` then `0x004DFA26 vmul.f32 s0,s0,s28` (`s0` = cos of the angle) and `0x004DFA2A vadd.f32 s0,s24,s0`;
  - `0x004DF9F4 vldr s30,[sl,#0x18]` then `0x004DFA78 vmul.f32 s0,s0,s30`;
  - `0x004DFB88 vldr s0,[sl,#0x18]` then `0x004DFB8C vcmpe.f32 s2,s0` (compare against the computed distance);
  - `0x004DFCAC vldr s26,[sl,#0x18]` and `0x004DFCC2 vldr s30,[sl,#0x18]` then `0x004DFCD8 vmul.f32 s0,s0,s26` and `0x004DFCDC vmul.f32 s2,s2,s30`, followed by `0x004DFCE0/0x004DFCE4 vsub.f32` and the result stored into the pose transform at `0x004DFCFC..0x004DFD00`;
  - it also passes the field through the copy-with-pose ctor at `0x004DF99A ldr.w r3,[sl,#0x18]`, `0x004DFBD8 ldr.w r3,[sl,#0x18]`, `0x004DFC64 ldr.w r3,[sl,#0x18]` (these are field-preserving copies).
- Live path: `DriveToObjectAction::GetPossiblePoses` (`0x00558C80`) calls `IDockAction::GetPreActionPoses` (`0x00558CDE blx #0x4ab950`); `IDockAction::GetPreActionPoses` (`0x005508C8`) calls `GetCurrentPreActionPoses` (`0x00550A3E blx #0x4a4210`). So the consumer is reachable from the M12 drive-to-object path.

**Whole-`.text` scan result.** A linear Thumb sweep of `.text` (`0x4D6860..0xAE3682`, excluding the ARM-mode Wwise region `0x95E540..0xAE2E40`) found **5,599** instructions whose memory displacement is exactly `0x18`. Filtered to `PreActionPose` elements (base advancing by `0x1C`), the only `+0x18` loads are:
- `ActionableObject::GetCurrentPreActionPoses` — the value reads above (**NEW**);
- `IDockAction::GetPreActionPoses` — the struct-move compaction only (`0x550B52/0x550B56`, and `0x550B82/0x550B96`);
- `PreActionPose::PreActionPose(PreActionPose const&)` (`0x50E138`) — `0x0050E160 ldr r0,[r4,#0x18]` / `0x0050E162 str r0,[r5,#0x18]` (struct copy);
- `vector<PreActionPose>::insert` (`0x4DFE5C`) — `0x004DFF7A ldrd r0,r1,[r5,#0x14]` (struct copy covering `+0x18`).

The `vldr s0,[sb,#0x14]` / `vldr s2,[sb,#0x18]` loads in `IDockAction::GetPreActionPoses` at `0x550F42/0x550F46`, `0x5510D6/0x5510DA`, `0x55114E/0x551152` are fields of the `PreActionPoseOutput` struct `sb` (vector at `sb+4`, index at `sb+0x10`), not of a `PreActionPose` element. `PreActionPose::GetVisualizeColor` takes an `ActionType`, not a pose; its `+0x18` hits are stack. `Block/Charger/Ramp::GeneratePreActionPoses` `+0x18` hits are producer-object geometry. `DriveToHelper::DriveToPreActionPose` `0x005B5980 ldr r0,[r0,#0x18]` is `[object+0x18]` used as a pointer base (`0x005B5982 str r1,[r0,#0x70]`), not a float read.

So `G1.14`'s statement "What reads `PreActionPose+0x18` was **not found** in this pass" is contradicted: it is read as a value in `ActionableObject::GetCurrentPreActionPoses`. The correct class is EXACT_SOURCE for the consumer; this is a NEW step (or a rewrite of G1.14), not a RECOVERABLE_GAP.

---

## What remains unread about PreActionPose+0x18

- The **identity** of `+0x18` is read: it is the 4th `PreActionPose` ctor argument (the 75/40/0/75/0 values), written at `0x0050DD46 vstr s16,[r5,#0x18]`, distinct from the `+0x14` height tolerance written by `SetHeightTolerance` (`0x0050DB06`).
- The **consumer** is read: `ActionableObject::GetCurrentPreActionPoses` uses it as a distance/radius in computing the world-space pose. What is *not* read is its exact physical name/units and whether any non-M12 consumer (a planner or behaviour outside the functions above) also reads it; the whole-`.text` sweep found no other `PreActionPose`-element `+0x18` value read, but the sweep is a linear-sweep heuristic and the planner-internal use of the profile is M13 territory.
- No other unread step for F1/F2/F3.

*Read-only extraction. Nothing outside `.scratch/I-M12-gap2/` and this report file was changed.*

