# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

TOKYO-CORNER is a 3D Unity café simulator (URP, Unity 6.x) that blends free-roam exploration with seated Pomodoro timer mechanics. There is one scene: `Assets/Scenes/Cafe.unity`.

## Unity Editor Setup (Required Before Development)

- `Project Settings > Editor > Version Control`: `Visible Meta Files`
- `Project Settings > Editor > Asset Serialization`: `Force Text`

These must be set to avoid merge conflicts and OS-specific diff noise.

## Build & Run

This is a standard Unity project with no custom build scripts. Open in Unity Hub and run from the Editor. There are no CLI build commands defined.

**Save data location (reset if Focus values look stale after `git pull`):**
- macOS: `~/Library/Application Support/DefaultCompany/TOKYO-CORNER_v_2/savedata.json`
- Windows: `%USERPROFILE%\AppData\LocalLow\DefaultCompany\TOKYO-CORNER_v_2\savedata.json`

Default timer values after reset: `focusMinutes=25`, `breakMinutes=5`, `pomodoroRounds=4`.

## Architecture

### Two Game Modes

`GameModeManager` (singleton) orchestrates the switch between:
- **Roaming** – player moves freely, `HudStatsUI` visible, camera follows player
- **Focus** – triggered by sitting at a seat (E key), camera locks, Pomodoro UI shown, movement disabled

Mode entry/exit is controlled by `SeatInteractable` calling into `GameModeManager`.

### Save Data (`SaveDataManager.cs`)

Singleton. Persists as JSON at `Application.persistentDataPath/savedata.json`. Tracks:
- Coins, stamps, stamp cards
- Cumulative and daily playtime split by mode (roaming vs focus)
- Daily login bonus (100 coins, UTC-keyed)
- Pomodoro defaults (saved on settings change)

Auto-saves on `OnApplicationPause`/`OnApplicationQuit`. Contains legacy ID migration logic — do not remove.

### Timer System (`TimerController.cs`)

Pomodoro engine with a phase queue (work → short break → work → … → long break). Event-driven via `OnTimerChanged`. Tracks real-time UTC for accuracy across app suspensions. Two modes: Pomodoro and Stopwatch.

### UI Architecture

All Focus UI is built with **Modern UI Pack** prefabs (`Assets/Modern UI Pack/Prefabs/`). Do not introduce custom UI frameworks. Key rule: prefer Inspector wiring over runtime hierarchy creation.

- `FocusTimerPanelUI.cs` – binds `TimerController` state to Modern UI Pack components; contains settings panel (sliders) and runtime panel (progress bars, phase label)
- `HudStatsUI.cs` – free-mode overlay (coins, stamps, playtime); hidden during Focus
- `DrinkHudUI.cs` / `DrinkDiscardUI.cs` – Focus-mode drink inventory display
- `StampCardUI.cs` – renders stamp progress as `[*][*]…` format

### Interaction Model

- **E key** – sit at nearest `SeatInteractable`; starts Focus mode
- **ESC** – stand up; exits Focus mode
- **F key** – consume one sip from `DrinkInventory`
- **Click** – `PurchaseInteractable` (buy drink, 100 coins), `TrashCanInteractable` (discard drink)

`DrinkInventory` is `DontDestroyOnLoad`, session-scoped, max 4 drinks × 4 sips. FIFO consumption.

## Focus UI Implementation Rules

1. Build with `Assets/Modern UI Pack/Prefabs/*` components only.
2. `TimerController` holds timer logic only — no UI code.
3. UI binder scripts connect timer state/actions to Modern UI components via Inspector references.
4. Reject scope creep by default. Extra features must be listed and approved before implementation.

## Cross-OS Commit Policy

1. `git pull` before starting work.
2. Before committing: `git status --short --branch` and `git diff` to confirm no OS-only noise.
3. `.gitattributes` enforces LF for Unity text assets — do not override.
4. After `git pull` on the other OS, verify no unexpected modifications appear.

## Console Log Tags

Scripts use prefixed log tags for filtering: `[Timer]`, `[Seat]`, `[Drink]`, `[Purchase]`, `[SaveData]`, `[Focus]`.

## Key Packages

- `com.unity.render-pipelines.universal` 17.3.0 (URP)
- `com.unity.inputsystem` 1.18.0 (New Input System)
- Modern UI Pack (external asset, not in manifest — lives in `Assets/Modern UI Pack/`)
- TextMesh Pro (built-in UPM)
