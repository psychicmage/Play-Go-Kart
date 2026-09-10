# Circuit scenery — 2026-09-03

Scene: `Assets/Scenes/InGame.unity`.

## Additions

All scene additions are grouped under `CircuitScenery`:

- Landscape: supplied grass mesh expanded beneath the existing track; two thin concourse/apron surfaces.
- StartFinish_Grandstands: five aligned stands facing the starting straight, with checkered flags.
- Paddock: three garages, race office, three hospitality/service tents and two banner towers.
- Trackside_Signage: seven inward-facing billboards and four venue light-post models. No additional realtime lights.
- Woodland_Outer / Woodland_Infield: 145 seeded, spaced trees in irregular clusters with varied sizes and three foliage colors.
- 172 model props plus three landscape surfaces total.
- Five new materials in `Assets/Background/SceneryMaterials`; imported materials/models were not modified.
- Placement recipe is retained in `Docs/AI/CircuitSceneryPlacement.cs.txt` for reference; it is not a Unity runtime script and refuses to duplicate an existing scenery root.

## Preservation and validation

- Captured serialized state of all 1,487 pre-existing scene components before adding scenery, then compared them after placement: **zero changed/missing components**.
- No existing track, car, starting grid, camera, UI, race/AI script or imported prefab was modified.
- Sampled each prop's world-space bounds on a 5 × 5 grid against the two original track colliders: **4,300 rays, zero props overlapping road/barrier geometry**. This is a sampled placement check, not an exhaustive triangle-intersection proof.
- New scenery has **zero enabled colliders** and does not affect AI obstacle sensing, kart grounding, collision sounds or respawn physics. Lawn is visual scenery, not a new drivable surface.
- Ground surface is y=5.70, below the existing road at about y=5.94; prop bases are aligned with the lawn/aprons.
- Top-down, venue overview, near-driver and runtime Game View images inspected. Materials rendered normally.
- Play Mode smoke check: at t≈17.42 seconds the three AI karts had moved away from the grid, remained at y=5.95, and reported approximately 15.34–16.24 m/s. Player was intentionally left stationary. This was a short startup/AI smoke test, not a full manual race test.
- Console after test: **zero errors**. One pre-existing MCP websocket connection warning was seen before Play Mode.
- Restored the project's run-in-background setting and stopped Play Mode after the check.

## Captures

- `Captures/Scenery_Before_Top.png`
- `Captures/Scenery_After_Top.png`
- `Captures/Scenery_After_Venue.png`
- `Captures/Scenery_Driver_Start.png`
- `Captures/Scenery_Runtime_Start.png`

Decoration can be hidden as a group using the `CircuitScenery` root without changing the original scene elements.
