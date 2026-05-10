# TOKYO-CORNER_v.2

## Development Settings (Unity)

Set the following in Unity before team development:

- `Project Settings > Editor > Version Control`: `Visible Meta Files`
- `Project Settings > Editor > Asset Serialization`: `Force Text`

These settings reduce merge conflicts and OS-specific diff noise.

## Focus Looks Stale After `git pull`

If Focus (Pomodoro) appears to keep old values after updating from Git, the cause is often local save data, not repository state.

This project stores save data at:

- `Application.persistentDataPath/savedata.json`

On macOS, an example path is:

- `~/Library/Application Support/DefaultCompany/TOKYO-CORNER_v_2/savedata.json`

Note: folder name can differ from repo name punctuation (for example `v.2` vs `v_2`).

### Reset Local Save Data (macOS)

1. Close Unity.
2. Back up current save file:
   - `cp "~/Library/Application Support/DefaultCompany/TOKYO-CORNER_v_2/savedata.json" "~/Library/Application Support/DefaultCompany/TOKYO-CORNER_v_2/savedata.backup.json"`
3. Remove save file:
   - `rm "~/Library/Application Support/DefaultCompany/TOKYO-CORNER_v_2/savedata.json"`
4. Reopen Unity and check the `Cafe` scene.

### Reset Local Save Data (Windows)

1. Close Unity.
2. Open:
   - `%USERPROFILE%\AppData\LocalLow\DefaultCompany\TOKYO-CORNER_v_2\`
3. Back up `savedata.json`, then delete it.
4. Reopen Unity and check the `Cafe` scene.

## Expected Default Focus Values

When save data is reset, `TimerController` defaults should be:

- `focusMinutes`: `25`
- `breakMinutes`: `5`
- `pomodoroRounds`: `4`

## Cross-OS Commit Policy (Mac / Windows)

To keep commits reproducible between macOS and Windows, always commit only meaningful project changes (not OS-specific noise).

### Commit / Push Checklist

1. Pull latest before work:
   - `git checkout main`
   - `git pull`
2. Open Unity with project settings kept as:
   - `Version Control: Visible Meta Files`
   - `Asset Serialization: Force Text`
3. Before commit, confirm there is no accidental diff:
   - `git status --short --branch`
   - `git diff`
4. If all changes are intended, commit and push.
5. On the other OS, `git pull` and confirm:
   - `git status --short --branch`
   - no unexpected modifications

### If Unexpected OS-Only Diffs Appear

- Check line endings first (`.gitattributes` in this repo already enforces LF for Unity text assets).
- Confirm Unity editor settings above are still correct.
- If behavior differs but Git has no diffs, check local save data (`savedata.json`) and retry.

This policy is used as the standard workflow for both Mac and Windows contributors.

## Focus UI Implementation Rules (Modern UI First)

To avoid over-engineering, Focus UI changes must follow these rules:

1. Build Focus UI primarily with `Assets/Modern UI Pack/Prefabs/*` components.
2. Do not introduce custom UI frameworks or large runtime UI builders unless explicitly approved.
3. Keep custom scripts to a minimum:
   - `TimerController`: timer logic only
   - UI binder script: connect existing timer state/actions to Modern UI components
4. Prefer Inspector wiring and prefab composition over hard-coded hierarchy creation.
5. Limit each implementation step to small, reviewable scope:
   - layout placement
   - action wiring (`Start`, `Pause/Resume`, `Next`)
   - visual polish
6. Reject scope creep by default. Any extra feature must be listed and approved before implementation.

This project prioritizes maintainable and reusable UI over one-off custom visuals.
