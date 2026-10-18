| Routine / question | Coverage | Verdict and remaining boundary |
| --- | --- | --- |
| UiMessageHandler::Init | Names the persistent UI fields, message gate, subscription storage and comms-init prerequisite, with readers. | REQUIRED: enables message dispatch and initializes the comms whose counts gate loading. UNKNOWN: full subscriber bodies and general socket internals; no claim they have no effects. |
| CozmoEngine::InitInternal | Audio input publication and first-tick main-thread flag recovered. Factory-log calls identified separately. | REQUIRED: audio dispatch registration and state consumed by Update. UNKNOWN: full factory archive/file effects; no NO EFFECT assertion for unread callees. |
| Manager's concrete MessageHandler Init | Resolves slot to 0x0069D088; publishes context, manager, connection manager and initialized flag; initializes transport and registers inputs. | REQUIRED: transport startup, connection/send/receive readiness. M1-001/-005/-024/-025/-026 already own substantial leaves; they do not cover this whole Init by implication. |
| SendSupportInfo | Native conditional send, source getter, field and E2G order recovered. | REQUIRED: SupportInfo(deviceId) reaches E2G before SetEngineState(2). UNKNOWN: full Android identity initialization and arbitrary runtime input value; identified as an app/host input boundary. |
| Robot WifiFlashID 0xEC | Native callback registration, payload getter, all handler operations read. | NO EFFECT on (a)–(d): only telemetry/log formatting and temporary allocation/cleanup. No persistent handler-state write, E2G call or robot send. |

# OV126 — five narrow M1/M2 startup checks

Answers [20261017-ov126-m1-startup-narrow.md](../requests/overnight/20261017-ov126-m1-startup-narrow.md), 2026-10-17.
Baseline e11caec3. Coverage table created first. This is an effect check under the 2026-10-17 scope, not a complete
extraction of the five routines and their descendants, an implementation review, or a manifest settlement.

Primary evidence: `resources/lib/armeabi-v7a/libcozmoEngine.so`,
SHA-256 `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`.
Additional shipped source: `resources/lib/armeabi-v7a/libDAS.so` for the imported platform getter, SHA-256 `0a0bc63b301fb7f9ffb559462f712a8bf9dd4954b11c43face8f4783852f496c`.
Addresses are each named ELF's virtual addresses, with Thumb bits removed.
Capstone instructions, literals, ELF relocations and vtable entries are evidence; decompilation/index names were navigation only.

Current manifest re-read:
- **M1-054, IMPLEMENTATION_GAP**, "CozmoEngine::Init before RobotManager::Init: the +0x10 Info, the three advertising-key isMember errors (return 1), UiMessageHandler::Init, vslot+8; and RobotManager::BroadcastAvailableAnimations".
  Evidence is OV22-check's call sites and `20261016-batch-check.md`'s Init gates/order. Its current unresolved names all three startup recipients here, and explicitly asks this narrow check.
- **M1-056, RECOVERABLE_GAP**, "CozmoEngine::Update states 1 and 2: HasDesiredNumUiDevices, SendSupportInfo, SetEngineState(2); the per-tick loading poll and EngineLoadingDataStatus; delivery of the Init-time broadcast with no UI device".
  Evidence is `20261017-ov124-check.md`'s switch, calls and partial progress/delivery evidence; its unresolved moves SendSupportInfo to OV126.
- **M2-020, IMPLEMENTATION_GAP**, "Robot-message dispatch residue: wildcard ID 0x101CF (all-signals branch unreachable), handle reset/unlink, re-Init appends before release, ~Messaging retirement order, TracePrinter B0/EC registrations; each owner's own handle wiring is owned by that owner's record".
  Evidence is `20261013-robot-message-subscribers-check.md` and `20261013-dispatch-residue-check.md`. Its current unresolved corrects EC to WifiFlashID, treats B0 as useful diagnostics without extraction, and requests this EC check.

Kind (a) = robot traffic, (b) = resulting robot behavior, (c) = game/API delivery or readiness, (d) = state a later path reads.
A REQUIRED verdict proves at least the listed positive effects; UNKNOWN rows remain UNKNOWN rather than being excluded.
No research row is marked EXACT_SOURCE and no existing record's classification changes.

## 1. UiMessageHandler::Init — 0x006601B0

| Effect | Kind | Citation | Reader for (d) |
| --- | --- | --- | --- |
| Initialize each non-null comms object with slot index and config. If any virtual Init returns other than 1, return 1 before publishing the successful UI initialization state. The outer engine Init checks this return. | c, d | 0x006601C6..0x006601E6: UI+4+4*i, virtual +8, compare result 1; failure 0x0066024A..0x0066024C. Engine caller 0x004EC84C/0x004EC854. Concrete direct Init 0x0065C3D4..0x0065C3D6 returns 1; TCP Init 0x0065F064..0x0065F1BE sets up its server/config. | HasDesiredNumUiDevices 0x006632F0..0x00663308 reads these comms' connected count via +0x20 and desired field +0x84; this gates Update 0x004ED5D8..0x004ED5EA. Thus a no-UI replacement cannot infer that initialization has no readiness effect. |
| Store context pointer at UI+0xF4. | d | 0x006601F6 str r5,[r4,#0xF4]. | OnEnterSdkMode 0x00661528..0x00661534 obtains context+0x34 NeedsManager and pauses it; OnExitSdkMode 0x0066154A..0x00661552 obtains it and conditionally unpauses. Those are unsupported SDK paths, already M1-051; this pointer's demonstrated readers are named, not treated as supported SDK behavior. |
| Set UI+0xF1 to 1, enabling inbound message processing. | c, d | 0x006601EC movs r2,#1; 0x006601FA strb r2,[r4,#0xF1]. | ProcessMessages 0x0066280A ldrb +0xF1, 0x0066280E compare zero, 0x00662810 branch past processing when false. UI Update invokes ProcessMessages at 0x00662A88; Engine Update invokes UI Update at 0x004ED514 before its state switch. |
| Install 16 G2E subscriptions and retain their scoped handles in the vector at UI+0x10/+0x14/+0x18. Registers HandleEvents for 4D,4E,00,4B,F3,F5,F6,F7; HandleGameToGameEvents for 3B..40; OnEnterSdkMode for F1; OnExitSdkMode for F2. These are input dispatch registrations, not immediate robot commands. | c, d | Subscribe calls 0x00660226, 0x006602A8, 0x0066031E, 0x00660398, 0x00660412, 0x0066048C, 0x00660506, 0x00660580, 0x006605FA, 0x00660674, 0x006606EE, 0x00660768, 0x006607E2, 0x00660858, 0x006608CE, 0x00660944. Each loads UI vslot +0x2C with a tag and bound object, then appends a handle. First vector publication 0x00660228..0x00660246, slow path 0x00660252. Callback GOT 0x0103F918 -> 0x00660B75, 0x0103F91C -> 0x0066107D, 0x0103F920 -> 0x0066150D, 0x0103F924 -> 0x00661539. | Registrations retain bound callbacks for dispatch; destructor reads/releases that handle vector via UI+0x10 at 0x00660188..0x0066018C. Callback bodies are the consumers; their supported-input effects are not re-extracted here. |
| No further direct persistent UI field write appears in this Init body beyond context, initialized flag and subscription vector storage. Callee-owned comms/signal state is separate. | d; bounded store census | Read executable Init instructions 0x006601B0..0x0066099C, including every subscription block; success returns zero at 0x00660988. Direct base-register store census identifies +0xF4, +0xF1 and vector-end +0x14; vector growth uses its named vector helper. | Context and flag readers above; vector retirement above. This is not a claim of no state changes inside virtual Init/Subscribe or allocator helpers. |
| UNKNOWN full effects of HandleEvents 0x00660B74, HandleGameToGameEvents 0x0066107C and the concrete signal Subscribe/dispatch descendants. General comms/network machinery is not fully checked here. | UNKNOWN | Bound callback addresses and calls above; direct/TCP Init/count boundaries were checked for OV125's narrow predicate. SDK callback context reads were read here, but full SDK descendants are excluded by the standing scope. | Future extraction is needed only for a supported feature whose effect crosses this boundary; no blanket NO EFFECT claim. |

**Verdict: REQUIRED** — UI+0xF1 gates message processing and comms initialization feeds the loading prerequisite.
The standalone stack replaces UI sockets, but readiness/dispatch cannot be dropped on that basis. The +0xF4 SDK readers are identified separately.

## 2. CozmoEngine::InitInternal — 0x004EDC6C

The request's table address needs an address-point distinction: ELF vtable starts at 0x0101E488, its runtime vptr is
0x0101E490 (table +8), and that vptr's +8 slot is **0x0101E498**, ARM_ABS32 to **0x004EDC6D**.
Symbol ownership here is CozmoEngine::InitInternal; "Impl" in the request is not a different target.

| Effect | Kind | Citation | Reader for (d) |
| --- | --- | --- | --- |
| Construct an AudioUnityInput using context+4 external interface. If context+0xC audio mux exists, register that input. This creates audio message subscriptions and a usable mux input. | c, d; boundary to M6 | 0x004EDC7C..0x004EDC8A allocation/constructor; audio-mux null gate 0x004EDC8E..0x004EDC92; RegisterInput 0x004EDC96. Constructor PLT 0x004A52FC -> 0x00591590; first subscription at 0x005915D8..0x005915FA. RegisterInput PLT 0x004A51D0 -> 0x008DEB62: increments mux+0x1C 0x008DEB6C..0x008DEB76, stores mux at input+4 0x008DEB7A and input ID at +8 0x008DEB7E, inserts input pointer into mux map 0x008DEC04 and 0x008DEC8A..0x008DECD0. | AudioMuxInput::HandleMessage 0x008DFC52 reads input+4, 0x008DFC56 reads ID+8, tail forwards at 0x008DFC5E. Current M6-023 owns the app-audio dispatch path; M6-029 owns lifetime. These leaves are not newly settled here. |
| Clear engine+0x61 so the first initialized Update installs the main thread in context, then sets the flag. | d | 0x004EDD0E movs r0,#0; 0x004EDD10 strb r0,[engine,#0x61]. | Update 0x004ED4E6 reads flag, 0x004ED4EA skips setup if set; SetMainThread 0x004ED4EE; flag=1 at 0x004ED4F2..0x004ED4F4. SetMainThread 0x004EAE60..0x004EAE6C stores current thread through context+0x40; IsMainThread 0x004EAE72..0x004EAE8A reads/compares it. For example loader uses IsMainThread 0x0051F398. This is thread bookkeeping with demonstrated readers, not a newly found robot command. |
| Construct a temporary FactoryTestLogger(true), get log count, and if count >=100 call ArchiveLogs; only the returned archive result selects the diagnostic branch in this caller. Destroy temporary and return zero normally. | File/diagnostic boundary; downstream effects UNKNOWN | 0x004EDC9A..0x004EDCAA constructor/GetNumLogs; compare 100 and blo 0x004EDCAC..0x004EDCAE; archive call 0x004EDCB6; result comparison 0x004EDCBA; diagnostics 0x004EDCD2 / 0x004EDCE8; destructor 0x004EDD16; normal zero return 0x004EDD28. Resolved callees: FactoryTestLogger constructor 0x00671658, GetNumLogs 0x00675D14, ArchiveLogs 0x00675FF0, destructor 0x0067173C. | UNKNOWN: GetNumLogs/ArchiveLogs file bodies not extracted in this narrow check; do not assert that archive output has no engine reader. M7-028 covers other FactoryTestLogger construction/lifetime facts, not automatically this archive path. |
| No direct robot-interface send or E2G send in this caller. Positive effect is registration, plus the state reset; callee audio effects remain real. | bounded caller finding | Executable caller instructions 0x004EDC6C..0x004EDD2E and cleanup pads 0x004EDD32..0x004EDD6E. All non-formatting functional calls are named above. | No whole-routine NO EFFECT conclusion is drawn. |

**Verdict: REQUIRED** — audio-input registration and a later-read engine flag are established.
UNKNOWN remains at full factory-log callees and the detailed audio descendants owned by M6; this does not authorize excluding them.

## 3. RobotManager's MessageHandler virtual Init

| Effect | Kind | Citation | Reader for (d) |
| --- | --- | --- | --- |
| Resolve the concrete object and virtual Init. Manager+0x60 holds RobotInterface::MessageHandler; vtable 0x010310D0 has runtime address point +8, whose Init slot +8 is 0x010310E0 -> 0x0069D089. | d; concrete target | Manager call 0x0052E75E loads +0x60, supplies manager/context at 0x0052E760..0x0052E768 and invokes vslot +8 at 0x0052E76A. ARM_ABS32 relocation at 0x010310E0 names MessageHandler::Init(Json const&,RobotManager*,Context const*). | Actual target body 0x0069D088; no guessed interface implementation. |
| Read AdvertisingHostIP and RobotAdvertisingPort. Reject native port values outside the unsigned 16-bit range before successful initialization. Address is used for initialization diagnostics here, not the later fixed robot remote port. | readiness/config input | 0x0069D0AE/0x0069D0B2 JSON lookup/asCString; 0x0069D0C4/0x0069D0C8 lookup/asInt; 0x0069D0CE compare 0x10000 and blo at 0x0069D0D2; error returns via 0x0069D412. | Existing M1-001 evidence B4 owns this config gate; actual remote address construction is separately read at 0x0069DE84..0x0069DE9E (5551/5552), not taken from this advertising port. |
| Store context pointer +0x40, derive a resource path through context DataPlatform, store manager pointer +0x20, construct/publish RobotConnectionManager at +0x24 (deleting prior instance if any), call its Init. | a, c, d | Context store 0x0069D136; path input context+8 0x0069D340 onward and pathToResource 0x0069D368; manager store 0x0069D3AA; new/constructor 0x0069D3AE..0x0069D3BC; publication 0x0069D3C4; previous virtual deletion 0x0069D3CE; RCM Init 0x0069D3D6 via PLT 0x004BE10C. | ProcessMessages uses +0x24 at 0x0069D87A (RCM Update), +0x24 at 0x0069D8BC (PopData), +0x20 at 0x0069D8CC (GetRobot); context +0x40 read in received-message path at 0x0069DA94. AddRobotConnection reads +0x24 at 0x0069DEA2. |
| Initialize transport configuration through RCM Init. This is real transport startup; replacing the surrounding plumbing must retain its output effects. | a, d | Init call above; current M1-005 evidence identifies RCM Init 0x0062EFCE..0x0062F076 and its reliable-transport writes, e.g. resend/ping timing and limits. | M1-005 owns tunables, M1-002 owns framing. This report establishes the production call, not a new extraction of those accepted leaf bodies. |
| Set initialized byte +0x28 =1 after RCM Init. | a, c, d | 0x0069D3DE movs r1,#1; 0x0069D3E0 strb r1,[handler,#0x28]. | ProcessMessages 0x0069D870..0x0069D876 skips processing while false; SendMessage 0x0069DCFE..0x0069DD0E also gates outgoing sends. Existing M1-024/-026 evidence owns these receiving/sending paths. |
| When context+4 is non-null, subscribe G2E tags 0x50, 0xF2, 0x58 and retain handles under handler+0x2C. | a, c, d | Interface gate 0x0069D3E4..0x0069D3E6; calls 0x0069D3F4/0x0069D3FA/0x0069D400 to registration helpers. Helpers set tag at 0x0069D624, 0x0069D6E4, 0x0069D7A4; invoke Subscribe slot +0x2C at 0x0069D64E, 0x0069D70E, 0x0069D7CE; append handles at 0x0069D650..0x0069D672 and corresponding later blocks. | These inputs retain callbacks for run-mode, SDK exit and disconnect-reason inputs (generated MessageGameToEngine tags 80,242,88 corroborate their names); handler destructor releases the vector at 0x0069D042..0x0069D046. Full callback bodies not re-extracted. SDK exit is already unsupported; M1-020 identifies unused ReliableTransportRunMode input. |
| UNKNOWN full pathToResource/RCM constructor effects and bound callback bodies beyond their existing checked ownership. | UNKNOWN | Concrete callees named at 0x0069D368, 0x0069D3BC and registration calls above. This check does not rebuild all transport/socket/file machinery. | Do not interpret one settled leaf as coverage of the whole Init. |

**Verdict: REQUIRED** — it publishes the connection manager, initializes transport and opens send/receive gates.
Existing **M1-001** (address/config gate), **M1-005** (transport tunables), **M1-024** (tick receive),
**M1-025** (connect/disconnect), **M1-026** (send gate) own relevant leaves. Their current titles/statuses/evidence were
re-read: all five are EXACT_SOURCE in the manifest; that is the existing classification, not a research upgrade.
No single one claims every store/registration in this Init.

## 4. CozmoEngine::SendSupportInfo — 0x004ED79C

| Effect | Kind | Citation | Reader for (d) |
| --- | --- | --- | --- |
| Ask DASGetPlatform; if null return without sending. Otherwise call platform vslot +0x10 and take its char pointer as the SupportInfo input. | c | Complete 0x004ED79C..0x004ED7BE: imported getter 0x004ED7A2, null gate 0x004ED7A6; platform slot load/call 0x004ED7AC..0x004ED7B0. | Not a state write in this caller. |
| Copy returned NUL-terminated bytes into a string, construct SupportInfo (tag 0x94), invoke external-interface +0x1C once, clean up temporaries. | c | Sender wrapper 0x004EDB20..0x004EDBF4: strlen 0x004EDB44, explicit-length copy 0x004EDB50; SupportInfo constructor 0x004EDB7A via PLT 0x004A52E4 -> 0x00728B36; E2G invocation 0x004EDB82. Constructor 0x00728B36..0x00728B52 moves the string to union+8, writes 0x94 tag at 0x00728B4E..0x00728B50. | Game/API consumer. Generated Unity field is deviceId (SupportInfo.cs:10); RobotEngineManager.cs:332–333 dispatches to ProcessSupportInfo, :425–427 passes to DataPersistenceManager; DataPersistenceManager.cs:379–381 stores info.deviceId. No app UI reproduction is inferred. |
| Source of the string is a platform-owned field, not a native literal supplied by CozmoEngine. | c; host input boundary | In shipped libDAS.so, DASGetPlatform 0x00012AC0..0x00012AC6 reads global 0x00037114. Android vtable 0x00036AB8 has address point 0x00036AC0; +0x10 at 0x00036AD0 targets 0x00028AF6. 0x00028AF6..0x00028B06 returns the C-string of platform object's string at +0x1C (short inline or heap at +0x24). | UNKNOWN: full Android platform initialization and runtime identity value not established here. The generated field/app consumer identifies deviceId; a standalone replacement identity is an input-policy matter, not a reason to drop this API event. |
| Payload encoding has a one-byte length and then that many bytes on the native generated path. | c | SupportInfo::Pack 0x0071CD98..0x0071CDAA calls string Pack helper 0x006C22DC..0x006C2326. Helper writes low length byte 0x006C22F8, count 1 at 0x006C22EC and WriteBytes 0x006C22FC; uxtb data count 0x006C2304 and WriteBytes 0x006C2316. No assumption that the C# setter's validation runs in native. | Serializer boundary only; no new outer framing claim. |
| State-1 ordering is gate succeeds -> SendSupportInfo -> SetEngineState(2). SupportInfo is not just a diagnostic. | c, readiness | 0x004ED5D8..0x004ED5EA: HasDesiredNumUiDevices, result ==1, SendSupportInfo at 0x004ED5E2, SetEngineState(2) at 0x004ED5EA. | State-2 loading is reached on a later switch pass, as OV125 establishes from the native branches. |

**Verdict: REQUIRED** — an actual SupportInfo(deviceId) E2G/API event, conditionally on a platform object, before state 2.
UNKNOWN input identity initialization is retained; no claim of no effect, constant empty payload or universally absent platform.

## 5. TracePrinter::HandleWiFiFlashID — 0x0053CD9C

| Effect | Kind | Citation | Reader for (d) |
| --- | --- | --- | --- |
| TracePrinter registers robot tag 0xEC to this callback. | callback identification | 0x0053BC94..0x0053BCA2: tag 0xEC at 0x0053BC9E; helper 0x0053C144. GOT 0x0103EAF4 (PC-relative load 0x0053BC98/0x0053BC9A) relocates to HandleWiFiFlashID 0x0053CD9D. | No new handler state from this registration is attributed to receipt of EC. Registration ownership remains M2-020. |
| Read the payload's u32 ID through RobotToEngine::Get_wifiFlashID; convert it to decimal string for telemetry data; extract bits 8..15 and 16..23 for telemetry formatting. | none of a–d; diagnostics only | Handler 0x0053CDA0 adds event+0xC, getter call 0x0053CDA4 via PLT 0x004AA75C -> 0x007B4992 (adds payload offset 4, returns). 0x0053CDAC loads u32; to_string(unsigned) 0x0053CDB0 via imported libc++ function. Bit extraction 0x0053CDF4 and 0x0053CDFA. | No persistent state reader; payload is only read. |
| Emit telemetry event hardware.wifi.flash_id with data pair "$data" -> decimal ID and format "%x%x"; free temporary vector/string and return. | none of a–d; log/telemetry sink only | 0x0053CDD4..0x0053CDF0 allocates/populates temporary pair; event key resolved from ADR at 0x0053CDFE to 0x0053CE7C, format 0x0053CE94; sEventF 0x0053CE04. Complete handler instructions 0x0053CD9C..0x0053CE3A: stores target stack or allocated temporary storage only; final cleanup 0x0053CE08..0x0053CE34. | No TracePrinter member write, no robot send, no E2G invocation, no retained ID in this handler. The logging sink is existing diagnostics ownership, not API event delivery. |
| Allocation/string conversion/logging are the only callees besides payload getter and cleanup. There is no unexplored functional callback in the handler. | none of a–d | Resolved callees: payload getter, imported std::to_string(unsigned), operator new/delete, sEventF. These are visible in the complete native instruction body above. Logging formatting internals are not re-extracted. | No later engine-state reader is introduced by the handler. |

**Verdict: NO EFFECT on (a)–(d)** — EC receipt's handler only emits diagnostics/telemetry.
This is an evidence-based no-effect finding for the callback, not an exclusion of the robot message or a statement about the enclosing dispatch.
M2-020's registration/lifetime obligations remain. Useful diagnostic handling is compatible with the operator's scope; no implementation is made here.

## Remaining boundaries and validation

No existing record is contradicted by the demonstrated effects. M1-054 and M1-056 remain incomplete by their own current text;
the positive findings do not settle the whole startup path or upgrade a status.
The vtable address-point clarification in section 2 is a citation precision issue, not a different function.

Each requested routine has an addressed positive effect or a bounded NO EFFECT verdict. Unread callees are explicitly named
where their completeness remains UNKNOWN: UI callback/comm machinery, full audio dispatch descendants,
FactoryTestLogger GetNumLogs/ArchiveLogs, RCM construction/resource-path leaves and Android identity initialization.
A manager can assign a follow-up only where a supported behavior needs those details, or record an authorized machinery exclusion;
none is silently declared unnecessary here.

Checked concrete virtual targets, state publications and named readers, subscription tags, native E2G constructors, the SupportInfo
string serializer, and every WifiFlashID handler instruction. Read UI Init for effects/stores, without extracting the downstream
semantics of all 16 handlers. The report intentionally does not re-audit the native engine Init or M1-024 running loop.
No production code, manifest, inventory, job or status changed. No implementation regression tests or hardware run applies.
Commit/push contains only this answer; manager independently checks the evidence before adoption.
