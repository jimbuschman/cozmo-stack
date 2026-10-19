# OV129 — marker normal for relative cube lighting

Request: `../requests/overnight/20261019-ov129-m4-052-compute-normal.md`.
Independent extraction, starting HEAD e4690f80; pending manager verification.

## Coverage table (opened before extraction)

| Question | Required coverage | Evidence / disposition |
| --- | --- | --- |
| 1 | ComputeNormal input, corners, differences/cross order, normalization, return/failures | N1–N4: 0x0087E3FA..0x0087E4A6, fully read; no normalization or zero branch |
| 2 | Get3dCorners canonical size/order/signs, transform arithmetic, output layout | C1–C5, R1–R3: initializer 0x004DD7D8..0x004DD81A; constructor 0x004E9636..0x004E9694; body 0x0087E2E8..0x0087E342; transform 0x0050343C..0x00503520, fully read |
| 3 | Result-changing callees and current record ownership | H1–H3: GetTransform 0x0084499A..0x0084499C; Rotation3d point rotation 0x0084ACA6..0x0084ACE2; float quaternion point rotation 0x00849C1C..0x00849CF8, fully read |
| 4 | RelativeCubeLights / existing Vision corner helper comparison | S1–S4 below: reusable scalar transform arithmetic exists; no existing complete bit-for-bit corner-normal helper |

## Result and current record boundary

The normal is **(C1 − C0) × (C2 − C0)**, where all four corners have first been scaled and transformed using the supplied pose, with binary32 rounding at each arithmetic instruction. It is **not normalized**. A rotated unit axis is not a replacement: it loses area, rounding in translated corners and cancellation in the differences.

Current manifest read at e4690f80: M4-052 is IMPLEMENTATION_GAP, titled “Relative-mode SetObjectLights: located lookup, ActiveCube cast, SetLEDs and MakeStateRelativeToXY”. Its evidence names OV128 L1–L8 as corrected by `20261018-ov128-check.md`, and GetTopMarker 0x004E69B8..0x004E6A7C; its unresolved text explicitly stops at ComputeNormal 0x0087E3FA. This report supplies that narrow extraction for manager checking; it changes no record or production code and does not settle the full relative-send path.

## Native rows

All addresses below refer to `libcozmoEngine.so`. `f(a op b)` means the binary32 result of **one separate** operation, not a fused expression; copied floats retain their bits. The extraction establishes instruction widths and dependencies, not the host JIT's NaN payload or floating-point control settings.

| Row | Result-changing step | Primary instructions / disposition |
| --- | --- | --- |
| N1 | Input and sole direct callee | ComputeNormal 0x0087E3FA..0x0087E404: hidden return buffer r0 (saved r7), marker r1, supplied Pose3d const-reference r2. It replaces r0 with a 48-byte stack corner buffer and calls Get3dCorners via PLT 0x004BA9A4 → 0x0087E2E8. GetTopMarker constructs the marker pose relative to root at 0x004E69E4..0x004E69EC (GetWithRespectToRoot 0x004DF628), then passes marker at list-node+8, root pose at sp+0xC, and normal output at sp at 0x004E69F0..0x004E69F8. |
| N2 | First difference A | Copies corner 2 (stack+0x18) to stack+0x40 at 0x0087E408..0x0087E416. Loop 0x0087E418..0x0087E430 computes each x/y/z as `A[i]=f(C2[i]-C0[i])`, specifically `vsub.f32 s0,s2,s0` at 0x0087E428. |
| N3 | Second difference B | Copies corner 1 (stack+0xC) to stack+0x30 at 0x0087E432..0x0087E440. Loop 0x0087E442..0x0087E45A computes `B[i]=f(C1[i]-C0[i])`, `vsub.f32` at 0x0087E452. |
| N4 | Cross product, output, no normalization | Six vmul.f32 operations at 0x0087E46C/474/47C/480/484/488; three vsub.f32 at 48C/490/494; three stores at 498/49C/4A0, then return 4A4/4A6. Exact arithmetic order given below. Output is inline Point3f `[Nx,Ny,Nz]`, offsets 0,4,8, 12 bytes, through saved r0. No scalar success result, sqrt, reciprocal, division, unit-length correction, zero/NaN check, error log or other call follows Get3dCorners. |
| C1 | Canonical source and order | Static initializer 0x004DD7D8..0x004DD81A loads raw 0xBF000000 (−0.5), 0x3F000000 (+0.5), and zero into four point buffers; call 0x004DD814 → Quad constructor 0x004E9636..0x004E9694. Constructor copies three words per argument into slots 0,1,2,3 (physical write order 0,2,1,3). Final canonical slots are C0=(−.5,+0,+.5), C1=(−.5,+0,−.5), C2=(+.5,+0,+.5), C3=(+.5,+0,−.5). No runtime trig or computed halves. |
| C2 | Fetch and layout | 0x0087E2EE..0x0087E304 resolves GOT 0x0103FEF8 to `_canonicalCorners3d` at 0x0105E0F0 (48 bytes) and copies all twelve words into the caller's return buffer. Quad memory is four consecutive XYZ f32 points at offsets 0,0xC,0x18,0x24; no extra metadata or pointers. |
| C3 | Marker size and scale | KnownMarker constructor 0x0087E26E..0x0087E272 copies the Point2f width/height pair into marker+0x10/+0x14. Get3dCorners loop 0x0087E30E..0x0087E332 visits slots 0,1,2,3, writes `x=f(width*canonical.x)` at 0x0087E31A/31E and `z=f(height*canonical.z)` at 0x0087E326/32A; y remains the copied +0. Width and height are independent binary32 values. There is no abs, half-width conversion in double, square assumption, dimension validation or default here. |
| C4 | Supplied pose only | 0x0087E334..0x0087E336 calls PoseBase::GetTransform 0x0084499A via 0x004A4048 using supplied r2. Its complete body is `ldr r0,[r0,#4]; bx lr`: retrieve the pose node whose initial bytes are the Transform3d. This body neither composes marker.Pose nor walks parents. Root flattening belongs to the caller (N1). |
| C5 | Transform all four corners | 0x0087E338..0x0087E342 calls Transform3d::ApplyTo<float>(Quad const&,Quad&) 0x0050343C via 0x004A6C58 with identical source and destination. Full helper 0x0050343C..0x00503520 rotates then adds translation per point, in physical processing order 0,2,1,3. Rotation calls at 0x00503450/488/4BE/4F4 target 0x0084ACA6. Translation begins Transform+0x20; each x,y,z add uses `vadd.f32 s0,s2,s0` (translation + rotated coordinate) at 0x0050346A/49E/4D4/0x0050350A, stored through a 12-byte temporary then copied into that corner. All four points, including C3 unused by the cross product, are transformed. |
| R1 | Quaternion narrowing | Complete Rotation3d::operator*(Point3f) 0x0084ACA6..0x0084ACE2: read double quaternion components at offsets 0,8,16,24, convert each with `vcvt.f32.f64` 0x0084ACCC into a temporary float quaternion, call float quaternion point rotation 0x00849C1C at 0x0084ACDC. Does not normalize the quaternion or build/orthonormalize a RotationMatrix. |
| R2 | Float quaternion point rotation | Complete 0x00849C1C..0x00849CF8: all products, sums, differences binary32; no callees or branches. Dependency-preserving arithmetic below. Point components are f32 loads; quaternion is w,x,y,z. |
| R3 | Failure and degeneracy | Entire N1–N4/C2–C5/R1–R2 bodies contain no dimension/length/finite-value validation, allocation, recoverable-error return or fallback. Collapsed rounded corners yield the arithmetic cross product of their differences; zero length does not trigger normalization. Overflow/NaN participates in the same operations. A missing pose node retrieved by C4 is later dereferenced by R1, not converted into a normal/error value; native invalid pointers are faults, covered by the standing M1-034 policy rather than an invented native return. Exact NaN payload/flush-to-zero control state is UNKNOWN from these bodies alone. |

### Cross product instruction order

Execute these binary32 temporaries in order (the operands are deliberately preserved):

```
t0 = f(A.z * B.x)     // 0x0087E46C
t1 = f(B.y * A.z)     // 0x0087E474
t2 = f(B.z * A.x)     // 0x0087E47C
t3 = f(A.y * B.z)     // 0x0087E480
t4 = f(B.y * A.x)     // 0x0087E484
t5 = f(A.y * B.x)     // 0x0087E488
Ny = f(t2 - t0)       // 0x0087E48C
Nx = f(t1 - t3)       // 0x0087E490
Nz = f(t5 - t4)       // 0x0087E494
```

For an identity transform and exactly represented ordinary positive dimensions, the real-arithmetic interpretation is `(0,-width*height,0)`, not `(0,-1,0)`; this is an explanatory identity, not a license to replace the rounded corner algorithm. Do not assume a particular signed zero for degenerate inputs or cancel translation algebraically before rounding.

### R2 dependencies (each operator rounded separately)

These names denote separate f32 values. Independent products can be listed together without reassociating a dependent sum:

```
ww=w*w; xx=x*x; wy=w*y; wz=w*z; wx=w*x;
xz=x*z; xy=x*y; yz=y*z; yy=y*y; zz=z*z;
d=ww-xx; s=ww+xx;
wz2=wz+wz; xy2=xy+xy; yz2=yz+yz;
wx2=wx+wx; wy2=wy+wy; xz2=xz+xz;
m11=(d+yy)-zz; m00=(s-yy)-zz; m22=(d-yy)+zz;
m10=xy2+wz2; m01=xy2-wz2;
m21=wx2+yz2; m20=xz2-wy2;
m02=wy2+xz2; m12=yz2-wx2;
ox=(m02*pz)+((m00*px)+(py*m01));
oy=(m12*pz)+((m11*py)+(px*m10));
oz=(m22*pz)+((px*m20)+(py*m21));
```

Multiply setup is 0x00849C24..0x00849C54 and 0x00849C60/0x00849C68; diagonal/doubled-term dependencies 0x00849C58..0x00849CA8 and 0x00849CBC; point multiply/add dependencies 0x00849CAC..0x00849CE8; XYZ stores 0x00849CEC..0x00849CF4. These expressions describe the dependencies; the instruction listing is the authority for interleaving independent temporaries. There is no FMA.

## Helper ownership and stack comparison

| Row | Existing ownership / code | Finding |
| --- | --- | --- |
| H1 | M11-003, IMPLEMENTATION_GAP, “Camera pose on the robot, cube size, marker size, face poses and codes” | Current evidence already names canonical initializer 0x004DD7D8..0x004DD81A, constructor 0x004E9636..0x004E968E, and Get3dCorners scaling. C1–C3 confirm that shared geometry; M4-052 owns its use in relative lighting. No manifest change proposed by this report. |
| H2 | M11-065, IMPLEMENTATION_GAP, “Pose3d / Rotation3d / Transform3d arithmetic: double unit quaternion, float translation, float point rotation, renormalised composition (rows R1-R34, R38-R40)” | Current evidence `20261013-pose3d-arithmetic.md` names Rotation3d point rotation 0x0084ACA6 → float quaternion rotation 0x00849C1C..0x00849CF8. C4/C5/R1/R2 are scalar pose/transform helpers in that ownership. They do not introduce a new Rodrigues or matrix helper. |
| H3 | M11-066, IMPLEMENTATION_GAP, “The pose tree: GetWithRespectTo (null/alias checks, depth walk, Tt^-1*Tf, 1000-step LoopBoundOverflow) and GetWithRespectToRoot (rows R35-R37)”; M11-067, IMPLEMENTATION_GAP, cv::Rodrigues / pose-tree copy semantics | Current M11-066 evidence names root helper 0x004DF628..0x004DF6DC, invoked upstream at N1. Neither ComputeNormal nor Get3dCorners invokes a tree traversal or Rodrigues. M11-067's pose-copy ownership may matter in the caller, but is not another result-changing callee inside these bodies. Direct calls are exhausted by N1,C4,C5,R1; R2 is a leaf. |
| S1 | `cozmo-stack/src/Cozmo.Robot/RelativeCubeLights.cs`, Apply | Existing normal callback receives `(marker,root)` after flattening. Production supplies no callback, visibly reports MISSING and skips the relative update/send. This is the insertion point for checked C1–R2/N2–N4, after manager adoption; do not silently replace it with NormalOnObject. No implementation done here. |
| S2 | `Vision/CubeGeometry.cs`, KnownMarker.Corners3d / CornersOnObject / CornersInWorld / NormalOnObject | Canonical signs/order match C1. SizeMm/HeightMm and Vec3 arithmetic are double in Corners3d, without the native input narrowing and per-multiply f32 semantics. CornersInWorld applies marker then object transforms separately instead of applying the supplied flattened pose once; rounding can differ. NormalOnObject rotates a unit −Y axis and omits transformed-corner differences/cross product entirely. None is a bit-for-bit complete Get3dCorners/ComputeNormal helper. |
| S3 | `Vision/NativePoseArithmetic.cs`, Rotation3d.Rotate / Transform3d.Apply; `Vision/Geometry.cs`, Pose3d.Apply | Existing Rotate narrows quaternion and point components to float, uses R2's diagonal grouping and component sum grouping. Transform.Apply does `Add(Translation,Rotation.Rotate(p))`, with f32 component addition in the native operand order, and Pose3d.Apply delegates to it. These are reusable arithmetic pieces for finite values under matching floating-point settings; no bit-for-bit runtime/corpus validation was performed here. Some multiply operands in Rotate are reversed relative to the instructions (e.g. m01*py versus py*m01), so this inspection does not claim identical NaN payload selection. Do not substitute Rotation3d.Matrix/RmatEntry, which follow other arithmetic. |
| S4 | `Vision/MarkerObservationTime.cs`, SetMarkerAsObserved | Already narrows width/height to f32 and scales canonical x/z before relative.Apply in its observation projection loop. This is another use of the geometry pieces, not a public four-corner supplied-pose return helper and not a ComputeNormal implementation. A normal still needs N2–N4. |

## Completion and limits

All requested bodies and all result-changing direct/transitive callees above were read through return. No unresolved algorithmic step remains in the requested valid-input normal path. UNKNOWN is limited to floating-point environment/exceptional NaN payload behavior not established by these bodies; no libm is called in this path. The separate relative-angle bionic atan2f last-ulp unknown in M4-052 remains outside this request.

Verification here is instruction-by-instruction extraction and current C# dependency comparison, not an emulator, hardware or production regression run. Suggested manager regression cases after adoption: rectangular marker to expose area/sign; nontrivial quaternion and translation to compare all corner bits; large translation that collapses differences; zero dimensions; and a top-marker selection where a rotated-axis substitute would disagree. Tests should compare a native reference, not only a duplicate formula. No status promotion or code change is claimed.

## Manager check and adoption (2026-10-19)

Checked inline by the manager (no agent; operator, 2026-10-19). Re-read 0x0087E2E8..0x0087E342 (Get3dCorners: copy of
`_canonicalCorners3d`, x = f(width * x) at 0x0087E31A, z = f(height * z) at 0x0087E326, GetTransform 0x004A4048, ApplyTo
0x004A6C58) and 0x0087E3FA..0x0087E4A6 (ComputeNormal: A = C2 − C0 at 0x0087E428, B = C1 − C0 at 0x0087E452; the six
products and three differences in the order given; stores Nx, Ny, Nz at 0x0087E498..0x0087E4A0; no normalization). N1–N4,
C2–C4 and the cross-product order CONFIRMED. C1, C5 and R1–R2 not re-read; they rest on M11-003 and M11-065's existing
evidence. **Adopted** onto M4-052; built in B-M3M4-35.
