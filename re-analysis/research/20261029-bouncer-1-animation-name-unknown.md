# BOUNCER-1 SetFace outer Animation name — one-literal supplement, 2026-10-29

Same Sol/medium reader request, actual backend unobserved. PROJECT_STATE reread; known.py queried SetFaceAction ctor00563870 and Animation::Animation before native inspection. Read-only except this report. No source/status changes, full M5/streamer extraction, tests/build, hardware, network or git mutation.

**The action-owned outer Animation name is `SetFaceImageAnimation` (21 bytes).** It is not empty and not `_PROCEDURAL_`.

Primary artifact: resources/lib/armeabi-v7a/libcozmoEngine.so. Exact native chain:

| Step | Instruction / data | Established result |
|---|---|---|
| Construct temporary name string | 00563978..7C clears temporary string;00563980 add r0,sp,#8;00563982 `adr r1,#0xFC`;00563984 `movs r2,#0x15`;00563986 `bl004E02B2` string construction | Source bytes atVA00563A80; explicit length21 |
| Literal bytes | VA00563A80: `53 65 74 46 61 63 65 49 6D 61 67 65 41 6E 69 6D 61 74 69 6F 6E 00` | ASCII `SetFaceImageAnimation` followed NUL |
| Construct action-owned Animation | 0056398A add r0,this,#0x168;0056398E add r1,sp,#8;00563990 blx004AC220 = Animation::Animation(string const&) at0057563C | Literal supplies OUTER Animation's ctor, independent from later keyframe name |
| Animation stores name | 00575642..46 clear leading string;00575648..54 short-string copy;00575658..5E long-string construction into r5=this | Name retained by owned Animation |

The separately checked later SetFaceAction::Init uses FaceAnimationManager::ProceduralAnimName for its FaceAnimationKeyFrame's sprite lookup. That existing `_PROCEDURAL_` keyframe/manager name does not change the outer constructor argument. IsLive=true and other already-frozen image/action fields are unchanged and not re-extracted.

No remaining UNKNOWN for this literal. No official record edit/settlement or full animation lifecycle claim. Report only, for independent source check before dependent bridge use.
