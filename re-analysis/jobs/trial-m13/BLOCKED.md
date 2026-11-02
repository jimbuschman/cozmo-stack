# POUNCE-REAL-FRAMES-1 — operator decision needed

Current branch keeps the shipped MeterFromDetections=true and remains WIP, NOT READY. No policy below has been applied. The source-built motion producer and positive consumer can be checked independently; this decision concerns the remaining default-profile quality input.

## Native fact and actual blocker

VisionSystem::Update creates one fresh rectangle vector (006B5154..5A), filled from actual selected marker, face and pet results (006B5182,006B521C,006B52B8). CheckImageQuality consumes it at006B5600. Shipped config enables face detection every frame and MeterFromDetections=true. Missing face/pet algorithms cannot establish that those results are empty. QualityRectangleInput currently has no production writer; silently providing an empty vector would invent source behavior.

When a frame has computed motion reports and unknown quality inputs, the strict implementation withholds publication and reports unavailable motion history. A later frame cannot generally restore clocks/actions/RNG knowledge. The constant-frame300s test and a mode3-only changed-pixel test therefore do not prove positive Pounce on the unmodified shipped profile. Existing records M11-016/M14 and M11-080 remain open; no duplicate record or status change is proposed here.

Native CheckImageQuality has an exact alternative input branch: MeterFromDetections=false bypasses the rectangle vector and performs full-image exposure metering (006B2D64..6E→006B2F08..F10). Supporting that already-defined branch does not change the default. Choosing it for the stack would deliberately override the shipped true value.

## Decision

Recommended bounded option: authorize a visible, reversible stack configuration override MeterFromDetections=false until the native rectangle producers are built. Keep the shipped asset unchanged; record the operator's decision and log the override. Use the actual native full-image metering algorithm and result, not a fabricated successful quality status. Face/pet detection and its API outputs remain explicitly unbuilt; this decision changes only exposure-metering input. Formal inventory/status/approval changes stay with Claude.

Alternative: retain the shipped profile and recover/build the selected native face/pet producers before claiming positive Pounce on the default camera path. This is a larger vision task, not a reason to call missing outputs empty.

Why the operator is required: AGENTS.md says “Apply no other departure without the operator” and “NEVER fill an unknown behavior with a plausible implementation and then treat it as recovered.” The manager cannot silently choose a different config value or exclude supported detector contributions. This file records that decision boundary; independent authorized checks continue.

Separate recorded gap: physical-worker rolling-shutter correction539258 affects marker corners/world state (006B4868..4968), although it does not alter motion pixels/carriers. The affected marker/world unit must remain visibly unavailable until built; no no-effect claim or silent uncorrected physical marker result is accepted. That is an implementation gap, not a request for a second policy.
