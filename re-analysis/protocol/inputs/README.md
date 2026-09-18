# Builder inputs

`cozmo_robot_protocol.json` is built from these six files. They are committed because two of the extractors
that produced them no longer exist in this repository, so regenerating from the primary artifacts is not
currently possible. Committing the derived intermediates is the alternative: the canonical definition is
reproducible from what is here, byte for byte, and the provenance of each input is recorded below.

Rebuild everything with:

```
python re-analysis/tools/regenerate_protocol.py
```

That runs the builder, the C# generator and the status document in order, then reports whether the result
matches what is committed.

| File | What it holds | Where it came from | Regenerable today |
| --- | --- | --- | --- |
| `robot_tags_official_named.json` | The 161 official tags with their union member and CLAD type names | `tools/robot_tagmap.py` against `libcozmoEngine.so`, disassembling the union `Set_*` setters for their tag constants | yes |
| `native_layouts.json` | Per-message native `Unpack` op sequences, `Size()` results and loop counts | a `native_layouts.py` extractor that is **not in this repository** | **no** |
| `csharp_twins.json` | Field names and types of the decompiled C# classes that mirror each robot message | a `csharp_twins.py` extractor that is **not in this repository** | **no** |
| `csharp_enums.json` | Enum names, base types and members from the same decompiled C# | same missing extractor | **no** |
| `pycozmo_decl.json` | PyCozmo's packet declarations, used for field names only and never for widths | `reference/pycozmo-master-2026-09-18/pycozmo/protocol_declaration.py` | yes, trivially |
| `capture_observed.json` | Payload lengths and counts per tag seen on real hardware | `tools/analyze_frame_log.py` over `captures/*.log` | yes |

The builder also reads `re-analysis/captures/*.log` and `re-analysis/captures/*probe-results*.json` directly,
so hardware evidence flows in without an intermediate step.

## What is missing, and what it would take

`native_layouts.py` and `csharp_twins.py` were written during an earlier session and were never committed.
Rewriting them is a bounded job, not a research one: the first walks each `Unpack` in `libcozmoEngine.so`
with the disassembler already in `tools/disarm.py` and records the read widths, and the second parses the
decompiled C# under `sources/`. Until they exist, treat the three files above as primary inputs. They are
small, human-readable JSON, and the builder validates what it reads from them, so a bad edit fails loudly
rather than silently producing a wrong codec.
