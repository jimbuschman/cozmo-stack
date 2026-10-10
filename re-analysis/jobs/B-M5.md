# Job B-M5: animation and the procedural face

- **Agent:** Codex, as builder (`jobs/CODEX-BUILDER.md`, rules 1–10).
- **When:** after M3+M4 is ACCEPTED.

## Rows checked (manager, 2026-10-10)

An Opus row check of `research/20261006-M5-rows.md` reopened every cited address and call target. A correction wins
over the row.

**Adopted as written:**
- **R1–R7** (M5-005/017). The keyframe, audio and static-init RNG pointers are one object, 0x0103E8D0.
- **P1–P14** (M5-021/032).
- **T1–T5** (M5-022/037).
- **X1–X5** and the distortion rows (M5-031). The sequence iterator is a function-static shared across glitches.
- **H1** (M5-036 stays HARDWARE_ONLY).
- **Z1–Z3** (M5-038's factory; its caller's gating is higher-layer).
- **A1/A2** (M5-018). HasFramesLeft is called unconditionally and then discarded when an audio object exists.

**Adopted with corrections:**
- **F1–F10 (M5-003):**
  - the blend path never touches the distorter, so a mid-blend result keeps the default (none);
  - Interpolate's fifth bool (false at 0x004F9A36) is never read;
  - the diagnostic callback is the global `std::function ProceduralFace::ClipWarnFcn` (GLOB_DAT 0x0103ED50), installed
    by `EnableClippingWarning` (0x00584B48). Cite the installer and the static initialiser; an empty `std::function`
    throws `bad_function_call`;
  - F8's NaN restore reads the **destination** face's current field (0x005848FA).
- **G0–G7 (M5-011/014):**
  - **G1** (0x0058A5EC..0x0058A798):
    - the name is assigned before the Animations check;
    - existing entries are destroyed before the walk, so a duplicate group name redefines the group;
    - one failed entry sets a sticky result of 1, so the group returns failure but keeps its good entries;
    - an empty array returns 0.
  - **G4:** the RNG is AnimationGroup+0, passed from AnimationGroupContainer+0x28 (0x0058B4B8..0x0058B4BE), not the
    static keyframe RNG.
  - **G3 is contradicted.** At 0x0058C712 `isDouble` tests r6, which still holds the **Weight** value. That test is
    always true here, so the code always calls `asDouble(CooldownTime_Sec)` (0x0058C71C), and the zero branch at
    0x0058C868 is dead. **Re-extract** what the shipped jsoncpp `asDouble` (PLT 0x004AE92C) returns for a missing
    key, a bool or a string.
- **S1–S5 (M5-013):**
  - S5: firstScanLine == 0 selects the even-rows-cleared vector (+0); non-zero selects odd-cleared (+0xC);
  - the index reset on abort is lazy (S4), so build it lazy only;
  - S2 stays PARTIAL.
- **U1–U5 (M5-020):** add the libunity icall bindings for `UnityEngine.Random::RandomRangeInt` and `::InitState`, and
  the managed `Range(int,int)` path.
- **L1–L13 (M5-027/030/035):**
  - **L5 is partly contradicted.** If the buffer count +0x94 == 0, the function returns 0 at once (0x0057D122..0x0057D12A):
    there's no UpdateAmountToSend, no drain and no End check. A SendBufferedMessages failure (0x0057D17A) only logs
    `sWarningF`, and the End condition is still evaluated at 0x0057D1BC. There is also an entry warning at 0x0057D142.
  - **L6** (0x0057D354, 0x0057D296): an empty group name or a null clip sets the idle pointer +0x34 to null.
  - **L7** (0x0057D402..0x0057D446): `+0x44 += 60` runs on both the InitStream and the UpdateStream branch, whatever
    UpdateStream returns. +0x88 is updated only on the UpdateStream branch.
  - **L12:** the generic SetParam (0x0057C178) clamps to GetParamRange: `hi` when `hi <= v` (NaN included), then `lo`
    when `lo >= result`.
  - **L13:** 0x00513000 is the only +0x348 reader, and it feeds the debug `VizManager::SendRobotState`. Cite the actual
    Disable/EnableAnimTracks emitter.
  - **L4:** the side effects are `VizManager::SetText` and `CozmoContext::SetSdkStatus(2,…)` (0x0057CE92/0x0057CF2A).
    Viz is debug and out of scope; SDK status is unreachable without SDK mode.
  - L8–L11 hold. All their draws use streamer+0xA4; name that RNG.

**Still to extract:**
- **M5-039:** the ready-to-stream consumer. Nothing covers it yet. Its evidence: the ready store 0x0052C3A6, SyncTimeAck
  0x0053667E..0x005366AC, the streaming gate 0x00513BF2..0x00514470.
- **G3:** jsoncpp `asDouble`.

## Readiness

| Record | State |
| --- | --- |
| M5-005, M5-017 | buildable |
| M5-021, M5-031, M5-032 | buildable (M5-031 reads M7's DesiredFaceDistortion as an interface) |
| M5-022, M5-037 | buildable (M12 Path::Abort and M13 AbortDocking as interfaces) |
| M5-003 | buildable with the F corrections |
| M5-027, M5-030, M5-035 | buildable with the L corrections |
| M5-011 | blocked on the G3 re-extraction |
| M5-014 | blocked on G3, and on the manager's decision about the uninitialised head window (SD2) |
| M5-013 | the flag-false path is buildable; the record can't settle while S2 is open |
| M5-018 | the predicate is buildable; the record can't settle until M6 has a readiness record |
| M5-020 | blocked on the manager's decision about the runtime seed and the interleaved Unity draws (U2/U3) |
| M5-038 | the factory is buildable; the record stays RECOVERABLE_GAP |
| M5-036 | stays HARDWARE_ONLY |
| M5-039 | blocked on extraction |

## Extractions checked (manager, 2026-10-10): `research/20261010-m5-g3-m5039-l30-rows.md`

**G3, adopted.** The manager re-read the asDouble dispatch at 0x008E9A38: type byte +8, `bhi` above 5 throws, and a
`tbb` table for 0–5. The caller always calls `asDouble(CooldownTime_Sec)`.
| Value type | Result |
| --- | --- |
| null / missing key | 0.0 |
| int | `l2d` |
| uint | `ul2d` |
| real | the stored double |
| bool | 1.0 or 0.0 |
| string, array, object | `Json::LogicError("Value is not convertible to double.")` |

- The throw is uncaught up to `LoadNonConfigData`; above that is UNKNOWN. **Parked:** where it's finally caught. It
  isn't reachable with the shipped animation-group files, which use numbers.
- Correct M5-011 and M5-014's evidence: the default comes from the null Value through asDouble (0x008E9A82), not from
  the dead constant at 0x0058C868.
- The mood == 3 fall-through at 0x0058C5DA isn't covered. Leave it as a RECOVERABLE_GAP for later.

**M5-039, adopted with a manager correction.**
- **The gate:** `Robot::Update` reads +0x29 (0x0051410C) and +0x2A (0x00514112). When both are non-zero it calls
  `AnimationStreamer::Update(robot+0x60, robot)` (0x0057CE5C); otherwise it skips the call silently. A non-zero result
  logs "Robot %d had an animation streamer failure (%d)" and Update continues.
- **The precondition:** +0x34E (0x00513C5C, "Waiting for first full robot state to be handled").
- **The writers:** +0x29 is written by the ctor, `SyncTime` (0x00515228) and `HandleSyncTimeAck` (0x005366AC). +0x2A
  is written by the on-idle lambda (0x0052C3A6).
- **The extractor's "no caller of ProcessOnIdleCallbacks" is wrong.** The manager re-read both callers: NV `Update`
  tail-calls it at 0x006456EC, and `AddOneShotOnIdleCallback` at 0x00645C32, both through the veneer 0x008CCE6C. This
  is the M1-041 path. The flag is reachable.
- Fix M5-039's effect text: the gate is polled every Update. Narrow its evidence to the gate span.
