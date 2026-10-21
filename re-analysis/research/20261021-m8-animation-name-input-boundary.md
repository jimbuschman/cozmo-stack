# R-M8-NAME-INPUT — animation completion name boundary

Request: `requests/20261021-m8-animation-name-input-boundary.md`. Examined HEAD5f06c9ab. Independent read-only research pending checking. No code/test, manifest or inventory change.

## Coverage table

| Boundary | Source | Result |
| --- | --- | --- |
| Play name input | Adopted play extraction C8/P2/P3,0x00543C44..0x00543C6C,0x00543F3A..0x00543F7E | Copies caller byte string; conditional custom-song remap |
| Trigger name input | Adopted T1/T8/T10,0x00544244..0x00544276,0x00544462..0x00544498 | Starts empty; selected group entry becomes action+0x78; not trigger/group/debug name |
| Actual loaded name | M5 loader L1/L6/L7; binary field0, JSON first member | Internal clip name, not filename stem |
| Shipped name census | Both actual loader directories,293 files fully parsed | 997 unique definitions,13..56 bytes, all ASCII |
| Group-selected names | 507 shipped group JSON files,1047 entries | 884 unique references,13..56 bytes, all ASCII |
| Generated app boundary | AnimationCompleted.cs / PlayAnimation.cs | .NET strings, UTF-8 bytes; property setter rejects >255; input unpack is byte-prefixed |
| Current C# | FlatTable.String, JsonClipLoader, public Play/PlayTracked, trigger _clip | UTF-16 host strings; loader UTF-8 decode; completion typed encoding still absent |
| Native over255 / non-UTF8 reach | Shipped corpus establishes neither; caller std::string unconstrained | Native production occurrence UNKNOWN; public arbitrary clip names not limited by shipped census |
| API/policy | No native constructor validation recovered | No new length/encoding rejection or exclusion inferred |

## Result

**The shipped canned/group name corpus does not exercise either disputed boundary.** Every recovered loaded name is ASCII and at most56 bytes, so UTF-8 round-trip preserves the name bytes exactly for this corpus. This narrows the previous report's asset-length UNKNOWN.

It does not constrain arbitrary native std::string inputs or public C# custom clips. The generated Unity serializer's255-byte setter is a boundary of that app class, not a validation rule in PlayAnimationAction, its completion getter, or the native completion packer. No new policy is selected.

## Name producers, without repeating payload extraction

Reuse `20261012-m8-play-animation-actions.md` and `20261021-m8-animation-completion-payload.md`, plus partial independent check `20261021-m8-animation-completion-check.md`.

- PlayAnimationAction constructor stores a separate copy of caller animationName at+0x78 (C8,0x00543C44..0x00543C6C). Base debug name is PlayAnimation concatenated with the input, at+0x48; it is not the completion source. No length/UTF-8 validation is established at this copy.
- Init with nonnull cached Animation+0x90 skips comparison/lookup. Otherwise the special string cozmo_sings_custom is replaced in+0x78 with anim_cozmosings_120_song_01 (P2,0x00543F3A..0x00543F6C). The custom-song producer/runtime is M9-029's approved SDK exclusion; it is documented here as a native writer, not newly supported.
- TriggerAnimationAction initially constructs its Play base with empty animationName. It maps enum trigger to a group, and each nonempty-group Init selects an animation name from the group and copies it into+0x78 (T8,0x00544462..0x0054448E), then calls base Init. Failed/empty choice leaves the actual sampled name as the reported value on that lifecycle branch. Re-Init selection and cached animation identity are distinct; the previous extraction's retry limits remain.
- Selected group entry names originate in shipped group JSON Animations[].Name. Filename stems name groups, not the selected clips. Weight/mood/cooldown choice is reused M5 evidence; no RNG re-extraction.
- Concrete literal callers, including sleep and internal Play animation creation, supply ordinary std::string values to the same copy. The actual caller-literal census outside the selected supported Play/Trigger paths is not expanded into a whole engine string audit.
- Completion samples the current+0x78 independent of result/cached animation pointer; packing/copies/Prep/callbacks are already the preceding reports' scope and were not re-extracted.

## Loader naming rules and corpus

Reuse adopted M5 loader L1/L2/L6/L7 in `20261011-m5-fix-rows.md`, and M5-X9 follow-up:
- Native CollectAnimFiles walks **assets/animations/** and **config/engine/animations/** (0x0051F4E4..0x0051F5A6), recursively accepting json/bin (0x00520E66..0x00520F70).
- Binary AllClips field0 is a vector; each clip's name is its own field0, not its path stem. L6/L7 at0x0052151E..0x00521592.
- JSON uses getMemberNames and the first member, warning if more than one (0x00588700..0x00588766). JsonCpp's byte-key order is relevant; census sorts keys by their UTF-8 bytes, not filename.
- Reserved _PROCEDURAL_ is skipped; duplicate definitions use the existing first-definition-wins rule. This corpus has neither a reserved name nor duplicate names, so native loader thread order cannot change this census.

Fresh instruction spot reads0x0052151E..0x00521578 and0x00588700..0x00588776 agree with the adopted naming/copy boundaries. Full loader recovery is reused rather than claimed freshly re-read. Source-backed naming precedes counting.

Actual files:
`re-analysis/obb/assets/cozmo_resources/assets/animations/`
and
`re-analysis/obb/assets/cozmo_resources/config/engine/animations/`.

| Asset category | Files read | Definition count | Unique names | Length |
| --- | ---: | ---: | ---: | --- |
| Binary canned files | 289 | 993 | 993 | Included in combined range below |
| JSON canned files | 4 | 4 | 4 | Included in combined range below |
| Combined loader corpus | 293 | 997 | 997 | Minimum13, maximum56 bytes |
| Group references, assets/animationGroups | 507 | 1047 reference entries | 884 | Minimum13, maximum56 bytes |

All files in those scopes were read fully. Binary strings were counted as **raw bytes before decoding**, using little-endian FlatBuffer root/vector/table/string offsets; JSON names were decoded as JSON keys then UTF-8 encoded. Zero parse errors, zero reserved names, zero duplicate loaded names, zero non-ASCII bytes, zero embedded NULs, zero lengths>255. Group reference count is a separate census, not an additional997 loaded clips or proof every reference resolves/runs. Groups were parsed by Animations[].Name; zero parse errors/non-ASCII/over255 entries.

Longest names:

| Bytes | Name |
| ---: | --- |
| 56 | anim_lookinplaceforfaces_keepalive_medium_head_angle_-20 |
| 55 | anim_lookinplaceforfaces_keepalive_medium_head_angle_20 |
| 55 | anim_lookinplaceforfaces_keepalive_medium_head_angle_40 |
| 55 | anim_lookinplaceforfaces_keepalive_short_head_angle_-20 |

The56-byte name also appears in group references. A corpus with external/custom directories, future asset revisions, SDK cache definitions, dynamically constructed clips or face-animation directory names is outside these997. Face names are not substituted for canned animation action names. No claim is made that every counted clip loads successfully past every keyframe/failure branch; the census is the actual names presented by these file parsers.

Reproduction recipe: recurse the two native directories for bin/json; for bin read root uoffset, vtable field0 vector, each vector element table and its field0 length-prefixed raw string; for JSON parse object and select first key in byte order; drop reserved _PROCEDURAL_; group by raw name bytes and measure len. The three distinct comparisons (raw byte name, display string, file path) must remain separate.

## Native byte strings versus the app's UTF-8 strings

Native std::string is a length-bearing byte sequence. The recovered constructor/name getter does not establish UTF-8 validation. This is not proof a supported native producer ever supplies invalid UTF-8; it means arbitrary byte fidelity cannot be inferred from the type of a C# string.

`unity/scripts/csharp/Anki.Cozmo/AnimationCompleted.cs`:
- property setter line20 uses Encoding.UTF8.GetByteCount and throws above255;
- Unpack lines73–77 reads a u8 count and exactly that many bytes, then directly assigns UTF8.GetString to the backing field;
- Pack lines86–92 encodes the backing field as UTF-8 and writes byte count plus bytes.

`unity/scripts/csharp/Anki.Cozmo.ExternalInterface/PlayAnimation.cs`:
- setter line40 likewise rejects UTF-8 byte counts>255;
- Unpack lines141–145 takes u8 count and UTF8-decodes directly;
- Pack lines157–163 uses Encoding.UTF8.GetBytes and byte length.

Thus ordinary generated app input is UTF-8 of at most255 encoded bytes through its property setter. Direct incoming byte unpack can decode invalid bytes using .NET's default fallback; it bypasses that setter. This does not turn the native input string into a validated Unicode type. It also does not constrain engine-internal callers.

The preceding partial check establishes native completion length256→0 name bytes and257→first byte, **not** clamp255. This report adds no opposite rejection. Native serializer byte truncation and generated app property validation are different boundaries with different directions.

## Current C# input conversion and reach

Under `cozmo-stack/src/Cozmo.Robot/Animation/`:
- FlatBufferReader.FlatTable.String decodes the entire raw length with Encoding.UTF8.GetString. Invalid bytes would be replaced by default fallback; raw bytes are not retained. For the997 ASCII names this conversion is exact.
- JsonClipLoader selects the first key as .NET string; JSON string AsString uses GetString, scalar fallback round-trips repo Json.AsStringBytes through UTF-8. Group/library names are host strings.
- TriggerLiftSafeAnimationAction.Init stores Library.GetAnimationNameFromGroup into _clip, then PlayTracked(_clip). It reports missing completion string/lifecycle semantics; it has no typed completion getter. Group-selected shipped bytes remain within the measured bound.
- CozmoAnimations.Play(string)/PlayTracked(string) perform lookup and the approved custom-song exclusion. They do not enforce255 bytes. Play(AnimationClip)/PlayTracked(AnimationClip) accept clips built in memory; AnimationClip.Name is a public init string without a length gate.
- These public playback APIs schedule a host animation directly. They are not automatically an IActionRunner with a source-backed animationCompleted payload. A custom overlength host clip name therefore demonstrates an unconstrained host input shape, not a demonstrated native+0x78 producer or working completion-wire behavior.
- Current RobotCompletedAction still has the previously reported scalar completion carrier. There is no current string completion encoding implementation to certify. Choosing UTF8.GetBytes for a future typed carrier preserves this ASCII corpus; behavior for arbitrary raw native names needs a deliberate representation/input decision.

## Boundary examples and remaining choices

| Input | Byte fact / established boundary |
| --- | --- |
| Shipped longest name above | 56 ASCII bytes; host UTF-8 encoding unchanged |
| é | One Unicode character, UTF-8 C3 A9 (2 bytes); do not use UTF-16 Length as wire length |
| 255 ASCII a bytes | Generated app setter permits; native completion length255 and255 bytes |
| 256 ASCII a bytes | Generated app setter rejects; native completion pack behavior remains length0/no bytes if such native input is supplied |
| 257 ASCII a bytes | Generated app setter rejects; native completion pack writes length1/61 if supplied |
| Raw FF | Invalid UTF-8; native byte string can represent it; no supported shipped producer established; host string decode loses that raw byte |

These are independent boundary examples, not assertions that every input reaches a successful supported action. Missing clip lookup/fault paths remain their existing owners, and payload getter behavior must not depend on playback success.

Remaining UNKNOWN/operator/API choices:
1. Any supported engine-internal caller that actually supplies >255 or invalid UTF-8 to action+0x78: not established by this corpus/selected constructors. No rejection inferred.
2. External custom clip/resource input contract: UTF-8-only versus byte-preserving names, allowed length and Unicode fallback. Operator/API checkpoint if constraints change supported behavior; not selected here.
3. Complete native ingress for arbitrary byte PlayAnimation inputs and host IActionRunner binding for public custom clips: outside this bounded name census.
4. Names in other asset versions/custom roots, SDK caches or dynamic runtime producers: not covered by997.
5. Typed completion value/encoding production path remains unbuilt per the checked report. No guarantee fabricated from asset success.

No regression tests or hardware run; census is a read-only data measurement. Current M8-020/021 and M5 loader ownership remain unchanged. The partial completion check is reused, not broadened into approval.
