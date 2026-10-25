# Claude check: R-M11-MAP-MERGE (row 66, 080f13b7) and R-M11-OBSTACLE-WRITERS (row 67, df0d13b2)

Checked against libcozmoEngine.so (SHA256 02263c07…e1) and the adopted rows each answer indexes. Instructions are
spot-checked with capstone (Thumb).

## Row 66, map merge: PASS (feeds B-M11-20)

- **Nothing new had to be read.** The merge was read in `20261013-quadtree-rest.md` §5 and adopted by its Opus check
  (`20261013-quadtree-rest-check.md`, "M13-032, the merge"): leaf collection, MakeQuadXY, ApplyTo 0,2,1,3, the
  Rotation3d point, budget = ceilf(dist/1280f), and the null-old-map crash at 0x00688254 under M1-034.
- **New in this answer, and verified:**
  - the vtable binding: 0x0067DF76 `ldr r0,[r7]`, 0x0067DF78 `ldr r3,[r0,#0xc]`, 0x0067DF80 `blx r3`;
  - relocation 0x01030970, ARM_ABS32 to `MemoryMap::Merge` = 0x006813A9;
  - the wrapper: 0x006813A8 `adds r0,#8`, 0x006813AA `adds r1,#8`, 0x006813AC `b.w 0x8cd74c`.

  So it merges the old map **into** the new/current map.
- **Also matching:** 0x006855DA `vcvt.s32.f32`, 0x006855FC `cmp r0,#0` (type 0 skipped), 0x00685622
  `ldr.w r7,[r0,#0x84]` with 0x00685646 `mov r2,r7` and 0x00685648 `blx 0x4bcb10` (the leaf's own data to the normal
  Insert), and 0x00685520 `add.w r0,r5,#0x50`.
- **For the builder:**
  - The budget is computed once, from the full 3D transform. The reinsert uses the planar Pose2d with PR R14's yaw order.
  - The data is cloned through the normal Insert. No new timestamp.
  - Only a null **destination** has a defined branch (NullMapFound, create). A null source is the M1-034 skip.

## Row 67, obstacle writers: PASS, with its PARTIALs standing

- **Type 6 (ObstacleProx).**
  - The engine's only entry is AddProxObstacle 0x00624A0A: 0x00624A0C `movs r2,#0xe`, 0x00624A0E
    `blx 0x4b810c`.
  - It has **no caller.** OV100 adopted this, and the decomp index shows only the function and its stub. The stack has no
    ProxObstacle producer either (search of `cozmo-stack/src`).
  - So the branch at 0x0062259C `cmp r0,#0xe` → 0x00622646 is unreachable on both sides. The payload rows are verified
    (0x00684B62 `strb.w lr,[r0,#4]`, 0x00684B66 `strd r3,r1,[r0,#0xc]`) but there is nothing to build.
  - Queued: the MISSING at `Vision/BlockWorld.cs:1263` says "has no checked stack port". It should say the branch has no
    engine caller (M11-091/OV100), when M13-030 is next worked.
- **Type 7 (ObstacleUnrecognized).**
  - The family-6 mapper is negatively bound: tbb bytes at 0x0067F4D0 `04 04 3a 09 3a 0e 04`, 0x0067F500 `adr r3,#0x78`
    (literal false), 0x0067F542 `movs r0,#0`, and AddObservableObject's 0x0067ED16 `beq.w 0x67ee34`.
  - The flag byte at 0x00C87660 is 0.
  - No producer was found. The whole-binary census is PARTIAL, as the answer says, and nothing may be invented.
- **Type 10 explicit quad.**
  - FlagQuadAsNotInterestingEdges: 0x0067E6F0 `movs r2,#0xa`, 0x0067E71C `blx r3`, with the null-map fault at 0x0067E712
    `ldr r0,[r5]`.
  - Its live caller is VisitInterestingEdge S3, FlagVisitedQuad: 0x005E7E48 `ldm r3,{r1,r2,r3}`, 0x005E7E4A
    `blx 0x4b4e9c`, 0x005E80F2 `blx 0x4b4ea8`. The shipped config gives 80/140/120 mm.
  - This belongs to the VisitInterestingEdge build (M11-092, B-M11-16, HELD), not to a separate job.
- **Result:** the stack's map now has native writers for everything the engine can write on the drive-off and freeplay
  paths except the type-10 visited quad, which waits for VisitInterestingEdge.
