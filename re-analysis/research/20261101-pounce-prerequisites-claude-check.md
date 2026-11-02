# Five Pounce prerequisite jobs: Claude check (manager, 2026-11-01)

**Verdict: PASS, merged, with one correction made here. M11-049 settled. M11-098 added for the motion detector. The
rounding-mode unknown is closed. Pounce still does not run on real frames.**

From `codex-m13-manager-2 050cb4ea`: VISION-GROUND-CARRIER-1, MOTION-IMAGE-PRIMITIVES-1, MOTION-MASK-COMPONENTS-1,
MOTION-BYTE-MORPH-1 and PLAYALONE-ACTION-TAG-1. Codex's full gate was 7501/7501 at `8365f430`.

## Checked directly in the `.so`

| Claim | Addresses | Result |
| --- | --- | --- |
| Lookup: empty and above-max fail, first key at or above head | 0x006533C4..0x006534F6 | Matches. The successor comparison can never pick the successor, because the lower bound is always the nearer key |
| LUT constants and loop | 0x00651998..0x00651B8E, caller 0x0065176A | Matches: start 0xBEDF66F3, step 0x3B8EFA35, limit 0x3F46D3F2 (at or below), break when neither far corner is in view |
| K and M packing | 0x0085EE62, 0x00651A7C..0x00651ACA, ctor 0x006594BE | Matches. Each point fills a column, so M is (col0, col1, t) |
| f32 multiply | 0x004F83F8 | Matches |
| Ground quad | 0x004F7774 | Matches: (190,75), (40,20), (190,-75), (40,-20) at z=1 |
| Projection | 0x004F78E0, 0x004F798A | Matches |
| Segment intersection | 0x008F6220, 0x008F6320 | Matches, including the 0.0001 test and NaN paths |
| Clamp | 0x004F7B74, call 0x004F7952..0x004F797C | **Corrected, see below** |
| FOV | 0x0085E13C | Matches for zero borders |
| Ratio test | 0x006ABA76, 0x006AB8DE | Matches: all bytes at least 11, strict 1.25, one count per pixel |
| Half size | 0x006AAB02, 0x00874304 | Matches: roundf of 0.5 times the size |
| Component delivery | 0x008721C0..0x00872282 | Matches: clamp, NaN on the HI path, field order |
| Close | 0x006A9880..0x006A98D6 | Matches: op 3, one iteration, in place, DBL_MAX border |
| Image drop | 0x00652B20 | Matches for M11-049 |

The labelling logic reads as the standard OpenCV 8-connected pass, but I compared it from memory of that code, not from
the shipped imgproc. Not read here: the imgproc addresses themselves, 0x00652CFC..0x00652D58, 0x006AF330, 0x006A9E08 and
0x006AA6FC. Those rest on Codex's independent reviews.

## Correction

`NativeGroundPlaneProjection.Clamp` ran the image's bottom edge right to left. The native call passes (0, rows-1) first,
so the edge starts at -(cols-1). Both directions find the same crossing, but f32 rounding differs: 14,742 of 20,000 random
crossings changed, by about 0.00006 pixels. Codex's test comment had the same reversal, so its expected values could not
catch this. Fixed on main, with `ClampRunsTheBottomEdgeFromLeftToRightLikeNative` using values worked outside C#.

## The rounding mode is known

Three jobs hold MISSING entries on "native FPSCR". That is now closed.

- A census of all 28 shipped `.so` files finds no `vmsr fpscr` anywhere except five sites in `libtbb.so`. The three I read
  write back a captured value after a memcmp shows it changed; the other two are the same shape of code by address.
- None of the shipped libraries names `fesetround`, `fesetenv`, `feupdateenv` or `feholdexcept` in its dynamic symbols.
- So shipped code never picks a rounding mode, and a process keeps the ARM Linux default: round to nearest, ties to even.
  The kernel default is outside shipped code, and it is the only input not read here.

Round to nearest, ties to even applies to `vcvtr`, `vcvt.f32.f64` and cvRound. C#'s `(float)double` and
`Math.Round(x, MidpointRounding.ToEven)` give the same result.

This matters more than it looks. `NativeMotionComponents.TryAppend` currently refuses any centroid that is not exactly
representable, and a blob of 3 pixels can have a centroid in thirds. The detector would fail on most real blobs, so this
gate has to go.

## The flake

PLAYALONE-ACTION-TAG-1 answers the question. The old test compared historical starts with the current playing state, and
a completed ticket's thread-pool callback could race the rig's clock. Starts now carry their own identity and the rig waits
for completed callbacks. The strict "every matched clip is in its group" assertion is back, replacing my relaxed one.

## Plain probe

No new probe. Nothing in this batch reaches Pounce: the motion helpers are called only from tests, and the ground carrier
stops at a named MISSING. Hiking still holds on a robot, so the running question is unchanged.

## Findings

No blocking finding after the correction. One accepted cost: `HandOverFrame` counts a dropped frame before the owner
check, so a frame rejected during robot removal can count once too often. It is host diagnostics only.

## Records

- **M11-049** EXACT_SOURCE on the reader: images are dropped until the NV callback has run.
- **M11-098** added, IMPLEMENTATION_GAP: the Anki MotionDetector. M11-016's OKAO authority and evidence are frozen and
  do not describe it, so the motion work has its own record rather than a correction to M11-016.
- M11-017, M11-033, M11-040, M11-054 and M11-097 stay open.

## Progress

1 record in 5 jobs. Totals: 13 settled across 37 start-path jobs, 1 of 178 behaviours finished, 753 records.
