# Research request: the R-ANIM pre-extraction (M5 animation, M10 derived state)

- **Date:** 2026-09-29
- **Requested by:** manager (Claude)
- **Kind:** independent extraction
- **Answer file:** `re-analysis/research/20260929-R-ANIM-pre-extraction.md`. Write each part as it finishes, so a partial
  run still leaves finished parts.

## Why this is needed

Round-3 job R-ANIM (`re-analysis/jobs/R-ANIM.md`) closes the M5 and M10 gaps. Its first step is to send each
"missing source" question to an extractor. Answering them now takes that off its critical path. R-ANIM checks every
row before using it.

Follow `re-analysis/research/README.md` and `.opencode/agent/cozmo-extractor.md` exactly: one row per
behaviour-changing step, an address citation or UNKNOWN, and no guesses. The Cozmo engine code is mostly **Thumb**;
the Wwise and OpenCV parts are ARM. Cite branch veneers and PLT hops. Each record's current text is in
`re-analysis/fidelity_manifest.json`, and its approved rows are in `re-analysis/inventory/M5-animation.md` and
`M10-derived.md`. Start from them.

## Part 1: M5 animation

1. **M5-001:** `SetMembersFromJson` for the FaceAnimation, Event, DeviceAudio, RecordHeading and
   TurnToRecordedHeading keyframe types. What each reads, with defaults and failures.
2. **M5-006:** does the body-motion keyframe compare the radius tokens with `strcmp` (case-sensitive) or something
   else? Is the speed taken before or after the radius string is processed?
3. **M5-010:** ProceduralFace's reset data before a neutral face is loaded: the constructor and default values.
4. **M5-011 / M5-014:** the animation-group loader. What happens to an entry whose Name is not a loaded clip? What are
   the defaults of Weight, Mood and HeadAngleMin/Max_Deg when absent?
5. **M5-013:** which stored RLE variant `GetFrame` returns for which `_firstScanLine` value. Is the frame index reset
   when the keyframe finishes? Is `Image::Threshold(0x80)` "at or above" or "above"?
6. **M5-016:** the NamedColors table, with its names and values, and where the backpack-light JSON reader uses it.
7. **M5-019:** the face written back to the layer base by a streaming animation: the animation's own face (before the
   layers are combined) or the composed one?
8. **M5-023:** when both a streaming and an idle animation are set, whose FaceAnimation keyframe does the abort reset?
9. **M5-027:** who sets streamer `+0x64`? Does the Count-top flush refresh the budgets before or after its drain? What
   does `HasAnimationForTrigger` (`0x00670AD0`) test?
10. **M5-032:** the type of the face transform matrix in DrawFace: float or double, and where it is converted.
11. **M5-020 (the operator's decision; `re-analysis/jobs/R.md`):** Code Lab's random expression pick uses Unity's
    `UnityEngine.Random.Range(int, int)` (`unity/scripts/csharp/CodeLab/CodeLabGame.cs:3094`). Does the Unity runtime
    ship in the APK (`resources/lib/armeabi-v7a/libunity.so` or similar)? If it does, what are the generator, its state
    and its seeding, from the shipped binary? If it doesn't, say so: the record then names it.

## Part 2: M10 derived state

1. **M10-001:** the Anki `Radians` `operator>` / `operator<` at `0x0084CC90` (`diff > 0 && !IsNear(1e-5)`): the exact
   compare, and whether the difference is wrapped to (-pi, pi] first.
2. **M10-002:** B8, whether the +1 paths add l and r to the sums; B9, whether the same-sign decrement is guarded by
   count > 0. Start from the rows' addresses.
3. **M10-004 / M10-008:** `CompletelyUnlockAllTracks`: which locks it clears and what it sends.
4. **M10-008:** the reaction manager's constructor values at `+8` / `+0xC`, and MoveLiftToHeightAction's defaults.
5. **M10-003:** `EnabledStateChanged` for the Shaken, Slope and Frustration strategies.

## Out of scope

- Any code, inventory or manifest edit. No branches, commits or pushes.
- The records whose gap is wiring or a later build, not source: M5-018, M5-022, M5-030, M10-007 and M10-013.

## Answer format

Per part, the table from `.opencode/agent/cozmo-extractor.md` (`step | what the original does | citation | record |
classification`), then contradictions with the current record or rows, weak evidence, and open questions.
