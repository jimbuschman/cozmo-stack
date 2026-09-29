# M9-024 — note-off envelope lifetime extraction

Read-only extraction from `resources/lib/armeabi-v7a/libcozmoEngine.so`
(3.4.0-1204). Addresses are ELF virtual addresses. The Ghidra export was used
only to navigate; the citations below are ARM instructions opened in the binary.

| step | what the original does | citation | record | classification |
|---|---|---|---|---|
| Event gate | The shared per-voice creator calls the modulator object's virtual `+0x24` method before allocating/reusing a voice. For type 22 that slot is `0x009D552C`. Its second argument contains the four-byte MIDI event at `+4`: status `+4`, channel `+5`, key `+6`, velocity/release velocity `+7`. The same event is subsequently copied into the per-voice object. A false result rejects creation. | call through parent vtable `0x009D7FEC..0x009D7FF8`; event-field copies `0x009E2700..0x009E2728`; type-22 vtable `0x0103B218..0x0103B23C` | M9-024 | EXACT_SOURCE |
| Property 15 meaning | `0x009D552C` reads property 15. Value 1 accepts ordinary starts but rejects event state 3; value 2 accepts MIDI status `0x80`, and status `0x90` only when `event+7 == 0` (the note-on-with-zero-velocity spelling of note-off). Thus property 15 is the event/trigger selector, not the stop-playback switch. The shipped singing envelope's value 2 makes it a note-off-triggered envelope. | property scan and value branches `0x009D552C..0x009D55F3`; shipped object 381606890 has property 15 raw value 2 (Cozmo.bnk) | M9-024 | EXACT_SOURCE |
| Envelope completion | The type-1 envelope evaluator records terminal state 3 in its carry object and its final sample at carry `+8`. On the next manager evaluation the per-voice object copies carry `+4` to `voice+0x44` (unless it is -1) and carry `+8` to `voice+0x4C`. | evaluator completion `0x009E57E8..0x009E57F4`; carry propagation `0x009E2C98..0x009E2CC0` | M9-024 | EXACT_SOURCE |
| Retirement | `0x009E2510` says a per-voice modulator is removable when its parent is null or `voice+0x44 == 3`. The list sweep calls that predicate, unlinks true entries, calls `0x009E21FC`, decrements the reference count, and destroys/frees the object at zero. | predicate `0x009E2510..0x009E2550`; sweep/unlink/cleanup `0x009E2AE4..0x009E2BBC` | M9-024 | EXACT_SOURCE |
| Stop-playback gate | Cleanup first sets `voice+0x44 = 3`, then calls `0x009D7EA4(parent)`. That function reads **property 1** and returns its Boolean value. If false, cleanup only detaches/releases the modulator's references. Therefore completion alone does not unconditionally stop an attached voice. | cleanup gate `0x009E21FC..0x009E2230`; property-1 reader `0x009D7EA4..0x009D7F34` | M9-024 | EXACT_SOURCE |
| Direct attached-voice stop path | When the property-1 gate is true, the creator's saved property-0 value at `voice+0x48` selects cleanup. For value 1, with attached voices at `voice+0x10`/count `+0x14` and no alternate owner at `+0x20`, cleanup calls `0x009F4A64(attachedVoice, voice+0x1C, voice+0x2C, 0, 4)` for every attached voice. The helper builds a command whose final word is 4 and invokes the attached voice's virtual `+0x4C`. The same helper/command-4 form is used by node teardown paths (`0x00A074AC`, `0x00A0759C`, `0x00A07688`, `0x00A07778`, `0x00A07A50`, `0x00A07DDC`), establishing it as the stop/termination command. | branch and loop `0x009E22E4..0x009E232C`, `0x009E2438..0x009E24A0`; command wrapper `0x009F4A64..0x009F4AB0`; teardown callers as listed | M9-024 | EXACT_SOURCE |
| Other ownership modes | A saved property-0 value 0 sends command 4 through `0x00A01280` to `voice+0x0C`. Value 1 with an alternate owner at `voice+0x20` follows the owner/list paths at `0x009E233C..0x009E2434` instead of the direct attached-voice loop. Other property-0 values fall through to detach-only cleanup. | `0x009E22E4..0x009E2434` | M9-024 | EXACT_SOURCE |
| Shipped final answer | Every shipped modulator explicitly stores property 0 as 1, including 381606890, so its applicable cleanup mode is the direct-attached-voice mode when `voice+0x20` is null. However, no shipped modulator explicitly stores property 1. Its runtime default is reached through `DAT_0108DAAC`; this pass did not recover the initializer/value of that default. Consequently the binary proves **the exact conditional stop path**, but does not yet prove whether the shipped note-off envelope takes it. | property-0 reader `0x009D7F3C..0x009D7FB8`; property-1 default load `0x009D7EA4..0x009D7ED0`; shipped bank property bundles | M9-024 | UNKNOWN for the default and therefore RECOVERABLE_GAP for the shipped yes/no answer |

## Contradictions with the current record/code text

- `WwiseModulatorProp.EnvelopeStopPlayback = 15` is contradicted. Property 15 is
  consumed by the pre-creation event predicate and value 2 selects note-off.
  Stop-on-completion is gated by property 1.
- A claim that envelope completion by itself stops playback would be too strong.
  Completion retires the modulator; the voice command is conditional on property 1,
  the saved property-0 mode, and which attachment/owner field is populated.

## Weak evidence

- Calling `0x009F4A64(..., 4)` a stop/termination command is supported by its use
  from six node-teardown paths and by the cleanup context. The symbolic Wwise SDK
  name is not present in the shipped binary.
- Bank parsing proves that property 1 is omitted, but omission is not proof of a
  numeric or Boolean default.

## Open questions

1. Recover the runtime initialization of the modulator-default table at
   `0x0108DAA8`; specifically establish `DAT_0108DAAC` (property 1).
2. If that default is true, confirm on the singing creation path that `voice+0x20`
   is null and the sampled voice pointers occupy `voice+0x10`, making the direct
   `0x009F4A64(..., 4)` loop the taken branch. If it is false, the shipped envelope
   does not stop the attached voice through this cleanup path.
