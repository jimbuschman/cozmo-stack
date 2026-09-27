# X5: M6-020 STMG group items and trailing objects

Scope: the unresolved parts of `M6-020`: state-group item meanings,
switch-group item meanings, and the two trailing STMG object bodies. Primary
source is `resources/lib/armeabi-v7a/libcozmoEngine.so` (ARM mode). The prior
STMG extraction in `.scratch/m6-stmg/report.md` was used as a lead; the cited
instructions below were re-read from the `.so`. All shipped banks have zero
state items and zero trailing-object counts, so those paths are source-backed
but not asset-exercised.

## Result

The item layouts and all behavior that the app can observe are recoverable.
The native binary does not retain human-readable class or member names for the
two trailing object types. Their byte layouts, identity/refcount behavior, raw
copy behavior, and numeric property-code setters are nevertheless exact. Any
semantic names beyond those behaviors remain `BLOCKED_EXTERNAL`; they must not
be invented in the implementation.

The Wwise terms in parentheses below are labels for the recovered behavior,
not recovered C++ identifiers.

## Production-path rows

| step | what the original does | citation | record id or NEW | classification |
|---|---|---|---|---|
| X5-S1 | A state item is three `u32`s passed to the state manager as `(groupId, field0, field1, field2, false)`. The handler finds the group, searches its 12-byte item array for an exact `(field0,field1)` match, and stores field2 at item `+8`. | reader `0x009B0C9C..0x009B0D0C`; handler `0x00A27CA4..0x00A27D60` | M6-020 | EXACT_SOURCE |
| X5-S2 | If the handler's final flag is nonzero it also searches for the reversed pair `(field1,field0)` and writes the same field2; the bank reader always passes zero. Thus the three fields behave as `{fromStateId,toStateId,transitionTimeMs}`. The last name is the Wwise public label; the binary proves only keyed pair plus stored `u32`. | flag load `0x00A27CB0`; forward store `0x00A27D34..0x00A27D60`; reverse search/store `0x00A27D78..0x00A27DD0`; reader writes zero at `sp` before call `0x009B0D04..0x009B0D0C` | M6-020 | EXACT_SOURCE for behavior; semantic labels are external |
| X5-S3 | A switch item is a 12-byte source record. For every item, source word 0 is copied to generated curve point `+0`; `float(itemIndex)` is written at `+4`; source word 2 is copied to `+8`; source word 1 is copied to a parallel `u32` ID array. The curve and ID array, group id, second header field, flags and count are then passed to `0xA12050`. | handler `0x00A325E0..0x00A32914`; source stride `0x00A3274C`; ID copy `0x00A32740..0x00A32744`; point writes `0x00A3275C..0x00A32780`; call `0x00A32644..0x00A3266C` | M6-020 | EXACT_SOURCE |
| X5-S4 | The switch item fields therefore behave as `{binary32 rtpcValue,u32 switchId,u32 interpolation}`. In shipped Init.bnk, word 0 has float values and word 2 is 9 for all items. `rtpcValue`, `switchId`, and `interpolation` are Wwise public labels; the exact implementation requirement is the raw copy and consumer shape in X5-S3. | `0x00A3276C`/`0x00A32778` (word 0), `0x00A32740..0x00A32744` (word 1), `0x00A32770`/`0x00A3277C` (word 2); shipped STMG body offsets 99..305 | M6-020 | EXACT_SOURCE for behavior; semantic labels are external |
| X5-S5 | Trailing count A entries are exactly 56 bytes: `u32 id`, six `u16` slots, then ten `u32` slots. The reader passes the id separately and a pointer to all 56 bytes to `0xA3B84C`. | reader `0x009B0FBC..0x009B127C`; handler `0x00A3B84C..0x00A3B998` | M6-020 | EXACT_SOURCE |
| X5-S6 | A-handler lookup is keyed by the first `u32`. On an existing object it increments refcount at node `+0x0c`. Otherwise it allocates 0x48 bytes, initializes the common base, assigns the A vtable, and copies all 56 input bytes byte-for-byte to object `+0x10`. | lookup/refcount `0x00A3B93C..0x00A3B990`; allocation/init `0x00A3B898..0x00A3B8E0`; copy `0x00A3B8E4..0x00A3B924`; A vtable target formed at `0x00A3B8C4..0x00A3B8DC` | M6-020 | EXACT_SOURCE |
| X5-S7 | A's six small properties have numeric codes 0..5: codes 0,1,2 canonicalize nonzero to byte 1 and write object body offsets `+4,+5,+6`; codes 3,4,5 write `u16` at body `+8,+10,+12`. Its ten large properties have codes 6..15 and write `u32` at body offsets `+16,+20,+24,+40,+44,+48,+28,+32,+36,+52` respectively. | `0x00A3B180..0x00A3B1E8` (codes 0..5); `0x00A3B0F8..0x00A3B17C` (codes 6..15); body base is object `+0x10` from X5-S6 | M6-020 | EXACT_SOURCE; property names BLOCKED_EXTERNAL |
| X5-S8 | Trailing count B entries are exactly 40 bytes: `u32 id` followed by nine `u32` slots. The reader passes the id separately and a pointer to all 40 bytes to `0xA3BA44`. | reader `0x009B12A4..0x009B1410`; handler `0x00A3BA44..0x00A3BB80` | M6-020 | EXACT_SOURCE |
| X5-S9 | B-handler lookup is also keyed by the first `u32` and increments the same refcount for an existing object. Otherwise it allocates 0x38 bytes, initializes the common base, assigns the B vtable, and copies all 40 input bytes byte-for-byte to object `+0x10`. | lookup/refcount `0x00A3BB24..0x00A3BB78`; allocation/init `0x00A3BA90..0x00A3BAD8`; copy `0x00A3BADC..0x00A3BB0C`; B vtable target formed at `0x00A3BABC..0x00A3BAD4` | M6-020 | EXACT_SOURCE |
| X5-S10 | B's nine properties use numeric codes 0x10..0x18. Codes 0x10..0x15 write body offsets `+4,+8,+12,+16,+20,+24`; code 0x16 is handled by a separate setter and writes `+28`; code 0x17 writes `+32`; code 0x18 writes `+36`. | `0x00A3B1EC..0x00A3B25C`; separate code-0x16 store `0x00A3B260..0x00A3B268`; body base is object `+0x10` from X5-S9 | M6-020 | EXACT_SOURCE; property names BLOCKED_EXTERNAL |
| X5-S11 | The two trailing counts are not refusal sentinels. A nonzero count runs the corresponding per-entry reader and handler. Both counts are zero in every shipped bank inspected, so these paths are not live for shipped assets. | A loop `0x009B0FDC..0x009B1298`; B loop `0x009B12C0..0x009B142C`; shipped Init.bnk STMG body offsets 1087 and 1091 are zero | M6-020 | EXACT_SOURCE; `live_path=false` for shipped assets |

## Exact raw layouts

Offsets are relative to the start of each serialized body and are preserved
unchanged in the allocated object's body at object `+0x10`.

### State item

| offset | width | exact behavior | public label only |
|---:|---:|---|---|
| 0 | 4 | first key of ordered pair | from-state id |
| 4 | 4 | second key of ordered pair | to-state id |
| 8 | 4 | value stored for matching pair | transition duration in ms |

### Switch item

| offset | width | exact behavior | public label only |
|---:|---:|---|---|
| 0 | 4 | copied as curve point word 0 | RTPC binary32 value |
| 4 | 4 | copied to parallel ID array | switch id |
| 8 | 4 | copied as curve point word 2 | interpolation |

### Trailing A

`u32[0]` at offset 0 is the object key/id. Serialized offsets 4..15 are six
`u16`s; offsets 16..55 are ten `u32`s. The direct copy is normative. The
setter mapping in X5-S7 additionally proves the first three `u16` slots are
observed as booleans through that interface, but the serialized copy does not
normalize them.

### Trailing B

Ten `u32`s at offsets 0..39. Word 0 is the object key/id; the remaining nine
words are exposed through the numeric property-code behavior in X5-S10. The
direct copy is normative.

## Existing record judgement

Current `M6-020` is titled “STMG: the group-item field meanings and the two
trailing bodies (unread source; unexercised by shipped banks)” and is
`RECOVERABLE_GAP`. Its evidence cites R6/R7, `0xA27CA4`, `0xA325E0`, and the
absence of Wwise field names. Its uncertainty is now split as follows:

- The serialized layouts, pair/curve behavior, object identity, raw copies,
  and numeric property-code mappings are all settled by X5-S1..X5-S11.
- The record should remain `IMPLEMENTATION_GAP` until B1 builds the settled
  byte behavior.
- Human-readable names for the trailing object types and their property codes
  are absent from the stripped app binary and all shipped counts are zero.
  Those names are `BLOCKED_EXTERNAL`, not a reason to guess or to hold the raw
  behavior as recoverable.
- `live_path` remains false: no shipped bank exercises state items or either
  trailing body. Switch items are present in Init.bnk and exercise X5-S3.

No other manifest record claims these unresolved parts. The surrounding STMG
reader remains owned by `M6-001`; X5 does not change its already recovered
field order or error propagation.

## Contradictions and corrections

The prior scratch report correctly rejected the old note that nonzero trailing
counts are refused. X5-S5..X5-S11 confirm that the original reads and registers
them. An implementation which rejects a nonzero count is contradicted by the
native path.

The prior scratch report left all four meanings unknown. The app binary settles
their observable semantics, but not every human-readable Wwise identifier.
Those two questions must remain distinct in the inventory.

## Open questions

1. No shipped asset supplies a nonzero state-item or trailing-object body, so
   asset bytes cannot supply the stripped property names.
2. Wwise SDK headers or symbols outside the shipped APK/OBB may name the two
   trailing object classes and numeric properties. That is external evidence;
   it cannot strengthen the app-source provenance of their already recovered
   byte behavior.
3. B1 should model the trailing bodies as raw, width-correct fields unless a
   primary shipped source supplies names before implementation. It must not
   invent names from the offsets or numeric property codes.

## Reproduction

The relevant ranges can be reopened directly with:

```text
python re-analysis/tools/arm_disasm.py 0xA27CA4 0xA27DE0
python re-analysis/tools/arm_disasm.py 0xA325E0 0xA32918
python re-analysis/tools/arm_disasm.py 0xA3B0F8 0xA3B26C
python re-analysis/tools/arm_disasm.py 0xA3B84C 0xA3B99C
python re-analysis/tools/arm_disasm.py 0xA3BA44 0xA3BB84
```
