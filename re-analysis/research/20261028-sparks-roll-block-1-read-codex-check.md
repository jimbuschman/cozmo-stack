# SPARKS-ROLL-BLOCK-1 independent source review

2026-10-28. Requested reviewer model/effort: omitted/default; operator reports default Terra, actual runtime telemetry unavailable. PROJECT_STATE read first, followed by AGENTS, AUTONOMY, job, BEHAVIOURS, REVIEW and research README. Source phase only: no .NET, production/tests/official metadata, git or hardware changes. This file is reviewer-owned; original reports unchanged.

## Independent primary checks

Python313 at C:/Users/JimBu/AppData/Local/Programs/Python/Python313/python.exe; native resources/lib/armeabi-v7a/libcozmoEngine.so, expected SHA256 02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. known.py preceded new native reads for BehaviorRollBlock, ActivitySparked, ActivityIDFromString, BehaviorObjectiveFromString, SetBehaviorStateLights and PlaceBlockHelper. Native instruction output, assets and current manifest were inspected independently; C# and earlier reports are not primary evidence.

- Shipped activities/sparks/sparksRollBlock.json: Sparked, RollCube, Spark strategy, StrictPriority list DriveOffCharger / KnockOverCubes / SparksRollBlock / SparksPutDownBlock / SparksLookInPlace, minimum0, maximum80, repetitions1, objectiveBlockRolled, softSparkTrigger SoftSparkUpgradeLift. Behavior freeplay/sparkable/sparksRollBlock.json: RollBlock class, SparksRollBlock ID, needs RollACube_Sparked, executable RollBlock, unlock RollCube, isBlockRotationImportant=false.
- Fresh Roll ctor5C8598..8608: default true at85DC, JSON asBool at85F0, byte128 store85F4; target124=-1, state130=0. IsRunnable8650..74 refreshes target then returns target!=-1 OR current action84!=0. Init8676..86AC clears pre-dock latch129, checks actual target located, updates up-axis then starts Performing(false), else returns1.
- Transition5C86E8..8938: state0 before target guard; D9 or D8 gives zero face-turn else PI/2. Helper target and true bool are passed5C8804..8814; success/failure delegates at8850; acting score0.8 then state light target/25/false with wrapper boolfalse at88CE. Same exact receiver applies to configured rotation=false; no hard flag is forced by configuration name.
- Update5C8A54..8B24: only state0 and located target, changed local axis AND successful distance computation AND distanceSQ>14400; NaN fails native BLE. Update axis, StopActing(false,false), then latch129 ->success; otherwise refresh, target-1 ->2, else Performing(false). Shared base Update tail retained.
- Success5C8B2C..8C2E: state1, mood RollSucceeded, update axis; celebrate trigger1FB only when both D9/D8 false; score0.8, objective2 true, then NeedActionCompleted(0). Hard-spark source is actual manager state, not any arbitrary requested setting.
- ActivitySparked OnSelected5B1518..16DC: timestamp/counter/state reset; requested60 invalid55 OR soft64==0 emits hard outputs (driving, idle213, backpack, Started requested u32), then reaction locks; specialized delegate only when configured. Update5B186C..18D0 and CheckEnd5B18D4..1A16 preserve reaction gating, native epsilon deadlines, repetitions, current unlock comparison, cancellation and soft-to-hard interruption. SelectNext5B1F94..2066 calls actual delegate if present else base chooser, preserves waitfalse special branch. Reuse only exact supported configured arguments.
- Full setter5A4320..45F2 independently decoded: store flag85 first; old entries use first matching new ID, unmatched old StopResume; matched StopAndPlay irrespective trigger equality; updated-ID insertion even when playfalse; new pass only skips IDs already replaced, does not insert newly played IDs; retained full vector copied after calls, class84 stored. Modifier payload copies117bytes plus relative-point two words. Current C# setter implements these normal defined semantics. Other producers are outside current generic M7-030 own title.
- PlaceBlock Init5B8CB0..8D50 independently decoded: double PI/4..PI/2 draw, independent RandDbl(1)<.5 sign flip, one f32 narrowing, TurnInPlace(angle,false), callback scheduling return ignored. RespondToTurn5B8DF4..8E44 ignores result and constructs sequential Ground Add(false,true); Update5B8E60 returns status; RespondToPlaced5B8E64 stores2 ignoring result. Ground result does not justify bypassing this native callback. Inherited common Stop remains reused checked source, outside fresh audit.

Current M12-051, M7-030 and M15-077 literal title/status/evidence quotations were read before ownership reliance. All are IMPLEMENTATION_GAP. Generic helper and state-light record own paths must be assessed independently of neighboring Ground/shared-input gaps. M15-077 current title names Hiking_RollCube; additive whole-class widening, if proposed, needs operator/Claude and never a duplicate per-configuration record. Ground completion getter is explicitly unowned in these records; M12-050 owns PlaceRel only. Preserve Roll full-public M1-034 guard and defined typed/scalar/local/proxy/callback paths, inherited atan2f-body-only equivalence and named source-input frontiers.

## Verdict

READ PASS final corrected coherent source packet SHA256 1E4AFB6CCDF9FA476FA16A9F9F1278DA48FA6381621AC736B97D04F033F82F06. All13 literal title/status/evidence snapshots match current716 manifest exactly. Earlier C023 packet/verdict is superseded by the finite family/PoseState clarification below. One concrete inherited production defect was identified in source review and is now explicitly corrected in the source contract below. This is not a build/test or settlement verdict; root may release the bounded implementation with that required correction. Builder corrective-batch count remains zero before any builder submission; this source finding must remain separately logged.

## Current record quotations

```json
[
  {
    "id": "M12-051",
    "title": "PlaceBlockHelper lifecycle: factory and ctor, Init RNG turn, turn-to-Ground delegation, placed callback status, Update and inherited Stop",
    "status": "IMPLEMENTATION_GAP",
    "evidence": [
      "Factory 0x005B5588 (allocation 0xF0); ctor 0x005B8C1C..0x005B8C6A; ShouldCancelDelegates 0x005B8CA6..0x005B8CA8.",
      "Init 0x005B8CB0..0x005B8D50: double magnitude PI/4..PI/2 and an independent sign draw, narrowed once to f32, TurnInPlace(angle,false).",
      "RespondToTurnAction 0x005B8DF4..0x005B8E44 ignores the result and starts sequential PlaceObjectOnGround Add(false,true); Update 0x005B8E60 returns status; RespondToPlacedAction 0x005B8E64..0x005B8E68 ignores the result and stores 2.",
      "PB1-PB4 in re-analysis/research/20261007-q17-checked-additions.md; re-analysis/research/20261028-hiking-roll-cube-1-helper-response-clarification.md, -place-helper-proposal.md; re-analysis/research/20261028-hiking-roll-cube-1-claude-check.md"
    ]
  },
  {
    "id": "M7-030",
    "title": "BehaviorManager::SetBehaviorStateLights: ordered diff against the previous lights, stop/replacement/new play on cube-light layer 1, retained vector and class, and the switch cleanup after Init",
    "status": "IMPLEMENTATION_GAP",
    "evidence": [
      "SetBehaviorStateLights 0x005A4320..0x005A45F2; IBehavior wrapper 0x005BEE38..0x005BEE46; manager ctor 0x005A0918..0x005A092A.",
      "Lower calls: StopAndPlay 0x006396AC..0x00639722; PlayLightAnim overload 0x006389FC forwards layer 1 (the trailing integer 0 is not the layer).",
      "Switch cleanup MS4 0x005A2114..0x005A226E inside SetRunningAndResumeInfo after Init.",
      "re-analysis/research/20261028-hiking-roll-cube-1-state-lights-read.md, -state-lights-boundary.md, -lower-light-bindings.md with their independent checks; re-analysis/research/20261028-hiking-roll-cube-1-claude-check.md"
    ]
  },
  {
    "id": "M15-077",
    "title": "BehaviorRollBlock (Hiking_RollCube): cache 7/6 target choice, Init, TransitionToPerformingAction, RollBlockHelper, DriveToRollObjectAction and RollObjectAction, the axis-change retarget, success animation/objective/needs and stop/reset",
    "status": "IMPLEMENTATION_GAP",
    "evidence": [
      "Factory 0x0059CF86; ctor 0x005C8598 (isBlockRotationImportant default true 0x005C85DC..0x005C85F4); class range 0x005C867A..0x005C8E3C.",
      "TransitionToPerformingAction 0x005C86E8..0x005C8938: target -1 warns and returns; face-turn 0.0f when +0xD9 or +0xD8 set, else 1.5707964f (0x005C8734..0x005C8744, 0x005C8934); CreateRollBlockHelper(...,target,true,params) 0x005C8814; SmartDelegateToHelper 0x005C8850; score 0.8f 0x005C888E; state light {target,25,false} 0x005C88A6..0x005C88CE.",
      "UpdateInternal 0x005C8A54..0x005C8B24: axis change AND ComputeDistanceSQBetween(robot,target) > 14400.0f (0x005C8ABE..0x005C8AD2); StopActing(false,false); pre-dock latch +0x129 -> TransitionToRollSuccess; else refresh and +0x124 == -1 returns 2.",
      "Target cache 0x0056F9B4..0x0056FA18 / 0x005707F0..0x00570902 (intention 7 when rotation is important, else 6); RollBlockHelper 0x005B98D4..0x005BAC54; DriveToRollObjectAction 0x0055C9DC..0x0055CECE with IDriveToInteractWithObject 0x0055B2C8..0x0055E0E6; RollObjectAction 0x00556540..0x00556DCE with shared IDock 0x005514FC/0x005521AC.",
      "Read packets re-analysis/research/20261028-hiking-roll-cube-1-read.md, -drive-joins.md, -reentry-contract.md with their independent checks; HANDOFF-HIKING-ROLL-CUBE-1.md; re-analysis/research/20261028-hiking-roll-cube-1-claude-check.md"
    ]
  }
]
```

Additional bounded primary checks: native SHA256 freshly measured equals expected. Cache6 ctor56F932 saves filter pointer atsp18, inserts families fromC58A04 (raw2,1),56F97C resolves CanRollObjectDelegateNoAxisCheck viaGOT103ECA4;56FC6A..70 maps key6 to that filter. Best map56FDB8..FDD0 binds RollBlockBestObjectFunction forkey6;56FDE4..FDFC binds samefunction forkey7. NoAxis body57066C..7C4 independently read beyond the early return: whiteboard failure-kind3 gate, family1/2, restingflat, object-relative-to-main height(2,15,.5), then reject membership in stored configurations with size>=3. The ctor and predicates are concrete evidence forcache6, not inferred fromcache7.

After known.py SetRunningAndResumeInfo, fresh MS4 instructions5A2114..226E confirm recordM7-030 switch cleanup: nonempty retained vector, nonnull newbehavior, classdiff; clear when newbehavior identity differs BOTH oldcurrent/oldresume OR oldrunningbyte16 and newbyte!=16 and savedflagfalse. Every saved entry StopResume in order, clear end only; retainclass/flag. Current generic implementation matches this owned path.

After known.py554AE0, fresh raw Ground getter554AE0..FA confirms savedcarry+7C->sp0, halfword1 atsp14, no writes tosp4..10, concrete completion settercall554AF4. Current record exclusions are accurate: separate coherent Ground getter/availability owner is needed; no PlaceRel zero-unused policy extension or fabricated fullbody.

## Final packet comparison and required correction

Independently read the complete coherent packet. Its S1-S7/R1-R8/A1-A6 concrete caller/receiver applicability agrees with fresh native/asset evidence and matching checked shared contracts, with the new additive correction controlling all frozen earlier shorthand. Native table cells independently read:103339C->C19A44 SparksRollBlock,10330D8 same,1033DE0->C1C7C9 RollACube_Sparked,1033750->C1A0E7 RollBlock,10340E4->C1C062 RollCube,1033148->C19B93 BlockRolled,1032EA4->C19170 SoftSparkUpgradeLift. Report identifies these enum indices as behavior155/activity26/needs40/executable8/unlock9/objective2/trigger517; no numeric namespace substitution.

**F1 required inherited production correction:** current NativePickupTargetCache.cs43 reverses the native Roll predicate thresholds. At570696 MOVT r2,#4270 forms60f, storedsp4 by57069C;5706A2 MOVT r3,#41F0 forms30f, passed to DidFailToUse at5706AC. After known.py56BC40 and56BAB4, independently decoded wrapper savesr3 at56BC54 and forwards it unchanged56BC76; transfers stackpose/dist/angle56BC60..72. Core retains firstfloat inSL56BAC0, forwards56BAE8, stackdist56BAEE. Therefore time30s/distance60mm, not current60s/30mm. This applies both cache6 and7 through identical NoAxis predicate. Packet S5 and additive correction now state it accurately, flag production defect and require meaningful age45s/samepose versus fresh40mm-displacement regressions with remaining gates admissible. Frozen Hiking/Side/Low reports unchanged; this does not invent policy or weaken an official status. No code was edited by reviewer.

Own-record conclusions: M12-051 whole generic helper and M7-030 whole generic setter/MS4 are source-complete candidate paths pending independent code/regression/fidelity tags; their external neighbors do not make these own claims partial. M15-077 current Hiking own path can be a candidate only after F1 correction is independently verified; widening existing record across checked configured Roll callers requires operator/Claude. Ground getter ownership proposal is coherent and separate; raw getter guard preserves defined target/count/false/local/scalar/proxy/callbacks while fullpublic payload remains unavailable under M1-034. M15-059 and the other broad framework/selection/reward records remain explicitly partial. No extra perconfig record, full Ground zero filling, expanded atan2f policy, host timer, forced winner/terminal or no-effect claim is supported.

The later finite source-label correction is documented below; F1 remains the only confirmed behavior-changing production correction. Source findingF1 counts separately from later builder review corrections. No .NET run, build release, implementation/hardware pass, official metadata edit, status settlement or commit occurred in this review phase.

## Final finite family-versus-state clarification

Final sourcehash1E4AFB6CCDF9FA476FA16A9F9F1278DA48FA6381621AC736B97D04F033F82F06 re-read and all13 current snapshots compared exactly. Prior C023 hash and interim HOLD superseded.

Reader noticed S5/frozen Hiking used pose1/2 label for NoAxis5706B4..BA. Fresh primary instruction reads object+48, which is ObjectFamily1/2, not state+24; reviewer initial native note already named family but missed the packet label. The corrected source now consistently states family. After known.py5711E4, fresh571246..4E passes storedfilter and57125E..6C collects allnonnull results; no extra pose guard. After known.py61EB94, fresh61ED48..8E shows predicates then collector, matching checked CM13-05 generic source.

This does not establish a second supported-path behavior bug: current M11-007 title/status/evidence was reread and final packet quotes it exactly; existing checked located-object invariant has onlyKnown/Dirty. Fresh known-first50612C..1D4 confirms state0 branches506136->50617A diagnostic and returns without store506154; MarkUnknown deletion remains reused checked source, not a new broad audit. Do not fabricate Unknown located production fixtures. Current extraKnown/Dirty predicate is redundant on that invariant; optional source-alignment cleanup does not count as a second required behavioral correction. Exact family filter stays. Final READ PASS covers corrected source contract; code/test review and root release remain separate. F1 threshold reversal is still required before M15-077 own scope can be proposed complete.

## Strict UTF-8 quotation qualification and combined final verdict

This explicitly qualifies EVERY earlier 'all13 literal snapshots exactly match' statement in this reviewer note: **12 of13 were exact; one M8-015 evidence[1] quotation had an encoding-only mismatch**. The earlier comparison used an implicit platform encoding for the manifest; rerunning with explicit UTF-8 for BOTH files detects the T1/U+00E2,U+20AC,U+201C/T9 sequence in the source packet where the manifest has U+2013. Title, status, native addresses and behavior meaning are unchanged. This comparison mistake is acknowledged and corrected here, without editing the original frozen source packet.

Independently read and strictly compared the complete M8-015 JSON object in root's additive `20261028-sparks-roll-block-1-record-quote-correction.md` against CURRENT716 manifest using explicit UTF-8. Its title/status/evidence all match exactly. Raw SHA256 freshly measured:

- Original source packet unchanged: **1E4AFB6CCDF9FA476FA16A9F9F1278DA48FA6381621AC736B97D04F033F82F06**.
- Root quotation addendum: **FF10AA0D3A21F28DD62BFC4F52A2F4439B683E9BA822F0E74DA87FF5C345961E**.

**READ PASS for this combined artifact pair.** The addendum supersedes only the typographic M8-015 quotation. No new native, code or behavior correction round is introduced. F1 remains the only required production behavior correction; family-vs-state clarification remains the bounded nonbehavioral source-label correction on the checked located invariant. Builder corrective-batch count remains0 before first submission; source findings/clarifications are separately logged.

Bounded official-scope check: current M15 inventory row1085 cites factory/ctor/class/cache/helper/action native ranges; current M15-077 quoted title/status/evidence likewise names ranges and does not state time60/distance30. Correcting the IMPLEMENTATION_GAP production literal to native30/60 does not contradict a frozen opposite numerical claim and requires no new compatibility departure. No official file was changed. No .NET run or production edit occurred.
