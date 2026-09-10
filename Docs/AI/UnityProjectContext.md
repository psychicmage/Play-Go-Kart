# Unity Project Context

<!-- unity-onboarding:generated:start -->

## Project Summary

- Project root: `C:/Users/hwang/Play Go Kart`
- Last analyzed: 2026-08-19
- Last analyzed commit: Unknown; the working tree contains extensive pre-existing user/import changes and Git LFS status can require local permissions.

## Confirmed Environment

- Unity version: 6000.5.4f1
- Render pipeline: Universal Render Pipeline 17.5.0
- Input system: Unity Input System 1.19.0 (`activeInputHandler: 1`)
- Primary target: Standalone Windows x86_64

## Important Packages And Frameworks

| Area | Finding | Confidence | Evidence |
| --- | --- | --- | --- |
| Rendering | URP 17.5.0 | Confirmed | `Packages/manifest.json` |
| Input | Input System 1.19.0 using `Player/Move` for WASD/gamepad | Confirmed | `Packages/manifest.json`, `Assets/InputSystem_Actions.inputactions` |
| UI | uGUI 2.5.0; title and settings screens use Canvas-based UI | Confirmed | package manifest and `MainMenu.unity` |
| Testing | Unity Test Framework 1.7.0; no first-party test assemblies found | Confirmed | package manifest and asset inventory |
| Editor automation | CoplayDev Unity MCP package and local relay are installed | Confirmed | package manifest and `Logs/relay.txt` |

## Directory Structure

| Path | Purpose | Confidence | Evidence |
| --- | --- | --- | --- |
| `Assets/Scenes` | `MainMenu` startup scene and `InGame` race scene | Confirmed | build settings |
| `Assets/Scripts` | First-party vehicle, camera, menu, and settings code | Confirmed | source inventory |
| `Assets/SteamPunk_Kart` | Imported kart model and `SpeedPunk` player prefab | Confirmed | prefab and scene references |
| `Assets/Apex Studio/Toon Racer Starter Track Kit` | Imported track meshes and prefabs | Confirmed | `InGame.unity` and `Track04.prefab` |
| `Assets/Audio` | Main mixer with Master/BGM/SFX groups | Confirmed | `MainAudioMixer.mixer` |

## Assembly Boundaries

- No first-party `.asmdef` or `.asmref` files were found. Runtime scripts compile into `Assembly-CSharp`.

## Scenes And Startup Flow

- Enabled build scenes: `Assets/Scenes/MainMenu.unity` (index 0), `Assets/Scenes/InGame.unity` (index 1).
- Startup scene: `MainMenu`.
- `MainMenuController.StartGame()` loads `InGame`; gameplay return flow should load `MainMenu`.

## Architecture

| Pattern | Finding | Confidence | Evidence |
| --- | --- | --- | --- |
| Runtime gameplay | Small composition-oriented MonoBehaviour project | Confirmed | `Assets/Scripts` |
| Vehicle control | `KartController` owns Rigidbody motion and reads `Player/Move` | Confirmed | `KartController.cs`, `SpeedPunk.prefab` |
| Camera | `ThirdPersonFollowCamera` follows the kart in `LateUpdate` | Confirmed | script and `InGame.unity` |
| UI/settings | Controllers with serialized uGUI references and persistent Button listeners | Confirmed | scripts and `MainMenu.unity` |

## Coding Conventions

- Namespace style: `PlayGoKart.Gameplay` and `PlayGoKart.UI`.
- Serialized fields: `[SerializeField] private`; public methods expose UI intent.
- Lifecycle: references cached in `Awake`; input callbacks balanced in `OnEnable`/`OnDisable`.
- Comments/docs: comments explain imported kart `+X` forward-axis and non-obvious physics intent.

## Testing And Validation

- EditMode tests: framework installed; no project tests found.
- PlayMode tests: framework installed; no project tests found.
- CI/build validation: no CI configuration found.
- Required feature validation: Unity compilation, serialized scene inspection, Play Mode race flow, and Windows fullscreen/pause checks.

## Available Unity Tooling

| Capability | Status | Evidence |
| --- | --- | --- |
| Unity Editor and local relay | available | running Editor process and `Logs/relay.txt` |
| Codex Unity MCP instance routing | temporarily unavailable | `mcpforunity://instances` reports zero instances |
| Repository and serialized asset inspection | available | local workspace |
| Console log inspection | available | local Unity Editor log |
| Tests/builds through MCP | unverified | no routed instance in current session |

## Important Constraints

- Preserve the imported kart model, track meshes, and current Rigidbody tuning.
- The kart faces local `+X`, not Unity's conventional local `+Z`.
- Reuse `Player/Move`; do not duplicate movement bindings.
- Preserve existing `MainMenu` and `SettingsManager` behavior.
- The working tree contains user-owned imported assets and project-setting changes; avoid unrelated edits.

## Unknowns And Confidence

- Exact checkpoint positions must be validated visually against scaled `Track04` in `InGame`.
- No existing lap, pause, or race-state system exists.
- Unity MCP relay is connected, but this Codex session currently has no registered Unity instance.

## Source Files Inspected

- `ProjectSettings/ProjectVersion.txt`, `ProjectSettings/ProjectSettings.asset`, `ProjectSettings/EditorBuildSettings.asset`
- `Packages/manifest.json`, `Packages/packages-lock.json`
- `Assets/InputSystem_Actions.inputactions`
- `Assets/Scenes/MainMenu.unity`, `Assets/Scenes/InGame.unity`
- `Assets/Scripts/*.cs`
- `Assets/SteamPunk_Kart/Prefab/SpeedPunk.prefab`
- `Assets/Apex Studio/Toon Racer Starter Track Kit/Prefabs/Tracks/Track04.prefab`

<!-- unity-onboarding:generated:end -->
