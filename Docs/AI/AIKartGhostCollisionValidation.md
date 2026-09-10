# AI Kart Ghost Collision — 2026-09-04

## Implementation

- Added project layer `AIKart` at layer 8.
- Assigned only the root GameObject of `GoKart Ai No.1`, `GoKart Ai No.2`, and `GoKart Ai No.3` to `AIKart`. Each kart's single physics `BoxCollider` is on that root.
- Disabled only `AIKart` ↔ `AIKart` in the 3D Physics Layer Collision Matrix.
- Kept `AIKart` ↔ `Default` enabled. The player, track mesh and barrier mesh remain on `Default`.
- No runtime scripts, Rigidbody settings, Collider settings, AI steering, sensor masks, audio or VFX were changed.
- Existing AI perception remains active because `AIKartController` uses all-layer (`~0`) SphereCast and OverlapSphere queries.

## Baseline

- All four karts were on `Default`; the matrix allowed `Default` ↔ `Default`.
- Each kart had one enabled, non-trigger BoxCollider and a dynamic 250-mass Rigidbody with collision detection enabled.
- Two overlapping AI karts, with driving and gravity disabled for isolation, separated from approximately 0 m to 9.03 m in 0.75 seconds. This directly reproduced the physics solver pushing the AI pair apart.

## Post-change validation

- Matrix after an Editor domain reload: `AIKart` ↔ `AIKart` ignored; `AIKart` ↔ `Default` enabled.
- AI ↔ AI crossing: two isolated AI karts started at x=-6 and x=6 with opposing 10 m/s velocities. After 1.3 seconds their positions had swapped to x=13.12 and x=-13.12. They passed completely through one another without a collision response.
- Player ↔ AI: an overlapped Player/AI pair physically separated by 1.02 m within 0.65 seconds, before the existing long-contact pass-through timeout. Collision remains enabled.
- AI ↔ barrier: an AI kart was launched at the existing `Barrier4` collider at 18 m/s. It remained 2.07 m on the track side of the sampled wall plane and did not pass through.
- Normal race at t=26.73 s: all three AI karts were on track, had passed 4–5 checkpoints, and `FrontVehicleDetected` / `IsSeparatingFromRacer` activated while their effective racing-line offsets remained distinct.
- At t=60.93 s: all AI karts were on lap 2 with 10–11 completed checkpoints and live ranks 1, 2 and 3.
- At t=149.61 s: all three AI karts completed 27 checkpoints and finished in positions 1, 2 and 3. Finished AI driving was disabled normally.
- Track height remained approximately y=5.95 throughout observation. Two AI completed without recovery; one used the existing recovery once and still finished third.
- Console after the runtime race: 0 errors and 0 warnings.
- Scene validation: 0 issues, 0 missing scripts and 0 broken prefabs.
- Unity EditMode test runner completed successfully, but the project contains 0 actual EditMode test cases; runtime evidence above is the meaningful regression coverage.

## Pre-existing diagnostics

Before the change, the Console contained Unity AI Assistant `NoSubscription` exceptions and one relay `TaskCanceledException` warning. These are tooling/connection messages unrelated to kart physics and did not recur during the final race observation.

## Follow-up: ghost traffic braking — 2026-09-04

### Confirmed cause

- The physics matrix was still working at runtime: `AIKart` ↔ `AIKart` was ignored and an isolated crossing test passed.
- `AIKartController` nevertheless treated a stopped AI ahead as a speed-matching obstacle. If both side checks were blocked, `IsOvertaking` became false and the follower's target speed was capped to `frontVehicleSpeed + 1`.
- Controlled reproduction: a follower started at 12 m/s behind a stopped AI. With both side probes blocked, its target speed became 1 m/s and its measured speed fell to about 0.88 m/s after 0.5 seconds. A line of followers could therefore form a stop chain even without physical collisions.

### Correction

- A detected racer is now classified as a ghost AI when both objects have `AIKartController` and their layers are configured not to collide.
- Ghost AI remains visible to forward detection, overtaking and racing-line separation.
- Ghost AI no longer causes speed matching and no longer blocks a side-lane clearance probe.
- Player racers and walls still require physical avoidance and can still limit or block an AI path.

### Follow-up evidence

- In the corresponding stopped-AI probe, the follower continued to detect the AI while overtaking was unavailable, but `FrontVehicleRequiresSpeedMatch` was false. In the same paused physics frame its target remained 14.88 m/s, full throttle stayed active and braking stayed zero, rather than the former 1 m/s target.
- Two AI karts crossed head-on from x=-6/x=6 to x=13.70/x=-13.70. Their post-crossing speeds were about 4.05 m/s. An equivalent no-contact control run ended at about 3.98 m/s, showing no collision-specific slowdown.
- Normal race at t≈34.22 s: all three AI were simultaneously detecting nearby AI racers with speed matching disabled; speeds remained 14.99–16.03 m/s and the existing separation/racing-line offsets were active.
- At t≈72.30 s, two AI were together on lap 2/checkpoint 15 at 15.33–15.58 m/s. A third AI stopped behind the intentionally stationary Player because Player↔AI physical collision remains enabled; this is deliberately preserved behavior.
- After moving the Player off the road for an AI-only completion check, all three AI completed lap 3/checkpoint 27 with finish ranks 1, 2 and 3.
- Final runtime Console: 0 errors and 0 warnings.
