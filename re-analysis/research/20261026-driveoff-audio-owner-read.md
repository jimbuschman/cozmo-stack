# Drive-off: actual object1 audio owner/readiness/consumer boundary

Requested by manager/root after D2 documentation reconciliation, 2026-10-26. Actual model/reasoning telemetry is unavailable (requested Solmedium). Research-only; no production/tests/manifest/status/git/hardware changes. This report does **not** finish DriveOff, M6 or M9, and does not release implementation before an independent check.

| Coverage | Result |
|---|---|
| Does reached stack/activity audio matter? | YES: object1 scoped state feeds actual music selection and active scheduling. A1–A7, SO1/CS4/SO4/SC1–9 are reused, not inferred from names. |
| Actual native owner/input attachment/lifetime | Existing checked SH1–8, UC38–39, M6-029; context construction and mux ownership personally rechecked below. |
| Constructor readiness vs bank readiness | Personally rechecked controller/helper result propagation and gates; existing SL1–5/33–49/CW281/CV282 cover concrete source defaults/scene/load ordering. These are distinct states. |
| Active observer/scheduler | Full 512-byte 009994F0 personally reread; existing SC/OQ/CF inventory supplies named direct recipients. No guessed immediate switch/play implementation. |
| Production closure | STILL MISSING: no actual controller/recipient, scoped object1 switch owner/query/observer, or source audio-tail binding. Existing wrappers are reusable components, not a complete sink. |
| Broader renderer/body completeness | NOT newly examined or claimed. Existing specific M9/M6 source and unresolved inputs remain; no whole-Wwise audit. |

## Current owning records before reliance

Literal current titles/statuses and whole selected evidence entries below were reread from the current manifest. Evidence selection is explicit: all entries for M6-024/M6-029/M9-004, first entry for the others. No record is reclassified; the full current unresolved text is retained in ignored scratch current-records.json and the existing source handoff.

```json
[
  {
    "id": "M13-039",
    "title": "BehaviorAudioClient::HandleRobotPublicStateChange and helpers: world events, round, spark, needs, dim, activity music, GuardDog and Feeding stages, unlock cache; SetSparkedMusicState input",
    "status": "IMPLEMENTATION_GAP",
    "evidence": [
      "re-analysis/research/overnight/20261015-ov48-49-check.md: handler 0x005921EE..0x00592258; helpers 0x0059225C..0x00592720, 0x00591FC4..0x00592190 (round table 0x00C5B65C, activity map 0x00C5B5F0, eighties 0x00573B70..0x00573B9A, 0x00573E30..0x00573E7E); handler lambda 0x01023D98 -> 0x00592B1E -> 0x004AF07C; SetSparkedMusicState (G2E 0xA3) lambda 0x00592A9E..0x00592ABA, client+0xC initial 0 (0x00591E56)."
    ]
  },
  {
    "id": "M6-024",
    "title": "Bank and scene loading call sites: the CozmoAudioController six-bank list and InitScene, LoadAudioScene/LoadSoundbank, AddZipFiles",
    "status": "IMPLEMENTATION_GAP",
    "evidence": [
      "CozmoAudioController ctor 0x00592BB0; InitializeAudioEngine 0x008D1D1E; RegisterAudioScene 0x005935E2; LoadAudioScene 0x005935EA; banks Init.bnk/Music.bnk/UI.bnk/SFX.bnk/Cozmo.bnk/Dev_Debug.bnk and scene InitScene",
      "LoadAudioScene 0x008D2EE8 -> LoadSoundbank 0x008D2FE4; AddZipFiles 0x008D1E3E feeds the OBB archives"
    ]
  },
  {
    "id": "M6-006",
    "title": "Control path: queued PostEvent, ExecuteEvent, EnqueueOrExecute (frames + remainder; PlayAndContinue one frame early), drain order, Play/Stop/Seek execute, switch resolution",
    "status": "IMPLEMENTATION_GAP",
    "evidence": [
      "PostEvent 0x009A6704 / core 0x9A0EF8; pump 0x9AE0B0; ExecuteEvent 0x9AA3DC"
    ]
  },
  {
    "id": "M6-029",
    "title": "Context +0xC AudioMultiplexer teardown (CozmoAudioController, AudioEngineController)",
    "status": "IMPLEMENTATION_GAP",
    "evidence": [
      "re-analysis/research/20261012-m5-teardown-members.md: ~CozmoContext slot 1 on +0xC -> 0x008DEB08: deletes CozmoAudioController (0x005946F0 -> AudioEngineController dtor 0x008D1C1C: ClearGarbageCollector, plug-in interface, 0x008D8048 ...), then the input connections (+0x10 list, each +0xC virtual delete); present only with an external interface (0x004EA84C).",
      "re-analysis/research/overnight/20261015-ov36-37-check.md: gate 0x004EA84C tests DataPlatform* (mov r5,r1 0x004EA73C; production 0x004EB286..0x004EB28C); ~CozmoContext 0x004EABBA..0x004EAC14; mux 0x008DEB08..0x008DEB4E, 0x008DF6E0..0x008DF758; derived 0x005946F0..0x0059472C; base 0x008D1C1C..0x008D1D0E; plug-in holders 0x008DC124..0x008DC178, 0x008DD552..0x008DD59A, 0x008DB2AC..0x008DB2FE; inputs AudioUnityInput 0x004EDC8A..0x004EDC96 / 0x00591BA0..0x00591BBE, AudioEngineInput 0x004ED260..0x004ED26C / 0x00590AA0..0x00590AE6; runtime sequence 0x008D8048..0x008D80A2."
    ]
  },
  {
    "id": "M9-004",
    "title": "Music switch containers, decision trees, meters and MIDI target",
    "status": "IMPLEMENTATION_GAP",
    "evidence": [
      "re-analysis/inventory/M6-wwise-bank.md",
      "Cozmo.bnk objects 914766641, 139286641, 602865028 and MIDI target 110896138"
    ]
  },
  {
    "id": "M6-022",
    "title": "The live voice and bus engine: the 0xA57FF8 wrapper and 0xA44D4C render body, the voice pass, the bus pass, idle removal, the per-voice DSP chain, the four Perform group members, the PBI flush, the 0x108DAE8 output-device state and its three gate bytes, and the JNI audio-route poll",
    "status": "IMPLEMENTATION_GAP",
    "evidence": [
      "wrapper 0xA57FF8 -> 0xA57D64 + 0xA44D4C; render body 0xA44D4C pre-loop over the output-device list 0x108DB04 (0xA44D5C..0xA44DA0); throttle 0x108DA9C/0x108D870+0x4c (0xA44DAC..0xA44DD4)"
    ]
  },
  {
    "id": "M6-025",
    "title": "The Play -> PBI -> voice -> source creation bridge: the node PlayInternal dispatch, the node vt+0x14 PBI creator, the PBI ctor 0xA000E8, CalcEffectiveParams 0x9FFAD4, the voice ctor/attach 0xA54650/0xA4304C, AddSrc 0xA558AC and the source factory 0xA562B8, and the PBI start list 0x9D3558/0x9D3644",
    "status": "IMPLEMENTATION_GAP",
    "evidence": [
      "Play helper 0xA62A1C: target node 0xA6168C -> 0x9A7EB0, stored at params+4 (0xA62B90), node->vt+0x128 call (0xA62D14/0xA62D20/0xA62D24)"
    ]
  },
  {
    "id": "M6-023",
    "title": "The app audio-input dispatch: Unity PostAudioEvent -> AudioUnityInput -> AudioMuxInput -> AudioMultiplexer -> AudioEngineController::PostAudioEvent -> Wwise PostEvent",
    "status": "IMPLEMENTATION_GAP",
    "evidence": [
      "Unity: unity/scripts/csharp/Anki.Cozmo.Audio/PlaySound.cs:29,72-82; GameAudioClient.cs:17-45; UnityAudioClient.cs:224-237,279-287; Anki.AudioEngine.Multiplexer/PostAudioEvent.cs:89-107; Anki.Cozmo.ExternalInterface/MessageGameToEngine.cs:14-16"
    ]
  }
]
```

## Exact producer-to-consumer relevance

The direct DriveOff motor action remains DriveStraight/custom line; this audio path is not a lattice prerequisite. The concrete **conditional** normal-ingress path already source-checked is contact/ObserveCharger → world pose/configuration pending → successful frame-tail native configuration rebuild → tallest/public state → two BehaviorAudioClient recipients (Activity !=2, stack>1 transition). These issue EC3490EC/3BA32BC6 on object1. Common configured activity entry also sends the checked activity switches. Mere association with a global engine tick is not the dependency proof.

A3's actual Music.bnk pair is type1901 SetSwitch, group8379F505, values817CDD53/27F8B66A, no authored delay/range; neither is direct Play. A4/SX store the state in the object-scoped owner **before** observer notification. SO1/CS4 query that scoped owner; SO4 changed-state notification reaches real scheduler9994F0. Thus caching an unused dictionary, rerouting to singing object7, or counting client requests would lose state that feeds future output. This proves observable-state relevance (item4) and a real downstream selection/scheduling path; it does not by itself prove present robot PCM or a phone-only exclusion. M6-023 expressly retains supported game-posted audio; there is no operator exclusion of object1.

A6 separately proves fresh no-music-input state: conductor pending0 and empty InitScene event vector mean these switch requests alone do not start music. It does **not** authorize dropping later supported MusicState/app Play or an already active schedule. Fresh admission and live active admission must not be conflated.

## Instruction inventory and exact production gaps

`known.py` preflight logs in `.scratch/driveoff-audio-owner/known-*.txt` precede the primary anchors used for this read. Primary is local ARM ELF `resources/lib/armeabi-v7a/libcozmoEngine.so`, same previously checked SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. Thumb and ARM are distinguished; 99DC54/9994F0 are ARM. Native disassembly, not decompiler output, supports the new rows. Reused row families below retain their existing primary companion files.

| Row | Behavior-changing witness | Established result and exact boundary |
|---|---|---|
| AO1 actual context owner | 004EA73C MOV r5,r1;742 STRD r2,r5,[context+4],746 clearscontext+C. 004EA84C CMP r5,0;858 BEQ886. Nonzero allocates mux20hex/controllerA0hex at85A..868;86A..876 constructs controller(context), then mux(controller);87A..884 replacescontext+C and deletes displaced mux. | Gate is DataPlatform argument/context+8, **not** external-interface pointer/context+4. This corrects the older first M6-029 evidence phrase; its second evidence entry already records the correction. Resource file presence is a later controller gate. An actual host DataPlatform does not imply initialized audio or bank readiness. |
| AO2 per-engine mux lifetime | 008DEAE6 stores incomingcontroller at mux+4;EAF0 zeros input map;EAF8 lastinputID0. 008DEB18..1E deletes controller first;B24 clears pointer;B28..36 deletes each input, thenB40..48 map storage cleanup. SH1–8 settle robot client handler/input attachment and teardown. | A controller owns its runtime, mux owns controller+registered inputs, client borrows handler. Production cannot bind a global unrelated runtime or let a borrowed facade retire this owner. Internal allocation/refcount imitation is unnecessary where the same lifetime/order and snapshots survive. |
| AO3 defined startup prerequisite | 00592C28 InitializeMusicConductor precedes resource resolution. C2C reads context+8 DataPlatform;C52 pathToResource;C64..72 obtains JNI env;C76 DirectoryExists;C84 OR env-presence;C86 BEQCF6. CF6..D68 logs/marks error, cleans wrapper/path and reaches ctor tail. Existing SL33–38 settle no-JNI AudioAssets.zip failure continuing to initialization and config overrides. | Native always constructs conductor before resource gate. Missing resource directory with null JNI skips audio init; missing archive is a different defined branch. Host missing-resource failure may be handled sensibly under AGENTS, but must never be advertised as a ready initialized renderer or silently synthetic bank success. |
| AO4 controller ready propagation | 008D1D22 loads controller+8 helper;1D24 calls8D80D8;1D28 STRB return→controller+4. 008D811A calls8D81B0;811E/20 requiresresult1;8126 invokes8D8228 post-init;812A calls99DC54;8130/32 failed init invokes8D8068 cleanup,8136 return0. 813A stores result tohelperbyte0;814C..52 normalizesreturn. ARM99DC54..60 loads source initialized byte (UC46 resolves108D8D4). | Controller enabled and helper readiness originate in native initialization results, not `banks.Count==6`, a defaulttrue delegate, supplied resource path, or PostEvent success. Existing UC46 gives cleanup recipients; this read does not pretend newly to close memory/stream/SoundEngine/MusicEngine Init leaf bodies. |
| AO5 exact prerequisites and post-init | 008D81BC result fromA7AC68 must1;81D0 pool resultFFFFFFFF fails;81E4 stream manager9604FC mustnonnull;81F0 resolver8D6C02 must1;81FC SoundEngine99E3EC must1;8206 MusicEngine97D72C must1. Any failedcheck exits0 at8214. 008D8228 post-init sets search path, mapper, archives and language (existing SL6–11, CW281/CV282);813E..46 seeds onlyconfig+74 nonzero. | These are observable readiness/failure gates and ordered collaborators. The wrapper is source-settled; their concrete actual input/writer/lifetime interfaces must be reused from checked source or remain UNKNOWN, never all replaced by a successful constructor. Native threads/pools may be hosted differently only when resulting readiness/control/output remains the same. |
| AO6 bank-ready is distinct | 0059346C/72 tests controller+4 before plugin+scene tail;5935E2 RegisterAudioScene;5935EA LoadAudioScene;5935F0 RegisterCladGameObjects follows with no resulttest. Existing SL1–5: six banks forward exactorder, firstloadfalse stops remaining banks/preparation/scene append, earlier loads retained; eventprepare resultignored; scene appendedonlyafterbanks. SL2/checked594410 registers1..10 even ifloadfalse. | An initialized controller may have a failed/partial scene. A registered object is not proof of successful bank load; a later lookup may fail. Existing C# LoadScene pre-enumeration/filtering is not a source readiness owner. Do not normalize all failure states to empty/successful scene. |
| AO7 actual command gates/order | Reused A1/SH1–8 and A2/UC30–35: synchronous native inputs, handler/mux/controller/helper gates; queuedPostEvent9A6704; direct switch tag8,size14hex99FE1C; originalobject1; callbackempty nativeflags0. A4/SX tag8consumer9AEFC4..9AF038 and1901 A65940..A659A0 converge on scopedstoreA32ED0. | Native inputs cannot be sent through app-envelope next-tick submission. Preserve source FIFO, playingID allocation and missingobject behavior. Bank existence alone does not execute a pending command. |
| AO8 real active scheduler | Personally full ARM9994F0..96EC: schedule48null oractive54zero returns4F8..514; mode8 returns534..544. Flag36bit0clear calls99D710 then999194 at96C4..D4. Bitset performs marker/time gates and ordered998A34→998B98→998D1C→99902C at9658..96BC. SC1–9 independently checked source fully specifies these local control bodies. | Active changes cancel/queue/remove/start/notify, not just overwrite selectedstate. No new DSP work is needed to establish this dependency. A7/UC20 binds conductor→RenderAudio request→callbackflush on engine audio-tail; rendering may be on its own source thread. Arbitrary `PumpMessages()` on a host clock is not shown equivalent. |
| AO9 concrete next consumer and uncertainty | Existing OQ1/2 signedtime orderedqueue/stableequal insertion, strict `<start+window`;OQ3 kind2 snapshot+queue;OQ4/5 initial/restart startoperations;OQ7..9 execute start/stop;CF1..8 concrete child/transition contracts. OQ4 at999194..99290 writesrequest14=0 but does NOT write18/1C;OQ9 at9988E0..9958 copies14/18/1C to startrequest;CF3 existing-transition branch readsrequest4 irrespective duration0. | Much scheduling source is already recovered and reusable. Do not pay for another full scheduler read or falsely mark all unread. The two unwritten request words are **UNKNOWN input provenance**, not proven zeros or automatically proven UB: obtain their actual allocation/writer/transition-admission proof before choosing M1-034. Kind2 execution/lifetime and actual child virtual34/render bridge also remain named joins; no silent skipped notifications. |

## Current C# comparison (read-only)

- `BehaviorAudioOwners.cs:10` declares `BehaviorAudioRecipient`; `BehaviorAudioClient.cs:166` invokes it; current production search finds **no writer**. Client-request tests cannot establish delivery.
- `EngineAppAudio.cs:12` only installs app input if Options.AudioInputDispatch is supplied. The actual native controller construction above is DataPlatform-gated; configured app dispatch is neither proof of source per-engine controller existence nor a substitute for native synchronous input. Posted diagnostics correctly separate submission from audible playback.
- `WwiseAnkiResolver.cs:9..12` explicitly says no production constructor yet. Its source-shaped resolver/file/zip pieces are reusable; no call currently establishes canonical controller readiness.
- `WwiseSoundLibrary.LoadScene` enumerates/parses/filter-orders assets; `LoadCore` catches InvalidDataException and continues. This is a convenient offline library, not SL3–5's actual partial-load/ready/error contract. `WwiseBankLoader` exposes remaining required reader/STMG/STID/streamed-DATA hooks. Do not present its class existence as a complete source-ready bank runtime.
- `WwiseEventRuntime.PumpMessages` currently handles queued events/aux/outputvolume, not native switchtag8. Event parser/executor lacks1901. EventFIFO/playingIDs/registeredobjectlookup/pendingactions are reusable after source comparison, not permission to add an unused switch dictionary.
- `WwiseAudioSource.SetSwitch` uses singingobjects6/7/global dictionary; its ResolveMusic/renderer has no actual object1 scoped query/observer/scheduler owner. No current source-backed binding joins this API to SO1/SC/OQ. Do not reroute.

## Smallest meaningful closure and STOP

The smallest **complete observable join** is a real per-engine controller with honest initialization/partial-bank readiness and lifetime, actual synchronous native input, common event/direct-switch queue, scoped object1 state+query+observer owner, and its actual source audio-tail consumer. It must preserve later active consumer state rather than stop at delegate invocation. This is not a proposal for a facade/control-only job. A fresh-empty state is only one reachable state of this owner.

The next genuinely unresolved **source-input join**, after reusing the existing startup/SC/OQ/CF work, is active operation creation → actual deferred operation executor/child context start → first render-owned state/output, specifically (1) the OQ4 unwrittenrequest18/1C provenance versus actual existing-transition admission, (2) kind2 operation consumer/lifetime, (3) concrete child virtual34 and source renderer scheduling attachment. Use existing detailed singing-decision primary companions and M6-022/025 evidence first. Primary exists locally. These are recoverable source joins and substantial unbuilt production bindings, **not BLOCKED_EXTERNAL**, not proved no-effect, and not a newly authorized audio exclusion.

Source startup wrappers and source bank manager rows are already reusable; avoid a second generic initialization/bank parser audit. The available source record is incomplete precisely at the named runtime input/consumer boundaries, so a complete live audio/pilot build cannot yet be released. If the source-proven active route requires further full voice/DSP closure, stop for that concrete scope instead of suppressing active state. ADP-1 applies only to eligible per-sample arithmetic with its native-corpus equivalence gate; it changes none of the control/readiness/RNG/timing obligations here.

Independent B check is requested for AO1–9 and this boundary. No whole behavior, subsystem, record or build verification is asserted. No new IDs/policy/job quota is proposed.
