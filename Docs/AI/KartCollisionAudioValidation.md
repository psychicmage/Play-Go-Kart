# Player collision sound validation

Date: 2026-09-03
Unity: 6000.5.4f1, Windows Editor
Scene: Assets/Scenes/InGame.unity

## Scope and correction

- Before the fix, KartCollisionAudio.cs existed but no racer in the saved InGame scene had the component. The original code also allowed AI-owned playback and played the entire 1.097-second clip.
- The component is now attached only to the RaceManager's player reference (GoKart), with Car Crash Sound.mp3 and MainAudioMixer/SFX assigned.
- Runtime player-ownership filtering excludes AI-only impacts even if the component is copied to an AI later.
- Each qualifying new player/AI or player/wall collision restarts one voice, limited to 0.35 seconds with a final 0.05-second fade. A 0.2-second cooldown filters collision jitter; impacts below 0.5 m/s and ground contacts are ignored.
- The stop/fade uses the DSP clock: a long preceding frame must not prematurely consume the playback duration, and the existing AudioListener.pause flow freezes playback timing.
- Leaderboard rows no longer display lap counts. The separate main LAP HUD remains unchanged.

## Executed checks

Play Mode used the actual scene player and AI Rigidbody/Collider components. Driving controllers were temporarily disabled and physics was stepped manually to isolate the contact scenarios; no direct calls to the collision callback or playback method were used.

| Scenario | Observed result |
| --- | --- |
| Player driven into actual track Barrier4 at 8 m/s | 1 playback; playing immediately; nonzero source audio output peak 0.31107 |
| Wall playback duration | Approximately 0.352 seconds wall-clock; stopped |
| Player driven into AI No.1 at 10 m/s | 1 playback; nonzero source audio output; approximately 0.360 seconds; stopped |
| Player driven into AI No.2 and No.3 in subsequent contacts | 1 new playback for each contact |
| Sustained player/AI contact, 100 additional physics steps | 0 additional playbacks |
| AI No.1 driven into AI No.2 | Moving AI stopped at collision; 0 player playbacks; no AI AudioSource components |
| Player landed on temporary flat floor | Player stopped on floor; 0 additional playbacks |
| Actual RaceManager Pause during AI No.3 impact | AudioListener paused; DSP clock advance 0.0000 during pause |
| Actual RaceManager Continue | Playback resumed and stopped after approximately 0.365 seconds |
| Runtime leaderboard | Rank/name rows, player marker, no LAP text |
| Saved scene after leaving Play Mode | Exactly 1 collision-audio component, owned by GoKart; clip/SFX group/duration references intact |
| Final Console | 0 errors, 0 warnings |
| InGame scene validation | 0 missing scripts, 0 broken prefabs |

An initial timing test exposed premature stopping when Time.deltaTime included a long preceding editor frame. The production code was changed to DSP timing and the wall/player-AI duration checks above were re-run afterward.

## Cleanup and limitations

- All forced vehicle positions, disabled controls and the temporary floor existed only in Play Mode and were reverted on exit.
- Physics simulation was restored to FixedUpdate. No driving, track, collision-pass-through, mixer settings or PlayerPrefs were changed.
- Validation checked real physics callbacks, AudioSource playback, output sample data and stopping; subjective listening on the user's speakers and a standalone player build were not performed.

## Manual regression procedure

1. Open MainMenu, Play, select GAME START and wait for the countdown.
2. Drive into each AI: one short crash sound per separate impact, with no doubled voice.
3. Hit the track barrier, then remain against it: one short sound rather than continuous replay.
4. Separate and hit again: another short sound should play.
5. Observe AI-only collisions and normal road contact: no crash sound from these events.
6. Adjust SFX/Master in Settings and verify the impact sound follows those levels.
7. Confirm the leaderboard shows ranks/names without lap counts, while the main LAP HUD still works.

## Impact clarity tuning (2026-09-03 follow-up)

- Local player impact feedback now uses spatialBlend 0, removing camera-distance attenuation.
- Peak source volume is 0.95, the minimum impact-volume scale is 0.7, and full volume is reached at 8 m/s. The original 0.35-second duration and SFX/Master routing remain.
- KartEngineAudio owns temporary impact ducking: 35% engine volume for 0.18 seconds, followed by a smooth 0.25-second return to normal. Engine clip switching respects the same multiplier. Mixer settings and PlayerPrefs are not changed.
- Actual Barrier4 contact with the engine running: one playback, engine source volume 0.9 -> 0.315, impact source volume 0.87475, nonzero audio output peak 0.6523, impact stopped at approximately 0.363 seconds. After stopping, idle audio restored to its normal 0.8 source volume.
- Actual player/AI No.1 contact: one playback, impact source volume 0.95, engine multiplier 0.35, no AI crash component.
- Actual Pause/Continue: DSP clock held still and engine multiplier remained 0.35 during pause; multiplier returned to 1 after Continue and the crash source stopped.
- Repeated calls to the engine's duck API restarted the envelope at 0.35 without multiplying reductions. Finishing during recovery kept both engine sources at volume 0 and stopped; the recovery did not unmute them.
- These are runtime signal/state checks, not subjective speaker listening or standalone build validation. A transient Unity MCP WebSocket reconnect warning occurred during tooling; no game-code compile/runtime errors were observed.
