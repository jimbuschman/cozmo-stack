| Item | Coverage | Outcome |
|---|---|---|
| Original Q9 left scale-X discrepancy | CHECKED | Native asset word rechecked; original compiler result differs by one ULP. |
| Full neutral-eye arrays (38 words) | CHECKED | Three original words differ; all 38 proposed replacement-array words match. |
| Current production callers and ownership | CHECKED | Raw-face API uses ShippedNeutral; streamer loads asset-backed pose separately. |
| Formal fidelity settlement | PARTIAL | Manager approval required; no record changed. |
| Other queue PARTIALs and audio renderer | PARTIAL | Not resolved by this bounded float investigation; existing blockers remain. |

Research follow-up to the operator's request to resolve the remaining evidence. This report closes the neutral-literal investigation, not the whole queue or any fidelity record. It corrects Q9's method naming/ownership shorthand: the actual method is `ProceduralFacePose.ShippedNeutral()` at ProceduralFace.cs:349, not `Neutral()` and not an AnimationStreamer fallback.

Current records, quoted before the findings:

> M5-002: 19 procedural-eye parameters and the ProceduralFace default (all 0 except EyeScaleX/Y 1, face scale 1, no distorter); SetFromFlatBuf rules. Status: EXACT_SOURCE.
> Evidence: ["C6 SetFromFlatBuf 0x005838D0..0x00583AAC; SetFacePosition 0x00583B20..0x00583BF8", "gap3 K4 ProceduralFace() 0x00583660..0x005836A0 (table 0x00C5A970 = {2,3})"]

> M5-010: Neutral face: the first ProceduralFace keyframe of the first clip of ag_neutral_face; reset data and layer base; replayed after abort-to-nothing and RemoveIdle. Status: EXACT_SOURCE.
> Evidence: ["A2 0x0057A0D4..0x0057A20E, ProceduralFace::Reset 0x0058359C; AnimationTriggerMap.json:1308-1309", "A30 0x0057BA60..0x0057BD6E", "A31 0x0057CF6A..0x0057CFF2", "R-ANIM pre-extraction part 1 item 3 P1..P8: _resetData GOT 0x103ED4C, Reset 0x0058359C, TLC::Init 0x0064EDE2, streamer ctor 0x0057A0D4..0x0057A288, default face 0x00583660"]

> M5-020: Expressions: the shipped Code Lab AnimationTrigger mapping and Unity Random; no invented faces. Status: IMPLEMENTATION_GAP.
> Evidence: ["operator decision 2026-09-29: no invented faces; rebuild on the shipped Code Lab expression mapping", "CodeLabGame.GetAnimationTriggerForScratchIndex CodeLabGame.cs:3094, Random.Range(1,34) :3101, Random.Range(1,14) :3106", "R-ANIM pre-extraction part 1 item 11 R3..R8: RandomRangeInt 0x10BCE8, xorshift128 0x10BD00..0x10BD2C, InitState 0x10BC00, time(NULL) seed 0x71ED4..0x71F04"]

The scalar defect is in the helper's asset copy, rather than the M5-002 constructor default or the asset-loaded M5-010 reset path. M5-020 provenance explicitly describes ShowExpression/HoldExpression retaining the shipped neutral face; these entry points actually consume the rounded copy. No claim about its still-unresolved RNG seed is changed.

| Step | Citation | Behaviour / gate / order | Result or remaining uncertainty | Float width and bits |
|---|---|---|---|---|
| N1 | anim_singlepose_01.bin root/clip/keyframe vectors; companion native fixture | Follow FlatBuffer root to clip named anim_neutral_eyes_01, first ProceduralFace, left/right fields 6/7, each length 19. Preserve eye and parameter order. | Native bytes independently establish all 38 expected words. | binary32 throughout. |
| N2 | asset 0xF8; ProceduralFace.cs:354 | Left EyeScaleX (parameter 2). | DEFECT: original literal is one ULP high. | Native 3F9B6F43; C# 3F9B6F44. |
| N3 | asset 0xFC; ProceduralFace.cs:354 | Left EyeScaleY (parameter 3). | DEFECT: original literal is seven ULPs low; omitted from Q9 because 0.90528 is outside the baseline. | Native 3F67C075; C# 3F67C06E. |
| N4 | asset 0xAC; ProceduralFace.cs:360 | Right EyeScaleY (parameter 3). | Same defect as N3. | Native 3F67C075; C# 3F67C06E. |
| N5 | engine 0x00583790..0x0058382E; Q9 native companion | SetEyeArrayHelper requires 19 floats, passes each to Clip then stores at eye-specific offset. | Existing native bounds/NaN semantics are not replaced by guessed behavior. The values in question are ordinary finite scales. | Word loads/stores preserve f32 input before the existing Clip rules. |
| N6 | CozmoAnimations.cs:560,564 | CozmoFace construction and ResetToConstructed call ShippedNeutral. | Retained Current carries the three incorrect words before a caller renders it. | f32. |
| N7 | CozmoAnimations.cs:583,598..601 | Neutral branch of ShowExpression renders via SetParameters -> renderer -> Display.Show; HoldExpression renders -> Display.Hold. Other expressions use trigger path. | Fix only the three constants; do not change routing, duration or nonneutral behavior. Whether a ULP changes any bitmap for a particular geometry is not established by this scalar comparison. | f32 pose parameters. |
| N8 | AnimationScheduler.cs:607..637 | Streamer resolves and loads neutral clip separately; it does not call ShippedNeutral. | No streamer defect is inferred from the helper literals. | Asset f32. |

Exact proposed implementation: replace only left EyeScaleX with `BitConverter.UInt32BitsToSingle(0x3F9B6F43)` and both EyeScaleY literals with `BitConverter.UInt32BitsToSingle(0x3F67C075)`. No production edit was made in this research task.

The companion JSON fixture contains the 38 native words and byte offsets, generated directly from the shipped asset. The companion net9.0 compiler probe (SDK 9.0.201) copies current source literals and the three proposed bit-pattern substitutions: original 35/38 match; proposed 38/38 match. This is a literal-conversion check, not a production regression test. A builder regression must call the actual ShippedNeutral(), compare every parameter against the checked-in native fixture, and exercise ShowExpression/HoldExpression through their normal entry if claiming downstream output. It must not calculate expected values using the implementation.

Remaining queue gaps are unchanged: M5-013 alias/bulk-store writer proof, whole-runtime ADP renderer/corpus, broader M6 seams and higher-layer dependencies, exhaustive helper/alias test provenance, and hardware/live-entry coverage. Formal settlement remains with the manager. This closes the bounded neutral-value evidence without inventing those missing paths.
