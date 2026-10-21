# R-M7-MOOD-JOIN — completion recipient construction and retirement

Request: `requests/20261021-m7-mood-completion-owner-join.md`. HEAD66c3dd9a. Independent read-only comparison pending checking; no status change.

## Coverage

| Step | Evidence / current source | Result |
| --- | --- | --- |
| Native owner | Adopted OV35 L1, Robot0x00510134..146 / Init0x00510356..35E | Per-Robot Mood allocation, not host Freeplay handle |
| Native registration | M7 inventory;0x0067AEE8..0x0067AF38 | Robot nonnull gates watcher registration; external-interface gate applies only separate C3 subscription |
| Dispatch input | Adopted0x0054187E..0x005418DE, checked watcher mapping | Deferred watcher records, not synchronous game/local5A callback |
| Type/category/time | Fresh HandleActionEnded0x0067B318..0x0067B400 | Full type+4, result high byte, one-shot tag suppression, native seconds at callback |
| Current owner | EngineRobot.MoodOwner nullable; only FreeplayStack binds MoodState | No owner/registration in ordinary no-Freeplay composition |
| Exactly once | FreeplayStack registers unconditionally when ActionList exists | Multiple handles can duplicate callbacks even for same MoodState |
| Retirement | Adopted0x0067AE18..30; current MoodLifetime/RobotLifetime | Normal null-ActionList skip preserved; fresh replacement not built |
| Proposal/tests | Actual Motion action test uses injected Freeplay/model/clock | Bounded real join test, not no-Freeplay owner availability or duplicate proof |

## Result

**An ordinary supported action completion does not reach a real Mood owner without host Freeplay construction.** The ActionList/Watcher exists and ordinary motion actions use it, but EngineRobot.MoodOwner is initially null; FreeplayStack.Create constructs/attaches the MoodState and registers the callback. This is a missing owner/init join, not an absent ActionList.

The existing hookup can deliver a completion to its mood once for a single created stack, and intentionally stays registered after host facade Dispose until native Robot retirement. However Create can register additional callbacks against the same owner, and its clock is a user-supplied clockSec rather than the native timer sampled at delivery. Neither “exactly once regardless of facade” nor “fresh owner after Robot replacement” is established.

## Adopted source and narrow instruction checks

Search/reuse:
- `re-analysis/inventory/M7-behaviour.md` action-result event production rows and later W13–W16 summary.
- `research/overnight/20261014-ov35-m7-023.md` and manager-adopted `overnight/20261015-ov34-35-check.md`.
- Existing ActionWatcher extraction/check0x0054187E..0x005418DE and action-result mapping; no affector or typed union re-extraction.
- `20261021-m8-actionlist-completion-settling.md` is a pending comparison lead for broad action completion; this report does not adopt its unreviewed findings.

OV35 L1: Robot allocates0x15C MoodManager, passes its own pointer and installs Robot+0x440 (0x00510134..0x00510146). Later constructor conditionally calls Init with shipped config (0x00510356..0x0051035E). Full missing/null-config behavior is not re-extracted here. This normal configured component precedes supported packet/action updates and exists independently of selected app activity.

Fresh registration block0x0067AEE8..0x0067AF42:
- loads Mood+0x12C Robot; null goes to0x0067AF50;
- HasExternalInterface result!=1 jumps to0x0067AF0E, not past watcher registration;
- external C3 subscription construction happens only on interface-present branch;
- common0x0067AF1E loads Robot+0x250 ActionList, builds raw-this callback and calls RegisterActionEndedCallbackForAllActions0x0067AF34;
- stores returned handle in Mood+0x158 at0x0067AF38.
Do not gate mood completion on presence of a game interface.

Watcher Update iterates completed deque records and registered callbacks in its adopted order, then removes the record; registration alone does not run the callback. The native callback reads the record at dispatch time, not the original action pointer after destruction. Input tag/type/result is sufficient here; union payload is unrelated to this scope.

Fresh entire HandleActionEnded executable/unwind0x0067B318..0x0067B400:
1. finds record tag in disabled set+0x140, erases and returns when found (0x0067B326..0x0067B33E);
2. loads full actionType/result words from record+4/+8 at0x0067B342, shifts result right24 at0x0067B348, stores category byte0x0067B350;
3. looks up that typed pair in map+0x134 at0x0067B354; miss returns;
4. hit formats enum names for diagnostic, then calls BaseStationTimer.getInstance0x0067B3C2, GetCurrentTimeInSeconds0x0067B3C6 and TriggerEmotionEvent0x0067B3D0.
Thus event time is the native float seconds **at callback invocation**, not action start, packet timestamp or a host Freeplay tick argument. Enum text is not the lookup key; unknown display name alone is not an additional native early-return condition.

Citation cleanup: current M7-020 evidence says “MoodManager::HandleActionEnded0x0067c770”. The symbol/body is0x0067B318, and0x0067C770 lies in unrelated emotion-scorer construction. The current M7 inventory's older per-instruction offsets within HandleActionEnded also predate the fresh offsets above (e.g. actual TriggerEmotionEvent call is0x0067B3D0). This is a primary-source citation correction, not a reason to invent new affector semantics. No manifest edited.

Retirement: adopted Mood destructor tests handle!=0, Robot!=null and ActionList!=null before unregister0x0067AE18..30. Normal Robot teardown has already cleared/nulled ActionList at0x00511150 before Mood deletion0x005111B6, so the unregister branch is skipped. It releases Mood-owned storage/subscriptions, then Robot nulls+0x440 after deletion. No last watcher Update is inferred during teardown.

## Current C# route

All paths under `cozmo-stack/src/Cozmo.Robot/`.

- EngineRobot has nullable MoodOwner (`RobotLifetime.cs`), no native-constructor MoodState/model/Init producer. Source search finds production new MoodState only in FreeplayStack fallback. Its callback owner is therefore unavailable before first facade Create; missing native Mood recipient is visible at lifetime retirement, not at each omitted completion.
- `Behavior/FreeplayStack.cs` Create sets ctx.Mood to existing Robot.MoodOwner or new MoodState(MoodModel.Load), unless caller already supplied one. AttachRobot sets output routing if Robot was null. It captures moodOwner as a concrete object, which correctly avoids mutable context-slot retargeting.
- It captures the current ActionList and calls RegisterActionEndedCallback whenever it exists. Callback maps int type/category through RobotActionType.NameOf / ActionResultCategory.NameOf and returns on either unknown, then calls moodOwner.HandleActionEnded(type,category,r.Tag.ToString(),clockSec()).
- The **string-keyed map/early return** is an existing M7-020 gap. Native typed map uses int32 type/u8 category; valid shipped enum cases can match the current adapter, but type/category values with failed name lookup are not proved no-effect. No new mapping policy is inferred.
- **Clock** is Create's clockSec, supplied by each host/test. It need not be engine Timer.SecondsF. Even if a normal tool supplies a similar stopwatch, that is not evidence it is the same cached native tick or float value.
- **Duplicate registration:** there is no “already initialized/registered for this Mood+ActionList” check before RegisterActionEndedCallback. Creating two stacks with default context on the same native Robot can reuse MoodOwner yet register twice. The callback-id member is overwritten by later BindNativeLifetime and C3 subscription may also be added again for the same native/owner. Mapped completion may therefore apply mood effects twice (subject to existing event/penalty behavior), not automatically exactly once.
- **Host disposal:** FreeplayStack.Dispose removes its _unsubscribe hooks and calls Manager.Dispose; it deliberately does not unregister action-ended callback (M7-023 comment). For a single still-owned native Mood this matches the separation between facade and native lifetime. Releasing a facade should not itself prove that engine mood stops receiving ordinary completions.
- `Behavior/MoodLifetime.cs` BindNativeLifetime rejects a retired Mood, another lifetime Robot, or a different Mood already installed. It sets MoodOwner and binds Robot+0x440 retirement. Freeplay's outer lifetime binder also rejects replacement AI/tracker before reaching mood rebinding. Native Robot replacement currently has no fresh Mood/model/init/registration producer.
- DestroyForRobot verifies ActionListOwner already null, retires subscriptions/maps/history and marks state retired; RejectRetiredUse preserves snapshots with M1-034 diagnostic. It does not fabricate unregister from a captured old ActionList. RobotLifetime clears MoodOwner after retirement. These meaningful guard/ordering behaviors remain credited; they do not establish missing construction or duplicate prevention.

Native mood callback comes from **watcher dispatch**, while local5A/report dispatch is a separate route. Wiring both would double-deliver even with one owner. No proposal to use public completion callbacks as a substitute is supported.

## Tests and minimal integration

`M7BatchThreeBTests.TheProductionRegistrationDrivesHandleActionEnded` creates a real FreeplayStack with an injected MoodModel/event and constant clock1.0, queues Motion.SetHeadAngleAsync, then drives real state/tick so watcher dispatch triggers the expected0.3 affector. It is a useful real motion→queue→watcher→mood hookup test. It does not test no-Freeplay construction, native model event selection/time, repeated facade registration or fresh replacement.

`M7LifetimeCheckedRowsTests` exercises actual removal ordering with explicit Mood binding/subscriptions, and verifies no unregister while ActionList already null. Those tests prove the bound retirement unit, not native constructor initialization. Direct HandleActionEnded tests cover suppression/map behavior only. No tests run here.

Smallest missing join proposal:
- construct/configure one Mood runtime per native EngineRobot through the existing model loader/Init contract; register once with that Robot's watcher when its native Init requires it, independent of host Freeplay;
- callback uses actual tag/type/u8 category and engine cached float clock at dispatch; retain native one-shot disabled-tag behavior and typed-map boundary;
- facade reuses that owner and does not register another closure or retire it; replacement creates a fresh native owner/registration while old callback storage dies with old ActionList before old Mood;
- retain current null-ActionList retirement guard and meaningful missing-init diagnostics. Do not require unused C++ control machinery where these outputs/lifetimes are preserved.

After source checking/inventory approval, independent regression: no-Freeplay connect with shipped model; finish one real supported action at known engine time, observe exactly one mapped event at watcher dispatch. Repeat after facade Create/Dispose and attempted second facade to show no extra registration. Test suppression once, category mapping, interface-absent registration, and Robot replacement with new owner. Capture native expected typed map/time and ordinary action results rather than using injected affection as the entire oracle.

Remaining UNKNOWNs: configured/null-config Init contract beyond adopted rows, intended arbitrary externally supplied MoodState/public overlapping-handle policy, complete model/typed-map migration, broader mood update/publication owners. No new policy chosen, affector arithmetic re-audit, typed union extraction, hardware or settlement.

## Current record quotes (66c3dd9a)

M7-012 — IMPLEMENTATION_GAP — Mood schema, decay graphs, action-result events, affectors, update and game output

> re-analysis/obb/assets/cozmo_resources/config/engine/mood_config.json

> StaticMoodData::ReadFromJson 0x0067ce6c

> MoodManager::Update 0x0067b5d4

> MoodManager::SendEmotionsToGame 0x0067b724..0x0067b7f8

M7-020 — IMPLEMENTATION_GAP — Mood event production, repetition penalty and affector application

> MoodManager::TriggerEmotionEvent 0x0067b85c

> MoodManager::AddToEmotion 0x0067bdca

> MoodManager::HandleActionEnded 0x0067c770

> EmotionAffector::ReadFromJson 0x00679910

> MoodManager::SendEmotionsToGame 0x0067b724

M7-023 — IMPLEMENTATION_GAP — Mood destruction with already-null ActionList dependency

> 0x0067AE14..0x0067AE74; 0x00511150; 0x005111B6; Callback-id/Robot/ActionList gates precede unregister; normal Robot teardown already nulled ActionList, so skips unregister. Remaining destructor children UNKNOWN.

> Manager-adopted re-analysis/research/20261005-B-M1M2-blockers-extraction.md T1â€“T9 and ownership table; instruction companion re-analysis/research/20261005-B-M1M2-blockers-native.txt

> re-analysis/research/overnight/20261015-ov34-35-check.md: L3 0x005110D4..0x005111C4 (owner nulled after delete 0x005111C4); gates 0x0067AE18..0x0067AE30; children 0x0067AE34..0x0067AE74 (0x004EAE94, 0x0067C0CC, 0x0067C0FC); subscription retirement 0x004EF184..0x004EF1CE, 0x00664EB8, 0x0066365C, 0x00663696, closure destroy 0x0067C228; sole caller BLX 0x005111B6 via PLT 0x004A7A80.
