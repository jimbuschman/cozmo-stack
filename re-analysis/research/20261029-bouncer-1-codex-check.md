# BOUNCER-1 independent source check — 2026-10-29

Request: `re-analysis/jobs/trial-m13/BOUNCER-1.md`; checked `20261029-bouncer-1-read.md`. Requested default model/effort; backend unobserved. This is a bounded independent check of material claims, not a duplicate whole extraction or implementation verification. No .NET, hardware, official records, or source code changed.

## Verdict: PASS for the checked source contract, with explicit frontiers

No contradiction or required source correction found in the checked claims. This does not settle a record, certify shared paths, or authorize a finished-behaviour claim. The manager's priority fix precedes source freeze and builder release.

Independent primary instruction checks against `resources/lib/armeabi-v7a/libcozmoEngine.so` (Thumb Capstone; `known.py` before substantive lookup):

- 0x005F133C..0x005F139C: feature ordinal 8 must equal 1 before face enumeration; false enumeration argument, nonempty check, best-face true, SmartFaceID assignment and validity. Shipped `config/features.json` says Bouncer false; `bouncer.json` supplies class, ID and display key only.
- 0x005F14CE..0x005F15B2: unsigned display dimensions converted to float, zero scores, step 10, W/2 and H/-4 initialization, three ordered RNG calls, sign comparison against 0.5, negative vertical velocity, pending clear and transition to 1.
- 0x005F15C4..0x005F167C: face +0xDC consumed via Radians conversion, bounded reversed mapping, strict step comparison and clamp. Its live producer remains unproved here.
- 0x005F1690..0x005F1790: center-based wall reflection, bottom scoring after X reflection, strict distance <16 for player, else opponent and pending flag, float clamp/store. No scoring cutoff is asserted.
- 0x005F19A4..0x005F1AC2: running duration comparison precedes face checks; valid target, nonnull face and unsigned age <5001 precede state logic, failure returns 2. 0x005F1ADE..0x005F1B96 confirms entry triggers 8/10/14/16, gameplay paddle then ball then conditional pending sound then display, and dormant exit trigger cases 15/11/9. The checked own writers/callback destinations do not justify inventing a state-6 transition.
- 0x005F17D8..0x005F1852: active action suppresses display; otherwise image construction and paddle, ball, score order precede SetFaceAction duration 33 and StartActing. Raster pixels and shared action scheduling are not certified by these call sites.

The read's further primary claims, including complete drawing adapters, trigger 521 identification, callback details, SetFeatureToggle dispatch/persistence, face ranking and all shared recipient tails, have not all been independently retraced in this bounded check. They remain subject to the cited instructions and named shared frontiers; no inference from this verdict closes them. In particular OpenCV raster/font, live face producer (+0xDC), host toggle dispatch/persistence, action/face-stream lifecycle and manager report delivery remain visible gaps, not assumed equivalent or silently disabled. The reader recorded its EnumToString known.py sequencing miss.

## Current manifest ownership (re-read after import, HEAD 248ea721)

- M7-019, **M7 concrete reaction-behaviour state machines**, IMPLEMENTATION_GAP: evidence is the reaction-class rows in M7 Appendices E/F plus named reaction addresses and the approved 2026-10-29 Stop correction; its unresolved text retains broad unbuilt behaviour. The Bouncer outline is not a complete constructor/helper/drawing/action lifecycle claim, and this work cannot settle the broad record.
- M7-018, **Shipped behaviour configuration and BehaviorClass factory binding**, IMPLEMENTATION_GAP: evidence is the 178-config and 79-class tables, factory 0x0059C888, and Wait constructor; unresolved retains broad factory and construction gaps. One Bouncer binding does not settle it.
- M7-014, **Reaction-lock manager lifetime and concrete per-class lock tables**, IMPLEMENTATION_GAP: evidence names SmartDisableReactionsWithLock 0x005BCE3C, manager lock/remove 0x005A27E8/0x005A3A48 and 13 tables; unresolved expressly includes Bouncer among remaining classes. One caller does not settle it.
- M7-021, **Live reaction robot fields and the WaitForLambda/state-name helpers**, IMPLEMENTATION_GAP: evidence includes state-name setter 0x005C0CA8; unresolved retains Robot Update SDK/Viz consumers and remaining callers. Bouncer's state name must not be treated as log-only.
- M8-001, **The IBehavior lifecycle: constructor fields, Init, Update, Stop, StopActing, Resume, IsRunnable and IsRunnableScored**, IMPLEMENTATION_GAP, and M8-012, **BehaviorManager::Update's tick order, behaviour switching, finishing and the reaction gate**, IMPLEMENTATION_GAP: their extensive lifecycle/manager evidence and unresolved whole paths are not discharged by Bouncer's calls.
- M15-007, **Activity feature gate and dead boredomMultiplier scoring data**, EXACT_SOURCE: evidence names IActivityStrategy::WantsToStart 0x005B529C, CozmoFeatureGate::IsFeatureEnabled 0x006A679C, features.json and Pounce constructor; unresolved null. This record's title and evidence do not establish the SetFeatureToggle dispatch/persistence path or Bouncer lifecycle; no contradiction to its present claim established.
- M14-009, **Face return eligibility and enrollment forwarding**, IMPLEMENTATION_GAP: evidence names 0x004F55B8..0x004F55E2 eligibility and enrollment forwarding; Bouncer's call does not settle live face inputs.

A separately scoped whole Bouncer lifecycle record is a reasonable proposal for manager/operator review, covering the own behaviour and naming the unresolved shared paths. No new record, status change, approval or exclusion is made here. M7 inventory rows 944–947 are compatible with the checked instructions; they are an outline, not a basis to assert the entire path complete.

Commands: explicit Python313 `known.py` for Bouncer and checked routine addresses; Capstone instruction reads of the bounded addresses above; PowerShell current manifest and shipped assets. No regression or compiled replay attempted.

## Supplemental critical own-path check

At manager request, independently checked additional cited instructions after `known.py` lookups; this supplements the bounded PASS and does not expand it to shared raster, font, streaming, persistence or face producers.

- Miss trigger: 0x00C6FBBC contains little-endian `09 02 00 00` = 521. Native EnumToString 0x0075E298 indexes the table; entry 521 at 0x01032EB4 points to 0x00C191D3, `SoundOnlyLiftEffortPlaceLow`. 0x005F1B30..0x005F1B48 clears pending before starting it with the current state address. 0x005F179C..0x005F17BA has the matching helper ordering.
- 0x005F1234..0x005F1290 captures next state by value and `this`, constructs the trigger action and passes its callback to StartActing. Callback 0x005F1D02..0x005F1D0A passes captured state and `this` to the transition veneer. 0x005F11C0..0x005F120E checks equality, calls the state-name helper before writing +0x11C. State branches and callback destinations checked in the original report provide no own entry to 6; this is an own-path observation, not a claim that arbitrary memory or external unsupported writes are impossible.
- 0x005F0F40..0x005F1018 clamps X±16 and Y/Y+4, passes left/top and differences; 0x005F1028..0x005F1092 clamps center and supplies radius 3; 0x005F10B4..0x005F113A converts both scores, supplies (4,12), then (W-4-8*length,12), scale 0.5 and false. Draw order and action duration 33 were independently checked at 0x005F1802..0x005F1852. These establish parameters, not rasterized pixels.
- SetFaceAction image constructor copies the image data and stores duration at +0x258 (0x005638BE..0x005639A8). Its Init sets live (0x00563D96..9C), clears the named image collection, adds the image and duration, then adds the keyframe (0x00563E1E..52); 0x00563F2C accepts 33, and 0x00563F6E..A0 constructs/stores the child. The shared manager, image encoding and wire lifecycle remain open.

No correction required by these checks. No build or .NET run.
