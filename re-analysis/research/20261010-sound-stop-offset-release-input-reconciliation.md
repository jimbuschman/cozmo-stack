# Q14-119: concrete timed-release and forced-stop input joins

Checkpoint756; queue5 scope guard reread. Primary engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1; `20261010-sound-stop-offset-release-input-native.txt`, `-release-caller-native.txt`; immediate navigation `-release-callers.py/.json`. This reconciles existing HN518/MAT/MUC/IST/EXMB contracts to119's named music-operation input boundary; it is not a global indirect-caller proof or Q15 completion.

| Actual call | Supplied operation and preserved order |
| --- | --- |
|A3EB40 ->A3E920 | MD's retained eventR4 is passed asR0. Actual status80 or90 with velocity17==0 reaches this release call. Release finishes before the caller updates/reloads dispatch flag bits and applies the separate bit0 gate to recorded-node dispatch. Preserve MD's time/active gates; no note-off release at prewarm. |
|A3E920 release body | IncomingeventR0 retainedR6; note-state=[event+8], PBI list=head2C, eachPBI=[link+8], eventtime=[event+18]. Publish eventtime toPBI38 or attachment9E805C before eventC.live10 query. Nonzero query markslink.kind3 and setsFFFFFFFF stop offset; zero-query kind3 sets raw eventtime, with arg2 selected by equality toFFFFFFFF; kind2 usesPBI.live1C. Thus nonzero query does not force all later zero-query kind3 offsets to sentinel. Callback next is reloaded. |
|A70AC0 ->A3EB4C | EXMB manager cleanup passes actual saved lifetime entry, after publishing new head/tail and clearing note-state.C, before entry releaseA3E3C0. Preserve source's head-identity trap and callback reentrancy order. |
|A716A0 ->A3EB4C | MUC9 actualA71634 retains managerR0/contextR1. First/next physical lifetime entries whose entryC equals supplied context are unlinked; note-state.C cleared; current entryR4 is supplied to stop then release. Other contexts retained. |
|A7171C/A71784 ->A3EB4C | MUC10 actualA716BC has two different inputs. Nonzeroarg1 flushes only entries whose note-state40 and5C are both zero. Zeroarg1 flushes all entries. Both paths unlink/head-tail update and clear note-state.C before passing current entry, then release it. Do not normalize this to one completion predicate. |
|A71A9C ->A3EB4C | MAT11 supplies current lifetime entry after both state counters40/5C are zero, list unlink and owner clear; stop precedes event release. |
|A71DA8 ->A3EB4C | IST3 supplies current selected-context lifetime entry after identity match/unlink/owner clear; stop precedes event release and other contexts survive. |
|Forced-stop body | A3EB4C reads supplied entry8 -> note-state2C; each linkedPBI8 receives1F8=FFFFFFFF atA3EB88 beforeA01280 with{0,4,byte0},arg2=1; link.next is reloaded after callback. No fresh target/container draw is inserted. |

All seven immediate navigation candidates are real ARM calls and now reconciled to existing positive input/order contracts. No new stop-offset local arithmetic or ordinary callback body is missing in these named paths. Alternate/computed callers and additional stop-offset writer aliases remain beyond this bounded result.119 remains PARTIAL for its original exhaustive writer/alias proof; this removes a vague unpaid claim against these already supplied music-operation paths, without asserting all musical inputs are covered.

out of scope: list/pool/backing mechanics and per-sample DSP. Counts107 native+8 scope CHECKED/25 PARTIAL;140 AUDIBLE/0 INTERNAL unchanged. No production/fidelity/hardware changes.
