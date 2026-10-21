# R-M7-MOOD-INIT — configured and missing-input boundary

Request: `requests/20261021-m7-mood-init-input.md`. HEAD2e0a1114. Independent read-only research; coverage opened before tracing. No policy/approval/status change.

## Coverage

| Step | Source / current route | Result |
| --- | --- | --- |
| Actual gate | Robot0x0051034C..0x00510364; context ctor0x004EA742 | **DataPlatform pointer nonnull**, not JSON/config nonnull |
| Config identity | Adopted loader C1;0x005222EC..0x005222F8 | Resource scope1 config/engine/mood_config.json → loader+0xD0 |
| Mood Init | 0x0067AEBC..0x0067AEE4; prior registration extraction | StaticMoodData Init then action map, registration if Robot exists; no config-success branch in wrapper |
| Separate events | Robot.LoadEmotionEvents0x00510EE0..0x00510FBC | Loader tree+0x10 entries applied after Mood.Init, before ctor continues |
| Missing-file failure | 0x005222FC..0x00522364, adopted readAsJson | Visible error/error flag; loader continues, no “skip Mood.Init” success gate |
| Null JSON/default details | StaticMoodData.ReadFromJson / LoadActionCompletedEventMap | Exact complete default/partial-map outcomes not re-extracted: UNKNOWN; do not manufacture initialized-empty fidelity |
| Current engine owner | EngineRobot.MoodOwner nullable; FreeplayStack.Create loads model | No mood model/Init producer in engine startup; late facade input differs |
| Readiness/minimum contract | Native constructor completes before action admission | No separate mood-ready API bit recovered; initialize actual owner before its watcher input |

## Main correction

The previous completion-owner report called the Mood Init call “conditional with shipped config” and left its gate open. It is **conditional on the native DataPlatform pointer**, not on successful JSON loading or whether the JSON is null.

CozmoContext constructor stores external interface at+4 and DataPlatform at+8 (0x004EA742 stores r2/r5, where r5 is incoming DataPlatform r1). Robot constructor loads context+8 at0x00510352, and cbz0x00510354 skips both Mood.Init and Robot.LoadEmotionEvents only when that pointer is null. With it present,0x0051035A takes context's RobotDataLoader+0x1C, adds0xD0 and calls Mood.Init0x0051035E, then LoadEmotionEvents0x00510364.

Thus a missing mood file and a deliberately absent native DataPlatform are different inputs. Do not reinterpret a missing host resource as the constructor's null-DataPlatform case.

## Input and loading provenance

Reused adopted sources:
- `re-analysis/inventory/M7-behaviour.md`, mood rows and W13–W16;
- `20260928-X3-M7-behaviour-part4-mood-extraction.md`, config identity/static data/action map;
- `20261007-M7-M8-rows.md`, existing M7 gaps;
- manager-adopted `overnight/20261018-ov127-check.md`, complete LoadRobotConfigs census. The pending completion-owner comparison is a lead, not authority.

Loader C1 names `config/engine/mood_config.json`, path literal0x00523130, storage+0xD0. Fresh0x005222EC loads DataPlatform,0x005222EE selects destination+0xD0, scope1 is stored0x005222F2,0x005222F8 invokes readAsJson. False result enters error0x00522322 then sets error flag and optional break0x00522348..0x00522360; both branches converge0x00522364 and continue other config loads. This block has no caller-visible success result used to gate later Robot Mood.Init.

The file in this clone is
`re-analysis/obb/assets/cozmo_resources/config/engine/mood_config.json`.
Read fully for input shape: decayGraphs, eventMapper, defaultRepetitionPenalty and actionResultEmotionEvents;12 action-type map entries; embedded emotionEvents array empty. The empty embedded array does **not** mean ordinary action event names can never affect mood, because separate emotion-event documents are subsequently loaded.

Robot.LoadEmotionEvents0x00510EE0..0x00510FBC was freshly read through normal/unwind paths:
- obtains loader's tree root+0x10 via context at0x00510EE8..0x00510EEC;
- each entry's Json payload+0x18 is tested empty;
- nonempty payload calls Mood.LoadEmotionEvents0x00510F16;
- logs success/failure per entry, advances tree node chain0x00510F88..0x00510F8C.
It does not register action callbacks a second time. The loaded per-document event corpus is the eleven files under config/engine/emotionevents (action, charger, cliff, cube, face, hiking, motion_react, ocd, reaction, spark, workout). Exact native upstream file enumeration/partial-failure storage of that tree is not re-extracted here; existing loader/model evidence supplies the corpus, while complete per-file error acceptance is UNKNOWN in this bounded task.

## Init, callback availability and readiness

Mood.Init first calls StaticMoodData.Init(config) at0x0067AED4, then loads config["actionResultEmotionEvents"] and calls LoadActionCompletedEventMap0x0067AEE4. There is no branch on either parser return before the next Robot-pointer/registration test. StaticMoodData.Init0x0067CE56 initializes default decay graphs then tail-calls ReadFromJson via0x008CD71C; no additional config-presence gate in that wrapper.

Registration behavior is reused, not re-extracted: if Mood's Robot+0x12C is nonnull, register one action watcher callback and store handle+0x158. HasExternalInterface only gates separate C3 subscription. Assuming constituent parsers return normally, null/empty JSON does not by itself bypass this registration block. Exceptions/terminating malformed branches can prevent normal return; they are not converted into a guessed fully initialized map.

No distinct Mood readiness flag/report was identified in this constructor/Init path. The relevant production guarantee is synchronous completion of model/map/event initialization before the Robot's ordinary action updates and watcher callbacks. The callback handle proves a registration, not that every input map/event loaded successfully. Do not set a new public Ready=true simply from handle!=0, or wait for a first completion to construct mood.

Native Robot allocation/constructor precedes RobotManager registration and ordinary packet/action delivery (adopted M1 CB5/CB6). Inside that constructor Mood.Init→LoadEmotionEvents precedes subsequent initialization. Public engine readiness may depend on other startup gates; none is newly certified here.

## Host failures versus malformed input

- Missing/unreadable resource file is a plausible host failure. Native readAsJson returns false/logs; loader continues. In-scope stack must fail clearly/log enough and preserve a sensible state, per operator rules. It cannot silently use the DataPlatform-null branch as provenance for that fallback.
- Parse failure can leave the existing Json slot's contents; this mood read block does not explicitly clear+0xD0 after failure. Exact readAsJson partial Json output is its existing parser dependency. Do not claim missing/bad content always produces a fresh null object.
- Full null/partial-json defaults and every map/parser wrong-kind branch are UNKNOWN here, with concrete boundaries StaticMoodData.ReadFromJson0x0067CE6C and LoadActionCompletedEventMap0x0067AFB4. Prior extraction covers shipped well-typed data, not arbitrary malformed recovery.
- JsonCpp throws/termination on malformed input fall under standing M1-034 when established, with smallest-unit skip/log and prior state retained. This report does not label all missing-file errors “unreachable” or invent a new exclusion for them. Wrong-kind paths from inputs our supported host never produces need not block supported-corpus integration; that is distinct from a real missing asset.

## Current C# provider

Under `cozmo-stack/src/Cozmo.Robot/`:
- EngineRobot.MoodOwner starts null. Engine startup has a concrete Needs initializer, vision config loader and resource-root/behavior-map inputs, but no matching MoodModel/Init provider. Source search again finds new MoodState production fallback in FreeplayStack.Create.
- FreeplayStack.Create uses caller-supplied ctx.Mood, existing native MoodOwner or new MoodState(MoodModel.Load(obbRoot)), attaches Robot and registers watcher callback. It is a late host input, not the native loader+0xD0/Robot constructor sequence.
- MoodModel.Load searches recursively by mood_config.json basename and emotionevents files; an absent config simply leaves defaults while separate event files may still load. Bad main JSON throws; per-event JsonException is silently continued. This host convenience loader is not proof of native missing-file diagnostics/partial state or native enumeration precedence.
- Constructor resources being present for other systems therefore does not mean mood initialization has happened before an ordinary action completes. The missing engine-owned producer remains the prior report's concrete gap; no broad claim that all engine resources are absent.
- Overlapping user handles/custom MoodModel policy is not decided here.

## Minimal integration contract and oracle

After source checking/inventory approval:
1. Actual engine resource owner supplies the native mood-config slot and separate event documents at the Robot Mood initialization boundary, using source-backed path identity rather than an arbitrary first duplicate basename.
2. One per-Robot Mood runtime completes static/model/action-map initialization and separate event loads before its one watcher callback can dispatch ordinary action completions.
3. Preserve actual null-DataPlatform input as a distinct host/context compatibility input if the stack exposes it; do not infer that it represents a configured file read failure.
4. Supported missing-resource/parse failures produce a meaningful diagnostic and a defined host outcome. Exact native parse-error machinery need not be copied; do not silently advertise full ready/model fidelity when required input is absent.
5. Keep generic callback/time/type and retirement requirements in the preceding report; no second registration for LoadEmotionEvents or facade recreation.

Independent regression proposal, not run: configured native/host startup with exact shipped config/events, before Freeplay Create, then one ordinary real action completion; verify model/event-map ready before dispatch and one callback at native timer. Separate missing-file and no-DataPlatform fixtures, and distinguish file-read failure from malformed wrong-kind throw. A prebuilt injected MoodState bypasses this input-source contract.

Remaining source gaps: loader emotion-tree per-file failure details, exact null/partial mood Json parser outcomes, duplicate resource search behavior and full source startup readiness interfaces. No affector arithmetic repeated, host-handle policy selected, code/tests/hardware or settlement.

## Current records

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


M1-054 — EXACT_SOURCE — CozmoEngine::Init before RobotManager::Init: the +0x10 Info, the three advertising-key isMember errors (return 1), UiMessageHandler::Init, vslot+8; and RobotManager::BroadcastAvailableAnimations

> re-analysis/research/overnight/20261014-ov22-m8-022-check.md: Info 0x004EC7B4..0x004EC7CC; isMember 0x004EC81C/0x004EC82C/0x004EC83E with bodies 0x004EC898..0x004EC962; UiMessageHandler::Init 0x004EC846..0x004EC896; vslot+8 0x004EC96A..0x004EC9C8; BroadcastAvailableAnimations call 0x0052E76E (body unread).

> re-analysis/research/overnight/20261016-batch-check.md: ReInit 0x004EC7B4..0x004EC80E; isMember gates 0x004EC812..0x004EC844, 0x004EC898..0x004EC968; UiMessageHandler::Init 0x004EC846..0x004EC896, 0x004EC9B6..0x004EC9C8; InitInternal (vtable 0x0101E488 +8 = 0x004EDC6C) 0x004EC96A..0x004ECA1A.
