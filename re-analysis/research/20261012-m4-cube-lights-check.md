# Opus check of R-M4-LIGHTS (2026-10-12)

- **Checked:** `research/20261012-m4-cube-lights.md` (Codex), against libcozmoEngine.so. Read-only cozmo-extractor.
- **Adopted:** by the manager on 2026-10-12. This file is the checked source for job B-M3M4-14.
- **Result:** every claim the report made checks out at its cited address. It left out the consumer of the blend result
  (now M4-045), and blending turns out to be driven by the animation data.

## M4-041

**BlendAnimWithCurLights** (0x00638B3C..0x00638BF0):
- **No active object** (GetConnectedActiveObjectByIdHelper 0x00638B4C, `cbz` 0x00638B52): the call returns 0 and the
  destination list is left as it is. It first logs a channeled info:
  - channel "CubeLightComponent" (0xBFA62F);
  - key "CubeLightComponent.BlendAnimWithCurLights.NullActiveObject" (0x638C24);
  - format "No active object with id %u found in block world unable to blend anims" (0xBFAA4C), with argument
    [ObjectID+4].
- **The copy:** clear the destination list (0x00638B56), then insert the source (0x00638B68).
- **The blend.** There is no floating point.
  - The flag starts at 0 once (0x00638B74).
  - For i = 0..3, GetLEDState(i) (0x00638B7C):
    - if node+0x14+4i == 0, store `rev [led+0]` and set the flag (0x00638B8C/8E);
    - if node+0x24+4i == 0, store `rev [led+4]` and set the flag (0x00638B98/9A).
  - The test is on the whole 32-bit word. The result is flag & 1 (0x00638BEA).
- **The caller:**
  - ApplyAnimModifier runs only when the bool argument is set (0x006384E2..0x006384F0).
  - Blend gets the modifier's output if the flag is set, otherwise the container's list (0x00638502..0x0063850C).
  - The AlreadyPlayingBlendedAnim info fires only when the stack's back entry is blended **and** Blend returned 1
    (0x00638510..0x0063853A).

**StopLignAnim.Result** (0x006390BA..0x00639300). It is a **debug** log (sChanneledDebugF), so sink (a) drops it on a
phone (M3-053).
- Update(bool) runs first (0x006390C2).
- The object filter: -1 means all objects, otherwise node+0x14 must be equal (0x0063918E..0x0063919A).
- The map is walked in ascending order (0x0063928C..0x006392B2).
- **The grammar**, for each object, with objects concatenated directly and nothing at all for zero objects:
  `object=<int> layer=<L> currLayer=<L> [` + for each entry `<name>:<EnumToString(trigger)>@<timer u32>[:STOP_NOW] ` +
  `]`.
  - The literals are at 0x6394EC..0x639524.
  - The layer names come from 0x0102EE64: {User/Game, Engine, State, Invalid}.
  - ":STOP_NOW" appears only if byte node+0x41 is non-zero.
- **The log:**
  - channel "CubeLightComponent";
  - key "CubeLightComponent.StopLignAnim.Result" (0xBFAA05);
  - format "%s anim '%s'. Current state: %s" (0xBFAA2C).

  The first %s is "found" (0x639538) or "did not find" (0x639528). It is "found" when any matching entry's trigger
  equals the requested one (0x0063907A..0x00639086).

**GetLEDState** (0x0063956C..0x00639600):
- An index of 4 or more logs IndexTooLarge: "Requested LED index is too large (%d > %d). Returning %d." (0xBFAB2B), with
  the arguments (i, 3, 3), and the index is clamped to 3.
- A negative index logs NegativeIndex: "LED index should be >= 0, not %d. Using 0." (0x639654), and the index is clamped
  to 0.
- It returns obj+0x0C+28·i.
- **Callers:** only 0x00638B7C (Blend) and 0x0063A642 (SetLights), both with i = 0..3. Both warnings are therefore
  unreachable.

## M4-045: the blend consumer in PlayLightAnim (new record)

- **B1, the branch:** blended = (modifierArg | BlendReturn) & !backBlended (0x00638590..0x0063859A). backBlended is the
  stack back's +0x40 byte, read only when the stack is non-empty (0x0063830C..0x00638326).
- **B2, what reaches it.** Blend runs on **every** PlayLightAnim. It returns 1 whenever a pattern has an all-zero on or
  off word.
  - Shipped data does reach it: fadeOff_1sec (DanceFadeOff), flash (Flash) and speedTapWin (SpeedTapWin) contain
    [0,0,0,0] colours. No other shipped file does.
  - So with a connected cube, these three take the blended branch and keep the cube's current colours in place of
    their zero words.
- **B3, the blended branch** (0x0063859C..0x00638690):
  - the per-layer slot node+0x44+12·layer := the blended list (0x006385B6);
  - the name is back.name + "+" (0x6389AC) + animName, or "" + animName if the stack is empty;
  - info +0x10/+0x14 point at the slot, and +0x38 blended = 1 (0x00638698..0x006386AE);
  - there is no zero-duration clear.
- **B4, the non-blended branch** (0x006385D4..0x006385F8):
  - the name is animName, and the info uses the container's list;
  - if the first pattern's duration_ms is 0, the layer stack is cleared before the push (0x006385EA..0x006385F4).
- **B5, edge cases:**
  - **backBlended, Blend 0 and the modifier set:** the non-blended branch plays the unmodified container animation,
    and the modifier output is discarded.
  - **NullActiveObject:**
    - modifier clear: the original animation plays;
    - modifier set: an empty slot is installed, and the timer read at 0x006386DA reads the list sentinel (undefined
      behaviour). That matters only if M4-046 finds a live modifier=true caller.

## M4-046: ApplyAnimModifier (new RECOVERABLE_GAP)

The body (0x00638A8C..0x00638B3A) is unread. So are the callers of PlayLightAnim 0x006382D0 / 0x006389FC that pass
modifier = true.

## M4-044: the cube-light loader

**Discovery:**
- The directory is "config/engine/lights/cubeLights" (0x51F80C). It is walked by WalkAnimationDir:
  - scope 1;
  - suffixes "json" and "bin", matched with no dot;
  - recursive;
  - files whose mtime can't be read are skipped with the "could not get mtime" warning;
  - the mtime gate always accepts in production.

  This walk is shared with M4-036/M5-050/M5-046, including the Results order.
- The callback 0x005241DE pushes onto the type-2 vector.

**Loading:**
- PushJob in vector order, then DispatchWorker<3> (0x0051FED8/0x0051FEE4), as M5-050 does.
- Then the LoadTime info:
  - channel "Animations";
  - key "RobotDataLoader.LoadCubeLightAnimations.LoadTime" (0x520058);
  - format "Time to load cube light animations = %.2f ms" (0x52008C), with the millisecond difference as a double.

**Per file** (0x00521C30..0x00521CA8). There is **no abort gate**, unlike the group loader.
1. readAsJson (M5-051: comments allowed, trailing commas rejected, the Failed to read error, errG, the reader-errors
   debug).
2. A non-empty result goes to DefineFromJson under the loader lock +0x26C. Its result is ignored.

**DefineFromJson** (0x00589910..0x00589CD8):
- **No names:** EmptyFile (0x00589C74), an error:
  - key "CubeLightComponent... DefineFromJson.EmptyFile" (0xBEF93F);
  - format "Found no animations in JSON" (0xBEF862);
  - then errG and break, and return 1.
- **More than one name:** the TooManyAnims warning (0x00589968), "Expecting only one animation per json file, found %lu.
  Will use first: %s" (0xBEF87E). The first name is in CZString order (memcmp, then length).
- **A root[name] with size < 1:** registers an **empty** animation and returns 0. A scalar, string included, has size 0.
- **Each entry:**
  - The scratch pattern starts as memclr 0x81 with the override byte = 1.
  - A missing "pattern" or "duration_ms" gives InvalidJson (0x00589C10): "Missing member field in %s json file" (root
    name), then errG and break.
  - ParseJsonToPattern false gives InvalidPattern (0x00589A2C): "Missing member field in one of the pattern definitions
    in %s json file", then errG and break.
  - duration is asUInt. canBeOverridden is asBool, if present.
  - patternDebugName is asString if present. Otherwise the default is name + to_string(i), e.g. "wakeUp0".
- **On failure:** the temporary list is cleared and the function returns 1; any earlier definition is kept.
- **On success:** operator[] and assign, so the last write wins.

**ParseJsonToPattern:**
- The order is onColors, offColors, onPeriod_ms, offPeriod_ms, transitionOnPeriod_ms, transitionOffPeriod_ms, offset.
  The results are ANDed, together with isMember("rotationPeriod_ms").
- The relative byte is **always 0**, and +0x84/+0x88 are zeroed (0x005894A4..0x005894C6). No "makeRelative" key is
  read.
- **The colour helper:**
  - a missing field is a silent false;
  - a size other than 4 logs "JsonTools.GetColorValuesToArrayOptional.DiffSizes" / "The json array and destination
    array are different sizes", then errG and break;
  - each component goes through asUInt, then uxtb, then ColorRGBA, then REV.
- **The array helper:** null, not an array, or a size other than 4 is a silent false. The elements are read with
  asUInt/asInt.

**Fatal paths:** std::terminate, with no catch.
- a JsonCpp stack-limit throw;
- getMemberNames on a non-empty array or scalar root;
- a non-empty object as an animation value;
- an entry or pattern that isn't an object;
- a wrong-kind or out-of-range as*() call.

**No shipped file reaches any error or fatal path.** All 37 were checked, including feeding/.

**What Lights.cs:578-647 and :804-832 must change:**
- discovery: json/bin, readdir order;
- the parser: JsonCpp semantics;
- the parse-failure log, which becomes M5-051's;
- the root: the first name only, plus TooManyAnims and EmptyFile;
- a scalar animation value registers an empty animation;
- the commit: replace, not TryAdd;
- canBeOverridden through asBool;
- patternDebugName defaults to name + index;
- makeRelative is never read;
- a short inner colour array yields 0;
- numbers through asUInt/asInt truncation;
- the exact log sequence, and the LoadTime info;
- PlayLightAnim: the data-driven Blend result, and the M4-045 branches.
