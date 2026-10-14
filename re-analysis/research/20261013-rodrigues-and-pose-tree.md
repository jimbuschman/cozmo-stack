# cv::Rodrigues and the pose-tree copy semantics (Opus extractor, 2026-10-13)

> **Superseded in part (2026-10-14):** `research/20261014-pose-rows-recheck.md` re-checks every row here against the binary and overrides each row it marks CORRECTED. Read it first.

- **Run:** read-only cozmo-extractor, finishing `research/20261013-pose3d-arithmetic.md`.
- **Adopted:** by the manager on 2026-10-13.
- **Helpers:** `.scratch/rod/` (lief + capstone over any shipped .so).

## cv::Rodrigues (shipped OpenCV 3.1.0; libopencv_calib3d.so, which calls libopencv_core.so)

All three engine callers pass CV_32F (flags 0xC1020005) and no jacobian: RenormalizeHelper 0x008499B0, and
`Anki::Rodrigues` (matrix → vector) at 0x0084A3F4 and (vector → matrix) at 0x0084ADFC.

**The wrapper** (calib3d 0x111F8..0x1133E):
- v2m = cols == 1 || rows == 1. Create dst (3, v2m ? 3 : 1);
- call cvRodrigues2(src, dst, 0);
- a result ≤ 0 → dst = 0.

**Vector → matrix** (0xF28E..0xF478):
1. The floats are widened to **double**; everything after is double.
2. theta = sqrt((rx²+ry²)+rz²). VMLA isn't fused, so this is the same as separate products.
3. theta < DBL_EPSILON → the identity. NaN doesn't take this branch.
4. c = cos(theta) and s = sin(theta) (**libm**); c1 = 1−c; itheta = theta ≠ 0 ? 1/theta : 0; r *= itheta.
5. R[k] = ((c1·rrt[k]) + (c·I[k])) + (s·[r]x[k]), where rrt = r·rᵀ and [r]x = {0,−rz,ry, rz,0,−rx, −ry,rx,0}.
6. Narrow to float with `vcvt.f32.f64` (cvConvertScale with unit scale → the plain converter; round-to-nearest).

**Matrix → vector** (0xF696..0xFB9A):
1. Widen to double (cvConvertScale).
2. **The range check:** cvCheckArr(RANGE|QUIET, −100, 100), i.e. [−100, 100). NaN, ±Inf or out of range → dst = 0,
   return 0. So Renormalize turns such a matrix into the **identity**.
3. **The SVD:** cvSVD(A, W, U, V, MODIFY_A|U_T|V_T) → _SVDcompute (core 0x5A7AC..0x5AB48) → JacobiSVDImpl_<double>
   (core 0x595C0..0x59B56). All of it is VFP double, with no NEON.
   - max_iter = max(m, 30). W[i] starts as Σ A[i]².
   - **Per pair i<j:**
     - p = Σ Ai·Aj; skip the pair if |p| ≤ eps·sqrt(a·b), with eps = 10·DBL_EPSILON;
     - otherwise p = p+p, β = a−b, γ = hypot(p, β), using OpenCV's own hypot (core 0x56EB0): |a| > |b| → a·sqrt(1+(b/a)²);
       otherwise b > 0 → b·sqrt(1+(a/b)²); otherwise 0;
     - **c and s:** if β < 0, s = sqrt(((γ−β)·0.5)/γ) and c = p/((γ·s)+(γ·s)); otherwise c = sqrt((γ+β)/(γ+γ)) and
       s = p/((γ·c)+(γ·c));
     - **the rotation:** t0 = s·Aj + c·Ai and t1 = c·Aj + (−s)·Ai; a += t0², b += t1². The same rotation is applied
       to Vt.
   - A sweep with no rotation ends the loop.
   - Then W[i] = sqrt(Σ At[i]²). A descending selection sort uses a strict `<`, and swaps the At and Vt rows.
   - **The zero-singular-value fill:** RNG(0x12345678) (state = lo·0xF83F630A + hi), ±1/m on bit 0x100;
     Gram–Schmidt in two passes with non-fused vmls; the scale guards are eps·100 and DBL_MIN.
4. **R = U·Vᵀ:** cvGEMM with GEMM_A_T, through GEMMSingleMul<double>'s tail loop. R[i][j] =
   ((0 + U[i][0]·Vt[0][j]) + U[i][1]·Vt[1][j]) + U[i][2]·Vt[2][j].
5. **The vector:**
   - rx = R7−R5, ry = R2−R6, rz = R3−R1;
   - s = sqrt(((ry²+rx²)+rz²)·0.25);
   - c = (((R0+R4)+R8)−1)·0.5, clamped to [−1, 1] (NaN passes through);
   - theta = acos(c) (**libm**).
6. **The branches:**
   - **s < 1e-5 and c > 0:** r = 0.
   - **s < 1e-5 and c ≤ 0** (180°):
     - rx = sqrt(max((R0+1)·0.5, 0));
     - ry = sqrt(max((R4+1)·0.5, 0)) · sign(R1);
     - rz = sqrt(max((R8+1)·0.5, 0)) · sign(R2);
     - if |rx| < |ry| and |rx| < |rz| and (R5 > 0) ≠ (ry·rz > 0), negate rz;
     - then r *= theta / sqrt((ry²+rx²)+rz²).
   - **Otherwise:** r *= theta·(1/(s+s)).
7. Narrow to float.

**libm:** cos, sin and acos come from the phone's libm.so, which isn't shipped. Under AGENTS.md they are
EQUIVALENT_IMPLEMENTATION, exactly like the other libm calls (M13-023(e)). sqrt is IEEE correctly rounded, so it is
exact.

## The pose tree: copies, assignment and SetParent (libcozmoEngine.so)

- **The node** (0x58 bytes):
  - +0 quaternion; +0x20 translation; +0x30 parent shared_ptr;
  - +0x38 name; +0x44 ID; +0x48 owner count; +0x4C a set.
- **PoseBase:** +4/+8 is shared_ptr<node>.

| Operation | What it does | Address |
|---|---|---|
| Copy constructor | **A deep copy:** a new node through the node's copy constructor (same parent; the name gets "_COPY" if non-empty; ID 0, owners 0); owners++. There is no move constructor | 0x008458F0..0x0084592A, 0x00845FE4..0x00846044 |
| Node constructor | Copies the quaternion and translation, clears the parent, copies the name, sets ID and owners to 0 and an empty set, then SetParent | 0x00845F44..0x00845FA8 |
| PoseBase::operator= | this == &other → return. Otherwise **node::operator= on this's existing node, in place**: SetParent(other.parent), copy the transform, assign the name + "_COPY". ID and owners aren't copied. If other's node is null: RemoveOwner, the NotLastOwner VERIFY, then reset | 0x00845940..0x008459BE, 0x008459D0..0x00845A2E |
| PoseBase::SetParent | If unowned parents aren't allowed and (the parent node is null, or its owner count is 0): VERIFY "PoseBase.SetParent.UnownedParent". **With a null parent node, the log reads [null+0x38]/[null+0x44]: a crash.** Then node->SetParent | 0x00844A14..0x00844A7A |
| PoseTreeNode::SetParent | Take the new parent, release the old one, then Dev_AssertIsValidParentPointer (a VERIFY only). **No self or cycle check** | 0x008449EA..0x00844A10, 0x008460C8..0x00846124 |
| WrapExistingNode | **Shares** the node, owners++. This is how aliasing wrappers arise (GetParent, FindRoot) | 0x0084529C..0x008452C0 |
| RemoveOwner / AddOwner | owners ±1, with the ZeroOwners VERIFYs | 0x008448B4, 0x008457D8 |
| GetWithRespectTo, the write order | Both inputs' transforms are copied first. `out`'s node is written in place only at the end, then SetParent(out, to). from == to → out = from (in place, "_COPY"), then SetParent(out, from) | 0x00845A34..0x00845E04 |

So `out` aliasing an input doesn't corrupt the arithmetic. But the write into `out`'s node changes every wrapper of
that node, and every child pose under it.

## Unread

- The jacobian blocks (unreachable from the engine), and the error-text decoding.
- The cvSVD W tail 0x5BB64..0x5BBD2 (W isn't used afterwards); cvSetIdentity, cvSetZero, transpose.
- The engine's GetParent 0x008453D4..0x00845448 and FindRoot 0x0084520C..0x0084529C bodies.
- Whether a SetParent with a null parent node is reachable.
