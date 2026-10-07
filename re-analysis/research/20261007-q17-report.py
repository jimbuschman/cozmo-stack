"""Assemble a research checkpoint without changing fidelity records."""
from pathlib import Path
import json
here=Path(__file__).resolve().parent
records=json.loads((here/'20261007-q17-current-records.json').read_text(encoding='utf-8'))
scope=[r for r in records if r['status']!='EXACT_SOURCE']
body='| Record / requested coverage | Status | Checkpoint |\n|---|---|---|\n'
progress={'M7-023':('CHECKED','MD1-MD3: full destructor, including nine reverse-order histories.'), 'M8-011':('PARTIAL','Common helper stack/Smart methods reopened; concrete/caller closure in progress.'), 'M8-001':('PARTIAL','RB1-RB5 eligibility gates; remaining Opus overrides pending.'), 'M8-013':('PARTIAL','SC1-SC3 executable-type traversal and process requests; other findings pending.'), 'M8-012':('PARTIAL','UI1-UI3 checked; remaining tick and channel obligations pending.'), 'M8-016':('PARTIAL','DT1-DT4 local order; recipient/writer census pending.')}
progress.update({
 'M7-005':('PARTIAL','PL1-PL9 close engine input, clock and package caller scan; runtime198 producer UNKNOWN; blink is M5.'),
 'M7-007':('PARTIAL','PL1-PL9 close engine input and package caller scan; runtime198 producer UNKNOWN; dart is M5.'),
 'M7-008':('PARTIAL','PL1-PL9 close engine live selection/time and package callers; runtime198 producer UNKNOWN; streamer error flag M5 boundary.'),
 'M7-009':('PARTIAL','PL1-PL9 close input/clock and package caller scan; runtime198 producer UNKNOWN; keyframes M5.'),
 'M7-010':('PARTIAL','PL1-PL9 close input/clock and package caller scan; runtime198 producer UNKNOWN; composition M5.'),
 'M7-012':('CHECKED','EM6/GC4 check action-event consumer and game sink; M13 owns callback production.'),
 'M7-014':('CHECKED','LK1-LK7,32 static tables, SS7, IL4 and DS22 check masks/install/remove; nonstatic M10/M13 suppliers named.'),
 'M7-015':('CHECKED','RC1/RC2 check reaction consumer, identify M10 state/trigger suppliers already covered by Q16.'),
 'M7-019':('CHECKED','RC3 / RB6-RB8 / NI check local objective, finished-message sink and reaction child inputs; M10/M13/M14/M5/M9 suppliers/recipients explicitly split.'),
 'M7-020':('PARTIAL','AM1-AM6 recover native numeric map and forward-map fallbacks; watcher name and action-union variants are M13 boundaries.'),
 'M7-021':('CHECKED','RC4,RC8,RC9 check setter/reader,175 calls and startup skip60/decrement; concrete state decisions and Viz/SDK bodies named boundaries.'),
 'M7-013':('CHECKED','EM1-EM7 / PL4 close NaN, floor, decay/history arithmetic and nine default graphs.'),
 'M7-016':('PARTIAL','PL1-PL9 check live/distortion consumers and package scan; runtime198 producer UNKNOWN; M5 face execution boundary.'),
 'M7-017':('PARTIAL','PL1-PL9 check handoff, timer, config/graph and195-input serialized scan; runtime198 producer UNKNOWN; StartupManager tree limitation explicit.'),
 'M7-003':('CHECKED','IM1 / RB1-RB5 / RV concrete gates close both Opus obligations.'),
 'M7-002':('CHECKED','RM1-RM5 close failure event, full JSON handoff and ordered mapping ownership; strategy parameters are M10 Q16 rows.'),
 'M8-002':('CHECKED','ES4 / PN1 / PN3 / GP1-GP5 check Stop, warning, NaN gate, config failure and default-node order.'),
 'M8-003':('CHECKED','ES1-ES9 / PN2 / GP1-GP5 check score overrides, warnings and emotion scorer construction; history values are named M7 boundaries.'),
 'M8-004':('CHECKED','PN4 / CS / SX close source chooser; test-only ChooseAndSwitch has no native counterpart.'),
 'M8-005':('CHECKED','PA1-PA4 close sequence, signed loop counter, result-independent adapter and pointer-ordered listeners; action resolution/results M5/M13.'),
 'M8-007':('PARTIAL','TR1-TR3 check native behavior queue vs unnamed clip-scope lock; runner/replaced-tag results are M13/M5 boundaries, not validated here.'),
 'M7-018':('CHECKED','BC1-BC6 and79 decoded TBH rows check owner, list handler, dispatch and unique factory insertion/return.'),
 'M7-022':('CHECKED','DS1-DS23/RC7 check all seven states, callbacks, RNG order/ranges, action inputs, initializer, Init/Stop and statistics.'),
 'M8-006':('CHECKED','NI1/NI2 and WS1-WS12 check eight strategy constructors/gates; needs and sensor writers are named M10 boundaries.'),
 'M8-008':('CHECKED','RC5/RC6 check four calibration consumers and result-independent adapter; timeout/result runner is named M13 boundary.'),
 'M8-010':('CHECKED','Quoted bookkeeping policy has no engine behavior claim; generated factory is M7-018.'),
 'M8-009':('CHECKED','IL4 / SS settle category/key order and plain track-lock names.'),
 'M8-011':('CHECKED','HS/SD/DV/PK/PB/PR/RL/SH/HC/HF/SS/TK: six concrete helpers, callbacks, factories, lifetime, scopes and tick; named M10/M11/M13/M15 boundaries.')
 ,'M8-015':('CHECKED','OB1 identifies ActionList owner and proposes M13-ACTION-TEARDOWN; upper action destructors excluded by layer boundary.')
 ,'M8-017':('CHECKED','OB2 identifies runner/compound ownership and proposes M13-SLEEP-COMPOUND; upper runtime execution excluded.')
 ,'M8-016':('CHECKED','DT1-DT4 / AT1-AT5 check base order and concrete member seams, proposing M15-ACTIVITY-MEMBER-TEARDOWN; child bodies excluded.')
 ,'M8-014':('PARTIAL','WB1-WB8 check binding, timestamp, removal/render consumers; pose/lookup/Viz recipients remain explicitly M11/M12/M15 boundaries.')
 ,'M8-001':('CHECKED','RB/RV/IL/MR/MS/PA close lifecycle, base/concrete gates, manager state messages/flags/light cancellation and listener/helper seams.')
 ,'M8-012':('CHECKED','UI/MT/MS/MR/TK close tick, UI, resume and action-list interface calls; concrete activity/action execution stays M15/M13.')
 ,'M8-013':('CHECKED','SC/CS/SX/GC close chooser handlers/lifetimes, executable traversal, scoring/RNG and process requests; AI arbitration M10 and wire underflow M2.')
})
for r in scope:
 status,note=progress.get(r['id'],('NOT DONE','Current record quoted; instruction extraction in progress.'))
 body+=f"| {r['id']} | {status} | {note} |\n"
body+='''
# Q17: remaining M7/M8 behavior rows

Answers Q17 of
`requests/20261007-codex-queue-5.md`, reordered by the operator after Q16.
Research only; operator authorizes committing and pushing each completed answer.
No production, inventory, manifest or approval changes. No hardware runs.

The scope includes records omitted by R-BEH2 and the open findings in
`20261003-R-BEH2-verify-1-2.md` and `20261003-R-BEH2-verify-3.md`.
Current non-EXACT M7/M8 records are quoted below to prevent stale-record findings.
M8-010 is a policy inventory item: inspect its actual claim separately rather than
inventing an engine classifier merely because its status is not EXACT_SOURCE.

Primary engine: `resources/lib/armeabi-v7a/libcozmoEngine.so`, SHA256
`02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`.
Native companions `20261007-q17-helpers-native.txt`,
`20261007-q17-remaining-native.txt`, `20261007-q17-ui-native.txt` and
`20261007-q17-fragments-native.txt` reopen shipped instructions. Decompilation
supplies navigation only. Fragmented bodies and interrupted linear disassembly
must be reopened at branch targets before claiming complete coverage.

## Current records, before comparisons

'''
for r in scope:body+='### '+r['id']+'\n\n```json\n'+json.dumps(r,ensure_ascii=False,indent=2)+'\n```\n\n'
body+='''## New instruction-checked rows

| Step | Address | Behavior | Gates | Order | Failure / result | Float bits / UNKNOWN | C# host |
|---|---|---|---|---|---|---|---|
| H-L1 | 0x005B6850..0x005B68EA | Choose stop event: status0 failure; status1 and supplied bool true cancel, false inactive_stop; status2 success. Event names are robot.behavior_helper.failure / cancel / inactive_stop / success. | Status1C; cancel bool only for status1 | Choose event before duration helper | Other status returns without event | No fixed floats | Behavior/IHelper.cs LogStopEvent |
| H-L2 | 0x005B698C..0x005B69B8 | Read timer binary32, subtract stored start98 in binary32, VCVT.S32.F32 truncates to signed integer seconds. | Stop event chosen | Subtraction before signed conversion | Signed integer <0 enters H-L3 | Both clock operands binary32; no double subtraction | Behavior/IHelper.cs LogStopEvent |
| H-L3 | 0x005B69BA..0x005B6A2A | Error IHelper.Stop.InvalidTime includes name, signed duration, exact widening of binary32 start and stop; set global error flag, optionally debug-break; then replace duration with zero. | Negative converted duration | Diagnostics before zero replacement and event | Continues with duration0 after optional debug-break | No epsilon/clamp before integer conversion | Behavior/IHelper.cs LogStopEvent |
| H-L4 | 0x005B6A2C..0x005B6ACC | Decimal integer duration is the $data field; event value is helper name. Destroy temporary attributes/string after synchronous sEventF. | H-L1 selected event | Duration string, attributes, event, destruction | No status mutation | No float formatting on payload | Behavior/IHelper.cs LogStopEvent; generic DAS dispatch is M1 boundary |
| RB1 | 0x005BD780..0x005BD7D2 | Already-running A1 emits IBehavior.IsRunnableBase debug and returns true immediately. | A1 !=0 | Skips all remaining eligibility gates | true | No floats | Behavior/IBehavior.cs IsRunnableBase |
| RB2 | 0x005BD7D4..0x005BD802 | Require process1C==0 or AI analyzer process running; severe-need field74==3 or equals SevereNeedsComponent14. | Not already running | Process gate then severe-need gate then timer read | Missing required process logs IBehavior.IsRunnable.RequiredProcessNotFound, error/debug-break, false; severe mismatch false | Timer returns binary32 | Behavior/IBehavior.cs; AI analyzer / needs values are M10 interfaces |
| RB3 | 0x005BD802..0x005BD862; 0x005BD936..0x005BD95E | Require spark70==55 or IsUnlocked(spark,true)==1. Recent-interaction gate skips when78 < negative epsilon; otherwise require whiteboard44 >= negative epsilon and f32(f32(78+whiteboard44)+epsilon) >= now. Max-time7C comparison widens to binary64 negative threshold; if not below it, require f32(7C+epsilon) >= f32(now-manager50). | Preceding gates | Unlock, interaction window, time-since-switch | False at failed comparison; preserve native BGE/BLT, including unordered | f32 epsilon3727C5AC, negativeB727C5AC; binary64 threshold read at005BD998 (full bits in companion) | Behavior/IBehavior.cs IsRunnableBase |
| RB4 | 0x005BD864..0x005BD89C | If off-treads355 !=0 require virtual20==1; if charging34A !=0 require virtual24==1; if carried-id at[robot284]+8 !=-1 require virtual28==1. | Each robot state independently | Off-treads, charging, carrying | Any virtual result other than exact1 rejects | No floats; actual overrides must be read from concrete vtable, not default true | Behavior/IBehavior.cs IsRunnableBase |
| RB5 | 0x005BD89E..0x005BD8CA | If strategy38 nonnull require WantsToRun==1. Read timer again, compare against118; MOVPL yields true for >= and unordered. | Prior gates | Strategy then second time read | now <118 rejects; NaN comparison admits through PL | Binary32 cooldown timestamp | Behavior/IBehavior.cs IsRunnableBase |

The following rows complete the local helper factories, action/result consumers,
delegation callbacks, manager/chooser integration and named Opus obligations.
PARTIAL means a named higher-layer recipient or runtime input remains outside
the extracted local path; it is not an implementation or settlement claim.
Existing 20261004 reports supplied leads; companions reopen primary instructions.
'''
body+='\n'+(here/'20261007-q17-checked-additions.md').read_text(encoding='utf-8')
body+='\n'+(here/'20261007-q17-runnable-rows.md').read_text(encoding='utf-8')
body+='\n'+(here/'20261007-q17-lock-table-rows.md').read_text(encoding='utf-8')
body+='\n'+(here/'20261007-q17-factory-census-rows.md').read_text(encoding='utf-8')
body+='\n'+(here/'20261007-q17-state-callers-rows.md').read_text(encoding='utf-8')
assert not any(progress.get(r['id'],('NOT DONE',''))[0]=='NOT DONE' for r in scope)
body+='''\n## Scope closure and limits\n\nEvery scoped record is quoted and has checked local rows or an explicit ownership/input limit. No NOT DONE item remains. No manifest status is raised and no C# is changed. The six helper implementations and all79 factory dispatch entries are instruction-checked; all175 state-setter calls and32 static reaction masks are independently reread. Serialized Unity inputs were read locally; one StartupManager full-tree parsing limitation remains explicit in PL7–PL9. There is no proven automatic ProceduralLive producer in this corpus. Do not manufacture one.\n\nHigher-layer proposals: M13-REACTION-CHILD-INPUTS (RB7), M13-ACTION-TEARDOWN (OB1), M13-SLEEP-COMPOUND (OB2), M15-ACTIVITY-MEMBER-TEARDOWN (AT), M10-SEVERE-NEED-EXPRESSION (NI), M5-REACTION-DRIVING-SCOPE (RB8). Existing action completion/watchers, pose/lookup and Viz/SDK records retain their cited ownership. These are proposals for manager review, not new settled records.\n\nValidation: evidence scripts read shipped bytes; no emulator values are claimed. Fidelity --check passes445 records; research-only patch checked for whitespace and scope. No production/full-suite or hardware claim is made. Continue Q18 next, then Q19, Q14 and Q15 under the operator's reordered queue.\n'''
(here/'20261007-M7-M8-rows.md').write_text(body,encoding='utf-8')
print('Saved Q17 review answer;',len(scope),'records; no NOT DONE.')
