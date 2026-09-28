# I-M13 gap pass 1 (G1..G10) — read-only extraction

Job: re-analysis/jobs/I-M13.md, gap pass 1. Subsystem: M13-navigation.
Agent: opencode (DeepSeek), window 3. Date: 2026-09-28.
Binary: `resources/lib/armeabi-v7a/libcozmoEngine.so`. Addresses are file VAs of the ELF,
disassembled with capstone in Thumb mode. The Ghidra decompilation `re-analysis/decomp/libcozmoEngine/`
was used as a navigation aid only; every claim below was re-read from the instructions.

Note on the addresses the job gave: several are import veneers, not function bodies. In this
ELF the import veneers live in an ARM-mode region near 0x004Cxxxx and the bodies live near
0x0085xxxx. The job's `ParseMotionPrims 0x004CE840` is the ARM veneer; the body is at
**0x00852014**. `MotionPrimitive::Create 0x00853DD0`, `ReadMotionPrimitives 0x008529C0`,
`Replan` PLT 0x004A6850 (body `xythetaPlanner::Replan 0x00858598`) are as given.

The OBB is present in this clone at
`re-analysis/obb/assets/cozmo_resources/config/engine/cozmo_mprim.json`.

---

## G1 — M13-004, M13-001 (X2 row N2). Class: EXACT_SOURCE

### G1a. `xythetaEnvironment::ParseMotionPrims(Json::Value const&, bool)` — body 0x00852014

Signature from the prologue: r0 = this (env), r1 = Json root, r2 = bool `oldFormat`.
`mov r4,r1` (root), `mov sl,r0` (env), `mov r6,r2` (bool) at 0x0085201A..0x00852026.

| JSON key | read at | how used |
|---|---|---|
| `resolution_mm` | 0x0085203C `GetValueOptional<float>(root, "resolution_mm", env+0)`; key ptr built at 0x00852024 (`add r1,pc` -> 0x00C29ADC "resolution_mm") | stored env+0; then 0x008520AA..0x008520B4 `vdiv 1.0 / env[0]` -> env+4 |
| `num_angles` | 0x00852062 `GetValueOptional<unsigned int>(root, "num_angles", env+8)`; key at 0x0085204C | stored env+8 |
| `actions` | 0x008520BA `Json::Value::operator[]("actions")`; size checked non-zero (0x008520BE..0x008520C4); iterated 0x008520D8..0x008521AE | each element -> `ActionType::Import` at 0x00852144, pushed into vector at env+0x30 |
| `angle_definitions` | 0x008521C6 `operator[]("angle_definitions")` (key at 0x008521C0); iterated, each `asFloat` pushed to vector env+0x38 (0x008521FA..0x00852222) | count compared with env+8 at 0x0085222E..0x0085223C |
| `angles` | 0x00852248 `operator[]("angles")` (key at 0x00852244); `size()` compared with env+8 at 0x0085224E..0x0085225A | then `resize` env+0x14 (vector<vector<MotionPrimitive>>) to env+8 at 0x00852264 |
| `angles[i].prims` | 0x008522B0 `operator[](unsigned i)`, 0x008522BC `operator[]("prims")`; each prim 0x008522EC / 0x00852300 | if bool `oldFormat`==1 -> `MotionPrimitive::Import` (0x008522F4); else `MotionPrimitive::Create(prim, (unsigned char)i, env)` (0x0085230E) |

Error strings (puts) confirm the keys: "error: could not find key 'resolution_mm' or 'num_angles' in motion primitives" (0x00852092), "empty or non-existant actions section! (old format, perhaps?)" (0x008521B4), "error: could not find key 'angles' in motion primitives" (0x00852452). On a bad primitive: "Failed to import motion primitive" (0x0085237C). At the end it calls `PopulateReverseMotionPrims` (0x008524AC).

The second bool argument is the old-format switch; `ReadMotionPrimitives` passes **0** (see G1d).

### G1b. Per-action keys — `Anki::Planning::ActionType::Import` 0x00851A18

The per-action fields are read here, not in `MotionPrimitive::Create`:

| key | read at | destination (ActionType layout, 24 bytes) |
|---|---|---|
| `extra_cost_factor` | 0x00851A82 (key), 0x00851A96 `GetValueOptional<float>` | ActionType+0x00 |
| `index` | 0x00851AA4 (key), 0x00851AB8 `GetValueOptional<unsigned char>` | ActionType+0x04 |
| `name` | 0x00851AC6 (key), 0x00851ADC `GetValueOptional<string>` | ActionType+0x08 (std::string, 12 bytes) |
| `reverse_action` | 0x00851B32 (key), 0x00851B48 `GetValueOptional<bool>` | ActionType+0x14 (bool) |

The `ActionType` constructor 0x008519EC initialises +0x00=0, +0x04=0xFF, +0x08="<invalid>", +0x14=0.

### G1c. Per-primitive keys — `Anki::Planning::MotionPrimitive::Create` 0x00853DD0

Signature: r0 = this (MotionPrimitive), r1 = Json prim, r2 = unsigned char action_index, r3 = env.
Prologue `strb.w r6,[sb,#1]` at 0x00853DEC stores action_index at MotionPrimitive+1.

| key | read at | how used |
|---|---|---|
| `action_index` | 0x00853DE8 (key), 0x00853E06 `GetValueOptional<unsigned char>(..., sb+0)` | presence required; error "error: missing key 'action_index'" (0x0085400E) |
| `end_pose` | 0x00853E20 (key -> "end_pose"), 0x00853E30 `State::Import` | MotionPrimitive+8; error "error: could not read 'end_pose'" (0x0085401C) |
| `intermediate_poses` | 0x00853E3E (key), iterated; each `State_c::Import` (0x00853E8C) pushed to vector at sb+0x14 | |
| `extra_cost_factor` | 0x00853FFC `isMember("extra_cost_factor")`; if present -> error "ERROR: individual primitives shouldn't have cost factors. Old file format?" (0x00854006) | **rejected** per primitive |
| (per-action lookup) | 0x00854036..0x00854044: `ldrb [sb]` action_index, `ldr r1,[env+0x2C]` (actions vector begin), stride 24, `ldrb r7,[action+0x14]` | reads `reverse_action` of the action; flips a scale sign (0x0085404C..0x00854054, 0x008540F8..0x00854106) |
| `straight_length_mm` | 0x00854088 (key), 0x00854094 `asDouble` | abs*scale added to sb+4; then 0x008540CC `operator[]("straight_length_mm")`, 0x008540D0 `asFloat`; if non-zero -> `Path::AppendLine` (0x00854140) |
| `arc` | 0x00854144 `isMember("arc")` (key -> "arc") | if present, reads the arc block (below) and calls `Path::AppendArc` 0x0085425A |
| `sweepRad` | 0x00854160 (key), 0x00854166 `asDouble` | arc |
| `radius_mm` | 0x0085417C (key), 0x00854184 `asDouble` | arc |
| `centerPt_x_mm` | 0x008541CE (key), 0x008541D4 `asFloat` | arc |
| `centerPt_y_mm` | 0x008541E4 (key), 0x008541EA `asFloat` | arc |
| `startRad` | 0x0085420C `adr r1,#0x28c` -> 0x0085449C "startRad", 0x00854216 `asFloat` | arc |
| `sweepRad` (again) | 0x00854226 (key), 0x0085423A `asFloat` | arc |
| `turn_in_place_direction` | 0x00854264 `isMember("turn_in_place_direction")` (key -> "turn_in_place_direction"), 0x00854278 `asDouble` | `Path::AppendPointTurn` 0x0085431A |

The arc key strings were read from the literal pool at 0x00854480 ("arc"), 0x0085449C ("startRad"),
and the annotated key pointers (0x00C29CD3 "sweepRad", 0x00C29CDC "radius_mm", 0x00C29CE6 "centerPt_x_mm",
0x00C29CF4 "centerPt_y_mm").

### G1d. `ReadMotionPrimitives` 0x008529C0 — file open

- 0x00852A44 `adr r1,#0x1b8` -> mode string at 0x00852C00 = **"r"**.
- 0x00852A48 `blx fopen`.
- 0x00852A62 `Json::Reader::parse(stream, root, true)`.
- 0x00852B06 `blx ParseMotionPrims` with `movs r2,#0` -> **oldFormat = false**, i.e. the shipped file goes through `MotionPrimitive::Create`.
- Returns `ParseMotionPrims`'s result (0x00852B0A `mov r7,r0`).

### G1e. "9 actions expected"

There is **no hard-coded 9** in `ParseMotionPrims`, `ReadMotionPrimitives`, `MotionPrimitive::Create`
or `xythetaEnvironment::Init`. The only count assertions are `angle_definitions.size() == num_angles`
and `angles.size() == num_angles` (0x0085223C, 0x0085225A). `MotionPrimitive::Create` indexes
`env->actions[action_index]` with a 24-byte stride and no bounds check (0x00854036..0x00854044).
The number 9 is the **shipped asset's** action count (G2); it is a fact about the OBB, not an engine constant.

### G1f. NEW: `xythetaEnvironment::Init` 0x008528A8 hard-codes 16 angles

`Init(char const*)` calls `ReadMotionPrimitives` (0x008528AE); on success it **overwrites** env+8
with `0x10` (0x008528B6..0x008528BA `movs r0,#0x10; str r0,[r4,#8]`), resizes the per-theta obstacle
table at env+0x44 to 16 (0x008528C0), and sets env+0xC = 2*pi/16 and env+0x10 = 1/(2*pi/16)
(0x008528C4..0x008528E6). So the planner's heading count is hard-coded 16 even though
`ParseMotionPrims` validates against the JSON `num_angles`. The asset's `num_angles` is 16, so they agree.

**G1 class: EXACT_SOURCE** for the loader and both parsers, and for the 16-heading override.
Record: M13-004 (schema) and M13-001 (loader). X2 row N2 is closed.

---

## G2 — M13-001, M13-004 asset side. Class: EXACT_SOURCE

File: `re-analysis/obb/assets/cozmo_resources/config/engine/cozmo_mprim.json`
Size 1,272,734 bytes. **sha256 = 4C79666BDD5F3F5FE6C7458B0E7D59ECBCCDB2AB3C12F1844D68BCC57887431D**

Top-level keys: `actions`, `angle_definitions`, `angles`, `num_angles`, `resolution_mm`.
`resolution_mm` = 10.0. `num_angles` = 16. `angle_definitions` = 16 values:
0.0, 0.4636476090008061, 0.7853981633974483, 1.1071487177940904, 1.5707963267948966,
2.0344439357957027, 2.356194490192345, 2.677945044588987, 3.141592653589793,
-2.677945044588987, -2.356194490192345, -2.0344439357957027, -1.5707963267948966,
-1.1071487177940904, -0.7853981633974483, -0.4636476090008061.

`actions` has exactly **9** entries:

| index | name | extra_cost_factor | reverse_action |
|---|---|---|---|
| 0 | "short straight" | 1.0001 | (absent -> false) |
| 1 | "long straight" | 1.0 | (absent) |
| 2 | "slight left" | 1.0 | (absent) |
| 3 | "slight right" | 1.0 | (absent) |
| 4 | "hard left" | 1.0 | (absent) |
| 5 | "hard right" | 1.0 | (absent) |
| 6 | "inplace left" | 2.0 | (absent) |
| 7 | "inplace right" | 2.0 | (absent) |
| 8 | "backwards short straight" | 1.2 | **true** |

`angles` has 16 entries; each has 9 primitives (action_index 0..8 in order) -> **144 primitives total**.
Every primitive has `action_index`, `end_pose`, `intermediate_poses`, `straight_length_mm`, and either
an `arc` block (actions 2..5) or `turn_in_place_direction` (actions 6..7) or neither (actions 0,1,8).

Angle 0 (heading 0.0) block, verbatim:

| action | straight_length_mm | arc / turn_in_place_direction |
|---|---|---|
| 0 | 10.0 | - |
| 1 | 50.0 | - |
| 2 | 7.639320225002111 | arc { centerPt_x_mm 7.639320225002111, centerPt_y_mm 94.72135954999578, radius_mm 94.72135954999578, startRad -1.5707963267948966, sweepRad 0.4636476090008061 } |
| 3 | 7.639320225002111 | arc { centerPt_x_mm 7.639320225002111, centerPt_y_mm -94.72135954999578, radius_mm 94.72135954999578, startRad 1.5707963267948966, sweepRad -0.4636476090008061 } |
| 4 | 5.857864376269046 | arc { centerPt_x_mm 5.857864376269046, centerPt_y_mm 34.14213562373096, radius_mm 34.14213562373096, startRad -1.5707963267948966, sweepRad 0.7853981633974483 } |
| 5 | 5.857864376269046 | arc { centerPt_x_mm 5.857864376269046, centerPt_y_mm -34.14213562373096, radius_mm 34.14213562373096, startRad 1.5707963267948966, sweepRad -0.7853981633974483 } |
| 6 | 0.0 | turn_in_place_direction 1.0 |
| 7 | 0.0 | turn_in_place_direction -1.0 |
| 8 | -10.0 | - |

The arc blocks are stored already rotated for the starting heading. Example (heading index 4,
angle pi/2, action 2 "slight left"): centerPt_x_mm = -94.72135954999578, centerPt_y_mm =
7.639320225002117, radius_mm = 94.72135954999578, startRad = 0.0, sweepRad = 0.46364760900080615.
This matches M13-004's evidence "the same slight left at heading 90 has its centre at
(-94.721, 7.639) with startRad 0". The values are JSON doubles; some straight_length_mm are
tiny floating-point residue (e.g. -8.88e-15 at angle 1 action 4), which the engine reads as double
(0x00854094) before the abs/scale step.

**G2 class: EXACT_SOURCE.** The X2 "OBB absent" limitation is closed. Records M13-001 and M13-004.

---

## G3 — M13-007 (X2 row N26). Class: EXACT_SOURCE

All five call sites re-read:

- `BlockConfigurations::StackOfCubes::BuildTallestStackForObject`
  - 0x0061928C `movt r7,#0x41f0` -> r7 = 0x41F00000 = **30.0**; passed as r2 at 0x0061929E
    `blx BlockWorld::FindObjectOnTopOrUnderneathHelper`. (5th stack arg = 1 at 0x0061929A.)
  - 0x00619344 `movt r5,#0x41f0` -> r5 = 0x41F00000 = **30.0**; passed as r2 at 0x00619358.
    (5th stack arg = 0 at 0x00619348/0x00619356.)
- `BlockWorld::UpdatePoseOfStackedObjects` 0x00621908 `movt r2,#0x4170` -> 0x41700000 = **15.0**;
  call at 0x0062190C.
- `DockingComponent::CanInteractWithObjectHelper` 0x0063C728 `movt r2,#0x4170` -> **15.0**;
  call at 0x0063C730.
- `CarryingComponent::SetObjectAsAttachedToLift` 0x00632F0C `movt r2,#0x4170` -> **15.0**;
  call at 0x00632F14.

Helper `BlockWorld::FindObjectOnTopOrUnderneathHelper` 0x0062601C:
- 0x00626026 `mov r4,r1` -> r1 = the object (first explicit argument);
- 0x00626038 `str r2,[sp,#0xa4]` -> r2 = the tolerance (second explicit argument), captured into
  the lambda at 0x006260CE/0x006260D8;
- 0x00626070 `blx GetDimInParentFrame<(char)90>(RotationMatrix3d)` -> r6 = the object's height;
- 0x0062607E `vmov.f32 s4,#0.5`; 0x00626086 `vmov.f32 s2,#-0.5`; 0x00626082 `cmp.w sl,#0`;
  0x0062608E `it ne`; 0x00626090 `vmovne.f32 s2,s4`; 0x00626094 `vmul.f32 s0,s0,s2`;
  0x00626098 `vldr s2,[r0,#0x28]` (parent Z); 0x0062609C `vadd.f32 s0,s2,s0`;
  0x006260A0 `vstr s0,[sp,#0x70]`. So the neighbour's centre is the parent Z plus +/-half the
  object's Z dimension; the tolerance is used inside the lambda passed to `FindLocatedObjectHelper`
  (0x0062610E).

**G3 class: EXACT_SOURCE.** Record M13-007. X2 row N26's deferred per-caller constants are now read.

---

## G4 — M13-005, NEW N8/N9/N10. Class: EXACT_SOURCE; the meaning of 0x01C9C380 is settled

### G4a. `LatticePlannerImpl::StartPlanning` 0x004FEB44

- 0x004FEB56 `mov r6,r2` -> r2 = the function's bool argument.
- 0x004FEB7E `strb.w r6,[sl,#0xA1]` -> **impl+0xA1 = the bool argument** (not a constant).
- 0x004FEC38 `movs r1,#1`; 0x004FEC3A `blx ImportBlockworldObstaclesIfNeeded(bool, ColorRGBA const*)`
  -> the import bool is **hard-coded 1**; r2 = `Anki::NamedColors::REPLAN_BLOCK_BOUNDING_QUAD`
  (0x004FEC32 `add r0,pc` -> GOT, 0x004FEC34 `ldr r2,[r0]`).

### G4b. `LatticePlannerImpl::DoPlanning` 0x00500090

- 0x00500098 `movs r0,#1; str.w r0,[fp,#0xF8]` -> status = 1.
- Sleep loop, 0x005000A2..0x005000EA:
  - 0x005000A2 `ldr.w r0,[fp,#0x108]` -> total wait in **milliseconds** (integer).
  - 0x005000B2 `movw r8,#0x4240; movt r8,#0xF` -> r8 = 0x000F4240 = 1,000,000 ns = 1 ms.
  - 0x005000C2 `ldr r0,[fp,#0x108]`; 0x005000C6 `subs r0,r0,r7` (remaining); 0x005000C8
    `cmp r0,#0xa`; 0x005000CC `it ge`; 0x005000CE `movge r4,#0xa` -> **r4 = min(remaining, 10)**.
  - 0x005000D4 `smull r0,r1,r4,r8` -> r4 * 1 ms; 0x005000DE `sleep_for`.
  - 0x005000BE `ldrb r0,[r6]` with r6 = fp+0xF2 -> the abort flag is checked every iteration.
  - So the wait is in **chunks of at most 10 ms**, not 1 ms. (The job's "1 ms chunks" is wrong.)
- 0x005000F2 `movw r1,#0xC380`; 0x005000FA `movt r1,#0x1C9` -> r1 = **0x01C9C380 = 30,000,000**;
  0x005000FE `mov r2,r6` (fp+0xF2, the volatile abort flag); 0x00500102 `blx Replan`.
- 0x00500106 `mov r4,r0` (save Replan result); 0x00500126 `cmp r4,#0`;
  0x00500132 `str r4,[sp,#8]`; 0x00500134 `it eq`; 0x00500136 `moveq r1,r0`
  with r0 = "robot.lattice_planner_failure" (0x00500112) and r1 = "robot.lattice_planner_success"
  (0x0050011C). So **r4 == 0 means failure**.
- 0x00500200 `ldr r0,[sp,#8]`; 0x00500202 `cbz r0,#0x00500216`; 0x00500216 `movs r0,#0`
  -> **return 0 when Replan == 0 (failure)**.
- 0x00500206 `GetPlan`; 0x0050020A `ldrd r1,r0,[r0,#8]`; 0x0050020E `cmp r0,r1`;
  0x00500210 `bne #0x0050021A`; 0x00500212 `movs r0,#3` -> **return 3 when Replan != 0 and the
  segment list is empty**.
- The normal path (Replan != 0, non-empty plan) ends at 0x0050058E `movs r0,#2` and
  0x00500590 `str.w r0,[fp,#0xF8]` -> **return 2**.
- There is no substitute path: every non-empty-plan branch builds the plan from `GetPlan()` and
  `xythetaPlan::Append` (0x00500540), never a locally constructed fallback.

### G4c. `xythetaPlanner::Replan` 0x00858598 and the argument

- 0x00858598 prologue; 0x008585AC `blx xythetaPlannerImpl::ComputePath(unsigned int, bool volatile*)`;
  0x00858642 `mov r0,r4` -> Replan returns ComputePath's value unchanged.
- `xythetaPlannerImpl::ComputePath` 0x008586A0: `mov sl,r1` (0x008586AE) is the argument;
  `str.w r2,[r8,#0x90]` (0x008586B4) stores the abort flag; `str.w sl,[sp,#0x28]` (0x0085883E);
  at 0x008588C6 `ldr r0,[sp,#0x28]`; 0x008588C8 `cmp sl,r0`; 0x008588CA `bhi #0x00858A96`;
  0x00858A96 warns "exceeded max expansions of %u, stopping" (string at 0x00858A9A)
  and returns 0. So **0x01C9C380 is the maximum number of state expansions (30,000,000), not a
  timeout and not a mode**, and the units are expansions.
- Return values: 0 on failure (CheckContextGoals 0x008586D4, CheckContextStart 0x00858798,
  InitializeHeuristic 0x0085882A, NoPlanFound 0x008589DE, exceeded max 0x00858B34); 1 on
  "No replan needed" (0x00858806) and after `BuildPlan` (0x00858A4C). So Replan's return is
  0 = failure, 1 = success, consistent with DoPlanning's string selection.

### G4d. Contradiction with X2 row N10

X2 row N10 says "If `Replan` returns non-zero, `DoPlanning` returns 0". The instructions say the
opposite: 0x00500202 is `cbz` (branch if zero) to the return-0 block, and 0x00500136 selects the
failure string when the saved Replan result is zero. **DoPlanning returns 0 when Replan returns 0
(failure), 3 when Replan returns non-zero with an empty plan, and 2 on success.** The job's G4/G10
premise repeats the X2 error. This is a contradiction of the X2 N10 row, not of M13-005's title
(M13-005's title "A lattice-planner failure sends no path" is still true).

**G4 class: EXACT_SOURCE.** Records M13-005 and the NEW N8/N9/N10 rows. The 0x01C9C380 meaning
is settled as max expansions.

---

## G5 — M13-014 (X2 row N22). Class: EXACT_SOURCE for the behaviour-changing path

Read from the instructions (the body is `0x005C2EA0..0x005C3E2A`; the callback is 0x005C3DCE).

### G5a. `BehaviorKnockOverCubes` body

- ctor 0x005C2EA0: calls `IBehavior::IBehavior` 0x005C2EA8; sets +0x11C/+0x120 = 0 (0x005C2EB8),
  +0x12C = -1 (0x005C2EBE), +0x134 = -1 (0x005C2EC8), +0x13C = -1, +0x140 = 0 (0x005C2ECC),
  +0x144 = &+0x148 (0x005C2EE2); +0x150/+0x154/+0x158/+0x15C/+0x160 = 0x23F (0x005C2EEE..0x005C2EFA);
  calls `LoadConfig` 0x005C2F02; subscribes to the message tag set at 0x005C2F16/0x005C2F1E.
- `LoadConfig` 0x005C2F70: reads AnimationTrigger `reachForBlockTrigger` -> +0x150 (key resolved
  at 0x005C30C0), `knockOverEyesTrigger` -> +0x158 (0x005C30DC), `knockOverSuccessTrigger` -> +0x15C
  (0x005C30F0), `knockOverFailureTrigger` -> +0x160 (0x005C3108), `knockOverPutDownTrigger` -> +0x154
  (0x005C3120), and int `minimumStackHeight` (default 3) -> +0x124 (0x005C306A/0x005C307A).
- `IsRunnableInternal` 0x005C314C: `StackOfCubes::GetStackHeight()` >= +0x124 (0x005C316E/0x005C317A).
- `InitInternal` 0x005C31A2: `InitializeMemberVars`; if +0xD9 (alwaysStreamline) or +0xD8 is set ->
  `TransitionToKnockingOverStack`, else `TransitionToReachingForBlock`.
- `InitializeMemberVars` 0x005C31D8: reaction lock, clear the object set at +0x144, +0x140 = 0
  (0x005C3218), copies +0x12C/+0x134/+0x13C from the stack configuration (0x005C321C..0x005C322A).
- `TransitionToReachingForBlock` 0x005C3254: object id from `BlockWorld::GetLocatedObjectByIdHelper`
  (0x005C3298); if null, calls vtable+0x90 (0x005C3414..0x005C341E); else builds a
  `CompoundActionSequential` and adds
  1. `TurnTowardsObjectAction(robot, objid, Radians(pi), false, false)` (0x005C32EE, pi = 0x40490FDB);
  2. if block x + 10 > 85.0 (0x005C3354..0x005C336C, 85.0 = 0x42AA0000) a
     `DriveStraightAction(robot, x - 85, 60.0, true)` (0x005C33A0, 60.0 = 0x42700000);
  3. `TriggerLiftSafeAnimationAction(robot, +0x150, 1, true, 0, 60.0, false)` (0x005C33E4);
  then `StartActing<BehaviorKnockOverCubes>(compound, &TransitionToKnockingOverStack)` (0x005C3408).
- `TransitionToKnockingOverStack` 0x005C34A8: if +0xD9/+0xD8 unset and +0x140 > 0 use the table
  entry at 0x005C36C4 (0.0), else 0x005C36BC (**pi/2 = 0x3FC90FDB**). Builds
  `DriveAndFlipBlockAction(robot, objid, false, 0.0, false, Radians(pi/2 or 0), false, 20.0)`
  (0x005C3548; 20.0 = 0x41A00000), sets say-name trigger 0xFD and no-name trigger 0xFE
  (0x005C3550/0x005C3558), then a `CompoundActionSequential` with
  1. `TurnTowardsObjectAction(robot, objid, Radians(pi), false, false)` (0x005C35A6),
  2. the `DriveAndFlipBlockAction` (0x005C35CC),
  3. `WaitAction(robot, 0.5)` (0x005C35FA, 0.5 = 0x3F000000);
  calls `PrepareForKnockOverAttempt` (0x005C3606) then `StartActing(compound, lambda 0x005C3DCE)`
  (0x005C3628).
- `PrepareForKnockOverAttempt` 0x005C3780: destroys and reinitialises the tipped-object set at +0x144
  (0x005C3786..0x005C37A4; +0x144=&+0x148, +0x148=0, +0x14C=0), `IncreaseScoreWhileActing(10.0)`,
  `SmartRemoveDisableReactionsLock("preparingToKnockOverDisable")`, then
  `SmartDisableReactionsWithLock` with the same string.
- `TransitionToBlindlyFlipping` 0x005C3840: `CompoundActionSequential` with
  1. `FlipBlockAction(robot, objid)` then `SetShouldCheckPreActionPose(false)` (0x005C387C/0x005C3884),
  2. `WaitAction(0.5)`;
  `PrepareForKnockOverAttempt`; `StartActing(compound, &TransitionToPlayingReaction)` (0x005C38E0).
- `TransitionToPlayingReaction` 0x005C3908: sets `robot->[+0x264]->[+0x18]->[+0xC] = 1`
  (0x005C3946..0x005C394E); if the tipped-object set size at +0x14C != 0 -> `BehaviorObjectiveAchieved(0xD, true)`,
  `NeedActionCompleted(0)`, trigger +0x15C (knockOverSuccessTrigger); else trigger +0x160
  (knockOverFailureTrigger) (0x005C3950..0x005C3972); if not streamlined, plays
  `TriggerLiftSafeAnimationAction(robot, trigger, 1, true, 0, 60.0, false)` (0x005C39A4).
- `ClearStack` 0x005C3A30 resets +0x11C/+0x120 and the three ids; `UpdateTargetStack` 0x005C3A56
  stores the tallest stack; `HandleObjectUpAxisChanged` 0x005C3A98 inserts the tipped object into
  the set at +0x144 (this is what makes the set non-empty); `HandleWhileRunning` 0x005C3AE4 and
  `AlwaysHandle` 0x005C3BC8 dispatch on message tag 0x11 (ObjectUpAxisChanged).

### G5b. Knock-over callback 0x005C3DCE

- 0x005C3DD0 `ldr r1,[r1]` -> the ActionResult's first word; 0x005C3DD4 `ldr r4,[r0,#4]` -> `this`.
- if result == **0x03000010** (NoPreActionPoses): 0x005C3DDE `ldr r0,[r0,#8]` (robot);
  0x005C3DE0 `ldr r1,[r4,#0x12C]` (object id); 0x005C3DE4 `ldr r0,[r0,#0x264]`;
  0x005C3DE8 `ldr r0,[r0,#0x18]`; 0x005C3DEA `str r1,[r0,#0x70]` -> writes the block to
  `AIWhiteboard+0x70`; return.
- if `result >> 24 == 4` (Retry): 0x005C3E08 `ldr r0,[r4,#0x140]` (attempt count); 0x005C3E0E
  `cmp r0,#1`; 0x005C3E10 `bgt` -> `TransitionToBlindlyFlipping` (0x005C3E1C); else
  `TransitionToKnockingOverStack` (0x005C3E14). Then 0x005C3E20 `ldr r0,[r4,#0x140]`;
  0x005C3E24 `adds r0,#1`; 0x005C3E26 `str.w r0,[r4,#0x140]` -> **the count is incremented after
  either branch**.
- if `result >> 24 == 0` (success): 0x005C3DFC `ldr r1,[r0,#8]` (robot); 0x005C3DFE `mov r0,r4`;
  0x005C3E04 `b.w #0x008CC16C` (ARM veneer -> `BehaviorKnockOverCubes::TransitionToPlayingReaction`
  via the thunk at 0x004B30B4, confirmed by the decompilation `005c3dce.c` callee list).
- otherwise return.

### G5c. `DriveAndFlipBlockAction` ctor 0x0055E208

Read past its arguments. It calls `IDriveToInteractWithObject` at 0x0055E24C with a
`PreActionPose::ActionType` value 5 (0x0055E222), then creates a `FlipBlockAction(robot, objid)`
(0x0055E27C), calls a helper at 0x0055C9BC and conditionally a lambda at 0x005586AC, adds the
FlipBlockAction to the compound (0x0055E2F2), and sets the proxy tag from `FlipBlockAction+0x60`
(0x0055E312/0x0055E316). The trailing float (the 8th argument, 20.0 in KnockOverCubes) is **not
read** in the ctor.

### G5d. `IDriveToInteractWithObject` 0x0055B1F4

- 0x0055B2C8 constructs `DriveToObjectAction`; 0x0055B2EC adds it to a compound; 0x0055B358 adds
  that compound to the outer compound; 0x0055B372 adds the outer compound to the behaviour.
- 0x0055B37C `Radians(0)`; 0x0055B38C `operator>(maxTurnTowardsFaceAngle, Radians(0))`;
  0x0055B392 `bne #0x0055B45E` -> the whole block is skipped when maxTurn <= 0.
- Inside (maxTurn > 0):
  1. 0x0055B398 allocate 0x1D8; 0x0055B3C0 `TurnTowardsFaceAction(robot, 0, angle, false)`;
     0x0055B3C4..0x0055B3CC overwrite the vtable with `TurnTowardsLastFacePoseAction`'s;
     0x0055B3D8 add with r3=1 (ignore failure).
  2. 0x0055B3FA allocate 0x198; 0x0055B42E `TurnTowardsObjectAction(robot, objid, angle, false, false)`;
     0x0055B43C add with r3=1.
  So **both** a TurnTowardsLastFacePoseAction and a TurnTowardsObjectAction are added when
  maxTurn > 0. X2 row N22 mentions only the first; the second is a missing behaviour-changing step.

### G5e. `IBehavior::StartActing` 0x005BE0E4 and the lambda 0x005BF8F4

- 0x005BE0E4 wraps the `function<void(Robot&)>` in a `function<void(ActionResult)>` and calls the
  base `StartActing(IActionRunner*, function<void(ActionResult)>)` (0x005BE132).
- The lambda's `operator()` 0x005BF8F4: `ldr r1,[r0,#8]`; `adds r0,#0x10`; `ldr r1,[r1,#0x2C]`;
  `b.w #0x008CC08C`. It invokes the stored `function<void(Robot&)>` and **ignores the
  ActionResult** (the wrapper's bound target takes no result). This matches X2 row N22's
  "StartActing's lambda ignores it".

### G5f. The C# difference

The engine constructs a `TurnTowardsFaceAction` and then overwrites its vtable with
`TurnTowardsLastFacePoseAction`'s (0x0055B3C4..0x0055B3CC), i.e. the action that runs is
`TurnTowardsLastFacePoseAction`. The existing C# uses `TurnTowardsFaceAction` for the last face.
That is a real difference; the engine's last-face action is `TurnTowardsLastFacePoseAction`
(plus the extra `TurnTowardsObjectAction`).

### G5g. What remains

The behaviour-changing path is read. The only unread item is the logging helper `FUN_005c0ca8`
(0x005C0CA8), which is non-behavioural (it formats a channel message). The `+0x14C` field read by
`TransitionToPlayingReaction` is the size/count of the tipped-object `std::set` at +0x144, written
by the tree insert from `HandleObjectUpAxisChanged` and zeroed by `PrepareForKnockOverAttempt`;
it is the success/failure trigger selector. No other behaviour-changing step is unread.

**G5 class: EXACT_SOURCE** for the production path. Record M13-014 can move from
EQUIVALENT_IMPLEMENTATION to EXACT_SOURCE once its evidence is updated with the transitions above
and the two missing steps (the second TurnTowardsObjectAction; the +0x14C success/failure trigger
selector).

---

## G6 — M13-002 / NEW M13-016 (X2 rows N21/N23). Class: EXACT_SOURCE

### G6a. `FlipBlockAction` ctor 0x0055EC80 (constants 0x0055ECAA..0x0055ED12)

- 0x0055ECAA `blx IAction::IAction` with `r3 = 0xF` (0x0055ECA8) -> IAction type **0xF**.
- 0x0055ECF2 `movt r0,#0x41A0` -> r0 = 0x41A00000 = **20.0**.
- 0x0055ECF6 `movt r1,#0x4316` -> r1 = 0x43160000 = **150.0**.
- 0x0055ECFA `strd r1,r0,[r4,#0x12C]` -> **+0x12C = 150.0, +0x130 = 20.0**.
- 0x0055ECFE `movt r2,#0x4234` -> r2 = 0x42340000 = **45.0**.
- 0x0055ED02 `movt r3,#0x4220` -> r3 = 0x42200000 = **40.0**.
- 0x0055ED06 `mov.w r7,#-1`.
- 0x0055ED0A `add.w r0,r4,#0x134`; 0x0055ED0E `stm r0!,{r2,r3,r7}` ->
  **+0x134 = 45.0, +0x138 = 40.0, +0x13C = -1**.
- 0x0055ED10 `movs r0,#1`; 0x0055ED12 `strb.w r0,[r4,#0x140]` -> **+0x140 = 1**.

All confirmed exactly as the job stated.

### G6b. `AlignWithObjectAction` alignment-type table 0x005533E4..0x00553426

- 0x005533CC `cmp r6,#3`; 0x005533E4 `bhi #0x0055340E` (types > 3 skip the table).
- 0x005533E6 `tbb [pc,r6]` with the byte table at 0x005533EA:
  type 0 -> 0x00553402, type 1 -> 0x005533EE, type 2 -> 0x005533F4, type 3 -> 0x005533FC.
- type 0: 0x00553402 `vmov.f32 s2,#-27.0`; 0x00553406 `vmov s0,r8` (the argument);
  0x0055340A `vadd.f32 s16,s0,s2` -> **arg + (-27.0)**.
- type 1: 0x005533EE `vmov.f32 s16,#6.0` -> **6.0**.
- type 2: 0x005533F4 `movs r0,#2`; 0x005533F6 `strb.w r0,[r4,#0xBB]` -> **sets a flag at +0xBB = 2**
  (distance stays 0, s16 was initialised to 0 at 0x005533C8).
- type 3: 0x005533FC `vmov.f32 s16,#-15.0` -> **-15.0**.
- 0x0055340E `mov r0,r6`; 0x00553410 `blx GetPreActionTypeFromAlignmentType`;
  0x00553414 `vldr s0,[pc,#0x68]` -> 0x00553480 = 0xC1800005 = **-16.000001**;
  0x0055341C `vcmpe.f32 s16,s0`; 0x00553424 `it mi`; 0x00553426 `vmovmi.f32 s16,s2` (s2 = 0.0)
  -> **clamp to 0.0 when s16 < -16.000001**.
- 0x0055342A `str.w r0,[r4,#0xFC]` stores the pre-action type; 0x00553436 `vstr s16,[r4,#0x9C]`
  stores the distance.

### G6c. `GetPreActionTypeFromAlignmentType` 0x005532B8

- if r0 < 4: 0x005532C0 `adr r1,#0x9c` -> table at 0x00553360, `ldr.w r0,[r1,r0,lsl#2]`.
  Table: type 0 -> 1, type 1 -> 0, type 2 -> 1, type 3 -> 1.
- else: logs "AlignWithObjectAction.GetPreActionTypeByAlignmentType.InvalidAlignmentType"
  (0x005532DC) and returns **1** (0x00553320).

**G6 class: EXACT_SOURCE.** Records M13-002 (FlipBlock constants) and the NEW M13-016
(AlignWithObject alignment-type table). X2 rows N21/N23 closed.

---

## G7 — M13-013 / NEW M13-017 (X2 rows N19/N20). Class: EXACT_SOURCE

### G7a. `DriveOffChargerContactsAction`

- Constructor 0x00558228: 0x00558232 `movt r2,#0x4120` -> r2 = 0x41200000 = **10.0**;
  0x00558236 `movt r3,#0x41A0` -> r3 = 0x41A00000 = **20.0**; 0x0055823C `str r5,[sp]` with
  r5 = 0; 0x0055823E `blx DriveStraightAction(robot, 10.0, 20.0, false)`. Then +0x44 = 7
  (0x00558276/0x00558278). **In the constructor** (not Init): 0x0055827C `blx CozmoContext::IsInSdkMode`;
  0x00558280 `cmp r0,#1`; 0x00558282 `bne`; 0x00558286 `movs r1,#0`; 0x00558288
  `blx IActionRunner::SetTracksToLock(0)` -> only in SDK mode.
- `Init` 0x005582D0: 0x005582D2 `ldrb.w r1,[robot+0x338]`; 0x005582D6 `strb.w r1,[action+0x8B]`;
  0x005582DA `cbz r1` -> return 0 if not on contacts; else tail-call at 0x005582DC
  `b.w #0x008CB5EC` (the DriveStraightAction Init veneer).
- `CheckIfDone` 0x005582E4: 0x005582EA `ldrb [action+0x8B]`; if 0 -> return 0 (0x00558302).
  Else `DriveStraightAction::CheckIfDone` (0x005582F2); if it returns 0x1000000 -> return 0x1000000
  (still running, 0x005582FC). Else 0x00558306 `ldr r0,[robot]`; 0x00558308 `ldrb.w r1,[r0,#0x338]`;
  0x0055830C `movs r0,#0`; 0x0055830E `cbz r1,#0x0055834A` -> if off contacts return 0; if still on
  contacts, warn and 0x00558344 `movs r0,#9; movt r0,#0x400` -> **0x04000009**.

### G7b. `BehaviorDriveOffCharger`

- ctor 0x005C0980: 0x005C09B2 reads JSON key at 0x005C0A94 = **"extraDistanceToDrive_mm"**
  (`Json::Value::get`, default a double 0.0 at 0x005C09AC); 0x005C09C0 `asFloat` -> s16;
  0x005C09D4 `vldr s0,[pc,#0xd4]` -> 0x005C0AB0 = 0x42C00000 = **96.0**;
  0x005C09E2 `vadd.f32 s0,s16,s0`; 0x005C09E6 `vstr s0,[r4,#0x11C]` -> **+0x11C = 96.0 + json**.
- `IsRunnableInternal` 0x005C0B10: `ldrb.w r0,[r1,#0x34A]` -> **robot+0x34A**.
- `InitInternal` 0x005C0B18: `SmartDisableReactionsWithLock` (0x005C0B2A); +0x120 = 0
  (0x005C0B2E/0x005C0B30); if `robot->[+0x264]->[+0x30]->[+0x14] == 3` then
  `DrivingAnimationHandler::PushDrivingAnimations` (0x005C0B4A) and +0x120 = 1 (0x005C0B4E/0x005C0B50);
  if `robot+0x355` != 0 it logs "WaitForOnTreads" (0x005C0B5A..0x005C0B74) and does not transition;
  else `TransitionToDrivingForward` (0x005C0B8C).
- `TransitionToDrivingForward` 0x005C0BB8: if `robot+0x34A` != 0 (0x005C0BF4/0x005C0BF8), creates
  `DriveStraightAction(robot, +0x11C)` (0x005C0C02/0x005C0C08) and `StartActing(action, lambda)`
  (0x005C0C26). Drives the 96.0+extra distance.
- `StopInternal` 0x005C0D90: if +0x120 != 0, calls the DrivingAnimationHandler pop at veneer
  0x008CABCC (0x005C0D96..0x005C0DA0).
- `UpdateInternal` 0x005C0DA8: 0x005C0DB0 `ldrb [robot+0x34A]`;
  - if **not** on contacts (r0 == 0): 0x005C0DF4 if `[this+0x84]==0` -> `BaseStationTimer::GetCurrentTimeInSeconds()`
    stored at `robot->[+0x264]->[+0x18]+0x44` (0x005C0DFA..0x005C0E08), return **2**.
  - if on contacts: 0x005C0DB6 `ldrb [robot+0x355]`; if != 0 -> `StopActing(false,false)`
    (0x005C0DC4) and logs "WaitForOnTreads"; else if `[this+0x84]==0` -> `TransitionToDrivingForward`
    (0x005C0E18); return **1**.

Note: the job's "UpdateInternal StopActing on leaving the contacts" is not what the instructions
say. StopActing is called when the robot is **still** on the contacts (`robot+0x34A != 0`) and
`robot+0x355 != 0`; when the robot has left the contacts (`robot+0x34A == 0`) UpdateInternal
records the time and returns 2.

**G7 class: EXACT_SOURCE.** Records M13-013 and the NEW M13-017 (BehaviorDriveOffCharger).
X2 rows N19/N20 closed.

---

## G8 — M13-008 (X2 row N16). Class: EXACT_SOURCE

`MountChargerAction::ConfigureTurnAndMountAction`:

- 0x0054E52E `ComputeVectorBetween`; 0x0054E536 `atan2f`; 0x0054E54C
  `TurnInPlaceAction(robot, atan2, true)`.
- 0x0054E550/0x0054E556 `movw r1,#0x66f3; movt r1,#0x3fdf` -> r1 = **0x3FDF66F3 = 1.7453293**;
  0x0054E55A `SetMaxSpeed(1.7453293)`.
- 0x0054E55E/0x0054E564 `movw r1,#0x8d36; movt r1,#0x40a7` -> r1 = **0x40A78D36 = 5.2359877**;
  0x0054E568 `SetAccel(5.2359877)`.
- 0x0054E58A `Robot::GetLiftHeight`; 0x0054E592 `vldr s2,[pc,#0x14c]` = 0x42340000 = **45.0**;
  0x0054E59E `bpl` -> if lift >= 45 skip; else 0x0054E5B2 `movt r2,#0x4234` (45.0),
  0x0054E5BE `movt r3,#0x40A0` (5.0), `str r7,[sp]` (0.0), 0x0054E5C6
  `MoveLiftToHeightAction(robot, 45.0, 5.0, 0.0)`.
- 0x0054E5FC `movt r2,#0xc2f0` -> 0xC2F00000 = **-120.0**; 0x0054E600 `movt r3,#0x41f0`
  -> 0x41F00000 = **30.0**; `str r4,[sp]` (0.0); 0x0054E608
  `DriveStraightAction(robot, -120.0, 30.0, false)`.
- 0x0054E60E `strb.w r7,[r6,#0x8B]` (copies +0x80); 0x0054E60C/0x0054E612
  `GOT->{vtable(BackupOntoChargerAction)}`; 0x0054E618 `str r0,[r6]` -> **vtable overwritten**.

The job's "max speed 1.7459" is slightly off: 0x3FDF66F3 = **1.7453293 rad/s = 100 deg/s**.

### Units

`TurnInPlaceAction::SetMaxSpeed` 0x00545C08:
- 0x00545C0E `vldr s4,[pc,#0xc8]` -> 0x00545CD8 = 0x40A78D36 = 5.2359877 (300 deg/s);
  0x00545C18 `vabs s2,s0`; 0x00545C1C `vcmpe s2,s4`; 0x00545C24 `ble` -> if |maxSpeed| <= 5.236 keep.
- if larger: 0x00545C26 `vldr s2,[pc,#0xb4]` -> 0x00545CE0 = 0x42652EE1 = **57.29578 (180/pi)**;
  0x00545C2E `vmul.f32 s0,s0,s2` (rad -> deg); the warning at 0x00545C36 is
  **"Speed of %f deg/s exceeds limit of %f deg/s. Clamping."** with the limit 0x4072C000 = 300.0;
  then clamps to 0x40A78D36.
- So **SetMaxSpeed takes rad/s** (the warning converts to deg/s only to display), and the limit is
  300 deg/s = 5.236 rad/s.
- `SetAccel` 0x00545D14 stores r1 (rad/s^2) at +0xC8, defaulting from +0x7C when r1 == 0
  (0x00545D22/0x00545D24). The constructor 0x00545A26..0x00545A44 sets +0x78 = 0x40A78D36
  (default max speed 5.236 rad/s), +0x7C = 0x41200000 (10.0 rad/s^2), +0x80 = 0x41C80000 (25.0).
- The units are owned by `TurnInPlaceAction` (the control layer), not by the mount.

**G8 class: EXACT_SOURCE.** Record M13-008; the units question is settled as rad/s and rad/s^2.

---

## G9 — M13-009 (X2 row N13). Class: EXACT_SOURCE

`Charger::Charger` 0x004E9B6C:

- 0x004E9B98 `movt r2,#0x42c0` -> 0x42C00000 = **96.0**; 0x004E9BAC `movt r1,#0x42a0`
  -> 0x42A00000 = **80.0**; 0x004E9BB4 `strd r2,r1,[r4,#0xF0]` -> +0xF0 = 96.0, +0xF4 = 80.0.
- 0x004E9BB0 `movt r0,#0x41f8` -> 0x41F80000 = **31.0**; 0x004E9BB8 `str.w r0,[r4,#0xF8]`.
- marker pose: 0x004E9BBC/0x004E9BC2 `movw r1,#0xfdb; movt r1,#0xbfc9` -> 0xBFC90FDB = **-pi/2**;
  `Radians::Radians` 0x004E9BC6; `Z_AXIS_3D` 0x004E9BCA; translation at 0x004E9BD6 `movt r2,#0x42ac`
  -> 0x42AC0000 = **86.0** (sp+0x14), 0 at sp+0x18, 0x004E9BE2 `movt r2,#0x41b0` -> 0x41B00000 = **22.0**
  (sp+0x1c); `Pose3d` 0x004E9C02.
- marker id: 0x004E9C16 `movs r1,#2`; 0x004E9C1C `strh.w r1,[sp,#8]`.
- **size**: 0x004E9C28 `str r1,[sp,#0x18]` with r1 = 0x41A00000 = **20.0** (y);
  0x004E9C30 `str r1,[sp,#0x14]` with r1 = 0x41D80000 = **27.0** (x). 0x004E9C36 `add r3,sp,#0x14`
  -> the pointer passed to `AddMarker` (0x004E9C38) is `sp+0x14`, whose first float is **27.0 (x)**
  and whose second is **20.0 (y)**.
- 0x004E9C38 `blx ObservableObject::AddMarker(short const&, Pose3d const&, Point<2,float> const&)`;
  result stored at +0xFC (0x004E9C3C).

So the size is **x = 27.0, y = 20.0**, and the pointer to `AddMarker` is `sp+0x14` (the 27.0 word).
M13-009's "20 x 27" reads x=20,y=27, which the source does not say; the source's order is x=27,y=20.
(X2's section 2 already flagged this.)

**G9 class: EXACT_SOURCE.** Record M13-009.

---

## G10 — M13-005 (X2 row N10). Class: EXACT_SOURCE; the job's premise is contradicted

`LatticePlannerImpl::DoPlanning` 0x00500090:

- 0x00500102 `Replan(0x01C9C380, abortFlag)`; 0x00500106 `mov r4,r0`; 0x00500132 `str r4,[sp,#8]`.
- 0x00500200 `ldr r0,[sp,#8]`; 0x00500202 `cbz r0,#0x00500216`; 0x00500216 `movs r0,#0` ->
  **return 0 when Replan returns 0**.
- 0x00500206 `GetPlan`; 0x0050020E `cmp r0,r1` (segment list begin vs end); 0x00500210 `bne #0x0050021A`;
  0x00500212 `movs r0,#3` -> **return 3 when Replan is non-zero and the plan's segment list is empty**.
- Normal success returns **2** (0x0050058E/0x00500590).
- There is no substitute path in `DoPlanning`: the non-empty-plan branch appends the engine's own
  `GetPlan()` result (0x00500540) and returns; nothing constructs a fallback plan.

**Contradiction:** X2 row N10 and the job's G4/G10 say "returns 0 when Replan is non-zero". The
source says 0 is returned when Replan returns **zero** (failure); the non-zero/empty case returns 3.
0x00500136 `moveq r1,r0` (failure string) also confirms Replan==0 is the failure. This is a
citation/semantics defect in the X2 row, not a change to M13-005's title ("A lattice-planner failure
sends no path" remains correct).

**G10 class: EXACT_SOURCE.** Record M13-005; X2 row N10 must be corrected.

---

## Contradictions of existing records / prior rows

1. **X2 row N10** (and the job's G4/G10 premise): "returns 0 when Replan is non-zero" is backwards.
   0x00500202 is `cbz` -> return 0 when Replan == 0 (failure); return 3 when Replan != 0 and the
   plan is empty; return 2 on success. Citation: 0x00500106, 0x00500136, 0x00500202, 0x00500212,
   0x0050058E.
2. **M13-009 / X2 row N13**: "sized 20 x 27" reads x=20, y=27. The source stores 27.0 at sp+0x14
   (x, the pointer passed to AddMarker) and 20.0 at sp+0x18 (y). Citation: 0x004E9C28 (20.0),
   0x004E9C30 (27.0), 0x004E9C36 (`add r3,sp,#0x14`), 0x004E9C38 (`AddMarker`). (Already noted by X2.)
3. **X2 row N16**: the mount's turn-in-place max speed is 0x3FDF66F3 = 1.7453293 rad/s (100 deg/s),
   not 1.7459. Citation: 0x0054E550/0x0054E556, 0x0054E55A.
4. **X2 row N22**: `IDriveToInteractWithObject` adds **two** actions when maxTurn > 0, not one:
   a `TurnTowardsLastFacePoseAction` (0x0055B3C4..0x0055B3D8) **and** a `TurnTowardsObjectAction`
   (0x0055B42E/0x0055B43C). The X2 row mentions only the first. This is a missing
   behaviour-changing step.
5. **X2 row N19**: the `SetTracksToLock(0)` SDK-mode clear is in the **constructor** (0x0055827C/
   0x00558288), not in `Init`; `Init` 0x005582D0 copies `robot+0x338` to `action+0x8B`. X2 attributed
   the copy and the clear to `Init` together.
6. **Job G7**: "UpdateInternal StopActing on leaving the contacts" is not what the source says.
   `StopActing` is called when still on the contacts (`robot+0x34A != 0`) and `robot+0x355 != 0`
   (0x005C0DB0..0x005C0DC4); when the robot has left (`robot+0x34A == 0`) it records the time and
   returns 2 (0x005C0DF4..0x005C0E0C).
7. **Job G4**: "the sleep loop (impl+0x108 ms in 1 ms chunks)" is wrong: the loop sleeps
   `min(remaining, 10)` ms per iteration (0x005000C8..0x005000CE), with r8 = 1,000,000 ns/ms.
8. **Job G1**: `ParseMotionPrims` is not at 0x004CE840; that is the ARM import veneer. The body is
   0x00852014. Likewise `MotionPrimitive::Create` is 0x00853DD0 and `ReadMotionPrimitives`
   0x008529C0 (those two are bodies).

## Records whose evidence is too weak to keep their status

- **M13-002** (EXACT_SOURCE): evidence is two bare addresses (`FlipBlockAction 0x0055EC80`,
  `charger 0x004E9B6C`); it does not carry the instructions. The behaviour is real and now fully
  read (G6, G9), but the record should cite the instructions.
- **M13-005** (EXACT_SOURCE): evidence is three prose sentences with no address. The behaviour is
  confirmed (G4/G10) but the record cites nothing; and its prose is contradicted on the Replan
  condition.
- **M13-014** (EQUIVALENT_IMPLEMENTATION): can be raised to EXACT_SOURCE now that the path is read
  (G5), but its evidence must add the second `TurnTowardsObjectAction`, the `+0x14C` success/failure
  trigger selector, and the corrected `TransitionToPlayingReaction` (0xD objective / NeedActionCompleted).

## Open questions for the manager

1. **M13-005 prose.** The record and X2 row N10 state the Replan condition backwards. Decide
   whether to rewrite M13-005's evidence and keep EXACT_SOURCE, or to re-cite it. The title still
   holds; the failure/no-substitute-path claim is correct.
2. **M13-014 status.** With G5 read, it can be EXACT_SOURCE. Confirm whether the two newly found
   steps (second `TurnTowardsObjectAction`; `+0x14C` trigger selector) get their own records or are
   folded into M13-014's evidence.
3. **M13-016 / M13-017 numbering.** G6's AlignWithObject table and G7's BehaviorDriveOffCharger are
   new records; confirm the numbers.
4. **`num_angles` override (G1f).** `xythetaEnvironment::Init` hard-codes 16 headings and overrides
   the JSON value. This is a behaviour-changing step not in M13-001/M13-004; decide whether it
   becomes a record.
5. **`FUN_005c0ca8`** (the channel logger used by the behaviours) was not read; it is non-behavioural.
   Confirm it does not need a record.
6. **M13-008 / M13-012** both cite the mount; the unit question is now settled inside
   `TurnInPlaceAction`, which is the control layer. Confirm which record owns the rad/s vs rad/s^2
   statement.

*Read-only extraction. Nothing outside this report file was changed.*
