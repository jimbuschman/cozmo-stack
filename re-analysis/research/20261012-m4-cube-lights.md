# Cube lights: blend, state text, LED callers and animation loader

## 1. M4-041 — coverage

| Step | What the original does | Primary citation | Record / coverage |
| --- | --- | --- | --- |
| Blend inputs | Takes ObjectID, source LightPattern list, destination list; resolves the connected ActiveObject through BlockWorld. | Engine 0x00638B3C..0x00638B50 | M4-041, read |
| Missing object | Info NullActiveObject, returns false without clearing/writing destination. | 0x00638BAA..0x00638BEA | M4-041, read |
| Copy | Clears destination, copies source patterns in list order. | 0x00638B54..0x00638B72 | M4-041, read |
| Blend operation | For every pattern and LED index 0..3, zero onColor/offColor words become REV32 of current LED's on/off ColorRGBA word. Any replacement sets return flag. No floating-point arithmetic. | 0x00638B74..0x00638BA6 | M4-041, read |
| Play caller | Blends original animation, or ApplyAnimModifier's temporary list when modifier argument is true; consumes return as “used blending,” including AlreadyPlayingBlendedAnim gate. | 0x006384D6..0x0063853A; 0x0063858C..0x006385BA | M4-041 / M4-018 interface, read |
| Stop timing | Stop flags are applied, then Update receives caller's bool, then a fresh stringstream serializes the resulting state. | 0x006390BA..0x0063918E | M4-041, read |
| Object selection/order | All objects for ObjectID=-1; otherwise matching map ID only; ascending ObjectID tree order. | 0x0063918E..0x0063919A; 0x0063928C..0x006392B2 | M4-041, read |
| Object prefix | Writes object signed integer, requested layer name, current layer name and opening bracket. | 0x0063919C..0x00639200 | M4-041, read |
| Anim entries | Requested layer's list in forward order: stored animation name, trigger name, uint32 timer, optional STOP_NOW, trailing space. Closes bracket; no separator between objects. | 0x00639204..0x00639286 | M4-041, read |
| Result log | Channeled debug; found/did not find, requested trigger, serialized state. Exact misspelling StopLignAnim retained. | 0x006392B6..0x00639300 | M4-041, read |
| GetLEDState body | Signed index >=4 warns and clamps to 3; negative warns and clamps to 0; returns object+0x0C+28*index. | 0x0063956C..0x00639600 | M4-041, read |
| All direct callers | Executable-section Thumb and ARM BL/BLX scan finds only 0x00638B7C (Blend) and 0x0063A642 (SetLights). No dynamic symbol/relocation or literal word holding this entry found. | Local scan script and output named below | M4-041, scanned |
| Caller index reachability | Blend loops 0..3; SetLights also loops 0..3. Both engine paths are in scope; neither reaches index-warning branches. | 0x00638B76..0x00638BA0; 0x0063A62A..0x0063A642; 0x0063A750..0x0063A75C | M4-041, read |

### Blend answer

The name “Blend” does **not** mean interpolating channels or multiplying an alpha. There are no floating-point instructions/constants in 0x00638B3C..0x00638BF0. There is no blend factor. It substitutes whole zero uint32 colors using the cube's current stored LED state.

For each copied pattern and `i=0,1,2,3`:

```
led = GetLEDState(connectedObject, i)
if copiedPattern.onColors[i] == uint32(0):
    copiedPattern.onColors[i] = REV32(led.onColor)
    usedBlend = true
if copiedPattern.offColors[i] == uint32(0):
    copiedPattern.offColors[i] = REV32(led.offColor)
    usedBlend = true
```

The source LED state is 28 bytes per LED; the first two 32-bit words are its on/off ColorRGBA bytes. REV exchanges all four bytes, including alpha; it is not RGB-only. The destination on/off arrays are LightPattern+0x0C/+0x1C (list node adds an 8-byte link header, yielding the +0x14/+0x24 stores). Zero is tested across the **entire** color word; a nonzero alpha or any nonzero channel prevents replacement. Periods, offsets, rotation, relative fields, duration, override flag and pattern debug name survive the list copy unchanged. All patterns inherit from the same current stored LED state; this body does not advance LED state while walking the list.

Index and color loads/stores are 32 bits. Index arithmetic computes `7*i`, shifts by 2 (28-byte stride); no narrow-color arithmetic or floating conversion occurs in blend. Return is `usedBlend & 1`, initially false. A zero destination slot makes it true even if the source LED color is also zero. Empty source yields empty destination and false. Missing connected object leaves destination untouched, logs channel `CubeLightComponent`, key `CubeLightComponent.BlendAnimWithCurLights.NullActiveObject`, value `No active object with id %u found in block world unable to blend anims`, ObjectID's uint32 value, empty metadata, and returns false; no errG/break.

PlayLightAnim calls the body at 0x0063850C after optional ApplyAnimModifier. Its return describes whether zero-color inheritance was used, rather than success/failure of the play operation. Existing gates/queue ownership remain M4-018; this read does not re-audit those unrelated gates.

### StopLignAnim.Result serializer answer

The log is debug, channel `CubeLightComponent`, key **`CubeLightComponent.StopLignAnim.Result`**, format **`%s anim '%s'. Current state: %s`**, empty metadata. Arguments are `found` or `did not find` from the earlier stop-match flag, EnumToString(requested trigger), then the stringstream text. It is constructed **after Update**, so a marked animation may already have disappeared by serialization. “found” is not inferred from whether an entry remains in the serialized list.

The exact grammar is:

```
object=<signed ObjectID> layer=<requested layer name> currLayer=<current layer name> [<entries>]
```

Each entry is:

```
<CurrentAnimInfo stored animation name>:<EnumToString(trigger)>@<uint32 timer>[:STOP_NOW] 
```

The space after every entry is present, including the final entry. An empty list renders `[]`. Multiple selected objects are concatenated directly: `]object=...`, with no newline, comma or inserted space between them. No selected objects yields the empty string after `Current state: `.

Fields, sources and rendering in order:

- `object=`: tree node+0x14, streamed as **signed int32 decimal** (0x006391A6..0x006391AA). No hex/manipulator is applied to this new stream.
- ` layer=`: requested layer argument, table at 0x0102EE64. Entries 0/1/2/3 are `User/Game`, `Engine`, `State`, `Invalid` (string pointers at 0x0102EE64..0x0102EE70).
- ` currLayer=`: tree node+0x18, same table. It can differ from the requested layer being printed.
- ` [`: requested layer's list starts at tree node+0x20+12*layer, first link +4.
- Entry name: CurrentAnimInfo's string at list-node+8, emitted with its stored **length**, not strlen. This is the animation-info name, not LightPattern.patternDebugName.
- `:` then trigger: uint32 enum at list-node+0x14, EnumToString then strlen bytes.
- `@` then timer: list-node+0x20, `operator<<(unsigned int)` (0x0063925A..0x0063925C), ordinary unsigned decimal; it is the stored timer, not remaining duration or a newly computed timestamp.
- `:STOP_NOW`: only if list-node byte+0x41 is nonzero.
- One literal space per entry, then `]`.

The object map is traversed in ascending ObjectID tree order, each layer stack in its linked-list forward order. Final `%s` consumes the serialized text as a C string; embedded NUL in a stored name would truncate the final displayed state even though the stream itself received the full name length. No escaping/quoting is added around entry names.

### GetLEDState caller answer

The local getter entry is 0x0063956C. The scan checked every two-byte Thumb candidate and every four-byte ARM direct BL/BLX candidate in **all executable ELF sections**, including PLT, and decoded candidates before comparing targets. Results:

1. `0x00638B7C: bl 0x0063956C`, BlendAnimWithCurLights. Index starts at 0, increments by 1, loop ends at 4; calls only 0..3.
2. `0x0063A642: bl 0x0063956C`, SetLights. Index starts at 0; 10-byte output offset starts at 0 and ends at 40 after four iterations (0x0063A750..0x0063A75C); calls only 0..3.

Both callers are engine-side, live, in-scope operations. **The getter is live; its IndexTooLarge and NegativeIndex warnings are not reachable from these callers' intact index loops.** The first warning's format is `Requested LED index is too large (%d > %d). Returning %d.`, arguments original signed index, 3, 3; the comparison is actually index>=4 (equivalent to >3). The second is `LED index should be >= 0, not %d. Using 0.`, original signed index. They are warnings with empty metadata, no errG/break, and clamp the returned pointer to LED 3/0 respectively.

This getter is not a defined exported dynamic symbol; no dynamic relocation targets it, and neither 0x0063956C nor Thumb pointer 0x0063956D occurs as a literal 32-bit word anywhere in the binary. The direct-call scan does not, by itself, prove arbitrary corrupted/computed function pointers impossible; no such caller was found. There is no basis here for requiring normal playback to emit these warnings.

### Current records and UNKNOWN

M4-041's current title is **“BlendAnimWithCurLights body, the StopLignAnim.Result serializer and the GetLEDState callers”**, status **RECOVERABLE_GAP**. Current evidence: `re-analysis/research/20261012-m3m4-settling-2.md: PlayLightAnim blend body (unread); StopLignAnim.Result current-state serializer (0x00639300); ActiveObject::GetLEDState IndexTooLarge/NegativeIndex (0x0063958E/0x006395CC) and its callers.` Those three requested reads are supplied above; this is not a fidelity-status claim.

M4-018 remains **IMPLEMENTATION_GAP**, title **“CubeLightComponent: layers, PlayLightAnim gates, SetObjectLights/SetLEDs, the SetLights wire group (SetCubeGamma, CubeID, CubeLights, white balance), WakeUp, EnableGameLayerOnly, default-layer anims, the +0x2C4 hold”**. Its current evidence includes `20261011-m3m4-fix-rows.md` §5/§13.2 and the named layer/gate/wire ranges; its unresolved text explicitly hands blend, StopLignAnim.Result and GetLEDState to these reads. No wider M4-018 production-path claim is upgraded.

**UNKNOWN:** actual runtime LED colors, object/animation names and timers without a runtime state; no hardware run was performed. The binary scan bounds direct native callers; fabricated/corrupted function pointers are not treated as a production path. Requested blend arithmetic, serializer structure and direct getter-caller index reachability have no remaining unread decision in these bodies.

## 2. M4-044 — coverage

| Step | What the original does | Primary citation | Record / coverage |
| --- | --- | --- | --- |
| Shared collect | LoadNonConfigData collects once, then animations, groups, cube lights, backpack lights, face animations. Cube directory uses the same WalkAnimationDir as M5. | Engine 0x0051F3B0..0x0051F3CE; 0x0051F5B8..0x0051F5F0 | M4-044, read |
| Directory/scope | `config/engine/lights/cubeLights`, scope 1 via pathToResource; resource root stored at DataPlatform+0x24. | 0x0051F5B8..0x0051F5D8; 0x00520E5A..0x00520E66; 0x0084BE66..0x0084BE7C | M4-044, read |
| Listing | Full paths, recurse=true, suffixes `json`,`bin`; readdir order; DT_REG accepted, DT_DIR recursed except . / ..; opendir null silent. | 0x00520E88..0x00520EFC; table 0x0101FF68..0x0101FF6C; 0x00802D70..0x0080300A; 0x00804200..0x0080422E | M4-044, read |
| Mtime filter | stat failure warns/skips; existing stored mtime >= current, signed, skips; otherwise updates cache before enqueue. | 0x00520F3A..0x00520F9A, 0x00521188..0x005211C4 | M4-044, read |
| Type-2 collection | Callback appends full file string to loader+0x44 map key 2, per-cube mtime cache +0x98. | 0x005241DE..0x0052422C; 0x0051F5CE..0x0051F5D8 | M4-044, read |
| Collection log | One Results info per present map key: count and type. Unordered-map traversal, no sorted key pass. | 0x0051F6A4..0x0051F712 | M4-044, read |
| Dispatch | Submits type-2 vector to DispatchWorker<3>; up to three worker threads plus caller's fourth partition; joins before LoadTime. | 0x0051FE98..0x0051FF0C; 0x00521AFC..0x00521C10; 0x00524656..0x00524674 | M4-044, read |
| File read | readAsJson; only return exactly 1 and !root.empty enter container mutex loader+0x26C; Define return ignored. | 0x00521C30..0x00521CA8 | M4-044, read |
| Reader/error | fopen r, legacy Json::Reader parse collectComments=true; failure error +errG/break, optional reader-errors debug. | 0x0084C01C..0x0084C15E | M4-044, read |
| Root names | getMemberNames; no names error/return 1; >1 warns and chooses first byte-lexicographic name. | 0x00589910..0x005899AA; 0x00589C62..0x00589CAE; 0x008E6B90..0x008E6C62; 0x008E7E4C..0x008E7E8A | M4-044, read |
| Entry schema | Requires pattern and duration_ms; optional canBeOverridden/asBool and patternDebugName/asString; default override=true, name=animationName+decimal index. | 0x00589B4A..0x00589BE0; 0x00589A68..0x00589B48 | M4-044, read |
| Pattern schema | Two colors, four period arrays, offset array and rotationPeriod_ms; mandatory despite Optional helper names. All seven helpers run before combined result. | 0x00589308..0x005894CA | M4-044, read |
| Color conversion/error | Four outer elements; each component asUInt→uint8; packed RGBA uint32. Wrong outer size logs DiffSizes +errG/break; absent key false silently. | 0x00589578..0x005896C8; 0x0083F49C..0x0083F4AA | M4-044, read |
| Period/offset conversion | Four-element arrays; uint32 periods / int32 offsets; null/non-array/wrong length false silently. Scalars use shipped asUInt/asInt. | 0x00589770..0x005897F6; 0x008400C4..0x008400CA; 0x008E89B8..0x008E8D1A; 0x008E60E8..0x008E644E | M4-044, read |
| InvalidJson/Pattern | Errors name animation root key, not path; log first, errG=1/break, stop at first invalid entry, clear temporary list, return 1. | 0x00589A0A..0x00589A66; 0x00589BF4..0x00589C60; 0x00589CD2..0x00589CE0 | M4-044, read |
| Successful/duplicate commit | Assign temporary list to container[name]; replaces duplicate, no duplicate warning; empty selected value can replace with empty list; return 0. | 0x00589CB6..0x00589CD4 | M4-044, read |
| Typed exceptions | Wrong JSON kinds/conversion ranges throw Json::LogicError; no catch converting those into InvalidJson/InvalidPattern in file/container bodies. | 0x008E6B9A..0x008E6BA0, 0x008E6CF4..0x008E6D06; numeric accessor bodies cited above | M4-044, read boundary |

### Discovery, ordering and file read

The directory is relative to **resource scope 1**, not the current directory or cache scope. WalkAnimationDir takes full paths from the shared FileUtils function, recursively. Suffix matching is case-sensitive `strcmp(filename+length-suffixLength,suffix)`; suffixes are the literal **`json` and `bin` without a dot**. Consequently `foojson` qualifies, `.JSON` does not, and `.bin` files also go through the JSON reader. It uses `dirent.d_type`: regular (8), directory (4), other types ignored; no fallback stat classification for DT_UNKNOWN/symlinks. stat is separately used for mtime after listing. Null opendir yields an empty list without a warning.

Directory traversal is unsorted readdir order. At a directory entry it recursively appends that subtree's results before resuming the parent scan. There is no ordinal filename sort. The type-2 callback at 0x005241DE appends to the same loader+0x44 collection used by the M5 animation loaders; cube-light mtime state is independently loader+0x98. M5's other type buckets do not substitute for type 2. For each collected type, channel `Animations`, key `RobotDataLoader.CollectAnimFiles.Results`, format `Found %zu animation files of type %d`, arguments uint32 size_t count and int32 type, empty metadata. Cross-type result log order is the unordered-map's current chain order, not sorted numeric type.

For a listed file, stat==-1 logs warning `RobotDataLoader.WalkAnimationDir`, `could not get mtime for %s`, full path, empty metadata, and skips it (0x00520F3A..0x00520F9A); no errG. Stored mtime and stat's mtime seconds are compared as signed 32-bit long; old>=new skips. Otherwise cache is updated **before reading/parsing** and file is appended. Therefore parse failure does not roll back mtime or force an immediate retry of an unchanged file.

LoadCubeLightAnimations queues the vector in its existing order, then DispatchWorker<3>::Process partitions N jobs into **four** contiguous ranges: quotient=floor(N/4), remainder=N%4; first remainder ranges get one extra. Up to three ranges run on new threads, the final range runs on the calling loading thread; for N<4 only the nonempty early worker ranges are created. Each range executes its jobs sequentially. Process joins every created thread and clears the pending job vector. Collection order is not a promise about completion order.

Each file is read outside the container lock, into a null-initialized Json::Value. readAsJson opens mode `r` and invokes the shipped legacy Json::Reader with collectComments=true; caller tests return==1, then root.empty(). Null/empty object/empty array are empty; scalar values, including an empty string, are not empty (0x008E9FA8..0x008E9FD6). Only a successful nonempty result takes loader+0x26C mutex, DefineFromJson on container loader+0x5C, then unlock. Its result is ignored. Thus the loader silently skips a syntactically valid `{}` before the container's EmptyFile error can run. Nonempty wrong-root-type data can instead throw during getMemberNames.

The reader is the shipped Json::Reader, not a modern reader with trailing-comma options. Its array comma branch loops back to readValue; a closing bracket there is not an element, so an array trailing comma fails (0x008E1274..0x008E13F2). Object trailing comma similarly returns to the member-name read with the previous nonempty name and reports missing member name; normal closing brace is handled after a value (0x008E0F78..0x008E0FA6, 0x008E10F6..0x008E11C0). Comments are accepted by the Reader's default feature setup (0x008E074A..0x008E0770) and handled in its token loops. The reader's ordinary object assignment overwrites repeated JSON keys via resolveReference; distinct top-level animation keys are a different case handled below.

Read/parse failure produces `DataPlatform.readAsJson` error, value `Failed to read [%s]`, full path, empty metadata, **then** errG=1 and conditional sDebugBreakOnError (0x0084C0E0..0x0084C122). If getFormattedErrorMessages is nonempty, afterwards debug channel `Unnamed`, same key, value `Json reader errors [%s]` (0x0084C12A..0x0084C15E). Reader diagnostics include their generated multiline details. The file callback does not define partially parsed data when readAsJson returns false. After all jobs/join, info channel `Animations`, key `RobotDataLoader.LoadCubeLightAnimations.LoadTime`, value `Time to load cube light animations = %.2f ms`, double elapsed milliseconds, empty metadata (0x0051FE24..0x0051FE32; 0x0051FEEA..0x0051FF0C).

### Container and per-pattern conversion

0x00589910 is the exported **DefineFromJson** body; `ParseJsonToAnim` occurs in its log names. It gets root member names in JsonCpp's ordered key tree, byte-lexicographic comparison (`memcmp` common prefix, shorter name first, 0x008E7E4C..0x008E7E8A). With multiple names it uses only the first, regardless of textual JSON order. That root key is the animation name; filename does not define the name.

It reads size() of root[name], then entry indices 0..size-1. There is no explicit isArray test for the animation value before indexing. Size zero succeeds with an empty list; a wrong nonempty kind can throw. Each entry's temporary LightPattern starts with zeroed data, duration 0, override byte true. Missing `pattern` or `duration_ms` takes InvalidJson. A present `pattern` is passed to ParseJsonToPattern; false takes InvalidPattern.

These pattern fields are mandatory. Their extraction order and output offsets relative to LightPattern are:

| Field | Conversion / requirement | Destination |
| --- | --- | --- |
| onColors | Outer size=4; four components per indexed entry via asUInt, each narrowed to uint8. Pack as R<<24 \| G<<16 \| B<<8 \| A. | uint32[4], +0x0C |
| offColors | Same | uint32[4], +0x1C |
| onPeriod_ms | Non-null array, exactly 4; asUInt each | uint32[4], +0x2C |
| offPeriod_ms | Same | uint32[4], +0x3C |
| transitionOnPeriod_ms | Same | uint32[4], +0x4C |
| transitionOffPeriod_ms | Same | uint32[4], +0x5C |
| offset | Non-null array, exactly 4; asInt each | int32[4], +0x6C |
| rotationPeriod_ms | Member must exist; asUInt | uint32, +0x7C |

The two color helpers receive **false** for their normalized-float parameter, so their alternative float ColorRGBA constructor is not used. Native uxtb instructions at 0x00589656..0x00589660 discard high bits after asUInt; values like 256 become 0, rather than clamp to 255. ColorRGBA byte stores are R,G,B,A at +0..+3 (0x0083F49C..0x0083F4AA); REV at 0x00589668 produces the packed uint32 pattern representation. The helper checks outer size only, not each inner array's length: missing inner components/null entries can become zero via const indexing and asUInt(null). Wrong kinds may throw rather than return false.

The seven array/color helpers are called in order even if an earlier returns false, and their flags are ANDed with the presence of rotationPeriod_ms. They are not short-circuited as a sequence. On combined failure ParseJsonToPattern returns false **without copying** its scratch result to the destination. On success it copies 0x75 bytes to +0x0C (arrays, rotation, relative-mode byte=0), zeroes the two 32-bit relative-coordinate slots at +0x84/+0x88, returns true (0x005894A4..0x005894CA). It does not read relative-mode/relative-coordinate JSON members.

Outer entry additions, after successful pattern parse:

- `duration_ms`: asUInt to LightPattern+0x8C; zero has no special loader rejection.
- `canBeOverridden`: optional asBool to +0x90; absent remains true. Null becomes false; numbers use nonzero, booleans their value. Strings/arrays/objects throw; it is not bool-only validation.
- `patternDebugName`: optional asString to the leading string at +0. Otherwise the name is **animationName concatenated directly with signed decimal zero-based entry index**, e.g. `WakeUp0`, no underscore/delimiter (0x00589A68..0x00589A8A). asString permits scalar string/number/bool/null conversions and rejects containers; no extra source-fidelity assumption is made about a replacement JSON library's formatting.

Array scalar GetValue<uint32>/GetValue<int32> tail-call shipped asUInt/asInt (0x008400C4..0x008400CA). Both map null→0 and bool→0/1. Integers must fit the target range. Finite real values in [0,4294967295] / [-2147483648,2147483647] use native `vcvt.u32.f64` / `vcvt.s32.f64` truncation toward zero (0x008E8AA0..0x008E8AC4; 0x008E61D0..0x008E61F8). Strings and containers throw. No saturating/default-on-error conversion is present. The numeric accessor error strings are `LargestInt out of UInt range`, `LargestUInt out of UInt range`, `double out of UInt range`, `Value is not convertible to UInt.` and corresponding `Int` versions. They go through Json::throwLogicError, not sErrorF/errG.

### Error rows, duplicates and retained state

All explicit rows below use empty metadata. The `%s` on container errors is the **animation root key**, not full path:

| Gate | Level/key | Exact format and arguments | Order/result |
| --- | --- | --- | --- |
| No root member names | Error `CubeLightAnimationContainer.DefineFromJson.EmptyFile` | `Found no animations in JSON`; no args | 0x00589C74; then errG=1, conditional break, return 1; no map write |
| More than one root name | Warning `CubeLightAnimationContainer.DefineFromJson.TooManyAnims` | `Expecting only one animation per json file, found %lu. Will use first: %s`; uint32 count, first root name | 0x00589968; no errG, continues first only |
| Present color member has size !=4 | Error `JsonTools.GetColorValuesToArrayOptional.DiffSizes` | `The json array and destination array are different sizes`; no args | 0x00589688; then errG=1, conditional break, helper false. Both color helpers can emit independently before InvalidPattern |
| Entry lacks pattern/duration_ms | Error `CubeLightAnimationContainer.ParseJsonToAnim.InvalidJson` | `Missing member field in %s json file`; root name | 0x00589C10; then errG=1, conditional break, stops entry loop, clears temporary list, return 1 |
| ParseJsonToPattern false | Error `CubeLightAnimationContainer.ParseJsonToAnim.InvalidPattern` | `Missing member field in one of the pattern definitions in %s json file`; root name | 0x00589A2C; then errG=1, conditional break, same temporary-list discard/return 1 |

An absent color field produces helper false silently. A period/offset field that is null, non-array or has length !=4 also produces false silently. Missing rotation gives false. These later produce InvalidPattern in the outer body, not a separate “bad period size” log. Type errors during isMember/index/asUInt/asInt/asBool/asString are distinct: Json::LogicError propagates; there is no exception catch turning them into either Invalid* log or return 1. getMemberNames accepts only null/object, otherwise throws `in Json::Value::getMemberNames(), value must be objectValue` (0x008E6B9A..0x008E6BA0; 0x008E6CF4..0x008E6D06). Ordinary lookup/index conversions similarly do not invent missing-member recovery for the wrong JSON type. The file callback's mutex is an explicit lock/Define/unlock sequence; it does not supply a scoped exception-recovery handler.

Patterns are assembled into a **temporary list**. First InvalidJson/InvalidPattern aborts the animation, clears all valid earlier temporary entries and never reaches `container[name]`. Any existing animation of that name is retained intact; a new name is not inserted. Earlier successful files remain in the container. No global clear occurs at entry.

When the entire selected animation succeeds, `container[name]` is accessed and its list is **assigned/replaced**, including when the selected value has size zero. Duplicate name has **no warning/error** and does not append to the old animation. Mutex acquisition serializes container writes, but reading is concurrent; across files the final duplicate is the **last successful assignment to acquire/complete under the lock**, which cannot be inferred from sorted names or readdir submission alone. Failed duplicates do not replace successful prior values. This concurrency is an observed original behavior, not a deterministic “last filename wins” policy.

### Current claim and UNKNOWN

Current M4-044 title: **“The cube-light animation loader (CubeLightAnimations / ParseJsonToAnim)”**, status **RECOVERABLE_GAP**. Evidence: `re-analysis/research/20261012-m3m4-settling-2.md: ParseJsonToAnim 0x00589910..0x00589CD8 and its loader; the stack (Lights.cs:578-625) still uses System.Text.Json with trailing commas, ordinal file order and *.json only.` The source loader, conversion/error gates and commit/retention behavior are supplied here. No manifest/inventory or implementation changes were made.

**UNKNOWN:** a particular filesystem's readdir order and concurrent-file completion/duplicate winner cannot be fixed statically; they are runtime inputs/scheduling. Actual file parse-error line/column diagnostics depend on its bytes. Full generic JsonCpp numeric-to-string formatting, diagnostic formatting and thread-creation/allocation exception unwinding are library boundaries, not claimed recovered in bulk by these bounded reads. They are not silently replaced with a plausible serializer or JSON parser. The requested loader selection, normal conversions, explicit log/error paths and success-versus-reported-failure retention are traced above.

## Provenance

Answers `re-analysis/research/requests/20261012-m4-cube-lights.md`, under research README/extractor rules after reading PROJECT_STATE.md, current M4-018/M4-041/M4-044 and the requested settling/fix-row notes. Native citations are engine ELF virtual addresses, Thumb low bits cleared for instruction addresses. Ghidra was navigation only; cited bodies, literals, PLT imports and caller branches were checked locally in `resources/lib/armeabi-v7a/libcozmoEngine.so` (SHA-256 `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`). Observed shared-checkout HEAD during this read: `36ab044546dcf5349f5baadd7f286f48e6dad738`.

Local reproducibility: `20261012-m4-cube-lights-disasm.py`, `20261012-m4-cube-lights-native.txt`, and the focused text dumps beside this report. `20261012-m4-cube-lights-call-scan.py` regenerates `20261012-m4-cube-lights-led-callers.txt`. Instruction dumps can decode literal pools after function tails; those are not instruction evidence. All files were read locally; no shipped content was uploaded or endpoint contacted. Writes stayed in research. No branches, worktrees, commits, builds/tests or hardware operations. Coverage is a description of this read, not a fidelity classification or approval.
