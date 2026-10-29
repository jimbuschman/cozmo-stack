# AcknowledgeFace, one whole behaviour: read (extractor, 2026-10-29)

Read-only. Binary resources/lib/armeabi-v7a/libcozmoEngine.so 3.4.0-1204; all addresses disassembled from the .so
(PLT stubs and veneers in ARM mode). Records quoted from re-analysis/fidelity_manifest.json as read 2026-10-29.

| # | step | what the original does | citation | record | class |
|---|---|---|---|---|---|
| C1 | factory | class 0x3A: new(0x140), ctor PLT 0x4AFF64, shared_ptr | 0x0059D0B2 mov.w r0,#0x140; 0x0059D0C2 blx 0x4aff64 | M7-018 | EXACT_SOURCE |
| C2 | ctor | IBehavior ctor; +0x11C = 0; set<int> +0x120 (size +0x128); set<IReactToFaceListener*> +0x130; strh +0x12C = 0 (clears +0x12C and +0x12D); reads no JSON key | 0x00602884..0x006028B6 | NEW | EXACT_SOURCE |
| C3 | config | ReactionAnimGroup in acknowledgeFace.json is not read by this class (only use 0x00603118, AcknowledgeObject); commented keys absent from .so | acknowledgeFace.json:1-12 | M7-018; NEW | EXACT_SOURCE |
| T1 | map | FacePositionUpdated -> AcknowledgeFace | reactionTrigger_behavior_map.json:4-5 | M7-018/M10-003 | EXACT_SOURCE |
| T2 | strategy fill | named-first-time or became-close (300/400 mm, 4.0 s cooldown) | 0x0060CB84..0x0060CE20 (M10-derived.md App. C 3a-3g) | M10-003 | per M10 inventory |
| T3 | AddListener | BehaviorThatStrategyWillTriggerInternal: behaviour vslot AP+0x34 (AddListener, reloc 0x0102B870) with strategy+0x50 | 0x0060D074..0x0060D080; 0x00602C9A..0x00602CB0 | NEW | EXACT_SOURCE |
| T4 | STBI | desiredFaces empty -> false; copy into AF+0x120; robot+0x34A -> false; else IsRunnable | 0x0060CAB0..0x0060CB5E | M10-003 | EXACT_SOURCE |
| T5 | forced trigger | desiredFaces, else FaceWorld::GetFaceIDs(false), else warning | 0x0060C744..0x0060C888 | NEW | EXACT_SOURCE; reachability RECOVERABLE_GAP |
| R1 | IsRunnableBase gates | AP+0x20 (robot+0x355) 0x005BF04C false; AP+0x24 (robot+0x34A) 0x0059EC12 false; AP+0x28 (carrying) 0x00602D20 false (class override) | 0x005BD864..0x005BD89C; vtable 0x0102B834 | M8-001; NEW (override) | EXACT_SOURCE; virtual name UNKNOWN |
| R2 | IsRunnableInternal | +0x128 != 0 | 0x006028BC..0x006028C6 | NEW | EXACT_SOURCE |
| I1 | InitInternal | +0x12C = 1, return 0 | 0x006028C8..0x006028D0 | NEW | EXACT_SOURCE |
| U1 | UpdateInternal | if +0x12C: clear, BeginIteration; tail IBehavior::UpdateInternal (veneer 0x008CC12C -> 0x004B2A90) | 0x00602928..0x0060294A | NEW; M8-001 | EXACT_SOURCE |
| B1 | choose | GetBestFaceToTrack(set, false); 0 -> no action | 0x00602966..0x00602982 | NEW | EXACT_SOURCE |
| B2 | ranking | single -> id unchecked; else min abs(head-robot+0x2FC)+3*abs(body), surcharge 0 with false, strict <, lowest id on ties | 0x0056C110..0x0056C27E | NEW | EXACT_SOURCE; angle helpers RECOVERABLE_GAP |
| B3 | turn ctor | TurnTowardsFaceAction(robot, id, Radians pi 0x40490FDB, sayName 1) via GOT 0x01041F20 (body 0x0054B8F0) | 0x00602986..0x006029AE | M13-014; NEW | EXACT_SOURCE |
| B4 | greet expr | (now - [mgr+0x54] <= 60.0) && [mgr+0x54] >= 0 && !HasTurnedTowardsFace && !+0x12D; info log AcknowledgeFace.DoAcknowledgement | 0x006029B2..0x00602A24; literal 0x00602B14 | NEW | EXACT_SOURCE |
| B5 | mgr+0x54 | ctor -1.0 (0x005A08E4/0x005A08F0); no other writer found | scans listed in report | NEW | UNKNOWN |
| B6 | greet branch | SetSayNameTriggerCallback; lambda 0x00602D8C: +0x12D=1, GreetingSayName emotion (Social +0.6), returns 301 NamedFaceInitialGreeting | 0x00602A4A..0x00602A72; 0x00602D8C..0x00602DDE; face_events.json:40-46; AnimationTrigger.cs:306 | NEW; M7-020 | EXACT_SOURCE (reach per B5) |
| B7 | normal | SetSayNameAnimationTrigger(1 AcknowledgeFaceNamed) | 0x00602A7C..0x00602A80 | M13-014; NEW | EXACT_SOURCE |
| B8 | both | SetNoNameAnimationTrigger(2 AcknowledgeFaceUnnamed) | 0x00602A8C..0x00602A90 | M13-014; NEW | EXACT_SOURCE |
| B9 | images | action+0x188 = 3 (ctor 10); read at 0x0054C69A into WaitForImagesAction | 0x00602A94..0x00602A98 | NEW; M14-002 incomplete | EXACT_SOURCE |
| B10 | StartActing | member callback FinishIteration (GOT 0x0103F580), run on any result (0x005BF8F4) | 0x00602A9A..0x00602AA6; 0x005BE0E4 | M8-001 | EXACT_SOURCE |
| F1 | FinishIteration | erase id; listeners slot0 FinishedReactingToFace(robot,id); BehaviorObjectiveAchieved(0x1A, true); BeginIteration again | 0x00602C34..0x00602C96 | NEW | EXACT_SOURCE |
| F2 | strategy finish | RobotReactedToId; reactedFaces insert; +0x54 = now | 0x0060D082..0x0060D0A8 | M10-003 | EXACT_SOURCE |
| F3 | objective | E2G BehaviorObjectiveAchieved (0x005BE246), info log, DAS | 0x005BE228..0x005BE308 | NEW for caller | EXACT_SOURCE order; packer RECOVERABLE_GAP |
| S1 | StopInternal | listeners slot1 ClearDesiredTargets; set destroyed; +0x12D kept | 0x006028D2..0x00602926; 0x0060D0CA | NEW | EXACT_SOURCE |
| A1 | turn action body | shared child | 0x0054BD34..0x0054C708 | M13-014, M14-002, M14-006, M14-009 | not re-traced |
