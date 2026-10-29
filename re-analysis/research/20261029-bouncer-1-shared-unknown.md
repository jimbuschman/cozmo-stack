# BOUNCER-1 UNKNOWN-only shared supplement — 2026-10-29

Request: manager supplement to `jobs/trial-m13/BOUNCER-1.md`. Same extractor, gpt-6.1-sol/medium requested, actual backend unobserved; no model switch. Read-only except this report. No C# audit/edits, builds, hardware, network, git mutation or workers. Original own-state/kernel/caller contract is frozen and not re-extracted. PROJECT_STATE reread at resume. Current source bodies below were inspected from native bytes with capstone; decompilation used neither as evidence nor as a substitute for bytes. known.py precedes new substantive function reads; imgproc-address queries are necessarily absent from its engine-only index, so such misses do not imply unavailable code. Current M14/M5/M8 whole claims reread before naming ownership (literal excerpts below).

## Disposition

Three apparent UNKNOWNs are now recoverable bounded contracts, not grounds for a kernel-only/full-behaviour claim:

1. **Face +0xDC is native detector output**, not an app/SDK angle input: the third integer result of OKAO_PT_GetFaceDirection is converted to f32 radians and preserved in the FaceWorld tracked-face value. Its exact slot/units are established below. Full OKAO detection is the operator-deferred RECOVERABLE_GAP M11-016; the current IFaceDetector seam needs to carry this value distinctly. Do not silently reuse a differently sourced RollRad field by name.
2. **Image -> face-animation frame is available source:** FaceAnimationManager pre-registers the procedural name, ClearAnimation removes its old frames, AddImage(33) thresholds and installs exactly one pair-RLE frame via settled M5-013; SetFaceAction keyframe references that name, with zero override. Existing M5 streaming and existing action lifecycle are reused at their actual interfaces. M8-020's existing completion/union/cached-pointer uncertainty is not settled by recovering this image join.
3. **The raster/font routines are in the shipped `libopencv_imgproc.so`, not bodies inside libcozmoEngine.so.** The engine imports the symbols. Original read's wording that they were “available in the .so” is corrected to this precise binary attribution. Used radius3/filled-rect/digits-only paths are bounded below; no generic/all-font audit or downloaded OpenCV source was needed.

The host SetFeatureToggle map write already in the frozen read remains source-backed. Its disk/update/host dispatch tail is not widened or built here; there is no new Bouncer consumer gate. Shipped feature=false remains default.

## Native tracked-face input contract

| Step | Exact source fact | Citation in libcozmoEngine.so | Ownership / limit |
|---|---|---|---|
| F1 default | TrackedFace constructor constructs Radians at DC, E4 and EC, each through default constructor. | 0087DBB2..0087DBC6 (add DC/E4/EC, calls004A5854) | M14-001 fields; value defaults use existing Radians primitive |
| F2 direction request | DetectFaceParts calls OKAO_PT_GetFaceDirection(resultHandle, first=&sp24, second=&sp20, third=&sp28). Those local integer outputs were zeroed. Nonzero result warns and bypasses assignments; the face retains its preceding/default angle values. | 0086CF22 add r1,sp,#24; 0086CF26 add r2,sp,#20; 0086CF28 strd zero,[sp,#20]; 0086CF2C add r3,sp,#28; 0086CF2E handle; 0086CF32 blx004CF83C; 0086CF38 cbz success; failure->0086D012 | M14-010 exact adapter; actual OKAO output M11-016 deferred |
| F3 conversion | On success: third(sp28) -> f32 signed conversion -> f32 multiply degrees-to-radians literal0x3C8EFA35 -> Radians temporary -> assignment face+DC. First(sp24) similarly ->E4; second(sp20) ->EC. No sign flip or extra smoothing in this adapter. | 0086CF74 ldr sp28; 0086CF78 vldr constant at0086D104; 0086CF82 vcvt.f32.s32; 0086CF86 vmul; 0086CF8E Radians; 0086CFC6 add face,#DC; 0086CFCC operator=; 0086CF92..CFA8 first; 0086CFAC..CFC2 second; 0086CFD0..CFE0 E4/EC | M14-010; source slot identity established. Human axis name not guessed |
| F4 actual producer | FaceTracker::Impl::Update appends a real TrackedFace for each successful detector result then passes that same value to DetectFaceParts. The reference is list node+8 (not a user UI angle). | 0086D962..D968 emplace; 0086D96C load node; 0086D9F2 add r6,node,#8; 0086D9F8 strd detectionIndex,r6,[sp]; 0086DA04 call DetectFaceParts | M14-010, not a new external input |
| F5 handoff | Existing accepted-source FaceWorld chain: VisionComponent::UpdateFaces -> FaceWorld::Update -> AddOrUpdateFace. On found entry, it calls the TrackedFace assignment helper; new entry copies the tracked face. | existing M14 inventory F2-F5: 00655274 ->004F531E; 004F47AE mov dst entry; 004F47B0 mov src tracked; 004F47B2 bl004F50F8; new004F45FC emplace<TrackedFace const&> | M14-001/008 interface; no full handoff audit |
| F6 copy preservation | TrackedFace copy constructor explicitly copies DC/E4/EC with Radians copy ctor. Assignment helper copies same offsets with Radians assignment. Thus source+DC reaches the FaceWorld object Bouncer::GetFace returns; it is not reconstructed from the flattened world pose. | copy004F3A96 add dst,#DC /004F3A9A src,#DC /004F3A9E copyRadians; assignment004F518A..004F5192; E4/EC004F5196..004F51AA | M14-001 adapter complete |
| F7 consumer limit | Frozen Bouncer consumer reads DC Radians and getDegrees; no listener set/host callback supplies it. Inputs may be carried by the existing detector seam with this exact native provenance, but a zero/arbitrary synthetic value is not a recovered production detector. | frozen P1,005F15CC..005F15DE | M11-016 remains deferred per current operator-approved unresolved; no detector re-extraction |

The existing manifest M14-001 mentions the production detector populating `DetectedFace.RollRad`, and its geometry calculation uses native **EC**, not DC. These are distinct offsets in the binary. The semantic name “RollRad” in C# or old notes is not evidence that it is Bouncer's DC. Preserve native output/slot identity until a separate semantic mapping is checked. This is an interface caution, not a proposed official-record contradiction or a need to extract the full SDK. No supplied input is fabricated here.

## Image -> named frame -> action join

| Step | Exact source fact | Citation | Ownership / remaining boundary |
|---|---|---|---|
| J1 registration | FaceAnimationManager constructor clears map, max load factor1, then map[ProceduralAnimName] creates its available-animation entry. SetFaceAction's singleton creation therefore has an existing procedural-name slot before Clear/AddImage. | 0057FDE4 resolves globalProceduralAnimName; 0057FDEE..FDF6 clear/mapfactor; 0057FDF8..0057FDFC map operator[] | NEW narrow registration; no hash-table implementation requirement |
| J2 clear | ClearAnimation(name): lookup; missing returns1; found destroys every pair-vector frame from back to begin, leaves entry/name present, returns0. | 0058168C lookup; 00581692 missing; 00581694..16CC truncate pairs; 005816CE zero; 005816D2 one | NEW wrapper; registry ownership remains source-local |
| J3 threshold | AddImage(name,image,duration): lookup null=>1; Threshold(0x80), then00581254 produces the two parity RLE variants; appends the pair as one frame. Threshold strictly above0x80 is already settled M5-013. For Bouncer pixels0/255 threshold leaves the binary image (0/255). | 0058144A lookup; 00581452 missing; 00581460 movs r2,#80; 00581462 Threshold; 0058146C helper; 005814C6..00581514 append; M5-013 evidence Threshold00871E98 | M5-013 accepted threshold/encoding reused |
| J4 duration33 | The extra-empty-frame path begins only when duration>=34 (00581518 cmp34), and the subsequent condition handles multiples. Bouncer duration33 bypasses it: exactly one encoded frame, no extra empty. Return0 after append. | 00581518 cmp r5,#22; 0058151E blo005815B0; 005815D0 movs r0,#0 | NEW used branch only; no generic duration policy proposed |
| J5 parity variants | Helper makes copy, clears rows0,2,... then CompressRLE into pair.first; copies original again, clears rows1,3,... then CompressRLE into pair.second. GetFrame selects with the streamer's firstScanLine and shared index logic. | 00581264 CopyTo; 00581272 r7=0; 00581286 +=2; 00581290 Compress; 00581298 secondCopy; 005812A2 r7=1; 005812C4 secondCompress; M5-013 GetFrame005817B4..CA | accepted M5-013, no second encoder |
| J6 keyframe reference | SetFaceAction makes FaceAnimationKeyFrame(ProceduralAnimName), all +18..+28 cleared (frame/index/byte-override storage). Adds that keyframe to its action-owned live Animation. It uses the named manager frame, not a new direct wire face format. | already-checked SetFace00563E3E..00563E52; helper00564290 name+0C; 005642C6 add+18/005642CA length11/005642CC memclr4 | M5-058 zero override; M5-013 named lookup |
| J7 actual action | Checked frozen SetFaceAction creates PlayAnimationAction(robot,&ownedAnimation,1,true,0,60) and stores child; CheckIfDone calls child IActionRunner::Update. Reuse the existing native action/queue and animation stream path. No timer-only replacement or fabricated success. | frozenD6:00563F72..00563FA0;005643EA..EE | M8-020 action lifecycle still IMPLEMENTATION_GAP; M5-024/025 and M5-013 settled stream pieces |
| J8 failure/cleanup | Missing manager entry/AddImage failure or keyframe-add failure is defined failure, not an empty successful display. Existing SetFace warning and failure result0x03000000 apply. Subsequent own Stop uses shared owned-action cancellation. Exact action-tail callbacks/report union remain M8-001/M8-020. | 00563E3C cbnz failure;00563E86 cmpResult;00563E9A warningKey SetFaceAction.Init.AddFaceKeyFrameFailed;00563F20 mov.w r2,#03000000 | reuse frozenD6 and owning records; not fully settled here |

Remaining material C# work question is concrete: can the current shared animation owner install/replace this one named frame pair, create the action-owned Animation with a zero-override FaceAnimationKeyFrame, and run an actual PlayAnimationAction owning its callbacks/cancellation? The native contract above is recoverable; absence of a current API is an IMPLEMENTATION_GAP, not an UNKNOWN native image format. Builder was sent these narrow interfaces and owns the C# audit. Full current M8-020 uncertainties remain visible; no full streamer re-read.

## Bouncer-only raster contract

Primary file for every address in this section: **resources/lib/armeabi-v7a/libopencv_imgproc.so**. The engine PLT imports match its exported C++ symbols. It embeds the build path opencv-3.1.0/modules/imgproc/src/drawing.cpp at0x000C93E7. Source availability does not depend on external web/source retrieval. All image channels for Bouncer are one8-bit grayscale; color255; line8; no AA; no thickness>1 outlines; no other fonts/text characters.

### Filled paddle rectangle

Native Mat+Rect overload00045E54 checks width*height>0 (00045EA2..AA); on success forms corners(x,y) and(x+width-(1<<shift), y+height-(1<<shift)) at00045EB8..EE. shift0 thus subtracts1 from width/height endpoint. It calls the point overload00045CF4. That forms4 corners at00045DBA..DDC; negative thickness routes00045E06..E10 to FillConvexPoly00042958 with line8,shift0. Empty/nonpositive area draws nothing.

M5-021 already owns the checked FillConvexPoly/Line/LineIterator/clipLine algorithm in this same shipped binary. Only this new caller/LINE8 choice is added; the underlying fill is not unrecovered. Reuse that accepted source implementation where its existing LINE8 branch is present. Source specializes on these axis-aligned corners to all integer pixels x in[left,right] and y in[top,bottom], clipped to0<=x<128,0<=y<64; equivalently rounded Rect coordinates with x<left+roundedWidth and y<top+roundedHeight. Do not independently round right and subtract rounded left to create width: the **engine caller passes width float then adapter rounds width**, as frozenD2/D5 specify. Rectangle helper's subtract1 acts AFTER integer width rounding.

Evidence for filled extent: FillConvexPoly000429A6..BA selects half-unit edge biases for line<=15;00042A38..54 draws boundary with Line;00042AD0..42B12 clipped row extent;00042B8E..94 edge dx;00042C32..56 inclusive rounded/clipped x limits;00042C62..7E pixel loop includes end; Line8 boundary helper is the same checked M5-021 routine. Axis-aligned vertical edges have dx0; horizontal boundaries include both ends. No bionic trig participates in this specialization.

### Filled radius3 ball

cv::circle000466F4 sees thickness-1<=1,line8<=15,shift0 and branches00046798->000467D6, sets fill=1 at000467D8, calls static Circle00040C0A at000467E6. No ellipse/AA path is reachable for these parameters.

Circle initializes dx=radius3,dy=0,err=0,plus=1,minus=5 (00040C2A..30,00040C62..6E). While dx>=dy, fill inclusive horizontal spans at y=centerY±dy,x=centerX-dx..centerX+dx and y=centerY±dx,x=centerX-dy..centerX+dy. Then dy++, err+=plus; plus+=2; if err>0:dx--,err-=minus,minus-=2 (00040F60..F96). Pixel fill loops00040D02..1A/00040D78..90; clipped path00040DD0..DDD/00040E26..42/00040EAC..EB4/00040EEE..F0A. Rows out of image omitted, horizontal spans clipped to0..127.

The EXACT radius3 mask, centered on the already-rounded native center, is:

| dy | inclusive dx span |
|---|---|
| -3 | 0..0 |
| -2 | -2..2 |
| -1 | -2..2 |
| 0 | -3..3 |
| 1 | -2..2 |
| 2 | -2..2 |
| 3 | 0..0 |

Derivation iterations(dx,dy,err before drawing): (3,0,0),(2,1,-4),(2,2,-1); next(1,3,1) exits. These29 pixels are a consequence of this routine, not a distance<=radius guessed disk. Center may round to128 or64; clip the mask, do not move its center inward.

### Exact decimal glyph data and fixed-point strokes

putText00046370 uses font argument0x32. getFontData00040AAC extracts low-nibble2 and bit4; its jump-table element2=04 at00040ACC goes to00040AD2, returns font table000E62D0+300=**000E65D0**. This case ignores the italic bit. The table header is0x10C9, low nibble9 is baseline magnitude. ASCII digits have table indices2700..2709 at font[char-31]; putText gets the glyph pointer through the relocated pointer-array GOT000FDB84->000FE008. Only decimal digits are reachable from the frozen unsigned to_string calls; no negative sign or locale punctuation.

The table below quotes the literal glyph bytes, JSON escaped to preserve the backslash in their bearings. Each string's first2 bytes are left/right bearing in units relative ASCII 'R'(82). Here every digit begins H-backslash: left=-10,right=10. Remaining byte pairs are points(u=byte-82,v=byte-82); a space separates polylines; NUL ends the glyph.

| Digit | index | glyph VA | Exact bytes (JSON string) |
|---|---|---|---|
| 0 | 2700 | 0x000D755B | `"H\\QFNGLJKOKRLWNZQ[S[VZXWYRYOXJVGSFQF OGMJLOLRMWOZ NYQZSZVY UZWWXRXOWJUG VHSGQGNH"` |
| 1 | 2701 | 0x000D75AC | `"H\\NJPISFS[ NJNKPJRHR[S["` |
| 2 | 2702 | 0x000D75C4 | `"H\\LKLJMHNGPFTFVGWHXJXLWNUQL[ LKMKMJNHPGTGVHWJWLVNTQK[ LZYZY[ K[Y["` |
| 3 | 2703 | 0x000D7606 | `"H\\MFXFQO MFMGWG WFPO QNSNVOXQYTYUXXVZS[P[MZLYKWLW POSOVPXS TOWQXTXUWXTZ XVVYSZPZMYLW OZLX"` |
| 4 | 2704 | 0x000D7660 | `"H\\UIU[V[ VFV[ VFKVZV UILV LUZUZV"` |
| 5 | 2705 | 0x000D7681 | `"H\\MFLO NGMN MFWFWG NGWG MNPMSMVNXPYSYUXXVZS[P[MZLYKWLW LOMOONSNVOXR TNWPXSXUWXTZ XVVYSZPZMYLW OZLX"` |
| 6 | 2706 | 0x000D76E4 | `"H\\VGWIXIWGTFRFOGMJLOLTMXOZR[S[VZXXYUYTXQVOSNRNOOMQ WHTGRGOH PGNJMOMTNXQZ MVOYRZSZVYXV TZWXXUXTWQTO XSVPSOROOPMS QONQMT"` |
| 7 | 2707 | 0x000D775B | `"H\\KFYFO[ KFKGXG XFN[O["` |
| 8 | 2708 | 0x000D7772 | `"H\\PFMGLILKMMNNPOTPVQWRXTXWWYTZPZMYLWLTMRNQPPTOVNWMXKXIWGTFPF NGMIMKNMPNTOVPXRYTYWXYWZT[P[MZLYKWKTLRNPPOTNVMWKWIVG WHTGPGMH LXOZ UZXX"` |
| 9 | 2709 | 0x000D77F7 | `"H\\WPURRSQSNRLPKMKLLINGQFRFUGWIXMXRWWUZR[P[MZLXMXNZ WMVPSR WNUQRRQRNQLN PRMPLMLLMIPG LKNHQGRGUHWK SGVIWMWRVWTZ UYRZPZMY"` |

putText converts scale0.5 to integer fixedpoint32768 using vcvtr(scale*65536), sets viewX=originX<<16, viewY=(originY<<16)-9*32768 (bottomLeftOrigin=false), then per glyph starts X=viewX-leftBearing*32768. Each point is(X+u*32768,viewY+v*32768). Polyline endpoints are retained in16.16. Glyph advances(right-left)*32768=655360=**10 pixels**, even though the frozen caller aligns the opponent using8*stringLength. Do not substitute an8-pixel advance. Actual caller origin is(4,12) or(128-4-8*len,12), so Y=7.5 before adding glyph-v/2.

Instruction evidence:000463BC..C8 header/base;000463C0 literal65536.0;000463CE..DC scale conversion;000463F0..6412 baseline/origin;00046458..68 glyph lookup;0004646C..78 bearings;0004648C..D0 stroke separators;000464D2..E8 coordinates;00046550..56 advance. ASCII digit validation is the simple bounded branch in00042D50 (char<=0x7F) to00042DDA; no UTF8 paths needed.

Each polyline with>1 point calls00045BF4 with thickness1,line8,shift16 (000464AE..C0). The wrapper calls ThickLine00045010 per consecutive pair (00045C66..B6), open polyline, no closing segment. ThickLine with thickness1,line8,shift16 goes through00045074..8A to**Line2 0004137C** via000450C6..D4, NOT to an AA or generic thick-line polygon. Its shift correction(16-shift) is0.

### Line2 exact used algorithm (one-channel branch)

This is the new fixed-point-line leaf used by digits; clipLine is already accepted under M5-021. Coordinates here are signed16.16 integer32. clipLine uses Size(128<<16,64<<16), i.e. right=(128<<16)-1 and bottom=(64<<16)-1. It rejects shared outside-code bits; clips Y before X; signed64 multiply/divide truncates toward0; second endpoint uses the already-updated first endpoint (000410DC..00041378, M5-021 A10). Do not replace it with floating clipping.

After clipping, let dx=x2-x1,dy=y2-y1,ax=abs(dx),ay=abs(dy). X-major only when ax>ay; a tie is Y-major. Normalize the major-axis direction positive by swapping endpoints if that major delta is negative (000413E0..00041458 X,00041488..C6 Y). Recompute the corresponding signed minor delta for swapped endpoints.

- X-major: xStep=65536; yStep=truncTowardZero((signedDy<<16)/(ax|1)); count=(x2-x1)>>16.
- Y-major: yStep=65536; xStep=truncTowardZero((signedDx<<16)/(ay|1)); count=(y2-y1)>>16.

The signed64 division is00041466..82 or000414CC..E8. Add32768 to both START coordinates (000414EC..FA). First plot the END endpoint independently at((x2+32768)>>16,(y2+32768)>>16), subject to ordinary0<=x<128,0<=y<64 (000415E6..00041610). Then:

- X-major: start integer x=(startX+32768)>>16; keep fixed y=startY+32768. For count down through0 inclusive, plot(x,y>>16) in bounds, x++,y+=yStep (00041612..56).
- Y-major: start integer y=(startY+32768)>>16; keep fixed x=startX+32768. For count down through0 inclusive, plot(x>>16,y) in bounds, y++,x+=xStep (00041658..96).

Plot writes byte255 (one-channel branch), never blends, and duplicate endpoint writes are harmless. Signed arithmetic right-shifts preserve native negative-coordinate rounding; this is half-up via fixed-point bias at this stage, distinct from caller vcvtr ties-to-even. Glyph data, scale/bearings/baseline, native clipLine and this Line2 contract determine the complete reachable digit pixels. Generic thick lines, AA, other channel counts, arbitrary fonts and arbitrary characters are outside the used path and were not extracted.

No runtime reference corpus was executed: unicorn unavailable in this reader's Python313 environment and read scope forbids adding/installing tools. The contract is instruction-derived and must receive the manager's independent source check before implementation. This is not a claim of tested equivalence or permission to modify the accepted M5-021 record.

## Interface/ownership outcome and remaining questions

- Native writer and transport for DC, exact used glyph/raster constants and the one-frame manager join are recovered. They can be built from this checked supplement without inventing native behaviour. A missing C# typed field/registry/action adapter is an implementation gap, not a native unknown.
- Full actual face recognition/detection remains **M11-016 RECOVERABLE_GAP, explicitly deferred by operator**. That is different from external SDK machinery or BLOCKED_EXTERNAL. Until it is ported, a supplied detector can carry the exact third-direction value but cannot prove live OKAO fidelity. Carrying0 as an absent/default value must not be claimed as native detected orientation.
- Existing M8-020 shared action lifecycle (completion union/cached Animation pointer and any unbuilt production seam) and M8-001 callback/cancel details remain their current gaps. This supplement does not relabel them or make SetFace success an elapsed-time event. Existing M5-013/M5-024/M5-025/M5-058 bodies are reused; no duplicate wire protocol.
- Host feature persistence/dispatch tail stays as the frozen named frontier; configured Bouncer uses the feature map value as-is. Nothing in this supplement enables Bouncer automatically.
- No new record, inventory/status/approval edit, settlement, regression/hardware test or commit. Root receives report for independent checking; builder owns C# interface verification. These bounded used paths did not require a new layer-wide audit. If the builder requires full detector or full action lifecycle beyond current shared interfaces, return that concrete interface to manager rather than filling it.

## Current whole-record excerpts (claims, not evidence)

```json
{
  "id": "M5-013",
  "title": "faceAnimations: one sprite frame per stream frame, empty frames skipped, two RLE variants per image chosen by _firstScanLine, index reset on abort",
  "status": "EXACT_SOURCE",
  "evidence": [
    "C12 0x004F9708..0x004F98C4; FaceAnimationManager 0x00581254..0x005812C4; GetFrame 0x005817B4..0x005817CA",
    "M3 B4",
    "R-ANIM pre-extraction part 1 item 5 5.1..5.8: Threshold 0x00871E98, libopencv_core 0x7A7A2, variant store 0x00581254, GetFrame 0x005817B4, GetStreamMessage 0x004F9812, IsDone 0x004F9770",
    "re-analysis/research/20261011-m5-settling-pass.md section 1, row M5-013 (Opus settling pass, manager-adopted 2026-10-11)."
  ],
  "unresolved": "Settled EXACT_SOURCE 2026-10-19 (re-analysis/research/20261019-m5-settling-a.md, manager-adopted): +0x28 override zero in production (M5-058). Superseded below: B-LOG-1 and \"CompressRLE != 0 unread\" belong to BufferFaceToSend; CompressRLE 0x00581904 errors only off 64x128 (0x00581916..0x0058191C), unreachable from DrawFace. Previous notes: Settling pass 2026-10-11: the FlatBuffer/JSON +0x28 add path is split out to M5-043. The threshold is strictly above 0x80, the variant and lazy index reset are settled, and an unknown name (0 frames) is done at once (C5). The writers of keyframe +0x28 (the IsDone override) are RECOVERABLE_GAP (0x004F9770, 0x004F97DE). B-M5-8 (2026-10-12), built, awaiting strong verification: ordinal case-sensitive name dictionaries and consumed zero-frame misses (0x005816D8, 0x00581790, 0x0058173A). Face-file loader recovery remains X-M5-9. B-M5-9 (2026-10-12), built, awaiting strong verification: typed GetNumFrames warning on exact map miss: Unknown animation requested: %s; lookup/consumption remains M5-013, loader is M5-052. B-M5-10 (2026-10-12), built, awaiting strong verification: Each GetNumFrames miss emits GetAnimationByName.UnknownName then GetNumFrames warnings, repeated by GetStreamMessage IsDone, in native order (0x005813B4, 0x005816DE, 0x00581714, 0x004F97A0, 0x004F97D4; vtable relocation 0x0101F0C0). The override gate still bypasses GetNumFrames. Second M5 pass: the +0x28 writer gap is M5-058. R-M5-TEARDOWN: the +0x28 override is natively always zero (M5-058), so IsDone/GetStreamMessage always go through GetNumFrames/GetFrame. Log-key check (2026-10-16, re-analysis/research/20261016-logkey-sites-check.md L3): BufferFaceToSend error is native sErrorF 0x0057C282 \"ProceduralFaceKeyFrame.GetStreamMessageHelper\" \"Failed to get RLE frame from procedural face.\" then _errG; stack key/text wrong. Fix: B-LOG-1. Condition equivalence with CompressRLE != 0 (0x0057C216) unread."
}
```

```json
{
  "id": "M5-021",
  "title": "Eye fill = shipped OpenCV 3.1.0 fillConvexPoly LINE_4 and ellipse2Poly; DrawEye outline and lid polygons; _firstScanLine offset; fill order",
  "status": "EQUIVALENT_IMPLEMENTATION",
  "evidence": [
    "gap1 D1..D6 DrawEye 0x0058520E..0x00585896",
    "gap3 A1..A10 libopencv_imgproc fillConvexPoly 0x00042F58, FillConvexPoly 0x00042958, Line 0x000419DC, LineIterator 0x00041810, clipLine 0x000410DC (matches stock 3.1.0)",
    "gap3 B1..B3 ellipse2Poly 0x00043C48 (SinTable 0x000E7910, vcvtr ties-to-even)",
    "re-analysis/research/20261011-m5-settling-pass.md section 1, row M5-021 (Opus settling pass, manager-adopted 2026-10-11)."
  ],
  "unresolved": "Settled 2026-10-12 by the final Opus pass (re-analysis/research/20261012-m5-final-settling.md): the OpenCv310 port matches stock 3.1.0 and the shipped libopencv_imgproc (edge dx 0x00042B8E..0x00042B94; fillConvexPoly, LineIterator 4, clipLine, ellipse2Poly ties-to-even, warpAffine nearest); the point transform holds (0x0053A8AC..0x0053A90C). Boundary: only the bionic tanf/cosf/sinf calls are EQUIVALENT_IMPLEMENTATION (unshipped system library, ruling 4.2); everything else is exact. Previous notes: Settling pass 2026-10-11 (ruling 4.2): the bionic tanf/cosf part is EQUIVALENT_IMPLEMENTATION (unshipped system library); the OpenCv310 port still needs re-verification. The trig in DrawEye and GetTransformationMatrix is MathF.Tan/Cos/Sin (bionic libm may differ in the last ulp, which can move a rounded point at a tie); the float operation order of the point transform is m00·x + m01·y + m02. Rounding assumes the default FPSCR mode (MD2)."
}
```

```json
{
  "id": "M11-016",
  "title": "Face, pet and motion detection",
  "status": "RECOVERABLE_GAP",
  "evidence": [
    "FaceTracker::Impl::Update",
    "FaceRecognizer",
    "OKAO_DT_* exports"
  ],
  "unresolved": "nothing recoverable: the detector is third-party binary code OV13 check (re-analysis/research/overnight/20261014-ov13-m11-055-check.md): the justification \"the detector is third-party binary code\" is weak under \"Exact, always\": the OKAO/OMCV code ships inside libcozmoEngine.so and its delete paths and mapping tables are readable (0x00933AAC, 0x00921944, tables 0x00E08E28, 0x00E34944, 0x00E594D8). Status question raised with the operator 2026-10-14. Reclassified 2026-10-14 by the operator (OV13 check, re-analysis/research/overnight/20261014-ov13-m11-055-check.md): BLOCKED_EXTERNAL was inaccurate, the OMRON OKAO/OMCV detector and recognizer ship inside libcozmoEngine.so and are readable. RECOVERABLE_GAP, ported as its own later layer after the current layers; nothing else waits on it. Until then the stack keeps its IFaceDetector/IOkaoFaceRecognizer seams. OV15-17 check (re-analysis/research/overnight/20261014-ov15-16-17-check.md): the OMCV_PD_* return values (GetVersion 0x0093392C, CreateHandle 0x009339CC, CreateResultHandle 0x00933B0C, SetAngle 0x00933DF0, SetSearchCycle 0x00933EEC, SetSizeRange 0x00933DB0, SetThreshold 0x00933E30, SetLostParam 0x00933EAC, SetSteadinessParam 0x00933F8C) decide whether VisionSystem::Init succeeds (M11-059 handoff)."
}
```

```json
{
  "id": "M14-001",
  "title": "TrackedFace geometry, FaceWorld match and forgetting rules",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x0086B244 is `movs r0,#1; bx lr`, so 0x004F43D6 `blx IsRecognitionSupported` / 0x004F43DE `cbz r0,#0x004F440C` never branches: the pose/overlap loop 0x004F440C..0x004F4536 is dead, and the reachable match is the map at FaceWorld+4 searched by lower_bound on the TrackedFace id at +0 (0x004F43EC..0x004F4556); an absent id creates an entry keyed by that id (0x004F45EA/0x004F45FC). The 220^2 constant 0x004F4428 and the overlap-0.5 instructions are on the dead branch.",
    "0x004F5380..0x004F53AC removes an unnamed entry when lastObserved+15000 is behind the last image timestamp.",
    "0x0087DE3C tests the parts-detected byte +0x30; 0x0087DE42..0x0087DE5E takes the eye centres +0x34..+0x40 through GetIntraEyeDistance 0x0087DC68 (sqrt(dx^2+dy^2)/cos(+0xec), divisor 1.0 when |cos|<1e-5, 6.0 when dist<1e-5); 0x0087DE60..0x0087DF1C derives the box eye distance |0.5*w| floored at 6.0; 0x0087DF20..0x0087E068 halves the (zeroed in the box branch) eye-slot sum for the midpoint, scales fx*62.0/eyeDistance (0x42780000) and parents the pose to Camera+0xc.",
    "0x004F4596 WasRotatingTooFast is on the new-entry path only; the found path 0x004F4558..0x004F456C is not gated. On the found path, a no-parts observation (TrackedFace+0x30 == 0) replaces the local pose translation with the entry's existing translation (0x004F4784..0x004F47A8) before writing it back to entry+0xf4 (0x004F47BE); the rotation stays from the TrackedFace pose."
  ],
  "unresolved": "built, awaiting strong verification (R-FIX, 2026-10-03): GetIntraEyeDistance and UpdateTranslation in binary32 in the engine's order (0x0087DC68..0x0087DDB0, 0x0087DE24..0x0087E068; epsilon 0x3727C5AC, 62.0 = 0x42780000 at 0x0087E084; the ray is GetInvCalibrationMatrix<float> 0x0085EF3A with no distortion model). Still open: the camera-frame translation is turned into the world pose with a double Pose3d; cosf is a MathF stand-in; the production detector that populates DetectedFace.RollRad is the M11-016 OKAO boundary.\nB-POSE-2 (2026-10-14, research/20261014-b-pose-1-check.md): item 8: MISSING (stack channel) at TrackedFace::UpdateTranslation: the native writes the camera-frame translation into the head pose, keeps its own rotation and calls SetParent(camera) (0x0087E042..0x0087E064); the stack's face consumers read a flattened world pose, so the previous flattening is kept."
}
```

```json
{
  "id": "M14-010",
  "title": "FaceTracker OKAO call order, detector output handling and enrollment gates",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x0086D776..0x0086D998 detects, enumerates raw results, creates TrackedFace entries and converts centre to square; 0x0086DA04..0x0086DB20 calls parts, expression, smile and gaze/blink in order.",
    "0x0086CCC6..0x0086CF32 and 0x0086D1F8..0x0086D2FC establish the OKAO parts/expression calls and five-result conversion.",
    "0x0086D474..0x0086D4A0 scales smile outputs by 0.01/0.001; 0x0086D5D0..0x0086D680 stores unscaled gaze and 0.001-scaled blink ratios under separate enable flags.",
    "0x0086E074..0x0086E1AC enforces 16/64/128/32 px and 25/45/10 degree gates; 0x0086E36C returns 16.0."
  ],
  "unresolved": "MISSING: the Anki-side handling operates on OKAO outputs (parts, expression, smile/gaze/blink values, face direction) that the IFaceDetector seam does not carry; the calls and the IsEnrollable gates are M11-016's third-party boundary. Nothing beyond the detector seam is buildable here."
}
```

```json
{
  "id": "M8-020",
  "title": "PlayAnimationAction: construction, Init, robot/game animation handlers, CheckIfDone, animationCompleted, destructor streamer stop",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/20261012-m8-play-animation-actions.md rows C1, C8..C11, P1..P12, D1..D3, L2..L8: ctor 0x00543C08..0x00543CBE (+0x84 loops, +0x8B 0, +0x8C interrupt, +0x90 null, +0x94 timeout; 60.0f -> FLT_MAX only when loops == 0), Init 0x00543F20 (cozmo_sings_custom remap 0x0054417C/0x00544190, lookup robot[0]->+0x20->+0x48, SetStreamingAnimation(robot+0x60, anim, loops, interrupt, flag) -> +0x8B, tag 0 -> failed, 0x03000001; R2E 0xCA/0xCB/0xD5 via 0x00519F8C and E2G 0x5F via external-interface vtable +0x28; callbacks 0x005452F1, 0x0054541D, 0x0054561D, 0x005457F5 and AnimationEvent forward 0x00545750), CheckIfDone 0x005441C0, GetCompletionUnion 0x005441DE (0x0075C544, 0x006C22DC), destructor 0x00543DAC (StillStreaming info 0x00543DF2 on robot+0x98 == +0x90; SetStreamingAnimation(null, 1, true, false) at 0x00543E26; releases +0xB4, +0xAC, +0xA4, +0x9C), vtable 0x01021584 slots +0x1C/+0x20/+0x2C (0x00545210).",
    "re-analysis/research/20261012-m8-play-animation-actions-check.md additions 2 (the null SetStreamingAnimation tail: +0x73 store and the idle Abort)."
  ],
  "unresolved": "Build after M8-015 (the action runtime), which it depends on, with M5-008/M5-023 (the streamer). It cannot settle while M8-015 is a RECOVERABLE_GAP. UNKNOWN: the C++ default constructor arguments. R-M9-029: the \"cozmo_sings_custom\" remap (0x00543F3A..0x00543F96) is reachable only from CodeLab (M9-029 policy); the stack rejects that name.\nB-M5-19 built, awaiting strong verification: driving calls now have a concrete ActionRunner bridge to existing animation playback, checked type 0x1A/timeout/track gate and terminal queue delivery. This is a partial bridge, not the complete native PlayAnimationAction port. Visible MISSING: CA/CB/D5/5F concrete-action subscriptions and exact start/end/event ordering, animationCompleted string union, cached Animation pointer retry/destructor including null==null idle Abort, and concrete track-stop lifecycle; completion currently comes from the existing playback ticket. The bridge still completes its action and the drive receives E2G 0x5A. No settlement or complete production-path claim.\nB-M5-20 (2026-10-15), built, awaiting strong verification: ported the checked C10 constructor rule: loops==0 AND binary32 timeout==60.0f (0x42700000) stores maximum finite float 0x7F7FFFFF; all other inputs, including adjacent floats and NaN payloads, are retained. Concrete-action MISSING is scoped once per drive via the existing stack diagnostic reporter, not the engine log. Full CA/CB/D5/5F/cached-pointer lifecycle remains unbuilt as already recorded. Ownership note 2026-10-16 (split from M2-023/M2-020; re-analysis/research/20261013-small-reads-2-check.md): the CA, CB and D5 handler bodies (0x005452F0..0x0054534E, 0x0054541C..0x005454F4, 0x0054561C..0x0054569E, 0x00545750..0x00545792) and their handle wiring are this record's. M2-022 answer (2026-10-16, re-analysis/research/20261016-m2-022-answer.md): subscriptions 0xCA/0xCB/0xD5 and E2G 0x5F (0x543FAE..0x5440F2); D5 gate +0x88 and tag byte vs +0x8B, log, AnimationEvent {ev+0 u32, ev+5 u8}, Broadcast via ctx+4 unchecked (0x545692..0x54577A); ~PlayAnimationAction releases four handles (0x543DAC..0x543E4E).\n\nB-M2M15-2 built, awaiting strong verification: TriggerLiftSafeAnimationAction now retains/replaces CA,CB,D5 and UI5F slots after real playback starts, using its own returned animation tag. CA match sets started after Info; CB requires start/tag and decrements finite u32 loops, zero loops never ends. D5 uses payload+4 tag/+5 event and forwards timestamp/event to typed API and E2G60 before local emit. UI5F compares full uint without started gate. CheckIfDone uses ended/failed flags, no ticket-completion success. Destructor releases UI5F,D5,CB,CA before base. Streamer Abort producer delivers app5F before RobotEventHandler/local action. Remaining completion-union/cached Animation-pointer lifecycle is not certified. Unit callback tests substitute direct Init, so only M5X19 real behavior/queue/streamer path is production coverage; firmware acknowledgments derive from delivered Start/End.\nB-DELIV-1 (2026-10-17), built, awaiting strong verification: 5F and forwarded60 app delivery use the common FIFO; raw/typed60 views remain adjacent and payload copies survive until dispatch. The existing synchronous local5F/60 subscribers and robot abort8D stay in their native place; only host invocation is deferred. Source authority is adopted research/20261017-b-m2m15-2-check.md finding1 (Broadcast6625B0 -> DeliverToGame661594 -> buffer65D8B8).\nB-M1-5 (2026-10-17), built, awaiting strong verification: Real ActionList queue/tick action Init and received CA/D5/D4 inputs cover 60-before-tap and 5F-before-RobotStopped. No direct Init or app producer substitution in these tests. Public ActionCompleted exposes the existing completion cache as such; no concrete completion-union serializer is invented. Existing completion-union/cached Animation-pointer lifecycle gaps remain. AWaitingHost is corrected to unit, not production coverage.\nB-M1-6 (2026-10-18), built, awaiting strong verification: Case-A regression now asserts host FIFO 5F, ActionCompleted, RobotStopped in the same tick. Existing completion-cache payload limitation retained."
}
```

## Primary artifact hashes

- libcozmoEngine.so: SHA256 `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`.
- libopencv_imgproc.so: SHA256 `3c4e3ff7e639c61e21c5ef91ea2cc4125bb9bd259ff53f5c0b1eeb9a95054830`.
