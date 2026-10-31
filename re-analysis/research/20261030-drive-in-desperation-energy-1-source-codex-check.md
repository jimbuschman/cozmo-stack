# DRIVE-IN-DESPERATION-ENERGY-1 — finite own-contract source review

Verdict: PASS for the finite behavior-owned contract in reader A v2, subject to the explicit shared gaps below. This is not an end-to-end shared-system closure or acceptance. Reader v2 SHA256 `5a05bd530b926c81fb8c6c6da309c36e74f4a692d23d713524ad1a195280f6d3`; binary SHA256 `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`; Energy JSON SHA256 `20917c9b09e4ea5e9cf4137ed068fe80da1a3c6c0a9ef8f3d45cb1a03b2fb704`; activity JSON SHA256 `1d6d8e59cd49bcf5da4897397334842887e13fb8d81d7b57c0b94e3a081e4186`. All 22 UTF-8-sig manifest snapshot dictionaries match the current manifest field-for-field. No approved shell-row contradiction found. `known.py` failed missing Ghidra index, then disassembly and assets were examined. Original bounded report preserved as `sourcecheck-bounded-v1.md`.

PASS means independently checked the cited call/branch/tuple or narrow join, not certification of the called shared owner. Each row identifier corresponds to reader v2:

| Rows | Verdict | Independent evidence and limits |
|---|---|---|
| E1 | PASS | 0x59ca9a..cab8 allocates 0x140, calls class ctor and shared owner, then common tail. |
| E2 | PASS | Shipped activity JSON; 0x5b4718, 0x5b4768..47b0, 0x6141a0..b4 check strategy forwarding and higher-priority branch. Expressed-state callee remains shared MISSING. |
| E3 | PASS | 0x60b23e..b270 selects first running or IsRunnable==1 in vector order. |
| E4 | PASS | Vtable addresspoint 0x10288d4: cells 0x10288f4/f8/fc resolve 0x5d9f46/0x59ec12/0x5d9f4a = 1/0/0; own runnable 0x5d90ac returns 1. |
| E5 | PASS (join) | 0x5bd778..d968 base gate dispatch; shared base M8-001 remains open, not claimed complete. |
| E6 | PASS | Params ctor 0x5d9f50..a0b8 parses trigger/profile/bool/floats and stores +0/+4/+30/+34/+38. |
| E7/E8 | PASS | 0x5d8afa..8b26 stores state, rounds, -1f cooldown, -1 target, zero latch; 0x5d8b4c..8c36 conditionally creates family2 predicate then calls RNG. 0x5d8d34..4a uses RandIntInRange(1,3); next call only on NoCubes exhaustion 0x5d9f34. Active and passive ObservableObject +C relocations 0x101cdf8/0x101d580 lead to true/false stubs; no radio-connect requirement inferred. |
| I1 | PASS | 0x5d8eca..8eee profile, target=-1, Idle, return 0; no round/cooldown/latch reset. |
| I2/I3/I4 | PASS | 0x5d8f12..8fe4 state, double range from f32 params, narrow f32, Wait and bound callback; Wait 0x54ca1c..60 uses f32 now+duration, strict now>deadline; 0x5d9140 branch chooses cube flag. |
| C1/C2/C3 | PASS | 0x5d9d5c..9e80: negative deadline bypass, otherwise now>=deadline; unordered goes NoCubes. Draw double 15..30, f64 add and f32 store before query; exact1 distance-result and strict greater choose farthest/first tie, empty or invalid falls back after stamp. Shared filter/query ordering/root semantics remain separate. |
| N1 | PASS | 0x5d9f14..42 positive count decrements then drive; else redraw then request. Subsequent actionless termination is governed by shared Update. |
| D1/D2/D3/D4 | PASS | 0x5d91c2..920e single DriveToPose(true,false,{10,10,10},0x3e32b8c2,4,1); 0x5d92e2..9338 angle 40..100, sign, distance 50..150; 0x5d934c..93fe full current rotation * Rz(currentYaw+delta), then TranslateForward; 0x5da2de searches only if useCubes. Rotation3d operator 0x84ac5c copies lhs, quaternion *= 0x849ef4 multiplies f64 and normalizes; TranslateForward 0x84b92a rotates f32 point then f32 adds. Do not flatten. Shared RNG/math/drive remain open. |
| S1/S2 | PASS (caller) | 0x5d96dc..9744 sends (-1,1,1) and success DriveToCube. Wrapper 0x5d97a0..ee passes empty failure; helper ctor 0x5bacec..cfa copies tuple. Sentinel observation 0x5bb81c..2c checks count, does not insert; helper mode remains MISSING. |
| B1/B2 | PASS | 0x5d98be..9910 and 0x5d9a1c..a4a: ID=-1 queries most recently observed; none warns and Idle; located lookup and dynamic cast failure clear ID then Idle. |
| B3/B4 | PASS | 0x5d9912..996c builds object,type4,false,0,0x3e860a92,false,word0; nonzero result or output byte +1c -> LookAtCube. Success copies embedded +8 poses in input order stride 0x1c, including empty vector, 0x5d996e..99bc; 0x5d99c4..9a10 DriveToPose(false,false,{30,30,30},0x3e860a92,4,1), bound Look callback. Shared PAP producer and drive behavior remain separate. |
| L1/L2 | PASS | 0x5d9bd2..9c62: ID=-1 verify/Idle else TurnTowardsObject(PI,true,false); callback 0x5da51e..28 continues only result0. |
| Q1/Q2 | PASS | 0x5d95be..9642 animation(trigger,1,true,0,60,false), wrapper(true,false,PI,false), result-ignoring callback; 0x5da354..3ce ID guard, TurnTowardsObject(PI,false,false) empty callback then clear ID. |
| U1/U2 | PASS | 0x5d9062..90a8 treads latch, StopActing(false,false) on first edge, recovery clears ID/latch then Idle then base. 0x5bd074..8a and 0x5bda56..62 establish shared actionless status. |
| T1 | PASS | 0x5d9060 bx lr; Resume slot 0x1028920 ARM_ABS32 targets IBehavior::ResumeInternal; Init and own Stop preserve round/cooldown/latch. Shared Stop/profile teardown remains its own record. |
| T2 | PASS (own body boundary) | 0x5d8e24 destructor and class methods/callbacks scanned: own teardown frees filter/params then base; no direct own robot send, objective/needs/mood update or own E2G report found. This says nothing about generic base, child, manager or action events. |

Shared admission/picker and expressed-state, the native family/filter iteration and pose roots, sentinel SearchForBlockHelper (-1,1,1), PAP producer, native drive/action/completion and typed reports remain under their current records and visible MISSING where unavailable. Do not fake observed IDs, successes, callback results, traffic or readiness. No record is settled by this check. No .NET, hardware, tracked edits or git actions performed.
