# Opus check of R-PLANNERS (2026-10-13)

- **Checked:** `research/20261013-planner-internals.md` (Codex), against libcozmoEngine.so. Read-only cozmo-extractor.
  Helper output is in `.scratch/plan/`.
- **Adopted:** by the manager on 2026-10-13. This is the checked source for job B-M13-1. Use the Codex report together
  with the corrections and additions below.

## The headline: the force=false branch is live (this contradicts M13-018 and the approved M13 inventory row R4-6)

1. PathComponent::Update, in status 3 → HandlePossibleOriginChanges. With the same origin and DidObjectsChange()==1
   (0x006498D6..0x006498EA) → RestartPlannerIfNeeded (veneer 0x008CCF3C → PLT 0x004B9D50).
2. RestartPlannerIfNeeded:
   - with a planner, and +0x47 clear, it calls slot +0xC with **r2 = 0** (0x00649C36), then `blx r7` (0x00649C3A);
   - **1** → +0x47 = 1, and the status stays 3;
   - **0** → the fallback, or Abort + status 6;
   - anything else → nothing.
3. Slot +0xC is ComputeNewPathIfNeeded in all four vtables: lattice 0x004FEB00, Face 0x004F35A8, Minimal 0x00503C18, base
   0x00508A90 (returns 2).
4. **Lattice:** try_lock(impl+0xE0) fails → 2. Otherwise StartPlanning(force) (0x004FEB7E stores A1).
5. **force=false** (0x004FEC2E..):
   - import, then FindClosestPlanSegmentToPose (0x004FEC50);
   - **distance ≥ 20.0:** log, and clear the old plan;
   - then PlanIsSafe (tolerance 40.0, from the closest index). Safe → **return 2, with no work** (0x004FF42E);
   - **unsafe → keep the safe prefix** as the old plan (0x004FECDE..0x004FECF6). The start is the prefix end, or the robot
     state if the prefix is empty (0x004FEEBC..0x004FEEDA);
   - DoPlanning later **appends** the new search to that prefix (xythetaPlan::Append, 0x00500540).
6. **The short planners on false:** +0x28 = 1, return 2 (Face 0x004F3806..0x004F380E, Minimal 0x00503DBA..0x00503DC2). So
   with a short planner, a restart does nothing.

**Stack text contradicted by this:**
- LatticePlanner.cs:914 and :967-970;
- M13-018's evidence ("not reachable from any caller ...");
- the M13 inventory, §4b line 2457 and row R4-6.

## The lattice planner's entry points and worker (all confirmed)

- **ComputePath:** the gates (0x004FE6A0..0x004FE704), then the helper with force=true (0x004FD318..0x004FD334).
- **StartPlanning:**
  - try_lock failure → sErrorF, return 0;
  - true → clear the old plan;
  - StartIsValid/GoalsAreValid failure → log, return 0;
  - PrepareForPlanning; F2 = 1;
  - **F5 == 0 (asynchronous, as in production)** → F0 = 1, F8 = 1, notify_all, return 1 (0x004FF432..0x004FF49E);
  - synchronous → DoPlanning inline.
- **F5** is written only by SetIsSynchronous, through the PlannerRunMode G2E message (tag 0x7B). The ctor subscribes only
  with an external interface (0x004FCF26..0x004FCF3C), and the app never sends it, so **planning is asynchronous in
  production**.
- **The worker** (0x005007E0..0x00500854), created unconditionally by the ctor (0x004FCF5C):
  - wait while !F0 && !F1;
  - on F0: F3 = 1, F0 = 0, DoPlanning under E0, F3 = 0;
  - loop until F1.

  The destructor sets F1, notifies and joins.
- **DoPlanning:**
  - F8 = 1, then a sleep loop, then Replan(30,000,000, &F2);
  - F8 = 0, 3 or 2 (0x00500216/0x00500212/0x0050058E);
  - a **transient 0** at 0x005004DE when the kept prefix's final state doesn't match the new plan;
  - **the return value is clobbered** (only F8 counts).
- **CheckPlanningStatus** = the plain F8 load (0x004FD192).
- **StopPlanning** clears F2 only if the thread [+0xDC] and F3 are both set (0x004FD1A2..0x004FD1CC).
- **GetCompletePath (lattice):** F8 == 0 → 0x004FFDC8; F8 == 1 → 0x004FFE2E; distance ≥ 20 → 0x004FFDCC; F8 = 0 at
  0x004FFE2A. AppendToPath (0x00855D90) is unread.
- **The ctor flags:** F0/F1/F3 = 0, F2 = 1, F4/F5 = 1/0, F8 = 0.

## The PathComponent planners (new)

- **The ctor:** +0x10 = FaceAndApproach; +0x18 = MinimalAngle; +8 = LatticePlanner if there is a context and a data
  platform, otherwise a copy of +0x10 with a warning (0x00648AE0..0x00648C54). The stack always has a data platform, so
  the lattice branch is the live one.
- **SelectPlannerHelper** (body 0x0064A530..0x0064A8DA; **M13-033 cited the wrong range**):
  - d² in f32/f64; ≥ 1600 (or NaN) → the lattice;
  - otherwise MinimalAngle iff d² > 1, heading ≤ 0x3F490FDB and the initial turn > 0x3E490FDB; otherwise Face;
  - the copies into +0x20/+0x24;
  - the `$data` = (int)roundf(d²) event;
  - +0x28 fallback = the lattice unless the current planner is the lattice.

## The short planners, IPathPlanner, and slot +0x20

- **FaceAndApproach** (0x004F35A8..0x004F38F6) and **MinimalAngle** (0x00503C18..0x005041B2): as in the Codex report,
  re-read (literals, turn speeds, tolerances, the backup line). Exceptions: the MinimalAngle compare operands
  (0x00503E7A..0x00503EA2) and logs were not re-read.
- **IPathPlanner:**
  - ComputeClosestGoalPose (0x005087AC..0x0050886E);
  - ComputePath(vector) (0x00508A60..0x00508A8E);
  - status (0x00508A94);
  - GetCompletePath (0x00508AB0..);
  - **the base internal returns 0 with no copy when +0x28 == 0** (0x0050924E..0x00509272). *(Codex correction.)*
- **ApplyMotionProfile:** the head, the speed magnitude from profile+0 (sign kept), accel/decel, and the forward pass,
  as confirmed. 0x00508C14..0x00508D90, 0x00508F48..0x0050906E and 0x00509070..0x005090DE were not re-read.
- **The stack's ToPath** uses `-ReverseSpeedMmps` for reverse primitives (LatticePlanner.cs:1334). Natively the magnitude
  comes from profile+0, and ApplyMotionProfile always runs in production.
- **Slot +0x20:** lattice 1 (0x00500AC3), others 0 (0x004F1A09). HandlePlanComplete's safety check goes through **slot
  0x18** (0x00649FB6) → IPathPlanner::CheckIsPathSafe 0x00509202 → slot 0x1C (lattice 0x004FF936) → PathIsSafe
  (0x00850E34, unread).

## The stack today (C1–C8)

- Synchronous `PlanTo` (DriveActions.cs:157-165).
- No worker, no F flags, no try_lock 2, and no F8 lifecycle.
- `StopPlanning() => RunFlag = false`, unguarded (LatticePlanner.cs:952).
- The false branch is not built.

## Unread (the dependencies)

- DidObjectsChange (0x00623020) and its writers;
- AppendToPath (0x00855D90);
- PathIsSafe (0x00850E34) and ApplyPathSegment (0x00850610);
- Path::AppendLine/AppendPointTurn/AppendSegment (0x0085CE88, 0x0085D3C8, 0x0085D46C);
- xythetaPlan::Append (0x00851870);
- FindClosestPlanSegmentToPose (0x00856310);
- the ComputePath goal loop (0x004FE7F6..0x004FE9E2);
- the ApplyMotionProfile sub-ranges above;
- the MinimalAngle compares.

**Which tick sees status 1, 2/3, or the transient 0** depends on thread scheduling: **HARDWARE_ONLY**.
