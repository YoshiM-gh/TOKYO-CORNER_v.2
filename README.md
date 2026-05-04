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
