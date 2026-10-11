# Q14-119: finite frame/PC encoding screen and JSON-reference boundary

Checkpoint746; queue5 scope guard reread before every step. Raw ELF bytes: `20261010-sound-stop-offset-frame-encoding-screen.json`, SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. Primary windows `20261010-sound-stop-offset-pc-addresses-native.txt`, `20261010-sound-stop-offset-sp-it-native.txt`, `20261010-sound-stop-offset-json-reference-native.txt`; PLT/relocation witness745.

The remaining197 SI741 entries were redecoded from raw ELF candidate-address bytes.102 are SP-relative arithmetic/address encodings;92 are SP-relative stores (73 STRD,19 single S-register VSTR);3 are PC-relative addresses. The JSON retains every address, bytes and disposition.195 isolated decodes match the navigation text exactly;50746A/623B6C lose the preceding IT condition in an isolated decode, so the raw operand match is retained with original conditional text. Actual507468 and623B6A ITT EQ windows confirm those epilogue adjustments are conditional, not unconditional. Ignore the first misaligned instruction of50745A capture; it is not evidence.

This is an operand screen, **not** proof that every candidate is a reachable instruction boundary, or that consumers of escaped frame pointers are closed. Literal/mid-instruction navigation does not become executed code by matching its bytes. These encodings do not identify a PBI receiver. Do not descend into unrelated frame calculation callees.

Reopened surrounding instruction streams confirm5209AA ADDW R8,PC,1F8 ->520BA4;6415BC ADDW R8,PC,1F8 ->6417B8;802A64 ADDW FP,PC,1F8 ->802C60 (Thumb PC+4). These architectural-PC addresses are not an admitted PBI+offset. No assertion about pointed-to data contents or unrelated consumer is needed.

For745's context-product lead, actual69F7E2 MOV R0,R2,69F7E0 inline key,69F7E4 BLX4A5F2C consume the incoming configuration reference.69F7E8..7EC converts its result to a local string;69F7F8 passes SP as that string. This bounded use matches the primary TextToSpeechProvider(context,const Json::Value&) ELF relocation. It does not settle context1C's whole producer or text-to-speech decisions. Stop at that JSON-input boundary; it supplies no positive PBI1F8 writer.

All291 SI741 entries now have explicit bounded navigation dispositions:29 ARM in741/742;65 non-SP/PC Thumb in743–745 plus JSON input boundary here;197 SP/PC encoding screens here. These are navigation dispositions, not291 obligations or an exhaustive writer proof. Original119 stays PARTIAL for complete relative/indirect/aliased PBI1F8 writers and actual admitted music-operation inputs. Return to another original positive frontier instead of expanding unrelated families.

out of scope: unrelated frame arithmetic, configuration backing and component internals; stop at receiver/input boundary. No per-sample DSP waiver for control arithmetic. Counts107 native+8 scope CHECKED/25 PARTIAL,140 AUDIBLE/0 INTERNAL unchanged. Research only.
