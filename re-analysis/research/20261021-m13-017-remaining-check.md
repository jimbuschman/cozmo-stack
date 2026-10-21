# M13-017 remaining prerequisites (2026-10-21)

**Codex review; Claude spot-check pending**

Baseline b08efa68 plus B-M13-10 7f42b427. Bounded ongoing review; no settlement or hardware readiness.

## Rotation cast claim is stale

Current M13-017 (IMPLEMENTATION_GAP; title "BehaviorDriveOffCharger: runnable on the charger, the drive distance and the leaving-the-contacts update") retains "double rotation cast" in unresolved. Current ObjectFootprint.ObservableObjectBoundingQuadXY uses atPose.Rotation, whose getter returns Quaternion.Matrix(). Mat3 stores nine float values; its double indexer widens those values exactly. Casting those elements back to float is not the historical double-matrix computation.

Manager reopened Rotation3d::GetRotationMatrix 0x0084AAE0..0x0084ABE4 directly in .so: quaternion products are f64, individually converted at0x0084AB16..0x0084AB46, combined in f32, copied as nine words, then RotationMatrixBase::Renormalize at0x0084ABE0. NativePoseArithmetic.Rotation3d.Matrix reproduces this operation sequence and calls Orthonormalized. Thus the old cast description is no longer a valid implementation defect. This bounded comparison does not settle Renormalize/Rodrigues or all pose ownership; those keep their existing records and review boundaries.

## Still concrete

BlockConfigurations.MinAreaRectF.Compute explicitly rejects hulls with <=2 points; ConvexHull currently indexes empty input. The reviewed M13-024 inventory already describes the native n<=2 paths and GetBoundingQuad catch-all, but their exact bodies must be reopened before a build. Footprint.TryGetBoundingQuadXY treats the unported branch as MISSING; it must not invent a native cv exception as a workaround. Markerless candidate still reports M11-091; shared map slots do not claim its complete producer/caller path. Ramp/HumanHead, configuration notification, pre-dock cache lifetime, real behavior action ownership, severe-need input and completion consequences remain separate boundaries.

No new research queued. Review continues from already-held answers and existing approved evidence; no build or record closure is implied by this note.
