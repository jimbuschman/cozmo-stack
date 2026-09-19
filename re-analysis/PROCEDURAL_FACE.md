# The procedural face renderer, reconstructed from libcozmoEngine.so

Every constant and every step below is read out of the binary. Where something could not be recovered it is
named as missing rather than filled in with a plausible value.

Addresses are in `resources/lib/armeabi-v7a/libcozmoEngine.so` from the official 3.4.0-1204 APK.

| function | address |
| --- | --- |
| `ProceduralFaceDrawer::DrawFace(ProceduralFace const&)` | 0x00585B30 |
| `ProceduralFaceDrawer::DrawEye(ProceduralFace const&, WhichEye, Image&, Rectangle<float>&)` | 0x005850E0 |
| `ProceduralFaceDrawer::GetTransformationMatrix(float×7)` | 0x00584FF8 |
| `FaceAnimationManager::CompressRLE(Image const&, vector<uint8>&)` | 0x00581904 |

## The canvas is 128×64, which resolves the (64, 32) question

`DrawFace` opens with

```
movs r1, #0x40      ; 64
movs r2, #0x80      ; 128
blx  Anki::Vision::Image::Image(int, int)
```

`Image(int, int)` takes rows then columns, so the drawing canvas is **64 rows by 128 columns**. Its centre
is therefore (64, 32), which is exactly the centre passed to `GetTransformationMatrix`, and `warpAffine`
writes back into a 128×64 output. `CompressRLE` then *asserts* its input is 64×128 — it compares against
0x40 and 0x80 and calls `sErrorF` otherwise — and builds one 64-bit column mask per column.

So there was never a contradiction: the earlier note that the transform centre is (64, 32) while our bitmap
was 128×32 simply meant **our canvas was half the height the engine uses**.

## The robot's 128×32 image: interlacing, not a smaller canvas

After `warpAffine`, `DrawFace` runs

```
r0 = _firstScanLine                 ; a byte, 0 or 1
r5 = (r0 == 0) ? 1 : 0
r6 = fp + (1 & ~(fp ^ r5))          ; snap the start row to the right parity
loop:  memclr(row(r6), 128)         ; blank a whole row
       r6 += 2
```

**Every other row of the 128×64 image is blanked**, and which parity is blanked alternates: `InitStream`
(0x0057B674) flips both `ProceduralFaceDrawer::_firstScanLine` and `FaceAnimationManager::_firstScanLine`
with `rsb r3, r3, #1`, i.e. `x = 1 - x`, on every new animation. Alongside
`GetMaxBlinkSpacingTimeForScreenProtection_ms` this reads as OLED burn-in protection rather than geometry.

The same byte is also added to each polygon point's y before filling, so the eye shape moves with the
parity rather than being clipped by it.

## The eye's own coordinate frame

`DrawEye` works in a local frame per eye, with these constants taken straight from the code:

| constant | value | where |
| --- | --- | --- |
| half-width | **15** | `vmov.f32 s18, #1.5e+01`; corner fallbacks use ±15 |
| half-height | **20** | corner fallbacks use ±20; lid maths uses ±20 |
| nominal width | **30** | `vmov.f32 s20, #3.0e+01`, the X radius scale |
| nominal height | **40** | literal 40.0 at 0x005853A8, the Y radius scale |

So the nominal eye is **30 wide by 40 tall**, spanning x ∈ [−15, +15] and y ∈ [−20, +20].

Each eye's 19 parameters are a contiguous block: `r4 = face + whichEye * 0x4C`, and 0x4C = 76 = 19 × 4.
The offsets used confirm our existing parameter order exactly — 0x00/0x04 centre, 0x08/0x0C scale, 0x10
angle, 0x14…0x30 the four radius pairs, 0x34…0x3C upper lid, 0x40…0x48 lower lid.

## Eye placement

```
s0 = [eye + 0x04]            ; EyeCenterY
s2 = [eye + 0x00]            ; EyeCenterX
s0 = s0 + 32.0               ; centreY
r1 = table;  if (whichEye == 0) r1 += 4
s2 = [r1] + s2               ; centreX
```

The two-entry table at 0x005859DC holds **96.0** and **32.0**, and `whichEye == 0` takes the second.

| | value |
| --- | --- |
| eye 0 base centre X | **32** |
| eye 1 base centre X | **96** |
| base centre Y | **32** |

`EyeCenterX/Y` are therefore **pixel offsets added directly to that base**, in the 128×64 canvas. The true
separation is **64 px**.

*(Previously this renderer used 40 and 88 on a 128×32 canvas — a separation of 48 — with a nominal eye of
28×28. All four numbers were invented. That is why a whole-face stretch merged the eyes: they were both too
close together and the wrong shape.)*

## Per-eye transform

```
GetTransformationMatrix(EyeAngle, EyeScaleX, EyeScaleY, centreX, centreY, 0, 0)
```

The centre argument is **(0, 0)**, so `EyeScaleX/Y` and `EyeAngle` scale and rotate the eye's polygon about
the eye's own origin, and the translation then places it at its centre. Each polygon point is carried
through `operator*(SmallMatrix<2,3>, Point<3>)` with the point as `(x, y, 1)`, rounded, and then
`_firstScanLine` is added to y.

**Mirroring.** Before the transform, `if (whichEye != 0) x = -x`. The local shape is authored once and the
second eye is a horizontal mirror of it, which is what makes the "inner" and "outer" corner names mean the
same thing on both eyes.

## The eye outline

The outline is a polygon, not a rounded rectangle. Each corner is an elliptical arc from
`cv::ellipse2Poly(centre, axes, angle, arcStart, arcEnd, delta, points)` with **delta = 10°**:

| order | corner | arc | arc centre | fallback point |
| --- | --- | --- | --- | --- |
| 1 | top right | 270° → 360° | (15 − rx, ry − 20) | (15, −20) |
| 2 | bottom right | 0° → 90° | (15 − rx, 20 − ry) | (15, 20) |
| 3 | bottom left | 90° → 180° | (rx − 15, 20 − ry) | (−15, 20) |
| 4 | top left | 180° → 270° | (rx − 15, ry − 20) | (−15, −20) |

Radii are fractions of the half-extents, rounded to whole pixels:

```
rx = round(radiusX * 0.5 * 30)      ; so 0..15
ry = round(radiusY * 0.5 * 40)      ; so 0..20
```

**A corner whose rx or ry rounds below 1 is a sharp corner**: a single point at the exact corner of the box
is pushed instead of an arc.

## Lids

Each lid is a filled quadrilateral that masks the eye from one edge inwards, plus an optional elliptical
bend across the lid line.

```
lid    = round(LidY * 40)                    ; pixels from that edge, over the full height
dx     = -round(tan(LidAngle * pi/180) * 15) ; the tilt across the half-width
```

* **Upper lid** quad: (−16, lid−20+dx), (−16, −21), (16, −21), (16, lid−20−dx)
* **Lower lid** quad: (16, 20−lid−dx), (16, 21), (−16, 21), (−16, 20−lid+dx)

Both overshoot the eye box by one pixel — ±16 rather than ±15, ±21 rather than ±20 — so the mask covers
the outline cleanly.

**Bend.** When `LidBend` is non-zero an arc is added with

```
axes   = (round(15 / cos(LidAngle)), round(LidBend * 40))
centre = (0, lid − 20)   upper,     (0, 20 − lid)   lower
angle  = LidAngle
arc    = 0° → 180°       upper,     180° → 360°     lower
delta  = 10°
```

## Whole-face transform

```
GetTransformationMatrix(FaceAngle, FaceScaleX, FaceScaleY, FaceCenterX, FaceCenterY, 64, 32)
cv::warpAffine(image, image, M, Size(128, 64), ...)
```

Disassembling the matrix builder gives it exactly:

```
row0:   cos*sx    sin*sy    (1 - cos*sx)*cx - sin*sy*cy + tx
row1:  -sin*sx    cos*sy      sin*sx*cx + (1 - cos*sy)*cy + ty
```

with `cx, cy = 64, 32`. Because the centre terms are present, a whole-face scale moves eye **positions**
as well as stretching eye geometry, and a whole-face angle rotates the pair about the face centre.

## What could not be recovered

Named rather than guessed:

1. **Which radius parameter feeds which corner.** The four arcs and their geometry are certain, and the
   parameter block offsets are certain, but the register plumbing between the eight radius loads and the
   four arc calls is heavily interleaved and was not traced to a definite mapping. The renderer uses the
   natural reading of the names under the established mirror — upper/lower × inner/outer, with "inner"
   towards the nose — which is consistent with the mirroring but is **not** confirmed instruction by
   instruction.
2. **The polygon fill rule.** No `fillPoly` appears in either function's imports, so the fill is inlined or
   in a helper that was not located. An even-odd scanline fill is used here. For the simple closed outlines
   these polygons form, even-odd and non-zero winding agree, so this is unlikely to be visible — but it is
   not established.
3. **How the 64-row image becomes the 32-row payload on our wire path.** `CompressRLE` encodes all 64 rows
   with half of them blank; our own codec, which was verified against 28 robot-produced byte sequences and
   on hardware, encodes 32. The renderer therefore keeps every other row of the 64-row canvas. Which parity
   to keep is the open question; `_firstScanLine` is fixed at 0 here rather than alternated, since the
   alternation reads as burn-in protection rather than geometry.
