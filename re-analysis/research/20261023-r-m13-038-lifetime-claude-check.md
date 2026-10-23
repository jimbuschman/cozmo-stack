# Claude check: R-M13-038-LIFETIME (row 58, answer 528ea827) — 2026-10-23

Manager spot-check of `research/20261023-m13-038-audio-client-lifetime.md` (Codex), instructions re-read in
libcozmoEngine.so. **PASS.** No record settles.

## Re-read

- W2/U2 (A teardown) 0x005A0D8E..0x005A0D9E: load +0x68, store 0 (r6), then destroy the client's handle vector (+0x1C)
  and delete the client. W5/U3 (B) 0x005A588A..0x005A589C: the same on +0x10.
- O3 (Robot destructor) 0x00511126..0x0051113A: clear Robot+0x44 then destroy A; clear +0x48 then destroy B.
- S4 (broadcaster Update) 0x0064D606..0x0064D610: current unlock compared with S+0, the requested-55 guard, then
  `str r1,[r0]` before publication.
- S9/S12 (spark handler) 0x00592424..0x0059242A: the workout-stage request, then reload music switch (+0xC) and the
  incoming unlock; activation call at 0x00592478.

## Consequence

The answer agrees with the result already adopted for M13-038 (2026-10-16, built in B-M13-8):
- the two clients live until Robot teardown, and nothing replaces them during a connected session;
- a configuration reload calls only the no-op Init (0x00591FC0);
- removal disconnects the slot before deleting the client;
- the repeated activation is A, B, A, under the stated conditions.

So B-M13-18 needs no lifetime handling, and no new build is needed for M13-038's lifetime. The freed-successor read
during a nested teardown stays under M1-034 as already recorded.

Next for M13-038: compare `Behavior/BehaviorAudioClient.cs` and `BehaviorAudioOwners.cs` with this answer (W1-W7,
O1-O10, U1-U9, S1-S13) as a Claude check before any settlement. That is manager work, not a Codex job.
