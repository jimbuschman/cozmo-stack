# HIKING-ROLL-CUBE-1 — lower light bindings

2026-10-28; finite builder source questions, no code/tests/official metadata/git edits. Frozen artifacts unchanged. Known.py preceded EnumToString and StopLightAnim original-ELF reads; sameSHA02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. Current M4-018 and M4-041 full title/status/evidence are already quoted in frozen state-lights-read packet before this reliance; no expansion or status change is claimed.

## Trigger identity

Native EnumToString(CubeAnimationTrigger)7655F0..5600: unsigned value>40 returnsnull; otherwise indexes pointertable01032F90 withstride4. All41cells and C strings below were read directly. The actual Roll tuple25 is **InteractingBehaviorLock**, distinct from24 Interacting. Shipped asset `obb/assets/cozmo_resources/assets/cubeAnimationGroupMaps/CubeAnimationTriggerMap.json` confirms InteractingBehaviorLock→interactingBehaviorLock. Asset array ordering alone was not used as ordinal evidence. No API enum is invented from C# candidate strings.

|Ordinal|Pointercell|Native name|
|---|---|---|
|0|01032F90|Carrying|
|1|01032F94|Connected|
|2|01032F98|CubePouncePlayerLose|
|3|01032F9C|CubePouncePlayerWin|
|4|01032FA0|Dance_01|
|5|01032FA4|Dance_02|
|6|01032FA8|Dance_03|
|7|01032FAC|Dance_04|
|8|01032FB0|Dance_05|
|9|01032FB4|Dance_06|
|10|01032FB8|DanceFadeOff|
|11|01032FBC|DrivingTo|
|12|01032FC0|FeedingBlank|
|13|01032FC4|FeedingCyanCycle|
|14|01032FC8|FeedingCyanSolid|
|15|01032FCC|Flash|
|16|01032FD0|GatherCubesAllCubesInBeacon|
|17|01032FD4|GatherCubesCubeInBeacon|
|18|01032FD8|GuardDogBeingMoved|
|19|01032FDC|GuardDogBusted|
|20|01032FE0|GuardDogOff|
|21|01032FE4|GuardDogSetup|
|22|01032FE8|GuardDogSleeping|
|23|01032FEC|GuardDogSuccessfullyFlipped|
|24|01032FF0|Interacting|
|25|01032FF4|InteractingBehaviorLock|
|26|01032FF8|Onboarding|
|27|01032FFC|PyramidBaseBottom|
|28|01033000|PyramidFlourish|
|29|01033004|PyramidOnSideLocated|
|30|01033008|PyramidOnSideNotLocated|
|31|0103300C|PyramidPickup|
|32|01033010|PyramidSingle|
|33|01033014|Sleep|
|34|01033018|SleepNoFade|
|35|0103301C|SpeedTapWin|
|36|01033020|SpeedTapLose|
|37|01033024|Visible|
|38|01033028|WakeUp|
|39|0103302C|Workout|
|40|01033030|Count|

40 is the native Count sentinel; unlike0..39 it is not a shipped playable animation pair. Unknown ordinals retain exact raw identity; no string alias to another trigger is allowed. This data table supports canonical raw ordinal/name binding for arbitrary retained state entries without changing the PlayLightAnim missing-trigger gates.

## Specific missing ObjectID in StopLightAnim

Existing checked `20260929-R-DEV-pre-extraction.md` W2 establishes matching-node stop flags then Update(bool), M4-018/M4-041 own lower stop/update/result diagnostic. The finite original-instruction recheck settles the absent positive-ID join:

- StopLightAnim638F58 logs the stop request first, at638F9C, using rawtrigger/layer/ObjectID.638FC2 readsObjectID+4; ONLY -1 takes all-objects branch639032. Every other ID, including missing positiveID, goes through map.find at638FD8;638FE0 compares iterator to mapend638FE2. Missing branches directly6390B6. No invalid-object lookup warning/early return is inserted.
- Missing or empty requested layer sets foundflagFP=0 at6390B6. Otherwise scan every entry of that object's requested layer, setnode41=1 whenever node.trigger14 equalsrequestedtrigger (63901A..2E), retaining foundflag. No found-entry result changes the later Update call.
-6390BA reloadscomponent;6390BE loads suppliedbool;6390C2 unconditionally calls actual CubeLightComponent.Update(bool). This occurs even for absent positiveID and can affect other currently running light entries on the component. Then existing result serialization/log path6390C6 onward records found/notfound; no Type-only wrapper may silently skip this call.
- For unmatched state-light removal, StopLightAnimAndResumePrevious supplies actual layer1 and its checked resume Update flag; for matched replacement, StopAndPlay supplies layer1,false as frozen H7 source. Preserve each exact argument and normal return/log behavior; no fake known cube type or new ObjectInfo is created just to stop an absentID.
