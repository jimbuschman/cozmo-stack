# M13-017 remaining prerequisites (2026-10-21)

**Codex review; Claude spot-check pending**

Baseline b08efa68 plus B-M13-10 7f42b427. Bounded ongoing review; no settlement or hardware readiness.

## Rotation cast claim is stale

Current M13-017 (IMPLEMENTATION_GAP; title "BehaviorDriveOffCharger: runnable on the charger, the drive distance and the leaving-the-contacts update") retains "double rotation cast" in unresolved. Current ObjectFootprint.ObservableObjectBoundingQuadXY uses atPose.Rotation, whose getter returns Quaternion.Matrix(). Mat3 stores nine float values; its double indexer widens those values exactly. Casting those elements back to float is not the historical double-matrix computation.

Manager reopened Rotation3d::GetRotationMatrix 0x0084AAE0..0x0084ABE4 directly in .so: quaternion products are f64, individually converted at0x0084AB16..0x0084AB46, combined in f32, copied as nine words, then RotationMatrixBase::Renormalize at0x0084ABE0. NativePoseArithmetic.Rotation3d.Matrix reproduces this operation sequence and calls Orthonormalized. Thus the old cast description is no longer a valid implementation defect. This bounded comparison does not settle Renormalize/Rodrigues or all pose ownership; those keep their existing records and review boundaries.

## Still concrete

BlockConfigurations.MinAreaRectF.Compute explicitly rejects hulls with <=2 points; ConvexHull currently indexes empty input. The reviewed M13-024 inventory already describes the native n<=2 paths and GetBoundingQuad catch-all, but their exact bodies must be reopened before a build. Footprint.TryGetBoundingQuadXY treats the unported branch as MISSING; it must not invent a native cv exception as a workaround. Markerless candidate still reports M11-091; shared map slots do not claim its complete producer/caller path. Ramp/HumanHead, configuration notification, pre-dock cache lifetime, real behavior action ownership, severe-need input and completion consequences remain separate boundaries.

No new research queued. Review continues from already-held answers and existing approved evidence; no build or record closure is implied by this note.


## Small-hull prerequisite reviewed

Manager reopened the shipped libopencv_imgproc.so (Thumb): minAreaRect zeroes all five result words at0x0009C7E2..0x0009C7EC; calls convexHull(clockwise=1,returnPoints=1) at0x0009C7EE; checkVector/count branch0x0009C8C0..0x0009C8D4. convexHull0x00039174..0x000391FC validates type/count and for count0 releases output then returns. Implement the empty managed hull as empty, not arr[0]. n==1 copies its point to centre at0x0009CE82..0x0009CE8C, keeping zero size/angle. n==2 at0x0009CDFC..0x0009CE80: f32 sums times0.5 for centre, f32 point differences, double dy*dy+dx*dx and sqrt cast to float for width, zero height, atan2(double dy,double dx) cast to float. Common0x0009CE8E..0x0009CEAC scales angle in f32 by180 (raw0x43340000 at0x0009CDF8), widens and divides by pi (raw0x400921FB54442D18 at0x0009CEF0), then casts f32. PLT0x17218 and0x17704 resolve sqrt/atan2. Existing approved M13-024 explicitly owns these n<=2 branches; this is implementation from that contract, not a new classification or arithmetic policy.

The wrapper at engine0x004E6490..0x004E65C6 confirms only minAreaRect is in the existing checked catch scope. Reopened catch tail0x004E6728..0x004E67C4: sErrorF, errG/error-break path, Rectangle::InitFromPointContainer, f32 centre construction and zero angle, then reentry at points/sort. Full exception classification/trigger and catch body are not yet released by this pass; retain that gap. Build only the small-hull results now, using the existing Compute/Points callers, with no fallback substitute for unported algorithms.
