# VR Fire Safety - Project Handoff Summary

Last reviewed: 2026-06-12

This file summarizes the Unity project so a new conversation can quickly understand the current codebase and continue work.

## Project Overview

`VR-Fire-Safety` is a Unity VR fire-safety training/simulation project. The current gameplay appears to focus on a gas leak/fire emergency scenario where the player must identify hazards, close the gas valve, ventilate the room, extinguish fires, and avoid gas exposure before time runs out.

Primary project code lives under:

- `Assets/_Project/Scripts`
- `Assets/_Project/Prefabs`
- `Assets/_Project/Scenes`
- `Assets/_Project/Models`
- `Assets/_Project/Materials`
- `Assets/_Project/Textures`

The project also includes third-party/vendor assets:

- Meta XR / Oculus packages and samples
- Photon Fusion
- QuickOutline
- WireBuilder
- TextMesh Pro
- Unity XR/OpenXR setup

## Unity And Packages

Unity version:

- `6000.1.14f1`

Important packages in `Packages/manifest.json`:

- `com.meta.xr.sdk.all` `81.0.0`
- `com.meta.xr.sdk.movement` from `https://github.com/oculus-samples/Unity-Movement.git`
- `com.unity.xr.openxr` `1.15.1`
- `com.unity.xr.management` `4.5.3`
- `com.unity.inputsystem` `1.14.0`
- `com.unity.render-pipelines.universal` `17.1.0`
- `com.unity.ai.navigation` `2.0.9`
- `com.unity.netcode.gameobjects` `1.15.0`
- `com.unity.services.multiplayer` `1.2.1`
- `com.unity.multiplayer.center` `1.0.0`
- Photon Fusion is present as assets under `Assets/Photon/Fusion`

## Build Scenes

Enabled build scenes in `ProjectSettings/EditorBuildSettings.asset`:

1. `Assets/_Project/Scenes/StartScene.unity`
2. `Assets/_Project/Scenes/MainScene.unity`
3. `Assets/_Project/Scenes/GameOverScene.unity`

Disabled scenes found in build settings:

- `Assets/Scenes/SampleScene.unity`
- `Assets/_Project/Scenes/Hien Test.unity`
- `Assets/_Project/Scenes/TestMulitplayerScene.unity`

Additional scenes present:

- `Assets/_Project/Scenes/FactoryScene.unity`
- `Assets/_Project/Scenes/Hien_Test.unity`
- `Assets/_Project/Scenes/TestMulitplayerScene.unity`

## Main Gameplay Flow

The main loop is managed by `GameFlowManager`.

Player goal:

- Stop active gas leaks.
- Reduce gas level to safe level.
- Extinguish or resolve all active flame nodes.
- Finish before the match timer reaches zero.
- Avoid fainting from gas exposure.

Win condition:

- `GasSystem.gas01 <= gasSafeThreshold01` or `GasSystem.GasLevel() == 0`
- If `requireLeakStopped` is enabled, `GasSystem.LeakActive` must be false.
- All `FlameNode.All` entries must not be burning and must have `Burn01 <= 0.02`.

Lose conditions:

- Timer reaches zero before win condition is met.
- `PlayerGasExposure` reaches full faint progress and reports fainting.

On match end:

- `GameOverPayload` stores final result data.
- End panel can be shown.
- Configured behaviours are disabled.
- End VO audio plays through `AudioManager`.
- Scene transition goes to build index `2`, currently `GameOverScene`.

## Core Systems

### Gas System

Main file:

- `Assets/_Project/Scripts/GasSystem/GasSystem.cs`

Responsibilities:

- Tracks room gas concentration as `gas01` from `0` to `1`.
- Converts gas concentration into levels:
  - Level 0: safe
  - Level 1: light gas smell
  - Level 2: strong gas smell
  - Level 3: dangerous
- Combines gas leak sources:
  - `hoseLeak`
  - gas stove knobs from `GasStoveKnobLeakByAngle`
- Combines ventilation sources from `GasVentByAngle`.
- Uses main gas valve openness (`mainValveOpen01`) to decide whether leak is active.
- Raises gameplay events when leak starts/stops.
- Supports Photon Fusion authority:
  - Host/state authority simulates gas.
  - Clients receive `Gas01Net`.
  - Single-player fallback works before Fusion spawn.

Important tunables:

- `secondsToFullAtMaxLeak`
- `secondsToClearWithFullVent`
- `secondsToClearNaturally`
- `level1Threshold`, `level2Threshold`, `level3Threshold`

### Gas Valve

Main file:

- `Assets/_Project/Scripts/GasCylinder/GasValveLeakByAngle.cs`

Responsibilities:

- Reads a valve handle local axis angle.
- Maps closed/open angles to `valveOpen01`.
- Calls `GasSystem.SetMainValveOpen01`.
- Raises `ValveClosed` and `ValveOpened` events.
- Syncs valve openness through Photon Fusion.
- Avoids visual snapback on clients using `ignoreNetworkVisualAfterLocalEdit`.

Default angle logic:

- Closed angle: `-45`
- Fully open angle: `135`
- Closed dead zone: `5` degrees

### Fire System

Main files:

- `Assets/_Project/Scripts/Managers/FireManager.cs`
- `Assets/_Project/Scripts/Effects/FlameNode.cs`
- `Assets/_Project/Scripts/Effects/FlameExtinguishable.cs`

`FlameNode` responsibilities:

- Represents one fire node.
- Tracks burning state, health, visual intensity, particle systems, lights, and extinguish collider.
- Can auto-find particle/light children.
- Can auto-find neighbor flame nodes by distance.
- Supports fire spread with delays.
- Has cooldown to prevent immediate re-ignite after extinguish.
- Maintains static list `FlameNode.All` used by managers.

`FireManager` responsibilities:

- Registers/auto-finds all `FlameNode` objects.
- Validates duplicate flame IDs.
- Owns network-authoritative ignite, spread, extinguish, and sync operations.
- Uses RPCs for client requests.
- Has single-player fallback when Fusion is not spawned.
- Raises `FireIgnited` and `FireExtinguished` events.

`FlameExtinguishable` appears to bridge particle/collision hits from extinguisher smoke into `FireManager.RequestExtinguish`.

### Fire Extinguisher

Main files:

- `Assets/_Project/Scripts/Fire Extinguisher/FireExtinguisherSmokeUse.cs`
- `Assets/_Project/Scripts/Fire Extinguisher/NozzleFireSmokeTrigger.cs`
- `Assets/_Project/Scripts/Fire Extinguisher/SafetyPinDetachOnPull.cs`
- `Assets/_Project/Scripts/Fire Extinguisher/NozzleFrostBySmoke.cs`
- `Assets/_Project/Scripts/Fire Extinguisher/ReleaseWhenOverstretched.cs`

`FireExtinguisherSmokeUse`:

- Implements Meta XR `IHandGrabUseDelegate`.
- Animates trigger lever by use strength.
- Requires safety pin removal if configured.
- Starts smoke after `delayBeforeSpray`.
- Limits spray duration with `maxSpraySeconds`.
- Uses Photon Fusion network state for spraying, remaining time, source player, and `canSpray`.
- Enables particle collision only for the local sprayer by default, so only the user spraying damages fire.
- Plays loop audio named `FESpray` through `AudioManager`.

`SafetyPinDetachOnPull`:

- Detects when pin is pulled far enough from socket.
- Removes/detaches pin.
- Can manage outlines and enable extinguisher use.

`NozzleFrostBySmoke`:

- Changes nozzle material through frost stages based on accumulated spray time.

### Player Gas Exposure

Main file:

- `Assets/_Project/Scripts/Player/PlayerGasExposure.cs`

Responsibilities:

- Trigger collider checks if player is inside a `GasSystem` zone.
- Only local/input-authority player runs exposure logic by default.
- At dangerous gas levels, faint progress increases:
  - Level 2: `secondsToFaintAtLevel2`
  - Level 3: `secondsToFaintAtLevel3`
- Outside danger, progress recovers over `recoverySecondsFromFull`.
- When progress reaches `1`, invokes `onFainted` and reports to `GameFlowManager`.

### Scoring And Events

Main files:

- `Assets/_Project/Scripts/Event/GameplayEventBus.cs`
- `Assets/_Project/Scripts/Event/GameplayEventType.cs`
- `Assets/_Project/Scripts/Score/ScoreManager.cs`
- `Assets/_Project/Scripts/Score/ScoreRule.cs`
- `Assets/_Project/Scripts/Score/FinalScoreText.cs`

Event types:

- `ValveClosed`
- `ValveOpened`
- `WindowOpened`
- `WindowClosed`
- `FireIgnited`
- `FireExtinguished`
- `GasLeakStarted`
- `GasLeakStopped`
- `GasLevelChanged`
- `PlayerEnteredDangerZone`
- `PlayerExitedDangerZone`
- `PlayerFainted`
- `MatchStarted`
- `MatchEnded`
- `WrongActionPerformed`

Default scoring rules in `ScoreManager.Reset()`:

- `ValveClosed`: `+15`
- `WindowOpened`: `+10`
- `FireExtinguished`: `+20`
- `PlayerEnteredDangerZone`: `-5`
- `WrongActionPerformed`: `-10`

`ScoreManager.LastScore` is used by `FinalScoreText` on the game-over scene.

### Audio

Main files:

- `Assets/_Project/Scripts/Audio/AudioManager.cs`
- `Assets/_Project/Scripts/Audio/FlameSfxController.cs`

`AudioManager`:

- Singleton.
- Optional `DontDestroyOnLoad`.
- Stores named sounds in a dictionary.
- Supports play/stop/pause/unpause/one-shot/volume/pitch.

Known sound keys referenced by scripts:

- `FlameLoop`
- `FESpray`
- `GasLeakLoop`
- `GasBurst`
- `VO_StartGame`
- `VO_GameWin`
- `VO_TimeUp`
- `VO_GameOver`
- `VO_GasLevel1`
- `VO_GasLevel2`
- `VO_GasLevel3`
- `VO_RightAction`

`FlameSfxController`:

- Monitors all `FlameNode.All`.
- Plays/stops `FlameLoop` depending on active or visible fire.
- Can raise `FireExtinguished` when all fire disappears.

## Interactions And Props

### Ceiling Fan And Light

Files:

- `Assets/_Project/Scripts/CeilingFan/FanSwitch.cs`
- `Assets/_Project/Scripts/CeilingFan/OneGrabKnobTransformer.cs`
- `Assets/_Project/Scripts/CeilingFan/FanRotator.cs`
- `Assets/_Project/Scripts/CeilingLight/LightSwitch.cs`
- `Assets/_Project/Scripts/CeilingLight/SparkIgnitionTrigger.cs`

Notes:

- `FanSwitch` reads knob rotation in 6 steps.
- The current fan speed application is commented out, but turning from off to on can trigger `SparkIgnitionTrigger`.
- `SparkIgnitionTrigger` plays spark FX/audio and ignites a `FlameNode` if gas level meets the configured required level.
- `LightSwitch` toggles light components/objects and button visual angle.

### Exhaust Fan

Files:

- `Assets/_Project/Scripts/Exhaust Fan/FanButton.cs`
- `Assets/_Project/Scripts/Exhaust Fan/FanRotator.cs`

Notes:

- Button toggles fan visual state.
- Can trigger sparks and ignite configured flame nodes, optionally only once.

### Doors, Windows, And VO Checks

Files:

- `Assets/_Project/Scripts/FrontDoor/DoorLeftOpenByHandle.cs`
- `Assets/_Project/Scripts/FrontDoor/DoorRightOpen.cs`
- `Assets/_Project/Scripts/FrontDoor/HandleController.cs`
- `Assets/_Project/Scripts/FrontDoor/LatchController.cs`
- `Assets/_Project/Scripts/FrontDoor/DoorOpenGasCheckVO.cs`
- `Assets/_Project/Scripts/FrontDoor/GasValveClosedWindowCheckVO.cs`

Notes:

- Left door can be opened through handle interaction and can raise `WindowOpened`.
- Right door has dependency checks based on left/right door open thresholds.
- VO helper scripts play right-action feedback when opening/closing conditions are met.

### Lighter

Files:

- `Assets/_Project/Scripts/Lighter/LighterIgnite.cs`
- `Assets/_Project/Scripts/Lighter/LighterIgniteOnGrab.cs`

Notes:

- Supports held/toggled lighter flame.
- Can turn fire effect on after grab delay.

### Gas Cylinder / Hose Sequence

Files:

- `Assets/_Project/Scripts/GasCylinder/HoseBurnSequence.cs`
- `Assets/_Project/Scripts/GasCylinder/HoseLeakRandomPoint.cs`
- `Assets/_Project/Scripts/GasCylinder/GasCylinderFlameShutdown.cs`
- `Assets/_Project/Scripts/Test/HoseAnchorRetarget.cs`
- `Assets/_Project/Scripts/Test/HoseRenderer.cs`

Notes:

- Hose leak point can be randomized along hose segments.
- Hose burn sequence can hide hose root, trigger leak state, manage hose leak audio, and chain flame nodes.
- Gas cylinder flame shutdown checks valve closure, gas leak state, nearby fire, and can extinguish/reignite as needed.
- Test scripts help retarget hose joints and draw simple hose line.

## UI And Scene Flow

Files:

- `Assets/_Project/Scripts/UI/FadeScreen.cs`
- `Assets/_Project/Scripts/UI/SceneTransitionManager.cs`
- `Assets/_Project/Scripts/UI/StartGameController.cs`
- `Assets/_Project/Scripts/UI/StartMenuLayoutManager.cs`
- `Assets/_Project/Scripts/UI/ShowGasLevelCheckbox.cs`
- `Assets/_Project/Scripts/UI/HandRayLineVisual.cs`
- `Assets/_Project/Scripts/UI/QuitButton.cs`
- `Assets/_Project/Scripts/HUB/GasHUDVR.cs`

Notes:

- Start menu can switch between start/settings/about layouts.
- Start button hides start menu, shows loading layout, then transitions to scene index `1`.
- `GameSettings.ShowGasLevel` is stored in `PlayerPrefs` and controls whether gas HUD is visible.
- `GasHUDVR` shows gas level text and can play gas-level VO.
- `SceneTransitionManager` handles async scene loading with optional fade out.

## Environment

Files:

- `Assets/_Project/Scripts/Environment/RandomDayNight.cs`
- `Assets/_Project/Scripts/Environment/DayNightPayload.cs`

Notes:

- Random/day/night skybox and lighting selection.
- `DayNightPayload` stores a static day/night selection across scenes if needed.

## Networking

Photon Fusion is used directly in several scripts:

- `GasSystem : NetworkBehaviour`
- `GasValveLeakByAngle : NetworkBehaviour`
- `FireManager : NetworkBehaviour`
- `GameFlowManager : NetworkBehaviour`
- `FireExtinguisherSmokeUse : NetworkBehaviour`
- `NozzleFireSmokeTrigger : NetworkBehaviour`
- `PlayerGasExposure` reads `NetworkObject` for input authority

Common pattern:

- Before Fusion spawn: scripts support single-player/editor fallback.
- After Fusion spawn:
  - State authority/host owns simulation.
  - Clients send RPC requests.
  - State is synced through `[Networked]` fields and RPCs.

## Important Prefabs And Assets

Project prefabs include:

- Player/camera rig:
  - `Assets/_Project/Prefabs/Camera - Player/[BuildingBlock] Camera Rig.prefab`
  - `Assets/_Project/Prefabs/Camera - Player/[BuildingBlock] Camera Rig Variant.prefab`
- Managers:
  - `Assets/_Project/Prefabs/Manager/TransitionManger.prefab`
- Effects:
  - `Assets/_Project/Prefabs/Effects/Flame_Node.prefab`
  - `Assets/_Project/Prefabs/Effects/Gas System Root.prefab`
  - `Assets/_Project/Prefabs/Effects/FireSmoke_Particle System.prefab`
  - `Assets/_Project/Prefabs/Effects/FireNode_GasValve Variant.prefab`
- Props:
  - `FireExtinguisher`
  - `FireExtinguisher_CO2_New`
  - `FireExtinguisher_CO2_Root`
  - `GasUnit`
  - `Gas_Cylinder`
  - `Lighter`
  - `FrontDoor`
  - `KitchenWindow`
  - switches/outlets/fans/lights
- UI:
  - `CanvasStartMenu`
  - `GameOverCanvas`
  - Meta/Horizon-style UI template prefabs

## Current Git State At Review Time

There were existing uncommitted changes before this summary file was created:

Modified:

- `Assets/_Project/Materials/Environment/Factory/CardboardBox.mat`
- `Assets/_Project/Materials/Environment/Factory/CardboardBox.mat.meta`
- `Assets/_Project/Models/Enviroment/Factory/CardboardBox.fbx`
- `Assets/_Project/Models/Enviroment/Factory/CardboardBox.fbx.meta`
- `Assets/_Project/Scenes/FactoryScene.unity`
- `Assets/_Project/Scenes/StartScene.unity`
- `Assets/_Project/Textures/Environment/Factory/CardboardBox_Base.jpg`
- `Assets/_Project/Textures/Environment/Factory/CardboardBox_Base.jpg.meta`

Untracked:

- `Assets/_Project/Materials/Environment/Factory/Metal.mat`
- `Assets/_Project/Materials/Environment/Factory/Metal.mat.meta`
- `Assets/_Project/Models/Enviroment/Factory/FactoryBase.fbx`
- `Assets/_Project/Models/Enviroment/Factory/FactoryBase.fbx.meta`
- `Assets/_Project/Textures/Environment/Factory/Warehouse_normals.png`
- `Assets/_Project/Textures/Environment/Factory/Warehouse_normals.png.meta`

This summary file was added separately as:

- `PROJECT_HANDOFF_SUMMARY.md`

## Useful Entry Points For Future Work

For gas/fire gameplay changes:

- Start with `GasSystem.cs`, `GasValveLeakByAngle.cs`, `FireManager.cs`, and `FlameNode.cs`.

For win/lose/timer changes:

- Start with `GameFlowManager.cs`.

For extinguisher behavior:

- Start with `FireExtinguisherSmokeUse.cs`, `SafetyPinDetachOnPull.cs`, and `FlameExtinguishable.cs`.

For scoring:

- Start with `ScoreManager.cs`, `ScoreRule.cs`, and `GameplayEventType.cs`.

For start/game-over scene flow:

- Start with `StartGameController.cs`, `SceneTransitionManager.cs`, `GameOverPayload.cs`, and `GameOverMessageUI.cs`.

For multiplayer bugs:

- Check whether the object has Fusion state authority.
- Check if the script has a single-player fallback path before `Spawned()`.
- Check RPC source/target attributes.
- Check whether visual state is local-only or network-synced.