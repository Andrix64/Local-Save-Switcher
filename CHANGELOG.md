# Changelog

All notable changes, newest first. Versions follow
[Semantic Versioning](https://semver.org/).

## 1.0.0 — 2026-10-25

First stable release of **LSS — Local Save Switcher**: multiple playthroughs
for your PC games, on Windows and Linux. Today it works with Steam games that
keep their saves in Steam's `userdata` folder. Everything in
1.0.0-beta.1 (below), plus:

- LSS opens even when no game can be managed yet, for example on a Steam
  Deck where every game saves inside Proton: *Other games* opens by itself
  and says why each game isn't supported.
- Clearer message when no game keeps its saves in `userdata`.

Known limits:

- The Windows exe is **not code-signed**. SmartScreen may warn the first time
  (*More info → Run anyway*); with Smart App Control on, Windows may refuse to
  start it. See the README.
- Games that save outside `userdata` (in Documents, in the game folder, inside
  Proton) and other launchers are not supported yet.

## 1.0.0-beta.1 — 2026-10-24

First beta of **LSS — Local Save Switcher**, the new name of Control Save
Manager. Multiple playthroughs for Steam games, on Windows and Linux.

### Games

- Every Steam game with saves in `userdata` can be used, not only Control.
- The games list shows the games that LSS can manage first; games that save
  elsewhere are under *Other games*, with the reason.
- Files kept the same for each game (in Settings), for example the options
  file of Control.

### Linux

- Linux x64 packages: `.tar.gz`, `.deb`, `.rpm` and AppImage.
- Steam installed natively, as Flatpak or as Snap, and Steam Deck in desktop
  mode.
- The running game is recognised through Steam's `registry.vdf` and the
  running processes.

### Interface

- New interface, the same features as 0.9.0.
- New icon.

### Data

- Control Save Manager is now **LSS**: the exe is `LSS.exe` and the Windows
  zip is `LSS-<version>-win-x64.zip`.
- Data moves from `%LOCALAPPDATA%\ControlSaveManager` to `%LOCALAPPDATA%\LSS`
  on first start, and the app says so. On Linux the data is in
  `~/.local/share/LSS`.
- Export format 2: a playthrough also records the game it belongs to.
  Playthroughs exported by Control Save Manager can still be imported.

## 0.9.0 — 2026-10-23

First public release.

### Playthroughs

- Several Control playthroughs on the same account, one at a time in the game.
- **Play this**: saves the progress of the current playthrough, loads the
  chosen one and starts Control through Steam. It can also just load, with
  **Undo** right after.
- Note and play time for each playthrough; play time is measured
  automatically while the app is open.
- Progress is saved automatically when the game closes (can be turned off).
- Option to keep the same game options in all playthroughs.
- New playthrough (empty or a copy of the current saves), rename, export and
  import as a zip file.
- A deleted playthrough can be recovered from “Deleted playthroughs”.
- On first start, the playthrough already in the game is saved under a name of
  your choice.
- If the PC has more than one Steam account with Control, you choose which
  one to use at startup.
- Playthrough names with spaces and accented letters. Folders in `Profili`
  with an invalid name are reported at startup.

### History

- Saved points (for example before a boss) and automatic copies made before
  every operation, per playthrough, with **Go back here**.
- The last 20 automatic copies are kept (configurable).

### Save safety

- Every copy is verified byte by byte.
- Two-step replacement: the new saves are prepared and verified in a separate
  folder before the swap; if anything fails, the copy is restored.
- If an operation is interrupted (for example by a power cut), the folders go
  back to their previous state on the next start.
- Operations are blocked while Control is running; only one instance of the
  app at a time.
- Data in `%LOCALAPPDATA%\ControlSaveManager`, which OneDrive doesn't sync.
  Data from test versions in `Documents\ControlSaves` is moved automatically on
  first start, without ever losing anything.
- Operation log in `log.txt`.

### Steam Cloud

- The app reads whether Steam Cloud is on for Control and shows a bar with
  instructions to turn it off. With the cloud off it no longer warns.

### Interface

- One card per playthrough with its actions visible, bars for app-wide states
  (game running, Steam Cloud) and confirmations with verbs ("Delete
  playthrough", "Cancel") only for what can't be undone. Designed following
  the Nielsen Norman Group heuristics and Microsoft's Windows guidelines.
- Dark theme, message boxes included.
- English and Italian: language and date format follow Windows.
- Single exe for 64-bit Windows, no installation.
