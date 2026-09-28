# B1-voice residual pass - gate writers, 0xA57D64, callee bodies, pre-loop node class

Read-only extraction from `resources/lib/armeabi-v7a/libcozmoEngine.so` (3.4.0-1204, ARM mode; Wwise 2016.2 statically linked at 0x0095E540..0x00AE2E40). Citations are instructions in the `.so`. The Ghidra tree `re-analysis/decomp/libcozmoEngine/` exists in this clone and was used only to navigate; Ghidra function boundaries are wrong in several places (noted), so every citation below was checked against the raw instruction stream. GOT base is 0x104028C (0x9AF8C4+0x6909C8 and 0xA44D58+0x5FB52C agree); a slot at base+off holds a pointer to the named global.

## Headline

**B1-V24 is contradicted: the three gate bytes all have writers.** They are not independent globals. Gate1 `0x108DAF0`, gate2 `0x108DB08`, the device count `0x108DAFC`, the device list head `0x108DB04`, the countdown `0x108DB0C` and the flag `0x108DB18` are fields of one global output-device state struct based at **0x108DAE8**; gate3 `0x1052430` is a separate global written alongside gate2. The earlier "no writer" scan searched only for the absolute global addresses and their GOT-slot offsets, and therefore missed every access made through the struct base 0x108DAE8 (`strb rX,[base,#8]` = gate1, `[base,#0x20]` = gate2). Writers exist and are listed below.

Consequences: B1-V23/V24's "the clock-paced branch is dead, the engine always takes the device path, arg=1" is not established and is probably wrong. gate1 is written by the same call Perform makes at the top of every frame (`0x9D4778` -> `0x9EC38C` -> `0x9EBE6C`), so the branch Perform reads is dynamic. Whether it is 0 or 1 at runtime depends on the Wwise output-device object (phone audio), which is HARDWARE_ONLY; the code path itself is fully recovered.

---

## Rows

### 1. The three gate bytes' writers (B1 residual 1)

| step | what the original does | citation | existing record id or NEW | classification |
|---|---|---|---|---|
| G1 | **The output-device state is one struct at 0x108DAE8.** 0x9EBE6C builds `r5 = pc + 0x6A1C44` and `add r5,pc,r5` -> 0x108DAE8; field `+8` is gate1 0x108DAF0, `+0x14` is 0x108DAFC, `+0x20` is gate2 0x108DB08, `+0x24` is 0x108DB0C, `+0x30` is 0x108DB18, `+0x1C` is the device list 0x108DB04. | 0x9EBE8C `ldr r5,[pc,#0x4dc]` (lit 0x9EC370=0x006A1C44); 0x9EBE9C `add r5,pc,r5` -> 0x108DAE8; 0x9EBF2C `ldr r2,[r3,#0x14]`; 0x9EBF34 `ldrb r3,[r3,#8]`; 0x9EBF74 `ldrb r3,[r8,#0x20]` | M6-022 (new) | EXACT_SOURCE |
| G2 | **Gate1 0x108DAF0 is written by the device init/term and by the per-frame device advance.** `0x9EAF90` (device term) stores 0; `0x9EBA54` stores a boolean; `0x9EBE6C` stores `sl` and, in the no-device branch, 1. | 0x9EB090 `strb r5,[r4,#8]` (0x9EAF90); 0x9EBBA8 `strb r4,[r0,#8]` (0x9EBA54); 0x9EC00C `strb sl,[r3,#8]` and 0x9EC1B4 `strb r2,[r3,#8]` (0x9EBE6C) | B1-V24 contradicted; M6-022 (new) | EXACT_SOURCE |
| G3 | **Gate2 0x108DB08 is written by the output init and by the SetOutputDevice command.** `0x9EADE8` stores 0; `0x9EC418` stores its `param_1` byte (with special cases that store 0 or 1). | 0x9EAE18 `strb r5,[r6,#0x20]` (0x9EADE8); 0x9EC47C `strb r0,[r4,#0x20]` and 0x9EC4A0 `strb r1,[r4,#0x20]` (0x9EC418; r4 = 0x108DAE8, 0x9EC444 `add r4,pc,r4`) | B1-V24 contradicted; M6-022 (new) | EXACT_SOURCE |
| G4 | **Gate3 0x1052430 is written with gate2.** `0x9EADE8` stores 1; `0x9EBA54` stores its `param_1` byte; `0x9EC418` stores its `param_2` byte, or 1 in the `param_1==0` case. | 0x9EAE24 `strb r8,[r3]` (0x9EADE8); 0x9EBBAC `strb r8,[r3]` (0x9EBA54); 0x9EC488 `strb ip,[r3]` and 0x9EC4A8 `strb r2,[r3]` (0x9EC418; r3 = 0x1052430, 0x9EC480 `add r3,pc,r3`) | B1-V24 contradicted; M6-022 (new) | EXACT_SOURCE |
| G5 | **`0x9EC418(param_1,param_2,param_3)` is the Wwise SetOutputDevice command.** Returns 3 if the pair is unchanged; else clamps a countdown 0x108DB0C, calls 0x9B08F4, then sets gate2 = param_1 and gate3 = param_2, then 0x9EBA54/0x9EBE6C. | 0x9EC418..0x9EC583; call site 0x9AE3B0 `bl 0x9EC418` (inside the 0x9ADFD8 command drain, case 0x35) | M6-022 (new) | EXACT_SOURCE |
| G6 | **`0x9EADE8` is the output-device module init.** Sets gate2=0, 0x108DB0C=0, gate3=1, `sem_init(0x108DAEC)`, creates the audio thread (`pthread_create` of LAB_009e9378) at 0x108DB10. | 0x9EADE8..0x9EAF73; 0x9EAE18, 0x9EAE20, 0x9EAE24; 0x9EAE28 `bl 0x4D6784` (sem_init); 0x9EAE7C `bl 0x4A6934` (pthread_create) | M6-022 (new) | EXACT_SOURCE |
| G7 | **`0x9EAF90` is the output-device module term.** Empties the device list, sets 0x108DAFC=0, 0x108DAF4=1.0, 0x108DAF8=1.0, gate1=0, 0x108DAE8=1, posts/joins the thread, destroys the sem, 0x108DB18=0. | 0x9EAF90..0x9EB0E7; 0x9EB090 `strb r5,[r4,#8]`; 0x9EB0D8 `strb r3,[r4,#0x30]`; 0x9EB0A8 `bl 0x4D676C` (sem_post); 0x9EB0B4 `bl 0x4D673C` (pthread_join); 0x9EB0C8 `bl 0x4D6790` (sem_destroy) | M6-022 (new) | EXACT_SOURCE |
| G8 | **`0x9EBA54(param_1)` is the per-frame device advance.** Walks the device list; for a device with state +0x88 != 0 it creates a PBI/GetJSON object and resets +0x88; then gate3 = param_1, gate1 = (all devices idle); if param_1 != 0, sem_post. Called from Perform's device path. | 0x9EBA54..0x9EBC37; 0x9EBBA8, 0x9EBBAC; call site 0x9AFB80 `bl 0x9EBA54`; also 0x9EC320 `bl 0x9EBA54` | M6-022 (new) | EXACT_SOURCE |
| G9 | **The neighbouring globals.** 0x108DAFC is the device count (0x9EAF90 `DAT_0108dafc=0`; 0x9EBE6C reads it at 0x9EBF2C and tests it); 0x108DA9C is the render body's last-tick throttle (0xA44DAC); 0x108D870 is the Wwise manager pointer (tick at +0x4C). None of the three is a writer target of the gates. | 0xA44DAC `ldr r2,[r5,r2]` (slot 0x1040160->0x108DA9C); 0xA44DB0 `ldr r1,[r5,r3]` (0x10400D0->0x108D870); 0xA44DC0 `ldr r1,[r1,#0x4c]`; 0x9EBF2C | M6-022 (new) | EXACT_SOURCE |
| G10 | **Which branch Perform takes is dynamic, not static.** Perform calls 0x9D4778 -> 0x9EC38C (`mov r0,#0`) -> 0x9EBE6C, which writes gate1, and only then reads gate1 at 0x9AF900. So the "clock-paced" branch is not dead code. The runtime value of gate1 depends on the phone's Wwise output-device object. | 0x9AF8F4 `bl 0x9D4778`; 0x9D4778 `b 0x9EC38C`; 0x9EC38C `mov r0,#0`; 0x9EC390 `b 0x9EBE6C`; 0x9AF900 `ldrb r3,[r8]`; 0x9AF904 `cmp r3,#0` | B1-V23/V24 contradicted; M6-022 (new) | branch code EXACT_SOURCE; runtime value HARDWARE_ONLY |

**Item 1 residual: CLOSED (writers exist). B1-V24 must be rewritten.**

### 2. 0xA57D64 identity (B1 residual 3)

| step | what the original does | citation | record | classification |
|---|---|---|---|---|
| P1 | **0xA57D64 is an Android-JNI audio-route poll, not a Wwise pre-update.** It gates on `[0x108DA84] != 0` (a `JavaVM*`), `[0x108DF9C] != 0` (the app Context jobject) and `(manager[0x108D870]+0x4C & 0x3f) == 0` (once per 64 ticks). | 0xA57D64..0xA57DA8; 0xA57D70/0xA57D74 `ldr r6,[pc,r6]`/`ldr r7,[r6,#0x4c]`; 0xA57D88 `ldr r3,[r8,#0xc]`; 0xA57DA0 `ldr r3,[r3,#0x4c]`; 0xA57DA4 `ands r4,r3,#0x3f` | M6-022 (new) | EXACT_SOURCE |
| P2 | **vt+0x18 on the JavaVM = GetEnv** (`r2 = 0x10006`, JNI_VERSION_1_6); if it returns null it calls **vt+0x10 = AttachCurrentThread** with `JavaVMAttachArgs{version=0x10006, name="NativeThread", group=0}`. | 0xA57DC4 `ldr r3,[r7]`; 0xA57DCC `ldr r3,[r3,#0x18]`; 0xA57DD0 `blx r3`; 0xA57F90 `ldr r3,[lr,#0x10]`; 0xA57F94 `str sb,[sp,#4]` (0x10006); string 0xFA7AE8 "NativeThread"; 0xA57F50 `ldr r3,[r3,#0x14]` = DetachCurrentThread | M6-022 (new) | EXACT_SOURCE |
| P3 | **vt+0x18 on the JNIEnv = FindClass**: "android/app/NativeActivity" and "android/media/AudioManager". | 0xA57DF0 `ldr r3,[r2,#0x18]`; strings 0xFA79DC, 0xFA79F8 | M6-022 (new) | EXACT_SOURCE |
| P4 | **vt+0x29c = NewStringUTF** ("audio"); **vt+0x84 = GetMethodID** ("getSystemService", "(Ljava/lang/String;)Ljava/lang/Object;", then "isBluetoothA2dpOn"/"isBluetoothScoOn", "()Z"). | 0xA57E34 `ldr r3,[r3,#0x29c]`; 0xA57E60 `ldr ip,[ip,#0x84]`; strings 0xFA7A4C, 0xFA7A60, 0xFA7ABC, 0xFA7AD4, 0xFA7AD0 | M6-022 (new) | EXACT_SOURCE |
| P5 | **0x593E58 = a varargs JNI thunk to env->vt+0x8c = CallObjectMethodV**; it builds a va_list from r3 and calls it. Used here to call `getSystemService("audio")` on the Context. | 0x593E58: `sub sp,#4`; `str r3,[sp,#0xc]`; `add r3,sp,#0xc`; `str r3,[sp]`; `ldr ip,[r0]`; `ldr ip,[ip,#0x8c]`; `blx ip` | M6-022 (new) | EXACT_SOURCE |
| P6 | **0xA58008 = a varargs JNI thunk to env->vt+0x98 = CallBooleanMethodV**, used for isBluetoothA2dpOn/isBluetoothScoOn. The result is OR-ed and stored to 0x108DF98; on a change it calls 0x9EA66C. | 0xA58008..0xA5803C; 0xA58028 `ldr ip,[lr,#0x98]`; 0xA57F00..0xA57F38; 0xA57F38 `bl 0x9EA66C`; 0xA57F04 `strb r5,[r8,#8]` (0x108DF98) | M6-022 (new) | EXACT_SOURCE |
| P7 | **The object types.** `[0x108DA84]` is the `JavaVM*`; the `JNIEnv*` is obtained per call; `[0x108DF9C]` is the Android Context (`android/app/NativeActivity`); `[0x108DF98]` is the bluetooth-active byte; `[0x108DF90]`/`[0x108DF94]` are the cached native output sample rate / frames-per-buffer written by 0xA56E20; 0x9EA66C is the Wwise output-device notification on a route change. | as above; 0xA56E20 (sample-rate/property reader) writes 0x108DF90 (0xA56EA4 `str r3,[r6]`), 0x108DF94 (0xA591B8), 0x108DF98 (0xA56FDC/0xA57004) | M6-022 (new) | EXACT_SOURCE |

**Item 2 residual: CLOSED.** 0xA57D64 is the per-64-tick Android audio-route (bluetooth A2DP/SCO) poll; 0x593E58 is the JNI CallObjectMethodV thunk, not "Anki-side".

### 3. Four group members' Wwise class identities (B1 residual 4)

| step | what the original does | citation | record | classification |
|---|---|---|---|---|
| I1 | **Identity is UNKNOWN.** Attempted: (a) `.dynsym` scan for Wwise/CAk/Ak* names - 0 matches; (b) `.rodata` string scan for Wwise/SoundEngine/CAk/Ak* - only `N2AK15IAkSourcePluginE` at 0x00DCA100; (c) no RTTI typeinfo for these managers. Behaviour is read and unchanged from B1-V25..V28. | 0x9EADE8 init / no symbols; `re-analysis/symbols/classes_by_method_count.txt` has no Wwise class; `re-analysis/tools/strings.py` output only 0xDCA100 | M6-022 (new); B1-V25..V28 identity label stays UNKNOWN | UNKNOWN |

### 4. Callee bodies not read (B1 residual 5)

| step | what the original does | citation | record | classification |
|---|---|---|---|---|
| C1 | **0xA54F1C(voice,&params) is the per-voice parameter/state machine.** Loads `source=[voice+0xD4]`, `bus=[source+0xC]`, `id=[voice+0xF0]`; if `[bus+0x1F8] != -1` sets `params+0x2C=1`, and returns 0 when it is 0; computes a dB gain `(vol+vol2)*0.05` clamped at -37.0 (0xC2140000) and linearised with the 27866352.0/1.0653532e9 fast-pow; calls 0xA4BC58; per-connection state machine over `[voice+0xE0]`, `[voice+0xE4]`, `+0xCD` bits; on the 0x11 path sets `bus[0x31]=0x42CA0000` (101.0f) and calls 0xA4B4B0. | 0xA54F1C; 0xA54F20 `ldr r5,[r0,#0xd4]`; 0xA54F28 `ldr r8,[r0,#0xf0]`; 0xA54F34 `ldr r2,[r6,#0x1f8]`; constants 0x3D4CCCCD, 0xC2140000, 0x42CA0000; calls 0xA4BC58 at 0xA55058 and 0xA5572C, 0xA56650 at 0xA554F8, 0xA54A30 at 0xA555C0, 0xA4B4B0 at 0xA55644 | M6-022 (new) | EXACT_SOURCE (body read) |
| C2 | **0xA4C60C is a thunk, not the filter body.** `if ([voice]==0) return; r0 += 0x10; b 0xA766B8`. 0xA766B8 calls 0xA766F0 (LPF coefficients) and tail-calls 0xA77480 (HPF). So the "voice filter A" body is 0xA766B8/0xA766F0/0xA77480. | 0xA4C60C `ldr r3,[r1]`; 0xA4C614 `bxeq lr`; 0xA4C618 `add r0,r0,#0x10`; 0xA4C61C `b 0xA766B8`; 0xA766D4 `bl 0xA766F0`; 0xA766EC `b 0xA77480` | M6-011 (evidence gap, see F1) | EXACT_SOURCE |
| C3 | **0xA56E00 is a thunk, not the volume body.** `if ([voice]==0) return; r3=[[r0+8]+0x34]; if 0 return; b 0xA56A7C`. The body 0xA56A7C is the per-connection gain/ramp application (guard 0x108DF80/0x108DF88/0x108DF8C, 0x9E8114/0x9E8248/0x9E83FC, multiplies the source buffer by a float). | 0xA56E00 `ldr r3,[r1]`; 0xA56E0C `ldr r3,[r0,#8]`; 0xA56E10 `ldr r3,[r3,#0x34]`; 0xA56E1C `b 0xA56A7C`; 0xA56A7C..0xA56E1F (decomp) | M6-022 (new) | EXACT_SOURCE |
| C4 | **0xA03E8C(manager, list) notifies per-listener callbacks.** For each entry `[param_2+0x14]`, index into the manager hash by `id=[obj+0x140]`, find the bucket node at `+0x4C`, and if `[node+0x48]&4` and `[node+0x40]` set, call the callback with a 4-word context under the two mutexes; then 0xA69A38. | 0xA03E8C..0xA03FEF; 0xA03E8C `ldr r3,[r1,#0x14]`; 0xA03E98 `ldrh r2,[r1,#0x10]`; `pthread_mutex_lock`; `(*pcVar4)(4,&local_44)` | M6-022 (new) | EXACT_SOURCE |
| C5 | **0xA05574(map,key1,val,key2) is an insert/update into a small sorted-by-insertion map** with a mutex at +0x18 and a clock timestamp at +0x20; grows by 0x20-byte entries. | 0xA05574..0xA0576B; 0xA0557C `ldr r1,[r0,#4]`; 0xA05594 `add r1,r2,r1,lsl #5`; 0xA0559C; 0xA05694; 0xA0569C `bl 0x4D3658` (clock) | M6-022 (new) | EXACT_SOURCE |
| C6 | **0xA56650(obj) returns 1 if `[obj+0x10]&1` is already set, else calls `obj->vt+0x28` and sets the bit on success.** | 0xA56650 `ldrb ip,[r0,#0x10]`; 0xA56654 `tst ip,#1`; 0xA5665C `mov r0,#1`; 0xA56664 `ldr r3,[r0]`; `ldr r3,[r3,#0x28]` | M6-022 (new) | EXACT_SOURCE |
| C7 | **0xA4F9E0(bus,out,voice) mixes a child bus/voice into the bus.** If `[bus+0x1BC]==4` set 1; set `[bus+0x68]=0x2D`; zero-pad past valid frames; if `[bus+0x1A8]` and its `+0xC` exist call that object's `vt+0x28`; else call the mixer 0xA45E9C with `[param_3+0x40]/[+0x3C]` gains. | 0xA4F9E0; 0xA4F9E4 `cmp ip,#0`; 0xA4F9EC `ldr r3,[r0,#0x1bc]`; 0xA4FA0C `mov lr,#0x2d`; 0xA4FB44 `bl 0xA45E9C` | M6-022 / M6-012 | EXACT_SOURCE |
| C8 | **0x9E9E78(device,buffer) applies the device master gain and consumes the bus buffer.** Multiplies `buffer+0x10` by `[device+0x74]*0x108DAF4` and `buffer+0x14` by `[device+0x78]*0x108DAF8`; if `[device+0x7C]` calls 0xA1C9CC and copies the frame count; then calls `[device+0x70]->vt+0x24`. | 0x9E9E78; 0x9E9E7C `vldr s15,[r0,#0x74]`; 0x9E9E90 `vldr s13,[r0,#0x78]`; 0x9E9EA0 `vldr s12,[r3,#0xc]` (0x108DAF4); 0x9E9EA4 `vldr s11,[r3,#0x10]` (0x108DAF8); `ldr ... [..+0x70]` vt+0x24 | M6-022 (new) | EXACT_SOURCE |
| C9 | **0x9E9F08(device) releases the device frame.** If `[device+0x7C]`: zero the buffer when the frame count `[device+0x80]+0xE==0`, call 0xA69268, clear the count; then `[device+0x70]->vt+0x28`; `[device+0x74]=[device+0x78]`. | 0x9E9F08; 0x9E9F1C `ldr r3,[r4,#0x80]`; 0x9E9F24 `ldrh r1,[r3,#0xe]`; 0x9E9F28 `ldrh r2,[r3,#0xc]`; vt+0x28; 0x9E9F64 `ldr r3,[r4,#0x78]`; 0x9E9F68 `str r3,[r4,#0x74]` | M6-022 (new) | EXACT_SOURCE |
| C10 | **0xA55750(voice) is the ducking/stop pre-pass.** Reads the bus `[voice+0xD4]+0xC`; if `[bus+0xE8]&0x20` and `[bus+0xE9]&1` calls `vt+0x28`; if `[bus+0x1BE]&0x14` is 0 calls 0xA4B93C; else sets `voice+0xE4=2`, `voice+0xE0=1`, clears the 0x4C-stride array count, calls 0xA01BD8 and (if `[voice+0xCC]`) 0x9D4228. | 0xA55750; 0xA55750 `ldr r3,[r0,#0xd4]`; 0xA5575C `ldr r5,[r3,#0xc]`; 0xA55768 `ldrb r1,[r5,#0xe8]`; 0xA55778 `ldrb r3,[r5,#0xe9]`; 0xA55784 `ldrb r3,[r5,#0x1be]`; 0xA5587C `bl 0xA4B93C`; 0xA557EC `bl 0xA01BD8`; 0xA55844 `bl 0x9D4228` | M6-022 (new) | EXACT_SOURCE |
| C11 | **0xA4AF50(voice) computes the voice's output dB and applies ducking.** `fVar18=[voice+0x1C]`; device volume via 0x9E84C8; walks the connection list (`voice+0x28`), takes the max `[conn+0x60]` or `+0x64/+0x68` by `[conn+0x6C]&0xFB`, multiplies by the bus `+0x8C`; converts to dB with `20*log10` (0.4342945, 0.6931472); stores `voice+0x20`; updates `+0x1D0` (or `[voice+0x1C]+0x1D0`); no-connection path sets `voice+0x20 = 0xBA800000` (-0.0009765625f). | 0xA4AF50; 0xA4AF68 `vldr s16,[sb,#0x1c]`; 0xA4AF74 `ldr r0,[r3,#0x28]`; 0xA4B0? `fVar17=fVar17*fVar18*fVar5*[conn+0x8c]`; 0xA4B170 `vstr s16,[sb,#0x20]`; 0xA4B3B8 `str r3,[sb,#0x20]` (-0x3a800000); constants 0.33333334, 0.6931472, 0.4342945, 20.0 | M6-022 (new) | EXACT_SOURCE |
| C12 | **0x9D3CC0(ticks) advances the bus/source tick counters and retires finished ones.** Iterates the list at 0x108DA30; if `[obj+0x1B0]&0x20` and `[obj+0x1EC]==-1`, unlink from 0x108DA10 and destroy via 0x9D40C4; else if `[obj+0x1B0]&0x80==0` subtracts `round(ticks*[obj+0x158])` from `[obj+0x1CC]` when >=0. | 0x9D3CC0; 0x9D3CD0 `ldr r4,[r3,#0x24]` (0x108DA30); 0x9D3CE4..; 0x9D3D34 `ldr r3,[r2,#0x1cc]`; 0x9D3D38 `vldr s14,[r2,#0x158]`; 0x9D3D68 `strge r3,[r2,#0x1cc]` | M6-022 (new) | EXACT_SOURCE |
| C13 | **0xA58008's vt+0x98 is JNI CallBooleanMethodV**, not a Wwise vtable (see P6). | 0xA58008..0xA5803C; 0xA58028 `ldr ip,[lr,#0x98]` | M6-022 (new) | EXACT_SOURCE |

**Item 4 residual: CLOSED** for the listed bodies (all read; three are thunks to mislabeled bodies). **B1 residual 5 is closed.**

### 5. 0xA4BC58 vs M6-011 (B1 residual 6)

| step | what the original does | citation | record | classification |
|---|---|---|---|---|
| F1 | **0xA4BC58 is the per-connection gain/format update, not the voice filter.** It walks the voice's connection list (`voice+0x28`), manages each connection's `+0x20/+0x24` conversion buffers (memcpy, zero-fill), sets `[conn+0xc]=[voice+0x1C]*param_4`, keeps the minimum of `[conn+0x50..0x5c]` in the four output floats, calls 0xA5975C (or 0xA5B9D0+0xA5993C), and propagates `param_2+0xB8..0xC4` from `+0xA8..+0xB4`. The filter A biquad is 0xA766B8/0xA766F0/0xA77480 (M6-011's LPF/HPF citations). | 0xA4BC58; 0xA4BC68 `ldr r0,[r0,#0x28]`; 0xA4BD54 `ldrb r3,[r4,#0x6c]`; 0xA4BE70 `vstr s15,[fp,#0xc]`; 0xA4BEC8 min over `+0x50..+0x5c`; 0xA4BFC4 `[param_2+0xb8..0xc4]=[+0xa8..+0xb4]`; callers 0xA54F1C | M6-011 | **M6-011 evidence is partial:** it cites "per connection 0xA4BC58" but 0xA4BC58 is the connection gain/buffer step, and it does not name 0xA4C60C/0xA766B8 (the filter body). The filter claim rests on 0xA766F0/0xA77480, which are correct. |
| F2 | **The filter A call site.** 0xA44630 step (2) calls `0xA4C60C(source+0x1C0, voice)`, which thunks to 0xA766B8, which calls 0xA766F0 (LPF) then 0xA77480 (HPF). | 0xA446E0 `add r0,r7,#0x1c0`; 0xA446E8 `bl 0xA4C60C`; 0xA4C61C `b 0xA766B8`; 0xA766D4 `bl 0xA766F0`; 0xA766EC `b 0xA77480` | M6-011 / M6-022 | EXACT_SOURCE |

**Item 5 residual: CLOSED with an M6-011 evidence correction (see below).**

### 6. 0xA44D4C pre-loop node class (B1 residual 7)

| step | what the original does | citation | record | classification |
|---|---|---|---|---|
| N1 | **The pre-loop walks the global Wwise output-device list.** `r5=GOT base`; `r3=[slot 0x10400B8]=0x108DAFC`; `r4=[r3+8]=[0x108DB04]` = the device list head; next `[node+4]`. For each node `r0=[node+0x70]` (the device object), call `vt+0x2c`; if non-zero call `vt+0x30`. | 0xA44D50/0xA44D58 (base); 0xA44D5C `ldr r3,[r5,r3]`; 0xA44D60 `ldr r4,[r3,#8]`; 0xA44D70 `ldr r4,[r4,#4]`; 0xA44D7C `ldr r0,[r4,#0x70]`; 0xA44D88 `blx r3` (vt+0x2c); 0xA44DA0 `blx r3` (vt+0x30) | M6-022 (new); B1-V3 node meaning corrected | EXACT_SOURCE |
| N2 | **The node class is the Wwise output-device object** in the same list 0x9EBE6C iterates (`node+0x70` = device object, node+0x04 = next, node+0x10/+0x14 = type/id used by 0x9EBE6C and 0x9EBA54). Its Wwise class name is UNKNOWN (no RTTI/symbols; see I1). | 0x9EBE6C 0x9EBF64 `ldr r0,[r4,#0x70]`; 0x9EBFF4 `ldr r4,[r4,#4]`; 0x9EC0B0 `ldr r0,[r4,#0x10]`; 0x9EBA54 `[iVar4+0x10]/[+0x14]` | M6-022 (new) | node identity UNKNOWN; list/offsets EXACT_SOURCE |
| N3 | **Throttle.** `last=[0x108DA9C]`, `tick=[0x108D870]+0x4c`; if last==0 store tick; else if tick-last <= 8 skip; else store tick. | 0xA44DAC `ldr r2,[r5,r2]`; 0xA44DB0 `ldr r1,[r5,r3]`; 0xA44DB4 `ldr r3,[r2]`; 0xA44DB8 `ldr r1,[r1]`; 0xA44DC0 `ldr r1,[r1,#0x4c]`; 0xA44DC8 `rsb r3,r3,r1`; 0xA44DCC `cmp r3,#8`; 0xA44DD4 `str r1,[r2]` | M6-022 (new) | EXACT_SOURCE |

**Item 6 residual: CLOSED** (node = the Wwise output-device node in 0x108DB04; class name UNKNOWN).

### 7. 0x9A9B40's start and caller (B1 residual 8)

| step | what the original does | citation | record | classification |
|---|---|---|---|---|
| H1 | **0x9A9B40 is mid-function; the function starts at 0x9A9B38.** It is `push {r4,r5,r6,r7,r8,lr}; mov r6,r0`, then calls 0x9D4778 and reads the three gates (same logic as Perform) and returns a frame count. | 0x9A9B38 `push {r4,r5,r6,r7,r8,lr}`; 0x9A9B3C `mov r6,r0`; 0x9A9B40 `bl 0x9D4778`; 0x9A9B48/0x9A9B4C (gate1 slot 0xFFFFFE30) | M6-022 (new) | EXACT_SOURCE |
| H2 | **No caller exists in the shipped binary.** Scans: no direct ARM `bl`/`b` to 0x9A9B38 or 0x9A9B40 in `.text`; no 32-bit literal equal to either address anywhere in the file; Ghidra reports callers: (none). It is dead code in 3.4.0-1204. | raw 32-bit scan for 0x009A9B38/0x009A9B40 = 0 hits; ARM branch scan over 0x4D6860..0xAE3684 = 0 hits | M6-022 (new) | EXACT_SOURCE |

**Item 7 residual: CLOSED** (start 0x9A9B38; no caller; dead).

### 8. 0x9EBE6C (B1 residual 2)

| step | what the original does | citation | record | classification |
|---|---|---|---|---|
| D1 | **0x9EBE6C(param) renders/advances the Wwise output devices and returns the frame count.** It reads `uRam01062444` and the manager tick (`[0x108D870]+0x4c`), OR-s `param` bit0 when a sub-tick condition holds, then walks the device list 0x108DB04. | 0x9EBE6C; 0x9EBE84 `mov r0,#0x3e8`; 0x9EBE94 `bl __aeabi_idiv`; 0x9EBEA8 `ldr r0,[r7,#0x4c]`; 0x9EBEBC `ldrh r3,[r3,#0x3c]`; 0x9EBEC0 `cmp r1,r3`; 0x9EBEC8 `orrlo sb,r4,#1` | M6-022 (new) | EXACT_SOURCE |
| D2 | **It reads the output-device object** (`[node+0x70]`) and calls `vt+0x20` on it to obtain the per-device frame count; it takes the minimum into the return. | 0x9EBF64 `ldr r0,[r4,#0x70]`; 0x9EBFC0 `blx r3` (vt+0x20); 0x9EC104 `ldr r3,[r3,#0x20]`; 0x9EC15C `ldr r3,[fp,#-0x28]`/`cmp`/`movhs` min | M6-022 (new) | EXACT_SOURCE |
| D3 | **It writes gate1** (see G2) and reads gate2. | 0x9EC00C `strb sl,[r3,#8]`; 0x9EC1B4 `strb r2,[r3,#8]`; 0x9EBF74 `ldrb r3,[r8,#0x20]` | M6-022 (new) | EXACT_SOURCE |
| D4 | **The device object's actual `vt+0x20` (the audio sink frame count / pacing) is HARDWARE_ONLY.** Without the phone's audio device the value is not derivable from the shipped artifact; everything around it (list walk, gate writes, min reduction) is source. | 0x9EBF64..0x9EC064 | M6-022 / M6-018 | HARDWARE_ONLY |

**Item 8 residual: CLOSED as HARDWARE_ONLY** (reads the device/sink; confirmed).

---

## B1 residuals - status

1. Writers of the gate bytes: **CLOSED** - writers exist (G2/G3/G4); B1-V24 contradicted.
2. 0x9EBE6C internals: **CLOSED** (device render; gate writer; sink frame count HARDWARE_ONLY).
3. 0xA57D64 identity / 0x593E58: **CLOSED** (Android JNI audio-route poll; 0x593E58 = CallObjectMethodV thunk).
4. Four group members' class names: **STILL A GAP** - UNKNOWN (no RTTI/symbols); behaviour unchanged from B1-V25..V28.
5. Callee bodies: **CLOSED** - all listed bodies read; 0xA4C60C and 0xA56E00 are thunks to 0xA766B8 and 0xA56A7C.
6. 0xA4BC58 vs M6-011: **CLOSED** - 0xA4BC58 is the per-connection gain/buffer update; M6-011's filter claim rests on 0xA766F0/0xA77480, which are correct, but the record should name the filter body.
7. Pre-loop node class: **CLOSED** - Wwise output-device node in 0x108DB04; class name UNKNOWN.
8. 0x9A9B40 start/caller: **CLOSED** - start 0x9A9B38, no caller, dead.

## Existing records contradicted or too weak

- **B1-V24 (proposed M6-022) - contradicted.** "No writer to 0x108DAF0/0x108DB08/0x1052430" is false: gate1 0x108DAF0 is written at 0x9EB090, 0x9EBBA8, 0x9EC00C, 0x9EC1B4; gate2 0x108DB08 at 0x9EAE18, 0x9EC47C, 0x9EC4A0; gate3 0x1052430 at 0x9EAE24, 0x9EBBAC, 0x9EC488, 0x9EC4A8. The scan missed the struct base 0x108DAE8 (fields +8/+0x20).
- **B1-V23/V24 conclusion - contradicted.** "The clock-paced branch is dead; the engine always takes the device path; arg=1" is not established. Perform reads gate1 immediately after 0x9EBE6C writes it; the branch is dynamic. The bus-pass arg `(gate2==0)?1:gate3` is also dynamic because gate2/gate3 are written by SetOutputDevice.
- **M6-017 (amendment needed).** Its frame model and its "gate bytes" note inherit B1-V23/V24; the four "buses" group and the device-state struct/gate writers should move into the new voice/bus record.
- **M6-014's unresolved** "the bus-pass gating flag writer (D2.8, the arg of 0xA44C18) is RECOVERABLE_GAP" can now be closed with 0x9EC418/0x9EAE18/0x9EBA54.
- **M6-011 evidence is partial.** It cites "per connection 0xA4BC58" (the connection gain/buffer update) but does not name the filter body 0xA4C60C -> 0xA766B8 -> 0xA766F0/0xA77480. The biquad claim itself is supported by 0xA766F0/0xA77480.
- **M6-018 (mix rate)** still rests on 0xA56E20 (0x108DF90); unchanged, and the JNI vt+0x29c identity (NewStringUTF) is now confirmed.

## New record needed

- **M6-022 - Live voice and bus engine / output-device state.** (B1 proposed the id M6-021, but the manifest already uses M6-021 for the RNG seed seam.) It should own: the 0xA57FF8 wrapper and 0xA44D4C body; the 0x108DAE8 output-device state struct and the three gate bytes with their writers; 0x9EADE8/0x9EAF90 init/term; 0x9EBA54/0x9EBE6C device advance and the SetOutputDevice command 0x9EC418; the 0xA57D64 Android audio-route poll and its JNI thunks 0x593E58/0xA58008; the voice-pass/bus-pass per-voice bodies (0xA54F1C, 0xA4BC58, 0xA4C60C->0xA766B8, 0xA56E00->0xA56A7C, 0xA03E8C, 0xA05574, 0xA56650, 0xA4F9E0, 0x9E9E78, 0x9E9F08, 0xA55750, 0xA4AF50, 0x9D3CC0); the pre-loop device list 0x108DB04; and the four group members (behaviour only; class names UNKNOWN).

## Open questions for the manager

1. **Id collision:** the B1 report proposed M6-021 for the voice engine, but M6-021 is the RNG seed seam. Freeze the new record as M6-022 (or renumber).
2. **Gate semantics at runtime:** gate1/gate2/gate3 are dynamic. The manager must decide whether the new record models the output-device state (recommended: yes, it is source-backed) or defers the runtime values to HARDWARE_ONLY. A capture of the original phone's Wwise device state is not available; the code path is.
3. **M6-017 amendment:** move the four "buses" group and the gate/device-state rows into M6-022 and correct M6-017's frame-model text.
4. **M6-011 evidence:** decide whether to add 0xA4C60C/0xA766B8 to M6-011's evidence or to M6-022's, so the filter's production path is owned by one record.
5. **M6-014 residual:** close the "bus-pass gating flag writer" gap with the 0x9EC418/0x9EAE18/0x9EBA54 citations.

## Method notes (for reproduction)

- Gate writers found with a function-scoped ARM scanner over the Ghidra index functions in 0x95E540..0xAE2E40: track `ldr rY,[pc,#imm]` literal values and `add rX,pc,rY` to recover a pc-relative base, then report `strb`/`str` to `base+off` for the target globals. This is why the earlier address-only scan missed them.
- No-writer evidence for other globals was not re-derived; only the three gate bytes and their struct were re-checked.
