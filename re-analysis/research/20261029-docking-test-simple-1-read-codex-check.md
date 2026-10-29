# DOCKING-TEST-SIMPLE-1 independent bounded source check

PASS for the finite diagnostic/documentation contract only, 2026-10-29. Checked reader report `20261029-docking-test-simple-1-read.md`, corrected SHA256 `c44c481a8f1f04e399169736b6393ab4cb6eec6d7048847c3486d710f6060590`. Requested independent reviewer model/effort both omitted/default; actual backend unobserved; no switches. No implementation, .NET, tests, Git mutations, official records or hardware changes by reviewer.

## Scope and finding

The original developer-test state machine remains excluded under the existing recorded M7-022 decision, not a new exclusion based on config absence. Canonical NativeBehaviorRuntime constructs only configured factory outputs; DockingTestSimple has metadata but no configured constructor. CreateNative observes those owners. Historical FreeplayStack.Create/CreateStandalone instead adds ShippedBehaviors.Navigation, whose public MountChargerBehavior preserves the historical DockingTestSimple ID/class. Public Bound/SelectionChooser/manager interfaces can select that custom object. This does not expose the original native seven-state test, but the old absolute wording about every public API must not be repeated without this caveat.

Concrete diagnostic defect: `ChargerBehaviors.cs:260..262` claims the developer config docks in front of the robot; `MountChargerBehavior.OnStart` has no boundary diagnostic. The method actually always constructs MountChargerAction with ChargerGeometry.ObjectId. Its default name and Navigation entry can therefore misleadingly look like recovered native DockingTestSimple. Root requested the smallest description/diagnostic correction; no API or robot-action change is authorized.

## Primary checks opened independently

Binary `resources/lib/armeabi-v7a/libcozmoEngine.so` SHA256 `02263C07F6BB60F4D7F351A3667DCA84FD3E6C0FC6F838E18B5DE7CF4E2989E1`. Used explicit `C:/Users/JimBu/AppData/Local/Programs/Python/Python313/python.exe`, known.py before each native target group, direct LIEF/Capstone/struct reads. Decomp index only for navigation; local function bodies absent.

- Exact shipped `behaviors/devBehaviors/dockingTestSimple.json` contains only behaviorClass and behaviorID, both DockingTestSimple. This is configured input, not proof of a caller or lack of effect.
- ID and class strings resolve to C19EA8. ID string construction 76B63E..64C is followed by movs r0,9 at 76B65A and strb at 76B664 to stack+9C. Class construction 76A43C..44A is followed by movs r0,9 at 76A458 and strb at 76A462. Earlier 8 belongs to the preceding enum. Both bindings are 9.
- Factory TBH entry9 at 59C8BA is halfword E8, yielding target59CA78 from base59C8A8. Allocation290, actual Robot/config forwarded at59CA84/86;59CA88 calls native ctor through4AFACC. No guessed receiver.
- Finite Selection prefix60AA6A compares tag94,60AA70 obtains payload,60AA78 loads payload ID byte,60AA7A obtains Robot+44 manager,60AA7E calls FindBehaviorByID,60AA86/88 copies numRuns. No SDK test occurs in this prefix. Do not recast recorded supported-entry scope as a native SDK gate.
- Runnable5CB384..390 loads state+11C, initializes result0, compares zero, conditionally writes1 and returns. It creates no caller. Reader's initial semantic conclusion was correct but its clz/lsrs instruction citation was wrong; corrected report now cites actual mov/cmp/it/moveq sequence.
- Constructor5CB18C/190 checks context+20 and its+60 before D3 registration. Handler5CB35C..382 gets message then reads byte+4: zero increments own+25C, one stores own+26C=1, other statuses return. No running gate and no later external call. Existing CheckedMessageHandlers registers two dev sets and retains CheckedDockingTest.Handle counters independently of original activation. These live slices belong to M7-027, not the excluded state machine.
- Constructor logger slice5CB04E..0A8 supplies scope2/null queue/size01400000 and calls RollingFileLogger; E2G tag set setup5CB148..17A calls SubscribeToTags. These are separate existing M7-027/M7-028 live constructor effects, not discarded as no effect. Full logger/dispatch/teardown records remain partial; no new settlement or whole-record verification asserted.

Fresh global caller negatives and dead app debug paths were not re-proven: app primary trees unavailable locally. Existing adopted OV34 scope decision is retained as recorded authority, not upgraded from notes into a new primary proof. Original state/actions/callbacks/PrintStats/RecordAttempt/Init/cleanup remain the explicit unread M7-022 frontier. No new crash/hang or M1-034 implementation claimed.

## Current records and contract

Read whole current manifest objects M7-022, M7-018, M7-027, M7-028, M8-013 and M15-002 including title/status/evidence/unresolved. Reader appendix preserves the whole current objects. M7-022 remains RECOVERABLE_GAP/live_path false; the other five remain IMPLEMENTATION_GAP with their own existing frontiers. No record settled, excluded anew, widened or edited. M7-027 and M7-028 live constructor slices are independent of M7-022 activation exclusion.

Builder contract supported in full:

1. Correct MountChargerBehavior XML and Navigation documentation to identify the historical/custom adapter and retained historical label.
2. Before the unchanged OnStart reaction-lock/action sequence, emit a visible MISSING naming M7-022, excluded/unrecovered original native DockingTestSimple, and custom MountCharger adapter. Do not invent SDK rejection or prohibit this custom API.
3. Preserve public type/constructor/default ID/class/list admission/runnable predicate/action arguments/result callback/Finish and order. No factory binding, original state machine, new policy, records/status or channel.
4. Bounded regression covers this custom diagnostic boundary and unchanged adapter outcome; canonical factory must remain unbound for DT. Test must identify substituted custom action/input and must not claim original native conformance. Existing factory delta test M7BatchThreeBTests only becomes required if factory changes; contract prohibits factory edits.

Checklist: production/custom entry named and distinguished; no duplicate native component; gates/order/messages untouched; no deferred native body called complete; no float/integer arithmetic change authorized; test expectations derived from diagnostic contract/current preserved behavior, not claimed native-body evidence; manifests unchanged. No implementation draft exists yet, so this is source-contract PASS only. Builder release and independent complete draft review remain root's next steps.

## Attempts, corrections and commands

Reviewer ran read-only Get-Content/rg/Get-FileHash, current manifest JSON selection, known.py for DockingTestSimple/60AA58/59CA78/5CB35C/5CB18C/76B632/76A430, direct raw finite instruction/data reads. No tests or native emulation launched. Some initial rg globs and guessed paths were invalid/absent (research glob, nonexistent BehaviorFactory.cs, missing Unity/decomp bodies); corrected to explicit current files and direct raw SO. These are tooling-origin attempts, not compiler/test passes or source discoveries. Earlier message misnamed the legacy enumeration CreateManipulation; corrected immediately to Navigation. No code was based on that typo.

Counts at source freeze: source-report correction batches1 (runnable exact-instruction citation); builder reviewer-required draft/test correction batches0; compiler corrections0; self-test corrections0; root production corrections0; reviewer tooling adjustments as documented above. Reader separately preserves its research tooling attempts. No commit or regression result yet. No operator decision required by this finite diagnostic contract.
