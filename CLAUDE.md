# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

UgaBugaBandrycha ("Побег от Бандрыхи") is a first-person survival horror prototype in Unity. There are no assembly definitions or tests; scripts compile into `Assembly-CSharp`. Everything lives under `Assets/`:

- `Scenes/SampleScene.unity` is the only scene (also the only entry in Build Settings). It holds the `BACKKKROOMMS` map, the `Bandrycha@Strut Walking` enemy, `Player`, `NavMeshBaker`, the `HUD` and `GameOverUI` canvases, and an `EventSystem` using `InputSystemUIInputModule`. The Directional Light and the camera imported with the map FBX are disabled.
- **World scale is metres.** The map root was scaled by 1/8 from its FBX import (ceiling about 2.7 m). Bandrycha is exactly 1.48 m tall (canon, scale ≈0.229; agent height 1.48, radius 0.28, eyeHeight 1.38). Distances in scripts (vision 15 m, catch 1.5 m) assume this scale.
- The map's floor planes are much larger than the walled area, and the north outer wall has a gap at x 9.5–12.3. `MapBoundary` holds four invisible BoxColliders on the outer perimeter (x -12.5 / 39.02, z -56.75 / 16.85) that keep both the player and the NavMesh inside.
- `walk.controller` has `Idle` (the walk clip at speed 0, so she stands instead of T-posing) and `Walk` states, switched on the `Speed` float (> 0.1). `Walk` playback speed is driven by the `AnimSpeed` parameter.

### Gameplay scripts (`Assets/Scripts/`)

- `RuntimeNavMeshBaker`: there is no AI Navigation package, so the NavMesh is **built at runtime** in `Awake` (execution order -1000) from the colliders under the map root plus `extraGeometry` (`MapBoundary`). The bake volume is clipped to `bakeArea` on XZ (the inside of the outer walls) and about 2.3 m above the floor, so nothing outside the Backrooms, on wall tops, or on the roof is walkable. It bakes with its own `agentRadius`/`agentHeight` (0.28 / 1.48), not the project's Humanoid agent settings. It exposes the static `IsReady` and `WalkBounds`.
- `BandrychaAI`: its `NavMeshAgent` is saved **disabled** in the scene. The script waits for `RuntimeNavMeshBaker.IsReady`, then snaps the agent onto the NavMesh and enables it. States: Patrol (random reachable points), Chase (raycast line of sight within 15 m), Caught (jumpscare: a red blinking light at her face, `PlayerController.Caught`, and `GameOverScreen.Show` after 1.5 s).
- `PlayerController`: CharacterController movement with stamina sprint and an F-key flashlight. It reads `Keyboard.current`/`Mouse.current` directly rather than the actions asset.
- `GameOverScreen`: wires the restart button in code (`onClick.AddListener`) and reloads the active scene by name.
- `InputSystem_Actions.inputactions` is Unity's default action asset. Its `Player` map has Move, Look, Attack, Interact, Crouch, Jump, Previous, Next, and Sprint; its `UI` map has the standard UI actions. It is registered as the project-wide actions asset in `EditorBuildSettings`.

## Engine and configuration

- **Unity version:** 6000.6.9f1 (Unity 6). Open the project with this exact editor version.
- **Render pipeline:** Built-in, because no SRP asset is set in `GraphicsSettings`. Use Built-in shaders and materials, not URP or HDRP ones.
- **Input:** `activeInputHandler: 1` means only the new Input System is active. The legacy `UnityEngine.Input` API will throw at runtime, so use `UnityEngine.InputSystem` and the actions asset.
- **Serialization:** Assets are serialized as Force Text (`m_SerializationMode: 2`), so scenes, prefabs, and controllers are YAML and can be read or grepped directly.
- **Atmosphere:** There is no sun or skybox. Fog is ExponentialSquared (density 0.09), and ambient light and reflections are near zero. The flashlight and the map's emissive ceiling lights are the only light sources.
- **Notable packages** (`Packages/manifest.json`): Input System, Timeline, uGUI, Visual Scripting, and the Unity AI Assistant (`com.unity.ai.assistant`, a prerelease).

## Working in this repo

- There is no build, lint, or test tooling outside the Unity Editor. Compile errors and test runs come from the Editor (or Unity batchmode).
- MCP `Unity_RunCommand` wraps code in the namespace `Unity.AI.Assistant...`, so `Image` resolves to the `Unity.AI.Image` namespace. Use aliases with different names (`using UIImage = UnityEngine.UI.Image;`). Project script types are reachable via `System.Type.GetType("Name, Assembly-CSharp")`.
- When the Unity MCP server is connected, use it to check results inside the Editor: `Unity_GetConsoleLogs` shows compile and runtime errors, and `Unity_RunCommand` runs editor C#. Use `Unity_Camera_Capture` or the `Unity_SceneView_*` tools to inspect the scene visually.
- Every asset has a matching `.meta` file holding its GUID, and scenes reference assets by that GUID. When moving or renaming assets outside the Editor, move the `.meta` file with the asset and never regenerate it. Otherwise scene references (such as the prefab instances in `SampleScene`) will break.
- Do not edit `Library/`, `Temp/`, `Logs/`, or `UserSettings/`. Unity generates them locally.
- Git repo on `main`, pushed to the public GitHub repo `gwayakamaha-cmyk/UgaBugaBandrycha`. `.gitignore` excludes Unity-generated folders (`Library/`, `Temp/`, `Logs/`, `UserSettings/`) and IDE files. Commits use the GitHub noreply email (set in the repo-local git config), so the real email never ends up in public history.
