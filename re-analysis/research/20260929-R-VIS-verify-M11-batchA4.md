# Verify M11 round 3 (read-only). Verdict FAIL. See the handback message for the numbered objections.
Key binary facts recovered (libcozmoEngine.so):
- AnyRemainingLocalizableObjects() 0x006270F4 = thunk -> AnyRemainingLocalizableObjects(PoseOriginList::UnknownOriginID) 0x00626EF4. Filter+0x6D (originMode) = 3 (0x00626FAC strb; Custom, allowed-origin set left empty because arg == UnknownOriginID at 0x00626FB0 -> every origin), ONE predicate = std::function whose operator() (0x0062C47E: ldr r0,[r1]; ldr r1,[r0]; ldr r1,[r1,#0x18]; bx r1) calls obj vtable+0x18 (the CanBeUsedForLocalization slot CouldUseObjectForLocalization uses at 0x0050D17C), FindLocatedObjectHelper(filter, EMPTY modify fn, returnFirst=1 at 0x00626FD2). C# uses default filter = InRobotFrame only.
- HistRobotState::Interpolate 0x0053068D: blend recovered (see handback).
- GetRawStateAt 0x00531431: t below first key fails (0x0053146A..0x00531470), origin mismatch / GetWithRespectTo != 1 returns 0x6000000.
- CullToWindowSize guards: size<2 (0x005309DC) and newest<window (0x00530A08..0x00530A0A) return.
- Robot::UpdateFullRobotState: +0x29==0 drop (0x00512940) precedes OR; OR 0x00512B8E; Delocalize only when r7 != 0 (0x00512B88..0x00512BA6), r7 = CheckAndUpdateTreadsState==1 && (old +0x355 == 0 || new +0x355 == 0) (0x00512A62..0x00512A96).
