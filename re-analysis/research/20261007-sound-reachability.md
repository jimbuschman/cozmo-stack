| Coverage | Status | Result |
|---|---|---|
| Open Q14 obligations (45 PARTIAL + 74 NOT DONE at parked checkpoint) | CHECKED at obligation level | 119/119 indexed below; each has a reachable portion. |
| Q15 scope | CHECKED at scope level | Eight decision categories, all REACHABLE. |
| Raw bank/object/media envelope census | CHECKED | Six loaded banks; 5350 HIRC objects; 2214 external WEM headers. |
| Every conditional subbranch and exhaustive engine/Unity writer absence | PARTIAL | Positive witnesses do not settle the exclusions listed below. No unsupported negative claim is used to remove research. |

# Sound reachability census — manager review required

This is a census, not resumed Q14/Q15 build rows. REACHABLE means at least one shipped input reaches the retained obligation, as the amendment asks; it does **not** mean every branch or transitive callee is reachable. None of the 119 broad open obligations can be excluded as a whole. Conditional subbranches without a closed negative proof remain retained. This report does not settle any fidelity record. Stop here for manager review.

Package input: the adjacent local clone's `re-analysis/obb/assets/cozmo_resources/sound/AudioAssets.zip`, read locally; archive SHA256 `d889b6e82606be8e284fedde44daecdbd5406f72bf5c68a1396f319eeecf6145`. Only metadata is committed. Bank filenames, raw offsets, object hashes, action arrays, child-array offsets and media format headers are in `20261007-sound-reachability-assets.json`. Offsets are bytes inside the uncompressed named bank. Primary engine SHA256 `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`.

The independent Python pass checks raw chunk envelopes, all object type/ID/length entries, Event arrays, Action types/targets, and serialized child arrays against the C# navigation export. The C# export is **candidate implementation output**, not an independent fidelity oracle; conditional Params/FX/music interpretations not independently proved by those checks remain provisional. Native route citations are separated below. No emulator output or hardware result is claimed.

## Raw bank census

| Bank | HIRC count | SHA256 |
|---|---:|---|
| Dev_Debug.bnk | 58 | `8385b5a53b7e60a6d5e0a34c68dc35ee41ad92768e6565d6fb9885966fddbf71` |
| English(US)/Cozmo.bnk | 4055 | `7a2770c62f3bf31ee4f77389d3a147c85768034bb29c9c84e913a3284a5efbe8` |
| Init.bnk | 28 | `fc4a48e1562875b045aa026e90c4389344410545a89bccd4725ab8c7141e2283` |
| Music.bnk | 674 | `424eace23a070f216ea81572fbfbf2f35cf0248d26287e26a9b72c3d39b4a77c` |
| SFX.bnk | 476 | `f805b233934e8079ea9f2645c6d941e229a9248b3d25599fe469bbc24f1171f4` |
| UI.bnk | 59 | `6ff4cba498fec24c8d1c68e1c946a0a6f378e67af93d26eb67cce74966f46301` |

835 Events refer to 906 Action objects. Raw action types/counts: 0x102=8, 0x103=109, 0x108=1, 0x202=1, 0x302=1, 0x403=723, 0x1204=7, 0x1303=3, 0x1901=51, 0x1e03=1, 0x2103=1.

2214 external RIFF/WEM format headers are 1987 formatFFFF and 227 format2; the channel counts are 1 or 2. DIDX/DATA also contains 46 non-RIFF singing MIDI payloads. They are the Wwise packed MIDI representation, **not** MThd/SMF files: e.g. media2087930 begins `25 80 00 00 C8 42` (big-endian division0x2580, then little-endian binary32 tempo42C80000). No absence of MThd is used to discard MIDI.

## Reachability witnesses

| Witness | Bank object or native call site | What it establishes / limit |
|---|---|---|
| LOAD: Bank loading/readers | CozmoAudioController 0x005935E2/0x005935EA -> scene 0x008D2EE8 -> bank 0x008D2FE4; HIRC dispatch 0x009B338C | All six bank envelopes consume exactly. Cozmo contains the Event/Action/Sound/container/music/modulator objects below; Init contains buses, FX and STMG. This proves load-time reachability, separately from playing them. |
| EVENT: Event/action queue | Cozmo event 819632272 -> action 968169310 (0x0403) -> music switch 139286641; Music event 2748524418 -> actions 711128030 (0x1204),481248312 (0x0403) -> 797713379; native 0x009A6704 -> 0x009AA3DC -> 0x00A62A1C | 723 Play actions and 183 other actions are serialized, with their order preserved. Play, termination and callback work has a concrete input; an absent specific action opcode is not inferred present. |
| RNG: Random/sequence selection | Cozmo event 2007174956 -> action 667287064 -> 399004754 -> 259413535 -> RanSeq 681157; selector 0x00A08A44, seed 0x0099EF80 -> 0x0099DB58 | RanSeq 681157 authors three children, weight50000 each, avoid1, randomMode1. Sequence parent259413535 provides a separate selection witness. Live seed and selection are control work. |
| CONT: Container scheduling | Cozmo 259413535,183681385,196431345,725225627; PlayInternal 0x00A0AFDC, next 0x00A09C40, prepare 0x00A6A07C | Positive base-container and completion path. Authored transition-mode4 nodes196431345/725225627 are retained, but a serialized mode is not proof of every loop/flag conjunction. No whole scheduling obligation is discarded. |
| RTPC: RTPC, curves and state | Cozmo sampler110896138 <- LFO528935089 <- RTPC0xC20F49DF; note-off layer462443456 <- envelope381606890; 0x005EF184..0x005EF18C,0x00A14E28..0x00A15244 | The singing parameter writer and authored bindings are concrete producers/consumers. Layer462443456 has scaling2 and points00000000/00000000,3F800000/BF800000; sampler pitch binding has scaling0 and endpoints00000000/00000000,3F800000/44110000. |
| GAIN: Voice gain/effective parameters | Cozmo Sound265437 on event1845501759 -> action85426994 -> 731109154 -> 265437; 0x009FFAD4,0x009EF258,0x009BCA68 | A playing sound necessarily takes parameter composition/audibility controls. Init duck sources1534528548,2459405053,3829101743 and state/RTPC bindings are additional concrete inputs. This does not certify every rare counter/threshold branch. |
| FILTER: Filter controls | Cozmo Switch53142486 on event3205213409 -> action184678442 -> 677877281 -> 53142486; authored properties2=42C80000,3=41A00000,4=41E80000; native0x00A550D8..0x00A551EC -> 0x00A44630 | Filter targets and render-stage control are reachable. Per-sample equivalence remains ADP-1 work, not a reachability deletion. |
| PAN: Routing/matrix controls | Sound265437; Init Robot_Bus_1=2678428988 channelConfig0x4101; stereo external media; 0x00A4FBEC,0x00A25FF8,0x00A4BC58 | Shipped mono/stereo sources and robot mono route require channel mapping and conversion. The wider speaker-mask branches are not proven reachable by this witness. |
| FX: Insert FX and wrappers | Init Robot_Bus_1=2678428988 slots0..3:1734867999,390660550,3743559935,412442143; voice bus2476517424 -> compressor2313011259; native0x00A4FD84..0x00A4FEF4,0x00A54A30 | Two EQs, limiter and Hijack have explicit bus references; the voice bus references the master compressor. Settings, initialization, bypass and lifecycle are retained. Registry membership alone is not used to mark unrelated plug-ins played. |
| BUS: Bus connection/lifetime | Init Robot_Bus_1=2678428988 -> parent3803692087; engine routing0x0059962A..0x005999A4; create0x00A42210, release0x00A4F36C | Robot audio supplies an actual bus use and connection lifecycle, including the zero-data/end path. Aux, alternate devices and null-parent branches still require their own predicates. |
| ROBOT: Robot output/framing | Init Robot_Bus_1 final slot412442143; SetupPlugins0x005942C6..0x00594354; Hijack Init0x008DBD74/Execute0x008DBFE8; client0x005985FC | Concrete installed Hijack route reaches its format, buffer, flush and 22320-Hz output controls. |
| TICK: Worker/render/clock | SoundEngine Init0x0099E3EC -> 0x009B0200 -> 0x00A40940 -> worker0x00A4087C; tick0x004ED4D4 -> 0x008DF3DA -> 0x008D2928 -> 0x008D88C0; Perform0x009AF8A8 | Startup and engine/robot render pumps are actual callers, independently of which bank sound is selected. OS wake timing does not erase shipped scheduling checks. |
| FORMAT: Rate/frame/source format | Shipped media rates24000/32000/36000/44100/48000, mono/stereo; native0x00A57724..0x00A57930,0x00A1C75C/0x00A1C7D4; Hijack0x008DBD74 | Actual inputs require format/rate/frame propagation and resampling. Source/header failures and the JNI state alternatives are retained until their exact gates are checked. |
| VORBIS: Vorbis source/decode | Cozmo Sound265437 and source-plugin0x00040001 objects; raw WEM formatFFFF; native0x00AB0B20/0x00AB22D4 -> framing0x00AB7E40 -> packet0x00AB3780 -> inverse0x00AB6B14 | Both in-memory and streamed Vorbis sources exist. Allocation, reset, prefix, skip/trim and teardown remain reachable lifecycle work; unsupported block-size/LFE subcases need separate exclusion proof. |
| ADPCM: ADPCM source/decode | Sound source-plugin0x00020001; 227 external format2 media, including7 stereo48000; native0x00A72618/0x00A73EA0/0x00A74100 -> 0x00A7A194 | ADPCM input and both channel geometries are present. Zero-channel, corrupt index and truncated-source branches are not established merely by the good shipped headers. |
| VOICE: Voice/PBI ownership and state | Cozmo event1845501759 ->85426994 ->731109154 -> Sound265437; Play0x00A62A1C -> sound0x00A1D448 -> PBI0x00A379D8 -> source0x00A562B8; flush0x00A38420/Term0x00A38600 | A shipped Play input reaches the voice/PBI/source production path. Counts, virtual state, callbacks, cleanup and failure edges remain retained; this is not proof of every indirect recipient. |
| LIMIT: Voice limits | Cozmo actor62050212 maxInstances1, object66225135 maxInstances5; source states0x009ED2CC..0x009ED3D0, counters0x009F29E8..0x009F2BC4, priority0x00A37100 | Authored nonzero limits are concrete control inputs. A particular victim/tie ordering still needs rows; PCM limiter equivalence does not apply. |
| MOD: Modulators/control delivery | Cozmo LFO528935089 -> binding110896138 and envelope381606890 ->462443456; factory0x009D7B6C, trigger0x009D552C, delivery0x00A6E848 | Authored consumers plus the engine RTPC writer establish modulator use. An empty-list example cannot eliminate the populated path. |
| METER: Meter/callback boundary | Robot bus2678428988 and render0x00A4FEF8 -> callback registry0x009C806C; metric bodies0x00A50044/0x00A52164 | The bus render/registry check is reachable. NONEMPTY meter-registration and nonzero meter flags are not proved here: the metric inner path remains a census uncertainty, not a proven UNREACHABLE branch. |
| UNITY: Unity/native audio messages | Assembly-CSharp Audio PlaySound.Play / GameAudioClient.PostAudioEvent -> UnityAudioClient.PostEvent -> MessageGameToEngine.PostAudioEvent; native0x00591968..0x00591A6E ->0x008DFC4C ->0x008DED14 ->0x008D1F20 ->0x008D8CE4 | Shipped managed message producers and native subscriptions establish live forwarding. See producer table; the decompiled managed text is navigation, not an independent IL oracle. |
| SING: Singing behavior lifecycle | Shipped Singing_Camptown.json groupCozmo_Sings_100Bpm/stateCozmo_Sings_Camptown_Races; Init0x005EEB46..0x005EEB4E, Update0x005EF184..0x005EF18C, Stop0x005EF2CA | A named shipped behavior config and native callers establish switch posting, animation sequence and vibration parameter lifecycle. |
| MIDI: Music and MIDI routing | Cozmo event819632272 ->968169310 ->139286641 ->1001278794 ->34452189; target property56=110896138; native0x00A3BDFC..0x00A3BEE4,0x00A3CC48,0x00A3EA3C | A music-event input and authored MIDI target exist. Target ancestor search and note routing must be checked; child403781184 is retained rather than declared unreachable from normal-Play builders. |
| NOTE: Note scheduling/held notes | Cozmo track11970948 source654585462 plugin0x00100001; MIDI target110896138 children403781184,462443456,774902407; native0x00A3F76C..0x00A3FBC0,0x00A3DDF0,0x00A3E688..0x00A3E9F4 | 46 authored MIDI sources and the sampler graph establish note-on/off, clip windows, recorded choice, tempo and held-note lifecycle. Alternate external MIDI posters remain unclosed. |
| CLASS: Cube classifier to singing RTPC | BehaviorSinging Init0x005EECB0..0x005EED6F installs cube listeners; classifier0x00636578..0x0063682F -> sample callback0x005EF490..0x005EF4DF -> parameter post0x005EF184..0x005EF18C | Shipped singing listener registration reaches the classifier, including first sample. It is reaction input, not exempt PCM DSP. |
| IO: Archive/stream/cache | Six-bank startup; 2131 Cozmo streamed-media prefixes (DIDX/DATA) and external WEMs; resolver0x00592CDC..0x00592D78/0x00593BF6..0x00593C44 ->0x008D843C; streams0x00961664/0x009656AC/0x00965B1C | Concrete package assets require the archive and streamed I/O paths. Checkpoints/retries/cache/allocation remain retained around external completion inputs; a specific error occurrence is not asserted. |
| PLUGIN: Source plug-in dispatch | Shipped Sound source plug-ins0x006412C2,0x00650002,0x00660002,0x00640002 and music MIDI0x00100001; factory0x00A78D10 | Source plug-in objects are package inputs. Specific factory/version/type gates still need checking; source generation is not inferred to be equivalent PCM arithmetic. |

## Q14: every open obligation

The original triage text is quoted per item after this table, preserving its exact scope/address list. A REACHABLE row retains that scope pending extraction; its witness is not a claim that the unresolved steps have been read.

| Item | Triage line | Classification | Positive witness |
|---|---:|---|---|
| Q14-002 | 33 | REACHABLE | LOAD; CozmoAudioController 0x005935E2/0x005935EA -> scene 0x008D2EE8 -> bank 0x008D2FE4; HIRC dispatch 0x009B338C |
| Q14-003 | 34 | REACHABLE | VORBIS; Cozmo Sound265437 and source-plugin0x00040001 objects; raw WEM formatFFFF; native0x00AB0B20/0x00AB22D4 -> framing0x00AB7E40 -> packet0x00AB3780 -> inverse0x00AB6B14 |
| Q14-004 | 35 | REACHABLE | ADPCM; Sound source-plugin0x00020001; 227 external format2 media, including7 stereo48000; native0x00A72618/0x00A73EA0/0x00A74100 -> 0x00A7A194 |
| Q14-005 | 36 | REACHABLE | FORMAT; Shipped media rates24000/32000/36000/44100/48000, mono/stereo; native0x00A57724..0x00A57930,0x00A1C75C/0x00A1C7D4; Hijack0x008DBD74 |
| Q14-007 | 40 | REACHABLE | EVENT; Cozmo event 819632272 -> action 968169310 (0x0403) -> music switch 139286641; Music event 2748524418 -> actions 711128030 (0x1204),481248312 (0x0403) -> 797713379; native 0x009A6704 -> 0x009AA3DC -> 0x00A62A1C |
| Q14-008 | 41 | REACHABLE | RNG; Cozmo event 2007174956 -> action 667287064 -> 399004754 -> 259413535 -> RanSeq 681157; selector 0x00A08A44, seed 0x0099EF80 -> 0x0099DB58 |
| Q14-009 | 42 | REACHABLE | CONT; Cozmo 259413535,183681385,196431345,725225627; PlayInternal 0x00A0AFDC, next 0x00A09C40, prepare 0x00A6A07C |
| Q14-010 | 43 | REACHABLE | RTPC; Cozmo sampler110896138 <- LFO528935089 <- RTPC0xC20F49DF; note-off layer462443456 <- envelope381606890; 0x005EF184..0x005EF18C,0x00A14E28..0x00A15244 |
| Q14-011 | 44 | REACHABLE | GAIN; Cozmo Sound265437 on event1845501759 -> action85426994 -> 731109154 -> 265437; 0x009FFAD4,0x009EF258,0x009BCA68 |
| Q14-012 | 46 | REACHABLE | FILTER; Cozmo Switch53142486 on event3205213409 -> action184678442 -> 677877281 -> 53142486; authored properties2=42C80000,3=41A00000,4=41E80000; native0x00A550D8..0x00A551EC -> 0x00A44630 |
| Q14-014 | 49 | REACHABLE | PAN; Sound265437; Init Robot_Bus_1=2678428988 channelConfig0x4101; stereo external media; 0x00A4FBEC,0x00A25FF8,0x00A4BC58 |
| Q14-015 | 50 | REACHABLE | PAN; Sound265437; Init Robot_Bus_1=2678428988 channelConfig0x4101; stereo external media; 0x00A4FBEC,0x00A25FF8,0x00A4BC58 |
| Q14-016 | 53 | REACHABLE | FX; Init Robot_Bus_1=2678428988 slots0..3:1734867999,390660550,3743559935,412442143; voice bus2476517424 -> compressor2313011259; native0x00A4FD84..0x00A4FEF4,0x00A54A30 |
| Q14-017 | 55 | REACHABLE | FX; Init Robot_Bus_1=2678428988 slots0..3:1734867999,390660550,3743559935,412442143; voice bus2476517424 -> compressor2313011259; native0x00A4FD84..0x00A4FEF4,0x00A54A30 |
| Q14-019 | 57 | REACHABLE | BUS; Init Robot_Bus_1=2678428988 -> parent3803692087; engine routing0x0059962A..0x005999A4; create0x00A42210, release0x00A4F36C |
| Q14-020 | 58 | REACHABLE | ROBOT; Init Robot_Bus_1 final slot412442143; SetupPlugins0x005942C6..0x00594354; Hijack Init0x008DBD74/Execute0x008DBFE8; client0x005985FC |
| Q14-021 | 59 | REACHABLE | ROBOT; Init Robot_Bus_1 final slot412442143; SetupPlugins0x005942C6..0x00594354; Hijack Init0x008DBD74/Execute0x008DBFE8; client0x005985FC |
| Q14-022 | 60 | REACHABLE | TICK; SoundEngine Init0x0099E3EC -> 0x009B0200 -> 0x00A40940 -> worker0x00A4087C; tick0x004ED4D4 -> 0x008DF3DA -> 0x008D2928 -> 0x008D88C0; Perform0x009AF8A8 |
| Q14-023 | 61 | REACHABLE | FORMAT; Shipped media rates24000/32000/36000/44100/48000, mono/stereo; native0x00A57724..0x00A57930,0x00A1C75C/0x00A1C7D4; Hijack0x008DBD74 |
| Q14-024 | 62 | REACHABLE | LOAD; CozmoAudioController 0x005935E2/0x005935EA -> scene 0x008D2EE8 -> bank 0x008D2FE4; HIRC dispatch 0x009B338C |
| Q14-025 | 63 | REACHABLE | RNG; Cozmo event 2007174956 -> action 667287064 -> 399004754 -> 259413535 -> RanSeq 681157; selector 0x00A08A44, seed 0x0099EF80 -> 0x0099DB58 |
| Q14-026 | 64 | REACHABLE | VOICE; Cozmo event1845501759 ->85426994 ->731109154 -> Sound265437; Play0x00A62A1C -> sound0x00A1D448 -> PBI0x00A379D8 -> source0x00A562B8; flush0x00A38420/Term0x00A38600 |
| Q14-027 | 65 | REACHABLE | MOD; Cozmo LFO528935089 -> binding110896138 and envelope381606890 ->462443456; factory0x009D7B6C, trigger0x009D552C, delivery0x00A6E848 |
| Q14-028 | 66 | REACHABLE | METER; Robot bus2678428988 and render0x00A4FEF8 -> callback registry0x009C806C; metric bodies0x00A50044/0x00A52164 |
| Q14-029 | 67 | REACHABLE | PAN; Sound265437; Init Robot_Bus_1=2678428988 channelConfig0x4101; stereo external media; 0x00A4FBEC,0x00A25FF8,0x00A4BC58 |
| Q14-030 | 68 | REACHABLE | UNITY; Assembly-CSharp Audio PlaySound.Play / GameAudioClient.PostAudioEvent -> UnityAudioClient.PostEvent -> MessageGameToEngine.PostAudioEvent; native0x00591968..0x00591A6E ->0x008DFC4C ->0x008DED14 ->0x008D1F20 ->0x008D8CE4 |
| Q14-031 | 69 | REACHABLE | LOAD; CozmoAudioController 0x005935E2/0x005935EA -> scene 0x008D2EE8 -> bank 0x008D2FE4; HIRC dispatch 0x009B338C |
| Q14-032 | 70 | REACHABLE | VOICE; Cozmo event1845501759 ->85426994 ->731109154 -> Sound265437; Play0x00A62A1C -> sound0x00A1D448 -> PBI0x00A379D8 -> source0x00A562B8; flush0x00A38420/Term0x00A38600 |
| Q14-033 | 71 | REACHABLE | VOICE; Cozmo event1845501759 ->85426994 ->731109154 -> Sound265437; Play0x00A62A1C -> sound0x00A1D448 -> PBI0x00A379D8 -> source0x00A562B8; flush0x00A38420/Term0x00A38600 |
| Q14-034 | 72 | REACHABLE | CONT; Cozmo 259413535,183681385,196431345,725225627; PlayInternal 0x00A0AFDC, next 0x00A09C40, prepare 0x00A6A07C |
| Q14-035 | 73 | REACHABLE | VOICE; Cozmo event1845501759 ->85426994 ->731109154 -> Sound265437; Play0x00A62A1C -> sound0x00A1D448 -> PBI0x00A379D8 -> source0x00A562B8; flush0x00A38420/Term0x00A38600 |
| Q14-036 | 74 | REACHABLE | FX; Init Robot_Bus_1=2678428988 slots0..3:1734867999,390660550,3743559935,412442143; voice bus2476517424 -> compressor2313011259; native0x00A4FD84..0x00A4FEF4,0x00A54A30 |
| Q14-037 | 75 | REACHABLE | LIMIT; Cozmo actor62050212 maxInstances1, object66225135 maxInstances5; source states0x009ED2CC..0x009ED3D0, counters0x009F29E8..0x009F2BC4, priority0x00A37100 |
| Q14-038 | 76 | REACHABLE | VOICE; Cozmo event1845501759 ->85426994 ->731109154 -> Sound265437; Play0x00A62A1C -> sound0x00A1D448 -> PBI0x00A379D8 -> source0x00A562B8; flush0x00A38420/Term0x00A38600 |
| Q14-039 | 77 | REACHABLE | VOICE; Cozmo event1845501759 ->85426994 ->731109154 -> Sound265437; Play0x00A62A1C -> sound0x00A1D448 -> PBI0x00A379D8 -> source0x00A562B8; flush0x00A38420/Term0x00A38600 |
| Q14-040 | 78 | REACHABLE | SING; Shipped Singing_Camptown.json groupCozmo_Sings_100Bpm/stateCozmo_Sings_Camptown_Races; Init0x005EEB46..0x005EEB4E, Update0x005EF184..0x005EF18C, Stop0x005EF2CA |
| Q14-041 | 79 | REACHABLE | SING; Shipped Singing_Camptown.json groupCozmo_Sings_100Bpm/stateCozmo_Sings_Camptown_Races; Init0x005EEB46..0x005EEB4E, Update0x005EF184..0x005EF18C, Stop0x005EF2CA |
| Q14-042 | 80 | REACHABLE | MIDI; Cozmo event819632272 ->968169310 ->139286641 ->1001278794 ->34452189; target property56=110896138; native0x00A3BDFC..0x00A3BEE4,0x00A3CC48,0x00A3EA3C |
| Q14-043 | 81 | REACHABLE | NOTE; Cozmo track11970948 source654585462 plugin0x00100001; MIDI target110896138 children403781184,462443456,774902407; native0x00A3F76C..0x00A3FBC0,0x00A3DDF0,0x00A3E688..0x00A3E9F4 |
| Q14-044 | 82 | REACHABLE | MOD; Cozmo LFO528935089 -> binding110896138 and envelope381606890 ->462443456; factory0x009D7B6C, trigger0x009D552C, delivery0x00A6E848 |
| Q14-045 | 83 | REACHABLE | MOD; Cozmo LFO528935089 -> binding110896138 and envelope381606890 ->462443456; factory0x009D7B6C, trigger0x009D552C, delivery0x00A6E848 |
| Q14-046 | 84 | REACHABLE | MOD; Cozmo LFO528935089 -> binding110896138 and envelope381606890 ->462443456; factory0x009D7B6C, trigger0x009D552C, delivery0x00A6E848 |
| Q14-047 | 85 | REACHABLE | MOD; Cozmo LFO528935089 -> binding110896138 and envelope381606890 ->462443456; factory0x009D7B6C, trigger0x009D552C, delivery0x00A6E848 |
| Q14-048 | 86 | REACHABLE | MOD; Cozmo LFO528935089 -> binding110896138 and envelope381606890 ->462443456; factory0x009D7B6C, trigger0x009D552C, delivery0x00A6E848 |
| Q14-049 | 87 | REACHABLE | NOTE; Cozmo track11970948 source654585462 plugin0x00100001; MIDI target110896138 children403781184,462443456,774902407; native0x00A3F76C..0x00A3FBC0,0x00A3DDF0,0x00A3E688..0x00A3E9F4 |
| Q14-050 | 88 | REACHABLE | ROBOT; Init Robot_Bus_1 final slot412442143; SetupPlugins0x005942C6..0x00594354; Hijack Init0x008DBD74/Execute0x008DBFE8; client0x005985FC |
| Q14-051 | 90 | REACHABLE | MIDI; Cozmo event819632272 ->968169310 ->139286641 ->1001278794 ->34452189; target property56=110896138; native0x00A3BDFC..0x00A3BEE4,0x00A3CC48,0x00A3EA3C |
| Q14-052 | 91 | REACHABLE | MIDI; Cozmo event819632272 ->968169310 ->139286641 ->1001278794 ->34452189; target property56=110896138; native0x00A3BDFC..0x00A3BEE4,0x00A3CC48,0x00A3EA3C |
| Q14-053 | 92 | REACHABLE | RNG; Cozmo event 2007174956 -> action 667287064 -> 399004754 -> 259413535 -> RanSeq 681157; selector 0x00A08A44, seed 0x0099EF80 -> 0x0099DB58 |
| Q14-055 | 94 | REACHABLE | CLASS; BehaviorSinging Init0x005EECB0..0x005EED6F installs cube listeners; classifier0x00636578..0x0063682F -> sample callback0x005EF490..0x005EF4DF -> parameter post0x005EF184..0x005EF18C |
| Q14-056 | 95 | REACHABLE | NOTE; Cozmo track11970948 source654585462 plugin0x00100001; MIDI target110896138 children403781184,462443456,774902407; native0x00A3F76C..0x00A3FBC0,0x00A3DDF0,0x00A3E688..0x00A3E9F4 |
| Q14-057 | 96 | REACHABLE | RNG; Cozmo event 2007174956 -> action 667287064 -> 399004754 -> 259413535 -> RanSeq 681157; selector 0x00A08A44, seed 0x0099EF80 -> 0x0099DB58 |
| Q14-059 | 98 | REACHABLE | MOD; Cozmo LFO528935089 -> binding110896138 and envelope381606890 ->462443456; factory0x009D7B6C, trigger0x009D552C, delivery0x00A6E848 |
| Q14-060 | 99 | REACHABLE | MOD; Cozmo LFO528935089 -> binding110896138 and envelope381606890 ->462443456; factory0x009D7B6C, trigger0x009D552C, delivery0x00A6E848 |
| Q14-061 | 100 | REACHABLE | FX; Init Robot_Bus_1=2678428988 slots0..3:1734867999,390660550,3743559935,412442143; voice bus2476517424 -> compressor2313011259; native0x00A4FD84..0x00A4FEF4,0x00A54A30 |
| Q14-062 | 102 | REACHABLE | FX; Init Robot_Bus_1=2678428988 slots0..3:1734867999,390660550,3743559935,412442143; voice bus2476517424 -> compressor2313011259; native0x00A4FD84..0x00A4FEF4,0x00A54A30 |
| Q14-063 | 104 | REACHABLE | FX; Init Robot_Bus_1=2678428988 slots0..3:1734867999,390660550,3743559935,412442143; voice bus2476517424 -> compressor2313011259; native0x00A4FD84..0x00A4FEF4,0x00A54A30 |
| Q14-064 | 106 | REACHABLE | FX; Init Robot_Bus_1=2678428988 slots0..3:1734867999,390660550,3743559935,412442143; voice bus2476517424 -> compressor2313011259; native0x00A4FD84..0x00A4FEF4,0x00A54A30 |
| Q14-065 | 107 | REACHABLE | SING; Shipped Singing_Camptown.json groupCozmo_Sings_100Bpm/stateCozmo_Sings_Camptown_Races; Init0x005EEB46..0x005EEB4E, Update0x005EF184..0x005EF18C, Stop0x005EF2CA |
| Q14-066 | 115 | REACHABLE | VOICE; Cozmo event1845501759 ->85426994 ->731109154 -> Sound265437; Play0x00A62A1C -> sound0x00A1D448 -> PBI0x00A379D8 -> source0x00A562B8; flush0x00A38420/Term0x00A38600 |
| Q14-067 | 116 | REACHABLE | NOTE; Cozmo track11970948 source654585462 plugin0x00100001; MIDI target110896138 children403781184,462443456,774902407; native0x00A3F76C..0x00A3FBC0,0x00A3DDF0,0x00A3E688..0x00A3E9F4 |
| Q14-073 | 122 | REACHABLE | FX; Init Robot_Bus_1=2678428988 slots0..3:1734867999,390660550,3743559935,412442143; voice bus2476517424 -> compressor2313011259; native0x00A4FD84..0x00A4FEF4,0x00A54A30 |
| Q14-074 | 123 | REACHABLE | VOICE; Cozmo event1845501759 ->85426994 ->731109154 -> Sound265437; Play0x00A62A1C -> sound0x00A1D448 -> PBI0x00A379D8 -> source0x00A562B8; flush0x00A38420/Term0x00A38600 |
| Q14-075 | 124 | REACHABLE | VOICE; Cozmo event1845501759 ->85426994 ->731109154 -> Sound265437; Play0x00A62A1C -> sound0x00A1D448 -> PBI0x00A379D8 -> source0x00A562B8; flush0x00A38420/Term0x00A38600 |
| Q14-076 | 125 | REACHABLE | LOAD; CozmoAudioController 0x005935E2/0x005935EA -> scene 0x008D2EE8 -> bank 0x008D2FE4; HIRC dispatch 0x009B338C |
| Q14-078 | 127 | REACHABLE | FX; Init Robot_Bus_1=2678428988 slots0..3:1734867999,390660550,3743559935,412442143; voice bus2476517424 -> compressor2313011259; native0x00A4FD84..0x00A4FEF4,0x00A54A30 |
| Q14-079 | 128 | REACHABLE | FX; Init Robot_Bus_1=2678428988 slots0..3:1734867999,390660550,3743559935,412442143; voice bus2476517424 -> compressor2313011259; native0x00A4FD84..0x00A4FEF4,0x00A54A30 |
| Q14-080 | 129 | REACHABLE | FX; Init Robot_Bus_1=2678428988 slots0..3:1734867999,390660550,3743559935,412442143; voice bus2476517424 -> compressor2313011259; native0x00A4FD84..0x00A4FEF4,0x00A54A30 |
| Q14-083 | 132 | REACHABLE | GAIN; Cozmo Sound265437 on event1845501759 -> action85426994 -> 731109154 -> 265437; 0x009FFAD4,0x009EF258,0x009BCA68 |
| Q14-084 | 133 | REACHABLE | VOICE; Cozmo event1845501759 ->85426994 ->731109154 -> Sound265437; Play0x00A62A1C -> sound0x00A1D448 -> PBI0x00A379D8 -> source0x00A562B8; flush0x00A38420/Term0x00A38600 |
| Q14-085 | 134 | REACHABLE | TICK; SoundEngine Init0x0099E3EC -> 0x009B0200 -> 0x00A40940 -> worker0x00A4087C; tick0x004ED4D4 -> 0x008DF3DA -> 0x008D2928 -> 0x008D88C0; Perform0x009AF8A8 |
| Q14-086 | 135 | REACHABLE | FORMAT; Shipped media rates24000/32000/36000/44100/48000, mono/stereo; native0x00A57724..0x00A57930,0x00A1C75C/0x00A1C7D4; Hijack0x008DBD74 |
| Q14-087 | 136 | REACHABLE | TICK; SoundEngine Init0x0099E3EC -> 0x009B0200 -> 0x00A40940 -> worker0x00A4087C; tick0x004ED4D4 -> 0x008DF3DA -> 0x008D2928 -> 0x008D88C0; Perform0x009AF8A8 |
| Q14-088 | 137 | REACHABLE | VOICE; Cozmo event1845501759 ->85426994 ->731109154 -> Sound265437; Play0x00A62A1C -> sound0x00A1D448 -> PBI0x00A379D8 -> source0x00A562B8; flush0x00A38420/Term0x00A38600 |
| Q14-089 | 138 | REACHABLE | LOAD; CozmoAudioController 0x005935E2/0x005935EA -> scene 0x008D2EE8 -> bank 0x008D2FE4; HIRC dispatch 0x009B338C |
| Q14-090 | 139 | REACHABLE | VORBIS; Cozmo Sound265437 and source-plugin0x00040001 objects; raw WEM formatFFFF; native0x00AB0B20/0x00AB22D4 -> framing0x00AB7E40 -> packet0x00AB3780 -> inverse0x00AB6B14 |
| Q14-091 | 140 | REACHABLE | VORBIS; Cozmo Sound265437 and source-plugin0x00040001 objects; raw WEM formatFFFF; native0x00AB0B20/0x00AB22D4 -> framing0x00AB7E40 -> packet0x00AB3780 -> inverse0x00AB6B14 |
| Q14-092 | 141 | REACHABLE | FORMAT; Shipped media rates24000/32000/36000/44100/48000, mono/stereo; native0x00A57724..0x00A57930,0x00A1C75C/0x00A1C7D4; Hijack0x008DBD74 |
| Q14-093 | 142 | REACHABLE | MOD; Cozmo LFO528935089 -> binding110896138 and envelope381606890 ->462443456; factory0x009D7B6C, trigger0x009D552C, delivery0x00A6E848 |
| Q14-094 | 143 | REACHABLE | IO; Six-bank startup; 2131 Cozmo streamed-media prefixes (DIDX/DATA) and external WEMs; resolver0x00592CDC..0x00592D78/0x00593BF6..0x00593C44 ->0x008D843C; streams0x00961664/0x009656AC/0x00965B1C |
| Q14-096 | 145 | REACHABLE | IO; Six-bank startup; 2131 Cozmo streamed-media prefixes (DIDX/DATA) and external WEMs; resolver0x00592CDC..0x00592D78/0x00593BF6..0x00593C44 ->0x008D843C; streams0x00961664/0x009656AC/0x00965B1C |
| Q14-097 | 146 | REACHABLE | IO; Six-bank startup; 2131 Cozmo streamed-media prefixes (DIDX/DATA) and external WEMs; resolver0x00592CDC..0x00592D78/0x00593BF6..0x00593C44 ->0x008D843C; streams0x00961664/0x009656AC/0x00965B1C |
| Q14-098 | 147 | REACHABLE | IO; Six-bank startup; 2131 Cozmo streamed-media prefixes (DIDX/DATA) and external WEMs; resolver0x00592CDC..0x00592D78/0x00593BF6..0x00593C44 ->0x008D843C; streams0x00961664/0x009656AC/0x00965B1C |
| Q14-099 | 148 | REACHABLE | FORMAT; Shipped media rates24000/32000/36000/44100/48000, mono/stereo; native0x00A57724..0x00A57930,0x00A1C75C/0x00A1C7D4; Hijack0x008DBD74 |
| Q14-100 | 149 | REACHABLE | IO; Six-bank startup; 2131 Cozmo streamed-media prefixes (DIDX/DATA) and external WEMs; resolver0x00592CDC..0x00592D78/0x00593BF6..0x00593C44 ->0x008D843C; streams0x00961664/0x009656AC/0x00965B1C |
| Q14-101 | 150 | REACHABLE | VORBIS; Cozmo Sound265437 and source-plugin0x00040001 objects; raw WEM formatFFFF; native0x00AB0B20/0x00AB22D4 -> framing0x00AB7E40 -> packet0x00AB3780 -> inverse0x00AB6B14 |
| Q14-102 | 151 | REACHABLE | ADPCM; Sound source-plugin0x00020001; 227 external format2 media, including7 stereo48000; native0x00A72618/0x00A73EA0/0x00A74100 -> 0x00A7A194 |
| Q14-103 | 152 | REACHABLE | PLUGIN; Shipped Sound source plug-ins0x006412C2,0x00650002,0x00660002,0x00640002 and music MIDI0x00100001; factory0x00A78D10 |
| Q14-104 | 153 | REACHABLE | BUS; Init Robot_Bus_1=2678428988 -> parent3803692087; engine routing0x0059962A..0x005999A4; create0x00A42210, release0x00A4F36C |
| Q14-105 | 154 | REACHABLE | RTPC; Cozmo sampler110896138 <- LFO528935089 <- RTPC0xC20F49DF; note-off layer462443456 <- envelope381606890; 0x005EF184..0x005EF18C,0x00A14E28..0x00A15244 |
| Q14-107 | 156 | REACHABLE | VORBIS; Cozmo Sound265437 and source-plugin0x00040001 objects; raw WEM formatFFFF; native0x00AB0B20/0x00AB22D4 -> framing0x00AB7E40 -> packet0x00AB3780 -> inverse0x00AB6B14 |
| Q14-108 | 157 | REACHABLE | VORBIS; Cozmo Sound265437 and source-plugin0x00040001 objects; raw WEM formatFFFF; native0x00AB0B20/0x00AB22D4 -> framing0x00AB7E40 -> packet0x00AB3780 -> inverse0x00AB6B14 |
| Q14-109 | 158 | REACHABLE | LOAD; CozmoAudioController 0x005935E2/0x005935EA -> scene 0x008D2EE8 -> bank 0x008D2FE4; HIRC dispatch 0x009B338C |
| Q14-110 | 159 | REACHABLE | MIDI; Cozmo event819632272 ->968169310 ->139286641 ->1001278794 ->34452189; target property56=110896138; native0x00A3BDFC..0x00A3BEE4,0x00A3CC48,0x00A3EA3C |
| Q14-113 | 162 | REACHABLE | RTPC; Cozmo sampler110896138 <- LFO528935089 <- RTPC0xC20F49DF; note-off layer462443456 <- envelope381606890; 0x005EF184..0x005EF18C,0x00A14E28..0x00A15244 |
| Q14-114 | 163 | REACHABLE | RTPC; Cozmo sampler110896138 <- LFO528935089 <- RTPC0xC20F49DF; note-off layer462443456 <- envelope381606890; 0x005EF184..0x005EF18C,0x00A14E28..0x00A15244 |
| Q14-115 | 164 | REACHABLE | RTPC; Cozmo sampler110896138 <- LFO528935089 <- RTPC0xC20F49DF; note-off layer462443456 <- envelope381606890; 0x005EF184..0x005EF18C,0x00A14E28..0x00A15244 |
| Q14-116 | 172 | REACHABLE | VOICE; Cozmo event1845501759 ->85426994 ->731109154 -> Sound265437; Play0x00A62A1C -> sound0x00A1D448 -> PBI0x00A379D8 -> source0x00A562B8; flush0x00A38420/Term0x00A38600 |
| Q14-117 | 173 | REACHABLE | FX; Init Robot_Bus_1=2678428988 slots0..3:1734867999,390660550,3743559935,412442143; voice bus2476517424 -> compressor2313011259; native0x00A4FD84..0x00A4FEF4,0x00A54A30 |
| Q14-118 | 174 | REACHABLE | GAIN; Cozmo Sound265437 on event1845501759 -> action85426994 -> 731109154 -> 265437; 0x009FFAD4,0x009EF258,0x009BCA68 |
| Q14-119 | 175 | REACHABLE | TICK; SoundEngine Init0x0099E3EC -> 0x009B0200 -> 0x00A40940 -> worker0x00A4087C; tick0x004ED4D4 -> 0x008DF3DA -> 0x008D2928 -> 0x008D88C0; Perform0x009AF8A8 |
| Q14-120 | 176 | REACHABLE | FORMAT; Shipped media rates24000/32000/36000/44100/48000, mono/stereo; native0x00A57724..0x00A57930,0x00A1C75C/0x00A1C7D4; Hijack0x008DBD74 |
| Q14-121 | 177 | REACHABLE | VOICE; Cozmo event1845501759 ->85426994 ->731109154 -> Sound265437; Play0x00A62A1C -> sound0x00A1D448 -> PBI0x00A379D8 -> source0x00A562B8; flush0x00A38420/Term0x00A38600 |
| Q14-122 | 178 | REACHABLE | LOAD; CozmoAudioController 0x005935E2/0x005935EA -> scene 0x008D2EE8 -> bank 0x008D2FE4; HIRC dispatch 0x009B338C |
| Q14-123 | 179 | REACHABLE | VORBIS; Cozmo Sound265437 and source-plugin0x00040001 objects; raw WEM formatFFFF; native0x00AB0B20/0x00AB22D4 -> framing0x00AB7E40 -> packet0x00AB3780 -> inverse0x00AB6B14 |
| Q14-124 | 180 | REACHABLE | FORMAT; Shipped media rates24000/32000/36000/44100/48000, mono/stereo; native0x00A57724..0x00A57930,0x00A1C75C/0x00A1C7D4; Hijack0x008DBD74 |
| Q14-125 | 181 | REACHABLE | MOD; Cozmo LFO528935089 -> binding110896138 and envelope381606890 ->462443456; factory0x009D7B6C, trigger0x009D552C, delivery0x00A6E848 |
| Q14-126 | 182 | REACHABLE | IO; Six-bank startup; 2131 Cozmo streamed-media prefixes (DIDX/DATA) and external WEMs; resolver0x00592CDC..0x00592D78/0x00593BF6..0x00593C44 ->0x008D843C; streams0x00961664/0x009656AC/0x00965B1C |
| Q14-127 | 183 | REACHABLE | IO; Six-bank startup; 2131 Cozmo streamed-media prefixes (DIDX/DATA) and external WEMs; resolver0x00592CDC..0x00592D78/0x00593BF6..0x00593C44 ->0x008D843C; streams0x00961664/0x009656AC/0x00965B1C |
| Q14-128 | 184 | REACHABLE | IO; Six-bank startup; 2131 Cozmo streamed-media prefixes (DIDX/DATA) and external WEMs; resolver0x00592CDC..0x00592D78/0x00593BF6..0x00593C44 ->0x008D843C; streams0x00961664/0x009656AC/0x00965B1C |
| Q14-129 | 185 | REACHABLE | IO; Six-bank startup; 2131 Cozmo streamed-media prefixes (DIDX/DATA) and external WEMs; resolver0x00592CDC..0x00592D78/0x00593BF6..0x00593C44 ->0x008D843C; streams0x00961664/0x009656AC/0x00965B1C |
| Q14-130 | 186 | REACHABLE | FORMAT; Shipped media rates24000/32000/36000/44100/48000, mono/stereo; native0x00A57724..0x00A57930,0x00A1C75C/0x00A1C7D4; Hijack0x008DBD74 |
| Q14-131 | 187 | REACHABLE | BUS; Init Robot_Bus_1=2678428988 -> parent3803692087; engine routing0x0059962A..0x005999A4; create0x00A42210, release0x00A4F36C |
| Q14-132 | 188 | REACHABLE | RTPC; Cozmo sampler110896138 <- LFO528935089 <- RTPC0xC20F49DF; note-off layer462443456 <- envelope381606890; 0x005EF184..0x005EF18C,0x00A14E28..0x00A15244 |
| Q14-133 | 189 | REACHABLE | VORBIS; Cozmo Sound265437 and source-plugin0x00040001 objects; raw WEM formatFFFF; native0x00AB0B20/0x00AB22D4 -> framing0x00AB7E40 -> packet0x00AB3780 -> inverse0x00AB6B14 |
| Q14-134 | 190 | REACHABLE | LOAD; CozmoAudioController 0x005935E2/0x005935EA -> scene 0x008D2EE8 -> bank 0x008D2FE4; HIRC dispatch 0x009B338C |
| Q14-135 | 191 | REACHABLE | MIDI; Cozmo event819632272 ->968169310 ->139286641 ->1001278794 ->34452189; target property56=110896138; native0x00A3BDFC..0x00A3BEE4,0x00A3CC48,0x00A3EA3C |
| Q14-136 | 192 | REACHABLE | RTPC; Cozmo sampler110896138 <- LFO528935089 <- RTPC0xC20F49DF; note-off layer462443456 <- envelope381606890; 0x005EF184..0x005EF18C,0x00A14E28..0x00A15244 |
| Q14-140 | 205 | REACHABLE | CLASS; BehaviorSinging Init0x005EECB0..0x005EED6F installs cube listeners; classifier0x00636578..0x0063682F -> sample callback0x005EF490..0x005EF4DF -> parameter post0x005EF184..0x005EF18C |

## Q15: scope census

| Piece | Classification | Witness |
|---|---|---|
| Segment selection | REACHABLE | Cozmo139286641 ->1001278794 ->segment34452189 ->track787982984; event819632272/action968169310. |
| Playlist selection, ordering and looping | REACHABLE | Cozmo event3805821586 ->action337801183 ->602865028 ->playlist1488599 ->segment424319695; serialized playlist item985849671/921105668. |
| Transitions and music switch/state selection | REACHABLE | Music event2748524418 ->action481248312 ->music switch797713379; Cozmo139286641 continues playback and has authored argument group3759662965; engine singing Init005EEB46..005EEB4E posts selected group/state. Exact individual transition-rule gates remain for Q15. |
| MIDI event dispatch | REACHABLE | Cozmo track11970948/media654585462 plugin00100001; 46 packed MIDI sources; dispatch00A3EA3C tests status90/80. |
| Note to sampler target/child selection | REACHABLE | Cozmo139286641,602865028,914766641 property56=110896138; target has children403781184,462443456,774902407; native00A3BDFC..00A3BEE4. Whether a particular child is eligible remains an extraction question, not an exclusion. |
| Note-on/off and clip timing | REACHABLE | Track11970948 contains clip654585462 with trim/play/duration fields; scheduler00A3F76C..00A3FBC0 and held-note path00A3E688..00A3E9F4. |
| Envelope parameter values/triggers | REACHABLE | Envelope381606890 bound to462443456; authored property15=2,9=00000000,10=41C00000,11=00000000,12=41180000,14=00000000; predicate009D552C. |
| Vibrato parameters and state changes | REACHABLE | LFO528935089 bound to110896138, external sourceC20F49DF; engine005EF184..005EF18C posts smoothed value, stop005EF2CA posts zero. Authored props0=1,2=42C80000,3=3E4CCCCD,4=40B00000,7=42480000. |

## Application producers and values

Native instruction companions: `20261007-sound-reachability-native.txt` and `20261007-sound-reachability-gates-native.txt`. Existing reopened Q14 UE1–UE7/UC1–UC7 and SL1–SL2 supply the dispatch/load routes; they are not new build rows in this census.

| Producer | Input values | Recipient / evidence |
|---|---|---|
| BehaviorSinging Init | Stored group/state from shipped Singing_*.json; 100Bpm/Camptown_Races is a concrete pair. Other configs include80Bpm/Danny_Boy and100Bpm/Beethovens_5Th. | 005EEB46 loads group/state,005EEB4E calls PostRobotSwitchState. Config directory is adjacent unpacked OBB `config/engine/behaviorSystem/behaviors/freeplay/singing/`. |
| BehaviorSinging Update/Stop | ParameterC20F49DF; smoothed/clamped shake signal0..1; stop0. | 005EF184..005EF18C; stop005EF2CA. RobotAudioClient00599F5E..00599F84 uses object7 when modebyte3C==2, else6, time/curve0. |
| Robot animation audio keyframes | Authored events/alternatives, per-playing event_volumeD2687048, object7..10 for robot bus routes; on-device6. | 0059962A..005999A4; native InitAnimation0059687E..00596914 and buffer lambda0059818C..005982A0. Exhaustive animation event-value census is not claimed here. |
| Unity PlaySound / GameAudioClient | Serialized AudioEventParameter; UI/SFX/CodeLab/Aria overloads use corresponding object type. | Adjacent Unity decompile PlaySound.cs72..82,GameAudioClient.cs17..45,UnityAudioClient.cs224..237; nativeAudioUnityInput00591968..00591A6E subscribes/forwards1..6. |
| Unity volume preferences | Music default3F4CCCCD; Robot/SFX/UI/VO default3F800000. Saved values can differ; Robot takes IRobot.SetRobotVolume, others PostParameter with requested time/curve. | GameAudioClient.cs52..137; UnityAudioClient.cs263..268. Do not restrict runtime values to defaults. |
| Unity music state | Caller-supplied Music state, interrupt flag and minimum duration. | GameAudioClient.SetMusicState138..141 ->UnityAudioClient271..276; native MusicConductor.SetMusicState008D74E0..008D750D. |
| Shipped soundboard methods | Enumerated events/groups/states; RTPC sliderBF800000..3F800000; all callback flags requested. | AudioSoundboardPanel.cs169..216; UnityAudioClient.GetEvents/GetParameters. These methods ship, but scene activation was not established; they are NOT used as a sole proof that every enum event is production-reachable. |

## Limits of negative reachability

No whole open obligation is marked UNREACHABLE. The following observations narrow candidates but do not warrant global exclusions without the indicated closure. This is the PARTIAL portion of the census; the manager must not use these observations as permission to skip a reachable decision.

| Candidate subbranch | Package observation | Classification / remaining proof |
|---|---|---|
| 3D positioning reader009ECFB0 onward | Navigation export has positioning bytes00,C0,C3; native009ECF6C/70 and009ECF98/9C require overridebit0 plusbit3. | Provisional UNREACHABLE for these bank-load payloads; Params layout must be independently checked for all objects before accepting this exclusion. Does not exclude runtime position writers or ordinary positioning controls. |
| Nonempty LayerCntr blend-track payload | All six type9 objects have BlendTracks0 in navigation export; sampler children are nonempty. | Provisional UNREACHABLE for nonempty serialized blend-track bodies; independently confirm reader cursor/count fields. Never exclude the reachable child/MIDI routing. |
| Ping-pong sequence bit5 | All468 RanSeq flags are12/1A in navigation export. | Provisional UNREACHABLE for bank-authored bit5 path; full runtime writer census is still needed for a playback exclusion. |
| Zero-channel / multichannel-LFE codec input | All2214 external headers and parsed embedded RIFF prefixes have1/2 channels. | UNREACHABLE for direct selection of a shipped zero-channel or multichannel WEM. Not a proof that malformed header/error/default-window paths are impossible; do not remove source error handling. |
| RIFF PCM file source | External/embedded RIFF formats observed are2/FFFF. | UNREACHABLE for direct selection of a shipped format1 PCM WEM in this archive. Generated plug-in output, voice buffers, and common source-class teardown are still REACHABLE. |
| Meter inner loops/visible meter callback | Bus rendering reaches registry lookup009C806C; no positive registered-meter witness established. | UNKNOWN; need registration callers/flag writers, including indirect/static initialization. Retained, not excluded. |
| Alternate MIDI posting versus normal Play defaults | Normal Play builder uses0/FF; shipped music MIDI uses explicit target and status events. | UNKNOWN for alternate external posters; normal-Play defaults cannot disprove music notes or sampler/get-in routing. Q14-110/135 remain retained. |
| Source failure, allocation failure, re-init/null-parent, unsupported plugin/version, device/JNI alternatives | Good bank inputs do not establish all runtime predicates. | UNKNOWN for individual rare branches; the enclosing lifecycle/control obligation is REACHABLE. Need the specific writer/caller predicate before deleting one. |
| Arbitrary registered FX/plugin | Registry entry is not an object-graph use. | UNKNOWN unless a slot/source reference and live route exists. Positive witnesses above use actual serialized references, not registration alone. |

No current manifest record is contradicted or reclassified here. Candidate-export totals (including62 RTPC entries) are navigation only and must not overwrite the current C35 census. Float property interpretations, music-tree branches and FX payload layouts remain subject to the manager's source check.

## Preserved open-obligation quotations

### Q14-002 — REACHABLE (LOAD)

> | M6-001 | **KEEP** | 0x009B338C;0x009F6EF8;0x009ECF44;0x009C3FFC;0x009D24D4;0x009B0B14 | All readers, conditional positioning/bus/layer branches and object-graph comparisons remain exact parsing work. |

### Q14-003 — REACHABLE (VORBIS)

> | M6-002 | **KEEP** | 0x00AB6380..0x00AB6780;0x00AB3780;0x00AB6B14;0x00AB7E40;0x00AB3520;0x00AB5A94;0x00AB4E34 | Live decoder integration, LFE channel reorder, reset/skip/trim, work-buffer lifetime, window-default reachability, and unshipped-size signed-zero residuals remain WEM decoding; ADP-1 explicitly excludes decoding from equivalence, including its IMDCT/NEON arithmetic. |

### Q14-004 — REACHABLE (ADPCM)

> | M6-003 | **KEEP** | 0x00A7A194..0x00A7A3C4;0x00A72618;0x00A73EA0;0x00A74100 | ADPCM channels/blockAlign, out-of-bounds/error behavior and zero-channel source handling remain decoding decisions; integer decoder arithmetic is not exempt DSP. |

### Q14-005 — REACHABLE (FORMAT)

> | M6-004 | **KEEP** | 0x00A46D80..0x00A46D88;0x00A47038;0x00A47384;0x00A47178;0x00A52D4C | Format/rate/channel writers, initial phase, pitch/ramp scheduling, input consumed/output produced, zero-input results and live wiring stay exact. |

### Q14-007 — REACHABLE (EVENT)

> | M6-006 | **KEEP** | 0x009A6704;0x009A0EF8;0x009AE0B0;0x009AA3DC;0x009AA0FC;0x009A9F88;0x009AF8A8;0x00A62A1C;0x00A663C8;0x00A645C8;0x00A04F54 | Queued event/action timing, play counts, callbacks/flags, switch resolution and start/stop/seek behavior remain exact, including unresolved drain internals. |

### Q14-008 — REACHABLE (RNG)

> | M6-007 | **KEEP** | 0x00A08A7C..0x00A08AC0;0x0099DB58;0x00A09698;0x00A099BC;0x00A0A3B4;0x00A08A44;0x00A08694;0x00A0A524..0x00A0A6E8 | RNG state/seed, draw cadence, eligibility, shuffle/avoid-repeat, weights and sequence behavior stay exact even where they use floating arithmetic. |

### Q14-009 — REACHABLE (CONT)

> | M6-008 | **KEEP** | 0x00A0AFDC;0x00A091CC;0x00A09C40;0x00A6A07C;0x00A6A580;0x00A62ED4;0x00A35998;0x00A6A2DC;0x00A4304C;0x00A52B90;0x00A549A0;0x00A09F04;0x00A6ACC0;0x00A03618 | Shared/per-object state, mode-4 chaining, lookahead/delay split, zero-frame fade handling, next-choice/start notification and Term/EndOfEvent latency remain exact scheduling/state work. |

### Q14-010 — REACHABLE (RTPC)

> | M6-009 | **KEEP** | 0x00A14E28..0x00A15244;0x00A17724;0x00A17878;0x00A17280;0x00A0F07C;0x00A0F594;0x00A137D8..0x00A13A60;0x00A0E5E4;0x00A1B5FC;0x00A0F678 | Curve search/shapes/scaling, RTPC values/precedence/accumulation, transition gates and value evolution, BuiltIn semantics and live delivery are parameters and state, including fast-log/pow arithmetic. |

### Q14-011 — REACHABLE (GAIN)

> | M6-010 | **KEEP** | 0x009EF258;0x009FFAD4;0x009FAE18;0x009A080C..0x009A0908;0x00A4B608..0x00A4B674;0x009BD368..0x009BD8B4;0x009C54E8 | Gain inputs/composition, randomizer/root note, mute keys, ducking maximum, audibility thresholds and gain values/routing stay exact; fastpow feeding those decisions is not blanket libm relief. |

### Q14-012 — REACHABLE (FILTER)

> | M6-011 | **KEEP** | 0x009FFD14..0x009FFD74;0x00A550D8..0x00A551EC;0x00A44630;0x00A766F0;0x00A77480;0x00A769EC..0x00A76A2C | Target writers/attenuation, cutoff map parameters, eight-step ramp cadence, finish countdown and immediate bypass predicates remain exact; do not invent default-zero inputs. |

### Q14-014 — REACHABLE (PAN)

> | M6-012 | **KEEP** | 0x00A4FBEC;0x00A45E9C;0x00A25FF8;0x00A1F79C;0x00A209BC | Channel mapping/LFE route, matrix inputs and updates, first-update rule and conn+6C fade arming remain exact; panner decisions cannot be replaced by an arbitrary stereo average. |

### Q14-015 — REACHABLE (PAN)

> | M6-012 | **VERIFY** | 0x00A25FF8;0x00A1F79C;0x00A209BC | Separate pure matrix-weight calculation from channel selection, speaker-mask routing and parameter/state writes before permitting any equivalent arithmetic. |

### Q14-016 — REACHABLE (FX)

> | M6-013 | **KEEP** | 0x00A4FD84..0x00A4FEF4;0x004DEB18;0x004DEB8C;0x00AA19CC;0x00AA25E0 | Registry, ShareSet parameters, slot order, bypass/LFE/channelLink settings, live insert-FX wiring, detector choice and reset timing remain exact; do not default missing bank fields. |

### Q14-017 — REACHABLE (FX)

> | M6-013 | **VERIFY** | 0x00AA25E0;0x00AA19CC;0x00AA18F4 | Separate coefficient/design computation and detector sample state from externally supplied settings, threshold/release control and enable choices; equivalence cannot legalize different filter types or parameters. |

### Q14-019 — REACHABLE (BUS)

> | M6-014 | **KEEP** | 0x00A42210;0x00A4FEF8;0x00A4F754;0x00A4F36C;0x00A43F64;0x00A4ECE4;0x005985FC;0x009CC2AC;0x009CC4D8 | Reuse keys/eState, aux-connect policy, device gates, FX factory/bypass, idle-frame lifetime, tail and zero-length chunks are routing/state/timing, not DSP. |

### Q14-020 — REACHABLE (ROBOT)

> | M6-015 | **KEEP** | 0x008DBD74;0x008DBFE8..0x008DC034;0x008DBF76..0x008DBFB6;0x005942C6..0x00594354 | Hijack registration, persistent validFrames, DataReady/NoMoreData callbacks, reset/flush and exact 22320-Hz/744-byte output framing remain exact; only its delegated resampler math is SIMPLIFY under M6-004. |

### Q14-021 — REACHABLE (ROBOT)

> | M6-016 | **KEEP** | 0x0059687E..0x00596914;0x00597F12..0x00597F8E;0x0059818C..0x005982A0;0x00596DC8;0x0059962A..0x005999A4;0x00599E6A..0x00599EB8;0x008D88CC;0x00597DB4..0x00597E8E;0x0059678E..0x005967B8 | Production composition, event_volume/robot_volume delivery, OnDevice object6 route, alternative draw/order, callbacks, abort and scheduling remain exact. |

### Q14-022 — REACHABLE (TICK)

> | M6-017 | **KEEP** | 0x009AF9F4..0x009AFAB8;0x00A4087C;0x00A40940;0x009AFD10;0x00A38420;0x00A03618;0x009EC418;0x009EBE6C | Thread lifecycle, group/voice/bus order, gate writers, pending-action drain, EndOfEvent and post-Term latency remain exact; external pacing uncertainty is not an ADP-1 arithmetic exemption. |

### Q14-023 — REACHABLE (FORMAT)

> | M6-018 | **KEEP** | 0x00A57724..0x00A5792C;0x00A1C75C;0x00A1C7D4;0x00A56E20..0x00A5705C | No-JNI 48000/1024 branch, rate-to-bus/voice link, frame rounding and exact robot-rate handoff are timing/geometry; Android system query is an existing separate external boundary, not new DSP permission. |

### Q14-024 — REACHABLE (LOAD)

> | M6-020 | **KEEP** | 0x009B0C9C..0x009B1410;0x00A27CA4..0x00A27DD0;0x00A325E0..0x00A32914;0x00A3B84C..0x00A3BB80 | STMG raw readers, dedup/refcounts and consumers remain exact; unavailable human-readable names can remain UNKNOWN, but are not pure-DSP DROP items. |

### Q14-025 — REACHABLE (RNG)

> | M6-021 | **KEEP** | 0x0099DB58;0x0099EF80;0x00A08A7C..0x00A08AC0 | Review the test seed seam/live Unix-seconds seed separately; RNG and draw order are explicitly exact under ADP-1, even though the record is policy. |

### Q14-026 — REACHABLE (VOICE)

> | M6-022 | **KEEP** | 0x00A44D4C;0x00A44948;0x00A44C18;0x00A54F1C..0x00A5574C;0x00A4B93C;0x009BE28C;0x009BDA88;0x009BD368;0x009BF8E4;0x00A5E694;0x009EEDA4 | Production wiring, voice state machine, effective-parameter inputs, route identities/virtual bodies, device/sample-scale writers and callback registry remain exact; anonymous class names need no invented semantics. |

### Q14-027 — REACHABLE (MOD)

> | M6-022 | **KEEP** | 0x009E52F8..0x009E5E8B;0x009E2BD0..0x009E52F3 | Modulator segment boundaries/slopes, first-sample convention, evaluator population/delivery and LFO/control-signal evolution affect parameter values and timing; no wholesale modulator DSP exemption. |

### Q14-028 — REACHABLE (METER)

> | M6-022 | **VERIFY** | 0x00A50044..0x00A50FD0;0x00A52164..0x00A5266B;0x009C806C..0x009C8104 | Meter filter identity, lane mapping and metric consumers must be bounded: prove whether a result drives decisions or is callback-visible before simplifying it. |

### Q14-029 — REACHABLE (PAN)

> | M6-022 | **VERIFY** | 0x00A25FF8;0x00A4D994;0x00A4BC58 | Panning/conversion calculations mix weight arithmetic with masks, routing and control-state updates; use the M6-012 boundary, not a whole-function DROP. |

### Q14-030 — REACHABLE (UNITY)

> | M6-023 | **KEEP** | 0x005919B8;0x008DFC4C;0x008DED14;0x008D1F20;0x008D8CE4;0x009A6704 | Unity/app dispatch, flags/cookie/callback propagation and live registration remain exact event behavior. |

### Q14-031 — REACHABLE (LOAD)

> | M6-024 | **KEEP** | 0x00592BB0;0x005935E2;0x005935EA;0x008D2EE8;0x008D2FE4;0x008D8280;0x008D8320 | Six-bank order, scene construction, unconditional load path, zip registration and live wiring are exact graph/loading work; the stale AddZipFiles claim must be checked against C33, not waived. |

### Q14-032 — REACHABLE (VOICE)

> | M6-025 | **KEEP** | 0x00A379D8;0x00A000E8;0x009FFAD4;0x00A4304C;0x00A558AC;0x00A562B8;0x00A54A30;0x009D3558;0x009D3644 | All Play/PBI/source creation, arguments, ownership, results, device/listener setup, failure cleanup and production wiring remain exact; FX sample bodies are separately split below. |

### Q14-033 — REACHABLE (VOICE)

> | M6-025 | **KEEP** | 0x009EB4C8;0x00A0CA04;0x00A40940;0x009EBE38..0x009EC0F0;0x00A42210;0x00A0428C;0x00A054D8;0x00A53558;0x00A5358C;0x00A535D8;0x00A53698;0x00A22304;0x00A22684 | Indirect-caller census, initialization barrier, re-init triggers, null-parent remainder, pending-state/device-build bodies and absent field writers are state/lifetime gaps. |

### Q14-034 — REACHABLE (CONT)

> | M6-025 | **KEEP** | 0x009EE9C4..0x009EEA5C;0x009E8940;0x009E8FCC;0x009E8F10;0x009E91F8;0x00A347A8;0x00A0CCF8;0x00A0CD78;0x009D0F3C;0x009EE2D8 | Continuous validation, switch precedence/last-switch state and special Sound dispatch remain exact even on unexercised branches. |

### Q14-035 — REACHABLE (VOICE)

> | M6-025 | **KEEP** | 0x00A01768;0x009BCA68;0x00A0228C;0x00A72760..0x00AB138C;0x00A56414..0x00A56468;0x009EEDA4;0x00A4C3D8..0x00A4C504;0x009EA23C | Source format writers, inaudibility/limiter decisions, source close/init ordering, route caches and device-table checks remain exact; later correction evidence does not eliminate their independent verification obligation. |

### Q14-036 — REACHABLE (FX)

> | M6-025 | **VERIFY** | 0x00A793D4;0x009CF644;0x00A6C22C;0x00A54A30 | Unknown FX helper/wrapper descendants must be separated into ownership/format/buffer/control KEEP and any demonstrated pure sample math SIMPLIFY. |

### Q14-037 — REACHABLE (LIMIT)

> | M6-026 | **KEEP** | 0x009ED2CC..0x009ED3D0;0x009C4F30;0x009F29E8..0x009F2BC4;0x009FA01C;0x009FA6F8;0x00A37100;0x00A01CA4;0x00A029DC;0x009ED428 | Playback limits, virtual/kill choice, counters, priority/tie order, remove/reposition, stop/Term/flush and production wiring remain exact; this limiter is a voice-count decision, not a peak-limiter DSP body. |

### Q14-038 — REACHABLE (VOICE)

> | M6-026 | **KEEP** | 0x00A54F50..0x00A5531C;0x00A548C0;0x009C5154;0x009BC66C;0x009BE898;0x00A11F98;0x00A1E8F4;0x00A1C660;0x00A1ECBC;0x00A3E27C;0x009BDC8C;0x00A366AC;0x009BDA28;0x009E808C;0x00A0054C;0x00A3EE9C..0x00A407B0 | Remaining voice/parameter/callback/MIDI-registration stores, missing collaborators, zero-playing-id event path and choices remain decision/state/timing work; no ADP-1 release. |

### Q14-039 — REACHABLE (VOICE)

> | M6-026 | **KEEP** | 0x00A17724;0x00A17878;0x009F7390..0x009F82EC;0x00A44D4C;0x00A023D4;0x00A01918;0x009E85C8;0x009FFC28 | RTPC max-instance subscription, device loop, Play success tail, parameter args/producers and early-return gates remain exact, including branch signedness and bus-count choices. |

### Q14-040 — REACHABLE (SING)

> | M9-002 | **KEEP** | 0x005EEB30..0x005EEE9F;0x00544444..0x0054445E;0x00540D1C;0x0054F70C | Trigger resolution failure, compound propagation, per-step timeout and render wait are singing/action decisions. |

### Q14-041 — REACHABLE (SING)

> | M9-003 | **KEEP** | 0x005EF0C8..0x005EF30F;0x005EF490..0x005EF4DF;0x005EF2C6 | Running means, posted vibrato values, duration log, acting-tag return and stop preserving smoothing state are parameter/lifecycle behavior. |

### Q14-042 — REACHABLE (MIDI)

> | M9-004 | **KEEP** | 0x00A3BDFC..0x00A3BEE4;0x00A3CC48 | Music hierarchy, table-select flag, nearest overridden MIDI target and target0 failure are routing decisions; bank-only evidence must be expanded with these runtime consumers. |

### Q14-043 — REACHABLE (NOTE)

> | M9-005 | **KEEP** | 0x00A3F76C..0x00A3FBC0 | MIDI tick-to-time, division9600, effective tempo and property55 are exact scheduling; header tempo cannot be discarded as a data-fit approximation. |

### Q14-044 — REACHABLE (MOD)

> | M9-006 | **KEEP** | 0x009D7B6C..0x009D7C97;0x009D552C..0x009D55F0;0x009D7EA4 | Modulator payloads, trigger selector property15 and stop gate property1 are decisions/state. |

### Q14-045 — REACHABLE (MOD)

> | M9-007 | **KEEP** | 0x00A14F88..0x00A15038;0x009D552C | Note-on/off gate, curve scaling2 fast-log and envelope trigger timing determine RTPC/control values; ADP-1 does not exempt them or the circular test. |

### Q14-046 — REACHABLE (MOD)

> | M9-008 | **KEEP** | 0x009D671C..0x009D7727;0x005EF184..0x005EF18C | Posted shake RTPC and per-voice depth initialization cadence must match; rereading once per block changes parameters/state. |

### Q14-047 — REACHABLE (MOD)

> | M9-009 | **KEEP** | 0x00A6E848..0x00A6F133 | Curve/property accumulation, buffer delivery and consumer update cadence remain exact parameter work; arbitrary per-sample application remains unsupported. |

### Q14-048 — REACHABLE (MOD)

> | M9-009 | **VERIFY** | 0x00A6E848..0x00A6F133 | If a descendant merely multiplies final PCM by an already-exact parameter stream, isolate that sample loop first; current evidence does not settle the boundary. |

### Q14-049 — REACHABLE (NOTE)

> | M9-010 | **KEEP** | 0x00A3E688..0x00A3E9F4;0x00A3E6A8..0x00A3E728 | Held-note lifetime via PBI vt+1C, recorded-node replay and fades are MIDI/selection/timing decisions; no fresh note-off RNG draw. |

### Q14-050 — REACHABLE (ROBOT)

> | M9-011 | **KEEP** | 0x00A4FD84..0x00A4FEF4;0x00AA257C;0x00AA18F4;0x008DBFE8;0x00A57724 | Robot_Bus_1 two-EQ/limiter/Hijack order, 48000 mix side, 22320 output and wiring stay exact; share the M6-013 arithmetic boundary only. |

### Q14-051 — REACHABLE (MIDI)

> | M9-013 | **KEEP** | 0x009B3260..0x009B4033;0x009BBF9C..0x009BC17B;0x00A78D10..0x00A78DE3 | Whether target110896138 routes notes to get-in branch403781184 is MIDI routing, not a waveform approximation. |

### Q14-052 — REACHABLE (MIDI)

> | M9-014 | **KEEP** | 0x00A78D10..0x00A78DE3;0x009B3260..0x009B4033 | Trace velocity to every gain/RTPC input and any implicit binding; velocity-to-level decisions stay exact, though the final PCM multiply can share M6-010 equivalence. |

### Q14-053 — REACHABLE (RNG)

> | M9-015 | **KEEP** | 0x0098A6D4..0x0098A7B8;0x00A3EA3C;0x00A3DDF0 | Container draws occur when each note fires, not prewarm; exact RNG cadence is explicitly required. |

### Q14-055 — REACHABLE (CLASS)

> | M9-017 | **KEEP** | 0x005EECB0..0x005EED6F;0x00635474..0x006355BF;0x00636578..0x0063682F | Cube acceleration HPF is a reaction classifier, not audio PCM DSP; first-sample initialization, smoothing, hysteresis/count thresholds and posted vibrato remain exact. |

### Q14-056 — REACHABLE (NOTE)

> | M9-020 | **KEEP** | 0x00A3CEB4;0x00A3F76C..0x00A3FBC0 | BeginTrim/clip-window filtering and held-note release at clip end require runtime timing/lifetime proof; circular tests remain defects. |

### Q14-057 — REACHABLE (RNG)

> | M9-022 | **KEEP** | 0x00A08A44;0x00A0A524;0x00A3E6A8..0x00A3E728 | Eligibility/blocked-list/random/sequence state and replaying recorded note-on selection stay exact. |

### Q14-059 — REACHABLE (MOD)

> | M9-024 | **KEEP** | 0x009D552C..0x009D55F3;0x009D5934..0x009D6598;0x009D7FC0..0x009D8137 | Envelope stop/trigger selector consumers determine voice lifetime; do not simplify a stop decision as envelope DSP. |

### Q14-060 — REACHABLE (MOD)

> | M9-025 | **KEEP** | 0x009D671C..0x009D7727;0x009D7FC0..0x009D8137;0x009E266C..0x009E2813 | LFO waveform is a modulation parameter stream feeding singing/pitch, not one of the allowed PCM filter/mixer operations; retain exact wave shape, phase, extrema and timing. |

### Q14-061 — REACHABLE (FX)

> | M9-026 | **KEEP** | 0x00AA257C;0x00AA18F4;0x00AA19CC | EQ/limiter selection, frequency/Q/gain and attack/release/channel settings remain exact. |

### Q14-062 — REACHABLE (FX)

> | M9-026 | **VERIFY** | 0x00AA2870..0x00AA2898 | Establish the coefficient-design boundary before changing the reported one-ulp association: this is not certified pure per-sample math, and parameter/state decisions remain KEEP. |

### Q14-063 — REACHABLE (FX)

> | M9-027 | **KEEP** | 0x00AA25E0;0x00A57724;0x008DBFE8 | 14298-Hz band, 48000 mix format and 22320 Hijack handoff are parameters/routing, so a 22320-Hz upstream clamp remains wrong under ADP-1. |

### Q14-064 — REACHABLE (FX)

> | M9-027 | **VERIFY** | 0x00AA25E0 | Establish coefficient-design arithmetic versus parameter/type/state writes before granting equivalence; the sample-loop exception alone does not settle this initializer. |

### Q14-065 — REACHABLE (SING)

> | M9-028 | **KEEP** | 0x00599F60..0x00599FBF;0x00596DC8 | Game object7 on-robot/object6 off-robot, zero transition and dispatch order remain exact live routing work. |

### Q14-066 — REACHABLE (VOICE)

> | C30.1 / N1 / StartStream arguments | M6-025 | **KEEP** | 0x00A544C4..0x00A545DC;0x00A56650 | Raw results, media-pointer/size arguments, Â±0.5f/trunc_s32 window, source bit gates and deliberate fault are timing/error behavior, not PCM math. |

### Q14-067 — REACHABLE (NOTE)

> | C30.2 / music PBI / prior A9 | M9-010,M9-020,M9-022; NEW M9 obligation | **KEEP** | 0x00A3E688..0x00A3E82C;0x00A3E920..0x00A3E9F4;0x00A381F4 | Recorded-node note-off replay, code2/3 lifetime and priority/voice-limit branch belong to music and remain exact; do not absorb into M6-026. |

### Q14-073 — REACHABLE (FX)

> | C30.8 / voice init seam | M6-025,M6-013 | **KEEP** | 0x00A54A30 | Caller/return/failure/FX-selection proof remains required; C31 improves evidence but not authority/settlement automatically. |

### Q14-074 — REACHABLE (VOICE)

> | C30 linker Init / device table / Reserve | M6-025,M6-022 | **KEEP** | 0x00A4F0EC;0x009EA23C;0x00A22A3C;0x00A4C280 | Initialization stores, device ownership/scan/append/remove, bus vt+98 and allocation-failure result2 are lifetime/routing. |

### Q14-075 — REACHABLE (VOICE)

> | C30 voice collaborators / C30.W | M6-016,M6-017,M6-022,M6-025,M6-026 | **KEEP** | 0x009D3CC0;0x00A43D24;0x00A39564;0x00A62A1C;0x00A4304C;0x00A44D4C;0x00A38420 | Live composition and ordered collaborators remain necessary; exact standalone components do not prove the production path. |

### Q14-076 — REACHABLE (LOAD)

> | C31.1 R1.1–R1.14 / media-table writer and manager census | M6-025,M6-024 | **KEEP** | 0x00A1EC54;0x009BB320;0x009BB1F8;0x00A01EF4;0x00A028F0..0x00A02938;0x00A04D48;0x009B49A4 | Pointers/size/bank ownership, table population and caller gates are loading/lifetime; C32 closes extraction claims, independent verification remains. |

### Q14-078 — REACHABLE (FX)

> | C31.2 R2.1–R2.15 caller, registry, wrapper setup | M6-025,M6-013 | **KEEP** | 0x00A54A30;0x009CC2AC;0x009CC4D8;0x00A47038;0x00A764D4;0x00A5676C;0x00A5335C | Plugin selection/type/version, bypass/async/in-place flags, format changes, wrapper construction, allocation and chain connection/cleanup order remain exact. |

### Q14-079 — REACHABLE (FX)

> | C31.2 R2.15 wrapper render / unknown helpers | M6-025,M6-013 | **VERIFY** | 0x00A793D4;0x009CF644;0x00A6C22C; vtables0x0103DB98/0x0103DC38 | Close virtual+24/+28/+2C control contracts and identify render descendants before calling them DSP-only. |

### Q14-080 — REACHABLE (FX)

> | C31.2 Compressor init/settings | M6-013,M6-025 | **KEEP** | 0x00AA0538;0x00AA0808;0x00A54A30 | Creation, parameter reads, formats, initialization failure and reset/bypass remain exact; execute endpoint UNKNOWN in current correction. |

### Q14-083 — REACHABLE (GAIN)

> | C31.3 effective parameter recompute / R3.1–R3.2 | M6-022,M6-025,M6-026 | **KEEP** | 0x009BCA68;0x009FFAD4;0x009FFD14..0x009FFE18 | Full recompute, bit reinterpretation/polynomial and threshold compare determine whether a voice is runnable/audible, so no fast-math drop. |

### Q14-084 — REACHABLE (VOICE)

> | C31.3 R3.3–R3.8 / undo / source sibling | M6-022,M6-025,M6-026 | **KEEP** | 0x00A0228C;0x00A022E8;0x00A370E4;0x00A55A84;0x00A55D04;0x00A53244;0x00A76608;0x00A69A38;0x00A69AC8;0x00A47360 | Limiter counters, source destroy path and ordered buffer cleanup are ownership/results; caller/absence census and global decrement remain exact checks. |

### Q14-085 — REACHABLE (TICK)

> | C31.4 R4 clock/callback/duration/stop offset | M6-026,M6-022,M9-010 | **KEEP** | 0x00A03618;0x00A05574;0x00A05370;0x00A054D8;0x00A56414;0x00A55CC4;0x00A5495C;0x00A56478;0x009CBACC;0x009886C0;0x0098822C | Clock sign extension, callback/game-object release, duration/pitch divisor, one-shot offset getter and writer census all alter timing/state; keep101f0x42CA0000 and Â±half bits exact. |

### Q14-086 — REACHABLE (FORMAT)

> | C31.5 R5 frame writer and seven callers | M6-018,M6-022,M6-025 | **KEEP** | 0x00A56650;0x00A52B90;0x00A44948;0x00A44BD0;0x00A1C7D4;0x00A57724 | Exhaustive callers and frame/rate-setting branch determine cadence; no arithmetic relief for clock or frame count. |

### Q14-087 — REACHABLE (TICK)

> | C31 explicit residual callbacks/state | M6-022,M6-025,M6-026 | **KEEP** | 0x00A0B600;0x00A05934;0x00A1C660;0x00A1C65C;0x009A6988;0x00A0C238; item+48 writer UNKNOWN | Unread action/callback/state bodies and a missing item-field writer are not proven pure DSP. |

### Q14-088 — REACHABLE (VOICE)

> | C32.1 P1–P12 shipped-path census | M6-025,M6-026 | **KEEP** | 0x009EEDA4;0x009F1F80;0x00A37A18;0x00A37C90;0x009BEB30;0x00A00618;0x00A0067C;0x00A1D448 | Bank reachability, positioning/virtual branches, fade-in and MIDI gate must be checked as exact decisions; later C34.5 narrows one builder route only. |

### Q14-089 — REACHABLE (LOAD)

> | C32.2 M1–M9 media table/assets | M6-001,M6-024,M6-025 | **KEEP** | 0x009B49A4;0x009B7A34;0x00A1EC54 | DIDX/DATA writer/lookup closure, streamed prefixes, media aliases and no-DIDX plugin cases are parsing/loading, not DSP. |

### Q14-090 — REACHABLE (VORBIS)

> | C32.3 S1–S7 source contracts | M6-002,M6-003,M6-022,M6-025 | **KEEP** | 0x00A562B8;0x009CC3EC;0x00AB0B20;0x00AB22D4;0x00AB12B4;0x00A7270C;0x00A73B40;0x00A72E00 | Source type writer, relocated slots, first-call/header branches, packed format fields, raw results and S4/S6 streaming behavior stay exact. |

### Q14-091 — REACHABLE (VORBIS)

> | C32.3 render/stream residuals | M6-002,M6-022,M6-025 | **KEEP** | 0x00AB1C04;0x00AB2088;0x00AB2BFC;0x00AB3244;0x00AB0448;0x00AB1550;0x00A74E00;0x00A746A8;0x00A75BC4;0x00A78D10 | Data collection/decode/stream lifecycle and plug-in-source selection remain exact; C33/C36 later evidence can reduce unread work only after checking, not through ADP-1. |

### Q14-092 — REACHABLE (FORMAT)

> | C32.4 F1–F5 rate/frame closure | M6-018,M6-022,M6-025 | **KEEP** | 0x0099DC68..0x0099DCE4;0x008D8158..0x008D81FC;0x00A57724..0x00A57930;0x00A548B8;0x00A35938..0x00A35964 | Defaults, complete writer census, JNI gates, rate-to-voice link and transition math are scheduling/parameters; imported exp used for transition timing is not a pure PCM loop. |

### Q14-093 — REACHABLE (MOD)

> | C32.5 V1/V2/V5/evaluators | M6-022,M9-008,M9-009,M9-025 | **KEEP** | 0x009E6D2C..0x009E6E10;0x009E2BD0..0x009E52F3;0x009E2AE4;0x009D8A24 | Prove list population/reachability and exact control-signal evaluator output; the verified empty-list case does not prove production silence. |

### Q14-094 — REACHABLE (IO)

> | C33.1 resolver A1–A11/B/C / index | M6-024 | **KEEP** | 0x00592CDC..0x00592D78;0x00593BF6..0x00593C44;0x008D824A..0x008D82B4;0x008D843C;0x008D6FFC;0x008DDECC..0x008DE324 | Directory binding, APK-before-OBB registration, loose-file/stat gate, index population/duplicates and binary read fields remain exact I/O/loading. |

### Q14-096 — REACHABLE (IO)

> | C33.2 stream manager/device/GetBuffer | M6-024,M6-025,M6-022 | **KEEP** | 0x009654E4;0x00961664;0x00964E4C;0x00965244;0x00961C1C;0x00961AD8;0x00964CB8;0x009657E0;0x00965B1C;0x00964D64;0x009656AC | Vtable identities, buffers, state/result codes and ownership remain exact despite external I/O completion-time inputs. |

### Q14-097 — REACHABLE (IO)

> | C33.3 F/G stream retry / prefix proof | M6-002,M6-003,M6-022,M6-025 | **KEEP** | 0x00A74564;0x00A7482C;0x00A74970;0x00A746A8;0x00A74E00;0x00A7538C;0x00AB22D4;0x00AB1C04;0x00AB2088;0x00AB2BFC;0x00AB1550;0x00A56650;0x00A544BC | 0x3F producers, raw non-1 results, voice-pass retry latch, frame window and setup/seek prefix coverage are timing/error/loading behavior. |

### Q14-098 — REACHABLE (IO)

> | C33.4 external completion-time boundary | M6-024,M6-025; NEW external-timing record pending | **KEEP** | 0x00965B1C;0x009656AC;0x00961800;0x009713B4;0x0096FE70;0x00961C58..0x00961C70 | ADP-1 adds no timing exemption: retain the existing operator-directed OS boundary, but extract/check every shipped checkpoint/retry/cache/stale-out operation exactly. |

### Q14-099 — REACHABLE (FORMAT)

> | C33.5 P1–P8 / no-JNI / format link | M6-018,M6-015,M6-004 | **KEEP** | 0x0099DC68;0x008D8158;0x0099E458;0x00A570A4;0x00A57724;0x00A56E20..0x00A57060;0x00A1C75C;0x00A1C7D4;0x005942CE..0x005942E6 | Default/writer absence, copies, unsigned min, JNI branch, derived timing and PS+38-to-Hijack format remain exact; separate phone-system policy is not DSP DROP. |

### Q14-100 — REACHABLE (IO)

> | C33 I/O memory manager / scheduler ties | M6-024,M6-025 | **KEEP** | 0x00969E8C;0x009713B4;0x009716F0;0x0096FE70;0x0097161C;0x00979B98;0x00962EA8;0x00962C24 | Allocation/cache availability, scheduling priority and tie-breaking are decisions/state/timing. |

### Q14-101 — REACHABLE (VORBIS)

> | C33 decoder/cache/emit/header residuals | M6-002,M6-003,M6-025 | **KEEP** | 0x00AB2D74;0x00AB7E40;0x00A73490;0x009CD340 | WEM decoding and frame handoff remain exact; later C36 rows are verification leads, not ADP-1 waivers. |

### Q14-102 — REACHABLE (ADPCM)

> | C33 ADPCM/PCM class residuals | M6-003,M6-025 | **KEEP** | 0x00A73ABC;0x00A73D34;0x00A739E8;0x00A75E34;0x00A75B1C;0x00A736D4 | Codec/format/stream/channel/seek behavior stays exact, including PCM data representation and start positions. |

### Q14-103 — REACHABLE (PLUGIN)

> | C33 remaining opaque helpers / plug-in source | M6-025,M6-024; M9-005 | **VERIFY** | 0x00A059D8;0x008DA938;0x00A78D10 | Establish helper/source descendants first; keep any source generation, MIDI, callback, timing and routing contract; no pure per-sample DROP is established here. |

### Q14-104 — REACHABLE (BUS)

> | C34.1 B1–B18 bus walk / parameter census | M6-025,M6-022,M6-014 | **KEEP** | 0x009F4BB8;0x00981940;0x009F1C40;0x009F1EE8;0x009C54E8;0x009C39DC;0x009FFEE4..0x00A00000;0x00A37FE8..0x00A38130 | Ancestor links, output-bus reachability, effective-parameter composition and cached path gates remain exact even when a branch is unused by shipped assets. |

### Q14-105 — REACHABLE (RTPC)

> | C34.2 R1–R11 modulator/RTPC | M6-009,M6-025,M9-009 | **KEEP** | 0x00A11590;0x00A17280;0x009E6748;0x009E8224;0x009E61B4;0x009E62AC;0x00A01918;0x009DCE44;0x00A6E848 | Store fallback, accumulation, subscription/list consumption and modulator consumers set values/state; full descendants and shipped binding census remain required. |

### Q14-107 — REACHABLE (VORBIS)

> | C34.3 S1–S8/S10 source duration/close/notification/free | M6-002,M6-003,M6-025,M6-026 | **KEEP** | 0x00A72F5C;0x009D4B20;0x00A72AF4;0x00A7427C;0x00A76178;0x00AB0FC0;0x00AB2958;0x00A38600;0x00A7A914;0x00A7A988 | Duration arithmetic is timing; ordered DSP teardown/free/reset, notification allocation fault and pool bookkeeping are lifetime/results, regardless of the word DSP in a destructor name. |

### Q14-108 — REACHABLE (VORBIS)

> | C34.3 still-unread internals and S8 callers | M6-002,M6-025,M6-026 | **KEEP** | 0x00AB3428;0x00A38420;0x00A38600;0x00A7A914;0x00A7A988 | Close codec-state teardown, allocator/free internals, flush and caller census as exact ownership/event ordering. |

### Q14-109 — REACHABLE (LOAD)

> | C34.4 K1–K19 bank/chunks | M6-001,M6-024,M6-025 | **KEEP** | 0x009B74D8;0x009B3260;0x00A68150;0x009B2B08;0x009A6518; XOR key0x0108D9A0 writer UNKNOWN | Header XOR/key writer, INIT/ENVS/PLAT/STID, per-type creators, hook results, mode args, unload/media-pool and stream bounds are all exact parsing/state. |

### Q14-110 — REACHABLE (MIDI)

> | C34.5 G1–G3 / other MIDI posters | M6-025,M9-004,M9-005,M9-013 | **KEEP** | 0x00A62A1C;0x00A1D448;0x009EE230 | Play builder0/FF and action census narrow the normal route only; locate other0x90 posters as MIDI/routing work. |

### Q14-113 — REACHABLE (RTPC)

> | C35.2 L5-01–L5-08 loader | M6-001,M6-009 | **KEEP** | 0x009F1DE0;0x00A1A338;0x00A11F98;0x00A117C8;0x00A1A160;0x00A0F990;0x00A0F07C;0x009E6EDC;0x00A11624;0x00A10C98;0x00A12244 | Key selection, point order, duplicate replace-and-append, error0x1F/allocation0x34 and unread loader/subscription bodies remain exact parsing/parameter work. |

### Q14-114 — REACHABLE (RTPC)

> | C35.3 accumulators / hints | M6-009 | **KEEP** | 0x00A17724;0x00A17878;0x00A14E28 | Sum/product, curve hints and twelve caller census affect control values/order. |

### Q14-115 — REACHABLE (RTPC)

> | C35.4 shipped-data census / C# D1–D9 | M6-009,M6-001 | **KEEP** | 0x009F7254..0x009F72EC;0x00A14E28..0x00A15244 | 64 entries, scaling/shape/accumulate census and binary32-vs-double defects must be verified; no ADP-1 control-value equivalence. |

### Q14-116 — REACHABLE (VOICE)

> | C31.1 | M6-025 | KEEP | 0x00A028F0..0x00A02938;0x00A1EA68;0x00A01EF4;0x009B49A4 | R1.1–R1.14: pre-limiter, playing-manager population, HIRC-to-setter dispatch, writer census and full Term/9B65A8 remain exact; D2 survives. |

### Q14-117 — REACHABLE (FX)

> | C31.2 | M6-025,M6-013 | KEEP / VERIFY | 0x00A54A30;0x00A019B8;0x00A793D4;0x009CF644;0x00A6C22C | Unclosed transitive R2 slots are VERIFY only for possible PCM math; caller/ownership/format/bypass and D3 stay KEEP. R2.15 is a gap declaration, not a verified DSP implementation. |

### Q14-118 — REACHABLE (GAIN)

> | C31.3 | M6-022,M6-025,M6-026 | KEEP | 0x009BCA68;0x009FFAD4;0x00A370E4;0x00A55A84 | Full recompute, sibling called operations, global undo and R3.5/R3.8 caller/absence census remain exact. |

### Q14-119 — REACHABLE (TICK)

> | C31.4 | M6-026,M6-022,M9-010 | KEEP | 0x00A05574;0x00A56478;0x00A0393C;0x0098822C;0x009CBACC | Position clock, callback registration/emit and complete stop-offset writer census remain exact even though inspected direct slices held. |

### Q14-120 — REACHABLE (FORMAT)

> | C31.5 | M6-025,M6-018 | KEEP | 0x00A56650;0x00A44948;0x00A1C7D4;0x00A57724 | Exhaustive caller/writer census and frame-setting branch are cadence/geometry. |

### Q14-121 — REACHABLE (VOICE)

> | C32.1 | M6-025 | KEEP | 0x00A379D8;0x00A1D448;0x009F1F80 | Repeat the independent bank/Play reachability census; preserve called-path uncertainty. |

### Q14-122 — REACHABLE (LOAD)

> | C32.2 | M6-024,M6-025 | KEEP | 0x009B49A4;0x009B7A34;0x00A1EC54 | Asset census slice held; native table writer/population closure remains loading proof. |

### Q14-123 — REACHABLE (VORBIS)

> | C32.3 | M6-002,M6-003,M6-025 | KEEP | 0x00AB22D4;0x00AB1C04;0x00AB2088;0x00A1EA68;0x009CD340 | S4/S6, source-type writer and remaining header/caller branches are codec/stream lifecycle, not exempt math. |

### Q14-124 — REACHABLE (FORMAT)

> | C32.4 | M6-018,M6-025 | KEEP | 0x00A56E20..0x00A57060;0x00A57724;0x00A548B8;0x00A35938..0x00A35964 | F4 JNI/robot sink branch, F5 rate-to-voice and unrestricted setting writer absence need closure; defaults alone prove neither production rate nor cadence. |

### Q14-125 — REACHABLE (MOD)

> | C32.5 | M6-022,M9-009 | KEEP | 0x009E2BD0..0x009E52F3;0x009E2AE4;0x009E6D2C | V5 list-population and evaluator consumer gaps survive the verified empty-list branch. |

### Q14-126 — REACHABLE (IO)

> | C33.1 | M6-024 | KEEP | 0x008D8280;0x008D843C;0x008DDECC..0x008DE408 | Remaining resolver rows, complete index population, APK/OBB order and AddZipFiles caller absence remain exact; D4 survives. |

### Q14-127 — REACHABLE (IO)

> | C33.2 | M6-024,M6-025 | KEEP | 0x009656AC;0x00965B1C;0x00961C1C | Stream-manager table and GetBuffer transitive closure remain state/results. |

### Q14-128 — REACHABLE (IO)

> | C33.3 | M6-002,M6-025 | KEEP | 0x00AB22D4;0x00A56650;0x00A544BC | Retry and bank-prefix proof remain loading/timing, including conditional PBI prefix size. |

### Q14-129 — REACHABLE (IO)

> | C33.4 | M6-024,M6-025 | KEEP | 0x00965B1C;0x009656AC;0x00961C58..0x00961C70 | Existing OS timing ruling is not an instruction claim; preserve shipped checks/retry around that external input. |

### Q14-130 — REACHABLE (FORMAT)

> | C33.5 | M6-018,M6-004,M6-015 | KEEP | 0x008D8158;0x00A57724;0x00A1C75C;0x00A1C7D4;0x005942CE..0x005942E6 | P2 exhaustive writer absence, imported zero-divisor outcome and Hijack format link remain explicit uncertainties; P1/P3–P8 body checks do not close them. |

### Q14-131 — REACHABLE (BUS)

> | C34.1 | M6-025,M6-014 | KEEP | 0x009F4BB8;0x009C54E8;0x009C39DC;0x009FFAD4 | Full bus census and parameter walk remain routing/value proof. |

### Q14-132 — REACHABLE (RTPC)

> | C34.2 | M6-009,M6-025,M9-009 | KEEP | 0x009BE898;0x009FB9B8;0x009DCE44;0x00A6E848 | D1 context reset/recompute gates and unread modulator callees remain exact; D0 alone is not a sufficient predicate. |

### Q14-133 — REACHABLE (VORBIS)

> | C34.3 | M6-002,M6-025,M6-026 | KEEP | 0x00AB3428;0x00A38420;0x00A7A914;0x00A7A988;0x00A38600 | S1–S8/S10 slices held; codec teardown, flush, allocator internals and S8 caller closure remain lifetime work. |

### Q14-134 — REACHABLE (LOAD)

> | C34.4 | M6-001,M6-024 | KEEP | 0x009B74D8;0x009B3260;0x009B2B08;0x009A6518 | Chunk-loader/census closure and explicit K11/creator/XOR writer residuals remain parsing. |

### Q14-135 — REACHABLE (MIDI)

> | C34.5 | M6-025,M9-013 | KEEP | 0x00A62A1C;0x00A1D448;0x009EE230 | Normal Play builder and906-action census were checked; alternate MIDI posters remain UNKNOWN. |

### Q14-136 — REACHABLE (RTPC)

> | C35 (old availability row) | M6-009,M6-001 | KEEP | 0x00A14E28..0x00A15244;0x009F7254..0x009F72EC | Earlier report found no C35; it now exists, so check current C35.1–C35.4 rather than retaining the stale availability finding. |

### Q14-140 — REACHABLE (CLASS)

> | 0x00636578..0x0063658C (Thumb) | Initial sample copies input vector to previous state and zeroes the three output words. | KEEP: reaction classifier first-sample behavior must remain exact despite its HPF name. |

Manager checkpoint: review the positive witnesses, then settle the conditional exclusions/UNKNOWNs before reducing Q14/Q15 scope. Q14 remains parked, Q15 rows remain unstarted, and no production code or manifest changed.
