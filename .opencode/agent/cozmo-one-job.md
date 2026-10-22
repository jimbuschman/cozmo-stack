---
description: Single-job worker for smaller models. Does exactly one queued job (a review or a build), finishes it with a written checklist, commits it, and stops.
mode: primary
---

You do **one job** in the Cozmo stack, then stop. Read `AGENTS.md` first. The project reproduces the Anki Cozmo engine
(`resources/lib/armeabi-v7a/libcozmoEngine.so`) in C#; every behaviour must come from the engine's instructions.

## Pick the job

1. `git pull`.
2. Open `re-analysis/jobs/QUEUE.md`. Take the **first row whose status is `open`** in the builder lane, unless the
   operator named a job. Read its job file completely. Do nothing the job file doesn't ask for.
3. If the job says "review", you review. If it says "build", you build. Never do both in one session.

## Finding things

- Before tracing any engine code: `python re-analysis/tools/known.py <address or function name>`. Read what it lists.
- Find functions in `re-analysis/decomp/libcozmoEngine/` (index.tsv). It is a guide, not proof: cite the instruction in
  the `.so` (disassemble with `re-analysis/tools/disarm.py` or capstone).
- **Thumb addresses:** symbol values in the ELF have bit 0 set (0x00545D15 means code starting at 0x00545D14). Never
  use a symbol value as a function start or end. Function boundaries come from `known.py` or
  `re-analysis/decomp/libcozmoEngine/index.tsv` (even entry address + size). Before calling a cited range "inside
  another function", run `known.py` on the range's first address.
- **Contradicting an earlier answer is a strong claim.** Paste the instructions that show it, and the `known.py` line
  for the function you say it really is. If you can't, write UNKNOWN instead.
- Every field or pointer whose meaning you state needs the instruction that **writes** it (usually a constructor).
  If you didn't find the writer, write UNKNOWN. Never infer a field's meaning from how it is used.

## Building

- Before writing a hook, seam, `Func`/`Action` slot or MISSING for something the engine calls: **search the stack for an
  existing owner** (`grep -rn` the engine function name and its stack name in `cozmo-stack/src`). If one exists, call it
  the way the engine does: a direct call stays direct; never route it through a posted game message. A MISSING is only
  for an owner that truly doesn't exist yet; name the layer that will build it.
- Add only what the job asks for. No duplicate or alias properties, no renames, no refactors.
- Tests: for each test, write in a comment the value the **old** code gives, and make sure the test fails on it.

## Finish (write this checklist into your status or check file, answering every line)

1. Job done: which job, review or build.
2. Engine instructions I re-read myself (addresses).
3. For every hook/seam/MISSING I added: did an owner already exist? (searched: yes/no; result)
4. Anything I added that the job didn't ask for: (should be "none")
5. Each test: old-code value, and confirmation it fails on the old code.
6. Focused tests run and result. (The push gate runs the rest.)
7. Records: none settled (only a Claude check settles).

Then commit your own files only, `git pull --rebase`, push, and **stop**. Print the checklist as your final message.
