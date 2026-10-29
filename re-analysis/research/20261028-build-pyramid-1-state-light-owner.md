# BUILD-PYRAMID-1 B2 — inherited Base state-light class owner

Same explicitly requested reader gpt-6.1-sol/medium; no backend inference. Narrow read-only follow-up: actual Base call, shared forwarder's operand, constructor config source and manager retained class only. `known.py` preceded SetBehaviorStateLights, IBehavior ctor, ExtractBehaviorClassFromConfig and BehaviorClassFromString. Same verified SO SHA256 as original report. Frozen `read.md` SHA2568B04FC1A597DCC0B1815A4D2E2E7D6A61CD56434D3E88A8362715ED1395D81E9 and prior addenda unchanged. No code, official metadata, .NET, git or hardware changes.

**Confirmed: the inherited Base pickup/placement state's light owner is the actual object's configured BehaviorClass byte, not hardcoded BuildPyramidBase.** Thus derived BuildPyramid sends class2; standalone BuildPyramidBase sends class3. The inheritance of the transition method does not change the stored class. This matters because manager retains class+84 and existing M7-030 cleanup compares the incoming class to it.

| Step | Defined native dataflow | Primary citation |
|---|---|---|
| Configuration | Derived asset contains behaviorClass BuildPyramid, Base asset contains BuildPyramidBase. | `re-analysis/obb/assets/cozmo_resources/config/engine/behaviorSystem/behaviors/freeplay/buildPyramid/buildPyramid.json:2`; sibling `buildPyramidBase.json:2` |
| Constructor chain | Derived calls Base with original r1Robot/r2Json untouched; Base calls IBehavior with same r0this/r1Robot/r2Json. No Baseclass constant is passed or installed. | Derived `0x005DBC88..BC8E`; Base `0x005DCA40..CA44`; signatures confirm `(Robot&, Json::Value const&)` |
| Class source | IBehavior saves incoming Jsonr2 tofp. Calls ExtractBehaviorClassFromConfig(fp), stores returned byte at this+64. | `0x005BBB88`; `0x005BBC90..BC96` (`mov r0,fp; blx Extract; strb.w r0,[r4,#64]`) |
| Json key | Extract reads literal `behaviorClass` through Json::Value::operator[], obtains string, calls BehaviorClassFromString and returns its result. | `0x005BBA74..BA88`; key pointer PCresolved0x00BF2A86=`behaviorClass`; map call `0x005BBAAE..BAB4`, returnBAC4 |
| Actual enum values | BuildPyramid string is the string object at localsp+20 with valuebyte2 atsp+2C; BuildPyramidBase atsp+30 with valuebyte3 atsp+3C. Successful lookup returns mapped byte. Do not associate a value store with the following string. | BuildPyramid pointer literal0x00C198C5 resolved at`0x0076A362`; string constructionA36E..372; value2 `0x0076A37E/388`; Base pointer0x00C19E56 resolvedA382, constructionA38C..390, value3A39E/A3A8; map find/return `0x0076AD54..AD66` |
| Base pickup producer | Uses actual thisr5, constructs light entry for stored base ID+128, trigger0x20 and modifierzero; passes vector inr1, boolfalse inr2 to IBehavior::SetBehaviorStateLights. It passes no explicit BehaviorClass constant. | `0x005DCBE2..CBFE`, `0x005DCC0C..CC12` (`mov r0,r5; movs r2,#0; blx0x004B35AC`) |
| Base placement clear producer | Uses same shared IBehavior forwarder with an empty vector andfalse. Consequently its clear call also carries actual this+64 class. | Frozen Base P1 `0x005DCDCC..CE08`; shared forwarder below owns class selection for both call sites |
| Forwarder | Receives actualthisr0. Loads Robot fromthis+2C; preserves flag inip; moves vectorr1 tor2; loads **byte[this+64] intor1**; obtains manager[Robot+44]; restoresflag tor3; tailcalls manager setter. | `0x005BEE38..EE46`: `ldrb.w r1,[r0,#64]` atEE3E. ARM veneer0x008CBF7C+4/literal+12 resolvesPLT0x004B29DC, typed managerSetBehaviorStateLights(BehaviorClass,vectorconst&,bool) |
| Manager retention | Saves incomingclassr1 inr4 then localsp+C; its final class store writes that exact value to manager+84 after vector diff/assignment. No class substitution in this bridge. | `0x005A432A`; `0x005A4374`; `0x005A45D2..D6` |

This establishes the requested source operand, including the constructor-to-shared-forwarder join. It does not recertify the complete common IBehavior lifecycle or redo the settled manager's light-diff algorithm. Current whole M7-030 is quoted below before relying on the shared setter/cleanup ownership; its explicit other-producer boundary is respected. Base trigger0x20 producer was already frozen; this follow-up establishes its dynamically supplied class operand for derived and standalone instances.

Scratch primary dumps: `rawdumps/light-class-forward.txt`, `behavior-ctor.txt`, `extract-class.txt`, `class-from-string.txt`, `class-from-string-result.txt`, `light-manager-entry.txt`, `light-manager-class-store.txt`; nativehelper scripts retain string and veneer resolution. No remaining UNKNOWN for B2's class operand. No new policy/approval departure proposed.

## Current M7-030 whole record

```json
{
  "id": "M7-030",
  "subsystem": "M7-behaviour",
  "title": "BehaviorManager::SetBehaviorStateLights: ordered diff against the previous lights, stop/replacement/new play on cube-light layer 1, retained vector and class, and the switch cleanup after Init",
  "location": "cozmo-stack/src/Cozmo.Robot/Behavior/BehaviorStateLights.cs",
  "status": "EXACT_SOURCE",
  "authority": "libcozmoEngine.so 3.4.0-1204",
  "evidence": [
    "SetBehaviorStateLights 0x005A4320..0x005A45F2; IBehavior wrapper 0x005BEE38..0x005BEE46; manager ctor 0x005A0918..0x005A092A.",
    "Lower calls: StopAndPlay 0x006396AC..0x00639722; PlayLightAnim overload 0x006389FC forwards layer 1 (the trailing integer 0 is not the layer).",
    "Switch cleanup MS4 0x005A2114..0x005A226E inside SetRunningAndResumeInfo after Init.",
    "re-analysis/research/20261028-hiking-roll-cube-1-state-lights-read.md, -state-lights-boundary.md, -lower-light-bindings.md with their independent checks; re-analysis/research/20261028-hiking-roll-cube-1-claude-check.md"
  ],
  "effect": "which cube lights a running behaviour shows, when they are replaced or stopped, and whether they are cleared when behaviours switch",
  "provenance": "HIKING-ROLL-CUBE-1 (Codex read and build, 2026-10-28), checked by Claude 2026-10-28; record added with operator approval 2026-10-28 (HANDOFF-HIKING-ROLL-CUBE-1.md).",
  "unresolved": "Settled EXACT_SOURCE by Claude 2026-10-28 (SPARKS-ROLL-BLOCK-1; re-analysis/research/20261028-sparks-roll-block-1-claude-check.md): setter 0x005A4320..0x005A45F2 stores the flag +0x85 first, matches each old entry to the first new entry with the same object ID (none: StopLightAnimAndResumePrevious 0x005A43C2; found: StopAndPlayLightAnim with the new entry's modifier 0x005A441A, ID inserted whatever the result), plays the remaining new entries (PlayLightAnim 0x005A4556), logs failed plays, assigns the vector when distinct and stores the class +0x84 last; StopAndPlay 0x006396AC..0x00639722 is StopLightAnim(old, layer 1, id, false) then PlayLightAnim; MS4 0x005A2114..0x005A226E clears (stop-and-resume each, vector emptied, class/flag kept) only when the incoming class differs from +0x84 and either the behaviour is neither the old current nor the old resume, or the old trigger is None, the new one is not, and the flag is false. Matches BehaviorStateLights.cs and Lights.cs. Built and checked in HIKING-ROLL-CUBE-1 but not settled. The ApplyAnimModifier body and the cube-light engine stay M4-046/M8-011/M8-012. Producers other than Roll (trigger 25 InteractingBehaviorLock) are not checked here.",
  "hardware_required": false,
  "live_path": true,
  "test": "BehaviorStateLightsNativeTests"
}
```
