# EMU-SAFETY-1

Base: `a9dbec14`. Scope is the embedded emulator follow-up in `REVIEW.md` and
`re-analysis/research/20261104-emu-runtime-claude-check.md`.

## Claims and evidence

- **Execution and lifetime.** `ArmMachine.Call` and `NestedCall` enter the vendored Unicorn 2.1.4
  `uc_emu_start`. The native implementation shares the instruction counter and timer across nested
  starts (`uc.c`, 1128–1181). The stack watchdog and terminal machine failure are host containment
  under M1-034. The active guest entry and PC identify the timeout. Native cleanup follows the
  machine owner. These are host implementation choices with no original Cozmo engine addresses.
- **Acapela atoi.** `libacattsandroid.so` calls PLT `0x0001FBB4` (GOT `0x0001E4F8C`), for example
  at `0x00039C6C`. Android supplies libc. Bionic implements atoi with `(int)strtol(..., 10)`.
  On ARM32, values beyond signed long limits saturate and set ERANGE (34). In-range conversions
  leave errno intact. Source: bionic `strtol.cpp`, revision `9dc37b9439063df2b0db6d1b3a37b491bc3cd427`.
- **Acapela sscanf.** PLT `0x0001FBCC` (GOT `0x0001E4F94`), for example call `0x0003A72A`.
  Bionic `libc/stdio/vfscanf.cpp`, revision `74866e160dd3b0d31e31befb36f334d4acc6e567`,
  directive loop 128–188 accepts width and suppression in either order. The input-failure tail
  728–733 returns EOF when no assignment counts. Suppression and `%n` do not count. The narrow
  `%c` body at 382–415 accepts a short nonempty read.
- The Acapela addresses above are ELF-relative. The stack maps this library at `0x30000000`.

## MISSING and scope boundary

- The watchdog bounds guest execution and checks on return from managed imports. It cannot preempt
  an import that blocks indefinitely in managed code.
- The scanf parser is a subset. Wider C format and numeric grammar are outside this job.
- Java speech ordering, bionic qsort ties, and the seeded 48-bit RNG remain in COZMO-VOICE-1a.
- No record status, approval, or hardware claim changes.

Reader request was gpt-6.1-sol medium. Builder and reviewer use the configured default.
Actual backends are unobserved. One independent review return corrected nested fault propagation. The manager's pass also guarded hook registration after disposal.

## Changed files and test reach

- ArmMachine.cs owns the shared deadline (five-minute default, configurable), guest failure latch,
  and native lifetime. The native instruction count remains nonaggregate across nested starts.
- AcapelaNative.cs and ScanfSubset.cs implement the bounded libc corrections above.
- EmulationSafetyTests.cs runs real ARM loops, nested ARM calls through managed imports, failure
  propagation through three levels, and disposal/finalization. It also invokes the shipped
  Acapela PLT entries and checks errno and destination memory. Standalone scanner cases distinguish
  input failure from a mismatch and cover suppression, width, and short character reads.
- The unchanged Acapela and OKAO oracles exercised the local shipped libraries and fixtures.

Batch full gate on 4487b1fa failed (7801/7803, zero skipped). The old stock-detector availability fixture was corrected and its follow-up passed 61/61. The identical SolvePnP oracle mismatch also reproduces on origin/main a9dbec14. The corrected gate ran on 601b4475 after rebasing onto 1771234b. All 7803 tests passed with zero failures or skips. This bounded handoff is READY FOR CLAUDE.

Focused 21/21 on the reviewed changes. Final combined run on faecca51 passed 28/28, including both bit-exact oracles. Synthesis took about 11 seconds with the watchdog on this machine.
