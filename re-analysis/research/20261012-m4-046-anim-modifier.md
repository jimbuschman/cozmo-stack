# M4-046 read: ApplyAnimModifier and the modifier=true callers (Opus extractor, 2026-10-12)

- **Run:** read-only cozmo-extractor.
- **Adopted:** by the manager on 2026-10-12. This is a checked source for job B-M3M4-14.

## ApplyAnimModifier (0x00638A8C..0x00638B3A)

`ApplyAnimModifier(src list, ObjectLights mod, dst list)`. `this` is unused.

1. **Copy.** Clear dst, then insert all of src (0x00638A9A, 0x00638AAC). If dst is empty, return (0x00638AB4/0x00638AB6).
2. **Per node, for i = 0..3:**
   - onColors[i] |= mod[+4i];
   - offColors[i] |= mod[+0x10+4i];
   - onPeriod[i] += mod[+0x20];
   - offPeriod[i] += mod[+0x30];
   - transitionOn[i] += mod[+0x40];
   - transitionOff[i] += mod[+0x50];
   - offset[i] += mod[+0x60].

   The ranges are 0x00638AC0..0x00638B02. The adds are wrapping 32-bit adds. The ORs work on the packed RGBA words.
3. **Rotation:** rotationPeriod (pattern+0x7C) += mod+0x70 **inside the i-loop**, so 4 times per pattern
   (0x00638B04..0x00638B0E).
4. **After the i-loop:**
   - the mode byte (pattern+0x80) := mod+0x74, overwritten (0x00638B14/0x00638B1A);
   - relativePoint (pattern+0x84/+0x88) := mod+0x78/+0x7C, a raw copy with no floating-point operation
     (0x00638B1E..0x00638B2E).
5. **Unchanged:** duration, canBeOverridden and name. There are no logs, no errG and no return value.

The layouts:
- **LightPattern:** offsets as in `research/20261012-m4-cube-lights-check.md`, with node = pattern + 8.
- **ObjectLights:**
  - onColors +0, offColors +0x10;
  - the periods +0x20/+0x30/+0x40/+0x50;
  - offset +0x60, rotation +0x70;
  - mode +0x74, point +0x78.

## In PlayLightAnim

- **The arguments:** the bool is [sp+0xCC], the lights are [sp+0xD0] and the int is [sp+0xD4] (0x006384DA..0x006384F0).
- **What Blend gets:** the modified list when the bool is set (0x00638502..0x00638506).
- **The overload** (0x006389FC) forwards the bool, lights and int with layer 1.
- **The timer:** every caller passes int = 0. The timer is now + firstPattern.duration + int when that sum is non-zero,
  and +0x44 := int (0x006386D0..0x006386E8).

## Callers (36 sites)

**Modifier = true:**
- **ActivityBuildPyramid::SetCubeLights** StopAndPlay (0x005A7D52..0x005A7D66) passes **true** with node+0x28. This is
  live freeplay: the Freeplay sub-activity, priority 14 (`activities_config.json:164`), and SparksBuildPyramid.
  - node+0x28 is filled by SetPyramidBaseLights (0x005A9238/0x005A928A) and UpdateDesiredLights (memcpy at 0x005A96F0,
    0x005A9758, 0x005A97C4; the sources are unread).
  - The getters:
    - **0x005A9960:** mode 2; point = the base midpoint; offsets {1000, 0, 2000, 1500}, only if both cubes are
      located.
    - **0x005A9A3C:** the same, without the offsets.
    - **0x005A9B00:** mode 0; offPeriod {575, 650, 725, 800}.

**Modifier variable, but never true in practice:**
- **BehaviorManager::SetBehaviorStateLights:** the modifier is info+0xC. All five callers build it as 0, or pass an
  empty vector, so it is always false in the shipped engine.
- **FeedingCubeController:** the modifier is +0xAC, set in UpdateCubeDrain and UpdateChargeLights. Feeding is entered
  through the app's ActivateHighLevelActivity(Feeding) (NeedsEnergyModal.cs:391).
- **IBehavior::SmartSetCustomLightPattern** passes true (0x005BE870), but has no caller.

**Modifier = false:** every other site, including:
- the actions: IDockAction, DriveToObject, IDriveToInteractWithObject, TriggerCubeAnimationAction;
- the component itself: the default layer, pose state, connection, delocalized;
- GatherCubes, CubeLiftWorkout, Dance, GuardDog, OnboardingShowCube;
- PlayCubeAnim and FlashCurrentLightsState (game messages).

The callers at 0x0055DE76 and 0x0055E0B0 (trigger 0xB) are unidentified.

## NullActiveObject with modifier = true (the M4-045 lead)

- **The ObjectInfo outlives the cube's connection.** It is created on connect (0x00639D4C), and no per-entry erase was
  found.
- **The slot gets an empty list.** With a null connected object, Blend leaves dst empty, and the blended branch assigns
  that empty list to the slot.
- **The timer read is past the end of the node.** The read at 0x006386DA loads mapnode + 0xD8 + 12·layer, beyond the
  0x68-byte node (0x0063AAEE / 0x0063B240). The value is the phone's heap contents and can't be reproduced. It sets
  `timer = v ? now + v : 0`.
- **The canBeOverridden read** (0x006386F0) reads the container's own pattern, so it isn't affected.
- **Reachability is UNKNOWN.** ActivityBuildPyramid erases its tracker on disconnect (0x005A7FD4 → 0x005A7FFA). These
  still need reading:
  - (a) the dispatch of HandleObjectConnectionStateChange to activities;
  - (b) its order against BlockWorld's removal;
  - (c) UpdateStateTrackerForUnrecognizedID 0x005A8004;
  - (d) whether Update or OnDeselected can light a cube that is gone.

## Manager decisions

- **M4-046** moves to IMPLEMENTATION_GAP, and is built in B-M3M4-14.
- **The BuildPyramid lights and tracker lifecycle** become a new RECOVERABLE_GAP under M15-freeplay, which owns the
  activity.
- **The NullActiveObject + modifier case:** until that M15 record is read, the stack gives a visible MISSING at that
  point, never a guessed timer. If the case turns out to be reachable, it needs an operator ruling.
- **M4-018:** the makeRelative path (0x00637E76) is reached live through BuildPyramid's modifiers. This is noted on the
  record.
