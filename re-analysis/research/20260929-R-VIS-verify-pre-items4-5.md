# Verification of R-VIS pre-extraction Part 1 items 4-5 (lines 426-507)
Method: capstone Thumb disassembly of resources/lib/armeabi-v7a/libcozmoEngine.so (.scratch/vdis.py, .scratch/sweep.py; PLT from .rel.plt), CLAD C# under unity/scripts/csharp of the original decompile root. Nothing written except .scratch.

Summary: all rows PASS on substance; three minor wrong readings (end). Manifest unchanged.

## 4a AddLiftOccluder 0x6564D8
- Caller normal path PASS: 0x624F88 ldr r0,[r6,#4]; 0x624F8C ldr r5,[r0,#0xC]; 0x624F8E str.w r5,[r4,#0x90]; ClearOccluders 0x624F98; AddLiftOccluder 0x624FA4; CreateObjectsFromMarkers 0x624FC4.
- Caller empty-list PASS: 0x624F86 beq 0x625020; 0x625024 str [r4+0x90]=0; GetLastImageTimeStamp 0x625028; 0x625030 beq 0x6250D4 when ts==0; else Clear 0x62503A, AddLift 0x625046, CheckForUnobserved 0x62504E, b 0x6250D4. Neither the +0x90 write nor the ts==0 skip is in M11-037 / C3.2.
- History lookup PASS: 0x6564E8/EA ldr [r4+0x14],[..+0x390]; GetRawStateAt 0x6564F8; 0x6564FC beq 0x656522 on 0; 0x656500 cmp #0x6000000 -> sChanneledInfoF 0x65651C -> b 0x656600; else 0x6565EA sWarningF 0x6565FC. Meaning of 0x6000000 UNKNOWN (agree).
- Lift transform PASS: 0x656522 ldrd r3,r2,[sp,#0x8C] (state sp+0x58: +0x34 head, +0x38 lift); 0x516FD0: r7=r2 -> ComputeLiftPose 0x516FF8, r5=r3 -> GetCameraPose 0x517002; translation copied from GetTransform()+0x20 to out+0x20 (0x51704A..0x517058).
- ApplyTo 0x65653E (this+4), Project3dPoints 0x656550 (this+0x24): PASS.
- Scale PASS, CONTRADICTS record wording: 0x656554 vldr s0,[sp,#0x40]; loop 0x65655E..0x656574 over +0x44/+0x48; vsqrt 0x656576; sqrtf fallback 0x656588; AddOccluder 0x656598. Lift transform at sp+0x20 so this is the translation vector length, not a z scale. Record M11-037 evidence and inventory C3.2 (M11-vision.md ~1068) say "the transform z scale": wrong. (Name TranslateForward for 0x84B92A not symbol-checked; +0x20 = translation independently confirmed by 0x51704A and 0x84B944.)
- Default vector PASS: 0x6500CA strd r5,r5,[r6,#8]; 0x6500D0 str r5,[r7,#4]!.
- Only writer SetPhysicalRobot 0x657F3C PASS: literals 0x65804C=0xC1E40000 (-28.5), 0x658050=0xC2120000 (-36.5); cmp r1,#0/vmovne 0x657F54/5A (nonzero -> -36.5). I reconstructed the stack stores: the 8 points are exactly as reported. new 0x60 at 0x657FB2; assign PLT 0x4A4858 at 0x657FDC to this+4.
- Robot::SetPhysicalRobot 0x513914 PASS (BlockFilter::Init 0x513954 when arg==1; strb [r5+0x14] 0x51397A; VisionComponent call 0x51397C). Sole PLT caller by sweep: 0x536980 in HandleFirmwareVersion 0x5368F4: parse 0x53692E, cbz 0x536932, operator[] 0x536938 with "sim" (string at 0x536A4C confirmed), isNull 0x53693C -> r7 unnegated -> 0x536980.

## 4b BlockConfigurationManager
- Ctor PASS 0x616B6A/6C/70/76 (+0x24=0, root, begin=this+0x20, +0xC=0), containers 0x616B82/8A/92, HasExternalInterface 0x616B98. IMPRECISE: only ONE subscription, via 0x616C44 with tag 0x32 (0x616C4C movs r3,#0x32; 0x616C64 strh); the extraction says handlers including tag-50.
- SetObjectPoseChanged 0x616D54 PASS (emplace this+0x1C PLT 0x4A7138; cbnz r6 0x616D6C; erase on this+0x10 0x616D74 only when PoseState==0). PoseState.cs:3-8 Invalid,Known,Dirty. 0x87662E strb.w r6,[r4,#0x24] default (r6 value not proven zero by me).
- Caller PASS: 0x62488C ldrb.w r2,[r6,#0x24]; 0x62488E ldr.w r0,[r5,#0x94]; 0x624892; r3 goes only to the PoseChange list. Sweep: sole PLT call of SetObjectPoseChanged = 0x624892; OnObjectPoseChanged called only at 0x506F98; BroadcastObjectPoseChanged callers 0x5060FA,0x506D48,0x506E9E,0x61D5C8,0x622E4E (match).
- Flag writer PASS: 0x61723A/3C; invoker 0x6172A6..B4 (Get_50, strb [r4+0xC]). CLAD MessageEngineToGame.cs:64 RobotDelocalized=50. Robot emits it at 0x510D88 (next to BlockWorld::OnRobotDelocalized 0x510D62) - not covered.
- Gate PASS (0x616D82..0x616D9C) exactly as reported. Update body PASS (0x616DA2 UpdateAll, 0x616DAE Prune(this+0x44,this+0x64), 0x616DB6 UpdateLast, Notify on [r5+0x40] 0x616DBE). Reset PASS 0x616DC2..0x616DDC; skipped only at 0x616D9A popeq.
- UpdateAllBlockConfigs 0x616F00 PASS (types 0,1,2 -> +0x28,+0x44,+0x60; exits at >2).
- DidAnyObjectsMovePastThreshold 0x616DDE PASS: GetLocatedObjectByIdHelper(id,-1) on [robot+0x34] 0x616E20..2A; map find 0x616E36; absent -> moved, continue (0x616E40->0x616EC4); IsSameAs 0x616E88 with 5,5,5 (movt #0x40A0 0x616E0E) and 0x3F060A92 (0x616E58); ==1 continue else return 1 (0x616EF0); not found -> types 0..2 vtable slot 0 (0x616EB8..BE), nonzero -> moved. IsObjectPartOfConfigurationType 0x61721C: 1->+0x44, 2->+0x60, else +0x28.
- UpdateLastConfigCheckBlockPoses 0x616F44 PASS: literal 0x6171E4=0x660848 (+0x616F88=0xC777D0), table {2,1}; emplace_hint 0x616FCE, __assign_multi 0x616FDE into filter+0x3C; flags strh 0x616FBC; FindLocatedMatchingObjects 0x617002; find 0x61703C, operator= 0x61705A else emplace 0x617098. ObjectFamily.cs Block=1, LightCube=2.

## 5a BroadcastLocatedObjectStates 0x61E6C0
- Entry PASS: 0x61E6BC b.w 0x8CCA6C; veneer word 0xFFBEB3FC; 0x8CCA7C+0xFFBEB3FC=0x4B7E78. MessageGameToEngine.cs:180 =167. Other caller 0x6206E6 (sweep).
- Filter PASS (0x61E6E0..0x61E742; strh 0x61E742; captures 0x61E74A/4C; push 0x61E764; lambda returns 1 at 0x628492).
- Iteration PASS: helper body 0x61EB78, copy 0x61EB94, +0x6C test 0x61EB98, +0x6D test 0x61EBF2, origin compare with [[robot+0x294]+0x10] 0x61EC22/24, empty sets pass, predicate loop 0x61ED52..60.
- Lambda 0x628406 PASS (0x62841A..20 id slot0; 0x628426 ts; 0x628428 ldrd family,type; 0x62843C ToPoseStruct3d; 0x628440 poseState; 0x62845C..62 isConnected; 0x628486 adds 0x34; 0x62848E slow path PLT 0x4B837C). Default -1 at +0x40 verified only for Charger ctor (0x4E9CC8, 0x4E9CD4 str.w r3,[r5,#0x144], base r5=obj+0x104; family 4 at 0x4E9CE0); other classes not checked.
- Message PASS: 0x72548C movs r1,#0x53; strh; Broadcast 0x61E7B4; ClearCurrent 0x61E7BA; CLAD :97=83.
- Wire PASS: Pack 0x714EE2..0x714F0A count via mul 0xC4EC4EC5 then strb.w [sp,#7], WriteBytes len 1; stride 0x34. CLAD LocatedObjectState.cs:111 Size=>50, Unpack 159-166; PoseStruct3d.cs:120 Size=>32, Unpack ends 176 originID; LocatedObjectStates.cs:74 ReadByte. LocatedObjectState::Pack 0x714A10 unread in .so.

## 5b BroadcastConnectedObjects 0x61E91C
- Entry PASS but WRONG NUMBER: 0x61E918 b.w 0x8CCA7C; word 0xFFBEB404 at 0x8CCA88; add pc at 0x8CCA84 (pc=0x8CCA8C) -> 0x4B7E90. The citation column says giving 0x4B7E8C; correct is 0x4B7E90 (the PLT named in the prose). CLAD :181 = 168. No other PLT caller.
- Filter/iteration PASS: FindConnectedObjectHelper at 0x61E9E2; body 0x61F078: copy 0x61F086, ldr.w fp,[r5,#0x30] 0x61F08A, end r5+0x34. Tail unread.
- Lambda 0x62868E PASS (0x628696, 0x6286A0 ldrd [r5,#0x34], 0x6286BC adds 0xC, slow path PLT 0x4B8394).
- Message PASS 0x725564 tag 0x54; 0x61EA00/08/0E; CLAD :98=84. ConnectedObjectState.cs:50 Size=>12, Unpack 89-91; ConnectedObjectStates.cs:74.

## 5c CheckMailbox 0x6B2AD4 - all rows PASS
Mutex this+0x35C 0x6B2ADC; size [+0x374] 0x6B2AE8; empty 0x6B2CB8/BC; block 26 (0x6B2B26/28) elem 0x9C (0x6B2B2C/2E); move-ctor 0x6BB40A called 0x6B2B20; header 5 words+byte 0x6B2B3A/40; 11 __move_assign at +0x18,24,30,3C,48,54,60,6C,78,84,90 (+0x84 list of pair string/Image, +0x90 pair string/ImageRGB by PLT demangle); write-back 0x6B2BD8..0x6B2C62; clears 0x6B2C68..A4; pop_front 0x6B2CAE; return 1. Consumer: 0x654306, 0x65430C, 0x654A14/1C, 0x654A74, 0x654A7A beq 0x654486. Producer UNKNOWN.

## Against current records (re-read from fidelity_manifest.json)
- M11-037 (title The BlockWorld frame sequence for observed markers, IMPLEMENTATION_GAP): evidence "Camera::AddOccluder with the transform z scale" contradicted. Unresolved items 1-2 (occluder points, gate fields) settled by this extraction; 0x621794 / 0x625704 still unread.
- M11-038 (title The BlockWorld game-broadcast entry points, IMPLEMENTATION_GAP): unresolved text (need M2/M10 layouts and the mailbox) covered for Located/Connected/CheckMailbox. Evidence still names BroadcastObjectObservation 0x61FED8: exists (Camera::ProjectObject 0x61FF04...) but unread; cannot be EXACT for it.
- Inventory V22/V23/C3.2 (M11-vision.md ~1068) repeat the z scale wording. Manifest and tree unchanged (git status clean).

## Wrong rows
1. 5b Entry citation: 0x4B7E8C should be 0x4B7E90.
2. 4b Ctor: exactly one handler (tag 50).
3. 5a lambda: -1 default of [obj+0x40] shown for Charger only.

## Stays open
0x6000000 meaning; container slot-0 and Update bodies, Prune/Notify bodies; RobotDelocalized emitter (0x510D88) and OnRobotDelocalized 0x510D62; veneer-only callers; FindLocated/ConnectedObjectHelper remaining checks (0x61ED94..0x61F078, 0x61F129..0x61F2A0); Pack bodies at 0x714A10/0x715370/0x7153A4; BroadcastObjectObservation body; mailbox producer (M11-040); names for [Robot+0x294] and [BlockWorld+0x94].

Verdict: PASS on all rows with three minor corrections; only record content contradicted is the z scale wording (M11-037 evidence, C3.2).
