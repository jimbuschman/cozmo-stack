# Tracked disassembly

## What is kept, and why

The repository does not redistribute the decompiled APK. `resources/`, `sources/`, `smali/` and `unity/`
are all gitignored, and so is the unpacked OBB. The files in this directory are the one exception, so they
are held to a rule: **a function is tracked only while a specific documented claim rests on it.**

`libcozmoEngine.so` exports tens of thousands of symbols. Thirty-four functions appear here. Each one is
named in the manifest in `../tools/regenerate_disassembly.py` together with the claim it supports, so
anything that stops being cited can be found and dropped. `dis_entrypoints.txt` was removed for exactly
that reason: nothing referenced it.

| File | Supports |
| --- | --- |
| `dis_robotconn.txt` | The engine's reliable-transport tunables in `TransportOptions`, and the 5551 vs 5552 physical/simulated port choice |
| `dis_reliabletransport.txt` | Frame construction, the receive path and sub-message dispatch that `ReliableTransport.cs` ports |
| `dis_reliableconn.txt` | The reliability state machine `ReliableConnection.cs` ports, including the resend and timeout rules |
| `dis_udptransport.txt` | The `COZ` prefix, the absence of a header CRC, and the 1420-byte maximum message size |
| `dis_cozmo_startup.txt` | The startup JSON keys the engine reads, in `../README.md` section 2 |
| `dis_mulaw.txt` | That Cozmo's companding is not G.711, which `Cozmo.Robot.AnkiMuLaw` implements |

## Regenerating

Everything here is reproducible from the binary, so it does not have to be trusted on sight:

```
python re-analysis/tools/regenerate_disassembly.py          # rewrite all but dis_mulaw.txt
python re-analysis/tools/regenerate_disassembly.py --check  # confirm they match the binary
python re-analysis/tools/extract_mulaw.py resources/lib/armeabi-v7a/libcozmoEngine.so \
    --out re-analysis/disassembly/dis_mulaw.txt
```

Both scripts resolve symbols by their demangled names against the library's own symbol table and fail if a
symbol is missing or at an unexpected address, so pointing them at a different build reports that rather
than producing plausible nonsense. `resources/` is not in the repository; unpack the APK locally first.

## If you are adding a file here

Add it to the manifest with the claim it supports. A disassembly with no cited claim does not belong in a
public repository.
