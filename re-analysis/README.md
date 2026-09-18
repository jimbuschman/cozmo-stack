# libcozmoEngine.so — reverse-engineering notes (Cozmo 3.4.0-1204, armeabi-v7a)

> Project goal (2026-09-18): build a complete standalone replacement Cozmo stack. See
> **`CAPABILITY_GAP.md`** for the official-vs-PyCozmo gap analysis, proposed architecture and the
> next milestone; **`OBB_INVENTORY.md`** for the unpacked resources/firmware; **`TRANSPORT_SPEC.md`** for the
> reconstructed engine↔robot transport (M1); **`PROTOCOL_STATUS.md`** for the state of all 161 robot
> messages (M2), generated from `protocol/cozmo_robot_protocol.json`. The replacement stack itself lives in **`../cozmo-stack/`** (C#).
> `protocol/robot_protocol_official_vs_pycozmo.txt` is the verified engine↔robot message comparison;
> `reference/pycozmo-master-2026-09-18/` (PyCozmo) and `reference/anki-util-transport-vector/` (Anki's
> transport library as released for Vector, identical to the engine's) are references only.

Generated 2026-09-18 from `resources/lib/armeabi-v7a/libcozmoEngine.so` (17,139,336 bytes,
ELF32 ARM/Thumb-2, built with NDK gold + libc++). Everything here was produced by the scripts
in `tools/`; re-run them if you swap in a different APK version.

## 1. The big win: it is not really stripped

`file` reports "stripped", but only `.symtab` is gone. `.dynsym` still carries **37,447 symbols**,
and because the library was built with default visibility and without `-Bsymbolic`, almost every
C++ method, vtable and typeinfo is exported by mangled name:

| what                              | count  |
|-----------------------------------|--------|
| exported functions                | 31,633 |
| exported objects (vtables, RTTI, globals) | 5,273 |
| vtables (`_ZTV*`)                 | 700    |
| typeinfo (`_ZTI*`)                | 1,070  |
| distinct classes / namespaces with exported methods | 1,466 |
| imported functions (libc, OpenCV, DAS, TTS, ...)   | 484 |

Consequence: nearly every `bl`/`blx` in the binary resolves to a readable name (most intra-library
calls even go through the PLT), so a decompiler will show you `Anki::Cozmo::Robot::Update()` calling
`Anki::Cozmo::BehaviorManager::...` rather than `FUN_006a1234`. This is about as friendly as a
production native binary gets.

Namespace breakdown of exported functions:

```
22372 Anki          (the engine proper)
 5608 std           (libc++ templates instantiated here)
  437 boost
  367 Json          (jsoncpp)
  118 google_breakpad
   18 CLAD          (Anki's message-serialization runtime)
```

Largest engine areas (`symbols/classes_by_method_count.txt` has the full list):

```
7998 Anki::Cozmo::ExternalInterface   generated CLAD message structs (game <-> engine)
2372 Anki::Cozmo::RobotInterface      generated CLAD message structs (engine <-> robot)
 971 Anki::Cozmo::VizInterface        generated CLAD message structs (engine -> visualizer)
 437 Anki::Embedded                   fixed-point vision/marker code shared with robot firmware
 305 Anki::AudioEngine::Multiplexer   Wwise wrapper
 187 Anki::Cozmo::AnimKeyFrame
 141 Anki::Util / 137 Anki::Util::AnkiLab / 117 Anki::Util::QuestEngine
 103 Anki::Vision  (FaceTracker, FaceRecognizer, ObservableObject ...)
  99 Anki::Cozmo::Robot
  61 Anki::Cozmo::IBehavior + ~100 Behavior*/Activity* classes
  42 Anki::Planning::xythetaEnvironment (path planner)
```

## 2. Exported C API (what Unity P/Invokes)

From `unity/scripts/csharp/CozmoBinding.cs` and the export table:

| symbol | signature (C#) | notes |
|---|---|---|
| `cozmo_startup` | `int (string jsonConfig)` | parses JSON, builds `DataPlatform`, inits DAS, calls `configure_engine()` then `Anki::Cozmo::CozmoAPI::StartRun()`, which spawns the engine thread (`CozmoInstanceRunner::Run`) |
| `cozmo_shutdown` | `int ()` | |
| `cozmo_transmit_game_to_engine` | `void (byte[] buf, UIntPtr len)` | push CLAD `MessageGameToEngine` bytes |
| `cozmo_transmit_engine_to_game` | `uint (byte[] buf, UIntPtr cap)` | pull CLAD `MessageEngineToGame` bytes; returns bytes written |
| `cozmo_transmit_viz_to_game` | `uint (byte[] buf, UIntPtr cap)` | pull `MessageViz` bytes |
| `cozmo_wifi_setup` | `int (string ssid, string psk)` | |
| `cozmo_execute_background_transfers` | `void ()` | |
| `cozmo_activate_experiment` | `uint (ptr req, len, ptr resp, cap)` | AnkiLab A/B |
| `cozmo_install_google_breakpad` / `cozmo_uninstall_google_breakpad` | | |
| `cozmo_get_device_id_file_path` | `string (string persistentDataPath)` | |
| `cozmo_send_to_clipboard`, `cozmo_app_install_timestamp` | | |
| `Unity_DAS_*` (10 funcs) | | logging bridge |
| `Java_com_anki_cozmoengine_Standalone_startCozmoEngine` / `stopCozmoEngine` | JNI | **no such Java class ships in this APK**; it just calls `SetCurrentActivity` + `cozmo_startup(jstring)`. Dev-only "engine without Unity" entry point. |
| `Java_com_anki_*` callbacks | JNI | Wi-Fi scan/bind, HTTP, TTS, audio-capture permission |

Global state: `engineAPI` (pointer to `Anki::Cozmo::CozmoAPI`) and `dataPlatform` are exported
data symbols; `cozmo_transmit_game_to_engine` is literally
"if (engineAPI) tail-call `CozmoAPI::ReceiveMessages(buf, len)`" (via a Thumb-to-ARM veneer, see section 4).

### Startup JSON

`cozmo_startup` reads these keys (see `disassembly/dis_cozmo_startup.txt`):

`DataPlatformFilesPath`, `DataPlatformCachePath`, `DataPlatformExternalPath`,
`DataPlatformResourcesPath`, `DataPlatformResourcesBasePath`, `appRunId`, `DataCollectionEnabled`,
`standalone` (bool; skips the Unity-player DAS init path when true).

`configure_engine()` then reads, with defaults confirmed from the immediates in the code:

| key | default |
|---|---|
| `AdvertisingHostIP` / `VizHostIP` / `SdkAdvertisingHostIP` | (from config) |
| `RobotAdvertisingPort` | 5100 |
| `UiAdvertisingPort` | 5102 |
| `SdkAdvertisingPort` | 5104 (5105 also used by `UdpSocketComms::StartAdvertising`) |
| `SdkOnDeviceTcpPort` | **5106** (the port the official Python SDK forwards over ADB/USB) |

The app's actual config TextAsset is extracted to `protocol/engine_configuration.json`
(127.0.0.1 everywhere; `NumRobotsToWaitFor`/`NumUiDevicesToWaitFor` = 1). Unity appends the
path fields at runtime (`RobotEngineManager.CozmoEngineInitialization`). Resources are expected at
`<persistentDataPath>/cozmo/cozmo_resources/...`, e.g. `config/engine/console_filter_config.json`.

### Robot side

`RobotInterface::MessageHandler::AddRobotConnection` hard-codes UDP port **5552**; the robot's
own AP address is supplied by `ConnectToRobot.ipAddress` from the UI (Cozmo's AP is 172.31.1.1).
Transport is `Anki::Util::ReliableTransport` over `UDPTransport` (`RobotConnectionManager`,
`MultiClientComms`).

## 3. Wire protocol on the byte pipe

`RobotDirectChannel.cs` shows the framing: messages are simply concatenated as

```
[u16 LE tag][CLAD payload]  [u16 LE tag][CLAD payload] ...
```

No length prefix; the engine knows each tag's size (error string: "Buffer's size does not match
expected size for this message ID"). Tags:

* `protocol/MessageGameToEngine_tags.txt` — 255 tags (0..254), e.g. `DriveWheels=91`,
  `PlayAnimation=23`, `SayText=33`, `EnterSdkMode=241`, `ConnectToRobot=76`.
* `protocol/MessageEngineToGame_tags.txt` — 155 tags, e.g. `RobotState=49`,
  `RobotObservedFace=70`, `ImageChunk=23`, `SdkStatus=134`.

Field layouts are in the decompiled C# under `unity/scripts/csharp/Anki.Cozmo.ExternalInterface/`
(one file per struct, with `Pack`/`Unpack`), and identically in the native
`Anki::Cozmo::ExternalInterface::*::Pack/Unpack/Size` exports. The public `cozmoclad` PyPI
package (version 3.4.0) is the same generated code in Python, so it can be used to build/parse
these buffers directly.

Note the engine also has a `Ping` / latency path and checks a CLAD hash on
`UiDeviceConnectionWrongVersion` — version 3.4.0 clients/tools must match.

## 4. Where things live (addresses are file VAs; Ghidra base 0x0)

```
.plt     0x004a4010  size 0x3284c
.text    0x004d6860  size 0x60ce22   (all Thumb-2)
.rodata  0x00be3ed0  size 0x433648   (30,557 printable strings -> symbols/rodata_strings.txt)
.data.rel.ro 0x0101cbf0 (vtables)
.init_array 0x0103e3b0 (0x7d static constructors)
cozmo_startup                         0x00665708
configure_engine(Json::Value&)        0x006655bc
Anki::Cozmo::CozmoAPI::StartRun       0x0065b0f8
cozmo_transmit_game_to_engine         0x006669b8  -> Anki::Cozmo::CozmoAPI::ReceiveMessages(uint8_t const*, uint32_t)
JNI_OnLoad                            0x006679f4
```

Disassembly gotcha: gold inserted Thumb-to-ARM long-branch veneers. A call landing on
`bx pc; nop; ldr ip,[pc]; add pc,ip,pc; <offset>` (e.g. 0x008cd44c) is a thunk; the real target is
`offset + (thunk + 0x10)`, usually a PLT stub. `tools/disarm.py` does not follow these yet, so
when a `bl` resolves to nothing, check for that pattern.

Strings worth grepping in `rodata_strings.txt`: `sdk_mode_obfusc8te`, `DASConfig.json`,
`uniqueDeviceID.dat`, `Play__Sdk__*` (audio events), `MARKER_SDK_*` (custom-marker names),
`FIRST_SDK_TAG`/`LAST_SDK_TAG`, `Use of head/lift/body motors is limited while on charger in SDK mode`.

## 5. Tooling

Nothing was installed on this machine except Python packages. `tools/` contains:

| script | purpose |
|---|---|
| `elfinfo.py <so> exports.txt imports.txt` | header, sections, NEEDED libs, export/import lists |
| `demangle.py exports.txt out.txt` | demangle with `cpp_demangle`, namespace/class histogram |
| `objects.py <so> out.txt` | vtables / typeinfo / globals |
| `strings.py <so> out.txt` | `.rodata` strings with VAs |
| `disarm.py <so> <mangled-or-C-symbol>...` | Thumb-2 disassembly with PLT/GOT/string-literal/movw-movt resolution. Pipe through `grep -E 'PLT|"'` for a call-and-string summary of a function. |
| `portscan.py <so> lo hi` | find `movw` immediates in a range and name the enclosing function (how the ports above were confirmed) |

Python deps: `pip install lief pyelftools capstone cpp-demangle` (already installed).

For real decompilation install **Ghidra** (free, ARM Thumb decompiler is good): import the .so,
let auto-analysis run (it will pick up all 37k dynsym names), then start from `cozmo_startup`,
`Anki::Cozmo::CozmoAPI::CozmoInstanceRunner::Run`, `Anki::Cozmo::CozmoEngine::Update`,
`Anki::Cozmo::UiMessageHandler::*` and `Anki::Cozmo::Robot::Update`. Because vtables and RTTI are
present, Ghidra's "RecoverClassesFromRTTI" script will rebuild the class hierarchy too.

## 6. Practical routes to "using" the engine

1. **Drive the stock app over the SDK port (least work).** The app already contains the SDK
   server (`SdkOnDeviceTcpPort` 5106, enabled by `EnterSdkMode` from the settings SDK modal).
   The archived open-source `cozmo` + `cozmoclad==3.4.0` Python packages speak exactly this
   protocol over `adb forward tcp:5106 tcp:5106`. No native RE needed.
2. **Run the engine without Unity.** The app expects the `cozmo_resources` tree, which is *not*
   in this APK: it comes from the Google Play OBB (`main.<version>.com.anki.cozmo.obb`,
   `GooglePlayDownloader.cs`), so you also need that OBB. With it, a small Android host could load
   `libDAS.so` + `libcozmoEngine.so`, call `cozmo_startup` with the JSON above (`"standalone":true`),
   and exchange CLAD frames via the three `cozmo_transmit_*` functions. Everything Unity does is
   in `RobotEngineManager.cs` / `RobotDirectChannel.cs`.
3. **Port / re-implement.** The symbol coverage makes this a viable long-term project, but note
   the engine depends on Wwise (`Anki::AudioEngine`), Acapela TTS (`libacattsandroid.so`) and
   OpenCV 3, all of which are separate binaries here.

## 7. Related upstream

Digital Dream Labs released the source of the sibling **Vector** engine after acquiring Anki. Vector's engine
shares the `Anki::Cozmo` namespace, `CLAD`, `DAS`, `AnkiLab`, `BehaviorManager`, `Util::Data::DataPlatform`
etc., so its source is the closest thing to header files for this binary and resolves most
struct layouts and JSON config keys you will meet in Ghidra.
