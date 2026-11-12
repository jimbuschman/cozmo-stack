# M11-043 settled EXACT_SOURCE; CARRIED-ONTOP-BINDING-CENSUS-1 merged (manager, 2026-11-12)

## The worker's job: CARRIED-ONTOP-BINDING-CENSUS-1

Merged as 7042b9fa (worker tip 54ae5d4f, full gate 8408/8408 on 29ad6aea). Handoff:
`re-analysis/jobs/trial-m13/HANDOFF-CARRIED-ONTOP-BINDING-CENSUS-1.md`.

**Re-read in the `.so`:**

| Address | What | Matches the job |
| --- | --- | --- |
| 0x00633F88..0x00633F9E | IsCarryingObject: `id == [+8]` → 1; else `id == [+0x14]` | yes: `IsCarryingObjectId` |
| 0x00633F6C..0x00633F86 | UnSetCarryObject: `[+8]` → UnSetCarryingObjects(r1=0); else `[+0x14]` → (r1=1); else return | yes: `CarryingComponent.UnSetCarryObject` |
| 0x00505E02..0x00505E0A | UpdatePoseInInstance calls IsCarryingObject through PLT 0x004A6FAC | yes: the shared binding |
| 0x00505FAE..0x00505FBC | `[robot+0x284]`, then the call 0x004A6FDC (UnSetCarryObject) | yes: wired in production |
| 0x00628E52..0x00628E72 | lambda1: IsCarryingObject; true → operator= (pose kept, parent kept) | yes |
| 0x00628E78..0x00628E88 | else GetWithRespectTo; a failure → 0x00628FB2 (OriginFail) | yes (built earlier) |

**Verdict: PASS.** The shared binding now matches every PLT 0x004A6FAC caller the stack builds. The three inline
carried-only reads keep the carried ID only (`BlockWorld.CarriedOnly`). M4-009's code is brought back to its own
evidence (0x0053417C/0x0053498C use the two-ID body).

The NotLastOwner log at the old-origin erase is a log only; the node's survival is what the engine does (the wrapper
releases only its own reference, 0x0084515E), and the stack's garbage-collected node survives the same way. The
original's error machinery (_errG, the call-stack dump, the logger provider) is not copied (AGENTS.md "Failures that
can happen here").

## M11-043: settled EXACT_SOURCE

**The path:** Robot::LocalizeToObject 0x00515992 → UpdateObjectOrigins 0x00620534..0x006207E4 → lambda1
0x00628E18..0x006293F6 per old-origin object → the conditional erase 0x00620642..0x0062064C → AddInExistingPose
0x006206E0 → BroadcastLocatedObjectStates 0x006206E6 → the force flag 0x006206EA..0x006206F0.

**Every reachable step is built and matches.** The census (`re-analysis/jobs/trial-m13/CENSUS-M11-043.md`) found one
difference, L2 (the carried test); this job built it. The rest:
- U2..U9 and L1, L3..L15 matched in the census;
- the tail (0x53, then the force flag, before X11 0x0051599E) is pinned by the production test
  `LocalizeCrossOriginTests.LCO_Production_ADelocalizedRobotThatSeesAKnownCubeRelocalizesIntoItsOrigin`
  (LCO-TAIL-PRODUCTION-TEST-1, merged ecdbc384);
- the carried-stack move is pinned by
  `M11RVisBuildTests.M11_043_ACarriedStackKeepsTheTopCubeOnTheOldCarriedNodeWhichFollowsTheLift` (unit).

**Recorded, not built:**
- U1, the UnknownOriginID guard (0x00620554..0x00620570): a branch our system never produces. The old ID is read
  before Rejigger, the new one after a successful Rejigger that requires ContainsOriginID, and origin IDs start at 1
  (0x00847934). Manager exclusion, 2026-11-12.
- L9, the throw for a non-unique object that is not a MarkerlessObject: reachable only through SDK custom objects.
  Not supported: SDK mode, operator 2026-10-09.
- Log formats (L10, L11, L12, L15, U1): best effort, never blocking.

**Inventory corrections applied at settlement** (standing authorisation for cited corrections), as listed in
CENSUS-M11-043.md:
1. Authority: lambda1's carried test (IsCarryingObject 0x00633F88, carried or on top) and the GetWithRespectTo /
   OriginFail branch.
2. Authority: CopyWithNewPose is SetPose with the source's fromDistance and PoseState, then the confirmer entry,
   returning 0.
3. Authority: the tail order is AddInExistingPose 0x006206E0, BroadcastLocatedObjectStates 0x006206E6, then the force
   flag 0x006206EA..0x006206F0.
4. Evidence[0]: widened to the full ranges with every step's address.

## Left for later

- **M11-009:** its evidence cites only the message read at 0x00533E54, not the guards 0x0053411E/0x0053492C. Correct
  it when M11-009 is settled.
- **M11-007's `unresolved`:** says `BlockWorld.DockingObjectId` is unwired; it is wired (Docking.cs). Stale note.
- **CheckForCollisionWithRobot (M11-042):** unbuilt; when built, its carried test uses the two-ID body (0x0062597A).
