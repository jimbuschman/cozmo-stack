# CM13-04 handoff — importer origin pose

**READY FOR CLAUDE.** Independent source/code check PASS; full gate **6306/6306 PASS,0failed/0skipped,6m55s**. Branch codex-m13-manager-2, base origin/main d2967c92 (R1 externally reviewed). Code f55fb6ee0857c13813a8f42b29853b1f67ed1af6; full gate ran on ec267402 before rebase. Complete code/record/inventory/tool trees are identical after rebase (git diff exit0), so the recorded gate fingerprint is unchanged. No manifest/status/approval/acceptance or REVIEW.md change; no hardware run or dependency on one.

The importer now applies the source pose round trip and carrying-dependent origin shift, replacing its direct-heading/unshifted robot quad. Scope: LatticePlanner.cs, NativeLatticePlannerRuntime.cs, NativePlannerSet.cs, standalone ManipTool input, M13OriginImportTests.cs and explicit-input adaptations in four existing test files. Canonical RobotQuad/ObjectFootprint and existing map query/cache remain.

Every behavior-changing claim for spot-check:

| Claim | Native evidence / implementation |
|---|---|
| Per heading, construct input Radians exactly once, Z-axis/zero-translation pose, and distinct default output |004FE006..004FE042,004FE06C..70; Radians0084C832..936; LatticeEnvironment.TryImportRobotQuad |
| Use float half-angle/trig widened into normalized double quaternion, then source narrowed-matrix yaw selection; no raw-heading or Mat3 shortcut |0084A526..582,00849DC2..E4E,0084AA24..AAB4; existing checked Rotation3d/Pose3d helpers |
| Read current native carrying owner/raw word after both pose locals, for every heading; construction-time Docking fallback is insufficient |Robot+284 store0050FE44/48, ctor+8 sentinel00632A6A/6E;00517F6C/78; NativePlannerSet.ReadCarryingState/provider passed through runtime |
| Only rawFFFFFFFF chooses+20; all other words including0/80000000 choose literal negative zero, not a nullable Boolean |00517F70..7E, literal00517F90=80000000; OriginOffset |
| Assign pose, get input quaternion yaw; separate f32 cos/sin products plus translation, force z=+0, retaining rotation/parent |00517ED4,00517EE4,00517EF4..00517F3E; TryImportRobotQuad; tail00517F8A via008CAE4C/PLT004A7C54 |
| Bounding quad gets yaw independently, uses unchanged canonical corner bits/padding, separate float rotation, then translation-first addition |00514D8A/92,0084A1CC..E6,00514DB2..00514EC8,004E68E6..F4; existing ObjectFootprint/RobotQuad called with constructed output |
| Both replacement and incremental tool imports feed this quad into existing polygon/C-space expansion; heading order/penalty retained; output disposed before input |004FE0AC/C4/CE/E2,004FE1CC/1D2/1D6..E2; BuildObstacleBuckets, incremental AddPolygon/Rectangle routes |
| Missing owner/provider is distinct from present sentinel: visible MISSING and complete prior buckets/count/padding retained, no partial publication |Host unavailable-input guard/staging, native address N/A; required input is00517F6C/78. No claim that native has this failure behavior |
| Legacy public signatures remain callable; absent implicit input visibly skips. Explicit raw/provider methods and CLI none\|uint supply standalone context; none mapsFFFFFFFF |Host input adapter, native address N/A; raw field selection00517F7A/7E. No feature exclusion/default carrying policy |

Preserved integration constraints: unsigned timestamp/tight skip and cache store before rebuild004FD4DA..004FD5CE, carry changes alone do not invalidate;7/6 versus2/1 padding and0.1 penalty; native map type3 source/order/IDs unchanged. No action/root conversion, carrying facade/setter or generic parent-ownership changes.

Evidence: research/20261026-cm13-04-origin-read.md and independent read/build Codex checks. Exact archived oracle drivers reuse existing emu_elf; independently reproduced132+8 helper cases. Selected numeric rows match this .NET host; Python host cos/sin/atan2/ceil hooks/FPSCR0 are declared. Phone/arbitrary-host libm last-bit identity remains UNKNOWN; no numerical tolerance/equivalence policy introduced.

Tests reach actual DriveToPoseAction.Init -> PathComponent -> NativePlannerSet -> native runtime -> source-map importer with a manually admitted cube (not camera/hardware), a conflicting early Docking owner and present-null versus missing actual owner. Separate explicit-input tests cover sentinel/0/high bits, per-heading sampling, whole-table missing retention, cache skips, generic footprints, non-axis/pi and large input4A001476 (first rescale3.25, duplicate changes yaw/quad). Workers join before fixture inputs are released.

Builder: affected143/143 PASS; final22/22 PASS. Actual old-geometry control13 cases6FAIL/7PASS: live dispatch/sentinel cases expose20mm error, three numeric cases expose quad differences. Duplicate-input control2FAIL/0PASS. Restored exact corrected bytes after each. Two new rectangle-oracle failures and their fixture-only corrections are documented honestly in CM13-04-builder.md. Different reviewer independently compared native instructions/code/production bindings and ran46/46 (origin21+map+timestamp),0skipped,1s: PASS. Required scripts/test-full.sh exit0: fidelity710 PASS; full6306/6306, zero skipped6m55s. Local log .scratch/cm13-04-test-full.log. No gate-affecting change after that run; only rebase with proven identical gated content and handoff/queue documentation.

Open: all broader M13-003 obligations/whole-path acceptance, upstream action/root pose borrowing/conversion, full carrying producer/lifetime and generic pose ownership remain. Missing owner is exposed, not recovered. No hardware verification/acceptance claimed. Proposed reconciliation only: after Claude check, narrow M13-003's now-built pose/origin unresolved clause and add this evidence; retain IMPLEMENTATION_GAP and all other gaps.
