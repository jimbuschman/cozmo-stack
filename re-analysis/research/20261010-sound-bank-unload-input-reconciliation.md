# Q14-109/134: concrete unload packet inputs

Checkpoint762; queue5 scope guard reread. Primary engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. Captures `20261010-sound-bank-unload-caller-native.txt`, `-table-native.txt`; immediate navigation `-input-callers.py/.json`. Reuses SL42–48 and UD261 rather than creating another unload family.

The two actual ARM calls to9B1F70 are9BA35C and9BB86C. The two Thumb encodings9B242E/9B2452 are inside actual ARM LDR9B242C/STR9B2450, not additional callers. Computed/alternate entry calls are not excluded by this navigation.

| Decision boundary | Exact input and result |
| --- | --- |
|Worker dispatcher9BA228 | Saves incoming managerR0 asR4 and packetR1 asIP; unsigned packetword0 selects the table. Kind1 ->9BA334. PacketC..2C copies nine words to outgoingSP0..20, packet0/4/8 suppliesR1/R2/R3, and managerR4 suppliesR0.9BA35C calls9B1F70;9BA360/364 returns its raw result. Known SL47 later ignores the dispatch result and disposes the copied owned name. |
|Immediate base dispatcher9BB65C | Frame delta98hex; incomingkind/key/thirdword preserved atSP8C/90/94, nine original stack words atSP98..B8. Nonnull incoming callbackSP9C first invokesA67E84(manager7C,cookieSP A0), without a result gate here. Kind1 ->9BB844 copies those nine words to outgoingSP0..20 and supplies the saved three words asR1/R2/R3.9BB86C calls9B1F70, then joins9BB74C: dispose owned name if byteSP98nonzero, return1. This acceptance is not the unload recipient result or proof of completion. |
|Unload recipient field mapping | Actual9B1F70 frame delta60hex. Commandkind=R1=packet0; primary key=R2=packet4; third wordR3=packet8 retained but not substituted for mode. Callback=incomingSP4=packet10; cookie=SP8=packet14; mode=SPC=packet18; selector=SP10=packet1C; auxiliary key=SP14=packet20. Registry lookup uses primary key and auxiliary key only when rawmode==2, otherwise secondkey0. No caller normalizes these fields. |
|Manager-specific retirement | Existing UD261C/E applies after eligibility: current managerlive24 selects ordinary9BB4A4 or derivedA3582C ->9BA624. Table103A9E0/103BF48 does not directly expose9B1F70. Ordinary retirement installs its own stack completion; derived preserves suppliedcallback/cookie. Registry unlink precedes retirement; failure callback shaping remains UD261B. |

This pays the actual packet-to-unload interface and distinguishes dispatcher acceptance from recipient/completion status. SL42–48 already establish startup products, immediate/threaded submission and worker copying. It does not establish every kind1 producer, all supplied rawmode/selector/auxiliary-key combinations, alternate manager/hook writers or encrypted-header input. Those specific input/writer questions remain UNKNOWN.109/134 stay PARTIAL;107 native+8 scope CHECKED/25 PARTIAL;140 AUDIBLE/0 INTERNAL unchanged.

out of scope: packet/node backing, owned-name storage release, registry hash/backing, semaphore implementation and media I/O; retain only exact argument, ownership, callback, result and ordering interfaces.
