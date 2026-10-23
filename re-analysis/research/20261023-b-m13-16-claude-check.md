# Claude check: B-M13-16 (ec46eceb, status b5371ef0) — 2026-10-23

Manager review of the MapTiming.cs diff and the status report; instructions re-read in libcozmoEngine.so. **PASS.**
No record settles (M11-074, M13-040, M13-017 stay IMPLEMENTATION_GAP). Builder: focused 86/86, full 6078/6078.

## Re-read

- Valid/valid cache path 0x0067EB9C..0x0067EC2E:
  - no inner entry for the current root (end, or `blo` on the root ID) goes to add only (0x0067EC22..0x0067EC2E);
  - added flag +0x20 == 0 goes to the retry (0x0067EBBC -> 0x0067EBFA);
  - added == 1 calls IsSameAs(current GetPose, cached +0x14, 5 mm (movt 0x40A0), angle 0x3E32B8C2) at 0x0067EBF4,
    and nonzero skips (0x0067EBF8 -> 0x0067EC32);
  - the retry calls slot +0x5C (0x0067EBFC..0x0067EC00), removes only when it returns 1 (0x0067EC02..0x0067EC0C),
    then adds (0x0067EC1C).

  The C# `UpdateObjectPose` follows each branch, with the same argument order (current pose against cached).
- Add (M5-M8) as built: the family converter; a supplied-root map is required before height and cache; the relative
  pose comes from the live robot pose; a too-high pose caches the supplied pose with false; accepted adds read the
  quad with padding 0 and insert base type-4 data with equal stamps, then cache the root-relative pose with true
  whatever Insert returned, then a second clock read and FillBorder.
- Remove (M9): only content 3 with a matching ObjectID is replaced; type-4 charger data is untouched. The content-3 ID
  reader is an explicit MISSING rather than a guess.

## Gaps, as reported (all visible MISSINGs, none fabricated)

The native tree overlap adapter (M11-048), the content-3 payload producer, the whole-map transform and its ID adapter,
the FillBorder processor, and the native tree-to-ray join for CanDriveIdealDistanceForward (W1). The last means live
driving does not yet avoid the charger through the map; no such claim is made. The image clock is the existing
unsourced stand-in (M11-037/M11-044).

## Queued (non-blocking)

1. A W1 test substitutes the overlap; it is labelled unit, which is correct. A production test waits for the tree-to-ray
   join.
