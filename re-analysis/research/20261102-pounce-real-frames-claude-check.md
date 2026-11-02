# POUNCE-REAL-FRAMES-1: Claude check (manager, 2026-11-02)

**Verdict: PASS, merged. M11-098 settled. M15-107 stays open. A robot left alone with changing frames no longer holds on
Pounce. No real frame has been through this code yet.**

From `codex-m13-manager-2 10318358`, one job, one handoff. Codex's full gate was 7581/7581 at `d843bc7d`.

## Checked directly in the `.so`

| Claim | Addresses | Result |
| --- | --- | --- |
| Rectangle from the raw quad, before the correction | 0x006B4854, 0x006B4868 | Matches |
| Empty vector takes the same branch as false | 0x006B2D6A..0x006B2D78 | Matches, so no override is needed |
| Worker flag gate before the shift pass | 0x006B50F6..0x006B5112 | Matches |
| 120 cumulative shifts, step, roundf, row order, dt | 0x00539258..0x00539352 | Matches |
| Shift lookup, bracket and interpolation | 0x005394BC..0x00539660 | Matches as far as read; the tail past 0x00539660 was not |
| Corner stripe, at(), subtraction, bounds, NaN | 0x006B4890..0x006B4950 | Matches, including NaN passing both bounds |
| Pose gates, 500 ms, status mask | 0x006A8FD8..0x006A9258 | Matches |
| Order: filter, ratio, peripheral, ground and image | 0x006A9268..0x006A92C0 | Matches |
| Two distinct appends of one report | 0x006A9DC4..0x006A9DF2, 0x006A92CA..0x006A92F4 | Matches: consumers get each report twice |
| Peripheral-only append reads an unwritten prefix | 0x006A92D4, only 0x006A9266 writes the buffer in the helper | Matches, M1-034 skip accepted |
| Peripheral close, area 500, decay, outputs | 0x006A9848..0x006A9AA8 | Matches for the caller; 0x006AB484 and 0x006AB738 not read |
| Image threshold, centroid, packing, ground fields | 0x006A9C62..0x006A9DC0 | Matches |
| Non-running Pounce: strict area, inclusive distance | 0x005F9B34..0x005F9B96 | Matches |

The 10x10 ellipse was recomputed by hand from the OpenCV formula: the rows and the 83 entries match. The default unlocks
include `PounceOnMotionAction`, and the 22-unlock count matches the last robot run's log.

**Not read here:** the box filter, `ExtractGroundPlaneMotion` 0x006AAD38 and the polygon and LU code, 0x006AB484 and
0x006AB738, and the positive Pounce body from 0x005F9C34. Those rest on Codex's independent reviews.

## Plain probe

Canonical robot, 300 s, one frame a second, no forced child. Temporary probe, not committed.

| Case | Idle | Behaviour time | Motion reports |
| --- | --- | --- | --- |
| Flat frames, six seeds, unlocks empty as the rig leaves them | 5.6 to 6.4 s | No Pounce: it is locked | 266 to 276 |
| Flat frames, Pounce unlocked | 0.2 s | Pounce 264 s, repeated full pounces | 298 |
| Moving patch, Pounce unlocked | 0.2 s | Pounce 264 s | 298 |
| Moving patch, unlocks empty | 175.1 s | Hiking waits with no child | 278 |

The second row is what a robot has: the default unlocks include Pounce, which the rig does not load.

- **No M8-013 hold** in any case. `NativeMotionReadyFor` is true on the canonical owner.
- **The 175 s row is not a hold.** The chooser reports nothing runnable, and the wait ends at Hiking's shipped
  `activityShouldEndDurationSecs` of 60. It only arises with Pounce locked, and a robot with its default unlocks has it open.
  Why flat frames leave Hiking quickly and the patch does not is unexplained.
- **Hiking did not leave Pounce in 264 s.** The end check runs only when the next pick isn't already running, and the
  shipped config says hiking finishes on its own. These frames report motion every comparison, so the behaviour never gets
  bored. A real room may differ, and only a robot run can show that.

## Findings

No blocking finding. The regression from the WIP is gone: physical markers now reach BlockWorld.

## Records

- **M11-098** EXACT_SOURCE on the detector's own path, with the narrow M1-034 skip. The unread items above are named in it.
- **M15-107** stays IMPLEMENTATION_GAP: its positive body was not read here and has never met a real frame. Settle it after
  that read and a robot run.
- M11-016, M11-033, M11-035, M11-059, M11-080 and M14-008 keep their named gaps. M11-080 now names frames with a face or pet
  in view.

## Progress

1 record in 1 job. Totals: 14 settled across 38 start-path jobs, 1 of 178 behaviours finished, 753 records.
